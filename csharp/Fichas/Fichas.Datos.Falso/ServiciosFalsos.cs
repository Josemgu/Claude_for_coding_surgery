using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Los once servicios falsos ya montados sobre un mismo almacen.
/// </summary>
/// <remarks>
/// Es lo que registra la app en un solo sitio (Fichas.App/Cascara/Servicios.cs). Cuando
/// exista Fichas.Datos, el supervisor cambia esa linea y esta clase deja de usarse; nada
/// mas cambia, porque las pantallas solo ven Fichas.Contratos.
/// </remarks>
public sealed class ServiciosFalsos
{
    /// <summary>Monta los once servicios sobre una base inventada de N casos con su semilla.</summary>
    /// <param name="cantidadDeCasos">Cuantos casos se inventan; 3 000 es la cifra del requisito 5.</param>
    /// <param name="semilla">La semilla del sorteo; la misma semilla da la misma base.</param>
    /// <param name="reloj">El reloj; si va nulo se usa el del sistema.</param>
    public ServiciosFalsos(int cantidadDeCasos, int semilla = 20260904, IReloj? reloj = null)
    {
        Reloj = reloj ?? new RelojDelSistema();
        Almacen = GeneradorFalso.Generar(cantidadDeCasos, semilla, Reloj);
        Casos = new RepositorioDeCasosFalso(Almacen);
        Personas = new RepositorioDePersonasFalso(Almacen);
        Companeros = new RepositorioDeCompanerosFalso(Almacen);
        Asignaciones = new RepositorioDeAsignacionesFalso(Almacen);
        Procedencia = new RepositorioDeProcedenciaFalso(Almacen);
        Ilegibles = new RepositorioDeIlegiblesFalso(Almacen);
        LecturaDePdf = new LecturaDePdfFalsa(semilla);
        Extraccion = new ExtraccionFalsa();
        Paquetes = new PaquetesFalsos(Almacen);
        Reportes = new ReportesFalsos(Almacen);
    }

    /// <summary>El almacen en memoria, por si una prueba necesita mirarlo por dentro.</summary>
    public AlmacenFalso Almacen { get; }

    /// <summary>El reloj con el que se generaron las fechas.</summary>
    public IReloj Reloj { get; }

    /// <summary>Los casos.</summary>
    public ICasos Casos { get; }

    /// <summary>Las personas.</summary>
    public IPersonas Personas { get; }

    /// <summary>Los companeros.</summary>
    public ICompaneros Companeros { get; }

    /// <summary>Las asignaciones.</summary>
    public IAsignaciones Asignaciones { get; }

    /// <summary>La procedencia de cada campo.</summary>
    public IProcedencia Procedencia { get; }

    /// <summary>Los documentos ilegibles y las filas descartadas.</summary>
    public IIlegibles Ilegibles { get; }

    /// <summary>La lectura de PDF, que aqui no lee ningun PDF.</summary>
    public ILecturaDePdf LecturaDePdf { get; }

    /// <summary>La extraccion, que aqui solo reconoce las anclas mas simples.</summary>
    public IExtraccion Extraccion { get; }

    /// <summary>Los paquetes de Excel, que aqui no escriben archivos.</summary>
    public IPaquetes Paquetes { get; }

    /// <summary>Los reportes en PDF, que aqui no escriben archivos.</summary>
    public IReportes Reportes { get; }
}
