namespace Fichas.App.Reportes;

/// <summary>
/// El nombre que se le propone al selector para el PDF y para el Excel.
/// </summary>
/// <remarks>
/// Se limpia en un solo sitio y para las dos pantallas. El nombre de un companero entra tal
/// como esta escrito en la base, y ahi puede haber una barra o dos puntos —«Ana María /
/// Sandy»—: Windows no los admite en un nombre de archivo y la escritura fallaria con un
/// mensaje del sistema que no dice que hacer. Aqui se sustituyen antes de que nadie los vea.
/// </remarks>
public static class NombreDeArchivo
{
    /// <summary>Lo que se pone donde el nombre venia vacio, para que el archivo tenga uno.</summary>
    public const string CuandoNoHayNombre = "sin nombre";

    /// <summary>Lo mas largo que puede medir un nombre de archivo en Windows, con su extension.</summary>
    private const int LargoMaximo = 255;

    private static readonly char[] LoQueWindowsNoAdmite = Path.GetInvalidFileNameChars();

    /// <summary>«Reporte 2026-09-01 a 2026-09-30.pdf».</summary>
    public static string DelReporteDelPeriodo(PeriodoDeLaPantalla periodo)
        => Limpiar($"Reporte {periodo.Desde} a {periodo.Hasta}", ".pdf");

    /// <summary>«Historico 2026-09-04.pdf».</summary>
    public static string DelHistorico(string hoyIso) => Limpiar($"Histórico {hoyIso}", ".pdf");

    /// <summary>«Paquete de Sandy 2026-09-04.xlsx».</summary>
    public static string DelPaquete(string? nombreDelCompanero, string hoyIso)
    {
        var quien = string.IsNullOrWhiteSpace(nombreDelCompanero)
            ? CuandoNoHayNombre
            : nombreDelCompanero.Trim();
        return Limpiar($"Paquete de {quien} {hoyIso}", ".xlsx");
    }

    /// <summary>«Segunda vuelta de Yudelka 2026-09-05.xlsx».</summary>
    /// <remarks>
    /// Se llama «segunda vuelta» y no «paquete del gerente» a proposito: el dueno generalizo su
    /// peticion a una escalera de categorias, y un gerente es un peldano de esa escalera, no una
    /// cosa aparte. Un nombre de archivo que dijera «gerente» envejeceria el dia que el anada
    /// una categoria 4.
    /// </remarks>
    public static string DeLaSegundaVuelta(string? nombreDelCompanero, string hoyIso)
    {
        var quien = string.IsNullOrWhiteSpace(nombreDelCompanero)
            ? CuandoNoHayNombre
            : nombreDelCompanero.Trim();
        return Limpiar($"Segunda vuelta de {quien} {hoyIso}", ".xlsx");
    }

    /// <summary>«Informe de Sandy 2026-09-01 a 2026-09-30.pdf».</summary>
    public static string DelInformeDeAgente(string? nombreDelCompanero, PeriodoDeLaPantalla periodo)
    {
        var quien = string.IsNullOrWhiteSpace(nombreDelCompanero)
            ? CuandoNoHayNombre
            : nombreDelCompanero.Trim();
        return Limpiar($"Informe de {quien} {periodo.Desde} a {periodo.Hasta}", ".pdf");
    }

    /// <summary>Quita lo que Windows no admite y recorta si no cabe, dejando la extension.</summary>
    private static string Limpiar(string tronco, string extension)
    {
        var limpio = new string(tronco.Select(letra => LoQueWindowsNoAdmite.Contains(letra) ? '-' : letra).ToArray())
            .Trim();
        if (limpio.Length == 0) limpio = CuandoNoHayNombre;

        var cabe = LargoMaximo - extension.Length;
        if (limpio.Length > cabe) limpio = limpio[..cabe].TrimEnd();

        return limpio + extension;
    }
}
