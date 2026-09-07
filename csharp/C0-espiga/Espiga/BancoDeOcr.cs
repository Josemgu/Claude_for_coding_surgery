using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Espiga;

/// <summary>
/// El criterio C0-8. Pasa los PDF reales por el OCR de Windows y cuenta lo que
/// se puede contar sin reglas de extraccion: MRN enteros y fechas. Los nombres
/// se comprueban a ojo sobre el volcado, que es lo que pide el criterio.
/// </summary>
public static partial class BancoDeOcr
{
    /// <summary>MRN: tres digitos, cuatro, cuatro, con guiones.</summary>
    [GeneratedRegex(@"\b\d{3}-\d{4}-\d{4}\b")]
    private static partial Regex PatronDeMrn();

    /// <summary>Cualquier fecha con anio de cuatro digitos, en varias formas.</summary>
    [GeneratedRegex(@"\b(\d{1,2}\s+\w+\s+\d{4}|\w+\s+\d{1,2},?\s+\d{4}|\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{4})\b")]
    private static partial Regex PatronDeFecha();

    /// <summary>Lee todos los PDF configurados y devuelve el resumen de cifras.</summary>
    public static async Task<string> MedirAsync()
    {
        var motor = LectorDeOcr.CrearMotorEnEspanol(out var idioma);
        if (motor is null)
        {
            return "C0-8 SIN MOTOR: no hay ningun idioma espanol de reconocimiento en esta maquina.";
        }

        var rutas = Ajustes.RutasDeLosPdf;
        if (rutas.Count == 0)
        {
            return "C0-8 SIN PDF: la variable FICHAS_C0_PDFS viene vacia.";
        }

        Directory.CreateDirectory(Ajustes.CarpetaDelVolcado);

        var resumen = new StringBuilder();
        resumen.AppendLine($"C0-8 motor de OCR en '{idioma}'; {rutas.Count} archivo(s)");

        foreach (var ruta in rutas)
        {
            resumen.AppendLine(await LeerYAnotarAsync(ruta, motor));
        }

        return resumen.ToString().TrimEnd();
    }

    /// <summary>Lee un PDF, guarda su volcado y devuelve el renglon de cifras.</summary>
    private static async Task<string> LeerYAnotarAsync(string ruta, Windows.Media.Ocr.OcrEngine motor)
    {
        if (!File.Exists(ruta))
        {
            return $"  {Path.GetFileName(ruta)}: NO EXISTE";
        }

        var lectura = await LectorDeOcr.LeerUnPdfAsync(ruta, motor);

        var destino = Path.Combine(
            Ajustes.CarpetaDelVolcado,
            Path.GetFileNameWithoutExtension(ruta)
                + "." + (Environment.GetEnvironmentVariable("FICHAS_C0_TOPE") ?? "3500")
                + ".volcado.txt");
        await File.WriteAllTextAsync(destino, lectura.Volcado, Encoding.UTF8);

        var mrn = PatronDeMrn().Matches(lectura.Texto)
            .Select(coincidencia => coincidencia.Value)
            .Distinct()
            .ToList();

        var fechas = PatronDeFecha().Matches(lectura.Texto)
            .Select(coincidencia => coincidencia.Value)
            .Distinct()
            .ToList();

        var renglones = lectura.Texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        return string.Format(
            CultureInfo.InvariantCulture,
            "  {0}: {1} hoja(s), {2:F1} s, {3} renglones, MRN enteros {4} [{5}], fechas {6} [{7}]",
            Path.GetFileName(ruta),
            lectura.Hojas,
            lectura.SegundosDeLectura,
            renglones,
            mrn.Count,
            string.Join(" ", mrn),
            fechas.Count,
            string.Join(" | ", fechas));
    }
}
