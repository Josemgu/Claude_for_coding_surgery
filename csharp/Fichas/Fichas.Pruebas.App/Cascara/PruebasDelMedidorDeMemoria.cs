using System.Globalization;
using System.Text.RegularExpressions;
using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El medidor permanente de memoria en <c>fichas.log</c> (R-0 del plan del 2026-09-15).
/// </summary>
/// <remarks>
/// <para>Criterio: <b>dado</b> el programa en uno de sus cuatro momentos —ventana lista, fin
/// de tanda, abrir un documento en Corrección, cierre—, <b>cuando</b> se anota en el
/// cuaderno, <b>entonces</b> queda una línea <c>MEMORIA</c> con el working set, lo
/// administrado por el GC, lo comprometido por el GC y los bytes privados del proceso; y
/// en esa línea hay <b>solo cifras</b>: ni la clave, ni rutas, ni datos del dueño.</para>
///
/// <para>Se prueba lo que se puede sin ventana: la lectura, la línea, y que el cuaderno
/// escribe la de «ventana lista» junto al arranque y la de «fin de tanda» cuando le llega
/// la línea con la que Importar cierra una tanda. Las de abrir un documento y cierre se
/// llaman desde la pantalla y desde los servicios, y se comprueban en el <c>fichas.log</c>
/// de una sesión de verdad (entrega).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelMedidorDeMemoria
{
    /// <summary>Una carpeta temporal por prueba, para que el cuaderno no pise el de nadie.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Abre la carpeta temporal.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-memoria-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Borra la carpeta temporal.</summary>
    [TestCleanup]
    public void Recoger()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    /// <summary>Dado un proceso vivo, cuando se lee, entonces las cuatro cifras son mayores que cero.</summary>
    [TestMethod]
    public void LasCuatroCifrasSonMayoresQueCero()
    {
        var lectura = MedidorDeMemoria.Leer();

        Assert.IsGreaterThan(0, lectura.WorkingSet, "working set");
        Assert.IsGreaterThan(0, lectura.Administrada, "administrada por el GC");
        Assert.IsGreaterThan(0, lectura.Comprometida, "comprometida por el GC");
        Assert.IsGreaterThan(0, lectura.Privados, "privados");
    }

    /// <summary>Dado un momento, cuando se compone la línea, entonces lleva la marca, el momento y las cuatro cifras en MiB, y nada más.</summary>
    [TestMethod]
    public void LaLineaLlevaLaMarcaElMomentoYLasCuatroCifrasEnMiB()
    {
        var lectura = new LecturaDeMemoria(
            WorkingSet: 300L * 1024 * 1024,
            Administrada: 40L * 1024 * 1024 + 512 * 1024,
            Comprometida: 96L * 1024 * 1024,
            Privados: 280L * 1024 * 1024);

        var linea = MedidorDeMemoria.Linea("ventana lista", lectura);

        Assert.AreEqual(
            "MEMORIA  ventana lista  working set 300.0 MiB  administrada 40.5 MiB  comprometida 96.0 MiB  privados 280.0 MiB",
            linea);
    }

    /// <summary>Dado cualquier lectura, cuando se compone la línea, entonces no lleva rutas, ni arrobas, ni nada que no sea un rótulo o una cifra.</summary>
    [TestMethod]
    public void LaLineaSoloLlevaRotulosYCifras()
    {
        var linea = MedidorDeMemoria.Linea("cierre", MedidorDeMemoria.Leer());

        Assert.IsFalse(linea.Contains(":\\", StringComparison.Ordinal), "sin rutas");
        Assert.DoesNotContain('@', linea, "sin correos ni claves");
        Assert.IsTrue(
            Regex.IsMatch(linea, @"^MEMORIA  cierre  working set \d+\.\d MiB  administrada \d+\.\d MiB  comprometida \d+\.\d MiB  privados \d+\.\d MiB$"),
            linea);
    }

    /// <summary>Dada la línea con la que Importar cierra una tanda, cuando se pregunta, entonces es fin de tanda; cualquier otra no.</summary>
    [TestMethod]
    [DataRow("IMPORTACIÓN  Importados 16 de 16 documentos. · 16 casos · 20 personas · 0 duplicados avisados · 0 ilegibles", true)]
    [DataRow("IMPORTACIÓN  Importación detenida en 3 de 16; lo procesado quedó guardado. · 3 casos", true)]
    [DataRow("IMPORTACIÓN  se abre el selector", false)]
    [DataRow("MEMORIA  fin de tanda  working set 1.0 MiB", false)]
    [DataRow("CIERRE  se cierra la base y se sale.", false)]
    public void ReconoceLaLineaConLaQueImportarCierraLaTanda(string linea, bool esFinDeTanda)
        => Assert.AreEqual(esFinDeTanda, MedidorDeMemoria.EsFinDeTanda(linea));

    /// <summary>Dado el arranque, cuando se anota, entonces detrás de la línea ARRANQUE va la MEMORIA de «ventana lista».</summary>
    [TestMethod]
    public void AlAnotarElArranqueSigueLaMemoriaDeVentanaLista()
    {
        var registro = new Registro(_carpeta);

        registro.AnotarArranque(900, null, _carpeta);

        var lineas = LeerElCuaderno(registro);
        Assert.HasCount(2, lineas, string.Join(Environment.NewLine, lineas));
        StringAssert.Contains(lineas[0], "ARRANQUE  ventana lista en 900 ms", StringComparison.Ordinal);
        StringAssert.Contains(lineas[1], "MEMORIA  ventana lista  working set ", StringComparison.Ordinal);
    }

    /// <summary>Dada la línea de fin de tanda de Importar, cuando se anota, entonces detrás va la MEMORIA de «fin de tanda»; una línea cualquiera no la trae.</summary>
    [TestMethod]
    public void AlAnotarElFinDeTandaSigueLaMemoriaDeFinDeTanda()
    {
        var registro = new Registro(_carpeta);

        registro.Anotar("IMPORTACIÓN  se abre el selector");
        registro.Anotar("IMPORTACIÓN  Importados 2 de 2 documentos. · 2 casos · 3 personas · 0 duplicados avisados · 0 ilegibles");

        var lineas = LeerElCuaderno(registro);
        Assert.HasCount(3, lineas, string.Join(Environment.NewLine, lineas));
        StringAssert.Contains(lineas[2], "MEMORIA  fin de tanda  working set ", StringComparison.Ordinal);
    }

    /// <summary>Dado un momento cualquiera, cuando se pide la memoria, entonces queda una sola línea MEMORIA con ese momento.</summary>
    [TestMethod]
    public void AnotarMemoriaDejaUnaLineaConSuMomento()
    {
        var registro = new Registro(_carpeta);

        registro.AnotarMemoria("Corrección abre el caso 7");

        var lineas = LeerElCuaderno(registro);
        Assert.HasCount(1, lineas);
        StringAssert.Contains(lineas[0], "MEMORIA  Corrección abre el caso 7  working set ", StringComparison.Ordinal);
    }

    /// <summary>Las líneas del cuaderno, sin la marca de tiempo delante.</summary>
    /// <param name="registro">El cuaderno recién escrito.</param>
    private static string[] LeerElCuaderno(Registro registro)
    {
        Assert.IsNotNull(registro.Ruta);
        return File.ReadAllLines(registro.Ruta!)
            .Select(linea => linea.Length > 25 ? linea[25..] : linea)
            .ToArray();
    }

    /// <summary>Que la cultura no cambie el punto decimal de las cifras: la línea se escribe invariante.</summary>
    [TestMethod]
    public void LaLineaSeEscribeConPuntoDecimalSeaCualSeaLaCultura()
    {
        var deAntes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
            var linea = MedidorDeMemoria.Linea("cierre", new LecturaDeMemoria(1024 * 1024 + 512 * 1024, 1, 1, 1));
            StringAssert.Contains(linea, "working set 1.5 MiB", StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = deAntes;
        }
    }
}
