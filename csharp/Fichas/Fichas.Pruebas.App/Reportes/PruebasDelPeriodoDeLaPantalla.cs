using Fichas.App.Reportes;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Los tres periodos que ofrece la pantalla, contados sobre una fecha dada.
/// </summary>
/// <remarks>
/// Son los tres del programa viejo (<c>interfaz/reportes.py</c>, <c>_construir_selector</c>):
/// este mes, el mes anterior y los ultimos 90 dias. La pantalla no valida fechas y esta
/// prueba no comprueba que las valide: quien decide si un periodo vale es
/// <c>Fichas.Reportes</c> detras de <c>IReportes</c>, en un solo sitio.
/// </remarks>
[TestClass]
public sealed class PruebasDelPeriodoDeLaPantalla
{
    /// <summary>«Este mes» va del dia 1 al ultimo, y septiembre tiene 30.</summary>
    [TestMethod]
    public void EsteMesVaDelUnoAlUltimoDiaDelMes()
    {
        var periodo = PeriodoDeLaPantalla.DelMesDe("2026-09-04");

        Assert.AreEqual("2026-09-01", periodo.Desde);
        Assert.AreEqual("2026-09-30", periodo.Hasta);
    }

    /// <summary>Febrero de un ano bisiesto termina el 29, no el 28.</summary>
    [TestMethod]
    public void FebreroBisiestoTerminaElVeintinueve()
    {
        var periodo = PeriodoDeLaPantalla.DelMesDe("2028-02-10");

        Assert.AreEqual("2028-02-01", periodo.Desde);
        Assert.AreEqual("2028-02-29", periodo.Hasta);
    }

    /// <summary>«Mes anterior» en enero es diciembre del ano de antes.</summary>
    [TestMethod]
    public void ElMesAnteriorAEneroEsDiciembreDelAnoDeAntes()
    {
        var periodo = PeriodoDeLaPantalla.DelMesAnteriorA("2026-01-15");

        Assert.AreEqual("2025-12-01", periodo.Desde);
        Assert.AreEqual("2025-12-31", periodo.Hasta);
    }

    /// <summary>90 dias son 90 y no 91: hoy es uno de los noventa.</summary>
    /// <remarks>Es la cuenta del Python (<c>periodo_de_los_ultimos_dias</c>) y la que espera quien lo pide.</remarks>
    [TestMethod]
    public void LosUltimosNoventaDiasCuentanHoyDentro()
    {
        var periodo = PeriodoDeLaPantalla.DeLosUltimosDias("2026-09-04", 90);

        Assert.AreEqual("2026-09-04", periodo.Hasta);
        Assert.AreEqual("2026-06-07", periodo.Desde);
        Assert.AreEqual(
            90,
            (DateOnly.Parse(periodo.Hasta).DayNumber - DateOnly.Parse(periodo.Desde).DayNumber) + 1);
    }

    /// <summary>Una fecha que no es fecha NO detiene nada: sale tal cual y la avisa la biblioteca.</summary>
    /// <remarks>
    /// Requisito 9 del dueno, «avisar, nunca impedir». La pantalla no inventa un periodo de
    /// recambio: pasa lo que hay, y <c>IReportes</c> devuelve el aviso que dice que no es
    /// una fecha. Inventar aqui una fecha buena escondia el error.
    /// </remarks>
    [TestMethod]
    public void UnaFechaQueNoEsFechaSaleTalCualYNoSeInventaOtra()
    {
        var periodo = PeriodoDeLaPantalla.DelMesDe("no soy una fecha");

        Assert.AreEqual("no soy una fecha", periodo.Desde);
        Assert.AreEqual("no soy una fecha", periodo.Hasta);
    }

    /// <summary>El titulo que se pinta lleva las dos fechas y se lee de un vistazo.</summary>
    [TestMethod]
    public void ElTituloLlevaLasDosFechas()
    {
        Assert.AreEqual(
            "del 2026-09-01 al 2026-09-30",
            new PeriodoDeLaPantalla("2026-09-01", "2026-09-30").EnTexto());

        Assert.AreEqual(
            "el 2026-09-04",
            new PeriodoDeLaPantalla("2026-09-04", "2026-09-04").EnTexto());
    }
}
