using Fichas.App.Correccion;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Lo que se lee cuando el escaneo no se puede pintar.
/// </summary>
/// <remarks>
/// Sale de la regla que dejo el fallo de la pantalla de Importar, medido por QA: alli el
/// manejador es <c>async void</c> y se traga la excepcion, asi que los botones parecen
/// muertos y no hay ni una linea que diga por que. Aqui la regla es al reves: <b>si
/// rasterizar falla, se ve en una linea que paso</b>, con el tipo del fallo y su mensaje.
/// <para>
/// La frase se prueba sin ventana por lo mismo que <see cref="TextoDelAcuse"/>: es la unica
/// forma de que alguien la lea de verdad antes de que la lea Miguel.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelTextoDelVisor
{
    /// <summary>La linea de un fallo dice la hoja, el tipo del fallo y su mensaje.</summary>
    [TestMethod]
    public void LaLineaDeUnFalloDiceLaHojaElTipoYElMensaje()
    {
        var linea = TextoDelVisor.LineaDeFallo(3, new IOException("el archivo esta en uso"));

        Assert.Contains("3", linea, StringComparison.Ordinal);
        Assert.Contains("IOException", linea, StringComparison.Ordinal);
        Assert.Contains("el archivo esta en uso", linea, StringComparison.Ordinal);
    }

    /// <summary>Es UNA linea: ni un salto de renglon, y cabe en el pie.</summary>
    [TestMethod]
    public void LaLineaDeUnFalloEsUnaSolaLineaYCabeEnElPie()
    {
        var largo = new string('x', 400);
        var linea = TextoDelVisor.LineaDeFallo(1, new InvalidOperationException($"{largo}\r\nsegundo renglon"));

        Assert.DoesNotContain("\n", linea, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", linea, StringComparison.Ordinal);
        Assert.IsLessThanOrEqualTo(TextoDelAcuse.LargoMaximoDeLaLinea, linea.Length);
    }

    /// <summary>Un fallo sin mensaje sigue diciendo algo: callar es lo que esta prohibido.</summary>
    [TestMethod]
    public void UnFalloSinMensajeSigueDiciendoAlgo()
    {
        var linea = TextoDelVisor.LineaDeFallo(2, new InvalidOperationException(string.Empty));

        Assert.IsFalse(string.IsNullOrWhiteSpace(linea));
        Assert.Contains("InvalidOperationException", linea, StringComparison.Ordinal);
    }

    /// <summary>Una hoja que no se pudo abrir se explica sobre el papel en blanco.</summary>
    /// <remarks>
    /// No es lo mismo que la linea del pie: esto va escrito sobre la hoja vacia, donde si
    /// cabe decir que los campos se corrigen igual. Negarse a pintar seria impedir por no
    /// poder.
    /// </remarks>
    [TestMethod]
    public void UnaHojaQueNoSePudoAbrirSeExplicaSobreElPapelEnBlanco()
    {
        var motivo = TextoDelVisor.HojaQueNoSePudoAbrir(4, @"C:\pdf\lote.pdf");

        Assert.Contains("4", motivo, StringComparison.Ordinal);
        Assert.Contains(@"C:\pdf\lote.pdf", motivo, StringComparison.Ordinal);
        Assert.Contains("se corrigen igual", motivo, StringComparison.Ordinal);
    }

    /// <summary>Ya NO se dice que la lectura de PDF sea una fase por hacer.</summary>
    /// <remarks>
    /// ⚠️ Esta prueba existe por una frase concreta que QA leyo en el paquete publicado:
    /// «El escaneo todavia no se puede pintar: la lectura de PDF es la fase C3». Esa fase
    /// esta cerrada —<c>Fichas.Lectura</c> rasteriza los siete documentos del dueno— y la
    /// frase decia que el programa no sabia hacer algo que si sabe. Un texto que miente
    /// sobre el estado del programa manda a buscar el fallo donde no esta.
    /// </remarks>
    [TestMethod]
    public void NingunTextoDelVisorHablaDeUnaFasePendiente()
    {
        string[] todos =
        [
            TextoDelVisor.SinEscaneo,
            TextoDelVisor.HojaQueNoSePudoAbrir(1, @"C:\pdf\lote.pdf"),
            TextoDelVisor.LineaDeFallo(1, new IOException("x")),
        ];

        foreach (var texto in todos)
        {
            Assert.DoesNotContain("fase C3", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("todavia no se puede pintar", texto, StringComparison.OrdinalIgnoreCase);
        }
    }
}
