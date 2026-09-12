using System.Globalization;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// La migracion 19: quien contesto las seis preguntas, cuando y desde donde (FASE C19).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Las pruebas salen de los criterios C19-1 a C19-4 y C19-8 de PENDIENTES.md, no del
/// codigo que prueban.</b> Ninguna asercion pide un nombre de columna a
/// <c>Fichas.Datos</c> para compararlo consigo mismo: los tres nombres se escriben aqui a
/// mano, copiados del bloque SQL del ADR-0006 §2.4.
/// </para>
/// <para>
/// <b>Que guardan estas tres columnas y que NO.</b> Guardan la firma de las SEIS preguntas
/// del sistema del lider —quien las contesto, cuando y por que via—. NO guardan el estado:
/// el estado de una persona se DERIVA de sus seis <c>paso_*</c> cada vez que se pregunta
/// (ADR-0006 §2.4), porque un veredicto guardado puede contradecir a su evidencia y uno
/// derivado no puede.
/// </para>
/// <para>
/// ⛔ <b>Y no son la firma de campos.</b> <c>procedencia_campo.verificado</c> es de Miguel,
/// campo por campo, y nunca automatica (regla permanente 5). Contestar las seis preguntas
/// del sistema del lider es otra cosa; la prueba
/// <see cref="LaDiecinueveNoFirmaNiUnCampo"/> lo mide contando filas.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaMigracion19
{
    /// <summary>La version que esta fase engancha, escrita a mano y no derivada del catalogo.</summary>
    private const int VersionDeEstaFase = 19;

    /// <summary>Las tres columnas, copiadas del bloque SQL del ADR-0006 §2.4.</summary>
    private static readonly string[] LasTresColumnas = ["pasos_por", "pasos_en", "pasos_origen"];

    // ═══════════════════════ C19-1: engancha al final ═══════════════════════

    /// <summary>
    /// Dado el catalogo de migraciones, cuando se lee la version al dia, entonces es la 19
    /// y sale DERIVADA de la lista, sin ningun numero escrito a mano en el aplicador.
    /// </summary>
    [TestMethod]
    public void LaDiecinueveEsLaUltimaDelCatalogoYLaVersionAlDiaSubeSola()
    {
        Assert.AreEqual(
            VersionDeEstaFase,
            CatalogoDeMigraciones.Todas[^1].Version,
            "La 19 no es la ultima del catalogo, y el orden ES el orden en que se aplican.");

        Assert.AreEqual(
            VersionDeEstaFase,
            AplicadorDeEsquema.VersionAlDia,
            "`VersionAlDia` no subio sola al enganchar la 19: si esta escrita a mano, la " +
            "migracion nueva no se aplica NUNCA y nadie se entera.");
    }

    // ═══════════════ C19-2: tres ADD COLUMN y CERO reconstrucciones ═══════════════

    /// <summary>
    /// Dada una base parada en la 18, cuando se aplica la 19, entonces <c>personas</c>
    /// conserva su <c>rootpage</c>: la tabla NO se reconstruyo, se le anadieron columnas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Mide la propiedad, no el reloj.</b> «Cero reconstrucciones» se puede afirmar
    /// mirando un cronometro —una reconstruccion tarda mas— o mirando el dato: una tabla
    /// reconstruida es OTRA tabla y estrena pagina raiz, porque reconstruir es crear,
    /// copiar, borrar y renombrar. El <c>rootpage</c> de <c>sqlite_master</c> lo dice sin
    /// depender de lo cargada que este la maquina, que es lo que hace que una prueba de
    /// tiempo se ponga roja sola cuando corre junto a otras.
    /// </para>
    /// <para>
    /// Que <c>ALTER TABLE ADD COLUMN</c> no reescribe la tabla lo dice la documentacion
    /// oficial (<see href="https://sqlite.org/lang_altertable.html"/>, consultada el
    /// 2026-09-05): «the execution time ... is independent of the amount of data in the
    /// table». Esta prueba comprueba que la 19 usa ese camino y no otro.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LaDiecinueveNoReconstruyePersonasYSeMidePorSuPaginaRaiz()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);

        var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-385A", "Elena");

        var raizAntes = PaginaRaizDe(baseDePrueba.Conexion, "personas");
        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var raizDespues = PaginaRaizDe(baseDePrueba.Conexion, "personas");

        Console.WriteLine(
            "== Migracion 19 · pagina raiz de 'personas': antes {0}, despues {1} ==",
            raizAntes,
            raizDespues);

        Assert.AreEqual(
            raizAntes,
            raizDespues,
            $"'personas' cambio de pagina raiz ({raizAntes} -> {raizDespues}): la 19 la " +
            "reconstruyo en vez de anadirle columnas, y el ADR-0006 §2.4 pide tres " +
            "ADD COLUMN y cero reconstrucciones.");
    }

    /// <summary>
    /// Dada una base al dia, cuando se leen las columnas de <c>personas</c>, entonces las
    /// tres de la 19 estan AL FINAL y en su orden, que es el que exige su propio CHECK.
    /// </summary>
    /// <remarks>
    /// El orden no es adorno: el CHECK de <c>pasos_en</c> nombra a <c>pasos_por</c>, asi que
    /// esa columna tiene que existir ya cuando se anade. Es el mismo cuidado que la version
    /// 6 tuvo con <c>pudo_viajar</c> y <c>motivo_no_viajo</c>.
    /// </remarks>
    [TestMethod]
    public void LasTresColumnasEstanAlFinalDePersonasYEnSuOrden()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var columnas = ColumnasDe(baseDePrueba.Conexion, "personas");
        var lasUltimasTres = columnas.TakeLast(3).ToArray();

        Console.WriteLine("== Las tres ultimas columnas de 'personas': {0} ==", string.Join(", ", lasUltimasTres));

        CollectionAssert.AreEqual(
            LasTresColumnas,
            lasUltimasTres,
            "Las tres ultimas columnas de 'personas' no son las de la 19 en su orden.\n" +
            $"  Se esperaban: {string.Join(", ", LasTresColumnas)}\n" +
            $"  Se encontraron: {string.Join(", ", lasUltimasTres)}");
    }

    /// <summary>
    /// Dada una persona firmada por un companero, cuando se intenta borrar ese companero,
    /// entonces el motor lo rechaza: la firma no se queda apuntando a nadie.
    /// </summary>
    /// <remarks>
    /// Es el <c>ON DELETE RESTRICT</c> del ADR-0006 §2.4, y es la misma regla que la 14 le
    /// dio a <c>casos.estado_marcado_por</c>. Una firma que apunta a un companero borrado
    /// es una respuesta de la que ya no se sabe de quien fiarse.
    /// </remarks>
    [TestMethod]
    public void UnCompaneroQueFirmoLosPasosNoSePuedeBorrar()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var companero = PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        var persona = PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-385A", "Elena");

        Ejecutar(
            baseDePrueba.Conexion,
            "UPDATE personas SET pasos_por = $por, pasos_en = '2026-09-05 11:00:00', " +
            "pasos_origen = 'a mano en la pantalla' WHERE id = " + persona.ToString(CultureInfo.InvariantCulture),
            ("$por", companero));

        var fallo = Assert.ThrowsExactly<SqliteException>(
            () => Ejecutar(
                baseDePrueba.Conexion,
                "DELETE FROM companeros WHERE id = " + companero.ToString(CultureInfo.InvariantCulture)),
            "Se borro un companero que habia firmado las seis preguntas de una persona.");

        Console.WriteLine("== El motor rechaza borrar al que firmo: {0} ==", fallo.Message);
    }

    /// <summary>
    /// Dada la columna <c>pasos_en</c>, cuando se escribe una fecha sin decir quien, entonces
    /// el motor la rechaza; y las dos juntas entran.
    /// </summary>
    /// <remarks>
    /// Es la misma forma que la 14 le dio a <c>casos.estado_marcado_en</c>: una fecha sin
    /// nombre no dice de quien fiarse, y un nombre sin fecha no dice si la respuesta es de
    /// antes o de despues de la del companero. O las dos, o ninguna.
    /// </remarks>
    [TestMethod]
    public void LaFechaSinNombreNoEntraYLasDosJuntasSi()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var companero = PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        var persona = PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-385A", "Elena");
        var donde = " WHERE id = " + persona.ToString(CultureInfo.InvariantCulture);

        Assert.ThrowsExactly<SqliteException>(
            () => Ejecutar(baseDePrueba.Conexion, "UPDATE personas SET pasos_en = '2026-09-05 11:00:00'" + donde),
            "Entro una fecha de respuesta sin decir quien contesto.");

        Ejecutar(
            baseDePrueba.Conexion,
            "UPDATE personas SET pasos_por = $por, pasos_en = '2026-09-05 11:00:00'" + donde,
            ("$por", companero));

        Assert.AreEqual(
            companero,
            Convert.ToInt64(
                Leer(baseDePrueba.Conexion, "SELECT pasos_por FROM personas" + donde),
                CultureInfo.InvariantCulture),
            "Las dos juntas tenian que entrar y no entraron.");
    }

    // ═════════ C19-3: el conteo de columnas sube de 108 a 111, y sale medido ═════════

    /// <summary>
    /// Dada una base al dia, cuando se cuentan TODAS sus columnas, entonces son 111: las 108
    /// de la 18 mas las tres de la 19.
    /// </summary>
    /// <remarks>
    /// El numero se escribe aqui a mano a proposito, igual que en las otras tres pruebas que
    /// lo llevan: si se derivara del esquema, comparar el esquema consigo mismo pasaria en
    /// verde el dia que una migracion se olvide a medias.
    /// </remarks>
    [TestMethod]
    public void ElEsquemaAlDiaSumaCientoOnceColumnas()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var porTabla = TablasDeLaBase(baseDePrueba.Conexion)
            .ToDictionary(tabla => tabla, tabla => ColumnasDe(baseDePrueba.Conexion, tabla).Count, StringComparer.Ordinal);

        Console.WriteLine("== Columnas por tabla en el esquema al dia ==");
        foreach (var (tabla, cuantas) in porTabla.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine("   {0,-24} {1,4}", tabla, cuantas);
        }

        Console.WriteLine("   {0,-24} {1,4}", "TOTAL", porTabla.Values.Sum());

        Assert.AreEqual(
            111,
            porTabla.Values.Sum(),
            "El esquema al dia no suma 111 columnas: las 108 de la 18 mas las tres de la 19.");

        Assert.AreEqual(
            28,
            porTabla["personas"],
            "'personas' no tiene 28 columnas: las 25 de siempre mas las tres de la 19.");
    }

    // ═══════════ C19-4: una base con filas sube sin perder ninguna ═══════════

    /// <summary>
    /// Dada una base parada en la 18 con casos, personas y un companero, cuando se aplica la
    /// 19, entonces llega a la 19, no pierde ni una fila y <c>integrity_check</c> dice ok.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es la forma de la base viva del dueno segun la medicion del planificador del
    /// 2026-09-05 —version 18, 2 casos, 1 companero llamado Sandy—, reproducida aqui. <b>No
    /// se abre su base</b>: se construye una igual, porque el pase prohibe tocar
    /// <c>C:\Users\josem\Documents\Fichas</c> ni para mirar.
    /// </remarks>
    [TestMethod]
    public void UnaBaseParadaEnLaDieciochoSubeALaDiecinueveSinPerderNiUnaFila()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);

        PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var primero = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        var segundo = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "SURB2609");
        PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, primero, "055-1111-385A", "Elena");
        PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, segundo, "055-1111-3853", "Julia");

        var antes = ContarPorTabla(baseDePrueba.Conexion);
        var version = AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var despues = ContarPorTabla(baseDePrueba.Conexion);
        var integridad = Convert.ToString(
            Leer(baseDePrueba.Conexion, "PRAGMA integrity_check"), CultureInfo.InvariantCulture);

        Console.WriteLine("== Filas de una base parada en la 18: version 18 -> {0} ==", version);
        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TABLA", "ANTES", "DESPUES");
        foreach (var (tabla, filas) in antes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine("   {0,-24} {1,6} {2,8}", tabla, filas, despues[tabla]);
        }

        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TOTAL", antes.Values.Sum(), despues.Values.Sum());
        Console.WriteLine("   integrity_check = {0}", integridad);

        Assert.AreEqual(VersionDeEstaFase, version, "No llego a la 19.");
        Assert.AreEqual("ok", integridad, "`pragma integrity_check` no devolvio ok.");
        foreach (var (tabla, filas) in antes)
        {
            Assert.AreEqual(filas, despues[tabla], $"La tabla '{tabla}' cambio de numero de filas.");
        }
    }

    // ═══════════ C19-8: la 19 no firma ni un campo ═══════════

    /// <summary>
    /// Dada una base al dia, cuando se cuentan los campos firmados, entonces son CERO: anadir
    /// las tres columnas de la 19 no marca nada como verificado.
    /// </summary>
    /// <remarks>
    /// ⛔ Regla permanente 5. La firma de campos es de Miguel, campo por campo, y nunca
    /// automatica. Las tres columnas de la 19 dicen quien contesto las SEIS PREGUNTAS del
    /// sistema del lider, que es otra cosa y vive en otra tabla.
    /// </remarks>
    [TestMethod]
    public void LaDiecinueveNoFirmaNiUnCampo()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);

        var firmadosAntes = Firmados(baseDePrueba.Conexion);
        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var firmadosDespues = Firmados(baseDePrueba.Conexion);

        Console.WriteLine(
            "== procedencia_campo con verificado = 1: antes {0}, despues {1} ==",
            firmadosAntes,
            firmadosDespues);

        Assert.AreEqual(0L, firmadosAntes, "La base de partida ya traia campos firmados.");
        Assert.AreEqual(
            firmadosAntes,
            firmadosDespues,
            "La migracion 19 cambio cuantos campos estan firmados, y no tiene por que tocar " +
            "ni uno: la firma de campos es de Miguel y nunca automatica.");
    }

    // ═════════════════════════════ utilidades ═════════════════════════════

    /// <summary>Cuantos campos estan firmados como verificados ahora mismo.</summary>
    private static long Firmados(SqliteConnection conexion)
        => Convert.ToInt64(
            Leer(conexion, "SELECT COUNT(*) FROM procedencia_campo WHERE verificado = 1"),
            CultureInfo.InvariantCulture);

    /// <summary>La pagina raiz que el motor le tiene asignada a esa tabla.</summary>
    private static long PaginaRaizDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT rootpage FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        orden.Parameters.AddWithValue("$tabla", tabla);
        return Convert.ToInt64(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Los nombres de las columnas de una tabla, en el orden en que las tiene.</summary>
    private static List<string> ColumnasDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = $"PRAGMA table_info(\"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\")";
        using var lector = orden.ExecuteReader();

        var columnas = new List<string>();
        while (lector.Read()) columnas.Add(lector.GetString(1));
        return columnas;
    }

    /// <summary>Las tablas de la base, sin las internas del motor.</summary>
    private static List<string> TablasDeLaBase(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var lector = orden.ExecuteReader();

        var tablas = new List<string>();
        while (lector.Read()) tablas.Add(lector.GetString(0));
        return tablas;
    }

    /// <summary>Cuantas filas tiene cada tabla de la base ahora mismo.</summary>
    private static Dictionary<string, long> ContarPorTabla(SqliteConnection conexion)
        => TablasDeLaBase(conexion)
            .Where(tabla => !string.Equals(tabla, "version_esquema", StringComparison.Ordinal))
            .ToDictionary(
                tabla => tabla,
                tabla => Convert.ToInt64(
                    Leer(conexion, $"SELECT COUNT(*) FROM \"{tabla}\""), CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

    /// <summary>El primer valor de la primera fila de la consulta, o nulo.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="consulta">Un SELECT escalar; el texto sale de esta clase.</param>
    private static object? Leer(SqliteConnection conexion, string consulta)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = consulta;
        return orden.ExecuteScalar();
    }

    /// <summary>Ejecuta una instrucción con sus parámetros nombrados.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="instruccion">La instrucción SQL con marcadores <c>$nombre</c>.</param>
    /// <param name="parametros">Pares (marcador, valor).</param>
    private static void Ejecutar(SqliteConnection conexion, string instruccion, params (string, object)[] parametros)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = instruccion;
        foreach (var (nombre, valor) in parametros) orden.Parameters.AddWithValue(nombre, valor);
        orden.ExecuteNonQuery();
    }
}
