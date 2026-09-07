namespace Fichas.Contratos.Modelos;

/// <summary>
/// Un contacto con el lider sobre un caso. Tabla <c>contactos</c>, 12 columnas (ARQUITECTURA §2.6).
/// </summary>
/// <remarks>
/// ⛔ La tabla esta marcada PROVISIONAL en ARQUITECTURA §2.6: nadie ha escrito todavia
/// que es un «contacto con el lider» ni quien es el lider respecto de un caso. Este
/// registro copia el esquema tal como esta; construir la pantalla encima antes de esa
/// entrada del dueno es asumir retrabajo conocido.
/// </remarks>
public sealed record Contacto
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>Caso del que se habla; obligatorio.</summary>
    public long CasoId { get; init; }

    /// <summary>Cuando ocurrio el contacto, ISO-8601.</summary>
    public string Fecha { get; init; } = string.Empty;

    /// <summary>Por que medio se contacto.</summary>
    public string? Medio { get; init; }

    /// <summary>Con quien se hablo, que es el lider.</summary>
    public string? ConQuien { get; init; }

    /// <summary>Que resulto de la llamada.</summary>
    public string? Resultado { get; init; }

    /// <summary>Si el registro esta anulado; no se borra, se anula con su motivo.</summary>
    public bool Anulado { get; init; }

    /// <summary>Por que se anulo; obligatorio si <see cref="Anulado"/>.</summary>
    public string? MotivoAnulacion { get; init; }

    /// <summary>Cuando se anulo, ISO-8601; obligatorio si <see cref="Anulado"/>.</summary>
    public string? AnuladoEn { get; init; }

    /// <summary>Cuando se registro en el programa, distinto de <see cref="Fecha"/>.</summary>
    public string RegistradoEn { get; init; } = string.Empty;

    /// <summary>Quien llamo; id de <c>companeros</c>. Otra pregunta que <see cref="ConQuien"/>.</summary>
    public long? ContactadoPor { get; init; }

    /// <summary>Si respondio (true), no respondio (false) o todavia no se sabe (nulo).</summary>
    public bool? Respondio { get; init; }
}
