using Fichas.App.Correccion;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Revisar;

/// <summary>
/// Guardar las seis preguntas de UNA persona, con quién las contestó y desde dónde.
/// </summary>
/// <remarks>
/// <para>
/// Sin ventana a propósito: lo que se escribe en la base y lo que se le dice al dueño se
/// leen desde una prueba, sin abrir nada.
/// </para>
/// <para>
/// ⛔ <b>No firma ni un campo.</b> Esta clase no conoce <c>IProcedencia</c>. «Todo correcto»
/// —<c>procedencia_campo.verificado</c>— es de Miguel, campo por campo, y nunca automática
/// (regla permanente 5, precisada por el dueño el 2026-09-03). Contestar las seis preguntas
/// del sistema del líder es otra cosa, y en la base van a columnas distintas.
/// </para>
/// <para>
/// ⛔ <b>Tampoco toca <c>casos.estado_recomendacion</c>.</b> Ese lo escribe el Excel que
/// devuelve el compañero, con su nombre — <i>«el documento que ellos llenan es el que marca,
/// y dice completado por Sandy»</i>. Criterio C18-5: no se toca ni se recalcula aquí.
/// </para>
/// </remarks>
public sealed class AccionesDeLasPreguntas
{
    /// <summary>
    /// Lo que se guarda en <c>pasos_origen</c> cuando las contesta una persona en esta ventana.
    /// </summary>
    /// <remarks>
    /// Es el texto del criterio C19-7, literal. Acaba en la base y lo leen los reportes:
    /// cambiarlo deja sin reconocer todo lo ya contestado por esta vía. Es distinto de
    /// <see cref="AccionesDeRevisar.OrigenAMano"/>, que es el de la marca del ESTADO del
    /// documento: son dos cosas y se cuentan aparte.
    /// </remarks>
    public const string OrigenAMano = "a mano en la pantalla";

    /// <summary>
    /// Lo que se guarda en <c>pasos_origen</c> cuando las seis se marcaron de un tirón.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Es distinto de <see cref="OrigenAMano"/> a propósito, y ahí está media entrega.</b>
    /// Las seis columnas quedan idénticas por los dos caminos —seis «sí» son seis «sí»—, así
    /// que si los dos escribieran el mismo origen no habría forma de saber cuál miró alguien
    /// pregunta por pregunta y cuál se marcó en bloque. Del pase: <i>«que se vea que fue en
    /// bloque y no una a una, y que él pueda distinguirlo después»</i>.
    /// </para>
    /// <para>
    /// El texto acaba en la base y se lee en la línea de la firma —«desde las seis marcadas de
    /// un tirón en la pantalla»—; cambiarlo deja sin reconocer todo lo ya marcado por esta vía.
    /// </para>
    /// </remarks>
    public const string OrigenDeUnTiron = "las seis marcadas de un tirón en la pantalla";

    private readonly IPersonas _personas;
    private readonly ICompaneros _companeros;

    /// <summary>Ata la acción a las personas y al equipo.</summary>
    public AccionesDeLasPreguntas(IPersonas personas, ICompaneros companeros)
    {
        _personas = personas;
        _companeros = companeros;
    }

    /// <summary>
    /// Quién firma lo que se conteste aquí, o nulo si el programa no lo sabe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Es la regla del rol, y NO la de buscar a alguien llamado «Miguel».</b>
    /// <see cref="AccionesDeRevisar.QuienFirmaAMano"/> coge el compañero activo llamado
    /// Miguel y, si no lo hay, <b>el primer activo</b>; en la base viva del dueño
    /// <c>companeros</c> tiene una sola fila, <c>Sandy</c> (medido el 2026-09-05), así que
    /// con esa regla <b>Miguel contestaría como Sandy</b> y el reporte diría que Sandy miró
    /// el sistema del obispo de gente que no miró. Es el defecto que nombra el criterio
    /// C19-10, y se cierra usando la misma regla que ya cerró la FASE C16:
    /// <see cref="ElAdministrador.De"/>, el único compañero activo con rol de administrador,
    /// un dato que la migración 18 puso en la base.
    /// </para>
    /// <para>
    /// Con cero administradores o con dos devuelve nulo y no se escribe nada:
    /// <see cref="PorQueNoSePuede"/> dice qué falta. <b>Nunca se firma a nombre de quien no
    /// fue.</b>
    /// </para>
    /// </remarks>
    public Companero? QuienContesta() => ElAdministrador.De(_companeros.Activos());

    /// <summary>
    /// Por qué no se puede contestar todavía, en una línea que dice qué hacer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La REGLA es la de <see cref="ElAdministrador"/> y no se duplica; lo que se escribe
    /// aquí son las PALABRAS, porque las de allí hablan del atajo de completar sin verificar
    /// y esto es otra cosa: contestar las seis preguntas del sistema del líder.
    /// </para>
    /// <para>
    /// ⛔ La línea NO nombra a ningún compañero. Ofrecer un nombre aquí es justo el paso
    /// previo a firmar con él.
    /// </para>
    /// </remarks>
    public Aviso PorQueNoSePuede()
    {
        var cuantos = _companeros.Activos()
            .Count(companero => companero.Activo && companero.Rol == RolDeCompanero.Administrador);

        return cuantos == 0
            ? Aviso.Problema(
                "para contestar estas preguntas, dese de alta usted como administrador",
                string.Empty,
                "Queda anotado quién contestó cada pregunta, y ahora mismo no hay ningún administrador "
                + "en la lista de compañeros. Dese de alta a usted mismo con el rol de administrador y "
                + "vuelva a pulsar. El programa NO contesta a nombre de otra persona.")
            : Aviso.Problema(
                $"hay {cuantos} administradores activos: el programa no adivina cuál de ellos es usted",
                string.Empty,
                "Con más de un administrador en la lista, guardar estas respuestas obligaría a elegir "
                + "un nombre, y el que se eligiera podría no ser el suyo. Deje activo solo al que "
                + "contesta y vuelva a pulsar.");
    }

    /// <summary>
    /// Guarda las seis preguntas de una persona y firma quién, cuándo y desde dónde.
    /// </summary>
    /// <remarks>
    /// Toca UNA persona. Las demás del mismo documento no se enteran: cada una es un ticket
    /// aparte, y un documento de diez puede tener tres resueltas y siete no.
    /// </remarks>
    /// <param name="personaId">De quién se contestan las seis.</param>
    /// <param name="respuesta">Las seis, cada una en sí, no o en blanco.</param>
    public ResultadoDeEscritura Guardar(long personaId, RespuestaALosPasos respuesta)
        => Guardar(personaId, respuesta, OrigenAMano);

    /// <summary>
    /// Guarda las seis que se marcaron de un tirón, y deja escrito que fue así.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Escribe exactamente lo mismo que <see cref="Guardar(long, RespuestaALosPasos)"/> salvo
    /// el origen, y pasa por la misma comprobación de quién firma: <b>marcar seis de golpe a
    /// nombre de quien no fue son seis mentiras, no una</b>.
    /// </para>
    /// <para>
    /// ⛔ <b>No es un botón que escriba solo.</b> Lo llama la ventana cuando él pulsa «Guardar
    /// las seis» habiendo usado antes el atajo, y el atajo por su cuenta no escribe nada: ver
    /// <see cref="MarcarLasSeisDeUnTiron"/>.
    /// </para>
    /// </remarks>
    /// <param name="personaId">De quién se marcan las seis.</param>
    /// <param name="respuesta">Las seis tal como quedaron en la pantalla.</param>
    public ResultadoDeEscritura GuardarDeUnTiron(long personaId, RespuestaALosPasos respuesta)
        => Guardar(personaId, respuesta, OrigenDeUnTiron);

    /// <summary>El camino común de los dos: comprobar quién firma y escribir con su origen.</summary>
    private ResultadoDeEscritura Guardar(long personaId, RespuestaALosPasos respuesta, string origen)
    {
        var quien = QuienContesta();
        if (quien is null) return ResultadoDeEscritura.NoSeEscribio(PorQueNoSePuede());

        return _personas.ResponderLosPasos(personaId, respuesta, quien.Id, origen);
    }

    /// <summary>Lo que se dice cuando las seis de una persona quedaron guardadas.</summary>
    /// <remarks>
    /// Nombra a la persona y dice cómo queda: es la frase que él va a leer antes de decidir
    /// si llama al obispo. Y dice quién firmó, porque de eso trata la migración 19.
    /// </remarks>
    public static Aviso LoQueSeGuardo(string deQuien, string fraseDelEstado, string quienFirmo)
        => Aviso.Informa(
            $"Guardadas las seis preguntas de {deQuien}: {fraseDelEstado}.",
            string.Empty,
            $"Quedó anotado que las contestó {quienFirmo} desde esta pantalla. Esto NO es la firma "
            + "de campos «Todo correcto», que sigue siendo campo por campo en Corrección.");
}
