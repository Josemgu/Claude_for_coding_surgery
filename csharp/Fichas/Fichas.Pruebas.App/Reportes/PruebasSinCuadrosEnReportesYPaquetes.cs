namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Requisitos 4 y 9 del dueno sobre las fuentes de Reportes y de Paquetes: cero cuadros
/// que detengan al usuario, y ningun manejador que se pueda quedar mudo.
/// </summary>
/// <remarks>
/// <para>Lo que sustituye al cuadro es la franja de avisos de la cascara —una linea, «ver» y
/// X— y el acuse del pie. La cifra se da CON su denominador, «0 de N archivos», para que
/// signifique algo.</para>
///
/// <para>La segunda prueba es la que importa hoy: QA encontro el 2026-09-04 que los dos
/// botones de Importar estaban muertos porque su manejador era <c>async void</c> y la
/// excepcion no la recogia nadie. Aqui se comprueba que en estas dos pantallas no hay ni un
/// <c>async void</c>.</para>
///
/// <para>Si no encuentra las carpetas NO pasa en verde: se declara no concluyente. Una
/// comprobacion que no encontro donde mirar no es una comprobacion.</para>
/// </remarks>
[TestClass]
public sealed class PruebasSinCuadrosEnReportesYPaquetes
{
    /// <summary>Las cuatro palabras que abren un cuadro que detiene al usuario.</summary>
    private static readonly string[] LoProhibido =
        ["ContentDialog", "MessageDialog", "ShowAsync", "MessageBox"];

    /// <summary>
    /// Cero cuadros modales en las fuentes de las dos pantallas, con su denominador.
    /// </summary>
    /// <remarks>
    /// ⚠️ Se busca en el CODIGO, con los comentarios quitados. Buscarlo todo fue lo primero que
    /// se probo y salio rojo señalando <c>SelectorDeArchivos.cs</c>: el archivo explica por
    /// escrito por que NO se usa un <c>ContentDialog</c> ni un <c>MessageBox</c>. Una prueba
    /// que no distingue el codigo de lo que se escribe sobre el codigo obliga a dejar de
    /// escribirlo, y ese texto es justo lo que impide que alguien lo deshaga dentro de un mes.
    /// </remarks>
    [TestMethod]
    public void NiUnCuadroModalEnLasDosPantallas()
    {
        var archivos = FuentesDeLasDosPantallas();

        var culpables = new List<string>();
        foreach (var archivo in archivos)
        {
            var texto = SinComentarios(File.ReadAllText(archivo));
            foreach (var palabra in LoProhibido)
            {
                if (texto.Contains(palabra, StringComparison.Ordinal))
                    culpables.Add($"{Path.GetFileName(archivo)} → {palabra}");
            }
        }

        Console.WriteLine($"Cuadros modales: {culpables.Count} en {archivos.Count} archivos de Reportes y Paquetes.");
        Assert.IsGreaterThanOrEqualTo(10, archivos.Count, $"Se esperaban al menos 10 archivos y se leyeron {archivos.Count}.");
        Assert.IsEmpty(culpables, "Cuadros encontrados: " + string.Join(" · ", culpables));
    }

    /// <summary>
    /// Ni un <c>async void</c>: es la forma que dejo muertos los botones de Importar.
    /// </summary>
    /// <remarks>
    /// <para>En un <c>async void</c> la excepcion no la recoge quien llamo: se va al aire y el
    /// boton parece que no hace nada. En estas dos pantallas todo manejador es <c>void</c> y
    /// llama a <c>ManejadorSeguro.Correr</c>, que espera la tarea y deja escrito lo que pase.</para>
    ///
    /// <para>⚠️ Se busca la DECLARACION —el modificador delante—, no las dos palabras sueltas.
    /// Buscarlas sueltas fue lo primero que se probo y salio rojo con tres archivos: los tres
    /// eran comentarios que EXPLICAN por que no se usa. Una prueba que no distingue el codigo
    /// de lo que se escribe sobre el codigo obliga a dejar de escribirlo.</para>
    /// </remarks>
    [TestMethod]
    public void NiUnAsyncVoidEnLasDosPantallas()
    {
        var declaracion = new System.Text.RegularExpressions.Regex(
            @"\b(private|public|internal|protected)\s+(static\s+)?async\s+void\s+\w+\s*\(",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(5));

        var archivos = FuentesDeLasDosPantallas()
            .Where(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var culpables = archivos
            .Where(archivo => declaracion.IsMatch(File.ReadAllText(archivo)))
            .Select(Path.GetFileName)
            .ToList();

        Console.WriteLine($"«async void»: {culpables.Count} en {archivos.Count} archivos .cs de Reportes y Paquetes.");
        Assert.IsEmpty(culpables, "Con «async void»: " + string.Join(" · ", culpables));
    }

    /// <summary>
    /// Ninguna de las dos pantallas nombra una implementacion: solo conocen los contratos.
    /// </summary>
    /// <remarks>
    /// Es la regla de <c>Fichas.App.csproj</c>: las implementaciones las nombra UN solo
    /// archivo, <c>Cascara/Servicios.cs</c>. Una pantalla que nombre <c>Fichas.Datos</c>,
    /// <c>Fichas.Reportes</c> o <c>Fichas.Paquetes</c> deja de poder probarse sin base.
    /// </remarks>
    [TestMethod]
    public void NingunaPantallaNombraUnaImplementacion()
    {
        string[] prohibidas = ["using Fichas.Datos", "using Fichas.Lectura", "using Fichas.Reportes;", "using Fichas.Paquetes;"];
        var archivos = FuentesDeLasDosPantallas()
            .Where(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var culpables = new List<string>();
        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(archivo);
            foreach (var prohibida in prohibidas)
            {
                if (texto.Contains(prohibida, StringComparison.Ordinal))
                    culpables.Add($"{Path.GetFileName(archivo)} → {prohibida}");
            }
        }

        Assert.IsEmpty(culpables, "Implementaciones nombradas: " + string.Join(" · ", culpables));
    }

    /// <summary>
    /// El texto sin comentarios: fuera los <c>//</c> de C# y los <c>&lt;!-- --&gt;</c> del XAML.
    /// </summary>
    /// <remarks>
    /// No pretende ser un analizador de C#: no hace falta. Lo unico que tiene que conseguir es
    /// que una palabra que solo aparece EXPLICADA no cuente como una palabra USADA.
    /// </remarks>
    private static string SinComentarios(string texto)
    {
        var sinBloques = System.Text.RegularExpressions.Regex.Replace(
            texto, @"<!--.*?-->|/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        var lineas = sinBloques
            .Split('\n')
            .Where(linea => !linea.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join('\n', lineas);
    }

    /// <summary>Todas las fuentes de las dos carpetas, o no concluyente si no se encuentran.</summary>
    private static List<string> FuentesDeLasDosPantallas()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var reportes = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Reportes");
            var paquetes = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Paquetes");
            if (Directory.Exists(reportes) && Directory.Exists(paquetes))
            {
                return [.. new[] { reportes, paquetes }
                    .SelectMany(carpeta => Directory.EnumerateFiles(carpeta, "*.*", SearchOption.AllDirectories))
                    .Where(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                                || a.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))];
            }
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            "No se encontraron «csharp/Fichas/Fichas.App/Reportes» y «…/Paquetes» desde "
            + $"«{AppContext.BaseDirectory}». Sin las fuentes delante esto no comprueba nada.");
        return [];
    }
}
