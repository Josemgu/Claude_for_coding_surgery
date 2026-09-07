using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Las seis preguntas llevan el nombre de QUIEN las contesto, y no el del Excel de vuelta.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>El defecto que estas pruebas cierran, medido el 2026-09-06.</b> Desde la migracion
/// 19 las seis preguntas se pueden contestar dentro del programa, y queda escrito quien en
/// <c>pasos_por</c>, <c>pasos_en</c> y <c>pasos_origen</c>. La pantalla de Correccion las
/// pintaba bajo el nombre de <c>propuesto_por</c>, que es quien propuso el ESTADO desde su
/// Excel: <c>grep -c "FirmaDeLosPasos|pasos_por|pasos_en|pasos_origen"</c> sobre
/// <c>Fichas.App/Correccion/</c> daba <b>0</b>. Consecuencia: en cuanto Miguel contestara
/// una pregunta, esa pantalla ensenaria SUS respuestas con el nombre de Sandy.
/// </para>
/// <para>
/// <b>Atribuirle a alguien algo que no hizo es el peor fallo que puede tener este
/// programa</b>: toda la separacion entre «lo del companero es suyo» y «lo de Miguel es
/// suyo» existe justo para eso (regla permanente 5, precisada por el dueno el 2026-09-03).
/// </para>
/// <para>
/// ⚠️ <b>Las dos vias escriben las MISMAS seis columnas.</b>
/// <c>IPersonas.AnotarPropuesta</c> —el Excel de vuelta— escribe los seis <c>paso_*</c>
/// junto a <c>propuesto_por</c> en el mismo UPDATE y <b>no toca</b> <c>pasos_por</c>;
/// <c>IPersonas.ResponderLosPasos</c> —la pantalla— escribe los seis <c>paso_*</c> y las
/// tres columnas de la firma, y no toca <c>propuesto_por</c>. Por eso, sin firma de la
/// migracion 19, las seis que se ven son las que trajo el Excel, y con firma son las de la
/// pantalla.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQuienContestoLasSeis
{
    private const long CasoDePrueba = 500;
    private const long LaDeSandy = 501;
    private const long LaDeMiguel = 502;
    private const long Sandy = 7;
    private const long Miguel = 9;

    /// <summary>Lo que la ventana de las preguntas guarda en <c>pasos_origen</c>.</summary>
    private const string ALaMano = "a mano en la pantalla";

    // ---- el montaje ------------------------------------------------------

    private static ServiciosFalsos MontarConDosPersonas()
    {
        var servicios = new ServiciosFalsos(0, 20260906, new RelojFijo("2026-09-06"));
        servicios.Almacen.Companeros[Sandy] = new Companero
        {
            Id = Sandy, Nombre = "Sandy", Activo = true, CreadoEn = "2026-08-01",
        };
        servicios.Almacen.Companeros[Miguel] = new Companero
        {
            Id = Miguel, Nombre = "Miguel", Activo = true, CreadoEn = "2026-08-01",
            Rol = RolDeCompanero.Administrador,
        };
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba, NumeroCaso = "SURB2609", CreadoEn = "2026-09-06",
        };
        servicios.Almacen.Personas[LaDeSandy] = new Persona
        {
            Id = LaDeSandy, CasoId = CasoDePrueba, Nombre = "Ana Prueba",
            Mrn = "055-1111-3853", FilaFormulario = 1, PaginaPdf = 1,
        };
        servicios.Almacen.Personas[LaDeMiguel] = new Persona
        {
            Id = LaDeMiguel, CasoId = CasoDePrueba, Nombre = "Linda Simulado",
            Mrn = "055-1111-3854", FilaFormulario = 2, PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Deja una persona tal como la deja el Excel que devuelve el companero.</summary>
    /// <remarks>
    /// Escribe lo mismo que <c>AnotarPropuesta</c>: el estado, la nota, su firma y los seis
    /// pasos. <b>Sin</b> firma de la migracion 19, que esa via no la toca.
    /// </remarks>
    private static void ElExcelDeSandyContestoPor(ServiciosFalsos servicios, long personaId, string cuando)
        => servicios.Almacen.Personas[personaId] = servicios.Almacen.Personas[personaId] with
        {
            EstadoPropuesto = "completa",
            PropuestoPor = Sandy,
            PropuestoEn = cuando,
            PasoPreparacion = true,
            PasoInformacion = true,
            PasoCitaDelTemplo = true,
            PasoAccionesRequeridas = true,
            PasoEntrevistas = true,
            PasoListoParaElTemplo = true,
        };

    /// <summary>Deja una persona tal como la deja contestar las seis dentro del programa.</summary>
    /// <remarks>
    /// Escribe lo mismo que <c>ResponderLosPasos</c>: los seis pasos y las tres columnas de
    /// la firma. <b>No toca</b> <c>estado_propuesto</c> ni <c>propuesto_por</c>.
    /// </remarks>
    private static void MiguelContestoPor(ServiciosFalsos servicios, long personaId, string cuando)
    {
        servicios.Almacen.Personas[personaId] = servicios.Almacen.Personas[personaId] with
        {
            PasoPreparacion = true,
            PasoInformacion = false,
            PasoCitaDelTemplo = null,
            PasoAccionesRequeridas = true,
            PasoEntrevistas = true,
            PasoListoParaElTemplo = false,
        };
        servicios.Almacen.FirmasDeLosPasos[personaId] = new FirmaDeLosPasos(Miguel, cuando, ALaMano);
    }

    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
    {
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, new ProcedenciaComoLaDeVerdad(Sandy),
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return modelo;
    }

    private static RespuestaDelCompanero LaDe(ModeloDeCorreccion modelo, long personaId)
        => modelo.RespuestasDeLosCompaneros.Single(una => una.PersonaId == personaId);

    // ---- el caso grave: dos personas, dos nombres -------------------------

    /// <summary>
    /// Dado un documento donde el companero contesto por una persona y Miguel por otra,
    /// cuando se abre, entonces cada una lleva el nombre de quien la contesto.
    /// </summary>
    /// <remarks>
    /// Es el criterio de cierre del pase, escrito en lo que se ve. Antes de esta prueba las
    /// dos decian «Sandy», porque el unico nombre que se leia era <c>propuesto_por</c>.
    /// </remarks>
    [TestMethod]
    public void CadaPersonaLlevaElNombreDeQuienContestoSusSeis()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");
        MiguelContestoPor(servicios, LaDeMiguel, "2026-09-06T09:10:00");

        var modelo = ModeloSobre(servicios);

        StringAssert.Contains(LaDe(modelo, LaDeSandy).LineaDeLosPasos, "Sandy", StringComparison.Ordinal);
        Assert.IsFalse(
            LaDe(modelo, LaDeSandy).LineaDeLosPasos.Contains("Miguel", StringComparison.Ordinal),
            "Las seis del Excel de Sandy salieron con el nombre de Miguel.");

        StringAssert.Contains(LaDe(modelo, LaDeMiguel).LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
        Assert.IsFalse(
            LaDe(modelo, LaDeMiguel).LineaDeLosPasos.Contains("Sandy", StringComparison.Ordinal),
            "Las seis que contestó Miguel salieron con el nombre de Sandy.");
    }

    /// <summary>
    /// Lo mismo dentro de UNA persona: Sandy propuso el estado y Miguel contesto las seis.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la forma exacta del defecto. Las dos firmas conviven en la misma fila y en
    /// columnas distintas, y cada frase tiene que leer la suya: el estado es de Sandy, las
    /// seis son de Miguel.
    /// </remarks>
    [TestMethod]
    public void ElEstadoEsDelCompaneroYLasSeisDeQuienLasContesto()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");
        MiguelContestoPor(servicios, LaDeSandy, "2026-09-06T09:10:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.Resumen, "Sandy", StringComparison.Ordinal);
        Assert.IsFalse(
            suya.Resumen.Contains("Miguel", StringComparison.Ordinal),
            "El estado que propuso Sandy salió con el nombre de Miguel.");

        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "6 de septiembre de 2026", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, ALaMano, StringComparison.Ordinal);
    }

    /// <summary>El resumen del estado ya NO cuenta pasos: esa cuenta es de la otra firma.</summary>
    /// <remarks>
    /// Mezclar las dos en una linea es lo que hacia que la cuenta de pasos de Miguel se
    /// leyera detras del nombre de Sandy.
    /// </remarks>
    [TestMethod]
    public void ElResumenDelEstadoNoCuentaLosPasos()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");
        MiguelContestoPor(servicios, LaDeSandy, "2026-09-06T09:10:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        Assert.IsFalse(
            suya.Resumen.Contains("pasos", StringComparison.OrdinalIgnoreCase),
            $"«{suya.Resumen}» sigue contando pasos junto al nombre de quien propuso el estado.");
        StringAssert.Contains(suya.LineaDeLosPasos, "de 6", StringComparison.Ordinal);
    }

    // ---- sin firma de la migracion 19: las seis son del Excel -------------

    /// <summary>
    /// Sin firma de los pasos, las seis se atribuyen a quien devolvio el Excel, y se dice
    /// que vinieron de ahi.
    /// </summary>
    /// <remarks>
    /// No es una suposicion: <c>AnotarPropuesta</c> escribe los seis <c>paso_*</c> y
    /// <c>propuesto_por</c> en el MISMO UPDATE, y es la unica via que lo hace sin firmar la
    /// migracion 19.
    /// </remarks>
    [TestMethod]
    public void SinFirmaDeLosPasosLasSeisSonDelExcelDeVuelta()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.LineaDeLosPasos, "Sandy", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "5 de septiembre de 2026", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "Excel", StringComparison.Ordinal);
    }

    // ---- si nadie contesto, NO se inventa un nombre -----------------------

    /// <summary>
    /// Dado que el companero dijo el estado pero dejo las seis en blanco, la linea de las
    /// seis NO nombra a nadie.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es el caso que destapo esta misma prueba al ponerse roja.</b> Un companero puede
    /// devolver su Excel diciendo «no completa» y no contestar ninguna de las seis:
    /// <c>propuesto_por</c> queda escrito y las seis vacias. Decir «contestó Sandy» ahi le
    /// atribuye un trabajo que no hizo, que es el mismo dano por el otro lado.
    /// </remarks>
    [TestMethod]
    public void SiNadieContestoLasSeisNoSeInventaUnNombre()
    {
        var servicios = MontarConDosPersonas();
        servicios.Almacen.Personas[LaDeSandy] = servicios.Almacen.Personas[LaDeSandy] with
        {
            EstadoPropuesto = "no_completa",
            NotaCompanero = "Le falta la entrevista con el obispo.",
            PropuestoPor = Sandy,
            PropuestoEn = "2026-09-05T14:22:00",
        };

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        Assert.IsFalse(
            suya.LineaDeLosPasos.Contains("Sandy", StringComparison.Ordinal),
            $"«{suya.LineaDeLosPasos}» le atribuye a Sandy seis preguntas que nadie contestó.");
        Assert.IsFalse(
            suya.LineaDeLosPasos.Contains("Miguel", StringComparison.Ordinal),
            $"«{suya.LineaDeLosPasos}» nombra a alguien que no contestó nada.");
        StringAssert.Contains(suya.LineaDeLosPasos, "Nadie ha contestado", StringComparison.Ordinal);
    }

    /// <summary>
    /// Con las seis contestadas y sin ninguna de las dos firmas, se dice que no se sabe
    /// quien, y no se dice que no las contesto nadie.
    /// </summary>
    /// <remarks>
    /// Son dos cosas distintas y las dos son ciertas de casos distintos: «nadie contesto» es
    /// una casilla vacia, y «hay respuestas sin firma» es un dato entrado por una via que no
    /// dejo nombre. Decir la primera cuando pasa la segunda esconde respuestas escritas.
    /// </remarks>
    [TestMethod]
    public void ConLasSeisEscritasYSinFirmaSeDiceQueNoSeSabeQuien()
    {
        var servicios = MontarConDosPersonas();
        servicios.Almacen.Personas[LaDeSandy] = servicios.Almacen.Personas[LaDeSandy] with
        {
            PasoPreparacion = true,
            PasoInformacion = true,
        };

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.LineaDeLosPasos, "no quedó anotado quién", StringComparison.Ordinal);
        Assert.IsFalse(
            suya.LineaDeLosPasos.Contains("Sandy", StringComparison.Ordinal),
            $"«{suya.LineaDeLosPasos}» inventa un nombre para unas respuestas sin firma.");
    }

    /// <summary>
    /// Si solo contesto Miguel y el Excel no ha vuelto, NO se dice que contesto un companero.
    /// </summary>
    /// <remarks>
    /// Antes, esta persona salia con <b>«Contestó un compañero que no quedó anotado»</b>,
    /// que le atribuye al companero un trabajo que no hizo y que ademas hace pensar que su
    /// Excel ya volvio.
    /// </remarks>
    [TestMethod]
    public void SiSoloContestoMiguelNoSeDiceQueContestoUnCompanero()
    {
        var servicios = MontarConDosPersonas();
        MiguelContestoPor(servicios, LaDeMiguel, "2026-09-06T09:10:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeMiguel);

        Assert.IsFalse(
            suya.Resumen.Contains("Contestó", StringComparison.Ordinal),
            $"«{suya.Resumen}» dice que contestó un compañero que no ha devuelto nada.");
        StringAssert.Contains(suya.Resumen, "no ha devuelto", StringComparison.Ordinal);
        Assert.AreEqual(string.Empty, suya.Quien);
        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
    }

    /// <summary>
    /// Pero si es MIGUEL quien las deja en blanco desde la pantalla, eso si lleva su nombre.
    /// </summary>
    /// <remarks>
    /// Las dos firmas no valen lo mismo con las seis vacias, y no es una asimetria gratuita:
    /// <c>propuesto_por</c> habla del ESTADO, y <c>pasos_por</c> existe justo para decir que
    /// alguien toco las seis. Volver a dejarlas en blanco es una respuesta y se puede
    /// (criterio C19-6); borrar de quien fue seria perder el rastro de quien las vacio.
    /// </remarks>
    [TestMethod]
    public void SiMiguelDejaLasSeisEnBlancoDesdeLaPantallaEsoSiLlevaSuNombre()
    {
        var servicios = MontarConDosPersonas();
        servicios.Almacen.Personas[LaDeSandy] = servicios.Almacen.Personas[LaDeSandy] with
        {
            EstadoPropuesto = "completa",
            PropuestoPor = Sandy,
            PropuestoEn = "2026-09-05T14:22:00",
        };
        servicios.Almacen.FirmasDeLosPasos[LaDeSandy] =
            new FirmaDeLosPasos(Miguel, "2026-09-06T09:10:00", ALaMano);

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.LineaDeLosPasos, "0 de 6", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
        Assert.IsFalse(
            suya.LineaDeLosPasos.Contains("Sandy", StringComparison.Ordinal),
            $"«{suya.LineaDeLosPasos}» le atribuye a Sandy unas seis que ella no contestó.");
    }

    // ---- lo que se estropea si nadie mira el orden ------------------------

    /// <summary>
    /// Si el Excel del companero llega DESPUES de que Miguel contestara, las seis que se
    /// ven son las del Excel, y se nombra a los dos.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es un agujero de la base y no de esta pantalla:</b>
    /// <c>AnotarPropuesta</c> pisa los seis <c>paso_*</c> y <b>deja</b> <c>pasos_por</c>
    /// apuntando a quien contesto antes. Fiarse de la firma sola diria que las respuestas de
    /// Sandy las contesto Miguel, que es el mismo dano por el otro lado. Aqui se decide por
    /// la fecha —la ultima escritura manda— y se nombra tambien a quien habia contestado
    /// antes, para que no desaparezca su rastro.
    /// </remarks>
    [TestMethod]
    public void SiElExcelLlegaDespuesLasSeisSonDelExcelYSeNombraALosDos()
    {
        var servicios = MontarConDosPersonas();
        MiguelContestoPor(servicios, LaDeSandy, "2026-09-05T09:10:00");
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-06T14:22:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.LineaDeLosPasos, "Sandy", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "Excel", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
    }

    /// <summary>Y si Miguel contesto despues, las seis son suyas y se nombra a los dos.</summary>
    [TestMethod]
    public void SiMiguelContestaDespuesLasSeisSonSuyas()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");
        MiguelContestoPor(servicios, LaDeSandy, "2026-09-06T09:10:00");

        var suya = LaDe(ModeloSobre(servicios), LaDeSandy);

        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, ALaMano, StringComparison.Ordinal);
    }

    // ---- lo que no tumba la linea ----------------------------------------

    /// <summary>Una fecha de firma que no se entiende no tumba la linea ni el nombre.</summary>
    /// <remarks>
    /// Requisito 9 del dueno, y la misma regla que ya sigue <c>propuesto_en</c>: lo
    /// importante —quien contesto— se sigue diciendo.
    /// </remarks>
    [TestMethod]
    public void UnaFechaDeFirmaQueNoSeEntiendeNoTumbaLaLinea()
    {
        var servicios = MontarConDosPersonas();
        MiguelContestoPor(servicios, LaDeMiguel, "el martes");

        var suya = LaDe(ModeloSobre(servicios), LaDeMiguel);

        StringAssert.Contains(suya.LineaDeLosPasos, "Miguel", StringComparison.Ordinal);
        StringAssert.Contains(suya.LineaDeLosPasos, ALaMano, StringComparison.Ordinal);
    }

    /// <summary>
    /// Un companero que ya no esta en la base no borra la firma: se dice su numero.
    /// </summary>
    [TestMethod]
    public void SiQuienFirmoYaNoEstaSeDiceSuNumero()
    {
        var servicios = MontarConDosPersonas();
        MiguelContestoPor(servicios, LaDeMiguel, "2026-09-06T09:10:00");
        servicios.Almacen.Companeros.Remove(Miguel);

        var suya = LaDe(ModeloSobre(servicios), LaDeMiguel);

        StringAssert.Contains(
            suya.LineaDeLosPasos,
            Miguel.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    // ---- y esto sigue sin firmar nada de Miguel ---------------------------

    /// <summary>Leer las dos firmas no firma ni un campo.</summary>
    /// <remarks>
    /// La regla permanente 5 puesta donde mas facil seria romperla: ahora que la pantalla
    /// sabe que Miguel contesto las seis, sigue sin dar por bueno ni un campo suyo.
    /// </remarks>
    [TestMethod]
    public void SaberQuienContestoLasSeisNoFirmaNingunCampo()
    {
        var servicios = MontarConDosPersonas();
        ElExcelDeSandyContestoPor(servicios, LaDeSandy, "2026-09-05T14:22:00");
        MiguelContestoPor(servicios, LaDeMiguel, "2026-09-06T09:10:00");

        var modelo = ModeloSobre(servicios);

        Assert.HasCount(2, modelo.RespuestasDeLosCompaneros);
        Assert.AreEqual(0, modelo.CuantosFirmados);
        Assert.IsEmpty(modelo.Campos.Where(campo => campo.Procedencia?.Verificado == true));
    }
}
