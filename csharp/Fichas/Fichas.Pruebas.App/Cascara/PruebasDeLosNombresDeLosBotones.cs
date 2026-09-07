using System.Xml.Linq;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Todo boton de la aplicacion tiene que llamarse de alguna manera para quien no ve la
/// pantalla.
/// </summary>
/// <remarks>
/// <para>Sale de un defecto MEDIDO por el supervisor sobre el paquete publicado el
/// 2026-09-04, navegando a Importar por automatizacion de interfaz:</para>
/// <code>
/// botones en la pantalla de Importar: 4
///    'Open Navigation'   ''   ''   'Detener'
/// </code>
/// <para>Los dos del medio son «Elegir archivos…» y «Elegir una carpeta…», y llegaban con el
/// nombre VACIO. Por eso la sonda del supervisor no podia encontrarlos por su texto, y un
/// lector de pantalla tampoco: su contenido no es texto suelto sino un icono con un
/// <c>TextBlock</c> dentro de un <c>StackPanel</c>, y de ahi no sale ningun nombre solo.</para>
///
/// <para>La regla que se comprueba: <b>un boton cuyo contenido son elementos —un icono, una
/// pila— y no un <c>Content</c> de texto, tiene que traer <c>AutomationProperties.Name</c></b>.
/// Es la misma solucion que ya llevaban Reportes, Paquetes y el visor de Correccion; lo que
/// faltaba era que nadie lo comprobase.</para>
///
/// <para>Se cuenta CON denominador: «0 de 43» significa algo, «0» no.</para>
///
/// <para>Si no encuentra las pantallas NO pasa en verde: se declara no concluyente. Una
/// comprobacion que no encontro donde mirar no es una comprobacion.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosNombresDeLosBotones
{
    /// <summary>Lo minimo que tiene que haber leido para que el resultado signifique algo.</summary>
    /// <remarks>
    /// Medido el 2026-09-04: los <c>.xaml</c> de la app declaran 43 botones. Se exige 40 y no
    /// 43 para que anadir o quitar uno no ponga esto en rojo por nada; se exige ALGO para que
    /// un barrido que no encontro archivos no se pueda leer como «cero botones sin nombre».
    /// </remarks>
    private const int BotonesQueTieneQueHaberLeido = 40;

    /// <summary>Cero botones sin nombre, con el denominador delante.</summary>
    [TestMethod]
    public void NingunBotonSeQuedaSinNombreParaUnLectorDePantalla()
    {
        var pantallas = LosXamlDeLaApp();

        var todos = new List<string>();
        var mudos = new List<string>();

        foreach (var archivo in pantallas)
        {
            var doc = XDocument.Load(archivo);
            foreach (var boton in doc.Descendants().Where(EsUnBoton))
            {
                var comoSeLlama = $"{Path.GetFileName(archivo)} → {ComoSeLeConoce(boton)}";
                todos.Add(comoSeLlama);
                if (!TieneNombre(boton)) mudos.Add(comoSeLlama);
            }
        }

        Console.WriteLine(
            $"Botones sin nombre: {mudos.Count} de {todos.Count} "
            + $"en {pantallas.Count} archivos .xaml de la app.");

        Assert.IsGreaterThanOrEqualTo(
            BotonesQueTieneQueHaberLeido,
            todos.Count,
            $"Se esperaban al menos {BotonesQueTieneQueHaberLeido} botones y se leyeron {todos.Count}. "
            + "Un barrido que no encontro botones no comprueba nada.");

        Assert.IsEmpty(mudos, "Botones sin nombre: " + string.Join(" · ", mudos));
    }

    /// <summary>Un elemento es un boton si su nombre acaba en «Button».</summary>
    /// <remarks>
    /// Se descartan los que llevan punto —<c>Button.Flyout</c>, <c>Button.Content</c>— porque
    /// no son botones: son la sintaxis de XAML para dar una propiedad como elemento.
    /// </remarks>
    private static bool EsUnBoton(XElement elemento)
        => !elemento.Name.LocalName.Contains('.', StringComparison.Ordinal)
           && elemento.Name.LocalName.EndsWith("Button", StringComparison.Ordinal);

    /// <summary>
    /// Tiene nombre si lo dice <c>AutomationProperties.Name</c> o si su <c>Content</c> es texto.
    /// </summary>
    /// <remarks>
    /// Un <c>Content</c> escrito como atributo es texto que el lector de pantalla lee tal cual
    /// —tambien si viene por enlace, porque el enlace acaba en texto—. Lo que NO deja nombre es
    /// el contenido por elementos: un <c>FontIcon</c>, o un <c>StackPanel</c> con un icono y un
    /// <c>TextBlock</c> dentro, que es exactamente lo que tenian los dos botones de Importar.
    /// </remarks>
    private static bool TieneNombre(XElement boton)
    {
        var puesto = boton.Attribute("AutomationProperties.Name")?.Value;
        if (!string.IsNullOrWhiteSpace(puesto)) return true;

        var contenido = boton.Attribute("Content")?.Value;
        return !string.IsNullOrWhiteSpace(contenido);
    }

    /// <summary>Como nombrar el boton en el mensaje del fallo, para poder ir a buscarlo.</summary>
    private static string ComoSeLeConoce(XElement boton)
    {
        var equis = boton.Attribute(
            XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
        if (!string.IsNullOrWhiteSpace(equis)) return equis;

        var contenido = boton.Attribute("Content")?.Value;
        return string.IsNullOrWhiteSpace(contenido) ? "(sin x:Name ni Content)" : $"«{contenido}»";
    }

    /// <summary>Todos los <c>.xaml</c> de la app, o no concluyente si no se encuentran.</summary>
    private static List<string> LosXamlDeLaApp()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var app = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App");
            if (Directory.Exists(app))
            {
                return [.. Directory
                    .EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories)
                    .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))];
            }
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontro «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}». "
            + "Sin las pantallas delante esto no comprueba nada.");
        return [];
    }
}
