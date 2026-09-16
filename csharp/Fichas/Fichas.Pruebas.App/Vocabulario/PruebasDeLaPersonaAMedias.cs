using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Vocabulario;

/// <summary>
/// Una persona con alguna de las seis en «sí» pero no las seis está A MEDIAS: sigue diciendo
/// «me falta», y el detalle dice cuántas le faltan y cuáles.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-16:</b> <i>«Las personas que se han completado, por
/// ejemplo 4 preguntas de las 6, deben pasar a color naranja e indicar que le falta»</i>.</para>
///
/// <para><b>Lo medido antes de tocar nada:</b> <c>LoQueSeLeeDeUnaPersona.De(null, [])</c> —que
/// es lo que recibía una persona con 4 en «sí» y 2 en blanco— decía <i>«nadie ha contestado sus
/// seis preguntas»</i>. Cuatro sí contestadas y el programa decía que nadie había contestado
/// nada: esa frase era falsa.</para>
///
/// <para>⛔ <b>Sigue habiendo DOS palabras.</b> «A medias» es un matiz de «me falta», igual que
/// «archivado» es una nota al lado de «resuelto» (2026-09-14): la palabra no cambia, cambia el
/// color y lo que dice el detalle. Estas pruebas se escribieron ANTES del código y salieron
/// rojas al escribirlas (el registro <c>LoQueSeLeeDeUnaPersona</c> no tenía <c>AMedias</c>).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaPersonaAMedias
{
    /// <summary>Los rótulos de los seis pasos, sin el número de delante, en su orden.</summary>
    private static readonly string[] LosSeis =
        ["Preparación", "Información", "Cita del templo", "Acciones requeridas", "Entrevistas", "Listo para el templo"];

    /// <summary>Cuatro en sí y dos en blanco: «me falta», a medias, y el detalle nombra las dos.</summary>
    [TestMethod]
    public void CuatroDeSeisEnSiSeLeeMeFaltaAMediasYNombraLasDosQueFaltan()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(
            recomendacion: null,
            seQuedoEn: [],
            lasQueNoDicenSi: ["Entrevistas", "Listo para el templo"]);

        Console.WriteLine($"«{lectura.Palabra}» · «{lectura.Detalle}»");
        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra, "La palabra sigue siendo una de las dos.");
        Assert.IsTrue(lectura.AMedias);
        Assert.IsTrue(lectura.EsMeFalta);
        StringAssert.Contains(lectura.Detalle, "le faltan 2 de 6: Entrevistas, Listo para el templo");
        Assert.IsFalse(
            lectura.Detalle.Contains("nadie ha contestado", StringComparison.Ordinal),
            "Cuatro contestadas no es «nadie ha contestado».");
    }

    /// <summary>Con las seis sin contestar no hay nada a medias: rojo, como hoy.</summary>
    [TestMethod]
    public void SeisEnBlancoNoEstaAMedias()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: null, seQuedoEn: [], lasQueNoDicenSi: LosSeis);

        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
        Assert.IsFalse(lectura.AMedias);
        StringAssert.Contains(lectura.Detalle, "nadie ha contestado");
    }

    /// <summary>Ninguna en sí y alguna en no tampoco está a medias: no se ha avanzado nada.</summary>
    [TestMethod]
    public void NingunaEnSiConAlgunaEnNoNoEstaAMedias()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: false, seQuedoEn: ["Preparación"], lasQueNoDicenSi: LosSeis);

        Assert.IsFalse(lectura.AMedias);
        StringAssert.Contains(lectura.Detalle, "se quedó en Preparación");
    }

    /// <summary>Las seis en sí: resuelto, y por tanto no a medias.</summary>
    [TestMethod]
    public void LasSeisEnSiNoEstaAMedias()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: true, seQuedoEn: [], lasQueNoDicenSi: []);

        Assert.AreEqual(DosEstados.Resuelto, lectura.Palabra);
        Assert.IsFalse(lectura.AMedias);
    }

    /// <summary>
    /// Cinco en sí y una en NO también está a medias: se avanzó cinco, y el detalle dice cuál
    /// falta Y que se quedó ahí, para que el dueño sepa que hay que llamar al líder.
    /// </summary>
    /// <remarks>
    /// Es la decisión que había que tomar y queda escrita: el naranja mide cuánto se AVANZÓ
    /// (cuántas dicen sí), no si alguna dice no. Lo que separa un «no» de un blanco no se
    /// pierde: sigue en el detalle, que es donde el 2026-09-07 dijo que se conserva.
    /// </remarks>
    [TestMethod]
    public void CincoEnSiYUnaEnNoEstaAMediasYDiceDondeSeQuedo()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(
            recomendacion: false, seQuedoEn: ["Entrevistas"], lasQueNoDicenSi: ["Entrevistas"]);

        Console.WriteLine($"«{lectura.Palabra}» · «{lectura.Detalle}»");
        Assert.IsTrue(lectura.AMedias);
        StringAssert.Contains(lectura.Detalle, "le falta 1 de 6: Entrevistas");
        StringAssert.Contains(lectura.Detalle, "se quedó en Entrevistas");
        StringAssert.Contains(lectura.Detalle, "le toca al líder");
    }

    /// <summary>A medias con solo blancos: le toca al dueño mirarlas, no al líder.</summary>
    [TestMethod]
    public void AMediasConSoloBlancosLeTocaAlDueno()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: null, seQuedoEn: [], lasQueNoDicenSi: ["Listo para el templo"]);

        StringAssert.Contains(lectura.Detalle, "te toca a ti");
        Assert.IsFalse(lectura.Detalle.Contains("le toca al líder", StringComparison.Ordinal));
    }

    /// <summary>
    /// Sin las seis a la vista no se puede saber si está a medias, y se lee como hasta hoy.
    /// </summary>
    /// <remarks>
    /// Es el camino de las pantallas que todavía no pasan las seis (los tickets de Inicio, la
    /// recomendación en Corrección): no se inventa un naranja, se queda en rojo con su frase.
    /// </remarks>
    [TestMethod]
    public void SinLasSeisALaVistaSeLeeComoHoy()
    {
        var deHoy = LoQueSeLeeDeUnaPersona.De(recomendacion: null, seQuedoEn: []);
        var conNo = LoQueSeLeeDeUnaPersona.De(recomendacion: false, seQuedoEn: ["Entrevistas"]);

        Assert.IsFalse(deHoy.AMedias);
        Assert.IsFalse(conNo.AMedias);
        StringAssert.Contains(deHoy.Detalle, "nadie ha contestado");
        StringAssert.Contains(conNo.Detalle, "se quedó en Entrevistas");
    }

    /// <summary>La nota «a medias» no es una tercera palabra: <c>LasDos</c> siguen siendo dos.</summary>
    [TestMethod]
    public void AMediasNoEsUnaTerceraPalabra()
    {
        Assert.HasCount(2, DosEstados.LasDos);
        Assert.IsFalse(DosEstados.LasDos.Contains(DosEstados.NotaDeAMedias), "«a medias» no es una palabra de estado.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(DosEstados.NotaDeAMedias));
    }

    /// <summary>El «6» de «le faltan 2 de 6» es el mismo seis de la ventana de las preguntas.</summary>
    [TestMethod]
    public void ElSeisDelDetalleEsElDeLasSeisPreguntas()
        => Assert.HasCount(LoQueSeLeeDeUnaPersona.LasSeis, PreguntasDeUnDocumento.Rotulos);

    // ────────────────────────── el documento ──────────────────────────

    /// <summary>
    /// Un documento «me falta» con una persona a medias está a medias, y su detalle la nombra
    /// con lo que le falta.
    /// </summary>
    [TestMethod]
    public void UnDocumentoConUnaPersonaAMediasEstaAMediasYLaNombra()
    {
        var ana = new PersonaLeida("Ana Pérez", LoQueSeLeeDeUnaPersona.De(null, [], ["Entrevistas", "Listo para el templo"]));
        var luis = new PersonaLeida("Luis Gómez", LoQueSeLeeDeUnaPersona.De(null, [], LosSeis));

        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, archivado: false, cuantoLeFalta: 0,
            sinNingunaPersonaLeida: false, quienLoLleva: string.Empty, firma: string.Empty,
            personas: [ana, luis]);

        Console.WriteLine($"«{lectura.Palabra}» · «{lectura.Detalle}»");
        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
        Assert.IsTrue(lectura.AMedias);
        StringAssert.Contains(lectura.Detalle, "Ana Pérez: le faltan 2 de 6: Entrevistas, Listo para el templo");
        StringAssert.Contains(lectura.Detalle, "Luis Gómez: sin contestar");
    }

    /// <summary>Un documento cuyas personas no tienen ninguna en sí no está a medias.</summary>
    [TestMethod]
    public void UnDocumentoSinNingunaEnSiNoEstaAMedias()
    {
        var luis = new PersonaLeida("Luis Gómez", LoQueSeLeeDeUnaPersona.De(null, [], LosSeis));

        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, false, 0, false, string.Empty, string.Empty, personas: [luis]);

        Assert.IsFalse(lectura.AMedias);
    }

    /// <summary>
    /// Una persona con las seis en sí y otra sin nada también es a medias: el documento avanzó
    /// y todavía le falta.
    /// </summary>
    [TestMethod]
    public void UnaPersonaResueltaYOtraSinNadaTambienEsAMedias()
    {
        var ana = new PersonaLeida("Ana Pérez", LoQueSeLeeDeUnaPersona.De(true, [], []));
        var luis = new PersonaLeida("Luis Gómez", LoQueSeLeeDeUnaPersona.De(null, [], LosSeis));

        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, false, 0, false, string.Empty, string.Empty, personas: [ana, luis]);

        Assert.IsTrue(lectura.AMedias);
        StringAssert.Contains(lectura.Detalle, "Ana Pérez: las seis en sí");
    }

    /// <summary>Resuelto —archivado o completo por el compañero— nunca está a medias.</summary>
    [TestMethod]
    public void LoResueltoNuncaEstaAMedias()
    {
        var ana = new PersonaLeida("Ana Pérez", LoQueSeLeeDeUnaPersona.De(null, [], ["Entrevistas"]));

        var archivado = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, archivado: true, 0, false, string.Empty, string.Empty, personas: [ana]);
        var completo = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.Completa, false, 0, false, string.Empty, "Completada por Sandy · 2026-08-30", personas: [ana]);

        Assert.IsFalse(archivado.AMedias);
        Assert.IsFalse(completo.AMedias);
        Assert.IsTrue(archivado.EsResuelto);
        Assert.IsTrue(completo.EsResuelto);
    }
}
