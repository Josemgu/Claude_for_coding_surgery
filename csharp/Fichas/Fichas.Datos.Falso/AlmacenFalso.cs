using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Todo lo inventado, en memoria y en un solo sitio. Los seis repositorios falsos leen
/// y escriben aqui, para que asignar desde una pantalla se vea desde las otras cinco.
/// </summary>
public sealed class AlmacenFalso
{
    /// <summary>Los casos, por id.</summary>
    public Dictionary<long, Caso> Casos { get; } = [];

    /// <summary>Las personas, por id.</summary>
    public Dictionary<long, Persona> Personas { get; } = [];

    /// <summary>
    /// Quién contestó las seis preguntas de cada persona, cuándo y desde dónde; por id de
    /// persona.
    /// </summary>
    /// <remarks>
    /// Va en un diccionario aparte y no dentro de <see cref="Persona"/> por el mismo motivo
    /// que en el repositorio de verdad no se lee con la fila: ese modelo vive en
    /// <c>Fichas.Contratos/Modelos</c>, que esta congelado. Aqui son las tres columnas de la
    /// migracion 19 —<c>pasos_por</c>, <c>pasos_en</c>, <c>pasos_origen</c>— en memoria.
    /// </remarks>
    public Dictionary<long, FirmaDeLosPasos> FirmasDeLosPasos { get; } = [];

    /// <summary>Los companeros, por id.</summary>
    public Dictionary<long, Companero> Companeros { get; } = [];

    /// <summary>Las asignaciones, por id.</summary>
    public Dictionary<long, Asignacion> Asignaciones { get; } = [];

    /// <summary>La procedencia de cada campo, por id.</summary>
    public Dictionary<long, ProcedenciaDeCampo> Procedencias { get; } = [];

    /// <summary>Los renglones de documentos ilegibles, por id.</summary>
    public Dictionary<long, RenglonIlegible> Ilegibles { get; } = [];

    /// <summary>Las filas del Excel que no entraron, por id.</summary>
    public Dictionary<long, FilaDescartada> Descartadas { get; } = [];

    /// <summary>El reloj con el que se genero y con el que se leen las ventanas de dias.</summary>
    public IReloj Reloj { get; }

    /// <summary>La semilla con la que se genero, para poder repetir exactamente esta base.</summary>
    public int Semilla { get; }

    /// <summary>El último id repartido; un solo contador para las ocho tablas, como el <c>rowid</c> no lo haría.</summary>
    private long _siguienteId;

    /// <summary>Crea un almacen vacio con su reloj y su semilla.</summary>
    /// <param name="reloj">El reloj con el que se fechará todo lo que se genere y se escriba.</param>
    /// <param name="semilla">La semilla del sorteo; solo se guarda para poder decir con cuál se generó.</param>
    public AlmacenFalso(IReloj reloj, int semilla)
    {
        Reloj = reloj;
        Semilla = semilla;
    }

    /// <summary>Devuelve el siguiente id libre; los ids no se reutilizan.</summary>
    /// <returns>Empieza en 1 y crece de uno en uno para todas las tablas a la vez: por eso una prueba puede comprobar que no se repiten entre ellas.</returns>
    public long SiguienteId() => ++_siguienteId;

    /// <summary>Devuelve las personas de un caso en el orden en que venian en el formulario.</summary>
    /// <param name="casoId">El caso; uno que no existe da la lista vacía.</param>
    /// <returns>Por fila del formulario, con las que no tienen fila al final y luego por id; nunca nulo.</returns>
    public List<Persona> PersonasDe(long casoId)
        => Personas.Values
            .Where(p => p.CasoId == casoId)
            .OrderBy(p => p.FilaFormulario ?? int.MaxValue)
            .ThenBy(p => p.Id)
            .ToList();
}
