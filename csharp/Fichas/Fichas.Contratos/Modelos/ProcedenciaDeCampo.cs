namespace Fichas.Contratos.Modelos;

/// <summary>
/// De donde salio cada valor, con que confianza, que leyo el OCR antes de corregirlo y
/// quien lo dio por bueno. Tabla <c>procedencia_campo</c>, 16 columnas (ARQUITECTURA §2.7).
/// </summary>
/// <remarks>
/// Las cuatro coordenadas de la banda van en FRACCIONES de la pagina (0,0 a 1,0), nunca
/// en pixeles: un pixel depende de la escala del dia en que se rasterizo.
/// La regla permanente 5 es estructura aqui: no se puede marcar verificado sin quien y cuando.
/// </remarks>
public sealed record ProcedenciaDeCampo
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>A que tabla apunta: casos o personas.</summary>
    public TablaDeProcedencia Tabla { get; init; }

    /// <summary>Id de la fila en esa tabla.</summary>
    public long RegistroId { get; init; }

    /// <summary>Nombre de la columna cuya procedencia se describe.</summary>
    public string Campo { get; init; } = string.Empty;

    /// <summary>De donde salio el valor: anotacion, ocr, vacio o manual.</summary>
    public OrigenDeCampo Origen { get; init; }

    /// <summary>Confianza de 0,0 a 1,0; nula cuando no aplica.</summary>
    public double? Confianza { get; init; }

    /// <summary>Lo que leyo el OCR ANTES de cualquier correccion; nulo si el OCR nunca lo leyo.</summary>
    public string? ValorOcr { get; init; }

    /// <summary>Si Miguel lo dio por bueno. Nunca se pone solo (regla permanente 5).</summary>
    public bool Verificado { get; init; }

    /// <summary>Quien lo firmo; id de <c>companeros</c>. Obligatorio si <see cref="Verificado"/>.</summary>
    public long? VerificadoPor { get; init; }

    /// <summary>Cuando lo firmo, ISO-8601. Obligatorio si <see cref="Verificado"/>.</summary>
    public string? VerificadoEn { get; init; }

    /// <summary>Borde izquierdo de la banda en fraccion de pagina, 0,0 a 1,0.</summary>
    public double? BandaX0 { get; init; }

    /// <summary>Borde superior de la banda en fraccion de pagina, 0,0 a 1,0.</summary>
    public double? BandaY0 { get; init; }

    /// <summary>Borde derecho de la banda en fraccion de pagina, 0,0 a 1,0.</summary>
    public double? BandaX1 { get; init; }

    /// <summary>Borde inferior de la banda en fraccion de pagina, 0,0 a 1,0.</summary>
    public double? BandaY1 { get; init; }

    /// <summary>El papel llevaba un tachon encima; distinto de vacio y de ausente.</summary>
    public bool AnuladoPorTachon { get; init; }

    /// <summary>La hoja no trae ese campo; no hay donde mirar y Miguel no pierde tiempo buscando.</summary>
    public bool AusenteEnElPapel { get; init; }
}
