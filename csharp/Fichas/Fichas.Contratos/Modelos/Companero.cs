namespace Fichas.Contratos.Modelos;

/// <summary>
/// Quien verifica. Tabla <c>companeros</c>, 7 columnas (ARQUITECTURA §2.4).
/// Se desactivan, no se borran. Miguel es una fila de aqui como cualquier otro.
/// </summary>
/// <remarks>
/// <b>El rol y la categoria son dos ejes y no uno.</b> El rol dice QUE ES esa persona y
/// tiene lista cerrada; la categoria dice EN QUE PELDANO esta y no tiene tope arriba,
/// porque los peldanos los pone el dueno: <i>«los agentes categoria 1 no pudieron
/// comunicarse con los lideres, debo pasarlo a los agentes de categoria 2... Asi puedes
/// agregarle a los gerentes categoria y a los agentes categorias»</i> (2026-09-05).
/// </remarks>
public sealed record Companero
{
    /// <summary>Clave primaria; 0 mientras no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>Su nombre; NO es unico, dos personas pueden llamarse igual.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Si sigue activo. Un companero desactivado no recibe casos nuevos.</summary>
    public bool Activo { get; init; } = true;

    /// <summary>Cuando se desactivo, ISO-8601; va con <see cref="Activo"/> en falso o ninguna.</summary>
    public string? DesactivadoEn { get; init; }

    /// <summary>Cuando se dio de alta, ISO-8601.</summary>
    public string CreadoEn { get; init; } = string.Empty;

    /// <summary>Que es: companero, gerente o administrador. Nadie cambia de rol solo.</summary>
    public RolDeCompanero Rol { get; init; } = RolDeCompanero.Companero;

    /// <summary>
    /// El peldano de la escalera, empezando en 1. Sin tope arriba: lo pone el dueno.
    /// </summary>
    public int Categoria { get; init; } = 1;

    /// <summary>Traduce la clave guardada a enumerado sin lanzar nunca; lo raro es companero.</summary>
    /// <param name="texto">Lo que hay en <c>companeros.rol</c>, tal cual.</param>
    /// <returns><see cref="RolDeCompanero.Gerente"/> o <see cref="RolDeCompanero.Administrador"/> si la clave lo dice; <see cref="RolDeCompanero.Companero"/> para nulo, vacío o cualquier otra cosa.</returns>
    public static RolDeCompanero LeerRol(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "gerente" => RolDeCompanero.Gerente,
        "administrador" => RolDeCompanero.Administrador,
        _ => RolDeCompanero.Companero,
    };

    /// <summary>Traduce el enumerado a la clave que admite el <c>CHECK</c> de la columna.</summary>
    /// <param name="rol">El rol que se quiere guardar.</param>
    /// <returns>Siempre una de las tres claves; nunca nulo, porque la columna tampoco lo admite.</returns>
    public static string EscribirRol(RolDeCompanero rol) => rol switch
    {
        RolDeCompanero.Gerente => "gerente",
        RolDeCompanero.Administrador => "administrador",
        _ => "companero",
    };
}
