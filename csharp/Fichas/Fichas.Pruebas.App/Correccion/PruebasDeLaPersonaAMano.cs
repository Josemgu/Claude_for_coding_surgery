using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Completar;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Un documento del que no se leyo a NADIE deja de ser un callejon sin salida: Miguel
/// escribe a mano quien es y sigue.
/// </summary>
/// <remarks>
/// <para><b>La queja del dueno, 2026-09-07</b>, ensenando la pantalla del grupo de un dia
/// donde un renglon dice «sin ninguna persona leída · sin cédula leída»: <i>«¿Cómo voy a
/// confirmar la recomendación si no me da la opción?»</i>.</para>
///
/// <para><b>Lo medido antes de tocar nada</b>, con la ventana abierta sobre el paquete
/// publicado y una base de 75 documentos: al pulsar ese renglon, Correccion abre el
/// documento y su pie dice «Sin ninguna persona leída · no hay a quién recomendar · 0 dados
/// por buenos», y en toda la pantalla <b>no hay un solo boton</b> que permita decir quien
/// es. Sin persona no hay seis preguntas que contestar, asi que su recomendacion no se
/// puede confirmar nunca: el documento queda atascado para siempre.</para>
///
/// <para>⛔ <b>La linea que no se cruza, y es la regla permanente 1 de <c>CLAUDE.md</c>.</b>
/// Que Miguel ESCRIBA el nombre y la cedula esta bien y es lo que pide. Que el programa los
/// ADIVINE —del nombre del archivo, de otro documento, de donde sea— esta prohibido, y no es
/// formalismo: una cedula inventada manda a una persona al templo con la recomendacion
/// equivocada. Aqui no se propone nada: lo que entra es lo que una mano tecleo, y su
/// procedencia lo dice —<c>origen = manual</c>— para que nunca se confunda con lo que salio
/// del papel.</para>
///
/// <para>⛔ Y NO es una firma: <c>verificado</c> se queda en cero (regla permanente 5). Se
/// comprueba contando filas del almacen, no mirando la pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaPersonaAMano
{
    /// <summary>Un documento con sus cinco campos del caso y sin una sola persona.</summary>
    private static (ServiciosFalsos Servicios, ModeloDeCorreccion Modelo, long CasoId) DocumentoSinNadie()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "ELTC2609", "2026-09-17", cuantasPersonas: 0);
        var modelo = BancoDeLaCola.ModeloDe(servicios);
        modelo.Cargar(casoId);
        return (servicios, modelo, casoId);
    }

    [TestMethod]
    public void ElDocumentoSinNadieEmpiezaAtascado()
    {
        var (_, modelo, _) = DocumentoSinNadie();

        Assert.IsTrue(modelo.SinNingunaPersonaLeida);
        Assert.IsFalse(modelo.ListoParaAsignar, "sin nadie dentro no hay a quién recomendar");
        Assert.IsEmpty(modelo.Personas);
    }

    [TestMethod]
    public void EscribirNombreYCedulaDejaUnaPersonaEnLaBase()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        var resultado = modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        Assert.IsTrue(resultado.SeEscribio);
        var suyas = servicios.Personas.DeCaso(casoId);
        Assert.HasCount(1, suyas);
        Assert.AreEqual("Maria Clarisa Simulado", suyas[0].Nombre);
        Assert.AreEqual("055-1111-3853", suyas[0].Mrn);
        Assert.AreEqual(1, suyas[0].FilaFormulario, "la primera persona del papel es la fila 1");
    }

    /// <summary>De donde salio cada dato queda escrito, y dice MANO y no papel.</summary>
    [TestMethod]
    public void LoQueSeEscribeAManoQuedaConSuOrigen()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        var persona = servicios.Personas.DeCaso(casoId)[0];
        var filas = servicios.Procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id);

        Assert.HasCount(2, filas, "las dos columnas de la persona dejan su fila: nombre y cédula");
        foreach (var fila in filas)
        {
            Assert.AreEqual(OrigenDeCampo.Manual, fila.Origen, $"«{fila.Campo}» lo escribió una mano");
            Assert.IsNull(fila.ValorOcr, "de este campo no hay ninguna lectura: no se inventa una");
            Assert.IsNull(fila.Confianza, "una mano no tiene confianza de OCR");
        }
    }

    /// <summary>Regla permanente 5: escribir no es firmar, y esto se cuenta en el almacen.</summary>
    [TestMethod]
    public void AnadirUnaPersonaNoFirmaNiUnCampo()
    {
        var (servicios, modelo, _) = DocumentoSinNadie();
        var antes = BancoDeLaCola.CuantasFirmasHayEn(servicios);

        modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        Assert.AreEqual(antes, BancoDeLaCola.CuantasFirmasHayEn(servicios),
            "ni una firma nueva: la firma la pulsa Miguel campo por campo");
        Assert.AreEqual(0, BancoDeLaCola.CuantasFirmasHayEn(servicios));
    }

    /// <summary>Con la persona dentro, el documento sale del atasco y se puede seguir.</summary>
    [TestMethod]
    public void ConLaPersonaDentroElDocumentoYaNoEstaAtascado()
    {
        var (_, modelo, _) = DocumentoSinNadie();

        modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        Assert.IsFalse(modelo.SinNingunaPersonaLeida);
        Assert.IsTrue(modelo.ListoParaAsignar,
            "con los cinco campos del caso y una persona con nombre y cédula, no le falta nada");
        Assert.HasCount(1, modelo.LaRecomendacionDeCadaPersona,
            "ahora SI hay de quién confirmar la recomendación");
    }

    /// <summary>Los dos campos nuevos se pueden corregir como los demas: salen en la pantalla.</summary>
    [TestMethod]
    public void LosCamposDeLaPersonaNuevaSalenEnLaPantalla()
    {
        var (_, modelo, _) = DocumentoSinNadie();
        var antes = modelo.Campos.Count;

        modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        Assert.HasCount(antes + 2, modelo.Campos, "la cédula y el nombre de la persona nueva");
    }

    [TestMethod]
    public void SinNombreNoSeEscribeNadaYSeDicePorQue()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        var resultado = modelo.AnadirUnaPersonaAMano("   ", "055-1111-3853");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsEmpty(servicios.Personas.DeCaso(casoId), "no entra una persona sin nombre");
        Assert.IsNotEmpty(resultado.Avisos, "un botón que no hace nada y no explica por qué es un botón roto");
    }

    /// <summary>
    /// La cedula puede faltar: el nombre basta para que exista el ticket de esa persona.
    /// </summary>
    /// <remarks>
    /// Es la diferencia entre «atascado» y «incompleto». Sin cedula el documento sigue sin
    /// estar listo para asignar y lo dice —falta ese dato—, pero la persona ya existe, y de
    /// ella SI se pueden contestar las seis preguntas del sistema del obispo, que es lo que
    /// el dueno preguntaba que como iba a hacer.
    /// </remarks>
    [TestMethod]
    public void SinCedulaLaPersonaEntraIgualYLoQueFaltaSeSigueDiciendo()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        var resultado = modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", null);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.HasCount(1, servicios.Personas.DeCaso(casoId));
        Assert.IsFalse(modelo.SinNingunaPersonaLeida, "ya hay a quién recomendar");
        Assert.IsFalse(modelo.ListoParaAsignar, "pero le sigue faltando la cédula, y se dice");
        Assert.AreEqual(1, modelo.CamposQueLeFaltan);
    }

    /// <summary>
    /// Requisito 9 del dueno: avisar, nunca impedir. Una cedula rara ENTRA y queda senalada.
    /// </summary>
    /// <remarks>
    /// Es lo mismo que dice la migracion 17 al quitarle el <c>CHECK</c> a <c>personas.mrn</c>:
    /// «lo que un CHECK tira aqui no es un dato, es la fila de una persona».
    /// </remarks>
    [TestMethod]
    public void UnaCedulaConMalaFormaEntraYQuedaSenalada()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        var resultado = modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "123");

        Assert.IsTrue(resultado.SeEscribio, "el dato entra: avisar, nunca impedir");
        Assert.AreEqual("123", servicios.Personas.DeCaso(casoId)[0].Mrn);
        Assert.IsNotEmpty(resultado.Avisos, "y se dice que hay que mirarla");
        Assert.IsFalse(modelo.ListoParaAsignar, "una cédula que no cumple su forma sigue faltando");
    }

    /// <summary>Una segunda persona a mano se pone en la fila siguiente, no encima de la primera.</summary>
    [TestMethod]
    public void LaSegundaPersonaAManoVaEnLaFilaSiguiente()
    {
        var (servicios, modelo, casoId) = DocumentoSinNadie();

        modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");
        modelo.AnadirUnaPersonaAMano("Peter Alcindor", "055-1111-3854");

        var suyas = servicios.Personas.DeCaso(casoId);
        Assert.HasCount(2, suyas);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, suyas.Select(p => p.FilaFormulario ?? 0).ToArray());
    }

    /// <summary>Sin ningun caso abierto no se escribe nada y se dice; nunca se lanza.</summary>
    [TestMethod]
    public void SinCasoAbiertoNoSeEscribeNada()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var modelo = BancoDeLaCola.ModeloDe(servicios);

        var resultado = modelo.AnadirUnaPersonaAMano("Maria Clarisa Simulado", "055-1111-3853");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsNotEmpty(resultado.Avisos);
    }

    /// <summary>
    /// El programa NO propone el nombre que lleva el archivo, y esto lo vigila.
    /// </summary>
    /// <remarks>
    /// <para>El archivo del caso real del dueno se llama <c>ELTC2609_Maria_Clarisa_Simulado.pdf</c>,
    /// y de ahi se podria sacar un nombre con una regla. <b>No se hace</b>, y la decision se
    /// justifica en <see cref="TextoDeLaPersonaAMano"/>: entre los siete escaneos reales hay
    /// <c>SURB2609_Suriname_Group_Complete.pdf</c>, del que esa misma regla propondria una
    /// persona llamada «Suriname Group Complete», y
    /// <c>CASP2609_Daniel_Jr._Damian_Dorian_Ejemplo.pdf</c>, del que no se sabe si es una
    /// persona o cuatro. Una propuesta puesta dentro de la casilla se acepta por cansancio, y
    /// la cedula —que es la que hace dano— no esta en el archivo de todas formas.</para>
    /// <para>Lo que si se hace es <b>ENSENAR</b> como se llama el archivo al lado de la
    /// casilla, que es evidencia y no un dato: el lo lee y decide el.</para>
    /// </remarks>
    [TestMethod]
    public void ElProgramaNoProponeNingunNombre()
    {
        var (_, modelo, _) = DocumentoSinNadie();

        Assert.AreEqual(string.Empty, modelo.NombrePropuestoParaLaPersonaNueva,
            "el programa no adivina un nombre: la regla permanente 1");
        Assert.AreEqual(string.Empty, modelo.CedulaPropuestaParaLaPersonaNueva,
            "y mucho menos una cédula");
    }

    /// <summary>Como se llama el archivo SI se ensena: es evidencia, y la lee el.</summary>
    [TestMethod]
    public void ElNombreDelArchivoSeEnsenaComoReferencia()
    {
        var (_, modelo, _) = DocumentoSinNadie();

        StringAssert.Contains(modelo.DeDondeSacarElNombre, "prueba.pdf");
        StringAssert.Contains(modelo.DeDondeSacarElNombre, TextoDeLaPersonaAMano.NiUnDatoSeAdivina);
    }

    /// <summary>
    /// El nombre del boton para un lector de pantalla dice lo mismo en el XAML y en el codigo.
    /// </summary>
    /// <remarks>
    /// Esta escrito en los dos sitios a proposito —en el XAML para que exista desde el primer
    /// momento, y en <see cref="TextoDeLaPersonaAMano"/> para poder leerlo sin abrir ventana—,
    /// y esta prueba es lo que impide que se separen. Es la misma disciplina que ya vigila
    /// <c>PruebasDeQuienContestoDesdeRevisar</c> con el texto de la via del Excel.
    /// </remarks>
    [TestMethod]
    public void ElNombreDelBotonEsElMismoEnElXamlYEnElCodigo()
    {
        var xaml = System.Xml.Linq.XDocument.Load(RutaDelXamlDeCorreccion());
        var boton = xaml.Descendants()
            .FirstOrDefault(e => e.Attribute(
                System.Xml.Linq.XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
                ?.Value == "_botonDeAnadirPersona");

        Assert.IsNotNull(boton, "no se encontró el botón «_botonDeAnadirPersona» en el XAML");
        Assert.AreEqual(
            TextoDeLaPersonaAMano.BotonParaElLector,
            boton.Attribute("AutomationProperties.Name")?.Value);
    }

    /// <summary>Donde esta el XAML de Correccion, subiendo desde donde corren las pruebas.</summary>
    private static string RutaDelXamlDeCorreccion()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var xaml = Path.Combine(
                actual.FullName, "csharp", "Fichas", "Fichas.App", "Correccion", "PaginaDeCorreccion.xaml");
            if (File.Exists(xaml)) return xaml;
            actual = actual.Parent;
        }

        Assert.Inconclusive($"No se encontró PaginaDeCorreccion.xaml desde «{AppContext.BaseDirectory}».");
        return string.Empty;
    }
}
