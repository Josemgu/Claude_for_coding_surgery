using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// La tarjeta de un duplicado: en rojo entero, con la palabra «DUPLICADO» y el botón que toca.
/// </summary>
/// <remarks>
/// <para>Palabras del dueño, 2026-09-14: <i>«Cuando un caso esté duplicado, ponlo en rojo
/// completo, que diga duplicado, y agrega la función de unificar el caso duplicado con el caso
/// original, para que se elimine el duplicado»</i>.</para>
///
/// <para>Se prueban dos cosas y de dos formas. La <b>decisión</b> —qué tarjeta es duplicado,
/// cuál ofrece unificar y cuál ofrece quitar la marca— se prueba sobre <see cref="TarjetaDeDocumento"/>
/// y <see cref="TableroDeRevisar"/>, sin ventana. El <b>color</b> se mide sobre el XAML y sobre
/// los valores declarados en <c>Inicio/PinturaDeInicio.cs</c>, porque <c>PinturaDeInicio</c>
/// construye pinceles de XAML que no existen fuera de una ventana (medido en
/// <c>Inicio/PruebasDelColorDeLoQueNoTieneANadie</c>). Los colores son datos, y un dato se lee.</para>
///
/// <para>⚠️ Lo que NO miran: los píxeles. Las capturas en claro y oscuro van en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaTarjetaDelDuplicado
{
    /// <summary>Lo mínimo que exige la WCAG para texto normal.</summary>
    private const double ContrasteMinimo = 4.5;

    /// <summary>Un duplicado cuyo original está en la base ofrece unificar, y lo dice con la palabra.</summary>
    [TestMethod]
    public void UnDuplicadoConOriginalOfreceUnificar()
    {
        var banco = new BancoDeCarpetas();
        var original = banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\CASP2609_Ana.pdf", 1);
        var duplicado = banco.Meter("CASP2609", null, "700001", "Castries Branch", @"C:\pdf\CASP2609_Ana.pdf", 2);
        MarcarComoDuplicado(banco, duplicado, original);

        var tarjeta = banco.ComoLoVeLaPantalla().Single(t => t.CasoId == duplicado);

        Assert.IsTrue(tarjeta.EsDuplicado);
        Assert.IsTrue(tarjeta.TieneOriginal);
        Assert.IsTrue(tarjeta.SePuedeUnificar);
        Assert.IsFalse(tarjeta.SePuedeQuitarLaMarca);
        Assert.AreEqual("DUPLICADO", tarjeta.PalabraDeDuplicado);
        Assert.AreEqual("duplicado de CASP2609_Ana.pdf hoja 1", tarjeta.MarcaDeDuplicado);
        Assert.Contains("Unificar", tarjeta.NombreDelBotonDeUnificar);
        Assert.Contains("CASP2609_Ana.pdf", tarjeta.NombreDelBotonDeUnificar, "El botón nombra su documento para quien no ve la pantalla.");
    }

    /// <summary>Un duplicado cuyo original ya no está ofrece quitar la marca, no unificar.</summary>
    [TestMethod]
    public void UnDuplicadoSinOriginalOfreceQuitarLaMarca()
    {
        var banco = new BancoDeCarpetas();
        var huerfano = banco.Meter("CASP2609", null, "700001", "Castries Branch", @"C:\pdf\CASP2609_Ana.pdf", 2);
        MarcarComoDuplicado(banco, huerfano, 9999);

        var tarjeta = banco.ComoLoVeLaPantalla().Single(t => t.CasoId == huerfano);

        Assert.IsTrue(tarjeta.EsDuplicado);
        Assert.IsFalse(tarjeta.TieneOriginal);
        Assert.IsFalse(tarjeta.SePuedeUnificar);
        Assert.IsTrue(tarjeta.SePuedeQuitarLaMarca);
        Assert.AreEqual("duplicado de un documento que ya no está en la base", tarjeta.MarcaDeDuplicado);
        Assert.Contains("Quitar la marca", tarjeta.NombreDelBotonDeQuitarLaMarca);
    }

    /// <summary>Un documento normal no es duplicado ni ofrece ninguno de los dos botones.</summary>
    [TestMethod]
    public void UnDocumentoNormalNoOfreceNinguno()
    {
        var banco = new BancoDeCarpetas();
        var normal = banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch");

        var tarjeta = banco.ComoLoVeLaPantalla().Single(t => t.CasoId == normal);

        Assert.IsFalse(tarjeta.EsDuplicado);
        Assert.IsFalse(tarjeta.SePuedeUnificar);
        Assert.IsFalse(tarjeta.SePuedeQuitarLaMarca);
    }

    /// <summary>El original de un duplicado NO se pone en rojo: solo el que repite.</summary>
    [TestMethod]
    public void ElOriginalNoSeMarca()
    {
        var banco = new BancoDeCarpetas();
        var original = banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\a.pdf", 1);
        var duplicado = banco.Meter("CASP2609", null, "700001", "Castries Branch", @"C:\pdf\a.pdf", 1);
        MarcarComoDuplicado(banco, duplicado, original);

        var tarjeta = banco.ComoLoVeLaPantalla().Single(t => t.CasoId == original);

        Assert.IsFalse(tarjeta.EsDuplicado);
        Assert.IsFalse(tarjeta.SePuedeUnificar);
    }

    /// <summary>
    /// En el XAML, la capa roja se pinta con <c>PinturaDeInicio.RojoFondo</c> y <c>RojoMarca</c>,
    /// solo se ve con <c>EsDuplicado</c>, y lleva dentro la palabra: el color nunca va solo.
    /// </summary>
    [TestMethod]
    public void LaCapaRojaUsaLaPinturaDeInicioYLlevaLaPalabra()
    {
        var pantalla = XDocument.Load(LaPantallaDeRevisar());

        var capa = pantalla.Descendants()
            .Where(e => e.Name.LocalName == "Border")
            .SingleOrDefault(e => (e.Attribute("Background")?.Value ?? string.Empty).Contains("PinturaDeInicio.RojoFondo", StringComparison.Ordinal));

        Assert.IsNotNull(capa, "No hay ninguna capa con fondo PinturaDeInicio.RojoFondo en la tarjeta.");
        Assert.Contains("PinturaDeInicio.RojoMarca", capa.Attribute("BorderBrush")?.Value ?? string.Empty, "El borde va con RojoMarca.");
        Assert.Contains(nameof(TarjetaDeDocumento.EsDuplicado), capa.Attribute("Visibility")?.Value ?? string.Empty, "Solo se ve en un duplicado.");

        var palabra = pantalla.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock")
            .SingleOrDefault(e => (e.Attribute("Text")?.Value ?? string.Empty).Contains(nameof(TarjetaDeDocumento.PalabraDeDuplicado), StringComparison.Ordinal));
        Assert.IsNotNull(palabra, "La tarjeta roja tiene que decir la palabra: el color nunca va solo.");
        Assert.Contains("PinturaDeInicio.RojoMarca", palabra.Attribute("Foreground")?.Value ?? string.Empty, "La palabra va en RojoMarca sobre RojoFondo.");
        Assert.Contains(nameof(TarjetaDeDocumento.EsDuplicado), palabra.Attribute("Visibility")?.Value ?? string.Empty);
    }

    /// <summary>Los dos botones del duplicado están en el XAML y cada uno se ve solo cuando toca.</summary>
    [TestMethod]
    public void LosDosBotonesSeVenSoloCuandoToca()
    {
        var pantalla = XDocument.Load(LaPantallaDeRevisar());
        var botones = pantalla.Descendants().Where(e => e.Name.LocalName == "Button").ToList();

        var unificar = botones.SingleOrDefault(b => (b.Attribute("Click")?.Value ?? string.Empty) == "AlPulsarUnificar");
        var quitar = botones.SingleOrDefault(b => (b.Attribute("Click")?.Value ?? string.Empty) == "AlPulsarQuitarLaMarcaDeDuplicado");

        Assert.IsNotNull(unificar, "Falta el botón de unificar.");
        Assert.Contains(nameof(TarjetaDeDocumento.SePuedeUnificar), unificar.Attribute("Visibility")?.Value ?? string.Empty);
        Assert.AreEqual("Unificar con el original", unificar.Attribute("Content")?.Value);
        Assert.IsNotNull(quitar, "Falta el botón de quitar la marca.");
        Assert.Contains(nameof(TarjetaDeDocumento.SePuedeQuitarLaMarca), quitar.Attribute("Visibility")?.Value ?? string.Empty);
    }

    /// <summary>La palabra en RojoMarca se lee sobre RojoFondo en los dos temas, con los valores de <c>PinturaDeInicio.cs</c>.</summary>
    /// <param name="tema">«Claro» u «Oscuro», el prefijo de las variables de la paleta.</param>
    [TestMethod]
    [DataRow("Claro")]
    [DataRow("Oscuro")]
    public void LaPalabraSeLeeSobreElRojoEnLosDosTemas(string tema)
    {
        var paleta = File.ReadAllText(LaPinturaDeInicio());
        var marca = ColorDeclarado(paleta, tema + "RojoMarca");
        var fondo = ColorDeclarado(paleta, tema + "RojoFondo");
        var razon = Contraste(marca, fondo);

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1} sobre {2} = {3:N2}:1", tema, marca, fondo, razon));

        Assert.IsGreaterThanOrEqualTo(ContrasteMinimo, razon, $"{tema}: RojoMarca sobre RojoFondo da {razon:N2}:1.");
    }

    /// <summary>Deja a un caso del banco señalando a otro id, como lo hace el importador.</summary>
    /// <param name="banco">El banco de la prueba.</param>
    /// <param name="casoId">El que repite.</param>
    /// <param name="deCual">Al que repite; puede no existir.</param>
    private static void MarcarComoDuplicado(BancoDeCarpetas banco, long casoId, long deCual)
    {
        var caso = banco.Servicios.Casos.Obtener(casoId)!;
        Assert.IsTrue(banco.Servicios.Casos.Guardar(caso with { DuplicadoDe = deCual }).SeEscribio);
    }

    /// <summary>El valor «#RRGGBB» de una variable <c>Pintar(0xRR, 0xGG, 0xBB)</c> de la paleta.</summary>
    /// <param name="paleta">El texto de <c>PinturaDeInicio.cs</c>.</param>
    /// <param name="variable">El nombre de la variable, por ejemplo <c>ClaroRojoMarca</c>.</param>
    private static string ColorDeclarado(string paleta, string variable)
    {
        var patron = new Regex(variable + @"\s*=\s*Pintar\(0x([0-9A-Fa-f]{2}),\s*0x([0-9A-Fa-f]{2}),\s*0x([0-9A-Fa-f]{2})\)");
        var m = patron.Match(paleta);
        Assert.IsTrue(m.Success, $"PinturaDeInicio.cs no declara {variable} con Pintar(0x.., 0x.., 0x..).");
        return "#" + m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value;
    }

    /// <summary>La razón de contraste de la WCAG entre dos colores «#RRGGBB».</summary>
    /// <param name="uno">Un color.</param>
    /// <param name="otro">El otro.</param>
    private static double Contraste(string uno, string otro)
    {
        var a = Luminancia(uno);
        var b = Luminancia(otro);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>La luminancia relativa de un color «#RRGGBB», según la WCAG.</summary>
    /// <param name="color">El color.</param>
    private static double Luminancia(string color)
    {
        double Canal(int desde)
        {
            var c = int.Parse(color.Substring(desde, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(1) + 0.7152 * Canal(3) + 0.0722 * Canal(5);
    }

    /// <summary>La ruta del XAML de Revisar, subiendo desde la carpeta de la prueba hasta la solución.</summary>
    private static string LaPantallaDeRevisar() => ArchivoDelProyecto(Path.Combine("Fichas.App", "Revisar", "PaginaDeRevisar.xaml"));

    /// <summary>La ruta de la paleta de Inicio.</summary>
    private static string LaPinturaDeInicio() => ArchivoDelProyecto(Path.Combine("Fichas.App", "Inicio", "PinturaDeInicio.cs"));

    /// <summary>Un archivo de la solución, buscando la carpeta que tiene <c>Fichas.sln</c> hacia arriba.</summary>
    /// <param name="relativa">La ruta desde la carpeta de la solución.</param>
    private static string ArchivoDelProyecto(string relativa)
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Fichas.sln"))) carpeta = carpeta.Parent;
        Assert.IsNotNull(carpeta, "No se encontró Fichas.sln subiendo desde la carpeta de la prueba.");
        var ruta = Path.Combine(carpeta.FullName, relativa);
        Assert.IsTrue(File.Exists(ruta), $"No está {ruta}.");
        return ruta;
    }
}
