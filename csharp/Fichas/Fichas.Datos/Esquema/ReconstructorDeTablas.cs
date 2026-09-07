using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// El procedimiento oficial de SQLite para reconstruir una tabla, en un solo sitio.
/// </summary>
/// <remarks>
/// <para>
/// Portado de <c>datos/reconstruccion_de_tablas.py</c>. Cambiar un <c>CHECK</c> o
/// quitar un <c>NOT NULL</c> en SQLite no se puede hacer con <c>ALTER TABLE</c>: hay
/// que reconstruir la tabla entera. La documentacion oficial
/// (<see href="https://www.sqlite.org/lang_altertable.html"/>, seccion «Making Other
/// Kinds Of Table Schema Changes», consultada 2026-09-04) publica un procedimiento de
/// 12 pasos.
/// </para>
/// <para>
/// Vive en un solo sitio y no copiado en cada migracion que lo necesita: tres
/// migraciones lo usan —la 2, la 7, la 12 y la 15— y con copias, el dia que una se
/// corrija las otras se quedan mal.
/// </para>
/// </remarks>
internal static class ReconstructorDeTablas
{
    /// <summary>
    /// Aplica un procedimiento de reconstruccion completo, o no aplica ninguno.
    /// </summary>
    /// <remarks>
    /// Las claves foraneas se apagan y se vuelven a encender FUERA de la transaccion,
    /// que es donde el pragma surte efecto (la documentacion oficial lo dice: dentro
    /// de una transaccion, <c>PRAGMA foreign_keys</c> no hace nada). Si algo falla, la
    /// transaccion se deshace y las claves vuelven a encenderse igual: una base a
    /// medio migrar con las claves apagadas seria peor que no haber empezado.
    /// </remarks>
    /// <param name="conexion">La conexion sobre la que se reconstruye.</param>
    /// <param name="rehacer">Los pasos 4 a 8: crear, copiar, tirar y renombrar.</param>
    internal static void Reconstruir(SqliteConnection conexion, Action<SqliteConnection> rehacer)
    {
        Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        try
        {
            using var transaccion = conexion.BeginTransaction();
            try
            {
                rehacer(conexion);
                ComprobarQueNoQuedaronHuerfanos(conexion);
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }

            transaccion.Commit();
        }
        finally
        {
            Ejecutar(conexion, "PRAGMA foreign_keys = ON");
            ComprobarQueSeVolvieronAEncender(conexion);
        }
    }

    /// <summary>Paso 10 del procedimiento oficial, antes de confirmar la transaccion.</summary>
    private static void ComprobarQueNoQuedaronHuerfanos(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_key_check";
        using var lector = orden.ExecuteReader();

        var huerfanos = 0;
        while (lector.Read())
        {
            huerfanos++;
        }

        if (huerfanos > 0)
        {
            throw new ErrorDeMigracion(
                "La migracion dejo filas huerfanas y se deshace entera: " +
                $"PRAGMA foreign_key_check devolvio {huerfanos} fila(s). " +
                "La base se queda como estaba.");
        }
    }

    /// <summary>
    /// Que las claves foraneas volvieron a quedar encendidas al terminar.
    /// </summary>
    private static void ComprobarQueSeVolvieronAEncender(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_keys";
        var encendidas = Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        if (encendidas != 1)
        {
            throw new ErrorDeMigracion(
                "Las claves foraneas no se pudieron volver a encender despues de la " +
                $"migracion (PRAGMA foreign_keys devolvio {encendidas}).");
        }
    }

    /// <summary>Ejecuta una instruccion suelta sobre la conexion.</summary>
    internal static void Ejecutar(SqliteConnection conexion, string instruccion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = instruccion;
        orden.ExecuteNonQuery();
    }
}

/// <summary>Una migracion no se pudo aplicar, o se aplico y no quedo integra.</summary>
/// <remarks>
/// Esta SI es una excepcion, y no un <c>Aviso</c>. El requisito 9 —«avisar, nunca
/// impedir»— habla de un valor raro de un campo, que se guarda y se senala. Una
/// migracion a medias es otra cosa: no hay pantalla que pueda seguir trabajando sobre
/// una base cuyo esquema no se sabe cual es.
/// </remarks>
public sealed class ErrorDeMigracion : InvalidOperationException
{
    /// <summary>Con el motivo escrito en espanol.</summary>
    public ErrorDeMigracion(string mensaje) : base(mensaje)
    {
    }

    /// <summary>Con el motivo y la causa de debajo.</summary>
    public ErrorDeMigracion(string mensaje, Exception causa) : base(mensaje, causa)
    {
    }

    /// <summary>Sin motivo; existe para cumplir el convenio de las excepciones.</summary>
    public ErrorDeMigracion()
    {
    }
}
