using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// Crea lo que falte, aplica las migraciones pendientes y registra cada una.
/// </summary>
/// <remarks>
/// <para>
/// Portado de <c>datos/esquema.py</c> (<c>aplicar_esquema</c>). Es idempotente por las
/// tres vias: <c>CREATE ... IF NOT EXISTS</c> para el DDL, <c>INSERT OR IGNORE</c>
/// para la fila de version, y la comparacion con la version que la base ya tiene para
/// no repetir una migracion. Arrancar dos veces sobre una base ya migrada no duplica
/// nada ni toca los datos.
/// </para>
/// <para>
/// Una base recien creada tambien pasa por las migraciones, aunque su tabla naciera
/// hace un segundo. Es A PROPOSITO: un camino de codigo que solo se ejecuta sobre
/// bases viejas es un camino que nadie prueba nunca, hasta el dia que hace falta.
/// </para>
/// <para>
/// ⚠️ <b>Una prueba pide el esquema a <see cref="Aplicar"/>; no llama a una migracion
/// suelta.</b> La leccion esta escrita en ARQUITECTURA y costo 45 errores: al
/// registrar la 14, el aplicador empezo a aplicarla sola y el arranque de una prueba
/// la volvia a aplicar a mano, con «duplicate column name».
/// </para>
/// </remarks>
public static class AplicadorDeEsquema
{
    /// <summary>La version con la que nacio la base.</summary>
    public const int VersionInicial = 1;

    /// <summary>
    /// La version a la que llega una base despues de aplicar todo lo pendiente.
    /// </summary>
    /// <remarks>
    /// Se DERIVA del catalogo en vez de escribirse a mano, por lo que dice
    /// <see cref="CatalogoDeMigraciones"/>: un numero copiado se queda desfasado y la
    /// migracion nueva no se aplica nunca.
    /// </remarks>
    public static int VersionAlDia { get; } =
        CatalogoDeMigraciones.Todas.Count > 0
            ? CatalogoDeMigraciones.Todas[^1].Version
            : VersionInicial;

    /// <summary>
    /// Deja la base en la version al dia y devuelve el numero al que llego.
    /// </summary>
    public static int Aplicar(SqliteConnection conexion) => AplicarHasta(conexion, VersionAlDia);

    /// <summary>
    /// Deja la base en la version que se pida, sin pasar de ahi.
    /// </summary>
    /// <remarks>
    /// El tope existe para que las pruebas puedan construir una base EXACTAMENTE como
    /// la que ya existe en la maquina del dueno y comprobar que la migracion la
    /// convierte. Sin el, solo se podria probar el camino facil —una base nueva— y no
    /// el que importa.
    /// </remarks>
    /// <param name="conexion">La conexion sobre la que se aplica.</param>
    /// <param name="versionDestino">La ultima version que se quiere aplicar.</param>
    public static int AplicarHasta(SqliteConnection conexion, int versionDestino)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        CrearLasTablasDeLaVersionInicial(conexion);
        AnotarVersion(conexion, VersionInicial, DdlDeLaVersionInicial.Descripcion);

        foreach (var migracion in CatalogoDeMigraciones.Todas)
        {
            if (migracion.Version > versionDestino)
            {
                break;
            }

            if (migracion.Version > (VersionDeLaBase(conexion) ?? 0))
            {
                migracion.Aplicar(conexion);
                AnotarVersion(conexion, migracion.Version, migracion.Descripcion);
            }
        }

        return VersionDeLaBase(conexion) ?? VersionInicial;
    }

    /// <summary>
    /// Crea las tablas tal como nacian en la version 1, y nada mas.
    /// </summary>
    /// <remarks>
    /// ⚠️ Deja la base en un esquema ANTIGUO a proposito. Quien quiera una base
    /// utilizable llama a <see cref="Aplicar"/>, no a esto.
    /// </remarks>
    public static void CrearLasTablasDeLaVersionInicial(SqliteConnection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        using var orden = conexion.CreateCommand();
        orden.CommandText = DdlDeLaVersionInicial.Guion;
        orden.ExecuteNonQuery();
    }

    /// <summary>La version vigente de la base, o nula si todavia no se aplico ninguna.</summary>
    public static int? VersionDeLaBase(SqliteConnection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT MAX(version) FROM version_esquema";
        var maximo = orden.ExecuteScalar();

        return maximo is null or DBNull
            ? null
            : Convert.ToInt32(maximo, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Deja constancia de que una version quedo aplicada, y cuando.
    /// </summary>
    /// <remarks>
    /// Con <c>INSERT OR IGNORE</c>: arrancar dos veces sobre una base ya creada deja el
    /// conteo igual y no duplica nada (idempotencia, FASE 1 crit. 8).
    /// </remarks>
    private static void AnotarVersion(SqliteConnection conexion, int version, string descripcion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT OR IGNORE INTO version_esquema (version, aplicada_en, descripcion) " +
            "VALUES ($version, $aplicada, $descripcion)";
        orden.Parameters.AddWithValue("$version", version);
        orden.Parameters.AddWithValue("$aplicada", MarcaDeTiempo());
        orden.Parameters.AddWithValue("$descripcion", descripcion);
        orden.ExecuteNonQuery();
    }

    /// <summary>Ahora, en el formato ISO-8601 extendido que usa todo el esquema.</summary>
    /// <remarks>
    /// Hora local y no UTC, igual que el Python: quien lee esta base mira su reloj de
    /// pared, y una marca en UTC le saldria corrida cuatro horas sin decirselo.
    /// </remarks>
    public static string MarcaDeTiempo()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
