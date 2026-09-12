using Fichas.App.Inicio;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// «0 de 0 confirmadas» pintado en ROJO: el defecto que el dueño vio y nombró el 2026-09-07.
/// </summary>
/// <remarks>
/// <para><b>Qué pasaba, medido.</b> <c>PinturaDeInicio.FondoDeLaPastilla</c> tenía dos
/// respuestas —verde si todas las personas del grupo estaban confirmadas, rojo si no— y decidía
/// con <c>EstaTodoCompleto</c>, que exige <c>cuantosDocumentos &gt; 0</c>. Con cero personas
/// leídas esa condición es falsa, así que el falso caía directo en el rojo: una unidad de la que
/// el lector no sacó a nadie salía en rojo diciendo «0 de 0 confirmadas».</para>
///
/// <para><b>Por qué no puede ser rojo.</b> El rojo de esta pantalla significa una cosa
/// concreta: alguien de ese grupo va a viajar sin la recomendación confirmada. Con cero
/// personas leídas no va a viajar nadie, así que el rojo afirma un riesgo que no existe — y un
/// rojo que salta cuando no pasa nada es un rojo que se deja de mirar, que es justo lo que este
/// programa no se puede permitir.</para>
///
/// <para><b>Por qué tampoco verde.</b> Nadie ha dicho que ese grupo esté resuelto: lo que pasa
/// es que no hay a quién preguntar. Verde sería afirmar lo contrario del rojo, y las dos
/// afirmaciones son falsas.</para>
///
/// <para><b>Gris, y con su palabra al lado.</b> El gris es el único color de la paleta que dice
/// «esto todavía no contesta esa pregunta». Y el color no va solo (mockup v2): la etiqueta dice
/// «me falta», porque al dueño sí le queda algo que hacer —conseguir que se lean esas
/// personas—, y el detalle lo explica.</para>
///
/// <para>⚠️ <b>Se mide la DECISIÓN y no el pincel</b>, y no es una comodidad:
/// <c>PinturaDeInicio</c> construye <c>SolidColorBrush</c>, que no existe fuera del tiempo de
/// ejecución de XAML, y tocar ese tipo desde una prueba lanza un <c>COMException</c> antes de
/// llegar a la regla — medido al escribir esta clase—. Por eso la regla vive en
/// <c>ColoresDeLaPastilla</c>, en <c>ModelosDeInicio.cs</c>, y el pincel solo la traduce a un
/// color en un <c>switch</c> de tres ramas que no decide nada. Es la misma disciplina del
/// ADR-0003 §8.1 que ya sostiene el resto de esta pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelColorDeLoQueNoTieneANadie
{
    /// <summary>Un grupo sin ninguna persona leída ya NO se pinta de rojo.</summary>
    [TestMethod]
    public void UnGrupoSinNingunaPersonaLeidaNoSePintaDeRojo()
    {
        var sinNadie = ColoresDeLaPastilla.De(cuantasResueltas: 0, cuantasPersonas: 0);
        var conGenteSinResolver = ColoresDeLaPastilla.De(cuantasResueltas: 0, cuantasPersonas: 3);

        Console.WriteLine($"sin nadie: {sinNadie}   ·   con 3 personas sin resolver: {conGenteSinResolver}");

        Assert.AreEqual(ColorDeLaPastilla.Rojo, conGenteSinResolver, "Con gente sin resolver, el rojo se queda.");
        Assert.AreNotEqual(
            ColorDeLaPastilla.Rojo, sinNadie, "Cero de cero no puede ser rojo: no hay a quién confirmar.");
    }

    /// <summary>Y tampoco de verde: nadie ha dicho que esté resuelto.</summary>
    [TestMethod]
    public void UnGrupoSinNingunaPersonaLeidaTampocoSePintaDeVerde()
    {
        var sinNadie = ColoresDeLaPastilla.De(0, 0);

        Console.WriteLine($"sin nadie: {sinNadie}   ·   todas resueltas: {ColoresDeLaPastilla.De(3, 3)}");

        Assert.AreNotEqual(
            ColorDeLaPastilla.Verde, sinNadie, "Verde sería afirmar que está resuelto, y nadie lo dijo.");
        Assert.AreEqual(ColorDeLaPastilla.Gris, sinNadie, "Gris: ni la alarma ni el visto bueno.");
    }

    /// <summary>El color de un grupo que sí tiene gente no cambia: verde o rojo, como siempre.</summary>
    /// <remarks>
    /// Es la guarda del arreglo: si el gris se hubiera comido también los grupos con gente, la
    /// pantalla habría dejado de avisar de lo único de lo que avisa.
    /// </remarks>
    [TestMethod]
    public void ElColorDeUnGrupoConGenteNoCambia()
    {
        Assert.AreEqual(ColorDeLaPastilla.Verde, ColoresDeLaPastilla.De(4, 4), "Todas resueltas: verde.");
        Assert.AreEqual(
            ColorDeLaPastilla.Rojo,
            ColoresDeLaPastilla.De(3, 4),
            "Falta una: rojo, y esa es exactamente la que no podría entrar al templo.");
    }

    /// <summary>El color no va solo: su etiqueta dice «me falta» y NO «0 de 0 confirmadas».</summary>
    /// <remarks>
    /// Es la mitad que hace que el gris se entienda. Un gris mudo se lee como «esto está
    /// apagado»; con la palabra al lado se lee como lo que es.
    /// </remarks>
    [TestMethod]
    public void LaPastillaSinNadieDiceMeFaltaYNoLaCifraQueNoDiceNada()
    {
        var pastilla = new PastillaDeDia(
            "9999999", "Unidad sin nadie leído",
            CuantosDocumentos: 2,
            CuantasPersonas: 0,
            CuantasCompletas: 0,
            Motivo: MotivoDeNoCompletar.SinMotivo);

        Console.WriteLine($"etiqueta: «{pastilla.Etiqueta}»   color: {pastilla.Color}");
        Console.WriteLine($"en voz alta: «{pastilla.ParaElLector}»");

        Assert.AreEqual(DosEstados.MeFalta, pastilla.Etiqueta);
        Assert.IsFalse(pastilla.Etiqueta.Contains("0 de 0", StringComparison.Ordinal));
        Assert.AreEqual(ColorDeLaPastilla.Gris, pastilla.Color);
        Assert.IsTrue(pastilla.SinNadieALaVista);

        // Y en voz alta se explica, porque quien no ve la pantalla no ve el gris.
        StringAssert.Contains(pastilla.ParaElLector, "sin ninguna persona leída");
        StringAssert.Contains(pastilla.ParaElLector, "no hay a quién confirmar");
    }

    /// <summary>Un grupo archivado va en VERDE aunque sus seis preguntas no digan que sí.</summary>
    /// <remarks>
    /// Del dueño, 2026-09-07: <i>«aunque se archive, debe quedarse en el calendario marcado en
    /// verde»</i>. Por eso el color mira <c>CuantasPersonasResueltas</c> y no
    /// <c>CuantasPersonasConfirmadas</c>: «confirmada» es una afirmación sobre las seis
    /// preguntas y no se toca; «resuelta» es si al dueño le queda algo que hacer.
    /// </remarks>
    [TestMethod]
    public void UnGrupoArchivadoVaEnVerdeAunqueNadieHayaConfirmadoSusSeisPreguntas()
    {
        var archivado = new PastillaDeDia(
            "9999999", "Unidad archivada",
            CuantosDocumentos: 1,
            CuantasPersonas: 3,
            CuantasCompletas: 0,
            Motivo: MotivoDeNoCompletar.SinMotivo,
            CuantasPersonasConfirmadas: 0,
            CuantasPersonasResueltas: 3);

        Console.WriteLine($"archivado: etiqueta «{archivado.Etiqueta}»   color: {archivado.Color}");

        Assert.AreEqual(ColorDeLaPastilla.Verde, archivado.Color);
        Assert.AreEqual(DosEstados.Resuelto, archivado.Etiqueta);
        Assert.AreEqual(
            0,
            archivado.CuantasPersonasConfirmadas,
            "Y sin decir que sus seis preguntas dicen que sí, que sería inventarlo.");
    }
}
