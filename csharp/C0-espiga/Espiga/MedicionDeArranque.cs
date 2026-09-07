using System.Diagnostics;
using System.Globalization;

namespace Espiga;

/// <summary>
/// El criterio C0-7 pide que el programa escriba su propia cifra de arranque en
/// un archivo de texto al lado del ejecutable. Aqui esta esa cifra, y no una
/// tomada por fuera con un cronometro de PowerShell, que mediria otra cosa.
/// </summary>
public static class MedicionDeArranque
{
    private static readonly string CarpetaDelEjecutable =
        AppContext.BaseDirectory;

    /// <summary>
    /// Segundos desde que Windows arranco el proceso hasta que la ventana esta
    /// lista. Se anexa al archivo para que las cuatro corridas queden juntas.
    /// </summary>
    public static string EscribirLaCifra()
    {
        var arranco = Process.GetCurrentProcess().StartTime;
        var segundos = (DateTime.Now - arranco).TotalSeconds;

        var renglon = string.Format(
            CultureInfo.InvariantCulture,
            "{0:yyyy-MM-dd HH:mm:ss}  arranque hasta ventana lista: {1:F3} s",
            DateTime.Now,
            segundos);

        File.AppendAllText(Path.Combine(CarpetaDelEjecutable, "arranque.txt"), renglon + Environment.NewLine);

        return $"C0-7 {renglon}";
    }

    /// <summary>Vuelca el cuaderno de cifras al lado del ejecutable.</summary>
    public static void Guardar(string cuaderno)
    {
        File.WriteAllText(Path.Combine(CarpetaDelEjecutable, "cuaderno.txt"), cuaderno);
    }
}
