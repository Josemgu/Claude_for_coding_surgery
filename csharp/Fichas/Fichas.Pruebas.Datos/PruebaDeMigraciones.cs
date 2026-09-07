using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que cada migracion existe, se aplica en orden y no pierde nada por el camino.
/// </summary>
/// <remarks>
/// Las versiones de esta clase se copian de `datos/migraciones.py` (la tupla
/// `MIGRACIONES`, medida el 2026-09-04: [2..15]), no de la lista de C# que prueban. La
/// 16, la 17 y la 18 se anaden aparte y a mano porque NO existen en el Python: las dos
/// primeras son las desviaciones declaradas del 2026-09-04 —quitar el CHECK del numero
/// de caso y el de la cedula— y la 18 es la FASE C10, escritas aqui para que se vean
/// deliberadas y no como un descuadre.
/// </remarks>
[TestClass]
public sealed class PruebaDeMigraciones
{
    /// <summary>La ultima version que este archivo declara a mano.</summary>
    /// <remarks>
    /// Se escribe con el numero puesto, y NO se deriva de <c>CatalogoDeMigraciones</c>: si
    /// se derivara, la prueba diria que si a cualquier cosa que se anadiera al catalogo,
    /// que es justo lo que no puede hacer una prueba guardian.
    /// </remarks>
    private const int UltimaVersionDeclarada = 19;

    /// <summary>Las catorce del Python (2 a 15) mas la 16, 17, 18 y 19, que solo son del C#.</summary>
    private static readonly int[] VersionesEsperadas =
        [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];

    [TestMethod]
    public void ElCatalogoDeclaraLasDieciochoMigracionesEnOrdenYSinSaltos()
    {
        var versiones = CatalogoDeMigraciones.Todas.Select(m => m.Version).ToArray();

        CollectionAssert.AreEqual(
            VersionesEsperadas,
            versiones,
            "Las migraciones declaradas no son las 14 de `datos/migraciones.py` mas la " +
            "16, la 17, la 18 y la 19 del C#, en su orden. Se declararon: " +
            string.Join(", ", versiones));
    }

    [TestMethod]
    public void CadaMigracionTraeUnaDescripcionQueDiceQueCambio()
    {
        foreach (var migracion in CatalogoDeMigraciones.Todas)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(migracion.Descripcion),
                $"La migracion {migracion.Version} no dice que cambio.");
        }
    }

    [TestMethod]
    public void UnaBaseNuevaLlegaALaVersionAlDia()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();

        var version = AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        Assert.AreEqual(
            AplicadorDeEsquema.VersionAlDia,
            version,
            "Una base nueva no llego a la version al dia.");
        Assert.AreEqual(
            UltimaVersionDeclarada,
            version,
            $"La version al dia no es la {UltimaVersionDeclarada}: las 15 que declara " +
            "`datos/esquema.py`, mas la 16 que quita el CHECK del numero de caso a las " +
            "bases que el Python dejo en la 12, mas la 17 que quita el de la cedula.");
    }

    [TestMethod]
    public void LaBaseRegistraUnaFilaPorCadaVersionAplicada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        var registradas = LeerVersionesRegistradas(baseDePrueba.Conexion);

        // La 1 es el DDL inicial y tambien deja su fila, asi que son 17 en total.
        CollectionAssert.AreEqual(
            Enumerable.Range(1, UltimaVersionDeclarada).ToArray(),
            registradas,
            "`version_esquema` no registro una fila por version, de la 1 a la " +
            $"{UltimaVersionDeclarada}. Registro: {string.Join(", ", registradas)}");
    }

    [TestMethod]
    public void AplicarElEsquemaDosVecesNoDuplicaNadaNiFalla()
    {
        // Idempotencia (FASE 1 crit. 8): arrancar dos veces sobre una base ya creada
        // deja el conteo igual.
        using var baseDePrueba = BaseDePrueba.SinEsquema();

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var versionesTrasLaPrimera = LeerVersionesRegistradas(baseDePrueba.Conexion);
        var tablasTrasLaPrimera = baseDePrueba.ContarFilasDe("version_esquema");

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var versionesTrasLaSegunda = LeerVersionesRegistradas(baseDePrueba.Conexion);

        CollectionAssert.AreEqual(
            versionesTrasLaPrimera,
            versionesTrasLaSegunda,
            "Aplicar el esquema dos veces cambio las versiones registradas.");
        Assert.AreEqual(
            tablasTrasLaPrimera,
            baseDePrueba.ContarFilasDe("version_esquema"),
            "Aplicar el esquema dos veces duplico filas en `version_esquema`.");
    }

    [TestMethod]
    public void AplicarElEsquemaSobreUnaBaseConDatosNoLosToca()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companero = InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var caso = InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-3853", "Fulano");

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("companeros"), "Se perdio el companero.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Se perdio el caso.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("personas"), "Se perdio la persona.");
        Assert.AreNotEqual(0, companero, "El companero no llego a guardarse.");
    }

    [TestMethod]
    public void UnaBaseParadaEnLaOnceLlegaAlDiaSinPerderFilas()
    {
        // La migracion 12 reconstruye `casos` entera y la 15 reconstruye `personas`:
        // son las dos que de verdad pueden perder datos.
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, 11);
        Assert.AreEqual(11, AplicadorDeEsquema.VersionDeLaBase(baseDePrueba.Conexion));

        var companero = InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var caso = InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-3853", "Fulano");
        InsertarPersona(baseDePrueba.Conexion, caso, "066-2222-1330", "Mengano");

        var antes = ContarTodo(baseDePrueba);

        var version = AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        Assert.AreEqual(
            AplicadorDeEsquema.VersionAlDia,
            version,
            "La base parada en la 11 no llego a la version al dia.");
        CollectionAssert.AreEqual(
            antes,
            ContarTodo(baseDePrueba),
            "Migrar de la 11 al dia cambio el numero de filas de alguna tabla.");
        Assert.AreNotEqual(0, companero, "El companero no llego a guardarse.");
    }

    [TestMethod]
    public void UnaBaseParadaEnLaTreceLlegaAlDiaSinPerderFilas()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, 13);
        Assert.AreEqual(13, AplicadorDeEsquema.VersionDeLaBase(baseDePrueba.Conexion));

        var caso = InsertarCaso(baseDePrueba.Conexion, "BALC2609");
        InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-3853", "Fulano");

        var antes = ContarTodo(baseDePrueba);

        Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion));
        CollectionAssert.AreEqual(
            antes,
            ContarTodo(baseDePrueba),
            "Migrar de la 13 al dia cambio el numero de filas de alguna tabla.");
    }

    [TestMethod]
    public void CadaVersionIntermediaSeAlcanzaUnaAUna()
    {
        // Que la cadena entera este completa: parar en cada peldano y comprobar que
        // la base dice estar en el, y que el siguiente sigue siendo alcanzable.
        foreach (var destino in VersionesEsperadas)
        {
            using var baseDePrueba = BaseDePrueba.SinEsquema();

            AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, destino);

            Assert.AreEqual(
                destino,
                AplicadorDeEsquema.VersionDeLaBase(baseDePrueba.Conexion),
                $"La base no quedo en la version {destino} al aplicar hasta ella.");

            Assert.AreEqual(
                AplicadorDeEsquema.VersionAlDia,
                AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion),
                $"Desde la version {destino} no se llego a la version al dia.");
        }
    }

    /// <summary>Las versiones registradas en la base, ordenadas.</summary>
    private static int[] LeerVersionesRegistradas(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT version FROM version_esquema ORDER BY version";
        using var lector = orden.ExecuteReader();

        var versiones = new List<int>();
        while (lector.Read())
        {
            versiones.Add(lector.GetInt32(0));
        }

        return [.. versiones];
    }

    /// <summary>El conteo de filas de las cuatro tablas que llevan datos en estas pruebas.</summary>
    private static long[] ContarTodo(BaseDePrueba baseDePrueba) =>
    [
        baseDePrueba.ContarFilasDe("casos"),
        baseDePrueba.ContarFilasDe("personas"),
        baseDePrueba.ContarFilasDe("companeros"),
        baseDePrueba.ContarFilasDe("procedencia_campo"),
    ];

    internal static long InsertarCompanero(SqliteConnection conexion, string nombre)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO companeros (nombre, activo, creado_en) VALUES ($nombre, 1, $creado); " +
            "SELECT last_insert_rowid()";
        orden.Parameters.AddWithValue("$nombre", nombre);
        orden.Parameters.AddWithValue("$creado", "2026-09-04 10:00:00");
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static long InsertarCaso(SqliteConnection conexion, string? numeroCaso)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO casos (numero_caso, creado_en) VALUES ($numero, $creado); " +
            "SELECT last_insert_rowid()";
        orden.Parameters.AddWithValue("$numero", (object?)numeroCaso ?? DBNull.Value);
        orden.Parameters.AddWithValue("$creado", "2026-09-04 10:00:00");
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static long InsertarPersona(
        SqliteConnection conexion, long casoId, string? mrn, string? nombre)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO personas (caso_id, mrn, nombre) VALUES ($caso, $mrn, $nombre); " +
            "SELECT last_insert_rowid()";
        orden.Parameters.AddWithValue("$caso", casoId);
        orden.Parameters.AddWithValue("$mrn", (object?)mrn ?? DBNull.Value);
        orden.Parameters.AddWithValue("$nombre", (object?)nombre ?? DBNull.Value);
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
