using System.Diagnostics;
using Fichas.Reportes;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Requisito 5 del dueno: rapido con 3 000 documentos, no con 2. La cifra se mide.
/// </summary>
/// <remarks>
/// El tope de esta prueba es DELIBERADAMENTE holgado —10 s— porque es una guardia contra
/// una regresion de orden de magnitud, no la medicion. La medicion de verdad es el numero
/// que esta prueba imprime en la salida, y es el que va en la entrega. Un tope apretado en
/// una maquina compartida convierte una prueba en un dado.
/// </remarks>
[TestClass]
public class PruebaDeRendimiento
{
    /// <summary>Los casos del requisito 5 del dueño: rápido con 3 000, no con 2.</summary>
    private const int TresMil = 3_000;
    /// <summary>El tope holgado que guarda contra una regresión de orden de magnitud; la medida real es la que se imprime.</summary>
    private const int TopeEnSegundos = 10;

    /// <summary>Vigila que el reporte del mes con 3 000 casos se escribe en menos de 10 s, e imprime el tiempo medido.</summary>
    [TestMethod]
    public void ConTresMilCasosElReporteDelMesSeGeneraYSeMideElTiempo()
    {
        var servicios = BaseDePrueba.Montar(TresMil);
        var reportes = new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);

        var ruta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-3000.pdf");

        var reloj = Stopwatch.StartNew();
        var resultado = reportes.GenerarReporteDelPeriodo("2026-09-01", "2026-09-30", ruta);
        reloj.Stop();

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));

        var tamano = new FileInfo(ruta).Length;
        Console.WriteLine(
            $"MEDIDO · reporte del mes con {TresMil} casos y {servicios.Almacen.Personas.Count} personas: "
            + $"{reloj.Elapsed.TotalSeconds:F3} s · PDF de {tamano / 1024} KiB");

        Assert.IsLessThan(TopeEnSegundos, reloj.Elapsed.TotalSeconds,
            $"Tardo {reloj.Elapsed.TotalSeconds:F3} s con {TresMil} casos.");

        File.Delete(ruta);
    }

    /// <summary>Vigila que el histórico con 3 000 casos se escribe en menos de 10 s, e imprime el tiempo medido.</summary>
    [TestMethod]
    public void ConTresMilCasosElHistoricoSeGeneraYSeMideElTiempo()
    {
        var servicios = BaseDePrueba.Montar(TresMil);
        var reportes = new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);

        var ruta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-hist.pdf");

        var reloj = Stopwatch.StartNew();
        var resultado = reportes.GenerarHistorico(ruta);
        reloj.Stop();

        Assert.IsTrue(resultado.SeEscribio);
        Console.WriteLine(
            $"MEDIDO · histórico con {servicios.Almacen.Casos.Values.Count(c => c.Archivado)} archivados: "
            + $"{reloj.Elapsed.TotalSeconds:F3} s · PDF de {new FileInfo(ruta).Length / 1024} KiB");

        Assert.IsLessThan(TopeEnSegundos, reloj.Elapsed.TotalSeconds);
        File.Delete(ruta);
    }

    /// <summary>Imprime cuánto tarda leer, armar y escribir por separado; solo exige que salgan bytes.</summary>
    [TestMethod]
    public void ConTresMilCasosSeMideEnQueSeVaElTiempo()
    {
        // El desglose importa mas que el total. Hasta el 2026-09-15 la lectura hacia una
        // llamada POR CASO y otra POR PERSONA (10 531 con esta base, 0,599 s); desde R-7 son
        // tres consultas en bloque (0,114 s medidos el mismo dia). Cuantas salen lo cuenta
        // PruebaDeQueLaLecturaNoVaFilaAFila; aqui se mide el reloj.
        var servicios = BaseDePrueba.Montar(TresMil);

        var reloj = Stopwatch.StartNew();
        var lectura = Fichas.Reportes.Consultas.LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia);
        var leer = reloj.Elapsed.TotalSeconds;

        reloj.Restart();
        var documento = Fichas.Reportes.Armado.ArmadoDelDocumento.DelPeriodo(
            lectura,
            Fichas.Reportes.Reglas.Periodo.Leer("2026-09-01", "2026-09-30").Periodo!,
            $"{BaseDePrueba.Hoy} 10:00:00");
        var armar = reloj.Elapsed.TotalSeconds;

        reloj.Restart();
        var bytes = Fichas.Reportes.Formato.Maqueta.ConstruirPdf(documento);
        var escribir = reloj.Elapsed.TotalSeconds;

        Console.WriteLine(
            $"MEDIDO · desglose con {TresMil} casos ({servicios.Almacen.Personas.Count} personas, "
            + $"{servicios.Almacen.Procedencias.Count} filas de procedencia): "
            + $"leer de los puertos {leer:F3} s · armar el documento {armar:F3} s · "
            + $"construir el PDF {escribir:F3} s · {bytes.Length / 1024} KiB");

        Assert.IsGreaterThan(0, bytes.Length);
    }

    /// <summary>Vigila que cuadruplicar las filas no multiplica el reparto por más de ocho; con la máquina cargada cae una de cada cinco.</summary>
    [TestMethod]
    public void ElRepartoEnPaginasNoEsCuadraticoConElNumeroDeFilas()
    {
        // La rejilla de tarjetas del Python fue n² una vez (DECISIONES.md, 2026-09-03) y
        // costo una jornada encontrarlo. Aqui se mide la forma, no el reloj: al doblar las
        // filas el tiempo no puede multiplicarse por mas de 4.
        var pequeno = MedirElReparto(2_000);
        var grande = MedirElReparto(8_000);

        Console.WriteLine($"MEDIDO · reparto en páginas: 2 000 filas {pequeno:F1} ms · 8 000 filas {grande:F1} ms");

        // Con el mismo tope holgado: cuatro veces las filas, como mucho ocho veces el tiempo.
        Assert.IsLessThan(Math.Max(50, pequeno * 8), grande,
            $"2 000 filas: {pequeno:F1} ms; 8 000 filas: {grande:F1} ms. Huele a n².");
    }

    /// <summary>Cuántos milisegundos tarda solo el reparto en páginas de una tabla de esas filas; las líneas se hacen fuera del cronómetro.</summary>
    /// <param name="filas">Cuántas filas de una columna lleva la tabla.</param>
    private static double MedirElReparto(int filas)
    {
        var seccion = new Fichas.Reportes.Modelo.Seccion(
            "Los viajes",
            [],
            [new Fichas.Reportes.Modelo.Columna("Caso", Fichas.Reportes.Modelo.ClaseDeColumna.Texto, 12)],
            Enumerable.Range(0, filas).Select(i => (IReadOnlyList<string?>)new string?[] { $"CASP{i:D6}" }).ToList(),
            null);

        var documento = new Fichas.Reportes.Modelo.Documento(
            "T", "S", "2026-09-20 10:00:00",
            new Fichas.Reportes.Modelo.Portada("Titular", "Frase", []), [], [seccion]);

        var lineas = Fichas.Reportes.Formato.Maqueta.LineasDelDocumento(documento, []);
        var reloj = Stopwatch.StartNew();
        Fichas.Reportes.Formato.Maqueta.RepartirEnPaginas(lineas);
        reloj.Stop();
        return reloj.Elapsed.TotalMilliseconds;
    }
}
