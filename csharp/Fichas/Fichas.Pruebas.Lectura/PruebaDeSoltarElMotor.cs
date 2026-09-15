using Fichas.Contratos.Lectura;
using Fichas.Lectura;
using SkiaSharp;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Soltar el motor de OCR a mitad de sesión para devolver su memoria, sin que cambie
/// lo que se lee después.
/// </summary>
/// <remarks>
/// <para>Criterio: <b>dado</b> un lector que ya leyó una imagen (motor cargado),
/// <b>cuando</b> se llama a <see cref="LecturaDePdf.SoltarElMotor"/>, <b>entonces</b> la
/// siguiente lectura de la misma imagen devuelve exactamente las mismas líneas —texto,
/// confianza y banda—, porque el motor se vuelve a cargar solo; y soltarlo sin motor, dos
/// veces seguidas o después de <c>Dispose</c> no lanza.</para>
///
/// <para>La imagen se dibuja aquí mismo con Skia: un renglón grande y negro sobre blanco.
/// No hace falta un escaneo del dueño para esto, y así la prueba corre en cualquier
/// máquina con los modelos. Lo que se compara es la lectura consigo misma, no con un
/// texto esperado: la exactitud del OCR la miden las pruebas del corpus.</para>
/// </remarks>
[TestClass]
public class PruebaDeSoltarElMotor
{
    /// <summary>Una hoja pequeña con un renglón legible, como PNG.</summary>
    private static ImagenDePagina ImagenConUnRenglon()
    {
        using var mapa = new SKBitmap(900, 260);
        using (var lienzo = new SKCanvas(mapa))
        {
            lienzo.Clear(SKColors.White);
            using var fuente = new SKFont(SKTypeface.Default, 72);
            using var pintura = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            lienzo.DrawText("CASO 2609 PRUEBA", 40, 160, SKTextAlign.Left, fuente, pintura);
        }
        using var datos = mapa.Encode(SKEncodedImageFormat.Png, 100);
        return new ImagenDePagina(1, mapa.Width, mapa.Height, datos.ToArray());
    }

    /// <summary>Dado un lector con el motor cargado, cuando se suelta, entonces la siguiente lectura es idéntica.</summary>
    [TestMethod]
    public void TrasSoltarElMotorLaLecturaEsLaMisma()
    {
        using var lectura = new LecturaDePdf();
        var imagen = ImagenConUnRenglon();

        var antes = lectura.LeerConOcr(imagen);
        Assert.IsNotEmpty(antes, "El renglón dibujado tiene que leerse: sin líneas la prueba no compara nada.");

        lectura.SoltarElMotor();
        var despues = lectura.LeerConOcr(imagen);

        CollectionAssert.AreEqual(antes.ToArray(), despues.ToArray());
    }

    /// <summary>Dado un lector sin motor, cuando se suelta una o dos veces, entonces no lanza y sigue leyendo.</summary>
    [TestMethod]
    public void SoltarSinMotorODosVecesNoLanza()
    {
        using var lectura = new LecturaDePdf();
        lectura.SoltarElMotor();
        lectura.SoltarElMotor();

        Assert.IsNotEmpty(lectura.LeerConOcr(ImagenConUnRenglon()));
    }

    /// <summary>Dado un lector ya desechado, cuando se suelta el motor, entonces no lanza: desechado ya es soltado.</summary>
    [TestMethod]
    public void SoltarTrasDisposeNoLanza()
    {
        var lectura = new LecturaDePdf();
        lectura.PrepararMotor();
        lectura.Dispose();

        lectura.SoltarElMotor();
    }

    /// <summary>Dado un lector, cuando se suelta el motor, entonces <see cref="LecturaDePdf.TieneElMotorCargado"/> lo dice.</summary>
    [TestMethod]
    public void TieneElMotorCargadoDiceLaVerdad()
    {
        using var lectura = new LecturaDePdf();
        Assert.IsFalse(lectura.TieneElMotorCargado);

        lectura.PrepararMotor();
        Assert.IsTrue(lectura.TieneElMotorCargado);

        lectura.SoltarElMotor();
        Assert.IsFalse(lectura.TieneElMotorCargado);
    }

    /// <summary>
    /// Dado un lector, cuando se pregunta cuánto lleva el motor sin leer, entonces es nulo sin
    /// motor, casi cero recién cargado o recién leído, y nulo otra vez tras soltarlo.
    /// </summary>
    [TestMethod]
    public void TiempoSinLeerSoloExisteConElMotorCargado()
    {
        using var lectura = new LecturaDePdf();
        Assert.IsNull(lectura.TiempoSinLeer);

        lectura.PrepararMotor();
        Assert.IsTrue(lectura.TiempoSinLeer < TimeSpan.FromSeconds(5), $"Recién cargado llevaba {lectura.TiempoSinLeer}.");

        lectura.LeerConOcr(ImagenConUnRenglon());
        Assert.IsTrue(lectura.TiempoSinLeer < TimeSpan.FromSeconds(5), $"Recién leído llevaba {lectura.TiempoSinLeer}.");

        lectura.SoltarElMotor();
        Assert.IsNull(lectura.TiempoSinLeer);
    }
}
