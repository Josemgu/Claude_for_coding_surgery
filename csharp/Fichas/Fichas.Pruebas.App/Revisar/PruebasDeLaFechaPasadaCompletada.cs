using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Un archivado con la fecha de viaje pasada se llama «fecha pasada completada», no «incompleto».
/// </summary>
/// <remarks>
/// <para>Dos frases del dueño, seguidas, el 2026-09-06 (<c>DECISIONES.md</c>, «Archivar gana a
/// estar incompleto, y lo pasado completado se llama así»):</para>
/// <list type="bullet">
///   <item><i>«Si yo lo archivo debe desaparecer aunque no estén completos, porque a veces lo
///   archivo porque son fechas pasadas y ya los completé antes de crear el sistema.»</i></item>
///   <item><i>«Debe decir fecha pasada completada.»</i></item>
/// </list>
///
/// <para><b>La primera explica la segunda.</b> Él archiva casos que están incompletos en la base
/// y completos en la vida real: gente que viajó antes de que existiera este programa y cuyo
/// trámite él ya resolvió a mano. Para esos, «incompleto» no significa «falta trabajo»: significa
/// «el programa no lo vio». Llamarlos incompletos es decir de ellos algo que no es verdad.</para>
///
/// <para>⚠️ <b>Lo que NO cambia, y por eso hay pruebas de las dos mitades:</b> el tablero «Fecha
/// pasada» de los NO archivados sigue significando lo que significaba —«esto viajó y hay que
/// decidir»— y su tarjeta sigue distinguiéndose en la base. Las dos cosas no se mezclan.</para>
///
/// <para>⛔ <b>2026-09-07: estas pruebas se movieron, no se debilitaron.</b> Miraban la PALABRA
/// —«fecha pasada completada» contra «no está completa»—, y el dueño colapsó las cuatro palabras
/// a dos: <i>«Dos estados nada más: resuelto y me falta»</i>. Lo que vigilaban —que un archivado
/// con fecha pasada NO se confunda con un incompleto vivo— sigue vigilado, y ahora se mira donde
/// de verdad tiene que sostenerse: en <see cref="TarjetaDeDocumento.EstadoQueSeVe"/> y en la
/// base, que siguen siendo cuatro. La palabra se comprueba además, para que las dos no puedan
/// separarse.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaFechaPasadaCompletada
{
    /// <summary>Una fecha de viaje anterior al día en que se paran los relojes de la prueba.</summary>
    private const string UnaFechaYaPasada = "2026-08-20";

    /// <summary>Una fecha de viaje posterior a ese día.</summary>
    private const string UnaFechaPorVenir = "2026-12-01";

    /// <summary>
    /// Un archivado incompleto con fecha pasada desaparece de Revisar, y con el interruptor
    /// vuelve a verse: se lee «resuelto» y la base lo sigue llamando «fecha pasada completada».
    /// </summary>
    /// <remarks>
    /// Es el criterio del pase entero en una prueba: primero que archivar gana a estar
    /// incompleto —desaparece aunque le falten campos— y después cómo se llama cuando se mira a
    /// propósito. Sin la primera mitad, la segunda no significaría nada.
    /// </remarks>
    [TestMethod]
    public void ElArchivadoIncompletoDesapareceYConElInterruptorSeLeeResuelto()
    {
        var banco = new BancoDeCarpetas();
        var viejo = banco.MeterConEstado("AAAA0001", UnaFechaYaPasada, EstadoDeRecomendacion.NoCompleta);
        banco.Archivar(viejo);

        banco.Tablero.Cargar(conArchivados: false);
        var alTrabajar = banco.Tablero.De(viejo);

        banco.Tablero.Cargar(conArchivados: true);
        var alMirarlosAProposito = banco.Tablero.De(viejo);

        Console.WriteLine($"Al trabajar: {(alTrabajar is null ? "no está" : "está")}. "
                          + $"Con el interruptor: «{alMirarlosAProposito?.PalabraDelEstado}».");

        Assert.IsNull(alTrabajar, "Archivar gana a estar incompleto: desaparece aunque le falten campos.");
        Assert.IsNotNull(alMirarlosAProposito, "Con «Ver los archivados» tiene que volver a verse.");
        Assert.AreEqual(DosEstados.Resuelto, alMirarlosAProposito.PalabraDelEstado);
        Assert.AreEqual(
            EstadoQueSeVe.FechaPasadaCompletada,
            alMirarlosAProposito.EstadoQueSeVe,
            "La palabra son dos; el estado que se ve sigue siendo el suyo y no se colapsa.");
    }

    /// <summary>
    /// Un documento NO archivado con fecha pasada se lee «me falta», y no se confunde con
    /// el archivado que ya está decidido.
    /// </summary>
    [TestMethod]
    public void ElNoArchivadoConFechaPasadaSigueSiendoLoQueEra()
    {
        var banco = new BancoDeCarpetas();
        var vivo = banco.MeterConEstado("AAAA0002", UnaFechaYaPasada, EstadoDeRecomendacion.NoCompleta);

        banco.Tablero.Cargar();
        var tarjeta = banco.Tablero.De(vivo)!;

        Console.WriteLine($"No archivado con fecha pasada: «{tarjeta.PalabraDelEstado}».");

        Assert.IsTrue(tarjeta.FechaYaPasada, "La fecha sigue siendo pasada; eso no cambia.");
        Assert.AreEqual(DosEstados.MeFalta, tarjeta.PalabraDelEstado);
        Assert.DoesNotContain("completada", tarjeta.PalabraDelEstado, StringComparison.Ordinal);
        Assert.AreEqual(
            EstadoQueSeVe.NoCompleta,
            tarjeta.EstadoQueSeVe,
            "En la base sigue siendo «no completa», que es otra cosa que «fecha pasada completada».");
        Assert.AreEqual(1, banco.Tablero.CuantasEn(FiltroDeTarjeta.FechaPasada), "Y sigue en su tablero de siempre.");
    }

    /// <summary>
    /// El tablero «Fecha pasada» es el de los que hay que decidir, y un archivado ya está decidido.
    /// </summary>
    /// <remarks>
    /// La decisión del dueño lo dice con todas las letras: un archivado no se retiene «en ninguna
    /// lista, en ningún contador ni en ningún aviso». Con los archivados a la vista, contarlos en
    /// «Fecha pasada» pondría en esa pastilla documentos cuya pregunta —«¿archivar o completar?»—
    /// ya está contestada.
    /// </remarks>
    [TestMethod]
    public void ElTableroDeFechaPasadaNoCuentaLosArchivadosNiConElInterruptorPuesto()
    {
        var banco = new BancoDeCarpetas();
        var vivo = banco.MeterConEstado("AAAA0003", UnaFechaYaPasada, EstadoDeRecomendacion.NoCompleta);
        var archivado = banco.MeterConEstado("AAAA0004", UnaFechaYaPasada, EstadoDeRecomendacion.NoCompleta);
        banco.Archivar(archivado);

        banco.Tablero.Cargar(conArchivados: true);

        var enFechaPasada = banco.Tablero.CuantasEn(FiltroDeTarjeta.FechaPasada);
        var enTodo = banco.Tablero.CuantasEn(FiltroDeTarjeta.Todo);

        Console.WriteLine($"Con el interruptor puesto: {enTodo} a la vista, {enFechaPasada} en «Fecha pasada».");

        Assert.AreEqual(2, enTodo, "Los dos se ven: para desarchivar algo hay que poder verlo.");
        Assert.AreEqual(1, enFechaPasada, "Solo el vivo; el archivado ya está decidido.");
        Assert.IsNotNull(banco.Tablero.De(vivo));
    }

    /// <summary>
    /// Un archivado SIN fecha pasada no se llama «completado»: eso sería inventarle algo.
    /// </summary>
    /// <remarks>
    /// <para>El dueño habló de los que archiva <b>porque son fechas pasadas</b>. Un archivado que
    /// todavía no ha viajado se archivó por otro motivo, y decir de él que está completado sería
    /// afirmar algo que nadie ha dicho.</para>
    ///
    /// <para>⚠️ <b>Y sí se lee «resuelto», que NO es lo mismo que «completado».</b> Archivar es el
    /// gesto con el que el dueño dice que ya no tiene nada que hacer con ese documento, y eso es
    /// exactamente lo que «resuelto» significa desde el 2026-09-07. Lo que esta prueba defiende
    /// —que nadie afirme que está completo— se comprueba donde vive esa afirmación: en
    /// <c>casos.estado_recomendacion</c>, que sigue diciendo «no completa», y en el detalle, que
    /// dice quién lo cerró y no dice que nadie lo completara.</para>
    [TestMethod]
    public void ElArchivadoQueTodaviaNoHaViajadoNoSeLlamaCompletadoEnNingunSitio()
    {
        var banco = new BancoDeCarpetas();
        var porVenir = banco.MeterConEstado("AAAA0005", UnaFechaPorVenir, EstadoDeRecomendacion.NoCompleta);
        banco.Archivar(porVenir);

        banco.Tablero.Cargar(conArchivados: true);
        var tarjeta = banco.Tablero.De(porVenir)!;

        Console.WriteLine($"Archivado que aún no viaja: «{tarjeta.PalabraDelEstado}».");

        Assert.IsFalse(tarjeta.FechaYaPasada);
        Assert.AreEqual(DosEstados.Resuelto, tarjeta.PalabraDelEstado, "Lo archivó él: no le queda nada que hacer.");
        Assert.AreEqual(
            EstadoDeRecomendacion.NoCompleta,
            banco.Servicios.Casos.Obtener(porVenir)!.Estado,
            "En la base sigue diciendo «no completa»: archivar no completa nada.");
        Assert.AreEqual(EstadoQueSeVe.NoCompleta, tarjeta.EstadoQueSeVe);
        Assert.DoesNotContain("completad", tarjeta.DetalleDelEstado, StringComparison.Ordinal);
        StringAssert.Contains(tarjeta.DetalleDelEstado, "archivaste");
    }

    /// <summary>
    /// Archivar sigue sin borrar: el caso entero se queda en la base con su fecha de archivado.
    /// </summary>
    /// <remarks>
    /// ⚠️ La decisión del 2026-09-06 lo subraya: <i>«Esto NO deshace la regla de que un caso se
    /// archiva y nunca se borra»</i>. Se comprueba en la BASE, que es donde el caso sigue estando.
    /// </remarks>
    [TestMethod]
    public void ArchivarSigueSinBorrarNada()
    {
        var banco = new BancoDeCarpetas();
        var viejo = banco.MeterConEstado("AAAA0006", UnaFechaYaPasada, EstadoDeRecomendacion.NoCompleta);
        banco.Archivar(viejo);

        var enLaBase = banco.Servicios.Casos.Obtener(viejo)!;

        Assert.IsTrue(enLaBase.Archivado);
        Assert.AreEqual(BancoDeCarpetas.ElDiaDeLaPrueba, enLaBase.FechaArchivado);
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, enLaBase.Estado, "El estado real no se toca: solo cambia cómo se lee.");
    }
}
