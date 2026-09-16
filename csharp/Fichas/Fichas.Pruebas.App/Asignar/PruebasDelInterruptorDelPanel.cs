using Fichas.App.Asignar;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// El botón «Equipo…» como interruptor: un clic abre, el siguiente cierra, y nunca revienta.
/// </summary>
/// <remarks>
/// <para>Nacen del defecto que el dueño anotó el 2026-09-16: <i>«En el botón Equipo, el
/// segundo clic friza todo el programa; y lo cierra también»</i>. Medido en el código ese
/// día: <c>PanelDelEquipo.Abrir</c> creaba un <c>Flyout</c> nuevo en CADA clic y volvía a
/// meter en un <c>Grid</c> nuevo los mismos controles que ya eran hijos del panel anterior;
/// WinUI no admite un elemento con dos padres y el proceso moría.</para>
///
/// <para>Lo que se prueba aquí es la regla del interruptor, que es lo único de ese gesto
/// que se puede medir sin ventana. Que el armazón se construya UNA sola vez se mide con la
/// ventana abierta (cinco clics seguidos y el proceso sigue vivo).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelInterruptorDelPanel
{
    /// <summary>Dado el panel cerrado, cuando se pulsa, entonces hay que abrirlo.</summary>
    [TestMethod]
    public void ConElPanelCerradoElClicAbre()
    {
        var interruptor = new InterruptorDelPanel();

        Assert.AreEqual(GestoDelPanel.Abrir, interruptor.AlPulsar());
        Assert.IsTrue(interruptor.EstaAbierto);
    }

    /// <summary>Dado el panel abierto, cuando se pulsa otra vez, entonces hay que cerrarlo: ni un segundo panel ni un fallo.</summary>
    [TestMethod]
    public void ConElPanelAbiertoElSegundoClicCierra()
    {
        var interruptor = new InterruptorDelPanel();
        interruptor.AlPulsar();

        Assert.AreEqual(GestoDelPanel.Cerrar, interruptor.AlPulsar());
        Assert.IsFalse(interruptor.EstaAbierto);
    }

    /// <summary>Dado que el panel se cerró por fuera (Escape o clic fuera), cuando se pulsa, entonces vuelve a abrir.</summary>
    [TestMethod]
    public void CerradoDesdeFueraElSiguienteClicVuelveAAbrir()
    {
        var interruptor = new InterruptorDelPanel();
        interruptor.AlPulsar();
        interruptor.AlCerrarse();

        Assert.IsFalse(interruptor.EstaAbierto);
        Assert.AreEqual(GestoDelPanel.Abrir, interruptor.AlPulsar());
    }

    /// <summary>Cinco clics seguidos alternan abrir y cerrar; es el gesto con el que se comprueba en la ventana.</summary>
    [TestMethod]
    public void CincoClicsSeguidosAlternanAbrirYCerrar()
    {
        var interruptor = new InterruptorDelPanel();

        var gestos = Enumerable.Range(0, 5).Select(_ => interruptor.AlPulsar()).ToList();

        CollectionAssert.AreEqual(
            new[] { GestoDelPanel.Abrir, GestoDelPanel.Cerrar, GestoDelPanel.Abrir, GestoDelPanel.Cerrar, GestoDelPanel.Abrir },
            gestos);
    }

    /// <summary>Avisar de que se cerró cuando ya estaba cerrado no cambia nada ni lanza.</summary>
    [TestMethod]
    public void CerrarseDosVecesNoLanzaNiCambiaNada()
    {
        var interruptor = new InterruptorDelPanel();
        interruptor.AlCerrarse();
        interruptor.AlCerrarse();

        Assert.IsFalse(interruptor.EstaAbierto);
    }
}
