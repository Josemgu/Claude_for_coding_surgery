using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El Excel del periodo es UNA hoja que reproduce el mockup v3: cabecera marino, tarjetas,
/// dos tablas con semaforo, un grafico nativo por mes y la lista de solo los pendientes.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-16: <i>«El reporte debe ser bonito, profesional, en un solo
/// worksheet, con colores como el azul marino […] Así mismo es que quiero el reporte, como
/// está en el mockup.»</i> (<c>mockups/mockup-v3-reporte-excel.html</c>).</para>
///
/// <para>Se escribe el archivo DE VERDAD y se vuelve a abrir de dos maneras: con ClosedXML para
/// leer celdas y estilos, y con el Open XML SDK para comprobar que el grafico es un objeto de
/// Excel —un <c>ChartPart</c> con dos series— y no una foto. Lo que ClosedXML no sabe leer
/// (el ajuste a una pagina) se cuenta en el XML de la hoja dentro del zip.</para>
/// </remarks>
[TestClass]
public class PruebaDeLaHojaDelResumen
{
    /// <summary>Tres meses, para que el grafico tenga tres barras dobles.</summary>
    private const string Desde = "2026-07-01";

    /// <summary>El ultimo dia del periodo.</summary>
    private const string Hasta = "2026-09-30";

    /// <summary>El doble de casos de los que usa <c>--falso</c> por defecto, como pide el pase.</summary>
    private const int CuantosCasos = 3000;

    /// <summary>El azul marino del mockup, el mismo de la hoja del companero.</summary>
    private static readonly XLColor Marino = XLColor.FromHtml("#16233A");

    /// <summary>El archivo escrito una sola vez para toda la clase, y el resumen con el que se compara.</summary>
    private static string s_ruta = string.Empty;

    /// <summary>El resumen armado sobre la misma base, para comparar celda a celda.</summary>
    private static ResumenDelPeriodo s_resumen = null!;

    /// <summary>El aviso que devolvio la escritura.</summary>
    private static string s_aviso = string.Empty;

    /// <summary>Escribe el <c>.xlsx</c> una vez sobre 3 000 casos; cada prueba lo vuelve a abrir.</summary>
    /// <param name="contexto">El contexto de MSTest; no se usa.</param>
    [ClassInitialize]
    public static void EscribirElArchivo(TestContext contexto)
    {
        var servicios = BaseDePrueba.Montar(CuantosCasos);
        var reportes = new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);

        s_ruta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-hoja-del-resumen.xlsx");
        var resultado = reportes.GenerarReporteDelPeriodoEnExcel(Desde, Hasta, s_ruta);
        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        s_aviso = resultado.Avisos[0].Linea;

        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia);
        s_resumen = ArmadoDelResumen.DelPeriodo(
            lectura, Periodo.Leer(Desde, Hasta).Periodo!, servicios.Reloj.Ahora());
    }

    /// <summary>Borra el archivo al terminar la clase.</summary>
    [ClassCleanup]
    public static void BorrarElArchivo()
    {
        if (File.Exists(s_ruta)) File.Delete(s_ruta);
    }

    // ─────────────────────── una hoja, no siete ───────────────────────

    /// <summary>Dado el periodo sobre 3 000 casos, el archivo tiene UNA hoja y se llama «Reporte».</summary>
    [TestMethod]
    public void ElArchivoTieneUnaSolaHoja()
    {
        using var libro = new XLWorkbook(s_ruta);

        Assert.HasCount(1, libro.Worksheets, "una pestaña: las siete de antes son lo que el dueño llamó «no profesional»");
        Assert.AreEqual(HojaDelResumen.NombreDeLaHoja, libro.Worksheet(1).Name);
    }

    /// <summary>La cabecera A1:N2 va en marino, con el titulo del PDF en B1, el periodo en B2 y el templo en I1.</summary>
    [TestMethod]
    public void LaCabeceraVaEnMarinoConElTituloElPeriodoYElTemplo()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        Assert.AreEqual(Marino, hoja.Cell("A1").Style.Fill.BackgroundColor);
        Assert.AreEqual(Marino, hoja.Cell("N2").Style.Fill.BackgroundColor);
        Assert.AreEqual(Vocabulario.Titulo, hoja.Cell("B1").GetString());
        Assert.AreEqual(XLColor.FromHtml("#FFFFFF"), hoja.Cell("B1").Style.Font.FontColor, "letra blanca sobre marino");
        Assert.IsTrue(hoja.Cell("B1").Style.Font.Bold);
        StringAssert.Contains(hoja.Cell("B2").GetString(), s_resumen.PeriodoEnTexto);
        Assert.AreEqual(s_resumen.Templo, hoja.Cell("I1").GetString());
        StringAssert.Contains(hoja.Cell("I2").GetString(), s_resumen.GeneradoEn[..10]);
    }

    // ─────────────────────── las tarjetas ───────────────────────

    /// <summary>Las tres tarjetas de la izquierda llevan las tres cifras del PDF, como numeros.</summary>
    [TestMethod]
    public void LasTarjetasLlevanViajaronCompletosYSinCompletarComoNumeros()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        Assert.AreEqual("VIAJARON", hoja.Cell("B4").GetString());
        Assert.AreEqual(s_resumen.Viajaron, hoja.Cell("B5").GetDouble());
        Assert.AreEqual("COMPLETOS", hoja.Cell("D4").GetString());
        Assert.AreEqual(s_resumen.Completos, hoja.Cell("D5").GetDouble());
        Assert.AreEqual("SIN COMPLETAR", hoja.Cell("G4").GetString());
        Assert.AreEqual(s_resumen.SinCompletar, hoja.Cell("G5").GetDouble());
        Assert.AreEqual("UNIDADES CON PENDIENTES", hoja.Cell("J4").GetString());
        StringAssert.StartsWith(hoja.Cell("J5").GetString(), $"{s_resumen.UnidadesConPendientes} de {s_resumen.Unidades}");
        Assert.AreEqual("PAQUETES DEVUELTOS", hoja.Cell("L4").GetString());
        StringAssert.StartsWith(hoja.Cell("L5").GetString(), $"{s_resumen.CasosDevueltos} de {s_resumen.CasosConAgente}");
    }

    // ─────────────────────── las dos tablas ───────────────────────

    /// <summary>La tabla por unidad tiene una fila por unidad y su total cuadra con las tarjetas.</summary>
    [TestMethod]
    public void LaTablaPorUnidadCuadraConLasTarjetas()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        CollectionAssert.AreEqual(
            new[] { "N.º", "Unidad", "Viajaron", "Completos", "Sin completar", "Agente", "Devolvió" },
            Enumerable.Range(2, 7).Select(n => hoja.Cell(9, n).GetString()).ToArray());
        Assert.AreEqual(Marino, hoja.Cell("B9").Style.Fill.BackgroundColor);

        var total = 10 + s_resumen.PorUnidad.Count;
        StringAssert.StartsWith(hoja.Cell(total, 2).GetString(), "Total");
        Assert.AreEqual(s_resumen.Viajaron, hoja.Cell(total, 4).GetDouble());
        Assert.AreEqual(s_resumen.Completos, hoja.Cell(total, 5).GetDouble());
        Assert.AreEqual(s_resumen.SinCompletar, hoja.Cell(total, 6).GetDouble());

        var primera = s_resumen.PorUnidad[0];
        Assert.AreEqual(primera.Numero, hoja.Cell("B10").GetString());
        Assert.AreEqual("@", hoja.Cell("B10").Style.NumberFormat.Format, "el número de unidad es texto: que no se coma ceros");
        Assert.AreEqual(primera.Nombre, hoja.Cell("C10").GetString());
        Assert.AreEqual(primera.Viajaron, hoja.Cell("D10").GetDouble());
        Assert.AreEqual(primera.Agente, hoja.Cell("G10").GetString());
    }

    /// <summary>La tabla por agente empieza en J10 y su total cuadra con la suma de sus filas.</summary>
    [TestMethod]
    public void LaTablaPorAgenteCuadraConSusFilas()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        CollectionAssert.AreEqual(
            new[] { "Agente", "Asignados", "Completos", "Sin completar", "Devolvió" },
            Enumerable.Range(10, 5).Select(n => hoja.Cell(9, n).GetString()).ToArray());

        var total = 10 + s_resumen.PorAgente.Count;
        Assert.AreEqual("Total", hoja.Cell(total, 10).GetString());
        Assert.AreEqual(s_resumen.PorAgente.Sum(a => a.Asignados), hoja.Cell(total, 11).GetDouble());
        Assert.AreEqual(s_resumen.PorAgente.Sum(a => a.SinCompletar), hoja.Cell(total, 13).GetDouble());
        Assert.AreEqual($"{s_resumen.CasosDevueltos} de {s_resumen.CasosConAgente}", hoja.Cell(total, 14).GetString());

        var primero = s_resumen.PorAgente[0];
        Assert.AreEqual(primero.Nombre, hoja.Cell("J10").GetString());
        Assert.AreEqual($"{primero.CasosDevueltos} de {primero.CasosACargo}", hoja.Cell("N10").GetString());
    }

    /// <summary>«Devolvió» por unidad es ✓, ✗ o una raya, en Segoe UI Symbol para que se vea en todo Windows.</summary>
    [TestMethod]
    public void DevolvioPorUnidadEsUnaMarcaEnSegoeUiSymbol()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        for (var i = 0; i < s_resumen.PorUnidad.Count; i++)
        {
            var celda = hoja.Cell(10 + i, 8);
            var esperado = s_resumen.PorUnidad[i].Devolvio switch { true => "✓", false => "✗", null => "—" };
            Assert.AreEqual(esperado, celda.GetString(), $"fila {10 + i}");
            Assert.AreEqual("Segoe UI Symbol", celda.Style.Font.FontName);
        }
    }

    /// <summary>El semaforo de «Sin completar» por unidad son tres reglas de formato condicional, en ese orden.</summary>
    [TestMethod]
    public void ElSemaforoPorUnidadSonTresReglasDeFormatoCondicional()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        var reglas = hoja.ConditionalFormats.ToList();
        Assert.HasCount(3, reglas, "verde con cero, naranja con pendientes y devuelto, rojo con pendientes sin devolver");
        var ultimaFila = 9 + s_resumen.PorUnidad.Count;
        foreach (var regla in reglas)
            Assert.AreEqual($"F10:F{ultimaFila}", regla.Range.RangeAddress.ToString());
    }

    // ─────────────────────── el grafico ───────────────────────

    /// <summary>El zip trae un <c>chart1.xml</c>: el grafico es un objeto de Excel, no una imagen.</summary>
    [TestMethod]
    public void ElZipTraeUnChart1Xml()
    {
        using var zip = ZipFile.OpenRead(s_ruta);

        // El SDK lo cuelga del dibujo —xl/drawings/charts/chart1.xml—; Excel lo escribiría en
        // xl/charts/. Los dos sitios valen: lo que importa es que haya un chart1.xml.
        Assert.IsTrue(
            zip.Entries.Any(e => e.FullName.EndsWith("charts/chart1.xml", StringComparison.Ordinal)),
            "sin chart1.xml no hay gráfico nativo: " + string.Join(", ", zip.Entries.Select(e => e.FullName)));
        Assert.AreEqual(0, zip.Entries.Count(e => e.FullName.StartsWith("xl/media/", StringComparison.Ordinal)), "ninguna imagen: el gráfico no es una foto");
    }

    /// <summary>El grafico es de barras con dos series —completos y con dificultades— y un punto por mes, y suma las cifras.</summary>
    [TestMethod]
    public void ElGraficoEsDeBarrasConDosSeriesYUnPuntoPorMes()
    {
        using var documento = SpreadsheetDocument.Open(s_ruta, false);
        var hoja = documento.WorkbookPart!.WorksheetParts.Single();
        var graficos = hoja.DrawingsPart!.ChartParts.ToList();

        Assert.HasCount(1, graficos);
        var barras = graficos[0].ChartSpace!.Descendants<C.BarChart>().Single();
        var series = barras.Elements<C.BarChartSeries>().ToList();
        Assert.HasCount(2, series);

        var completos = series[0].Descendants<C.NumericPoint>().Select(p => double.Parse(p.NumericValue!.Text, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var dificultades = series[1].Descendants<C.NumericPoint>().Select(p => double.Parse(p.NumericValue!.Text, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.HasCount(s_resumen.PorMes.Count, completos);
        Assert.AreEqual(s_resumen.Completos, completos.Sum());
        Assert.AreEqual(s_resumen.SinCompletar, dificultades.Sum());

        var meses = series[0].Descendants<C.StringPoint>().Select(p => p.NumericValue!.Text).ToArray();
        CollectionAssert.AreEqual(s_resumen.PorMes.Select(m => m.Rotulo).ToArray(), meses);
    }

    /// <summary>El archivo ENTERO —hoja, dibujo y grafico— pasa el validador del Open XML SDK sin ningun error.</summary>
    /// <remarks>Entero y no solo el grafico: el <c>drawing</c> y los <c>ignoredErrors</c> se meten en la hoja de ClosedXML, y el sitio donde caen lo manda el esquema.</remarks>
    [TestMethod]
    public void ElArchivoEnteroPasaElValidadorDelOpenXmlSdk()
    {
        using var documento = SpreadsheetDocument.Open(s_ruta, false);

        var errores = new OpenXmlValidator().Validate(documento).ToList();

        Assert.IsEmpty(errores, string.Join(" · ", errores.Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")));
    }

    // ─────────────────────── la lista de pendientes ───────────────────────

    /// <summary>Debajo de todo va la lista de solo los pendientes, una fila por persona, con sus rotulos.</summary>
    [TestMethod]
    public void LaListaDePendientesTieneUnaFilaPorPersonaSinCompletar()
    {
        using var libro = new XLWorkbook(s_ruta);
        var hoja = libro.Worksheet(1);

        var titulo = hoja.Column(2).CellsUsed().Single(c => c.GetString().StartsWith("Pendientes", StringComparison.Ordinal));
        var cabecera = titulo.Address.RowNumber + 1;
        Assert.AreEqual("N.º", hoja.Cell(cabecera, 2).GetString());
        Assert.AreEqual("Persona", hoja.Cell(cabecera, 4).GetString());
        Assert.AreEqual("Qué le falta", hoja.Cell(cabecera, 7).GetString());
        Assert.AreEqual("Agente", hoja.Cell(cabecera, 10).GetString());
        Assert.AreEqual("Templo", hoja.Cell(cabecera, 13).GetString());

        var primera = cabecera + 1;
        Assert.AreEqual(s_resumen.Pendientes[0].Persona, hoja.Cell(primera, 4).GetString());
        Assert.AreEqual(s_resumen.Pendientes[0].QueLeFalta, hoja.Cell(primera, 7).GetString());
        Assert.AreEqual(primera + s_resumen.Pendientes.Count - 1, hoja.LastRowUsed()!.RowNumber(), "tras el último pendiente no hay nada");
    }

    // ─────────────────────── impresion y lectura ───────────────────────

    /// <summary>Las filas 1 y 2 quedan fijas al desplazar.</summary>
    [TestMethod]
    public void LasDosFilasDeCabeceraQuedanFijas()
    {
        using var libro = new XLWorkbook(s_ruta);

        Assert.AreEqual(2, libro.Worksheet(1).SheetView.SplitRow);
    }

    /// <summary>Se imprime apaisado, en A4, a UNA pagina de ancho y las de alto que hagan falta: contado en el XML.</summary>
    [TestMethod]
    public void SeImprimeApaisadoAUnaPaginaDeAncho()
    {
        using var zip = ZipFile.OpenRead(s_ruta);
        using var lector = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = lector.ReadToEnd();

        // ClosedXML escribe la hoja con el prefijo «x:».
        var pageSetup = Regex.Match(xml, "<(x:)?pageSetup [^>]*/>").Value;
        Assert.IsFalse(string.IsNullOrEmpty(pageSetup), "no hay <pageSetup> en la hoja");
        StringAssert.Contains(pageSetup, "orientation=\"landscape\"");
        // fitToWidth vale 1 por defecto en el esquema (ECMA-376, CT_PageSetup) y ClosedXML no lo
        // escribe cuando es 1: lo que no puede haber es otro valor.
        Assert.IsFalse(Regex.IsMatch(pageSetup, "fitToWidth=\"(?!1\")"), pageSetup);
        StringAssert.Contains(pageSetup, "fitToHeight=\"0\"");
        StringAssert.Contains(pageSetup, "paperSize=\"9\"", "9 es A4 en el estándar");
        Assert.IsTrue(Regex.IsMatch(xml, "<(x:)?pageSetUpPr fitToPage=\"1\""), "sin fitToPage el fitToWidth no se aplica");
    }

    /// <summary>Ni «País» ni «no consta» en ninguna celda: el dueno dijo que no son una respuesta.</summary>
    [TestMethod]
    public void NingunaCeldaDicePaisNiNoConsta()
    {
        using var libro = new XLWorkbook(s_ruta);
        var textos = libro.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.IsFalse(textos.Any(t => t.Contains("País", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(textos.Any(t => t.Contains("no consta", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>El aviso de la escritura dice lo que se puede comprobar abriendo: 1 hoja, unidades, agentes, pendientes y 1 grafico.</summary>
    [TestMethod]
    public void ElAvisoDiceUnaHojaYLoQueLleva()
    {
        StringAssert.Contains(s_aviso, "1 hoja");
        StringAssert.Contains(s_aviso, $"{s_resumen.Unidades} unidades");
        StringAssert.Contains(s_aviso, $"{s_resumen.Pendientes.Count} pendientes");
        StringAssert.Contains(s_aviso, "1 gráfico");
    }
}
