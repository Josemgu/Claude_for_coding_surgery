namespace Fichas.Contratos.Puertos;

/// <summary>El reloj del programa. Nadie llama a DateTime.Now directamente: se pide aqui.</summary>
/// <remarks>
/// Sin esto no hay forma de probar «viaja en los proximos 7 dias» sin esperar a que
/// llegue el dia. Es la unica dependencia que toda pantalla tiene garantizada.
/// <para>
/// <b>Quién lo implementa:</b> <c>RelojDelSistema</c> (hay uno en <c>Fichas.App/Cascara</c>
/// y otro en <c>Fichas.Datos.Falso</c>, los dos sobre <c>DateTime.Now</c>) y
/// <c>Fichas.Datos.Falso.RelojFijo</c>, parado en un día, que es el que usan las pruebas.
/// <b>Quién lo consume:</b> la cabecera, Inicio, Grupo, Revisar, Paquetes y Reportes para
/// la fecha de hoy; y todo lo que escribe una marca de tiempo —asignar, firmar, guardar
/// una corrección, aplicar un Excel— para el instante de ahora.
/// </para>
/// </remarks>
public interface IReloj
{
    /// <summary>La fecha de hoy en ISO-8601 (AAAA-MM-DD), que es como se guardan las fechas.</summary>
    /// <returns>Diez caracteres, hora local; comparable con <c>string.CompareOrdinal</c> contra cualquier fecha de la base.</returns>
    string Hoy();

    /// <summary>El instante de ahora en ISO-8601, que es lo que se escribe en las marcas de tiempo.</summary>
    /// <returns><c>AAAA-MM-DD HH:mm:ss</c>, hora local, con espacio entre fecha y hora; el reloj fijo da siempre las 12:00.</returns>
    string Ahora();

    /// <summary>La fecha de dentro de N dias en ISO-8601; N negativo va hacia atras.</summary>
    /// <remarks>Hoy no lo llama ninguna pantalla (grep del 2026-09-11): lo usan el generador falso y sus pruebas para repartir fechas de viaje.</remarks>
    /// <param name="dias">Cuántos días se suman a hoy; 0 es <see cref="Hoy"/>.</param>
    /// <returns>Diez caracteres, en el mismo formato que <see cref="Hoy"/>.</returns>
    string HoyMasDias(int dias);
}
