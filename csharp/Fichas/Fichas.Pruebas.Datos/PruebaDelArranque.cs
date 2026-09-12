using Fichas.Datos;
using Fichas.Datos.Esquema;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que el arranque dice DONDE va a escribir antes de escribir nada.
/// </summary>
[TestClass]
public sealed class PruebaDelArranque
{
    /// <summary>Vigila que mostrar la ruta solo escribe líneas: ni carpeta ni archivo, y dice la carpeta y el motor.</summary>
    [TestMethod]
    public void MostrarLaRutaNoCreaNiCarpetaNiArchivo()
    {
        // DECISIONES.md (2026-09-02): el programa muestra la ruta que resolvio ANTES de
        // escribir, para que se vea si cayo donde debia.
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-solo-mirar-" + Guid.NewGuid().ToString("N"));
        var dicho = new List<string>();

        var ruta = ArranqueDeLaBase.MostrarRutaDeDatos(carpeta, dicho.Add);

        Assert.IsFalse(Directory.Exists(carpeta), "Mostrar la ruta creo la carpeta.");
        Assert.IsFalse(File.Exists(ruta), "Mostrar la ruta creo el archivo.");
        Assert.IsTrue(
            dicho.Any(l => l.Contains(carpeta, StringComparison.Ordinal)),
            "No dijo la carpeta a la que iba a escribir.");
        Assert.IsTrue(
            dicho.Any(l => l.Contains("Motor SQLite", StringComparison.Ordinal)),
            "No dijo con que motor va a trabajar.");
    }

    /// <summary>Vigila que una carpeta bajo OneDrive produce una línea «AVISO:» y no una excepción.</summary>
    [TestMethod]
    public void SeAvisaCuandoLaCarpetaCaeBajoOneDriveYNoSeDetieneElPrograma()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "OneDrive", "Documentos", "Fichas");
        var dicho = new List<string>();

        ArranqueDeLaBase.MostrarRutaDeDatos(carpeta, dicho.Add);

        Assert.IsTrue(
            dicho.Any(l => l.StartsWith("AVISO:", StringComparison.Ordinal)
                           && l.Contains("OneDrive", StringComparison.Ordinal)),
            "No aviso de que la base iba a caer dentro de OneDrive. Esa base lleva " +
            "nombres y MRN de personas reales.");
    }

    /// <summary>El control: una carpeta normal no produce ningún «AVISO:», o el aviso dejaría de significar algo.</summary>
    [TestMethod]
    public void NoSeAvisaDeOneDriveCuandoLaCarpetaEsNormal()
    {
        // El control: si el aviso saliera siempre, dejaria de significar nada.
        var carpeta = Path.Combine(Path.GetTempPath(), "Documentos", "Fichas");
        var dicho = new List<string>();

        ArranqueDeLaBase.MostrarRutaDeDatos(carpeta, dicho.Add);

        Assert.IsFalse(
            dicho.Any(l => l.StartsWith("AVISO:", StringComparison.Ordinal)),
            "Aviso de OneDrive en una carpeta que no tiene nada que ver.");
    }

    /// <summary>Vigila que preparar una base nueva la deja en la versión al día y lo dice con «creada» y el conteo de casos.</summary>
    [TestMethod]
    public void PrepararLaBaseLaCreaConElEsquemaAlDiaYLoDice()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-arranque-" + Guid.NewGuid().ToString("N"));
        var dicho = new List<string>();

        try
        {
            using (var conexion = ArranqueDeLaBase.PrepararLaBase(carpeta, dicho.Add))
            {
                Assert.AreEqual(
                    AplicadorDeEsquema.VersionAlDia,
                    AplicadorDeEsquema.VersionDeLaBase(conexion),
                    "La base no quedo en la version al dia.");
            }

            Assert.IsTrue(
                File.Exists(Path.Combine(carpeta, "fichas.db")),
                "No se creo el archivo de base de datos.");
            Assert.IsTrue(
                dicho.Any(l => l.Contains(
                    "creada con esquema versión " + AplicadorDeEsquema.VersionAlDia.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)),
                "No dijo que la base se creo ni con que version. Se dijo: " +
                string.Join(" | ", dicho));
            Assert.IsTrue(
                dicho.Any(l => l.Contains("Casos guardados: 0", StringComparison.Ordinal)),
                "No dijo cuantos casos hay.");
        }
        finally
        {
            Limpiar(carpeta);
        }
    }

    /// <summary>Vigila que la segunda apertura dice «abierta», no «creada», y sigue al día.</summary>
    [TestMethod]
    public void ArrancarDosVecesSobreLaMismaBaseNoLaVuelveACrear()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-dos-veces-" + Guid.NewGuid().ToString("N"));

        try
        {
            using (var primera = ArranqueDeLaBase.PrepararLaBase(carpeta, _ => { }))
            {
                // La primera crea.
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var dicho = new List<string>();
            using (var segunda = ArranqueDeLaBase.PrepararLaBase(carpeta, dicho.Add))
            {
                Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, AplicadorDeEsquema.VersionDeLaBase(segunda));
            }

            Assert.IsTrue(
                dicho.Any(l => l.Contains(
                    "abierta con esquema versión " + AplicadorDeEsquema.VersionAlDia.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)),
                "La segunda vez dijo que la CREO, y ya existia. Se dijo: " +
                string.Join(" | ", dicho));
        }
        finally
        {
            Limpiar(carpeta);
        }
    }

    /// <summary>Vigila que <c>--carpeta-de-datos</c> pasado por argumentos es donde acaba el archivo.</summary>
    [TestMethod]
    public void ElArgumentoDeCarpetaLlegaHastaDondeSeEscribeDeVerdad()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-por-argumento-" + Guid.NewGuid().ToString("N"));

        try
        {
            using (var conexion = ArranqueDeLaBase.PrepararLaBase(
                new[] { "--carpeta-de-datos", carpeta }, _ => { }))
            {
                Assert.AreEqual(AplicadorDeEsquema.VersionAlDia, AplicadorDeEsquema.VersionDeLaBase(conexion));
            }

            Assert.IsTrue(
                File.Exists(Path.Combine(carpeta, "fichas.db")),
                "La base no se creo en la carpeta que decia el argumento.");
        }
        finally
        {
            Limpiar(carpeta);
        }
    }

    /// <summary>Suelta las conexiones agrupadas y borra la carpeta temporal; si Windows la retiene, no tumba la prueba.</summary>
    /// <param name="carpeta">La carpeta de la prueba.</param>
    private static void Limpiar(string carpeta)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, recursive: true);
            }
        }
        catch (IOException)
        {
            // La carpeta temporal la limpia el sistema; lo que se media ya se midio.
        }
    }
}
