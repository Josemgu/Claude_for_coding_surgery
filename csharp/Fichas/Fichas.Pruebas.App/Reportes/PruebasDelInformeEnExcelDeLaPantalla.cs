using ClosedXML.Excel;
using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
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
    private const string Desde = "2026-09-01";
    private const string Hasta = "2026-09-30";

    private static readonly PeriodoDeLaPantalla ElPeriodo = new(Desde, Hasta);

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

    [TestMethod]
    public void ElDelPeriodoEnExcelLlevaLasDosFechasYTerminaEnXlsx()
        => Assert.AreEqual(
            "Reporte 2026-09-01 a 2026-09-30.xlsx",
            NombreDeArchivo.DelReporteDelPeriodoEnExcel(ElPeriodo));

    [TestMethod]
    public void ElDelHistoricoEnExcelLlevaElDiaEnQueSeGenero()
        => Assert.AreEqual("Histórico 2026-09-04.xlsx", NombreDeArchivo.DelHistoricoEnExcel("2026-09-04"));

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

    [TestMethod]
    public void ConMotorDeVerdadLaPantallaSabeQuePuedeEscribirEnExcel()
        => Assert.IsTrue(new OperacionDeReporte(MotorDeVerdad(out _)).SabeEscribirEnExcel);

    // ─────────────────────── el andamio ───────────────────────

    /// <summary>El motor de verdad sobre datos inventados: escribe archivos, no los finge.</summary>
    private static IReportes MotorDeVerdad(out ServiciosFalsos servicios)
    {
        servicios = new ServiciosFalsos(40, 20260907, new RelojFijoDeLaPrueba("2026-09-20"));
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    /// <summary>Un reloj fijo: sin el, «ya viajó» cambia de respuesta según el día que se corra.</summary>
    private sealed class RelojFijoDeLaPrueba(string hoy) : IReloj
    {
        public string Hoy() => hoy;

        public string Ahora() => $"{hoy} 10:00:00";

        public string HoyMasDias(int dias)
            => DateOnly.ParseExact(hoy, "yyyy-MM-dd").AddDays(dias).ToString("yyyy-MM-dd");
    }
}
