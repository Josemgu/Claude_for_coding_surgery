namespace Fichas.Contratos.Puertos;

/// <summary>El reloj del programa. Nadie llama a DateTime.Now directamente: se pide aqui.</summary>
/// <remarks>
/// Sin esto no hay forma de probar «viaja en los proximos 7 dias» sin esperar a que
/// llegue el dia. Es la unica dependencia que toda pantalla tiene garantizada.
/// </remarks>
public interface IReloj
{
    /// <summary>La fecha de hoy en ISO-8601 (AAAA-MM-DD), que es como se guardan las fechas.</summary>
    string Hoy();

    /// <summary>El instante de ahora en ISO-8601, que es lo que se escribe en las marcas de tiempo.</summary>
    string Ahora();

    /// <summary>La fecha de dentro de N dias en ISO-8601; N negativo va hacia atras.</summary>
    string HoyMasDias(int dias);
}
