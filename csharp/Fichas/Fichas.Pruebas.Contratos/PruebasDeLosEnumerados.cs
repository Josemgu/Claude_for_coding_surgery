using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.Contratos;

/// <summary>
/// Los enumerados de estado, y sobre todo lo que pasa con un texto que no se reconoce.
/// </summary>
/// <remarks>
/// El criterio es del que sale la prueba: la columna es texto libre SIN lista cerrada
/// (ARQUITECTURA §2.2), asi que el programa se va a encontrar valores que no conoce. El
/// requisito 9 dice que eso avisa, nunca impide: aqui se comprueba que no lanza.
/// </remarks>
[TestClass]
public sealed class PruebasDeLosEnumerados
{
    /// <summary>El estado de la recomendacion tiene exactamente tres valores.</summary>
    [TestMethod]
    public void ElEstadoDeLaRecomendacionTieneTresValores()
    {
        var valores = Enum.GetNames<EstadoDeRecomendacion>();

        CollectionAssert.AreEquivalent(
            new[] { "SinMarcar", "Completa", "NoCompleta" },
            valores);
    }

    /// <summary>El texto que escribe el Excel del companero se lee como el estado que toca.</summary>
    [TestMethod]
    public void ElTextoDelExcelSeLeeComoSuEstado()
    {
        Assert.AreEqual(EstadoDeRecomendacion.Completa, Caso.LeerEstado("completa"));
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, Caso.LeerEstado("no_completa"));
        Assert.AreEqual(EstadoDeRecomendacion.Completa, Caso.LeerEstado("  COMPLETA  "),
            "Los espacios y las mayusculas de una hoja no cambian lo que la hoja dice.");
    }

    /// <summary>Un estado que nadie conoce no lanza: cuenta como sin marcar.</summary>
    [TestMethod]
    public void UnEstadoDesconocidoNoTumbaNada()
    {
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, Caso.LeerEstado(null));
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, Caso.LeerEstado(""));
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, Caso.LeerEstado("incompleta"),
            "«incompleta» es uno de los valores viejos del Python: se lee, no rompe nada.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, Caso.LeerEstado("lo que sea"));
    }

    /// <summary>Lo que se guarda del estado vuelve a leerse igual.</summary>
    [TestMethod]
    public void ElEstadoVaYVuelveSinCambiar()
    {
        foreach (var estado in Enum.GetValues<EstadoDeRecomendacion>())
        {
            Assert.AreEqual(estado, Caso.LeerEstado(Caso.EscribirEstado(estado)));
        }
    }

    /// <summary>El texto crudo de la base no se pierde aunque no se reconozca.</summary>
    [TestMethod]
    public void ElTextoCrudoSeConservaAunqueNoSeReconozca()
    {
        var caso = new Caso { CreadoEn = "2026-09-04", EstadoRecomendacion = "no_indicada" };

        Assert.AreEqual("no_indicada", caso.EstadoRecomendacion, "Lo escrito en la base no se toca.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, caso.Estado);
    }

    /// <summary>El origen de un campo tiene los cuatro valores del CHECK del esquema.</summary>
    [TestMethod]
    public void ElOrigenDeUnCampoTieneLosCuatroDelEsquema()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Anotacion", "Ocr", "Vacio", "Manual" },
            Enum.GetNames<OrigenDeCampo>());
    }

    /// <summary>La procedencia apunta solo a las dos tablas que admite el CHECK.</summary>
    [TestMethod]
    public void LaProcedenciaApuntaSoloADosTablas()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Casos", "Personas" },
            Enum.GetNames<TablaDeProcedencia>());
    }

    /// <summary>Un aviso tiene tres pesos, y ninguno de ellos detiene nada.</summary>
    [TestMethod]
    public void UnAvisoTieneTresPesos()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Informacion", "Advertencia", "Problema" },
            Enum.GetNames<GravedadDeAviso>());
    }
}
