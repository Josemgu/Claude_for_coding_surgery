using System.Text.RegularExpressions;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Mantenimiento;

/// <summary>
/// La regla pura de unificar: qué personas y qué campos del duplicado pasan al original.
/// </summary>
/// <remarks>
/// <para>Vive aparte del repositorio y sin una sola consulta a propósito: es una decisión sobre
/// dos listas y cinco campos, y se prueba con objetos sueltos. El repositorio la lee dos veces
/// —al planear, para preguntar; al ejecutar, sobre lo que hay entonces— y por eso tiene que ser
/// la misma función las dos veces.</para>
///
/// <para>⚠️ <b>Ante la duda, la persona pasa.</b> Una persona repetida en el original se ve en
/// la tarjeta y se arregla a mano; una persona perdida no la ve nadie hasta que llega al templo
/// sin recomendación. Por eso «nombre exacto» es exacto en mayúsculas y solo perdona los
/// espacios de sobra, y por eso una persona con cédula solo se empareja por cédula.</para>
/// </remarks>
public static class LoQuePasaAlOriginal
{
    /// <summary>Dos o más espacios seguidos, para colapsarlos antes de comparar nombres.</summary>
    private static readonly Regex EspaciosDeSobra = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Las personas del duplicado que el original no tiene, y cuántas ya estaban.</summary>
    /// <remarks>
    /// Misma cédula (comparación ordinal) es la misma persona, diga lo que diga el nombre: la
    /// cédula es de UNA persona y el nombre lo escribe el OCR. Sin cédula en la del duplicado,
    /// decide el nombre exacto contra todas las del original.
    /// </remarks>
    /// <param name="delOriginal">Las personas que ya tiene el original.</param>
    /// <param name="delDuplicado">Las personas del duplicado.</param>
    /// <returns>Las que pasan, en el orden del duplicado, y cuántas ya estaban.</returns>
    public static RepartoDePersonas Personas(IReadOnlyList<Persona> delOriginal, IReadOnlyList<Persona> delDuplicado)
    {
        ArgumentNullException.ThrowIfNull(delOriginal);
        ArgumentNullException.ThrowIfNull(delDuplicado);

        var cedulas = new HashSet<string>(
            delOriginal.Select(p => p.Mrn).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!.Trim()),
            StringComparer.Ordinal);
        var nombres = new HashSet<string>(
            delOriginal.Select(p => NombreComparable(p.Nombre)).Where(n => n.Length > 0),
            StringComparer.Ordinal);

        var pasan = new List<PersonaQuePasaAlOriginal>();
        var yaEstaban = 0;
        foreach (var persona in delDuplicado)
        {
            if (YaEsta(persona, cedulas, nombres)) yaEstaban++;
            else pasan.Add(new PersonaQuePasaAlOriginal(persona.Id, persona.Nombre?.Trim() ?? string.Empty, persona.Mrn, persona.PaginaPdf));
        }

        return new RepartoDePersonas(pasan, yaEstaban);
    }

    /// <summary>Los campos vacíos en el original que el duplicado sí trae; lo lleno no se pisa.</summary>
    /// <param name="original">El documento que se queda.</param>
    /// <param name="duplicado">El documento que se va.</param>
    /// <returns>Los que pasan, en el orden de <see cref="PlanDeUnificacion.LosCincoCampos"/>.</returns>
    public static IReadOnlyList<CampoQuePasaAlOriginal> Campos(Caso original, Caso duplicado)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(duplicado);

        var pasan = new List<CampoQuePasaAlOriginal>();
        foreach (var campo in PlanDeUnificacion.LosCincoCampos)
        {
            var enElOriginal = ValorDe(original, campo.Columna);
            var enElDuplicado = ValorDe(duplicado, campo.Columna);
            if (!string.IsNullOrWhiteSpace(enElOriginal) || string.IsNullOrWhiteSpace(enElDuplicado)) continue;
            pasan.Add(new CampoQuePasaAlOriginal(campo.Columna, campo.Rotulo, enElDuplicado!.Trim()));
        }

        return pasan;
    }

    /// <summary>El valor de una de las cinco columnas en un caso, por su nombre de columna.</summary>
    /// <param name="caso">El documento.</param>
    /// <param name="columna">Una de las cinco de <see cref="PlanDeUnificacion.LosCincoCampos"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si la columna no es una de las cinco: nunca se lee una columna que no esté en la lista.</exception>
    public static string? ValorDe(Caso caso, string columna)
    {
        ArgumentNullException.ThrowIfNull(caso);

        return columna switch
        {
            "numero_caso" => caso.NumeroCaso,
            "unidad_numero" => caso.UnidadNumero,
            "unidad_nombre" => caso.UnidadNombre,
            "fecha_viaje" => caso.FechaViaje,
            "templo_nombre" => caso.TemploNombre,
            _ => throw new ArgumentOutOfRangeException(nameof(columna), columna, "No es uno de los cinco campos que se unifican."),
        };
    }

    /// <summary>Si esa persona del duplicado ya está en el original, por cédula o, sin ella, por nombre.</summary>
    /// <param name="persona">La del duplicado.</param>
    /// <param name="cedulas">Las cédulas del original.</param>
    /// <param name="nombres">Los nombres comparables del original.</param>
    private static bool YaEsta(Persona persona, HashSet<string> cedulas, HashSet<string> nombres)
    {
        if (!string.IsNullOrWhiteSpace(persona.Mrn)) return cedulas.Contains(persona.Mrn.Trim());
        var nombre = NombreComparable(persona.Nombre);
        return nombre.Length > 0 && nombres.Contains(nombre);
    }

    /// <summary>El nombre recortado y con un solo espacio entre palabras; las mayúsculas se quedan.</summary>
    /// <param name="nombre">El nombre tal como está en la base, o nulo.</param>
    private static string NombreComparable(string? nombre)
        => nombre is null ? string.Empty : EspaciosDeSobra.Replace(nombre.Trim(), " ");
}

/// <summary>Lo que decidió <see cref="LoQuePasaAlOriginal.Personas"/>.</summary>
/// <param name="Pasan">Las personas del duplicado que el original no tiene.</param>
/// <param name="YaEstaban">Cuántas ya estaban y se van con el duplicado.</param>
public sealed record RepartoDePersonas(IReadOnlyList<PersonaQuePasaAlOriginal> Pasan, int YaEstaban);
