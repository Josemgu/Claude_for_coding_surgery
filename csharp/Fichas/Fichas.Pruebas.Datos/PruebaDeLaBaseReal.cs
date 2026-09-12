using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que una base de verdad, de las que ya existen en la maquina del dueno, migra entera.
/// </summary>
/// <remarks>
/// ⚠️ Ninguna prueba de esta clase escribe en la base del dueno: <see cref="BaseDePrueba.DesdeCopiaDe"/>
/// copia el archivo a una carpeta temporal y trabaja sobre la copia.
///
/// ⚠️ El respaldo NO se guarda en el repositorio: lleva datos de personas reales. Si
/// no esta en la maquina, la prueba lo dice y construye una base equivalente con el
/// propio codigo. Una base generada prueba menos —solo que el codigo es coherente
/// consigo mismo— y por eso se distingue en el mensaje en vez de disimularlo.
/// </remarks>
[TestClass]
public sealed class PruebaDeLaBaseReal
{
    /// <summary>
    /// Los respaldos que el pase nombraba, por orden. El pase citaba
    /// `respaldo-base-20260903-2100`, que NO existe en esta maquina (medido el
    /// 2026-09-04); los dos que si existen estan en la version 8.
    /// </summary>
    private static readonly string[] RespaldosCandidatos =
    [
        @"C:\Users\josem\Fichas-entrega\respaldo-base-20260903-2100\fichas.db",
        @"C:\Users\josem\Fichas-entrega\respaldo-base-20260903-0815\fichas.db",
        @"C:\Users\josem\Fichas-entrega\respaldo-base-20260903\fichas.db",
    ];

    /// <summary>Vigila que una copia de un respaldo real llega a la versión al día con el mismo número de filas por tabla; sin respaldo en la máquina, lo dice y prueba con una base generada.</summary>
    [TestMethod]
    public void UnRespaldoDeVerdadMigraAlDiaSinPerderNiUnaFila()
    {
        var respaldo = RespaldosCandidatos.FirstOrDefault(File.Exists);

        using var baseDePrueba = respaldo is not null
            ? BaseDePrueba.DesdeCopiaDe(respaldo)
            : BaseDePrueba.SinEsquema();

        if (respaldo is null)
        {
            // No hay respaldo en esta maquina: se construye uno equivalente y se dice.
            AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, 11);
            SembrarComoElRespaldo(baseDePrueba.Conexion);
        }

        var versionDePartida = AplicadorDeEsquema.VersionDeLaBase(baseDePrueba.Conexion);
        var antes = ContarPorTabla(baseDePrueba.Conexion);

        Assert.IsTrue(
            versionDePartida >= 1 && versionDePartida < AplicadorDeEsquema.VersionAlDia,
            $"La base de partida dice estar en la version {versionDePartida}, y esta " +
            $"prueba necesita una anterior a la {AplicadorDeEsquema.VersionAlDia} para " +
            "que migrar signifique algo.");

        var version = AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var despues = ContarPorTabla(baseDePrueba.Conexion);

        // El conteo se ESCRIBE, no solo se compara: es el numero que se pega en la
        // entrega, y un «paso en verde» sin la cifra delante no es una medicion.
        Console.WriteLine(
            "== Migracion de una base real: version {0} -> {1} ==",
            versionDePartida,
            version);
        Console.WriteLine(
            "   Origen: {0}", respaldo ?? "(GENERADA: no habia respaldo en esta maquina)");
        Console.WriteLine("   {0,-24} {1,6} {2,7}", "TABLA", "ANTES", "DESPUES");
        foreach (var (tabla, filasAntes) in antes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine("   {0,-24} {1,6} {2,7}", tabla, filasAntes, despues[tabla]);
        }

        Console.WriteLine(
            "   {0,-24} {1,6} {2,7}", "TOTAL", antes.Values.Sum(), despues.Values.Sum());

        Assert.AreEqual(
            AplicadorDeEsquema.VersionAlDia,
            version,
            $"La base de la version {versionDePartida} no llego a la " +
            $"{AplicadorDeEsquema.VersionAlDia}.");

        foreach (var (tabla, filasAntes) in antes)
        {
            Assert.AreEqual(
                filasAntes,
                despues[tabla],
                $"La tabla '{tabla}' tenia {filasAntes} filas antes de migrar de la " +
                $"{versionDePartida} a la {AplicadorDeEsquema.VersionAlDia} y tiene " +
                $"{despues[tabla]} despues. " +
                (respaldo is null
                    ? "(Base GENERADA: no habia respaldo en esta maquina.)"
                    : $"(Respaldo real: {respaldo})"));
        }

        // Y el conteo total, que es el numero que se pega en la entrega.
        Assert.AreEqual(
            antes.Values.Sum(),
            despues.Values.Sum(),
            "El total de filas de la base cambio al migrar.");
    }

    /// <summary>Vigila que un respaldo migrado suma las 111 columnas del esquema al día (el nombre dice 108: quedó viejo cuando entraron las de la 18 y la 19; se apunta en la entrega).</summary>
    [TestMethod]
    public void LasColumnasDeUnRespaldoMigradoSonLasCientoOcho()
    {
        var respaldo = RespaldosCandidatos.FirstOrDefault(File.Exists);

        using var baseDePrueba = respaldo is not null
            ? BaseDePrueba.DesdeCopiaDe(respaldo)
            : BaseDePrueba.SinEsquema();

        if (respaldo is null)
        {
            AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, 11);
        }

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        // Se cuentan TODAS las tablas, `version_esquema` incluida: sus 3 columnas son
        // parte de las 111. (`ContarPorTabla` la deja fuera a proposito, porque para
        // comparar FILAS antes y despues de migrar no sirve —crece al migrar—, pero
        // para contar COLUMNAS si cuenta.)
        var total = LeerNombresDeTablas(baseDePrueba.Conexion)
            .Sum(tabla => ContarColumnas(baseDePrueba.Conexion, tabla));

        Assert.AreEqual(
            111,
            total,
            "Una base migrada no tiene las 111 columnas del esquema al dia: las 104 de " +
            "ARQUITECTURA §2, mas las cuatro de la migracion 18 y las tres de la 19.");
    }

    /// <summary>Deja la base con los mismos conteos que traian los respaldos medidos.</summary>
    /// <remarks>
    /// Los conteos salen de mirar los dos respaldos reales el 2026-09-04:
    /// casos 2, companeros 1, documentos_ilegibles 2, procedencia_campo 8, personas 0.
    /// </remarks>
    private static void SembrarComoElRespaldo(SqliteConnection conexion)
    {
        var companero = PruebaDeMigraciones.InsertarCompanero(conexion, "Miguel");
        var primero = PruebaDeMigraciones.InsertarCaso(conexion, "CASP2609");
        PruebaDeMigraciones.InsertarCaso(conexion, "BALC2609");

        for (var campo = 0; campo < 8; campo++)
        {
            using var orden = conexion.CreateCommand();
            orden.CommandText =
                "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen) " +
                "VALUES ('casos', $registro, $campo, 'ocr')";
            orden.Parameters.AddWithValue("$registro", primero);
            orden.Parameters.AddWithValue("$campo", "campo_" + campo.ToString(System.Globalization.CultureInfo.InvariantCulture));
            orden.ExecuteNonQuery();
        }

        for (var renglon = 0; renglon < 2; renglon++)
        {
            using var orden = conexion.CreateCommand();
            orden.CommandText =
                "INSERT INTO documentos_ilegibles (ruta_pdf, motivo, registrado_en) " +
                "VALUES ($ruta, 'sin_texto', '2026-09-03 07:24:00')";
            orden.Parameters.AddWithValue("$ruta", @"C:\escaneos\hoja" + renglon.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".pdf");
            orden.ExecuteNonQuery();
        }

        Assert.AreNotEqual(0, companero, "El companero sembrado no se guardo.");
    }

    /// <summary>Los nombres de las tablas propias de la base, ordenados.</summary>
    private static List<string> LeerNombresDeTablas(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' " +
            "AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var lector = orden.ExecuteReader();

        var tablas = new List<string>();
        while (lector.Read())
        {
            tablas.Add(lector.GetString(0));
        }

        return tablas;
    }

    /// <summary>Cuantas filas tiene cada tabla de la base, por su nombre.</summary>
    private static Dictionary<string, long> ContarPorTabla(SqliteConnection conexion)
    {
        var conteo = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var tabla in LeerNombresDeTablas(conexion))
        {
            conteo[tabla] = 0;
        }

        foreach (var tabla in conteo.Keys.ToList())
        {
            using var orden = conexion.CreateCommand();
            orden.CommandText = $"SELECT COUNT(*) FROM \"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            conteo[tabla] = Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        // `version_esquema` crece a proposito al migrar: no es un dato del usuario,
        // es el registro de la propia migracion. Se saca de la comparacion.
        conteo.Remove("version_esquema");
        return conteo;
    }

    /// <summary>Cuántas columnas tiene la tabla, preguntándoselo al motor con <c>PRAGMA table_info</c>.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="tabla">El nombre de la tabla; sale de las listas literales de esta clase, nunca de fuera.</param>
    private static int ContarColumnas(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = $"PRAGMA table_info(\"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\")";
        using var lector = orden.ExecuteReader();

        var columnas = 0;
        while (lector.Read())
        {
            columnas++;
        }

        return columnas;
    }
}
