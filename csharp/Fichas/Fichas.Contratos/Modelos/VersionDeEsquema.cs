namespace Fichas.Contratos.Modelos;

/// <summary>
/// Que version de esquema tiene la base y cuando se aplico.
/// Tabla <c>version_esquema</c>, 3 columnas (ARQUITECTURA §2.1). La vigente es el maximo.
/// </summary>
/// <remarks>
/// No lo pidio el pase por su nombre, y va aqui por una razon medible: las 104 columnas
/// del esquema v14 solo suman con estas tres (20+25+5+6+12+16+8+9+3). Sin este registro
/// el conteo da 101 y la prueba del esquema no cerraria.
/// </remarks>
public sealed record VersionDeEsquema
{
    /// <summary>El entero que identifica la version; clave primaria.</summary>
    public int Version { get; init; }

    /// <summary>Cuando se aplico, ISO-8601.</summary>
    public string AplicadaEn { get; init; } = string.Empty;

    /// <summary>Que cambio esa version, en espanol.</summary>
    public string Descripcion { get; init; } = string.Empty;
}
