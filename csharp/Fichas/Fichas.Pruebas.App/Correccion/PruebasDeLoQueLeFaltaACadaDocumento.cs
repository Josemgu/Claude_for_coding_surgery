using System.Diagnostics;
using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Lo que le falta a CADA documento, leido de una pasada, para que el desplegable de
/// Correccion lo diga sin abrirlos uno a uno.
/// </summary>
/// <remarks>
/// <para><b>Por que hace falta.</b> Trabajar por grupo solo sirve si al elegir un grupo se ve
/// cuales de sus documentos siguen pidiendo algo. Si hay que abrirlos uno a uno para saberlo,
/// el grupo es una lista mas.</para>
///
/// <para>⛔ <b>Y por que de UNA pasada.</b> Preguntar la procedencia documento a documento
/// cuesta <b>4,4 s</b> con las 18 000 lecturas reales y <b>50 ms</b> en bloque; esta medido en
/// <see cref="ProcedenciasDeUnaPasada"/> y no se vuelve a discutir aqui. El veredicto lo da
/// <see cref="LoQueLeFalta"/>, que es la MISMA funcion que usan la cola y la pantalla del
/// grupo: con dos, el mismo documento se leeria distinto segun donde se mire.</para>
///
/// <para>⛔ De aqui no sale ni una escritura (regla permanente 5).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueLeFaltaACadaDocumento
{
    /// <summary>Vigila que las tres respuestas —resuelto, le falta un dato, sin ninguna persona— se distingan y salgan de la MISMA composicion que la pantalla del grupo.</summary>
    [TestMethod]
    public void DiceLoMismoQueLaPantallaDelGrupoDeCadaDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var entero = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 2);
        var sinTemplo = BaseDeInicio.MeterCaso(servicios, "CASP2610", "2026-09-17", cuantasPersonas: 1, temploNombre: null);
        var sinNadie = BaseDeInicio.MeterCaso(servicios, "ELTC2609", "2026-09-17", cuantasPersonas: 0);

        var loQueFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            servicios.Casos, servicios.Personas, servicios.Procedencia);

        // ⛔ 2026-09-07: las tres frases cambiaron de redacción con el colapso a dos palabras.
        // Lo que esta prueba defiende es que las TRES respuestas se distingan y salgan de la
        // misma composición que la pantalla del grupo, y eso no cambia.
        StringAssert.Contains(loQueFalta.De(entero), "repartirlo", StringComparison.Ordinal);
        StringAssert.Contains(loQueFalta.De(sinTemplo), "le falta 1 dato", StringComparison.Ordinal);
        StringAssert.Contains(loQueFalta.De(sinNadie), "no se leyó ninguna persona", StringComparison.Ordinal);

        Assert.AreEqual(
            LasDosPreguntas.LoQueLeFaltaAlDocumento(0, sinNingunaPersonaLeida: false),
            loQueFalta.De(entero),
            "sale de la MISMA composición que la pantalla del grupo");
    }

    /// <summary>Un documento que no esta en la base no se inventa: se calla.</summary>
    [TestMethod]
    public void DeUnDocumentoQueNoEstaNoSeDiceNada()
    {
        var servicios = BaseDeInicio.MontarServicios(0);

        var loQueFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            servicios.Casos, servicios.Personas, servicios.Procedencia);

        Assert.AreEqual(string.Empty, loQueFalta.De(9999));
    }

    /// <summary>
    /// Contesta EXACTAMENTE lo mismo que la pantalla del grupo, documento por documento.
    /// </summary>
    /// <remarks>
    /// Se compara sobre la base inventada entera y no sobre tres casos a mano: la divergencia
    /// que <c>DECISIONES.md</c> midio el 2026-09-06 —seis casos contestados al reves— no salio
    /// de un caso raro, salio de dos caminos distintos que casi siempre coincidian.
    /// </remarks>
    [TestMethod]
    public void ContestaLoMismoQueLaPantallaDelGrupoEnTodaLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var loQueFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            servicios.Casos, servicios.Personas, servicios.Procedencia);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var comparados = 0;

        foreach (var fecha in BaseDeInicio.TodosLosCasos(servicios)
                     .Select(c => Fichas.App.Inicio.FechasEnEspanol.Leer(c.FechaViaje))
                     .OfType<DateOnly>()
                     .Distinct())
        {
            // Los archivados se ven en el grupo desde el 2026-09-14 pero Correccion no los
            // recibe (2026-09-06): su renglon no contesta esta pregunta, dice donde esta.
            foreach (var renglon in lector.DelDia(fecha).EnUnaSolaLista().Where(r => r.EsUnaPersona && !r.Archivado))
            {
                Assert.AreEqual(
                    renglon.LoQueLeFaltaAlDocumento,
                    loQueFalta.De(renglon.CasoId),
                    $"el caso {renglon.CasoId} se lee distinto en las dos pantallas");
                comparados++;
            }
        }

        Assert.IsGreaterThan(100, comparados, "la comparación tiene que haber mirado documentos de verdad");
    }

    /// <summary>
    /// Con 3 000 documentos NO se pregunta ni una vez por documento: todo va en bloque.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Se cuentan llamadas y no milisegundos, y eso es a proposito.</b> Lo que hay
    /// que fijar es la FORMA de la consulta: preguntar la procedencia documento a documento
    /// cuesta <b>4,4 s</b> con las 18 000 lecturas reales y <b>50 ms</b> en bloque. Un
    /// cronometro contesta eso de forma indirecta y se cae solo con la maquina cargada: medido
    /// el 2026-09-07 sobre esta misma base, la misma pasada dio <b>238 ms</b> y <b>493 ms</b>
    /// en dos ejecuciones seguidas. Una prueba asi no dice si el codigo esta bien, dice como
    /// estaba la maquina. La cuenta de llamadas da lo mismo en cualquiera.</para>
    ///
    /// <para>El tiempo se ANOTA igual, sin exigirlo, para que la cifra quede en el informe de
    /// la ejecucion y nadie tenga que suponerla.</para>
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosNoSePreguntaNiUnaVezPorDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(3000);
        var contada = new ProcedenciaQueSeCuenta(servicios.Procedencia);

        var cronometro = Stopwatch.StartNew();
        var loQueFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            servicios.Casos, servicios.Personas, contada);
        cronometro.Stop();

        Console.WriteLine(
            $"La pasada de 3 000 documentos costó {cronometro.Elapsed.TotalMilliseconds:F0} ms "
            + "en esta ejecución (no se exige: depende de la carga de la máquina).");

        Assert.IsNotEmpty(loQueFalta.De(BaseDeInicio.TodosLosCasos(servicios)[0].Id));
        Assert.AreEqual(0, contada.VecesQueSePreguntoPorUnRegistro,
            "ni una consulta por documento: con 3 000 serían 3 000 idas a la base");
        Assert.AreEqual(1, contada.VecesEnBloque, "las filas que pesan se piden UNA vez");
        Assert.AreEqual(2, contada.VecesLosCamposAnotados, "una por tabla: casos y personas");
        Assert.AreEqual(0, contada.Escrituras, "esto es una lectura: la regla permanente 5 entera");
    }
}
