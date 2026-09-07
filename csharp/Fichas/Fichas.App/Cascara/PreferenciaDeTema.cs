using System.Diagnostics;
using System.Text;

namespace Fichas.App.Cascara;

/// <summary>
/// Donde se guarda el tema que eligio el dueno: <c>preferencias.txt</c> en la carpeta de datos.
/// </summary>
/// <remarks>
/// <para>⛔ En la CARPETA DE DATOS —la que resuelve <c>SHGetKnownFolderPath</c> y cambia
/// <c>--carpeta-de-datos</c>—, no junto al ejecutable y no en el registro de Windows. Junto al
/// ejecutable se pierde en cuanto se sustituye la carpeta del programa por una version nueva,
/// que es exactamente lo que dice la regla 4 de <c>CLAUDE.md</c> sobre la base y el Excel.</para>
///
/// <para>Es texto plano y en plural a proposito: el dueno puede abrirlo y ver que guardo el
/// programa, y si manana entra otra preferencia cabe al lado sin inventar otro archivo.</para>
///
/// <para>No lanza nunca. Si no se puede leer, se abre con el de Windows, que es lo que hacia
/// el programa antes de que esto existiera; si no se puede escribir, se dice devolviendo
/// falso y quien llama decide si lo cuenta en la franja.</para>
/// </remarks>
public sealed class PreferenciaDeTema
{
    /// <summary>La clave dentro del archivo. Sin tildes: es una clave, no un rotulo.</summary>
    private const string LaClave = "tema";

    private readonly string _carpetaDeDatos;

    /// <summary>Apunta a la carpeta de datos; todavia no toca el disco.</summary>
    public PreferenciaDeTema(string carpetaDeDatos)
    {
        ArgumentNullException.ThrowIfNull(carpetaDeDatos);
        _carpetaDeDatos = carpetaDeDatos;
    }

    /// <summary>El archivo donde vive la eleccion, para poder ensenarlo en una entrega.</summary>
    public string Ruta => Path.Combine(_carpetaDeDatos, "preferencias.txt");

    /// <summary>Lo que eligio el dueno, o el de Windows si nunca eligio o el archivo no sirve.</summary>
    public TemaDeLaVentana Leer()
    {
        foreach (var linea in LasLineas())
        {
            if (EsLaDelTema(linea)) return TemasDeLaVentana.Interpretar(linea[(LaClave.Length + 1)..]);
        }

        return TemaDeLaVentana.ElDeWindows;
    }

    /// <summary>Guarda la eleccion; devuelve falso si no se pudo escribir.</summary>
    /// <remarks>
    /// Reescribe el archivo entero con las demas lineas intactas y UNA sola de tema. Anadir al
    /// final dejaria dos lineas de tema despues del segundo cambio, y entonces cual manda
    /// dependeria del orden de lectura.
    /// </remarks>
    public bool Guardar(TemaDeLaVentana tema)
    {
        var lineas = LasLineas().Where(l => !EsLaDelTema(l)).ToList();
        lineas.Add($"{LaClave}={TemasDeLaVentana.ComoSeGuarda(tema)}");

        try
        {
            Directory.CreateDirectory(_carpetaDeDatos);
            File.WriteAllLines(Ruta, lineas, Encoding.UTF8);
            return true;
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            Debug.WriteLine($"No se pudo guardar el tema en «{Ruta}»: {fallo.Message}");
            return false;
        }
    }

    /// <summary>Las lineas del archivo, o ninguna si no hay archivo o no se puede leer.</summary>
    private IReadOnlyList<string> LasLineas()
    {
        try
        {
            return File.Exists(Ruta) ? File.ReadAllLines(Ruta) : [];
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            Debug.WriteLine($"No se pudo leer «{Ruta}»: {fallo.Message}");
            return [];
        }
    }

    /// <summary>Si la linea es la del tema; se admite «tema = oscuro» con espacios.</summary>
    private static bool EsLaDelTema(string linea)
        => linea.TrimStart().StartsWith(LaClave + "=", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Los fallos de disco que se recogen; cualquier otro sube, porque seria un error nuestro.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>ArgumentException</c> y <c>NotSupportedException</c> estan porque una ruta con
    /// caracteres que Windows no admite entra por ahi y no por <c>IOException</c>. Sin ellas,
    /// un <c>--carpeta-de-datos</c> mal tecleado tumbaba el arranque en vez de ignorarse.
    /// </remarks>
    private static bool EsUnFalloDeDisco(Exception fallo)
        => fallo is IOException or UnauthorizedAccessException or ArgumentException
                 or NotSupportedException or System.Security.SecurityException;
}
