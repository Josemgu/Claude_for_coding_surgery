using Fichas.Contratos.Modelos;
using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;
using Fichas.Datos.Repositorios;
using Fichas.Datos.Rutas;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Donde cae la base, y en que estado queda una conexion.
/// </summary>
[TestClass]
public sealed class PruebaDeRutasYConexion
{
    /// <summary>Vigila que <c>SHGetKnownFolderPath</c> devuelve una ruta absoluta que existe en esta máquina.</summary>
    [TestMethod]
    public void LaCarpetaDeDocumentosSePideALaApiYExiste()
    {
        // DECISIONES.md (2026-09-02): la carpeta se resuelve por API y NUNCA se compone
        // pegando el nombre al perfil del usuario. En la maquina del dueno hay tres
        // carpetas candidatas y dos sincronizan con OneDrive.
        var documentos = CarpetaDeDatos.ResolverCarpetaDeDocumentos();

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(documentos),
            "SHGetKnownFolderPath no devolvio ninguna carpeta.");
        Assert.IsTrue(
            Directory.Exists(documentos),
            $"La carpeta que devolvio Windows no existe: '{documentos}'.");
        Assert.IsTrue(
            Path.IsPathRooted(documentos),
            $"La carpeta que devolvio Windows no es una ruta absoluta: '{documentos}'.");
    }

    /// <summary>Vigila que la carpeta se llama «Fichas» y el archivo «fichas.db».</summary>
    [TestMethod]
    public void LaCarpetaDeDatosCuelgaDeDocumentosYSeLlamaFichas()
    {
        var carpeta = CarpetaDeDatos.ResolverCarpetaDeDatos();

        Assert.AreEqual(
            "Fichas",
            Path.GetFileName(carpeta),
            "La carpeta de datos no se llama 'Fichas'.");
        Assert.AreEqual(
            "fichas.db",
            Path.GetFileName(CarpetaDeDatos.RutaDeLaBase(carpeta)),
            "El archivo de base no se llama 'fichas.db'.");
    }

    /// <summary>Vigila las dos formas del argumento, con espacio y con igual, y que se encuentra detrás de otros.</summary>
    [TestMethod]
    public void ElArgumentoDeCarpetaMandaSobreLaCarpetaQueResuelveLaApi()
    {
        // Las dos formas que la gente teclea.
        Assert.AreEqual(
            @"D:\otro\sitio",
            CarpetaDeDatos.ResolverCarpetaDeDatos([@"--carpeta-de-datos", @"D:\otro\sitio"]),
            "La forma con espacio no se leyo.");

        Assert.AreEqual(
            @"D:\otro\sitio",
            CarpetaDeDatos.ResolverCarpetaDeDatos([@"--carpeta-de-datos=D:\otro\sitio"]),
            "La forma con igual no se leyo.");

        Assert.AreEqual(
            @"D:\otro\sitio",
            CarpetaDeDatos.ResolverCarpetaDeDatos(
                ["--otra-cosa", "valor", @"--carpeta-de-datos", @"D:\otro\sitio"]),
            "No se encontro el argumento cuando venia detras de otros.");
    }

    /// <summary>Vigila que sin argumento, con otros, o con el argumento sin valor, se devuelve nulo y no se revienta.</summary>
    [TestMethod]
    public void SinArgumentoSeUsaLaCarpetaQueResuelveLaApi()
    {
        Assert.IsNull(
            CarpetaDeDatos.LeerCarpetaDeLosArgumentos([]),
            "Sin argumentos deberia devolver nulo.");

        Assert.IsNull(
            CarpetaDeDatos.LeerCarpetaDeLosArgumentos(["--otra-cosa", "valor"]),
            "Con otros argumentos deberia devolver nulo.");

        // Un argumento presente pero SIN valor detras no revienta: se sigue con la
        // carpeta que resuelve la API, que es lo correcto (requisito 9).
        Assert.IsNull(
            CarpetaDeDatos.LeerCarpetaDeLosArgumentos(["--carpeta-de-datos"]),
            "Un argumento sin valor detras deberia devolver nulo, no reventar.");

        Assert.IsNull(
            CarpetaDeDatos.LeerCarpetaDeLosArgumentos(["--carpeta-de-datos="]),
            "Un argumento con igual y nada detras deberia devolver nulo.");
    }

    /// <summary>Vigila que «OneDrive» se detecta como segmento, en cualquier caja, y «OneDriveViejo» no.</summary>
    [TestMethod]
    public void SeDetectaOneDriveComparandoSegmentoASegmentoYNoPorSubcadena()
    {
        Assert.IsTrue(
            CarpetaDeDatos.EstaBajoOneDrive(@"C:\Users\josem\OneDrive\Documentos\Fichas"),
            "No detecto una ruta que si esta bajo OneDrive.");

        Assert.IsTrue(
            CarpetaDeDatos.EstaBajoOneDrive(@"C:\Users\josem\onedrive\Documentos"),
            "No detecto OneDrive escrito en minuscula.");

        // Y lo que NO es OneDrive: comparar por subcadena daria un falso positivo aqui,
        // y el aviso dejaria de significar nada de tanto salir.
        Assert.IsFalse(
            CarpetaDeDatos.EstaBajoOneDrive(@"C:\Users\josem\OneDriveViejo\Documentos"),
            "'OneDriveViejo' no es OneDrive.");

        Assert.IsFalse(
            CarpetaDeDatos.EstaBajoOneDrive(@"C:\Users\josem\Documentos\Fichas"),
            "Una ruta normal salio como OneDrive.");
    }

    /// <summary>Vigila que una conexión de solo lectura cuenta el caso y rechaza guardar otro, con aviso y sin excepción.</summary>
    [TestMethod]
    public void UnaConexionDeSoloLecturaDejaLeerYNoDejaEscribir()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-solo-lectura-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "fichas.db");

        try
        {
            // Se construye una base normal y se le mete un caso.
            using (var escritura = FabricaDeConexiones.Abrir(ruta))
            {
                AplicadorDeEsquema.Aplicar(escritura);
                var casos = new RepositorioDeCasos(escritura);
                casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            using var lectura = FabricaDeConexiones.AbrirSoloLectura(ruta);

            // Leer, si.
            var soloLectura = new RepositorioDeCasos(lectura);
            Assert.AreEqual(1, soloLectura.Contar(Fichas.Contratos.Consultas.FiltroDeCasos.Todo),
                "No se pudo leer el caso con una conexion de solo lectura.");

            // Escribir, no. Y falla en el MOTOR, no por un convenio nuestro.
            var intento = soloLectura.Guardar(new Caso { NumeroCaso = "CASP2609" });
            Assert.IsFalse(
                intento.SeEscribio,
                "Se escribio en una base abierta para solo lectura.");
            Assert.IsTrue(intento.HayAvisos, "No dijo por que no se pudo escribir.");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (IOException)
            {
                // La carpeta temporal la limpia el sistema; lo que se media ya se midio.
            }
        }
    }

    /// <summary>Vigila que abrir en solo lectura un archivo inexistente lanza <c>SqliteException</c> y no crea el archivo.</summary>
    [TestMethod]
    public void AbrirUnaBaseQueNoExisteEnSoloLecturaNoLaCrea()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-inexistente-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "no-existe.db");

        try
        {
            Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(
                () => FabricaDeConexiones.AbrirSoloLectura(ruta),
                "Abrir en solo lectura una base que no existe deberia fallar.");

            Assert.IsFalse(
                File.Exists(ruta),
                "Abrir en solo lectura creo el archivo, y no deberia crear nada.");
        }
        finally
        {
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (IOException)
            {
                // Igual que arriba.
            }
        }
    }
}
