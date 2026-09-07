using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Que ningún campo tome su valor de otra fila, de otra franja del papel o de otra hoja.
/// </summary>
/// <remarks>
/// <para><b>Lo que el dueño enseñó el 2026-09-07</b>, con la pantalla al lado del papel:
/// «muchos de ellos cargan informaciones al lado de otro PDF, no son informaciones
/// correctas. Siempre debe ser el nombre y debajo la cédula de miembro de la persona; si
/// no pudo leerla la deja vacía. Lo que está pasando es que las informaciones se están
/// cruzando». Y sobre una hoja concreta: «en el PDF se ve que la rama es Wanica Branch,
/// pero en la parte del lado dice Barrio Cantaura, y el PDF no tiene unidad».</para>
///
/// <para><b>La medición que ordena esta clase</b>, hecha el 2026-09-07 sobre los diez PDF
/// reales de esta máquina —20 hojas de OCR de verdad— antes de tocar nada:</para>
/// <list type="bullet">
///   <item>En <b>20 de 20</b> filas de persona, el valor propuesto de <c>mrn</c> salía del
///   RENGLÓN entero: «Wendell Wilfred Rink ###-####-###A Verified». Y en la hoja 5 de los
///   dos <c>SURB2609</c>, donde el OCR leyó la cédula con una letra O por un cero, el valor
///   de <c>mrn</c> era literalmente <b>el nombre de la persona</b>.</item>
///   <item>De las líneas que caían en la banda de un campo del caso, las que de verdad son
///   de esa fila del papel tienen entre <b>0,588 y 1,000</b> de su alto dentro de la banda;
///   las que son de otra fila —una caja del OCR que se tragó tres renglones de una hoja
///   escaneada del revés— tienen entre <b>0,354 y 0,481</b>. Cuatro de esas entraron:
///   «ceniceq t zcench:» y «WANLCA Bzeench» como nombre de unidad, y «Currency:» dentro de
///   la fecha de viaje.</item>
/// </list>
///
/// <para>⚠️ <b>Y lo más grave, que es lo que estas pruebas matan:</b> esos valores salían
/// con la confianza del OCR delante —0,784 el de la hoja 6— como si la lectura fuera del
/// campo al que se le asignaba. Una confianza alta sobre un dato cruzado hace que nadie lo
/// mire. Un valor que no se puede atribuir a su fila no lleva confianza ninguna.</para>
///
/// <para><b>Y el requisito 9 se cumple igual, que es la trampa de este arreglo:</b> lo
/// leído NO se pierde. Sale del campo —que queda vacío— y viaja en
/// <see cref="CampoPropuesto.ValorOcr"/>, que es la columna <c>procedencia_campo.valor_ocr</c>
/// que <c>Fichas.App/Correccion/EstadosDeCampo.cs:136</c> ya pinta como «El lector leyó
/// aquí «…» y no encajó: no se guardó». Vacío en el dato, dicho en el aviso, y lo leído
/// enseñado donde no se puede confundir con el valor.</para>
/// </remarks>
[TestClass]
public class PruebaDeQueNadaSeCruzaDeSitio
{
    // --- Una página construida con dos filas de personas -----------------------------
    // Las coordenadas son las medidas en los siete escaneos reales (ver
    // `PruebaDelTachonEnLasPersonas`): anclas en y 0,222–0,239 y filas de 0,0150 de alto.

    private static LineaDeOcr AnclaDeLosNombres()
        => new("Full Name(s)", 1.0, new BandaDeLaPagina(0.1967, 0.2229, 0.2695, 0.2394));

    private static LineaDeOcr AnclaDeLasCedulas()
        => new("Membership Record Number", 1.0, new BandaDeLaPagina(0.4935, 0.2223, 0.6536, 0.2383));

    private static LineaDeOcr CierreDelBloque()
        => new("Temple Name", 1.0, new BandaDeLaPagina(0.06, 0.3400, 0.16, 0.3560));

    /// <summary>La primera fila de personas, en y 0,2890–0,3040.</summary>
    private static LineaDeOcr NombreDeLaPrimeraFila(string texto = "Ana Prueba")
        => new(texto, 0.98, new BandaDeLaPagina(0.1980, 0.2890, 0.2730, 0.3040));

    /// <summary>La segunda fila, pegada debajo: y 0,3050–0,3200. Es la de al lado.</summary>
    private static LineaDeOcr NombreDeLaSegundaFila(string texto = "Mark Porter")
        => new(texto, 0.98, new BandaDeLaPagina(0.1980, 0.3050, 0.2730, 0.3200));

    private static LineaDeOcr CedulaDeLaPrimeraFila(string texto = "055-1111-3853")
        => new(texto, 0.95, new BandaDeLaPagina(0.5280, 0.2900, 0.6230, 0.3030));

    private static LineaDeOcr CedulaDeLaSegundaFila(string texto = "066-2222-133A")
        => new(texto, 0.95, new BandaDeLaPagina(0.5280, 0.3060, 0.6230, 0.3190));

    private static ResultadoDeExtraccion Extraer(params LineaDeOcr[] lineas)
        => new Extraccion(612.0 / 792.0).ProponerCamposDePersonas(lineas, []);

    private static IReadOnlyList<CampoPropuesto> CamposDe(ResultadoDeExtraccion resultado, string campo)
        => resultado.Campos.Where(c => c.Campo == campo).OrderBy(c => c.FilaFormulario).ToArray();

    // --- 1. La cédula es de la fila de su nombre, o no es -----------------------------

    /// <summary>
    /// La fila que no trae cédula NO se queda con la de la fila de al lado.
    /// </summary>
    /// <remarks>
    /// Es la primera frase del dueño: «siempre debe ser el nombre y debajo la cédula de
    /// miembro de la persona». Aquí la primera fila no tiene cédula y la segunda sí; lo que
    /// no puede pasar es que la de la segunda aparezca en la primera.
    /// </remarks>
    [TestMethod]
    public void LaCedulaDeUnaFilaNoSeVaALaFilaDeAlLado()
    {
        var resultado = Extraer(
            AnclaDeLosNombres(), AnclaDeLasCedulas(),
            NombreDeLaPrimeraFila(), NombreDeLaSegundaFila(), CedulaDeLaSegundaFila(),
            CierreDelBloque());

        var cedulas = CamposDe(resultado, Extraccion.CampoCedula);
        Assert.HasCount(2, cedulas, "hay dos filas con nombre: tienen que salir las dos");
        Assert.IsNull(cedulas[0].Valor,
            "la primera fila no trae cédula en el papel: se queda VACÍA, no toma la de la segunda");
        Assert.AreEqual("066-2222-133A", cedulas[1].Valor, "y la segunda conserva la suya");
    }

    /// <summary>
    /// Una fila con nombre y sin cédula legible deja el campo vacío y lo dice nombrándolo.
    /// </summary>
    /// <remarks>
    /// El defecto medido: <c>CedulaDeLaFila</c> juntaba todas las líneas del renglón y,
    /// cuando ninguna tenía forma de cédula, el respaldo del requisito 9 metía el renglón
    /// entero —o sea el NOMBRE— dentro de <c>mrn</c>. Pasa de verdad en la hoja 5 de los dos
    /// <c>SURB2609</c>, donde el OCR leyó un cero como letra O.
    /// </remarks>
    [TestMethod]
    public void UnaFilaConNombreYSinCedulaLegibleDejaElMrnVacioYLoAvisa()
    {
        var resultado = Extraer(
            AnclaDeLosNombres(), AnclaDeLasCedulas(),
            NombreDeLaPrimeraFila(), CedulaDeLaPrimeraFila("8O5-1111-3853"),
            CierreDelBloque());

        var mrn = CamposDe(resultado, Extraccion.CampoCedula).Single();
        Assert.IsNull(mrn.Valor, "no se leyó ninguna cédula: el campo se queda vacío");
        Assert.AreEqual(OrigenDeCampo.Vacio, mrn.Origen);
        Assert.IsNull(mrn.Confianza, "sin valor no hay confianza que enseñar");

        Assert.IsTrue(
            resultado.Avisos.Any(a => a.Campo == Extraccion.CampoCedula),
            "vacío en silencio es lo único que el requisito 9 no permite: tiene que haber aviso");
    }

    /// <summary>Y lo leído se conserva para poder enseñarlo, fuera del valor.</summary>
    /// <remarks>
    /// Es la otra mitad del requisito 9. <c>Fichas.App/Importar/GuardadoDeHojas.Filas.cs:238</c>
    /// escribe este campo en <c>procedencia_campo.valor_ocr</c> y
    /// <c>Fichas.App/Correccion/EstadosDeCampo.cs:136</c> lo pinta como «El lector leyó aquí
    /// «…» y no encajó: no se guardó». Lo leído se ve; el dato sigue vacío.
    /// </remarks>
    [TestMethod]
    public void LoQueSeLeyoEnElRenglonSeConservaAunqueNoSeaLaCedula()
    {
        var resultado = Extraer(
            AnclaDeLosNombres(), AnclaDeLasCedulas(),
            NombreDeLaPrimeraFila(), CedulaDeLaPrimeraFila("8O5-1111-3853"),
            CierreDelBloque());

        var mrn = CamposDe(resultado, Extraccion.CampoCedula).Single();
        Assert.IsNotNull(mrn.ValorOcr, "lo que el papel decía tiene que seguir estando");
        StringAssert.Contains(mrn.ValorOcr, "8O5-1111-3853",
            "y tiene que ser lo que se leyó, para que Miguel vea por qué no encajó");
    }

    /// <summary>
    /// Dos textos con forma de cédula en el mismo renglón: no se elige uno, se avisa.
    /// </summary>
    /// <remarks>
    /// Es el caso que hace imposible atribuir con certeza, y el que hoy pasaba en silencio:
    /// <c>NormalizarCedula</c> se quedaba con la primera coincidencia del renglón junto, sin
    /// decir que había otra. En una hoja escaneada del revés el OCR devuelve cajas de hasta
    /// 0,043 de alto —medido en la hoja 5 de los <c>SURB2609</c>, casi tres renglones—, y ahí
    /// dos filas caben en la misma. Elegir la de más a la izquierda sería inventarse a quién
    /// pertenece.
    /// </remarks>
    [TestMethod]
    public void DosCedulasEnElMismoRenglonDejanElCampoVacioYSinConfianza()
    {
        var segundaCedulaEnLaMismaFila = new LineaDeOcr(
            "066-2222-133A", 0.99, new BandaDeLaPagina(0.6300, 0.2900, 0.7200, 0.3030));

        var resultado = Extraer(
            AnclaDeLosNombres(), AnclaDeLasCedulas(),
            NombreDeLaPrimeraFila(), CedulaDeLaPrimeraFila(), segundaCedulaEnLaMismaFila,
            CierreDelBloque());

        var mrn = CamposDe(resultado, Extraccion.CampoCedula).Single();
        Assert.IsNull(mrn.Valor, "hay dos cédulas en el renglón: no se sabe cuál es la de esta persona");
        Assert.IsNull(mrn.Confianza, "y un valor que no se puede atribuir no puede decir 0,98");
        Assert.IsTrue(resultado.Avisos.Any(a => a.Campo == Extraccion.CampoCedula));
    }

    // --- 2. El nombre es el nombre, y la cédula la cédula -----------------------------

    /// <summary>
    /// Cuando el OCR pega el nombre y la cédula en UNA línea, cada cosa va a su campo.
    /// </summary>
    /// <remarks>
    /// Es un caso medido, no hipotético: en los siete escaneos del dueño el OCR devolvió
    /// «Ejemplo, Daniel Jr. Damian Dorian |055-1111-3853 Verified √» y por eso
    /// <c>CedulaDeLaFila</c> mira el renglón entero. Lo que estaba mal no era mirarlo: era
    /// que el renglón entero se quedara dentro del campo.
    /// </remarks>
    [TestMethod]
    public void CuandoElOcrPegaNombreYCedulaEnUnaLineaCadaUnoVaASuCampo()
    {
        var resultado = Extraer(
            AnclaDeLosNombres(), AnclaDeLasCedulas(),
            NombreDeLaPrimeraFila("Ana Prueba 055-1111-3853"),
            CierreDelBloque());

        Assert.AreEqual("Ana Prueba", CamposDe(resultado, Extraccion.CampoNombreDePersona).Single().Valor,
            "el nombre es el nombre: la cédula pegada no forma parte de él");
        Assert.AreEqual("055-1111-3853", CamposDe(resultado, Extraccion.CampoCedula).Single().Valor,
            "y la cédula sale de la misma línea, que es donde estaba");
    }

    // --- 3. Un valor que no cabe en su fila no entra, y menos con confianza -----------

    /// <summary>
    /// Una caja del OCR que se traga tres renglones no le da su texto al campo de uno.
    /// </summary>
    /// <remarks>
    /// <para>Es el caso del dueño, reproducido con la geometría de la hoja 6 de los
    /// <c>SURB2609</c>: el ancla «Ward/Branch Name and Unit Number» mide 0,0166 de alto, su
    /// banda de valor 0,0133, y la línea que el OCR devolvió mide <b>0,0283</b> —dos
    /// renglones—. Solo el <b>0,469</b> de esa línea cae dentro de la banda.</para>
    ///
    /// <para>Hasta hoy esa línea entraba entera y su texto salía como nombre Y como número
    /// de unidad, con la confianza del OCR delante. Es lo que hace que un dato cruzado pase
    /// por bueno.</para>
    /// </remarks>
    [TestMethod]
    public void UnaLineaQueNoCabeEnLaFilaDeSuRotuloNoLeDaSuTextoAlCampo()
    {
        var ancla = new LineaDeOcr(
            "Ward/Branch Name and Unit Number", 1.0, new BandaDeLaPagina(0.1190, 0.6549, 0.3079, 0.6714));
        var lineaDeDosRenglones = new LineaDeOcr(
            "Barrio Cantaura (700004)", 0.98, new BandaDeLaPagina(0.1050, 0.6623, 0.3227, 0.6906));

        var resultado = new Extraccion(612.0 / 792.0)
            .ProponerCamposDelCaso([ancla, lineaDeDosRenglones], []);

        var nombre = resultado.Campos.Single(c => c.Campo == Extraccion.CampoUnidadNombre);
        var numero = resultado.Campos.Single(c => c.Campo == Extraccion.CampoUnidadNumero);

        Assert.IsNull(nombre.Valor, "esa línea no es de la fila de este rótulo: no puede ser su valor");
        Assert.IsNull(numero.Valor);
        Assert.IsNull(nombre.Confianza, "y sobre todo NO puede decir 0,98: no se pudo atribuir");
        Assert.IsNull(numero.Confianza);
    }

    /// <summary>Y ese texto tampoco se pierde: se conserva y se avisa nombrando el campo.</summary>
    [TestMethod]
    public void ElTextoQueNoSePudoAtribuirSeConservaYSeAvisa()
    {
        var ancla = new LineaDeOcr(
            "Ward/Branch Name and Unit Number", 1.0, new BandaDeLaPagina(0.1190, 0.6549, 0.3079, 0.6714));
        var lineaDeDosRenglones = new LineaDeOcr(
            "Barrio Cantaura (700004)", 0.98, new BandaDeLaPagina(0.1050, 0.6623, 0.3227, 0.6906));

        var resultado = new Extraccion(612.0 / 792.0)
            .ProponerCamposDelCaso([ancla, lineaDeDosRenglones], []);

        var nombre = resultado.Campos.Single(c => c.Campo == Extraccion.CampoUnidadNombre);
        Assert.AreEqual("Barrio Cantaura (700004)", nombre.ValorOcr,
            "lo leído se enseña; lo que no se hace es darlo por el valor del campo");
        Assert.IsTrue(
            resultado.Avisos.Any(a => a.Campo == Extraccion.CampoUnidadNombre),
            "y se dice, nombrando el campo: el requisito 9 prohíbe el hueco mudo, no el hueco");
    }

    /// <summary>
    /// La línea que SÍ es de la fila sigue entrando: el corte no se come lo bueno.
    /// </summary>
    /// <remarks>
    /// La geometría es la de los siete <c>CASP2609</c>: la línea «Castries Branch - 700001»
    /// tiene el <b>0,729</b> de su alto dentro de la banda. El corte está en 0,5, entre el
    /// peor caso bueno medido —0,588— y el mejor caso malo —0,481—.
    /// </remarks>
    [TestMethod]
    public void LaLineaQueSiEsDeLaFilaSigueEntrandoConSuValor()
    {
        var ancla = new LineaDeOcr(
            "Ward/Branch Name and Unit Number", 1.0, new BandaDeLaPagina(0.0591, 0.6866, 0.2636, 0.7011));
        var linea = new LineaDeOcr(
            "Castries Branch - 700001", 0.993, new BandaDeLaPagina(0.0599, 0.6991, 0.2303, 0.7151));

        var resultado = new Extraccion(612.0 / 792.0).ProponerCamposDelCaso([ancla, linea], []);

        Assert.AreEqual("700001", resultado.Campos.Single(c => c.Campo == Extraccion.CampoUnidadNumero).Valor);
        Assert.AreEqual("Castries Branch",
            resultado.Campos.Single(c => c.Campo == Extraccion.CampoUnidadNombre).Valor);
    }
}
