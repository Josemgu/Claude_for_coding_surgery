using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El programa lleva un número de versión, escrito en UN solo sitio, y ese número es el que
/// enseña la cabecera y el que declara el ejecutable.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> El dueño lo pidió el 2026-09-07 y lo repitió el
/// 2026-09-10: «control de actualización». Va a recibir versiones nuevas y tiene que saber
/// cuál tiene abierta. Medido sobre <c>master</c> el 2026-09-10 antes de tocar nada:</para>
/// <code>
/// grep -rn "Version" Directory.Build.props Fichas.App/Fichas.App.csproj
///   -> TargetPlatformMinVersion, LangVersion y dos «Version=» de paquetes de terceros.
///   Ningún número de versión del programa.
/// </code>
///
/// <para><b>El único sitio</b> es <c>&lt;Version&gt;</c> en <c>Fichas.App.csproj</c>: la versión
/// es del programa —de <c>Fichas.exe</c>—, no de cada biblioteca. De ahí MSBuild deriva solo
/// <c>AssemblyVersion</c>, <c>FileVersion</c> e <c>InformationalVersion</c>; el ejecutable las
/// lleva en sus propiedades (clic derecho → Detalles), <see cref="VersionDelPrograma"/> las
/// lee para la cabecera y <c>publish.ps1</c> lee el <c>.csproj</c> para el resumen y para
/// negarse si ya hay etiqueta. Si alguien sube el número en un sitio y no en otro, estas
/// pruebas lo dicen.</para>
///
/// <para>⚠️ <b>Lo que NO miran:</b> que la cabecera lo pinte de verdad. Eso se lee por
/// accesibilidad con la ventana abierta y va en la entrega con su salida.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaVersionDelPrograma
{
    /// <summary>
    /// La forma que tiene que tener el número: enteros separados por puntos, sin letras.
    /// </summary>
    /// <remarks>
    /// El dueño cuenta las entregas como v2…v9, así que «9» vale, y «9.1» vale para una
    /// segunda salida del mismo número. Una letra —«9-beta»— no: la etiqueta de git y las
    /// propiedades del .exe dejarían de decir lo mismo.
    /// </remarks>
    private static readonly Regex LaFormaDelNumero = new(@"^\d+(\.\d+){0,3}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Las propiedades que, escritas fuera del sitio único, lo pisarían o lo duplicarían.
    /// </summary>
    private static readonly string[] LasQuePisarianLaVersion =
        ["Version", "VersionPrefix", "VersionSuffix", "AssemblyVersion", "FileVersion", "InformationalVersion"];

    /// <summary>La variable de entorno con la carpeta publicada, si quien corre esto la publicó.</summary>
    private const string LaVariableDeLaCarpetaPublicada = "FICHAS_CARPETA_PUBLICADA";

    // ---- un solo sitio -----------------------------------------------------------

    /// <summary>El número está escrito en <c>Fichas.App.csproj</c> y tiene forma de número.</summary>
    [TestMethod]
    public void ElNumeroEstaEscritoEnElSitioUnicoYTieneFormaDeNumero()
    {
        var numero = ElNumeroQueDiceElCodigo();
        Console.WriteLine($"Fichas.App.csproj dice: «{numero}»");

        Assert.IsTrue(
            LaFormaDelNumero.IsMatch(numero),
            $"«{numero}» no es un número de versión: se esperan enteros separados por puntos, como «9» o «9.1».");
    }

    /// <summary>
    /// Ni <c>Directory.Build.props</c> ni ningún otro proyecto vuelven a escribir la versión:
    /// si lo hicieran, habría dos sitios.
    /// </summary>
    [TestMethod]
    public void NingunOtroArchivoVuelveAEscribirLaVersion()
    {
        var proyectos = Directory.GetFiles(LaCarpetaDeFichas(), "*.csproj", SearchOption.AllDirectories)
            .Where(p => !string.Equals(Path.GetFileName(p), "Fichas.App.csproj", StringComparison.Ordinal))
            .Append(Path.Combine(LaCarpetaDeFichas(), "Directory.Build.props"))
            .ToArray();
        var repetidores = new List<string>();

        foreach (var proyecto in proyectos)
        {
            var pisadas = XDocument.Load(proyecto).Descendants()
                .Where(e => e.Parent?.Name.LocalName == "PropertyGroup")
                .Where(e => LasQuePisarianLaVersion.Contains(e.Name.LocalName, StringComparer.Ordinal))
                .Select(e => e.Name.LocalName)
                .ToList();

            if (pisadas.Count > 0)
                repetidores.Add($"{Path.GetRelativePath(LaCarpetaDeFichas(), proyecto)}: {string.Join(", ", pisadas)}");
        }

        Console.WriteLine($"Archivos barridos: {proyectos.Length}. Con versión propia: {repetidores.Count}.");

        Assert.IsGreaterThanOrEqualTo(2, proyectos.Length, "El barrido no encontró proyectos: un cero no dice nada.");
        Assert.IsEmpty(
            repetidores,
            "Estos archivos escriben la versión por su cuenta y duplican la de Fichas.App.csproj:"
            + Environment.NewLine + string.Join(Environment.NewLine, repetidores));
    }

    // ---- lo que el programa enseña ---------------------------------------------

    /// <summary>Lo que el programa dice de sí mismo es lo que dice el código.</summary>
    [TestMethod]
    public void LoQueDiceElProgramaEsLoQueDiceElCodigo()
    {
        Console.WriteLine($"Código: «{ElNumeroQueDiceElCodigo()}» · programa: «{VersionDelPrograma.Numero}»");

        Assert.AreEqual(ElNumeroQueDiceElCodigo(), VersionDelPrograma.Numero);
    }

    /// <summary>La cabecera lo escribe como lo cuenta el dueño: «v9», igual que la etiqueta de git.</summary>
    [TestMethod]
    public void LaCabeceraLoEscribeComoLoCuentaElDueno()
        => Assert.AreEqual("v" + ElNumeroQueDiceElCodigo(), VersionDelPrograma.ComoSeLee);

    /// <summary>La cabecera tiene dónde ponerla, y con nombre para quien no ve la pantalla.</summary>
    /// <remarks>
    /// Se lee el XAML, no la ventana. Que el texto que se pinta sea «v9» se comprueba con la
    /// ventana abierta y va en la entrega.
    /// </remarks>
    [TestMethod]
    public void LaCabeceraTieneDondePonerLaVersion()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var cabecera = XDocument.Load(Path.Combine(LaCarpetaDeFichas(), "Fichas.App", "Cascara", "VentanaPrincipal.xaml"))
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "NavigationView.Header");

        Assert.IsNotNull(cabecera, "La ventana no declara «NavigationView.Header».");

        var sitio = cabecera.Descendants()
            .FirstOrDefault(e => e.Attribute(x + "Name")?.Value == "_versionDelPrograma");

        Assert.IsNotNull(sitio, "La cabecera no tiene un «_versionDelPrograma» donde escribir la versión.");
        Assert.AreEqual(
            "versionDelPrograma",
            sitio.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value,
            "Sin «AutomationId» la versión no se puede leer por accesibilidad, que es como se comprueba.");
    }

    // ---- lo que el ejecutable declara ------------------------------------------

    /// <summary>
    /// El <c>Fichas.exe</c> declara en sus propiedades la misma versión que el código.
    /// </summary>
    /// <remarks>
    /// Mira el <c>Fichas.exe</c> que compila la suite —siempre está— y, si quien corre esto
    /// publicó antes y dejó la carpeta en <c>FICHAS_CARPETA_PUBLICADA</c>, también el
    /// publicado, que es el que abre el dueño. «Versión de producto» es lo que Windows
    /// enseña en Detalles y lo que <c>publish.ps1</c> pone en su resumen.
    /// </remarks>
    [TestMethod]
    public void ElEjecutableDeclaraLaMismaVersionQueElCodigo()
    {
        var ejecutables = LosEjecutablesQueSePuedenMirar();
        if (ejecutables.Count == 0)
            Assert.Inconclusive("No hay ningún Fichas.exe que mirar: sin ejecutable esto no comprueba nada.");

        var numero = ElNumeroQueDiceElCodigo();
        var distintos = new List<string>();

        foreach (var ejecutable in ejecutables)
        {
            var propiedades = FileVersionInfo.GetVersionInfo(ejecutable);
            Console.WriteLine(
                $"{ejecutable}{Environment.NewLine}"
                + $"  producto «{propiedades.ProductVersion}» · archivo «{propiedades.FileVersion}»");

            if (!string.Equals(propiedades.ProductVersion, numero, StringComparison.Ordinal))
                distintos.Add($"{ejecutable}: producto «{propiedades.ProductVersion}», código «{numero}»");

            if (!string.Equals(ConCuatroPartes(numero), propiedades.FileVersion, StringComparison.Ordinal))
                distintos.Add($"{ejecutable}: archivo «{propiedades.FileVersion}», código «{ConCuatroPartes(numero)}»");
        }

        Assert.IsEmpty(
            distintos,
            "El ejecutable no dice la versión del código:" + Environment.NewLine
            + string.Join(Environment.NewLine, distintos));
    }

    // ---- de dónde salen las cifras ---------------------------------------------

    /// <summary>El texto de <c>&lt;Version&gt;</c> en <c>Fichas.App.csproj</c>, tal cual.</summary>
    private static string ElNumeroQueDiceElCodigo()
    {
        var proyecto = XDocument.Load(Path.Combine(LaCarpetaDeFichas(), "Fichas.App", "Fichas.App.csproj"));
        var versiones = proyecto.Descendants()
            .Where(e => e.Name.LocalName == "Version" && e.Parent?.Name.LocalName == "PropertyGroup")
            .ToList();

        Assert.HasCount(1, versiones, "Fichas.App.csproj tiene que escribir <Version> una vez, ni cero ni dos.");
        return versiones[0].Value.Trim();
    }

    /// <summary>«9» → «9.0.0.0», que es como Windows escribe la versión de archivo.</summary>
    private static string ConCuatroPartes(string numero)
    {
        var partes = numero.Split('.').ToList();
        while (partes.Count < 4) partes.Add("0");
        return string.Join('.', partes);
    }

    /// <summary>El <c>Fichas.exe</c> de la suite y, si lo hay, el publicado.</summary>
    private static List<string> LosEjecutablesQueSePuedenMirar()
    {
        var candidatos = new List<string> { Path.Combine(AppContext.BaseDirectory, "Fichas.exe") };

        var publicada = Environment.GetEnvironmentVariable(LaVariableDeLaCarpetaPublicada);
        if (!string.IsNullOrWhiteSpace(publicada))
            candidatos.Add(Path.Combine(publicada, "Fichas.exe"));

        return candidatos.Where(File.Exists).ToList();
    }

    /// <summary>La carpeta <c>csharp/Fichas</c>, o no concluyente si no se encuentra.</summary>
    private static string LaCarpetaDeFichas()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var fichas = Path.Combine(actual.FullName, "csharp", "Fichas");
            if (File.Exists(Path.Combine(fichas, "Directory.Build.props"))) return fichas;
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «csharp/Fichas/Directory.Build.props» desde «{AppContext.BaseDirectory}». "
            + "Sin el árbol delante esto no comprueba nada.");
        return string.Empty;
    }
}
