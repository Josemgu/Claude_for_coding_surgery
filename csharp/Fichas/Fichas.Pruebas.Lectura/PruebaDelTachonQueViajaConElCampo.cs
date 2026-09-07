using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Que la marca de tachón VIAJE en el <see cref="CampoPropuesto"/>, y no solo en el aviso.
/// </summary>
/// <remarks>
/// <para><b>El daño que esto evita.</b> Un campo tachado en el papel salía de la
/// extracción indistinguible de una lectura limpia: se construía el
/// <see cref="CampoPropuesto"/> con siete argumentos posicionales y
/// <c>AnuladoPorTachon</c> —el noveno, con valor por defecto <c>false</c>— no se pasaba.
/// Si lo que había debajo del trazo rojo tenía por casualidad la forma correcta —una
/// cédula de once dígitos, una fecha bien formada—, el programa lo daba por bueno. Es
/// justo el dato que el obispo tachó por estar equivocado.</para>
///
/// <para><b>Por qué las dos ramas del tachón NO se marcan igual.</b> En el contrato,
/// <c>CampoPropuesto.AnuladoPorTachon</c> quiere decir «el valor que viaja aquí está
/// anulado», no «hubo un trazo rojo en esta fila del papel». Lo dice el código que ya
/// lee la marca:
/// <list type="bullet">
///   <item><c>Fichas.App/Importar/CamposDeLaHoja.cs:106</c> devuelve <c>null</c> en cuanto
///   ve la marca. Ponerla en un campo corregido tiraría el valor bueno que escribió la
///   persona, que es lo contrario de lo que se pretende.</item>
///   <item><c>Fichas.App/Correccion/EstadosDeCampo.cs:71</c> mira el tachón ANTES que
///   <c>Origen == Anotacion</c>, y su palabra es «tachado, sin corrección». Un campo con
///   corrección escrita saldría en la pantalla con una frase literalmente falsa.</item>
/// </list>
/// Que hubo un trazo rojo sobre un campo corregido se sigue diciendo donde ya se decía
/// —en el aviso de la franja— y lo tachado sigue entero en <c>ValorOcr</c>. No se pierde
/// nada; lo que no se hace es anular un dato correcto.</para>
/// </remarks>
[TestClass]
public class PruebaDelTachonQueViajaConElCampo
{
    /// <summary>El rótulo impreso del que cuelga la banda de la fecha de viaje.</summary>
    private static readonly BandaDeLaPagina Rotulo = new(0.05, 0.42, 0.21, 0.44);

    private static LineaDeOcr Ancla()
        => new("Date traveling to the temple", 1.0, Rotulo);

    /// <summary>Una línea del OCR dentro de la banda que cuelga del rótulo.</summary>
    private static LineaDeOcr EnLaBanda(string texto, double x0 = 0.06, double x1 = 0.20)
        => new(texto, 0.97, new BandaDeLaPagina(x0, 0.443, x1, 0.453));

    /// <summary>Un trazo rojo fino que cruza la banda: color y grosor medidos del papel real.</summary>
    private static AnotacionDelPdf Tachon()
        => new(Anotaciones.SubtipoDeTrazo, null, new BandaDeLaPagina(0.05, 0.442, 0.22, 0.452),
            0.8902, 0.0941, 0.1765, Anotaciones.GrosorDelTachon);

    /// <summary>La corrección escrita al lado, en la misma banda.</summary>
    private static AnotacionDelPdf Correccion(string texto)
        => new(Anotaciones.SubtipoDeTexto, texto, new BandaDeLaPagina(0.24, 0.443, 0.32, 0.453),
            null, null, null, null);

    private static CampoPropuesto FechaPropuesta(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        var resultado = new Extraccion().ProponerCamposDelCaso(lineas, anotaciones);
        var fecha = resultado.Campos.SingleOrDefault(c => c.Campo == Extraccion.CampoFechaDeViaje);

        Assert.IsNotNull(fecha, "la extracción tiene que proponer la fecha de viaje, aunque sea vacía");
        return fecha;
    }

    /// <summary>
    /// El defecto: tachón sin corrección, y el campo llegaba como una lectura normal.
    /// </summary>
    /// <remarks>
    /// «September 7, 2026» tiene la forma de una fecha buena, así que el valor viaja
    /// —requisito 9: lo que el papel decía no se pierde en silencio— pero tiene que
    /// viajar MARCADO. Sin la marca, aguas abajo es idéntico a un dato limpio.
    /// </remarks>
    [TestMethod]
    public void UnTachonSinCorreccionLlegaMarcadoEnElCampoPropuesto()
    {
        var fecha = FechaPropuesta([Ancla(), EnLaBanda("September 7, 2026")], [Tachon()]);

        Assert.IsTrue(fecha.AnuladoPorTachon,
            "el papel lo tachó y nadie escribió el valor bueno: la marca tiene que viajar con el dato");

        // Lo leído no se pierde —requisito 9—, pero hoy viaja en `Valor`, no en `ValorOcr`:
        // la extracción todavía no llena `CampoPropuesto.ValorOcr` en ninguna rama, y quien
        // guarda cubre el hueco con un respaldo (`GuardadoDeHojas.Filas.cs:243` hace
        // `ValorOcr = propuesto?.ValorOcr ?? propuesto?.Valor`). Se fija aquí tal como es,
        // para que si alguien lo cambia se vea en esta prueba y no en la base.
        Assert.AreEqual("September 7, 2026", fecha.Valor, "lo tachado se conserva para poder enseñarlo");
        Assert.AreEqual(OrigenDeCampo.Ocr, fecha.Origen);
    }

    /// <summary>
    /// El otro caso, que NO es igual: hay tachón y alguien escribió el valor bueno al lado.
    /// </summary>
    /// <remarks>
    /// Es el caso de los siete escaneos reales del dueño. Marcarlo como anulado tiraría
    /// la fecha correcta: <c>CamposDeLaHoja.ValorDe</c> devuelve <c>null</c> en cuanto ve
    /// la marca, así que el 8 de septiembre no llegaría a la base y la persona se
    /// quedaría sin fecha de viaje.
    /// </remarks>
    [TestMethod]
    public void UnTachonCONCorreccionNoAnulaElValorBuenoQueEscribioLaPersona()
    {
        var fecha = FechaPropuesta(
            [Ancla(), EnLaBanda("September 7, 2026")], [Tachon(), Correccion("8 Sept 2026")]);

        Assert.AreEqual("2026-09-08", fecha.Valor, "gana lo que escribió la persona");
        Assert.AreEqual(OrigenDeCampo.Anotacion, fecha.Origen);
        Assert.IsFalse(fecha.AnuladoPorTachon,
            "el valor que viaja es el bueno, no el de debajo del trazo: anularlo tiraría un dato correcto");
    }

    /// <summary>
    /// El tachón se dice igual en la franja, tenga corrección o no. La marca no lo sustituye.
    /// </summary>
    [TestMethod]
    public void ElTachonSigueDiciendoseEnLaFranjaEnLosDosCasos()
    {
        var sinCorreccion = new Extraccion()
            .ProponerCamposDelCaso([Ancla(), EnLaBanda("September 7, 2026")], [Tachon()]);
        var conCorreccion = new Extraccion().ProponerCamposDelCaso(
            [Ancla(), EnLaBanda("September 7, 2026")], [Tachon(), Correccion("8 Sept 2026")]);

        Assert.IsTrue(
            sinCorreccion.Avisos.Any(a => a.Campo == Extraccion.CampoFechaDeViaje && a.Linea.Contains("tachado")),
            "sin corrección hay que avisar del tachón");
        Assert.IsTrue(
            conCorreccion.Avisos.Any(a => a.Campo == Extraccion.CampoFechaDeViaje && a.Linea.Contains("tachado")),
            "con corrección también: el aviso es lo que cuenta que hubo un trazo rojo");
    }

    /// <summary>
    /// Tachón sobre una banda donde el OCR no leyó nada legible: sigue siendo un tachón.
    /// </summary>
    /// <remarks>
    /// Sin la marca este campo sale como «no se pudo leer del papel», que es falso: sí se
    /// pudo mirar, y lo que dice el papel es que ahí no vale nada. Son dos cosas
    /// distintas para quien tiene que corregir la hoja.
    /// </remarks>
    [TestMethod]
    public void UnTachonSobreUnaBandaSinNadaLegibleTambienLlegaMarcado()
    {
        var fecha = FechaPropuesta([Ancla()], [Tachon()]);

        Assert.IsNull(fecha.Valor);
        Assert.AreEqual(OrigenDeCampo.Vacio, fecha.Origen);
        Assert.IsTrue(fecha.AnuladoPorTachon, "no es un campo ilegible: es un campo tachado");
    }

    /// <summary>Sin trazo rojo, ningún campo sale marcado. La marca no se reparte sola.</summary>
    [TestMethod]
    public void SinTachonNingunCampoDelCasoSaleMarcado()
    {
        var resultado = new Extraccion()
            .ProponerCamposDelCaso([Ancla(), EnLaBanda("September 7, 2026")], []);

        Assert.IsEmpty(resultado.Campos.Where(c => c.AnuladoPorTachon).ToArray(),
            "sin tachón en el papel no hay ningún campo anulado");
    }
}
