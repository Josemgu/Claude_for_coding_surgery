using System.Globalization;

namespace Fichas.App.Reportes;

/// <summary>
/// El periodo que la pantalla tiene puesto ahora mismo: dos fechas ISO-8601.
/// </summary>
/// <remarks>
/// <para>Son los tres atajos del programa viejo (<c>interfaz/reportes.py</c>): este mes, el
/// mes anterior y los ultimos 90 dias. Nada mas. <b>Aqui no se valida ninguna fecha</b>, y es
/// a proposito: quien decide si un periodo vale es <c>Fichas.Reportes</c> detras de
/// <c>IReportes</c>, y validar tambien aqui seria una segunda regla que puede decir otra cosa
/// que la primera.</para>
///
/// <para>Por eso una fecha ilegible sale TAL CUAL y no se sustituye por una buena: pasa a
/// <c>IReportes</c>, que devuelve su aviso de una linea (requisito 9, «avisar, nunca
/// impedir»). Cambiarla aqui por la de hoy escondia el error y generaba un reporte del mes
/// equivocado sin que nadie se enterara.</para>
/// </remarks>
/// <param name="Desde">El primer dia, incluido, en ISO-8601.</param>
/// <param name="Hasta">El ultimo dia, incluido, en ISO-8601.</param>
public readonly record struct PeriodoDeLaPantalla(string Desde, string Hasta)
{
    /// <summary>La única forma en que se leen y se escriben las fechas: ISO-8601 estricta.</summary>
    private const string FormaIso = "yyyy-MM-dd";

    /// <summary>El mes entero al que pertenece esa fecha, del dia 1 al ultimo.</summary>
    /// <param name="hoyIso">La fecha de hoy en ISO; si no se puede leer, el período es esa misma cadena dos veces.</param>
    public static PeriodoDeLaPantalla DelMesDe(string hoyIso) => MesQueContieneA(hoyIso, 0);

    /// <summary>El mes anterior al de esa fecha, entero.</summary>
    /// <param name="hoyIso">La fecha de hoy en ISO; si no se puede leer, el período es esa misma cadena dos veces.</param>
    public static PeriodoDeLaPantalla DelMesAnteriorA(string hoyIso) => MesQueContieneA(hoyIso, -1);

    /// <summary>Los ultimos N dias contando esa fecha como el ultimo de los N.</summary>
    /// <remarks>
    /// 90 dias son 90 y no 91: hoy es uno de los noventa. Es la cuenta del Python
    /// (<c>periodo_de_los_ultimos_dias</c>) y la que espera quien pide «los últimos 90 días».
    /// </remarks>
    /// <param name="hoyIso">La fecha de hoy en ISO; si no se puede leer, el período es esa misma cadena dos veces.</param>
    /// <param name="dias">Cuántos días, contando hoy; menos de 1 devuelve solo hoy.</param>
    public static PeriodoDeLaPantalla DeLosUltimosDias(string hoyIso, int dias)
    {
        if (!LeerFecha(hoyIso, out var hoy) || dias < 1) return new PeriodoDeLaPantalla(hoyIso, hoyIso);
        return new PeriodoDeLaPantalla(Escribir(hoy.AddDays(-(dias - 1))), Escribir(hoy));
    }

    /// <summary>Como se pinta el periodo en el titulo de la pantalla.</summary>
    /// <remarks>
    /// Con las fechas en ISO y no en letra. En el PDF salen en letra —lo hace
    /// <c>Fichas.Reportes</c>—; aqui van tal como se teclean en las dos casillas de al lado,
    /// para que se pueda comparar el titulo con lo escrito sin traducir nada de cabeza.
    /// </remarks>
    public string EnTexto() => Desde == Hasta ? $"el {Desde}" : $"del {Desde} al {Hasta}";

    /// <summary>El mes que contiene esa fecha, corrido N meses; N negativo va hacia atras.</summary>
    /// <param name="hoyIso">La fecha de hoy en ISO; si no se puede leer, el período es esa misma cadena dos veces.</param>
    /// <param name="mesesDeDesplazamiento">Cuántos meses correr el resultado; 0 es el mes de hoy.</param>
    private static PeriodoDeLaPantalla MesQueContieneA(string hoyIso, int mesesDeDesplazamiento)
    {
        if (!LeerFecha(hoyIso, out var hoy)) return new PeriodoDeLaPantalla(hoyIso, hoyIso);

        var primero = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(mesesDeDesplazamiento);
        return new PeriodoDeLaPantalla(Escribir(primero), Escribir(primero.AddMonths(1).AddDays(-1)));
    }

    /// <summary>Lee una fecha ISO estricta; falso si no lo es, sin lanzar.</summary>
    /// <param name="iso">La cadena tal como venga.</param>
    /// <param name="fecha">La fecha leída; sin valor útil cuando devuelve falso.</param>
    private static bool LeerFecha(string? iso, out DateOnly fecha)
        => DateOnly.TryParseExact(iso, FormaIso, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    /// <summary>La fecha en ISO, con cultura invariante para que el separador no dependa de Windows.</summary>
    /// <param name="fecha">La fecha.</param>
    private static string Escribir(DateOnly fecha) => fecha.ToString(FormaIso, CultureInfo.InvariantCulture);
}
