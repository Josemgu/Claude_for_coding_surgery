using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Lo que se lee en la pantalla se escribe en espanol de verdad: con sus tildes, con su ene
/// y sin la muleta del «(s)».
/// </summary>
/// <remarks>
/// <para>Es la regla permanente 4 de <c>CLAUDE.md</c> —«Espanol en todo […] textos de
/// interfaz y mensajes de error»— puesta donde se pueda medir. Nace de un defecto MEDIDO
/// por QA sobre el paquete publicado el 2026-09-04, leyendo la pantalla: «Correccion»,
/// «Cedula», «Lo demas», «leidos», «Si, completa», «No esta completa», «companeros», «sin
/// numero», «irian». Y en la misma tirada: «Quedan 1 campos por comprobar», «1 companeros
/// activos», «1 persona(s)», «3 documento(s) archivado(s)».</para>
///
/// <para><b>No es politica del proyecto escribir sin tildes:</b> los PDF que genera el mismo
/// programa las llevan bien —«Período», «Histórico», «N.º de caso»—, asi que la pantalla y el
/// papel se contradecian. Lo que va SIN tildes son los nombres de clases, metodos y columnas,
/// que es como esta el proyecto entero y esta prueba no toca: aqui solo se mira lo que un
/// usuario lee.</para>
///
/// <para>⚠️ <b>Que NO cubre, dicho para que nadie lo lea como «toda la app esta barrida»:</b>
/// la carpeta <c>Correccion</c> queda fuera. La esta tocando otro programador en el mismo
/// ciclo y ponerla aqui teniria de rojo su trabajo por algo que no es suyo. Sus defectos
/// medidos por QA —«Cedula», «Lo demas», «Esta bien», «Quedan 1 campos por comprobar» y el
/// «N.o de caso» de <c>ModeloDeCorreccion.Guardado.cs:48</c>, que usa una «o» normal donde el
/// PDF usa «º»— siguen abiertos y van dichos en la entrega. Quitar esa exclusion cuando su
/// pase cierre es una linea.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelEspanolDeLaPantalla
{
    /// <summary>Las carpetas de la app cuyos textos se barren.</summary>
    /// <remarks><c>Correccion</c> no esta, y el motivo va en el resumen de la clase.</remarks>
    private static readonly string[] ElTerrenoQueSeBarre =
        ["Cascara", "Revisar", "Asignar", "Paquetes", "Reportes", "Importar", "Inicio"];

    /// <summary>Lo minimo que hay que haber leido para que un cero signifique algo.</summary>
    /// <remarks>
    /// Medido el 2026-09-04: el barrido encuentra 396 textos en ese terreno. Se exige 250 para
    /// que anadir o quitar pantallas no lo ponga rojo por nada, y se exige ALGO para que un
    /// barrido que no encontro archivos no se pueda leer como «ni un texto mal escrito».
    /// </remarks>
    private const int TextosQueTieneQueHaberLeido = 250;

    /// <summary>
    /// Las palabras que en espanol SIEMPRE llevan tilde o ene, con su forma correcta.
    /// </summary>
    /// <remarks>
    /// Van solo las inequivocas. «mas», «aun», «solo», «este» y «esta» NO estan: las cinco son
    /// palabras distintas con tilde y sin ella, y una lista que las metiera obligaria a poner
    /// tilde donde no va. Lo ambiguo se comprueba abajo, como frase entera y no como palabra.
    /// </remarks>
    private static readonly (string Mal, string Bien)[] PalabrasQueLlevanTilde =
    [
        ("numero", "número"), ("numeros", "números"),
        ("companero", "compañero"), ("companeros", "compañeros"),
        ("dia", "día"), ("dias", "días"),
        ("periodo", "período"), ("historico", "histórico"),
        ("proximo", "próximo"), ("proximos", "próximos"),
        ("proxima", "próxima"), ("proximas", "próximas"),
        ("aqui", "aquí"), ("asi", "así"),
        ("ultimo", "último"), ("ultima", "última"),
        ("ultimos", "últimos"), ("ultimas", "últimas"),
        ("correccion", "corrección"), ("cedula", "cédula"),
        ("demas", "demás"), ("estan", "están"),
        ("leido", "leído"), ("leida", "leída"),
        ("leidos", "leídos"), ("leidas", "leídas"),
        ("anotacion", "anotación"), ("anotaciones", "anotaciones"),
        ("irian", "irían"), ("iria", "iría"),
        ("cuantos", "cuántos"), ("cuantas", "cuántas"),
        ("recomendacion", "recomendación"), ("recomendaciones", "recomendaciones"),
        ("verificacion", "verificación"), ("revision", "revisión"),
        ("asignacion", "asignación"), ("asignaciones", "asignaciones"),
        ("importacion", "importación"), ("informacion", "información"),
        ("seleccion", "selección"), ("generacion", "generación"),
        ("opcion", "opción"), ("opciones", "opciones"),
        ("version", "versión"), ("sesion", "sesión"), ("razon", "razón"),
        ("segun", "según"), ("tambien", "también"), ("despues", "después"),
        ("ademas", "además"), ("todavia", "todavía"),
        ("ningun", "ningún"), ("algun", "algún"),
        ("facil", "fácil"), ("util", "útil"),
        ("unico", "único"), ("unica", "única"),
        ("rapido", "rápido"), ("maximo", "máximo"), ("minimo", "mínimo"),
        ("codigo", "código"), ("pagina", "página"), ("paginas", "páginas"),
        ("telefono", "teléfono"), ("automatico", "automático"),
        ("electronico", "electrónico"), ("estadistica", "estadística"),
    ];

    /// <summary>Lo ambiguo, comprobado como frase entera y no como palabra suelta.</summary>
    private static readonly (string Mal, string Bien)[] FrasesQueLlevanTilde =
    [
        ("no esta completa", "no está completa"),
        ("No esta completa", "No está completa"),
        ("Si, completa", "Sí, completa"),
        ("esta bien", "está bien"),
        ("Esta bien", "Está bien"),
        ("ya esta", "ya está"),
        ("no esta", "no está"),
        ("No esta", "No está"),
    ];

    /// <summary>Ni una tilde perdida, con el denominador delante.</summary>
    [TestMethod]
    public void NingunTextoDeLaPantallaPierdeUnaTilde()
    {
        var textos = LosTextosQueVeElUsuario();
        var malos = new List<string>();

        foreach (var (donde, texto) in textos)
        {
            var escrito = SoloLoEscrito(texto);

            foreach (var (mal, bien) in PalabrasQueLlevanTilde)
            {
                if (Regex.IsMatch(escrito, $@"(?<![\p{{L}}\d_]){Regex.Escape(mal)}(?![\p{{L}}\d_])", RegexOptions.IgnoreCase))
                    malos.Add($"{donde}: «{texto}» → «{mal}» debe ser «{bien}»");
            }

            foreach (var (mal, bien) in FrasesQueLlevanTilde)
            {
                // Con limite de palabra al final: «ya estaba» es correcto y no es «ya esta».
                if (Regex.IsMatch(escrito, $@"{Regex.Escape(mal)}(?![\p{{L}}\d_])"))
                    malos.Add($"{donde}: «{texto}» → «{mal}» debe ser «{bien}»");
            }
        }

        Console.WriteLine($"Textos sin su tilde: {malos.Count} de {textos.Count} leídos en la pantalla.");

        Assert.IsGreaterThanOrEqualTo(
            TextosQueTieneQueHaberLeido,
            textos.Count,
            $"Se leyeron {textos.Count} textos y se esperaban al menos {TextosQueTieneQueHaberLeido}. "
            + "Un barrido que no encontró textos no comprueba nada.");

        Assert.IsEmpty(malos, "Textos sin su tilde:" + Environment.NewLine + string.Join(Environment.NewLine, malos));
    }

    /// <summary>
    /// Ni un «(s)»: o se escribe «1 persona» y «3 personas», o no se cuenta.
    /// </summary>
    /// <remarks>
    /// El proyecto ya tiene <c>Fichas.Reportes.Reglas.Plural</c> y lo usa en los PDF; lo que
    /// faltaba era usarlo tambien en la pantalla. «1 persona(s)» no es un plural: es no haber
    /// decidido, escrito delante de quien lee.
    /// </remarks>
    [TestMethod]
    public void NingunTextoUsaLaMuletaDelParentesisEse()
    {
        var textos = LosTextosQueVeElUsuario();

        var malos = textos
            .Where(t => t.Texto.Contains("(s)", StringComparison.Ordinal))
            .Select(t => $"{t.Donde}: «{t.Texto}»")
            .ToList();

        Console.WriteLine($"Textos con «(s)»: {malos.Count} de {textos.Count} leídos en la pantalla.");

        Assert.IsGreaterThanOrEqualTo(TextosQueTieneQueHaberLeido, textos.Count);
        Assert.IsEmpty(malos, "Textos con «(s)»:" + Environment.NewLine + string.Join(Environment.NewLine, malos));
    }

    /// <summary>
    /// Ni un «de el»: en castellano es «del», y la franja de avisos lo escribia entero.
    /// </summary>
    /// <remarks>
    /// Medido por QA en la franja: «Reporte de el período…» y «Reporte de el histórico…». Sale
    /// de juntar «Reporte de » con un trozo que ya empezaba por «el», en
    /// <c>Fichas.Reportes/ReportesEnPdf.cs</c>.
    /// </remarks>
    [TestMethod]
    public void NingunTextoEscribeDeElEnVezDeDel()
    {
        var textos = LosTextosQueVeElUsuario();

        var malos = textos
            .Where(t => Regex.IsMatch(t.Texto, @"(?<![\p{L}])de el(?![\p{L}])"))
            .Select(t => $"{t.Donde}: «{t.Texto}»")
            .ToList();

        Assert.IsGreaterThanOrEqualTo(TextosQueTieneQueHaberLeido, textos.Count);
        Assert.IsEmpty(malos, "Textos con «de el»:" + Environment.NewLine + string.Join(Environment.NewLine, malos));
    }

    /// <summary>
    /// El texto quitandole lo que va dentro de <c>{…}</c>, que es codigo y no palabras.
    /// </summary>
    /// <remarks>
    /// Sin esto, «hoja {pagina}» se leeria como la palabra «pagina» sin tilde, y lo que hay
    /// ahi es el NOMBRE DE UNA VARIABLE, que va sin tildes como todo el codigo del proyecto.
    /// El barrido acusaria de mal escrito lo que esta bien, y arreglarlo romperia el programa.
    /// </remarks>
    private static string SoloLoEscrito(string texto) => Regex.Replace(texto, @"\{[^{}]*\}", " ");

    // ---- de donde salen los textos -------------------------------------------

    /// <summary>Todo lo que un usuario puede leer en el terreno barrido: XAML y literales.</summary>
    private static List<(string Donde, string Texto)> LosTextosQueVeElUsuario()
    {
        var raiz = LaCarpetaDeLaApp();
        var textos = new List<(string, string)>();

        foreach (var carpeta in ElTerrenoQueSeBarre.Select(c => Path.Combine(raiz, c)).Where(Directory.Exists))
        {
            foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.xaml", SearchOption.AllDirectories))
                textos.AddRange(DeUnXaml(archivo));

            foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
                textos.AddRange(DeUnFuente(archivo));
        }

        return textos;
    }

    /// <summary>Los atributos de un <c>.xaml</c> que acaban delante de los ojos de alguien.</summary>
    private static IEnumerable<(string, string)> DeUnXaml(string archivo)
    {
        var mirados = new[]
        {
            "Text", "Content", "PlaceholderText", "Header", "Description",
            "ToolTipService.ToolTip", "AutomationProperties.Name", "Title",
        };

        return XDocument.Load(archivo).Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => mirados.Contains(a.Name.LocalName, StringComparer.Ordinal))
            // Un valor entre llaves es un enlace o un recurso, no un texto escrito.
            .Where(a => !a.Value.StartsWith('{') && a.Value.Length > 0)
            .Select(a => ($"{Path.GetFileName(archivo)} → {a.Name.LocalName}", a.Value));
    }

    /// <summary>
    /// Los literales de un <c>.cs</c> que son frases, no identificadores.
    /// </summary>
    /// <remarks>
    /// Se exige un espacio dentro. Es lo que separa un rotulo —«sin numero de caso»— de una
    /// clave de datos como <c>"numero_caso"</c> o de un nombre de pagina como
    /// <c>"Correccion"</c>, que van sin tilde a proposito porque son identificadores. El
    /// rotulo de una sola palabra que si se lee en pantalla vive en el XAML, donde este mismo
    /// barrido lo mira por su atributo.
    /// </remarks>
    private static IEnumerable<(string, string)> DeUnFuente(string archivo)
    {
        var dentro = new List<(string, string)>();
        var lineas = File.ReadAllLines(archivo);

        for (var i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i].TrimStart();
            if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith('*')) continue;

            foreach (Match literal in Regex.Matches(lineas[i], "\"([^\"\\\\]*)\""))
            {
                var texto = literal.Groups[1].Value;
                if (texto.Contains(' ', StringComparison.Ordinal))
                    dentro.Add(($"{Path.GetFileName(archivo)}:{i + 1}", texto));
            }
        }

        return dentro;
    }

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
