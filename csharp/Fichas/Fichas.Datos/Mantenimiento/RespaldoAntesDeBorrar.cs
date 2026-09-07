using System.Globalization;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Conexion;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Mantenimiento;

/// <summary>
/// La copia que se hace ANTES de borrar, con la fecha en el nombre.
/// </summary>
/// <remarks>
/// <para>
/// Es hermana de <see cref="Esquema.RespaldoAntesDeMigrar"/> y NO la misma clase, por una
/// diferencia que no es de estilo: aquella copia una base que todavia NO se ha abierto,
/// con <c>File.Copy</c>, y esta copia una base ABIERTA, con la conexion viva del programa
/// delante. Copiar el archivo de una base abierta puede llevarse un estado a medias; la
/// documentacion de SQLite (<see href="https://www.sqlite.org/backup.html"/>, consultada el
/// 2026-09-05) dice que para eso esta la API de respaldo en linea, que es lo que
/// <c>BackupDatabase</c> expone. Compartir codigo entre las dos habria significado usar el
/// mecanismo equivocado en una de ellas.
/// </para>
/// <para>
/// Lo que si se copia es la FORMA del nombre —<c>fichas-antes-de-borrar-20260905-143012.db</c>,
/// al lado de la base— porque es donde el dueno la va a buscar, y porque una marca distinta
/// deja claro de un vistazo si la copia era de migrar o de borrar.
/// </para>
/// </remarks>
public static class RespaldoAntesDeBorrar
{
    /// <summary>El trozo que marca en el nombre que la copia es de antes de borrar.</summary>
    public const string MarcaDeLaCopia = "antes-de-borrar";

    /// <summary>
    /// Cuantas veces el tamano de la base tiene que haber libre para atreverse a borrar.
    /// </summary>
    /// <remarks>
    /// Dos, por el mismo motivo que en <see cref="Esquema.RespaldoAntesDeMigrar"/>: una es
    /// la copia y la otra el diario que SQLite escribe mientras corre el borrado, que puede
    /// llegar a pesar lo que pesa lo borrado. Con sitio justo para la copia, el borrado se
    /// quedaria sin espacio a mitad.
    /// </remarks>
    public const int VecesElTamanoQueHacenFalta = 2;

    /// <summary>
    /// Copia la base abierta a un archivo nuevo con la fecha en el nombre.
    /// </summary>
    /// <param name="conexion">La conexion viva sobre la base que se va a tocar.</param>
    /// <param name="cuando">La hora que va en el nombre; por defecto, ahora.</param>
    /// <param name="espacioLibre">
    /// Cuantos bytes quedan en la unidad; por defecto se le pregunta al sistema. Se puede
    /// pasar para poder probar el disco lleno sin llenar uno.
    /// </param>
    /// <returns>Donde quedo la copia, o el aviso de por que no se pudo hacer.</returns>
    public static ResultadoDeLaCopia Hacer(
        SqliteConnection conexion, DateTime? cuando = null, Func<string, long>? espacioLibre = null)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var rutaDeLaBase = RutaDeLaBaseAbierta(conexion);
        if (rutaDeLaBase is null)
        {
            return ResultadoDeLaCopia.NoSePudo(Aviso.Problema(
                "No se pudo hacer la copia previa: la base no está en un archivo.",
                string.Empty,
                "El programa no borra nada sin copia. Cierre y vuelva a abrir el programa."));
        }

        var falta = QueFaltaDeSitio(rutaDeLaBase, espacioLibre ?? EspacioLibreDeLaUnidad);
        if (falta is not null) return ResultadoDeLaCopia.NoSePudo(falta);

        var destino = SitioLibreParaLaCopia(rutaDeLaBase, cuando ?? DateTime.Now);
        try
        {
            using var copia = FabricaDeConexiones.Abrir(destino);
            conexion.BackupDatabase(copia);
        }
        catch (Exception causa) when (
            causa is SqliteException or IOException or UnauthorizedAccessException or ErrorDeConexion)
        {
            return ResultadoDeLaCopia.NoSePudo(Aviso.Problema(
                "No se pudo hacer la copia previa, así que NO se borró nada.",
                string.Empty,
                $"Se intentaba copiar en «{destino}». Motivo: {causa.Message}"));
        }

        return new ResultadoDeLaCopia(destino, null);
    }

    /// <summary>«fichas-antes-de-borrar-20260905-143012.db», al lado de la base.</summary>
    public static string NombreDeLaCopia(string rutaDeLaBase, DateTime cuando, int vuelta = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeLaBase);

        var carpeta = Path.GetDirectoryName(Path.GetFullPath(rutaDeLaBase)) ?? string.Empty;
        var nombre = Path.GetFileNameWithoutExtension(rutaDeLaBase);
        var extension = Path.GetExtension(rutaDeLaBase);
        var marca = cuando.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var repeticion = vuelta > 1 ? "-" + vuelta.ToString(CultureInfo.InvariantCulture) : string.Empty;

        return Path.Combine(carpeta, $"{nombre}-{MarcaDeLaCopia}-{marca}{repeticion}{extension}");
    }

    /// <summary>
    /// Que archivo tiene abierto esta conexion, preguntandoselo al motor.
    /// </summary>
    /// <remarks>
    /// Con <c>PRAGMA database_list</c> y no con la propiedad del proveedor: lo que importa
    /// es el archivo que el MOTOR tiene abierto como <c>main</c>. Una base en memoria
    /// devuelve una ruta vacia, y entonces no hay nada que copiar y no se borra.
    /// </remarks>
    private static string? RutaDeLaBaseAbierta(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA database_list";
        using var lector = orden.ExecuteReader();
        while (lector.Read())
        {
            if (!string.Equals(lector.GetString(1), "main", StringComparison.Ordinal)) continue;
            var archivo = lector.IsDBNull(2) ? string.Empty : lector.GetString(2);
            return string.IsNullOrWhiteSpace(archivo) ? null : archivo;
        }

        return null;
    }

    /// <summary>El aviso de que no cabe la copia, o nulo si cabe.</summary>
    private static Aviso? QueFaltaDeSitio(string rutaDeLaBase, Func<string, long> espacioLibre)
    {
        long tamano;
        try
        {
            tamano = new FileInfo(rutaDeLaBase).Length;
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            // Si no se puede ni mirar el tamano, no se bloquea por eso: la copia se intenta
            // igual y, si no cabe, fallara al copiar con su motivo de verdad.
            return null;
        }

        var hacenFalta = tamano * VecesElTamanoQueHacenFalta;
        if (espacioLibre(rutaDeLaBase) >= hacenFalta) return null;

        return Aviso.Problema(
            "No hay sitio en el disco para copiar la base, así que NO se borró nada.",
            string.Empty,
            $"Hacen falta {EnMiB(hacenFalta)} libres y no los hay. Libere espacio y vuelva a intentarlo.");
    }

    /// <summary>Cuantos bytes quedan libres en la unidad donde vive esa ruta.</summary>
    private static long EspacioLibreDeLaUnidad(string ruta)
    {
        try
        {
            var raiz = Path.GetPathRoot(Path.GetFullPath(ruta));
            return string.IsNullOrEmpty(raiz) ? long.MaxValue : new DriveInfo(raiz).AvailableFreeSpace;
        }
        catch (Exception fallo) when (fallo is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return long.MaxValue;
        }
    }

    /// <summary>Un nombre libre: sobrescribir seria perder justo el respaldo recien hecho.</summary>
    private static string SitioLibreParaLaCopia(string rutaDeLaBase, DateTime cuando)
    {
        var candidato = NombreDeLaCopia(rutaDeLaBase, cuando);
        var vuelta = 2;
        while (File.Exists(candidato))
        {
            candidato = NombreDeLaCopia(rutaDeLaBase, cuando, vuelta);
            vuelta++;
        }

        return candidato;
    }

    private static string EnMiB(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
}

/// <summary>Donde quedo la copia, o por que no se pudo hacer. Nunca las dos cosas.</summary>
/// <param name="Ruta">El archivo de la copia, o nulo si no se hizo.</param>
/// <param name="Fallo">El motivo, o nulo si salio bien.</param>
public sealed record ResultadoDeLaCopia(string? Ruta, Aviso? Fallo)
{
    /// <summary>Si hay copia y por tanto se puede seguir.</summary>
    public bool HayCopia => Ruta is not null;

    /// <summary>La copia que no se pudo hacer, con su motivo.</summary>
    public static ResultadoDeLaCopia NoSePudo(Aviso fallo) => new(null, fallo);
}
