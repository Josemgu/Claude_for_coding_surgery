using System.Globalization;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Cascara;

/// <summary>
/// Como se escribe una fecha en la cabecera: en espanol, entera y sin hora.
/// </summary>
/// <remarks>
/// <para>El mockup escribe «jueves 3 de septiembre de 2026» al lado del titulo. Sin coma
/// detras del dia, que es lo que saldria con <c>ToString("D")</c> de es-ES.</para>
///
/// <para>⛔ La cultura va FIJADA y no se hereda de la maquina. Con la cultura del sistema, un
/// Windows en ingles ensenaria «Wednesday, September 9, 2026» dentro de un programa cuya
/// regla permanente 4 dice «espanol en todo». Y no es teorico: el mismo programa escribe
/// «Período» y «Histórico» en los PDF, asi que la pantalla y el papel se contradirian.</para>
/// </remarks>
public static class FechaDeLaCabecera
{
    /// <summary>El espanol con el que se escriben las fechas de la pantalla.</summary>
    private static readonly CultureInfo ElEspanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Lo que se ensena cuando la fecha guardada no se deja leer.</summary>
    private const string CuandoNoHayFecha = "sin fecha de viaje";

    /// <summary>La fecha entera: «miércoles 9 de septiembre de 2026».</summary>
    /// <param name="dia">El dia que se escribe; la hora se descarta.</param>
    public static string Larga(DateTime dia)
        => dia.ToString("dddd d 'de' MMMM 'de' yyyy", ElEspanol);

    /// <summary>
    /// El «hoy» del reloj del programa, escrito entero.
    /// </summary>
    /// <remarks>
    /// Se pasa por <see cref="IReloj.Hoy"/> y no por <c>DateTime.Now</c>: es la regla del
    /// puerto —«nadie llama a DateTime.Now directamente»— y ademas es lo unico que deja
    /// comprobar la cabecera un dia que no sea hoy.
    /// </remarks>
    /// <param name="reloj">El reloj del programa.</param>
    public static string LargaDelReloj(IReloj reloj)
        => DateTime.TryParseExact(
            reloj.Hoy(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? Larga(dia)
            : string.Empty;

    /// <summary>El dia sin el nombre de la semana: «9 de septiembre de 2026».</summary>
    /// <param name="dia">El dia que se escribe.</param>
    public static string Corta(DateTime dia)
        => dia.ToString("d 'de' MMMM 'de' yyyy", ElEspanol);

    /// <summary>
    /// Una fecha guardada como <c>yyyy-MM-dd</c>, escrita corta; y si no se deja leer, se dice.
    /// </summary>
    /// <remarks>
    /// La base guarda las fechas como texto <c>yyyy-MM-dd</c> y admite el nulo. Un valor que
    /// no case no se adivina ni se deja en blanco: se dice que no hay fecha, porque una
    /// fecha de viaje ausente es justo lo que el programa existe para avisar.
    /// </remarks>
    /// <param name="guardada">Lo que hay en la columna, que puede ser nulo o cualquier cosa.</param>
    public static string CortaDeLoGuardado(string? guardada)
        => DateTime.TryParseExact(
            guardada, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? Corta(dia)
            : CuandoNoHayFecha;
}
