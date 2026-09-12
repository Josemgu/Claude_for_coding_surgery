using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Contestar las seis preguntas del sistema del lider desde dentro del programa (FASE C19).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Las pruebas salen de los criterios C19-6 a C19-9 y de las palabras del dueno del
/// 2026-09-05</b> —<i>«cada caso que tengo en revisión debe poder entrar y completar las
/// preguntas que dicen si está listo para viajar o no»</i>—, no del codigo que prueban.
/// </para>
/// <para>
/// <b>Las cuatro cosas que aqui se miden, y por que cada una.</b>
/// </para>
/// <list type="number">
///   <item>Se escriben las seis Y la firma: sin firma, nadie sabe de quien fiarse.</item>
///   <item>Las demas personas del mismo documento NO se tocan: el ticket es la persona
///   (ADR-0006 §2.1), y un documento de diez puede tener tres resueltas y siete no.</item>
///   <item>Lo que dejo escrito el companero —su estado, su nota y su firma— <b>sigue
///   ahi con su nombre</b>. Miguel contesta las preguntas; no borra a Sandy.</item>
///   <item>⛔ No se firma ni un campo. <c>procedencia_campo.verificado</c> es de Miguel,
///   campo por campo, y nunca automatica (regla permanente 5). Contestar las seis y
///   firmar un campo son dos cosas y no se mezclan.</item>
/// </list>
/// </remarks>
[TestClass]
public sealed class PruebaDeContestarLosPasos
{
    /// <summary>La fecha fija de estas pruebas, para que las marcas de tiempo sembradas no dependan del reloj.</summary>
    private const string DiaDeLasPruebas = "2026-09-05";
    /// <summary>Lo que va en <c>pasos_origen</c> cuando contesta Miguel desde la pantalla.</summary>
    private const string OrigenAMano = "a mano en la pantalla";
    /// <summary>Una ruta inventada de Excel de vuelta; no se abre ningún archivo.</summary>
    private const string RutaDelExcelDeSandy = @"C:\Fichas\paquetes\sandy-2609.xlsx";

    /// <summary>
    /// Dada una persona sin contestar, cuando se contestan sus seis, entonces al releer la
    /// base estan las seis escritas y con quien, cuando y desde donde.
    /// </summary>
    [TestMethod]
    public void ContestarLasSeisLasEscribeConQuienCuandoYDesdeDonde()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, _, persona) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);

        var escritura = personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);

        Assert.IsTrue(escritura.SeEscribio, "No se escribieron las seis preguntas.");

        var despues = personas.Obtener(persona)!;
        var firma = personas.FirmasDeLosPasosDelCaso(despues.CasoId)[persona];

        Console.WriteLine(
            "== Contestadas las seis de la persona {0}: por {1}, el {2}, desde «{3}» ==",
            persona,
            firma.Por,
            firma.En,
            firma.Origen);

        CollectionAssert.AreEqual(
            new bool?[] { true, true, true, true, true, true },
            LasSeisDe(despues),
            "Las seis respuestas no quedaron escritas.");

        Assert.AreEqual(miguel, firma.Por, "No quedo escrito QUIEN contesto.");
        Assert.AreEqual(OrigenAMano, firma.Origen, "No quedo escrito DESDE DONDE se contesto.");
        Assert.IsNotNull(firma.En, "No quedo escrito CUANDO se contesto.");
        Assert.IsTrue(
            DateTime.TryParse(firma.En, out _),
            $"La fecha de la respuesta «{firma.En}» no se entiende como fecha.");
    }

    /// <summary>
    /// Dado un documento de cinco personas, cuando se contestan las seis de UNA, entonces
    /// las otras cuatro se quedan exactamente como estaban.
    /// </summary>
    /// <remarks>
    /// Es el corazon del modelo nuevo: el ticket es la persona. Con los siete escaneos
    /// reales del dueno —una persona cada uno, medido por el supervisor el 2026-09-04—
    /// esta diferencia NO se ve, y por eso el documento de varias hay que fabricarlo.
    /// </remarks>
    [TestMethod]
    public void ContestarLasDeUnaPersonaNoTocaALasOtrasDelMismoDocumento()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, caso, cinco) = SembrarUnDocumentoDeCincoPersonas(baseDePrueba.Conexion);

        var antes = personas.DeCaso(caso).ToDictionary(p => p.Id, p => p);

        personas.ResponderLosPasos(cinco[2], LasSeisEnSi, miguel, OrigenAMano);

        var despues = personas.DeCaso(caso).ToDictionary(p => p.Id, p => p);
        var firmas = personas.FirmasDeLosPasosDelCaso(caso);
        var conFirma = firmas.Count(f => f.Value.Por is not null);

        Console.WriteLine(
            "== Documento de {0} personas: contestada la n.º {1}; con firma quedan {2} ==",
            despues.Count,
            cinco[2],
            conFirma);

        Assert.HasCount(5, despues, "El documento dejo de tener cinco personas.");
        Assert.AreEqual(1, conFirma, "Se firmaron las preguntas de mas de una persona.");

        foreach (var id in cinco.Where(id => id != cinco[2]))
        {
            CollectionAssert.AreEqual(
                LasSeisDe(antes[id]),
                LasSeisDe(despues[id]),
                $"La persona {id} cambio de respuestas sin que nadie la contestara.");
            Assert.IsNull(firmas[id].Por, $"La persona {id} quedo firmada sin que nadie la contestara.");
        }
    }

    /// <summary>
    /// Dada una pregunta ya contestada, cuando se vuelve a dejar en blanco, entonces se
    /// queda en blanco: contestar no es irreversible.
    /// </summary>
    /// <remarks>
    /// Criterio C19-6, y su motivo esta escrito alli: <i>si contestar fuera irreversible,
    /// nadie se atreveria a contestar</i>. Y en blanco NO es «no»: es una pregunta que
    /// nadie miro (comentario de <c>Pasos.Estado</c>).
    /// </remarks>
    [TestMethod]
    public void UnaRespuestaSePuedeVolverADejarEnBlanco()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, _, persona) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);

        personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);
        personas.ResponderLosPasos(
            persona,
            LasSeisEnSi with { Entrevistas = null, ListoParaElTemplo = false },
            miguel,
            OrigenAMano);

        var despues = LasSeisDe(personas.Obtener(persona)!);

        Console.WriteLine("== Las seis tras volver una a blanco: {0} ==", Enumerar(despues));

        CollectionAssert.AreEqual(
            new bool?[] { true, true, true, true, null, false },
            despues,
            "No se pudo volver a dejar en blanco una pregunta ya contestada.");
    }

    /// <summary>
    /// Dada una persona de la que ya contesto Sandy por su Excel, cuando Miguel contesta
    /// encima, entonces lo de Sandy SIGUE AHI con su nombre y su fecha.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la regla que el dueno fijo el 2026-09-03 para el estado —<i>«lo que Miguel
    /// marque a mano despues manda… sin borrar lo que dijo el companero»</i>— aplicada
    /// aqui: <c>estado_propuesto</c>, <c>nota_companero</c>, <c>propuesto_por</c> y
    /// <c>propuesto_en</c> son de Sandy y esta escritura no los toca. Lo unico que cambia
    /// de manos son las seis respuestas, que no son la opinion de nadie: son lo que pone
    /// en la pantalla del obispo (ADR-0006 §2.5).
    /// </remarks>
    [TestMethod]
    public void LoQueContestoElCompaneroSigueAhiConSuNombre()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, _, persona) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);
        var sandy = new RepositorioDeCompaneros(baseDePrueba.Conexion)
            .Guardar(new Companero { Nombre = "Sandy", CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;

        personas.AnotarPropuesta(
            persona,
            new Persona
            {
                EstadoPropuesto = "no_completa",
                NotaCompanero = "El líder no contestó el teléfono.",
                PasoPreparacion = true,
                PasoEntrevistas = false,
                LlamoAlLider = true,
            },
            sandy);

        var loDeSandy = personas.Obtener(persona)!;

        personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);

        var despues = personas.Obtener(persona)!;
        var firma = personas.FirmasDeLosPasosDelCaso(despues.CasoId)[persona];

        Console.WriteLine(
            "== Tras contestar Miguel: propuesto_por sigue en {0} (Sandy es {1}); pasos_por = {2} (Miguel es {3}) ==",
            despues.PropuestoPor,
            sandy,
            firma.Por,
            miguel);

        Assert.AreEqual(sandy, despues.PropuestoPor, "Miguel piso el nombre de Sandy.");
        Assert.AreEqual(loDeSandy.PropuestoEn, despues.PropuestoEn, "Miguel piso la fecha de Sandy.");
        Assert.AreEqual("no_completa", despues.EstadoPropuesto, "Miguel piso el estado que propuso Sandy.");
        Assert.AreEqual(
            "El líder no contestó el teléfono.",
            despues.NotaCompanero,
            "Miguel piso la nota que escribió Sandy.");
        Assert.IsTrue(
            despues.LlamoAlLider,
            "Contestar las seis borró que Sandy llamó al líder, y eso NO es un séptimo paso.");
        Assert.AreEqual(miguel, firma.Por, "La firma de las seis no quedó a nombre de quien las contestó.");
    }

    /// <summary>
    /// Dada una respuesta de Sandy que llego por su Excel, cuando se lee su firma, entonces
    /// dice su nombre y la ruta del Excel: los dos origenes se distinguen.
    /// </summary>
    /// <remarks>
    /// Es lo que hace falta para el criterio C19-9: la pantalla tiene que poder decir «esto
    /// lo contesto Sandy el 4 de septiembre desde su Excel» ANTES de dejar cambiarlo.
    /// <c>pasos_origen</c> es texto libre y sin catalogo, igual que
    /// <c>estado_marcado_origen</c> de la migracion 14.
    /// </remarks>
    [TestMethod]
    public void LaFirmaDistingueLaViaDeSandyDeLaDeMiguel()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, caso, primera) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);
        var sandy = new RepositorioDeCompaneros(baseDePrueba.Conexion)
            .Guardar(new Companero { Nombre = "Sandy", CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;
        var segunda = personas.Guardar(
            new Persona { CasoId = caso, Mrn = "055-1111-3853", Nombre = "Julia", FilaFormulario = 2 }).Id;

        personas.ResponderLosPasos(primera, LasSeisEnSi, miguel, OrigenAMano);
        personas.ResponderLosPasos(segunda, LasSeisEnSi, sandy, RutaDelExcelDeSandy);

        var firmas = personas.FirmasDeLosPasosDelCaso(caso);

        Console.WriteLine(
            "== Dos vias en el mismo documento: {0} desde «{1}», {2} desde «{3}» ==",
            firmas[primera].Por,
            firmas[primera].Origen,
            firmas[segunda].Por,
            firmas[segunda].Origen);

        Assert.AreEqual(OrigenAMano, firmas[primera].Origen, "La via de Miguel no quedo escrita.");
        Assert.AreEqual(RutaDelExcelDeSandy, firmas[segunda].Origen, "La via de Sandy no quedo escrita.");
        Assert.AreNotEqual(
            firmas[primera].Por,
            firmas[segunda].Por,
            "Las dos respuestas quedaron a nombre de la misma persona.");
    }

    /// <summary>
    /// ⛔ Dada una base con campos sin firmar, cuando se contestan las seis, entonces
    /// <c>procedencia_campo</c> con <c>verificado = 1</c> sigue en CERO filas.
    /// </summary>
    /// <remarks>
    /// Criterio C19-8 y regla permanente 5. Es la prueba que impide que esta pantalla se
    /// convierta, sin que nadie lo decida, en un boton que marca campos como verificados.
    /// </remarks>
    [TestMethod]
    public void ContestarLasSeisNoFirmaNiUnCampo()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, caso, persona) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);

        new RepositorioDeProcedencia(baseDePrueba.Conexion).Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso,
            Campo = "fecha_viaje",
            Origen = OrigenDeCampo.Ocr,
            ValorOcr = "2026-09-17",
        });

        var antes = Firmados(baseDePrueba.Conexion);
        personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);
        var despues = Firmados(baseDePrueba.Conexion);

        Console.WriteLine("== procedencia_campo con verificado = 1: antes {0}, despues {1} ==", antes, despues);

        Assert.AreEqual(0L, antes, "La base de partida ya traia campos firmados.");
        Assert.AreEqual(0L, despues, "Contestar las seis firmo un campo, y firmar campos es otra cosa.");
    }

    /// <summary>
    /// Dada una persona que no esta, cuando se intentan contestar sus seis, entonces se
    /// dice que no se escribio y NO se lanza.
    /// </summary>
    /// <remarks>
    /// Es el contrato de <c>ResultadoDeEscritura</c> en todo el proyecto: se DICE, no se
    /// lanza, para que la pantalla pueda ensenarlo en la franja y seguir viva.
    /// </remarks>
    [TestMethod]
    public void ContestarLasSeisDeUnaPersonaQueNoEstaSeDiceYNoSeLanza()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, _, _) = SembrarUnDocumentoDeUnaPersona(baseDePrueba.Conexion);

        var escritura = personas.ResponderLosPasos(9999, LasSeisEnSi, miguel, OrigenAMano);

        Assert.IsFalse(escritura.SeEscribio, "Dijo que escribio las seis de una persona que no existe.");
        Assert.IsNotEmpty(escritura.Avisos, "No se escribio y no dijo por que.");
    }

    // ═════════════════════ paridad: el falso hace lo mismo ═════════════════════

    /// <summary>
    /// Dada la misma operacion, cuando se contesta en el de verdad y en el falso, entonces
    /// los dos escriben lo mismo: las seis, la firma, y nada del companero.
    /// </summary>
    /// <remarks>
    /// El motivo esta escrito en <see cref="PruebaDeLaParidadDelFalso"/>: un falso que no
    /// se comporta como el original deja pruebas de pantalla en verde sobre un hueco real.
    /// </remarks>
    [TestMethod]
    public void ElFalsoContestaLasSeisIgualQueElDeVerdad()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);

        var deVerdad = new RepositorioDePersonas(baseDePrueba.Conexion);
        var falso = new RepositorioDePersonasFalso(almacen);
        var companerosDeVerdad = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var companerosFalsos = new RepositorioDeCompanerosFalso(almacen);
        var casosDeVerdad = new RepositorioDeCasos(baseDePrueba.Conexion);
        var casosFalsos = new RepositorioDeCasosFalso(almacen);

        var quienEs = new Companero { Nombre = "Miguel", CreadoEn = DiaDeLasPruebas + " 09:00:00" };
        var miguelDeVerdad = companerosDeVerdad.Guardar(quienEs).Id;
        var miguelFalso = companerosFalsos.Guardar(quienEs).Id;

        var elCaso = new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" };
        var casoDeVerdad = casosDeVerdad.Guardar(elCaso).Id;
        var casoFalso = casosFalsos.Guardar(elCaso).Id;

        var laPersona = new Persona { Mrn = "055-1111-385A", Nombre = "Elena", FilaFormulario = 1 };
        var personaDeVerdad = deVerdad.Guardar(laPersona with { CasoId = casoDeVerdad }).Id;
        var personaFalsa = falso.Guardar(laPersona with { CasoId = casoFalso }).Id;

        var respuesta = LasSeisEnSi with { Entrevistas = false, ListoParaElTemplo = null };
        var enElDeVerdad = deVerdad.ResponderLosPasos(personaDeVerdad, respuesta, miguelDeVerdad, OrigenAMano);
        var enElFalso = falso.ResponderLosPasos(personaFalsa, respuesta, miguelFalso, OrigenAMano);

        Assert.AreEqual(enElDeVerdad.SeEscribio, enElFalso.SeEscribio, "Uno escribio y el otro no.");

        CollectionAssert.AreEqual(
            LasSeisDe(deVerdad.Obtener(personaDeVerdad)!),
            LasSeisDe(falso.Obtener(personaFalsa)!),
            "El falso no escribio las mismas seis respuestas que el de verdad.");

        var firmaDeVerdad = deVerdad.FirmasDeLosPasosDelCaso(casoDeVerdad)[personaDeVerdad];
        var firmaFalsa = falso.FirmasDeLosPasosDelCaso(casoFalso)[personaFalsa];

        Assert.AreEqual(miguelDeVerdad, firmaDeVerdad.Por, "El de verdad no firmo con quien contesto.");
        Assert.AreEqual(miguelFalso, firmaFalsa.Por, "El falso no firmo con quien contesto.");
        Assert.AreEqual(firmaDeVerdad.Origen, firmaFalsa.Origen, "Los dos no escribieron la misma via.");
        Assert.AreEqual(
            firmaDeVerdad.En is null,
            firmaFalsa.En is null,
            "Uno escribio la fecha de la respuesta y el otro la dejo vacia.");
    }

    // ═════════════════════════════ utilidades ═════════════════════════════

    /// <summary>Las seis en «si», que es el punto de partida de casi todas las pruebas.</summary>
    private static RespuestaALosPasos LasSeisEnSi => new(true, true, true, true, true, true);

    /// <summary>Las seis de esa persona, en el orden de la pantalla del lider.</summary>
    private static bool?[] LasSeisDe(Persona persona) =>
    [
        persona.PasoPreparacion,
        persona.PasoInformacion,
        persona.PasoCitaDelTemplo,
        persona.PasoAccionesRequeridas,
        persona.PasoEntrevistas,
        persona.PasoListoParaElTemplo,
    ];

    /// <summary>Las seis casillas como texto («sí, no, en blanco…») para los mensajes de fallo.</summary>
    /// <param name="seis">Los seis pasos en su orden.</param>
    private static string Enumerar(bool?[] seis)
        => string.Join(", ", seis.Select(v => v switch { true => "sí", false => "no", _ => "en blanco" }));

    /// <summary>Cuántas filas de <c>procedencia_campo</c> están firmadas: tiene que seguir en cero después de contestar.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    private static long Firmados(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM procedencia_campo WHERE verificado = 1";
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Un documento con una sola persona, como los siete escaneos reales del dueno.</summary>
    private static (long Miguel, long Caso, long Persona) SembrarUnDocumentoDeUnaPersona(SqliteConnection conexion)
    {
        var miguel = new RepositorioDeCompaneros(conexion)
            .Guardar(new Companero { Nombre = "Miguel", CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;
        var caso = new RepositorioDeCasos(conexion)
            .Guardar(new Caso { NumeroCaso = "CASP2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" }).Id;
        var persona = new RepositorioDePersonas(conexion)
            .Guardar(new Persona { CasoId = caso, Mrn = "055-1111-385A", Nombre = "Elena", FilaFormulario = 1 }).Id;

        return (miguel, caso, persona);
    }

    /// <summary>
    /// Un documento con CINCO personas, que es el que hace falta para ver el cambio.
    /// </summary>
    /// <remarks>
    /// Los siete escaneos del dueno traen UNA persona cada uno, asi que sobre sus datos de
    /// hoy la diferencia entre «el estado del documento» y «el estado de la persona» no se
    /// ve. Los suyos de grupo —los SURB2609— si traen diez en seis hojas.
    /// </remarks>
    private static (long Miguel, long Caso, List<long> Personas) SembrarUnDocumentoDeCincoPersonas(
        SqliteConnection conexion)
    {
        var miguel = new RepositorioDeCompaneros(conexion)
            .Guardar(new Companero { Nombre = "Miguel", CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;
        var caso = new RepositorioDeCasos(conexion)
            .Guardar(new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" }).Id;
        var personas = new RepositorioDePersonas(conexion);

        var nombres = new[] { "Elena", "Julia", "Marlon", "Damian", "Jonas" };
        var ids = nombres
            .Select((nombre, i) => personas.Guardar(new Persona
            {
                CasoId = caso,
                Mrn = $"055-1111-38{(50 + i).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Nombre = nombre,
                FilaFormulario = i + 1,
            }).Id)
            .ToList();

        return (miguel, caso, ids);
    }
}
