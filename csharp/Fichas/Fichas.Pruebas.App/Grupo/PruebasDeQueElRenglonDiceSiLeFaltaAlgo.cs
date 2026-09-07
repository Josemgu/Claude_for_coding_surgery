using Fichas.App.Grupo;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// El renglon del grupo DICE si al documento le falta algo, que hasta hoy no lo decia.
/// </summary>
/// <remarks>
/// <para><b>La queja del dueno, 2026-09-07:</b> <i>«Cuando guardo información ya corregida no
/// cambia de estado, sigue igual. Es algo que pedí arreglar»</i>. Y lo que pidio el 2026-09-05:
/// <i>«cuando yo revise un documento y coloque la información y diga que están bien todos los
/// campos y le dé a guardar, de manera automática debe decirme: este paquete está listo para
/// asignar»</i>.</para>
///
/// <para><b>Lo medido con la ventana abierta antes de tocar nada</b>, sobre el paquete
/// publicado y una base de 75 documentos:</para>
/// <list type="bullet">
///   <item><b>El veredicto estaba bien calculado.</b> Inicio decia «Listo para asignar · el
///   sistema llenó todos los campos · 71 · 71 documentos · 144 personas», y 71 son exactamente
///   los 75 menos los cuatro a los que les falta algo.</item>
///   <item><b>Y Correccion lo decia al guardar:</b> «Guardado a las 12:22 · sin cambios · este
///   documento ya está listo para asignar · el sistema llenó todos los campos».</item>
///   <item><b>Lo que no cambiaba era el renglon.</b> El del grupo decia
///   <c>«CASP2609 · sin mirar · recomendación sin confirmar · sin asignar · el PDF no está en
///   su ruta»</c> —y esa linea es la misma para un documento entero y para uno al que le
///   faltan tres campos—. <c>PersonaDelGrupo</c> ya calculaba <c>CuantoLeFalta</c> y hasta
///   tenia la frase en <c>LoQueFaltaTexto</c>, pero <c>RenglonDelGrupo</c> no se la llevaba a
///   la pantalla. O sea: no era el veredicto, era lo que se ve.</item>
/// </list>
///
/// <para>⛔ <b>Y sigue sin mezclarse con la otra pregunta.</b> «Listo para asignar» lo contesta
/// el programa mirando NUESTROS campos; «lista para viajar» lo contesta una persona mirando el
/// sistema del obispo. Van en dos sitios del renglon y con sus palabras, que es lo que
/// <see cref="LasDosPreguntas"/> existe para sostener.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueElRenglonDiceSiLeFaltaAlgo
{
    /// <summary>El grupo del dia tal como lo pinta la pantalla, ya aplanado en renglones.</summary>
    private static IReadOnlyList<RenglonDelGrupo> RenglonesDelDia(Fichas.Datos.Falso.ServiciosFalsos servicios)
        => BaseDeInicio.LectorDeGruposDe(servicios)
            .DelDia(new DateOnly(2026, 9, 17))
            .EnUnaSolaLista();

    [TestMethod]
    public void UnDocumentoAlQueNoLeFaltaNadaLoDiceEnSuRenglon()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 2);

        var personas = RenglonesDelDia(servicios).Where(r => r.EsUnaPersona).ToList();

        Assert.HasCount(2, personas);
        foreach (var renglon in personas)
        {
            Assert.AreEqual(
                LasDosPreguntas.ListoParaAsignarConSuSignificado,
                renglon.LoQueLeFaltaAlDocumento,
                "el renglón tiene que decir que a este documento ya no le falta nada");
        }
    }

    [TestMethod]
    public void UnDocumentoAlQueLeFaltaUnDatoLoDiceYDiceCuantos()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1, temploNombre: null);

        var renglon = RenglonesDelDia(servicios).First(r => r.EsUnaPersona);

        Assert.AreEqual("le falta 1 dato", renglon.LoQueLeFaltaAlDocumento);
    }

    [TestMethod]
    public void DosDatosQueFaltanSeDicenEnPlural()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(
            servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1, temploNombre: null, sinCedula: true);

        var renglon = RenglonesDelDia(servicios).First(r => r.EsUnaPersona);

        Assert.AreEqual("le faltan 2 datos", renglon.LoQueLeFaltaAlDocumento);
    }

    /// <summary>
    /// El documento del que no se leyo a nadie NO dice «le falta 1 dato»: dice lo que pasa.
    /// </summary>
    /// <remarks>
    /// <c>LoQueLeFalta</c> mete «sin ninguna persona leída» en la lista de lo que falta, asi
    /// que la cuenta seca diria «le falta 1 dato» de un campo que no existe. Es el mismo caso
    /// que el pie de Correccion ya trataba aparte desde el 2026-09-06.
    /// </remarks>
    [TestMethod]
    public void ElDocumentoSinNadieDiceQueNoHayAQuienRecomendar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ELTC2609", "2026-09-17", cuantasPersonas: 0);

        var renglon = RenglonesDelDia(servicios).First(r => r.EsUnaPersona);

        Assert.AreEqual("sin ninguna persona leída", renglon.Titulo);
        Assert.AreEqual(
            LasDosPreguntas.SinNingunaPersonaLeidaConSuSignificado,
            renglon.LoQueLeFaltaAlDocumento);
        StringAssert.Contains(renglon.LoQueLeFaltaAlDocumento, "no hay a quién recomendar");
    }

    /// <summary>Quien no ve la pantalla tambien lo oye; si no, la mitad del arreglo no existe.</summary>
    [TestMethod]
    public void LoQueLeFaltaTambienSeLeeEnVozAlta()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1, temploNombre: null);

        var renglon = RenglonesDelDia(servicios).First(r => r.EsUnaPersona);

        StringAssert.Contains(renglon.ParaElLector, "le falta 1 dato");
    }

    /// <summary>La cabecera de una unidad no contesta esa pregunta: no es un documento.</summary>
    [TestMethod]
    public void LaCabeceraDeUnaUnidadNoDiceNadaDeLoQueFalta()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);

        var cabecera = RenglonesDelDia(servicios).First(r => r.EsCabecera);

        Assert.AreEqual(string.Empty, cabecera.LoQueLeFaltaAlDocumento);
    }

    /// <summary>
    /// La frase se compone en UN solo sitio, que es el que ya usa la pantalla de Correccion.
    /// </summary>
    /// <remarks>
    /// Con dos composiciones, el mismo documento podria leerse «listo para asignar» en una
    /// pantalla y «le faltan 0 datos» en la otra. Es la misma disciplina que dejo escrita
    /// <c>LoQueLeFalta</c> el 2026-09-06 tras medir seis divergencias.
    /// </remarks>
    [TestMethod]
    public void LaFraseSaleDeLasDosPreguntasYNoDeCadaPantalla()
    {
        Assert.AreEqual(
            LasDosPreguntas.ListoParaAsignarConSuSignificado,
            LasDosPreguntas.LoQueLeFaltaAlDocumento(0, sinNingunaPersonaLeida: false));
        Assert.AreEqual(
            "le faltan 3 datos",
            LasDosPreguntas.LoQueLeFaltaAlDocumento(3, sinNingunaPersonaLeida: false));
        Assert.AreEqual(
            LasDosPreguntas.SinNingunaPersonaLeidaConSuSignificado,
            LasDosPreguntas.LoQueLeFaltaAlDocumento(1, sinNingunaPersonaLeida: true));
    }
}
