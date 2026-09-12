
using Fichas.App.Paquetes;
using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// La ida: a quien se le genera el paquete, cuanto lleva, y donde queda el Excel.
/// </summary>
/// <remarks>
/// El pase lo pide asi: «se elige a quién, se ve cuántos casos y personas lleva, se genera el
/// Excel y se dice dónde quedó». Las tres cosas se comprueban aqui sin abrir ventana.
/// </remarks>
[TestClass]
public sealed class PruebasDelPaqueteDeIda
{
    /// <summary>La carpeta propia de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba; nunca la carpeta de datos del dueno.</summary>
    [TestInitialize]
    public void PrepararLaCarpeta()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-paquetes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Se lleva la carpeta al terminar.</summary>
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

    /// <summary>
    /// La carga de un companero reparte sus casos vivos entre los que van y los que ya hizo.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta prueba cambio el 2026-09-07 porque afirmaba lo contrario de lo que pidio el
    /// dueno.</b> Hasta hoy exigia que la carga fuese IGUAL a todas sus asignaciones vivas, que
    /// es exactamente el defecto que el describio: <i>«te carga todas las fechas pasadas que ya
    /// completó y las fechas nuevas en un solo paquete. Es trabajar dos veces»</i>. Lo que se
    /// mide ahora es que no se pierde ninguna: las que van MAS las que el ya devolvio completas
    /// vuelven a ser todas sus asignaciones vivas, sin que sobre ni falte una.
    /// </remarks>
    [TestMethod]
    public void LaCargaRepartLosCasosVivosEntreLosQueVanYLosQueYaHizo()
    {
        var servicios = new ServiciosFalsos(200, 13, new RelojFijo("2026-09-04"));
        var quien = servicios.Companeros.Activos()[0];

        var carga = CargaDeUnCompanero.Leer(servicios.Asignaciones, servicios.Casos, quien.Id);

        var vivas = servicios.Asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: quien.Id, SoloActivas: true), Pagina.Primera(int.MaxValue));
        var todosLosSuyos = vivas.Elementos.Select(a => a.CasoId).Distinct().ToList();

        Assert.AreEqual(todosLosSuyos.Count, carga.CasosQueLlevaEnTotal, "Se perdió algún caso por el camino.");
        CollectionAssert.AreEquivalent(
            todosLosSuyos,
            carga.CasoIds.Concat(carga.YaLosDevolvioCompletos).ToList());
        Assert.IsGreaterThan(0, carga.CasoIds.Count, "El compañero inventado no traía ningún caso pendiente.");

        var esperadas = servicios.Casos.ContarPersonasDe(carga.CasoIds).Values.Sum();
        Assert.AreEqual(esperadas, carga.Personas);
    }

    /// <summary>La linea corta de la carga dice las dos cifras y concuerda en singular.</summary>
    [TestMethod]
    public void LaLineaDeLaCargaDiceLasDosCifras()
    {
        Assert.AreEqual(
            "12 casos · 38 personas",
            new CargaDelCompanero(1, [.. Enumerable.Range(1, 12).Select(n => (long)n)], 38, [], []).Linea);
        Assert.AreEqual("1 caso · 1 persona", new CargaDelCompanero(1, [7L], 1, [], []).Linea);
        Assert.AreEqual("ningún caso asignado", new CargaDelCompanero(1, [], 0, [], []).Linea);
    }

    /// <summary>
    /// Y quien no lleva nada pendiente PORQUE ya lo devolvio todo no se dice igual.
    /// </summary>
    /// <remarks>
    /// «No tiene ningún caso asignado» manda al dueno a Asignar a arreglar algo que no esta
    /// roto. Son dos situaciones distintas y se dicen distinto.
    /// </remarks>
    [TestMethod]
    public void NoEsLoMismoNoLlevarNadaQueHaberloDevueltoTodo()
    {
        Assert.AreEqual(
            "nada pendiente · 3 casos ya devueltos completos",
            new CargaDelCompanero(1, [], 0, [1L, 2L, 3L], []).Linea);
        Assert.AreEqual(
            "2 casos · 5 personas · 3 ya devueltos completos que no vuelven a ir · 1 volvió sin completar y sigue dentro",
            new CargaDelCompanero(1, [7L, 8L], 5, [1L, 2L, 3L], [7L]).LineaConLoQueNoVa);
    }

    /// <summary>Un companero sin casos NO genera un Excel vacio: se dice y no se escribe nada.</summary>
    /// <remarks>
    /// Un archivo con cabeceras y sin filas se envia igual, el companero lo abre y no sabe
    /// que mirar. Mejor decirlo aqui, que es donde todavia se puede asignar.
    /// </remarks>
    [TestMethod]
    public void UnCompaneroSinCasosNoGeneraUnExcelVacio()
    {
        var servicios = new ServiciosFalsos(0, 3, new RelojFijo("2026-09-04"));
        servicios.Almacen.Companeros[1] = new Companero { Id = 1, Nombre = "Sandy", Activo = true };
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, new byte[10]);
        var operacion = new OperacionDelPaquete(paquetes, servicios.Asignaciones, servicios.Casos);
        var ruta = Path.Combine(_carpeta, "vacio.xlsx");

        var resumen = operacion.Generar(servicios.Companeros.Obtener(1)!, ruta);

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsFalse(File.Exists(ruta), "Se escribió un Excel para un compañero sin ningún caso.");
        Assert.Contains("Sandy", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>El Excel queda escrito, y la linea dice el nombre del archivo y cuanto ocupa.</summary>
    [TestMethod]
    public void ElExcelQuedaEscritoYSeDiceDondeYCuantoOcupa()
    {
        var servicios = new ServiciosFalsos(120, 17, new RelojFijo("2026-09-04"));
        var quien = servicios.Companeros.Activos()[0];
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, new byte[4096]);
        var operacion = new OperacionDelPaquete(paquetes, servicios.Asignaciones, servicios.Casos);
        var ruta = Path.Combine(_carpeta, NombreDeArchivo.DelPaquete(quien.Nombre, "2026-09-04"));

        var resumen = operacion.Generar(quien, ruta);

        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        Assert.IsTrue(File.Exists(ruta));
        Assert.AreEqual(ruta, resumen.Ruta);
        Assert.Contains("4096 bytes", resumen.Linea, StringComparison.Ordinal);
        Assert.IsLessThanOrEqualTo(160, resumen.Linea.Length, $"La línea mide {resumen.Linea.Length}.");
    }

    /// <summary>Los casos que van al Excel son EXACTAMENTE los que la pantalla enseño.</summary>
    /// <remarks>
    /// Si la cuenta de la pantalla y la lista que se manda pudieran separarse, el dueno veria
    /// «12 casos» y el companero recibiria otros; aqui se comprueba que salen de la misma lectura.
    /// </remarks>
    [TestMethod]
    public void LosCasosQueVanAlExcelSonLosQueLaPantallaEnseno()
    {
        var servicios = new ServiciosFalsos(120, 17, new RelojFijo("2026-09-04"));
        var quien = servicios.Companeros.Activos()[0];
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, new byte[8]);
        var operacion = new OperacionDelPaquete(paquetes, servicios.Asignaciones, servicios.Casos);

        var carga = operacion.Carga(quien.Id);
        operacion.Generar(quien, Path.Combine(_carpeta, "p.xlsx"));

        CollectionAssert.AreEqual(carga.CasoIds.ToList(), paquetes.UltimosCasos.ToList());
    }

    /// <summary>Con los paquetes inventados no se miente: se dice que no se escribió nada.</summary>
    [TestMethod]
    public void ConLosPaquetesInventadosSeDiceQueNoSeEscribioNada()
    {
        var servicios = new ServiciosFalsos(120, 17, new RelojFijo("2026-09-04"));
        var quien = servicios.Companeros.Activos()[0];
        var operacion = new OperacionDelPaquete(
            servicios.Paquetes, servicios.Asignaciones, servicios.Casos);

        var resumen = operacion.Generar(quien, Path.Combine(_carpeta, "inventado.xlsx"));

        Assert.IsFalse(resumen.SalioBien);
        Assert.Contains("no está", resumen.Linea, StringComparison.Ordinal);
    }
}
