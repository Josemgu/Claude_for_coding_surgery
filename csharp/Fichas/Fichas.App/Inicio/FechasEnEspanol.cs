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

    /// <summary>Los doce meses en minúsculas, indexados por <c>Month - 1</c>; nunca por cultura.</summary>
    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>
    /// Los siete días empezando por el lunes, que es como se lee un calendario aquí;
    /// <c>DayOfWeek</c> empieza por el domingo y quien indexa tiene que girarlo.
    /// </summary>
    private static readonly string[] DiasDeLaSemana =
    [
        "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo",
    ];

    /// <summary>Las iniciales de los siete dias, de lunes a domingo, para la cabecera del calendario.</summary>
    public static IReadOnlyList<string> InicialesDeLosDias { get; } = ["L", "M", "M", "J", "V", "S", "D"];

    /// <summary>Lee una fecha ISO-8601; devuelve nulo si no tiene esa forma, sin lanzar nunca.</summary>
    /// <param name="fechaIso">El texto tal como está en la base; nulo o vacío dan nulo.</param>
    /// <returns>La fecha, o nulo si el texto no es exactamente <c>AAAA-MM-DD</c>.</returns>
    public static DateOnly? Leer(string? fechaIso)
        => DateOnly.TryParseExact(fechaIso, "yyyy-MM-dd", SinIdioma, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    /// <summary>Escribe una fecha en ISO-8601, que es como se guarda todo en la base.</summary>
    /// <param name="fecha">La fecha que se va a guardar o comparar.</param>
    public static string Escribir(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", SinIdioma);

    /// <summary>«septiembre de 2026», que es el titulo del calendario.</summary>
    /// <param name="fecha">Cualquier día del mes que se quiere nombrar.</param>
    public static string DecirElMes(DateOnly fecha) => $"{Meses[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>«17 de septiembre», que es como el dueno nombra un grupo.</summary>
    /// <remarks>
    /// Sin el dia de la semana ni el ano a proposito: sus palabras del 2026-09-05 son <i>«las
    /// personas del grupo del 17 de septiembre»</i>, y una frase que ya dice cuantas personas
    /// faltan no puede llevar ademas «jueves» y «de 2026» sin dejar de caber en una linea.
    /// </remarks>
    /// <param name="fecha">El día del grupo.</param>
    public static string DecirElDiaYElMes(DateOnly fecha) => $"{fecha.Day} de {Meses[fecha.Month - 1]}";

    /// <summary>«viernes 4 de septiembre de 2026», la linea de la cabecera de la pantalla.</summary>
    /// <param name="fecha">El día que se dice entero.</param>
    public static string DecirElDiaCompleto(DateOnly fecha)
        => $"{DiasDeLaSemana[(int)fecha.DayOfWeek == 0 ? 6 : (int)fecha.DayOfWeek - 1]} "
           + $"{fecha.Day} de {Meses[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>«hoy», «mañana», «en 3 días», «ayer» o «hace 5 días».</summary>
    /// <param name="dias">Cuántos días faltan: negativo si ya pasó, cero si es hoy.</param>
    public static string DecirLosDias(int dias) => dias switch
    {
        0 => "hoy",
        1 => "mañana",
        -1 => "ayer",
        > 1 => $"en {dias} días",
        _ => $"hace {-dias} días",
    };
}
