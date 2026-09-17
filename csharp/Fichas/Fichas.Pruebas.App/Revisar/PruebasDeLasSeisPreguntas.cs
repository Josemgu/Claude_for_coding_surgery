using Fichas.App.Vocabulario;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// La ventana de las seis preguntas, sin ventana (FASE C19, criterios C19-5 a C19-10).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Las aserciones salen de los criterios y de las palabras del dueño</b>, no del
/// codigo que prueban. Sus palabras del 2026-09-05: <i>«cada caso que tengo en revisión
/// debe poder entrar y completar las preguntas que dicen si está listo para viajar o
/// no»</i>.
/// </para>
/// <para>
/// ⚠️ <b>El documento de VARIAS personas hay que fabricarlo.</b> Los siete escaneos reales
/// del dueño traen UNA persona cada uno (medición del supervisor, 2026-09-04), así que
/// sobre sus datos de hoy la diferencia entre «el estado del documento» y «el estado de la
/// persona» no se ve. Los suyos de grupo —los SURB2609— sí traen diez en seis hojas.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasSeisPreguntas
{
    /// <summary>El día en el que se paran los relojes de estas pruebas; es el de la petición del dueño.</summary>
    private const string DiaDeLasPruebas = "2026-09-05";

    // ═════════ C19-5 y C19-6: las seis, con tres respuestas y reversibles ═════════

    /// <summary>
    /// Dado un documento de cinco personas, cuando se abre, entonces salen cinco tickets,
    /// cada uno con sus seis preguntas y su propio estado.
    /// </summary>
    [TestMethod]
    public void UnDocumentoDeCincoPersonasAbreCincoTicketsConSuPropioEstado()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(5);
        banco.Contestar(banco.Personas(caso)[0].Id, LasSeisEnSi);
        banco.Contestar(banco.Personas(caso)[1].Id, LasSeisEnSi with { Entrevistas = false });

        var documento = banco.Abrir(caso);

        Console.WriteLine("== {0} · {1} ==", documento.Titulo, documento.Resumen);
        foreach (var ticket in documento.Personas)
        {
            Console.WriteLine("   {0} · {1} · {2}", ticket.DeQuien, ticket.Cedula, ticket.FraseDelEstado);
        }

        Assert.HasCount(5, documento.Personas, "No salio un ticket por persona.");
        Assert.IsTrue(
            documento.Personas.All(t => t.Preguntas.Count == 6),
            "Algun ticket no trae las seis preguntas.");

        // ⛔ 2026-09-07: la frase decía «lista para viajar · recomendación confirmada» o
        // «sin mirar · recomendación sin confirmar», con TRES de las cuatro palabras que el
        // dueño retiró. Lo que estas pruebas defienden es lo que más importa de aquel criterio
        // y NO cambia: que «sin mirar» y «no lista» sigan siendo cosas distintas, porque dar
        // por lista a una persona de la que faltan preguntas por mirar es lo que manda a
        // alguien al templo con la recomendación mal. La palabra es la misma para las dos; el
        // detalle las separa, y eso es lo que se comprueba.
        Assert.AreEqual(DosEstados.Resuelto, Palabra(documento.Personas[0].FraseDelEstado));
        Assert.AreEqual(DosEstados.MeFalta, Palabra(documento.Personas[1].FraseDelEstado));
        Assert.Contains("dicen que sí", documento.Personas[0].FraseDelEstado);
        Assert.Contains("Entrevistas", documento.Personas[1].FraseDelEstado);
        Assert.AreEqual(DosEstados.MeFalta, Palabra(documento.Personas[2].FraseDelEstado));
        Assert.Contains("nadie ha contestado", documento.Personas[2].FraseDelEstado);
        Assert.AreNotEqual(
            documento.Personas[1].FraseDelEstado,
            documento.Personas[2].FraseDelEstado,
            "«sin mirar» y «no lista» dicen la misma palabra y NO el mismo detalle.");
    }

    /// <summary>
    /// Dadas las respuestas que se pueden elegir, entonces son TRES y la primera es dejarla
    /// en blanco.
    /// </summary>
    /// <remarks>
    /// Criterio C19-6, con su motivo: si contestar fuera irreversible, nadie se atrevería a
    /// contestar. Y «sin mirar» NO es «no»: es una pregunta que nadie miró.
    /// </remarks>
    [TestMethod]
    public void CadaPreguntaTieneTresRespuestasYSePuedeVolverAEnBlanco()
    {
        CollectionAssert.AreEqual(
            new bool?[] { null, true, false },
            PreguntasDeUnDocumento.LasTresRespuestas.ToArray(),
            "Las respuestas que se ofrecen no son las tres, o «sin mirar» no va la primera.");

        Assert.AreEqual("sin mirar", PreguntasDeUnDocumento.DecirLaRespuesta(null));
        Assert.AreEqual("sí", PreguntasDeUnDocumento.DecirLaRespuesta(true));
        Assert.AreEqual("no", PreguntasDeUnDocumento.DecirLaRespuesta(false));

        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        banco.Contestar(persona, LasSeisEnSi);
        banco.Contestar(persona, LasSeisEnSi with { Entrevistas = null });

        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine(
            "== Tras volver una a blanco: {0} ==",
            string.Join(", ", ticket.Preguntas.Select(p => PreguntasDeUnDocumento.DecirLaRespuesta(p.Respuesta))));

        Assert.IsNull(ticket.Preguntas[4].Respuesta, "No se pudo volver a dejar en blanco una ya contestada.");
        Assert.AreEqual(DosEstados.MeFalta, Palabra(ticket.FraseDelEstado));
        // Hasta el 2026-09-17 aquí se fijaba «nadie ha contestado»: con cinco en «sí» y una
        // en blanco, la ventana decía eso y se leía como que no se guardó (medido con la
        // v16). Ahora dice cuál queda en blanco, la misma frase que la tarjeta y el grupo.
        // Lo que se vigila sigue siendo lo mismo: en blanco NO es «sí» ni es «no».
        Assert.DoesNotContain("dicen que sí", ticket.FraseDelEstado, "La que volvió a blanco se leyó como «sí».");
        Assert.DoesNotContain("se quedó en", ticket.FraseDelEstado, "La que volvió a blanco se leyó como «no».");
        Assert.Contains("le falta 1 de 6: Entrevistas", ticket.FraseDelEstado, "No dice cuál es la que quedó en blanco.");
    }

    /// <summary>
    /// Dados los seis rótulos de esta pantalla, entonces son los mismos que los de
    /// <c>Fichas.Reportes.Reglas.Pasos</c>, uno a uno y en el mismo orden.
    /// </summary>
    /// <remarks>
    /// Los de allí son privados y no se pueden referenciar, así que están escritos dos
    /// veces. Que no se separen no se deja a la buena fe: si alguien renombra uno, la
    /// pantalla y el reporte dirían cosas distintas de la misma pregunta.
    /// </remarks>
    [TestMethod]
    public void LosSeisRotulosSonLosMismosQueLosDeCorreccion()
    {
        CollectionAssert.AreEqual(
            Fichas.App.Correccion.LoQueContestoElCompanero.RotulosDeLosPasos.ToArray(),
            PreguntasDeUnDocumento.Rotulos.ToArray(),
            "Los seis rótulos de esta pantalla se separaron de los de Corrección.");
    }

    // ═════════ C19-7: se escriben las seis y las tres de la firma ═════════

    /// <summary>
    /// Dada una persona sin contestar, cuando se guardan sus seis, entonces se escriben con
    /// quién, cuándo y desde dónde, y el origen es el del criterio.
    /// </summary>
    [TestMethod]
    public void GuardarEscribeLasSeisYFirmaQuienCuandoYDesdeDonde()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        var escritura = banco.Contestar(persona, LasSeisEnSi);
        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine("== {0} ==", ticket.LineaDeLaFirma);

        Assert.IsTrue(escritura.SeEscribio, "No se guardaron las seis preguntas.");
        Assert.Contains("Miguel", ticket.LineaDeLaFirma);
        // El texto del origen se comprueba en la linea que VUELVE de la base, y no
        // comparando la constante consigo misma: eso pasaria en verde siempre.
        Assert.Contains("a mano en la pantalla", ticket.LineaDeLaFirma);
        Assert.AreEqual(DosEstados.Resuelto, Palabra(ticket.FraseDelEstado));
        Assert.Contains("dicen que sí", ticket.FraseDelEstado);
    }

    /// <summary>
    /// Dado un documento de cinco, cuando se guardan las seis de UNA, entonces el resumen
    /// dice cuántas van y las otras cuatro siguen sin firma.
    /// </summary>
    /// <remarks>
    /// El denominador se ve siempre (criterio C1-1): una cifra sin su denominador no se
    /// puede comprobar. Y se cuenta en PERSONAS: una familia de cinco donde falla uno no es
    /// «un documento no completo», es una persona de cinco.
    /// </remarks>
    [TestMethod]
    public void ElResumenCuentaPersonasYLasDemasNoSeTocan()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(5);

        var antes = banco.Abrir(caso);
        banco.Contestar(banco.Personas(caso)[2].Id, LasSeisEnSi);
        var despues = banco.Abrir(caso);

        Console.WriteLine("== Antes: {0} · Despues: {1} ==", antes.Resumen, despues.Resumen);

        Assert.AreEqual("0 personas de 5 con la recomendación confirmada.", antes.Resumen);
        Assert.AreEqual("1 persona de 5 con la recomendación confirmada.", despues.Resumen);

        var sinFirma = despues.Personas
            .Where((_, i) => i != 2)
            .Count(t => t.LineaDeLaFirma == PreguntasDeUnDocumento.NadieHaContestado);

        Assert.AreEqual(4, sinFirma, "Contestar de una persona dejo firma en las demas.");
    }

    // ═════════ C19-9: se dice quién contestó ANTES de dejar cambiarlo ═════════

    /// <summary>
    /// Dada una respuesta que dejó Sandy, cuando la mira Miguel, entonces la línea dice el
    /// nombre de Sandy y el ticket avisa de que lo contestó otro.
    /// </summary>
    /// <remarks>
    /// ⛔ Criterio C19-9. Sobrescribir la respuesta de Sandy sin que se vea que era suya es
    /// la forma de que nadie sepa nunca de quién se fía.
    /// </remarks>
    [TestMethod]
    public void SiLoContestoUnAgenteLoDiceConSuNombreAntesDeDejarCambiarlo()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        banco.Personas_.ResponderLosPasos(
            persona, LasSeisEnSi with { Entrevistas = false }, banco.Sandy, @"C:\Fichas\paquetes\sandy.xlsx");

        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine("== {0} ==", ticket.LineaDeLaFirma);

        Assert.Contains("Sandy", ticket.LineaDeLaFirma);
        Assert.Contains("sandy.xlsx", ticket.LineaDeLaFirma);
        Assert.IsTrue(ticket.LoContestoOtro, "No avisa de que lo contesto otra persona.");
    }

    /// <summary>
    /// Dada una persona de la que no ha contestado nadie, entonces se dice con esas
    /// palabras y no con un hueco.
    /// </summary>
    [TestMethod]
    public void LaQueNadieHaContestadoLoDiceYNoDejaUnHueco()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(1);

        var ticket = banco.Abrir(caso).Personas[0];

        Assert.AreEqual(PreguntasDeUnDocumento.NadieHaContestado, ticket.LineaDeLaFirma);
        Assert.IsFalse(ticket.LoContestoOtro, "Dice que lo contesto otro cuando no ha contestado nadie.");
    }

    // ═════════ C19-10: nunca se firma a nombre de quien no fue ═════════

    /// <summary>
    /// Dado un equipo SIN administrador —una sola fila, Sandy, como la base viva del
    /// dueño—, cuando se intenta contestar, entonces NO se escribe y se dice qué falta,
    /// sin nombrar a Sandy.
    /// </summary>
    /// <remarks>
    /// ⛔ Es el defecto del criterio C19-10, medido: <c>AccionesDeRevisar.QuienFirmaAMano</c>
    /// cogería «el primer activo», que en su base es Sandy, y el reporte diría que Sandy
    /// miró el sistema del obispo de gente que no miró. Ofrecer un nombre aquí es el paso
    /// previo a firmar con él.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorNoSeEscribeNadaYNoSeFirmaConSandy()
    {
        var banco = new BancoDePreguntas(conAdministrador: false);
        var caso = banco.SembrarUnDocumentoDe(1);
        var persona = banco.Personas(caso)[0].Id;

        var escritura = banco.Contestar(persona, LasSeisEnSi);
        var ticket = banco.Abrir(caso).Personas[0];

        Console.WriteLine("== {0} ==", escritura.Avisos[0].Linea);

        Assert.IsFalse(escritura.SeEscribio, "Se contesto sin saber quien contestaba.");
        Assert.IsNull(banco.QuienContesta(), "Se eligio a alguien para firmar sin que hubiera administrador.");
        Assert.AreEqual(PreguntasDeUnDocumento.NadieHaContestado, ticket.LineaDeLaFirma);

        var todoLoQueSeDijo = string.Join(" ", escritura.Avisos.Select(a => a.Linea + " " + a.Detalle));
        Assert.DoesNotContain("Sandy", todoLoQueSeDijo, "El aviso nombra a Sandy, y eso es el paso previo a firmar con ella.");
        Assert.Contains("administrador", todoLoQueSeDijo);
    }

    /// <summary>
    /// Dado un equipo con administrador, cuando se pregunta quién contesta, entonces es él
    /// y NO el primer activo de la lista.
    /// </summary>
    [TestMethod]
    public void QuienContestaEsElAdministradorYNoElPrimeroDeLaLista()
    {
        var banco = new BancoDePreguntas();

        var quien = banco.QuienContesta();

        Console.WriteLine("== Firma {0} (el primer activo de la lista es Sandy) ==", quien?.Nombre);

        Assert.IsNotNull(quien, "Con un administrador en la lista, alguien tiene que poder firmar.");
        Assert.AreEqual("Miguel", quien.Nombre, "Firmo alguien que no es el administrador.");
    }

    // ═════════ Regla permanente 5: esto no firma ni un campo ═════════

    /// <summary>
    /// Dada una base con campos sin firmar, cuando se contestan las seis, entonces
    /// <c>procedencia_campo</c> con <c>verificado = 1</c> sigue en CERO.
    /// </summary>
    /// <remarks>
    /// ⛔ Criterio C19-8. Contestar las seis y firmar un campo son dos cosas, y esta prueba
    /// es lo que impide que esta pantalla se convierta, sin que nadie lo decida, en un botón
    /// que marca campos como verificados.
    /// </remarks>
    [TestMethod]
    public void ContestarLasSeisNoFirmaNiUnCampo()
    {
        var banco = new BancoDePreguntas();
        var caso = banco.SembrarUnDocumentoDe(3);

        var antes = banco.CamposFirmados();
        foreach (var persona in banco.Personas(caso)) banco.Contestar(persona.Id, LasSeisEnSi);
        var despues = banco.CamposFirmados();

        Console.WriteLine("== Campos firmados: antes {0}, despues {1} ==", antes, despues);

        Assert.AreEqual(0, antes, "La base de partida ya traia campos firmados.");
        Assert.AreEqual(0, despues, "Contestar las seis firmo un campo, y firmar campos es otra cosa.");
    }

    /// <summary>Las seis contestadas que sí, que es lo que deja a una persona resuelta.</summary>
    private static RespuestaALosPasos LasSeisEnSi => new(true, true, true, true, true, true);

    /// <summary>
    /// Un almacén inventado con su equipo, para probar la pantalla sin abrir ninguna.
    /// </summary>
    /// <remarks>
    /// El equipo se siembra a propósito con <b>Sandy la primera y Miguel después</b>: es la
    /// forma que caza el defecto del criterio C19-10, porque la regla vieja cogía «el primer
    /// activo» y con este orden se vería igual que la buena.
    /// </remarks>
    private sealed class BancoDePreguntas
    {
        /// <summary>El almacén en memoria sobre el que corren los repositorios falsos.</summary>
        private readonly AlmacenFalso _almacen;
        /// <summary>Los documentos de la prueba.</summary>
        private readonly RepositorioDeCasosFalso _casos;
        /// <summary>El equipo: Sandy primero y, si se pide, Miguel administrador después.</summary>
        private readonly RepositorioDeCompanerosFalso _companeros;
        /// <summary>La procedencia de campos, solo para comprobar que contestar no firma ninguno.</summary>
        private readonly RepositorioDeProcedenciaFalso _procedencia;
        /// <summary>Lo que se prueba: las acciones de las seis preguntas, montadas sobre los falsos.</summary>
        private readonly AccionesDeLasPreguntas _acciones;

        /// <summary>Siembra el equipo y monta las acciones.</summary>
        /// <param name="conAdministrador">Si se da de alta a Miguel como administrador; sin él nadie puede firmar.</param>
        public BancoDePreguntas(bool conAdministrador = true)
        {
            _almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);
            _casos = new RepositorioDeCasosFalso(_almacen);
            Personas_ = new RepositorioDePersonasFalso(_almacen);
            _companeros = new RepositorioDeCompanerosFalso(_almacen);
            _procedencia = new RepositorioDeProcedenciaFalso(_almacen);

            Sandy = _companeros.Guardar(new Companero
            {
                Nombre = "Sandy",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Companero,
            }).Id;

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

        /// <summary>El número interno de Sandy, la compañera que NO es administradora.</summary>
        public long Sandy { get; }

        /// <summary>Mete un documento con esas personas, numeradas.</summary>
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

        /// <summary>Quién firmaría, según la regla del administrador; nulo si no hay exactamente uno.</summary>
        public Companero? QuienContesta() => _acciones.QuienContesta();

        /// <summary>Contesta las seis de una persona por el camino de la ventana.</summary>
        /// <param name="personaId">La persona.</param>
        /// <param name="respuesta">Las seis, cada una en sí, no o en blanco.</param>
        public ResultadoDeEscritura Contestar(long personaId, RespuestaALosPasos respuesta)
            => _acciones.Guardar(personaId, respuesta);

        /// <summary>Lo que vería la ventana de ese documento, compuesto por el mismo camino que ella.</summary>
        /// <param name="casoId">El documento que se abre.</param>
        public DocumentoConPreguntas Abrir(long casoId)
            => PreguntasDeUnDocumento.De(
                _casos.Obtener(casoId)!,
                Personas(casoId),
                Personas_.FirmasDeLosPasosDelCaso(casoId),
                _companeros.Activos(),
                QuienContesta());

        /// <summary>Cuántos campos del documento están firmados «Todo correcto»; tiene que seguir en cero.</summary>
        public int CamposFirmados() => _procedencia.DeRegistro(TablaDeProcedencia.Casos, 1).Count(c => c.Verificado);
    }

    /// <summary>La palabra de estado de una frase: lo que va antes del primer punto medio.</summary>
    /// <remarks>
    /// Desde el 2026-09-07 toda frase de una persona empieza por una de las dos palabras del
    /// dueño y sigue con su detalle. Se parte aquí para que cada prueba diga qué mira: la
    /// palabra, o lo que la explica.
    /// </remarks>
    /// <param name="frase">La frase entera de una persona, con sus trozos separados por « · ».</param>
    private static string Palabra(string frase) => frase.Split(" · ")[0];
}
