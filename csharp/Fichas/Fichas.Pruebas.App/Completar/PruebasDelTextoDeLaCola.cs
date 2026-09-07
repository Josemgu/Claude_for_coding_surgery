using Fichas.App.Completar;
using Fichas.App.Grupo;

namespace Fichas.Pruebas.App.Completar;

/// <summary>
/// Lo que la pestana de completar DICE, palabra por palabra.
/// </summary>
/// <remarks>
/// <para>Se prueba el texto y no el dibujo porque es el texto lo que el dueno lee. Su
/// primera frase del 2026-09-05 sobre el programa entero fue <i>«el programa es confuso,
/// muy confuso»</i>, y la causa medida en el ADR-0006 §1.3 es que la pantalla ensena una
/// pregunta y el lee la otra.</para>
///
/// <para>⛔ Por eso «listo para asignar» NUNCA sale a secas aqui: va siempre con lo que
/// significa —«el sistema llenó todos los campos»— tal como fija el criterio C17-1. «Listo»
/// a secas se lee como «listo para viajar», que es otra pregunta y la contesta otra
/// persona.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelTextoDeLaCola
{
    /// <summary>La cabecera dice cuantos quedan Y de cuantos salen; nunca una cifra suelta.</summary>
    [TestMethod]
    public void LaCabeceraDiceCuantosQuedanYDeCuantosSalen()
    {
        var linea = TextoDeLaCola.LineaDelDenominador(8, 24, 26);

        Assert.AreEqual(
            "8 documentos a los que les falta información · de 24 sin archivar, de 26 en la base",
            linea);
    }

    /// <summary>Uno solo se dice en singular; «1 documentos» delata que nadie leyo la pantalla.</summary>
    [TestMethod]
    public void UnSoloDocumentoSeDiceEnSingular()
    {
        Assert.AreEqual(
            "1 documento al que le falta información · de 3 sin archivar, de 4 en la base",
            TextoDeLaCola.LineaDelDenominador(1, 3, 4));
    }

    /// <summary>
    /// Al salir de la cola se dice «listo para asignar» CON su significado, y cuantos quedan.
    /// </summary>
    /// <remarks>
    /// La frase sale de <see cref="LasDosPreguntas"/> y no de un literal aqui: escrita en
    /// dos sitios, el dia que se cambie una quedan dos redacciones para la misma cosa.
    /// </remarks>
    [TestMethod]
    public void AlSalirDeLaColaSeDiceListoParaAsignarConSuSignificado()
    {
        var linea = TextoDeLaCola.AlSalirDeLaCola("CASP2609", 7);

        StringAssert.Contains(linea, LasDosPreguntas.ListoParaAsignarConSuSignificado);
        StringAssert.Contains(linea, "CASP2609");
        StringAssert.Contains(linea, "quedan 7");
        Assert.IsFalse(linea.Contains("verificado", StringComparison.OrdinalIgnoreCase),
            "La cola no verifica nada: la palabra no puede salir de aqui (regla permanente 5).");
    }

    /// <summary>El que sigue incompleto lo dice, y dice CUANTO le falta.</summary>
    [TestMethod]
    public void ElQueSigueIncompletoLoDiceYDiceCuantoLeFalta()
    {
        Assert.AreEqual(
            "CASP2609 sigue en la cola: le faltan 2 datos.",
            TextoDeLaCola.AlSeguirEnLaCola("CASP2609", 2));

        Assert.AreEqual(
            "CASP2609 sigue en la cola: le falta 1 dato.",
            TextoDeLaCola.AlSeguirEnLaCola("CASP2609", 1));
    }

    /// <summary>
    /// Cuando la cola se vacia, la pantalla lo DICE; no deja una lista en blanco.
    /// </summary>
    /// <remarks>
    /// Criterio del pase: <i>«Cuando la cola se vacía, lo dice y no deja al dueño mirando
    /// una lista en blanco sin explicación»</i>. Y lo que dice tiene que aclarar que
    /// «sin huecos» NO es «listo para viajar», que es la confusion de fondo del 2026-09-05.
    /// </remarks>
    [TestMethod]
    public void CuandoLaColaSeVaciaLaPantallaLoDiceYExplicaQueNoSignifica()
    {
        var texto = TextoDeLaCola.CuandoNoQuedaNada;

        StringAssert.Contains(texto, "No queda ningún documento");
        StringAssert.Contains(texto, LasDosPreguntas.ListaParaViajar,
            "Hay que decir lo que esto NO significa, o se lee como que ya pueden viajar.");

        // ⚠️ Concordancia, y sale de un defecto MEDIDO con la ventana abierta el 2026-09-06:
        // la frase decia «que estén lista para viajar». La constante está en singular porque
        // es el estado de UNA persona, así que lo que va delante tiene que ser singular.
        StringAssert.Contains(texto, "cada persona esté " + LasDosPreguntas.ListaParaViajar);
        Assert.IsFalse(texto.Contains("estén " + LasDosPreguntas.ListaParaViajar, StringComparison.Ordinal),
            "«estén lista para viajar» no concuerda: la constante es de una sola persona.");
    }

    /// <summary>Al vaciarse encadenando se dice cuantos se resolvieron en la vuelta.</summary>
    /// <remarks>
    /// Terminar sin cifra deja al dueno sin saber si trabajo tres documentos o treinta, que
    /// es lo unico que puede comprobar de una sesion de trabajo.
    /// </remarks>
    [TestMethod]
    public void AlVaciarseEncadenandoSeDiceCuantosSeResolvieron()
    {
        Assert.AreEqual(
            "Se resolvieron 3 documentos y la cola quedó vacía.",
            TextoDeLaCola.AlVaciarseLaCola(3));

        Assert.AreEqual(
            "Se resolvió 1 documento y la cola quedó vacía.",
            TextoDeLaCola.AlVaciarseLaCola(1));
    }
}
