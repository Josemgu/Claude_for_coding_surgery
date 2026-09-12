using Fichas.App.Actualizacion;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// Lo que la franja dice de cada resultado, y cuándo NO dice nada.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> El pase del 2026-09-11, con sus frases: «Hay una
/// versión nueva: v12» con botón «Actualizar ahora»; «Tienes la última versión, v11» solo a
/// mano; «Para que el programa se actualice solo, pega la clave en &lt;ruta&gt;» discreta y
/// sin botón; «La clave de actualización no vale» sin enseñar la clave; sin red, nada al
/// arrancar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosTextosDeActualizacion
{
    /// <summary>Dada una versión nueva, la línea la nombra y el aviso trae botón.</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnaVersionNuevaSeDiceConSuEtiquetaYConBoton(bool aMano)
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.HayVersionNueva, "v12", new ActivoDelRelease("Instalar-Fichas-v12.exe", "https://api/x", 10, null));

        var aviso = TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano);

        Assert.IsNotNull(aviso);
        Assert.AreEqual("Hay una versión nueva: v12", aviso.Linea);
        Assert.AreEqual(GravedadDeAviso.Informacion, aviso.Gravedad);
        Assert.IsTrue(TextosDeActualizacion.LlevaBotonDeActualizar(hallazgo));
        Assert.AreEqual("Actualizar ahora", TextosDeActualizacion.ElBotonDeActualizar);
    }

    /// <summary>Dada la última versión, al arrancar no se dice nada; a mano se dice cuál es.</summary>
    [TestMethod]
    public void LaUltimaVersionSoloSeDiceAMano()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.EsLaUltima, "v11");

        Assert.IsNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false));
        Assert.AreEqual("Tienes la última versión, v11", TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: true)!.Linea);
    }

    /// <summary>Dado que falta la clave, la línea dice la ruta completa y no lleva botón.</summary>
    [TestMethod]
    public void SiFaltaLaClaveSeDiceDondePegarla()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.FaltaLaClave, RutaDeLaClave: @"C:\Documentos\Fichas\clave-de-actualizacion.txt");

        var aviso = TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false);

        Assert.IsNotNull(aviso);
        Assert.AreEqual(@"Para que el programa se actualice solo, pega la clave en C:\Documentos\Fichas\clave-de-actualizacion.txt", aviso.Linea);
        Assert.AreEqual(GravedadDeAviso.Informacion, aviso.Gravedad, "Discreta: no es un problema, es una instrucción.");
        Assert.IsFalse(TextosDeActualizacion.LlevaBotonDeActualizar(hallazgo));
    }

    /// <summary>Dado que la clave no vale, se dice sin enseñar ninguna clave.</summary>
    [TestMethod]
    public void SiLaClaveNoValeSeDiceSinEnsenarla()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.LaClaveNoVale, Motivo: "403");

        var aviso = TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false);

        Assert.IsNotNull(aviso);
        Assert.AreEqual("La clave de actualización no vale", aviso.Linea);
        Assert.AreEqual(GravedadDeAviso.Advertencia, aviso.Gravedad);
    }

    /// <summary>Dado que no hay red, al arrancar no se dice nada; a mano, sí.</summary>
    [TestMethod]
    public void SinRedNoSeDiceNadaAlArrancarPeroSiAMano()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.SinRed, Motivo: "sin red");

        Assert.IsNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false));
        Assert.IsNotNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: true));
    }

    /// <summary>Dado que no se buscó, no hay nada que decir ni al arrancar ni a mano.</summary>
    [TestMethod]
    public void SiNoSeBuscoNoHayNadaQueDecir()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.NoSeBusco);

        Assert.IsNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false));
        Assert.IsNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: true));
    }

    /// <summary>Dado que no se pudo, al arrancar se calla y a mano se dice el motivo.</summary>
    [TestMethod]
    public void SiNoSePudoSeDiceElMotivoSoloAMano()
    {
        var hallazgo = new ResultadoDeLaBusqueda(QueSeEncontro.NoSePudo, Motivo: "GitHub contestó 500");

        Assert.IsNull(TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: false));
        var aviso = TextosDeActualizacion.AvisoDe(hallazgo, "11", aMano: true);
        Assert.IsNotNull(aviso);
        Assert.Contains("GitHub contestó 500", aviso.Linea + aviso.Detalle);
    }
}
