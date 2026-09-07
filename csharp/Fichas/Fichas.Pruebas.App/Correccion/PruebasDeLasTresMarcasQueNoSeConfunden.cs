using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Las tres marcas que existen sobre un documento, y que NO se pueden confundir.
/// </summary>
/// <remarks>
/// Son tres cosas distintas y el dueno las nombro como tres:
/// <list type="number">
/// <item><b>La firma de campos</b> —«Todo correcto»— que es suya y campo por campo (regla
/// permanente 5). No sale en esta frase: vive en <c>procedencia_campo.verificado</c> y la
/// cuenta el pie de la pantalla aparte.</item>
/// <item><b>El estado que escribe el Excel del companero</b>, con el nombre del companero:
/// «el documento que ellos llenan es el que marca, y dice completado por Sandy»
/// (2026-09-03).</item>
/// <item><b>El atajo del administrador</b>: «yo puedo completarlos tambien desde el sistema
/// sin pasar la verificacion, y cuando pase eso debe decir "el administrador lo hizo"».</item>
/// </list>
/// <para>
/// ⚠️ Lo que separa la 2 de la 3 en la base es <c>estado_marcado_origen</c>, que la
/// migracion 14 creo y que ya guardaba dos valores distintos (ADR-0005 §6.1). No hace falta
/// ninguna columna nueva, y por eso no se anade.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasTresMarcasQueNoSeConfunden
{
    /// <summary>Un caso completado por el atajo del administrador.</summary>
    private static Caso PorElAdministrador() => new()
    {
        Id = 1,
        NumeroCaso = "CASP2609",
        CreadoEn = "2026-09-05",
        EstadoRecomendacion = "completa",
        EstadoMarcadoPor = 2,
        EstadoMarcadoEn = "2026-09-05T10:00:00",
        EstadoMarcadoOrigen = ElAdministrador.Origen,
    };

    /// <summary>Un caso completado por el Excel que devolvio un companero.</summary>
    /// <remarks>
    /// Es lo que escribe <c>MarcarEstadoDelCompanero</c>: ademas del estado vigente deja
    /// <c>estado_del_companero</c>, y el origen es la RUTA del Excel que lo trajo.
    /// </remarks>
    private static Caso PorElExcelDelCompanero() => new()
    {
        Id = 2,
        NumeroCaso = "CASP2609",
        CreadoEn = "2026-09-05",
        EstadoRecomendacion = "completa",
        EstadoMarcadoPor = 1,
        EstadoMarcadoEn = "2026-09-05T10:00:00",
        EstadoMarcadoOrigen = @"C:\Users\josem\Documents\Fichas\paquetes\vuelta-Sandy.xlsx",
        EstadoDelCompanero = "completa",
        EstadoDelCompaneroPor = 1,
        EstadoDelCompaneroEn = "2026-09-05T10:00:00",
    };

    /// <summary>Un caso marcado a mano en la pantalla de Revisar.</summary>
    private static Caso AManoEnRevisar() => new()
    {
        Id = 3,
        NumeroCaso = "CASP2609",
        CreadoEn = "2026-09-05",
        EstadoRecomendacion = "completa",
        EstadoMarcadoPor = 1,
        EstadoMarcadoEn = "2026-09-05T10:00:00",
        EstadoMarcadoOrigen = "a mano en la pantalla Revisar",
    };

    // ---- lo que pidio el dueno con sus palabras -------------------------

    /// <summary>
    /// Dado un caso completado por el atajo, cuando se lee la frase, entonces dice
    /// «el administrador lo hizo».
    /// </summary>
    [TestMethod]
    public void ElAtajoDelAdministradorSeLeeConSusPalabras()
    {
        var frase = TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), "Miguel");
        Assert.Contains("el administrador lo hizo", frase, StringComparison.Ordinal);
    }

    /// <summary>Y dice de quien fue: la frase lleva su nombre.</summary>
    /// <remarks>
    /// Sin nombre, «el administrador lo hizo» no distingue quien lo hizo el dia que haya
    /// dos administradores en la historia de la base, y ese dato ya esta en
    /// <c>estado_marcado_por</c>.
    /// </remarks>
    [TestMethod]
    public void ElAtajoDiceQuienFue()
        => Assert.Contains("Miguel", TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), "Miguel"), StringComparison.Ordinal);

    /// <summary>Y dice que fue SIN verificar: es lo que lo separa de la firma de campos.</summary>
    [TestMethod]
    public void ElAtajoDiceQueFueSinVerificar()
        => Assert.Contains("sin verificar", TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), "Miguel"), StringComparison.OrdinalIgnoreCase);

    // ---- que no se confunden entre si ------------------------------------

    /// <summary>Lo del Excel del companero NO se lee como el atajo del administrador.</summary>
    [TestMethod]
    public void LoDelExcelDelCompaneroNoDiceQueLoHizoElAdministrador()
    {
        var frase = TextoDeLaMarcaDelEstado.Componer(PorElExcelDelCompanero(), "Sandy");

        Assert.DoesNotContain("administrador", frase, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sandy", frase, StringComparison.Ordinal);
    }

    /// <summary>Las tres frases son tres frases distintas, y ninguna esta vacia.</summary>
    /// <remarks>
    /// Es el criterio entero: «son tres cosas y tienen que distinguirse a simple vista».
    /// Dos frases iguales para dos hechos distintos serian la mentira que hay que cazar.
    /// </remarks>
    [TestMethod]
    public void LasTresSeDistinguenAsimpleVista()
    {
        var frases = new[]
        {
            TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), "Miguel"),
            TextoDeLaMarcaDelEstado.Componer(PorElExcelDelCompanero(), "Sandy"),
            TextoDeLaMarcaDelEstado.Componer(AManoEnRevisar(), "Sandy"),
        };

        foreach (var frase in frases) Assert.IsFalse(string.IsNullOrWhiteSpace(frase));
        Assert.AreEqual(3, frases.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- los bordes -------------------------------------------------------

    /// <summary>Un caso que nadie marco no dice nada: callar aqui es la verdad.</summary>
    /// <remarks>
    /// Una frase inventada sobre un caso sin marcar se leeria como un estado que nadie
    /// puso. Quien dice si el companero contesto o no es la linea del desplegable, que ya
    /// lo dice con palabras.
    /// </remarks>
    [TestMethod]
    public void UnCasoSinMarcarNoDiceNada()
    {
        var sinMarcar = new Caso { Id = 4, CreadoEn = "2026-09-05" };
        Assert.AreEqual(string.Empty, TextoDeLaMarcaDelEstado.Componer(sinMarcar, null));
    }

    /// <summary>Sin nombre de quien marco, la frase sale igual y no inventa uno.</summary>
    /// <remarks>
    /// Pasa cuando el companero se borro de la lista o el id no resuelve. La marca sigue
    /// siendo cierta; lo que no se puede es rellenar el hueco con el primero que haya.
    /// </remarks>
    [TestMethod]
    public void SinNombreLaFraseSaleIgualYNoSeInventaUno()
    {
        var frase = TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), null);

        Assert.Contains("el administrador lo hizo", frase, StringComparison.Ordinal);
        Assert.DoesNotContain("Miguel", frase, StringComparison.Ordinal);
    }

    /// <summary>Un caso marcado NO completa tambien se lee, y dice que no esta completa.</summary>
    /// <remarks>
    /// El atajo del administrador solo escribe «completa», pero el Excel del companero
    /// escribe las dos, y esta frase la lee la misma pantalla.
    /// </remarks>
    [TestMethod]
    public void UnCasoNoCompletoSeLeeComoNoCompleto()
    {
        var noCompleta = PorElExcelDelCompanero() with { EstadoRecomendacion = "no_completa" };
        var frase = TextoDeLaMarcaDelEstado.Componer(noCompleta, "Sandy");

        Assert.Contains("no está completa", frase, StringComparison.Ordinal);
    }

    /// <summary>La frase cabe en un renglon: es requisito 4 del dueno, «ni un parrafo».</summary>
    [TestMethod]
    public void LaFraseCabeEnUnRenglon()
    {
        Assert.IsLessThanOrEqualTo(
            TextoDeLaMarcaDelEstado.LargoMaximoDeLaLinea,
            TextoDeLaMarcaDelEstado.Componer(PorElAdministrador(), "José Miguel Anonimo").Length);
    }
}
