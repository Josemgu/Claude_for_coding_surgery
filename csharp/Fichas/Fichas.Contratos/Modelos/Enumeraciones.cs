namespace Fichas.Contratos.Modelos;

/// <summary>Estado de la recomendacion de un caso, tal como lo entiende el programa.</summary>
/// <remarks>
/// El esquema guarda TEXTO libre sin CHECK de lista (docs/ARQUITECTURA.md §2.2), y eso
/// no se cambia aqui: el texto crudo sigue viviendo en <see cref="Caso.EstadoRecomendacion"/>.
/// Este enumerado es solo la lectura del programa. Un texto que no se reconoce cae en
/// <see cref="SinMarcar"/> y deja aviso; nunca se pierde ni tumba nada (requisito 9).
/// </remarks>
public enum EstadoDeRecomendacion
{
    /// <summary>Nadie ha dicho nada todavia sobre este caso.</summary>
    SinMarcar = 0,

    /// <summary>El Excel del companero la marco completa, con su nombre.</summary>
    Completa = 1,

    /// <summary>El Excel del companero la marco no completa, con su nombre.</summary>
    NoCompleta = 2,
}

/// <summary>Por que una recomendacion no esta completa (columnas <c>casos.motivo_*</c>).</summary>
/// <remarks>
/// <para>
/// Son los dos estados que el dueno pidio ver en el calendario el 2026-09-05 —<i>«no se
/// pudo comunicar con el lider, o el lider no lo hizo»</i>— mas un cajon para lo demas.
/// El tercero que nombro, «no completado», NO esta aqui: ese es
/// <see cref="EstadoDeRecomendacion.NoCompleta"/>, y son dos ejes distintos. Un caso que
/// no esta completo porque no se pudo hablar con el lider SIGUE estando no completo
/// (ADR-0005 §3).
/// </para>
/// <para>
/// El esquema SI tiene lista cerrada en estas dos columnas, al reves que en
/// <c>estado_recomendacion</c>: aqui no hay texto que venga del OCR ni del papel, solo
/// lo que alguien elige en un menu, asi que un valor fuera de la lista es un defecto del
/// programa y no un dato mal leido que haya que conservar.
/// </para>
/// </remarks>
public enum MotivoDeNoCompletar
{
    /// <summary>Nadie ha dicho todavia por que; es el nulo de la columna.</summary>
    SinMotivo = 0,

    /// <summary>Se intento y no se consiguio hablar con el lider.</summary>
    NoSePudoComunicar = 1,

    /// <summary>Se hablo con el lider y el lider no lo hizo.</summary>
    ElLiderNoLoHizo = 2,

    /// <summary>Otra razon; la explica la nota del companero, no esta lista.</summary>
    OtraRazon = 3,
}

/// <summary>Que clase de persona es un companero (columna <c>companeros.rol</c>).</summary>
/// <remarks>
/// El rol NO es el peldano de la escalera: eso es <see cref="Companero.Categoria"/>, un
/// numero cuyos topes pone el dueno. El rol solo contesta una pregunta que la categoria
/// no puede contestar: quien es el administrador, para que «el administrador lo hizo» no
/// se firme adivinando (ADR-0005 §6.3).
/// </remarks>
public enum RolDeCompanero
{
    /// <summary>Quien recibe paquetes y habla con los lideres. El defecto de la columna.</summary>
    Companero = 0,

    /// <summary>Quien se ocupa de lo que un companero no consiguio. Es un peldano, no otra tabla.</summary>
    Gerente = 1,

    /// <summary>Miguel: el unico que puede completar un caso sin pasar por la verificacion.</summary>
    Administrador = 2,
}

/// <summary>De donde salio el valor de un campo (columna <c>procedencia_campo.origen</c>).</summary>
public enum OrigenDeCampo
{
    /// <summary>Lo escribio una anotacion del propio PDF; confianza 1,0.</summary>
    Anotacion = 0,

    /// <summary>Lo leyo el OCR de la imagen rasterizada.</summary>
    Ocr = 1,

    /// <summary>El OCR miro y no leyo nada.</summary>
    Vacio = 2,

    /// <summary>Lo tecleo Miguel a mano en la pantalla de correccion.</summary>
    Manual = 3,
}

/// <summary>A que tabla apunta una fila de procedencia (columna <c>procedencia_campo.tabla</c>).</summary>
public enum TablaDeProcedencia
{
    /// <summary>La fila describe un campo de <c>casos</c>.</summary>
    Casos = 0,

    /// <summary>La fila describe un campo de <c>personas</c>.</summary>
    Personas = 1,
}

/// <summary>Cuanto pesa un aviso; decide el color de la franja, nunca si la accion sigue.</summary>
public enum GravedadDeAviso
{
    /// <summary>Solo informa: la operacion salio bien.</summary>
    Informacion = 0,

    /// <summary>Algo no tiene la forma esperada y se guardo igual, senalado.</summary>
    Advertencia = 1,

    /// <summary>Algo no se pudo hacer. Se dice; no se lanza excepcion ni se abre cuadro.</summary>
    Problema = 2,
}
