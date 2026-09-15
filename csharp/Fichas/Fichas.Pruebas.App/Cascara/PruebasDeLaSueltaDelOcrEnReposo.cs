using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El motor de OCR se suelta solo cuando lleva un rato parado, y no antes.
/// </summary>
/// <remarks>
/// <para>Criterio (pase de memoria, 2026-09-15): <b>dado</b> un motor cargado que lleva
/// más del reposo sin leer, <b>cuando</b> la cáscara comprueba, <b>entonces</b> lo suelta
/// una vez; <b>dado</b> un motor sin cargar, o cargado pero leyendo hace menos del reposo,
/// <b>entonces</b> no lo toca. Así una tanda de importación —una hoja cada pocos
/// segundos— nunca pierde el motor a medias, y un motor parado tras la tanda o mientras
/// Miguel corrige a mano devuelve su memoria.</para>
///
/// <para>Se prueba la decisión, no el reloj: la clase recibe cuánto lleva sin leer y qué
/// hacer para soltar, y aquí se le dan valores a mano.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaSueltaDelOcrEnReposo
{
    /// <summary>Treinta segundos, como en la cáscara.</summary>
    private static readonly TimeSpan Reposo = TimeSpan.FromSeconds(30);

    /// <summary>Dado un motor sin cargar, cuando se comprueba, entonces no se suelta nada.</summary>
    [TestMethod]
    public void SinMotorNoSueltaNada()
    {
        var veces = 0;
        var suelta = new SueltaDelOcrEnReposo(() => null, () => veces++, Reposo);

        Assert.IsFalse(suelta.Comprobar());
        Assert.AreEqual(0, veces);
    }

    /// <summary>Dado un motor que leyó hace menos del reposo, cuando se comprueba, entonces se queda.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(10)]
    [DataRow(29)]
    public void ConElMotorEnUsoRecienteSeQueda(int segundosSinLeer)
    {
        var veces = 0;
        var suelta = new SueltaDelOcrEnReposo(() => TimeSpan.FromSeconds(segundosSinLeer), () => veces++, Reposo);

        Assert.IsFalse(suelta.Comprobar());
        Assert.AreEqual(0, veces);
    }

    /// <summary>Dado un motor parado más del reposo, cuando se comprueba, entonces se suelta una vez.</summary>
    [TestMethod]
    [DataRow(30)]
    [DataRow(31)]
    [DataRow(600)]
    public void ConElMotorParadoLoSuelta(int segundosSinLeer)
    {
        var veces = 0;
        var suelta = new SueltaDelOcrEnReposo(() => TimeSpan.FromSeconds(segundosSinLeer), () => veces++, Reposo);

        Assert.IsTrue(suelta.Comprobar());
        Assert.AreEqual(1, veces);
    }
}
