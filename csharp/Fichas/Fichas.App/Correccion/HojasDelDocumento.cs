using Fichas.Contratos.Lectura;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Correccion;

/// <summary>Lo que vuelve de otro hilo al pedir una hoja: la imagen y cuántas hojas tiene el documento.</summary>
/// <param name="Imagen">La hoja rasterizada, o nula si no se pudo dibujar.</param>
/// <param name="Total">Cuántas hojas tiene el PDF; 0 si no se pudo abrir.</param>
public sealed record HojaTraida(ImagenDePagina? Imagen, int Total);

/// <summary>
/// La memoria corta del visor: las últimas hojas rasterizadas del documento que está
/// delante y cuántas hojas tiene, para no volver a pedírselo a PDFium ni a PdfPig.
/// </summary>
/// <remarks>
/// <para><b>Existe por el plan del 2026-09-15 (R-1, b y c).</b> Hasta entonces cada hoja que se
/// enseñaba —al abrir, con las flechas, al pedir «ver dónde se leyó»— se rasterizaba otra vez
/// (384 a 1 380 ms por hoja real, <c>DECISIONES.md</c> 2026-09-04) y volvía a abrir el PDF con
/// PdfPig para contar sus hojas. Ir a la hoja 2 y volver a la 1 costaba dos rasterizados de
/// la 1.</para>
///
/// <para><b>La regla:</b> se guardan las últimas <see cref="CuantasSeGuardan"/> hojas del
/// documento abierto —las más usadas, no las más nuevas: volver a una la hace reciente— y el
/// número de hojas se pregunta una vez por documento. <b>Todo se vacía al abrir otro caso</b>,
/// también si el papel es el mismo archivo: la memoria es del caso, y un caso tiene su
/// copia del escaneo desde el 2026-09-15.</para>
///
/// <para><b>Hilos.</b> La memoria corta se toca solo desde el hilo de la ventana
/// (<see cref="Abrir"/>, <see cref="YaRasterizada"/>, <see cref="Guardar"/>); lo único que corre
/// en otro hilo es <see cref="Traer"/>, que no la toca: recibe la ruta y el total ya sabido
/// como parámetros y devuelve lo traído, y es la pantalla quien lo guarda al volver, después de
/// comprobar el turno. Por eso <see cref="Guardar"/> pide la ruta: lo traído para un caso que
/// ya no está delante se tira.</para>
///
/// <para>Tres hojas son unos pocos MB de PNG por documento y cubren ir y volver entre dos
/// hojas, que es lo que el dueño hace con un formulario de dos caras. El tope de 1 700 px
/// del visor no se toca (regla de no regresión): viaja tal cual en <see cref="Traer"/>.</para>
/// </remarks>
public sealed class HojasDelDocumento
{
    /// <summary>Cuántas hojas se guardan por documento.</summary>
    public const int CuantasSeGuardan = 3;

    /// <summary>La lectura de PDF, que rasteriza y cuenta.</summary>
    private readonly ILecturaDePdf _lectura;

    /// <summary>El tope de píxeles en el lado largo con el que se rasteriza para el visor.</summary>
    private readonly int _anchoMaximo;

    /// <summary>Cuántas hojas caben.</summary>
    private readonly int _capacidad;

    /// <summary>Las hojas guardadas, de la menos reciente a la más reciente.</summary>
    private readonly List<ImagenDePagina> _recientes = [];

    /// <summary>Cuántas veces se rasterizó de verdad; se suma desde otro hilo.</summary>
    private int _rasterizadas;

    /// <summary>Cuántas veces se contaron las hojas de un documento; se suma desde otro hilo.</summary>
    private int _conteos;

    /// <summary>Monta la memoria corta, vacía y sin documento.</summary>
    /// <param name="lectura">La lectura de PDF con la que se rasteriza y se cuenta.</param>
    /// <param name="anchoMaximo">El tope de píxeles en el lado largo; el visor pasa 1 700.</param>
    /// <param name="capacidad">Cuántas hojas se guardan; <see cref="CuantasSeGuardan"/> salvo en una prueba.</param>
    public HojasDelDocumento(ILecturaDePdf lectura, int anchoMaximo, int capacidad = CuantasSeGuardan)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacidad, 1);
        _lectura = lectura;
        _anchoMaximo = anchoMaximo;
        _capacidad = capacidad;
    }

    /// <summary>La ruta del documento abierto; nula sin documento.</summary>
    public string? Ruta { get; private set; }

    /// <summary>Cuántas hojas tiene el documento abierto; nulo hasta que se contó.</summary>
    public int? TotalDeHojas { get; private set; }

    /// <summary>Cuántas hojas hay guardadas ahora.</summary>
    public int Guardadas => _recientes.Count;

    /// <summary>Cuántas hojas se rasterizaron de verdad desde que nació; para el cuaderno.</summary>
    public int Rasterizadas => Volatile.Read(ref _rasterizadas);

    /// <summary>Cuántas veces se contaron las hojas de un documento; para el cuaderno.</summary>
    public int Conteos => Volatile.Read(ref _conteos);

    /// <summary>Cuántas hojas se sirvieron de la memoria corta sin rasterizar; para el cuaderno.</summary>
    public int ServidasDeLaMemoria { get; private set; }

    /// <summary>Abre otro caso: se vacían las hojas y se olvida el total, aunque la ruta sea la misma.</summary>
    /// <param name="ruta">La ruta del escaneo del caso; nula o vacía deja la memoria sin documento.</param>
    public void Abrir(string? ruta)
    {
        Ruta = string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        TotalDeHojas = null;
        _recientes.Clear();
    }

    /// <summary>La hoja si ya está guardada, y entonces pasa a ser la más reciente; nula si hay que traerla.</summary>
    /// <param name="hoja">Qué hoja del PDF, base 1.</param>
    public ImagenDePagina? YaRasterizada(int hoja)
    {
        var donde = _recientes.FindIndex(imagen => imagen.Pagina == hoja);
        if (donde < 0) return null;

        var imagen = _recientes[donde];
        _recientes.RemoveAt(donde);
        _recientes.Add(imagen);
        ServidasDeLaMemoria++;
        return imagen;
    }

    /// <summary>
    /// Rasteriza la hoja y, solo si el total no se sabe todavía, cuenta las hojas. Corre en
    /// otro hilo y no toca la memoria corta.
    /// </summary>
    /// <param name="ruta">La ruta del escaneo, capturada en el hilo de la ventana antes de irse.</param>
    /// <param name="hoja">Qué hoja del PDF, base 1.</param>
    /// <param name="totalConocido">El total si ya se contó para este documento; nulo obliga a contar.</param>
    /// <returns>La imagen (o nula) y el total.</returns>
    public HojaTraida Traer(string ruta, int hoja, int? totalConocido)
    {
        var imagen = _lectura.RasterizarPagina(ruta, hoja, _anchoMaximo);
        Interlocked.Increment(ref _rasterizadas);

        var total = totalConocido ?? Contar(ruta);
        return new HojaTraida(imagen, total);
    }

    /// <summary>
    /// Guarda lo traído si sigue siendo del documento abierto: la imagen entra como la más
    /// reciente (y sale la menos reciente si ya no cabe) y el total queda sabido.
    /// </summary>
    /// <param name="ruta">La ruta con la que se trajo; si ya no es la abierta, se tira.</param>
    /// <param name="traida">Lo que devolvió <see cref="Traer"/>.</param>
    public void Guardar(string ruta, HojaTraida traida)
    {
        ArgumentNullException.ThrowIfNull(traida);
        if (Ruta is null || !string.Equals(Ruta, ruta, StringComparison.Ordinal)) return;

        TotalDeHojas = traida.Total;
        if (traida.Imagen is null) return;

        _recientes.RemoveAll(imagen => imagen.Pagina == traida.Imagen.Pagina);
        _recientes.Add(traida.Imagen);
        if (_recientes.Count > _capacidad) _recientes.RemoveAt(0);
    }

    /// <summary>Cuenta las hojas del documento y lo apunta.</summary>
    /// <param name="ruta">La ruta del escaneo.</param>
    private int Contar(string ruta)
    {
        Interlocked.Increment(ref _conteos);
        return _lectura.ContarPaginas(ruta);
    }
}
