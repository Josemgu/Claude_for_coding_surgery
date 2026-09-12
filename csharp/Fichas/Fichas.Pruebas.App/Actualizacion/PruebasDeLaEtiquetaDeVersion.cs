using Fichas.App.Actualizacion;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// La etiqueta del Release («v12») se compara con la versión del programa («11») como
/// números, no como texto.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> DECISIONES.md, 2026-09-11, «EL DUEÑO PIDE QUE EL
/// PROGRAMA SE ACTUALICE SOLO»: el programa «compara la etiqueta <c>vN</c> con su
/// <c>&lt;Version&gt;</c>». Medido con <c>gh api …/releases/latest</c> el 2026-09-11: la
/// etiqueta es «v11» y el csproj dice «11».</para>
///
/// <para>Como texto, «v9» sería mayor que «v10» y el programa diría que hay versión nueva
/// cuando no la hay. Por eso se parte por puntos y se compara número a número.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaEtiquetaDeVersion
{
    /// <summary>Dado un Release con etiqueta mayor que la versión del programa, hay versión nueva.</summary>
    [TestMethod]
    [DataRow("v12", "11")]
    [DataRow("v10", "9")]
    [DataRow("v9.1", "9")]
    [DataRow("v11.0.1", "11")]
    [DataRow("12", "11")]
    public void UnaEtiquetaMayorEsUnaVersionNueva(string etiqueta, string actual)
        => Assert.IsTrue(EtiquetaDeVersion.EsMasNuevaQue(etiqueta, actual), $"«{etiqueta}» debería ser más nueva que «{actual}».");

    /// <summary>Dado un Release con la misma etiqueta o una menor, no hay nada nuevo.</summary>
    [TestMethod]
    [DataRow("v11", "11")]
    [DataRow("v10", "11")]
    [DataRow("v9", "10")]
    [DataRow("v11.0", "11")]
    [DataRow("v11", "11.1")]
    public void UnaEtiquetaIgualOMenorNoEsNada(string etiqueta, string actual)
        => Assert.IsFalse(EtiquetaDeVersion.EsMasNuevaQue(etiqueta, actual), $"«{etiqueta}» no debería ser más nueva que «{actual}».");

    /// <summary>Una etiqueta que no es un número no se interpreta: no hay versión nueva y se dice.</summary>
    /// <remarks>Regla permanente 1 llevada aquí: no se adivina un número que no está.</remarks>
    [TestMethod]
    [DataRow("beta")]
    [DataRow("")]
    [DataRow("v")]
    [DataRow("v11-ensayo")]
    public void UnaEtiquetaQueNoEsNumeroNoSeInterpreta(string etiqueta)
    {
        Assert.IsFalse(EtiquetaDeVersion.SeEntiende(etiqueta));
        Assert.IsFalse(EtiquetaDeVersion.EsMasNuevaQue(etiqueta, "11"));
    }

    /// <summary>Se limpia la «v» y los espacios: «v12» y « V12 » son la misma etiqueta.</summary>
    [TestMethod]
    public void LaVeYLosEspaciosNoCuentan()
    {
        Assert.AreEqual("12", EtiquetaDeVersion.SoloElNumero(" V12 "));
        Assert.AreEqual("12", EtiquetaDeVersion.SoloElNumero("v12"));
        Assert.AreEqual("12", EtiquetaDeVersion.SoloElNumero("12"));
    }
}
