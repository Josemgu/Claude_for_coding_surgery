namespace Fichas.App.Actualizacion;

/// <summary>
/// La clave de solo lectura que el dueño pega en la carpeta de datos para que el programa
/// pueda ver el repositorio privado.
/// </summary>
/// <remarks>
/// <para>DECISIONES.md, 2026-09-11: la crea el dueño en GitHub (fine-grained, solo
/// «Contents: read» sobre ese repositorio) y la pega <b>él</b> en
/// <c>Documentos\Fichas\clave-de-actualizacion.txt</c>. Nadie del proyecto la ve ni la
/// escribe: este archivo no la crea, no la pide y no la imprime.</para>
///
/// <para>Va en la carpeta de DATOS y no en la del programa: una actualización vacía la del
/// programa (<c>Fichas.iss</c>, [InstallDelete]) y se llevaría la clave con ella.</para>
///
/// <para>⛔ Lo que devuelve <see cref="Leer"/> va a UNA cabecera HTTP y a ningún otro sitio:
/// ni al registro, ni a un aviso, ni a una excepción. Hay una prueba que lo vigila.</para>
/// </remarks>
public static class ClaveDeActualizacion
{
    /// <summary>El nombre del archivo, como lo fija la decisión del 2026-09-11.</summary>
    public const string NombreDelArchivo = "clave-de-actualizacion.txt";

    /// <summary>La ruta completa del archivo en esa carpeta de datos, exista o no.</summary>
    /// <param name="carpetaDeDatos">La carpeta de datos (<c>--carpeta-de-datos</c>, o <c>Documentos\Fichas</c>).</param>
    public static string RutaEn(string carpetaDeDatos) => Path.Combine(carpetaDeDatos, NombreDelArchivo);

    /// <summary>
    /// La clave recortada de espacios y saltos, o nula si el archivo no existe, está vacío o
    /// no se puede leer.
    /// </summary>
    /// <remarks>
    /// Solo cuenta la primera línea con algo: si el dueño deja una nota debajo, la nota no se
    /// manda a GitHub. Nunca lanza: sin clave se consulta sin cabecera, y si el repositorio
    /// es privado GitHub contestará 404 y la franja dirá dónde pegarla.
    /// </remarks>
    /// <param name="carpetaDeDatos">La carpeta de datos donde se busca el archivo.</param>
    public static string? Leer(string carpetaDeDatos)
    {
        var ruta = RutaEn(carpetaDeDatos);
        try
        {
            if (!File.Exists(ruta)) return null;
            foreach (var linea in File.ReadLines(ruta))
            {
                var recortada = linea.Trim();
                if (recortada.Length > 0) return recortada;
            }

            return null;
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
