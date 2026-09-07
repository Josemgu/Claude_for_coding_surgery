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

        Assert.Contains("lista para viajar", documento.Personas[0].FraseDelEstado);
        Assert.Contains("no lista para viajar", documento.Personas[1].FraseDelEstado);
        Assert.Contains("Entrevistas", documento.Personas[1].FraseDelEstado);
        Assert.Contains("sin mirar", documento.Personas[2].FraseDelEstado);
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
        Assert.Contains("sin mirar", ticket.FraseDelEstado);
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
        Assert.Contains("lista para viajar", ticket.FraseDelEstado);
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
        private readonly AlmacenFalso _almacen;
        private readonly RepositorioDeCasosFalso _casos;
        private readonly RepositorioDeCompanerosFalso _companeros;
        private readonly RepositorioDeProcedenciaFalso _procedencia;
        private readonly AccionesDeLasPreguntas _acciones;

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

        public IPersonas Personas_ { get; }

        public long Sandy { get; }

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

        public IReadOnlyList<Persona> Personas(long casoId) => Personas_.DeCaso(casoId);

        public Companero? QuienContesta() => _acciones.QuienContesta();

        public ResultadoDeEscritura Contestar(long personaId, RespuestaALosPasos respuesta)
            => _acciones.Guardar(personaId, respuesta);

        public DocumentoConPreguntas Abrir(long casoId)
            => PreguntasDeUnDocumento.De(
                _casos.Obtener(casoId)!,
                Personas(casoId),
                Personas_.FirmasDeLosPasosDelCaso(casoId),
                _companeros.Activos(),
                QuienContesta());

        public int CamposFirmados() => _procedencia.DeRegistro(TablaDeProcedencia.Casos, 1).Count(c => c.Verificado);
    }
}
