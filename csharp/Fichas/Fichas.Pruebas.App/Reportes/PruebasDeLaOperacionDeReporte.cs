
using Fichas.App.Reportes;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Generar el reporte desde la pantalla: que se dice, con que numero, y que se deja en la franja.
/// </summary>
/// <remarks>
/// El criterio del pase: «generar el reporte desde la ventana produce un PDF que existe».
/// La parte de «que existe» se comprueba AQUI, sin ventana, midiendo el archivo despues de
/// escribirlo. Si la biblioteca dijera que escribio y el archivo no estuviera, la pantalla
/// tiene que decirlo: eso es lo que pasa hoy con <c>--falso</c>, y callarlo dejaria al dueno
/// buscando un PDF que nunca se creo.
/// </remarks>
[TestClass]
public sealed class PruebasDeLaOperacionDeReporte
{
    /// <summary>La carpeta propia de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba; nunca la carpeta de datos del dueno.</summary>
    [TestInitialize]
    public void PrepararLaCarpeta()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", Guid.NewGuid().ToString("N"));
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
            // Si Windows todavia tiene el archivo tomado, no es motivo para fallar la prueba:
            // lo que se comprobaba ya se comprobo. Se dice y se sigue.
            Console.WriteLine($"No se pudo borrar «{_carpeta}»: {causa.Message}");
        }
    }

    /// <summary>El PDF queda escrito, y la linea dice su nombre y cuanto ocupa.</summary>
    [TestMethod]
    public void ElReporteDelPeriodoQuedaEscritoYSeDiceCuantoOcupa()
    {
        var reportes = new ReportesDeMentirijilla(new byte[1234]);
        var operacion = new OperacionDeReporte(reportes);
        var ruta = Path.Combine(_carpeta, "Reporte 2026-09-01 a 2026-09-30.pdf");

        var resumen = operacion.DelPeriodo(new PeriodoDeLaPantalla("2026-09-01", "2026-09-30"), ruta);

        Assert.IsTrue(File.Exists(ruta), $"No se escribió «{ruta}».");
        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        Assert.AreEqual(ruta, resumen.Ruta);
        Assert.Contains("1234 bytes", resumen.Linea, StringComparison.Ordinal);
        Assert.Contains("Reporte 2026-09-01 a 2026-09-30.pdf", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>Las dos fechas llegan a la biblioteca tal como las tiene la pantalla.</summary>
    [TestMethod]
    public void LasDosFechasLleganTalCualALaBiblioteca()
    {
        var reportes = new ReportesDeMentirijilla(new byte[1]);
        var operacion = new OperacionDeReporte(reportes);

        operacion.DelPeriodo(new PeriodoDeLaPantalla("2026-06-07", "2026-09-04"), Path.Combine(_carpeta, "r.pdf"));

        Assert.AreEqual(("2026-06-07", "2026-09-04"), reportes.UltimoPeriodo);
    }

    /// <summary>La linea del resumen cabe en un renglon: ni un parrafo (requisito 4).</summary>
    [TestMethod]
    public void LaLineaCabeEnUnRenglon()
    {
        var operacion = new OperacionDeReporte(new ReportesDeMentirijilla(new byte[10]));

        var resumen = operacion.Historico(Path.Combine(_carpeta, "h.pdf"));

        Assert.IsLessThanOrEqualTo(160, resumen.Linea.Length, $"La línea mide {resumen.Linea.Length}: «{resumen.Linea}».");
        Assert.DoesNotContain("\n", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>
    /// Si la biblioteca dice que escribio y el archivo NO esta, se dice; no se da por bueno.
    /// </summary>
    /// <remarks>
    /// Es lo que pasa hoy al arrancar con <c>--falso</c>: <c>ReportesFalsos</c> contesta que
    /// genero y no toca el disco. Sin esta comprobacion la pantalla diria «quedó en…» y el
    /// dueno buscaria un archivo que nunca existio.
    /// </remarks>
    [TestMethod]
    public void SiDijoQueEscribioYNoEstaElArchivoSeDice()
    {
        var operacion = new OperacionDeReporte(new ReportesDeMentirijilla());
        var ruta = Path.Combine(_carpeta, "no-existe.pdf");

        var resumen = operacion.Historico(ruta);

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsNull(resumen.Ruta);
        Assert.Contains("no está", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>
    /// Todos los avisos de la biblioteca salen DENTRO del resumen; ninguno se pierde.
    /// </summary>
    /// <remarks>
    /// ⚠️ Viajan dentro y no se dejan en la franja desde aqui, y es por un fallo medido el
    /// 2026-09-04 abriendo la ventana: generar corre fuera del hilo de la interfaz, y dejar un
    /// aviso hace que la franja se repinte sola. Repintar desde otro hilo revienta con
    /// <c>COMException 0x8001010E</c>. La franja la toca la pantalla, que si esta en su hilo.
    /// </remarks>
    [TestMethod]
    public void TodosLosAvisosDeLaBibliotecaSalenDentroDelResumen()
    {
        var operacion = new OperacionDeReporte(new ReportesDeMentirijilla(new byte[3]));

        var resumen = operacion.Historico(Path.Combine(_carpeta, "h.pdf"));

        Assert.IsNotEmpty(resumen.Avisos);
        Assert.IsTrue(
            resumen.Avisos.Any(aviso => aviso.Linea.Contains("Histórico completo", StringComparison.Ordinal)),
            "El aviso que dio la biblioteca no salió en el resumen.");
    }

    /// <summary>Con los reportes inventados no se miente: se dice que no se escribió nada.</summary>
    [TestMethod]
    public void ConLosReportesInventadosSeDiceQueNoSeEscribioNada()
    {
        var servicios = new ServiciosFalsos(20, 5, new RelojFijo("2026-09-04"));
        var operacion = new OperacionDeReporte(servicios.Reportes);

        var resumen = operacion.DelPeriodo(new PeriodoDeLaPantalla("2026-09-01", "2026-09-30"), Path.Combine(_carpeta, "x.pdf"));

        Assert.IsFalse(resumen.SalioBien, "Con datos inventados no se escribe ningún PDF y la pantalla tiene que decirlo.");
    }

    /// <summary>Un periodo del reves NO se escribe, y el motivo llega entero a la franja.</summary>
    [TestMethod]
    public void UnPeriodoDelRevesNoSeEscribeYSeDicePorQue()
    {
        var servicios = new ServiciosFalsos(5, 5, new RelojFijo("2026-09-04"));
        var operacion = new OperacionDeReporte(servicios.Reportes);

        // Los reportes inventados no validan el periodo, asi que aqui se comprueba lo unico
        // que es de esta clase: que lo que devuelva la biblioteca se pinta sin recortarlo.
        var resumen = operacion.DelPeriodo(new PeriodoDeLaPantalla("2026-09-30", "2026-09-01"), Path.Combine(_carpeta, "y.pdf"));

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsNotEmpty(resumen.Detalle);
    }

    /// <summary>El aviso de un manejador roto nombra el boton y trae la excepcion entera.</summary>
    [TestMethod]
    public void ElAvisoDeUnManejadorRotoNombraElBotonYTraeLaExcepcion()
    {
        var aviso = ManejadorSeguro.TextoDelFallo(
            "Generar el PDF…", new InvalidOperationException("el selector no abrió"));

        Assert.AreEqual(GravedadDeAviso.Problema, aviso.Gravedad);
        Assert.Contains("Generar el PDF…", aviso.Linea, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", aviso.Linea, StringComparison.Ordinal);
        Assert.Contains("el selector no abrió", aviso.Detalle!, StringComparison.Ordinal);
        Assert.Contains("fichas.log", aviso.Detalle!, StringComparison.Ordinal);
    }
}
