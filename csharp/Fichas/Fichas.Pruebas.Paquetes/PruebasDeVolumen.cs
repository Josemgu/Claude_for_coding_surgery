using System.Diagnostics;
using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Un Excel de 3 000 filas: cuanto tarda en escribirse y cuanto en volver.
/// </summary>
/// <remarks>
/// La cifra sale del dueno: «imagina que tenga 3 000 formularios, me enviaron 300, no me puedo
/// poner a ver uno a uno cuál se completó y cuál no». Lo que se mide aqui es que el camino
/// entero —generar, contestar, leer, aplicar— aguanta ese tamano y que se aplica DE GOLPE.
/// <para>
/// ⚠️ Los topes son generosos a proposito. Esta prueba existe para cazar un derrumbe —minutos
/// donde deberian ser segundos, o una lectura que crece al cuadrado—, no para vigilar
/// milisegundos: un tope apretado se pone rojo en una maquina cargada y entonces se ignora, y
/// una prueba que se ignora no protege nada. La cifra REAL de cada ejecucion se imprime.
/// </para>
/// <para>
/// <b>Medido en esta maquina el 2026-09-04</b> (Windows 11, .NET SDK 10.0.400, ClosedXML
/// 0.105.1), 3 000 filas en 1 000 casos:
/// <c>generar 5,12 s · leer 2,31 s · aplicar 2,57 s · archivo 212 KiB</c>. Es una medicion de
/// una sola pasada y sin repeticiones, asi que sirve para descartar un derrumbe y NO para
/// comparar dos versiones entre si.
/// </para>
/// </remarks>
[TestClass]
public class PruebasDeVolumen
{
    private const int Casos = 1_000;
    private const int PersonasPorCaso = 3;
    private const int FilasEsperadas = Casos * PersonasPorCaso;

    /// <summary>Tope de escritura, en segundos. Ver la nota de la clase sobre por que es generoso.</summary>
    private const int TopeDeGenerar = 60;

    /// <summary>Tope de lectura, en segundos.</summary>
    private const int TopeDeLeer = 60;

    /// <summary>Tope de aplicacion, en segundos.</summary>
    private const int TopeDeAplicar = 60;

    [TestMethod]
    public void TresMilFilasSeGeneranSeLeenYSeAplicanEnUnTiempoQueSeMide()
    {
        var baseInventada = new BaseInventada();
        var sandy = baseInventada.Companero("Sandy");
        var casoIds = new List<long>(Casos);
        for (var numero = 0; numero < Casos; numero++)
        {
            // Numeros de caso distintos: lo que se mide aqui es el tamano, no la ambiguedad.
            var caso = baseInventada.Caso($"BA{(char)('A' + numero % 26)}{(char)('A' + numero / 26 % 26)}2609");
            casoIds.Add(caso);
            for (var fila = 1; fila <= PersonasPorCaso; fila++)
                baseInventada.Persona(caso, $"Persona {numero}-{fila}", $"055-{numero:0000}-{fila:0000}", fila);
        }

        var carpeta = BaseInventada.CarpetaDePruebas();
        var ruta = Path.Combine(carpeta, "por_verificar.xlsx");
        try
        {
            var cronometro = Stopwatch.StartNew();
            var generado = baseInventada.Paquetes.GenerarExcelDeCompanero(sandy, casoIds, ruta);
            var alGenerar = cronometro.Elapsed;
            Assert.IsTrue(generado.SeEscribio, string.Join(" | ", generado.Avisos.Select(a => a.Linea)));

            ContestarTodo(ruta);

            cronometro.Restart();
            var vuelta = baseInventada.Paquetes.LeerExcelDevuelto(ruta, sandy);
            var alLeer = cronometro.Elapsed;

            cronometro.Restart();
            var aplicado = baseInventada.Paquetes.AplicarMarcas(vuelta.Marcas, sandy, ruta);
            var alAplicar = cronometro.Elapsed;

            var peso = new FileInfo(ruta).Length / 1024d;
            Console.WriteLine(
                $"{FilasEsperadas} filas · generar {alGenerar.TotalSeconds:0.00} s · leer {alLeer.TotalSeconds:0.00} s · "
                + $"aplicar {alAplicar.TotalSeconds:0.00} s · archivo {peso:0} KiB");

            Assert.HasCount(FilasEsperadas, vuelta.Marcas);
            Assert.IsEmpty(vuelta.Descartadas, "con claves buenas no se descarta ninguna");
            Assert.IsTrue(aplicado.SeEscribio);
            Assert.HasCount(Casos, baseInventada.Almacen.Casos.Values.Where(c => c.EstadoRecomendacion == "completa").ToList(),
                "los 1 000 documentos se marcan de golpe: el dueño no puede mirarlos uno a uno");

            Assert.IsLessThan(TopeDeGenerar, alGenerar.TotalSeconds, $"generar tardó {alGenerar.TotalSeconds:0.00} s");
            Assert.IsLessThan(TopeDeLeer, alLeer.TotalSeconds, $"leer tardó {alLeer.TotalSeconds:0.00} s");
            Assert.IsLessThan(TopeDeAplicar, alAplicar.TotalSeconds, $"aplicar tardó {alAplicar.TotalSeconds:0.00} s");
        }
        finally
        {
            try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Rellena las siete columnas de las 3 000 filas de una pasada.</summary>
    private static void ContestarTodo(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        for (var fila = Columnas.PrimeraFilaDeDatos; fila < Columnas.PrimeraFilaDeDatos + FilasEsperadas; fila++)
            foreach (var columna in Columnas.Todas.Where(c => c.EsRespuesta))
                hoja.Cell(fila, Columnas.IndiceDe(columna.Nombre)).SetValue("Sí");
        libro.Save();
    }
}
