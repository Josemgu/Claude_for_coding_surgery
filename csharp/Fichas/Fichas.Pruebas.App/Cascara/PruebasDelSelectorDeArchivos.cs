using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Hay UN solo selector de archivos en la aplicacion, vive en la cascara, y es el de
/// <c>comdlg32</c>.
/// </summary>
/// <remarks>
/// <para>Sale de un fallo medido por el supervisor el 2026-09-04 sobre el paquete publicado,
/// no sobre <c>bin\Release</c>: dos programadores en paralelo arreglaron el mismo selector de
/// dos formas distintas, y una de las dos no abre nada. La medicion, arrancando
/// <c>Fichas.exe</c> con <c>--carpeta-de-datos</c> y pulsando los dos botones de Importar por
/// automatizacion de interfaz:</para>
/// <code>
/// pulso el boton de elegir archivos  -> ventanas del proceso tras pulsar: 1
/// pulso el boton de la carpeta       -> ventanas del proceso tras pulsar: 1
/// proceso vivo: True
/// </code>
/// <para>Uno, o sea solo la ventana del programa: ningun cuadro se abre. Lo que fallaba eran
/// los selectores de <c>Microsoft.Windows.Storage.Pickers</c>, que su programador midio
/// funcionando en su arbol de compilacion y NO funcionan publicados.</para>
///
/// <para>Estas pruebas son la valla para que nadie los vuelva a meter dentro de un mes. El
/// razonamiento entero —con las dos familias probadas y por que ninguna sirve aqui— esta
/// escrito en <see cref="SelectorDeArchivos"/>.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelSelectorDeArchivos
{
    /// <summary>Las palabras de las dos familias de selectores que NO abren en el paquete.</summary>
    private static readonly string[] LosQueNoAbren =
    [
        "Storage.Pickers",
        "FileOpenPicker",
        "FileSavePicker",
        "FolderPicker",
        "InitializeWithWindow",
    ];

    // ---- que no vuelvan los que no abren ------------------------------------

    /// <summary>Ninguna pantalla nombra un selector de WinRT ni del Windows App SDK.</summary>
    /// <remarks>
    /// ⚠️ Se busca en el CODIGO, con los comentarios quitados: el archivo que sobrevive los
    /// nombra a proposito para explicar por que no se usan, y ese texto es justo lo que impide
    /// que alguien lo deshaga. Una prueba que no distingue el codigo de lo que se escribe sobre
    /// el codigo obliga a dejar de escribirlo.
    /// </remarks>
    [TestMethod]
    public void NingunaPantallaVuelveALosSelectoresQueNoAbren()
    {
        var fuentes = LasFuentesDeLaApp();

        var culpables = new List<string>();
        foreach (var archivo in fuentes)
        {
            var texto = SinComentarios(File.ReadAllText(archivo));
            foreach (var palabra in LosQueNoAbren)
            {
                if (texto.Contains(palabra, StringComparison.Ordinal))
                    culpables.Add($"{Path.GetFileName(archivo)} → {palabra}");
            }
        }

        Console.WriteLine($"Selectores que no abren: {culpables.Count} en {fuentes.Count} fuentes .cs de la app.");
        Assert.IsGreaterThanOrEqualTo(30, fuentes.Count, $"Se esperaban al menos 30 fuentes y se leyeron {fuentes.Count}.");
        Assert.IsEmpty(culpables, "Vuelven los selectores que no abren: " + string.Join(" · ", culpables));
    }

    /// <summary>Un solo archivo de selector en toda la app, y esta en la cascara.</summary>
    /// <remarks>
    /// La leccion del supervisor: cuando el fallo es de la plataforma y no de una pantalla, el
    /// arreglo se encarga UNA vez y se pone en la cascara. Dos archivos de selector es como
    /// empezo esto.
    /// </remarks>
    [TestMethod]
    public void ElSelectorViveEnUnSoloSitioYEsLaCascara()
    {
        var selectores = LasFuentesDeLaApp()
            .Where(a => Path.GetFileName(a).StartsWith("Selector", StringComparison.Ordinal))
            .ToList();

        Console.WriteLine("Archivos de selector: " + string.Join(" · ", selectores.Select(RutaCorta)));

        Assert.HasCount(1, selectores, "Selectores encontrados: " + string.Join(" · ", selectores.Select(RutaCorta)));
        Assert.Contains(
            $"{Path.DirectorySeparatorChar}Cascara{Path.DirectorySeparatorChar}",
            selectores[0],
            $"El selector tiene que vivir en la cascara y esta en «{RutaCorta(selectores[0])}».");
    }

    // ---- lo que devuelve el cuadro de varios archivos -----------------------

    /// <summary>Un solo archivo elegido: <c>comdlg32</c> devuelve la ruta entera y ya esta.</summary>
    /// <remarks>
    /// Es el caso que mas se equivoca al escribirlo: con un archivo NO hay carpeta separada, y
    /// tratar el unico trozo como si fuera la carpeta deja una lista vacia.
    /// </remarks>
    [TestMethod]
    public void ConUnSoloArchivoDevuelveEseArchivo()
    {
        var rutas = SelectorDeArchivos.RutasDeLaSeleccion([@"C:\Escaneos\CASP2609-1.pdf"]);

        Assert.HasCount(1, rutas);
        Assert.AreEqual(@"C:\Escaneos\CASP2609-1.pdf", rutas[0]);
    }

    /// <summary>Varios archivos: el primer trozo es la carpeta y los demas son los nombres.</summary>
    [TestMethod]
    public void ConVariosArchivosPegaLaCarpetaDelanteDeCadaNombre()
    {
        var rutas = SelectorDeArchivos.RutasDeLaSeleccion(
            [@"C:\Escaneos", "CASP2609-1.pdf", "CASP2609-2.pdf", "CASP2609-3.pdf"]);

        Assert.HasCount(3, rutas);
        Assert.AreEqual(@"C:\Escaneos\CASP2609-1.pdf", rutas[0]);
        Assert.AreEqual(@"C:\Escaneos\CASP2609-3.pdf", rutas[2]);
    }

    /// <summary>La raiz de una unidad ya trae su barra y no se duplica.</summary>
    [TestMethod]
    public void LaRaizDeLaUnidadNoDuplicaLaBarra()
    {
        var rutas = SelectorDeArchivos.RutasDeLaSeleccion([@"D:\", "uno.pdf", "dos.pdf"]);

        Assert.AreEqual(@"D:\uno.pdf", rutas[0]);
        Assert.AreEqual(@"D:\dos.pdf", rutas[1]);
    }

    /// <summary>Sin trozos —se cerro el cuadro sin elegir— la lista sale vacia.</summary>
    [TestMethod]
    public void SinTrozosLaListaSaleVacia()
        => Assert.IsEmpty(SelectorDeArchivos.RutasDeLaSeleccion([]));

    // ---- lo comun -----------------------------------------------------------

    /// <summary>La ruta desde «Fichas.App» hacia dentro, para que el mensaje se lea.</summary>
    private static string RutaCorta(string archivo)
    {
        var corte = archivo.IndexOf("Fichas.App", StringComparison.Ordinal);
        return corte < 0 ? archivo : archivo[corte..];
    }

    /// <summary>El texto sin comentarios: fuera los <c>//</c> y los <c>/* */</c>.</summary>
    private static string SinComentarios(string texto)
    {
        var sinBloques = System.Text.RegularExpressions.Regex.Replace(
            texto, @"/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        var lineas = sinBloques
            .Split('\n')
            .Where(linea => !linea.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join('\n', lineas);
    }

    /// <summary>Todas las fuentes .cs de la app, o no concluyente si no se encuentran.</summary>
    private static List<string> LasFuentesDeLaApp()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var app = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App");
            if (Directory.Exists(app))
            {
                return [.. Directory
                    .EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
                    .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))];
            }
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontro «csharp/Fichas/Fichas.App» desde «{AppContext.BaseDirectory}». "
            + "Sin las fuentes delante esto no comprueba nada.");
        return [];
    }
}
