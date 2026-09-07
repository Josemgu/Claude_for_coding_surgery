using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Arrancar con datos inventados no toca NADA de la carpeta del dueño.
/// </summary>
/// <remarks>
/// <para><b>El defecto, medido por el supervisor el 2026-09-05 en la carpeta REAL del
/// dueño.</b> Arrancar con <c>--falso N</c> y sin <c>--carpeta-de-datos</c> resolvía igual la
/// carpeta de verdad y escribía allí. Esto quedó en
/// <c>C:\Users\josem\Documents\Fichas\fichas.log</c>:</para>
/// <code>
/// 2026-09-05 15:33:24.075  ARRANQUE  ventana lista en 1717 ms  datos=INVENTADOS (3000 casos)
///                          carpeta=C:\Users\josem\Documents\Fichas
/// </code>
/// <para>Le pasó a tres agentes distintos el mismo día. La base no se tocó, pero eso fue
/// suerte: con datos inventados el programa no abre la base, y el día que algo escriba,
/// escribirá en la del dueño. Es la única puerta por la que una prueba alcanza sus datos sin
/// querer.</para>
///
/// <para><b>La regla que se comprueba:</b> quien pide datos inventados y NO dice carpeta, se
/// lleva una carpeta de usar y tirar. Quien la dice, manda él. Y quien no pide nada abre la
/// del dueño, como siempre.</para>
///
/// <para>⚠️ Estas pruebas NO abren la ventana ni tocan el disco: solo leen los argumentos,
/// que es donde se decide la carpeta. Que el programa publicado no escriba de verdad en
/// <c>Documents\Fichas</c> se mide aparte, con la ventana, y va en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueLoFalsoNoTocaLoDelDueno
{
    /// <summary>La carpeta de verdad del dueño, la que este programa no puede rozar.</summary>
    private static string LaDelDueno => Fichas.Datos.Rutas.CarpetaDeDatos.ResolverCarpetaDeDatos();

    /// <summary>Con datos inventados y sin decir carpeta, NO se usa la del dueño.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(40)]
    [DataRow(3000)]
    public void ConDatosInventadosYSinDecirCarpetaNoSeTocaLaDelDueno(int casos)
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso", casos.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

        Console.WriteLine($"--falso {casos} → {argumentos.CarpetaDeDatos}");

        Assert.AreNotEqual(
            LaDelDueno,
            argumentos.CarpetaDeDatos,
            $"Con «--falso {casos}» el programa apuntaba a la carpeta del dueño.");
        Assert.IsTrue(
            argumentos.LaCarpetaEsDeUsarYTirar,
            "Y tiene que decirlo, para que la ventana pueda avisar de dónde escribe.");
    }

    /// <summary>
    /// El cero cuenta como los demás: <c>--falso 0</c> son datos inventados VACÍOS, no
    /// «sin datos inventados».
    /// </summary>
    /// <remarks>
    /// Va aparte porque es el caso que ya se confundió una vez en este archivo: el nulo y el
    /// cero no son lo mismo, y con un cero mal leído se abriría la base del dueño.
    /// </remarks>
    [TestMethod]
    public void ElCeroTambienEsDatosInventados()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso", "0"]);

        Assert.IsTrue(argumentos.SeUsanDatosInventados);
        Assert.AreNotEqual(LaDelDueno, argumentos.CarpetaDeDatos);
    }

    /// <summary>Si el dueño dice la carpeta, manda él, aunque los datos sean inventados.</summary>
    /// <remarks>
    /// Es lo que usan todas las mediciones: <c>--falso N --carpeta-de-datos mi-carpeta</c>.
    /// Si esto se rompiera, cada agente mediría en la carpeta de al lado.
    /// </remarks>
    [TestMethod]
    public void LaCarpetaQueSeDiceManda()
    {
        var mia = Path.Combine(Path.GetTempPath(), "fichas-de-la-prueba");

        var argumentos = ArgumentosDeArranque.Leer(["--falso", "40", "--carpeta-de-datos", mia]);

        Assert.AreEqual(mia, argumentos.CarpetaDeDatos);
        Assert.IsFalse(argumentos.LaCarpetaEsDeUsarYTirar, "La dijo el dueño: no es de usar y tirar.");
    }

    /// <summary>Y da igual en qué orden vengan los dos argumentos.</summary>
    [TestMethod]
    public void ElOrdenDeLosArgumentosNoImporta()
    {
        var mia = Path.Combine(Path.GetTempPath(), "fichas-de-la-prueba");

        Assert.AreEqual(mia, ArgumentosDeArranque.Leer(["--carpeta-de-datos", mia, "--falso", "40"]).CarpetaDeDatos);
        Assert.AreEqual(mia, ArgumentosDeArranque.Leer([$"--carpeta-de-datos={mia}", "--falso", "40"]).CarpetaDeDatos);
    }

    /// <summary>Sin «--falso» se abre la del dueño, como siempre. Esto no cambia.</summary>
    /// <remarks>
    /// Es la mitad que hay que proteger al arreglar la otra: un programa que dejara de abrir
    /// la carpeta del dueño al hacer doble clic estaría peor que el defecto que se cierra.
    /// </remarks>
    [TestMethod]
    public void SinPedirDatosInventadosSeAbreLaDelDueno()
    {
        var argumentos = ArgumentosDeArranque.Leer([]);

        Assert.AreEqual(LaDelDueno, argumentos.CarpetaDeDatos);
        Assert.IsFalse(argumentos.LaCarpetaEsDeUsarYTirar);
    }

    /// <summary>
    /// Un «--falso» sin número detrás no se entiende, así que no son datos inventados y la
    /// carpeta sigue siendo la del dueño.
    /// </summary>
    [TestMethod]
    public void UnFalsoSinNumeroNoCambiaLaCarpeta()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso"]);

        Assert.IsFalse(argumentos.SeUsanDatosInventados);
        Assert.AreEqual(LaDelDueno, argumentos.CarpetaDeDatos);
    }

    /// <summary>La carpeta de usar y tirar se llama de forma que se sepa qué es al verla.</summary>
    /// <remarks>
    /// Quien encuentre esa carpeta en su disco dentro de seis meses tiene que poder saber de
    /// dónde salió sin abrir el código.
    /// </remarks>
    [TestMethod]
    public void LaCarpetaDeUsarYTirarSeLlamaComoLoQueEs()
    {
        var carpeta = ArgumentosDeArranque.Leer(["--falso", "40"]).CarpetaDeDatos;

        StringAssert.StartsWith(carpeta, Path.GetTempPath());
        StringAssert.Contains(carpeta, "inventados");
    }
}
