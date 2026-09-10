using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Las reglas de un campo tecleado en el formulario, sobre una página construida.
/// </summary>
/// <remarks>
/// <para>Lo que el PDF real del dueño no puede demostrar por sí solo —trae una persona y
/// todo tecleado con su forma— se fija aquí: que una fila tecleada sale sin ninguna línea
/// de OCR, que la cédula se atribuye a la fila por su <c>/Rect</c> y no por el orden en
/// que el PDF guarde los campos, que un valor sin forma va a revisión con lo tecleado a
/// la vista, que una fila sin nada tecleado no es una persona, y que una corrección escrita
/// a mano le gana al campo tecleado que corrige.</para>
///
/// <para>La geometría de las anclas es la medida en los siete escaneos (ver
/// <see cref="PruebaDelTachonEnLasPersonas"/>); la de los campos, la del formulario del
/// dueño pasada a fracciones de página: la columna del nombre va de x 0,059 a 0,411 y la
/// de la cédula de 0,413 a 0,736.</para>
/// </remarks>
[TestClass]
public class PruebaDeLosCamposTecleados
{
    private static LineaDeOcr AnclaDeLosNombres()
        => new("Nombre(s) de pila", 1.0, new BandaDeLaPagina(0.1967, 0.2229, 0.2695, 0.2394));

    private static LineaDeOcr AnclaDeLasCedulas()
        => new("Número de cédula de miembro", 1.0, new BandaDeLaPagina(0.4935, 0.2223, 0.6536, 0.2383));

    private static LineaDeOcr AnclaDelTemplo()
        => new("Nombre del templo", 1.0, new BandaDeLaPagina(0.06, 0.3400, 0.16, 0.3560));

    private static LineaDeOcr AnclaDeLaFechaDeViaje()
        => new("Fecha de viaje al templo", 1.0, new BandaDeLaPagina(0.06, 0.4300, 0.25, 0.4430));

    /// <summary>Las etiquetas impresas, y ni una línea de OCR dentro de las filas.</summary>
    private static IReadOnlyList<LineaDeOcr> SoloLasEtiquetas()
        => [AnclaDeLosNombres(), AnclaDeLasCedulas(), AnclaDelTemplo(), AnclaDeLaFechaDeViaje()];

    private static (double Y0, double Y1) Fila(int numero)
        => (0.2890 + (numero - 1) * 0.0180, 0.3040 + (numero - 1) * 0.0180);

    private static AnotacionDelPdf CampoTecleado(string? texto, double x0, double x1, double y0, double y1)
        => new(Anotaciones.SubtipoDeCampoDeTexto, texto, new BandaDeLaPagina(x0, y0, x1, y1), null, null, null, null);

    private static AnotacionDelPdf NombreTecleado(int fila, string? texto)
        => CampoTecleado(texto, 0.059, 0.411, Fila(fila).Y0, Fila(fila).Y1);

    private static AnotacionDelPdf CedulaTecleada(int fila, string? texto)
        => CampoTecleado(texto, 0.413, 0.736, Fila(fila).Y0, Fila(fila).Y1);

    private static AnotacionDelPdf CorreccionAMano(string texto, double x0, double x1, double y0, double y1)
        => new(Anotaciones.SubtipoDeTexto, texto, new BandaDeLaPagina(x0, y0, x1, y1), null, null, null, null);

    private static ResultadoDeExtraccion Personas(params AnotacionDelPdf[] anotaciones)
        => new Extraccion(612.0 / 792.0).ProponerCamposDePersonas(SoloLasEtiquetas(), anotaciones);

    private static ResultadoDeExtraccion Caso(params AnotacionDelPdf[] anotaciones)
        => new Extraccion(612.0 / 792.0).ProponerCamposDelCaso(SoloLasEtiquetas(), anotaciones);

    private static CampoPropuesto CampoDe(ResultadoDeExtraccion resultado, string campo, int? fila = null)
    {
        var propuesto = resultado.Campos.SingleOrDefault(c => c.Campo == campo && (fila is null || c.FilaFormulario == fila));
        Assert.IsNotNull(propuesto, $"la extracción tiene que proponer «{campo}»{(fila is null ? "" : $" de la fila {fila}")}");
        return propuesto;
    }

    // --- Las personas -------------------------------------------------------------------

    /// <summary>Sin una sola línea de OCR en la fila, la persona sale de los campos tecleados.</summary>
    [TestMethod]
    public void UnaFilaTecleadaSaleComoPersonaSinNingunaLineaDeOcr()
    {
        var resultado = Personas(NombreTecleado(1, "Ana Prueba"), CedulaTecleada(1, "055-1111-3853"));

        var nombre = CampoDe(resultado, Extraccion.CampoNombreDePersona);
        var cedula = CampoDe(resultado, Extraccion.CampoCedula);
        Assert.AreEqual("Ana Prueba", nombre.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, nombre.Origen);
        Assert.AreEqual(Campos.ConfianzaDeUnaAnotacion, nombre.Confianza);
        Assert.AreEqual(1, nombre.FilaFormulario);
        Assert.AreEqual("055-1111-3853", cedula.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, cedula.Origen);
        Assert.AreEqual(1, cedula.FilaFormulario);
        Assert.IsFalse(resultado.Avisos.Any(a => a.Linea.Contains("ninguna persona", StringComparison.Ordinal)));
    }

    /// <summary>
    /// La trampa geométrica: el orden en que el PDF guarda los campos no dice de qué fila son.
    /// </summary>
    [TestMethod]
    public void LaCedulaSeAtribuyeALaFilaPorSuRectanguloYNoPorElOrdenDeLosCampos()
    {
        var resultado = Personas(
            CedulaTecleada(2, "055-1111-3999"),
            NombreTecleado(1, "Ana Prueba"),
            CedulaTecleada(1, "055-1111-3853"),
            NombreTecleado(2, "Beto Prueba"));

        Assert.AreEqual("Ana Prueba", CampoDe(resultado, Extraccion.CampoNombreDePersona, fila: 1).Valor);
        Assert.AreEqual("055-1111-3853", CampoDe(resultado, Extraccion.CampoCedula, fila: 1).Valor);
        Assert.AreEqual("Beto Prueba", CampoDe(resultado, Extraccion.CampoNombreDePersona, fila: 2).Valor);
        Assert.AreEqual("055-1111-3999", CampoDe(resultado, Extraccion.CampoCedula, fila: 2).Valor);
    }

    /// <summary>Las filas del formulario sin nada tecleado no son personas ni se cuentan como descartadas.</summary>
    [TestMethod]
    public void UnaFilaSinNadaTecleadoNoEsUnaPersona()
    {
        var resultado = Personas(
            NombreTecleado(1, "Ana Prueba"), CedulaTecleada(1, "055-1111-3853"),
            NombreTecleado(2, null), CedulaTecleada(2, null),
            NombreTecleado(3, ""), CedulaTecleada(3, "  "));

        Assert.HasCount(1, resultado.Campos.Where(c => c.Campo == Extraccion.CampoNombreDePersona).ToArray());
        Assert.IsFalse(resultado.Avisos.Any(a => a.Linea.Contains("descartaron", StringComparison.Ordinal)),
            "una fila vacía del formulario no se leyó y se tiró: nunca tuvo nada");
    }

    /// <summary>
    /// Un valor tecleado sin forma de cédula NO se inventa ni se recorta: viaja tal cual, avisado.
    /// </summary>
    /// <remarks>
    /// Es la misma rama que salva las cédulas terminadas en letra de los escaneos: el valor
    /// va a la vista y sin la confianza de una lectura limpia, para que llegue a Corrección.
    /// </remarks>
    [TestMethod]
    public void UnValorTecleadoSinFormaDeCedulaVaARevisionYSeEnsenaTalCual()
    {
        var resultado = Personas(NombreTecleado(1, "Ana Prueba"), CedulaTecleada(1, "sin forma 12"));

        var cedula = CampoDe(resultado, Extraccion.CampoCedula);
        Assert.AreEqual("sin forma 12", cedula.Valor, "lo tecleado se enseña tal cual");
        Assert.AreEqual("sin forma 12", cedula.ValorOcr);
        Assert.AreNotEqual(OrigenDeCampo.Anotacion, cedula.Origen, "sin forma no puede pasar por dato exacto y limpio");
        Assert.IsNull(cedula.Confianza, "sin la confianza de una lectura limpia: así llega a revisión");
        Assert.IsTrue(resultado.Avisos.Any(a => a.Campo == Extraccion.CampoCedula
                                              && a.Linea.Contains("no tiene la forma esperada", StringComparison.Ordinal)),
            string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
    }

    /// <summary>Un nombre tecleado con la cédula vacía sigue siendo una persona, con la cédula vacía y avisada.</summary>
    [TestMethod]
    public void UnNombreTecleadoConLaCedulaVaciaSaleConLaCedulaVaciaYAvisada()
    {
        var resultado = Personas(NombreTecleado(1, "Ana Prueba"), CedulaTecleada(1, null));

        Assert.AreEqual("Ana Prueba", CampoDe(resultado, Extraccion.CampoNombreDePersona).Valor);
        var cedula = CampoDe(resultado, Extraccion.CampoCedula);
        Assert.IsNull(cedula.Valor);
        Assert.AreEqual(OrigenDeCampo.Vacio, cedula.Origen);
        Assert.IsTrue(cedula.NecesitaRevision);
    }

    // --- Los campos del caso ------------------------------------------------------------

    /// <summary>La fecha tecleada en la banda de la etiqueta sale normalizada y con origen anotación.</summary>
    [TestMethod]
    public void LaFechaTecleadaBajoSuEtiquetaSaleNormalizada()
    {
        var resultado = Caso(CampoTecleado("17/09/2026", 0.059, 0.573, 0.4444, 0.4609));

        var fecha = CampoDe(resultado, Extraccion.CampoFechaDeViaje);
        Assert.AreEqual("2026-09-17", fecha.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, fecha.Origen);
    }

    /// <summary>Una fecha tecleada ambigua no se adivina: va a revisión con lo tecleado a la vista.</summary>
    [TestMethod]
    public void UnaFechaTecleadaAmbiguaNoSeAdivina()
    {
        var resultado = Caso(CampoTecleado("05/10/2026", 0.059, 0.573, 0.4444, 0.4609));

        var fecha = CampoDe(resultado, Extraccion.CampoFechaDeViaje);
        Assert.AreEqual("05/10/2026", fecha.Valor, "se enseña lo tecleado, no un día elegido al azar");
        Assert.AreNotEqual(OrigenDeCampo.Anotacion, fecha.Origen);
        Assert.IsNull(fecha.Confianza);
        Assert.IsTrue(resultado.Avisos.Any(a => a.Campo == Extraccion.CampoFechaDeViaje));
    }

    /// <summary>
    /// Una corrección escrita a mano le gana al campo tecleado de la misma banda.
    /// </summary>
    /// <remarks>
    /// Alguien teclea la fecha, y luego otra persona la corrige encima con una nota: la nota
    /// es lo más reciente y lo que una persona quiso decir. Estar más a la derecha no la
    /// desempata en contra.
    /// </remarks>
    [TestMethod]
    public void UnaCorreccionEscritaAManoLeGanaAlCampoTecleado()
    {
        var resultado = Caso(
            CampoTecleado("17/09/2026", 0.059, 0.573, 0.4444, 0.4609),
            CorreccionAMano("18 Sept 2026", 0.300, 0.400, 0.4440, 0.4620));

        Assert.AreEqual("2026-09-18", CampoDe(resultado, Extraccion.CampoFechaDeViaje).Valor);
    }

    /// <summary>Y un campo tecleado vacío no dice nada: la banda queda como si no estuviera.</summary>
    [TestMethod]
    public void UnCampoTecleadoVacioNoProponeNada()
    {
        var resultado = Caso(CampoTecleado("", 0.059, 0.573, 0.4444, 0.4609));

        var fecha = CampoDe(resultado, Extraccion.CampoFechaDeViaje);
        Assert.IsNull(fecha.Valor);
        Assert.AreEqual(OrigenDeCampo.Vacio, fecha.Origen);
    }
}
