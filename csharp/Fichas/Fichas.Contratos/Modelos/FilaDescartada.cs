namespace Fichas.Contratos.Modelos;

/// <summary>
/// Lo que volvio en el Excel de un companero y NO entro en la base.
/// Tabla <c>filas_descartadas</c>, 9 columnas (ARQUITECTURA §2.9).
/// </summary>
/// <remarks>
/// Ni <see cref="NumeroCaso"/> ni <see cref="Mrn"/> se validan aqui: lo que venia escrito
/// puede ser justo lo que estaba mal, y una tabla que existe para guardar lo que no entro
/// no puede rechazar lo que no entro.
/// </remarks>
public sealed record FilaDescartada
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>De quien era el Excel; id de <c>companeros</c>, obligatorio.</summary>
    public long CompaneroId { get; init; }

    /// <summary>El archivo del que volvio la fila.</summary>
    public string? RutaExcel { get; init; }

    /// <summary>El numero de fila con el que choco, base 1; es lo que hace falta para ir a mirarla.</summary>
    public int? FilaExcel { get; init; }

    /// <summary>El numero de caso tal como venia escrito, sin validar.</summary>
    public string? NumeroCaso { get; init; }

    /// <summary>El MRN tal como venia escrito, sin validar.</summary>
    public string? Mrn { get; init; }

    /// <summary>El nombre tal como venia escrito, sin validar.</summary>
    public string? Nombre { get; init; }

    /// <summary>Una frase, no un codigo: lleva dentro la fila y la causa concreta.</summary>
    public string Motivo { get; init; } = string.Empty;

    /// <summary>Cuando se registro, ISO-8601.</summary>
    public string RegistradoEn { get; init; } = string.Empty;
}
