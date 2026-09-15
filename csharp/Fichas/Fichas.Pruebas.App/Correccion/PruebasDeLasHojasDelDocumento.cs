using Fichas.App.Correccion;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// La memoria corta del visor: las últimas hojas rasterizadas del documento abierto y cuántas
/// tiene (R-1 del plan del 2026-09-15, puntos b y c).
/// </summary>
/// <remarks>
/// <para>Criterio, con las palabras del plan: <b>dado</b> un documento abierto en Corrección,
/// <b>cuando</b> se vuelve a una hoja ya vista, <b>entonces</b> hay <b>0 rasterizados
/// nuevos</b>; <b>cuando</b> se piden varias hojas del mismo documento, <b>entonces</b>
/// <c>ContarPaginas</c> se llama <b>una vez por documento</b> y no por hoja; se guardan las
/// <b>últimas 3</b> hojas, y la memoria <b>se vacía al cambiar de caso</b>.</para>
///
/// <para>Se cuenta con un doble de la lectura y no con un cronómetro, por lo mismo que
/// <c>ProcedenciaQueSeCuenta</c>: una cifra de llamadas es la misma en cualquier máquina.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasHojasDelDocumento
{
    /// <summary>Una ruta cualquiera; la lectura falsa no la abre.</summary>
    private const string Ruta = "un-escaneo.pdf";

    /// <summary>Otra ruta, para el cambio de caso.</summary>
    private const string OtraRuta = "otro-escaneo.pdf";

    /// <summary>Hace lo que hace la pantalla: mira la memoria corta, y si no está, trae y guarda.</summary>
    /// <param name="hojas">La memoria corta del documento.</param>
    /// <param name="hoja">Qué hoja se pide.</param>
    /// <returns>La imagen, de la memoria o recién traída.</returns>
    private static ImagenDePagina? PedirComoLaPantalla(HojasDelDocumento hojas, int hoja)
    {
        if (hojas.YaRasterizada(hoja) is { } deLaMemoria) return deLaMemoria;
        var traida = hojas.Traer(Ruta, hoja, hojas.TotalDeHojas);
        hojas.Guardar(Ruta, traida);
        return traida.Imagen;
    }

    /// <summary>Dado un documento recién abierto, cuando se pide una hoja, entonces se rasteriza y se cuenta una vez.</summary>
    [TestMethod]
    public void LaPrimeraHojaSeRasterizaYSeCuenta()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);

        var imagen = PedirComoLaPantalla(hojas, 1);

        Assert.IsNotNull(imagen);
        Assert.AreEqual(1, lectura.Rasterizados);
        Assert.AreEqual(1, lectura.Conteos);
        Assert.AreEqual(6, hojas.TotalDeHojas);
    }

    /// <summary>Dado que una hoja ya se vio, cuando se vuelve a ella, entonces hay 0 rasterizados nuevos.</summary>
    [TestMethod]
    public void VolverAUnaHojaYaVistaNoRasterizaNada()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);
        var primera = PedirComoLaPantalla(hojas, 1);
        PedirComoLaPantalla(hojas, 2);

        var otraVez = PedirComoLaPantalla(hojas, 1);

        Assert.AreSame(primera, otraVez, "es la misma imagen, no una nueva");
        Assert.AreEqual(2, lectura.Rasterizados, "0 rasterizados nuevos al volver");
        Assert.AreEqual(1, hojas.ServidasDeLaMemoria);
    }

    /// <summary>Dado un documento, cuando se piden cuatro hojas distintas, entonces se contó una sola vez.</summary>
    [TestMethod]
    public void ContarPaginasSeLlamaUnaVezPorDocumento()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);

        foreach (var hoja in new[] { 1, 2, 3, 4 }) PedirComoLaPantalla(hojas, hoja);

        Assert.AreEqual(4, lectura.Rasterizados);
        Assert.AreEqual(1, lectura.Conteos, "una vez por documento, no por hoja");
    }

    /// <summary>Dadas tres hojas guardadas, cuando entra la cuarta, entonces sale la que lleva más tiempo sin usarse.</summary>
    [TestMethod]
    public void SeGuardanLasUltimasTresYSaleLaMenosReciente()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);
        foreach (var hoja in new[] { 1, 2, 3 }) PedirComoLaPantalla(hojas, hoja);

        // Se vuelve a la 1: ahora la menos reciente es la 2.
        PedirComoLaPantalla(hojas, 1);
        PedirComoLaPantalla(hojas, 4);

        Assert.AreEqual(3, hojas.Guardadas);
        Assert.IsNull(hojas.YaRasterizada(2), "la 2 era la menos reciente y salió");
        Assert.IsNotNull(hojas.YaRasterizada(1));
        Assert.IsNotNull(hojas.YaRasterizada(3));
        Assert.IsNotNull(hojas.YaRasterizada(4));
    }

    /// <summary>Dado un documento con hojas guardadas, cuando se abre otro caso, entonces la memoria se vacía y el total se olvida.</summary>
    [TestMethod]
    public void AlCambiarDeCasoSeVaciaYSeOlvidaElTotal()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);
        PedirComoLaPantalla(hojas, 1);
        PedirComoLaPantalla(hojas, 2);

        hojas.Abrir(OtraRuta);

        Assert.AreEqual(0, hojas.Guardadas);
        Assert.IsNull(hojas.TotalDeHojas);
        Assert.IsNull(hojas.YaRasterizada(1));
        Assert.AreEqual(OtraRuta, hojas.Ruta);
    }

    /// <summary>Dado el mismo papel abierto otra vez (otro caso con la misma ruta), cuando se abre, entonces también se vacía: es por caso, no por archivo.</summary>
    [TestMethod]
    public void AbrirLaMismaRutaTambienVacia()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);
        PedirComoLaPantalla(hojas, 1);

        hojas.Abrir(Ruta);

        Assert.AreEqual(0, hojas.Guardadas);
        Assert.IsNull(hojas.TotalDeHojas);
    }

    /// <summary>Dada una hoja traída para un caso que ya no está delante, cuando se guarda, entonces se tira.</summary>
    [TestMethod]
    public void LoTraidoParaOtroCasoNoSeGuarda()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);
        var traida = hojas.Traer(Ruta, 1, null);

        hojas.Abrir(OtraRuta);
        hojas.Guardar(Ruta, traida);

        Assert.AreEqual(0, hojas.Guardadas);
        Assert.IsNull(hojas.TotalDeHojas, "ni el total: era del otro papel");
    }

    /// <summary>Dada una hoja que no se pudo dibujar, cuando se guarda, entonces no entra en la memoria pero el total sí queda.</summary>
    [TestMethod]
    public void UnaHojaQueNoSePudoDibujarNoSeGuardaPeroElTotalSi()
    {
        var lectura = new LecturaQueSeCuenta { RasterizaEnBlanco = true };
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);

        var traida = hojas.Traer(Ruta, 1, null);
        hojas.Guardar(Ruta, traida);

        Assert.IsNull(traida.Imagen);
        Assert.AreEqual(0, hojas.Guardadas);
        Assert.AreEqual(6, hojas.TotalDeHojas);
    }

    /// <summary>Dado un total ya sabido, cuando se trae otra hoja, entonces no se vuelve a contar aunque se pase el total a mano.</summary>
    [TestMethod]
    public void ConElTotalSabidoTraerNoCuenta()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);

        var traida = hojas.Traer(Ruta, 3, totalConocido: 6);

        Assert.AreEqual(6, traida.Total);
        Assert.AreEqual(0, lectura.Conteos);
    }

    /// <summary>Sin documento abierto, nada está en la memoria y guardar no hace nada.</summary>
    [TestMethod]
    public void SinDocumentoAbiertoNoHayNada()
    {
        var hojas = new HojasDelDocumento(new LecturaQueSeCuenta(), 1700);

        Assert.IsNull(hojas.YaRasterizada(1));
        hojas.Guardar(Ruta, new HojaTraida(new ImagenDePagina(1, 10, 10, [1]), 2));
        Assert.AreEqual(0, hojas.Guardadas);
    }

    /// <summary>El tope del visor viaja tal cual a la lectura: 1 700 y no otro (regla de no regresión).</summary>
    [TestMethod]
    public void ElAnchoQueSePideEsElDelVisor()
    {
        var lectura = new LecturaQueSeCuenta();
        var hojas = new HojasDelDocumento(lectura, 1700);
        hojas.Abrir(Ruta);

        var traida = hojas.Traer(Ruta, 1, null);

        Assert.AreEqual(1700, lectura.UltimoAnchoPedido);
        Assert.AreEqual(1700, traida.Imagen!.Ancho);
    }

    /// <summary>Una lectura falsa que cuenta cuántas veces rasteriza y cuántas cuenta hojas.</summary>
    private sealed class LecturaQueSeCuenta : ILecturaDePdf
    {
        /// <summary>La de verdad de las falsas, que da 6 hojas y una imagen sin bytes.</summary>
        private readonly LecturaDePdfFalsa _falsa = new(1);

        /// <summary>Cuántas veces se rasterizó una hoja.</summary>
        public int Rasterizados { get; private set; }

        /// <summary>Cuántas veces se contaron las hojas.</summary>
        public int Conteos { get; private set; }

        /// <summary>El último tope de ancho que se pidió.</summary>
        public int UltimoAnchoPedido { get; private set; }

        /// <summary>Si en vez de una imagen se devuelve nulo, como cuando la hoja no se puede dibujar.</summary>
        public bool RasterizaEnBlanco { get; init; }

        /// <inheritdoc />
        public int ContarPaginas(string rutaPdf)
        {
            Conteos++;
            return _falsa.ContarPaginas(rutaPdf);
        }

        /// <inheritdoc />
        public ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
        {
            Rasterizados++;
            UltimoAnchoPedido = anchoMaximo;
            return RasterizaEnBlanco ? null : _falsa.RasterizarPagina(rutaPdf, pagina, anchoMaximo);
        }

        /// <inheritdoc />
        public IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina) => _falsa.LeerAnotaciones(rutaPdf, pagina);

        /// <inheritdoc />
        public IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen) => _falsa.LeerConOcr(imagen);

        /// <inheritdoc />
        public IReadOnlyList<string> IdiomasDisponibles() => _falsa.IdiomasDisponibles();
    }
}
