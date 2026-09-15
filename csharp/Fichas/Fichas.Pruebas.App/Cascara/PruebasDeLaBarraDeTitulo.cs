using System.Xml.Linq;
using Fichas.App.Cascara;
using Microsoft.UI.Windowing;
using Windows.UI;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// La barra de título de la ventana toma el color del tema del programa.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> El dueño mandó el 2026-09-15 una captura de la
/// barra de título —«Fichas», minimizar, maximizar, cerrar— BLANCA con el programa en tema
/// oscuro, y dijo: «esta parte quiero que se ponga de color de Wind…». Medido sobre
/// <c>master</c> ese día, con el paquete compilado y <c>tema=oscuro</c> en
/// <c>preferencias.txt</c>: barra del sistema #F3F3F3, botón cerrar #171818, y DEBAJO una
/// segunda fila «Fichas» —el control <c>TitleBar</c> de WinUI, que estaba en el XAML sin
/// <c>ExtendsContentIntoTitleBar</c>— con fondo #202020. Dos barras con el mismo título.</para>
///
/// <para>De ahí salen cuatro cosas comprobables sin ventana:</para>
/// <list type="number">
///   <item>Con el tema oscuro la barra es oscura; con el claro, clara. No «un poco más
///   oscura»: la luminancia de una y otra están en extremos opuestos.</item>
///   <item>Los botones de la barra se leen sobre su fondo en los dos temas (4,5:1, que es lo
///   que exige la WCAG para texto), y también cuando la ventana está detrás de otra (3:1,
///   lo que exige para un control).</item>
///   <item>Pasar por encima y pulsar un botón se nota: su fondo cambia respecto al de la
///   barra.</item>
///   <item>El título «Fichas» y el icono se quedan, y hay UNA sola barra, con nombre, para
///   que la ventana la pueda declarar como su barra de título.</item>
/// </list>
///
/// <para>⚠️ <b>Lo que NO miran, porque no hay ventana:</b> que la barra se pinte de verdad
/// con estos colores, que cambie en vivo al pulsar el conmutador y que los tres botones
/// sigan funcionando. Eso se mide con el paquete publicado, captura y píxel, y va en la
/// entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaBarraDeTitulo
{
    /// <summary>Lo mínimo que exige la WCAG para texto normal.</summary>
    private const double ContrasteDeTexto = 4.5;

    /// <summary>Lo mínimo que exige la WCAG para un control o un glifo (1.4.11).</summary>
    private const double ContrasteDeControl = 3.0;

    /// <summary>Con el tema oscuro la barra es oscura; con el claro, clara.</summary>
    [TestMethod]
    public void LaBarraEsOscuraConElTemaOscuroYClaraConElClaro()
    {
        var oscura = ColoresDeLaBarraDeTitulo.Para(esOscuro: true);
        var clara = ColoresDeLaBarraDeTitulo.Para(esOscuro: false);

        Assert.IsLessThan(0.05, Luminancia(oscura.Fondo), $"El fondo oscuro {Hex(oscura.Fondo)} no es oscuro.");
        Assert.IsGreaterThan(0.80, Luminancia(clara.Fondo), $"El fondo claro {Hex(clara.Fondo)} no es claro.");
    }

    /// <summary>Los tres botones se leen sobre la barra, con la ventana delante y detrás.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LosBotonesSeLeenSobreLaBarra(bool esOscuro)
    {
        var paleta = ColoresDeLaBarraDeTitulo.Para(esOscuro);

        var activa = Contraste(paleta.Tinta, paleta.Fondo);
        var detras = Contraste(paleta.TintaInactiva, paleta.Fondo);
        Console.WriteLine($"{(esOscuro ? "oscuro" : "claro")}: tinta {activa:F2}:1 · inactiva {detras:F2}:1");

        Assert.IsGreaterThanOrEqualTo(ContrasteDeTexto, activa,
            $"Tinta {Hex(paleta.Tinta)} sobre {Hex(paleta.Fondo)} da {activa:F2}:1.");
        Assert.IsGreaterThanOrEqualTo(ContrasteDeControl, detras,
            $"Tinta inactiva {Hex(paleta.TintaInactiva)} sobre {Hex(paleta.Fondo)} da {detras:F2}:1.");
    }

    /// <summary>Y se siguen leyendo con el fondo de pasar por encima y el de pulsar.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LosBotonesSeLeenAlPasarPorEncimaYAlPulsar(bool esOscuro)
    {
        var paleta = ColoresDeLaBarraDeTitulo.Para(esOscuro);

        Assert.IsGreaterThanOrEqualTo(ContrasteDeTexto, Contraste(paleta.Tinta, paleta.FondoDelBotonAlPasar));
        Assert.IsGreaterThanOrEqualTo(ContrasteDeTexto, Contraste(paleta.Tinta, paleta.FondoDelBotonAlPulsar));
    }

    /// <summary>Pasar por encima de un botón se nota: su fondo no es el de la barra.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PasarPorEncimaDeUnBotonSeNota(bool esOscuro)
    {
        var paleta = ColoresDeLaBarraDeTitulo.Para(esOscuro);

        Assert.AreNotEqual(paleta.Fondo, paleta.FondoDelBotonAlPasar, "Al pasar por encima no cambia nada.");
        Assert.AreNotEqual(paleta.FondoDelBotonAlPasar, paleta.FondoDelBotonAlPulsar, "Pulsar se ve igual que pasar por encima.");
    }

    /// <summary>Los colores son opacos: un fondo con transparencia dejaría ver lo de detrás.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LosColoresDeLaBarraSonOpacos(bool esOscuro)
    {
        var paleta = ColoresDeLaBarraDeTitulo.Para(esOscuro);

        foreach (var color in new[] { paleta.Fondo, paleta.Tinta, paleta.TintaInactiva, paleta.FondoDelBotonAlPasar, paleta.FondoDelBotonAlPulsar })
        {
            Assert.AreEqual((byte)0xFF, color.A, $"{Hex(color)} no es opaco.");
        }
    }

    /// <summary>
    /// El marco de la ventana sigue la elección del dueño: oscuro, claro, o el de Windows.
    /// </summary>
    /// <remarks>
    /// «El de Windows» tiene que seguir siendo «el de Windows» también en el marco: si se
    /// tradujera a claro u oscuro fijo, la ventana dejaría de seguir al sistema justo en la
    /// parte que el dueño señaló.
    /// </remarks>
    [TestMethod]
    [DataRow(TemaDeLaVentana.Oscuro, TitleBarTheme.Dark)]
    [DataRow(TemaDeLaVentana.Claro, TitleBarTheme.Light)]
    [DataRow(TemaDeLaVentana.ElDeWindows, TitleBarTheme.UseDefaultAppMode)]
    public void ElMarcoSigueLaEleccionDelDueno(TemaDeLaVentana elegido, TitleBarTheme esperado)
        => Assert.AreEqual(esperado, TemasDeLaVentana.ComoLoPrefiereElMarco(elegido));

    /// <summary>Hay UNA barra de título, con nombre, con el título «Fichas» y con su icono.</summary>
    [TestMethod]
    public void HayUnaSolaBarraDeTituloConNombreTituloEIcono()
    {
        var xaml = XDocument.Load(Path.Combine(LaCarpetaDeLaApp(), "Cascara", "VentanaPrincipal.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var barras = xaml.Descendants().Where(e => e.Name.LocalName == "TitleBar").ToList();
        Assert.HasCount(1, barras, "Tiene que haber una barra de título y solo una.");

        var barra = barras[0];
        Assert.AreEqual("Fichas", (string?)barra.Attribute("Title"), "El título «Fichas» se queda.");
        Assert.IsFalse(string.IsNullOrWhiteSpace((string?)barra.Attribute(x + "Name")),
            "Sin nombre, la ventana no puede declararla como su barra de título.");
        Assert.IsTrue(barra.Elements().Any(e => e.Name.LocalName == "TitleBar.IconSource"), "El icono se queda.");
    }

    // ---- de dónde salen las cifras -------------------------------------------

    /// <summary>La razón de contraste de la WCAG entre dos colores opacos.</summary>
    private static double Contraste(Color uno, Color otro)
    {
        var a = Luminancia(uno);
        var b = Luminancia(otro);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>La luminancia relativa de la WCAG, entre 0 (negro) y 1 (blanco).</summary>
    private static double Luminancia(Color color)
    {
        static double Canal(byte v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(color.R)) + (0.7152 * Canal(color.G)) + (0.0722 * Canal(color.B));
    }

    /// <summary>El color como se lee en un mensaje: «#RRGGBB».</summary>
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>La carpeta <c>Fichas.App</c>, o no concluyente si no se encuentra.</summary>
    private static string LaCarpetaDeLaApp()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var app = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App");
            if (Directory.Exists(app)) return app;
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}». "
            + "Sin el XAML delante esto no comprueba nada.");
        return string.Empty;
    }
}
