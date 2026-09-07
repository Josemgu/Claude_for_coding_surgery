using Fichas.App.Correccion;
using Fichas.Contratos.Lectura;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// La aritmetica del visor: donde cae la banda y cuanto hay que moverse para verla.
/// </summary>
/// <remarks>
/// Portada de <c>interfaz/encuadre.py</c>, y por el mismo motivo que alli: sin una sola
/// linea de interfaz, para que corra en milisegundos y de el mismo numero en cualquier
/// maquina. El zoom y el arrastre de verdad los hace el <c>ScrollView</c> de WinUI; lo que
/// se prueba aqui es lo que WinUI no sabe: donde esta la banda de un campo.
/// </remarks>
[TestClass]
public sealed class PruebasDelEncuadre
{
    private const double AnchoDeLaHoja = 2705;
    private const double AltoDeLaHoja = 3500;

    /// <summary>Una banda en fracciones se convierte en un rectangulo de la hoja.</summary>
    /// <remarks>
    /// Las coordenadas van en FRACCIONES y no en pixeles porque un pixel depende de la
    /// escala del dia en que se rasterizo; una fraccion vale a cualquier escala.
    /// </remarks>
    [TestMethod]
    public void UnaBandaEnFraccionesSeVuelveUnRectanguloDeLaHoja()
    {
        var banda = new BandaDeLaPagina(0.10, 0.20, 0.50, 0.25);
        var rectangulo = Encuadre.RectanguloDeLaBanda(banda, AnchoDeLaHoja, AltoDeLaHoja);

        Assert.AreEqual(270.5, rectangulo.X, 0.01);
        Assert.AreEqual(700.0, rectangulo.Y, 0.01);
        Assert.AreEqual(1082.0, rectangulo.Ancho, 0.01);
        Assert.AreEqual(175.0, rectangulo.Alto, 0.01);
    }

    /// <summary>El rectangulo no se recalcula al hacer zoom: se multiplica por la escala.</summary>
    [TestMethod]
    public void ElRectanguloEscalaConElZoomSinRecalcularse()
    {
        var banda = new BandaDeLaPagina(0.10, 0.20, 0.50, 0.25);
        var aUno = Encuadre.RectanguloDeLaBanda(banda, AnchoDeLaHoja, AltoDeLaHoja);
        var alDoble = Encuadre.RectanguloDeLaBanda(banda, AnchoDeLaHoja * 2, AltoDeLaHoja * 2);
        Assert.AreEqual(aUno.X * 2, alDoble.X, 0.01);
        Assert.AreEqual(aUno.Ancho * 2, alDoble.Ancho, 0.01);
    }

    /// <summary>El zoom de arranque mete la hoja entera a lo ancho del panel.</summary>
    /// <remarks>
    /// El modo de arranque lo cambio el dueno: «¿Para que me pones el PDF al lado si no
    /// puedo moverme dentro de el? Esta muy pequeno». Ajustar a la banda encuadra un
    /// renglon de tres centimetros; a lo ancho se ve la hoja como es.
    /// </remarks>
    [TestMethod]
    public void ElZoomDeArranqueMeteLaHojaALoAncho()
    {
        Assert.AreEqual(0.2, Encuadre.ZoomParaElAncho(541, AnchoDeLaHoja), 0.001);
        Assert.AreEqual(0.4, Encuadre.ZoomParaElAncho(1082, AnchoDeLaHoja), 0.001);
    }

    /// <summary>Un panel sin ancho todavia no dice nada: se queda en 1 y no divide por cero.</summary>
    [TestMethod]
    public void UnPanelSinAnchoNoRompeElZoom()
    {
        Assert.AreEqual(1.0, Encuadre.ZoomParaElAncho(0, AnchoDeLaHoja), 0.001);
        Assert.AreEqual(1.0, Encuadre.ZoomParaElAncho(541, 0), 0.001);
    }

    /// <summary>Los pasos de zoom son fijos: dos personas que dicen «al 200» ven lo mismo.</summary>
    [TestMethod]
    public void LosPasosDeZoomSonFijos()
    {
        Assert.AreEqual(1.50, Encuadre.PasoDeZoom(1.00, 1), 0.001);
        Assert.AreEqual(0.75, Encuadre.PasoDeZoom(1.00, -1), 0.001);
        // Desde un valor que no es un paso, se salta al siguiente que si lo es.
        Assert.AreEqual(1.00, Encuadre.PasoDeZoom(0.83, 1), 0.001);
        Assert.AreEqual(0.75, Encuadre.PasoDeZoom(0.83, -1), 0.001);
    }

    /// <summary>En los topes, otro paso no se sale de la escala.</summary>
    [TestMethod]
    public void EnLosTopesElZoomNoSeSale()
    {
        Assert.AreEqual(3.00, Encuadre.PasoDeZoom(3.00, 1), 0.001);
        Assert.AreEqual(0.50, Encuadre.PasoDeZoom(0.50, -1), 0.001);
    }

    // ---- traer la banda a la vista ---------------------------------------

    /// <summary>
    /// Si la banda ya se ve entera, no se mueve nada.
    /// </summary>
    /// <remarks>
    /// Una vista que salta en cada tabulacion marea y hace perder de vista el campo
    /// anterior, que es contra lo que se compara (<c>interfaz/correccion.py</c>).
    /// </remarks>
    [TestMethod]
    public void UnaBandaQueYaSeVeNoMueveNada()
    {
        var rectangulo = new RectanguloEnPixeles(100, 200, 400, 60);
        var (x, y) = Encuadre.OffsetParaVer(rectangulo, 800, 600, zoom: 1.0, offsetX: 0, offsetY: 0);
        Assert.AreEqual(0, x, 0.01);
        Assert.AreEqual(0, y, 0.01);
    }

    /// <summary>Una banda por debajo de la vista la trae con su margen, sin centrarla.</summary>
    [TestMethod]
    public void UnaBandaPorDebajoSeTraeConSuMargen()
    {
        var rectangulo = new RectanguloEnPixeles(100, 2000, 400, 60);
        var (_, y) = Encuadre.OffsetParaVer(rectangulo, 800, 600, zoom: 1.0, offsetX: 0, offsetY: 0);
        // Su borde de abajo cae en 2060; con 12 px de margen, la vista acaba en 2072.
        Assert.AreEqual(2072 - 600, y, 0.01);
    }

    /// <summary>Una banda por encima de la vista la trae hacia arriba con su margen.</summary>
    [TestMethod]
    public void UnaBandaPorEncimaSeTraeHaciaArriba()
    {
        var rectangulo = new RectanguloEnPixeles(100, 300, 400, 60);
        var (_, y) = Encuadre.OffsetParaVer(rectangulo, 800, 600, zoom: 1.0, offsetX: 0, offsetY: 1000);
        Assert.AreEqual(288, y, 0.01);
    }

    /// <summary>Con zoom, la banda se mide en pixeles de pantalla, no de la hoja.</summary>
    [TestMethod]
    public void ConZoomLaBandaSeMideEnPixelesDePantalla()
    {
        var rectangulo = new RectanguloEnPixeles(0, 1000, 400, 60);
        var (_, y) = Encuadre.OffsetParaVer(rectangulo, 800, 600, zoom: 2.0, offsetX: 0, offsetY: 0);
        // A 200 %, el borde de abajo de la banda cae en 2120 px de pantalla.
        Assert.AreEqual(2120 + 12 - 600, y, 0.01);
    }

    /// <summary>El desplazamiento nunca sale por debajo de cero.</summary>
    [TestMethod]
    public void ElDesplazamientoNoSeVaANegativo()
    {
        var rectangulo = new RectanguloEnPixeles(0, 0, 400, 60);
        var (x, y) = Encuadre.OffsetParaVer(rectangulo, 800, 600, zoom: 1.0, offsetX: 50, offsetY: 50);
        Assert.IsGreaterThanOrEqualTo(0.0, x);
        Assert.IsGreaterThanOrEqualTo(0.0, y);
    }

    // ---- el zoom que deja quieto lo que hay bajo el cursor ---------------

    /// <summary>Ampliar en un punto deja quieto lo que hay debajo de ese punto.</summary>
    /// <remarks>
    /// Es lo que pidio el dueno: «que pueda hacer zoom a cualquier parte». Sin esto, ampliar
    /// se lleva la vista a otro sitio y hay que volver a buscar el dato.
    /// </remarks>
    [TestMethod]
    public void AmpliarEnUnPuntoDejaQuietoLoQueHayDebajo()
    {
        // A zoom 1, el punto (300, 400) del panel con la vista en (100, 200) cae en la hoja
        // en (400, 600). Al pasar a zoom 2 tiene que seguir cayendo en el mismo punto.
        var (x, y) = Encuadre.OffsetAlAmpliarEn(300, 400, 100, 200, zoomViejo: 1.0, zoomNuevo: 2.0);
        Assert.AreEqual((400 * 2) - 300, x, 0.01);
        Assert.AreEqual((600 * 2) - 400, y, 0.01);
    }

    /// <summary>Reducir tambien deja quieto ese punto, y no se va a negativo.</summary>
    [TestMethod]
    public void ReducirTambienDejaQuietoElPunto()
    {
        var (x, y) = Encuadre.OffsetAlAmpliarEn(300, 400, 800, 1200, zoomViejo: 2.0, zoomNuevo: 1.0);
        Assert.AreEqual(((800 + 300) / 2.0) - 300, x, 0.01);
        Assert.AreEqual(((1200 + 400) / 2.0) - 400, y, 0.01);
    }
}
