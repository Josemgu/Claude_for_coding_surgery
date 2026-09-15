using Fichas.App.Correccion;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// La hoja que el dueño eligió a mano MANDA mientras escribe: enfocar un campo no cambia de
/// hoja, y la banda de un campo leído en otra hoja no se pinta sobre la que está delante.
/// </summary>
/// <remarks>
/// <para><b>Las palabras del dueño, 2026-09-15:</b> <i>«Cuando un PDF tiene dos hojas, a veces
/// la primera es solo una factura y la segunda es la correcta. Debe permitirme dar clic a la
/// segunda hoja y a la derecha colocar la información. El bug es que al pasar a la segunda hoja
/// y comenzar a escribir los datos, como el nombre, la cédula y eso, el documento salta de
/// manera automática a la primera hoja de la factura y no te deja colocar la información que
/// realmente necesito.»</i></para>
///
/// <para><b>Medido con la ventana abierta sobre el paquete de master (b78c0e3)</b> con un PDF
/// sintético de dos hojas —la 1 una «factura» con dos rótulos del formulario, la 2 el
/// formulario—: el caso se abrió en la hoja 1 con la unidad vacía; al pulsar «Hoja siguiente»
/// el visor decía <c>2 / 2</c>, y al enfocar «Unidad» decía <c>1 / 2</c>. Añadir a mano una
/// persona también devolvía a la 1, y enfocar su «Nombre» otra vez. La causa:
/// <c>AlEnfocarUnCampo</c> cambiaba de hoja a la del campo cada vez que uno tomaba el foco.</para>
///
/// <para>La regla queda en <see cref="LaHojaQueManda"/>, sin ventana, y estas pruebas se
/// derivan del criterio y no del código: si dos personas leen «la hoja elegida manda» y esperan
/// cosas distintas, el criterio no estaba listo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaHojaQueManda
{
    /// <summary>Una banda cualquiera, para poder distinguir «la suya» de «ninguna».</summary>
    private static readonly BandaDeLaPagina LaBanda = new(0.1, 0.2, 0.6, 0.25);

    /// <summary>Un campo de persona leído en esa hoja, con o sin banda.</summary>
    private static CampoEnPantalla CampoLeidoEn(int? hoja, bool conBanda = true) => new()
    {
        Tabla = TablaDeProcedencia.Personas,
        RegistroId = 7,
        Campo = "nombre",
        Etiqueta = "Nombre",
        PaginaPdf = hoja,
        Banda = conBanda ? LaBanda : null,
    };

    /// <summary>Dado un campo leído en la hoja 1 y la hoja 2 delante, cuando se enfoca, entonces la hoja que se enseña sigue siendo la 2.</summary>
    [TestMethod]
    public void EnfocarUnCampoDeOtraHojaNoCambiaDeHoja()
    {
        var campo = CampoLeidoEn(1);

        Assert.AreEqual(2, LaHojaQueManda.HojaQueSeEnsenaAlEnfocar(campo, hojaDelante: 2),
            "la hoja elegida a mano manda: enfocar no la cambia");
    }

    /// <summary>Dado un campo leído en la hoja 1 y la hoja 2 delante, cuando se enfoca, entonces NO se ilumina su banda: esa banda es de otra hoja.</summary>
    [TestMethod]
    public void LaBandaDeOtraHojaNoSePintaSobreLaQueEstaDelante()
    {
        var campo = CampoLeidoEn(1);

        Assert.IsNull(LaHojaQueManda.BandaQueSeIlumina(campo, hojaDelante: 2),
            "pintar la banda de la hoja 1 sobre la hoja 2 señalaría un sitio que no es");
    }

    /// <summary>Dado un campo leído en la hoja que está delante, cuando se enfoca, entonces se ilumina su banda como siempre.</summary>
    [TestMethod]
    public void LaBandaDeLaMismaHojaSeIluminaComoSiempre()
    {
        var campo = CampoLeidoEn(2);

        Assert.AreEqual(LaBanda, LaHojaQueManda.BandaQueSeIlumina(campo, hojaDelante: 2));
    }

    /// <summary>Un campo que no sabe de qué hoja salió se toma como de la que está delante: se ilumina su banda y no se cambia de hoja.</summary>
    [TestMethod]
    public void UnCampoSinHojaSeTomaComoDeLaQueEstaDelante()
    {
        var campo = CampoLeidoEn(null);

        Assert.AreEqual(LaBanda, LaHojaQueManda.BandaQueSeIlumina(campo, hojaDelante: 2));
        Assert.AreEqual(2, LaHojaQueManda.HojaQueSeEnsenaAlEnfocar(campo, hojaDelante: 2));
        Assert.AreEqual(2, LaHojaQueManda.HojaDondeSeLeyo(campo, hojaDelante: 2));
    }

    /// <summary>Un campo sin banda no ilumina nada, esté en la hoja que esté.</summary>
    [TestMethod]
    public void UnCampoSinBandaNoIluminaNada()
    {
        Assert.IsNull(LaHojaQueManda.BandaQueSeIlumina(CampoLeidoEn(2, conBanda: false), hojaDelante: 2));
        Assert.IsNull(LaHojaQueManda.BandaQueSeIlumina(CampoLeidoEn(1, conBanda: false), hojaDelante: 2));
    }

    /// <summary>Ir a la hoja del campo sigue existiendo, pero como acción explícita: dice a qué hoja hay que ir.</summary>
    [TestMethod]
    public void LaAccionExplicitaSabeAQueHojaIr()
    {
        Assert.AreEqual(1, LaHojaQueManda.HojaDondeSeLeyo(CampoLeidoEn(1), hojaDelante: 2));
        Assert.AreEqual(4, LaHojaQueManda.HojaDondeSeLeyo(CampoLeidoEn(4), hojaDelante: 1));
    }

    /// <summary>La ficha puede decir «se leyó en otra hoja», y solo cuando es verdad.</summary>
    [TestMethod]
    public void SeSabeSiElCampoSeLeyoEnOtraHoja()
    {
        Assert.IsTrue(LaHojaQueManda.SeLeyoEnOtraHoja(CampoLeidoEn(1), hojaDelante: 2));
        Assert.IsFalse(LaHojaQueManda.SeLeyoEnOtraHoja(CampoLeidoEn(2), hojaDelante: 2));
        Assert.IsFalse(LaHojaQueManda.SeLeyoEnOtraHoja(CampoLeidoEn(null), hojaDelante: 2),
            "sin hoja conocida no se afirma que sea otra");
    }

    /// <summary>Un campo nulo no se enfoca: se dice con la excepción de siempre, no con un NullReference.</summary>
    [TestMethod]
    public void UnCampoNuloSeRechazaConSuNombre()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LaHojaQueManda.BandaQueSeIlumina(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => LaHojaQueManda.HojaDondeSeLeyo(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => LaHojaQueManda.SeLeyoEnOtraHoja(null!, 1));
    }
}
