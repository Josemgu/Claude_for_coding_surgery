using System.Globalization;
using System.Xml.Linq;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Si el dueño elige un tema, las pantallas le hacen caso, y en los dos temas se lee.
/// </summary>
/// <remarks>
/// <para>El criterio del que salen son las palabras del dueño del 2026-09-05 —«dame opción
/// también de cambiar el tema del sistema»— más lo que ya nos dijo dos veces: que no quiere
/// texto que se pierda. De ahí salen tres cosas comprobables sin ventana:</para>
/// <list type="number">
///   <item>Una pantalla clavada en un tema con <c>RequestedTheme</c> ignora la elección del
///   dueño. Solo se admite la excepción que esté nombrada aquí, con su motivo.</item>
///   <item>La paleta compartida trae los DOS temas. Una clave con solo uno deja la mitad de
///   la pantalla pintada con el color del otro.</item>
///   <item>Cada par de texto sobre fondo pasa 4,5:1 en los dos temas.</item>
/// </list>
///
/// <para><b>El defecto que las hizo nacer, MEDIDO el 2026-09-05 sobre el paquete publicado:</b>
/// con el tema claro, la franja de avisos daba <b>1,70:1</b> —texto casi negro (#070603) sobre
/// marrón oscuro (#433519)—. El motivo: el fondo lo ponía el código leyendo de
/// <c>Application.Current.Resources</c>, que resuelve con el tema de la APLICACIÓN, mientras
/// que el texto seguía el del elemento. Dos temas peleando dentro del mismo control. La
/// franja es lo que el dueño lee cuando algo va mal.</para>
///
/// <para>⚠️ Lo que NO miran: cómo se ve. El contraste se calcula sobre los colores
/// declarados, no sobre píxeles; las capturas van en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosDosTemas
{
    /// <summary>
    /// La única pantalla a la que hoy se le admite quedarse clavada en un tema, y por qué.
    /// </summary>
    /// <remarks>
    /// ⛔ Es una excepción con fecha, no una regla. El 2026-09-05 su programador estaba
    /// trabajando en esa carpeta y el coordinador se reservó pedírselo él. Quitar esta línea
    /// cuando ese pase cierre es todo lo que hace falta; la prueba ya está escrita.
    /// </remarks>
    private static readonly string[] LaExcepcionDeHoy = ["PaginaDeInicio.xaml"];

    /// <summary>Los pares de texto sobre fondo que la paleta tiene que dejar legibles.</summary>
    /// <remarks>
    /// <c>Linea</c> no está: es un borde de 1 píxel, no texto, y exigirle 4,5:1 obligaría a
    /// dibujar las tarjetas con una raya negra alrededor.
    /// </remarks>
    private static readonly (string Texto, string Fondo)[] LosParesQueSeLeen =
    [
        ("Tinta", "Papel"), ("Tinta", "Panel"),
        ("TintaSuave", "Papel"), ("TintaSuave", "Panel"),
        ("Apagado", "Papel"), ("Apagado", "Panel"),
    ];

    /// <summary>Lo mínimo que exige la WCAG para texto normal.</summary>
    private const double ContrasteMinimo = 4.5;

    /// <summary>Ninguna pantalla se clava en un tema, con el denominador delante.</summary>
    [TestMethod]
    public void NingunaPantallaIgnoraElTemaQueEligioElDueno()
    {
        var pantallas = LosXamlDeLaApp();
        var clavadas = new List<string>();

        foreach (var archivo in pantallas)
        {
            var nombre = Path.GetFileName(archivo);
            if (LaExcepcionDeHoy.Contains(nombre, StringComparer.Ordinal)) continue;

            foreach (var elemento in XDocument.Load(archivo).Descendants())
            {
                var puesto = elemento.Attribute("RequestedTheme")?.Value;
                if (!string.IsNullOrWhiteSpace(puesto))
                    clavadas.Add($"{nombre} → {elemento.Name.LocalName} RequestedTheme=\"{puesto}\"");
            }
        }

        Console.WriteLine($"Pantallas clavadas en un tema: {clavadas.Count} de {pantallas.Count} archivos .xaml.");

        Assert.IsGreaterThanOrEqualTo(
            10, pantallas.Count, "Un barrido que no encontró pantallas no comprueba nada.");
        Assert.IsEmpty(
            clavadas,
            "Estas pantallas ignoran el tema que eligió el dueño:" + Environment.NewLine
            + string.Join(Environment.NewLine, clavadas));
    }

    /// <summary>La paleta compartida trae los dos temas, y las mismas claves en cada uno.</summary>
    [TestMethod]
    public void LaPaletaTraeLosDosTemasConLasMismasClaves()
    {
        var claro = LaPaletaDe("Light");
        var oscuro = LaPaletaDe("Dark");

        Console.WriteLine($"Claves de la paleta: {claro.Count} en claro, {oscuro.Count} en oscuro.");

        Assert.IsGreaterThanOrEqualTo(6, claro.Count, "La paleta compartida está casi vacía.");
        CollectionAssert.AreEquivalent(
            claro.Keys.ToList(),
            oscuro.Keys.ToList(),
            "Una clave que solo existe en un tema deja ese trozo pintado con el color del otro.");
    }

    /// <summary>Cada par de texto sobre fondo se lee en los dos temas.</summary>
    [TestMethod]
    [DataRow("Light")]
    [DataRow("Dark")]
    public void CadaParDeTextoSobreFondoSeLee(string tema)
    {
        var paleta = LaPaletaDe(tema);
        var malos = new List<string>();

        foreach (var (texto, fondo) in LosParesQueSeLeen)
        {
            var razon = Contraste(paleta[texto], paleta[fondo]);
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-8} {1,-11} sobre {2,-6} {3} sobre {4}  {5,5:N2}:1",
                tema, texto, fondo, paleta[texto], paleta[fondo], razon));

            if (razon < ContrasteMinimo)
                malos.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} ({2}) sobre {3} ({4}) da {5:N2}:1 y hacen falta {6:N1}:1",
                    tema, texto, paleta[texto], fondo, paleta[fondo], razon, ContrasteMinimo));
        }

        Assert.IsEmpty(malos, string.Join(Environment.NewLine, malos));
    }

    /// <summary>
    /// La franja de avisos no saca sus colores del diccionario de la aplicación.
    /// </summary>
    /// <remarks>
    /// Es la causa medida del 1,70:1. <c>Application.Current.Resources</c> resuelve con el
    /// tema de la APLICACIÓN, que se fija al arrancar; el texto de al lado sigue el del
    /// elemento. Con los dos temas distintos, el fondo y la letra se acercan hasta perderse.
    /// </remarks>
    [TestMethod]
    public void LaFranjaDeAvisosNoTomaSusColoresDelTemaDeLaAplicacion()
    {
        // Los comentarios NO cuentan: el archivo explica ahí dentro por qué no se hace, y una
        // prueba que se pusiera roja por su propia documentación empujaría a borrarla.
        var codigo = File.ReadAllLines(
                Path.Combine(LaCarpetaDeLaApp(), "Cascara", "FranjaDeAvisos.xaml.cs"))
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .ToList();

        var malas = codigo
            .Select((linea, i) => (Numero: i + 1, Linea: linea))
            .Where(l => l.Linea.Contains("Application.Current.Resources", StringComparison.Ordinal))
            .Select(l => $"FranjaDeAvisos.xaml.cs:{l.Numero}: {l.Linea.Trim()}")
            .ToList();

        Console.WriteLine($"Líneas de código leídas: {codigo.Count}.");

        Assert.IsGreaterThanOrEqualTo(
            40, codigo.Count, "Un barrido que no encontró código no comprueba nada.");
        Assert.IsEmpty(
            malas,
            "El fondo de la franja tiene que resolverse con el tema del ELEMENTO, no con el "
            + "de la aplicación: si no, el fondo va por un tema y la letra por el otro."
            + Environment.NewLine + string.Join(Environment.NewLine, malas));
    }

    // ---- de donde salen las cifras -------------------------------------------

    /// <summary>La paleta de un tema, leída de <c>App.xaml</c>, por clave y color.</summary>
    private static Dictionary<string, string> LaPaletaDe(string tema)
    {
        var doc = XDocument.Load(Path.Combine(LaCarpetaDeLaApp(), "App.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var diccionario = doc.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary")
            .FirstOrDefault(e => (string?)e.Attribute(x + "Key") == tema);

        Assert.IsNotNull(diccionario, $"App.xaml no declara el diccionario del tema «{tema}».");

        return diccionario.Descendants()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                e => (string)e.Attribute(x + "Key")!,
                e => (string)e.Attribute("Color")!,
                StringComparer.Ordinal);
    }

    /// <summary>La razón de contraste de la WCAG entre dos colores «#RRGGBB».</summary>
    private static double Contraste(string uno, string otro)
    {
        var a = Luminancia(uno);
        var b = Luminancia(otro);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>La luminancia relativa de la WCAG de un color «#RRGGBB».</summary>
    private static double Luminancia(string color)
    {
        var limpio = color.TrimStart('#');
        Assert.HasCount(6, limpio, $"«{color}» no tiene la forma #RRGGBB.");

        double Canal(int desde)
        {
            var v = int.Parse(limpio.Substring(desde, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(0)) + (0.7152 * Canal(2)) + (0.0722 * Canal(4));
    }

    /// <summary>Todos los <c>.xaml</c> de la app, sin lo generado.</summary>
    private static List<string> LosXamlDeLaApp()
        => [.. Directory
            .EnumerateFiles(LaCarpetaDeLaApp(), "*.xaml", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))];

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
            + "Sin las pantallas delante esto no comprueba nada.");
        return string.Empty;
    }
}
