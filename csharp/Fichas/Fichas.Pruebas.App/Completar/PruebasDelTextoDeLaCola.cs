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
    /// Al salir de la cola se dice QUE PASO, con el numero y cuantos quedan.
    /// </summary>
    /// <remarks>
    /// <para>⛔ 2026-09-07: esta linea decia «listo para asignar · el sistema llenó todos los
    /// campos». Esa era una de las cuatro palabras que el dueño retiró.</para>
    ///
    /// <para><b>Lo que la prueba defiende no cambia:</b> que la linea diga de qué documento
    /// habla, cuántos quedan, y que NO diga «verificado» — la firma es de Miguel y sigue sin
    /// ser automática (regla permanente 5). Y ahora ademas se comprueba que no haya vuelto
    /// ninguna de las palabras retiradas.</para>
    /// </remarks>
    [TestMethod]
    public void AlSalirDeLaColaSeDiceQuePasoSinNingunaPalabraRetirada()
    {
        var linea = TextoDeLaCola.AlSalirDeLaCola("CASP2609", 7);

        StringAssert.Contains(linea, "ya no le falta información");
        StringAssert.Contains(linea, "CASP2609");
        StringAssert.Contains(linea, "quedan 7");
        Assert.IsFalse(linea.Contains("verificado", StringComparison.OrdinalIgnoreCase),
            "La cola no verifica nada: la palabra no puede salir de aqui (regla permanente 5).");
        Assert.IsFalse(linea.Contains(LasDosPreguntas.ListoParaAsignar, StringComparison.OrdinalIgnoreCase),
            "«Listo para asignar» se retiró de la pantalla el 2026-09-07.");
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
        // ⚠️ ESTA es la mitad que sostiene la prueba y no cambia: hay que decir lo que esto NO
        // significa, o «no queda nada» se lee como «ya pueden viajar», que es la confusión de
        // fondo del 2026-09-05. Lo que cambió el 2026-09-07 son las palabras: «lista para
        // viajar» era una de las cuatro retiradas, y la frase lo dice ahora sin ella.
        StringAssert.Contains(texto, "no quiere decir que cada persona pueda viajar");
        StringAssert.Contains(texto, "la recomendación se confirma en el sistema del obispo");

        // La concordancia sigue vigilada, y sale de un defecto MEDIDO con la ventana abierta
        // el 2026-09-06: la frase llegó a decir «que estén lista para viajar».
        Assert.IsFalse(texto.Contains("estén ", StringComparison.Ordinal),
            "La frase habla de «cada persona», en singular: un plural aquí no concuerda.");
        Assert.IsFalse(texto.Contains(LasDosPreguntas.ListaParaViajar, StringComparison.Ordinal),
            "«Lista para viajar» se retiró de la pantalla el 2026-09-07.");
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
