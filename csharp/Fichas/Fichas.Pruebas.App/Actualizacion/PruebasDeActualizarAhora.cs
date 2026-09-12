using System.Net;
using Fichas.App.Actualizacion;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// «Actualizar ahora»: se baja el instalador, se comprueban tamaño y huella, y solo si
/// cuadran se lanza. Nunca se lanza lo que no cuadra.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> DECISIONES.md, 2026-09-11: «baja
/// <c>Instalar-Fichas-vN.exe</c> del Release a la carpeta temporal, comprueba tamaño y
/// huella SHA-256 (el <c>digest</c> que GitHub da por activo) y, solo si cuadran, lanza el
/// instalador en silencio y cierra el programa». Y el pase: «si algo falla (huella distinta,
/// descarga cortada), no lanza nada y lo dice».</para>
///
/// <para>Un instalador con la huella cambiada es, en el mejor caso, una descarga rota y, en
/// el peor, un ejecutable que no es el nuestro. En ninguno de los dos se ejecuta.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeActualizarAhora
{
    /// <summary>El montaje de cada prueba; se tira al terminar.</summary>
    private MontajeDelActualizador _montaje = null!;

    /// <summary>Un instalador de mentirijilla: bytes cualesquiera con los que calcular una huella.</summary>
    private static readonly byte[] ElInstalador = [.. Enumerable.Range(0, 5000).Select(i => (byte)(i * 7 % 251))];

    /// <summary>Un montaje limpio por prueba.</summary>
    [TestInitialize]
    public void Preparar() => _montaje = new MontajeDelActualizador();

    /// <summary>Borra las carpetas de la prueba.</summary>
    [TestCleanup]
    public void Recoger() => _montaje.Dispose();

    /// <summary>Dado un instalador cuyo tamaño y huella cuadran, la descarga queda lista en la carpeta temporal.</summary>
    [TestMethod]
    public async Task ConTamanoYHuellaQueCuadranLaDescargaQuedaLista()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador);
        var actualizador = _montaje.Montar("11");
        var hallazgo = await actualizador.Buscar();

        var descarga = await actualizador.DescargarElInstalador(hallazgo);

        Assert.IsTrue(descarga.Lista, descarga.Motivo);
        Assert.IsTrue(descarga.HuellaComprobada);
        Assert.AreEqual(Path.Combine(_montaje.CarpetaTemporal, "Instalar-Fichas-v12.exe"), descarga.Ruta);
        CollectionAssert.AreEqual(ElInstalador, File.ReadAllBytes(descarga.Ruta));
    }

    /// <summary>Al bajar el activo se pide como binario y con la clave si la hay: así es como GitHub da un activo de un repositorio privado.</summary>
    [TestMethod]
    public async Task ElActivoSePideComoBinarioYConLaClave()
    {
        _montaje.ConLaClavePegada();
        var url = _montaje.ConElUltimoRelease("v12", ElInstalador);
        var actualizador = _montaje.Montar("11");

        await actualizador.DescargarElInstalador(await actualizador.Buscar());

        var laDelActivo = _montaje.Servidor.Peticiones.Single(p => p.Direccion == url);
        Assert.AreEqual("application/octet-stream", laDelActivo.Acepta);
        Assert.AreEqual("Bearer " + MontajeDelActualizador.LaClaveInventada, laDelActivo.Autorizacion);
    }

    /// <summary>Dada una huella distinta de la declarada, no queda lista, se dice y el archivo no se queda.</summary>
    [TestMethod]
    public async Task ConLaHuellaDistintaNoQuedaLista()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador, huellaDeclarada: MontajeDelActualizador.HuellaDe([9, 9, 9]));
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsFalse(descarga.Lista);
        Assert.Contains("huella", descarga.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(File.Exists(Path.Combine(_montaje.CarpetaTemporal, "Instalar-Fichas-v12.exe")), "Un instalador que no cuadra no se deja en el disco.");
    }

    /// <summary>Dado un tamaño distinto del declarado (descarga cortada), no queda lista y se dice.</summary>
    [TestMethod]
    public async Task ConElTamanoDistintoNoQuedaLista()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador, tamanoDeclarado: ElInstalador.Length + 100);
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsFalse(descarga.Lista);
        Assert.Contains("tamaño", descarga.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dado que la API no da huella, se compara al menos el tamaño y se dice que la huella no se comprobó.</summary>
    [TestMethod]
    public async Task SinHuellaSeCompruebaElTamanoYSeDice()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador, conHuella: false);
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsTrue(descarga.Lista, descarga.Motivo);
        Assert.IsFalse(descarga.HuellaComprobada);
    }

    /// <summary>Dado que la descarga falla a medias (la red se va), no queda lista y se dice.</summary>
    [TestMethod]
    public async Task SiLaRedSeVaAMediasNoQuedaLista()
    {
        var url = _montaje.ConElUltimoRelease("v12", ElInstalador);
        _montaje.Servidor.AlPedirFallaLaRed(url, "connection reset");
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsFalse(descarga.Lista);
        Assert.IsNotEmpty(descarga.Motivo);
    }

    /// <summary>Dado un 404 al bajar el activo, no queda lista y se dice.</summary>
    [TestMethod]
    public async Task Un404AlBajarNoQuedaLista()
    {
        var url = _montaje.ConElUltimoRelease("v12", ElInstalador);
        _montaje.Servidor.AlPedir(url, HttpStatusCode.NotFound, "{}");
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsFalse(descarga.Lista);
    }

    /// <summary>Dado un hallazgo que no es una versión nueva, no se baja nada: cero peticiones al activo.</summary>
    [TestMethod]
    public async Task SinVersionNuevaNoSeBajaNada()
    {
        _montaje.ConElUltimoRelease("v11", ElInstalador);
        var actualizador = _montaje.Montar("11");
        var hallazgo = await actualizador.Buscar();

        var descarga = await actualizador.DescargarElInstalador(hallazgo);

        Assert.IsFalse(descarga.Lista);
        Assert.AreEqual(1, _montaje.Servidor.CuantasVecesSeLlamo, "Solo la consulta; el activo no se pidió.");
    }

    /// <summary>Dado un nombre de activo con ruta dentro, se rechaza: el instalador se escribe con SU nombre, no donde diga el JSON.</summary>
    [TestMethod]
    public async Task UnNombreDeActivoConRutaSeRechaza()
    {
        _montaje.Servidor.AlPedir(
            MontajeDelActualizador.LaDireccionDelUltimoRelease,
            HttpStatusCode.OK,
            MontajeDelActualizador.ReleaseComoLoDaGitHub("v12", ("../Instalar-Fichas-v12.exe", "https://api.github.com/x/2", 3, null)));
        var actualizador = _montaje.Montar("11");

        var hallazgo = await actualizador.Buscar();

        Assert.AreEqual(QueSeEncontro.NoSePudo, hallazgo.Que, "Un nombre con ruta no es un instalador nuestro.");
    }

    // ---- lanzar -------------------------------------------------------------------

    /// <summary>Dada una descarga lista, se lanza esa ruta y se dice que se lanzó.</summary>
    [TestMethod]
    public async Task UnaDescargaListaSeLanza()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador);
        var actualizador = _montaje.Montar("11");
        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        var lanzado = actualizador.LanzarElInstalador(descarga);

        Assert.IsTrue(lanzado);
        CollectionAssert.AreEqual(new[] { descarga.Ruta }, _montaje.Lanzador.Lanzados);
    }

    /// <summary>Dada una descarga que no quedó lista, NO se lanza nada: cero lanzamientos.</summary>
    [TestMethod]
    public async Task UnaDescargaQueNoCuadraNoSeLanza()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador, huellaDeclarada: MontajeDelActualizador.HuellaDe([9]));
        var actualizador = _montaje.Montar("11");
        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        var lanzado = actualizador.LanzarElInstalador(descarga);

        Assert.IsFalse(lanzado);
        Assert.IsEmpty(_montaje.Lanzador.Lanzados);
    }

    /// <summary>Al lanzar se le dice al instalador la carpeta de datos, para que el Fichas que reabre siga con la misma.</summary>
    /// <remarks>
    /// El instalador reabre Fichas al terminar (<c>[Run]</c> de Fichas.iss). Sin esto lo
    /// reabriría sin argumentos, y un programa arrancado con <c>--carpeta-de-datos</c> volvería
    /// abierto sobre la carpeta por defecto: otra base, sin que nadie lo dijera.
    /// </remarks>
    [TestMethod]
    public async Task AlLanzarSeLeDiceAlInstaladorLaCarpetaDeDatos()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador);
        var actualizador = _montaje.Montar("11");
        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        actualizador.LanzarElInstalador(descarga);

        CollectionAssert.AreEqual(new[] { _montaje.CarpetaDeDatos }, _montaje.Lanzador.CarpetasDeDatos);
    }

    /// <summary>El instalador se lanza en silencio y sin cuadros, que es lo que Fichas.iss admite, y con la carpeta de datos entre comillas.</summary>
    /// <remarks>
    /// <c>/SILENT</c> y no <c>/VERYSILENT</c>: con el primero se ve la barra de progreso, y
    /// el dueño sabe que algo está pasando mientras el programa está cerrado. La carpeta va
    /// entre comillas porque puede llevar espacios, y sin la barra final: una barra pegada a
    /// la comilla de cierre la escaparía y Windows partiría el argumento por otro sitio.
    /// </remarks>
    /// <param name="carpeta">La carpeta de datos tal como la tiene el programa.</param>
    /// <param name="esperado">La línea de argumentos que tiene que salir.</param>
    [TestMethod]
    [DataRow(@"C:\Users\alguien\Documents\Fichas", @"/SILENT /SUPPRESSMSGBOXES /NORESTART /carpetadedatos=""C:\Users\alguien\Documents\Fichas""")]
    [DataRow(@"C:\una carpeta con espacios\", @"/SILENT /SUPPRESSMSGBOXES /NORESTART /carpetadedatos=""C:\una carpeta con espacios""")]
    public void ElInstaladorSeLanzaEnSilencioYConLaCarpetaDeDatos(string carpeta, string esperado)
        => Assert.AreEqual(esperado, LanzadorDelInstalador.ArgumentosPara(carpeta));

    /// <summary>Dado que GitHub no declara ni tamaño ni huella, no hay contra qué comparar y no queda lista.</summary>
    /// <remarks>Comprobar nada y llamarlo comprobado sería peor que no comprobar: se lanzaría cualquier cosa.</remarks>
    [TestMethod]
    public async Task SinTamanoNiHuellaNoQuedaLista()
    {
        _montaje.ConElUltimoRelease("v12", ElInstalador, conHuella: false, tamanoDeclarado: -1);
        var actualizador = _montaje.Montar("11");

        var descarga = await actualizador.DescargarElInstalador(await actualizador.Buscar());

        Assert.IsFalse(descarga.Lista);
        Assert.Contains("tamaño", descarga.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(File.Exists(Path.Combine(_montaje.CarpetaTemporal, "Instalar-Fichas-v12.exe")));
    }
}
