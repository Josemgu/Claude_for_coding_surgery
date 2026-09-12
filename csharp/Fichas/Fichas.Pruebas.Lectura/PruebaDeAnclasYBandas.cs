using Fichas.Contratos.Lectura;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Las anclas bilingues y la localizacion de bandas POR TEXTO, nunca por coordenada.
/// </summary>
/// <remarks>
/// Los numeros de esta clase son mediciones de `extraccion/bandas.py` y
/// `extraccion/etiquetas.py`, hechas sobre nueve paginas reales el 2026-09-03. No se
/// vuelven a medir aqui: se vigila que sigan cumpliendose.
/// </remarks>
[TestClass]
public class PruebaDeAnclasYBandas
{
    /// <summary>Una línea del OCR con confianza 1,0 y la caja que se le diga, en fracciones de página.</summary>
    /// <param name="texto">Lo que «leyó» el OCR.</param>
    /// <param name="x0">Borde izquierdo.</param>
    /// <param name="y0">Borde superior.</param>
    /// <param name="x1">Borde derecho.</param>
    /// <param name="y1">Borde inferior.</param>
    private static LineaDeOcr Linea(string texto, double x0, double y0, double x1, double y1)
        => new(texto, 1.0, new BandaDeLaPagina(x0, y0, x1, y1));

    // --- Parecido ----------------------------------------------------------------

    /// <summary>Vigila que el parecido dé 1,0 exacto entre la misma etiqueta con y sin tildes, y con y sin mayúsculas.</summary>
    [TestMethod]
    public void ElParecidoNoDistingueMayusculasNiTildes()
    {
        Assert.AreEqual(1.0, Bandas.Parecido("Numero de cedula de miembro", "Número de cédula de miembro"), 1e-9);
        Assert.AreEqual(1.0, Bandas.Parecido("TEMPLE NAME", "Temple Name"), 1e-9);
    }

    /// <summary>
    /// El cruce maximo medido entre CUALQUIER etiqueta inglesa y CUALQUIER espanola es
    /// 0.357. Si sube por encima del corte del ancla, los dos idiomas ya no pueden
    /// convivir en la misma familia y hay que separarlos.
    /// </summary>
    [TestMethod]
    public void LasDosFormasDeUnMismoRotuloNoSeParecenEntreIdiomas()
    {
        double mayor = 0.0;
        foreach (var campo in Etiquetas.CamposDelFormulario)
        {
            foreach (var otro in Etiquetas.CamposDelFormulario)
            {
                // FormasDe devuelve [inglesa, espanola]: la primera es la forma inglesa.
                double cruce = Bandas.Parecido(Etiquetas.FormasDe(campo)[0], Etiquetas.FormaEspanolaDe(otro));
                if (cruce > mayor) mayor = cruce;
            }
        }
        Assert.IsLessThan(Bandas.ParecidoMinimoDelAncla, mayor,
            $"el cruce maximo entre idiomas subio a {mayor:F3} y el corte del ancla es {Bandas.ParecidoMinimoDelAncla}");
    }

    // --- Localizar el ancla ------------------------------------------------------

    /// <summary>Vigila que la etiqueta de la fecha de viaje se localice tanto con su forma inglesa como con la española.</summary>
    [TestMethod]
    public void ElAnclaSeEncuentraEnInglesYEnEspanol()
    {
        var enIngles = new[] { Linea("Date traveling to the temple", 0.05, 0.42, 0.21, 0.44) };
        var enEspanol = new[] { Linea("Fecha de viaje al templo", 0.05, 0.42, 0.21, 0.44) };

        Assert.IsNotNull(Bandas.LocalizarAncla(enIngles, Etiquetas.CampoDeLaFechaDeViaje));
        Assert.IsNotNull(Bandas.LocalizarAncla(enEspanol, Etiquetas.CampoDeLaFechaDeViaje));
    }

    /// <summary>
    /// El caso medido que obliga a la regla de la etiqueta rival: en una pagina real el
    /// OCR leyo «Date traveling home fror the temple», que se parece 0.77 a la etiqueta
    /// de IDA. Sin la regla, el sistema leeria la fecha de vuelta como fecha de ida, y
    /// eso manda a alguien al templo el dia equivocado.
    /// </summary>
    [TestMethod]
    public void LaFechaDeRegresoNuncaSeTomaPorLaFechaDeIda()
    {
        var lineas = new[] { Linea("Date traveling home fror the temple", 0.55, 0.42, 0.78, 0.44) };
        Assert.IsNull(Bandas.LocalizarAncla(lineas, Etiquetas.CampoDeLaFechaDeViaje));
    }

    /// <summary>Vigila que una línea de otra etiqueta («Associated Costs») no se tome por el ancla de la fecha de viaje.</summary>
    [TestMethod]
    public void UnaLineaQueNoSeParecceANadaNoEsAncla()
    {
        var lineas = new[] { Linea("Associated Costs", 0.05, 0.42, 0.16, 0.44) };
        Assert.IsNull(Bandas.LocalizarAncla(lineas, Etiquetas.CampoDeLaFechaDeViaje));
    }

    // --- La banda que cuelga del ancla ------------------------------------------

    /// <summary>
    /// La banda del valor es la fila de DEBAJO del ancla. La regla no es una coordenada:
    /// se calcula del rectangulo que el OCR encontro en ESTA pagina.
    /// </summary>
    [TestMethod]
    public void LaBandaDelValorCuelgaDebajoDelAncla()
    {
        var ancla = new BandaDeLaPagina(0.10, 0.40, 0.20, 0.42);
        var banda = Bandas.BandaDeValor(ancla, relacionDeAspecto: 612.0 / 792.0);

        Assert.AreEqual(ancla.Y1, banda.Y0, 1e-9, "empieza justo donde acaba el ancla");
        Assert.IsGreaterThan(banda.Y0, banda.Y1);
        Assert.IsGreaterThan(ancla.X1, banda.X1, "se ensancha a la derecha: el valor es mas ancho que su rotulo");
        Assert.IsLessThan(ancla.X0, banda.X0, "y un poco a la izquierda");
    }

    /// <summary>Vigila que una línea a la misma altura pero en la columna derecha no cuente como dentro de la banda.</summary>
    [TestMethod]
    public void UnValorDeLaColumnaDeAlLadoNoEntraEnLaBanda()
    {
        var ancla = new BandaDeLaPagina(0.05, 0.40, 0.15, 0.42);
        var banda = Bandas.BandaDeValor(ancla, relacionDeAspecto: 612.0 / 792.0);
        var mismaAltura = Linea("September 12, 2026", 0.58, 0.425, 0.70, 0.44);

        Assert.IsFalse(Bandas.EstaEnLaBanda(mismaAltura.Banda, banda),
            "el formulario tiene dos columnas: la misma altura no basta");
    }

    /// <summary>Vigila que las líneas de una banda vuelvan ordenadas por su borde izquierdo, no por el orden en que llegaron.</summary>
    [TestMethod]
    public void LasLineasDeLaBandaSalenDeIzquierdaADerecha()
    {
        var banda = new BandaDeLaPagina(0.0, 0.40, 1.0, 0.44);
        var lineas = new[]
        {
            Linea("segunda", 0.50, 0.41, 0.60, 0.43),
            Linea("primera", 0.10, 0.41, 0.20, 0.43),
        };

        var dentro = Bandas.LineasEnLaBanda(lineas, banda);
        CollectionAssert.AreEqual(new[] { "primera", "segunda" }, dentro.Select(l => l.Texto).ToArray());
    }
}
