using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Fichas.App.Revisar;

/// <summary>
/// Donde se recuerda el ancho del panel de carpetas: la línea <c>ancho_del_panel_de_carpetas=</c>
/// del mismo <c>preferencias.txt</c> del tema, en la carpeta de datos.
/// </summary>
/// <remarks>
/// <para>⛔ En la CARPETA DE DATOS —la de <c>--carpeta-de-datos</c>— y no junto al ejecutable,
/// por la misma razón que el tema (<c>Cascara/PreferenciaDeTema</c>): junto al ejecutable se
/// pierde con cada versión nueva. Y en el MISMO archivo, no en otro: el dueño abre uno y ve
/// todo lo que el programa le guardó.</para>
///
/// <para>Reescribe el archivo entero con las demás líneas intactas y UNA sola de ancho, que es
/// lo mismo que hace el tema con la suya; así las dos clases pueden escribir el mismo archivo
/// sin pisarse la línea. Comparten la forma y no el código porque esta carpeta es del
/// programador de Revisar y aquella de la cáscara; si mañana entra una tercera preferencia,
/// lo que toca es sacar un lector-escritor común a la cáscara.</para>
///
/// <para>No lanza nunca. Si el archivo no está, no se puede leer o el número está estropeado,
/// se abre con lo de siempre; si no se puede escribir, devuelve falso y la página decide si
/// lo cuenta en la franja.</para>
/// </remarks>
public sealed class PreferenciaDelAnchoDelPanel
{
    /// <summary>La clave dentro del archivo. Sin tildes: es una clave, no un rótulo.</summary>
    private const string LaClave = "ancho_del_panel_de_carpetas";

    /// <summary>La carpeta de datos donde vive <c>preferencias.txt</c>; se crea al guardar si no existe.</summary>
    private readonly string _carpetaDeDatos;

    /// <summary>Apunta a la carpeta de datos; todavía no toca el disco.</summary>
    /// <param name="carpetaDeDatos">La misma carpeta de la base, la que resolvió <c>ArgumentosDeArranque</c>.</param>
    public PreferenciaDelAnchoDelPanel(string carpetaDeDatos)
    {
        ArgumentNullException.ThrowIfNull(carpetaDeDatos);
        _carpetaDeDatos = carpetaDeDatos;
    }

    /// <summary>El archivo donde vive la elección: el mismo del tema.</summary>
    public string Ruta => Path.Combine(_carpetaDeDatos, "preferencias.txt");

    /// <summary>El ancho que él dejó, o lo de siempre si nunca arrastró o el archivo no sirve.</summary>
    /// <remarks>
    /// Un número que no se entiende o negativo cuenta como «no hay»: es preferible abrir con
    /// 290 que con un panel invisible. El recorte al mínimo y al máximo NO se hace aquí, sino
    /// al pintar, porque depende de la ventana de ese momento.
    /// </remarks>
    public double Leer()
    {
        foreach (var linea in LasLineas())
        {
            if (!EsLaDelAncho(linea)) continue;
            var texto = linea.TrimStart()[(LaClave.Length + 1)..].Trim();
            return int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var ancho) && ancho > 0
                ? ancho
                : AnchoDelPanelDeCarpetas.ElDeSiempre;
        }

        return AnchoDelPanelDeCarpetas.ElDeSiempre;
    }

    /// <summary>Guarda el ancho redondeado a píxel entero; devuelve falso si no se pudo escribir.</summary>
    /// <param name="ancho">El ancho elegido, tal como lo dejó el ratón; se redondea.</param>
    /// <returns>Verdadero si el archivo quedó escrito; falso si el disco no dejó, y entonces se sigue con el ancho en memoria.</returns>
    public bool Guardar(double ancho)
    {
        var lineas = LasLineas().Where(l => !EsLaDelAncho(l)).ToList();
        lineas.Add($"{LaClave}={Math.Round(ancho).ToString(CultureInfo.InvariantCulture)}");

        try
        {
            Directory.CreateDirectory(_carpetaDeDatos);
            File.WriteAllLines(Ruta, lineas, Encoding.UTF8);
            return true;
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            Debug.WriteLine($"No se pudo guardar el ancho del panel en «{Ruta}»: {fallo.Message}");
            return false;
        }
    }

    /// <summary>Las líneas del archivo, o ninguna si no hay archivo o no se puede leer.</summary>
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

    /// <summary>Si la línea es la del ancho; se admite «ancho_del_panel_de_carpetas = 300» con espacios.</summary>
    /// <param name="linea">Una línea del archivo tal cual.</param>
    private static bool EsLaDelAncho(string linea)
        => linea.TrimStart().StartsWith(LaClave + "=", StringComparison.OrdinalIgnoreCase);

    /// <summary>Los fallos de disco que se recogen; cualquier otro sube, porque sería un error nuestro.</summary>
    /// <remarks>
    /// Los mismos que recoge el tema, y por la misma razón: una ruta con caracteres que
    /// Windows no admite entra por <c>ArgumentException</c> o <c>NotSupportedException</c>,
    /// no por <c>IOException</c>.
    /// </remarks>
    /// <param name="fallo">La excepción que saltó al leer o escribir.</param>
    private static bool EsUnFalloDeDisco(Exception fallo)
        => fallo is IOException or UnauthorizedAccessException or ArgumentException
                 or NotSupportedException or System.Security.SecurityException;
}
