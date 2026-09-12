using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Vocabulario;

/// <summary>
/// Las DOS palabras que el dueno acepta ver, y de donde sale cada una.
/// </summary>
/// <remarks>
/// <para>Estas pruebas salen del CRITERIO y no del codigo: la decision del dueno del
/// 2026-09-07 dice <i>«Dos estados nada mas: resuelto y me falta»</i>, y define resuelto
/// como <i>«que no queda nada que el tenga que hacer con eso»</i>. Cada prueba de aqui es
/// una lectura de esa frase, no una foto de lo que el programa hacia.</para>
///
/// <para>⚠️ <b>Lo que NO se colapsa.</b> Las cuatro columnas de la base siguen siendo cuatro;
/// lo que se colapsa es la palabra. Que sigan distinguiendose lo vigila
/// <c>PruebasDeQueLaBaseSigueDiciendoQuienLoDijo</c>, que mira la BASE y no la pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosDosEstados
{
    /// <summary>Las palabras de estado que este pase retira de la pantalla.</summary>
    /// <remarks>
    /// Estan aqui escritas para que la prueba falle el dia que alguna vuelva a salir de este
    /// vocabulario. No es una lista de prohibidas en TODO el programa: los reportes de los
    /// jefes y el historico las conservan a proposito, porque esos no los lee el dueno.
    /// </remarks>
    private static readonly string[] LasQueSeRetiran =
    [
        "listo para asignar", "completa", "no está completa", "no completada",
        "sin revisar", "sin marcar", "confirmada", "lista para viajar",
        "fecha pasada completada",
    ];

    // ────────────────────────── las dos palabras y ninguna mas ──────────────────────────

    /// <summary>Vigila que de las lecturas salgan exactamente dos palabras: «resuelto» y «me falta».</summary>
    [TestMethod]
    public void SoloHayDosPalabrasDeEstado()
    {
        var todas = Enum.GetValues<LoQueSeLee>().Select(DosEstados.Palabra).Distinct().ToList();

        Assert.HasCount(2, todas);
        CollectionAssert.Contains(todas, "resuelto");
        CollectionAssert.Contains(todas, "me falta");
    }

    /// <summary>Vigila que ninguna de las palabras retiradas el 2026-09-07 vuelva a salir de este vocabulario.</summary>
    [TestMethod]
    public void NingunaDeLasPalabrasViejasSaleDeEsteVocabulario()
    {
        foreach (var palabra in Enum.GetValues<LoQueSeLee>().Select(DosEstados.Palabra))
        {
            foreach (var vieja in LasQueSeRetiran)
            {
                Assert.IsFalse(
                    palabra.Contains(vieja, StringComparison.OrdinalIgnoreCase),
                    $"«{palabra}» sigue llevando dentro la palabra retirada «{vieja}».");
            }
        }
    }

    // ────────────────────────── un documento: cuando esta resuelto ──────────────────────────

    /// <summary>El caso del criterio: el Excel del companero lo dio por completo → «resuelto».</summary>
    [TestMethod]
    public void LoQueElExcelDelCompaneroDioPorCompletoSeLeeResuelto()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.Completa,
            archivado: false,
            cuantoLeFalta: 0,
            sinNingunaPersonaLeida: false,
            quienLoLleva: "Sandy",
            firma: "Completada por Sandy · 2026-08-30");

        Assert.AreEqual(DosEstados.Resuelto, lectura.Palabra);
        Assert.IsTrue(lectura.EsResuelto);
    }

    /// <summary>El otro caso del criterio: lo que el dueno archivo, tambien «resuelto».</summary>
    /// <remarks>
    /// Archivar ES el gesto con el que el dueno dice que ya no tiene nada que hacer con ese
    /// documento (2026-09-06: <i>«si ya resolvi un archivo y lo archivo…»</i>). Por eso vale
    /// aunque a los campos les falte algo: quien lo cerro fue el.
    /// </remarks>
    [TestMethod]
    public void LoQueElDuenoArchivoSeLeeResueltoAunqueLeFaltenCampos()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar,
            archivado: true,
            cuantoLeFalta: 3,
            sinNingunaPersonaLeida: false,
            quienLoLleva: "",
            firma: "");

        Assert.AreEqual(DosEstados.Resuelto, lectura.Palabra);
    }

    /// <summary>Los dos caminos dan la MISMA palabra; es lo que el dueno pidio.</summary>
    [TestMethod]
    public void ElCompletadoPorElCompaneroYElArchivadoPorElDuenoDicenLaMisma()
    {
        var delCompanero = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.Completa, false, 0, false, "Sandy", "Completada por Sandy · 2026-08-30");
        var delDueno = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, true, 0, false, "", "", fechaDeArchivado: "2026-09-07");

        Assert.AreEqual(delCompanero.Palabra, delDueno.Palabra);
    }

    /// <summary>⚠️ Y aun diciendo la misma palabra, el DETALLE conserva quien lo dijo.</summary>
    /// <remarks>
    /// Es la mitad que sostiene la regla permanente 5. Si las dos lecturas dijeran lo mismo
    /// tambien por dentro, dejaria de poder saberse si aquello lo dio por bueno Sandy o lo
    /// cerro Miguel, que es la unica defensa del proyecto el dia que alguien no pueda entrar
    /// al templo.
    /// </remarks>
    [TestMethod]
    public void AunDiciendoLoMismoElDetalleNoConfundeAQuienLoDijo()
    {
        var delCompanero = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.Completa, false, 0, false, "Sandy", "Completada por Sandy · 2026-08-30");
        var delDueno = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar, true, 0, false, "", "", fechaDeArchivado: "2026-09-07");

        Assert.AreNotEqual(delCompanero.Detalle, delDueno.Detalle);
        StringAssert.Contains(delCompanero.Detalle, "Sandy");
        StringAssert.Contains(delDueno.Detalle, "archiv");
        StringAssert.Contains(delDueno.Detalle, "2026-09-07");
    }

    // ────────────────────────── un documento: cuando falta ──────────────────────────

    /// <summary>Vigila que un documento sin marcar y uno «no completa», ninguno archivado, se lean los dos «me falta».</summary>
    /// <param name="estado">El estado de la base con el que se prueba.</param>
    [TestMethod]
    [DataRow(EstadoDeRecomendacion.SinMarcar)]
    [DataRow(EstadoDeRecomendacion.NoCompleta)]
    public void LoQueNadieCerroSeLeeMeFalta(EstadoDeRecomendacion estado)
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(estado, false, 0, false, "", "");

        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
        Assert.IsFalse(lectura.EsResuelto);
    }

    /// <summary>«Me falta» SIEMPRE puede decir que falta y a quien le toca; nunca se queda mudo.</summary>
    [TestMethod]
    public void MeFaltaSiempreDiceQueFaltaYAQuienLeToca()
    {
        LoQueSeLeeDeUnDocumento[] todas =
        [
            LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 0, false, "", ""),
            LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 3, false, "", ""),
            LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 0, true, "", ""),
            LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.NoCompleta, false, 0, false, "Sandy", ""),
            LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.NoCompleta, false, 2, false, "", ""),
        ];

        foreach (var lectura in todas)
        {
            Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
            Assert.AreNotEqual(string.Empty, lectura.Detalle);
            StringAssert.Contains(lectura.Detalle, "toca");
        }
    }

    /// <summary>Lo que le falta al PAPEL le toca a el, y el detalle lo dice con su cifra.</summary>
    [TestMethod]
    public void CuandoFaltanCamposElDetalleDiceCuantosYQueLeTocaAEl()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 3, false, "", "");

        StringAssert.Contains(lectura.Detalle, "3");
        StringAssert.Contains(lectura.Detalle, "datos");
        StringAssert.Contains(lectura.Detalle, "te toca a ti");
    }

    /// <summary>Lo que lleva un companero le toca al companero, con su nombre.</summary>
    [TestMethod]
    public void CuandoLoLlevaUnCompaneroElDetalleLoNombra()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 0, false, "Sandy", "");

        StringAssert.Contains(lectura.Detalle, "Sandy");
    }

    /// <summary>Un documento del que no se leyo a nadie NO dice «le faltan 0 datos».</summary>
    [TestMethod]
    public void SinNingunaPersonaLeidaTieneSuPropioDetalle()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(EstadoDeRecomendacion.SinMarcar, false, 0, true, "", "");

        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
        Assert.IsFalse(lectura.Detalle.Contains("0 dato", StringComparison.Ordinal));
        StringAssert.Contains(lectura.Detalle, "persona");
    }

    /// <summary>El motivo que dijo el companero se conserva en el detalle, con su autor.</summary>
    [TestMethod]
    public void ElMotivoDelCompaneroSigueDiciendoQueFueElCompanero()
    {
        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.NoCompleta, false, 0, false, "", "", motivo: "el líder no lo hizo");

        StringAssert.Contains(lectura.Detalle, "el compañero dijo: el líder no lo hizo");
    }

    // ────────────────────────── una persona ──────────────────────────

    /// <summary>Vigila que una persona con las seis en «sí» se lea «resuelto».</summary>
    [TestMethod]
    public void UnaPersonaConLasSeisEnSiSeLeeResuelto()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: true, seQuedoEn: []);

        Assert.AreEqual(DosEstados.Resuelto, lectura.Palabra);
    }

    /// <summary>Vigila que una persona con un paso en «no» se lea «me falta» y el detalle nombre ese paso.</summary>
    [TestMethod]
    public void UnaPersonaConAlgunPasoEnNoSeLeeMeFaltaYDiceDondeSeQuedo()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(recomendacion: false, seQuedoEn: ["Entrevistas"]);

        Assert.AreEqual(DosEstados.MeFalta, lectura.Palabra);
        StringAssert.Contains(lectura.Detalle, "Entrevistas");
    }

    /// <summary>
    /// ⛔ «Sin mirar» NO es «no lista», y aunque las dos digan «me falta», el detalle no las
    /// confunde.
    /// </summary>
    /// <remarks>
    /// Lo dice <c>Pasos.Estado</c>: <i>«dar por lista a una persona de la que faltan preguntas
    /// por mirar es exactamente lo que manda a alguien al templo con la recomendacion mal»</i>.
    /// Una es una respuesta y la otra es la ausencia de respuesta.
    /// </remarks>
    [TestMethod]
    public void SinMirarYNoListaDicenLaMismaPalabraYDistintoDetalle()
    {
        var sinMirar = LoQueSeLeeDeUnaPersona.De(recomendacion: null, seQuedoEn: []);
        var noLista = LoQueSeLeeDeUnaPersona.De(recomendacion: false, seQuedoEn: ["Entrevistas"]);

        Assert.AreEqual(sinMirar.Palabra, noLista.Palabra);
        Assert.AreEqual(DosEstados.MeFalta, sinMirar.Palabra);
        Assert.AreNotEqual(sinMirar.Detalle, noLista.Detalle);
    }

    /// <summary>Los pasos se enumeran TODOS: el dueno llama al lider una vez.</summary>
    [TestMethod]
    public void SeEnumeranTodosLosPasosDondeSeQuedo()
    {
        var lectura = LoQueSeLeeDeUnaPersona.De(false, ["Entrevistas", "Preparación", "Cita del templo"]);

        StringAssert.Contains(lectura.Detalle, "Entrevistas, Preparación y Cita del templo");
    }

    // ────────────────────────── la cuenta de un grupo ──────────────────────────

    /// <summary>Vigila que siete de siete se diga «resuelto», sin cifra detrás.</summary>
    [TestMethod]
    public void UnGrupoConTodoResueltoDiceResueltoASecas()
        => Assert.AreEqual(DosEstados.Resuelto, DosEstados.Cuenta(7, 7));

    /// <summary>Vigila que cuatro de diez empiece por «me falta» y diga «6 de 10».</summary>
    [TestMethod]
    public void UnGrupoAMediasDiceCuantoLeFaltaConLaCifraDelante()
    {
        var texto = DosEstados.Cuenta(4, 10);

        StringAssert.StartsWith(texto, DosEstados.MeFalta);
        StringAssert.Contains(texto, "6 de 10");
    }

    /// <summary>⚠️ Cero de cero no es «resuelto»: nadie ha dicho nada de nadie.</summary>
    /// <remarks>
    /// Y tampoco lleva la cifra, que seria «me falta 0 de 0» — la cifra que el dueno vio
    /// pintada en rojo y que no dice nada. Que ademas deje de pintarse de rojo lo vigila
    /// <c>PruebasDelColorDeLoQueNoTieneANadie</c>.
    /// </remarks>
    [TestMethod]
    public void CeroDeCeroDiceMeFaltaSinCifra()
    {
        var texto = DosEstados.Cuenta(0, 0);

        Assert.AreEqual(DosEstados.MeFalta, texto);
        Assert.IsFalse(texto.Contains("0 de 0", StringComparison.Ordinal));
    }
}
