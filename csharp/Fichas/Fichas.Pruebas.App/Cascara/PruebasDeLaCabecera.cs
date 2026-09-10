using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// La cabecera del mockup: el titulo de la pantalla donde se esta, y la fecha de hoy.
/// </summary>
/// <remarks>
/// <para>Sale de <c>mockups/mockup-v2-inicio.html</c>, que dibuja
/// <c>&lt;h1&gt;Panel&lt;/h1&gt;</c> junto a <c>&lt;span class="hoy"&gt;jueves 3 de
/// septiembre de 2026&lt;/span&gt;</c>, y de su tabla de controles: «el icono pulsado queda
/// con fondo tinta y <b>la cabecera cambia de titulo</b>».</para>
///
/// <para>El mockup esta escrito para <c>tkinter</c> y a 1280 px; de el se copia <b>que</b>
/// dice la cabecera, no cuanto mide.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaCabecera
{
    /// <summary>Las nueve etiquetas del menu, que son las que la cabecera tiene que saber traducir.</summary>
    /// <remarks>
    /// Salen del propio XAML y no de una lista escrita a mano: si manana entra una decima
    /// entrada, esta prueba la exige sola en vez de darla por buena en silencio.
    /// </remarks>
    private static List<string> LasEtiquetasDelMenu()
        => [.. XDocument.Load(Path.Combine(LaCarpetaDeLaApp(), "Cascara", "VentanaPrincipal.xaml"))
            .Descendants()
            .Where(e => e.Name.LocalName == "NavigationViewItem")
            .Select(e => e.Attribute("Tag")?.Value)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)];

    /// <summary>Cada entrada del menu tiene su titulo, y ninguno se repite.</summary>
    [TestMethod]
    public void CadaEntradaDelMenuTieneSuTituloYNingunoSeRepite()
    {
        var etiquetas = LasEtiquetasDelMenu();
        var titulos = new List<string>();
        var vacios = new List<string>();

        foreach (var etiqueta in etiquetas)
        {
            var titulo = TituloDeLaPantalla.De(etiqueta);
            Console.WriteLine($"  {etiqueta,-11} -> «{titulo}»");

            if (string.IsNullOrWhiteSpace(titulo)) vacios.Add(etiqueta);
            else titulos.Add(titulo);
        }

        var repetidos = titulos.GroupBy(t => t, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"«{g.Key}» sale {g.Count()} veces")
            .ToList();

        Assert.HasCount(9, etiquetas, "El menu no tiene las nueve entradas: el barrido no dice nada.");
        Assert.IsEmpty(vacios, "Entradas sin titulo en la cabecera: " + string.Join(", ", vacios));
        Assert.IsEmpty(
            repetidos,
            "Dos pantallas con el mismo titulo: la cabecera deja de decir donde se esta." + Environment.NewLine
            + string.Join(Environment.NewLine, repetidos));
    }

    /// <summary>
    /// Una etiqueta que no se conozca no deja la cabecera en blanco.
    /// </summary>
    /// <remarks>
    /// El mismo trato que <c>VentanaPrincipal.PantallaDe</c>, que ante un nombre raro abre
    /// Inicio en vez de reventar: una cabecera vacia no dice nada y una excepcion cierra el
    /// programa.
    /// </remarks>
    [TestMethod]
    public void UnaEtiquetaDesconocidaNoDejaLaCabeceraEnBlanco()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(TituloDeLaPantalla.De("LoQueSea")));
        Assert.IsFalse(string.IsNullOrWhiteSpace(TituloDeLaPantalla.De("")));
    }

    /// <summary>La fecha de hoy se escribe en espanol, entera y sin hora.</summary>
    [TestMethod]
    public void LaFechaDeHoySeEscribeEnEspanolYSinHora()
    {
        var escrita = FechaDeLaCabecera.Larga(new DateTime(2026, 9, 9, 14, 35, 0, DateTimeKind.Local));
        Console.WriteLine($"9 de septiembre de 2026, 14:35 -> «{escrita}»");

        Assert.AreEqual("miércoles 9 de septiembre de 2026", escrita);
    }

    /// <summary>
    /// La fecha no depende del idioma de la maquina que la escriba.
    /// </summary>
    /// <remarks>
    /// ⛔ Sin cultura fijada, <c>ToString("D")</c> sale en el idioma de Windows. La regla
    /// permanente 4 dice «espanol en todo»: una maquina en ingles ensenaria «Wednesday».
    /// </remarks>
    [TestMethod]
    public void LaFechaSaleEnEspanolAunqueLaMaquinaEsteEnOtroIdioma()
    {
        var antes = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            var escrita = FechaDeLaCabecera.Larga(new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Local));
            Console.WriteLine($"con la maquina en en-US -> «{escrita}»");
            Assert.AreEqual("miércoles 9 de septiembre de 2026", escrita);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = antes;
        }
    }

    /// <summary>Los doce meses y los siete dias se escriben con su tilde donde toca.</summary>
    /// <remarks>
    /// «miércoles» y «sábado» llevan tilde; «marzo» y los demas meses no. Se recorre un ano
    /// entero para que ningun nombre se cuele sin mirar.
    /// </remarks>
    [TestMethod]
    public void NingunNombreDeDiaODeMesPierdeSuTilde()
    {
        (string Mal, string Bien)[] loQueLlevaTilde =
        [
            ("miercoles", "miércoles"), ("sabado", "sábado"),
        ];

        var malos = new List<string>();
        for (var dia = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local); dia.Year == 2026; dia = dia.AddDays(1))
        {
            var escrita = FechaDeLaCabecera.Larga(dia);
            foreach (var (mal, bien) in loQueLlevaTilde)
            {
                if (Regex.IsMatch(escrita, $@"(?<![\p{{L}}]){mal}(?![\p{{L}}])"))
                    malos.Add($"{dia:yyyy-MM-dd}: «{escrita}» deberia decir «{bien}»");
            }
        }

        Console.WriteLine($"Dias recorridos: 365. Fechas mal escritas: {malos.Count}.");
        Assert.IsEmpty(malos, string.Join(Environment.NewLine, malos.Take(5)));
    }

    private static string LaCarpetaDeLaApp()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var app = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App");
            if (Directory.Exists(app)) return app;
            actual = actual.Parent;
        }

        Assert.Inconclusive($"No se encontro «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}».");
        return string.Empty;
    }
}
