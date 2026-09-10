using System.Security.Cryptography;
using Fichas.Contratos.Consultas;
using Fichas.Datos.Falso;
using Fichas.Reportes;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La huella de los tres PDF, byte a byte, para que anadir OTRO formato no los mueva.
/// </summary>
/// <remarks>
/// <para>Existe por una frase del dueno del 2026-09-07: <i>«está bien el de PDF, pero también
/// quiero uno con Excel»</i>. «Esta bien» quiere decir que no se toca. Un motor de Excel que
/// comparte armado con el de PDF puede mover el PDF sin que nadie lo note —basta anadir una
/// columna «para que el Excel filtre mejor»—, y eso no se ve en ninguna prueba que mire
/// cifras: las cifras seguirian cuadrando.</para>
///
/// <para>Las tres huellas se midieron sobre <c>master</c> ANTES de escribir una sola linea del
/// Excel, con la base inventada de <see cref="BaseDePrueba"/> y su reloj fijo, que es lo que
/// hace la salida repetible.</para>
///
/// <para>⚠️ <b>Como se actualiza esta prueba, el dia que haya que actualizarla.</b> Si el PDF
/// cambia A PROPOSITO —porque el dueno pidio otro rotulo—, esta prueba se pone roja y ESO ESTA
/// BIEN: se corre, se copia la huella nueva de su mensaje y se cambia aqui EN EL MISMO commit
/// que cambia el informe, para que el diff diga «el PDF cambio» a la cara. Lo que no vale es
/// borrarla o aflojarla: entonces vuelve a ser posible mover el PDF sin querer.</para>
/// </remarks>
[TestClass]
public class PruebaDeQueElPdfNoCambia
{
    /// <summary>El periodo con el que se miden las tres huellas.</summary>
    private const string Desde = "2026-09-01";

    /// <summary>El fin del periodo con el que se miden las tres huellas.</summary>
    private const string Hasta = "2026-09-30";

    /// <summary>Cuantos casos inventados entran en la medida.</summary>
    private const int CuantosCasos = 300;

    // ─────────────────────────────────────────────────────────────────────────────────────
    // ⚠️ LAS TRES HUELLAS CAMBIARON EL 2026-09-08, A PROPÓSITO Y POR ORDEN DEL DUEÑO.
    //
    // QUÉ CAMBIÓ. La unidad, que salía pegada en una sola celda —«Castries Branch · 0700016»—,
    // pasa a DOS columnas: «Número de unidad» delante y el nombre detrás. Toca las seis tablas
    // que la llevan: «Quiénes viajaron sin verificar», «Unidades con preparaciones sin
    // completar», la Parte 1, la Parte 2, el histórico y la segunda vuelta. Y en el informe de
    // agente cambia además QUÉ CASOS entran: las secciones de cola se recortaban a las
    // asignaciones vivas y ahora traen también las retiradas.
    //
    // POR ORDEN DE QUIÉN. Del dueño, el 2026-09-08, contestando a que partirla cambia también
    // el PDF —cosa que se le dijo antes de tocarlo—: «Sí, pártelo en dos y que conserve lo
    // retirado». El motivo: pegados NO SE PODÍA FILTRAR POR NÚMERO DE UNIDAD en el Excel.
    //
    // LAS HUELLAS QUE SUSTITUYEN. Se dejan escritas para que el día que alguien sospeche de
    // este commit pueda volver al PDF de antes y compararlos:
    //   período  eafd50ccaa449a3d4fc34a6f2106a74d7a38a9add1dcfc6953b209f265b3dd05
    //   histórico b70ad2203616e56040de9cf5882b45acebc3bab4d1a616cc299068f299973e5f
    //   agente   195bdb4754152955b87fb9a1db4dd4f0c204735a5b1335059afcd2a4c4071a84
    //
    // La prueba NO se aflojó ni se borró: sigue comparando byte a byte, y lo único que se movió
    // son los tres valores de abajo, en el MISMO commit que mueve el informe.
    // ─────────────────────────────────────────────────────────────────────────────────────

    private const string HuellaDelPeriodo = "a8cb023f44da1ab7bc7b6a2cb7804cf77cfdb72612c35f9cd06f5315e50a8b8c";
    private const string HuellaDelHistorico = "9508588f9afc32b6653222b58ad036a9f0838176e93ba3812d2e1724b2adc122";
    private const string HuellaDelInformeDeAgente = "c515723aa2d13a082a94cf441cb677035a0cb57b8563e37f81bb5d4ed5106bb3";

    [TestMethod]
    public void ElPdfDelPeriodoSigueSiendoElMismoByteAByte()
    {
        var reportes = Montar(out _);
        ComprobarLaHuella(
            ruta => reportes.GenerarReporteDelPeriodo(Desde, Hasta, ruta),
            "periodo",
            HuellaDelPeriodo);
    }

    [TestMethod]
    public void ElPdfDelHistoricoSigueSiendoElMismoByteAByte()
    {
        var reportes = Montar(out _);
        ComprobarLaHuella(ruta => reportes.GenerarHistorico(ruta), "historico", HuellaDelHistorico);
    }

    [TestMethod]
    public void ElPdfDelInformeDeAgenteSigueSiendoElMismoByteAByte()
    {
        var reportes = Montar(out var servicios);
        var quien = PrimerCompanero(servicios);
        ComprobarLaHuella(
            ruta => reportes.GenerarReporteDeCompanero(quien, Desde, Hasta, ruta),
            "informe-de-agente",
            HuellaDelInformeDeAgente);
    }

    // ---- el andamio ---------------------------------------------------------

    private static ReportesEnPdf Montar(out ServiciosFalsos servicios)
    {
        servicios = BaseDePrueba.Montar(CuantosCasos);
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    /// <summary>El companero de numero interno mas bajo; con la misma semilla, siempre el mismo.</summary>
    private static long PrimerCompanero(ServiciosFalsos servicios)
        => servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, int.MaxValue))
            .Elementos.OrderBy(c => c.Id).First().Id;

    /// <summary>Genera el PDF, saca su SHA-256 y lo compara con el que estaba medido.</summary>
    private static void ComprobarLaHuella(
        Func<string, Fichas.Contratos.Consultas.ResultadoDeEscritura> generar, string nombre, string huellaEsperada)
    {
        var ruta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-{nombre}.pdf");
        try
        {
            var resultado = generar(ruta);
            Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));

            var huella = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta))).ToLowerInvariant();
            Assert.AreEqual(
                huellaEsperada,
                huella,
                $"El PDF «{nombre}» cambió. Si el cambio es a propósito, la huella nueva es «{huella}».");
        }
        finally
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
    }
}
