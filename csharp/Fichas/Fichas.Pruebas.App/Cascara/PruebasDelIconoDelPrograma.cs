using System.Globalization;
using System.Security.Cryptography;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El programa tiene icono propio, y el ejecutable lo declara.
/// </summary>
/// <remarks>
/// <para>El criterio del que salen son las palabras del dueño del 2026-09-05: «ponle un logo
/// genérico al programa», y el sitio donde más cuenta es el explorador de archivos, que es
/// donde busca <c>Fichas.exe</c> para abrirlo con doble clic.</para>
///
/// <para>Lo que se comprueba aquí es lo que se puede comprobar SIN ventana:</para>
/// <list type="number">
///   <item>El <c>.csproj</c> declara <c>ApplicationIcon</c>. Sin esa línea el ejecutable sale
///   con el icono por defecto de .NET, por muy bonito que sea el <c>.ico</c> del repositorio.
///   Medido el 2026-09-05 antes de tocar nada: la línea NO estaba.</item>
///   <item>El <c>.ico</c> existe y trae las medidas que Windows pide para la ventana (16 y
///   32) y para las vistas grandes del explorador (256).</item>
///   <item>NO es el de la plantilla de Microsoft. Medido el 2026-09-05: el
///   <c>AppIcon.ico</c> de la app era byte a byte el mismo que el de la espiga C0
///   (<c>md5 c2838761752b2161371390e41154ed92</c>), que es el de la plantilla.</item>
/// </list>
///
/// <para>⚠️ Lo que NO mira: cómo se ve. Que el dibujo sea sobrio y no lleve nada del templo
/// ni de ninguna marca es cosa de mirarlo, y va con capturas en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelIconoDelPrograma
{
    /// <summary>El MD5 del icono de la plantilla de Microsoft, medido en la espiga C0.</summary>
    private const string ElDeLaPlantillaDeMicrosoft = "c2838761752b2161371390e41154ed92";

    /// <summary>El ejecutable declara su icono; sin esta línea el explorador no lo ve.</summary>
    [TestMethod]
    public void ElEjecutableDeclaraSuIcono()
    {
        var proyecto = File.ReadAllText(Path.Combine(LaCarpetaDeLaApp(), "Fichas.App.csproj"));

        StringAssert.Contains(
            proyecto,
            "<ApplicationIcon>",
            "Sin «ApplicationIcon» el .exe sale con el icono por defecto de .NET.");
    }

    /// <summary>El archivo que declara el proyecto existe de verdad.</summary>
    [TestMethod]
    public void ElIconoQueDeclaraElProyectoExiste()
        => Assert.IsTrue(File.Exists(RutaDelIcono()), $"No existe «{RutaDelIcono()}».");

    /// <summary>No es el icono de la plantilla de Microsoft: es uno propio.</summary>
    [TestMethod]
    public void ElIconoNoEsElDeLaPlantilla()
    {
        var huella = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(RutaDelIcono())))
            .ToLowerInvariant();

        Assert.AreNotEqual(
            ElDeLaPlantillaDeMicrosoft,
            huella,
            "El icono sigue siendo el de la plantilla de Microsoft copiado de la espiga C0.");
    }

    /// <summary>
    /// Trae las medidas que hacen falta: 16 y 32 para la ventana y la barra de tareas, 256
    /// para las vistas grandes del explorador.
    /// </summary>
    [TestMethod]
    public void ElIconoTraeLasMedidasQueWindowsPide()
    {
        var medidas = LasMedidasDelIcono(RutaDelIcono());

        Console.WriteLine(
            "Medidas dentro del .ico: "
            + string.Join(", ", medidas.Select(m => m.ToString(CultureInfo.InvariantCulture))));

        foreach (var pide in new[] { 16, 32, 256 })
            Assert.Contains(pide, medidas, $"Falta la medida de {pide} píxeles.");
    }

    // ---- de donde salen las cifras -------------------------------------------

    /// <summary>Las medidas declaradas en la cabecera del <c>.ico</c>, en píxeles.</summary>
    /// <remarks>
    /// El formato: 6 bytes de cabecera —reservado, tipo, cuántas imágenes— y luego 16 bytes
    /// por imagen, de los que el primero es el ancho y el segundo el alto. Un cero significa
    /// 256, que es como el formato mete un número que no cabe en un byte.
    /// </remarks>
    private static IReadOnlyList<int> LasMedidasDelIcono(string ruta)
    {
        var bytes = File.ReadAllBytes(ruta);

        Assert.IsGreaterThanOrEqualTo(6, bytes.Length, "El archivo no tiene ni la cabecera de un .ico.");
        Assert.AreEqual(1, BitConverter.ToUInt16(bytes, 2), "El tipo 1 es «icono»; el 2 sería un cursor.");

        var cuantas = BitConverter.ToUInt16(bytes, 4);
        Assert.IsGreaterThan(0, cuantas, "Un .ico sin imágenes dentro no es un icono.");

        return Enumerable.Range(0, cuantas)
            .Select(i => bytes[6 + (i * 16)] == 0 ? 256 : bytes[6 + (i * 16)])
            .ToList();
    }

    /// <summary>La ruta del icono que declara el <c>.csproj</c>, resuelta desde el proyecto.</summary>
    private static string RutaDelIcono()
    {
        var app = LaCarpetaDeLaApp();
        var proyecto = File.ReadAllText(Path.Combine(app, "Fichas.App.csproj"));

        var abre = proyecto.IndexOf("<ApplicationIcon>", StringComparison.Ordinal);
        if (abre < 0) Assert.Fail("El .csproj no declara «ApplicationIcon».");

        abre += "<ApplicationIcon>".Length;
        var cierra = proyecto.IndexOf("</ApplicationIcon>", abre, StringComparison.Ordinal);

        return Path.Combine(app, proyecto[abre..cierra].Trim().Replace('\\', Path.DirectorySeparatorChar));
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
            + "Sin el proyecto delante esto no comprueba nada.");
        return string.Empty;
    }
}
