using System.Security.Cryptography;
using Fichas.Datos;
using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Antes de migrar se COPIA la base; si la migracion falla, la base queda como estaba.
/// </summary>
/// <remarks>
/// <para>
/// La regla es del dueno y esta en DECISIONES.md, entrada del 2026-09-04 18:05 («El
/// programa nuevo migro la base VIVA del dueno solo por abrirse»): alguien arranco el
/// <c>.exe</c> sin <c>--carpeta-de-datos</c>, el programa abrio la base real y la llevo de
/// la 13 a la 15 <b>sin preguntar y sin copia previa</b>. Aquel dia no se perdio nada
/// porque su base tenia dos casos; el dia que tenga tres mil, una migracion a medias sin
/// respaldo se lleva el trabajo de meses.
/// </para>
/// <para>
/// Las cinco cosas que se prueban aqui son las cinco que pide esa entrada: copia antes de
/// migrar, con la fecha en el nombre y en la misma carpeta; nada de copia cuando no hay
/// nada que migrar; si falla, la base intacta y la copia localizable; si no hay sitio en
/// disco, no se migra; y el registro diciendo de que version venia y a cual fue.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDelRespaldoAntesDeMigrar
{
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta temporal por prueba; se borra al terminar.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(
            Path.GetTempPath(), "fichas-respaldo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Borra la carpeta temporal.</summary>
    [TestCleanup]
    public void Recoger()
    {
        SqliteConnectionesLibres();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Una base que YA esta al dia no se copia: no hay nada que migrar.
    /// </summary>
    /// <remarks>
    /// Sin esto, cada doble clic dejaria una copia entera de la base al lado, y una base
    /// de tres mil casos abierta cinco veces al dia llena el disco en un mes.
    /// </remarks>
    [TestMethod]
    public void UnaBaseAlDiaNoSeCopia()
    {
        var ruta = CrearBaseEnLaVersion(AplicadorDeEsquema.VersionAlDia);

        var respaldo = RespaldoAntesDeMigrar.PrepararPara(ruta);

        Assert.IsFalse(respaldo.HayCopia, "se copio una base que no habia que migrar.");
        Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, respaldo.VersionDeOrigen);
        Assert.HasCount(1, Directory.GetFiles(_carpeta, "*.db"),
            "quedo algun archivo de base de mas en la carpeta.");
    }

    /// <summary>Una base que todavia no existe tampoco se copia: no hay que respaldar nada.</summary>
    [TestMethod]
    public void UnaBaseQueTodaviaNoExisteNoSeCopia()
    {
        var respaldo = RespaldoAntesDeMigrar.PrepararPara(Path.Combine(_carpeta, "fichas.db"));

        Assert.IsFalse(respaldo.HayCopia);
        Assert.AreEqual(0, respaldo.VersionDeOrigen);
    }

    /// <summary>
    /// Una base vieja se copia ANTES de migrar: misma carpeta, fecha en el nombre y
    /// contenido identico byte a byte.
    /// </summary>
    [TestMethod]
    public void UnaBaseViejaSeCopiaEnLaMismaCarpetaConLaFechaEnElNombre()
    {
        var ruta = CrearBaseEnLaVersion(13);
        var huellaOriginal = HuellaDe(ruta);

        var respaldo = RespaldoAntesDeMigrar.PrepararPara(ruta, new DateTime(2026, 9, 4, 18, 5, 37));

        Assert.IsTrue(respaldo.HayCopia, "no se copio una base que si habia que migrar.");
        Assert.AreEqual(13, respaldo.VersionDeOrigen);
        Assert.AreEqual(_carpeta, Path.GetDirectoryName(respaldo.RutaDeLaCopia),
            "la copia tiene que quedar en la MISMA carpeta que la base.");
        StringAssert.Contains(Path.GetFileName(respaldo.RutaDeLaCopia), "20260904-180537",
            "el nombre de la copia tiene que llevar la fecha.");
        StringAssert.Contains(Path.GetFileName(respaldo.RutaDeLaCopia), "13",
            "el nombre de la copia tiene que decir de que version venia.");
        Assert.AreEqual(huellaOriginal, HuellaDe(respaldo.RutaDeLaCopia!),
            "la copia no es identica a la base que se copio.");
    }

    /// <summary>
    /// Dos copias el mismo segundo no se pisan: la segunda coge otro nombre.
    /// </summary>
    /// <remarks>
    /// Sobrescribir la copia anterior seria perder justo el respaldo que se acaba de
    /// hacer, que es el unico caso en que el respaldo importa.
    /// </remarks>
    [TestMethod]
    public void DosCopiasEnElMismoSegundoNoSePisan()
    {
        var ruta = CrearBaseEnLaVersion(13);
        var cuando = new DateTime(2026, 9, 4, 18, 5, 37);

        var primera = RespaldoAntesDeMigrar.PrepararPara(ruta, cuando);
        var segunda = RespaldoAntesDeMigrar.PrepararPara(ruta, cuando);

        Assert.AreNotEqual(primera.RutaDeLaCopia, segunda.RutaDeLaCopia);
        Assert.IsTrue(File.Exists(primera.RutaDeLaCopia!), "se perdio la primera copia.");
        Assert.IsTrue(File.Exists(segunda.RutaDeLaCopia!));
    }

    /// <summary>
    /// Sin sitio en disco NO se migra y se avisa: ni copia, ni base tocada.
    /// </summary>
    /// <remarks>
    /// Migrar sin haber podido copiar es exactamente lo que la regla del dueno prohibe.
    /// Preferir «migro igual, sin red» seria dejar la base sin salida el dia que la
    /// migracion se rompa a mitad.
    /// </remarks>
    [TestMethod]
    public void SinSitioEnDiscoNoSeMigraYSeAvisa()
    {
        var ruta = CrearBaseEnLaVersion(13);
        var huellaOriginal = HuellaDe(ruta);

        var fallo = Assert.ThrowsExactly<ErrorDeMigracion>(
            () => RespaldoAntesDeMigrar.PrepararPara(ruta, null, _ => 1024));

        StringAssert.Contains(fallo.Message, "sitio");
        Assert.HasCount(1, Directory.GetFiles(_carpeta, "*.db"),
            "se dejo una copia a medias cuando no habia sitio.");
        Assert.AreEqual(huellaOriginal, HuellaDe(ruta), "se toco la base sin haber copiado.");
    }

    /// <summary>
    /// Una migracion que va bien: la base llega al dia, la copia se queda y el registro
    /// dice de que version venia y a cual fue.
    /// </summary>
    [TestMethod]
    public void UnaMigracionQueVaBienDejaLaCopiaYLoDiceEnElRegistro()
    {
        CrearBaseEnLaVersion(13);
        var dicho = new List<string>();

        using (var conexion = ArranqueDeLaBase.PrepararLaBase(_carpeta, dicho.Add))
        {
            Assert.AreEqual(
                AplicadorDeEsquema.VersionAlDia, AplicadorDeEsquema.VersionDeLaBase(conexion));
        }

        var linea = dicho.Find(l => l.Contains("migrada", StringComparison.Ordinal));
        Assert.IsNotNull(linea, "el registro no dijo que hubiera migrado nada.");
        StringAssert.Contains(linea, "13", "no dijo de que version venia.");
        StringAssert.Contains(
            linea, AplicadorDeEsquema.VersionAlDia.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "no dijo a que version fue.");
        Assert.IsTrue(
            dicho.Any(l => l.Contains("Copia", StringComparison.OrdinalIgnoreCase)),
            "no dijo donde quedo la copia.");
        Assert.HasCount(2, Directory.GetFiles(_carpeta, "*.db"),
            "tenia que quedar la base y su copia.");
    }

    /// <summary>
    /// Una base que ya estaba al dia lo dice, en vez de dar a entender que migro.
    /// </summary>
    /// <remarks>
    /// Es el fallo que esta entrega corrige: el 2026-09-04 el registro escribio «Base
    /// abierta con esquema version 15» tanto si habia migrado como si no, y por eso no
    /// hubo forma de saber, leyendo el cuaderno, que la base del dueno se acababa de
    /// migrar de la 13 a la 15.
    /// </remarks>
    [TestMethod]
    public void UnaBaseAlDiaLoDiceYNoParecemQueMigro()
    {
        CrearBaseEnLaVersion(AplicadorDeEsquema.VersionAlDia);
        var dicho = new List<string>();

        using (var conexion = ArranqueDeLaBase.PrepararLaBase(_carpeta, dicho.Add))
        {
            Assert.IsNotNull(conexion);
        }

        Assert.IsFalse(
            dicho.Any(l => l.Contains("migrada", StringComparison.Ordinal)),
            "dijo que migro una base que ya estaba al dia.");
        Assert.IsTrue(
            dicho.Any(l => l.Contains("ya estaba al día", StringComparison.Ordinal)),
            "no dejo dicho que no habia migrado nada.");
    }

    /// <summary>
    /// Una migracion que se rompe A MITAD deja la base EXACTAMENTE como estaba, y la
    /// copia existe y se nombra en el mensaje.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El fallo se fuerza de verdad, no se simula: se deja creada en la base una tabla
    /// llamada <c>casos_version_16</c>, que es la que la migracion 16 crea. La 14 y la 15
    /// se aplican bien, y la 16 revienta con «table casos_version_16 already exists». Es
    /// justo la forma del riesgo real: una migracion en medio de la cola.
    /// </para>
    /// <para>
    /// Sin restaurar, la base se quedaria en la 15 —o sea, cambiada—; el criterio del
    /// dueno es que quede COMO ESTABA, y eso se comprueba byte a byte.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void UnaMigracionQueFallaAMitadDejaLaBaseIntactaYLaCopiaEnSuSitio()
    {
        var ruta = CrearBaseEnLaVersion(13);
        EstorbarLaMigracion16(ruta);
        var huellaAntes = HuellaDe(ruta);
        var dicho = new List<string>();

        var fallo = Assert.ThrowsExactly<ErrorDeMigracion>(
            () => ArranqueDeLaBase.PrepararLaBase(_carpeta, dicho.Add).Dispose());

        SqliteConnectionesLibres();
        Assert.AreEqual(huellaAntes, HuellaDe(ruta),
            "la base NO quedo como estaba: la migracion a medias se quedo escrita.");

        var copias = Directory.GetFiles(_carpeta, "*antes-de-migrar*.db");
        Assert.HasCount(1, copias, "no quedo la copia previa.");
        StringAssert.Contains(fallo.Message, Path.GetFileName(copias[0]),
            "el mensaje no dice donde quedo la copia.");
    }

    /// <summary>Una base con el esquema aplicado hasta la version que se pida.</summary>
    private string CrearBaseEnLaVersion(int version)
    {
        var ruta = Path.Combine(_carpeta, "fichas.db");
        using (var conexion = FabricaDeConexiones.Abrir(ruta))
        {
            AplicadorDeEsquema.AplicarHasta(conexion, version);
        }

        SqliteConnectionesLibres();
        return ruta;
    }

    /// <summary>
    /// Deja puesta la tabla que la migracion 16 va a querer crear, para que reviente.
    /// </summary>
    private static void EstorbarLaMigracion16(string ruta)
    {
        using (var conexion = FabricaDeConexiones.Abrir(ruta))
        {
            using var orden = conexion.CreateCommand();
            orden.CommandText = "CREATE TABLE casos_version_16 (estorbo INTEGER)";
            orden.ExecuteNonQuery();
        }

        SqliteConnectionesLibres();
    }

    /// <summary>La huella SHA-256 de un archivo, para comparar byte a byte sin leerlo entero.</summary>
    private static string HuellaDe(string ruta)
    {
        using var archivo = File.OpenRead(ruta);
        return Convert.ToHexString(SHA256.HashData(archivo));
    }

    /// <summary>Suelta los archivos que el proveedor pudiera tener tomados.</summary>
    private static void SqliteConnectionesLibres()
        => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}
