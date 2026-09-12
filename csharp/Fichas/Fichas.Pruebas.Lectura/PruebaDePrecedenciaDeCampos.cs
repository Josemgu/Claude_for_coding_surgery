using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// El orden de precedencia de `DECISIONES.md`, portado de `extraccion/campos.py`.
/// </summary>
/// <remarks>
/// El caso real que lo obliga esta en los siete documentos del dueno: la fila «Date
/// traveling to the temple» dice «September 7, 2026», encima hay un trazo rojo que lo
/// cruza, y al lado una anotacion `/FreeText` que dice «8 Sept 2026».
/// </remarks>
[TestClass]
public class PruebaDePrecedenciaDeCampos
{
    /// <summary>La fila de valor donde caen todas las líneas del OCR de esta clase.</summary>
    private static readonly BandaDeLaPagina Fila = new(0.05, 0.42, 0.30, 0.44);

    /// <summary>Una línea del OCR dentro de <see cref="Fila"/>.</summary>
    /// <param name="texto">Lo leído.</param>
    /// <param name="confianza">La que dio el motor; 0,97 por defecto, para que se vea que se conserva.</param>
    private static LineaDeOcr LineaOcr(string texto, double confianza = 0.97)
        => new(texto, confianza, Fila);

    /// <summary>Una nota escrita a mano (<c>/FreeText</c>) dentro de la fila, de 0,06 de ancho.</summary>
    /// <param name="texto">Lo escrito.</param>
    /// <param name="x0">Dónde empieza; para poner dos y ver cuál gana.</param>
    private static AnotacionDelPdf Correccion(string texto, double x0 = 0.06)
        => new("FreeText", texto, new BandaDeLaPagina(x0, 0.425, x0 + 0.06, 0.438), null, null, null, null);

    /// <summary>Vigila el tercer punto de la precedencia: sin tachón ni corrección manda el OCR, con su confianza tal cual.</summary>
    [TestMethod]
    public void SinTachonNiCorreccionValeLoQueLeyoElOcr()
    {
        var campo = Campos.ResolverCampo([LineaOcr("September 7, 2026")], [], hayTachon: false, Normalizacion.NormalizarFecha);

        Assert.AreEqual("2026-09-07", campo.Valor);
        Assert.AreEqual(OrigenDeCampo.Ocr, campo.Origen);
        Assert.AreEqual(0.97, campo.Confianza!.Value, 1e-9);
    }

    /// <summary>Vigila el segundo punto: la corrección escrita gana incluso con tachón, con origen anotación y confianza 1,0.</summary>
    [TestMethod]
    public void UnaCorreccionEscritaGanaAlOcrYValeUnoComaCero()
    {
        var campo = Campos.ResolverCampo(
            [LineaOcr("September 7, 2026")], [Correccion("8 Sept 2026")], hayTachon: true, Normalizacion.NormalizarFecha);

        Assert.AreEqual("2026-09-08", campo.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, campo.Origen);
        Assert.AreEqual(1.0, campo.Confianza!.Value, 1e-9);
        Assert.IsTrue(campo.AnuladoPorTachon);
    }

    /// <summary>
    /// El punto que hay que defender cuando alguien proponga «aprovechar» el texto de
    /// debajo del tachon: si se aprovecha, se guarda justo el dato que una persona
    /// marco como equivocado.
    /// </summary>
    [TestMethod]
    public void UnTachonSinCorreccionDejaElCampoVacioYNoRecuperaLoTachado()
    {
        var campo = Campos.ResolverCampo([LineaOcr("September 7, 2026")], [], hayTachon: true, Normalizacion.NormalizarFecha);

        Assert.IsNull(campo.Valor);
        Assert.AreEqual(OrigenDeCampo.Vacio, campo.Origen);
        Assert.IsTrue(campo.AnuladoPorTachon);
        Assert.AreEqual("September 7, 2026", campo.ValorOcr, "lo tachado se conserva para poder ensenarlo, no para usarlo");
    }

    /// <summary>
    /// Caso medido: en la fila «Ward/Branch Name and Unit Number» hay una anotacion con
    /// el NOMBRE de la unidad y nada mas. Tomandola como correccion del NUMERO, el
    /// numero que el OCR habia leido bien se perdia.
    /// </summary>
    [TestMethod]
    public void UnaAnotacionQueCorrigeOtroCampoDeLaFilaNoSeLlevaElQueEstabaBien()
    {
        var campo = Campos.ResolverCampo(
            [LineaOcr("Castries Branch - 700001")],
            [Correccion("Castries Branch")],
            hayTachon: false,
            Normalizacion.NormalizarNumeroDeUnidad);

        Assert.AreEqual("700001", campo.Valor);
        Assert.AreEqual(OrigenDeCampo.Ocr, campo.Origen);
    }

    // --- Requisito 9: se avisa, no se vacia en silencio ---------------------------

    /// <summary>
    /// El criterio C3-L5. Un valor que no encaja con la forma del campo se DEVUELVE con
    /// su aviso; jamas se pierde en silencio. Es la regla que hacia desaparecer 2 de las
    /// 7 cedulas del dueno.
    /// </summary>
    [TestMethod]
    public void LoQueNoEncajaVuelveConSuAvisoYNuncaVacioEnSilencio()
    {
        var campo = Campos.ResolverCampo([LineaOcr("055-1111-38")], [], hayTachon: false, Normalizacion.NormalizarCedula);

        Assert.IsNull(campo.Valor, "no tiene la forma pedida");
        Assert.AreEqual("055-1111-38", campo.ValorOcr, "pero lo que el papel decia sigue ahi");
        Assert.IsTrue(campo.NecesitaRevision);
    }

    /// <summary>Vigila que sin líneas ni anotaciones el campo salga vacío de todo, sin inventar un texto leído.</summary>
    [TestMethod]
    public void CuandoElOcrNoLeyoNadaElCampoQuedaVacioSinValorLeido()
    {
        var campo = Campos.ResolverCampo([], [], hayTachon: false, Normalizacion.NormalizarCedula);

        Assert.IsNull(campo.Valor);
        Assert.IsNull(campo.ValorOcr);
        Assert.AreEqual(OrigenDeCampo.Vacio, campo.Origen);
    }
}
