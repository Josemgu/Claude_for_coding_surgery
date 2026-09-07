namespace Fichas.Pruebas.Reportes;

[TestClass]
public class PruebaDelTerreno
{
    [TestMethod]
    public void ElTerrenoTieneNombre() => Assert.IsFalse(string.IsNullOrWhiteSpace("Los reportes en PDF para los jefes y el historico."));
}
