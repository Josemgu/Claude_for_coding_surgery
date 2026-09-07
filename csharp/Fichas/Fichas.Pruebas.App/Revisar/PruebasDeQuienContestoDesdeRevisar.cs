using Fichas.App.Correccion;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// La ventana de las seis de Revisar dice de CADA persona quién contestó, cuándo y desde
/// dónde, venga del Excel del compañero o de la pantalla.
/// </summary>
/// <remarks>
/// <para>
/// <b>El defecto que estas pruebas cierran, medido con la ventana abierta.</b> La ventana
/// decía <i>«Nadie ha contestado estas seis preguntas todavía»</i> sobre una persona cuyas
/// seis SÍ había contestado el Excel de Sandy y estaban escritas en la base. Es el mismo
/// defecto que la pantalla de Corrección por el otro lado —allí se atribuía a Sandy lo de
/// Miguel—, y <b>las dos mitades tienen que decir lo mismo</b>.
/// </para>
/// <para>
/// La causa no estaba en esta pantalla: <c>AnotarPropuesta</c> escribía los seis
/// <c>paso_*</c> sin tocar <c>pasos_por</c>, así que
/// <see cref="IPersonas.FirmasDeLosPasosDelCaso"/> —que es de donde sale esta línea— no
/// tenía nada que devolver. Se cerró en la base; aquí se mide lo que se VE.
/// </para>
/// <para>
/// ⚠️ Se prueba contra el falso, igual que el resto de esta pantalla. Que el falso haga lo
/// mismo que la base de verdad lo comprueba <c>PruebaDeQuienEscribioLasSeis</c> corriendo la
/// misma operación contra los dos.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQuienContestoDesdeRevisar
{
    private const string DiaDeLasPruebas = "2026-09-05";
    private const string OrigenAMano = "a mano en la pantalla";

    /// <summary>
    /// Dado un documento donde el Excel de un compañero contestó las seis de una persona,
    /// Miguel contestó las de otra a mano y una tercera no la miró nadie, cuando se abre la
    /// ventana, entonces de cada una dice quién, cuándo y desde dónde, y de la tercera dice
    /// que no la ha mirado nadie.
    /// </summary>
    /// <remarks>
    /// Es el criterio de cierre entero, en una sola prueba y en observables: lo que se
    /// comprueba son las tres frases que él lee en la ventana.
    /// </remarks>
    [TestMethod]
    public void LaVentanaDiceDeCadaPersonaQuienContestoCuandoYDesdeDonde()
    {
        var banco = new BancoDeLaVentana();
        var caso = banco.SembrarUnDocumentoDe(3);
        var personas = banco.Personas(caso);

        banco.ElExcelDeSandyContesta(personas[0].Id);
        banco.MiguelContestaAMano(personas[1].Id);

        var documento = banco.Abrir(caso);

        foreach (var ticket in documento.Personas)
        {
            Console.WriteLine("   {0} · {1}", ticket.DeQuien, ticket.LineaDeLaFirma);
        }

        var porElExcel = documento.Personas[0].LineaDeLaFirma;
        Assert.Contains("Sandy", porElExcel, "La ventana no dice que las contestó Sandy por su Excel.");
        Assert.Contains("5 de septiembre de 2026", porElExcel, "La ventana no dice CUÁNDO las contestó Sandy.");
        Assert.Contains("desde", porElExcel, "La ventana no dice DESDE DÓNDE llegaron las de Sandy.");
        Assert.AreNotEqual(
            PreguntasDeUnDocumento.NadieHaContestado,
            porElExcel,
            "La ventana sigue diciendo que nadie contestó unas seis que contestó el Excel de Sandy.");

        var aMano = documento.Personas[1].LineaDeLaFirma;
        Assert.Contains("Miguel", aMano, "La ventana no dice que las contestó Miguel.");
        Assert.Contains("5 de septiembre de 2026", aMano, "La ventana no dice CUÁNDO las contestó Miguel.");
        Assert.Contains(OrigenAMano, aMano, "La ventana no dice que Miguel las contestó desde la pantalla.");

        Assert.AreEqual(
            PreguntasDeUnDocumento.NadieHaContestado,
            documento.Personas[2].LineaDeLaFirma,
            "De una persona que no ha mirado nadie, la ventana no dice que no la ha mirado nadie.");
    }

    /// <summary>
    /// Dadas las dos vías en el mismo documento, cuando se abre la ventana, entonces se
    /// distinguen: la del Excel no se lee igual que la de la pantalla.
    /// </summary>
    /// <remarks>
    /// No es lo mismo que una respuesta venga del Excel del compañero que de esta pantalla, y
    /// si las dos se leyeran igual, la línea diría quién pero no de dónde salió.
    /// </remarks>
    [TestMethod]
    public void LasDosViasSeDistinguenEnLaVentana()
    {
        var banco = new BancoDeLaVentana();
        var caso = banco.SembrarUnDocumentoDe(2);
        var personas = banco.Personas(caso);

        banco.ElExcelDeSandyContesta(personas[0].Id);
        banco.MiguelContestaAMano(personas[1].Id);

        var documento = banco.Abrir(caso);
        var porElExcel = documento.Personas[0].LineaDeLaFirma;

        Console.WriteLine("== Excel: {0} · Pantalla: {1} ==", porElExcel, documento.Personas[1].LineaDeLaFirma);

        Assert.DoesNotContain(
            OrigenAMano,
            porElExcel,
            "Lo que llegó por el Excel se lee como contestado a mano en la pantalla.");
    }

    /// <summary>
    /// Dada una respuesta que llegó por el Excel, cuando la lee la ventana y cuando la lee
    /// Corrección, entonces las dos mitades la nombran con las MISMAS palabras.
    /// </summary>
    /// <remarks>
    /// ⛔ Se compara el texto que VUELVE de la base con el que usa Corrección, y no una
    /// constante consigo misma: eso pasaría en verde siempre. Si las dos se separan, la misma
    /// respuesta se leería de dos formas distintas según la pantalla desde la que se mire, que
    /// es justo el defecto que este trabajo cierra.
    /// </remarks>
    [TestMethod]
    public void LaVentanaYCorreccionNombranLaViaDelExcelIgual()
    {
        var banco = new BancoDeLaVentana();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        banco.ElExcelDeSandyContesta(persona);

        var origen = banco.FirmaDe(caso, persona).Origen;

        Console.WriteLine(
            "== La base dice «{0}»; Corrección dice «{1}» ==",
            origen,
            LoQueContestoElCompanero.ElExcelDeVuelta);

        Assert.AreEqual(
            LoQueContestoElCompanero.ElExcelDeVuelta,
            origen,
            "La ventana de Revisar y Corrección llaman de dos formas distintas a la misma vía.");
    }

    /// <summary>
    /// ⛔ Dado un Excel que dice el estado y deja las seis EN BLANCO, cuando se abre la
    /// ventana, entonces NO dice que ese compañero las contestó.
    /// </summary>
    /// <remarks>
    /// Un compañero puede devolver su Excel diciendo «no completa» sin haber mirado ninguna
    /// de las seis. Decir «contestó Sandy» ahí le atribuye un trabajo que no hizo.
    /// </remarks>
    [TestMethod]
    public void UnExcelConLasSeisEnBlancoNoSeLeeComoQueSandyContesto()
    {
        var banco = new BancoDeLaVentana();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        banco.ElExcelDeSandyDiceElEstadoYNadaMas(persona);

        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine("== {0} ==", ticket.LineaDeLaFirma);

        Assert.AreEqual(
            PreguntasDeUnDocumento.NadieHaContestado,
            ticket.LineaDeLaFirma,
            "Un Excel que dejó las seis en blanco se lee como que ese compañero las contestó.");
        Assert.IsFalse(ticket.LoContestoOtro, "Avisa de que lo contestó otro cuando nadie contestó nada.");
    }

    /// <summary>
    /// Un documento con su equipo, para leer la ventana sin abrir ninguna.
    /// </summary>
    /// <remarks>
    /// El equipo se siembra con <b>Sandy la primera y Miguel después</b>, igual que en
    /// <c>PruebasDeLasSeisPreguntas</c>: es el orden que caza que se firme con «el primer
    /// activo» en vez de con el administrador.
    /// </remarks>
    private sealed class BancoDeLaVentana
    {
        private readonly AlmacenFalso _almacen;
        private readonly RepositorioDeCasosFalso _casos;
        private readonly RepositorioDePersonasFalso _personas;
        private readonly RepositorioDeCompanerosFalso _companeros;
        private readonly AccionesDeLasPreguntas _acciones;
        private readonly long _sandy;

        public BancoDeLaVentana()
        {
            _almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);
            _casos = new RepositorioDeCasosFalso(_almacen);
            _personas = new RepositorioDePersonasFalso(_almacen);
            _companeros = new RepositorioDeCompanerosFalso(_almacen);

            _sandy = _companeros.Guardar(new Companero
            {
                Nombre = "Sandy",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Companero,
            }).Id;

            _companeros.Guardar(new Companero
            {
                Nombre = "Miguel",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Administrador,
            });

            _acciones = new AccionesDeLasPreguntas(_personas, _companeros);
        }

        public long SembrarUnDocumentoDe(int cuantas)
        {
            var caso = _casos.Guardar(new Caso
            {
                NumeroCaso = "SURB2609",
                FechaViaje = "2026-09-17",
                UnidadNombre = "Paramaribo",
                RutaPdf = @"C:\Fichas\entrada\SURB2609_grupo.pdf",
                CreadoEn = DiaDeLasPruebas + " 10:00:00",
            }).Id;

            for (var i = 0; i < cuantas; i++)
            {
                _personas.Guardar(new Persona
                {
                    CasoId = caso,
                    Mrn = $"055-1111-38{50 + i}",
                    Nombre = $"Persona {i + 1}",
                    FilaFormulario = i + 1,
                });
            }

            return caso;
        }

        public IReadOnlyList<Persona> Personas(long casoId) => _personas.DeCaso(casoId);

        /// <summary>El Excel de vuelta de Sandy, que contesta preguntas y propone un estado.</summary>
        public void ElExcelDeSandyContesta(long personaId)
            => _personas.AnotarPropuesta(
                personaId,
                new Persona
                {
                    EstadoPropuesto = "no_completa",
                    NotaCompanero = "El líder no contestó el teléfono.",
                    PasoPreparacion = true,
                    PasoInformacion = true,
                    PasoCitaDelTemplo = true,
                    PasoAccionesRequeridas = true,
                    PasoEntrevistas = false,
                    PasoListoParaElTemplo = false,
                    LlamoAlLider = true,
                },
                _sandy);

        /// <summary>Un Excel que solo dice el estado: no contestó ninguna de las seis.</summary>
        public void ElExcelDeSandyDiceElEstadoYNadaMas(long personaId)
            => _personas.AnotarPropuesta(
                personaId,
                new Persona { EstadoPropuesto = "completa", LlamoAlLider = true },
                _sandy);

        public void MiguelContestaAMano(long personaId)
            => _acciones.Guardar(personaId, new RespuestaALosPasos(true, true, true, true, true, true));

        public FirmaDeLosPasos FirmaDe(long casoId, long personaId)
            => _personas.FirmasDeLosPasosDelCaso(casoId)[personaId];

        public DocumentoConPreguntas Abrir(long casoId)
            => PreguntasDeUnDocumento.De(
                _casos.Obtener(casoId)!,
                Personas(casoId),
                _personas.FirmasDeLosPasosDelCaso(casoId),
                _companeros.Activos(),
                _acciones.QuienContesta());
    }
}
