using System.Text;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Reportes;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El historico: los casos archivados, marcados como archivados.
/// </summary>
/// <remarks>
/// ⚠️ El historico NO es un porte: el proyecto viejo del dueno no lo tiene (medido por el
/// agente que lo leyo: `grep -rn -i "historico|histórico"` sobre el proyecto viejo no
/// devuelve ninguna linea). Se arma con la consulta `casos_archivados` de
/// `datos/archivo.py:215` del Python nuevo, que es lo unico que hay. Va dicho en la
/// entrega para que nadie lo lea como «igual que el viejo».
/// </remarks>
[TestClass]
public class PruebaDelHistorico
{
    private static ReportesEnPdf Montar(out Fichas.Datos.Falso.ServiciosFalsos servicios, int casos = 300)
    {
        servicios = BaseDePrueba.Montar(casos);
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    private static string RutaTemporal(string nombre)
        => Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-{nombre}");

    [TestMethod]
    public void ElHistoricoEscribeUnPdfQueAbre()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("historico.pdf");

        var resultado = reportes.GenerarHistorico(ruta);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        var texto = Encoding.Latin1.GetString(File.ReadAllBytes(ruta));
        StringAssert.StartsWith(texto, "%PDF-1.4");
        StringAssert.EndsWith(texto, "%%EOF\n");

        File.Delete(ruta);
    }

    [TestMethod]
    public void SoloSalenLosArchivadosYTodosLosArchivados()
    {
        var reportes = Montar(out var servicios);

        var archivados = servicios.Casos
            .Listar(new FiltroDeCasos(IncluirArchivados: true), new Pagina(0, int.MaxValue))
            .Elementos.Where(c => c.Archivado).ToList();
        Assert.IsNotEmpty(archivados, "La base inventada tiene que traer casos archivados o esto no prueba nada.");

        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        Assert.HasCount(archivados.Count, tabla.Filas);
    }

    [TestMethod]
    public void CadaFilaDelHistoricoDiceArchivadoYSuFecha()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        var columnaDelEstado = tabla.Columnas.Select(c => c.Nombre).ToList().IndexOf("Archivado");
        Assert.IsGreaterThanOrEqualTo(0, columnaDelEstado, "La tabla del histórico tiene que traer la columna «Archivado».");

        foreach (var fila in tabla.Filas)
        {
            StringAssert.StartsWith(fila[columnaDelEstado], Vocabulario.Archivado,
                "«Lo que archivo debe verse... debe decir archivado» (dueño, 2026-09-03).");
        }
    }

    [TestMethod]
    public void ElUltimoArchivadoVaPrimero()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");
        var columnaDelEstado = tabla.Columnas.Select(c => c.Nombre).ToList().IndexOf("Archivado");

        // Se compara la FECHA y no el renglon entero: «archivado, sin fecha» ordena distinto que
        // «archivado el ...» como texto, y lo que se prueba es el orden por fecha.
        var fechas = tabla.Filas
            .Select(f => f[columnaDelEstado]!)
            .Select(t => t.StartsWith("archivado el ", StringComparison.Ordinal) ? t[13..] : string.Empty)
            .ToList();
        var ordenadas = fechas.OrderByDescending(f => f, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(ordenadas, fechas, "El histórico se lee del último archivado hacia atrás.");
    }

    [TestMethod]
    public void CadaFilaDiceCuantasPersonasLlevaYCuantasNoPudieronViajar()
    {
        var reportes = Montar(out var servicios);
        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        var personas = tabla.Columnas.Select(c => c.Nombre).ToList().IndexOf("Personas");
        var noViajaron = tabla.Columnas.Select(c => c.Nombre).ToList().IndexOf("No pudieron viajar");
        Assert.IsTrue(personas >= 0 && noViajaron >= 0);

        var totalDelHistorico = tabla.Filas.Sum(f => int.Parse(f[personas]!));
        var totalDeLosPuertos = servicios.Casos
            .Listar(new FiltroDeCasos(IncluirArchivados: true), new Pagina(0, int.MaxValue))
            .Elementos.Where(c => c.Archivado)
            .Sum(c => servicios.Personas.DeCaso(c.Id).Count);

        Assert.AreEqual(totalDeLosPuertos, totalDelHistorico);
    }

    [TestMethod]
    public void SinNingunArchivadoElHistoricoLoDiceEnVezDeSalirVacio()
    {
        var servicios = BaseDePrueba.Montar(300);
        foreach (var caso in servicios.Almacen.Casos.Values.Where(c => c.Archivado).ToList())
        {
            servicios.Almacen.Casos[caso.Id] = caso with { Archivado = false, FechaArchivado = null };
        }
        var reportes = new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);

        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        Assert.IsEmpty(tabla.Filas);
        StringAssert.Contains(tabla.Resumen, "Todavía no se ha archivado");
    }

    // ---- el reporte de un companero ----------------------------------------

    [TestMethod]
    public void ElReporteDeUnCompaneroSoloTraeSusCasos()
    {
        var reportes = Montar(out var servicios);
        var companero = servicios.Companeros.Activos()
            .First(c => servicios.Asignaciones
                .Listar(new FiltroDeAsignaciones(CompaneroId: c.Id), new Pagina(0, int.MaxValue))
                .Elementos.Count > 0);

        var documento = reportes.DocumentoDeCompanero(
            companero.Id, Periodo.Leer("2026-01-01", "2026-12-31").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

        StringAssert.Contains(documento.Subtitulo, companero.Nombre);
        var equipo = documento.Secciones.SingleOrDefault(s => s.Titulo == "El equipo");
        Assert.IsNotNull(equipo);
        foreach (var fila in equipo!.Filas)
        {
            Assert.AreEqual(companero.Nombre, fila[0]);
        }
    }

    [TestMethod]
    public void UnCompaneroQueNoExisteSeAvisa_NoLanza()
    {
        var reportes = Montar(out _);

        var resultado = reportes.GenerarReporteDeCompanero(
            999_999, "2026-09-01", "2026-09-30", RutaTemporal("nadie.pdf"));

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(GravedadDeAviso.Problema, resultado.Avisos[0].Gravedad);
    }
}
