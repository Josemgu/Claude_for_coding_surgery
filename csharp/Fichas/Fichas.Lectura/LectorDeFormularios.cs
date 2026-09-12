using System.Diagnostics;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Lectura;

/// <summary>Lo que sale de leer UNA hoja: lo propuesto, lo que hay que decir y cuanto costo.</summary>
/// <param name="RutaPdf">De que archivo salio.</param>
/// <param name="Pagina">Que hoja del archivo es, base 1.</param>
/// <param name="Campos">Los campos propuestos del caso y de sus personas, juntos.</param>
/// <param name="Avisos">Todo lo que hay que senalar en la franja.</param>
/// <param name="Ilegible">
/// El renglon de ilegible, o nulo si la hoja se pudo leer. ⛔ Nunca es un rechazo: la
/// hoja entra igual, con lo que se pudo sacar de ella.
/// </param>
/// <param name="CapturaManual">Si la hoja esta demasiado floja como para fiarse de ella.</param>
/// <param name="LineasLeidas">Cuantas lineas devolvio el OCR. Es diagnostico, no dato.</param>
/// <param name="TextoLeido">Lo que la maquina leyo en la hoja entera, tal cual.</param>
/// <param name="Segundos">Lo que tardo esta hoja de punta a punta.</param>
public sealed record HojaLeida(
    string RutaPdf,
    int Pagina,
    IReadOnlyList<CampoPropuesto> Campos,
    IReadOnlyList<Aviso> Avisos,
    RenglonIlegible? Ilegible,
    bool CapturaManual,
    int LineasLeidas,
    string? TextoLeido,
    double Segundos);

/// <summary>
/// Pone en orden los pasos de leer un PDF: rasterizar, anotaciones, OCR y extraccion.
/// </summary>
/// <remarks>
/// Solo coordina: cada paso vive en su clase y aqui se llaman en orden. No guarda nada en
/// la base y no dibuja nada en pantalla.
///
/// <para>Un PDF puede traer VARIOS formularios, uno por pagina —medido sobre un documento
/// real de seis—, asi que la unidad de trabajo es la HOJA y no el archivo. El nombre del
/// archivo no identifica un caso.</para>
///
/// <para>⛔ <b>Un documento que no se puede leer NUNCA se rechaza.</b> Se devuelve con su
/// renglon de ilegible y su motivo, y los campos que se hayan podido sacar van con el. Es
/// la regla que el dueno vio romperse como «se guardaron 0 casos».</para>
/// </remarks>
public sealed class LectorDeFormularios
{
    /// <summary>La capa de PDF: rasteriza, cuenta páginas, lee anotaciones y pasa el OCR.</summary>
    private readonly LecturaDePdf _lectura;

    /// <summary>El reloj del renglón de ilegible; inyectado para que las pruebas fijen la hora.</summary>
    private readonly Func<DateTimeOffset> _ahora;

    /// <summary>Crea el lector sobre una capa de PDF ya construida.</summary>
    /// <param name="lectura">Quien rasteriza, lee anotaciones y pasa el OCR.</param>
    /// <param name="ahora">De donde sale la hora del renglon de ilegible. Se inyecta para poder probarla.</param>
    public LectorDeFormularios(LecturaDePdf lectura, Func<DateTimeOffset>? ahora = null)
    {
        _lectura = lectura;
        _ahora = ahora ?? (() => DateTimeOffset.Now);
    }

    /// <summary>Todas las hojas del PDF, cada una como un formulario propio.</summary>
    /// <remarks>
    /// Un archivo que no se puede abrir devuelve UNA hoja con su renglon de ilegible y
    /// nada mas. No devuelve la lista vacia: una lista vacia se pierde en silencio, y eso
    /// es justo lo que no puede pasar.
    /// </remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco; no se comprueba que exista antes de intentar abrirlo.</param>
    /// <returns>Una <see cref="HojaLeida"/> por página, en orden; o una sola, ilegible y con página 0, si el archivo no se abrió.</returns>
    public IReadOnlyList<HojaLeida> LeerDocumento(string rutaPdf)
    {
        int paginas = _lectura.ContarPaginas(rutaPdf);
        if (paginas <= 0)
        {
            return [HojaIlegible(rutaPdf, pagina: null, "El archivo no se pudo abrir como PDF.", lineasLeidas: 0, segundos: 0.0)];
        }
        return Enumerable.Range(1, paginas).Select(pagina => LeerHoja(rutaPdf, pagina)).ToArray();
    }

    /// <summary>Lee una hoja de punta a punta.</summary>
    /// <remarks>
    /// El orden es rasterizar, OCR, anotaciones, tamaño de página y extracción. Tres
    /// salidas distintas y las tres llevan la hoja de vuelta: no se pudo rasterizar
    /// (ilegible, sin campos); el OCR no devolvió ni una línea (ilegible, pero con los
    /// campos que las anotaciones hayan dado); y la lectura normal, floja o no.
    /// </remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco.</param>
    /// <param name="pagina">Número de hoja, base 1.</param>
    /// <returns>La hoja con sus campos, avisos y tiempo; nunca nula, y una hoja mala no lanza.</returns>
    /// <exception cref="FileNotFoundException">Faltan los modelos de OCR: <see cref="LecturaDePdf.LeerConOcr"/> la deja subir a propósito para que no pase por una hoja en blanco.</exception>
    public HojaLeida LeerHoja(string rutaPdf, int pagina)
    {
        var crono = Stopwatch.StartNew();

        var imagen = _lectura.RasterizarPagina(rutaPdf, pagina, Geometria.LadoLargoMaximoPx);
        if (imagen is null)
        {
            return HojaIlegible(rutaPdf, pagina, "La hoja no se pudo convertir en imagen.", 0, crono.Elapsed.TotalSeconds);
        }

        var lineas = _lectura.LeerConOcr(imagen);
        var anotaciones = _lectura.LeerAnotaciones(rutaPdf, pagina);
        string? textoLeido = TextoDeLaPagina(lineas);

        var tamano = _lectura.TamanoDeLaPagina(rutaPdf, pagina);
        var extraccion = new Extraccion(
            tamano is null ? 612.0 / 792.0 : tamano.Value.AnchoPuntos / tamano.Value.AltoPuntos);

        var delCaso = extraccion.ProponerCamposDelCaso(lineas, anotaciones);
        var deLasPersonas = extraccion.ProponerCamposDePersonas(lineas, anotaciones);

        var campos = delCaso.Campos.Concat(deLasPersonas.Campos).ToArray();
        var avisos = delCaso.Avisos.Concat(deLasPersonas.Avisos).ToList();
        bool capturaManual = EsCapturaManual(campos);

        if (lineas.Count == 0)
        {
            // El OCR no devolvio NI UNA linea. Eso no es «un campo vacio»: es una hoja que
            // no se leyo, y hay que dejar constancia con su motivo.
            return new HojaLeida(
                rutaPdf, pagina, campos, avisos,
                Ilegible: Renglon(rutaPdf, pagina, "El OCR no leyó ni una línea en esta hoja.", 0),
                CapturaManual: true, LineasLeidas: 0, TextoLeido: null, Segundos: crono.Elapsed.TotalSeconds);
        }

        if (capturaManual)
        {
            avisos.Add(Aviso.Advierte(
                "Esta hoja se leyó floja: hay que capturarla mirando el escaneo.",
                detalle: "Más de la mitad de los campos vinieron sin valor o con poca confianza. "
                       + "Lo leído se conserva y se enseña; no se da por bueno."));
        }

        return new HojaLeida(
            rutaPdf, pagina, campos, avisos,
            Ilegible: null,
            CapturaManual: capturaManual,
            LineasLeidas: lineas.Count,
            TextoLeido: textoLeido,
            Segundos: crono.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Cierto cuando la hoja esta demasiado floja para fiarse de ella.
    /// </summary>
    /// <remarks>
    /// Cuenta como flojo tanto un campo que volvio con confianza baja como uno que no se
    /// pudo leer: los dos significan lo mismo para quien tiene que corregir.
    ///
    /// <para>⚠️ <b>Aqui NO se vacia nada</b>, y esto se aparta del Python a proposito. El
    /// `vaciar_el_formulario` de `extraccion/formulario.py` tira los campos de una pagina
    /// floja; el requisito 9 y el criterio C3-L5 dicen que lo leido se ENSENA. Asi que la
    /// hoja se marca para captura manual —que es el aviso— y los valores viajan igual, con
    /// su confianza baja delante. Quien decide es Miguel, no esto.</para>
    /// </remarks>
    /// <param name="campos">Todos los campos propuestos de la hoja; solo se miran número de caso, fecha de viaje, número de unidad, nombre y cédula.</param>
    /// <returns>Cierto si más del 60 % de los mirados vino flojo, o si no hay ninguno que mirar.</returns>
    private static bool EsCapturaManual(IReadOnlyList<CampoPropuesto> campos)
    {
        var mirados = campos
            .Where(c => c.Campo is Extraccion.CampoNumeroDeCaso or Extraccion.CampoFechaDeViaje
                                or Extraccion.CampoUnidadNumero or Extraccion.CampoNombreDePersona
                                or Extraccion.CampoCedula)
            .ToArray();
        if (mirados.Length == 0) return true;

        int flojos = mirados.Count(c =>
            c.Valor is null || c.Confianza is null || c.Confianza < Extraccion.ConfianzaQueSeConsideraBaja);

        return (double)flojos / mirados.Length > Extraccion.ProporcionDeCamposFlojosQueMarcaCapturaManual;
    }

    /// <summary>
    /// Lo que el OCR leyo en la hoja entera, junto y en el orden en que salio.
    /// </summary>
    /// <remarks>
    /// Es para diagnosticar, no para extraer: ningun campo se saca de aqui. Sin esto, «no
    /// se pudo leer el número de caso» no distingue tres averias distintas: que el OCR no
    /// devolviera ni una linea, que devolviera muchas y ninguna encaje, o que lo leyera y
    /// se descartara al guardar.
    /// </remarks>
    /// <param name="lineas">Las líneas del OCR de la hoja, en el orden en que salieron.</param>
    /// <returns>Los textos no vacíos unidos por un espacio, o nulo si no había ninguno.</returns>
    private static string? TextoDeLaPagina(IReadOnlyList<LineaDeOcr> lineas)
    {
        string junto = string.Join(" ", lineas
            .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
            .Select(l => l.Texto.Trim()));
        return junto.Length == 0 ? null : junto;
    }

    /// <summary>
    /// La hoja que se devuelve cuando no se pudo ni empezar a leer: sin campos, con un
    /// aviso de problema y su renglón de ilegible. Marcada para captura manual.
    /// </summary>
    /// <param name="rutaPdf">Ruta del archivo, que va también en el detalle del aviso.</param>
    /// <param name="pagina">La hoja, o nula si el archivo entero no se abrió; entonces viaja como página 0.</param>
    /// <param name="motivo">La frase que verá Miguel y que se guarda en el renglón.</param>
    /// <param name="lineasLeidas">Cuántas líneas dio el OCR antes de fallar; 0 si no llegó a correr.</param>
    /// <param name="segundos">Lo que se tardó hasta rendirse.</param>
    private HojaLeida HojaIlegible(string rutaPdf, int? pagina, string motivo, int lineasLeidas, double segundos)
        => new(
            RutaPdf: rutaPdf,
            Pagina: pagina ?? 0,
            Campos: [],
            Avisos: [Aviso.Problema(motivo, detalle: $"Archivo: {rutaPdf}")],
            Ilegible: Renglon(rutaPdf, pagina, motivo, lineasLeidas),
            CapturaManual: true,
            LineasLeidas: lineasLeidas,
            TextoLeido: null,
            Segundos: segundos);

    /// <summary>
    /// El renglón de ilegible que se guarda en la base, con la hora del reloj inyectado en
    /// formato ISO 8601 de ida y vuelta (<c>"O"</c>).
    /// </summary>
    /// <param name="rutaPdf">Ruta del archivo.</param>
    /// <param name="pagina">La hoja, o nula si el archivo entero no se abrió.</param>
    /// <param name="motivo">Por qué no se pudo leer, en la frase que verá Miguel.</param>
    /// <param name="lineasLeidas">Cuántas líneas dio el OCR; distingue «no leyó nada» de «leyó y no encajó».</param>
    private RenglonIlegible Renglon(string rutaPdf, int? pagina, string motivo, int lineasLeidas)
        => new()
        {
            RutaPdf = rutaPdf,
            PaginaPdf = pagina,
            Motivo = motivo,
            LineasLeidas = lineasLeidas,
            RegistradoEn = _ahora().ToString("O"),
        };
}
