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
    private readonly SqliteConnection? _conexion;
    private readonly Lazy<LecturaDePdf>? _lecturaDeVerdad;
    private readonly Lazy<LectorDeFormularios>? _lector;

    /// <summary>Con la base de verdad detras.</summary>
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

    /// <summary>Con datos inventados, que es lo que pide <c>--falso N</c>.</summary>
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

    /// <summary>Lo comun a las dos formas de montarse.</summary>
    private Servicios(ArgumentosDeArranque argumentos, Registro registro)
    {
        Argumentos = argumentos;
        Registro = registro;
        Avisos = new BuzonDeAvisos();
        Reloj = new RelojDelSistema();
    }

    /// <summary>Monta los servicios que usara toda la app.</summary>
    /// <remarks>
    /// Si la base no se puede abrir, el programa NO se queda sin arrancar: se monta lo
    /// falso vacio, se deja el aviso en la franja y la ventana abre igual. Una ventana que
    /// no aparece no deja donde leer el motivo, y entonces el dueno tiene un icono que no
    /// hace nada.
    /// </remarks>
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

    /// <summary>Los casos.</summary>
    public ICasos Casos { get; } = null!;

    /// <summary>Las personas.</summary>
    public IPersonas Personas { get; } = null!;

    /// <summary>Los companeros.</summary>
    public ICompaneros Companeros { get; } = null!;

    /// <summary>Las asignaciones; la unica puerta de asignar, desde cualquier pantalla.</summary>
    public IAsignaciones Asignaciones { get; } = null!;

    /// <summary>La procedencia de cada campo.</summary>
    public IProcedencia Procedencia { get; } = null!;

    /// <summary>Los documentos ilegibles y las filas descartadas.</summary>
    public IIlegibles Ilegibles { get; } = null!;

    /// <summary>La lectura de PDF.</summary>
    public ILecturaDePdf LecturaDePdf { get; } = null!;

    /// <summary>La extraccion de campos.</summary>
    public IExtraccion Extraccion { get; } = null!;

    /// <summary>Los paquetes de Excel.</summary>
    public IPaquetes Paquetes { get; } = null!;

    /// <summary>Los reportes en PDF.</summary>
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

    /// <summary>El cuaderno de tiempos.</summary>
    public Registro Registro { get; }

    /// <summary>Donde cualquier pantalla deja un aviso para que lo pinte la franja.</summary>
    public BuzonDeAvisos Avisos { get; }

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

    /// <summary>Cierra la base. El archivo tiene que quedar libre al salir.</summary>
    public void Dispose()
    {
        if (_lecturaDeVerdad is { IsValueCreated: true }) _lecturaDeVerdad.Value.Dispose();
        _conexion?.Close();
        _conexion?.Dispose();
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
    private readonly Lazy<LecturaDePdf> _deVerdad;

    internal LecturaPerezosa(Lazy<LecturaDePdf> deVerdad) => _deVerdad = deVerdad;

    public int ContarPaginas(string rutaPdf) => _deVerdad.Value.ContarPaginas(rutaPdf);

    public Contratos.Lectura.ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
        => _deVerdad.Value.RasterizarPagina(rutaPdf, pagina, anchoMaximo);

    public IReadOnlyList<Contratos.Lectura.AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina)
        => _deVerdad.Value.LeerAnotaciones(rutaPdf, pagina);

    public IReadOnlyList<Contratos.Lectura.LineaDeOcr> LeerConOcr(Contratos.Lectura.ImagenDePagina imagen)
        => _deVerdad.Value.LeerConOcr(imagen);

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
    private readonly List<Aviso> _pendientes = [];

    /// <summary>Salta cuando entra un aviso nuevo, para que la franja se pinte sola.</summary>
    public event EventHandler? Cambio;

    /// <summary>Los avisos que todavia no se han cerrado, del mas nuevo al mas viejo.</summary>
    public IReadOnlyList<Aviso> Pendientes => _pendientes;

    /// <summary>Deja un aviso en el buzon.</summary>
    public void Dejar(Aviso aviso)
    {
        _pendientes.Insert(0, aviso);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deja varios avisos de una vez; si no hay ninguno, no molesta.</summary>
    public void Dejar(IReadOnlyList<Aviso> avisos)
    {
        ArgumentNullException.ThrowIfNull(avisos);
        if (avisos.Count == 0) return;
        for (var i = avisos.Count - 1; i >= 0; i--) _pendientes.Insert(0, avisos[i]);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cierra el aviso que se esta ensenando.</summary>
    public void CerrarElPrimero()
    {
        if (_pendientes.Count == 0) return;
        _pendientes.RemoveAt(0);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cierra todos los avisos de golpe.</summary>
    public void CerrarTodos()
    {
        if (_pendientes.Count == 0) return;
        _pendientes.Clear();
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
