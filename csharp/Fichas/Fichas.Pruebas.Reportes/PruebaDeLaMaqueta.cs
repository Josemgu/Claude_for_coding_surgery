using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La maqueta: donde va cada linea, donde corta la pagina y que se repite arriba.
/// </summary>
/// <remarks>Portado de reportes/pdf.py.</remarks>
[TestClass]
public class PruebaDeLaMaqueta
{
    /// <summary>Vigila que el partido corta en los espacios y no a mitad de palabra cuando cabe entera.</summary>
    [TestMethod]
    public void PartirEnLineasCortaPorPalabras()
    {
        var lineas = Maqueta.PartirEnLineas("uno dos tres cuatro", 9);

        CollectionAssert.AreEqual(new[] { "uno dos", "tres", "cuatro" }, lineas.ToArray());
    }

    /// <summary>Vigila que un MRN más largo que la columna se parte en trozos y no se escribe encima de la de al lado.</summary>
    [TestMethod]
    public void UnaPalabraMasLargaQueLaColumnaSeParteEnVezDeDesbordar()
    {
        var lineas = Maqueta.PartirEnLineas("055-1111-3853", 5);

        Assert.IsEmpty(lineas.Where(l => l.Length > 5).ToList(), "Un MRN no puede escribirse encima de la columna de al lado.");
        Assert.AreEqual("055-1111-3853", string.Concat(lineas));
    }

    /// <summary>Vigila que nulo y vacío dan una línea vacía, nunca cero líneas.</summary>
    [TestMethod]
    public void UnTextoVacioSigueSiendoUnaLinea()
    {
        CollectionAssert.AreEqual(new[] { "" }, Maqueta.PartirEnLineas(null, 10).ToArray());
        CollectionAssert.AreEqual(new[] { "" }, Maqueta.PartirEnLineas("", 10).ToArray());
    }

    /// <summary>Vigila que con capacidad cero el partido termina en vez de quedarse dando vueltas.</summary>
    [TestMethod]
    public void LaCapacidadNuncaEsCeroPorqueElPartidoNoAvanzaria()
        => Assert.IsNotEmpty(Maqueta.PartirEnLineas("palabra", 0));

    /// <summary>Vigila que a partir de la segunda página las dos primeras líneas son los títulos de columna.</summary>
    [TestMethod]
    public void LosTitulosDeColumnaSeRepitenArribaDeCadaPaginaNueva()
    {
        var seccion = new Seccion(
            "Los viajes",
            [],
            [new Columna("Caso", ClaseDeColumna.Texto, 12), new Columna("Sale", ClaseDeColumna.Temporal, 14)],
            Enumerable.Range(0, 400).Select(i => (IReadOnlyList<string?>)new string?[] { $"CASP{i:D4}", "2026-09-01" }).ToList(),
            null);

        var documento = DocumentoDePrueba([seccion]);
        var paginas = Maqueta.RepartirEnPaginas(Maqueta.LineasDelDocumento(documento, []));

        Assert.IsGreaterThan(1, paginas.Count, "Con 400 filas tiene que haber mas de una pagina.");
        foreach (var pagina in paginas.Skip(1))
        {
            var primeros = pagina.Take(2).SelectMany(p => p.Linea.Trazos).Select(t => t.Texto).ToList();
            CollectionAssert.Contains(primeros, "Caso");
            CollectionAssert.Contains(primeros, "Sale");
        }
    }

    /// <summary>Vigila que ninguna línea queda por debajo del pie en ninguna página.</summary>
    [TestMethod]
    public void NingunaLineaBajaDelTopeInferior()
    {
        var seccion = new Seccion(
            "Los viajes", [],
            [new Columna("Caso", ClaseDeColumna.Texto, 12)],
            Enumerable.Range(0, 300).Select(i => (IReadOnlyList<string?>)new string?[] { $"CASP{i:D4}" }).ToList(),
            null);

        var paginas = Maqueta.RepartirEnPaginas(Maqueta.LineasDelDocumento(DocumentoDePrueba([seccion]), []));

        foreach (var pagina in paginas)
        {
            foreach (var (y, _) in pagina)
            {
                Assert.IsGreaterThanOrEqualTo(Maqueta.TopeInferior - 1, y, $"Una linea cayo en y={y}, debajo del pie.");
            }
        }
    }

    /// <summary>Vigila que se cuentan los caracteres perdidos del título de la sección y de las celdas, no solo de la portada.</summary>
    [TestMethod]
    public void ContarCaracteresQueNoCabenRecorreElDocumentoEntero()
    {
        var seccion = new Seccion(
            "T中", [], [new Columna("C", ClaseDeColumna.Crudo, 10)],
            [new string?[] { "Ana 文" }], null);

        Assert.AreEqual(2, Maqueta.ContarCaracteresQueNoCaben(DocumentoDePrueba([seccion])));
    }

    /// <summary>Vigila que un documento sin caracteres raros cuenta cero perdidos.</summary>
    [TestMethod]
    public void SinCaracteresPerdidosNoSaleElAvisoDeLaCodificacion()
        => Assert.AreEqual(0, Maqueta.ContarCaracteresQueNoCaben(DocumentoDePrueba([])));

    /// <summary>Un documento mínimo con portada vacía y esas secciones, para probar solo la maqueta.</summary>
    /// <param name="secciones">Las secciones que se colocan.</param>
    private static Documento DocumentoDePrueba(IReadOnlyList<Seccion> secciones)
        => new(
            "Fichas — Reporte de recomendaciones al templo",
            "Período: del 1 al 30 de septiembre de 2026",
            "2026-09-30 10:00:00",
            new Portada("Cuántas personas viajaron sin estar listas", "Frase", []),
            [],
            secciones);
}
