using Fichas.App.Correccion;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Las reglas de formato de la pantalla de correccion, sin abrir ninguna ventana.
/// </summary>
/// <remarks>
/// Se escriben desde el criterio, no desde el codigo: cada una nombra la decision del
/// dueno o el archivo de Python del que se porta la regla. Ninguna llama a nada de XAML.
/// </remarks>
[TestClass]
public sealed class PruebasDeLasReglasDeCampo
{
    // ---- la cedula de miembro (MRN) --------------------------------------

    /// <summary>Dado un MRN de solo digitos, cuando se valida, entonces no hay motivo.</summary>
    [TestMethod]
    public void UnMrnDeSoloDigitosNoTieneMotivo()
        => Assert.IsNull(ReglasDeCampo.MotivoDelMrn("055-1111-3853"));

    /// <summary>
    /// Dado un MRN terminado en letra, cuando se valida, entonces PASA y sin marca de duda.
    /// </summary>
    /// <remarks>
    /// DECISIONES.md, 2026-09-04: «Muchas cedulas de miembro tienen una A u otra letra al
    /// final». La regla vieja de «11 digitos» dejaba 2 de 7 cedulas reales sin guardar.
    /// </remarks>
    [TestMethod]
    public void UnMrnTerminadoEnLetraPasa()
    {
        Assert.IsNull(ReglasDeCampo.MotivoDelMrn("066-2222-133A"));
        Assert.IsNull(ReglasDeCampo.MotivoDelMrn("066-2222-133a"));
    }

    /// <summary>Dado un MRN corto, cuando se valida, entonces el motivo nombra el campo.</summary>
    [TestMethod]
    public void UnMrnCortoTraeMotivoQueNombraElCampo()
    {
        var motivo = ReglasDeCampo.MotivoDelMrn("123");
        Assert.IsNotNull(motivo);
        StringAssert.Contains(motivo, "cédula", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dado un MRN de nueve digitos —el de C4-10—, entonces no vale.</summary>
    [TestMethod]
    public void UnMrnDeNueveDigitosNoVale()
        => Assert.IsNotNull(ReglasDeCampo.MotivoDelMrn("123-4567-890"));

    /// <summary>Dado un MRN nulo o vacio, entonces no hay motivo: el OCR pudo no leerlo.</summary>
    [TestMethod]
    public void UnMrnVacioNoEsUnError()
    {
        Assert.IsNull(ReglasDeCampo.MotivoDelMrn(null));
        Assert.IsNull(ReglasDeCampo.MotivoDelMrn("   "));
    }

    // ---- el numero de unidad ---------------------------------------------

    /// <summary>Seis y siete digitos valen los dos: manda el papel (DECISIONES 2026-09-02).</summary>
    [TestMethod]
    public void LaUnidadAdmiteSeisYSieteDigitos()
    {
        Assert.IsNull(ReglasDeCampo.MotivoDeLaUnidadNumero("123456"));
        Assert.IsNull(ReglasDeCampo.MotivoDeLaUnidadNumero("7000011"));
    }

    /// <summary>Cinco digitos —el de C4-10— no vale.</summary>
    [TestMethod]
    public void UnaUnidadDeCincoDigitosNoVale()
        => Assert.IsNotNull(ReglasDeCampo.MotivoDeLaUnidadNumero("12345"));

    // ---- la fecha de viaje -----------------------------------------------

    /// <summary>Una fecha ISO real no tiene motivo.</summary>
    [TestMethod]
    public void UnaFechaRealNoTieneMotivo()
        => Assert.IsNull(ReglasDeCampo.MotivoDeLaFechaDeViaje("2026-09-08"));

    /// <summary>La forma no basta: el 31 de febrero no existe y no pasa.</summary>
    [TestMethod]
    public void UnaFechaConFormaBuenaQueNoExisteNoPasa()
        => Assert.IsNotNull(ReglasDeCampo.MotivoDeLaFechaDeViaje("2026-02-31"));

    /// <summary>Otro formato de fecha no pasa.</summary>
    [TestMethod]
    public void UnaFechaEnOtroFormatoNoPasa()
        => Assert.IsNotNull(ReglasDeCampo.MotivoDeLaFechaDeViaje("08/09/2026"));

    // ---- el mes cruzado: AVISA, no impide --------------------------------

    /// <summary>
    /// Dado un numero de caso de septiembre y una fecha de octubre, entonces hay AVISO.
    /// </summary>
    /// <remarks>
    /// Es aviso y no pared por decision del dueno (DECISIONES 2026-09-02, P-2): como pared
    /// haria imposible guardar un viaje reprogramado a otro mes.
    /// </remarks>
    [TestMethod]
    public void ElMesCruzadoAvisaYNoImpide()
    {
        var aviso = ReglasDeCampo.AvisoDelMesCruzado("CASP2609", "2026-10-05");
        Assert.IsNotNull(aviso);
        Assert.IsNull(ReglasDeCampo.MotivoDeLaFechaDeViaje("2026-10-05"), "El aviso NO puede volver invalida la fecha.");
    }

    /// <summary>Cuando el mes cuadra, no hay aviso.</summary>
    [TestMethod]
    public void ElMesQueCuadraNoAvisa()
        => Assert.IsNull(ReglasDeCampo.AvisoDelMesCruzado("CASP2609", "2026-09-05"));

    /// <summary>Sin numero de caso no hay con que comparar: no se inventa un aviso.</summary>
    [TestMethod]
    public void SinNumeroDeCasoNoHayAvisoDeMes()
        => Assert.IsNull(ReglasDeCampo.AvisoDelMesCruzado(null, "2026-10-05"));

    // ---- el numero de caso y el nombre de unidad -------------------------

    /// <summary>Cuatro letras y cuatro digitos; nulo se admite porque puede no leerse.</summary>
    [TestMethod]
    public void ElNumeroDeCasoSonCuatroLetrasYCuatroDigitos()
    {
        Assert.IsNull(ReglasDeCampo.MotivoDelNumeroDeCaso("CASP2609"));
        Assert.IsNull(ReglasDeCampo.MotivoDelNumeroDeCaso(null));
        Assert.IsNotNull(ReglasDeCampo.MotivoDelNumeroDeCaso("casp2609"));
        Assert.IsNotNull(ReglasDeCampo.MotivoDelNumeroDeCaso("CASP26"));
    }

    /// <summary>Un nombre de unidad larguisimo es una linea de OCR que se colo entera.</summary>
    [TestMethod]
    public void UnNombreDeUnidadDemasiadoLargoNoVale()
    {
        Assert.IsNull(ReglasDeCampo.MotivoDelNombreDeUnidad("Paramaribo Branch"));
        Assert.IsNotNull(ReglasDeCampo.MotivoDelNombreDeUnidad(new string('x', 121)));
    }

    // ---- el repartidor ---------------------------------------------------

    /// <summary>Un campo sin regla —el nombre— siempre pasa: no hay forma de decidirlo.</summary>
    [TestMethod]
    public void UnCampoSinReglaSiemprePasa()
        => Assert.IsNull(ReglasDeCampo.MotivoDe("nombre", "ANONIMO, J0SE M1GUEL"));

    /// <summary>El repartidor lleva cada campo a su regla.</summary>
    [TestMethod]
    public void ElRepartidorLlevaCadaCampoASuRegla()
    {
        Assert.IsNotNull(ReglasDeCampo.MotivoDe("mrn", "123"));
        Assert.IsNotNull(ReglasDeCampo.MotivoDe("unidad_numero", "12345"));
        Assert.IsNotNull(ReglasDeCampo.MotivoDe("fecha_viaje", "2026-02-31"));
        Assert.IsNull(ReglasDeCampo.MotivoDe("mrn", "055-1111-3853"));
    }

    /// <summary>Ningun motivo pasa de dos lineas del panel: es criterio C4-12, y se mide.</summary>
    /// <remarks>
    /// 430 px de columna con la letra del panel dan sitio a unos 62 caracteres por linea
    /// (mockup v2: «Columna de campos 430 x 634 px»). Dos lineas son 124.
    /// </remarks>
    [TestMethod]
    public void NingunMotivoPasaDeDosLineasDelPanel()
    {
        string?[] motivos =
        [
            ReglasDeCampo.MotivoDelMrn("123"),
            ReglasDeCampo.MotivoDeLaUnidadNumero("12345"),
            ReglasDeCampo.MotivoDeLaFechaDeViaje("2026-02-31"),
            ReglasDeCampo.MotivoDeLaFechaDeViaje("08/09/2026"),
            ReglasDeCampo.MotivoDelNumeroDeCaso("CASP26"),
            ReglasDeCampo.MotivoDelNombreDeUnidad(new string('x', 121)),
        ];
        foreach (var motivo in motivos)
        {
            Assert.IsNotNull(motivo);
            Assert.IsLessThanOrEqualTo(124, motivo.Length, $"«{motivo}» mide {motivo.Length} caracteres y el tope son 124.");
        }
    }
}
