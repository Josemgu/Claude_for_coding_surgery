using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Marcar las seis preguntas de una persona de un tirón, probado SIN VENTANA.
/// </summary>
/// <remarks>
/// <para>
/// Del dueño, 2026-09-07: <i>«en el botón de las seis preguntas debe haber un botón que me
/// permita marcar todos los pasos en un solo clic, en el caso de que no quiera crear un
/// paquete para mí, sino que si una persona puede trabajar sola con el sistema, que se pueda
/// hacer sin problema»</i>.
/// </para>
/// <para>
/// ⛔ <b>Esto roza la regla permanente 5 y por eso las pruebas son lo que son.</b> Contestar
/// las seis es decir que alguien fue al sistema del obispo y lo comprobó. Un botón que las
/// marca todas puede convertir «no lo he mirado» en «está todo bien» sin que nadie lo haya
/// mirado, y eso manda a una persona al templo con la recomendación mal. Lo que estas pruebas
/// defienden, por tanto, no es que el botón funcione: es que <b>un solo clic no escribe
/// nada</b>, que lo que se escribe lleva <b>quién y cuándo</b>, y que <b>se distingue en la
/// base</b> de haber contestado una a una.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasSeisDeUnTiron
{
    /// <summary>El día en el que se paran los relojes de estas pruebas; es el de la decisión del atajo.</summary>
    private const string DiaDeLasPruebas = "2026-09-09";

    // ═════════ Un clic no escribe: propone, y firma el de guardar ═════════

    /// <summary>
    /// Dado un documento sin contestar, cuando se pulsa el botón de un tirón, entonces la
    /// base sigue exactamente igual: nadie ha contestado nada todavía.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la defensa de la regla permanente 5 y el motivo de que el botón proponga en vez
    /// de escribir: entre el clic y la firma él ve las seis puestas en «sí» y puede
    /// arrepentirse. Lo que escribe sigue siendo «Guardar las seis», que es el gesto que ya
    /// existía y ya se auditaba.
    /// </remarks>
    [TestMethod]
    public void PulsarElBotonDeUnTironNoEscribeNadaEnLaBase()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(2);
        var ticket = banco.Abrir(caso).Personas[0];

        var propuestas = MarcarLasSeisDeUnTiron.LoQuePropone(ticket);

        var despues = banco.Abrir(caso).Personas[0];

        Console.WriteLine(
            "== Propone: {0} ==",
            string.Join(", ", propuestas.Select(p => $"{p.Rotulo}={PreguntasDeUnDocumento.DecirLaRespuesta(p.Respuesta)}")));

        Assert.HasCount(6, propuestas, "El botón no propone las seis.");
        Assert.IsTrue(propuestas.All(p => p.Respuesta == true), "Alguna de las seis no queda en «sí».");
        Assert.AreEqual(
            PreguntasDeUnDocumento.NadieHaContestado,
            despues.LineaDeLaFirma,
            "El botón escribió en la base con un solo clic, y eso es la regla permanente 5.");
        Assert.IsTrue(
            despues.Preguntas.All(p => p.Respuesta is null),
            "El botón dejó respuestas escritas sin que nadie firmara.");
    }

    /// <summary>
    /// Dadas las seis que propone el botón, entonces son los mismos seis rótulos y en el
    /// mismo orden que ya tenía esa persona.
    /// </summary>
    /// <remarks>
    /// Si el botón compusiera su propia lista, un rótulo renombrado dejaría de casar con la
    /// columna que le toca y se escribiría un «sí» en la pregunta de al lado.
    /// </remarks>
    [TestMethod]
    public void LoQueProponeSonLosSeisRotulosDeEsaPersonaYEnSuOrden()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(1);
        var ticket = banco.Abrir(caso).Personas[0];

        var propuestas = MarcarLasSeisDeUnTiron.LoQuePropone(ticket);

        CollectionAssert.AreEqual(
            PreguntasDeUnDocumento.Rotulos.ToArray(),
            propuestas.Select(p => p.Rotulo).ToArray(),
            "Los rótulos que propone el botón no son los de la pantalla.");
    }

    // ═════════ Lo que queda escrito: quién, cuándo, y que fue en bloque ═════════

    /// <summary>
    /// Dado que se guardan las seis de un tirón, entonces quedan las seis en «sí» con quién
    /// las marcó, cuándo, y con un origen que dice que fue de un tirón.
    /// </summary>
    [TestMethod]
    public void GuardarDeUnTironEscribeLasSeisConQuienCuandoYQueFueDeUnTiron()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0];

        var escritura = banco.ContestarDeUnTiron(persona.Id);
        var ticket = banco.Abrir(caso).Personas[0];
        var firma = banco.FirmaDe(caso, persona.Id);

        Console.WriteLine("== {0} ==", ticket.LineaDeLaFirma);
        Console.WriteLine("== por={0} en={1} origen={2} ==", firma.Por, firma.En, firma.Origen);

        Assert.IsTrue(escritura.SeEscribio, "No se guardaron las seis de un tirón.");
        Assert.IsTrue(
            ticket.Preguntas.All(p => p.Respuesta == true),
            "Alguna de las seis no quedó en «sí».");
        Assert.Contains("Miguel", ticket.LineaDeLaFirma, "No quedó anotado quién lo marcó.");
        Assert.Contains(DiaDeLasPruebas[..4], ticket.LineaDeLaFirma, "No quedó anotado cuándo.");
        Assert.Contains("de un tirón", ticket.LineaDeLaFirma, "No se ve que fuera en bloque.");
        Assert.AreEqual(DosEstados.Resuelto, ticket.FraseDelEstado.Split(" · ")[0]);
        Assert.IsNotNull(firma.Por, "No quedó anotado quién en la base.");
        Assert.IsNotNull(firma.En, "No quedó la fecha en la base.");
        Assert.AreEqual(AccionesDeLasPreguntas.OrigenDeUnTiron, firma.Origen);
    }

    /// <summary>
    /// Dadas dos personas, una contestada una a una y otra de un tirón, entonces en la base
    /// se distinguen: el origen no es el mismo.
    /// </summary>
    /// <remarks>
    /// ⛔ Es el criterio del pase: <i>«que él pueda distinguirlo después»</i>. Las seis
    /// columnas de los pasos quedan idénticas en los dos casos —seis «sí» son seis «sí»—, así
    /// que lo único que las separa es <c>pasos_origen</c>. Si los dos orígenes coincidieran,
    /// no habría forma de saber cuál miró alguien y cuál se marcó en bloque.
    /// </remarks>
    [TestMethod]
    public void EnLaBaseSeDistingueLoMarcadoDeUnTironDeLoContestadoUnaAUna()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(2);
        var unaAUna = banco.Personas(caso)[0].Id;
        var deUnTiron = banco.Personas(caso)[1].Id;

        banco.ContestarUnaAUna(unaAUna);
        banco.ContestarDeUnTiron(deUnTiron);

        var personas = banco.Personas(caso);
        var origenUnaAUna = banco.FirmaDe(caso, unaAUna).Origen;
        var origenDeUnTiron = banco.FirmaDe(caso, deUnTiron).Origen;

        Console.WriteLine("== una a una: {0} · de un tirón: {1} ==", origenUnaAUna, origenDeUnTiron);
        Console.WriteLine(
            "== las seis columnas quedan iguales en las dos: [{0}] y [{1}] ==",
            LasSeisDe(personas[0]), LasSeisDe(personas[1]));

        Assert.AreEqual(LasSeisDe(personas[0]), LasSeisDe(personas[1]),
            "Las seis columnas tenían que quedar iguales: lo que las separa es el origen.");
        Assert.AreNotEqual(origenUnaAUna, origenDeUnTiron,
            "Los dos caminos escriben el mismo origen: en la base no se pueden distinguir.");
        Assert.AreEqual(AccionesDeLasPreguntas.OrigenAMano, origenUnaAUna);
        Assert.AreEqual(AccionesDeLasPreguntas.OrigenDeUnTiron, origenDeUnTiron);
    }

    /// <summary>
    /// Dado un equipo SIN administrador, cuando se marcan las seis de un tirón, entonces NO
    /// se escribe nada y se dice qué falta, sin nombrar a nadie.
    /// </summary>
    /// <remarks>
    /// ⛔ El atajo no puede ser también un atajo por la puerta de la firma. Es la misma regla
    /// del criterio C19-10, y aquí importa más: seis respuestas de golpe a nombre de quien no
    /// fue son seis mentiras, no una.
    /// </remarks>
    [TestMethod]
    public void DeUnTironTampocoSeFirmaANombreDeQuienNoFue()
    {
        var banco = new BancoDeUnTiron(conAdministrador: false);
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        var escritura = banco.ContestarDeUnTiron(persona);
        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine("== {0} ==", escritura.Avisos[0].Linea);

        Assert.IsFalse(escritura.SeEscribio, "Se marcaron las seis sin saber quién las marcaba.");
        Assert.AreEqual(PreguntasDeUnDocumento.NadieHaContestado, ticket.LineaDeLaFirma);

        var todoLoQueSeDijo = string.Join(" ", escritura.Avisos.Select(a => a.Linea + " " + a.Detalle));
        Assert.DoesNotContain("Sandy", todoLoQueSeDijo, "El aviso nombra a Sandy, y eso es el paso previo a firmar con ella.");
    }

    // ═════════ Lo que el atajo NO hace ═════════

    /// <summary>
    /// Dado un documento con campos sin firmar, cuando se marcan las seis de un tirón,
    /// entonces <c>procedencia_campo</c> con <c>verificado = 1</c> sigue en CERO.
    /// </summary>
    /// <remarks>
    /// ⛔ Regla permanente 5: «Todo correcto» es de Miguel, campo por campo, y vive en
    /// Corrección. El atajo de las seis no puede convertirse, de paso, en un botón que da
    /// campos por verificados.
    /// </remarks>
    [TestMethod]
    public void MarcarDeUnTironNoFirmaNiUnCampo()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(3);

        var antes = banco.CamposFirmados();
        foreach (var persona in banco.Personas(caso)) banco.ContestarDeUnTiron(persona.Id);
        var despues = banco.CamposFirmados();

        Console.WriteLine("== Campos firmados: antes {0}, despues {1} ==", antes, despues);

        Assert.AreEqual(0, antes, "La base de partida ya traía campos firmados.");
        Assert.AreEqual(0, despues, "Marcar de un tirón firmó un campo, y firmar campos es otra cosa.");
    }

    /// <summary>
    /// Dado un documento de tres, cuando se marcan de un tirón las de UNA, entonces las otras
    /// dos siguen sin contestar.
    /// </summary>
    /// <remarks>
    /// El botón es por PERSONA y no por documento. Uno de diez podría marcarse entero con un
    /// clic, y ahí sí que nadie habría mirado nada: cada persona es un ticket aparte
    /// (ADR-0006 §2.1).
    /// </remarks>
    [TestMethod]
    public void ElTironEsDeUnaPersonaYNoDelDocumentoEntero()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(3);

        banco.ContestarDeUnTiron(banco.Personas(caso)[1].Id);

        var documento = banco.Abrir(caso);
        var sinContestar = documento.Personas.Count(t => t.LineaDeLaFirma == PreguntasDeUnDocumento.NadieHaContestado);

        Console.WriteLine("== {0} ==", documento.Resumen);

        Assert.AreEqual(2, sinContestar, "El tirón de una persona contestó por las demás.");
        Assert.AreEqual("1 persona de 3 con la recomendación confirmada.", documento.Resumen);
    }

    /// <summary>
    /// Dado el estado de la recomendación que escribe el Excel del compañero, cuando se
    /// marcan las seis de un tirón, entonces NO se toca.
    /// </summary>
    /// <remarks>
    /// ⛔ CLAUDE.md §1.5: <c>casos.estado_recomendacion</c> lo escribe el documento que llena
    /// el compañero, con su nombre. Son dos cosas y no se mezclan.
    /// </remarks>
    [TestMethod]
    public void MarcarDeUnTironNoTocaElEstadoQueEscribeElCompanero()
    {
        var banco = new BancoDeUnTiron();
        var caso = banco.SembrarUnDocumentoDe(1);

        var antes = banco.EstadoDelCaso(caso);
        banco.ContestarDeUnTiron(banco.Personas(caso)[0].Id);
        var despues = banco.EstadoDelCaso(caso);

        Console.WriteLine("== estado_recomendacion: antes {0}, despues {1} ==", antes, despues);

        Assert.AreEqual(antes, despues, "El atajo escribió en el estado que es del compañero.");
    }

    /// <summary>Las seis columnas de una persona, en una línea, para poder compararlas.</summary>
    /// <param name="persona">La persona cuyas seis columnas se leen.</param>
    private static string LasSeisDe(Persona persona)
        => string.Join(
            ",",
            new bool?[]
            {
                persona.PasoPreparacion, persona.PasoInformacion, persona.PasoCitaDelTemplo,
                persona.PasoAccionesRequeridas, persona.PasoEntrevistas, persona.PasoListoParaElTemplo,
            }.Select(r => r?.ToString() ?? "nulo"));

    /// <summary>
    /// Un almacén inventado con su equipo, para probar el atajo sin abrir ninguna ventana.
    /// </summary>
    /// <remarks>
    /// El equipo se siembra con <b>Sandy la primera y Miguel después</b> a propósito: es la
    /// forma que caza que se firme con «el primer activo» en vez de con el administrador.
    /// </remarks>
    private sealed class BancoDeUnTiron
    {
        /// <summary>El almacén en memoria sobre el que corren los repositorios falsos.</summary>
        private readonly AlmacenFalso _almacen;
        /// <summary>Los documentos de la prueba.</summary>
        private readonly RepositorioDeCasosFalso _casos;
        /// <summary>El equipo: Sandy primero y, si se pide, Miguel administrador después.</summary>
        private readonly RepositorioDeCompanerosFalso _companeros;
        /// <summary>La procedencia de campos, solo para comprobar que el atajo no firma ninguno.</summary>
        private readonly RepositorioDeProcedenciaFalso _procedencia;
        /// <summary>Lo que se prueba: las acciones de las seis preguntas, montadas sobre los falsos.</summary>
        private readonly AccionesDeLasPreguntas _acciones;

        /// <summary>Siembra el equipo y monta las acciones.</summary>
        /// <param name="conAdministrador">Si se da de alta a Miguel como administrador; sin él nadie puede firmar.</param>
        public BancoDeUnTiron(bool conAdministrador = true)
        {
            _almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 2);
            _casos = new RepositorioDeCasosFalso(_almacen);
            Personas_ = new RepositorioDePersonasFalso(_almacen);
            _companeros = new RepositorioDeCompanerosFalso(_almacen);
            _procedencia = new RepositorioDeProcedenciaFalso(_almacen);

            _companeros.Guardar(new Companero
            {
                Nombre = "Sandy",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Companero,
            });

            if (conAdministrador)
            {
                _companeros.Guardar(new Companero
                {
                    Nombre = "Miguel",
                    CreadoEn = DiaDeLasPruebas + " 09:00:00",
                    Rol = RolDeCompanero.Administrador,
                });
            }

            _acciones = new AccionesDeLasPreguntas(Personas_, _companeros);
        }

        /// <summary>El puerto de personas, expuesto para leer las firmas y las seis columnas escritas.</summary>
        public IPersonas Personas_ { get; }

        /// <summary>Mete un documento «no completa» con esas personas, numeradas.</summary>
        /// <param name="cuantas">Cuántas personas trae el documento.</param>
        /// <returns>El número interno del documento.</returns>
        public long SembrarUnDocumentoDe(int cuantas)
        {
            var caso = _casos.Guardar(new Caso
            {
                NumeroCaso = "SURB2609",
                FechaViaje = "2026-09-17",
                UnidadNombre = "Paramaribo",
                RutaPdf = @"C:\Fichas\entrada\SURB2609_grupo.pdf",
                CreadoEn = DiaDeLasPruebas + " 10:00:00",
                EstadoRecomendacion = Caso.EscribirEstado(EstadoDeRecomendacion.NoCompleta),
            }).Id;

            for (var i = 0; i < cuantas; i++)
            {
                Personas_.Guardar(new Persona
                {
                    CasoId = caso,
                    Mrn = $"055-1111-38{50 + i}",
                    Nombre = $"Persona {i + 1}",
                    FilaFormulario = i + 1,
                });
            }

            return caso;
        }

        /// <summary>Las personas de un documento, releídas de la base.</summary>
        /// <param name="casoId">El documento.</param>
        public IReadOnlyList<Persona> Personas(long casoId) => Personas_.DeCaso(casoId);

        /// <summary>Lo que dice <c>estado_recomendacion</c> ahora mismo, tal cual está guardado.</summary>
        /// <param name="casoId">El documento.</param>
        public string? EstadoDelCaso(long casoId) => _casos.Obtener(casoId)?.EstadoRecomendacion;

        /// <summary>Las tres columnas de la firma tal como quedaron ESCRITAS en la base.</summary>
        /// <param name="casoId">El documento.</param>
        /// <param name="personaId">La persona cuya firma se lee.</param>
        public FirmaDeLosPasos FirmaDe(long casoId, long personaId)
            => Personas_.FirmasDeLosPasosDelCaso(casoId)[personaId];

        /// <summary>Las seis a mano, una a una, que es el camino que ya existía.</summary>
        /// <param name="personaId">La persona a la que se le contestan las seis en «sí».</param>
        public ResultadoDeEscritura ContestarUnaAUna(long personaId)
            => _acciones.Guardar(personaId, new RespuestaALosPasos(true, true, true, true, true, true));

        /// <summary>Las seis de un tirón, que es el atajo nuevo.</summary>
        /// <param name="personaId">La persona a la que se le marcan las seis en «sí».</param>
        public ResultadoDeEscritura ContestarDeUnTiron(long personaId)
            => _acciones.GuardarDeUnTiron(personaId, new RespuestaALosPasos(true, true, true, true, true, true));

        /// <summary>Lo que vería la ventana de ese documento, compuesto por el mismo camino que ella.</summary>
        /// <param name="casoId">El documento que se abre.</param>
        public DocumentoConPreguntas Abrir(long casoId)
            => PreguntasDeUnDocumento.De(
                _casos.Obtener(casoId)!,
                Personas(casoId),
                Personas_.FirmasDeLosPasosDelCaso(casoId),
                _companeros.Activos(),
                _acciones.QuienContesta());

        /// <summary>Cuántos campos del documento están firmados «Todo correcto»; tiene que seguir en cero.</summary>
        public int CamposFirmados() => _procedencia.DeRegistro(TablaDeProcedencia.Casos, 1).Count(c => c.Verificado);
    }
}
