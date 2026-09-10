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

    /// <summary>El rotulo de la primera pregunta tal como se lee dentro de una linea.</summary>
    public const string ListoParaAsignar = "listo para asignar";

    /// <summary>El mismo rotulo cuando encabeza un recuadro o una cifra.</summary>
    public const string ListoParaAsignarEnCabecera = "Listo para asignar";

    /// <summary>
    /// Que significa «listo para asignar», con las palabras del dueno.
    /// </summary>
    /// <remarks>
    /// Va SIEMPRE pegado al rotulo (criterio C17-1). Sin esta frase, «listo» a secas se lee
    /// como «listo para viajar», que es justo lo que el dijo que el programa hacia mal.
    /// </remarks>
    public const string QueSignificaListoParaAsignar = "el sistema llenó todos los campos";

    /// <summary>«listo para asignar · el sistema llenó todos los campos».</summary>
    public static string ListoParaAsignarConSuSignificado
        => $"{ListoParaAsignar} · {QueSignificaListoParaAsignar}";

    /// <summary>«Listo para asignar · el sistema llenó todos los campos», para una cabecera.</summary>
    public static string ListoParaAsignarEnCabeceraConSuSignificado
        => $"{ListoParaAsignarEnCabecera} · {QueSignificaListoParaAsignar}";

    /// <summary>Lo que se dice de un documento del que el lector no saco a nadie.</summary>
    /// <remarks>
    /// ⚠️ <b>No es un campo que falte</b>, y por eso tiene frase propia: con los cinco campos
    /// del caso perfectos la cuenta de campos es 0, y decir «le faltan 0 datos» de un
    /// documento que no esta listo son dos cifras de la misma pantalla que no encajan. Desde
    /// el 2026-09-07 tambien deja de ser un callejon sin salida: el nombre y la cedula se
    /// pueden escribir a mano en Correccion (<c>ModeloDeCorreccion.AnadirUnaPersonaAMano</c>).
    /// </remarks>
    public const string SinNingunaPersonaLeida = "sin ninguna persona leída";

    /// <summary>El mismo rotulo cuando encabeza una linea.</summary>
    public const string SinNingunaPersonaLeidaEnCabecera = "Sin ninguna persona leída";

    /// <summary>Por que eso detiene al documento, con las palabras de la pantalla.</summary>
    public const string QueSignificaSinNingunaPersonaLeida = "no hay a quién recomendar";

    /// <summary>«sin ninguna persona leída · no hay a quién recomendar».</summary>
    public static string SinNingunaPersonaLeidaConSuSignificado
        => $"{SinNingunaPersonaLeida} · {QueSignificaSinNingunaPersonaLeida}";

    /// <summary>«Sin ninguna persona leída · no hay a quién recomendar», para una cabecera.</summary>
    public static string SinNingunaPersonaLeidaEnCabeceraConSuSignificado
        => $"{SinNingunaPersonaLeidaEnCabecera} · {QueSignificaSinNingunaPersonaLeida}";

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

    /// <summary>Lo que se dice de una persona con alguna de las seis en no.</summary>
    public const string NoListaParaViajar = "no lista para viajar";

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

    /// <summary>La frase del dueno para lo que todavia no esta cerrado en el sistema del obispo.</summary>
    /// <remarks>Literal suya: <i>«la recomendación para el templo no está confirmada»</i>.</remarks>
    public const string RecomendacionSinConfirmar = "recomendación sin confirmar";

    /// <summary>Lo contrario, dicho con las mismas palabras para que se lean como pareja.</summary>
    public const string RecomendacionConfirmada = "recomendación confirmada";

    /// <summary>
    /// El estado de UNA persona: si, no, o nada si todavia no se sabe.
    /// </summary>
    /// <remarks>
    /// Es <see cref="Pasos.Estado"/> y no una regla nueva: la regla vive en un solo sitio
    /// para poder cambiarla de un sitio el dia que el dueno decida si basta la sexta
    /// pregunta (decision suya n.º 2 del ADR-0006 §2.6). Una persona sin leer —un documento
    /// del que no se saco a nadie— es <c>null</c>: nadie la miro.
    /// </remarks>
    public static bool? EstadoDe(Persona? persona) => persona is null ? null : Pasos.Estado(persona);

    /// <summary>En que pasos se quedo esa persona; vacio si no hay ninguno en no.</summary>
    public static IReadOnlyList<string> SeQuedoEn(Persona? persona)
        => persona is null ? [] : Pasos.SinCompletar(persona);

    /// <summary>«lista para viajar», «no lista para viajar» o «sin mirar».</summary>
    public static string Decir(bool? estado) => estado switch
    {
        true => ListaParaViajar,
        false => NoListaParaViajar,
        _ => SinMirar,
    };

    /// <summary>«recomendación confirmada» solo cuando las seis dicen que si.</summary>
    public static string DecirLaRecomendacion(bool? estado)
        => estado == true ? RecomendacionConfirmada : RecomendacionSinConfirmar;

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

    /// <summary>
    /// «Entrevistas», «Entrevistas y Preparación», «Entrevistas, Preparación y Cita del templo».
    /// </summary>
    /// <remarks>
    /// Se enumeran TODOS y no solo el primero: el dueno llama al obispo una vez y le dice
    /// todo lo que falta. Como mucho son seis, asi que la linea no se dispara.
    /// </remarks>
    private static string Enumerar(IReadOnlyList<string> nombres)
    {
        if (nombres.Count == 1) return nombres[0];
        var todosMenosElUltimo = string.Join(", ", nombres.Take(nombres.Count - 1));
        return $"{todosMenosElUltimo} y {nombres[^1]}";
    }
}
