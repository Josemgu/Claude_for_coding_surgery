using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Las tres metricas de trabajo del equipo, cada una con la columna de fecha que usa.
/// </summary>
/// <remarks>Portado de reportes/metricas.py.</remarks>
[TestClass]
public class PruebaDeLasMetricas
{
    /// <summary>El periodo de todas las pruebas de esta clase: septiembre de 2026 entero.</summary>
    private static readonly Periodo Septiembre = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

    /// <summary>Un caso con su verificación, escrito a mano con lo justo para cada métrica.</summary>
    /// <param name="id">El id del caso; el número sale de él.</param>
    /// <param name="campos">Cuántos campos de procedencia tiene.</param>
    /// <param name="verificados">Cuántos de ellos están firmados.</param>
    /// <param name="verificadoEn">La marca de la última firma, o nula.</param>
    /// <param name="creadoEn">La marca de importación.</param>
    /// <param name="fechaViaje">La fecha de viaje, o nula.</param>
    /// <param name="estado">El estado de la recomendación, o nulo.</param>
    private static CasoConSuVerificacion Caso(
        long id, int campos, int verificados, string? verificadoEn,
        string creadoEn = "2026-08-01 09:00:00", string? fechaViaje = null, string? estado = null)
        => new(
            new Caso { Id = id, NumeroCaso = $"CASP{id:D4}", CreadoEn = creadoEn, FechaViaje = fechaViaje, EstadoRecomendacion = estado },
            Personas: 1, Campos: campos, CamposVerificados: verificados, VerificadoEn: verificadoEn);

    /// <summary>Vigila que un caso con cero campos no cuenta como verificado, y uno con todos firmados sí.</summary>
    [TestMethod]
    public void CeroCamposNoEsTodoVerificado()
    {
        Assert.IsFalse(Metricas.CasoVerificado(Caso(1, campos: 0, verificados: 0, verificadoEn: null)),
            "Cero campos sin verificar significa que nadie leyo nada, no que todo este verificado.");
        Assert.IsTrue(Metricas.CasoVerificado(Caso(2, campos: 3, verificados: 3, verificadoEn: "2026-09-10 10:00:00")));
        Assert.IsFalse(Metricas.CasoVerificado(Caso(3, campos: 3, verificados: 2, verificadoEn: "2026-09-10 10:00:00")));
    }

    /// <summary>Vigila que una verificación de la tarde del último día del periodo entra en la métrica 1.</summary>
    [TestMethod]
    public void LaMarcaConHoraDelUltimoDiaDelPeriodoEntra()
    {
        // '2026-09-30 14:12:03' es mayor que '2026-09-30' comparado como texto: un `<=`
        // se dejaria fuera todo lo que paso ese dia despues de medianoche.
        var casos = new[] { Caso(1, 2, 2, "2026-09-30 14:12:03") };

        Assert.HasCount(1, Metricas.CasosVerificadosEnElPeriodo(casos, Septiembre));
    }

    /// <summary>Vigila que una verificación anterior a la importación no entra en el promedio y sí en los descartados.</summary>
    [TestMethod]
    public void UnaDemoraNegativaSeDescartaYSeCuentaAparte()
    {
        var casos = new[]
        {
            Caso(1, 2, 2, "2026-09-10 10:00:00", creadoEn: "2026-09-09 10:00:00"),  // 24 h
            Caso(2, 2, 2, "2026-09-10 10:00:00", creadoEn: "2026-09-11 10:00:00"),  // reloj movido
        };

        var demora = Metricas.DemoraDeImportarAVerificar(casos, Septiembre);

        Assert.AreEqual(1, demora.CasosMedidos);
        Assert.AreEqual(24.0, demora.PromedioEnHoras!.Value, 0.001);
        Assert.AreEqual(1, demora.DescartadosPorFechasIncoherentes);
    }

    /// <summary>Vigila que una fecha ilegible deja el promedio nulo en vez de sumar un cero.</summary>
    [TestMethod]
    public void UnaFechaIlegibleNoSeCuentaComoCero()
    {
        var casos = new[] { Caso(1, 2, 2, "2026-09-10 10:00:00", creadoEn: "no es una fecha") };

        var demora = Metricas.DemoraDeImportarAVerificar(casos, Septiembre);

        Assert.AreEqual(0, demora.CasosMedidos);
        Assert.IsNull(demora.PromedioEnHoras, "Un cero se sumaria al promedio como si se hubiera verificado al instante.");
        Assert.AreEqual(1, demora.DescartadosPorFechasIncoherentes);
    }

    /// <summary>Vigila que los cinco cubos de la métrica 3 suman exactamente los casos del periodo.</summary>
    [TestMethod]
    public void LosCincoCubosDeLaDeteccionSonExcluyentesYSumanElTotal()
    {
        var casos = new[]
        {
            Caso(1, 1, 1, "2026-09-05 10:00:00", fechaViaje: "2026-09-10", estado: "no_completa"), // a tiempo
            Caso(2, 1, 1, "2026-09-12 10:00:00", fechaViaje: "2026-09-10", estado: "no_completa"), // tarde
            Caso(3, 0, 0, null, fechaViaje: "2026-09-10", estado: "no_completa"),                  // sin fecha de deteccion
            Caso(4, 1, 1, "2026-09-05 10:00:00", fechaViaje: "2026-09-10", estado: null),          // nadie dijo nada
            Caso(5, 1, 1, "2026-09-05 10:00:00", fechaViaje: "2026-09-10", estado: "completa"),    // resuelta
            Caso(6, 1, 1, "2026-09-05 10:00:00", fechaViaje: "2026-10-15", estado: "no_completa"), // fuera del periodo
        };

        var deteccion = Metricas.DeteccionAntesDelViaje(casos, Septiembre);

        Assert.AreEqual(5, deteccion.CasosDelPeriodo);
        Assert.AreEqual(1, deteccion.DetectadosATiempo);
        Assert.AreEqual(1, deteccion.DetectadosDespuesDelViaje);
        Assert.AreEqual(1, deteccion.ConProblemaSinFechaDeDeteccion);
        Assert.AreEqual(1, deteccion.SinEstadoRegistrado);
        Assert.AreEqual(1, deteccion.ConRecomendacionResuelta);
        Assert.AreEqual(3, deteccion.ConProblemaRegistrado);
        Assert.AreEqual(
            deteccion.CasosDelPeriodo,
            deteccion.DetectadosATiempo + deteccion.DetectadosDespuesDelViaje
                + deteccion.ConProblemaSinFechaDeDeteccion + deteccion.SinEstadoRegistrado
                + deteccion.ConRecomendacionResuelta,
            "Si los cinco cubos no suman el universo, algo se conto dos veces.");
    }

    /// <summary>Vigila que verificar la mañana del viaje cuenta como tarde, no como a tiempo.</summary>
    [TestMethod]
    public void ATiempoEsElDiaAnteriorOAntes_NoElMismoDia()
    {
        var mismoDia = new[] { Caso(1, 1, 1, "2026-09-10 08:00:00", fechaViaje: "2026-09-10", estado: "no_completa") };

        var deteccion = Metricas.DeteccionAntesDelViaje(mismoDia, Septiembre);

        Assert.AreEqual(0, deteccion.DetectadosATiempo,
            "La manana del viaje no deja margen para arreglar una recomendacion.");
        Assert.AreEqual(1, deteccion.DetectadosDespuesDelViaje);
    }

    /// <summary>Vigila que un caso sin estado escrito va a su propio cubo y no al de problemas.</summary>
    [TestMethod]
    public void UnEstadoVacioNoCuentaComoProblema()
    {
        var casos = new[] { Caso(1, 1, 1, "2026-09-05 10:00:00", fechaViaje: "2026-09-10", estado: null) };

        var deteccion = Metricas.DeteccionAntesDelViaje(casos, Septiembre);

        Assert.AreEqual(0, deteccion.ConProblemaRegistrado,
            "Un numero de mas en un reporte a los jefes es una cifra falsa.");
        Assert.AreEqual(1, deteccion.SinEstadoRegistrado);
    }
}
