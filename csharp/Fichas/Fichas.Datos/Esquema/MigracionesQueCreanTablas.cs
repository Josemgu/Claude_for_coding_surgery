using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// Las dos migraciones que anaden una tabla entera: la 8 y la 11.
/// </summary>
/// <remarks>
/// Las dos existen por el mismo motivo, dicho por el dueno de dos maneras: lo que no
/// se pudo procesar tiene que dejar un renglon que se pueda volver a mirar manana. Un
/// cuadro de dialogo se cierra con Aceptar y no deja nada; con 500 documentos no hay
/// forma de leerlo al vuelo.
/// </remarks>
internal static class MigracionesQueCreanTablas
{
    // ==================================================================
    // VERSION 8 — lo que no se pudo leer deja su renglon.
    // ==================================================================

    /// <summary>La frase que <c>version_esquema</c> guarda para la versión 8: la tabla de los PDF o páginas que no se pudieron leer.</summary>
    /// <remarks>Historia de la base del dueño: se lee, no se edita. Cambiarla no cambiaría lo que las bases ya migradas tienen escrito.</remarks>
    internal const string DescripcionDeLaVersion8 =
        "'documentos_ilegibles': cada PDF o pagina que no se pudo leer deja su " +
        "renglon con la ruta, la pagina y el motivo, y se puede consultar despues.";

    /// <summary>
    /// La tabla de lo que no se pudo leer.
    /// </summary>
    /// <remarks>
    /// <c>caso_id</c> apunta al caso que SI se llego a guardar de esa pagina, cuando
    /// lo hay: es lo que convierte el renglon en algo accionable, porque desde la
    /// lista se sabe si hay a donde ir o si de esa pagina no quedo nada.
    ///
    /// SIN CHECK con la lista de motivos, por el mismo criterio con el que
    /// <c>estado_recomendacion</c> no lo lleva: a la lista le pueden faltar motivos
    /// que todavia no se han visto, y un CHECK incompleto rechazaria manana un motivo
    /// verdadero. Entonces el documento ilegible se perderia por culpa de la tabla que
    /// existe para no perderlo.
    /// </remarks>
    private const string TablaDeLaVersion8 = """
        CREATE TABLE IF NOT EXISTS documentos_ilegibles (
            id             INTEGER PRIMARY KEY,
            ruta_pdf       TEXT    NOT NULL,
            pagina_pdf     INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            motivo         TEXT    NOT NULL,
            detalle        TEXT,
            lineas_leidas  INTEGER CHECK (lineas_leidas IS NULL OR lineas_leidas >= 0),
            caso_id        INTEGER
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            registrado_en  TEXT    NOT NULL
        )
        """;

    /// <summary>Crea la tabla de los documentos que no se pudieron leer.</summary>
    /// <param name="conexion">La conexión abierta sobre la base que se migra; la migración no la abre ni la cierra.</param>
    internal static void AVersion8(SqliteConnection conexion)
    {
        Aplicar(conexion, 8, () =>
        {
            Ejecutar(conexion, TablaDeLaVersion8);
            Ejecutar(conexion,
                "CREATE INDEX IF NOT EXISTS idx_ilegibles_ruta " +
                "ON documentos_ilegibles (ruta_pdf)");
        });
    }

    // ==================================================================
    // VERSION 11 — lo que volvio del Excel y no entro.
    // ==================================================================

    /// <summary>La frase que <c>version_esquema</c> guarda para la versión 11: la tabla de las filas del Excel de un compañero que no entraron.</summary>
    /// <remarks>Historia de la base del dueño: se lee, no se edita. Cambiarla no cambiaría lo que las bases ya migradas tienen escrito.</remarks>
    internal const string DescripcionDeLaVersion11 =
        "'filas_descartadas': lo que volvio en el Excel de un companero y NO entro en " +
        "la base deja su renglon, que se queda al cerrar el programa.";

    /// <summary>
    /// La tabla de las filas del Excel que no casaron con nadie.
    /// </summary>
    /// <remarks>
    /// El motivo se guarda como FRASE y no como codigo, al reves que en
    /// <c>documentos_ilegibles</c>, y la diferencia esta razonada: alli hay motivos
    /// cerrados que se pueden contar —«de 500 documentos, cuantos no se abrieron»—;
    /// aqui el motivo lleva dentro el numero de la fila del Excel con la que choco y
    /// la causa concreta, que es lo que hace falta para ir a mirar ESA fila. Un codigo
    /// perderia justo eso.
    ///
    /// Ni <c>numero_caso</c> ni <c>mrn</c> llevan CHECK: lo que venia escrito puede ser
    /// justo lo que estaba mal, y una tabla que existe para guardar lo que no entro no
    /// puede rechazar lo que no entro.
    /// </remarks>
    private const string TablaDeLaVersion11 = """
        CREATE TABLE IF NOT EXISTS filas_descartadas (
            id            INTEGER PRIMARY KEY,
            companero_id  INTEGER NOT NULL
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            ruta_excel    TEXT,
            fila_excel    INTEGER CHECK (fila_excel IS NULL OR fila_excel >= 1),
            numero_caso   TEXT,
            mrn           TEXT,
            nombre        TEXT,
            motivo        TEXT    NOT NULL,
            registrado_en TEXT    NOT NULL
        )
        """;

    /// <summary>Crea la tabla de las filas que volvieron y no entraron.</summary>
    /// <param name="conexion">La conexión abierta sobre la base que se migra; la migración no la abre ni la cierra.</param>
    internal static void AVersion11(SqliteConnection conexion)
    {
        Aplicar(conexion, 11, () =>
        {
            Ejecutar(conexion, TablaDeLaVersion11);
            Ejecutar(conexion,
                "CREATE INDEX IF NOT EXISTS idx_descartadas_companero " +
                "ON filas_descartadas (companero_id)");
        });
    }

    /// <summary>Ejecuta los pasos de una migracion traduciendo el fallo del motor.</summary>
    /// <param name="conexion">La conexión abierta sobre la base; solo se comprueba que no sea nula.</param>
    /// <param name="version">El número de la migración, para nombrarla en el error.</param>
    /// <param name="pasos">Las instrucciones de la migración, ya cerradas sobre la conexión.</param>
    /// <exception cref="ErrorDeMigracion">Envuelve cualquier <see cref="SqliteException"/> con la versión y el mensaje del motor en español.</exception>
    /// <remarks>Copia literal del <c>Aplicar</c> de las otras dos clases de migraciones; se repite a propósito para que cada archivo se lea solo. Sin transacción: aquí no importa porque <c>CREATE TABLE IF NOT EXISTS</c> y <c>CREATE INDEX IF NOT EXISTS</c> se pueden repetir.</remarks>
    private static void Aplicar(SqliteConnection conexion, int version, Action pasos)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        try
        {
            pasos();
        }
        catch (SqliteException causa)
        {
            throw new ErrorDeMigracion(
                $"No se pudo migrar la base a la version {version}: {causa.Message}. " +
                "La base se queda en la version anterior y no se perdio ningun dato.",
                causa);
        }
    }

    /// <summary>Atajo a <see cref="ReconstructorDeTablas.Ejecutar"/> para que cada migración se lea sin el nombre de la clase delante.</summary>
    /// <param name="conexion">La conexión abierta sobre la base.</param>
    /// <param name="instruccion">Una sola instrucción SQL sin parámetros.</param>
    private static void Ejecutar(SqliteConnection conexion, string instruccion)
        => ReconstructorDeTablas.Ejecutar(conexion, instruccion);
}
