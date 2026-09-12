using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Los seis estados de un campo y las palabras que los dicen. Sin ventana.
/// </summary>
/// <remarks>
/// Portadas de <c>interfaz/tema.py</c>. Lo que se comprueba aqui es que la distincion
/// entre estados <b>no depende de percibir un color</b>: cada estado tiene su palabra.
/// Eran cinco al portarlas; el sexto —«no está en el papel»— entro el 2026-09-05.
/// </remarks>
[TestClass]
public sealed class PruebasDelEstadoDeCampo
{
    /// <summary>Lo que no valida manda sobre todo lo demas, venga de donde venga.</summary>
    /// <remarks>
    /// Al reves, un dato de anotacion que no valida saldria pintado de verde y nadie lo
    /// miraria (<c>interfaz/tema.py</c>, <c>estado_del_campo</c>).
    /// </remarks>
    [TestMethod]
    public void LoQueNoValidaMandaSobreElOrigen()
    {
        var deAnotacion = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Anotacion, Confianza = 1.0 };
        Assert.AreEqual(EstadoDeCampo.NoValido, EstadosDeCampo.Decidir(deAnotacion, esValido: false));
    }

    /// <summary>Sin procedencia guardada, el campo hay que revisarlo.</summary>
    [TestMethod]
    public void SinProcedenciaSeRevisa()
        => Assert.AreEqual(EstadoDeCampo.Revisar, EstadosDeCampo.Decidir(null, esValido: true));

    /// <summary>El tachon es su propio estado y no se confunde con vacio ni con invalido.</summary>
    [TestMethod]
    public void ElTachonEsSuPropioEstado()
    {
        var tachado = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Ocr, AnuladoPorTachon = true };
        Assert.AreEqual(EstadoDeCampo.Tachado, EstadosDeCampo.Decidir(tachado, esValido: true));
    }

    /// <summary>Una anotacion del propio PDF es dato exacto.</summary>
    [TestMethod]
    public void LaAnotacionEsDatoExacto()
    {
        var anotacion = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Anotacion, Confianza = 1.0 };
        Assert.AreEqual(EstadoDeCampo.Anotacion, EstadosDeCampo.Decidir(anotacion, esValido: true));
    }

    /// <summary>El OCR por encima del umbral es OCR; por debajo, hay que revisarlo.</summary>
    [TestMethod]
    public void ElUmbralDeConfianzaPartaElOcrDeLoQueHayQueRevisar()
    {
        var alto = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Ocr, Confianza = 0.91 };
        var bajo = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Ocr, Confianza = 0.42 };
        Assert.AreEqual(EstadoDeCampo.Ocr, EstadosDeCampo.Decidir(alto, esValido: true));
        Assert.AreEqual(EstadoDeCampo.Revisar, EstadosDeCampo.Decidir(bajo, esValido: true));
    }

    /// <summary>Lo tecleado a mano ya no hay que revisarlo: lo puso una persona.</summary>
    [TestMethod]
    public void LoTecleadoAManoNoSeMarcaParaRevisar()
    {
        var aMano = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Manual };
        Assert.AreEqual(EstadoDeCampo.Ocr, EstadosDeCampo.Decidir(aMano, esValido: true));
    }

    /// <summary>Cada estado tiene su palabra escrita, y ninguna se repite.</summary>
    /// <remarks>
    /// Es el criterio que impide que la pantalla dependa del color: quien no distingue el
    /// ambar del verde lee «revisar» y «anotacion», que son dos palabras distintas.
    /// <para>
    /// ⚠️ Eran cinco estados hasta el 2026-09-05 y ahora son SEIS: entro
    /// <see cref="EstadoDeCampo.NoEstaEnElPapel"/>, que el dueno pidio como «esta
    /// informacion no es necesaria». La cuenta va escrita a proposito y no sacada del
    /// <c>enum</c>: asi, quien anada un estado y se olvide de su palabra lo ve aqui en vez
    /// de descubrirlo con un hueco en la pantalla.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void CadaEstadoTieneSuPropiaPalabra()
    {
        var estados = Enum.GetValues<EstadoDeCampo>();
        var palabras = estados.Select(EstadosDeCampo.Palabra).ToList();
        Assert.HasCount(6, estados);
        Assert.AreEqual(6, palabras.Distinct(StringComparer.Ordinal).Count());
        Assert.IsFalse(palabras.Any(string.IsNullOrWhiteSpace));
    }

    // ---- la linea que dice lo que leyo la maquina ------------------------

    /// <summary>
    /// Dado un campo vacio cuyo OCR leyo algo que no encajo, entonces se dice lo que leyo.
    /// </summary>
    /// <remarks>
    /// El problema que tapa: lo leido se guarda en <c>valor_ocr</c>, donde el dueno no lo
    /// ve. Medido sobre sus siete PDF reales, dos cedulas desaparecian de la pantalla sin
    /// dejar rastro.
    /// </remarks>
    [TestMethod]
    public void UnCampoVacioDiceLoQueElLectorLeyo()
    {
        var vacio = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Vacio, ValorOcr = "066-2222-133A" };
        var linea = EstadosDeCampo.LineaDeLoQueSeLeyo(null, vacio);
        StringAssert.Contains(linea, "066-2222-133A", StringComparison.Ordinal);
    }

    /// <summary>Un campo que TIENE valor no dice nada: senalar lo bueno ensena a ignorar.</summary>
    [TestMethod]
    public void UnCampoConValorNoDiceNada()
    {
        var vacio = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Vacio, ValorOcr = "algo" };
        Assert.AreEqual(string.Empty, EstadosDeCampo.LineaDeLoQueSeLeyo("055-1111-3853", vacio));
    }

    /// <summary>
    /// Lo que habia DEBAJO de un tachon no se resucita nunca: alguien lo tacho por algo.
    /// </summary>
    [TestMethod]
    public void LoQueHabiaBajoUnTachonNoSeEnsenaNunca()
    {
        var tachado = new ProcedenciaDeCampo
        {
            Origen = OrigenDeCampo.Vacio,
            ValorOcr = "055-1111-3853",
            AnuladoPorTachon = true,
        };
        Assert.AreEqual(string.Empty, EstadosDeCampo.LineaDeLoQueSeLeyo(null, tachado));
    }

    /// <summary>Un campo que vacio una mano no perdio nada: no se cuenta un problema falso.</summary>
    [TestMethod]
    public void UnCampoVaciadoAManoNoDiceNada()
    {
        var aMano = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Manual, ValorOcr = "algo" };
        Assert.AreEqual(string.Empty, EstadosDeCampo.LineaDeLoQueSeLeyo(null, aMano));
    }

    /// <summary>Sin procedencia no hay nada que contar.</summary>
    [TestMethod]
    public void SinProcedenciaNoHayLineaDeLoLeido()
        => Assert.AreEqual(string.Empty, EstadosDeCampo.LineaDeLoQueSeLeyo(null, null));

    /// <summary>La descripcion de cada estado cabe en una linea del panel de 430 px.</summary>
    [TestMethod]
    public void LasDescripcionesCabenEnUnaLinea()
    {
        var ocr = new ProcedenciaDeCampo { Origen = OrigenDeCampo.Ocr, Confianza = 0.91 };
        foreach (var estado in Enum.GetValues<EstadoDeCampo>())
        {
            var texto = EstadosDeCampo.Descripcion(estado, ocr);
            Assert.IsLessThanOrEqualTo(62, texto.Length, $"«{texto}» mide {texto.Length} y el tope de una linea son 62.");
        }
    }
}
