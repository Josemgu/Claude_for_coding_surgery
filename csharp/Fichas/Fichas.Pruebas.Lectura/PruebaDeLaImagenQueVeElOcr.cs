using Fichas.Lectura;
using SkiaSharp;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// La imagen que el OCR recibe por el camino nuevo —el mapa de bits de PDFium tal cual—
/// es, byte a byte, la misma que recibía por el viejo —ese mapa codificado a PNG y
/// decodificado otra vez—.
/// </summary>
/// <remarks>
/// <para>Criterio (plan R-3 del 2026-09-15, la prueba que bloquea el cierre): <b>dado</b>
/// una hoja del corpus del dueño, <b>cuando</b> se rasteriza por
/// <see cref="LecturaDePdf.RasterizarPagina"/> (PNG) y se decodifica como hacía el OCR, y
/// se rasteriza por <see cref="LecturaDePdf.RasterizarHoja"/> (sin PNG), <b>entonces</b>
/// los dos mapas tienen el mismo ancho, alto, tipo de color y alfa, y
/// <see cref="SKBitmap.Bytes"/> idénticos. El OCR es sensible a la imagen: si un solo byte
/// cambiara, la huella de los dieciséis podría cambiar sin que nadie supiera por qué.</para>
///
/// <para>Se comparan cuatro hojas: la primera de tres documentos de una hoja y la sexta
/// del primero de seis. ⚠️ Son datos personales: no están en el repositorio, no se copian
/// y aquí solo se nombran por su prefijo. Sin la carpeta, no concluyente.</para>
/// </remarks>
[TestClass]
public class PruebaDeLaImagenQueVeElOcr
{
    /// <summary>Dónde están los documentos en esta máquina; fuera del repositorio.</summary>
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>Las hojas que se comparan: prefijo del documento y número de hoja.</summary>
    private static readonly (string Prefijo, int Hoja)[] HojasComparadas =
    [
        ("45ee5474", 1),
        ("4899dc88", 1),
        ("87b3ffb0", 1),
        ("cb2f18be", 6),
    ];

    /// <summary>La ruta del documento con ese prefijo, o no concluyente si no está.</summary>
    /// <param name="prefijo">Los ocho primeros caracteres del nombre del archivo.</param>
    private static string RutaDe(string prefijo)
    {
        var ruta = Directory.Exists(CarpetaDeLosDocumentos)
            ? Directory.GetFiles(CarpetaDeLosDocumentos, prefijo + "*.pdf").SingleOrDefault()
            : null;
        if (ruta is null)
        {
            Assert.Inconclusive($"No está el documento {prefijo} en «{CarpetaDeLosDocumentos}»: son datos personales del dueño.");
        }
        return ruta!;
    }

    /// <summary>Dada una hoja del corpus, cuando se rasteriza por los dos caminos, entonces los píxeles son idénticos byte a byte.</summary>
    /// <param name="indice">Qué hoja de <see cref="HojasComparadas"/>.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void ElMapaDePdfiumYElPngDecodificadoSonElMismoByteAByte(int indice)
    {
        var (prefijo, hoja) = HojasComparadas[indice];
        var ruta = RutaDe(prefijo);
        using var lectura = new LecturaDePdf();

        var imagen = lectura.RasterizarPagina(ruta, hoja, Geometria.LadoLargoMaximoPx);
        Assert.IsNotNull(imagen, $"El camino viejo tiene que rasterizar la hoja {hoja} de {prefijo}.");
        using var viejo = SKBitmap.Decode(imagen.Png);
        Assert.IsNotNull(viejo);

        using var documento = lectura.AbrirDocumento(ruta);
        Assert.IsNotNull(documento);
        using var nuevo = lectura.RasterizarHoja(documento, hoja, Geometria.LadoLargoMaximoPx);
        Assert.IsNotNull(nuevo, $"El camino nuevo tiene que rasterizar la hoja {hoja} de {prefijo}.");

        Assert.AreEqual((viejo.Width, viejo.Height), (nuevo.Width, nuevo.Height), "Mismo tamaño en píxeles.");
        Assert.AreEqual((viejo.ColorType, viejo.AlphaType, viejo.RowBytes), (nuevo.ColorType, nuevo.AlphaType, nuevo.RowBytes),
            "Mismo tipo de color, mismo alfa y misma anchura de fila: si no, los bytes no son comparables.");
        Assert.IsTrue(viejo.Bytes.AsSpan().SequenceEqual(nuevo.Bytes),
            $"Los píxeles de la hoja {hoja} de {prefijo} no son los mismos por los dos caminos ({viejo.Bytes.Length} bytes).");
    }
}
