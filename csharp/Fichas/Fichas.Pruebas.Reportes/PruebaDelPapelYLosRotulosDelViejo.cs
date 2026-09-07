using System.Text;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El informe de los jefes vuelve al papel y a los rotulos del programa viejo.
/// </summary>
/// <remarks>
/// <para><b>De donde sale el criterio, y no del codigo.</b> Es una decision del dueno,
/// recogida en <c>DECISIONES.md</c> el 2026-09-04 con sus palabras: <i>«si el informe de los
/// jefes como el viejo está bien»</i>. El supervisor la leyo alli mismo como respuesta a las
/// DOS preguntas que se le habian hecho: <b>papel Carta vertical</b> (el viejo era 612 × 792;
/// el Python nuevo lo puso apaisado, 792 × 612) y <b>los rotulos del viejo</b>
/// —«Verificadas», «Sin verificar» y «Quiénes viajaron sin verificar»—, no los del criterio
/// C8-2.</para>
///
/// <para><b>Esto DESHACE a proposito una parte del criterio C8-2</b>, que habia cambiado esas
/// tres palabras por «con la preparación completa» para que «verificada» no significara dos
/// cosas en el mismo programa. El motivo de aquel cambio era bueno y sigue siendo cierto; lo
/// que pasa es que el dueno prefiere el documento que ya conocia. Lo que se hace para que la
/// confusion no vuelva a colarse en silencio es dejar la aclaracion DENTRO del informe, en una
/// nota de la seccion, en vez de esconderla cambiando el rotulo.</para>
///
/// <para>Dado que el papel encoge de 792 a 612 puntos de ancho, la tabla de ocho columnas
/// reparte menos sitio por columna y el informe ocupa mas paginas. Eso NO es un fallo: el
/// reparto de <see cref="Maqueta"/> es proporcional, asi que ninguna columna se sale por la
/// derecha. La cifra de paginas antes y despues va en la entrega.</para>
/// </remarks>
[TestClass]
public class PruebaDelPapelYLosRotulosDelViejo
{
    private const string GeneradoEn = "2026-09-20 10:00:00";

    /// <summary>Dado el informe de los jefes, cuando se escribe el PDF, es Carta VERTICAL.</summary>
    /// <remarks>
    /// Se mira el <c>/MediaBox</c> de los bytes y no las dos constantes: una prueba que compara
    /// la constante consigo misma pasa en verde con el papel puesto del reves.
    /// </remarks>
    [TestMethod]
    public void ElPapelEsCartaVerticalDeSeiscientosDoceParaSetecientosNoventaYDos()
    {
        var bytes = Maqueta.ConstruirPdf(Informe());
        var texto = Encoding.Latin1.GetString(bytes);

        StringAssert.Contains(texto, "/MediaBox [0 0 612 792]");
        Assert.IsFalse(
            texto.Contains("/MediaBox [0 0 792 612]", StringComparison.Ordinal),
            "El informe sigue saliendo apaisado; el dueño pidió el papel del viejo, que era Carta vertical.");
    }

    /// <summary>La seccion que abre el informe se llama como en el viejo.</summary>
    [TestMethod]
    public void LaSeccionQueAbreSeLlamaQuienesViajaronSinVerificar()
    {
        var informe = Informe();

        Assert.AreEqual("Quiénes viajaron sin verificar", informe.Secciones[0].Titulo);
    }

    /// <summary>Las dos columnas del viejo, y ni un rotulo con la palabra que las sustituyo.</summary>
    [TestMethod]
    public void LasColumnasDicenVerificadasYSinVerificar()
    {
        var rotulos = Informe().Secciones
            .SelectMany(seccion => seccion.Columnas)
            .Select(columna => columna.Nombre)
            .ToList();

        CollectionAssert.Contains(rotulos, "Verificadas");
        CollectionAssert.Contains(rotulos, "Sin verificar");
        Assert.IsFalse(
            rotulos.Any(rotulo => rotulo.Contains("preparación", StringComparison.OrdinalIgnoreCase)),
            "Ningún ROTULO de columna puede seguir diciendo «preparación»: el dueño pidió los del viejo. "
            + $"Los de ahora: {string.Join(" · ", rotulos)}");
    }

    /// <summary>Las cuatro cifras de la portada, con las palabras del viejo.</summary>
    [TestMethod]
    public void LaPortadaCuentaLoQueViajoSinVerificar()
    {
        var rotulos = Informe().Portada.Cifras.Select(cifra => cifra.Rotulo).ToList();

        CollectionAssert.Contains(rotulos, "viajaron sin verificar");
        CollectionAssert.Contains(rotulos, "viajaron verificadas");
    }

    /// <summary>
    /// La aclaracion NO desaparece: se dice dentro del informe, que es donde la lee quien manda.
    /// </summary>
    /// <remarks>
    /// Es lo unico que se conserva del criterio C8-2. Sin esta nota, un jefe que lea
    /// «Verificadas» puede creer que habla de la firma de Miguel sobre un campo leido, y son
    /// dos hechos distintos que conviven en el mismo documento —las metricas del final SI
    /// hablan de la firma—.
    /// </remarks>
    [TestMethod]
    public void ElInformeDiceQueVerificadaAquiSonLosSeisPasosYNoLaFirmaDeMiguel()
    {
        var notas = Informe().Secciones.SelectMany(seccion => seccion.Notas).ToList();

        Assert.IsTrue(
            notas.Any(nota =>
                nota.Contains("seis pasos", StringComparison.OrdinalIgnoreCase)
                && nota.Contains("firma", StringComparison.OrdinalIgnoreCase)),
            "Falta la nota que separa «verificada» aquí (los seis pasos del sistema del líder) de la "
            + "firma de Miguel sobre un campo. Sin ella la palabra vuelve a significar dos cosas.");
    }

    /// <summary>El pie vuelve a la frase del viejo, para que no discuta con las columnas.</summary>
    [TestMethod]
    public void ElPieHablaDeLoVerificadoYNoDeLaPreparacion()
    {
        var texto = Encoding.Latin1.GetString(Maqueta.ConstruirPdf(Informe()));

        // El PDF lleva el texto en WinAnsi dentro de literales; basta con la parte sin tildes.
        StringAssert.Contains(texto, "Lo verificado es lo que consta");
    }

    /// <summary>
    /// Deja el PDF puesto y dice cuantas paginas ocupa, para poder comparar antes y despues.
    /// </summary>
    /// <remarks>
    /// ⚠️ NO comprueba que el PDF abra: comprueba que se escribe y con que tamano. Que ABRE lo
    /// dice un lector ajeno a este codigo, y por eso el archivo se deja puesto en vez de
    /// borrarse. El coste del papel vertical es el numero de paginas, y esta linea es la que
    /// permite decirlo con una cifra en vez de con un adjetivo.
    /// </remarks>
    [TestMethod]
    public void DejaElPdfPuestoYDiceCuantasPaginasOcupa()
    {
        var informe = Informe();
        var bytes = Maqueta.ConstruirPdf(informe);

        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-papel-del-viejo");
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "informe-de-los-jefes.pdf");
        File.WriteAllBytes(ruta, bytes);

        var paginas = Maqueta.RepartirEnPaginas(Maqueta.LineasDelDocumento(informe, [])).Count;
        Console.WriteLine($"MEDIDO · {ruta} · {bytes.Length} bytes · {paginas} páginas");

        Assert.IsGreaterThan(1000, bytes.Length);
    }

    /// <summary>El informe del periodo sobre una base inventada de tamano manejable.</summary>
    private static Documento Informe()
    {
        var servicios = BaseDePrueba.Montar(40);
        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia);

        return ArmadoDelDocumento.DelPeriodo(
            lectura, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
    }
}
