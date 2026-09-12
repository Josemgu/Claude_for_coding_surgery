namespace Fichas.App.Correccion;

/// <summary>
/// Lo que se lee cuando el escaneo no se puede pintar.
/// </summary>
/// <remarks>
/// Va como clase sin ventana por lo mismo que <see cref="TextoDelAcuse"/>: la frase que ve
/// Miguel se comprueba en una prueba.
/// <para>
/// ⛔ <b>La regla que manda aqui:</b> ningun fallo se queda callado. Sale del defecto que QA
/// midio en la pantalla de Importar —manejador <c>async void</c> que se traga la excepcion,
/// botones que parecen muertos y ni una linea que lo explique—. Aqui, si rasterizar falla,
/// se ve en una linea que paso: el tipo del fallo y su mensaje, sin adornos.
/// </para>
/// <para>
/// ⚠️ Y NO se dice que la lectura de PDF este pendiente. Lo decia hasta hoy —«la lectura de
/// PDF es la fase C3»— y esa fase esta cerrada: <c>Fichas.Lectura</c> rasteriza con PDFium.
/// Un texto que miente sobre lo que el programa sabe hacer manda a buscar el fallo donde no
/// esta.
/// </para>
/// </remarks>
public static class TextoDelVisor
{
    /// <summary>Lo que se pinta sobre el papel en blanco cuando el caso no trae escaneo.</summary>
    public const string SinEscaneo =
        "Este caso no guarda ninguna ruta de escaneo. Los campos se corrigen igual, tecleando.";

    /// <summary>La linea del pie cuando el escaneo no se pudo pintar; nunca vacia.</summary>
    /// <remarks>
    /// Se recorta al mismo largo que el acuse y se le quitan los saltos de renglon: es UNA
    /// linea en el pie, y un mensaje de tres parrafos ahi no se lee, se ignora.
    /// </remarks>
    /// <param name="hoja">Que hoja se estaba pintando, base 1.</param>
    /// <param name="fallo">El fallo tal como llego; su tipo importa tanto como su texto.</param>
    public static string LineaDeFallo(int hoja, Exception fallo)
    {
        ArgumentNullException.ThrowIfNull(fallo);

        var cabecera = $"No se pudo pintar la hoja {hoja}: {fallo.GetType().Name}";
        var mensaje = EnUnaLinea(fallo.Message);
        var entera = mensaje.Length == 0 ? cabecera : $"{cabecera}: {mensaje}";
        return Recortar(entera, TextoDelAcuse.LargoMaximoDeLaLinea);
    }

    /// <summary>Lo que se pinta sobre el papel en blanco cuando la hoja no se pudo abrir.</summary>
    /// <remarks>
    /// Aqui si cabe la frase larga, porque va sobre la hoja vacia y no en el pie. Y dice que
    /// los campos se corrigen igual: negarse a pintar seria impedir por no poder.
    /// </remarks>
    /// <param name="hoja">Que hoja se pedia, base 1.</param>
    /// <param name="ruta">La ruta del escaneo, para que se sepa cual.</param>
    public static string HojaQueNoSePudoAbrir(int hoja, string ruta)
        => $"No se pudo abrir la hoja {hoja} de «{ruta}». Los campos se corrigen igual, sin la imagen "
           + "al lado; lo que no se puede es iluminar donde estaba cada dato en el papel.";

    /// <summary>Deja el texto en un solo renglon, sin dobles espacios.</summary>
    /// <param name="texto">El mensaje del fallo, que puede traer saltos de renglon; nulo devuelve vacio.</param>
    private static string EnUnaLinea(string? texto)
        => string.IsNullOrWhiteSpace(texto)
            ? string.Empty
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Recorta con puntos suspensivos; lo que se corta es la cola del mensaje.</summary>
    /// <param name="texto">La linea entera.</param>
    /// <param name="largoMaximo">Cuantos caracteres caben, contando los puntos suspensivos.</param>
    private static string Recortar(string texto, int largoMaximo)
        => texto.Length <= largoMaximo ? texto : string.Concat(texto.AsSpan(0, largoMaximo - 1), "…");
}
