using System.Xml.Linq;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Un control con el que se trabaja tiene que llamarse de alguna manera para quien no ve la
/// pantalla. Y un glifo NO es un nombre.
/// </summary>
/// <remarks>
/// <para>Es el paso siguiente de <see cref="PruebasDeLosNombresDeLosBotones"/>, que ya exige
/// nombre a todo boton pero da por bueno cualquier <c>Content</c> que no este vacio. Medido
/// por QA el 2026-09-04: el boton de cerrar un aviso de la franja lleva
/// <c>Content="&amp;#xE711;"</c> —un glifo del area de uso privado— y un <c>ToolTip</c>, y un
/// lector de pantalla anuncia «botón» y nada mas. El <c>ToolTip</c> no es el nombre.</para>
///
/// <para>Y los controles que no son botones no los miraba nadie. QA nombro seis:
/// <c>_rejilla</c> (Revisar), <c>_destinos</c> y <c>_lista</c> (Asignar), <c>_aQuien</c> y
/// <c>_deQuienViene</c> (Paquetes) y <c>_barra</c> (Importar). Los dos desplegables de
/// Paquetes son «a quien va» y «de quien viene»: con lector de pantalla son indistinguibles,
/// y confundirlos manda el paquete a la persona equivocada.</para>
///
/// <para>⚠️ <c>Correccion</c> queda fuera del barrido, como en
/// <see cref="PruebasDelEspanolDeLaPantalla"/> y por lo mismo: otro programador la esta
/// tocando en este ciclo. Va dicho en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelNombreParaElLectorDePantalla
{
    /// <summary>Las carpetas de la app cuyos controles se barren.</summary>
    private static readonly string[] ElTerrenoQueSeBarre =
        ["Cascara", "Revisar", "Asignar", "Paquetes", "Reportes", "Importar", "Inicio", "Flujo"];

    /// <summary>Los controles con los que se trabaja y que por tanto necesitan nombre.</summary>
    /// <remarks>
    /// <c>ProgressBar</c> esta porque dice cuanto falta de una importacion que dura minutos, y
    /// sin nombre es un porcentaje suelto. Los <c>TextBlock</c> NO estan: son texto, y el
    /// lector ya los lee.
    /// </remarks>
    private static readonly string[] ControlesQueNecesitanNombre =
        ["ComboBox", "ItemsView", "ListView", "GridView", "ProgressBar"];

    /// <summary>Lo minimo que hay que haber leido para que un cero signifique algo.</summary>
    /// <remarks>
    /// Medido el 2026-09-04: el terreno tiene 6 controles de esos tipos, y los 6 estaban sin
    /// nombre. Se exige 4 para que quitar uno no ponga esto rojo por nada, y se exige ALGO
    /// para que un barrido que no encontro archivos no se pueda leer como «ni un control mudo».
    /// </remarks>
    private const int ControlesQueTieneQueHaberLeido = 4;

    /// <summary>Cero controles mudos, con el denominador delante.</summary>
    [TestMethod]
    public void NingunControlConElQueSeTrabajaSeQuedaSinNombre()
    {
        var todos = new List<string>();
        var mudos = new List<string>();

        foreach (var archivo in LosXamlDelTerreno())
        {
            foreach (var control in XDocument.Load(archivo).Descendants().Where(EsUnControlQueSeUsa))
            {
                var comoSeLlama = $"{Path.GetFileName(archivo)} → {ComoSeLeConoce(control)}";
                todos.Add(comoSeLlama);
                if (!TieneNombre(control)) mudos.Add(comoSeLlama);
            }
        }

        Console.WriteLine($"Controles sin nombre: {mudos.Count} de {todos.Count} en el terreno barrido.");

        Assert.IsGreaterThanOrEqualTo(
            ControlesQueTieneQueHaberLeido,
            todos.Count,
            $"Se leyeron {todos.Count} controles y se esperaban al menos {ControlesQueTieneQueHaberLeido}. "
            + "Un barrido que no encontró controles no comprueba nada.");

        Assert.IsEmpty(mudos, "Controles sin nombre: " + string.Join(" · ", mudos));
    }

    /// <summary>Un boton cuyo contenido es un glifo necesita nombre escrito, no un ToolTip.</summary>
    /// <remarks>
    /// El <c>ToolTip</c> no vale: aparece al posar el raton, que es justo lo que no puede hacer
    /// quien navega con teclado y lector. Ademas el de la franja solo existe cuando hay un
    /// aviso puesto —el estado que nadie mira— y por eso llevaba asi sin que se notara.
    /// </remarks>
    [TestMethod]
    public void NingunBotonSeQuedaConUnGlifoPorTodoNombre()
    {
        var conGlifo = new List<string>();
        var mudos = new List<string>();

        foreach (var archivo in LosXamlDelTerreno())
        {
            foreach (var boton in XDocument.Load(archivo).Descendants().Where(EsUnBoton))
            {
                var contenido = boton.Attribute("Content")?.Value;
                if (contenido is null || !EsUnGlifoYNoUnaPalabra(contenido)) continue;

                var comoSeLlama = $"{Path.GetFileName(archivo)} → «{contenido}»";
                conGlifo.Add(comoSeLlama);
                if (string.IsNullOrWhiteSpace(boton.Attribute("AutomationProperties.Name")?.Value))
                    mudos.Add(comoSeLlama);
            }
        }

        Console.WriteLine($"Botones de glifo sin nombre: {mudos.Count} de {conGlifo.Count} botones de glifo.");

        Assert.IsNotEmpty(
            conGlifo,
            "No se encontró ni un botón de glifo. O el barrido no leyó los .xaml, o dejaron de existir; "
            + "en los dos casos esto no comprueba lo que dice comprobar.");

        Assert.IsEmpty(mudos, "Botones de glifo sin AutomationProperties.Name: " + string.Join(" · ", mudos));
    }

    /// <summary>Un contenido sin ni una letra es un simbolo, y un simbolo no se lee en voz alta.</summary>
    private static bool EsUnGlifoYNoUnaPalabra(string contenido)
        => contenido.Length > 0 && !contenido.Any(char.IsLetter);

    /// <summary>Si el nodo es uno de los controles con los que se interactúa y que por eso necesitan nombre.</summary>
    /// <param name="elemento">El nodo del XAML.</param>
    private static bool EsUnControlQueSeUsa(XElement elemento)
        => ControlesQueNecesitanNombre.Contains(elemento.Name.LocalName, StringComparer.Ordinal);

    /// <summary>Si el nodo es un botón de cualquier clase (<c>Button</c>, <c>ToggleButton</c>…), sin contar las propiedades adjuntas con punto.</summary>
    /// <param name="elemento">El nodo del XAML.</param>
    private static bool EsUnBoton(XElement elemento)
        => !elemento.Name.LocalName.Contains('.', StringComparison.Ordinal)
           && elemento.Name.LocalName.EndsWith("Button", StringComparison.Ordinal);

    /// <summary>Un control tiene nombre si lo dice <c>AutomationProperties.Name</c> o su <c>Header</c>.</summary>
    private static bool TieneNombre(XElement control)
        => !string.IsNullOrWhiteSpace(control.Attribute("AutomationProperties.Name")?.Value)
           || !string.IsNullOrWhiteSpace(control.Attribute("Header")?.Value);

    /// <summary>Cómo nombrar el control en el mensaje de fallo: su <c>x:Name</c>, o su clase si no tiene.</summary>
    /// <param name="control">El nodo del XAML.</param>
    private static string ComoSeLeConoce(XElement control)
    {
        var equis = control.Attribute(
            XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
        return string.IsNullOrWhiteSpace(equis) ? $"({control.Name.LocalName} sin x:Name)" : equis;
    }

    /// <summary>Los <c>.xaml</c> del terreno barrido, o no concluyente si no se encuentran.</summary>
    private static List<string> LosXamlDelTerreno()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var app = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App");
            if (Directory.Exists(app))
            {
                return [.. ElTerrenoQueSeBarre
                    .Select(c => Path.Combine(app, c))
                    .Where(Directory.Exists)
                    .SelectMany(c => Directory.EnumerateFiles(c, "*.xaml", SearchOption.AllDirectories))];
            }
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}». "
            + "Sin las pantallas delante esto no comprueba nada.");
        return [];
    }
}
