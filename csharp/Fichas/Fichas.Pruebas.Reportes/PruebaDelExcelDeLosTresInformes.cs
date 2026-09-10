using ClosedXML.Excel;
using Fichas.Contratos.Consultas;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Formato;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Los tres informes de la pantalla de Reportes, tambien en Excel, desde el MISMO motor.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-07: <i>«agregar un paquete de reporte en Excel; está bien el de
/// PDF, pero también quiero uno con Excel»</i>. Los tres y no uno: el armado es el mismo para
/// los tres, asi que dejar dos fuera no ahorra trabajo, solo deja al dueno preguntandose por
/// que este si y aquel no.</para>
///
/// <para>⚠️ <b>El Excel sale del MISMO <c>Documento</c> que el PDF</b> y no de un armado
/// propio. Con dos armados, el dia que cambie una seccion uno de los dos se queda atras y
/// nadie lo nota hasta que el PDF y el Excel del mismo mes dicen cifras distintas. Lo que
/// cambia entre los dos es la FORMA, no el contenido.</para>
/// </remarks>
[TestClass]
public class PruebaDelExcelDeLosTresInformes
{
    private const string Desde = "2026-09-01";
    private const string Hasta = "2026-09-30";
    private const int CuantosCasos = 300;

    private static ReportesEnPdf Montar(out ServiciosFalsos servicios, int casos = CuantosCasos)
    {
        servicios = BaseDePrueba.Montar(casos);
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    private static string RutaTemporal(string nombre)
        => Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-{nombre}");

    private static long PrimerCompanero(ServiciosFalsos servicios)
        => servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, int.MaxValue))
            .Elementos.OrderBy(c => c.Id).First().Id;

    // ─────────────────────── los tres escriben un .xlsx que abre ───────────────────────

    [TestMethod]
    public void ElInformeDelPeriodoEnExcelAbreYTieneUnaHojaPorSeccion()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("periodo.xlsx");
        try
        {
            var resultado = reportes.GenerarReporteDelPeriodoEnExcel(Desde, Hasta, ruta);

            Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
            var documento = reportes.DocumentoDelPeriodo(
                Fichas.Reportes.Reglas.Periodo.Leer(Desde, Hasta).Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

            using var libro = new XLWorkbook(ruta);
            Assert.AreEqual(documento.Secciones.Count + 1, libro.Worksheets.Count, "una hoja por sección, más el resumen");
            Assert.AreEqual(NombreDeHoja.DelResumen, libro.Worksheets.First().Name);
        }
        finally { Borrar(ruta); }
    }

    [TestMethod]
    public void ElHistoricoEnExcelAbreYTraeSusFilas()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("historico.xlsx");
        try
        {
            var resultado = reportes.GenerarHistoricoEnExcel(ruta);

            Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
            var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
            var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");
            Assert.IsNotEmpty(tabla.Filas, "sin archivados esto no probaría nada");

            using var libro = new XLWorkbook(ruta);
            var hoja = libro.Worksheets.Skip(1).First();
            Assert.AreEqual(tabla.Columnas.Count, hoja.LastCellUsed().Address.ColumnNumber);
            Assert.AreEqual(tabla.Filas.Count + 1, hoja.LastCellUsed().Address.RowNumber, "las filas de la sección más la cabecera");
        }
        finally { Borrar(ruta); }
    }

    [TestMethod]
    public void ElInformeDeAgenteEnExcelAbreYAbreConLoQueHizo()
    {
        var reportes = Montar(out var servicios);
        var ruta = RutaTemporal("agente.xlsx");
        try
        {
            var resultado = reportes.GenerarReporteDeCompaneroEnExcel(PrimerCompanero(servicios), Desde, Hasta, ruta);

            Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
            using var libro = new XLWorkbook(ruta);
            StringAssert.Contains(libro.Worksheets.Skip(1).First().Name, "Lo que hizo",
                "el informe de un agente abre por lo que hizo, igual que su PDF");
        }
        finally { Borrar(ruta); }
    }

    // ─────────────────────── el cero de delante, en el archivo de verdad ───────────────────────

    /// <summary>
    /// Se abre el <c>.xlsx</c> ESCRITO y se lee de vuelta un MRN de los que empiezan por cero.
    /// </summary>
    /// <remarks>
    /// No basta con mirar el libro en memoria: lo que se come el cero es el viaje a disco y la
    /// vuelta. Aqui se escribe el archivo, se cierra, se vuelve a abrir y se compara con lo que
    /// dice la base.
    /// </remarks>
    [TestMethod]
    public void UnMrnConCeroDelanteVuelveDelArchivoConSuCero()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("ceros.xlsx");
        try
        {
            Assert.IsTrue(reportes.GenerarReporteDelPeriodoEnExcel(Desde, Hasta, ruta).SeEscribio);

            using var libro = new XLWorkbook(ruta);
            var parteUno = libro.Worksheets.Single(h => h.Name.Contains("Parte 1", StringComparison.Ordinal));
            var columnaDelMrn = Enumerable.Range(1, 20).First(n => parteUno.Cell(1, n).GetString() == "MRN");

            var conCero = parteUno.Column(columnaDelMrn).CellsUsed()
                .Skip(1)
                .FirstOrDefault(c => c.GetString().StartsWith('0'));

            Assert.IsNotNull(conCero, "la base inventada tiene que traer algún MRN con cero delante o esto no prueba nada");
            Assert.AreEqual(XLDataType.Text, conCero.DataType);
            Assert.AreEqual("@", conCero.Style.NumberFormat.Format);
            Assert.AreEqual(13, conCero.GetString().Length, "un MRN es 3-4-4 con sus dos guiones: 13 caracteres");
        }
        finally { Borrar(ruta); }
    }

    // ─────────────────────── avisar, nunca impedir ───────────────────────

    [TestMethod]
    public void UnPeriodoDelRevesNoEscribeNingunExcelYLoDice_NoLanza()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("del-reves.xlsx");

        var resultado = reportes.GenerarReporteDelPeriodoEnExcel(Hasta, Desde, ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(ruta), "no se escribe un archivo de un periodo que no se entiende");
        Assert.IsNotEmpty(resultado.Avisos);
    }

    [TestMethod]
    public void UnCompaneroQueNoExisteNoEscribeNingunExcelYLoDice_NoLanza()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("nadie.xlsx");

        var resultado = reportes.GenerarReporteDeCompaneroEnExcel(-7, Desde, Hasta, ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(ruta));
        StringAssert.Contains(resultado.Avisos[0].Linea, "-7");
    }

    [TestMethod]
    public void UnaRutaVaciaNoEscribeNadaYLoDice_NoLanza()
    {
        var reportes = Montar(out _);

        var resultado = reportes.GenerarHistoricoEnExcel("   ");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsNotEmpty(resultado.Avisos);
    }

    /// <summary>Lo que esta a medias se llama <c>.parcial</c>, y al terminar no queda nada a medias.</summary>
    [TestMethod]
    public void AlTerminarNoQuedaNingunArchivoParcial()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("sin-restos.xlsx");
        try
        {
            Assert.IsTrue(reportes.GenerarHistoricoEnExcel(ruta).SeEscribio);
            Assert.IsFalse(File.Exists(ruta + ".parcial"));
        }
        finally { Borrar(ruta); }
    }

    /// <summary>El aviso dice cuantas hojas y cuantas filas, que es lo que se puede comprobar abriendo.</summary>
    [TestMethod]
    public void ElAvisoDiceCuantasHojasYCuantasFilasLleva()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("aviso.xlsx");
        try
        {
            var resultado = reportes.GenerarHistoricoEnExcel(ruta);
            var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");

            var linea = resultado.Avisos[0].Linea;
            StringAssert.Contains(linea, (documento.Secciones.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            StringAssert.Contains(linea, LibroDelInforme.CuantasFilasLleva(documento).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        finally { Borrar(ruta); }
    }

    // ─────────────────────── el motor es uno solo ───────────────────────

    /// <summary>
    /// El mismo objeto contesta a los dos puertos: no hay un segundo motor de informes.
    /// </summary>
    [TestMethod]
    public void ElMotorDePdfEsElMismoObjetoQueElDeExcel()
    {
        var reportes = Montar(out _, casos: 1);

        Assert.IsInstanceOfType<Fichas.Contratos.Puertos.IReportes>(reportes);
        Assert.IsInstanceOfType<IReportesEnExcel>(reportes);
    }

    private static void Borrar(string ruta)
    {
        if (File.Exists(ruta)) File.Delete(ruta);
        if (File.Exists(ruta + ".parcial")) File.Delete(ruta + ".parcial");
    }
}
