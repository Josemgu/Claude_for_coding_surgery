using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El marco que dibuja <c>mockups/mockup-v2-inicio.html</c>: el menu en grupos, un atajo
/// por entrada, y el rotulo del atajo diciendo el atajo de verdad.
/// </summary>
/// <remarks>
/// <para><b>De donde sale el criterio.</b> El dueno abrio el programa el 2026-09-09 y dijo
/// que «no se parece en nada al mockup de inicio v2». Medido sobre <c>master</c> ese mismo
/// dia, antes de tocar nada:</para>
/// <code>
/// grep -c NavigationViewItemHeader Fichas.App/Cascara/VentanaPrincipal.xaml -> 0
/// grep -c AutoSuggestBox           Fichas.App/Cascara/VentanaPrincipal.xaml -> 0
/// grep -c KeyboardAccelerator      Fichas.App/Cascara/VentanaPrincipal.xaml -> 0
/// grep -c "&lt;NavigationViewItem " Fichas.App/Cascara/VentanaPrincipal.xaml -> 9
/// </code>
///
/// <para><b>Lo que se copia del mockup es la FORMA, no la lista.</b> El mockup dibuja cinco
/// entradas con otros nombres; el programa tiene nueve y cada nombre lo puso el dueno. Aqui
/// se exige que las nueve esten agrupadas y con atajo, no que se llamen como alli.</para>
///
/// <para>⚠️ <b>Lo que NO miran:</b> como se ve. Esto lee el XAML, no pixeles: que el atajo
/// dispare de verdad y que el rotulo se lea en los dos temas se mide con la ventana abierta
/// y va en la entrega con su captura.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelMarcoDeLaVentana
{
    /// <summary>Cuantas entradas tiene el menu hoy. Si se anade una, esta cifra sube con ella.</summary>
    /// <remarks>
    /// Va explicita para que un barrido que no encontro entradas no se pueda leer como «todas
    /// tienen atajo». Medido el 2026-09-09 sobre <c>master</c>: nueve.
    /// </remarks>
    private const int EntradasQueTieneElMenu = 9;

    /// <summary>Cuantos grupos dibuja el mockup: «El trabajo» y «La gente».</summary>
    private const int GruposQueDibujaElMockup = 2;

    /// <summary>Las nueve entradas quedan repartidas en grupos, y ninguna se queda fuera.</summary>
    [TestMethod]
    public void LasNueveEntradasVanDentroDeUnGrupo()
    {
        var menu = ElMenu();
        var hijos = menu.Elements().ToList();

        var grupos = hijos.Where(EsUnGrupo).ToList();
        var entradas = hijos.Where(EsUnaEntrada).ToList();

        // Una entrada esta «fuera» si aparece antes del primer grupo: el usuario la ve
        // colgando sin titulo, que es justo lo que el dueno dijo que no se parece al mockup.
        var primerGrupo = hijos.FindIndex(EsUnGrupo);
        var fuera = hijos
            .Select((h, i) => (Indice: i, Nodo: h))
            .Where(p => EsUnaEntrada(p.Nodo) && (primerGrupo < 0 || p.Indice < primerGrupo))
            .Select(p => Etiqueta(p.Nodo))
            .ToList();

        Console.WriteLine($"Grupos: {grupos.Count}. Entradas: {entradas.Count}. Fuera de grupo: {fuera.Count}.");
        foreach (var grupo in grupos) Console.WriteLine($"  grupo «{grupo.Attribute("Content")?.Value}»");
        foreach (var entrada in entradas) Console.WriteLine($"  entrada «{Etiqueta(entrada)}»");

        Assert.HasCount(
            EntradasQueTieneElMenu, entradas,
            "El menu no tiene las entradas que se esperaban: sin ellas, contar atajos no dice nada.");
        Assert.HasCount(
            GruposQueDibujaElMockup, grupos,
            "El mockup agrupa el menu en dos: «El trabajo» y «La gente».");
        Assert.IsEmpty(
            fuera,
            "Estas entradas quedan antes del primer grupo y se ven sueltas:" + Environment.NewLine
            + string.Join(Environment.NewLine, fuera));
    }

    /// <summary>Cada grupo lleva un nombre escrito, no un separador mudo.</summary>
    [TestMethod]
    public void CadaGrupoDiceComoSeLlama()
    {
        var sinNombre = ElMenu().Elements().Where(EsUnGrupo)
            .Where(g => string.IsNullOrWhiteSpace(g.Attribute("Content")?.Value))
            .Select((_, i) => $"el grupo numero {i + 1} no tiene «Content»")
            .ToList();

        Assert.IsEmpty(sinNombre, string.Join(Environment.NewLine, sinNombre));
    }

    /// <summary>Las nueve entradas tienen atajo, todos con Alt y sin repetir tecla.</summary>
    [TestMethod]
    public void CadaEntradaTieneSuAtajoYNingunoChoca()
    {
        var entradas = ElMenu().Elements().Where(EsUnaEntrada).ToList();

        var sinAtajo = new List<string>();
        var teclas = new List<string>();
        var sinAlt = new List<string>();

        foreach (var entrada in entradas)
        {
            var atajo = AtajoDe(entrada);
            if (atajo is null) { sinAtajo.Add(Etiqueta(entrada)); continue; }

            var modificador = atajo.Attribute("Modifiers")?.Value;
            // «Menu» es como se llama Alt en XAML: Windows.System.VirtualKeyModifiers.Menu.
            if (!string.Equals(modificador, "Menu", StringComparison.Ordinal))
                sinAlt.Add($"{Etiqueta(entrada)} lleva Modifiers=«{modificador}» y hace falta «Menu» (Alt)");

            teclas.Add(atajo.Attribute("Key")?.Value ?? "(sin Key)");
        }

        var repetidas = teclas.GroupBy(t => t, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"«{g.Key}» esta puesta {g.Count()} veces")
            .ToList();

        Console.WriteLine($"Entradas leidas: {entradas.Count}. Teclas: {string.Join(", ", teclas)}.");

        Assert.HasCount(EntradasQueTieneElMenu, entradas);
        Assert.IsEmpty(sinAtajo, "Entradas sin atajo: " + string.Join(", ", sinAtajo));
        Assert.IsEmpty(sinAlt, string.Join(Environment.NewLine, sinAlt));
        Assert.IsEmpty(
            repetidas,
            "Dos entradas con el mismo atajo: una de las dos no se alcanza nunca." + Environment.NewLine
            + string.Join(Environment.NewLine, repetidas));
    }

    /// <summary>
    /// El rotulo que se pinta al lado de la entrada dice el atajo de VERDAD.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la prueba que justifica pintar el rotulo. El mockup escribe «Alt+1» al lado de
    /// cada entrada; si el rotulo dijera Alt+3 y la tecla fuera otra, seria peor que no
    /// escribir nada: el usuario pulsaria lo que pone y no pasaria lo que espera.
    /// </remarks>
    [TestMethod]
    public void ElRotuloDelAtajoDiceLaTeclaQueDeVerdadEsta()
    {
        var mentiras = new List<string>();
        var comprobados = 0;

        foreach (var entrada in ElMenu().Elements().Where(EsUnaEntrada))
        {
            var atajo = AtajoDe(entrada);
            if (atajo is null) continue;

            var tecla = atajo.Attribute("Key")?.Value ?? string.Empty;
            var cifra = Regex.Match(tecla, @"\d+$").Value;

            var rotulos = entrada.Descendants()
                .Select(e => e.Attribute("Text")?.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t) && t.StartsWith("Alt+", StringComparison.Ordinal))
                .ToList();

            if (rotulos.Count == 0)
            {
                mentiras.Add($"{Etiqueta(entrada)}: tiene atajo «{tecla}» y no lo escribe en ningun sitio");
                continue;
            }

            comprobados++;
            foreach (var rotulo in rotulos)
            {
                if (!string.Equals(rotulo, $"Alt+{cifra}", StringComparison.Ordinal))
                    mentiras.Add($"{Etiqueta(entrada)}: escribe «{rotulo}» y la tecla es «{tecla}»");
            }
        }

        Console.WriteLine($"Rotulos de atajo comprobados: {comprobados}.");

        Assert.AreEqual(
            EntradasQueTieneElMenu, comprobados,
            "No se comprobaron los nueve rotulos: un cero aqui no significaria nada.");
        Assert.IsEmpty(mentiras, string.Join(Environment.NewLine, mentiras));
    }

    /// <summary>La cabecera del mockup: titulo, fecha y buscador, los tres puestos.</summary>
    [TestMethod]
    public void LaCabeceraTraeElTituloLaFechaYElBuscador()
    {
        var ventana = ElXamlDeLaVentana();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var cabecera = ventana.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "NavigationView.Header");

        Assert.IsNotNull(
            cabecera,
            "La ventana no declara «NavigationView.Header»: no hay cabecera donde poner el titulo.");

        var nombres = cabecera.Descendants()
            .Select(e => e.Attribute(x + "Name")?.Value)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        Console.WriteLine($"Piezas de la cabecera: {string.Join(", ", nombres)}.");

        CollectionAssert.Contains(nombres, "_tituloDeLaPantalla", "La cabecera no tiene donde poner el titulo.");
        CollectionAssert.Contains(nombres, "_fechaDeHoy", "La cabecera no tiene donde poner la fecha de hoy.");
        CollectionAssert.Contains(nombres, "_buscador", "La cabecera no tiene buscador.");

        Assert.IsTrue(
            cabecera.Descendants().Any(e => e.Name.LocalName == "AutoSuggestBox"),
            "El buscador de la cabecera tiene que ser un «AutoSuggestBox»: un cuadro de texto suelto "
            + "no puede ensenar lo que encontro, y el mockup dice que un control mudo esta roto.");
    }

    /// <summary>
    /// Ningun texto de la cabecera se deja el tamano de letra sin decir.
    /// </summary>
    /// <remarks>
    /// <para><b>Defecto MEDIDO el 2026-09-09 sobre el paquete publicado, no razonado:</b> la
    /// fecha salia MAS ALTA que el titulo de la pantalla. El motivo es que
    /// <c>NavigationView</c> pinta su cabecera con un tamano de letra grande y el
    /// <c>TextBlock</c> lo hereda; el titulo no lo sufre porque su <c>Style</c> fija el suyo,
    /// y la fecha, que no lo fijaba, lo heredaba.</para>
    ///
    /// <para>Se comprobo quitando el <c>Style</c>, volviendo a publicar y leyendo el arbol de
    /// accesibilidad de la ventana abierta, y despues al reves:</para>
    /// <code>
    /// con el Style ... titulo «Inicio» 51x28 px · fecha 974x20 px
    /// sin el Style ... titulo «Inicio» 51x28 px · fecha 974x38 px
    /// </code>
    ///
    /// <para>⚠️ Aqui decia «978x38». La cifra se corrige el mismo dia: quien la escribio no
    /// llego a abrir la ventana —lo dice su propio commit— y el ancho real es 974. El alto,
    /// 38, si era el que sale.</para>
    ///
    /// <para>Un dia normal en letra mas grande que el sitio donde se esta invierte lo que
    /// importa de la cabecera.</para>
    /// </remarks>
    [TestMethod]
    public void NingunTextoDeLaCabeceraHeredaSuTamanoDeLetra()
    {
        var cabecera = ElXamlDeLaVentana().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "NavigationView.Header");

        Assert.IsNotNull(cabecera, "La ventana no declara «NavigationView.Header».");

        var textos = cabecera.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();

        var heredados = textos
            .Where(t => string.IsNullOrWhiteSpace(t.Attribute("Style")?.Value)
                        && string.IsNullOrWhiteSpace(t.Attribute("FontSize")?.Value))
            .Select(t => t.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                         ?? "(TextBlock sin x:Name)")
            .ToList();

        Console.WriteLine($"Textos de la cabecera: {textos.Count}. Sin tamano propio: {heredados.Count}.");

        Assert.IsGreaterThanOrEqualTo(
            2, textos.Count, "Un barrido que no encontro textos en la cabecera no comprueba nada.");
        Assert.IsEmpty(
            heredados,
            "Estos textos de la cabecera heredan el tamano de letra del NavigationView, que es 28:"
            + Environment.NewLine + string.Join(Environment.NewLine, heredados));
    }

    // ---- de donde sale lo leido ---------------------------------------------

    private static bool EsUnGrupo(XElement e)
        => e.Name.LocalName == "NavigationViewItemHeader";

    private static bool EsUnaEntrada(XElement e)
        => e.Name.LocalName == "NavigationViewItem";

    private static string Etiqueta(XElement entrada)
        => entrada.Attribute("Tag")?.Value ?? "(sin Tag)";

    /// <summary>El <c>KeyboardAccelerator</c> de una entrada, o nulo si no tiene.</summary>
    private static XElement? AtajoDe(XElement entrada)
        => entrada.Descendants().FirstOrDefault(e => e.Name.LocalName == "KeyboardAccelerator");

    /// <summary>El nodo <c>NavigationView.MenuItems</c> de la ventana.</summary>
    private static XElement ElMenu()
    {
        var menu = ElXamlDeLaVentana().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "NavigationView.MenuItems");

        Assert.IsNotNull(menu, "VentanaPrincipal.xaml no declara «NavigationView.MenuItems».");
        return menu;
    }

    private static XDocument ElXamlDeLaVentana()
        => XDocument.Load(Path.Combine(LaCarpetaDeLaApp(), "Cascara", "VentanaPrincipal.xaml"));

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
            $"No se encontro «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}». "
            + "Sin la ventana delante esto no comprueba nada.");
        return string.Empty;
    }
}
