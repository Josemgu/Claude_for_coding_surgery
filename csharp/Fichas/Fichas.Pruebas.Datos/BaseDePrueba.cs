using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Una base de verdad, en un archivo temporal, que se borra sola al terminar la prueba.
/// </summary>
/// <remarks>
/// Va en archivo y NO en memoria a proposito: las migraciones reconstruyen tablas y
/// renombran, y una base en memoria no prueba que eso sobreviva a cerrar y volver a
/// abrir, que es lo que hace el programa de verdad.
/// </remarks>
public sealed class BaseDePrueba : IDisposable
{
    /// <summary>La carpeta temporal que contiene el <c>fichas.db</c>; se borra entera en <see cref="Dispose"/>.</summary>
    private readonly string _carpeta;

    /// <summary>Privado: solo las tres fábricas de arriba saben qué carpeta y qué conexión van juntas.</summary>
    /// <param name="carpeta">La carpeta temporal recién creada.</param>
    /// <param name="conexion">La conexión ya abierta sobre el archivo de esa carpeta.</param>
    private BaseDePrueba(string carpeta, SqliteConnection conexion)
    {
        _carpeta = carpeta;
        Conexion = conexion;
    }

    /// <summary>La conexion abierta sobre la base, con las claves foraneas encendidas.</summary>
    public SqliteConnection Conexion { get; }

    /// <summary>La ruta del archivo de base de datos.</summary>
    public string Ruta => Path.Combine(_carpeta, "fichas.db");

    /// <summary>Una base nueva con el esquema al dia aplicado.</summary>
    /// <remarks>
    /// Pide el esquema a <see cref="AplicadorDeEsquema"/> y NO llama a una migracion
    /// suelta. La leccion esta escrita en ARQUITECTURA (la ronda que costo 45 errores):
    /// una prueba que aplica una migracion a mano choca con la que el aplicador ya
    /// aplico sola.
    /// </remarks>
    public static BaseDePrueba Nueva()
    {
        var carpeta = CrearCarpetaTemporal();
        var conexion = FabricaDeConexiones.Abrir(Path.Combine(carpeta, "fichas.db"));
        AplicadorDeEsquema.Aplicar(conexion);
        return new BaseDePrueba(carpeta, conexion);
    }

    /// <summary>Una base vacia, SIN esquema, para probar una migracion desde su origen.</summary>
    public static BaseDePrueba SinEsquema()
    {
        var carpeta = CrearCarpetaTemporal();
        var conexion = FabricaDeConexiones.Abrir(Path.Combine(carpeta, "fichas.db"));
        return new BaseDePrueba(carpeta, conexion);
    }

    /// <summary>Una base construida sobre una copia del archivo que se le pase.</summary>
    /// <remarks>Copia el archivo: la base de origen no se toca nunca.</remarks>
    public static BaseDePrueba DesdeCopiaDe(string rutaDeOrigen)
    {
        var carpeta = CrearCarpetaTemporal();
        var destino = Path.Combine(carpeta, "fichas.db");
        File.Copy(rutaDeOrigen, destino);
        return new BaseDePrueba(carpeta, FabricaDeConexiones.Abrir(destino));
    }

    /// <summary>Cuenta las filas de una tabla; -1 si la tabla no existe.</summary>
    public long ContarFilasDe(string tabla)
    {
        using var existe = Conexion.CreateCommand();
        existe.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        existe.Parameters.AddWithValue("$tabla", tabla);
        if (Convert.ToInt64(existe.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            return -1;
        }

        using var orden = Conexion.CreateCommand();
        orden.CommandText = $"SELECT COUNT(*) FROM \"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Cierra la conexion y borra la carpeta temporal entera.</summary>
    public void Dispose()
    {
        Conexion.Close();
        Conexion.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Un archivo que Windows todavia tiene abierto no vale una prueba en rojo:
            // lo que se estaba midiendo ya se midio. La carpeta temporal la limpia el
            // sistema.
        }
        catch (UnauthorizedAccessException)
        {
            // Por el mismo motivo.
        }
    }

    /// <summary>Una carpeta nueva bajo la temporal del sistema, con un GUID en el nombre para que dos pruebas en paralelo no choquen.</summary>
    /// <returns>La ruta de la carpeta, ya creada.</returns>
    private static string CrearCarpetaTemporal()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }
}
