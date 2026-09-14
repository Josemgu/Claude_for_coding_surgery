using Fichas.Contratos.Modelos;
using Fichas.Datos.Mantenimiento;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// La regla que decide qué personas del duplicado pasan al original y cuáles ya estaban.
/// </summary>
/// <remarks>
/// <para>Del pase del 2026-09-14: «personas que el original no tiene (por MRN; si el MRN está
/// vacío, por nombre exacto; si coincide, no se duplica la persona)». Es una regla pura sobre
/// dos listas y se prueba sin base: la base solo la aplica.</para>
///
/// <para>⚠️ <b>Cuando hay duda, la persona pasa.</b> Una persona repetida en el original se ve
/// y se arregla a mano; una persona perdida no la ve nadie hasta que llega al templo. Por eso
/// «nombre exacto» es exacto: mayúsculas distintas no son la misma persona para esta regla.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaParejaDePersonas
{
    /// <summary>Una persona con esa cédula y ese nombre, en el caso 1.</summary>
    /// <param name="nombre">El nombre.</param>
    /// <param name="mrn">La cédula, o nula.</param>
    /// <param name="id">El número interno.</param>
    private static Persona Una(string nombre, string? mrn, long id = 0)
        => new() { Id = id, CasoId = 1, Nombre = nombre, Mrn = mrn };

    /// <summary>Misma cédula, misma persona: no pasa aunque el nombre esté escrito distinto.</summary>
    [TestMethod]
    public void LaMismaCedulaEsLaMismaPersonaAunqueElNombreVarie()
    {
        var original = new[] { Una("Ana Perez", "1234567890001") };
        var duplicado = new[] { Una("ANA PÉREZ", "1234567890001", 9) };

        var reparto = LoQuePasaAlOriginal.Personas(original, duplicado);

        Assert.IsEmpty(reparto.Pasan);
        Assert.AreEqual(1, reparto.YaEstaban);
    }

    /// <summary>Una cédula que el original no tiene pasa, con su id para moverla.</summary>
    [TestMethod]
    public void UnaCedulaQueElOriginalNoTienePasa()
    {
        var original = new[] { Una("Ana Perez", "1234567890001"), Una("Beto Perez", "1234567890002") };
        var duplicado = new[]
        {
            Una("Ana Perez", "1234567890001", 7),
            Una("Beto Perez", "1234567890002", 8),
            Una("Carla Perez", "1234567890003", 9),
        };

        var reparto = LoQuePasaAlOriginal.Personas(original, duplicado);

        Assert.HasCount(1, reparto.Pasan);
        Assert.AreEqual(9, reparto.Pasan[0].Id);
        Assert.AreEqual(2, reparto.YaEstaban);
    }

    /// <summary>Sin cédula en el duplicado, decide el nombre exacto.</summary>
    [TestMethod]
    public void SinCedulaDecideElNombreExacto()
    {
        var original = new[] { Una("Ana Perez", "1234567890001"), Una("Dario Perez", null) };
        var duplicado = new[]
        {
            Una("Ana Perez", null, 7),
            Una("Dario Perez", null, 8),
            Una("Elena Perez", null, 9),
        };

        var reparto = LoQuePasaAlOriginal.Personas(original, duplicado);

        CollectionAssert.AreEqual(new long[] { 9 }, reparto.Pasan.Select(p => p.Id).ToList());
        Assert.AreEqual(2, reparto.YaEstaban);
    }

    /// <summary>El nombre se compara sin los espacios de sobra, pero con sus mayúsculas: ante la duda, pasa.</summary>
    [TestMethod]
    public void ElNombreIgnoraEspaciosDeSobraPeroNoMayusculas()
    {
        var original = new[] { Una("Ana  Perez ", null), Una("Beto Perez", null) };
        var duplicado = new[] { Una(" Ana Perez", null, 7), Una("BETO PEREZ", null, 8) };

        var reparto = LoQuePasaAlOriginal.Personas(original, duplicado);

        CollectionAssert.AreEqual(new long[] { 8 }, reparto.Pasan.Select(p => p.Id).ToList());
    }

    /// <summary>Con cédula en el duplicado, el nombre no decide: si la cédula no está, pasa.</summary>
    /// <remarks>
    /// Es la decisión conservadora escrita: una persona del original sin cédula y otra del
    /// duplicado con cédula y el mismo nombre se quedan las dos. Duplicar se ve; perder no.
    /// </remarks>
    [TestMethod]
    public void ConCedulaEnElDuplicadoElNombreNoDecide()
    {
        var original = new[] { Una("Ana Perez", null) };
        var duplicado = new[] { Una("Ana Perez", "1234567890001", 7) };

        var reparto = LoQuePasaAlOriginal.Personas(original, duplicado);

        Assert.HasCount(1, reparto.Pasan);
        Assert.AreEqual(0, reparto.YaEstaban);
    }

    /// <summary>Un campo vacío en el original se rellena con el del duplicado; uno lleno nunca se pisa.</summary>
    [TestMethod]
    public void UnCampoVacioSeRellenaYUnoLlenoNoSePisa()
    {
        var original = new Caso { NumeroCaso = "CASP2609", UnidadNumero = "7000011", FechaViaje = null, TemploNombre = "  ", UnidadNombre = null };
        var duplicado = new Caso { NumeroCaso = "CASP2609", UnidadNumero = "7000022", FechaViaje = "2026-10-08", TemploNombre = "Santo Domingo", UnidadNombre = null };

        var campos = LoQuePasaAlOriginal.Campos(original, duplicado);

        CollectionAssert.AreEqual(
            new[] { "fecha_viaje", "templo_nombre" },
            campos.Select(c => c.Columna).ToList(),
            "Solo los vacíos en el original con valor en el duplicado; el número de unidad lleno no se toca.");
        Assert.AreEqual("2026-10-08", campos[0].Valor);
        Assert.AreEqual("fecha de viaje", campos[0].Rotulo);
    }
}
