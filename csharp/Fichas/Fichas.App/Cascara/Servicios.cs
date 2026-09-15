using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos;
using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;
using Fichas.Datos.Repositorios;
using Fichas.Datos.Rutas;
using Fichas.Lectura;
using Fichas.Reportes;
using Microsoft.Data.Sqlite;

namespace Fichas.App.Cascara;

/// <summary>
/// EL UNICO SITIO donde se dice que implementacion cumple cada contrato.
/// </summary>
/// <remarks>
/// <para>Por defecto se monta lo REAL: la base de <c>Documentos\Fichas</c>, el lector de
/// PDF con su OCR, el Excel de los companeros y los reportes en PDF. Las seis pantallas no
/// se enteran de nada: solo conocen <c>Fichas.Contratos</c>.</para>
///
/// <para><c>--falso N</c> monta datos inventados, y existe para poder medir una pantalla
/// con 3 000 documentos sin tener 3 000 documentos. Cuando se usa, <b>se dice en el pie de
/// la ventana</b>: un programa que ensena datos que no son y no lo dice es peor que uno
/// que no abre.</para>
/// </remarks>
public sealed class Servicios : IDisposable
{
    /// <summary>La conexión abierta a la base de verdad; nula con datos inventados, y eso es lo que mide <see cref="SonDatosInventados"/>.</summary>
    private readonly SqliteConnection? _conexion;

    /// <summary>La lectura de PDF de verdad, sin construir hasta que alguien importa; nula con datos inventados.</summary>
    private readonly Lazy<LecturaDePdf>? _lecturaDeVerdad;

    /// <summary>El lector de formularios de punta a punta, perezoso como la lectura de la que depende; nulo con datos inventados.</summary>
    private readonly Lazy<LectorDeFormularios>? _lector;

    /// <summary>Quien suelta el motor de OCR cuando lleva un rato parado; nulo con datos inventados.</summary>
    private readonly SueltaDelOcrEnReposo? _sueltaDelOcr;

    /// <summary>Cuánto tiene que llevar parado el motor de OCR para soltarlo: más que el hueco entre dos hojas de una tanda, menos que lo que Miguel tarda en corregir un caso.</summary>
    private static readonly TimeSpan ReposoDelOcr = TimeSpan.FromSeconds(30);

    /// <summary>Cada cuánto se mira si el motor lleva parado el reposo.</summary>
    private static readonly TimeSpan CadaCuantoSeMiraElOcr = TimeSpan.FromSeconds(10);

    /// <summary>Con la base de verdad detras: repositorios sobre SQLite, OCR perezoso, paquetes y reportes reales.</summary>
    /// <param name="argumentos">Lo que se pidió en la línea de órdenes.</param>
    /// <param name="registro">El cuaderno de tiempos ya abierto en la carpeta de datos.</param>
    /// <param name="conexion">La conexión ya preparada y migrada por <c>ArranqueDeLaBase</c>; esta clase la cierra en <see cref="Dispose"/>.</param>
    private Servicios(ArgumentosDeArranque argumentos, Registro registro, SqliteConnection conexion)
        : this(argumentos, registro)
    {
        _conexion = conexion;

        var casos = new RepositorioDeCasos(conexion);
        var personas = new RepositorioDePersonas(conexion);
        var companeros = new RepositorioDeCompaneros(conexion);
        var asignaciones = new RepositorioDeAsignaciones(conexion);
        var procedencia = new RepositorioDeProcedencia(conexion);
        var ilegibles = new RepositorioDeIlegibles(conexion);

        // El lector es PEREZOSO: construirlo carga los tres modelos de OCR y eso tarda
        // segundos medidos. Quien solo abre a mirar el calendario no tiene por que pagarlos.
        _lecturaDeVerdad = new Lazy<LecturaDePdf>(() => new LecturaDePdf());
        _lector = new Lazy<LectorDeFormularios>(() => new LectorDeFormularios(_lecturaDeVerdad.Value));

        // Y se suelta solo cuando lleva un rato parado (pase de memoria, 2026-09-15): ver
        // SueltaDelOcrEnReposo. Con la lectura sin construir no hay motor y no hay nada que soltar.
        _sueltaDelOcr = new SueltaDelOcrEnReposo(TiempoSinLeerDelOcr, SoltarElOcr, ReposoDelOcr);
        _sueltaDelOcr.Arrancar(CadaCuantoSeMiraElOcr);

        Casos = casos;
        Personas = personas;
        Companeros = companeros;
        Asignaciones = asignaciones;
        Procedencia = procedencia;
        Ilegibles = ilegibles;

        // Solo con la base de VERDAD detras. Con `--falso` se queda nulo a proposito: ver
        // el comentario de la propiedad.
        Mantenimiento = new Datos.Mantenimiento.RepositorioDeMantenimiento(conexion);

        LecturaDePdf = new LecturaPerezosa(_lecturaDeVerdad);
        Extraccion = new Extraccion();
        Paquetes = new Fichas.Paquetes.Paquetes(casos, personas, companeros, ilegibles, Reloj);
        var reportes = new ReportesEnPdf(casos, personas, companeros, asignaciones, procedencia, Reloj);
        Reportes = reportes;

        // El MISMO motor por otra puerta. Existe porque el metodo que hace falta —el reporte de
        // la segunda vuelta— tendria que estar en IReportes y ese archivo esta congelado; ver
        // ReporteDeLaSegundaVueltaEnPdf. No hay un segundo motor de PDF en ninguna parte.
        ReporteDeLaSegundaVuelta = new ReporteDeLaSegundaVueltaEnPdf(reportes);
    }

    /// <summary>Con datos inventados, que es lo que pide <c>--falso N</c>. Sin base, sin OCR, sin mantenimiento ni reporte de la segunda vuelta.</summary>
    /// <param name="argumentos">Lo que se pidió en la línea de órdenes.</param>
    /// <param name="registro">El cuaderno de tiempos.</param>
    /// <param name="falsos">Las implementaciones en memoria de <c>Fichas.Datos.Falso</c>, ya sembradas con N casos.</param>
    private Servicios(ArgumentosDeArranque argumentos, Registro registro, Datos.Falso.ServiciosFalsos falsos)
        : this(argumentos, registro)
    {
        Casos = falsos.Casos;
        Personas = falsos.Personas;
        Companeros = falsos.Companeros;
        Asignaciones = falsos.Asignaciones;
        Procedencia = falsos.Procedencia;
        Ilegibles = falsos.Ilegibles;
        LecturaDePdf = falsos.LecturaDePdf;
        Extraccion = falsos.Extraccion;
        Paquetes = falsos.Paquetes;
        Reportes = falsos.Reportes;
    }

    /// <summary>Lo comun a las dos formas de montarse: argumentos, cuaderno, buzón de avisos, reloj y actualizador.</summary>
    /// <remarks>
    /// El actualizador se monta siempre, también con <c>--falso</c>, porque montarlo no toca
    /// la red: solo sale a internet quien llama a <c>Buscar</c>, y con datos inventados el
    /// arranque no lo llama (<see cref="ArgumentosDeArranque.SeBuscaActualizacionAlArrancar"/>).
    /// </remarks>
    /// <param name="argumentos">Lo que se pidió en la línea de órdenes.</param>
    /// <param name="registro">El cuaderno de tiempos.</param>
    private Servicios(ArgumentosDeArranque argumentos, Registro registro)
    {
        Argumentos = argumentos;
        Registro = registro;
        Avisos = new BuzonDeAvisos();
        Reloj = new RelojDelSistema();
        Actualizador = new Actualizacion.Actualizador(
            new Actualizacion.ServidorDeReleasesDeGitHub(new HttpClientHandler()),
            argumentos.CarpetaDeDatos,
            VersionDelPrograma.Numero,
            new Actualizacion.LanzadorDelInstalador(registro.Anotar),
            registro.Anotar);
    }

    /// <summary>Monta los servicios que usara toda la app.</summary>
    /// <remarks>
    /// Si la base no se puede abrir, el programa NO se queda sin arrancar: se monta lo
    /// falso vacio, se deja el aviso en la franja y la ventana abre igual. Una ventana que
    /// no aparece no deja donde leer el motivo, y entonces el dueno tiene un icono que no
    /// hace nada.
    /// </remarks>
    /// <param name="argumentos">Lo leído de la línea de órdenes; decide entre la base de verdad y lo inventado.</param>
    /// <param name="registro">El cuaderno donde se anota la ruta de la base y, si falla, el motivo.</param>
    /// <returns>Los servicios montados; nunca nulo, aunque la base no haya abierto.</returns>
    public static Servicios Montar(ArgumentosDeArranque argumentos, Registro registro)
    {
        ArgumentNullException.ThrowIfNull(argumentos);
        ArgumentNullException.ThrowIfNull(registro);

        if (argumentos.SeUsanDatosInventados)
        {
            return new Servicios(
                argumentos, registro, new Datos.Falso.ServiciosFalsos(argumentos.CasosInventados!.Value));
        }

        try
        {
            // PrepararLaBase escribe la ruta ANTES de tocar el disco (DECISIONES.md,
            // 2026-09-02) y aplica las migraciones que falten: la base del dueno esta en la
            // 13 y el esquema va por la 15, asi que al abrirla aqui se migra sola.
            var conexion = ArranqueDeLaBase.PrepararLaBase(argumentos.CarpetaDeDatos, registro.Anotar);
            return new Servicios(argumentos, registro, conexion);
        }
        // ErrorDeMigracion entra en la lista desde el 2026-09-04: es lo que se lanza
        // cuando no hay sitio para la copia previa, o cuando la migracion fallo y la base
        // se repuso. Sin recogerlo aqui, el mensaje que dice DONDE quedo la copia moriria
        // con la ventana y el dueno se quedaria con un icono que no abre.
        catch (Exception fallo) when (
            fallo is SqliteException or IOException or UnauthorizedAccessException
                  or ErrorDeRuta or ErrorDeConexion or ErrorDeMigracion)
        {
            registro.Anotar($"NO SE PUDO ABRIR LA BASE  {fallo.GetType().Name}: {fallo.Message}");
            var deEmergencia = new Servicios(argumentos, registro, new Datos.Falso.ServiciosFalsos(0));
            deEmergencia.Avisos.Dejar(Aviso.Problema(
                "No se pudo abrir la base de datos: el programa abrió sin ella.",
                string.Empty,
                $"Carpeta: {argumentos.CarpetaDeDatos}. Motivo: {fallo.Message}. " +
                "NADA de lo que se vea en esta ventana son datos reales y nada de lo que se " +
                "haga se guardará. Cierre el programa y avise."));
            return deEmergencia;
        }
    }

    /// <summary>Los casos (un caso es un PDF leído); de verdad o inventados según el arranque.</summary>
    public ICasos Casos { get; } = null!;

    /// <summary>Las personas de cada caso, hasta seis por formulario.</summary>
    public IPersonas Personas { get; } = null!;

    /// <summary>Los companeros (agentes) a los que se reparten los documentos.</summary>
    public ICompaneros Companeros { get; } = null!;

    /// <summary>Las asignaciones; la unica puerta de asignar, desde cualquier pantalla.</summary>
    public IAsignaciones Asignaciones { get; } = null!;

    /// <summary>La procedencia de cada campo: de qué anotación, OCR o mano salió cada valor.</summary>
    public IProcedencia Procedencia { get; } = null!;

    /// <summary>Los documentos ilegibles y las filas descartadas.</summary>
    public IIlegibles Ilegibles { get; } = null!;

    /// <summary>La lectura de PDF por pasos (páginas, imagen, anotaciones, OCR); con la base de verdad es perezosa.</summary>
    public ILecturaDePdf LecturaDePdf { get; } = null!;

    /// <summary>La extraccion de campos a partir de lo que leyó el OCR y las anotaciones.</summary>
    public IExtraccion Extraccion { get; } = null!;

    /// <summary>Los paquetes de Excel que van a los companeros y vuelven llenos.</summary>
    public IPaquetes Paquetes { get; } = null!;

    /// <summary>Los reportes en PDF; con datos inventados es un motor falso que no escribe nada.</summary>
    public IReportes Reportes { get; } = null!;

    /// <summary>
    /// Quien escribe el reporte de la segunda vuelta, o NULO si se arranco con datos inventados.
    /// </summary>
    /// <remarks>
    /// ⛔ Nulo a proposito con <c>--falso</c>, por el mismo motivo que
    /// <see cref="Mantenimiento"/>: alli no hay motor de PDF y un reporte que ahi dijera que se
    /// escribio estaria mintiendo. La pantalla lo dice en su linea, que es la version honesta
    /// de «aqui no».
    ///
    /// ⚠️ <b>Deuda declarada:</b> esto deberia ser un metodo mas de <see cref="Reportes"/>
    /// (criterio C15-4). <c>Fichas.Contratos</c> esta congelado, asi que va por su propia puerta
    /// mientras tanto. Es el MISMO objeto <c>ReportesEnPdf</c>, no un segundo motor.
    /// </remarks>
    public Paquetes.IReporteDeLaSegundaVuelta? ReporteDeLaSegundaVuelta { get; }

    /// <summary>
    /// Lo unico que borra de verdad, o NULO si el programa abrio con datos inventados.
    /// </summary>
    /// <remarks>
    /// ⛔ Nulo a proposito con <c>--falso</c> y cuando la base no se pudo abrir. Borrar
    /// exige copiar la base antes, y con datos inventados NO HAY base que copiar: un boton
    /// de borrar que ahi hiciera algo estaria mintiendo sobre lo unico del programa que no
    /// tiene vuelta atras. Las pantallas apagan el boton y lo dicen, que es la version
    /// honesta de «aqui no».
    /// </remarks>
    public IMantenimiento? Mantenimiento { get; }

    /// <summary>El reloj; nadie llama a DateTime.Now por su cuenta.</summary>
    public IReloj Reloj { get; }

    /// <summary>Lo que se pidio en la linea de ordenes.</summary>
    public ArgumentosDeArranque Argumentos { get; }

    /// <summary>El cuaderno de tiempos (<c>fichas.log</c>) donde se anotan arranque, navegaciones y fallos.</summary>
    public Registro Registro { get; }

    /// <summary>Donde cualquier pantalla deja un aviso para que lo pinte la franja.</summary>
    public BuzonDeAvisos Avisos { get; }

    /// <summary>
    /// El control de versiones: pregunta a GitHub por la última, baja el instalador y lo
    /// lanza, cada cosa solo cuando la ventana se lo pide.
    /// </summary>
    /// <remarks>
    /// Es la única salida a internet del programa (DECISIONES.md, 2026-09-11): una llamada
    /// HTTPS a la API de GitHub, y solo en un arranque normal o al pulsar «Buscar
    /// actualización». La clave la lee él de la carpeta de datos en cada llamada.
    /// </remarks>
    public Actualizacion.Actualizador Actualizador { get; }

    /// <summary>Si el programa esta ensenando datos inventados en vez de los del dueno.</summary>
    public bool SonDatosInventados => _conexion is null;

    /// <summary>
    /// El lector de formularios de punta a punta, o nulo si se arranco con datos inventados.
    /// </summary>
    /// <remarks>
    /// Es lo unico de esta clase que NO va por un contrato, y es a proposito:
    /// <c>ILecturaDePdf</c> da los pasos sueltos —rasterizar, anotaciones, OCR— y ponerlos
    /// en orden es trabajo con reglas medidas que ya vive en <c>Fichas.Lectura</c>.
    /// Repetirlo aqui seria repetir el unico sitio del programa donde una regla mal copiada
    /// produce un dato falso.
    /// </remarks>
    public LectorDeFormularios? ObtenerElLector() => _lector?.Value;

    /// <summary>
    /// Suelta el motor de OCR si está cargado, para devolver su memoria; la siguiente
    /// lectura lo vuelve a cargar sola (entre 340 y 470 ms medidos).
    /// </summary>
    /// <remarks>
    /// Lo llama el temporizador de reposo, y puede llamarlo cualquier pantalla que sepa que
    /// acaba de terminar con el OCR —al cerrar una tanda, al salir de Corrección— sin esperar
    /// al reposo. Con datos inventados, o sin haber leído nada todavía, no hace nada.
    /// </remarks>
    public void SoltarElOcr()
    {
        if (_lecturaDeVerdad is not { IsValueCreated: true }) return;
        var lectura = _lecturaDeVerdad.Value;
        var parado = lectura.TiempoSinLeer;
        if (parado is null) return;

        lectura.SoltarElMotor();
        Registro.Anotar($"OCR  motor soltado tras {parado.Value.TotalSeconds:F0} s parado; se recarga solo en la siguiente hoja");
    }

    /// <summary>Cuánto lleva el motor de OCR sin leer, o nulo si no está cargado (o la lectura ni se construyó).</summary>
    private TimeSpan? TiempoSinLeerDelOcr()
        => _lecturaDeVerdad is { IsValueCreated: true } ? _lecturaDeVerdad.Value.TiempoSinLeer : null;

    /// <summary>Cierra la base y libera el OCR si llegó a cargarse. El archivo tiene que quedar libre al salir.</summary>
    /// <remarks>El temporizador de reposo se para ANTES de desechar la lectura, y espera a que termine una comprobación en curso: así ninguna llega a un lector ya desechado.</remarks>
    public void Dispose()
    {
        _sueltaDelOcr?.Dispose();
        if (_lecturaDeVerdad is { IsValueCreated: true }) _lecturaDeVerdad.Value.Dispose();
        _conexion?.Close();
        _conexion?.Dispose();
    }
}

/// <summary>
/// Suelta el motor de OCR cuando lleva un rato parado, para que no se quede en memoria
/// entre una tanda y la siguiente.
/// </summary>
/// <remarks>
/// <para>Nace el 2026-09-15, con el pase de memoria. Sin la arena de ONNX el motor ya no
/// retiene el gigabyte, pero un motor cargado que ya leyó sigue pesando: medido en el
/// programa publicado, 482 MiB privados tras importar dieciséis documentos, frente a los
/// 112 del arranque. Soltarlo devuelve parte de eso y volverlo a cargar cuesta entre 340 y
/// 470 ms, que en una hoja de cinco segundos no se nota.</para>
///
/// <para>Se decide por tiempo sin leer y no por pantalla: importar y corregir viven en
/// otras carpetas, y así ninguna tiene que acordarse de avisar. Durante una tanda la
/// hoja siguiente llega antes del reposo, de modo que el motor no se suelta a medias; y
/// <c>SoltarElMotor</c> espera de todos modos a que termine la hoja en curso. La
/// comprobación corre en un temporizador del sistema, fuera del hilo de la ventana.</para>
/// </remarks>
public sealed class SueltaDelOcrEnReposo : IDisposable
{
    /// <summary>Cuánto lleva el motor sin leer, o nulo si no está cargado.</summary>
    private readonly Func<TimeSpan?> _tiempoSinLeer;

    /// <summary>Lo que suelta el motor de verdad.</summary>
    private readonly Action _soltar;

    /// <summary>A partir de cuánto tiempo parado se suelta.</summary>
    private readonly TimeSpan _reposo;

    /// <summary>El temporizador que comprueba cada tanto; nulo hasta <see cref="Arrancar"/>.</summary>
    private Timer? _temporizador;

    /// <summary>Monta la política sin arrancar ningún reloj; con <see cref="Comprobar"/> se decide a mano.</summary>
    /// <param name="tiempoSinLeer">Cuánto lleva el motor sin leer; nulo cuando no hay motor cargado.</param>
    /// <param name="soltar">Qué hacer para soltarlo.</param>
    /// <param name="reposo">A partir de cuánto tiempo parado se suelta.</param>
    public SueltaDelOcrEnReposo(Func<TimeSpan?> tiempoSinLeer, Action soltar, TimeSpan reposo)
    {
        _tiempoSinLeer = tiempoSinLeer;
        _soltar = soltar;
        _reposo = reposo;
    }

    /// <summary>Suelta el motor si lleva parado el reposo o más; dice si lo soltó.</summary>
    public bool Comprobar()
    {
        var parado = _tiempoSinLeer();
        if (parado is null || parado < _reposo) return false;
        _soltar();
        return true;
    }

    /// <summary>Empieza a comprobar cada <paramref name="cadaCuanto"/> en un hilo del sistema.</summary>
    /// <param name="cadaCuanto">Cada cuánto se mira; lo que tarde de más el motor en soltarse tras el reposo.</param>
    public void Arrancar(TimeSpan cadaCuanto)
        => _temporizador ??= new Timer(_ => Comprobar(), null, cadaCuanto, cadaCuanto);

    /// <summary>Para el temporizador y espera a que termine una comprobación en curso; no suelta nada por su cuenta.</summary>
    /// <remarks>Se espera a propósito: así quien desecha la lectura después sabe que ninguna comprobación la va a tocar.</remarks>
    public void Dispose()
    {
        if (_temporizador is null) return;
        using var terminado = new ManualResetEvent(false);
        if (_temporizador.Dispose(terminado)) terminado.WaitOne();
        _temporizador = null;
    }
}

/// <summary>
/// La lectura de PDF, construida solo cuando alguien la usa de verdad.
/// </summary>
/// <remarks>
/// Existe porque <c>Servicios</c> se monta al arrancar y cargar los tres modelos de OCR
/// cuesta segundos. Con esto, quien abre el programa a mirar el calendario no los paga, y
/// quien importa los paga una sola vez.
/// </remarks>
internal sealed class LecturaPerezosa : ILecturaDePdf
{
    /// <summary>La lectura de verdad; <c>Value</c> la construye la primera vez que se toca.</summary>
    private readonly Lazy<LecturaDePdf> _deVerdad;

    /// <summary>Envuelve la lectura sin construirla.</summary>
    /// <param name="deVerdad">La lectura perezosa que comparte con <see cref="Servicios"/>, para que se cargue una sola vez.</param>
    internal LecturaPerezosa(Lazy<LecturaDePdf> deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuántas hojas tiene el PDF (0 si no se pudo abrir); es el primer paso y el que fuerza la carga de la lectura.</summary>
    /// <param name="rutaPdf">Ruta del PDF en disco.</param>
    public int ContarPaginas(string rutaPdf) => _deVerdad.Value.ContarPaginas(rutaPdf);

    /// <summary>Rasteriza una página a imagen para el OCR; nulo si la página no se pudo dibujar.</summary>
    /// <param name="rutaPdf">Ruta del PDF en disco.</param>
    /// <param name="pagina">Número de hoja, empezando en 1.</param>
    /// <param name="anchoMaximo">Tope de píxeles en el lado largo, para que un escaneo grande no tarde minutos.</param>
    public Contratos.Lectura.ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
        => _deVerdad.Value.RasterizarPagina(rutaPdf, pagina, anchoMaximo);

    /// <summary>Las anotaciones (texto libre, tachones) de una página, que se leen sin OCR.</summary>
    /// <param name="rutaPdf">Ruta del PDF en disco.</param>
    /// <param name="pagina">Número de hoja, empezando en 1.</param>
    public IReadOnlyList<Contratos.Lectura.AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina)
        => _deVerdad.Value.LeerAnotaciones(rutaPdf, pagina);

    /// <summary>Pasa el OCR determinista sobre una imagen ya rasterizada; sin IA generativa (regla permanente 1).</summary>
    /// <param name="imagen">La imagen que devolvió <see cref="RasterizarPagina"/>.</param>
    public IReadOnlyList<Contratos.Lectura.LineaDeOcr> LeerConOcr(Contratos.Lectura.ImagenDePagina imagen)
        => _deVerdad.Value.LeerConOcr(imagen);

    /// <summary>Los idiomas con modelo cargado, para poder decir en pantalla con cuál se leyó.</summary>
    public IReadOnlyList<string> IdiomasDisponibles() => _deVerdad.Value.IdiomasDisponibles();
}

/// <summary>
/// Donde cualquier pantalla deja sus avisos y desde donde los recoge la franja.
/// </summary>
/// <remarks>
/// Es el sustituto de los 84 puntos del programa viejo donde una validacion levantaba un
/// error o abria un cuadro (requisito 9). Aqui nadie lanza: se deja la linea y se sigue.
/// </remarks>
public sealed class BuzonDeAvisos
{
    /// <summary>Los avisos abiertos; el más nuevo va en la posición 0, que es el que enseña la franja.</summary>
    private readonly List<Aviso> _pendientes = [];

    /// <summary>
    /// La acción de los avisos que la traen, por el OBJETO del aviso y no por su texto.
    /// </summary>
    /// <remarks>
    /// <c>Aviso</c> es un <c>record</c> de <c>Fichas.Contratos</c>, que está congelado: dos
    /// avisos con el mismo texto son iguales para él. Aquí se compara por referencia para que
    /// el botón vaya con el aviso que lo pidió y no con cualquiera que diga lo mismo.
    /// </remarks>
    private readonly Dictionary<Aviso, AccionDelAviso> _acciones = new(ReferenceEqualityComparer.Instance);

    /// <summary>Salta cuando entra un aviso nuevo, para que la franja se pinte sola.</summary>
    public event EventHandler? Cambio;

    /// <summary>Los avisos que todavia no se han cerrado, del mas nuevo al mas viejo.</summary>
    public IReadOnlyList<Aviso> Pendientes => _pendientes;

    /// <summary>Deja un aviso en el buzon, delante de los demás, y avisa a la franja.</summary>
    /// <param name="aviso">El aviso con su gravedad, su texto y su detalle.</param>
    public void Dejar(Aviso aviso)
    {
        _pendientes.Insert(0, aviso);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deja un aviso que trae un botón: la franja lo pinta con el rótulo y, al pulsarlo, hace lo que diga.</summary>
    /// <remarks>Nace el 2026-09-11 para «Hay una versión nueva: v12» con «Actualizar ahora». Es el único aviso con botón.</remarks>
    /// <param name="aviso">El aviso con su gravedad, su texto y su detalle.</param>
    /// <param name="accion">El rótulo del botón y qué hacer al pulsarlo.</param>
    public void Dejar(Aviso aviso, AccionDelAviso accion)
    {
        _acciones[aviso] = accion;
        Dejar(aviso);
    }

    /// <summary>La acción que trae este aviso, o nula si no trae ninguna o ya se cerró.</summary>
    /// <param name="aviso">El aviso, el mismo objeto que se dejó.</param>
    public AccionDelAviso? AccionDe(Aviso aviso) => _acciones.GetValueOrDefault(aviso);

    /// <summary>Deja varios avisos de una vez conservando su orden, y avisa a la franja una sola vez; si no hay ninguno, no molesta.</summary>
    /// <param name="avisos">Los avisos en el orden en que deben leerse; el primero queda delante.</param>
    public void Dejar(IReadOnlyList<Aviso> avisos)
    {
        ArgumentNullException.ThrowIfNull(avisos);
        if (avisos.Count == 0) return;
        for (var i = avisos.Count - 1; i >= 0; i--) _pendientes.Insert(0, avisos[i]);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cierra el aviso que se esta ensenando, y su acción se va con él.</summary>
    public void CerrarElPrimero()
    {
        if (_pendientes.Count == 0) return;
        _acciones.Remove(_pendientes[0]);
        _pendientes.RemoveAt(0);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cierra todos los avisos de golpe, con sus acciones.</summary>
    public void CerrarTodos()
    {
        if (_pendientes.Count == 0) return;
        _pendientes.Clear();
        _acciones.Clear();
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>El botón que puede traer un aviso: su rótulo y qué hacer al pulsarlo.</summary>
/// <param name="Rotulo">Lo que se lee en el botón: «Actualizar ahora».</param>
/// <param name="Hacer">Lo que pasa al pulsarlo; corre en el hilo de la ventana.</param>
public sealed record AccionDelAviso(string Rotulo, Action Hacer);
