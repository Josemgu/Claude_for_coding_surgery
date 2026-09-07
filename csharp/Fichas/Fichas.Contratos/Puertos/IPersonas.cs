using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Las seis preguntas de «Preparación para las ordenanzas» del sistema del líder,
/// contestadas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tres respuestas y no dos.</b> <c>true</c> es si, <c>false</c> es no, y <b>nulo es
/// una pregunta que nadie miro</b>, que no es lo mismo que «no»: dar por lista a una
/// persona de la que faltan preguntas es exactamente lo que manda a alguien al templo con
/// la recomendacion mal. Y por eso mismo se puede volver a dejar en blanco una ya
/// contestada (criterio C19-6): si contestar fuera irreversible, nadie se atreveria.
/// </para>
/// <para>
/// ⚠️ <b>NO son las seis <c>Ord*</c> de <see cref="Modelos.Persona"/>.</b> Aquellas dicen
/// a QUE va la persona al templo, leidas del papel; estas dicen si esta en condiciones de
/// ir, y salen de la pantalla del lider.
/// </para>
/// <para>
/// ⚠️ <b>Y no incluye <c>llamo_al_lider</c>, que no es un septimo paso.</b> Quien llame a
/// <see cref="IPersonas.ResponderLosPasos"/> contesta seis cosas y solo seis; lo que dejo
/// escrito el companero por su Excel se queda donde estaba.
/// </para>
/// </remarks>
/// <param name="Preparacion">1. Preparación.</param>
/// <param name="Informacion">2. Información.</param>
/// <param name="CitaDelTemplo">3. Cita del templo.</param>
/// <param name="AccionesRequeridas">4. Acciones requeridas.</param>
/// <param name="Entrevistas">5. Entrevistas.</param>
/// <param name="ListoParaElTemplo">6. Listo para el templo.</param>
public sealed record RespuestaALosPasos(
    bool? Preparacion,
    bool? Informacion,
    bool? CitaDelTemplo,
    bool? AccionesRequeridas,
    bool? Entrevistas,
    bool? ListoParaElTemplo)
{
    /// <summary>Las seis sin contestar, que es como llegan todas.</summary>
    public static RespuestaALosPasos SinContestar { get; } = new(null, null, null, null, null, null);
}

/// <summary>
/// Quién contestó las seis preguntas de una persona, cuándo y desde dónde.
/// </summary>
/// <remarks>
/// <para>
/// Son las tres columnas de la migracion 19 —<c>pasos_por</c>, <c>pasos_en</c>,
/// <c>pasos_origen</c>—, y existen por lo mismo que las de la 14: <i>confiar sin saber de
/// quien te fias es otra cosa</i>. <c>Origen</c> es texto libre y sin catalogo: ahi caben
/// la ruta del Excel que trajo las respuestas, «a mano en la pantalla» y «el administrador
/// lo hizo», y manana una cuarta via sin migrar nada.
/// </para>
/// <para>
/// ⛔ <b>No es la firma de campos.</b> <c>procedencia_campo.verificado</c> es de Miguel,
/// campo por campo, y nunca automatica (regla permanente 5). Son dos cosas y no se mezclan.
/// </para>
/// <para>
/// ⚠️ <b>Tampoco es <c>propuesto_por</c> / <c>propuesto_en</c>.</b> Aquellas son del
/// <c>estado_propuesto</c> que escribe el Excel del companero. Pueden venir de personas
/// distintas el mismo dia, y con un solo par de columnas la segunda respuesta pisaria el
/// nombre de la primera sin dejar rastro.
/// </para>
/// </remarks>
/// <param name="Por">El companero que contesto, o nulo si nadie ha contestado todavia.</param>
/// <param name="En">Cuando, en ISO-8601; nulo si no hay respuesta.</param>
/// <param name="Origen">Por que via se contesto; nulo si no hay respuesta.</param>
public sealed record FirmaDeLosPasos(long? Por, string? En, string? Origen)
{
    /// <summary>La firma de una persona de la que nadie ha contestado nada.</summary>
    public static FirmaDeLosPasos SinFirmar { get; } = new(null, null, null);

    /// <summary>Si alguien contesto ya las seis preguntas de esa persona.</summary>
    public bool YaContesto => Por is not null;
}

/// <summary>Todo lo que se puede hacer con las personas de los formularios.</summary>
/// <remarks>
/// ⚠️ <b>Los dos tipos de arriba viven en este archivo y no en <c>Modelos</c>.</b> No es la
/// carpeta que les tocaria: estan aqui porque <c>Fichas.Contratos</c> esta congelado salvo
/// este archivo, y moverlos es un cambio de una linea el dia que se descongele.
/// </remarks>
public interface IPersonas
{
    /// <summary>Devuelve un trozo de la lista de personas que cumplen el filtro, con el total detras.</summary>
    PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo);

    /// <summary>Cuenta cuantas personas cumplen el filtro, sin traerlas.</summary>
    int Contar(FiltroDePersonas filtro);

    /// <summary>Devuelve una persona por su id, o nulo si no esta.</summary>
    Persona? Obtener(long id);

    /// <summary>Devuelve todas las personas de un caso, que nunca son muchas.</summary>
    IReadOnlyList<Persona> DeCaso(long casoId);

    /// <summary>Guarda una persona nueva o cambia una existente; un MRN corto entra y sale avisado.</summary>
    ResultadoDeEscritura Guardar(Persona persona);

    /// <summary>Anota lo que el companero propuso sobre una persona, con su firma; no verifica nada.</summary>
    ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId);

    /// <summary>
    /// Contesta las seis preguntas del sistema del líder de UNA persona y firma quién,
    /// cuándo y desde dónde. No firma ningún campo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Escribe seis columnas y tres, y ni una mas.</b> No toca <c>estado_propuesto</c>,
    /// <c>nota_companero</c>, <c>propuesto_por</c>, <c>propuesto_en</c> ni
    /// <c>llamo_al_lider</c>: eso es lo que dejo escrito el companero por su Excel, es suyo
    /// y lleva su nombre. Lo unico que pasa de manos son las seis respuestas, que no son la
    /// opinion de nadie —son lo que pone en la pantalla del lider— y por eso vale la ultima.
    /// </para>
    /// <para>
    /// <b>Toca UNA persona.</b> Un documento puede traer diez y cada una es un ticket
    /// aparte: tres pueden estar listas y siete no (ADR-0006 §2.1).
    /// </para>
    /// <para>
    /// ⛔ <b>El estado NO se guarda.</b> Si esa persona esta lista para viajar se deriva de
    /// sus seis respuestas cada vez que se pregunta. Un veredicto guardado puede
    /// contradecir a su evidencia; uno derivado no puede.
    /// </para>
    /// </remarks>
    /// <param name="personaId">De quien se contestan las seis.</param>
    /// <param name="respuesta">Las seis, cada una en si, no o en blanco.</param>
    /// <param name="companeroId">Quien las contesta; nunca se inventa un nombre.</param>
    /// <param name="origen">Por que via; texto libre, sin catalogo.</param>
    ResultadoDeEscritura ResponderLosPasos(
        long personaId, RespuestaALosPasos respuesta, long companeroId, string origen);

    /// <summary>
    /// La firma de las seis preguntas de cada persona de un caso, por su número interno.
    /// </summary>
    /// <remarks>
    /// Va por CASO y no por persona porque la pantalla las pinta todas juntas: una consulta
    /// para diez personas en vez de diez. Toda persona del caso sale en el resultado; la que
    /// nadie contesto todavia sale con <see cref="FirmaDeLosPasos.SinFirmar"/>, y no
    /// ausente, para que quien pinte no tenga que distinguir «no esta» de «no contestada».
    /// </remarks>
    IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId);
}
