using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El tema lo elige el dueno, y el programa lo recuerda entre sesiones.
/// </summary>
/// <remarks>
/// <para>El criterio del que salen estas pruebas son las palabras del dueno del 2026-09-05:
/// «dame opcion tambien de cambiar el tema del sistema, si es negro o oscuro». De ahi se
/// derivan cuatro cosas comprobables, y ninguna se escribio mirando el codigo:</para>
/// <list type="number">
///   <item>Hay TRES elecciones, no dos: claro, oscuro y el de Windows.</item>
///   <item>Quien no ha elegido nada sigue a Windows, que es lo que hace el programa hoy.</item>
///   <item>Lo elegido sobrevive a cerrar y volver a abrir.</item>
///   <item>Se guarda en la CARPETA DE DATOS —la de <c>--carpeta-de-datos</c>— y no junto al
///   ejecutable, que se pierde al actualizar, ni en el registro de Windows.</item>
/// </list>
///
/// <para>⚠️ Lo que estas pruebas NO miran, porque no hay ventana: que la ventana se pinte
/// oscura de verdad. Eso se mide con el paquete publicado y capturas, y va en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelTemaElegido
{
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba: nunca la del dueno.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-tema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Se recoge lo que se creo, para no dejar carpetas sueltas.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try
        {
            if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            // Que no se pueda borrar una carpeta temporal no invalida la prueba.
        }
    }

    /// <summary>Son tres y solo tres: claro, oscuro y el de Windows.</summary>
    [TestMethod]
    public void SonTresElecciones()
    {
        var todas = Enum.GetValues<TemaDeLaVentana>();

        Assert.HasCount(3, todas, "El dueño pidió también «el de Windows», que es lo que hace hoy.");
        CollectionAssert.AreEquivalent(
            new[] { TemaDeLaVentana.ElDeWindows, TemaDeLaVentana.Claro, TemaDeLaVentana.Oscuro },
            todas);
    }

    /// <summary>Cada eleccion tiene un rotulo en español para leerlo en la pantalla.</summary>
    [TestMethod]
    [DataRow(TemaDeLaVentana.ElDeWindows, "El de Windows")]
    [DataRow(TemaDeLaVentana.Claro, "Claro")]
    [DataRow(TemaDeLaVentana.Oscuro, "Oscuro")]
    public void CadaEleccionSeLeeEnEspanol(TemaDeLaVentana tema, string rotulo)
        => Assert.AreEqual(rotulo, TemasDeLaVentana.ComoSeLee(tema));

    /// <summary>Quien nunca eligió nada sigue a Windows, que es lo que hace el programa hoy.</summary>
    [TestMethod]
    public void SinHaberElegidoNadaSeSigueAWindows()
    {
        var preferencia = new PreferenciaDeTema(_carpeta);

        Assert.AreEqual(TemaDeLaVentana.ElDeWindows, preferencia.Leer());
        Assert.IsFalse(File.Exists(preferencia.Ruta), "Leer no escribe: hasta que no elige, no hay archivo.");
    }

    /// <summary>
    /// Se elige, se cierra y se vuelve a abrir: sigue lo elegido. Es el criterio del dueño.
    /// </summary>
    /// <remarks>
    /// La segunda instancia es la «sesión siguiente»: nada en memoria pasa de una a otra, así
    /// que si esto está en verde es porque lo leyó del disco.
    /// </remarks>
    [TestMethod]
    [DataRow(TemaDeLaVentana.Oscuro)]
    [DataRow(TemaDeLaVentana.Claro)]
    [DataRow(TemaDeLaVentana.ElDeWindows)]
    public void LoElegidoSobreviveACerrarElPrograma(TemaDeLaVentana elegido)
    {
        Assert.IsTrue(new PreferenciaDeTema(_carpeta).Guardar(elegido));

        var laSesionSiguiente = new PreferenciaDeTema(_carpeta);

        Assert.AreEqual(elegido, laSesionSiguiente.Leer());
    }

    /// <summary>
    /// El archivo vive en la carpeta de datos, no junto al ejecutable ni en el registro.
    /// </summary>
    [TestMethod]
    public void LaEleccionSeGuardaEnLaCarpetaDeDatos()
    {
        var preferencia = new PreferenciaDeTema(_carpeta);
        preferencia.Guardar(TemaDeLaVentana.Oscuro);

        Assert.AreEqual(_carpeta, Path.GetDirectoryName(preferencia.Ruta));
        Assert.IsTrue(File.Exists(preferencia.Ruta), $"No hay archivo en «{preferencia.Ruta}».");

        // Y se lee a simple vista, para que el dueño pueda mirar qué guardó el programa.
        StringAssert.Contains(File.ReadAllText(preferencia.Ruta), "oscuro");
    }

    /// <summary>La carpeta la manda «--carpeta-de-datos», que es lo que usan las mediciones.</summary>
    [TestMethod]
    public void LaCarpetaEsLaQueDicenLosArgumentos()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--carpeta-de-datos", _carpeta]);

        var preferencia = new PreferenciaDeTema(argumentos.CarpetaDeDatos);

        Assert.AreEqual(Path.Combine(_carpeta, "preferencias.txt"), preferencia.Ruta);
    }

    /// <summary>Un archivo con basura dentro no impide arrancar: se vuelve al de Windows.</summary>
    /// <remarks>
    /// Es la regla de siempre en este programa: avisar, nunca impedir. Un archivo de
    /// preferencias roto no puede dejar al dueño con un icono que no abre.
    /// </remarks>
    [TestMethod]
    [DataRow("")]
    [DataRow("cualquier cosa")]
    [DataRow("tema=morado")]
    [DataRow("tema=")]
    [DataRow("\0\0\0\0")]
    public void UnArchivoRotoNoImpideArrancar(string basura)
    {
        var preferencia = new PreferenciaDeTema(_carpeta);
        File.WriteAllText(preferencia.Ruta, basura);

        Assert.AreEqual(TemaDeLaVentana.ElDeWindows, preferencia.Leer());
    }

    /// <summary>Guardar donde no se puede escribir no lanza: lo dice devolviendo falso.</summary>
    [TestMethod]
    public void GuardarEnUnaCarpetaImposibleNoLanza()
    {
        // Un nombre de archivo con caracteres que Windows no admite como carpeta.
        var imposible = new PreferenciaDeTema(Path.Combine(_carpeta, "no|se|puede"));

        Assert.IsFalse(imposible.Guardar(TemaDeLaVentana.Oscuro));
        Assert.AreEqual(TemaDeLaVentana.ElDeWindows, imposible.Leer());
    }

    /// <summary>Otras preferencias del archivo no se pierden al guardar el tema.</summary>
    /// <remarks>
    /// El archivo se llama <c>preferencias.txt</c> en plural a propósito: si mañana entra otra
    /// preferencia, guardar el tema no puede borrarla. Se comprueba ahora, que es cuando
    /// cuesta una línea.
    /// </remarks>
    [TestMethod]
    public void GuardarElTemaNoBorraLoDemasDelArchivo()
    {
        var preferencia = new PreferenciaDeTema(_carpeta);
        File.WriteAllText(preferencia.Ruta, "otra_cosa=1" + Environment.NewLine + "tema=claro" + Environment.NewLine);

        preferencia.Guardar(TemaDeLaVentana.Oscuro);

        var escrito = File.ReadAllText(preferencia.Ruta);
        StringAssert.Contains(escrito, "otra_cosa=1");
        Assert.AreEqual(TemaDeLaVentana.Oscuro, preferencia.Leer());
        Assert.AreEqual(1, escrito.Split("tema=").Length - 1, "Una sola línea de tema, no dos.");
    }
}
