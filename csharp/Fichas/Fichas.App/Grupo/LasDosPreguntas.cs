using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Grupo;

/// <summary>
/// Las DOS preguntas que el programa hace sobre el mismo papel, con sus palabras, en un
/// solo sitio.
/// </summary>
/// <remarks>
/// <para><b>Por que existe este archivo.</b> El dueno abrio el programa el 2026-09-05 y
/// dijo <i>«el programa es confuso, muy confuso»</i>. La confusion tiene nombre y esta
/// medida en el ADR-0006 §1.3: <b>la pantalla ensena una pregunta y el lee la otra</b>.</para>
///
/// <list type="table">
///   <listheader><term>Pregunta</term><description>Quien la contesta y de donde sale</description></listheader>
///   <item>
///     <term><b>Listo para asignar</b></term>
///     <description>«¿tenemos los datos suficientes para mandarle este documento a un
///     agente?». <b>La contesta el programa solo</b>, mirando NUESTROS campos
///     (<see cref="LoQueLeFalta"/>). Es lo que el pidio con estas palabras el 2026-09-05:
///     <i>«Si el sistema escanea y verifica todos los campos sin mi intervención, debe decir
///     "listo para asignar"»</i>.</description>
///   </item>
///   <item>
///     <term><b>Listo para viajar</b></term>
///     <description>«¿esta confirmada la recomendacion de ESTA PERSONA en el sistema del
///     obispo?». <b>La contesta una persona</b> —Miguel, un agente o un peldano superior—, y
///     sale de las seis preguntas de <see cref="Pasos"/>. Sus palabras:
///     <i>«No puede poner los PDF listos para viajar porque no se ha verificado la
///     recomendación en el sistema del obispo, que es lo que realmente verifico yo»</i>.</description>
///   </item>
/// </list>
///
/// <para>⛔ <b>Ninguna de las dos borra a la otra, y esto no es una opinion de quien
/// programa.</b> Las dos frases son suyas y son del mismo dia. Quitar la primera le
/// devolveria el trabajo que este programa le quita: con 3 000 documentos, mirar campo por
/// campo cuales se pueden mandar a un agente.</para>
///
/// <para><b>Que NO hace este archivo:</b> no escribe ni una fila y no firma nada. La firma
/// de campos —<c>procedencia_campo.verificado</c>— es de Miguel y nunca automatica (regla
/// permanente 5), y el estado del documento —<c>casos.estado_recomendacion</c>— lo escribe
/// el Excel del companero con su nombre. Aqui solo hay PALABRAS y una lectura.</para>
/// </remarks>
public static class LasDosPreguntas
{
    // ────────────────────────── PREGUNTA 1 · la contesta el programa ──────────────────────────

    /// <summary>
    /// Que le falta a un documento, en una frase: listo, cuantos datos, o que no hay nadie.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Se compone AQUI y no en cada pantalla</b>, y ese es el arreglo del
    /// 2026-09-07. El dueno dijo <i>«cuando guardo información ya corregida no cambia de
    /// estado, sigue igual»</i>, y lo medido con la ventana abierta fue que el veredicto
    /// estaba bien —Inicio contaba 71 de 75 y el pie de Correccion lo decia al guardar— y lo
    /// que no cambiaba era el renglon de las listas, que nunca llevo esta frase. Con una sola
    /// composicion, las dos pantallas no pueden decir cosas distintas del mismo documento.</para>
    ///
    /// <para>⛔ Y esto NO firma nada ni es un estado de la base: es una lectura, la respuesta a
    /// «¿le falta algo a este documento?». No hay columna que lo guarde.</para>
    ///
    /// <para>⛔ <b>2026-09-07:</b> devolvia «listo para asignar · el sistema llenó todos los
    /// campos», «le faltan 3 datos» o «sin ninguna persona leída · no hay a quién recomendar».
    /// La primera era una de las cuatro palabras que el dueno retiro, asi que la composicion se
    /// mudo a <c>Fichas.App.Vocabulario.LoQueSeLeeDeUnDocumento</c> y esta funcion delega en
    /// ella. <b>Se queda en vez de borrarse</b> porque es el punto por el que pasan las DOS
    /// pantallas —el grupo del dia y <c>LoQueLeFaltaACadaDocumento</c>—, y lo vigila
    /// <c>PruebasDeLoQueLeFaltaACadaDocumento.ContestaLoMismoQueLaPantallaDelGrupoEnTodaLaBase</c>:
    /// con dos composiciones, el mismo documento se leia distinto en cada pantalla.</para>
    /// </remarks>
    /// <param name="cuantoLeFalta">Cuantos datos le faltan, contados por <see cref="LoQueLeFalta"/>.</param>
    /// <param name="sinNingunaPersonaLeida">Si el documento no trae ni una persona.</param>
    public static string LoQueLeFaltaAlDocumento(int cuantoLeFalta, bool sinNingunaPersonaLeida)
        => Fichas.App.Vocabulario.LoQueSeLeeDeUnDocumento.De(
            Fichas.Contratos.Modelos.EstadoDeRecomendacion.SinMarcar,
            archivado: false,
            cuantoLeFalta,
            sinNingunaPersonaLeida,
            quienLoLleva: string.Empty,
            firma: string.Empty).Detalle;

    // ────────────────────────── PREGUNTA 2 · la contesta una persona ──────────────────────────

    /// <summary>Lo que se dice de una persona cuyas seis preguntas estan todas en si.</summary>
    public const string ListaParaViajar = "lista para viajar";

    /// <summary>
    /// Lo que se dice de una persona con alguna pregunta en blanco y ninguna en no.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>«Sin mirar» NO es «no lista»</b>, y las dos no se pintan igual. Lo dice el
    /// comentario de <see cref="Pasos.Estado"/>: <i>«dar por lista a una persona de la que
    /// faltan preguntas por mirar es exactamente lo que manda a alguien al templo con la
    /// recomendacion mal»</i>. Una es una respuesta y la otra es la ausencia de respuesta.
    /// </remarks>
    public const string SinMirar = "sin mirar";

    /// <summary>
    /// El estado de UNA persona: si, no, o nada si todavia no se sabe.
    /// </summary>
    /// <remarks>
    /// Es <see cref="Pasos.Estado"/> y no una regla nueva: la regla vive en un solo sitio
    /// para poder cambiarla de un sitio el dia que el dueno decida si basta la sexta
    /// pregunta (decision suya n.º 2 del ADR-0006 §2.6). Una persona sin leer —un documento
    /// del que no se saco a nadie— es <c>null</c>: nadie la miro.
    /// </remarks>
    /// <param name="persona">La persona, o nulo si el documento no trajo ninguna.</param>
    public static bool? EstadoDe(Persona? persona) => persona is null ? null : Pasos.Estado(persona);

    /// <summary>En que pasos se quedo esa persona; vacio si no hay ninguno en no.</summary>
    /// <param name="persona">La persona, o nulo si el documento no trajo ninguna.</param>
    public static IReadOnlyList<string> SeQuedoEn(Persona? persona)
        => persona is null ? [] : Pasos.SinCompletar(persona);

    /// <summary>
    /// La linea entera de una persona: como esta, como esta su recomendacion y, si no esta
    /// lista, en que paso se quedo.
    /// </summary>
    /// <remarks>
    /// <para>El paso donde se quedo es el criterio C18-3 y sale de <see cref="Pasos.SinCompletar"/>,
    /// ya construido: es lo que el dueno le dice al lider por telefono. Una linea, nunca un
    /// parrafo.</para>
    ///
    /// <para>⛔ <b>2026-09-07:</b> devolvia «no lista para viajar · recomendación sin confirmar ·
    /// se quedó en Entrevistas», con TRES de las cuatro palabras que el dueno retiro. La
    /// composicion se mudo a <c>Fichas.App.Vocabulario.LoQueSeLeeDeUnaPersona</c> y esta funcion
    /// delega en ella. <b>Se queda en vez de borrarse</b> porque por aqui pasan CUATRO
    /// pantallas —Inicio, el grupo del dia, Correccion y la ventana de las seis preguntas de
    /// Revisar—, y componer la frase en cuatro sitios es como empieza que el mismo renglon diga
    /// dos cosas distintas de la misma persona.</para>
    ///
    /// <para>⚠️ Y el paso donde se quedo SIGUE saliendo: es lo unico que el dueno le dice al
    /// lider por telefono, y ahora va dentro del detalle en vez de al final de la linea.</para>
    /// </remarks>
    /// <param name="estado">Lo que dicen sus seis preguntas.</param>
    /// <param name="seQuedoEn">Los pasos marcados que NO, sin el numero de delante.</param>
    public static string FraseDeUnaPersona(bool? estado, IReadOnlyList<string>? seQuedoEn)
    {
        var lectura = Fichas.App.Vocabulario.LoQueSeLeeDeUnaPersona.De(estado, seQuedoEn);
        return $"{lectura.Palabra} · {lectura.Detalle}";
    }
}
