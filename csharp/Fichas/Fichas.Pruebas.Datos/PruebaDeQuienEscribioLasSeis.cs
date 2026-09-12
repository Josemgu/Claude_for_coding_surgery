using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Quien escribe los seis <c>paso_*</c> es quien queda como que los contesto, venga del
/// Excel o de la pantalla.
/// </summary>
/// <remarks>
/// <para>
/// <b>El defecto que estas pruebas cierran, y donde estaba.</b> <c>AnotarPropuesta</c> —el
/// Excel que devuelve el companero— escribia los seis <c>paso_*</c> en el MISMO UPDATE que
/// <c>propuesto_por</c> y dejaba <c>pasos_por</c> apuntando a quien hubiera contestado
/// antes. La base quedaba diciendo que contesto alguien que no fue, y por eso la ventana de
/// las seis de Revisar decia «nadie ha contestado» de una persona cuyas seis SI habia
/// contestado el Excel de Sandy: <see cref="IPersonas.FirmasDeLosPasosDelCaso"/> lee esas
/// tres columnas y no habia nada escrito en ellas.
/// </para>
/// <para>
/// ⛔ <b>La regla que no es obvia y aqui se respeta.</b> Un Excel que dice el estado del
/// caso y deja las seis EN BLANCO <b>no</b> cuenta como que ese companero las contesto:
/// firmarlas ahi le atribuye un trabajo que no hizo. Es la misma regla que ya sigue la
/// pantalla de Correccion, medida por otro programador antes que esto.
/// </para>
/// <para>
/// ⚠️ <b>Las aserciones salen del criterio de cierre</b> —«aplicado un Excel de vuelta, la
/// base dice que contesto ese companero, con su fecha y su origen; y si despues Miguel
/// contesta encima, dice Miguel»—, no del codigo que prueban.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeQuienEscribioLasSeis
{
    /// <summary>La fecha fija de estas pruebas, para que las marcas de tiempo sembradas no dependan del reloj.</summary>
    private const string DiaDeLasPruebas = "2026-09-05";
    /// <summary>Lo que va en <c>pasos_origen</c> cuando contesta Miguel desde la pantalla.</summary>
    private const string OrigenAMano = "a mano en la pantalla";

    /// <summary>
    /// Dado un Excel de Sandy que contesta las seis, cuando se aplica, entonces la base dice
    /// que las contesto Sandy, con su fecha y su origen.
    /// </summary>
    [TestMethod]
    public void ElExcelQueContestaLasSeisQuedaComoQuienLasContesto()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (_, caso, persona) = SembrarUnDocumento(baseDePrueba.Conexion);
        var sandy = Companero(baseDePrueba.Conexion, "Sandy");

        personas.AnotarPropuesta(persona, ElExcelDeSandy, sandy);

        var firma = personas.FirmasDeLosPasosDelCaso(caso)[persona];

        Console.WriteLine(
            "== Tras aplicar el Excel: contesto {0} (Sandy es {1}), el {2}, desde «{3}» ==",
            firma.Por,
            sandy,
            firma.En,
            firma.Origen);

        Assert.AreEqual(sandy, firma.Por, "La base no dice que las seis las contesto el companero del Excel.");
        Assert.IsNotNull(firma.En, "No quedo escrito CUANDO se contestaron.");
        Assert.IsTrue(
            DateTime.TryParse(firma.En, out _),
            $"La fecha «{firma.En}» no se entiende como fecha.");
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(firma.Origen),
            "No quedo escrito DESDE DONDE se contestaron, y las dos vias tienen que distinguirse.");
        Assert.AreNotEqual(
            OrigenAMano,
            firma.Origen,
            "Lo que llego por el Excel quedo escrito como contestado a mano en la pantalla.");
    }

    /// <summary>
    /// Dada una persona que contesto el Excel de Sandy, cuando Miguel contesta encima en la
    /// pantalla, entonces la base dice Miguel, y no hace falta comparar fechas para saberlo.
    /// </summary>
    /// <remarks>
    /// Y al reves de lo que se pisa: lo de Sandy —<c>estado_propuesto</c>,
    /// <c>nota_companero</c>, <c>propuesto_por</c>, <c>propuesto_en</c>— sigue ahi con su
    /// nombre. Miguel contesta las preguntas; no borra a Sandy.
    /// </remarks>
    [TestMethod]
    public void SiMiguelContestaEncimaLaBaseDiceMiguelYLoDeSandySigueAhi()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, caso, persona) = SembrarUnDocumento(baseDePrueba.Conexion);
        var sandy = Companero(baseDePrueba.Conexion, "Sandy");

        personas.AnotarPropuesta(persona, ElExcelDeSandy, sandy);
        var deSandy = personas.FirmasDeLosPasosDelCaso(caso)[persona];

        personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);

        var despues = personas.Obtener(persona)!;
        var firma = personas.FirmasDeLosPasosDelCaso(caso)[persona];

        Console.WriteLine(
            "== Antes contesto {0} desde «{1}»; ahora {2} desde «{3}»; propuesto_por sigue en {4} ==",
            deSandy.Por,
            deSandy.Origen,
            firma.Por,
            firma.Origen,
            despues.PropuestoPor);

        Assert.AreEqual(sandy, deSandy.Por, "El Excel no quedo firmado antes de que Miguel entrara.");
        Assert.AreEqual(miguel, firma.Por, "Tras contestar Miguel, la base sigue diciendo que contesto otro.");
        Assert.AreEqual(OrigenAMano, firma.Origen, "La via de Miguel no quedo escrita.");
        Assert.AreEqual(sandy, despues.PropuestoPor, "Contestar Miguel piso el nombre de Sandy en el estado.");
        Assert.AreEqual("no_completa", despues.EstadoPropuesto, "Miguel piso el estado que propuso Sandy.");
    }

    /// <summary>
    /// ⛔ Dado un Excel que dice el estado y deja las seis EN BLANCO, cuando se aplica,
    /// entonces NO queda como que ese companero las contesto.
    /// </summary>
    /// <remarks>
    /// Es la regla que otro programador descubrio midiendo, y no es obvia: un companero
    /// puede devolver su Excel diciendo «no completa» sin haber mirado ninguna de las seis.
    /// Decir «contesto Sandy» ahi le atribuye un trabajo que no hizo, que es el mismo dano
    /// por el otro lado. Y <c>llamo_al_lider</c> no cuenta: no es un septimo paso.
    /// </remarks>
    [TestMethod]
    public void UnExcelConLasSeisEnBlancoNoCuentaComoQueEseCompaneroLasContesto()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (_, caso, persona) = SembrarUnDocumento(baseDePrueba.Conexion);
        var sandy = Companero(baseDePrueba.Conexion, "Sandy");

        personas.AnotarPropuesta(
            persona,
            new Persona
            {
                EstadoPropuesto = "no_completa",
                NotaCompanero = "No pude hablar con el líder.",
                LlamoAlLider = true,
            },
            sandy);

        var despues = personas.Obtener(persona)!;
        var firma = personas.FirmasDeLosPasosDelCaso(caso)[persona];

        Console.WriteLine(
            "== Excel con las seis en blanco: propuesto_por = {0}, pasos_por = {1} (Sandy es {2}) ==",
            despues.PropuestoPor,
            firma.Por,
            sandy);

        Assert.AreEqual(sandy, despues.PropuestoPor, "El estado que propuso Sandy no quedo con su nombre.");
        Assert.IsFalse(
            firma.YaContesto,
            "Un Excel que deja las seis en blanco quedo como que ese companero las contesto.");
        Assert.IsNull(firma.En, "Quedo una fecha de respuesta sin que nadie respondiera.");
        Assert.IsNull(firma.Origen, "Quedo una via de respuesta sin que nadie respondiera.");
    }

    /// <summary>
    /// Dado que Miguel ya contesto las seis, cuando llega un Excel que las deja en blanco,
    /// entonces NO se le atribuyen a ese companero.
    /// </summary>
    /// <remarks>
    /// Es el caso que separa «no escribir la firma» de «no tocarla»: si el Excel en blanco
    /// borrase la firma, se perderia que Miguel las habia mirado.
    /// </remarks>
    [TestMethod]
    public void UnExcelEnBlancoNoSeQuedaConLoQueContestoMiguel()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var (miguel, caso, persona) = SembrarUnDocumento(baseDePrueba.Conexion);
        var sandy = Companero(baseDePrueba.Conexion, "Sandy");

        personas.ResponderLosPasos(persona, LasSeisEnSi, miguel, OrigenAMano);
        personas.AnotarPropuesta(
            persona,
            new Persona { EstadoPropuesto = "completa" },
            sandy);

        var firma = personas.FirmasDeLosPasosDelCaso(caso)[persona];

        Console.WriteLine("== Tras el Excel en blanco: contesto {0} (Miguel es {1}) ==", firma.Por, miguel);

        Assert.AreEqual(miguel, firma.Por, "El Excel en blanco se quedo con lo que habia contestado Miguel.");
        Assert.AreEqual(OrigenAMano, firma.Origen, "El Excel en blanco cambio la via de la respuesta de Miguel.");
    }

    /// <summary>
    /// Dado el mismo Excel, cuando se aplica en el de verdad y en el falso, entonces los dos
    /// dejan la MISMA firma: quien, si hay fecha, y por que via.
    /// </summary>
    /// <remarks>
    /// El motivo esta escrito en <c>PruebaDeLaParidadDelFalso</c>: un falso que no se comporta
    /// como el original deja pruebas de pantalla en verde sobre un hueco real —y la ventana de
    /// las seis se prueba contra el falso—. Se comparan los dos valores OBSERVADOS y no una
    /// constante consigo misma, que pasaria en verde siempre.
    /// </remarks>
    [TestMethod]
    public void ElFalsoDejaLaMismaFirmaQueElDeVerdadAlAplicarUnExcel()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);

        var deVerdad = new RepositorioDePersonas(baseDePrueba.Conexion);
        var falso = new RepositorioDePersonasFalso(almacen);

        var quienEs = new Companero { Nombre = "Sandy", CreadoEn = DiaDeLasPruebas + " 09:00:00" };
        var sandyDeVerdad = new RepositorioDeCompaneros(baseDePrueba.Conexion).Guardar(quienEs).Id;
        var sandyFalso = new RepositorioDeCompanerosFalso(almacen).Guardar(quienEs).Id;

        var elCaso = new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" };
        var casoDeVerdad = new RepositorioDeCasos(baseDePrueba.Conexion).Guardar(elCaso).Id;
        var casoFalso = new RepositorioDeCasosFalso(almacen).Guardar(elCaso).Id;

        var laPersona = new Persona { Mrn = "055-1111-385A", Nombre = "Elena", FilaFormulario = 1 };
        var personaDeVerdad = deVerdad.Guardar(laPersona with { CasoId = casoDeVerdad }).Id;
        var personaFalsa = falso.Guardar(laPersona with { CasoId = casoFalso }).Id;

        deVerdad.AnotarPropuesta(personaDeVerdad, ElExcelDeSandy, sandyDeVerdad);
        falso.AnotarPropuesta(personaFalsa, ElExcelDeSandy, sandyFalso);

        var firmaDeVerdad = deVerdad.FirmasDeLosPasosDelCaso(casoDeVerdad)[personaDeVerdad];
        var firmaFalsa = falso.FirmasDeLosPasosDelCaso(casoFalso)[personaFalsa];

        Console.WriteLine(
            "== De verdad: {0} desde «{1}» · Falso: {2} desde «{3}» ==",
            firmaDeVerdad.Por,
            firmaDeVerdad.Origen,
            firmaFalsa.Por,
            firmaFalsa.Origen);

        Assert.AreEqual(sandyDeVerdad, firmaDeVerdad.Por, "El de verdad no firmo con quien mando el Excel.");
        Assert.AreEqual(sandyFalso, firmaFalsa.Por, "El falso no firmo con quien mando el Excel.");
        Assert.AreEqual(firmaDeVerdad.Origen, firmaFalsa.Origen, "Los dos no escribieron la misma via.");
        Assert.AreEqual(
            firmaDeVerdad.En is null,
            firmaFalsa.En is null,
            "Uno escribio la fecha de la respuesta y el otro la dejo vacia.");
    }

    /// <summary>
    /// Y con las seis en blanco los dos hacen lo mismo: ninguno firma.
    /// </summary>
    [TestMethod]
    public void ConLasSeisEnBlancoNingunoDeLosDosFirma()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);

        var deVerdad = new RepositorioDePersonas(baseDePrueba.Conexion);
        var falso = new RepositorioDePersonasFalso(almacen);

        var quienEs = new Companero { Nombre = "Sandy", CreadoEn = DiaDeLasPruebas + " 09:00:00" };
        var sandyDeVerdad = new RepositorioDeCompaneros(baseDePrueba.Conexion).Guardar(quienEs).Id;
        var sandyFalso = new RepositorioDeCompanerosFalso(almacen).Guardar(quienEs).Id;

        var elCaso = new Caso { NumeroCaso = "SURB2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" };
        var casoDeVerdad = new RepositorioDeCasos(baseDePrueba.Conexion).Guardar(elCaso).Id;
        var casoFalso = new RepositorioDeCasosFalso(almacen).Guardar(elCaso).Id;

        var laPersona = new Persona { Mrn = "055-1111-385A", Nombre = "Elena", FilaFormulario = 1 };
        var personaDeVerdad = deVerdad.Guardar(laPersona with { CasoId = casoDeVerdad }).Id;
        var personaFalsa = falso.Guardar(laPersona with { CasoId = casoFalso }).Id;

        var enBlanco = new Persona { EstadoPropuesto = "completa" };
        deVerdad.AnotarPropuesta(personaDeVerdad, enBlanco, sandyDeVerdad);
        falso.AnotarPropuesta(personaFalsa, enBlanco, sandyFalso);

        var firmaDeVerdad = deVerdad.FirmasDeLosPasosDelCaso(casoDeVerdad)[personaDeVerdad];
        var firmaFalsa = falso.FirmasDeLosPasosDelCaso(casoFalso)[personaFalsa];

        Assert.IsFalse(firmaDeVerdad.YaContesto, "El de verdad firmo un Excel con las seis en blanco.");
        Assert.IsFalse(firmaFalsa.YaContesto, "El falso firmo un Excel con las seis en blanco.");
    }

    // ═════════════════════════════ utilidades ═════════════════════════════

    /// <summary>Un Excel de vuelta que SI contesta preguntas: dos de las seis.</summary>
    /// <remarks>
    /// Dos y no seis a proposito: con una sola contestada ya es trabajo suyo, y el criterio
    /// no dice «las seis», dice que las contesto.
    /// </remarks>
    private static Persona ElExcelDeSandy => new()
    {
        EstadoPropuesto = "no_completa",
        NotaCompanero = "El líder no contestó el teléfono.",
        PasoPreparacion = true,
        PasoEntrevistas = false,
        LlamoAlLider = true,
    };

    /// <summary>Las seis preguntas contestadas que sí.</summary>
    private static RespuestaALosPasos LasSeisEnSi => new(true, true, true, true, true, true);

    /// <summary>Un compañero guardado por el repositorio de verdad.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="nombre">El nombre.</param>
    /// <returns>Su id.</returns>
    private static long Companero(SqliteConnection conexion, string nombre)
        => new RepositorioDeCompaneros(conexion)
            .Guardar(new Companero { Nombre = nombre, CreadoEn = DiaDeLasPruebas + " 09:00:00" }).Id;

    /// <summary>Miguel, un caso y una persona sin contestar, listos para que alguien conteste las seis.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <returns>Los tres ids.</returns>
    private static (long Miguel, long Caso, long Persona) SembrarUnDocumento(SqliteConnection conexion)
    {
        var miguel = Companero(conexion, "Miguel");
        var caso = new RepositorioDeCasos(conexion)
            .Guardar(new Caso { NumeroCaso = "CASP2609", CreadoEn = DiaDeLasPruebas + " 10:00:00" }).Id;
        var persona = new RepositorioDePersonas(conexion)
            .Guardar(new Persona { CasoId = caso, Mrn = "055-1111-385A", Nombre = "Elena", FilaFormulario = 1 }).Id;

        return (miguel, caso, persona);
    }
}
