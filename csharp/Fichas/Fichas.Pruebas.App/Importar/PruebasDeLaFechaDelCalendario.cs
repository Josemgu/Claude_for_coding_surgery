using Fichas.App.Importar;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// La fecha que entra en el calendario de la pantalla tiene que salir siendo la MISMA.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Esta clase nace de un defecto medido con la ventana abierta el 2026-09-09</b>,
/// y no de una simetría bonita. Se corrigió el NOMBRE de una carpeta —sin tocar su fecha— y
/// la carpeta pasó de «Grupo del 5 de septiembre» a «Grupo del 4 de septiembre»: el grupo
/// entero se movió de día solo.</para>
///
/// <para>El motivo: <c>CalendarDatePicker</c> trabaja con <c>DateTimeOffset</c>, y la fecha
/// se le entregaba a medianoche con desplazamiento CERO. En un huso al oeste de Greenwich
/// —el del dueño lo está— esa medianoche en UTC es el día ANTERIOR por la tarde en hora
/// local, y al recogerla se leía ese día anterior.</para>
///
/// <para>Un grupo movido de día no es un detalle de presentación: es gente que aparece
/// preparándose para viajar el día que no es.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaFechaDelCalendario
{
    /// <summary>Toda fecha de un año entero entra y sale igual, en el huso que sea.</summary>
    /// <remarks>
    /// Se recorre un año y no una fecha: el defecto solo se ve cuando el desplazamiento del
    /// huso cruza la medianoche, y con el cambio de hora de verano el desplazamiento no es
    /// el mismo todo el año.
    /// </remarks>
    [TestMethod]
    public void UnAnioEnteroDeFechasEntraYSaleIgual()
    {
        var dia = new DateOnly(2026, 1, 1);
        var fin = new DateOnly(2026, 12, 31);

        while (dia <= fin)
        {
            var escrita = dia.ToString("yyyy-MM-dd");
            var vuelta = FechaDelCalendario.Escribir(FechaDelCalendario.Leer(escrita));

            Assert.AreEqual(escrita, vuelta, $"La fecha {escrita} no volvió igual del calendario.");
            dia = dia.AddDays(1);
        }
    }

    /// <summary>Sin fecha sigue siendo sin fecha; no se inventa el día de hoy.</summary>
    /// <remarks>Regla permanente 1: una fecha de viaje que nadie leyó no se rellena sola.</remarks>
    [TestMethod]
    public void SinFechaSigueSiendoSinFecha()
    {
        Assert.IsNull(FechaDelCalendario.Leer(string.Empty));
        Assert.AreEqual(string.Empty, FechaDelCalendario.Escribir(null));
    }

    /// <summary>Lo que no es una fecha llega al calendario vacío, y no revienta la pantalla.</summary>
    /// <remarks>
    /// La fecha sale del papel por OCR y puede traer cualquier cosa. Vacío es lo honesto:
    /// es exactamente lo que el árbol de Revisar hace con ella, dejarla «sin fecha de viaje».
    /// </remarks>
    [TestMethod]
    public void LoQueNoEsUnaFechaLlegaVacio()
    {
        Assert.IsNull(FechaDelCalendario.Leer("12 de sept"));
        Assert.IsNull(FechaDelCalendario.Leer("2026-02-30"));
        Assert.IsNull(FechaDelCalendario.Leer("08/09/2026"));
    }

    /// <summary>La hora del día que se le da al calendario es el mediodía, no la medianoche.</summary>
    /// <remarks>
    /// Es la mitad que impide que el defecto vuelva por otro camino: a mediodía, ningún huso
    /// de la Tierra —de -12 a +14— cae en otro día. A medianoche, casi la mitad lo hacen.
    /// </remarks>
    [TestMethod]
    public void AlCalendarioSeLeDaElMediodia()
    {
        var enElCalendario = FechaDelCalendario.Leer("2026-09-05");

        Assert.IsNotNull(enElCalendario);
        Assert.AreEqual(12, enElCalendario.Value.Hour);
        Assert.AreEqual(2026, enElCalendario.Value.Year);
        Assert.AreEqual(9, enElCalendario.Value.Month);
        Assert.AreEqual(5, enElCalendario.Value.Day);
    }
}
