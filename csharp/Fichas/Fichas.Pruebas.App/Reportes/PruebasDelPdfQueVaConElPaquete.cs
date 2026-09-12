using Fichas.App.Paquetes;
using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// El PDF que sale JUNTO al Excel cuando se genera el paquete de un companero.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno: <i>«el Excel normal y también un PDF de todos, asignado, en un
/// solo PDF»</i>. Lo que se comprueba aqui es de la PANTALLA y no de la union: que se pide
/// el PDF con los MISMOS casos y en el MISMO orden que el Excel, que queda al lado con el
/// mismo nombre, que se dice cuanto ocupa habiendolo mirado, y —sobre todo— que un PDF que
/// no se puede hacer NO se lleva por delante el Excel. Como se juntan las hojas esta probado
/// en <c>Fichas.Pruebas.Paquetes/PruebasDelPdfDelPaquete.cs</c>, abriendo el archivo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelPdfQueVaConElPaquete
{
    /// <summary>La carpeta propia de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba; nunca la carpeta de datos del dueño.</summary>
    [TestInitialize]
    public void PrepararLaCarpeta()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-paquetes", Guid.NewGuid().ToString("N"));
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

    /// <summary>Un <see cref="IPaquetes"/> que apunta con que se le pidio el PDF y escribe unos bytes.</summary>
    private sealed class PaquetesQueTambienHacenElPdf : IPaquetes
    {
        /// <summary>Los bytes que escribe como PDF, o nulo para decir que no se pudo.</summary>
        private readonly byte[]? _contenidoDelPdf;

        /// <summary>Con qué contesta al PDF.</summary>
        /// <param name="contenidoDelPdf">Los bytes del PDF, o nulo para que el PDF no salga.</param>
        internal PaquetesQueTambienHacenElPdf(byte[]? contenidoDelPdf) => _contenidoDelPdf = contenidoDelPdf;

        /// <summary>Los casos con los que se le pidió el Excel.</summary>
        internal IReadOnlyList<long> CasosDelExcel { get; private set; } = [];

        /// <summary>Los casos con los que se le pidió el PDF; tienen que ser los mismos y en el mismo orden.</summary>
        internal IReadOnlyList<long> CasosDelPdf { get; private set; } = [];

        /// <summary>Dónde se le pidió el PDF; nulo si no se le pidió.</summary>
        internal string? RutaDelPdf { get; private set; }

        /// <inheritdoc />
        public ResultadoDeEscritura GenerarExcelDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
        {
            CasosDelExcel = casoIds;
            File.WriteAllBytes(rutaDestino, new byte[2048]);
            return ResultadoDeEscritura.Bien(companeroId);
        }

        /// <inheritdoc />
        public ResultadoDeEscritura GenerarPdfDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
        {
            CasosDelPdf = casoIds;
            RutaDelPdf = rutaDestino;
            if (_contenidoDelPdf is null)
            {
                return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                    "Ninguna hoja se pudo leer.", string.Empty, "Lo dice la prueba."));
            }

            File.WriteAllBytes(rutaDestino, _contenidoDelPdf);
            return ResultadoDeEscritura.BienCon(companeroId, Aviso.Informa(
                "PDF con 3 hoja(s).", string.Empty, "Lo dice la prueba."));
        }

        /// <inheritdoc />
        public ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId)
            => new([], [], []);

        /// <inheritdoc />
        public ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel)
            => ResultadoDeEscritura.NoSeEscribio();
    }

    /// <summary>Servicios inventados con 120 casos y el primer compañero activo, que es a quien va el paquete.</summary>
    private static (ServiciosFalsos Servicios, Companero Quien) Banco()
    {
        var servicios = new ServiciosFalsos(120, 17, new RelojFijo("2026-09-04"));
        return (servicios, servicios.Companeros.Activos()[0]);
    }

    /// <summary>Vigila que junto al Excel quede un PDF con el mismo nombre y la extensión cambiada.</summary>
    [TestMethod]
    public void JuntoAlExcelQuedaUnPdfConElMismoNombre()
    {
        var (servicios, quien) = Banco();
        var paquetes = new PaquetesQueTambienHacenElPdf(new byte[7000]);
        var operacion = new OperacionDelPaquete(paquetes, servicios.Asignaciones, servicios.Casos);
        var excel = Path.Combine(_carpeta, NombreDeArchivo.DelPaquete(quien.Nombre, "2026-09-04"));

        var resumen = operacion.Generar(quien, excel);

        var pdf = Path.ChangeExtension(excel, ".pdf");
        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        Assert.IsTrue(File.Exists(excel), "el Excel del paquete");
        Assert.IsTrue(File.Exists(pdf), "y el PDF de los documentos, al lado y con el mismo nombre");
        Assert.AreEqual(pdf, paquetes.RutaDelPdf);
    }

    /// <summary>Vigila que el PDF se pida con los mismos casos y en el mismo orden que el Excel.</summary>
    [TestMethod]
    public void ElPdfSePideConLosMismosCasosYEnElMismoOrdenQueElExcel()
    {
        var (servicios, quien) = Banco();
        var paquetes = new PaquetesQueTambienHacenElPdf(new byte[512]);
        var operacion = new OperacionDelPaquete(paquetes, servicios.Asignaciones, servicios.Casos);

        operacion.Generar(quien, Path.Combine(_carpeta, "p.xlsx"));

        Assert.IsGreaterThan(0, paquetes.CasosDelExcel.Count, "el compañero inventado no traía casos");
        CollectionAssert.AreEqual(
            paquetes.CasosDelExcel.ToList(),
            paquetes.CasosDelPdf.ToList(),
            "si las dos listas se separaran, el PDF tendría las hojas correctas en el orden equivocado");
    }

    /// <summary>Vigila que la línea diga que el paquete lleva también el PDF y cuánto ocupa, sin pasarse de largo.</summary>
    [TestMethod]
    public void LaLineaDiceQueElPaqueteLlevaTambienElPdfYCuantoOcupa()
    {
        var (servicios, quien) = Banco();
        var operacion = new OperacionDelPaquete(
            new PaquetesQueTambienHacenElPdf(new byte[7000]), servicios.Asignaciones, servicios.Casos);

        var resumen = operacion.Generar(quien, Path.Combine(_carpeta, "p.xlsx"));

        Assert.Contains("PDF", resumen.Linea, StringComparison.Ordinal);
        Assert.Contains("7000 bytes", resumen.Linea, StringComparison.Ordinal);
        Assert.IsLessThanOrEqualTo(190, resumen.Linea.Length, $"La línea mide {resumen.Linea.Length}: «{resumen.Linea}»");
    }

    /// <summary>Vigila que si el PDF no se puede hacer, el paquete salga igual con su Excel y se diga por qué.</summary>
    [TestMethod]
    public void SiElPdfNoSePuedeHacerElPaqueteSaleIgualConSuExcelYSeDicePorQue()
    {
        var (servicios, quien) = Banco();
        var operacion = new OperacionDelPaquete(
            new PaquetesQueTambienHacenElPdf(contenidoDelPdf: null), servicios.Asignaciones, servicios.Casos);
        var excel = Path.Combine(_carpeta, "p.xlsx");

        var resumen = operacion.Generar(quien, excel);

        Assert.IsTrue(resumen.SalioBien, "un PDF que no sale no puede tumbar el paquete: el Excel es lo que se manda");
        Assert.IsTrue(File.Exists(excel));
        Assert.AreEqual(excel, resumen.Ruta, "«abrir» sigue abriendo el Excel");
        Assert.IsTrue(
            resumen.Avisos.Any(aviso => aviso.Linea.Contains("Ninguna hoja se pudo leer", StringComparison.Ordinal)),
            "el motivo tiene que llegar a la franja de avisos");
        Assert.Contains("SIN el PDF de los documentos", resumen.Detalle, StringComparison.Ordinal);
    }

    /// <summary>Con los datos inventados NO aparece un PDF de mentira al lado del Excel.</summary>
    /// <remarks>
    /// El almacen de <c>--falso</c> no toca el disco: ni escribe el Excel ni implementa el PDF
    /// —cae en la implementacion por defecto del puerto, que dice que no escribio—. Lo que se
    /// comprueba aqui es que no queda NINGUN archivo: un .pdf de cero bytes en la carpeta del
    /// dueno seria un paquete que parece hecho y no lo esta.
    /// </remarks>
    [TestMethod]
    public void ConLosDatosInventadosNoQuedaNiElExcelNiElPdf()
    {
        var (servicios, quien) = Banco();
        var operacion = new OperacionDelPaquete(servicios.Paquetes, servicios.Asignaciones, servicios.Casos);

        var resumen = operacion.Generar(quien, Path.Combine(_carpeta, "inventado.xlsx"));

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsFalse(File.Exists(Path.Combine(_carpeta, "inventado.xlsx")));
        Assert.IsFalse(File.Exists(Path.Combine(_carpeta, "inventado.pdf")));
    }
}
