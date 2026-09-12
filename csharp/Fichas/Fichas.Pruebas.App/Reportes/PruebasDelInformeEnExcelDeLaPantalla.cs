using ClosedXML.Excel;
using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Reportes;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Los tres informes en Excel desde la pantalla: el nombre que se propone y lo que se dice.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-07: <i>«agregar un paquete de reporte en Excel; está bien el de
/// PDF, pero también quiero uno con Excel»</i>.</para>
///
/// <para>⚠️ <b>Aqui se comprueba lo que la pantalla AFIRMA</b>, que es lo que ya fallo una vez:
/// con <c>--falso</c> el motor decia que habia escrito y no tocaba el disco, y la pantalla
/// repetia su palabra. Por eso cada prueba de estas mira el archivo, no el resultado.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelInformeEnExcelDeLaPantalla
{
    /// <summary>El primer día del período de estas pruebas.</summary>
    private const string Desde = "2026-09-01";
    /// <summary>El último día del período de estas pruebas.</summary>
    private const string Hasta = "2026-09-30";

    /// <summary>Septiembre entero, tal como lo tendría puesto la pantalla.</summary>
    private static readonly PeriodoDeLaPantalla ElPeriodo = new(Desde, Hasta);

    /// <summary>La carpeta propia de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba; nunca la carpeta de datos del dueno.</summary>
    [TestInitialize]
    public void PrepararLaCarpeta()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-excel-reportes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Se lleva la carpeta al terminar; una prueba no deja basura en el disco.</summary>
    [TestCleanup]
    public void RecogerLaCarpeta()
    {
        try
        {
            if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"No se pudo borrar «{_carpeta}»: {causa.Message}");
        }
    }

    // ─────────────────────── el nombre que se propone ───────────────────────

    /// <summary>Vigila que el nombre del informe del período en Excel lleve las dos fechas y acabe en «.xlsx».</summary>
    [TestMethod]
    public void ElDelPeriodoEnExcelLlevaLasDosFechasYTerminaEnXlsx()
        => Assert.AreEqual(
            "Reporte 2026-09-01 a 2026-09-30.xlsx",
            NombreDeArchivo.DelReporteDelPeriodoEnExcel(ElPeriodo));

    /// <summary>Vigila que el nombre del histórico en Excel lleve el día en que se generó.</summary>
    [TestMethod]
    public void ElDelHistoricoEnExcelLlevaElDiaEnQueSeGenero()
        => Assert.AreEqual("Histórico 2026-09-04.xlsx", NombreDeArchivo.DelHistoricoEnExcel("2026-09-04"));

    /// <summary>Vigila que el nombre del informe de agente en Excel lleve al agente y el período.</summary>
    [TestMethod]
    public void ElDelInformeDeAgenteEnExcelLlevaAlAgenteYElPeriodo()
        => Assert.AreEqual(
            "Informe de Sandy 2026-09-01 a 2026-09-30.xlsx",
            NombreDeArchivo.DelInformeDeAgenteEnExcel("Sandy", ElPeriodo));

    /// <summary>Lo que Windows no admite se limpia igual que en el del PDF.</summary>
    [TestMethod]
    public void LoQueWindowsNoAdmiteSeSustituyeTambienEnElDeExcel()
    {
        var nombre = NombreDeArchivo.DelInformeDeAgenteEnExcel("Ana/María: \"la\" <2>", ElPeriodo);

        Assert.IsFalse(
            nombre.Any(letra => Path.GetInvalidFileNameChars().Contains(letra)),
            $"El nombre «{nombre}» todavía lleva un carácter que Windows no admite.");
        Assert.EndsWith(".xlsx", nombre, StringComparison.Ordinal);
    }

    /// <summary>Un agente sin nombre no deja el archivo sin nombre.</summary>
    [TestMethod]
    public void UnAgenteSinNombreSigueDandoUnNombreDeArchivo()
        => Assert.AreEqual(
            $"Informe de {NombreDeArchivo.CuandoNoHayNombre} 2026-09-01 a 2026-09-30.xlsx",
            NombreDeArchivo.DelInformeDeAgenteEnExcel("   ", ElPeriodo));

    // ─────────────────────── los tres, escritos de verdad ───────────────────────

    /// <summary>Vigila que el informe del período en Excel quede escrito de verdad y la línea diga cuánto ocupa.</summary>
    [TestMethod]
    public void ElInformeDelPeriodoEnExcelQuedaEscritoYSeDiceCuantoOcupa()
    {
        var operacion = new OperacionDeReporte(MotorDeVerdad(out _));
        var ruta = Path.Combine(_carpeta, NombreDeArchivo.DelReporteDelPeriodoEnExcel(ElPeriodo));

        var resumen = operacion.DelPeriodoEnExcel(ElPeriodo, ruta);

        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        Assert.IsTrue(File.Exists(ruta), $"No se escribió «{ruta}».");
        Assert.AreEqual(ruta, resumen.Ruta);
        Assert.Contains("bytes", resumen.Linea, StringComparison.Ordinal);
        Assert.Contains("Reporte 2026-09-01 a 2026-09-30.xlsx", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>Vigila que el histórico en Excel quede escrito y se abra como libro con más de una hoja.</summary>
    [TestMethod]
    public void ElHistoricoEnExcelQuedaEscritoYSeAbre()
    {
        var operacion = new OperacionDeReporte(MotorDeVerdad(out _));
        var ruta = Path.Combine(_carpeta, NombreDeArchivo.DelHistoricoEnExcel("2026-09-20"));

        var resumen = operacion.HistoricoEnExcel(ruta);

        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        using var libro = new XLWorkbook(ruta);
        Assert.IsGreaterThan(1, libro.Worksheets.Count, "el resumen y al menos una tabla");
    }

    /// <summary>Vigila que el informe de un agente en Excel quede escrito y se abra como libro.</summary>
    [TestMethod]
    public void ElInformeDeUnAgenteEnExcelQuedaEscritoYSeAbre()
    {
        var motor = MotorDeVerdad(out var servicios);
        var quien = servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, int.MaxValue))
            .Elementos.OrderBy(c => c.Id).First();
        var operacion = new OperacionDeReporte(motor);
        var ruta = Path.Combine(_carpeta, NombreDeArchivo.DelInformeDeAgenteEnExcel(quien.Nombre, ElPeriodo));

        var resumen = operacion.DeUnAgenteEnExcel(quien, ElPeriodo, ruta);

        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        using var libro = new XLWorkbook(ruta);
        Assert.IsGreaterThan(1, libro.Worksheets.Count);
    }

    // ─────────────────────── cuando NO hay motor de Excel ───────────────────────

    /// <summary>
    /// Con <c>--falso</c> no hay motor de informes, y la pantalla lo dice en vez de fingir.
    /// </summary>
    /// <remarks>
    /// Es la misma decision que ya tomaron el reporte de la segunda vuelta y el mantenimiento:
    /// un boton que ahi dijera que escribio estaria mintiendo sobre un archivo que nadie va a
    /// encontrar despues.
    /// </remarks>
    [TestMethod]
    public void SinMotorDeExcelSeDiceYNoSeEscribeNada_NoLanza()
    {
        var operacion = new OperacionDeReporte(new ServiciosFalsos(3, 1, new RelojDelSistema()).Reportes);
        var ruta = Path.Combine(_carpeta, "no-deberia-existir.xlsx");

        Assert.IsFalse(operacion.SabeEscribirEnExcel);

        var resumen = operacion.DelPeriodoEnExcel(ElPeriodo, ruta);

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsNull(resumen.Ruta, "no hay archivo que abrir");
        Assert.IsFalse(File.Exists(ruta));
        Assert.IsNotEmpty(resumen.Avisos);
    }

    /// <summary>Vigila que con el motor de verdad la pantalla sepa que puede escribir en Excel.</summary>
    [TestMethod]
    public void ConMotorDeVerdadLaPantallaSabeQuePuedeEscribirEnExcel()
        => Assert.IsTrue(new OperacionDeReporte(MotorDeVerdad(out _)).SabeEscribirEnExcel);

    // ─────────────────────── el andamio ───────────────────────

    /// <summary>El motor de verdad sobre datos inventados: escribe archivos, no los finge.</summary>
    /// <param name="servicios">Los servicios falsos sobre los que se montó, por si la prueba necesita sus datos.</param>
    private static IReportes MotorDeVerdad(out ServiciosFalsos servicios)
    {
        servicios = new ServiciosFalsos(40, 20260907, new RelojFijoDeLaPrueba("2026-09-20"));
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    /// <summary>Un reloj fijo: sin el, «ya viajó» cambia de respuesta según el día que se corra.</summary>
    /// <param name="hoy">El día en que se para, en ISO.</param>
    private sealed class RelojFijoDeLaPrueba(string hoy) : IReloj
    {
        /// <inheritdoc />
        public string Hoy() => hoy;

        /// <inheritdoc />
        public string Ahora() => $"{hoy} 10:00:00";

        /// <inheritdoc />
        public string HoyMasDias(int dias)
            => DateOnly.ParseExact(hoy, "yyyy-MM-dd").AddDays(dias).ToString("yyyy-MM-dd");
    }
}
