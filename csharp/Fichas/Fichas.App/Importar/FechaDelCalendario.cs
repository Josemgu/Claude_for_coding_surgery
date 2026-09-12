using System.Globalization;

namespace Fichas.App.Importar;

/// <summary>
/// La traduccion entre la fecha de viaje que guarda la base y la que entiende el calendario
/// de la pantalla.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Existe por un defecto medido con la ventana abierta el 2026-09-09</b>, no por
/// gusto de separar. Se corrigio el NOMBRE de una carpeta —sin tocarle la fecha— y la
/// carpeta paso de «Grupo del 5 de septiembre» a «Grupo del 4 de septiembre»: el grupo
/// entero se movio de dia solo.</para>
///
/// <para><c>CalendarDatePicker</c> trabaja con <see cref="DateTimeOffset"/>, y la fecha se le
/// entregaba <b>a medianoche con desplazamiento cero</b>. En un huso al oeste de Greenwich
/// —el del dueño lo esta— esa medianoche en UTC es el dia ANTERIOR por la tarde en hora
/// local, y eso es lo que se recogia al leerla.</para>
///
/// <para>⛔ La cura es doble y las dos mitades hacen falta: se entrega <b>al mediodia</b>,
/// que es la unica hora en la que ningun huso de la Tierra —de -12 a +14— cae en otro dia; y
/// se recoge por <see cref="DateTimeOffset.LocalDateTime"/>, que es el dia que la persona ve
/// escrito en el calendario. Con una sola de las dos, el defecto vuelve por el otro camino.
/// </para>
///
/// <para>Va aparte de la pantalla —y no como un par de metodos privados suyos— porque asi se
/// puede probar sin abrir ninguna ventana, que es lo que faltaba el dia que esto se rompio.
/// </para>
/// </remarks>
public static class FechaDelCalendario
{
    /// <summary>Como se escribe una fecha de viaje en la base: ISO-8601 y nada mas.</summary>
    private const string ComoSeGuarda = "yyyy-MM-dd";

    /// <summary>
    /// La fecha que hay que darle al calendario, o nulo si no hay ninguna que darle.
    /// </summary>
    /// <remarks>
    /// Lo que no sea una fecha ISO-8601 estricta vuelve NULO y no lanza: la fecha sale del
    /// papel por OCR y puede traer cualquier cosa dentro. Vacio es lo honesto, porque es
    /// justo lo que el arbol de Revisar hace con ella —dejarla en «sin fecha de viaje»—.
    /// </remarks>
    /// <param name="fechaIso">La fecha tal como está en la base, <c>AAAA-MM-DD</c>; nula o mal formada devuelve nulo.</param>
    public static DateTimeOffset? Leer(string? fechaIso)
    {
        if (!DateOnly.TryParseExact(
                fechaIso, ComoSeGuarda, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
        {
            return null;
        }

        var mediodia = dia.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local);
        return new DateTimeOffset(mediodia);
    }

    /// <summary>Lo que el calendario dice, en <c>AAAA-MM-DD</c>, o vacio si no hay fecha.</summary>
    /// <remarks>
    /// Por <see cref="DateTimeOffset.LocalDateTime"/> y no por <c>Date</c>: lo que hay que
    /// guardar es el dia que la persona ve escrito en el calendario, no el que salga de
    /// mirar ese mismo instante desde otro meridiano.
    /// </remarks>
    /// <param name="delCalendario">Lo que devuelve el <c>CalendarDatePicker</c>; nulo cuando no hay fecha elegida.</param>
    public static string Escribir(DateTimeOffset? delCalendario)
        => delCalendario is null
            ? string.Empty
            : DateOnly.FromDateTime(delCalendario.Value.LocalDateTime)
                .ToString(ComoSeGuarda, CultureInfo.InvariantCulture);
}
