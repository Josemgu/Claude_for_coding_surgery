using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Cuatro de las seis en «sí» y «Guardar las seis»: la base queda con cuatro en «sí» y la
/// ventana lo dice, en vez de decir que nadie ha contestado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Del dueño, 2026-09-17, sobre la v16: <i>«No se guardan los «sí» de
/// las preguntas: las colocas, le das a Guardar y no se guarda»</i>. Medido con la v16
/// publicada y con la v15 sobre la misma base sembrada (informe del programador del 17): las
/// dos escriben <c>paso_* = 1,1,1,1,NULL,NULL</c> con su firma. Lo que sí se midió que
/// contradecía lo guardado es lo que la ventana ENSEÑA después: la línea del estado decía
/// <i>«me falta · nadie ha contestado sus seis preguntas»</i> de una persona con cuatro en
/// «sí» recién escritas, mientras la tarjeta de Revisar y el renglón del grupo ya la leían
/// «a medias» desde el 2026-09-16. Cuatro contestadas no es nadie, y una ventana que lo dice
/// se lee como que no se guardó.
/// </para>
/// <para>
/// Es la prueba de regresión que ata el camino entero sin ventana: lo que producen los seis
/// desplegables → <see cref="PreguntasDeUnDocumento.ComoSeGuarda"/> →
/// <see cref="AccionesDeLasPreguntas.Guardar(long, Fichas.Contratos.Puertos.RespuestaALosPasos)"/>
/// → el puerto → releer → <see cref="PreguntasDeUnDocumento.De"/>. Que el falso escriba lo
/// mismo que SQLite lo vigila <c>PruebaDeContestarLosPasos</c> en Datos, que además fija
/// «cuatro en sí y dos en blanco» sobre la base de verdad leyendo las columnas crudas.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasCuatroEnSi
{
    /// <summary>El día en el que se paran los relojes de estas pruebas.</summary>
    private const string DiaDeLasPruebas = "2026-09-17";

    /// <summary>
    /// Dada una persona sin contestar, cuando se ponen sus cuatro primeras en «sí» y se
    /// guardan las seis tal como quedaron en la pantalla, entonces la base tiene cuatro en
    /// «sí» y dos en blanco, con la firma de quien las contestó.
    /// </summary>
    [TestMethod]
    public void CuatroEnSiYGuardarDejanCuatroEnSiYDosEnBlancoEnLaBase()
    {
        var banco = new BancoDeLasCuatro();
        var persona = banco.SembrarUnaPersona();

        var escritura = banco.GuardarLoQueHayEnLaPantalla(persona, ConCuatroEnSi(banco.Abrir(persona).Preguntas));
        var releida = banco.Personas.Obtener(persona)!;

        Console.WriteLine("== escrito={0} · {1} ==", escritura.SeEscribio, Enumerar(LasSeisDe(releida)));

        Assert.IsTrue(escritura.SeEscribio, "Guardar las seis con cuatro en «sí» dijo que no escribió.");
        CollectionAssert.AreEqual(
            new bool?[] { true, true, true, true, null, null },
            LasSeisDe(releida),
            $"La base no tiene cuatro en «sí» y dos en blanco: tiene {Enumerar(LasSeisDe(releida))}.");

        var firma = banco.Personas.FirmasDeLosPasosDelCaso(releida.CasoId)[persona];
        Assert.AreEqual(banco.Miguel, firma.Por, "Las cuatro en «sí» no quedaron firmadas por quien las contestó.");
        Assert.AreEqual(AccionesDeLasPreguntas.OrigenAMano, firma.Origen, "No quedó escrito que fue a mano en la pantalla.");
    }

    /// <summary>
    /// Dada una persona con cuatro en «sí» recién guardadas, cuando se vuelve a abrir la
    /// ventana, entonces sus desplegables traen esas cuatro en «sí» y la línea del estado
    /// dice cuántas le faltan y cuáles, y NO que nadie ha contestado.
    /// </summary>
    /// <remarks>
    /// Es lo que él lee justo después de pulsar Guardar: la ventana se repinta desde la base.
    /// «Nadie ha contestado» sobre cuatro contestadas es lo que se lee como «no se guardó».
    /// La frase es la misma que la de la tarjeta de Revisar y la del renglón del grupo
    /// (<see cref="LoQueSeLeeDeUnaPersona"/>), para que las tres pantallas digan lo mismo.
    /// </remarks>
    [TestMethod]
    public void TrasGuardarCuatroEnSiLaVentanaLasEnsenaYDiceQueLeFaltanDos()
    {
        var banco = new BancoDeLasCuatro();
        var persona = banco.SembrarUnaPersona();
        banco.GuardarLoQueHayEnLaPantalla(persona, ConCuatroEnSi(banco.Abrir(persona).Preguntas));

        var ticket = banco.Abrir(persona);

        Console.WriteLine("== {0} ==", ticket.FraseDelEstado);

        CollectionAssert.AreEqual(
            new bool?[] { true, true, true, true, null, null },
            ticket.Preguntas.Select(pregunta => pregunta.Respuesta).ToArray(),
            "La ventana no enseña las cuatro en «sí» que acaba de guardar.");
        Assert.AreEqual(DosEstados.MeFalta, Palabra(ticket.FraseDelEstado), "Con dos en blanco sigue siendo «me falta».");
        Assert.DoesNotContain(
            "nadie ha contestado",
            ticket.FraseDelEstado,
            "La ventana dice que nadie ha contestado de una persona con cuatro en «sí» guardadas.");
        Assert.Contains("le faltan 2 de 6", ticket.FraseDelEstado, "La ventana no dice cuántas le faltan.");
        Assert.Contains("Entrevistas", ticket.FraseDelEstado, "La ventana no dice cuáles le faltan.");
        Assert.Contains("Listo para el templo", ticket.FraseDelEstado, "La ventana no dice cuáles le faltan.");
    }

    /// <summary>
    /// Dada una persona con cuatro en «sí» guardadas, cuando se lee la frase con la que la
    /// ventana acusa el guardado, entonces es la misma que la del ticket: no hay dos frases
    /// para la misma persona.
    /// </summary>
    [TestMethod]
    public void ElAcuseDelGuardadoDiceLoMismoQueElTicket()
    {
        var banco = new BancoDeLasCuatro();
        var persona = banco.SembrarUnaPersona();
        banco.GuardarLoQueHayEnLaPantalla(persona, ConCuatroEnSi(banco.Abrir(persona).Preguntas));

        var releida = banco.Personas.Obtener(persona)!;
        var ticket = banco.Abrir(persona);

        Assert.AreEqual(
            ticket.FraseDelEstado,
            PreguntasDeUnDocumento.FraseDelEstadoDe(releida),
            "El acuse y el ticket dicen cosas distintas de la misma persona.");
    }

    /// <summary>Las seis tal como quedan en los desplegables tras poner las cuatro primeras en «sí».</summary>
    /// <param name="deLaPantalla">Las seis que la ventana pintó, todas en blanco.</param>
    private static IReadOnlyList<PreguntaDeUnaPersona> ConCuatroEnSi(IReadOnlyList<PreguntaDeUnaPersona> deLaPantalla)
        => [.. deLaPantalla.Select((pregunta, i) => i < 4 ? pregunta with { Respuesta = true } : pregunta)];

    /// <summary>La palabra con la que empieza la frase del estado: «resuelto» o «me falta».</summary>
    /// <param name="frase">La frase entera del ticket.</param>
    private static string Palabra(string frase) => frase.Split(" · ")[0];

    /// <summary>Las seis de esa persona, en el orden de la pantalla del líder.</summary>
    /// <param name="persona">La persona releída.</param>
    private static bool?[] LasSeisDe(Persona persona) =>
    [
        persona.PasoPreparacion,
        persona.PasoInformacion,
        persona.PasoCitaDelTemplo,
        persona.PasoAccionesRequeridas,
        persona.PasoEntrevistas,
        persona.PasoListoParaElTemplo,
    ];

    /// <summary>Las seis como texto para los mensajes de fallo.</summary>
    /// <param name="seis">Las seis en su orden.</param>
    private static string Enumerar(bool?[] seis)
        => string.Join(", ", seis.Select(v => v switch { true => "sí", false => "no", _ => "en blanco" }));

    /// <summary>Un almacén falso con Miguel de administrador y un documento de una persona.</summary>
    private sealed class BancoDeLasCuatro
    {
        /// <summary>Los documentos de la prueba.</summary>
        private readonly RepositorioDeCasosFalso _casos;
        /// <summary>El equipo: Sandy primero y Miguel administrador después.</summary>
        private readonly RepositorioDeCompanerosFalso _companeros;
        /// <summary>Lo que se prueba: las acciones de la ventana, montadas sobre los falsos.</summary>
        private readonly AccionesDeLasPreguntas _acciones;
        /// <summary>El documento sembrado, para releerlo como lo hace la ventana.</summary>
        private long _caso;

        /// <summary>Siembra el equipo y monta las acciones.</summary>
        public BancoDeLasCuatro()
        {
            var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);
            _casos = new RepositorioDeCasosFalso(almacen);
            Personas = new RepositorioDePersonasFalso(almacen);
            _companeros = new RepositorioDeCompanerosFalso(almacen);

            _companeros.Guardar(new Companero { Nombre = "Sandy", CreadoEn = DiaDeLasPruebas + " 09:00:00" });
            Miguel = _companeros.Guardar(new Companero
            {
                Nombre = "Miguel",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Administrador,
            }).Id;

            _acciones = new AccionesDeLasPreguntas(Personas, _companeros);
        }

        /// <summary>El puerto de personas, para releer lo escrito.</summary>
        public IPersonas Personas { get; }

        /// <summary>El número interno de Miguel, el único administrador.</summary>
        public long Miguel { get; }

        /// <summary>Un documento con una persona sin contestar; devuelve el número interno de la persona.</summary>
        public long SembrarUnaPersona()
        {
            _caso = _casos.Guardar(new Caso
            {
                NumeroCaso = "CASP2609",
                FechaViaje = "2026-09-25",
                UnidadNombre = "Rama de ensayo",
                RutaPdf = @"C:\Fichas\entrada\CASP2609.pdf",
                CreadoEn = DiaDeLasPruebas + " 10:00:00",
            }).Id;

            return Personas.Guardar(new Persona
            {
                CasoId = _caso,
                Mrn = "000-0001-0000",
                Nombre = "Persona 1 de CASP2609",
                FilaFormulario = 1,
            }).Id;
        }

        /// <summary>Guarda las seis tal como están en los desplegables, por el mismo camino que el botón.</summary>
        /// <param name="personaId">La persona.</param>
        /// <param name="elegidas">Las seis con lo elegido en cada desplegable.</param>
        public ResultadoDeEscritura GuardarLoQueHayEnLaPantalla(
            long personaId, IReadOnlyList<PreguntaDeUnaPersona> elegidas)
            => _acciones.Guardar(personaId, PreguntasDeUnDocumento.ComoSeGuarda(elegidas));

        /// <summary>El ticket de esa persona, compuesto por el mismo camino que la ventana al repintar.</summary>
        /// <param name="personaId">La persona.</param>
        public TicketDeUnaPersona Abrir(long personaId)
        {
            var documento = PreguntasDeUnDocumento.De(
                _casos.Obtener(_caso)!,
                Personas.DeCaso(_caso),
                Personas.FirmasDeLosPasosDelCaso(_caso),
                _companeros.Activos(),
                _acciones.QuienContesta());

            return documento.Personas.Single(ticket => ticket.PersonaId == personaId);
        }
    }
}
