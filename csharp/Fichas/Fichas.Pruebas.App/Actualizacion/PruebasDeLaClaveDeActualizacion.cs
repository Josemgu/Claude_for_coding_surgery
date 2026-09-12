using Fichas.App.Actualizacion;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// La clave de solo lectura que el dueño pega en la carpeta de datos: dónde se busca, cómo
/// se limpia y qué pasa cuando no está.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> DECISIONES.md, 2026-09-11: «La clave […] la pega
/// él en <c>Documentos\Fichas\clave-de-actualizacion.txt</c>. […] Si el archivo no está, el
/// programa prueba sin clave». La carpeta es la de datos —la de <c>--carpeta-de-datos</c>—,
/// no la del programa: una actualización vacía la del programa (Fichas.iss, [InstallDelete])
/// y se llevaría la clave con ella.</para>
///
/// <para>⛔ Aquí no hay ninguna clave de verdad. Las de estas pruebas son texto inventado en
/// una carpeta temporal que se borra al terminar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaClaveDeActualizacion
{
    /// <summary>La carpeta de datos de cada prueba; nunca la del dueño.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Crea una carpeta propia y vacía.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "Fichas-pruebas-clave-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Borra la carpeta de la prueba.</summary>
    [TestCleanup]
    public void Recoger()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    /// <summary>El archivo se llama como dice la decisión y vive en la carpeta de datos.</summary>
    [TestMethod]
    public void ElArchivoSeLlamaComoDiceLaDecisionYViveEnLaCarpetaDeDatos()
    {
        var ruta = ClaveDeActualizacion.RutaEn(_carpeta);

        Assert.AreEqual("clave-de-actualizacion.txt", Path.GetFileName(ruta));
        Assert.AreEqual(_carpeta, Path.GetDirectoryName(ruta));
    }

    /// <summary>Dado que el archivo no existe, no hay clave: se consultará sin cabecera.</summary>
    [TestMethod]
    public void SinArchivoNoHayClave()
        => Assert.IsNull(ClaveDeActualizacion.Leer(_carpeta));

    /// <summary>Dado un archivo vacío o solo con espacios y saltos, tampoco hay clave.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\r\n\r\n")]
    public void UnArchivoVacioEsComoNoTenerlo(string contenido)
    {
        File.WriteAllText(ClaveDeActualizacion.RutaEn(_carpeta), contenido);

        Assert.IsNull(ClaveDeActualizacion.Leer(_carpeta));
    }

    /// <summary>Dado un archivo con la clave y espacios o saltos alrededor, se lee recortada.</summary>
    /// <remarks>Pegar con el Bloc de notas deja un salto de línea al final casi siempre.</remarks>
    [TestMethod]
    [DataRow("clave-inventada-123\r\n")]
    [DataRow("  clave-inventada-123  ")]
    [DataRow("\r\nclave-inventada-123\r\n\r\n")]
    [DataRow("clave-inventada-123")]
    public void LaClaveSeLeeRecortada(string contenido)
    {
        File.WriteAllText(ClaveDeActualizacion.RutaEn(_carpeta), contenido);

        Assert.AreEqual("clave-inventada-123", ClaveDeActualizacion.Leer(_carpeta));
    }

    /// <summary>Dado un archivo con varias líneas, solo vale la primera que tenga algo.</summary>
    /// <remarks>Si el dueño deja una nota debajo, la nota no se manda a GitHub.</remarks>
    [TestMethod]
    public void SoloCuentaLaPrimeraLineaConAlgo()
    {
        File.WriteAllText(ClaveDeActualizacion.RutaEn(_carpeta), "\r\nclave-inventada-123\r\nuna nota mía\r\n");

        Assert.AreEqual("clave-inventada-123", ClaveDeActualizacion.Leer(_carpeta));
    }

    /// <summary>Dado que la carpeta de datos no existe, no hay clave y no se lanza.</summary>
    [TestMethod]
    public void UnaCarpetaQueNoExisteNoLanza()
        => Assert.IsNull(ClaveDeActualizacion.Leer(Path.Combine(_carpeta, "no-existe")));
}
