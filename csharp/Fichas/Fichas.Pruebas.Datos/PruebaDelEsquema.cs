using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// El recuento del esquema contra <c>docs/ARQUITECTURA.md</c>, que es la especificacion.
/// </summary>
/// <remarks>
/// ⚠️ Los numeros de esta clase NO se leen del codigo que prueban: se copian de la
/// medicion que el Python de este mismo arbol produce (`datos/esquema.py` +
/// `datos/migraciones*.py`) y que ARQUITECTURA §2 declara. Escribir la cifra mirando
/// lo que devuelve el DDL de C# convertiria la prueba en un espejo: pasaria en verde
/// estando mal. Por eso van a mano, tabla por tabla, y suman 111.
///
/// ⚠️ Las 104 de la medicion del Python, mas las CUATRO de la migracion 18 y las TRES
/// de la 19, que el
/// Python no tiene y no tendra: los dos motivos de `casos` y el rol y la categoria de
/// `companeros` (FASE C10, ADR-0005 §3.3 y §4.3).
/// </remarks>
[TestClass]
public sealed class PruebaDelEsquema
{
    /// <summary>Las nueve tablas que una base al dia tiene (ARQUITECTURA §2).</summary>
    private static readonly string[] TablasEsperadas =
    [
        "asignaciones", "casos", "companeros", "contactos", "documentos_ilegibles",
        "filas_descartadas", "personas", "procedencia_campo", "version_esquema",
    ];

    /// <summary>
    /// Cuantas columnas tiene cada tabla, una a una (ARQUITECTURA §2.1 a §2.9).
    /// Suman 111: 22+28+7+6+12+16+8+9+3.
    /// </summary>
    private static readonly (string Tabla, int Columnas)[] ColumnasEsperadas =
    [
        ("asignaciones", 6),
        ("casos", 22),
        ("companeros", 7),
        ("contactos", 12),
        ("documentos_ilegibles", 8),
        ("filas_descartadas", 9),
        ("personas", 28),
        ("procedencia_campo", 16),
        ("version_esquema", 3),
    ];

    /// <summary>Los ocho indices propios que declara ARQUITECTURA §6.</summary>
    private static readonly string[] IndicesEsperados =
    [
        "idx_asignacion_viva", "idx_asignaciones_companero", "idx_casos_numero",
        "idx_casos_viaje_activos", "idx_descartadas_companero", "idx_ilegibles_ruta",
        "idx_personas_caso", "idx_procedencia_registro",
    ];

    /// <summary>Vigila que una base nueva tiene exactamente las nueve tablas, por nombre.</summary>
    [TestMethod]
    public void LaBaseAlDiaTieneLasNueveTablasDeLaArquitectura()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var tablas = LeerNombres(
            baseDePrueba.Conexion,
            "SELECT name FROM sqlite_master WHERE type = 'table' " +
            "AND name NOT LIKE 'sqlite_%' ORDER BY name");

        CollectionAssert.AreEqual(
            TablasEsperadas,
            tablas,
            "Las tablas de la base no son las nueve que declara ARQUITECTURA §2. " +
            $"Se encontraron {tablas.Length}: {string.Join(", ", tablas)}");
    }

    /// <summary>Vigila que cada tabla tiene el número de columnas de ARQUITECTURA §2, una a una.</summary>
    [TestMethod]
    public void CadaTablaTieneLasColumnasQueDeclaraLaArquitectura()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        foreach (var (tabla, esperadas) in ColumnasEsperadas)
        {
            var reales = ContarColumnas(baseDePrueba.Conexion, tabla);
            Assert.AreEqual(
                esperadas,
                reales,
                $"La tabla '{tabla}' tiene {reales} columnas y ARQUITECTURA §2 declara {esperadas}.");
        }
    }

    /// <summary>Vigila que las columnas de las nueve tablas suman 111 (el nombre dice 108: quedó viejo cuando entraron las de la 18 y la 19; se apunta en la entrega).</summary>
    [TestMethod]
    public void ElEsquemaSumaLasCientoOchoColumnasDeLaArquitectura()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var total = 0;
        foreach (var (tabla, _) in ColumnasEsperadas)
        {
            total += ContarColumnas(baseDePrueba.Conexion, tabla);
        }

        Assert.AreEqual(
            111,
            total,
            "El esquema no suma las 111 columnas del esquema al dia " +
            "(22+28+7+6+12+16+8+9+3): las 104 de ARQUITECTURA §2, mas las cuatro de la " +
            "migracion 18, mas las tres de la 19.");
    }

    /// <summary>Vigila que los índices propios son exactamente los ocho de ARQUITECTURA §6.</summary>
    [TestMethod]
    public void LaBaseAlDiaTieneLosOchoIndicesPropiosDeLaArquitectura()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var indices = LeerNombres(
            baseDePrueba.Conexion,
            "SELECT name FROM sqlite_master WHERE type = 'index' " +
            "AND name NOT LIKE 'sqlite_%' ORDER BY name");

        CollectionAssert.AreEqual(
            IndicesEsperados,
            indices,
            $"Los indices propios no son los ocho de ARQUITECTURA §6. " +
            $"Se encontraron {indices.Length}: {string.Join(", ", indices)}");
    }

    /// <summary>Vigila que <c>PRAGMA foreign_keys</c> devuelve 1 en una conexión recién abierta.</summary>
    [TestMethod]
    public void LasClavesForaneasQuedanEncendidasEnCadaConexion()
    {
        // ARQUITECTURA §1.5: sin este pragma cada REFERENCES es un comentario
        // decorativo y los RESTRICT no impiden nada.
        using var baseDePrueba = BaseDePrueba.Nueva();

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_keys";

        Assert.AreEqual(
            1L,
            Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture),
            "Las claves foraneas no quedaron encendidas en la conexion.");
    }

    /// <summary>Vigila que <c>PRAGMA journal_mode</c> no es WAL (ARQUITECTURA §1.8, por OneDrive).</summary>
    [TestMethod]
    public void ElDiarioSeQuedaEnElDeDefectoYNoEnWal()
    {
        // ARQUITECTURA §1.8: no se activa WAL porque la carpeta de datos puede caer
        // bajo OneDrive y los archivos satelite -wal/-shm se sincronizarian
        // desacompasados respecto del .db.
        using var baseDePrueba = BaseDePrueba.Nueva();

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "PRAGMA journal_mode";
        var modo = Convert.ToString(orden.ExecuteScalar())!;

        Assert.AreNotEqual(
            "wal",
            modo.ToLowerInvariant(),
            "El diario quedo en WAL, y ARQUITECTURA §1.8 lo prohibe.");
    }

    /// <summary>Cuenta las columnas de una tabla preguntandoselo al motor.</summary>
    private static int ContarColumnas(SqliteConnection conexion, string tabla)
    {
        // El nombre de tabla NO viene de fuera: sale de la lista literal de esta
        // misma clase. Aun asi va por `quote` del propio motor y no por concatenacion
        // a pelo, que es la costumbre que ARQUITECTURA §1.7 exige mantener.
        using var orden = conexion.CreateCommand();
        orden.CommandText = $"PRAGMA table_info({Citar(tabla)})";
        using var lector = orden.ExecuteReader();

        var columnas = 0;
        while (lector.Read())
        {
            columnas++;
        }

        return columnas;
    }

    /// <summary>Lee una columna de nombres en una lista ordenada.</summary>
    private static string[] LeerNombres(SqliteConnection conexion, string consulta)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = consulta;
        using var lector = orden.ExecuteReader();

        var nombres = new List<string>();
        while (lector.Read())
        {
            nombres.Add(lector.GetString(0));
        }

        return [.. nombres];
    }

    /// <summary>Entrecomilla un identificador al modo de SQLite, doblando la comilla.</summary>
    private static string Citar(string identificador)
        => "\"" + identificador.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
