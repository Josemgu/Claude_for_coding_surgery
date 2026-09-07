using Fichas.App.Cascara;

namespace Fichas.Pruebas.App;

/// <summary>
/// Lo que se le dice al programa por la linea de ordenes.
/// </summary>
/// <remarks>
/// El criterio del que salen es el requisito 9: un argumento que no se entiende NO impide
/// arrancar. Se comprueba que ninguna entrada rara lanza y que el programa se queda con
/// sus valores por defecto.
/// </remarks>
[TestClass]
public sealed class PruebasDeLosArgumentos
{
    /// <summary>Sin argumentos, la carpeta de datos es Documentos\Fichas y no hay casos inventados.</summary>
    [TestMethod]
    public void SinArgumentosSeUsaLaCarpetaDeDocumentos()
    {
        var argumentos = ArgumentosDeArranque.Leer([]);

        Assert.EndsWith("Fichas", argumentos.CarpetaDeDatos);
        // ⚠️ NULO, no cero. Se cambio el 2026-09-04 al enchufar la app a la base real: sin
        // «--falso» hay que abrir la base del dueno, y con «--falso 0» hay que montar datos
        // inventados VACIOS. Con un cero por defecto los dos casos se confundian, y
        // cualquier arranque normal habria abierto datos de mentira sin decirlo.
        Assert.IsNull(argumentos.CasosInventados);
        Assert.IsFalse(argumentos.SeUsanDatosInventados);
        Assert.IsEmpty(argumentos.NoSeEntendio);
    }

    /// <summary>La carpeta de datos se puede cambiar para probar sin tocar la de verdad.</summary>
    [TestMethod]
    public void LaCarpetaDeDatosSeCambia()
    {
        var argumentos = ArgumentosDeArranque.Leer([@"--carpeta-de-datos", @"C:\pruebas\fichas"]);

        Assert.AreEqual(@"C:\pruebas\fichas", argumentos.CarpetaDeDatos);
    }

    /// <summary>Se piden tres mil casos inventados, que es la cifra del requisito 5.</summary>
    [TestMethod]
    public void SePidenTresMilCasosInventados()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso", "3000"]);

        Assert.AreEqual(3000, argumentos.CasosInventados);
    }

    /// <summary>El tamano de la ventana se puede fijar para medir siempre igual.</summary>
    [TestMethod]
    [DataRow("1730x770", 1730, 770)]
    [DataRow("1100x700", 1100, 700)]
    public void ElTamanoDeLaVentanaSeFija(string texto, int ancho, int alto)
    {
        var argumentos = ArgumentosDeArranque.Leer(["--tamano", texto]);

        Assert.AreEqual(ancho, argumentos.Ancho);
        Assert.AreEqual(alto, argumentos.Alto);
    }

    /// <summary>Un argumento que no se entiende no detiene nada: se ignora y se apunta.</summary>
    [TestMethod]
    [DataRow("--falso", "muchos")]
    [DataRow("--tamano", "grande")]
    [DataRow("--lo-que-sea", "1")]
    public void UnArgumentoRaroNoImpideArrancar(string nombre, string valor)
    {
        var argumentos = ArgumentosDeArranque.Leer([nombre, valor]);

        Assert.IsNotEmpty(argumentos.NoSeEntendio, "Se apunta para decirlo en la franja de avisos.");
        Assert.AreEqual(1730, argumentos.Ancho, "Y se queda el valor por defecto.");
        Assert.AreEqual(770, argumentos.Alto);
    }

    /// <summary>Un tamano ridiculo se ignora en vez de dejar una ventana que no se puede usar.</summary>
    [TestMethod]
    public void UnTamanoRidiculoSeIgnora()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--tamano", "10x10"]);

        Assert.AreEqual(1730, argumentos.Ancho);
        Assert.IsNotEmpty(argumentos.NoSeEntendio);
    }

    /// <summary>Un argumento que espera valor y no lo trae no lanza.</summary>
    [TestMethod]
    public void UnArgumentoSinSuValorNoLanza()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso"]);

        // «--falso» sin numero detras no se entiende, asi que se queda como si no se
        // hubiera pedido: se abre la base de verdad. Lo que NO hace es lanzar.
        Assert.IsNull(argumentos.CasosInventados);
    }
}
