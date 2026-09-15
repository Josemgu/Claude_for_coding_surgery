using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.App.Importar;
using Fichas.Datos;
using Fichas.Datos.Repositorios;
using Fichas.Lectura;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Cuánto tarda en guardarse una hoja de verdad con y sin el ámbito de R-4, sobre el corpus
/// del dueño leído con el OCR de verdad.
/// </summary>
/// <remarks>
/// <para>Es la cifra que el pase del 2026-09-15 pedía: «ms de guardado por hoja antes/después,
/// SQLite real, las hojas del corpus». El OCR se paga UNA vez —es lo lento, 9 a 13 s por hoja
/// medidos— y las hojas leídas se guardan después en bases nuevas, cronometrando solo el
/// guardado, cinco pasadas alternadas sin y con ámbito para que el orden no pese.</para>
///
/// <para>⚠️ <b>Son datos personales.</b> El corpus no está en el repositorio, se lee desde donde
/// está y no se copia; no se imprime ningún nombre de archivo ni de persona, solo cifras. Sin
/// el corpus o sin los modelos la prueba se declara NO CONCLUYENTE, no falla.</para>
///
/// <para>No afirma un umbral de milisegundos: lo que mide depende del disco. Lo que sí afirma
/// es lo que no depende de él: con ámbito, el archivo recibe UNA confirmación por hoja.</para>
/// </remarks>
[TestClass]
public sealed class MedicionDelGuardadoEnUnaTransaccion
{
    /// <summary>Dónde está el corpus en esta máquina; si no está, «no tengo el material».</summary>
    private const string CarpetaDelCorpus = @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>Cuántas veces se guarda todo el corpus con cada variante.</summary>
    private const int Pasadas = 5;

    /// <summary>Lee el corpus una vez y cronometra el guardado sin y con ámbito.</summary>
    [TestMethod]
    public void ElGuardadoDelCorpusConYSinAmbito()
    {
        var pdfs = Directory.Exists(CarpetaDelCorpus)
            ? Directory.GetFiles(CarpetaDelCorpus, "*.pdf").Order(StringComparer.Ordinal).ToArray()
            : [];
        if (pdfs.Length == 0) Assert.Inconclusive($"No hay PDF en «{CarpetaDelCorpus}»: son datos del dueño y no viven en el repositorio.");

        var modelos = CarpetaDeLosModelos();
        if (modelos is null) Assert.Inconclusive("No están los modelos de OCR en la salida de Fichas.App. Compile la solución entera antes de medir.");

        var hojasPorDocumento = LeerElCorpus(pdfs, modelos);
        var hojas = hojasPorDocumento.Sum(documento => documento.Count);
        Console.WriteLine($"CORPUS · {pdfs.Length} documentos · {hojas} hojas · {hojasPorDocumento.Sum(d => d.Sum(h => h.Campos.Count))} campos leídos");

        var sinAmbito = new List<double>();
        var conAmbito = new List<double>();
        uint confirmacionesSin = 0, confirmacionesCon = 0;
        for (var pasada = 0; pasada < Pasadas; pasada++)
        {
            (var ms, confirmacionesSin) = Guardar(hojasPorDocumento, conexion => () => new SinAmbitoDeGuardado());
            sinAmbito.Add(ms);
            (ms, confirmacionesCon) = Guardar(hojasPorDocumento, conexion => () => new AmbitoDeGuardadoSobreSqlite(conexion));
            conAmbito.Add(ms);
        }

        Console.WriteLine($"SIN ÁMBITO · confirmaciones por tanda: {confirmacionesSin} · ms por tanda: [{Lista(sinAmbito)}] · mediana {Mediana(sinAmbito):0} ms · {Mediana(sinAmbito) / hojas:0.0} ms/hoja");
        Console.WriteLine($"CON ÁMBITO · confirmaciones por tanda: {confirmacionesCon} · ms por tanda: [{Lista(conAmbito)}] · mediana {Mediana(conAmbito):0} ms · {Mediana(conAmbito) / hojas:0.0} ms/hoja");

        Assert.AreEqual((uint)hojas, confirmacionesCon, "con ámbito, una confirmación por hoja: ni más ni menos.");
        Assert.IsGreaterThan(confirmacionesCon, confirmacionesSin, "control positivo: sin ámbito son más.");
    }

    /// <summary>Lee todos los PDF del corpus con el lector de verdad, uno tras otro.</summary>
    /// <remarks>
    /// Las hojas vuelven con una ruta inventada en vez de la real: así el guardado no copia el
    /// escaneo a la carpeta temporal (la copia es de archivos que existen) y se cronometra
    /// solo lo que va a la base, que es lo que R-4 cambia. Y ningún nombre real sale de aquí.
    /// </remarks>
    /// <param name="pdfs">Las rutas, en orden fijo.</param>
    /// <param name="modelos">La carpeta de los modelos de OCR.</param>
    private static List<IReadOnlyList<HojaLeida>> LeerElCorpus(string[] pdfs, string modelos)
    {
        using var lectura = new LecturaDePdf(modelos);
        var lector = new LectorDeFormularios(lectura);
        var crono = Stopwatch.StartNew();
        var leidas = pdfs
            .Select((pdf, indice) => (IReadOnlyList<HojaLeida>)lector.LeerDocumento(pdf)
                .Select(hoja => hoja with { RutaPdf = $"C:/corpus/documento-{indice + 1}.pdf" })
                .ToList())
            .ToList();
        Console.WriteLine($"OCR · {crono.Elapsed.TotalSeconds:0} s en total (no cuenta en la medición del guardado)");
        return leidas;
    }

    /// <summary>Guarda el corpus entero en una base nueva y devuelve los ms y las confirmaciones que costó.</summary>
    /// <param name="hojasPorDocumento">Las hojas leídas, por documento.</param>
    /// <param name="abrirAmbito">Cómo abrir el ámbito sobre la conexión de esa base.</param>
    private static (double Ms, uint Confirmaciones) Guardar(
        List<IReadOnlyList<HojaLeida>> hojasPorDocumento, Func<SqliteConnection, Func<IAmbitoDeGuardado>> abrirAmbito)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-medicion-r4", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, Fichas.Datos.Rutas.CarpetaDeDatos.NombreDeLaBase);
        try
        {
            using var conexion = ArranqueDeLaBase.PrepararLaBase(carpeta, _ => { });
            var guardado = new GuardadoDeHojas(
                new RepositorioDeCasos(conexion), new RepositorioDePersonas(conexion), new RepositorioDeProcedencia(conexion),
                new RepositorioDeIlegibles(conexion), new RelojDelSistema(), new CopiaDelEscaneo(Path.Combine(carpeta, "datos")),
                abrirAmbito(conexion));
            guardado.EmpezarUnaTanda();

            var antes = ContadorDeCambiosDelArchivo(ruta);
            var crono = Stopwatch.StartNew();
            foreach (var hojas in hojasPorDocumento) guardado.GuardarLasHojasDelDocumento(hojas);
            crono.Stop();
            return (crono.Elapsed.TotalMilliseconds, ContadorDeCambiosDelArchivo(ruta) - antes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>El contador de cambios de la cabecera del archivo: cuántas transacciones lo han cambiado.</summary>
    /// <param name="ruta">El archivo <c>.db</c>.</param>
    private static uint ContadorDeCambiosDelArchivo(string ruta)
    {
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var cabecera = new byte[28];
        flujo.ReadExactly(cabecera);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cabecera.AsSpan(24, 4));
    }

    /// <summary>La mediana de una lista de tiempos.</summary>
    /// <param name="valores">Los tiempos, en cualquier orden.</param>
    private static double Mediana(List<double> valores)
    {
        var ordenados = valores.Order().ToArray();
        return ordenados.Length % 2 == 1
            ? ordenados[ordenados.Length / 2]
            : (ordenados[ordenados.Length / 2 - 1] + ordenados[ordenados.Length / 2]) / 2;
    }

    /// <summary>Los tiempos separados por comas, sin decimales.</summary>
    /// <param name="valores">Los tiempos.</param>
    private static string Lista(List<double> valores)
        => string.Join(", ", valores.Select(v => v.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Los modelos de OCR, prestados de la salida de <c>Fichas.App</c>; ver <c>MedicionDelDocumentoDeSeisHojas</c>.</summary>
    private static string? CarpetaDeLosModelos()
    {
        var deLaApp = AppContext.BaseDirectory.Replace("Fichas.Pruebas.App", "Fichas.App", StringComparison.Ordinal);
        var carpeta = Path.Combine(deLaApp, "models", "v5");
        return Directory.Exists(carpeta) ? carpeta : null;
    }
}
