using System.Globalization;

namespace Fichas.App.Inicio;

/// <summary>
/// Las fechas dichas en espanol, escritas a mano.
/// </summary>
/// <remarks>
/// Los nombres NO se piden a <c>CultureInfo</c> a proposito: la cultura de la maquina
/// del dueno puede ser otra, y entonces el calendario saldria en ingles sin que nadie
/// lo note hasta verlo. Regla permanente 4: espanol en todo, tambien en la pantalla.
/// Las fechas de la base son ISO-8601 y se leen por posiciones, nunca por cultura.
/// </remarks>
public static class FechasEnEspanol
{
    /// <summary>La cultura invariable; las fechas de la base son ISO y no dependen del idioma.</summary>
    public static CultureInfo SinIdioma => CultureInfo.InvariantCulture;

    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly string[] DiasDeLaSemana =
    [
        "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo",
    ];

    /// <summary>Las iniciales de los siete dias, de lunes a domingo, para la cabecera del calendario.</summary>
    public static IReadOnlyList<string> InicialesDeLosDias { get; } = ["L", "M", "M", "J", "V", "S", "D"];

    /// <summary>Lee una fecha ISO-8601; devuelve nulo si no tiene esa forma, sin lanzar nunca.</summary>
    public static DateOnly? Leer(string? fechaIso)
        => DateOnly.TryParseExact(fechaIso, "yyyy-MM-dd", SinIdioma, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    /// <summary>Escribe una fecha en ISO-8601, que es como se guarda todo en la base.</summary>
    public static string Escribir(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", SinIdioma);

    /// <summary>«septiembre de 2026», que es el titulo del calendario.</summary>
    public static string DecirElMes(DateOnly fecha) => $"{Meses[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>«17 de septiembre», que es como el dueno nombra un grupo.</summary>
    /// <remarks>
    /// Sin el dia de la semana ni el ano a proposito: sus palabras del 2026-09-05 son <i>«las
    /// personas del grupo del 17 de septiembre»</i>, y una frase que ya dice cuantas personas
    /// faltan no puede llevar ademas «jueves» y «de 2026» sin dejar de caber en una linea.
    /// </remarks>
    public static string DecirElDiaYElMes(DateOnly fecha) => $"{fecha.Day} de {Meses[fecha.Month - 1]}";

    /// <summary>«viernes 4 de septiembre de 2026», la linea de la cabecera de la pantalla.</summary>
    public static string DecirElDiaCompleto(DateOnly fecha)
        => $"{DiasDeLaSemana[(int)fecha.DayOfWeek == 0 ? 6 : (int)fecha.DayOfWeek - 1]} "
           + $"{fecha.Day} de {Meses[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>«hoy», «mañana», «en 3 días», «ayer» o «hace 5 días».</summary>
    public static string DecirLosDias(int dias) => dias switch
    {
        0 => "hoy",
        1 => "mañana",
        -1 => "ayer",
        > 1 => $"en {dias} días",
        _ => $"hace {-dias} días",
    };
}
