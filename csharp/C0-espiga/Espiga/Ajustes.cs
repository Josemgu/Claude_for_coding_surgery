namespace Espiga;

/// <summary>
/// Lo que la espiga toma del entorno. Va por variables de entorno y no por
/// constantes en el codigo para que ninguna ruta del dueno viaje al repositorio:
/// los PDF y el volcado son datos personales.
/// </summary>
public static class Ajustes
{
    /// <summary>Cierra la ventana en cuanto anota el arranque. Sirve para el C0-7.</summary>
    public static bool SaleSolo => EstaEncendida("FICHAS_C0_SALIR");

    /// <summary>Mide pintado, conteo y clic sin que nadie toque nada. Para el C0-3 y el C0-4.</summary>
    public static bool MideSolo => EstaEncendida("FICHAS_C0_AUTO");

    /// <summary>Ademas de lo anterior, pasa los PDF por el OCR. Para el C0-8.</summary>
    public static bool LeeLosPdf => EstaEncendida("FICHAS_C0_OCR");

    /// <summary>Cierra la ventana al terminar el modo automatico. Sin esto se queda abierta.</summary>
    public static bool CierraAlTerminar => EstaEncendida("FICHAS_C0_CIERRA");

    /// <summary>Rutas de los PDF a leer, separadas por punto y coma.</summary>
    public static IReadOnlyList<string> RutasDeLosPdf =>
        (Environment.GetEnvironmentVariable("FICHAS_C0_PDFS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Carpeta donde cae el volcado del OCR. Fuera del repositorio.</summary>
    public static string CarpetaDelVolcado =>
        Environment.GetEnvironmentVariable("FICHAS_C0_VOLCADO")
        ?? Path.Combine(Path.GetTempPath(), "fichas-c0");

    private static bool EstaEncendida(string nombre) =>
        Environment.GetEnvironmentVariable(nombre) == "1";
}
