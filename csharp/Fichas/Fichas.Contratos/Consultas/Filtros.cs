using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Consultas;

/// <summary>
/// Los filtros simples con los que se pide una lista de casos. Todos opcionales.
/// </summary>
/// <remarks>
/// Son cuatro preguntas y nada mas: lo de hoy, lo de una ventana de dias, lo de un
/// companero y lo que contenga un texto. Un filtro que no se entiende no rechaza la
/// consulta: se ignora y se deja aviso (requisito 9).
/// </remarks>
/// <param name="SoloDeHoy">Solo lo que viaja hoy.</param>
/// <param name="VentanaDeDias">Lo que viaja de hoy a hoy mas N dias; la franja roja usa 7.</param>
/// <param name="CompaneroId">Solo lo asignado vivo a ese companero.</param>
/// <param name="Texto">Busqueda por numero de caso, nombre o MRN; sin distinguir mayusculas.</param>
/// <param name="IncluirArchivados">Si entran tambien los archivados; por defecto no.</param>
/// <param name="SoloVencidos">Solo lo que ya viajo y sigue sin resolver.</param>
/// <param name="Estado">Solo los que esten en ese estado; nulo es cualquiera.</param>
public sealed record FiltroDeCasos(
    bool SoloDeHoy = false,
    int? VentanaDeDias = null,
    long? CompaneroId = null,
    string? Texto = null,
    bool IncluirArchivados = false,
    bool SoloVencidos = false,
    EstadoDeRecomendacion? Estado = null)
{
    /// <summary>Sin filtro ninguno: todo lo no archivado.</summary>
    public static FiltroDeCasos Todo { get; } = new();
}

/// <summary>Los filtros simples con los que se pide una lista de personas.</summary>
/// <param name="CasoId">Solo las personas de ese caso.</param>
/// <param name="Texto">Busqueda por nombre o MRN; sin distinguir mayusculas.</param>
/// <param name="SinMrn">Solo las que no tienen MRN, que son las que rompen la reconciliacion.</param>
public sealed record FiltroDePersonas(
    long? CasoId = null,
    string? Texto = null,
    bool SinMrn = false)
{
    /// <summary>Sin filtro ninguno.</summary>
    public static FiltroDePersonas Todo { get; } = new();
}

/// <summary>Los filtros simples con los que se pide una lista de companeros.</summary>
/// <param name="SoloActivos">Solo los que siguen activos; por defecto si.</param>
/// <param name="Texto">Busqueda por nombre; sin distinguir mayusculas.</param>
public sealed record FiltroDeCompaneros(
    bool SoloActivos = true,
    string? Texto = null)
{
    /// <summary>Los activos, que es lo que pide casi toda pantalla.</summary>
    public static FiltroDeCompaneros Activos { get; } = new();
}

/// <summary>Los filtros simples con los que se pide una lista de asignaciones.</summary>
/// <param name="CasoId">Solo las de ese caso.</param>
/// <param name="CompaneroId">Solo las de ese companero.</param>
/// <param name="SoloActivas">Solo las vivas; por defecto si.</param>
/// <param name="SinDevolver">Solo las vivas de las que el companero no ha devuelto su hoja.</param>
public sealed record FiltroDeAsignaciones(
    long? CasoId = null,
    long? CompaneroId = null,
    bool SoloActivas = true,
    bool SinDevolver = false)
{
    /// <summary>Las vivas, que es lo que pide casi toda pantalla.</summary>
    public static FiltroDeAsignaciones Activas { get; } = new();
}

/// <summary>Los filtros simples con los que se pide la lista de documentos ilegibles.</summary>
/// <param name="RutaPdf">Solo los renglones de ese archivo.</param>
/// <param name="Motivo">Solo los de ese codigo de motivo.</param>
public sealed record FiltroDeIlegibles(
    string? RutaPdf = null,
    string? Motivo = null)
{
    /// <summary>Sin filtro ninguno.</summary>
    public static FiltroDeIlegibles Todo { get; } = new();
}
