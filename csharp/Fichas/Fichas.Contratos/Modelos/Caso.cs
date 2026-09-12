namespace Fichas.Contratos.Modelos;

/// <summary>
/// Un caso: un formulario de recomendacion al templo. Tabla <c>casos</c>, 22 columnas.
/// </summary>
/// <remarks>
/// Los nombres salen de docs/ARQUITECTURA.md §2.2 uno a uno; la unica traduccion es
/// pasar <c>columna_asi</c> a <c>ColumnaAsi</c>. Nada se anade y nada se quita: si aqui
/// falta una columna, es que el modelo esta mal, no el esquema (CLAUDE.md §7).
/// </remarks>
public sealed record Caso
{
    /// <summary>Clave primaria; 0 mientras el caso no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>Cuatro letras y cuatro digitos, o nulo si no se pudo leer. Ya NO es unico (migracion 12).</summary>
    public string? NumeroCaso { get; init; }

    /// <summary>Id del caso del que este documento es duplicado; nulo si no repite a ninguno.</summary>
    public long? DuplicadoDe { get; init; }

    /// <summary>Quien puso el estado que vale ahora; id de <c>companeros</c>.</summary>
    public long? EstadoMarcadoPor { get; init; }

    /// <summary>Cuando se puso ese estado, ISO-8601.</summary>
    public string? EstadoMarcadoEn { get; init; }

    /// <summary>De donde salio la marca: la ruta del Excel, o la frase que dice que fue a mano.</summary>
    public string? EstadoMarcadoOrigen { get; init; }

    /// <summary>Lo que dijo la hoja del companero, sin pisar lo que marque Miguel.</summary>
    public string? EstadoDelCompanero { get; init; }

    /// <summary>Que companero lo dijo; id de <c>companeros</c>.</summary>
    public long? EstadoDelCompaneroPor { get; init; }

    /// <summary>Cuando lo dijo, ISO-8601.</summary>
    public string? EstadoDelCompaneroEn { get; init; }

    /// <summary>Numero de unidad, 6 o 7 digitos; TEXT para no perder el cero de delante.</summary>
    public string? UnidadNumero { get; init; }

    /// <summary>Fecha del viaje al templo, ISO-8601; nula mientras no se sepa.</summary>
    public string? FechaViaje { get; init; }

    /// <summary>El formulario era manuscrito y entro vacio para teclearlo entero.</summary>
    public bool CapturaManual { get; init; }

    /// <summary>El caso esta archivado y sale de la vista entera, calendario incluido (2026-09-06). Solo se ve con «Ver los archivados» de Revisar, y sigue contando en los reportes.</summary>
    public bool Archivado { get; init; }

    /// <summary>Cuando se archivo, ISO-8601; va con <see cref="Archivado"/> o ninguna de las dos.</summary>
    public string? FechaArchivado { get; init; }

    /// <summary>Ruta del PDF del que salio este caso.</summary>
    public string? RutaPdf { get; init; }

    /// <summary>Cuando se dio de alta en el programa, ISO-8601.</summary>
    public string CreadoEn { get; init; } = string.Empty;

    /// <summary>El estado tal como esta escrito en la base; texto libre, sin lista cerrada.</summary>
    public string? EstadoRecomendacion { get; init; }

    /// <summary>Hoja del PDF que abrio el caso, base 1.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>Nombre de la unidad tal como se leyo de la etiqueta del papel.</summary>
    public string? UnidadNombre { get; init; }

    /// <summary>Nombre del templo tal como se leyo; sin catalogo y sin correccion a mano todavia.</summary>
    public string? TemploNombre { get; init; }

    /// <summary>Por que NO esta completa, el que vale ahora; nulo mientras nadie lo diga.</summary>
    public string? MotivoNoCompleta { get; init; }

    /// <summary>Por que lo dijo la hoja del companero; no se borra cuando Miguel corrige encima.</summary>
    public string? MotivoDelCompanero { get; init; }

    /// <summary>Lectura del texto libre de <see cref="EstadoRecomendacion"/> como enumerado.</summary>
    public EstadoDeRecomendacion Estado => LeerEstado(EstadoRecomendacion);

    /// <summary>Lectura de <see cref="MotivoNoCompleta"/> como enumerado.</summary>
    public MotivoDeNoCompletar Motivo => LeerMotivo(MotivoNoCompleta);

    /// <summary>Lectura de <see cref="MotivoDelCompanero"/> como enumerado.</summary>
    public MotivoDeNoCompletar MotivoQueDijoElCompanero => LeerMotivo(MotivoDelCompanero);

    /// <summary>Traduce el texto guardado a enumerado sin lanzar nunca: lo que no se reconoce es SinMarcar.</summary>
    /// <param name="texto">Lo que hay en la columna, tal cual; espacios y mayúsculas no cambian la lectura.</param>
    /// <returns>El estado, o <see cref="EstadoDeRecomendacion.SinMarcar"/> para nulo, vacío o cualquier valor que no sea <c>completa</c> ni <c>no_completa</c> (los del Python viejo, como «incompleta», caen aquí).</returns>
    public static EstadoDeRecomendacion LeerEstado(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "completa" => EstadoDeRecomendacion.Completa,
        "no_completa" => EstadoDeRecomendacion.NoCompleta,
        _ => EstadoDeRecomendacion.SinMarcar,
    };

    /// <summary>Traduce el enumerado al texto que se escribe en la columna.</summary>
    /// <param name="estado">El estado que se quiere guardar.</param>
    /// <returns><c>completa</c>, <c>no_completa</c>, o nulo para <see cref="EstadoDeRecomendacion.SinMarcar"/>: sin marcar es la columna a NULL, no una palabra.</returns>
    public static string? EscribirEstado(EstadoDeRecomendacion estado) => estado switch
    {
        EstadoDeRecomendacion.Completa => "completa",
        EstadoDeRecomendacion.NoCompleta => "no_completa",
        _ => null,
    };

    /// <summary>Traduce la clave guardada a enumerado sin lanzar nunca; lo raro es SinMotivo.</summary>
    /// <remarks>
    /// El esquema tiene lista cerrada en estas dos columnas, asi que un valor desconocido
    /// no deberia poder existir. Se contempla igual: una lectura que lanza deja la
    /// pantalla en blanco, y aqui lo que se pierde de vista es un documento.
    /// </remarks>
    /// <param name="texto">La clave guardada en <c>motivo_no_completa</c> o <c>motivo_del_companero</c>, tal cual.</param>
    /// <returns>El motivo, o <see cref="MotivoDeNoCompletar.SinMotivo"/> para nulo, vacío o clave desconocida.</returns>
    public static MotivoDeNoCompletar LeerMotivo(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "no_se_pudo_comunicar" => MotivoDeNoCompletar.NoSePudoComunicar,
        "el_lider_no_lo_hizo" => MotivoDeNoCompletar.ElLiderNoLoHizo,
        "otra_razon" => MotivoDeNoCompletar.OtraRazon,
        _ => MotivoDeNoCompletar.SinMotivo,
    };

    /// <summary>Traduce el enumerado a la CLAVE que se escribe en la columna, nunca a prosa.</summary>
    /// <remarks>
    /// La prosa —«No se pudo comunicar con el lider»— es de la pantalla. Guardando la
    /// clave, cambiar la redaccion no obliga a migrar datos.
    /// </remarks>
    /// <param name="motivo">El motivo que se quiere guardar.</param>
    /// <returns>La clave con guiones bajos que admite el <c>CHECK</c> de la columna, o nulo para <see cref="MotivoDeNoCompletar.SinMotivo"/>.</returns>
    public static string? EscribirMotivo(MotivoDeNoCompletar motivo) => motivo switch
    {
        MotivoDeNoCompletar.NoSePudoComunicar => "no_se_pudo_comunicar",
        MotivoDeNoCompletar.ElLiderNoLoHizo => "el_lider_no_lo_hizo",
        MotivoDeNoCompletar.OtraRazon => "otra_razon",
        _ => null,
    };
}
