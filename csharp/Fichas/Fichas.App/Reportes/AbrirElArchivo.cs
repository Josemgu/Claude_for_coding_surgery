using System.ComponentModel;
using System.Diagnostics;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Reportes;

/// <summary>
/// Abrir con Windows el archivo que se acaba de generar, o su carpeta.
/// </summary>
/// <remarks>
/// El pase lo pide con estas palabras: «decir dónde quedó y poder abrirlo». Se abre con el
/// programa que el dueno tenga puesto para los PDF y para los Excel; el programa no trae
/// visor propio y no hace falta.
/// <para>
/// ⚠️ Un fallo al abrir NO se traga. Si no hay ningun programa asociado a los <c>.pdf</c>, o
/// si el archivo se movio entre generarlo y pulsar «Abrir», sale un aviso de una linea con lo
/// que dijo el sistema. Callarlo dejaria un boton que no hace nada, que es el fallo que QA
/// encontro en la pantalla de Importar.
/// </para>
/// </remarks>
public static class AbrirElArchivo
{
    /// <summary>Abre ese archivo con Windows; devuelve el aviso si no se pudo.</summary>
    public static Aviso? Abrir(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return Aviso.Advierte(
                "Todavía no hay ningún archivo que abrir.",
                string.Empty,
                "Genere primero el reporte o el paquete; el botón se enciende cuando el archivo existe.");
        }

        if (!File.Exists(ruta))
        {
            return Aviso.Problema(
                $"«{Path.GetFileName(ruta)}» ya no está donde quedó.",
                string.Empty,
                $"Se buscó en «{ruta}». Alguien lo movió, lo renombró o lo borró después de generarlo.");
        }

        return Lanzar(ruta, $"«{Path.GetFileName(ruta)}»");
    }

    /// <summary>Abre en el Explorador la carpeta donde quedo el archivo.</summary>
    public static Aviso? AbrirLaCarpeta(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return Aviso.Advierte(
                "Todavía no hay ninguna carpeta que abrir.",
                string.Empty,
                "Genere primero el reporte o el paquete.");
        }

        var carpeta = Path.GetDirectoryName(ruta);
        if (string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            return Aviso.Problema(
                "La carpeta donde quedó el archivo ya no está.",
                string.Empty,
                $"Se buscó «{carpeta}», que sale de la ruta «{ruta}».");
        }

        return Lanzar(carpeta, $"la carpeta «{carpeta}»");
    }

    /// <summary>
    /// Se lo entrega a Windows y traduce el fallo, si lo hay, a un aviso de una linea.
    /// </summary>
    /// <remarks>
    /// <c>UseShellExecute</c> en verdadero es lo que hace que Windows elija el programa
    /// asociado; sin el, .NET intentaria ejecutar el propio PDF como si fuera un programa.
    /// </remarks>
    private static Aviso? Lanzar(string queSeAbre, string comoSeLlama)
    {
        try
        {
            using var proceso = Process.Start(new ProcessStartInfo(queSeAbre) { UseShellExecute = true });
            return null;
        }
        catch (Exception causa) when (causa is Win32Exception or InvalidOperationException
                                          or FileNotFoundException or ObjectDisposedException
                                          or PlatformNotSupportedException)
        {
            return Aviso.Problema(
                $"Windows no pudo abrir {comoSeLlama}: {causa.GetType().Name}.",
                string.Empty,
                $"Lo que dijo el sistema: {causa.Message}. "
                + "Casi siempre significa que no hay ningún programa asociado a ese tipo de archivo. "
                + "El archivo está escrito y se puede abrir a mano desde la carpeta.");
        }
    }
}
