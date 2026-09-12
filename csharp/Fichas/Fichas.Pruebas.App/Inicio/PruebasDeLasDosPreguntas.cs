using Fichas.App.Vocabulario;
using Fichas.App.Correccion;
using Fichas.App.Grupo;

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
    /// C17-1. El renglon dice una de las dos palabras del dueno, y no el veredicto viejo.
    /// </summary>
    /// <remarks>
    /// ⛔ 2026-09-07: el RENGLÓN ya no dice «listo para asignar» —dice una de las dos
    /// palabras—. La frase larga «listo para asignar · el sistema llenó todos los campos» se
    /// retiro del programa ese dia, y en la limpieza de codigo muerto del 2026-09-11 se quito
    /// tambien la constante que la componia, que ya no leia ninguna pantalla.
    /// </remarks>
    [TestMethod]
    public void ElRenglonDiceUnaDeLasDosPalabrasYNoElVeredictoViejo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SIGN2609", "2026-09-30");
        var renglon = BaseDeInicio.LeerInicio(servicios).Listos[0];

        Assert.AreNotEqual("listo para asignar", renglon.PalabraDelEstado,
            "El renglón dice una de las dos palabras del dueño, no el veredicto viejo.");
        CollectionAssert.Contains(DosEstados.LasDos.ToList(), renglon.PalabraDelEstado);
    }

    /// <summary>
    /// C17-1, la mitad que se ve. El acuse de Correccion dice la palabra Y qué falta.
    /// </summary>
    /// <remarks>
    /// <para>⛔ 2026-09-07: el acuse decía «listo para asignar · el sistema llenó todos los
    /// campos». Lo que este criterio exige —que la palabra NUNCA salga sola, porque «listo» a
    /// secas se lee como «listo para viajar»— sigue exigido y ahora se cumple con las dos
    /// palabras del dueño: nunca salen sin su detalle detrás.</para>
    ///
    /// <para>Se comprueba aqui y no mirando la pantalla porque <see cref="TextoDelAcuse"/> vive
    /// fuera del XAML justamente para poder leerlo en una prueba.</para>
    /// </remarks>
    [TestMethod]
    public void ElAcuseDeCorreccionDiceLaPalabraYNuncaLaDiceSola()
    {
        var sinHuecos = TextoDelAcuse.FraseDelDocumento(listo: true, camposQueLeFaltan: 0);
        var conHuecos = TextoDelAcuse.FraseDelDocumento(listo: false, camposQueLeFaltan: 3);

        foreach (var frase in (string[])[sinHuecos, conHuecos])
        {
            var palabra = DosEstados.LasDos.Single(dos => frase.StartsWith(dos, StringComparison.Ordinal));
            Assert.AreNotEqual(palabra, frase, "La palabra nunca sale sola: siempre con su detalle.");
            StringAssert.Contains(frase, "toca");
        }

        StringAssert.Contains(sinHuecos, "repartirlo", StringComparison.Ordinal);
        StringAssert.Contains(conHuecos, "le faltan 3 datos", StringComparison.Ordinal);
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
            LasDosPreguntas.LoQueLeFaltaAlDocumento(0, sinNingunaPersonaLeida: false),
            LasDosPreguntas.LoQueLeFaltaAlDocumento(3, sinNingunaPersonaLeida: false),
            LasDosPreguntas.LoQueLeFaltaAlDocumento(0, sinNingunaPersonaLeida: true),
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
        var delPrograma = LasDosPreguntas.LoQueLeFaltaAlDocumento(3, sinNingunaPersonaLeida: false);
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

        // ⚠️ ESTA es la línea que sostiene la prueba y no cambia: la CIFRA de lo que se puede
        // repartir mira nuestros campos, y las seis preguntas del sistema del líder no le
        // tocan. Que la palabra del renglón se haya colapsado a dos el 2026-09-07 no mezcla
        // nada, y por eso se comprueba además que el detalle no las confunda.
        Assert.AreEqual(1, resumen.Contadores.ListoParaAsignar,
            "«Listo para asignar» mira NUESTROS campos; las seis preguntas no le tocan.");
        Assert.AreEqual(0, resumen.Listos[0].CuantoLeFalta, "Al papel no le falta ningún dato.");
        Assert.IsFalse(
            resumen.Listos[0].DetalleDelEstado.Contains("líder", StringComparison.Ordinal),
            "Lo que le falta al papel no puede explicarse con las seis preguntas del líder.");
    }

    /// <summary>Si la frase dice la palabra suelta, sin decir para que.</summary>
    /// <remarks>
    /// «listo para asignar» y «lista para viajar» pasan; «listo» y «documentos listos» no.
    /// Se mira palabra a palabra para que «listos» dentro de otra palabra no cuente.
    /// </remarks>
    /// <param name="frase">El texto que se mira.</param>
    /// <param name="palabra">La palabra que no puede ir sola, sin distinguir mayúsculas.</param>
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
