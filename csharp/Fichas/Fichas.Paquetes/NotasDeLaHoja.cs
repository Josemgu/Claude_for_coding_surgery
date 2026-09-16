using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Paquetes;

/// <summary>Los nombres de los companeros que firmaron respuestas, leidos una vez por paquete.</summary>
/// <remarks>
/// Un paquete de cincuenta personas contestadas por la misma persona no tiene por que pedir
/// cincuenta veces el mismo nombre. Vive lo que dura una generacion.
/// </remarks>
public sealed class NombresDeCompaneros
{
    /// <summary>De donde se leen los que todavia no se han pedido.</summary>
    private readonly ICompaneros _companeros;

    /// <summary>Los ya pedidos, por id; nulo el que no existe.</summary>
    private readonly Dictionary<long, string?> _nombres = [];

    /// <summary>Se ata al puerto de compañeros.</summary>
    /// <param name="companeros">El puerto del que se leen los nombres.</param>
    public NombresDeCompaneros(ICompaneros companeros) => _companeros = companeros;

    /// <summary>El nombre de ese companero, o una frase de respaldo si ya no esta en la base.</summary>
    /// <param name="id">El número interno del compañero.</param>
    public string De(long id)
    {
        if (!_nombres.TryGetValue(id, out var nombre))
        {
            nombre = _companeros.Obtener(id)?.Nombre;
            _nombres[id] = nombre;
        }
        return string.IsNullOrWhiteSpace(nombre) ? $"un compañero que ya no está (número {id})" : nombre;
    }
}

/// <summary>
/// Las notas que acompanan a una respuesta que sale ya contestada en la hoja «Por verificar»:
/// quien la contesto, cuando y desde donde, en palabras.
/// </summary>
/// <remarks>
/// Existen desde el 2026-09-16, cuando el dueno pidio que el paquete saliera con lo que ya
/// esta contestado (PENDIENTES.md, v16, 5). Sin la nota, el companero ve una celda amarilla
/// ya rellena y no sabe si es un error; con ella sabe que fue Miguel desde la pantalla, o
/// otro companero desde su Excel, y cuando.
/// </remarks>
public static class NotasDeLaHoja
{
    /// <summary>
    /// La nota de los seis pasos: quien los contesto, cuando y desde donde; nulo si nadie.
    /// </summary>
    /// <param name="firma">La firma de la migración 19 de esa persona; nula o sin firmar da nulo.</param>
    /// <param name="nombres">Los nombres de los compañeros de este paquete.</param>
    public static string? DeQuienContestoLasSeis(FirmaDeLosPasos? firma, NombresDeCompaneros nombres)
    {
        ArgumentNullException.ThrowIfNull(nombres);
        if (firma is null || firma.Por is not long por)
            return null;
        var partes = new List<string> { $"Contestó {nombres.De(por)}" };
        var cuando = LibroDeTrabajo.FechaLegible(SoloLaFecha(firma.En));
        if (cuando.Length > 0) partes.Add($"el {cuando}");
        if (!string.IsNullOrWhiteSpace(firma.Origen)) partes.Add($"desde {firma.Origen}");
        return string.Join(" · ", partes) + ".";
    }

    /// <summary>
    /// La nota de la llamada al lider: quien la dijo y cuando; nulo si nadie.
    /// </summary>
    /// <remarks>
    /// La llamada no es un paso y <c>pasos_por</c> no la firma: la trae el Excel de un
    /// companero, asi que su firma es <c>propuesto_por</c> / <c>propuesto_en</c>.
    /// </remarks>
    /// <param name="persona">La persona, con lo que propuso el último compañero.</param>
    /// <param name="nombres">Los nombres de los compañeros de este paquete.</param>
    public static string? DeQuienDijoLaLlamada(Persona persona, NombresDeCompaneros nombres)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(nombres);
        if (persona.LlamoAlLider is null || persona.PropuestoPor is not long por)
            return null;
        var partes = new List<string> { $"Lo dijo {nombres.De(por)} en su Excel" };
        var cuando = LibroDeTrabajo.FechaLegible(SoloLaFecha(persona.PropuestoEn));
        if (cuando.Length > 0) partes.Add($"el {cuando}");
        return string.Join(" · ", partes) + ".";
    }

    /// <summary>Los diez primeros caracteres de una marca de tiempo ISO-8601: la fecha sin la hora.</summary>
    /// <param name="marcaDeTiempo">«aaaa-MM-dd HH:mm:ss», o solo la fecha, o nulo.</param>
    /// <returns>«aaaa-MM-dd», o lo que venga si es más corto, o nulo.</returns>
    private static string? SoloLaFecha(string? marcaDeTiempo)
        => marcaDeTiempo is { Length: >= 10 } ? marcaDeTiempo[..10] : marcaDeTiempo;
}
