using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Los nombres de las pestanas del informe en Excel, que Excel acepta con condiciones.
/// </summary>
/// <remarks>
/// Excel no admite mas de 31 caracteres en el nombre de una pestana, ni los siete signos
/// <c>: \ / ? * [ ]</c>, ni dos pestanas que se llamen igual. Los titulos de las secciones
/// del informe pasan de los sesenta caracteres —«Parte 1 — Personas que viajaron, y en qué
/// estado quedó su recomendación»— asi que hay que recortarlos, y recortarlos puede dejar
/// dos iguales. El numero de delante es lo que garantiza que no.
/// </remarks>
[TestClass]
public class PruebaDelNombreDeHoja
{
    private static Seccion Con(string titulo) => new(titulo, [], [], [], null);

    [TestMethod]
    public void CadaHojaLlevaSuNumeroDelanteYEnElOrdenDeLasSecciones()
    {
        var nombres = NombreDeHoja.DeLasSecciones([Con("Los viajes"), Con("A qué van"), Con("El equipo")]);

        CollectionAssert.AreEqual(
            new[] { "1. Los viajes", "2. A qué van", "3. El equipo" },
            nombres.ToArray());
    }

    [TestMethod]
    public void NingunNombrePasaDeLosTreintaYUnCaracteresQueExcelAdmite()
    {
        var nombres = NombreDeHoja.DeLasSecciones(
            [Con("Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación")]);

        Assert.AreEqual(NombreDeHoja.LargoMaximo, nombres[0].Length);
        StringAssert.StartsWith(nombres[0], "1. Parte 1 ");
    }

    [TestMethod]
    public void LosSieteSignosQueExcelNoAdmiteSeSustituyen()
    {
        var nombres = NombreDeHoja.DeLasSecciones([Con(@"Uno: dos/tres\cuatro?cinco*seis[siete]")]);

        foreach (var prohibido in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            Assert.DoesNotContain(prohibido.ToString(), nombres[0], $"«{prohibido}» sigue en «{nombres[0]}».");
    }

    /// <summary>Dos secciones con el mismo titulo largo no pueden acabar con el mismo nombre.</summary>
    [TestMethod]
    public void DosSeccionesQueSeLlamanIgualNoDanDosPestanasIguales()
    {
        var largo = new string('a', 60);
        var nombres = NombreDeHoja.DeLasSecciones([Con(largo), Con(largo)]);

        Assert.AreNotEqual(nombres[0], nombres[1], "Excel rechaza el libro entero con dos pestañas del mismo nombre");
    }

    /// <summary>Una seccion sin titulo tiene que dar una pestana igual, no un nombre vacio.</summary>
    [TestMethod]
    public void UnaSeccionSinTituloSigueTeniendoPestana()
    {
        var nombres = NombreDeHoja.DeLasSecciones([Con("   ")]);

        Assert.AreEqual("1. sección 1", nombres[0]);
    }

    /// <summary>Con diez secciones o mas, el numero de dos cifras no puede desbordar el largo.</summary>
    [TestMethod]
    public void ConDiezSeccionesElNumeroDeDosCifrasSigueCabiendo()
    {
        var secciones = Enumerable.Range(1, 12).Select(n => Con(new string('b', 60))).ToList();

        var nombres = NombreDeHoja.DeLasSecciones(secciones);

        Assert.AreEqual(12, nombres.Distinct().Count(), "doce secciones, doce pestañas distintas");
        foreach (var nombre in nombres)
            Assert.IsLessThanOrEqualTo(NombreDeHoja.LargoMaximo, nombre.Length);
        StringAssert.StartsWith(nombres[11], "12. ");
    }

    /// <summary>La hoja del resumen se llama siempre igual y no choca con ninguna seccion.</summary>
    [TestMethod]
    public void LaHojaDelResumenNoChocaConNingunaSeccionPorqueTodasLlevanNumero()
    {
        var nombres = NombreDeHoja.DeLasSecciones([Con(NombreDeHoja.DelResumen)]);

        Assert.AreNotEqual(NombreDeHoja.DelResumen, nombres[0]);
    }
}
