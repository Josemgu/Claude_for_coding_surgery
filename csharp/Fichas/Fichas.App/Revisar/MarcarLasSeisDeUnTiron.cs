namespace Fichas.App.Revisar;

/// <summary>
/// El atajo de las seis preguntas: ponerlas todas en «sí» de un tirón, sin escribir.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Del dueño, 2026-09-07: <i>«en el botón de las seis preguntas debe
/// haber un botón que me permita marcar todos los pasos en un solo clic, en el caso de que no
/// quiera crear un paquete para mí, sino que si una persona puede trabajar sola con el
/// sistema, que se pueda hacer sin problema»</i>.
/// </para>
/// <para>
/// ⛔⛔ <b>Por qué PROPONE y no escribe, que es la decisión que más pesa de este archivo.</b>
/// Contestar las seis es decir que alguien fue al sistema del obispo y lo comprobó. Un botón
/// que escribiera las seis con un clic convertiría «no lo he mirado» en «está todo bien» sin
/// que nadie lo hubiera mirado, y eso manda a una persona al templo con la recomendación mal
/// — que es el daño que este programa existe para evitar (regla permanente 5).
/// </para>
/// <para>
/// <b>Así que el atajo hace lo que él pidió —marcar los seis pasos en un clic— y la firma
/// sigue donde estaba:</b> el clic pone los seis desplegables en «sí», él ve en pantalla lo
/// que va a firmar, y quien escribe en la base sigue siendo «Guardar las seis», el mismo
/// botón que ya existía y ya se auditaba. Un clic de más, y a cambio: nada se escribe por
/// accidente, y lo que se escribe se puede desandar antes de firmarlo.
/// </para>
/// <para>
/// <b>Y queda escrito que fue en bloque</b>: lo guardado por esta vía lleva
/// <see cref="AccionesDeLasPreguntas.OrigenDeUnTiron"/> en <c>pasos_origen</c>, distinto del
/// de contestarlas una a una. Las seis columnas quedan idénticas por los dos caminos —seis
/// «sí» son seis «sí»—, así que el origen es lo único que los separa en la base.
/// </para>
/// <para>
/// <b>Esta clase no conoce ningún repositorio y no tiene por dónde escribir</b>, y eso no es
/// casualidad: es la forma de que la promesa del párrafo de arriba la sostenga el compilador
/// y no la buena fe.
/// </para>
/// </remarks>
public static class MarcarLasSeisDeUnTiron
{
    /// <summary>Lo que se lee en el botón del atajo.</summary>
    public const string LoQueDiceElBoton = "Marcar las seis en «sí»";

    /// <summary>Lo que se le dice al pulsarlo, para que sepa que todavía no ha firmado nada.</summary>
    /// <remarks>
    /// Se dice SIEMPRE, y no solo la primera vez: la frase es la mitad de la protección. Sin
    /// ella, el atajo se parece a un guardado y él creería haber terminado.
    /// </remarks>
    public const string LoQueSeAvisa =
        "Las seis quedan puestas en «sí», pero todavía no se ha guardado nada. "
        + "Revíselas y pulse «Guardar las seis» para firmarlas con su nombre.";

    /// <summary>
    /// Las seis preguntas de esa persona, todas en «sí», en su orden y con sus rótulos.
    /// </summary>
    /// <remarks>
    /// Los rótulos salen del ticket y no se vuelven a escribir aquí: cada respuesta va a la
    /// columna que le toca por su posición, y una lista compuesta aparte podría desalinearse
    /// el día que se renombre una pregunta.
    /// </remarks>
    public static IReadOnlyList<PreguntaDeUnaPersona> LoQuePropone(TicketDeUnaPersona ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return [.. ticket.Preguntas.Select(pregunta => pregunta with { Respuesta = true })];
    }
}
