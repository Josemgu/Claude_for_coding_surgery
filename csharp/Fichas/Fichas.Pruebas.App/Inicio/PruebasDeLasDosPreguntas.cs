using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.App.Inicio;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// FASE C17 — las dos palabras dejan de confundirse.
/// </summary>
/// <remarks>
/// <para>Sale de la primera frase del dueno el 2026-09-05: <i>«el programa es confuso, muy
/// confuso»</i>, y de la correccion del ADR-0006 §1.3: <b>no hay que rehacer «listo para
/// asignar»; hay que dejar de llamar a dos cosas con la misma palabra</b>.</para>
///
/// <para>Las dos preguntas son suyas y son del MISMO DIA:</para>
/// <list type="bullet">
///   <item><i>«Si el sistema escanea y verifica todos los campos sin mi intervención, debe
///   decir "listo para asignar"»</i> — la contesta el programa.</item>
///   <item><i>«No puede poner los PDF listos para viajar porque no se ha verificado la
///   recomendación en el sistema del obispo»</i> — la contesta una persona.</item>
/// </list>
///
/// <para>Se prueba SIN ABRIR VENTANA (ADR-0003 §8.1): las frases viven en
/// <see cref="LasDosPreguntas"/> y en los modelos, no dentro de un control de XAML.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasDosPreguntas
{
    /// <summary>
    /// C17-1. Donde pone «listo para asignar» sigue poniendo eso, y ademas dice QUE SIGNIFICA.
    /// </summary>
    /// <remarks>
    /// ⛔ Lo primero que comprueba esta prueba es que la frase NO SE HA QUITADO. El dueno la
    /// pidio expresamente el 2026-09-05 y quitarla le devolveria las miles de pulsaciones que
    /// este programa le quita.
    /// </remarks>
    [TestMethod]
    public void ListoParaAsignarSigueEstandoYAhoraDiceQueSignifica()
    {
        // Se comprueba contra lo que el modelo COMPONE y no contra la constante consigo
        // misma: comparar una constante con su propio texto no prueba nada, y el analizador
        // de pruebas lo rechaza con razón.
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SIGN2609", "2026-09-30");
        var renglon = BaseDeInicio.LeerInicio(servicios).Listos[0];

        Assert.AreEqual(LasDosPreguntas.ListoParaAsignar, renglon.LoQueFaltaTexto,
            "La frase de la pantalla y la constante tienen que ser la misma, o hay dos redacciones.");

        StringAssert.Contains(
            LasDosPreguntas.ListoParaAsignarConSuSignificado, "listo para asignar", StringComparison.Ordinal);
        StringAssert.Contains(
            LasDosPreguntas.ListoParaAsignarConSuSignificado,
            "el sistema llenó todos los campos",
            StringComparison.Ordinal);
        Assert.DoesNotContain("\n", LasDosPreguntas.ListoParaAsignarConSuSignificado, "Ni un párrafo: una línea.");
    }

    /// <summary>
    /// C17-1, la mitad que se ve. El acuse de Correccion dice la frase Y su significado.
    /// </summary>
    /// <remarks>
    /// Se comprueba aqui y no mirando la pantalla porque <see cref="TextoDelAcuse"/> vive
    /// fuera del XAML justamente para poder leerlo en una prueba.
    /// </remarks>
    [TestMethod]
    public void ElAcuseDeCorreccionDiceLaFraseYQueSignifica()
    {
        var listo = TextoDelAcuse.FraseDelDocumento(listo: true, camposQueLeFaltan: 0);

        StringAssert.Contains(listo, "listo para asignar", StringComparison.Ordinal);
        StringAssert.Contains(listo, "el sistema llenó todos los campos", StringComparison.Ordinal);
    }

    /// <summary>
    /// C17-2. Ni «listo» ni «lista» a secas: cada rotulo dice listo PARA QUE.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta prueba no puede recorrer el XAML</b> —no hay ventana— y por eso comprueba
    /// las frases que el programa compone, que son las que el dueno lee. El barrido del XAML
    /// va en la entrega, con su <c>grep</c> y su salida pegada.
    /// </remarks>
    [TestMethod]
    public void NingunaFraseDiceListoASecas()
    {
        string[] todas =
        [
            LasDosPreguntas.ListoParaAsignar,
            LasDosPreguntas.ListoParaAsignarEnCabecera,
            LasDosPreguntas.ListoParaAsignarConSuSignificado,
            LasDosPreguntas.ListoParaAsignarEnCabeceraConSuSignificado,
            LasDosPreguntas.Decir(true),
            LasDosPreguntas.Decir(false),
            LasDosPreguntas.Decir(null),
            LasDosPreguntas.FraseDeUnaPersona(true, []),
            LasDosPreguntas.FraseDeUnaPersona(false, ["Entrevistas"]),
            LasDosPreguntas.FraseDeUnaPersona(null, []),
        ];

        foreach (var frase in todas)
        {
            foreach (var suelta in new[] { "listo", "lista", "listos", "listas" })
            {
                Assert.IsFalse(
                    DiceLaPalabraSuelta(frase, suelta),
                    $"«{frase}» dice «{suelta}» sin decir para qué.");
            }
        }
    }

    /// <summary>
    /// C17-3. Mientras las seis no esten todas en si, se dice «recomendación sin confirmar».
    /// </summary>
    /// <remarks>
    /// La frase es del dueno, literal: <i>«la recomendación para el templo no está
    /// confirmada»</i>. Y las tres respuestas son distintas: «sin mirar» tambien lleva la
    /// recomendacion sin confirmar, porque nadie la ha mirado, pero NO se llama «no lista».
    /// </remarks>
    [TestMethod]
    public void LaRecomendacionSoloSeDiceConfirmadaConLasSeisEnSi()
    {
        Assert.AreEqual("recomendación confirmada", LasDosPreguntas.DecirLaRecomendacion(true));
        Assert.AreEqual("recomendación sin confirmar", LasDosPreguntas.DecirLaRecomendacion(false));
        Assert.AreEqual("recomendación sin confirmar", LasDosPreguntas.DecirLaRecomendacion(null));
    }

    /// <summary>
    /// Las dos preguntas no comparten ni una palabra que las pueda confundir.
    /// </summary>
    /// <remarks>
    /// Es el criterio del pase: <i>«en ninguna pantalla se pueden confundir las dos
    /// frases»</i>. La comprobacion es dura a proposito: la frase de una persona NO puede
    /// contener «asignar», y la del documento NO puede contener «viajar» ni «recomendación».
    /// </remarks>
    [TestMethod]
    public void LasDosFrasesNoSeParecenEnNada()
    {
        var delPrograma = LasDosPreguntas.ListoParaAsignarConSuSignificado;
        var deLaPersona = LasDosPreguntas.FraseDeUnaPersona(false, ["Entrevistas"]);

        Assert.DoesNotContain("viajar", delPrograma);
        Assert.DoesNotContain("recomendación", delPrograma);
        Assert.DoesNotContain("obispo", delPrograma);

        Assert.DoesNotContain("asignar", deLaPersona);
        Assert.DoesNotContain("campos", deLaPersona);
    }

    /// <summary>
    /// C17-4. Nada cambia de comportamiento: «listo para asignar» sigue saliendo de los
    /// campos y de nada mas.
    /// </summary>
    /// <remarks>
    /// Una persona con las seis preguntas en NO, en un documento sin ningun hueco, sigue
    /// contando como «listo para asignar»: son dos preguntas distintas y una no manda sobre
    /// la otra. Si esta prueba se pusiera roja, seria que se mezclaron.
    /// </remarks>
    [TestMethod]
    public void LasSeisPreguntasNoCambianLoQueEstaListoParaAsignar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "MEZC2609", "2026-09-30", cuantasPersonas: 2);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 2);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.AreEqual(1, resumen.Contadores.ListoParaAsignar,
            "«Listo para asignar» mira NUESTROS campos; las seis preguntas no le tocan.");
        Assert.AreEqual("listo para asignar", resumen.Listos[0].LoQueFaltaTexto);
    }

    /// <summary>Si la frase dice la palabra suelta, sin decir para que.</summary>
    /// <remarks>
    /// «listo para asignar» y «lista para viajar» pasan; «listo» y «documentos listos» no.
    /// Se mira palabra a palabra para que «listos» dentro de otra palabra no cuente.
    /// </remarks>
    private static bool DiceLaPalabraSuelta(string frase, string palabra)
    {
        var palabras = frase.Split([' ', '·', ',', '.', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < palabras.Length; i++)
        {
            if (!palabras[i].Equals(palabra, StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < palabras.Length && palabras[i + 1].Equals("para", StringComparison.OrdinalIgnoreCase)) continue;
            return true;
        }

        return false;
    }
}
