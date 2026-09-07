using System.Globalization;
using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Reglas;

/// <summary>Un periodo leido, o el aviso que dice por que no se pudo leer.</summary>
/// <remarks>
/// ⚠️ El Python LEVANTA <c>ErrorDePeriodo</c>. Aqui no se levanta nada: requisito 9 del dueno,
/// «avisar, nunca impedir». Lo que alli era una excepcion aqui es un <see cref="Aviso"/> que
/// se devuelve, y la operacion lo pasa a un <c>ResultadoDeEscritura</c> con
/// <c>SeEscribio</c> en falso.
/// </remarks>
/// <param name="Periodo">El periodo, o nulo si no se pudo leer.</param>
/// <param name="Problema">Por que no se pudo leer, o nulo si se leyo bien.</param>
public readonly record struct LecturaDePeriodo(Periodo? Periodo, Aviso? Problema);

/// <summary>
/// El trozo de calendario que se reporta, validado en un solo sitio.
/// </summary>
/// <remarks>
/// Un periodo son dos fechas ISO-8601 y LOS DOS EXTREMOS ENTRAN. Se dice aqui porque es la
/// clase de detalle que, sin decirlo, hace que un reporte de septiembre se deje fuera el dia
/// 30 y nadie lo note hasta que los numeros no cuadran.
///
/// <b>El limite de arriba para las marcas de tiempo es otro.</b> <c>fecha_viaje</c> es
/// 'AAAA-MM-DD' pelado, pero <c>creado_en</c> y <c>verificado_en</c> llevan la hora pegada:
/// '2026-09-30 14:12:03' es mayor que '2026-09-30' comparado como texto, asi que un
/// <c>&lt;= hasta</c> se dejaria fuera todo lo que paso ese dia despues de medianoche. Por eso
/// existe <see cref="SiguienteAHasta"/>, y las comparaciones contra columnas con hora son
/// <c>&gt;= Desde AND &lt; SiguienteAHasta</c>.
///
/// Ninguna funcion de esta clase mira el reloj. La fecha de hoy entra desde fuera: lo que
/// mira el reloj por dentro no se puede probar por los bordes.
/// </remarks>
/// <param name="Desde">El primer dia, incluido, en ISO-8601.</param>
/// <param name="Hasta">El ultimo dia, incluido, en ISO-8601.</param>
/// <param name="SiguienteAHasta">El dia de despues; el limite abierto de las marcas con hora.</param>
public sealed record Periodo(string Desde, string Hasta, string SiguienteAHasta)
{
    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>El periodo entre dos fechas, los dos extremos incluidos.</summary>
    /// <remarks>
    /// Un periodo de un solo dia es legitimo. Lo que no se admite es uno del reves: un reporte
    /// «del 30 al 1» no contaria nada y saldria con todos los numeros a cero, que se lee igual
    /// que un mes sin trabajo.
    /// </remarks>
    public static LecturaDePeriodo Leer(string? desde, string? hasta)
    {
        if (!EsFecha(desde))
        {
            return new LecturaDePeriodo(null, Aviso.Problema(
                "La fecha de inicio del período no es una fecha.",
                nameof(desde),
                $"Se recibió «{desde}» y se esperaba una fecha con la forma AAAA-MM-DD, como 2026-09-01."));
        }
        if (!EsFecha(hasta))
        {
            return new LecturaDePeriodo(null, Aviso.Problema(
                "La fecha de fin del período no es una fecha.",
                nameof(hasta),
                $"Se recibió «{hasta}» y se esperaba una fecha con la forma AAAA-MM-DD, como 2026-09-30."));
        }
        if (string.CompareOrdinal(desde, hasta) > 0)
        {
            return new LecturaDePeriodo(null, Aviso.Problema(
                $"El período va del {desde} al {hasta}, que está del revés.",
                nameof(desde),
                "Se esperaba que la fecha de inicio fuera anterior o igual a la de fin. Un período "
                + "del revés no contaría nada y el reporte saldría con todos los números a cero, "
                + "que se lee igual que un mes sin trabajo."));
        }

        var siguiente = DateOnly.ParseExact(hasta!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(1)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new LecturaDePeriodo(new Periodo(desde!, hasta!, siguiente), null);
    }

    /// <summary>El mes entero, del dia 1 al ultimo, sea de 28, 29, 30 o 31 dias.</summary>
    public static LecturaDePeriodo DelMes(int anio, int mes)
    {
        if (anio is < 1 or > 9999 || mes is < 1 or > 12)
        {
            return new LecturaDePeriodo(null, Aviso.Problema(
                $"No hay ningún mes {mes} del año {anio}.",
                nameof(mes),
                "Se esperaban dos números, como el mes 9 del año 2026."));
        }

        var primero = new DateOnly(anio, mes, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        return Leer(
            primero.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ultimo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    /// <summary>Si esa fecha de viaje —sin hora— cae dentro, los dos extremos incluidos.</summary>
    public bool ContieneFecha(string? fecha)
        => fecha is not null
           && string.CompareOrdinal(fecha, Desde) >= 0
           && string.CompareOrdinal(fecha, Hasta) <= 0;

    /// <summary>Si esa marca de tiempo CON hora cae dentro, los dos extremos incluidos.</summary>
    public bool ContieneMarcaConHora(string? marca)
        => marca is not null
           && string.CompareOrdinal(Desde, marca) <= 0
           && string.CompareOrdinal(marca, SiguienteAHasta) < 0;

    /// <summary>Como se escribe el periodo en la cabecera del reporte, en espanol.</summary>
    /// <remarks>
    /// Los meses van en una lista escrita a mano y no salen del idioma del sistema: en una
    /// maquina en ingles saldria «September», y el informe es en espanol siempre.
    /// </remarks>
    public string EnTexto()
        => Desde == Hasta
            ? $"el {FechaLarga(Desde)}"
            : $"del {FechaLarga(Desde)} al {FechaLarga(Hasta)}";

    /// <summary>El trozo que va en el nombre del archivo: '2026-09-01_a_2026-09-30'.</summary>
    public string NombreCorto() => $"{Desde}_a_{Hasta}";

    private static string FechaLarga(string iso)
    {
        var dia = DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{dia.Day} de {Meses[dia.Month - 1]} de {dia.Year}";
    }

    /// <summary>Forma ISO-8601 de solo fecha, y que ademas exista en el calendario.</summary>
    private static bool EsFecha(string? valor)
        => valor is not null
           && DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
