using System.Globalization;
using System.Text;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Espiga;

/// <summary>Lo que el OCR devolvio de un PDF entero.</summary>
public sealed class LecturaDeUnPdf
{
    public required string Ruta { get; init; }

    public required int Hojas { get; init; }

    public required string Texto { get; init; }

    public required double SegundosDeLectura { get; init; }

    public required string Volcado { get; init; }
}

/// <summary>
/// Rasteriza un PDF con Windows.Data.Pdf y le pasa cada hoja al motor de OCR de
/// Windows. Es determinista: no hay modelo generativo por medio, solo el motor
/// que trae el sistema (regla permanente 1 de CLAUDE.md).
/// </summary>
public static class LectorDeOcr
{
    /// <summary>
    /// Tope del lado largo al rasterizar, en pixeles. Sale de la regla de no
    /// regresion del proyecto: sin tope, un escaneo a 300 DPI tardaba minutos.
    /// Cabe de sobra en el MaxImageDimension de 10 000 px del motor.
    /// </summary>
    private const uint TopePorOmision = 3500;

    /// <summary>
    /// El tope efectivo. Se puede subir por entorno para medir si la lectura de
    /// los MRN depende de la resolucion; el motor admite hasta 10 000 px.
    /// </summary>
    private static uint TopeDelLadoLargo =>
        uint.TryParse(Environment.GetEnvironmentVariable("FICHAS_C0_TOPE"), out var tope) && tope > 0
            ? Math.Min(tope, OcrEngine.MaxImageDimension)
            : TopePorOmision;

    /// <summary>Idiomas que se prueban, en orden, para crear el motor.</summary>
    private static readonly string[] IdiomasPreferidos = ["es-MX", "es-ES", "es"];

    /// <summary>Devuelve los idiomas de reconocimiento instalados en la maquina.</summary>
    public static string ListarIdiomasDisponibles()
    {
        var renglones = OcrEngine.AvailableRecognizerLanguages
            .Select(idioma => $"  {idioma.LanguageTag}  |  {idioma.DisplayName}")
            .ToList();

        return $"Idiomas de reconocimiento: {renglones.Count}\n"
             + string.Join("\n", renglones)
             + $"\nMaxImageDimension: {OcrEngine.MaxImageDimension}";
    }

    /// <summary>
    /// Crea el motor con el primer idioma espanol disponible. Devuelve null si no
    /// hay ninguno: ese es el caso en que la via A del ADR no sirve.
    /// </summary>
    public static OcrEngine? CrearMotorEnEspanol(out string etiquetaDelIdioma)
    {
        foreach (var etiqueta in IdiomasPreferidos)
        {
            var motor = OcrEngine.TryCreateFromLanguage(new Language(etiqueta));
            if (motor is not null)
            {
                etiquetaDelIdioma = motor.RecognizerLanguage.LanguageTag;
                return motor;
            }
        }

        etiquetaDelIdioma = "(ninguno)";
        return null;
    }

    /// <summary>Lee un PDF entero y devuelve su texto y el volcado con rectangulos.</summary>
    public static async Task<LecturaDeUnPdf> LeerUnPdfAsync(string ruta, OcrEngine motor)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        var archivo = await StorageFile.GetFileFromPathAsync(ruta);
        var documento = await PdfDocument.LoadFromFileAsync(archivo);

        var texto = new StringBuilder();
        var volcado = new StringBuilder();
        volcado.AppendLine($"===== {Path.GetFileName(ruta)}  ({documento.PageCount} hoja(s)) =====");

        for (var numeroDeHoja = 0u; numeroDeHoja < documento.PageCount; numeroDeHoja++)
        {
            using var hoja = documento.GetPage(numeroDeHoja);
            var resultado = await LeerUnaHojaAsync(hoja, motor);

            volcado.AppendLine($"--- hoja {numeroDeHoja + 1} ---");
            foreach (var renglon in resultado.Lines)
            {
                var caja = CajaDelRenglon(renglon);
                volcado.AppendLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "[x={0:F1} y={1:F1} ancho={2:F1} alto={3:F1}] {4}",
                        caja.X, caja.Y, caja.Ancho, caja.Alto, renglon.Text));
                texto.AppendLine(renglon.Text);
            }
        }

        reloj.Stop();

        return new LecturaDeUnPdf
        {
            Ruta = ruta,
            Hojas = (int)documento.PageCount,
            Texto = texto.ToString(),
            SegundosDeLectura = reloj.Elapsed.TotalSeconds,
            Volcado = volcado.ToString()
        };
    }

    /// <summary>Rasteriza una hoja al tope de 3 500 px y se la pasa al motor.</summary>
    private static async Task<OcrResult> LeerUnaHojaAsync(PdfPage hoja, OcrEngine motor)
    {
        using var memoria = new InMemoryRandomAccessStream();
        await hoja.RenderToStreamAsync(memoria, OpcionesDeRasterizado(hoja));

        var descodificador = await BitmapDecoder.CreateAsync(memoria);
        using var mapaDeBits = await descodificador.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        return await motor.RecognizeAsync(mapaDeBits);
    }

    /// <summary>Calcula el tamano de salida respetando el tope del lado largo.</summary>
    private static PdfPageRenderOptions OpcionesDeRasterizado(PdfPage hoja)
    {
        var ancho = hoja.Size.Width;
        var alto = hoja.Size.Height;
        var factor = TopeDelLadoLargo / Math.Max(ancho, alto);

        return new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Round(ancho * factor),
            DestinationHeight = (uint)Math.Round(alto * factor)
        };
    }

    /// <summary>Une las cajas de las palabras para dar la caja del renglon.</summary>
    private static (double X, double Y, double Ancho, double Alto) CajaDelRenglon(OcrLine renglon)
    {
        if (renglon.Words.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var izquierda = renglon.Words.Min(palabra => palabra.BoundingRect.X);
        var arriba = renglon.Words.Min(palabra => palabra.BoundingRect.Y);
        var derecha = renglon.Words.Max(palabra => palabra.BoundingRect.X + palabra.BoundingRect.Width);
        var abajo = renglon.Words.Max(palabra => palabra.BoundingRect.Y + palabra.BoundingRect.Height);

        return (izquierda, arriba, derecha - izquierda, abajo - arriba);
    }
}
