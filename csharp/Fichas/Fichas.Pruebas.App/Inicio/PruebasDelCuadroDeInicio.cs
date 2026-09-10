using Fichas.App.Inicio;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// El cuadro de Inicio: las DOS cifras que el dueno dejo en esa pantalla.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Palabras del dueno el 2026-09-07: <i>«Lo unico que quiero [en
/// Inicio] es el calendario y un cuadro informando cuales hacen falta por completar, y
/// cuantos casos tienen los agentes»</i>.</para>
///
/// <para>⛔ <b>Lo que estas pruebas defienden es que el programa no diga dos cosas.</b> La
/// cifra de lo que falta por completar sale de la MISMA pasada que arma Inicio, y la ventana
/// de incompletos la vuelve a calcular por su cuenta. Si las dos reglas se separan, el dueno
/// leeria «14» en Inicio y contaria doce renglones al abrir la ventana, que es exactamente la
/// enfermedad que este proyecto ya diagnostico el 2026-09-07 con las cuatro palabras de
/// estado. Por eso la prueba compara las DOS cuentas, y no comprueba la de Inicio contra un
/// numero escrito a mano.</para>
///
/// <para>Todo sin abrir ventana (ADR-0003 §8.1): la regla vive en <see cref="LectorDelInicio"/>
/// y en <see cref="CuadroDeInicio"/>, que solo hablan con los contratos.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelCuadroDeInicio
{
    /// <summary>
    /// Lo que el cuadro dice que falta por completar es lo mismo que la ventana de
    /// incompletos ensena, contado sobre la misma base.
    /// </summary>
    [TestMethod]
    public void ElCuadroCuentaLoMismoQueLaVentanaDeIncompletos()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);

        var cuadro = BaseDeInicio.LeerInicio(servicios).Cuadro;
        var laVentana = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Console.WriteLine(
            $"Cuadro de Inicio: {cuadro.DocumentosPorCompletar} documentos · {cuadro.PersonasPorCompletar} personas. "
            + $"Ventana de incompletos: {laVentana.CuantosDocumentos} documentos · {laVentana.CuantasPersonas} personas.");

        Assert.IsGreaterThan(
            0,
            laVentana.CuantosDocumentos,
            "Con cero documentos incompletos las dos cuentas coincidirían sin decir nada.");
        Assert.AreEqual(
            laVentana.CuantosDocumentos,
            cuadro.DocumentosPorCompletar,
            "El cuadro de Inicio y la ventana de incompletos cuentan documentos distintos.");
        Assert.AreEqual(
            laVentana.CuantasPersonas,
            cuadro.PersonasPorCompletar,
            "El cuadro de Inicio y la ventana de incompletos cuentan personas distintas.");
    }

    /// <summary>
    /// La segunda cifra es la de los agentes, y es la MISMA que llevaba la lista
    /// que se fue de Inicio a la pestana nueva.
    /// </summary>
    /// <remarks>
    /// ⚠️ Se compara contra <c>Contadores.AsignadoALosAgentes</c>, que es lo que pinta la
    /// pestana nueva. Asi el cuadro y la lista no pueden separarse: si alguien cambia una
    /// regla, esta prueba lo dice antes de que el dueno lea dos numeros distintos de lo mismo.
    /// </remarks>
    [TestMethod]
    public void LaCifraDeLosAgentesEsLaMismaQueLaDeLaListaQueSeFue()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var resumen = BaseDeInicio.LeerInicio(servicios);

        Console.WriteLine(
            $"Cuadro: {resumen.Cuadro.CasosDeLosAgentes} casos en los agentes, "
            + $"{resumen.Cuadro.SinDevolver} sin devolver. Lista de la pestaña: {resumen.Asignados.Count} renglones.");

        Assert.IsGreaterThan(0, resumen.Cuadro.CasosDeLosAgentes, "Sin ningún caso asignado esto no comprueba nada.");
        Assert.AreEqual(resumen.Contadores.AsignadoALosAgentes, resumen.Cuadro.CasosDeLosAgentes);
        Assert.AreEqual(resumen.Asignados.Count, resumen.Cuadro.CasosDeLosAgentes);
        Assert.AreEqual(resumen.Contadores.SinDevolver, resumen.Cuadro.SinDevolver);
    }

    /// <summary>
    /// Un documento archivado no cuenta en ninguna de las dos cifras del cuadro.
    /// </summary>
    /// <remarks>
    /// Regla del dueno del 2026-09-06: archivar ES resolver. Un archivado incompleto que
    /// siguiera sumando en el cuadro le pediria trabajo que el ya cerro.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoArchivadoNoSumaEnElCuadro()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var antes = BaseDeInicio.LeerInicio(servicios).Cuadro;

        // Le falta el templo, así que estaría incompleto; y nace archivado.
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-20", archivado: true, temploNombre: null);

        var despues = BaseDeInicio.LeerInicio(servicios).Cuadro;

        Console.WriteLine(
            $"Antes: {antes.DocumentosPorCompletar} por completar. Después de meter un archivado incompleto: "
            + $"{despues.DocumentosPorCompletar}.");
        Assert.AreEqual(antes.DocumentosPorCompletar, despues.DocumentosPorCompletar);
    }

    /// <summary>
    /// Un documento al que le falta un campo suma UNO, y sus personas suman.
    /// </summary>
    [TestMethod]
    public void UnDocumentoAlQueLeFaltaUnCampoSumaEnElCuadro()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var antes = BaseDeInicio.LeerInicio(servicios).Cuadro;

        BaseDeInicio.MeterCaso(servicios, "FALT2609", "2026-09-20", cuantasPersonas: 4, temploNombre: null);

        var despues = BaseDeInicio.LeerInicio(servicios).Cuadro;

        Console.WriteLine(
            $"Por completar: {antes.DocumentosPorCompletar} → {despues.DocumentosPorCompletar} documentos, "
            + $"{antes.PersonasPorCompletar} → {despues.PersonasPorCompletar} personas.");
        Assert.AreEqual(antes.DocumentosPorCompletar + 1, despues.DocumentosPorCompletar);
        Assert.AreEqual(antes.PersonasPorCompletar + 4, despues.PersonasPorCompletar);
    }

    /// <summary>
    /// Las dos lineas del cuadro se leen enteras y con su denominador, y ninguna
    /// deja un cero mudo.
    /// </summary>
    /// <remarks>
    /// Criterio C20-6: un cero que no se explica se lee como «no hay datos». El cuadro es lo
    /// unico que queda en Inicio junto al calendario, asi que si dice «0» sin decir de que, la
    /// pantalla no informa de nada.
    /// </remarks>
    [TestMethod]
    public void LasDosLineasDelCuadroSeLeenEnterasTambienEnCero()
    {
        var vacia = BaseDeInicio.MontarServicios(0);
        var enCero = BaseDeInicio.LeerInicio(vacia).Cuadro;

        Console.WriteLine($"En cero → «{enCero.LineaDeLoQueFalta}» · «{enCero.LineaDeLosAgentes}».");
        Assert.AreEqual("0", enCero.CifraDeLoQueFalta);
        Assert.AreEqual("0", enCero.CifraDeLosAgentes);
        Assert.Contains("documento", enCero.LineaDeLoQueFalta, StringComparison.Ordinal);
        Assert.Contains("persona", enCero.LineaDeLoQueFalta, StringComparison.Ordinal);
        Assert.Contains("sin devolver", enCero.LineaDeLosAgentes, StringComparison.Ordinal);

        var llena = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var conDatos = BaseDeInicio.LeerInicio(llena).Cuadro;

        Console.WriteLine($"Con datos → «{conDatos.LineaDeLoQueFalta}» · «{conDatos.LineaDeLosAgentes}».");
        Assert.Contains("compañero", conDatos.LineaDeLosAgentes, StringComparison.Ordinal);
    }

    /// <summary>
    /// La cifra del cuadro NO se paga con una segunda pasada por la base.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es la trampa evidente de este cambio</b> y por eso se mide. Lo facil habria sido
    /// que Inicio montara ademas un <c>LectorDeIncompletos</c> para pintar el cuadro; eso es
    /// recorrer los casos, las personas y la procedencia DOS veces por pintado. El criterio
    /// C20-5 pone el techo de Inicio en 200 ms, y aqui se comprueba que leer Inicio entero
    /// —cuadro incluido— sigue costando menos que leer Inicio mas la ventana de incompletos.
    /// </remarks>
    [TestMethod]
    public void ElCuadroNoCuestaUnaSegundaPasadaPorLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var lector = BaseDeInicio.LectorDe(servicios);
        var deIncompletos = BaseDeInicio.LectorDeIncompletosDe(servicios);

        // Una vuelta en vacío: la primera pasada paga el compilado al vuelo y no mide la regla.
        lector.Leer();
        deIncompletos.Leer();

        var soloInicio = Cronometrar(() => lector.Leer());
        var lasDosPasadas = Cronometrar(() =>
        {
            lector.Leer();
            deIncompletos.Leer();
        });

        Console.WriteLine(
            $"Inicio con su cuadro: {soloInicio:F1} ms. Inicio más la ventana de incompletos aparte: "
            + $"{lasDosPasadas:F1} ms, sobre {BaseDeInicio.CasosDePrueba} casos.");

        Assert.IsLessThan(
            lasDosPasadas,
            soloInicio,
            "El cuadro está costando otra pasada por la base: sale igual de caro que leer las dos pantallas.");
    }

    /// <summary>Cuanto tarda algo, en milisegundos.</summary>
    private static double Cronometrar(Action que)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        que();
        reloj.Stop();
        return reloj.Elapsed.TotalMilliseconds;
    }
}
