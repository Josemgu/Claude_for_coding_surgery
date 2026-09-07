using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// PDF fabricados a mano para las pruebas, con cada hoja ROTULADA.
/// </summary>
/// <remarks>
/// ⚠️ Las hojas llevan un texto distinto cada una a proposito. Contar cuantas paginas
/// tiene el PDF unido no prueba el orden, y el orden es la mitad del encargo: el companero
/// lee la fila 3 del Excel y espera la hoja 3 del PDF. Con las hojas rotuladas, la prueba
/// puede leer el texto de vuelta y comparar la LISTA, no el numero.
///
/// <para>Ningun PDF real del dueno entra en las pruebas: llevan datos de personas. Estos se
/// fabrican en una carpeta temporal y se borran con ella.</para>
/// </remarks>
public static class PdfDePrueba
{
    /// <summary>Escribe un PDF con una hoja por rotulo, en ese orden, y devuelve su ruta.</summary>
    public static string Escribir(string carpeta, string nombre, params string[] rotulos)
    {
        var constructor = new PdfDocumentBuilder();
        var tipografia = constructor.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var rotulo in rotulos)
        {
            var hoja = constructor.AddPage(PageSize.A4);
            hoja.AddText(rotulo, 20, new PdfPoint(60, 700), tipografia);
        }

        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllBytes(ruta, constructor.Build());
        return ruta;
    }

    /// <summary>Los rotulos que lleva cada hoja de ese PDF, en el orden en que estan.</summary>
    /// <remarks>
    /// <para>Abre el archivo de verdad: es la unica forma de comprobar que la union hizo lo que
    /// dice.</para>
    ///
    /// <para>⚠️ Se leen PALABRAS y no letras sueltas. Pegando las letras, un texto que salta de
    /// linea sale con las dos palabras del salto unidas —«no se» + «pudo leer» daba «no
    /// sepudo leer»— y una prueba que busque la frase falla por donde el PDF partio el
    /// renglon, que no es lo que se esta comprobando.</para>
    /// </remarks>
    public static IReadOnlyList<string> RotulosDe(string rutaPdf)
    {
        using var documento = PdfDocument.Open(rutaPdf);
        return [.. documento.GetPages().Select(hoja => string.Join(" ", hoja.GetWords().Select(palabra => palabra.Text)))];
    }

    /// <summary>Cuanto mide cada hoja, redondeado al punto, para poder compararlas entre si.</summary>
    /// <remarks>
    /// Se redondea porque el ancho de una pagina es un decimal y dos hojas del mismo tamano
    /// pueden diferir en la ultima cifra; lo que se comprueba es que al hojear midan igual, no
    /// que coincidan hasta el ultimo bit.
    /// </remarks>
    public static IReadOnlyList<string> TamanosDe(string rutaPdf)
    {
        using var documento = PdfDocument.Open(rutaPdf);
        return [.. documento.GetPages().Select(hoja => $"{Math.Round(hoja.Width)}x{Math.Round(hoja.Height)}")];
    }
}
