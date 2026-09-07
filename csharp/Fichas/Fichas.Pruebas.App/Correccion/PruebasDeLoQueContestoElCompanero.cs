using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Lo que el companero contesto en su Excel se VE, y no se confunde con la firma de Miguel.
/// </summary>
/// <remarks>
/// <para>La queja del dueno, con sus palabras: <i>«cuando tomar el Excel del agente de vuelta
/// no hace ningun cambio en el sistema; debe hacer un cambio de si esta completo o no de la
/// persona»</i>.</para>
/// <para><b>Y la causa no era la que parecia.</b> Medido el 2026-09-05 sobre los siete
/// escaneos reales: el Excel de vuelta SI escribe —7 casos pasan de nulo a <c>completa</c> o
/// <c>no_completa</c> con quien y cuando, y 7 personas quedan con sus seis pasos,
/// <c>propuesto_por</c> y <c>propuesto_en</c>—. Lo que fallaba es que <b>ninguna pantalla lo
/// leia</b>: un <c>grep</c> de
/// <c>PasoPreparacion|PasoListoParaElTemplo|EstadoPropuesto|PropuestoPor|NotaCompanero</c>
/// sobre todo <c>Fichas.App</c> daba <b>cero</b> coincidencias. El dato entraba y se perdia
/// de vista.</para>
/// <para>⛔ <b>Lo que dice el companero es SUYO y no firma nada de Miguel.</b> Son dos cosas
/// distintas y no se mezclan (regla permanente 5, precisada por el dueno el 2026-09-03): el
/// estado de la recomendacion lo escribe el Excel que devuelve el companero, con su nombre;
/// la firma de campos —«Todo correcto», <c>verificado</c>— es de Miguel y nunca automatica.
/// Hay una prueba abajo que lo fija.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueContestoElCompanero
{
    private const long CasoDePrueba = 400;
    private const long PrimeraPersona = 401;
    private const long SegundaPersona = 402;
    private const long Sandy = 7;

    // ---- el montaje ------------------------------------------------------

    private static ServiciosFalsos MontarConDosPersonas()
    {
        var servicios = new ServiciosFalsos(0, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Companeros[Sandy] = new Companero
        {
            Id = Sandy, Nombre = "Sandy", Activo = true, CreadoEn = "2026-08-01",
        };
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = "Panama City, Panama",
            RutaPdf = @"C:\pdfs\CASP2609_Ana_Prueba.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PrimeraPersona] = new Persona
        {
            Id = PrimeraPersona, CasoId = CasoDePrueba, Nombre = "Ana Prueba",
            Mrn = "055-1111-3853", FilaFormulario = 1, PaginaPdf = 1,
        };
        servicios.Almacen.Personas[SegundaPersona] = new Persona
        {
            Id = SegundaPersona, CasoId = CasoDePrueba, Nombre = "Linda Simulado",
            Mrn = "055-1111-3854", FilaFormulario = 2, PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>La primera persona, tal como la deja el Excel que devuelve el companero.</summary>
    private static void ContestoPorLaPrimera(ServiciosFalsos servicios, bool completa = true)
        => servicios.Almacen.Personas[PrimeraPersona] = servicios.Almacen.Personas[PrimeraPersona] with
        {
            EstadoPropuesto = completa ? "completa" : "no_completa",
            NotaCompanero = completa ? null : "Le falta la entrevista con el obispo.",
            PropuestoPor = Sandy,
            PropuestoEn = "2026-09-05T14:22:00",
            PasoPreparacion = true,
            PasoInformacion = true,
            PasoCitaDelTemplo = completa,
            PasoAccionesRequeridas = true,
            PasoEntrevistas = completa ? true : false,
            PasoListoParaElTemplo = completa,
            LlamoAlLider = !completa,
        };

    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
    {
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, new ProcedenciaComoLaDeVerdad(Sandy),
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return modelo;
    }

    // ---- abriendo la persona, se ve QUE contesto, QUIEN y CUANDO ---------

    /// <summary>
    /// Dado que el companero contesto por una persona, cuando se abre el caso, entonces la
    /// pantalla trae su respuesta con su nombre y su fecha.
    /// </summary>
    [TestMethod]
    public void AlAbrirElCasoSeVeQueContestoQuienYCuando()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);

        var respuestas = ModeloSobre(servicios).RespuestasDeLosCompaneros;

        Assert.HasCount(1, respuestas);
        var suya = respuestas[0];
        Assert.AreEqual(PrimeraPersona, suya.PersonaId);
        StringAssert.Contains(suya.DeQuien, "Ana Prueba", StringComparison.Ordinal);
        StringAssert.Contains(suya.Quien, "Sandy", StringComparison.Ordinal);
        StringAssert.Contains(suya.Cuando, "5 de septiembre de 2026", StringComparison.Ordinal);
        StringAssert.Contains(suya.Estado, "completa", StringComparison.Ordinal);
    }

    /// <summary>Los seis pasos salen con su rotulo y con lo que el companero contesto.</summary>
    /// <remarks>
    /// Los seis y en su orden: el companero los copia de arriba abajo de la pantalla del
    /// lider, y ensenarlos en otro orden obliga a ir y venir para compararlos.
    /// </remarks>
    [TestMethod]
    public void LosSeisPasosSalenConSuRotuloYSuRespuesta()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios, completa: false);

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        Assert.HasCount(6, suya.Pasos);
        Assert.AreEqual("1. Preparación", suya.Pasos[0].Rotulo);
        Assert.AreEqual("sí", suya.Pasos[0].Respuesta);
        Assert.AreEqual("6. Listo para el templo", suya.Pasos[5].Rotulo);
        Assert.AreEqual("no", suya.Pasos[5].Respuesta);
    }

    /// <summary>
    /// Un paso que el companero dejo en blanco dice «sin contestar», y NO dice «no».
    /// </summary>
    /// <remarks>
    /// Es la distincion que evita el dano: una casilla en blanco es una pregunta que nadie
    /// miro, y esa persona queda a medias, no reprobada. Pintarla como «no» le inventa al
    /// companero una respuesta que no dio (regla permanente 1).
    /// </remarks>
    [TestMethod]
    public void UnPasoEnBlancoDiceSinContestarYNoDiceNo()
    {
        var servicios = MontarConDosPersonas();
        servicios.Almacen.Personas[PrimeraPersona] = servicios.Almacen.Personas[PrimeraPersona] with
        {
            EstadoPropuesto = "no_completa",
            PropuestoPor = Sandy,
            PropuestoEn = "2026-09-05T14:22:00",
            PasoPreparacion = true,
            PasoEntrevistas = null,
        };

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        Assert.AreEqual("sin contestar", suya.Pasos[4].Respuesta);
        Assert.AreEqual("5. Entrevistas", suya.Pasos[4].Rotulo);
    }

    /// <summary>La nota que escribio el companero se ve tal cual, sin recortarla.</summary>
    [TestMethod]
    public void LaNotaDelCompaneroSeVeTalCual()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios, completa: false);

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        Assert.AreEqual("Le falta la entrevista con el obispo.", suya.Nota);
    }

    /// <summary>Si llamo al lider, se dice; y NO cuenta como un septimo paso.</summary>
    [TestMethod]
    public void SiLlamoAlLiderSeDiceYNoEsUnSeptimoPaso()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios, completa: false);

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        Assert.HasCount(6, suya.Pasos);
        StringAssert.Contains(suya.LlamoAlLider, "llamó al líder", StringComparison.Ordinal);
    }

    /// <summary>De la persona por la que nadie contesto NO se inventa una respuesta.</summary>
    [TestMethod]
    public void DeQuienNadieContestoNoSaleNingunaRespuesta()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);

        var respuestas = ModeloSobre(servicios).RespuestasDeLosCompaneros;

        Assert.IsEmpty(respuestas.Where(una => una.PersonaId == SegundaPersona));
    }

    /// <summary>Un companero que ya no esta en la base no borra su respuesta: se dice su numero.</summary>
    /// <remarks>
    /// Los companeros se desactivan pero tambien se les puede haber cambiado el id por una
    /// importacion vieja. Perder la respuesta entera por no poder poner un nombre seria
    /// tirar el dato que se vino a ensenar.
    /// </remarks>
    [TestMethod]
    public void SiElCompaneroYaNoEstaSeDiceSuNumeroYNoSePierdeLaRespuesta()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);
        servicios.Almacen.Companeros.Remove(Sandy);

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        StringAssert.Contains(suya.Quien, Sandy.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        Assert.AreEqual("completa", suya.Estado);
    }

    /// <summary>Una fecha que no se entiende no tumba la respuesta: se queda sin fecha.</summary>
    [TestMethod]
    public void UnaFechaQueNoSeEntiendeNoTumbaLaRespuesta()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);
        servicios.Almacen.Personas[PrimeraPersona] = servicios.Almacen.Personas[PrimeraPersona] with
        {
            PropuestoEn = "el martes",
        };

        var suya = ModeloSobre(servicios).RespuestasDeLosCompaneros.Single();

        Assert.AreEqual(string.Empty, suya.Cuando);
        Assert.AreEqual("completa", suya.Estado);
    }

    // ---- lo que dice el companero NO firma nada de Miguel -----------------

    /// <summary>
    /// Un caso contestado entero por el companero NO tiene ni un campo firmado.
    /// </summary>
    /// <remarks>
    /// Es la regla permanente 5 puesta donde mas facil seria romperla: el companero dice
    /// «completa» y eso no da por bueno ni el nombre, ni la cedula, ni la fecha. Lo que el
    /// dice es suyo; la firma es de Miguel.
    /// </remarks>
    [TestMethod]
    public void LoQueDiceElCompaneroNoFirmaNiUnCampoDeMiguel()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);

        var modelo = ModeloSobre(servicios);

        Assert.HasCount(1, modelo.RespuestasDeLosCompaneros);
        Assert.AreEqual(0, modelo.CuantosFirmados);
        Assert.IsEmpty(modelo.Campos.Where(campo => campo.Procedencia?.Verificado == true));
    }

    // ---- sin abrirlo, se distingue si ya contesto o no --------------------

    /// <summary>El desplegable dice que el companero ya contesto por todos.</summary>
    [TestMethod]
    public void ElDesplegableDiceQueYaContestoPorTodos()
    {
        var caso = new Caso { Id = 1, NumeroCaso = "CASP2609", RutaPdf = @"C:\pdfs\uno.pdf" };
        var linea = TextoDelDesplegable.Componer(caso, "Ana Prueba", 1, original: null, contestadas: 1);

        StringAssert.Contains(linea, "ya contestó", StringComparison.Ordinal);
    }

    /// <summary>Y dice que nadie ha contestado cuando nadie ha contestado.</summary>
    /// <remarks>
    /// Las dos se dicen con palabras y no una con marca y otra con nada: la ausencia de una
    /// marca no se distingue de una pantalla que todavia no la sabe pintar.
    /// </remarks>
    [TestMethod]
    public void ElDesplegableDiceQueNadieHaContestado()
    {
        var caso = new Caso { Id = 1, NumeroCaso = "CASP2609", RutaPdf = @"C:\pdfs\uno.pdf" };
        var linea = TextoDelDesplegable.Componer(caso, "Ana Prueba", 1, original: null, contestadas: 0);

        StringAssert.Contains(linea, "sin contestar", StringComparison.Ordinal);
    }

    /// <summary>Cuando contestaron por unos si y por otros no, lo dice con los dos numeros.</summary>
    [TestMethod]
    public void ElDesplegableDiceCuantasDeCuantasSiFaltanPersonas()
    {
        var caso = new Caso { Id = 1, NumeroCaso = "SURB2609", RutaPdf = @"C:\pdfs\grupo.pdf" };
        var linea = TextoDelDesplegable.Componer(caso, "Uno", 12, original: null, contestadas: 5);

        StringAssert.Contains(linea, "contestadas 5 de 12", StringComparison.Ordinal);
    }

    /// <summary>La linea del desplegable sigue cabiendo en su tope.</summary>
    [TestMethod]
    public void LaLineaDelDesplegableNoPasaDeSuTope()
    {
        var caso = new Caso
        {
            Id = 1,
            NumeroCaso = "SURB2609",
            DuplicadoDe = 2,
            RutaPdf = @"C:\pdfs\SURB2609_Suriname_Group_Complete_con_un_nombre_larguisimo.pdf",
        };
        var original = new Caso { Id = 2, RutaPdf = @"C:\pdfs\otro_documento_igual_de_largo.pdf" };
        var linea = TextoDelDesplegable.Componer(caso, "Un nombre bastante largo", 12, original, contestadas: 5);

        Assert.IsLessThanOrEqualTo(
            TextoDelDesplegable.LargoMaximoDeLaLinea, linea.Length, $"«{linea}» mide {linea.Length}.");
    }

    /// <summary>
    /// Al cambiar de caso, la respuesta del anterior NO se queda pegada en la pantalla.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es un defecto medido, no una precaucion.</b> Con la ventana abierta el
    /// 2026-09-05 sobre los siete escaneos reales: elegido el caso <c>CASD2609</c> de Jonas
    /// Ficticio, el panel seguia diciendo <b>«Ana Prueba, fila 1 · Contestó Sandy · completa ·
    /// 6 de 6 pasos»</b>, que era la respuesta del caso ANTERIOR. La causa: el modelo vaciaba
    /// y rellenaba la MISMA lista, el repetidor recibia la misma referencia y no volvia a
    /// leerla.
    /// <para>
    /// Ensenar la respuesta de otra persona es peor que no ensenar ninguna: se lee como si
    /// esa persona estuviera lista cuando no lo esta, y eso es exactamente el dano que este
    /// programa existe para evitar.
    /// </para>
    /// <para>
    /// La prueba comprueba las DOS cosas: que el contenido cambia, y que la lista es OTRA
    /// —lo segundo es lo que hace que el repetidor se entere—.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void AlCambiarDeCasoLaRespuestaDelAnteriorNoSeQuedaPegada()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);

        // Un segundo caso, con una persona por la que nadie contesto.
        servicios.Almacen.Casos[CasoDePrueba + 1] = new Caso
        {
            Id = CasoDePrueba + 1, NumeroCaso = "CASD2609", CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[SegundaPersona + 1] = new Persona
        {
            Id = SegundaPersona + 1, CasoId = CasoDePrueba + 1, Nombre = "Jonas Ficticio",
            Mrn = "055-1111-3855", FilaFormulario = 1,
        };

        var modelo = ModeloSobre(servicios);
        var primeras = modelo.RespuestasDeLosCompaneros;
        Assert.HasCount(1, primeras);

        modelo.Cargar(CasoDePrueba + 1);

        Assert.IsEmpty(modelo.RespuestasDeLosCompaneros, "Se quedó pegada la del caso anterior.");
        Assert.AreNotSame(primeras, modelo.RespuestasDeLosCompaneros,
            "Es la MISMA lista: el repetidor de la pantalla no se entera de que cambió.");
        Assert.HasCount(1, primeras, "La lista que ya se entregó no se toca por detrás.");
    }

    /// <summary>
    /// Los seis rotulos de la pantalla son los MISMOS que los de los reportes.
    /// </summary>
    /// <remarks>
    /// Estan escritos dos veces —aqui en <see cref="LoQueContestoElCompanero"/> y alli en
    /// <c>Fichas.Reportes.Reglas.Pasos</c>— porque las pantallas solo conocen
    /// <c>Fichas.Contratos</c> (Fichas.App.csproj) y los rotulos de aquella son privados.
    /// <b>Dos verdades escritas dos veces se separan el dia que alguien toque una</b>, y eso
    /// es exactamente lo que acaba de pasar con las listas de campos de la importacion y de
    /// la correccion. Aqui NO se deja a la buena fe: se comparan uno a uno.
    /// <para>
    /// <c>Pasos.SinCompletar</c> devuelve los rotulos de los pasos marcados que NO, sin el
    /// numero de delante: por eso se le da una persona con los seis en falso y se le quita
    /// el numero a los de aqui.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LosSeisRotulosSonLosMismosQueLosDeReportes()
    {
        var todosQueNo = new Persona
        {
            PasoPreparacion = false,
            PasoInformacion = false,
            PasoCitaDelTemplo = false,
            PasoAccionesRequeridas = false,
            PasoEntrevistas = false,
            PasoListoParaElTemplo = false,
        };

        var sinElNumero = LoQueContestoElCompanero.RotulosDeLosPasos
            .Select(rotulo => rotulo[(rotulo.IndexOf(". ", StringComparison.Ordinal) + 2)..])
            .ToArray();

        CollectionAssert.AreEqual(
            Fichas.Reportes.Reglas.Pasos.SinCompletar(todosQueNo).ToArray(),
            sinElNumero,
            "Los rótulos de la pantalla y los de los reportes se han separado.");
    }

    /// <summary>El modelo dice por cuantas personas del caso se contesto, para el desplegable.</summary>
    [TestMethod]
    public void ElModeloDicePorCuantasPersonasSeContesto()
    {
        var servicios = MontarConDosPersonas();
        ContestoPorLaPrimera(servicios);

        Assert.AreEqual(1, LoQueContestoElCompanero.CuantasContestadas(
            servicios.Personas.DeCaso(CasoDePrueba)));
        Assert.AreEqual(0, LoQueContestoElCompanero.CuantasContestadas([]));
    }
}
