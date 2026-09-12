using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Las reglas de formato de los campos, portadas de `extraccion/normalizacion.py`.
/// </summary>
/// <remarks>
/// Cada caso sale de una medicion escrita en `DECISIONES.md`, no del codigo: la cedula
/// que termina en letra (2026-09-04), la unidad de 6 o 7 digitos (2026-09-02), y el
/// rango de fechas que no es una fecha de viaje.
/// </remarks>
[TestClass]
public class PruebaDeNormalizacion
{
    // --- Cedula de miembro (MRN) -------------------------------------------------

    /// <summary>Vigila que una cédula de once dígitos salga con sus guiones y sin el texto de alrededor.</summary>
    [TestMethod]
    public void LaCedulaDeOnceDigitosSeGuardaTalCual()
        => Assert.AreEqual("055-1115-4668", Normalizacion.NormalizarCedula("MRN 055-1115-4668 "));

    /// <summary>
    /// `DECISIONES.md`, 2026-09-04: «muchas cedulas de miembro tienen una A u otra letra
    /// al final». Es la regla que hacia perder 2 de las 7 cedulas de los escaneos reales.
    /// </summary>
    [TestMethod]
    public void LaCedulaPuedeTerminarEnLetraYNoSeToca()
    {
        Assert.AreEqual("066-2222-133A", Normalizacion.NormalizarCedula("066-2222-133A"));
        Assert.AreEqual("066-2222-133a", Normalizacion.NormalizarCedula("066-2222-133a"),
            "la letra no se sube a mayuscula: eso seria corregir lo leido");
    }

    /// <summary>Vigila que la letra solo se admita en la última posición: en medio, no hay cédula.</summary>
    [TestMethod]
    public void UnaCedulaConLaLetraEnMedioNoEsUnaCedula()
        => Assert.IsNull(Normalizacion.NormalizarCedula("055-11A1-3853"));

    /// <summary>Vigila que un nombre, un nulo o solo blancos den nulo, no una excepción ni una cadena vacía.</summary>
    [TestMethod]
    public void UnTextoSinCedulaDevuelveNulo()
    {
        Assert.IsNull(Normalizacion.NormalizarCedula("Ana Prueba"));
        Assert.IsNull(Normalizacion.NormalizarCedula(null));
        Assert.IsNull(Normalizacion.NormalizarCedula("   "));
    }

    // --- Numero de caso ----------------------------------------------------------

    /// <summary>Vigila que el número de caso se saque de dentro de un texto con más cosas.</summary>
    [TestMethod]
    public void ElNumeroDeCasoSonCuatroLetrasYCuatroDigitos()
        => Assert.AreEqual("CASP2609", Normalizacion.NormalizarNumeroDeCaso("Caso: CASP2609"));

    /// <summary>
    /// `DECISIONES.md`, ADR-0004 §6ter: `CASD2609` pasa el formato aunque el papel diga
    /// `CASP2609`. Ninguna restriccion de forma caza ese error, y por eso el numero
    /// entra tal como se leyo y se corrige a mano.
    /// </summary>
    [TestMethod]
    public void UnNumeroDeCasoMalLeidoPeroConLaFormaBuenaEntraIgual()
        => Assert.AreEqual("CASD2609", Normalizacion.NormalizarNumeroDeCaso("CASD2609"));

    /// <summary>Vigila que «casp2609» dé nulo: subirlo a mayúsculas escondería que el OCR pudo leer mal las cifras también.</summary>
    [TestMethod]
    public void ElNumeroDeCasoEnMinusculasNoSeSubeAMayuscula()
        => Assert.IsNull(Normalizacion.NormalizarNumeroDeCaso("casp2609"));

    // --- Fecha -------------------------------------------------------------------

    /// <summary>Vigila las tres formas con el mes en letras: mes-día-año inglés, día-mes-año inglés abreviado y «de … de» español.</summary>
    [TestMethod]
    public void LaFechaConElMesEnLetrasSaleEnIso()
    {
        Assert.AreEqual("2026-09-07", Normalizacion.NormalizarFecha("September 7, 2026"));
        Assert.AreEqual("2026-09-08", Normalizacion.NormalizarFecha("8 Sept 2026"));
        Assert.AreEqual("2027-03-14", Normalizacion.NormalizarFecha("14 de marzo de 2027"));
    }

    /// <summary>
    /// Medido el 2026-09-03: las fechas del formulario espanol llegan en cifras. Y las
    /// cifras se miran ANTES que el rango, porque «14-03-2027» tambien casa con el
    /// patron de rango y sin ese orden se perderian todas.
    /// </summary>
    [TestMethod]
    public void LaFechaEnCifrasSeResuelveCuandoLosDigitosLaDecidan()
    {
        Assert.AreEqual("2027-03-14", Normalizacion.NormalizarFecha("14-03-2027"));
        Assert.AreEqual("2026-09-08", Normalizacion.NormalizarFecha("2026-09-08"));
    }

    /// <summary>Vigila que con los dos números de 12 o menos no se elija un orden.</summary>
    [TestMethod]
    public void UnaFechaAmbiguaEnCifrasNoSeAdivina()
        => Assert.IsNull(Normalizacion.NormalizarFecha("05-10-2027"),
            "«05-10-2027» es el 5 de octubre o el 10 de mayo, y elegir seria inventar");

    /// <summary>
    /// «September 8-11, 2026» es la cita del templo, no el dia de viaje. Es el texto que
    /// de verdad aparece en los siete documentos del dueno.
    /// </summary>
    [TestMethod]
    public void UnRangoDeFechasNoEsUnaFechaDeViaje()
        => Assert.IsNull(Normalizacion.NormalizarFecha("September 8-11, 2026"));

    /// <summary>Vigila que un 31 de febrero dé nulo en vez de moverse al 28 o al 3 de marzo.</summary>
    [TestMethod]
    public void UnDiaQueNoExisteNoSeRedondea()
        => Assert.IsNull(Normalizacion.NormalizarFecha("February 31, 2026"));

    // --- Unidad ------------------------------------------------------------------

    /// <summary>Vigila los dos largos que manda el papel, con el nombre delante y un guion en medio.</summary>
    [TestMethod]
    public void LaUnidadAdmiteSeisOSieteDigitos()
    {
        Assert.AreEqual("700001", Normalizacion.NormalizarNumeroDeUnidad("Castries Branch - 700001"));
        Assert.AreEqual("7000015", Normalizacion.NormalizarNumeroDeUnidad("Kingstown, St. Vincent - 7000015"));
    }

    /// <summary>Vigila que una cifra de ocho dígitos dé nulo en vez de perder los dos últimos.</summary>
    [TestMethod]
    public void UnaCifraDeOtroLargoNoSeRecorta()
        => Assert.IsNull(Normalizacion.NormalizarNumeroDeUnidad("Rama - 12345678"));

    /// <summary>Vigila que el nombre salga sin el número ni el guion, y conserve su coma y su punto.</summary>
    [TestMethod]
    public void ElNombreDeLaUnidadSaleSinSuNumero()
    {
        Assert.AreEqual("Castries Branch", Normalizacion.NormalizarNombreDeUnidad("Castries Branch - 700001"));
        Assert.AreEqual("Kingstown, St. Vincent", Normalizacion.NormalizarNombreDeUnidad("Kingstown, St. Vincent - 7000015"));
    }

    // --- Templo ------------------------------------------------------------------

    /// <summary>Vigila que el templo solo pierda los espacios de sobra y nunca se complete contra una lista.</summary>
    [TestMethod]
    public void ElTemploSoloJuntaEspaciosYNoSeCorrigeContraNingunCatalogo()
    {
        Assert.AreEqual("Panama City, Panama", Normalizacion.NormalizarNombreDelTemplo(" Panama  City, Panama "));
        Assert.AreEqual("Panama City", Normalizacion.NormalizarNombreDelTemplo("Panama City"),
            "no hay catalogo: un templo a medias se ve a medias, no se completa");
    }
}
