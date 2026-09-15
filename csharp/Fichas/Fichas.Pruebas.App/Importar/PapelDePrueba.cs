using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Papeles de verdad en disco para las pruebas del papel de cada documento: un PDF por
/// hoja rotulada, y la forma de leer de vuelta qué dice una hoja.
/// </summary>
/// <remarks>
/// <para>Existe por el defecto del 2026-09-15: el visor de un documento enseñaba el papel de
/// OTRO. Eso no se puede probar con una hoja de mentira que solo lleva una ruta: hace falta
/// que en esa ruta haya un archivo, que el archivo CAMBIE por debajo —como hace un escáner
/// que escribe siempre <c>Scan.pdf</c>— y poder abrir después lo que el caso guarda y leer
/// qué papel es.</para>
///
/// <para>El OCR sigue siendo de mentira (<see cref="BaseDeImportacion.Hoja"/>): lo que se
/// prueba aquí no es leer el papel sino a qué papel queda atado cada documento. Ningún PDF
/// del dueño entra en las pruebas.</para>
/// </remarks>
internal static class PapelDePrueba
{
    /// <summary>Escribe un PDF con una hoja por rótulo y devuelve su ruta; si ya existe, lo sobrescribe.</summary>
    /// <param name="carpeta">Dónde dejarlo; se crea si no existe.</param>
    /// <param name="nombre">El nombre del archivo, con su <c>.pdf</c>.</param>
    /// <param name="rotulos">Un texto por hoja, en el orden en que irán; es lo que <see cref="LoQueDiceLaHoja"/> lee de vuelta.</param>
    public static string Escribir(string carpeta, string nombre, params string[] rotulos)
    {
        Directory.CreateDirectory(carpeta);
        var constructor = new PdfDocumentBuilder();
        var tipografia = constructor.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var rotulo in rotulos)
        {
            var hoja = constructor.AddPage(PageSize.Letter);
            hoja.AddText(rotulo, 20, new PdfPoint(60, 700), tipografia);
        }

        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllBytes(ruta, constructor.Build());
        return ruta;
    }

    /// <summary>Lo que dice una hoja de un PDF, leído del archivo de verdad.</summary>
    /// <param name="rutaPdf">El archivo; nulo o inexistente devuelve «(sin papel)».</param>
    /// <param name="pagina">La hoja, base 1; nula o fuera de rango devuelve «(sin hoja)».</param>
    /// <returns>Las palabras de la hoja unidas por un espacio.</returns>
    public static string LoQueDiceLaHoja(string? rutaPdf, int? pagina)
    {
        if (rutaPdf is null || !File.Exists(rutaPdf)) return "(sin papel)";
        using var documento = PdfDocument.Open(rutaPdf);
        if (pagina is null || pagina < 1 || pagina > documento.NumberOfPages) return "(sin hoja)";
        return string.Join(" ", documento.GetPage(pagina.Value).GetWords().Select(palabra => palabra.Text));
    }
}
