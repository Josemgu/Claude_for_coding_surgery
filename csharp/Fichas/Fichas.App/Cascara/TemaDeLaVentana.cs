namespace Fichas.App.Cascara;

/// <summary>
/// Entre que temas elige el dueno.
/// </summary>
/// <remarks>
/// Son TRES y no dos. «El de Windows» es lo que hacia el programa antes del 2026-09-05, y
/// tiene que seguir estando: quitarlo obligaria a elegir a quien no quiere elegir.
/// </remarks>
public enum TemaDeLaVentana
{
    /// <summary>Lo que hacia el programa hasta el 2026-09-05: seguir al sistema.</summary>
    ElDeWindows = 0,

    /// <summary>Claro siempre, este Windows como este.</summary>
    Claro = 1,

    /// <summary>Oscuro siempre, este Windows como este.</summary>
    Oscuro = 2,
}

/// <summary>
/// El vocabulario del tema: como se guarda en el archivo y como se lee en la pantalla.
/// </summary>
/// <remarks>
/// Esta separado de <see cref="PreferenciaDeTema"/> a proposito: aqui no se toca el disco, y
/// por eso se puede probar entero sin carpeta y sin ventana.
/// </remarks>
public static class TemasDeLaVentana
{
    /// <summary>Como se escribe en <c>preferencias.txt</c>; en minusculas y sin tildes.</summary>
    /// <remarks>
    /// Sin tildes NO por descuido: es una clave de archivo, no un rotulo, y va como van los
    /// identificadores de todo el proyecto. Lo que lee el dueno es <see cref="ComoSeLee"/>.
    /// </remarks>
    public static string ComoSeGuarda(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => "claro",
        TemaDeLaVentana.Oscuro => "oscuro",
        _ => "windows",
    };

    /// <summary>El rotulo que se lee en la pantalla, en español.</summary>
    public static string ComoSeLee(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => "Claro",
        TemaDeLaVentana.Oscuro => "Oscuro",
        _ => "El de Windows",
    };

    /// <summary>
    /// Traduce lo que habia escrito en el archivo; lo que no se entiende vuelve al de Windows.
    /// </summary>
    /// <remarks>
    /// No lanza nunca. Un archivo de preferencias roto no puede dejar al dueno con un icono
    /// que no abre: es la misma regla que ya siguen los argumentos de la linea de ordenes.
    /// </remarks>
    public static TemaDeLaVentana Interpretar(string? guardado) => guardado?.Trim().ToLowerInvariant() switch
    {
        "claro" => TemaDeLaVentana.Claro,
        "oscuro" => TemaDeLaVentana.Oscuro,
        _ => TemaDeLaVentana.ElDeWindows,
    };
}
