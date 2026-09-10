using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Vocabulario;

/// <summary>
/// ⚠️⚠️ EL LIMITE DEL PASE: en la pantalla son dos, en la base siguen siendo cuatro.
/// </summary>
/// <remarks>
/// <para><b>Esta es la prueba que mas importa de este cambio</b>, y por eso mira la BASE y no
/// la pantalla. La decision del dueno del 2026-09-07 colapsa las cuatro palabras a dos
/// <i>a la vista</i>, y avisa con todas las letras de lo que no se puede perder: <i>«las tres
/// columnas dicen quien dijo cada cosa, y esa es la unica defensa del proyecto el dia que
/// alguien llegue al templo y no pueda entrar. Si se funden en una, deja de poder saberse si
/// aquello lo dio por bueno Sandy, lo dio por bueno Miguel, o lo dedujo el programa»</i>.</para>
///
/// <para><b>Como esta escrita.</b> Los dos casos recorren los dos caminos DE VERDAD —el que
/// escribe el Excel de un companero y el que escribe la mano de Miguel en Revisar— y despues
/// se comprueban las dos mitades: que la pantalla dice de los dos la misma palabra, y que la
/// base sigue sabiendo cual fue cual. Si alguien colapsara las columnas, la primera mitad
/// seguiria verde y esta se pondria roja, que es exactamente para lo que existe.</para>
///
/// <para>⛔ Esta prueba NO se toca para hacerla pasar: si se pone roja, lo que hay que arreglar
/// es el codigo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueLaBaseSigueDiciendoQuienLoDijo
{
    /// <summary>El dia en el que se paran los relojes de estas pruebas.</summary>
    private const string ElDiaDeLaPrueba = "2026-09-07";

    /// <summary>La ruta que el Excel de un companero deja como origen de su marca.</summary>
    private const string ElExcelDeSandy = @"C:\paquetes\vuelta-de-sandy.xlsx";

    private RelojFijo _reloj = null!;
    private ServiciosFalsos _servicios = null!;
    private AccionesDeRevisar _acciones = null!;
    private TableroDeRevisar _tablero = null!;

    [TestInitialize]
    public void Montar()
    {
        _reloj = new RelojFijo(ElDiaDeLaPrueba);
        _servicios = new ServiciosFalsos(0, 20260907, _reloj);
        _acciones = new AccionesDeRevisar(_servicios.Casos, _reloj, new BuzonDeAvisos());
        _tablero = new TableroDeRevisar(
            _servicios.Casos, _servicios.Asignaciones, _servicios.Companeros, _reloj);
    }

    /// <summary>Los dos casos del criterio, cada uno por su camino de verdad.</summary>
    /// <returns>El id del que marco el Excel de Sandy y el del que dio por bueno Miguel.</returns>
    private (long DelExcel, long DeMiguel, long SandyId, long MiguelId) MontarLosDos()
    {
        var sandy = _servicios.Companeros.Guardar(new Companero { Nombre = "Sandy", Activo = true }).Id;
        var miguel = _servicios.Companeros.Guardar(new Companero { Nombre = "Miguel", Activo = true }).Id;

        var delExcel = MeterUnCaso("CASP2601");
        var deMiguel = MeterUnCaso("CASP2602");

        // El camino del companero: su hoja escribe el estado Y deja registrado que lo dijo el.
        _servicios.Casos.MarcarEstadoDelCompanero(
            delExcel, EstadoDeRecomendacion.Completa, MotivoDeNoCompletar.SinMotivo, sandy, ElExcelDeSandy);

        // El camino de Miguel: la mano, desde la pantalla de Revisar.
        _acciones.MarcarAMano(deMiguel, EstadoDeRecomendacion.Completa, miguel);

        return (delExcel, deMiguel, sandy, miguel);
    }

    private long MeterUnCaso(string numero)
        => _servicios.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            FechaViaje = "2026-10-15",
            RutaPdf = $@"C:\pdf\{numero}.pdf",
            PaginaPdf = 1,
            CreadoEn = _reloj.Ahora(),
        }).Id;

    // ────────────────── la mitad de la pantalla: los dos dicen lo mismo ──────────────────

    /// <summary>
    /// El que dio por completo el Excel del companero y el que dio por bueno el dueno se leen
    /// los dos «resuelto».
    /// </summary>
    [TestMethod]
    public void LosDosCaminosSeLeenConLaMismaPalabra()
    {
        var (delExcel, deMiguel, _, _) = MontarLosDos();
        _tablero.Cargar();

        var unaDelExcel = _tablero.De(delExcel)!;
        var unaDeMiguel = _tablero.De(deMiguel)!;

        Assert.AreEqual(DosEstados.Resuelto, unaDelExcel.PalabraDelEstado);
        Assert.AreEqual(DosEstados.Resuelto, unaDeMiguel.PalabraDelEstado);
    }

    // ────────────────── la mitad de la base: siguen distinguiendose ──────────────────

    /// <summary>
    /// ⚠️ En la base, lo que dijo el Excel de Sandy queda en <c>estado_del_companero</c>, con
    /// su nombre y su fecha; lo de Miguel, no.
    /// </summary>
    [TestMethod]
    public void LaBaseSigueSabiendoCualLoDijoElExcelDeUnCompanero()
    {
        var (delExcel, deMiguel, sandyId, _) = MontarLosDos();

        var elDelExcel = _servicios.Casos.Obtener(delExcel)!;
        var elDeMiguel = _servicios.Casos.Obtener(deMiguel)!;

        Assert.AreEqual("completa", elDelExcel.EstadoDelCompanero);
        Assert.AreEqual(sandyId, elDelExcel.EstadoDelCompaneroPor);
        // Se compara el DIA y no la marca entera: el reloj de la prueba devuelve
        // «2026-09-07 12:00:00», y fijar la hora aquí ataría la prueba a un detalle del reloj
        // falso en vez de a lo que se está comprobando, que es que quede escrito CUÁNDO.
        StringAssert.StartsWith(elDelExcel.EstadoDelCompaneroEn, ElDiaDeLaPrueba);

        Assert.IsNull(elDeMiguel.EstadoDelCompanero, "Miguel no puede acabar escrito como si fuera una hoja de compañero.");
        Assert.IsNull(elDeMiguel.EstadoDelCompaneroPor);
    }

    /// <summary>
    /// Y el ORIGEN de la marca vigente separa la hoja de la mano, con las dos frases distintas.
    /// </summary>
    [TestMethod]
    public void ElOrigenDeLaMarcaSeparaLaHojaDeLaMano()
    {
        var (delExcel, deMiguel, _, _) = MontarLosDos();

        var elDelExcel = _servicios.Casos.Obtener(delExcel)!;
        var elDeMiguel = _servicios.Casos.Obtener(deMiguel)!;

        Assert.AreEqual(ElExcelDeSandy, elDelExcel.EstadoMarcadoOrigen);
        Assert.AreEqual(AccionesDeRevisar.OrigenAMano, elDeMiguel.EstadoMarcadoOrigen);
        Assert.AreNotEqual(elDelExcel.EstadoMarcadoOrigen, elDeMiguel.EstadoMarcadoOrigen);
    }

    /// <summary>Quien lo marco y cuando siguen escritos en los dos, y no son el mismo.</summary>
    [TestMethod]
    public void QuienLoMarcoYCuandoSiguenEnLosDos()
    {
        var (delExcel, deMiguel, sandyId, miguelId) = MontarLosDos();

        var elDelExcel = _servicios.Casos.Obtener(delExcel)!;
        var elDeMiguel = _servicios.Casos.Obtener(deMiguel)!;

        Assert.AreEqual(sandyId, elDelExcel.EstadoMarcadoPor);
        Assert.AreEqual(miguelId, elDeMiguel.EstadoMarcadoPor);
        StringAssert.StartsWith(elDelExcel.EstadoMarcadoEn, ElDiaDeLaPrueba);
        StringAssert.StartsWith(elDeMiguel.EstadoMarcadoEn, ElDiaDeLaPrueba);
    }

    /// <summary>
    /// Y la firma que se lee en la tarjeta tampoco los confunde, aunque la palabra sea la misma.
    /// </summary>
    /// <remarks>
    /// Es la puerta por la que el dueno llega a esa distincion sin abrir la base: la palabra es
    /// una, y debajo sigue diciendo quien lo dio por bueno.
    /// </remarks>
    [TestMethod]
    public void LaFirmaDeLaTarjetaSigueNombrandoAQuienLoDioPorBueno()
    {
        var (delExcel, deMiguel, _, _) = MontarLosDos();
        _tablero.Cargar();

        var firmaDelExcel = _tablero.De(delExcel)!.Firma;
        var firmaDeMiguel = _tablero.De(deMiguel)!.Firma;

        StringAssert.Contains(firmaDelExcel, "Sandy");
        StringAssert.Contains(firmaDeMiguel, "Miguel");
        Assert.AreNotEqual(firmaDelExcel, firmaDeMiguel);
    }

    /// <summary>
    /// El tercer camino —el dueno archiva— tambien dice «resuelto» y tambien se distingue.
    /// </summary>
    /// <remarks>
    /// Es el caso del apartado 5 del 2026-09-07: un archivado sale de las listas y se queda en
    /// el calendario. Aqui solo se mide que su marca de archivado sigue en la base con su
    /// fecha, que es lo que permite saber que lo cerro el y cuando.
    /// </remarks>
    [TestMethod]
    public void ElArchivadoDelDuenoSeDistingueDeLosOtrosDos()
    {
        var (delExcel, _, _, _) = MontarLosDos();
        var archivado = MeterUnCaso("CASP2603");
        _acciones.ArchivarEnLote([archivado]);

        var elArchivado = _servicios.Casos.Obtener(archivado)!;
        var elDelExcel = _servicios.Casos.Obtener(delExcel)!;

        Assert.IsTrue(elArchivado.Archivado);
        Assert.AreEqual(ElDiaDeLaPrueba, elArchivado.FechaArchivado);
        Assert.IsNull(elArchivado.EstadoDelCompanero, "Archivar no puede escribir lo que dijo ningún compañero.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, elArchivado.Estado);

        Assert.IsFalse(elDelExcel.Archivado, "El que marcó el Excel no se archiva solo.");
    }
}
