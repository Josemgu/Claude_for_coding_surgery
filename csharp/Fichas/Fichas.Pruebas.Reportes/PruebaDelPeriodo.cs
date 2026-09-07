using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El periodo: los dos extremos entran, y el limite de arriba de las marcas con hora es otro.
/// </summary>
/// <remarks>
/// Portado de reportes/periodo.py con una diferencia de contrato, y es a proposito: el
/// Python LEVANTA `ErrorDePeriodo` y aqui no se levanta nada (requisito 9 del dueno,
/// «avisar, nunca impedir»). Lo que en Python era una excepcion aqui es un
/// <see cref="Fichas.Contratos.Modelos.Aviso"/> devuelto.
/// </remarks>
[TestClass]
public class PruebaDelPeriodo
{
    [TestMethod]
    public void LosDosExtremosEntran()
    {
        var lectura = Periodo.Leer("2026-09-01", "2026-09-30");

        Assert.IsNotNull(lectura.Periodo);
        Assert.AreEqual("2026-09-01", lectura.Periodo!.Desde);
        Assert.AreEqual("2026-09-30", lectura.Periodo.Hasta);
        Assert.IsNull(lectura.Problema);
    }

    [TestMethod]
    public void SiguienteAHastaEsElDiaDeDespuesParaLasMarcasConHora()
    {
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

        Assert.AreEqual("2026-10-01", periodo.SiguienteAHasta);
        // '2026-09-30 14:12:03' es mayor que '2026-09-30' comparado como texto.
        Assert.IsTrue(periodo.ContieneMarcaConHora("2026-09-30 14:12:03"));
        Assert.IsFalse(periodo.ContieneMarcaConHora("2026-10-01 00:00:01"));
        Assert.IsFalse(periodo.ContieneMarcaConHora(null));
    }

    [TestMethod]
    public void UnPeriodoDeUnSoloDiaEsLegitimo()
    {
        var periodo = Periodo.Leer("2026-09-15", "2026-09-15").Periodo!;

        Assert.AreEqual("2026-09-16", periodo.SiguienteAHasta);
        Assert.AreEqual("el 15 de septiembre de 2026", periodo.EnTexto());
    }

    [TestMethod]
    public void UnPeriodoDelRevesNoSeLanza_SeAvisa()
    {
        var lectura = Periodo.Leer("2026-09-30", "2026-09-01");

        Assert.IsNull(lectura.Periodo);
        Assert.IsNotNull(lectura.Problema);
        StringAssert.Contains(lectura.Problema!.Linea, "del revés");
    }

    [TestMethod]
    public void UnaFechaQueNoEsFechaNoSeLanza_SeAvisa()
    {
        var lectura = Periodo.Leer("septiembre", "2026-09-30");

        Assert.IsNull(lectura.Periodo);
        Assert.IsNotNull(lectura.Problema);
    }

    [TestMethod]
    public void ElMesEnteroVaDelDiaUnoAlUltimo()
    {
        Assert.AreEqual("2026-09-30", Periodo.DelMes(2026, 9).Periodo!.Hasta);
        Assert.AreEqual("2026-02-28", Periodo.DelMes(2026, 2).Periodo!.Hasta);
        Assert.AreEqual("2024-02-29", Periodo.DelMes(2024, 2).Periodo!.Hasta);
        Assert.AreEqual("2026-12-31", Periodo.DelMes(2026, 12).Periodo!.Hasta);
    }

    [TestMethod]
    public void ElTextoDelPeriodoVaEnEspanolYNoDependeDelIdiomaDelSistema()
    {
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

        Assert.AreEqual("del 1 de septiembre de 2026 al 30 de septiembre de 2026", periodo.EnTexto());
        Assert.AreEqual("2026-09-01_a_2026-09-30", periodo.NombreCorto());
    }

    [TestMethod]
    public void ContieneFechaDeViajeMiraLosDosExtremos()
    {
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

        Assert.IsTrue(periodo.ContieneFecha("2026-09-01"));
        Assert.IsTrue(periodo.ContieneFecha("2026-09-30"));
        Assert.IsFalse(periodo.ContieneFecha("2026-08-31"));
        Assert.IsFalse(periodo.ContieneFecha("2026-10-01"));
        Assert.IsFalse(periodo.ContieneFecha(null));
    }
}
