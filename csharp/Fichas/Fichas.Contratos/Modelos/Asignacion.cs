namespace Fichas.Contratos.Modelos;

/// <summary>
/// Que caso lleva que companero. Tabla <c>asignaciones</c>, 6 columnas (ARQUITECTURA §2.5).
/// </summary>
/// <remarks>
/// Se retiran desactivando, no borrando, para conservar quien llevo que.
/// ⚠️ El motor permite hoy DOS asignaciones vivas sobre el mismo caso con companeros
/// distintos (medido, ARQUITECTURA §2.5). Es la P-11, abierta y devuelta al dueno;
/// este contrato no inventa la regla que falta.
/// </remarks>
public sealed record Asignacion
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>Caso asignado; obligatorio.</summary>
    public long CasoId { get; init; }

    /// <summary>Companero que lo lleva; obligatorio.</summary>
    public long CompaneroId { get; init; }

    /// <summary>Cuando se asigno, ISO-8601.</summary>
    public string AsignadoEn { get; init; } = string.Empty;

    /// <summary>Si la asignacion sigue viva.</summary>
    public bool Activa { get; init; } = true;

    /// <summary>Cuando se retiro, ISO-8601; va con <see cref="Activa"/> en falso o ninguna.</summary>
    public string? DesactivadaEn { get; init; }
}
