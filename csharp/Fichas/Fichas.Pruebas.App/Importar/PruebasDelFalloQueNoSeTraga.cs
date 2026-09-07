using Fichas.App.Importar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Un manejador de la pantalla NO se traga un fallo: lo convierte en una linea de aviso.
/// </summary>
/// <remarks>
/// <para>
/// La regla sale de un fallo medido el 2026-09-04: se pulsaba «Elegir archivos…» y no
/// pasaba NADA —sin selector, sin error, sin mensaje y sin una linea en <c>fichas.log</c>—.
/// Un boton que no hace nada y no dice nada es lo peor que puede tener este programa: el
/// dueno no tiene forma de saber si el programa esta trabajando, si se rompio, o si el
/// pulso no llego.
/// </para>
/// <para>
/// Lo que se prueba aqui es la REGLA, no el dibujo: que de una excepcion cualquiera sale
/// un aviso que cabe en un renglon (requisito 4 del dueno, «ni un parrafo en pantalla») y
/// que ademas conserva la traza entera en el detalle, que es lo unico con lo que despues
/// se puede diagnosticar.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelFalloQueNoSeTraga
{
    /// <summary>Un fallo es un problema, no una advertencia: la accion NO se hizo.</summary>
    [TestMethod]
    public void UnFalloSaleComoProblemaYNoComoAdvertencia()
    {
        var aviso = AvisoDeUnFalloEnPantalla.Describir(
            "Elegir archivos", new InvalidOperationException("el selector no abrio"));

        Assert.AreEqual(GravedadDeAviso.Problema, aviso.Gravedad);
    }

    /// <summary>La linea nombra la accion que se pulso y lo que paso.</summary>
    /// <remarks>
    /// Las dos cosas hacen falta: sin la accion, el dueno no sabe cual de los botones
    /// fallo; sin el motivo, la franja solo dice «algo salio mal», que es lo mismo que
    /// no decir nada.
    /// </remarks>
    [TestMethod]
    public void LaLineaNombraLaAccionYLoQuePaso()
    {
        var aviso = AvisoDeUnFalloEnPantalla.Describir(
            "Elegir archivos", new InvalidOperationException("el selector no abrio"));

        StringAssert.Contains(aviso.Linea, "Elegir archivos");
        StringAssert.Contains(aviso.Linea, "el selector no abrio");
    }

    /// <summary>La linea es UNA linea: ni un salto, ni un retorno, ni un tabulador.</summary>
    /// <remarks>
    /// La franja de avisos pinta <c>Aviso.Linea</c> en un solo renglon. Un mensaje de
    /// excepcion con saltos dentro —los de SQLite y los de WinRT los traen— romperia la
    /// franja o dejaria escondido justo el trozo que dice que paso.
    /// </remarks>
    [TestMethod]
    public void LaLineaNoLlevaNingunSaltoDeLinea()
    {
        var conSaltos = new InvalidOperationException("primero esto\r\nluego lo otro\ty algo mas");

        var aviso = AvisoDeUnFalloEnPantalla.Describir("Elegir una carpeta", conSaltos);

        Assert.DoesNotContain('\n', aviso.Linea, "la linea no puede llevar un salto.");
        Assert.DoesNotContain('\r', aviso.Linea, "la linea no puede llevar un retorno.");
        Assert.DoesNotContain('\t', aviso.Linea, "la linea no puede llevar un tabulador.");
        StringAssert.Contains(aviso.Linea, "primero esto");
    }

    /// <summary>Un mensaje larguisimo se recorta y se marca con puntos suspensivos.</summary>
    [TestMethod]
    public void UnMensajeLarguisimoSeRecortaYSeMarca()
    {
        var larguisimo = new InvalidOperationException(new string('x', 4000));

        var aviso = AvisoDeUnFalloEnPantalla.Describir("Elegir archivos", larguisimo);

        Assert.IsLessThanOrEqualTo(
            AvisoDeUnFalloEnPantalla.LargoMaximoDeLaLinea, aviso.Linea.Length,
            "la linea tiene que caber en el renglon de la franja.");
        StringAssert.EndsWith(aviso.Linea, "…");
    }

    /// <summary>
    /// El detalle conserva el tipo, el mensaje y la traza: es lo unico que sirve para
    /// diagnosticar despues.
    /// </summary>
    [TestMethod]
    public void ElDetalleConservaElTipoYLaTrazaEnteros()
    {
        Exception atrapado;
        try
        {
            throw new NotSupportedException("esto no se puede hacer aqui");
        }
        catch (NotSupportedException fallo)
        {
            atrapado = fallo;
        }

        var aviso = AvisoDeUnFalloEnPantalla.Describir("Elegir archivos", atrapado);

        Assert.IsNotNull(aviso.Detalle);
        StringAssert.Contains(aviso.Detalle, "System.NotSupportedException");
        StringAssert.Contains(aviso.Detalle, "esto no se puede hacer aqui");
        StringAssert.Contains(aviso.Detalle, nameof(ElDetalleConservaElTipoYLaTrazaEnteros));
    }

    /// <summary>
    /// La causa de debajo tambien sale, porque es donde suele estar el motivo de verdad.
    /// </summary>
    /// <remarks>
    /// Un <c>TargetInvocationException</c> o un <c>AggregateException</c> dicen «algo
    /// fallo dentro»; el motivo esta en <c>InnerException</c>. Sin ella, el aviso
    /// nombraria el envoltorio y no el fallo.
    /// </remarks>
    [TestMethod]
    public void LaCausaDeDebajoTambienSaleEnLaLinea()
    {
        var envuelto = new InvalidOperationException(
            "no se pudo abrir el selector",
            new UnauthorizedAccessException("acceso denegado a Documentos"));

        var aviso = AvisoDeUnFalloEnPantalla.Describir("Elegir archivos", envuelto);

        StringAssert.Contains(aviso.Linea, "acceso denegado a Documentos");
    }

    /// <summary>Una linea para el cuaderno, con la accion delante y en un solo renglon.</summary>
    /// <remarks>
    /// El cuaderno se lee con <c>Get-Content</c> renglon a renglon; una entrada de varias
    /// lineas se mezcla con las de al lado y deja de poderse buscar.
    /// </remarks>
    [TestMethod]
    public void LaLineaDelCuadernoVaEnUnSoloRenglonYNombraLaAccion()
    {
        var conSaltos = new InvalidOperationException("primero esto\nluego lo otro");

        var linea = AvisoDeUnFalloEnPantalla.LineaParaElCuaderno("Elegir archivos", conSaltos);

        Assert.DoesNotContain('\n', linea, "el cuaderno se lee renglon a renglon.");
        StringAssert.Contains(linea, "Elegir archivos");
        StringAssert.Contains(linea, "System.InvalidOperationException");
    }

    /// <summary>Sin fallo no hay aviso que describir: es un error de programacion.</summary>
    [TestMethod]
    public void SinFalloSeQuejaEnVezDeDevolverUnAvisoVacio()
        => Assert.ThrowsExactly<ArgumentNullException>(
            () => AvisoDeUnFalloEnPantalla.Describir("Elegir archivos", null!));
}
