using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Que un tachón sobre la CÉDULA o sobre el NOMBRE de una persona llegue marcado.
/// </summary>
/// <remarks>
/// <para><b>El daño que esto evita, y por qué es peor aquí que en los campos del caso.</b>
/// La cédula de miembro es la mitad del par que reconcilia todo el programa
/// —<c>numero_caso</c> + <c>mrn</c>, `DECISIONES.md` FASE 6—. Un texto tachado que por
/// casualidad tenga la forma de una cédula entraba como lectura limpia: nadie lo miraba, y
/// el Excel del compañero volvía emparejado contra un número que alguien había tachado en
/// el papel por estar mal.</para>
///
/// <para><b>Lo que estaba roto.</b> <c>Personas.Extraer</c> no recibía las anotaciones del
/// PDF, así que <c>CedulaDeLaFila</c> llamaba a <c>ResolverCampo</c> con
/// <c>hayTachon: false</c> fijo y <c>NombreDeLaFila</c> construía su campo con
/// <c>AnuladoPorTachon: false</c> escrito a mano. Las personas NUNCA detectaban un tachón,
/// aunque la capa de anotaciones lo trajera y aunque el mismo trazo sí anulara un campo del
/// caso dos filas más abajo.</para>
///
/// <para><b>La geometría no se inventó: está medida sobre los siete escaneos reales</b>
/// (2026-09-06). Las anclas caen en x 0,197–0,270 los nombres y x 0,494–0,654 las cédulas;
/// las filas de personas viven en y 0,287–0,305; el texto de la cédula va de x 0,411 a
/// 0,625 según el escaneo, y las marcas de ordenanza que el OCR devuelve como «V» están en
/// x 0,775–0,874. Los números de esta prueba son esos.</para>
/// </remarks>
[TestClass]
public class PruebaDelTachonEnLasPersonas
{
    // --- La página de prueba, con las coordenadas medidas en los siete reales ---------

    /// <summary>La cabecera «Full Name(s)» donde la mide el OCR en los siete escaneos (x 0,197–0,270).</summary>
    private static LineaDeOcr AnclaDeLosNombres()
        => new("Full Name(s)", 1.0, new BandaDeLaPagina(0.1967, 0.2229, 0.2695, 0.2394));

    /// <summary>La cabecera «Membership Record Number» donde la mide el OCR (x 0,494–0,654); su borde derecho cierra la columna.</summary>
    private static LineaDeOcr AnclaDeLasCedulas()
        => new("Membership Record Number", 1.0, new BandaDeLaPagina(0.4935, 0.2223, 0.6536, 0.2383));

    /// <summary>La etiqueta que cierra el bloque por abajo. Sin ella no sale ninguna persona.</summary>
    private static LineaDeOcr CierreDelBloque()
        => new("Temple Name", 1.0, new BandaDeLaPagina(0.06, 0.3400, 0.16, 0.3560));

    /// <summary>El renglón de la persona: el nombre manda dónde está la fila.</summary>
    private static LineaDeOcr LineaDelNombre(string texto = "Ana Prueba")
        => new(texto, 0.97, new BandaDeLaPagina(0.1980, 0.2890, 0.2730, 0.3040));

    /// <summary>La cédula, en su columna. El «055-1111-3853» es del formulario en blanco.</summary>
    private static LineaDeOcr LineaDeLaCedula(string texto = "055-1111-3853")
        => new(texto, 0.95, new BandaDeLaPagina(0.5280, 0.2900, 0.6230, 0.3030));

    /// <summary>Un trazo rojo fino: color y grosor medidos del papel real.</summary>
    private static AnotacionDelPdf TrazoRojo(double x0, double x1)
        => new(Anotaciones.SubtipoDeTrazo, null, new BandaDeLaPagina(x0, 0.2910, x1, 0.3020),
            0.8902, 0.0941, 0.1765, Anotaciones.GrosorDelTachon);

    /// <summary>El trazo rojo cruzando la columna de la cédula, de x 0,520 a 0,630.</summary>
    private static AnotacionDelPdf TachonSobreLaCedula() => TrazoRojo(0.5200, 0.6300);

    /// <summary>El trazo rojo cruzando justo el rectángulo del nombre.</summary>
    private static AnotacionDelPdf TachonSobreElNombre() => TrazoRojo(0.1980, 0.2730);

    /// <summary>Un trazo sobre las casillas de ordenanza, que están a la derecha de la cédula.</summary>
    private static AnotacionDelPdf TachonSobreLasOrdenanzas() => TrazoRojo(0.7750, 0.8740);

    /// <summary>La cédula buena escrita a mano en la misma fila, a la derecha del tachón.</summary>
    private static AnotacionDelPdf CorreccionDeLaCedula(string texto = "055-1111-3999")
        => new(Anotaciones.SubtipoDeTexto, texto, new BandaDeLaPagina(0.5300, 0.2895, 0.6300, 0.3035),
            null, null, null, null);

    /// <summary>La página mínima: las dos cabeceras, el nombre, el cierre y, si se pide, la cédula leída.</summary>
    /// <param name="conCedula">Falso para simular que el OCR no leyó la cédula de esa fila.</param>
    private static IReadOnlyList<LineaDeOcr> LaHoja(bool conCedula = true)
        => conCedula
            ? [AnclaDeLosNombres(), AnclaDeLasCedulas(), LineaDelNombre(), LineaDeLaCedula(), CierreDelBloque()]
            : [AnclaDeLosNombres(), AnclaDeLasCedulas(), LineaDelNombre(), CierreDelBloque()];

    /// <summary>Extrae las personas con la relación de aspecto de una carta, que es la de los siete.</summary>
    /// <param name="lineas">Las líneas del OCR de la página.</param>
    /// <param name="anotaciones">Los trazos y las notas de la página.</param>
    private static ResultadoDeExtraccion Extraer(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
        => new Extraccion(612.0 / 792.0).ProponerCamposDePersonas(lineas, anotaciones);

    /// <summary>El único campo propuesto con ese nombre; falla la prueba si no está o hay varios.</summary>
    /// <param name="resultado">Lo que devolvió la extracción.</param>
    /// <param name="campo">El nombre de columna: nombre o cédula.</param>
    private static CampoPropuesto CampoDe(ResultadoDeExtraccion resultado, string campo)
    {
        var propuesto = resultado.Campos.SingleOrDefault(c => c.Campo == campo);
        Assert.IsNotNull(propuesto, $"la extracción tiene que proponer «{campo}» de la persona");
        return propuesto;
    }

    // --- Las pruebas -----------------------------------------------------------------

    /// <summary>
    /// El defecto: cédula tachada, nadie escribió la buena, y llegaba como lectura limpia.
    /// </summary>
    /// <remarks>
    /// «055-1111-3853» tiene la forma exacta que <see cref="Normalizacion.NormalizarCedula"/>
    /// acepta, así que entraba a la base con origen OCR y sin una sola señal. Es el caso que
    /// el dueño señaló por su cuenta el 2026-09-04: «tiene tanto problema en leer la cédula
    /// de miembro».
    /// </remarks>
    [TestMethod]
    public void UnaCedulaTachadaSinCorreccionLlegaMarcadaEnElCampoPropuesto()
    {
        var mrn = CampoDe(Extraer(LaHoja(), [TachonSobreLaCedula()]), Extraccion.CampoCedula);

        Assert.IsTrue(mrn.AnuladoPorTachon,
            "el papel tachó la cédula y nadie escribió la buena: la marca tiene que viajar con el dato");

        // ⚠️ Hasta el 2026-09-07 aquí se fijaba «Ana Prueba 055-1111-3853»: lo tachado
        // viajaba con el RENGLÓN entero dentro, nombre incluido. Se cambió porque afirmaba lo
        // contrario de lo que el dueño pidió ese día —«siempre debe ser el nombre y debajo la
        // cédula de miembro de la persona»—, y porque el motivo que llevaba escrito seguía
        // siendo cierto a medias: `CedulaDeLaFila` sigue mirando el renglón entero, que es lo
        // medido, pero el VALOR sale de la línea que tiene forma de cédula. El nombre ya no
        // entra en el campo de la cédula ni siquiera tachado.
        Assert.AreEqual("055-1111-3853", mrn.Valor,
            "lo tachado se conserva para poder enseñarlo, pero es la cédula, no el renglón");
        Assert.AreEqual(OrigenDeCampo.Ocr, mrn.Origen);
    }

    /// <summary>Y se dice en la franja, con la frase que corresponde a esta rama.</summary>
    [TestMethod]
    public void UnaCedulaTachadaSinCorreccionSeAvisaEnLaFranja()
    {
        var avisos = Extraer(LaHoja(), [TachonSobreLaCedula()]).Avisos;

        Assert.IsTrue(
            avisos.Any(a => a.Campo == Extraccion.CampoCedula && a.Linea.Contains("tachado")),
            "hay que decir que la cédula venía tachada, no solo marcarla");
    }

    /// <summary>
    /// El segundo caso, que NO se marca igual: hay tachón y la cédula buena escrita al lado.
    /// </summary>
    /// <remarks>
    /// La marca significa «el valor que viaja está anulado», no «hubo un trazo rojo en esta
    /// fila»: lo fija el código que ya la lee. <c>Fichas.App/Importar/CamposDeLaHoja.cs:106</c>
    /// devuelve <c>null</c> en cuanto la ve, así que marcar esta rama tiraría la cédula buena
    /// que escribió la persona —y con ella la mitad de la clave de reconciliación—; y
    /// <c>Fichas.App/Correccion/EstadosDeCampo.cs:71</c> mira el tachón ANTES que el origen y
    /// le pondría «tachado, sin corrección», que aquí sería falso. Es la misma decisión que
    /// se tomó el mismo día para los campos del caso, y por el mismo motivo.
    /// </remarks>
    [TestMethod]
    public void UnaCedulaTachadaCONCorreccionConservaLaBuenaYNoSeAnula()
    {
        var mrn = CampoDe(
            Extraer(LaHoja(), [TachonSobreLaCedula(), CorreccionDeLaCedula()]), Extraccion.CampoCedula);

        Assert.AreEqual("055-1111-3999", mrn.Valor, "gana la que escribió la persona, no la tachada");
        Assert.AreEqual(OrigenDeCampo.Anotacion, mrn.Origen);
        Assert.IsFalse(mrn.AnuladoPorTachon,
            "el valor que viaja es el bueno: anularlo tiraría la mitad de la clave de reconciliación");
    }

    /// <summary>Que hubo un trazo rojo se sigue diciendo en la franja, también con corrección.</summary>
    [TestMethod]
    public void ElTachonDeLaCedulaSeAvisaTambienCuandoHayCorreccion()
    {
        var avisos = Extraer(LaHoja(), [TachonSobreLaCedula(), CorreccionDeLaCedula()]).Avisos;

        Assert.IsTrue(
            avisos.Any(a => a.Campo == Extraccion.CampoCedula && a.Linea.Contains("tachado")),
            "la marca no sustituye al aviso: el trazo rojo se cuenta igual");
    }

    /// <summary>
    /// Tachón sobre la cédula de una fila cuya cédula el OCR no llegó a leer.
    /// </summary>
    /// <remarks>
    /// Sin la marca este campo sale como «no tiene la forma esperada», y es falso: no es una
    /// cédula mal leída, es una cédula tachada. Son dos cosas distintas para quien tiene que
    /// corregir la hoja, y solo una de las dos significa «este número no vale».
    ///
    /// <para>⚠️ <b>Cambiado el 2026-09-07.</b> Hasta ese día esta prueba fijaba
    /// <c>mrn.Valor == "Ana Prueba"</c>: no había cédula legible y el campo se rellenaba con
    /// el nombre de la persona. Afirmaba justo lo que el dueño señaló como el defecto —«si no
    /// pudo leerla la deja vacía»—, así que ahora fija lo contrario: el campo VACÍO, la marca
    /// de tachón puesta, y lo que el renglón decía conservado en <c>ValorOcr</c>, que es la
    /// columna que la pantalla de corrección enseña. No se pierde nada; deja de estar donde
    /// no le toca.</para>
    /// </remarks>
    [TestMethod]
    public void UnaCedulaTachadaYSinNadaLegibleDebajoTambienLlegaMarcada()
    {
        var mrn = CampoDe(Extraer(LaHoja(conCedula: false), [TachonSobreLaCedula()]), Extraccion.CampoCedula);

        Assert.IsNull(mrn.Valor, "no había cédula: el campo se queda vacío, no se rellena con el nombre");
        Assert.AreEqual("Ana Prueba", mrn.ValorOcr, "pero lo que el renglón decía se conserva para enseñarlo");
        Assert.IsTrue(mrn.AnuladoPorTachon, "no es una cédula ilegible: es una cédula tachada");
    }

    // --- Que la marca no se reparta sola: las columnas se respetan --------------------

    /// <summary>
    /// El nombre de la misma fila NO se marca porque le tacharan la cédula.
    /// </summary>
    /// <remarks>
    /// Es la regla que `Bandas.cs:174-178` ya defiende para los campos del caso: el
    /// formulario tiene columnas, y un trazo de una columna no anula el campo de la otra
    /// solo por estar a su misma altura. Sin esto, tachar una cédula borraría también el
    /// nombre de la persona.
    /// </remarks>
    [TestMethod]
    public void UnTachonSobreLaCedulaNoMarcaElNombreDeLaMismaFila()
    {
        var resultado = Extraer(LaHoja(), [TachonSobreLaCedula()]);

        Assert.IsFalse(CampoDe(resultado, Extraccion.CampoNombreDePersona).AnuladoPorTachon,
            "el trazo cruzaba la columna de la cédula, no la del nombre");
    }

    /// <summary>Y al revés: tachar el nombre no anula la cédula.</summary>
    [TestMethod]
    public void UnTachonSobreElNombreLoMarcaAElYNoALaCedula()
    {
        var resultado = Extraer(LaHoja(), [TachonSobreElNombre()]);

        Assert.IsTrue(CampoDe(resultado, Extraccion.CampoNombreDePersona).AnuladoPorTachon,
            "el nombre venía tachado y tiene que decirlo");
        Assert.IsFalse(CampoDe(resultado, Extraccion.CampoCedula).AnuladoPorTachon,
            "la cédula no la tocó nadie");
    }

    /// <summary>
    /// Un trazo sobre las casillas de ordenanza NO anula la cédula.
    /// </summary>
    /// <remarks>
    /// Es el falso positivo que más cerca está de pasar: las seis casillas están en la MISMA
    /// fila que la cédula, a su derecha, y tachar una ordenanza mal marcada es justo lo que
    /// alguien hace con un bolígrafo rojo. Medido en los siete: la cédula acaba como mucho en
    /// x 0,625 y las marcas de ordenanza empiezan en x 0,775. Sin el corte por columna, este
    /// trazo borraría la mitad de la clave de reconciliación de la persona.
    /// </remarks>
    [TestMethod]
    public void UnTachonSobreLasCasillasDeOrdenanzaNoAnulaLaCedula()
    {
        var resultado = Extraer(LaHoja(), [TachonSobreLasOrdenanzas()]);

        Assert.IsFalse(CampoDe(resultado, Extraccion.CampoCedula).AnuladoPorTachon,
            "el trazo cae en la columna de las ordenanzas, no en la de la cédula");
        Assert.IsFalse(CampoDe(resultado, Extraccion.CampoNombreDePersona).AnuladoPorTachon);
    }

    /// <summary>Un resaltador verde no anula nada, ni siquiera encima de la cédula.</summary>
    [TestMethod]
    public void UnResaltadorVerdeSobreLaCedulaNoLaAnula()
    {
        var resaltador = new AnotacionDelPdf(
            Anotaciones.SubtipoDeTrazo, null, new BandaDeLaPagina(0.5200, 0.2910, 0.6300, 0.3020),
            0.4941, 0.7686, 0.0, Anotaciones.GrosorDelResaltador);

        Assert.IsFalse(CampoDe(Extraer(LaHoja(), [resaltador]), Extraccion.CampoCedula).AnuladoPorTachon,
            "el verde grueso resalta, no tacha: `DECISIONES.md` lo separa por color y grosor");
    }

    /// <summary>Sin trazo rojo, ningún campo de persona sale marcado. La marca no se inventa.</summary>
    [TestMethod]
    public void SinTachonNingunCampoDePersonaSaleMarcado()
    {
        var resultado = Extraer(LaHoja(), []);

        Assert.IsEmpty(resultado.Campos.Where(c => c.AnuladoPorTachon).ToArray(),
            "sin tachón en el papel no hay ningún campo de persona anulado");
        Assert.AreEqual("055-1111-3853", CampoDe(resultado, Extraccion.CampoCedula).Valor);
        Assert.AreEqual("Ana Prueba", CampoDe(resultado, Extraccion.CampoNombreDePersona).Valor);
    }

    /// <summary>
    /// Una fila con el nombre tachado y sin cédula legible NO se descarta como fila en blanco.
    /// </summary>
    /// <remarks>
    /// Es la consecuencia de que el nombre pase ahora por la precedencia: un nombre tachado
    /// sale con origen <see cref="OrigenDeCampo.Vacio"/>, igual que uno que no se leyó. Si la
    /// fila se descartara mirando solo el origen, un tachón sobre el nombre haría desaparecer
    /// a la persona del caso, que es exactamente lo que la clase entera existe para evitar
    /// —el mismo motivo por el que las filas se buscan por nombre y no por cédula—.
    /// </remarks>
    [TestMethod]
    public void UnaFilaConElNombreTachadoNoSeDescartaComoFilaEnBlanco()
    {
        var resultado = Extraer(LaHoja(conCedula: false), [TachonSobreElNombre()]);

        var nombre = CampoDe(resultado, Extraccion.CampoNombreDePersona);
        Assert.IsTrue(nombre.AnuladoPorTachon, "el nombre venía tachado");
        Assert.AreEqual("Ana Prueba", nombre.Valor,
            "lo tachado se conserva para poder enseñarlo: la persona no desaparece del caso");
    }

    /// <summary>
    /// Una anotación que no tiene forma de cédula NO se toma como corrección de la cédula.
    /// </summary>
    /// <remarks>
    /// La fila lleva escrito «Verified for endowment and sealing» a mano en varios de los
    /// siete. Si esa nota se tomara como el valor corregido, la cédula buena que el OCR leyó
    /// se perdería. Es el mismo caso medido que documenta <c>Campos.MejorCorreccion</c> con
    /// el nombre de la unidad, y aquí se fija para las personas.
    /// </remarks>
    [TestMethod]
    public void UnaNotaEscritaQueNoTieneFormaDeCedulaNoSustituyeALaLeida()
    {
        var nota = new AnotacionDelPdf(
            Anotaciones.SubtipoDeTexto, "Verified for endowment and sealing",
            new BandaDeLaPagina(0.5300, 0.2895, 0.6300, 0.3035), null, null, null, null);

        var mrn = CampoDe(Extraer(LaHoja(), [nota]), Extraccion.CampoCedula);

        Assert.AreEqual("055-1111-3853", mrn.Valor, "la nota no es una cédula: no corrige la cédula");
        Assert.AreEqual(OrigenDeCampo.Ocr, mrn.Origen);
    }
}
