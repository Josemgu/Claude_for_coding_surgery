using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// La clave de fila <c>CASO:MRN:ID</c>: como se arma, como se parte y que pasa cuando
/// llega a medias.
/// </summary>
/// <remarks>
/// Los casos salen del criterio de aceptacion, no del codigo: DECISIONES.md del
/// 2026-09-03 dice que la tercera parte —el id del caso— es obligatoria porque desde la
/// version 12 del esquema dos casos pueden llevar el MISMO numero, y que una clave sin
/// esa tercera parte tiene que seguir volviendo (un paquete generado antes de ese dia).
/// </remarks>
[TestClass]
public class PruebasDeLaClave
{
    [TestMethod]
    public void LaClaveLlevaElCasoElMrnYElIdSeparadosPorDosPuntos()
        => Assert.AreEqual("BALC2609:055-1111-3853:12", Columnas.ArmarLaClave("BALC2609", "055-1111-3853", 12));

    [TestMethod]
    public void UnaPersonaSinMrnSaleConLaClaveAMediasYNoConUnaInventada()
        => Assert.AreEqual("BALC2609::12", Columnas.ArmarLaClave("BALC2609", null, 12));

    [TestMethod]
    public void SinIdDeCasoLaClaveSaleConDosPartes()
        => Assert.AreEqual("BALC2609:055-1111-3853", Columnas.ArmarLaClave("BALC2609", "055-1111-3853", null));

    [TestMethod]
    public void PartirDevuelveLasTresPartes()
    {
        var partida = Columnas.PartirLaClave("BALC2609:055-1111-3853:12");
        Assert.IsNotNull(partida);
        Assert.AreEqual("BALC2609", partida.Value.NumeroCaso);
        Assert.AreEqual("055-1111-3853", partida.Value.Mrn);
        Assert.AreEqual(12L, partida.Value.CasoId);
    }

    [TestMethod]
    public void UnaClaveViejaDeDosPartesSigueVolviendoConElIdEnNulo()
    {
        var partida = Columnas.PartirLaClave("BALC2609:055-1111-3853");
        Assert.IsNotNull(partida);
        Assert.AreEqual("BALC2609", partida.Value.NumeroCaso);
        Assert.AreEqual("055-1111-3853", partida.Value.Mrn);
        Assert.IsNull(partida.Value.CasoId, "una clave de dos partes no trae id y no se inventa uno");
    }

    [TestMethod]
    public void UnaTerceraParteQueNoEsUnNumeroDejaElIdEnNulo()
    {
        var partida = Columnas.PartirLaClave("BALC2609:055-1111-3853:doce");
        Assert.IsNotNull(partida);
        Assert.IsNull(partida.Value.CasoId);
    }

    [TestMethod]
    public void UnaClaveVaciaODeUnaSolaPiezaNoSePuedePartir()
    {
        Assert.IsNull(Columnas.PartirLaClave(null));
        Assert.IsNull(Columnas.PartirLaClave(""));
        Assert.IsNull(Columnas.PartirLaClave("   "));
        Assert.IsNull(Columnas.PartirLaClave("BALC2609"), "sin el separador no hay clave");
    }

    /// <summary>
    /// La cedula PUEDE terminar en letra (DECISIONES.md, 2026-09-04, palabras del dueno:
    /// «muchas cedulas de miembro tienen una A u otra letra al final»). Ni se normaliza
    /// ni se avisa: entra y sale igual.
    /// </summary>
    [TestMethod]
    public void UnaCedulaQueTerminaEnLetraViajaEnteraDentroDeLaClave()
    {
        var clave = Columnas.ArmarLaClave("BALC2609", "055-1111-385A", 12);
        Assert.AreEqual("BALC2609:055-1111-385A:12", clave);
        Assert.AreEqual("055-1111-385A", Columnas.PartirLaClave(clave)!.Value.Mrn);
    }

    [TestMethod]
    public void ElCeroDeDelanteDelMrnSobreviveALaIdaYALaVuelta()
        => Assert.AreEqual("055-1111-3853", Columnas.PartirLaClave(Columnas.ArmarLaClave("X", "055-1111-3853", 1))!.Value.Mrn);
}
