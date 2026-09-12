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

    /// <summary>Los caracteres que Windows no admite en un nombre de archivo; cada uno se vuelve un guion.</summary>
    private static readonly char[] LoQueWindowsNoAdmite = Path.GetInvalidFileNameChars();

    /// <summary>Lo que llevan los tres nombres antes de la extension, en un solo sitio.</summary>
    /// <remarks>
    /// Desde el 2026-09-07 cada informe sale en dos formatos, y el tronco lo comparten los dos:
    /// el PDF y el Excel del mismo período son el mismo informe y se llaman igual, con distinta
    /// extension. Escrito dos veces, cambiar el nombre en un sitio dejaria al dueño con
    /// «Reporte …» y «Informe …» del mismo mes, buscando en cuál está lo que ya vio.
    /// </remarks>
    /// <param name="periodo">El período de la pantalla, con sus dos fechas ISO.</param>
    private static string TroncoDelPeriodo(PeriodoDeLaPantalla periodo)
        => $"Reporte {periodo.Desde} a {periodo.Hasta}";

    /// <summary>«Histórico 2026-09-04», sin extensión; lo comparten el PDF y el Excel.</summary>
    /// <param name="hoyIso">La fecha de hoy en <c>AAAA-MM-DD</c>.</param>
    private static string TroncoDelHistorico(string hoyIso) => $"Histórico {hoyIso}";

    /// <summary>«Informe de Sandy 2026-09-01 a 2026-09-30», sin extensión; lo comparten el PDF y el Excel.</summary>
    /// <param name="nombreDelCompanero">El nombre tal como está en la base; nulo o vacío se vuelve «sin nombre».</param>
    /// <param name="periodo">El período de la pantalla.</param>
    private static string TroncoDelInformeDeAgente(string? nombreDelCompanero, PeriodoDeLaPantalla periodo)
        => $"Informe de {QuienEs(nombreDelCompanero)} {periodo.Desde} a {periodo.Hasta}";

    /// <summary>«Reporte 2026-09-01 a 2026-09-30.pdf».</summary>
    /// <param name="periodo">El período de la pantalla.</param>
    public static string DelReporteDelPeriodo(PeriodoDeLaPantalla periodo)
        => Limpiar(TroncoDelPeriodo(periodo), ".pdf");

    /// <summary>«Reporte 2026-09-01 a 2026-09-30.xlsx».</summary>
    /// <param name="periodo">El período de la pantalla.</param>
    public static string DelReporteDelPeriodoEnExcel(PeriodoDeLaPantalla periodo)
        => Limpiar(TroncoDelPeriodo(periodo), ".xlsx");

    /// <summary>«Historico 2026-09-04.pdf».</summary>
    /// <param name="hoyIso">La fecha de hoy en <c>AAAA-MM-DD</c>.</param>
    public static string DelHistorico(string hoyIso) => Limpiar(TroncoDelHistorico(hoyIso), ".pdf");

    /// <summary>«Historico 2026-09-04.xlsx».</summary>
    /// <param name="hoyIso">La fecha de hoy en <c>AAAA-MM-DD</c>.</param>
    public static string DelHistoricoEnExcel(string hoyIso) => Limpiar(TroncoDelHistorico(hoyIso), ".xlsx");

    /// <summary>«Paquete de Sandy 2026-09-04.xlsx».</summary>
    /// <param name="nombreDelCompanero">El nombre tal como está en la base; nulo o vacío se vuelve «sin nombre».</param>
    /// <param name="hoyIso">La fecha de hoy en <c>AAAA-MM-DD</c>.</param>
    public static string DelPaquete(string? nombreDelCompanero, string hoyIso)
        => Limpiar($"Paquete de {QuienEs(nombreDelCompanero)} {hoyIso}", ".xlsx");

    /// <summary>«Segunda vuelta de Yudelka 2026-09-05.xlsx».</summary>
    /// <remarks>
    /// Se llama «segunda vuelta» y no «paquete del gerente» a proposito: el dueno generalizo su
    /// peticion a una escalera de categorias, y un gerente es un peldano de esa escalera, no una
    /// cosa aparte. Un nombre de archivo que dijera «gerente» envejeceria el dia que el anada
    /// una categoria 4.
    /// </remarks>
    /// <param name="nombreDelCompanero">El nombre de quien la recibe; nulo o vacío se vuelve «sin nombre».</param>
    /// <param name="hoyIso">La fecha de hoy en <c>AAAA-MM-DD</c>.</param>
    public static string DeLaSegundaVuelta(string? nombreDelCompanero, string hoyIso)
        => Limpiar($"Segunda vuelta de {QuienEs(nombreDelCompanero)} {hoyIso}", ".xlsx");

    /// <summary>«Informe de Sandy 2026-09-01 a 2026-09-30.pdf».</summary>
    /// <param name="nombreDelCompanero">El nombre tal como está en la base; nulo o vacío se vuelve «sin nombre».</param>
    /// <param name="periodo">El período de la pantalla.</param>
    public static string DelInformeDeAgente(string? nombreDelCompanero, PeriodoDeLaPantalla periodo)
        => Limpiar(TroncoDelInformeDeAgente(nombreDelCompanero, periodo), ".pdf");

    /// <summary>«Informe de Sandy 2026-09-01 a 2026-09-30.xlsx».</summary>
    /// <param name="nombreDelCompanero">El nombre tal como está en la base; nulo o vacío se vuelve «sin nombre».</param>
    /// <param name="periodo">El período de la pantalla.</param>
    public static string DelInformeDeAgenteEnExcel(string? nombreDelCompanero, PeriodoDeLaPantalla periodo)
        => Limpiar(TroncoDelInformeDeAgente(nombreDelCompanero, periodo), ".xlsx");

    /// <summary>El nombre del companero limpio de espacios, o la palabra que dice que no hay.</summary>
    /// <param name="nombreDelCompanero">El nombre tal como está en la base.</param>
    private static string QuienEs(string? nombreDelCompanero)
        => string.IsNullOrWhiteSpace(nombreDelCompanero) ? CuandoNoHayNombre : nombreDelCompanero.Trim();

    /// <summary>Quita lo que Windows no admite y recorta si no cabe, dejando la extension.</summary>
    /// <param name="tronco">El nombre sin extensión; si queda vacío tras limpiar, se pone «sin nombre».</param>
    /// <param name="extension">La extensión con su punto; nunca se recorta.</param>
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
