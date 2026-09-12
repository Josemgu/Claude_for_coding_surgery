namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La prueba del esqueleto: que este proyecto compila, se descubre y corre al menos un método.
/// </summary>
/// <remarks>No vigila nada del código; existía antes que cualquier reporte y se conserva como suelo de la suite.</remarks>
[TestClass]
public class PruebaDelTerreno
{
    /// <summary>Vigila que el proyecto de pruebas compila y corre: es la prueba que existe desde el esqueleto.</summary>
    [TestMethod]
    public void ElTerrenoTieneNombre() => Assert.IsFalse(string.IsNullOrWhiteSpace("Los reportes en PDF para los jefes y el historico."));
}
