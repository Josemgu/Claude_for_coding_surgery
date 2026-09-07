using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Conexion;

/// <summary>
/// Apertura de conexiones a la base. Toda conexion enciende las claves foraneas.
/// </summary>
/// <remarks>
/// <para>
/// Portado de <c>datos/conexion.py</c>. La documentacion oficial de SQLite
/// (<see href="https://www.sqlite.org/foreignkeys.html"/>, consultada 2026-09-02)
/// dice, literal: «Foreign key constraints are disabled by default (for backwards
/// compatibility), so must be enabled separately for each database connection.»
/// </para>
/// <para>
/// Sin ese pragma cada <c>REFERENCES</c> del esquema es un comentario decorativo y
/// los <c>RESTRICT</c> no impiden nada: el motor dejaria borrar un caso con personas
/// dentro sin decir una palabra.
/// </para>
/// <para>
/// El modo de diario se queda en el de por defecto: NO se activa WAL
/// (docs/ARQUITECTURA.md §1.8). La carpeta de datos puede caer bajo OneDrive y los
/// archivos satelite <c>-wal</c>/<c>-shm</c> se sincronizarian desacompasados
/// respecto del <c>.db</c>, que es una via de corrupcion que el modo por defecto no
/// tiene.
/// </para>
/// </remarks>
public static class FabricaDeConexiones
{
    /// <summary>
    /// Abre la base para leer y escribir, creandola si no existe, y comprueba que las
    /// claves foraneas quedaron encendidas.
    /// </summary>
    /// <param name="rutaDeLaBase">La ruta del archivo <c>.db</c>.</param>
    /// <exception cref="ErrorDeConexion">
    /// Si las claves foraneas no quedaron encendidas. Es una de las excepciones que el
    /// requisito 9 SI reserva: no es un valor raro de un campo, es que la base entera
    /// no esta defendida y ninguna pantalla puede decidir nada sobre eso.
    /// </exception>
    public static SqliteConnection Abrir(string rutaDeLaBase)
        => AbrirCon(rutaDeLaBase, SqliteOpenMode.ReadWriteCreate);

    /// <summary>
    /// Abre la base para SOLO LEER. No la crea y no deja escribir.
    /// </summary>
    /// <remarks>
    /// Sirve para mirar una base sin arriesgarse a tocarla: un respaldo, o la base del
    /// dueno mientras se diagnostica algo. Una escritura sobre esta conexion falla en
    /// el motor, no por convenio.
    /// </remarks>
    public static SqliteConnection AbrirSoloLectura(string rutaDeLaBase)
        => AbrirCon(rutaDeLaBase, SqliteOpenMode.ReadOnly);

    /// <summary>El trabajo comun de las dos aperturas, con el modo por parametro.</summary>
    private static SqliteConnection AbrirCon(string rutaDeLaBase, SqliteOpenMode modo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeLaBase);

        var cadena = new SqliteConnectionStringBuilder
        {
            DataSource = rutaDeLaBase,
            Mode = modo,
            // Sin agrupacion de conexiones: el archivo tiene que quedar libre en
            // cuanto se cierra la conexion, y con la agrupacion encendida Windows lo
            // retiene. Eso rompe borrar la base en las pruebas y, peor, deja el
            // archivo tomado si el programa se reinicia.
            Pooling = false,
        }.ToString();

        var conexion = new SqliteConnection(cadena);
        conexion.Open();

        EncenderLasClavesForaneas(conexion);
        ComprobarQueQuedaronEncendidas(conexion);

        return conexion;
    }

    /// <summary>Enciende las claves foraneas ANTES de la primera consulta.</summary>
    private static void EncenderLasClavesForaneas(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_keys = ON";
        orden.ExecuteNonQuery();
    }

    /// <summary>
    /// Comprueba que el motor las encendio de verdad, en vez de darlo por hecho.
    /// </summary>
    /// <remarks>
    /// Se pregunta en vez de confiar porque un <c>PRAGMA</c> que el motor no reconoce
    /// no da error: se ignora en silencio. Sin esta comprobacion, un cambio de
    /// proveedor dejaria las foraneas apagadas y nadie se enteraria hasta que faltara
    /// una fila.
    /// </remarks>
    private static void ComprobarQueQuedaronEncendidas(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_keys";
        var encendidas = Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        if (encendidas != 1)
        {
            conexion.Close();
            conexion.Dispose();
            throw new ErrorDeConexion(
                "Las claves foraneas no quedaron encendidas en esta conexion " +
                $"(PRAGMA foreign_keys devolvio {encendidas}). Sin ellas el motor " +
                "dejaria borrar un caso con personas dentro.");
        }
    }
}

/// <summary>La conexion se abrio pero no quedo en el estado que el esquema exige.</summary>
public sealed class ErrorDeConexion : InvalidOperationException
{
    /// <summary>Con el motivo escrito en espanol.</summary>
    public ErrorDeConexion(string mensaje) : base(mensaje)
    {
    }

    /// <summary>Con el motivo y la causa de debajo.</summary>
    public ErrorDeConexion(string mensaje, Exception causa) : base(mensaje, causa)
    {
    }

    /// <summary>Sin motivo; existe para cumplir el convenio de las excepciones.</summary>
    public ErrorDeConexion()
    {
    }
}
