namespace Fichas.Contratos.Modelos;

/// <summary>
/// Un PDF o una pagina que no se pudo leer, con su ruta, su hoja y su motivo.
/// Tabla <c>documentos_ilegibles</c>, 8 columnas (ARQUITECTURA §2.8).
/// </summary>
/// <remarks>
/// Es una tabla y no un cuadro de dialogo a proposito: con 500 documentos, un dialogo
/// que se cierra con Aceptar no deja nada que consultar despues.
/// </remarks>
public sealed record RenglonIlegible
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>El archivo del que se habla; sin el, el renglon no sirve para ir a mirar nada.</summary>
    public string RutaPdf { get; init; } = string.Empty;

    /// <summary>La hoja, base 1; nula cuando el problema fue del archivo entero.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>Un codigo, no una frase: un codigo se agrupa y se cuenta.</summary>
    public string Motivo { get; init; } = string.Empty;

    /// <summary>El texto libre que acompana al codigo, cuando lo hay.</summary>
    public string? Detalle { get; init; }

    /// <summary>Cuantas lineas llego a leer el OCR; cero es un dato, nulo es «no se conto».</summary>
    public int? LineasLeidas { get; init; }

    /// <summary>El caso que si se llego a guardar de esa pagina, cuando lo hay.</summary>
    public long? CasoId { get; init; }

    /// <summary>Cuando se registro, ISO-8601.</summary>
    public string RegistradoEn { get; init; } = string.Empty;
}
