using Fichas.App.Importar;

namespace Fichas.Pruebas.App.Importar;

/// <summary>Que PDF entran en una tanda, y que dice el resumen cuando termina.</summary>
[TestClass]
public sealed class PruebasDeLasRutasYElResumen
{
    private string _carpeta = string.Empty;

    /// <summary>Monta un arbol de carpetas con PDF y con cosas que no lo son.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-rutas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_carpeta, "agosto"));
        Directory.CreateDirectory(Path.Combine(_carpeta, "septiembre", "semana-2"));
        File.WriteAllText(Path.Combine(_carpeta, "agosto", "b.pdf"), "no importa");
        File.WriteAllText(Path.Combine(_carpeta, "agosto", "a.pdf"), "no importa");
        File.WriteAllText(Path.Combine(_carpeta, "agosto", "notas.txt"), "no es un PDF");
        File.WriteAllText(Path.Combine(_carpeta, "septiembre", "semana-2", "c.PDF"), "mayusculas");
    }

    /// <summary>Borra el arbol de la prueba.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// Una carpeta se recorre con sus subcarpetas, y lo que no es PDF se queda fuera.
    /// </summary>
    /// <remarks>
    /// Los escaneos llegan repartidos por meses: obligar a entrar carpeta por carpeta
    /// seria devolverle a Miguel el problema que la pantalla viene a resolver.
    /// </remarks>
    [TestMethod]
    public void UnaCarpetaSeRecorreConSusSubcarpetasYSoloTraePdf()
    {
        var rutas = RutasDePdf.Reunir([_carpeta]).Pdf;

        Assert.HasCount(3, rutas);
        Assert.IsFalse(rutas.Any(ruta => ruta.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(rutas.Any(ruta => ruta.EndsWith("c.PDF", StringComparison.OrdinalIgnoreCase)),
            "la extension en mayusculas tambien es un PDF.");
    }

    /// <summary>
    /// Elegir un archivo y ademas su carpeta no lo procesa dos veces.
    /// </summary>
    /// <remarks>
    /// Es un descuido normal, y procesarlo dos veces produciria un duplicado avisado que
    /// parece un fallo del programa cuando no lo es.
    /// </remarks>
    [TestMethod]
    public void UnArchivoElegidoDosVecesEntraUnaSolaVez()
    {
        var suelto = Path.Combine(_carpeta, "agosto", "a.pdf");

        var rutas = RutasDePdf.Reunir([suelto, _carpeta, suelto]).Pdf;

        Assert.HasCount(3, rutas);
    }

    /// <summary>Las rutas salen ordenadas, para que la barra avance por un orden seguible.</summary>
    [TestMethod]
    public void LasRutasSalenOrdenadas()
    {
        var rutas = RutasDePdf.Reunir([_carpeta]).Pdf;

        CollectionAssert.AreEqual(rutas.OrderBy(ruta => ruta, StringComparer.OrdinalIgnoreCase).ToArray(),
                                  rutas.ToArray());
    }

    /// <summary>Un origen que no existe se ignora y no tumba la tanda.</summary>
    [TestMethod]
    public void UnOrigenQueNoExisteSeIgnora()
    {
        var rutas = RutasDePdf.Reunir([Path.Combine(_carpeta, "no-existe"), _carpeta]).Pdf;

        Assert.HasCount(3, rutas);
    }

    /// <summary>
    /// El resumen cabe en UNA linea y lleva las cuatro cifras, tambien cuando son cero.
    /// </summary>
    /// <remarks>
    /// Requisito 4 del dueno: «ni un parrafo en pantalla». Y las cifras van siempre,
    /// tambien los ceros: un resumen que se calla los ceros obliga a preguntarse si es
    /// que no hubo o es que no se cuenta, y esa duda recae sobre los numeros.
    /// </remarks>
    [TestMethod]
    public void ElResumenCabeEnUnaLineaYLlevaLasCuatroCifras()
    {
        var resumen = new ResumenDeLaTanda(7);
        for (var documento = 0; documento < 7; documento++)
        {
            resumen.Anotar(new ResultadoDeUnDocumento(
                $"C:/pdfs/{documento}.pdf", Hojas: 1, Casos: 1, Personas: 1,
                Pendientes: 0, Duplicados: 0, Ilegibles: 0, Error: null, Segundos: 9.0));
        }

        var linea = resumen.Linea();

        Assert.DoesNotContain("\n", linea);
        Assert.Contains("7 casos", linea);
        Assert.Contains("7 personas", linea);
        Assert.Contains("0 duplicados", linea);
        Assert.Contains("0 ilegibles", linea);
    }

    /// <summary>El resumen concuerda en numero: «1 caso», no «1 casos».</summary>
    [TestMethod]
    public void ElResumenConcuerdaEnNumero()
    {
        var resumen = new ResumenDeLaTanda(1);
        resumen.Anotar(new ResultadoDeUnDocumento(
            "C:/pdfs/uno.pdf", Hojas: 1, Casos: 1, Personas: 1,
            Pendientes: 0, Duplicados: 0, Ilegibles: 0, Error: null, Segundos: 9.0));

        Assert.Contains("1 caso ", resumen.Linea() + " ");
        Assert.DoesNotContain("1 casos", resumen.Linea());
    }

    /// <summary>El detalle si es largo, y dice de cada documento que salio.</summary>
    [TestMethod]
    public void ElDetalleDiceLoDeCadaDocumento()
    {
        var resumen = new ResumenDeLaTanda(2);
        resumen.Anotar(new ResultadoDeUnDocumento(
            "C:/pdfs/uno.pdf", 1, 1, 3, 0, 0, 0, null, 9.4));
        resumen.Anotar(new ResultadoDeUnDocumento(
            "C:/pdfs/roto.pdf", 0, 0, 0, 0, 0, 1, "No se pudo abrir", 0.2));

        var detalle = resumen.Detalle();

        Assert.Contains("uno.pdf", detalle);
        Assert.Contains("roto.pdf", detalle);
        Assert.Contains("No se pudo abrir", detalle);
    }
}
