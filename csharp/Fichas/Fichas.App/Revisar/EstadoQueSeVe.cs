using Fichas.App.Asignar;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Revisar;

/// <summary>
/// Lo que una tarjeta de Revisar dice de sí misma de un vistazo: su color y su palabra.
/// </summary>
/// <remarks>
/// <para>No es el estado de la base (<see cref="EstadoDeRecomendacion"/>) y por eso tiene nombre
/// propio: son <b>cuatro</b> y no tres, porque el cuarto no está en ninguna columna. Un archivado
/// con la fecha de viaje pasada se lee distinto de un incompleto vivo aunque en la base los dos
/// digan <c>no_completa</c>.</para>
///
/// <para>El estado de la base NO se toca: lo que cambia es cómo se lee (ver
/// <see cref="EstadosQueSeVen.DeLaTarjeta"/>).</para>
/// </remarks>
public enum EstadoQueSeVe
{
    /// <summary>Nadie ha dicho nada todavía de este documento.</summary>
    SinRevisar = 0,

    /// <summary>El Excel del compañero dijo que no está completa, y sigue vivo.</summary>
    NoCompleta = 1,

    /// <summary>El Excel del compañero la dio por completa.</summary>
    Completa = 2,

    /// <summary>Archivado y con la fecha de viaje ya pasada: viajó y él ya lo resolvió.</summary>
    FechaPasadaCompletada = 3,
}

/// <summary>
/// De qué estado se ve cada tarjeta, qué palabra lleva y de qué claves salen sus colores.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe el cuarto, con las palabras del dueño del 2026-09-06:</b> <i>«Si yo lo
/// archivo debe desaparecer aunque no estén completos, porque a veces lo archivo porque son
/// fechas pasadas y ya los completé antes de crear el sistema»</i>, y seguido: <i>«Debe decir
/// fecha pasada completada»</i>. La primera frase explica la segunda. Él archiva casos que están
/// incompletos <b>en la base</b> y completos <b>en la vida real</b>: gente que viajó antes de que
/// existiera este programa y cuyo trámite él ya resolvió a mano. Para esos, «incompleto» no
/// significa «falta trabajo»: significa «el programa no lo vio», y llamarlos incompletos es decir
/// de ellos algo que no es verdad.</para>
///
/// <para>⛔ <b>El color nunca va solo</b> (mockup v2, y el comentario de
/// <see cref="RenglonParaAsignar.PalabraDe"/>): cada estado trae SIEMPRE su palabra, y por eso
/// <see cref="PalabraDe"/> vive al lado de las claves de color y no en otro archivo. Un color sin
/// palabra deja la tarjeta muda para quien no distingue esos colores o mira una pantalla mala, y
/// este programa decide si alguien puede entrar al templo.</para>
///
/// <para>Está aquí y no en la página porque en la página no se podría probar: los colores se
/// declaran en <c>PaginaDeRevisar.xaml</c> con las claves que da esta clase, y
/// <c>PruebasDelColorDeLaTarjeta</c> mide su contraste leyéndolas de aquí.</para>
/// </remarks>
public static class EstadosQueSeVen
{
    /// <summary>
    /// De qué estado se ve una tarjeta, a partir de lo que hay en la base.
    /// </summary>
    /// <remarks>
    /// <para><b>Archivado más fecha pasada mandan sobre el estado</b>, sea cual sea: es la
    /// decisión del dueño del 2026-09-06 («Archivar gana a estar incompleto»). Un archivado que
    /// ya viajó y que estaba marcado completa tampoco pierde nada, porque su firma sigue debajo
    /// diciendo quién lo marcó y cuándo (<see cref="TarjetaDeDocumento.Firma"/>).</para>
    ///
    /// <para>⚠️ Un archivado que <b>todavía no ha viajado</b> se queda con su estado de siempre.
    /// El dueño habló de los que archiva <i>porque son fechas pasadas</i>; decir de uno que aún
    /// no viajó que está completado sería afirmar algo que nadie ha dicho.</para>
    /// </remarks>
    /// <param name="estado">Lo que dice <c>casos.estado_recomendacion</c>.</param>
    /// <param name="archivado">Si el caso está archivado.</param>
    /// <param name="fechaYaPasada">Si la fecha de viaje ya pasó.</param>
    public static EstadoQueSeVe DeLaTarjeta(EstadoDeRecomendacion estado, bool archivado, bool fechaYaPasada)
    {
        if (archivado && fechaYaPasada) return EstadoQueSeVe.FechaPasadaCompletada;

        return estado switch
        {
            EstadoDeRecomendacion.Completa => EstadoQueSeVe.Completa,
            EstadoDeRecomendacion.NoCompleta => EstadoQueSeVe.NoCompleta,
            _ => EstadoQueSeVe.SinRevisar,
        };
    }

    /// <summary>La palabra en español de cada estado que se ve; el color nunca va solo.</summary>
    /// <remarks>
    /// Las tres primeras son las mismas de <see cref="RenglonParaAsignar.PalabraDe"/> y salen de
    /// allí a propósito: la misma cosa no puede llamarse de dos maneras en dos pantallas. La
    /// cuarta es la que pidió el dueño con esas palabras exactas: «fecha pasada completada».
    /// </remarks>
    public static string PalabraDe(EstadoQueSeVe estado) => estado switch
    {
        EstadoQueSeVe.Completa => RenglonParaAsignar.PalabraDe(EstadoDeRecomendacion.Completa),
        EstadoQueSeVe.NoCompleta => RenglonParaAsignar.PalabraDe(EstadoDeRecomendacion.NoCompleta),
        EstadoQueSeVe.FechaPasadaCompletada => "fecha pasada completada",
        _ => RenglonParaAsignar.PalabraDe(EstadoDeRecomendacion.SinMarcar),
    };

    /// <summary>La clave del pincel del fondo de la pastilla de ese estado.</summary>
    /// <remarks>
    /// Se compone del nombre del estado y no se escribe a mano cuatro veces: así, añadir un
    /// estado nuevo sin declarar sus dos colores pone roja
    /// <c>PruebasDelColorDeLaTarjeta.CadaEstadoTraeSuFondoYSuTintaEnLosDosTemas</c> en vez de
    /// dejar una pastilla pintada del color de otra.
    /// </remarks>
    public static string ClaveDelFondo(EstadoQueSeVe estado) => $"FondoDelEstado{estado}";

    /// <summary>La clave del pincel de la letra y el borde de la pastilla de ese estado.</summary>
    public static string ClaveDeLaTinta(EstadoQueSeVe estado) => $"TintaDelEstado{estado}";
}
