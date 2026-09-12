using System.Globalization;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que una base acabe con el MISMO esquema venga de donde venga.
/// </summary>
/// <remarks>
/// <para>
/// El defecto que estas pruebas cazan, medido el 2026-09-04 sobre una copia de
/// <c>C:\Users\josem\Documents\Fichas\fichas.db</c>: la migracion 12 del C# reconstruye
/// <c>casos</c> SIN el <c>CHECK (numero_caso GLOB ...)</c> —decision del dueno en
/// DECISIONES.md, «El CHECK del numero de caso se quita en el programa nuevo»— pero la
/// del Python la reconstruyo CONSERVANDOLO. La base del dueno ya paso por la 12 con el
/// Python, asi que la reconstruccion del C# no vuelve a ejecutarse nunca sobre ella y el
/// CHECK se queda dentro: dos bases al dia con esquema distinto.
/// </para>
/// <para>
/// ⚠️ <b>La prueba se deriva del criterio, no del codigo.</b> El DDL que se compara no
/// se pide a ninguna constante de <c>Fichas.Datos</c>: se leen los dos de
/// <c>sqlite_master</c> y se exige que sean iguales. Y lo que de verdad importa —que un
/// numero mal leido ENTRE en vez de rechazarse— se comprueba insertandolo por las dos
/// rutas, no mirando el texto del CHECK.
/// </para>
/// <para>
/// ⚠️ Ninguna prueba de esta clase escribe en la base del dueno:
/// <see cref="BaseDePrueba.DesdeCopiaDe"/> copia el archivo a una carpeta temporal.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaConvergenciaDelEsquema
{
    /// <summary>La base con la que trabaja el dueno todos los dias.</summary>
    private const string RutaDeLaBaseDelDueno = @"C:\Users\josem\Documents\Fichas\fichas.db";

    /// <summary>Un numero de caso que el OCR lee mal: una letra O donde va un cero.</summary>
    /// <remarks>
    /// Es el ejemplo que el supervisor midio con <c>GLOB</c> el 2026-09-04: este NO entra
    /// con el CHECK puesto, y por eso sirve de sonda. El dueno decidio que se guarde y se
    /// senale en la pantalla; un CHECK del motor no senala, rechaza, y lo rechazado se
    /// pierde.
    /// </remarks>
    private const string NumeroQueElOcrLeeMal = "CASP26O9";

    /// <summary>
    /// La tabla <c>casos</c> tal como la deja la migracion 12 DEL PYTHON, con su CHECK.
    /// </summary>
    /// <remarks>
    /// No es una suposicion: es el DDL que devolvio <c>sqlite_master</c> sobre una copia
    /// de la base del dueno el 2026-09-04, menos las columnas que le anadieron despues
    /// las migraciones 13 y 14 por <c>ALTER TABLE</c>. Existe para que esta prueba no
    /// dependa de que el archivo del dueno este en la maquina.
    /// </remarks>
    private const string TablaDeLaVersion12DelPython = """
        CREATE TABLE casos_python_12 (
            id                   INTEGER PRIMARY KEY,
            numero_caso          TEXT
                CHECK (numero_caso IS NULL
                       OR numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]'),
            unidad_numero        TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje          TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual       INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado            INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado      TEXT,
            ruta_pdf             TEXT,
            creado_en            TEXT    NOT NULL,
            estado_recomendacion TEXT,
            pagina_pdf           INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            unidad_nombre        TEXT,
            templo_nombre        TEXT,

            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL))
        )
        """;

    /// <summary>
    /// Dado que una base la migro el Python y otra nacio con el C#, cuando las dos
    /// llegan a la version al dia, entonces <c>casos</c> tiene el mismo DDL.
    /// </summary>
    [TestMethod]
    public void UnaBaseNuevaYUnaMigradaPorElPythonAcabanConElMismoDdlDeCasos()
    {
        using var nueva = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.Aplicar(nueva.Conexion);

        using var delPython = BaseDePrueba.SinEsquema();
        MontarComoLaDejoElPython(delPython.Conexion);
        AplicadorDeEsquema.Aplicar(delPython.Conexion);

        var ddlDeLaNueva = DdlDe(nueva.Conexion, "casos");
        var ddlDeLaDelPython = DdlDe(delPython.Conexion, "casos");

        Console.WriteLine("== DDL de `casos` en una base NUEVA, creada por el C# ==");
        Console.WriteLine(ddlDeLaNueva);
        Console.WriteLine();
        Console.WriteLine("== DDL de `casos` en una base que el PYTHON dejo en la 12 ==");
        Console.WriteLine(ddlDeLaDelPython);

        Assert.AreEqual(
            ddlDeLaNueva,
            ddlDeLaDelPython,
            "Dos bases en la version al dia tienen `casos` con esquema distinto: de donde "
            + "viene la base cambia lo que la base acepta.");
    }

    /// <summary>
    /// Dado un numero de caso que el OCR leyo mal, cuando se guarda por cualquiera de las
    /// dos rutas, entonces entra y no se rechaza.
    /// </summary>
    /// <remarks>
    /// Este es el criterio de verdad. El DDL es la forma; esto es la consecuencia:
    /// DECISIONES.md 2026-09-04 dice que el numero se guarda como venga y se senala.
    /// </remarks>
    [TestMethod]
    public void UnNumeroMalLeidoEntraVengaLaBaseDeDondeVenga()
    {
        using var nueva = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.Aplicar(nueva.Conexion);
        Assert.AreNotEqual(
            0,
            PruebaDeMigraciones.InsertarCaso(nueva.Conexion, NumeroQueElOcrLeeMal),
            $"Una base nueva rechazo el numero «{NumeroQueElOcrLeeMal}».");

        using var delPython = BaseDePrueba.SinEsquema();
        MontarComoLaDejoElPython(delPython.Conexion);
        AplicadorDeEsquema.Aplicar(delPython.Conexion);

        // Si el CHECK sigue dentro, esta linea levanta SqliteException y el documento
        // entero se pierde: es exactamente el dano que la decision del dueno evita.
        Assert.AreNotEqual(
            0,
            PruebaDeMigraciones.InsertarCaso(delPython.Conexion, NumeroQueElOcrLeeMal),
            $"Una base que venia del Python rechazo el numero «{NumeroQueElOcrLeeMal}».");
    }

    /// <summary>
    /// Dada una base que el Python dejo en la 12 con filas dentro, cuando se migra al
    /// dia, entonces no se pierde ninguna.
    /// </summary>
    [TestMethod]
    public void MigrarUnaBaseDelPythonNoPierdeNiUnaFila()
    {
        using var delPython = BaseDePrueba.SinEsquema();
        MontarComoLaDejoElPython(delPython.Conexion);

        var antes = ContarPorTabla(delPython.Conexion);
        var version = AplicadorDeEsquema.Aplicar(delPython.Conexion);
        var despues = ContarPorTabla(delPython.Conexion);

        EscribirLosConteos("una base que el Python dejo en la 12", 12, version, antes, despues);

        Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, version, "No llego a la version al dia.");
        foreach (var (tabla, filasAntes) in antes)
        {
            Assert.AreEqual(
                filasAntes,
                despues[tabla],
                $"La tabla '{tabla}' tenia {filasAntes} filas y ahora tiene {despues[tabla]}.");
        }
    }

    /// <summary>
    /// Dada LA base del dueno, cuando se migra una copia suya, entonces acaba con el
    /// mismo DDL que una base nueva y sin perder ninguna fila.
    /// </summary>
    /// <remarks>
    /// Si el archivo no esta en esta maquina la prueba lo DICE y queda sin concluir, en
    /// vez de pasar en verde sin haber mirado nada.
    /// </remarks>
    [TestMethod]
    public void LaBaseRealDelDuenoAcabaConElMismoEsquemaQueUnaNueva()
    {
        if (!File.Exists(RutaDeLaBaseDelDueno))
        {
            Assert.Inconclusive(
                $"No hay ninguna base en «{RutaDeLaBaseDelDueno}»: esta prueba no midio nada. "
                + "La convergencia sin el archivo del dueno la mide "
                + nameof(UnaBaseNuevaYUnaMigradaPorElPythonAcabanConElMismoDdlDeCasos) + ".");
            return;
        }

        using var copia = BaseDePrueba.DesdeCopiaDe(RutaDeLaBaseDelDueno);
        using var nueva = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.Aplicar(nueva.Conexion);

        var versionDePartida = AplicadorDeEsquema.VersionDeLaBase(copia.Conexion);
        var ddlAntes = DdlDe(copia.Conexion, "casos");
        var antes = ContarPorTabla(copia.Conexion);

        var version = AplicadorDeEsquema.Aplicar(copia.Conexion);

        var ddlDespues = DdlDe(copia.Conexion, "casos");
        var despues = ContarPorTabla(copia.Conexion);

        Console.WriteLine("== DDL de `casos` en la base del dueno ANTES de migrar (version {0}) ==", versionDePartida);
        Console.WriteLine(ddlAntes);
        Console.WriteLine();
        Console.WriteLine("== DDL de `casos` DESPUES de migrar (version {0}) ==", version);
        Console.WriteLine(ddlDespues);
        EscribirLosConteos(
            "la base real del dueno", versionDePartida ?? 0, version, antes, despues);

        Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, version, "La base del dueno no llego a la version al dia.");
        Assert.AreEqual(
            DdlDe(nueva.Conexion, "casos"),
            ddlDespues,
            "La base del dueno migrada NO tiene el mismo `casos` que una base nueva.");

        foreach (var (tabla, filasAntes) in antes)
        {
            Assert.AreEqual(
                filasAntes,
                despues[tabla],
                $"La tabla '{tabla}' de la base del dueno tenia {filasAntes} filas y ahora tiene {despues[tabla]}.");
        }
    }

    /// <summary>
    /// Deja la conexion como la dejaba el Python: en la version 12, con su CHECK dentro.
    /// </summary>
    /// <remarks>
    /// Se llega a la 11 con el propio aplicador y solo la reconstruccion de la 12 se hace
    /// a mano, porque es LA que difiere. Sembrar antes de reconstruir es a proposito: una
    /// reconstruccion sobre una tabla vacia no prueba que copie las filas.
    /// </remarks>
    private static void MontarComoLaDejoElPython(SqliteConnection conexion)
    {
        AplicadorDeEsquema.AplicarHasta(conexion, 11);

        PruebaDeMigraciones.InsertarCompanero(conexion, "Sandy");
        var caso = PruebaDeMigraciones.InsertarCaso(conexion, "CASP2609");
        PruebaDeMigraciones.InsertarPersona(conexion, caso, "055-1111-3853", "Fulano");
        // Con digitos y no con la letra final: en la version 11 el CHECK de `mrn` todavia
        // es el viejo, y la letra no entra hasta la 15.
        PruebaDeMigraciones.InsertarPersona(conexion, caso, "066-2222-1330", "Mengano");

        Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        try
        {
            Ejecutar(conexion, TablaDeLaVersion12DelPython);
            Ejecutar(
                conexion,
                """
                INSERT INTO casos_python_12
                    (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
                     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
                     unidad_nombre, templo_nombre)
                SELECT
                     id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
                     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
                     unidad_nombre, templo_nombre
                FROM casos
                """);
            Ejecutar(conexion, "DROP TABLE casos");
            Ejecutar(conexion, "ALTER TABLE casos_python_12 RENAME TO casos");
            Ejecutar(
                conexion,
                "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0");
            Ejecutar(conexion, "CREATE INDEX idx_casos_numero ON casos (numero_caso)");
        }
        finally
        {
            Ejecutar(conexion, "PRAGMA foreign_keys = ON");
        }

        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT OR IGNORE INTO version_esquema (version, aplicada_en, descripcion) "
            + "VALUES (12, '2026-09-03 22:28:24', "
            + "'''casos.numero_caso'' deja de ser UNICO: el numero identifica una unidad y un mes, "
            + "no una familia, y dos documentos distintos lo comparten por construccion.')";
        orden.ExecuteNonQuery();
    }

    /// <summary>El DDL que el motor guarda para una tabla, tal cual.</summary>
    private static string DdlDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        orden.Parameters.AddWithValue("$tabla", tabla);
        return orden.ExecuteScalar() as string ?? $"(no hay ninguna tabla '{tabla}')";
    }

    /// <summary>Cuantas filas tiene cada tabla, sin contar el registro de las migraciones.</summary>
    private static Dictionary<string, long> ContarPorTabla(SqliteConnection conexion)
    {
        var conteo = new Dictionary<string, long>(StringComparer.Ordinal);

        using (var orden = conexion.CreateCommand())
        {
            orden.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' "
                + "AND name NOT LIKE 'sqlite_%' AND name <> 'version_esquema' ORDER BY name";
            using var lector = orden.ExecuteReader();
            while (lector.Read())
            {
                conteo[lector.GetString(0)] = 0;
            }
        }

        foreach (var tabla in conteo.Keys.ToList())
        {
            using var orden = conexion.CreateCommand();
            orden.CommandText = $"SELECT COUNT(*) FROM \"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            conteo[tabla] = Convert.ToInt64(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        return conteo;
    }

    /// <summary>Escribe la tabla de conteos: un verde sin la cifra delante no es una medicion.</summary>
    private static void EscribirLosConteos(
        string deQueBase,
        int versionDePartida,
        int versionFinal,
        Dictionary<string, long> antes,
        Dictionary<string, long> despues)
    {
        Console.WriteLine(
            "== Filas de {0}: version {1} -> {2} ==", deQueBase, versionDePartida, versionFinal);
        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TABLA", "ANTES", "DESPUES");
        foreach (var (tabla, filas) in antes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine("   {0,-24} {1,6} {2,8}", tabla, filas, despues[tabla]);
        }

        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TOTAL", antes.Values.Sum(), despues.Values.Sum());
    }

    /// <summary>Ejecuta una instrucción suelta; el texto sale de las constantes de esta clase.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="instruccion">La instrucción SQL sin parámetros.</param>
    private static void Ejecutar(SqliteConnection conexion, string instruccion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = instruccion;
        orden.ExecuteNonQuery();
    }
}
