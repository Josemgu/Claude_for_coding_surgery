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

    // ─────────────────────────────────────────────────────────────────────────────────────
    // ⚠️ DOS DE LAS TRES HUELLAS CAMBIARON EL 2026-09-16, A PROPÓSITO Y POR ORDEN DEL DUEÑO.
    //
    // QUÉ CAMBIÓ. (1) La columna «País» de «Los viajes», que salía siempre «no consta», se va:
    // la tabla queda con siete columnas. (2) Las dieciséis notas explicativas de las nueve
    // secciones del informe del período se van —«Con nombre y unidad, porque…», «"Verificada"
    // quiere decir aquí…», «"País" sale como…»—; las dos cifras que iban en la nota de las
    // métricas pasan a la celda «Cómo se cuenta» de la métrica 3, y «N personas no traen
    // ninguna casilla marcada» pasa a ser una fila de «A qué van al templo». El informe de
    // agente cambia porque comparte esas secciones; el histórico NO cambia y su huella es la
    // misma del 08.
    //
    // POR ORDEN DE QUIÉN. Del dueño, el 2026-09-16 (DECISIONES.md, «EL REPORTE EN EXCEL ES UNA
    // SOLA HOJA»): «"no consta" no es una respuesta; a dónde viajarán es el templo» y «los
    // reportes deben ser más simples, se están colocando muchas letras; debe explicarse sin
    // leer una sola palabra». Medido sobre la base falsa de 300: 16 notas, 2 500 caracteres y
    // 26 líneas del PDF antes; 0 después.
    //
    // LAS HUELLAS QUE SUSTITUYEN (las del 08):
    //   período  a8cb023f44da1ab7bc7b6a2cb7804cf77cfdb72612c35f9cd06f5315e50a8b8c
    //   agente   c515723aa2d13a082a94cf441cb677035a0cb57b8563e37f81bb5d4ed5106bb3
    //
    // La prueba NO se aflojó ni se borró: sigue comparando byte a byte.
    // ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>La SHA-256 del PDF del periodo, medida el 2026-09-16 sin «País» y sin notas.</summary>
    private const string HuellaDelPeriodo = "47e2f82805c0b03774d0f1c21e236dfc1f5fb87456f1d8643614e15e70939fa2";
    /// <summary>La SHA-256 del PDF del histórico, medida el 2026-09-08; el 16 no cambió.</summary>
    private const string HuellaDelHistorico = "9508588f9afc32b6653222b58ad036a9f0838176e93ba3812d2e1724b2adc122";
    /// <summary>La SHA-256 del PDF del informe de agente, medida el 2026-09-16 sin «País» y sin las notas de las secciones compartidas.</summary>
    private const string HuellaDelInformeDeAgente = "e3367e855054f8d8dc2314c74ff7109529f37c560e05f67831dd5a7717d901f6";

    /// <summary>Vigila que el PDF del periodo da la misma huella SHA-256 que la medida.</summary>
    [TestMethod]
    public void ElPdfDelPeriodoSigueSiendoElMismoByteAByte()
    {
        var reportes = Montar(out _);
        ComprobarLaHuella(
            ruta => reportes.GenerarReporteDelPeriodo(Desde, Hasta, ruta),
            "periodo",
            HuellaDelPeriodo);
    }

    /// <summary>Vigila que el PDF del histórico da la misma huella SHA-256 que la medida.</summary>
    [TestMethod]
    public void ElPdfDelHistoricoSigueSiendoElMismoByteAByte()
    {
        var reportes = Montar(out _);
        ComprobarLaHuella(ruta => reportes.GenerarHistorico(ruta), "historico", HuellaDelHistorico);
    }

    /// <summary>Vigila que el PDF del informe de agente da la misma huella SHA-256 que la medida.</summary>
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

    /// <summary>El motor sobre la base falsa de 300 casos con reloj fijo.</summary>
    /// <param name="servicios">Los servicios falsos, por si la prueba necesita mirar dentro.</param>
    private static ReportesEnPdf Montar(out ServiciosFalsos servicios)
    {
        servicios = BaseDePrueba.Montar(CuantosCasos);
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    /// <summary>El companero de numero interno mas bajo; con la misma semilla, siempre el mismo.</summary>
    /// <param name="servicios">Los servicios falsos montados.</param>
    private static long PrimerCompanero(ServiciosFalsos servicios)
        => servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, int.MaxValue))
            .Elementos.OrderBy(c => c.Id).First().Id;

    /// <summary>Genera el PDF, saca su SHA-256 y lo compara con el que estaba medido.</summary>
    /// <remarks>El archivo se borra siempre, pase o no; el mensaje de fallo trae la huella nueva para copiarla si el cambio es a propósito.</remarks>
    /// <param name="generar">Cómo se genera el PDF en una ruta dada.</param>
    /// <param name="nombre">Cómo se llama en el mensaje y en el archivo temporal.</param>
    /// <param name="huellaEsperada">La SHA-256 medida, en hexadecimal minúsculo.</param>
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
