using Fichas.App.Reportes;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// El nombre que se le propone al selector para el PDF y para el Excel.
/// </summary>
/// <remarks>
/// No es cosmetica. El nombre de un companero entra tal como esta escrito en la base, y ahi
/// puede haber una barra o dos puntos —«Ana María / Sandy»— que Windows no admite en un
/// nombre de archivo: el selector se abriria vacio o la escritura fallaria con un mensaje
/// del sistema en ingles. Aqui se limpia una vez y se prueba por los bordes.
/// </remarks>
[TestClass]
public sealed class PruebasDeLosNombresDeArchivo
{
    /// <summary>El del periodo lleva las dos fechas y termina en .pdf.</summary>
    [TestMethod]
    public void ElDelPeriodoLlevaLasDosFechas()
        => Assert.AreEqual(
            "Reporte 2026-09-01 a 2026-09-30.pdf",
            NombreDeArchivo.DelReporteDelPeriodo(new PeriodoDeLaPantalla("2026-09-01", "2026-09-30")));

    /// <summary>El del historico lleva el dia en que se genero.</summary>
    [TestMethod]
    public void ElDelHistoricoLlevaElDiaEnQueSeGenero()
        => Assert.AreEqual("Histórico 2026-09-04.pdf", NombreDeArchivo.DelHistorico("2026-09-04"));

    /// <summary>El del paquete lleva el nombre del companero y el dia.</summary>
    [TestMethod]
    public void ElDelPaqueteLlevaAlCompaneroYElDia()
        => Assert.AreEqual(
            "Paquete de Sandy 2026-09-04.xlsx",
            NombreDeArchivo.DelPaquete("Sandy", "2026-09-04"));

    /// <summary>Los caracteres que Windows no admite se sustituyen; no se dejan pasar.</summary>
    [TestMethod]
    public void LoQueWindowsNoAdmiteSeSustituye()
    {
        var nombre = NombreDeArchivo.DelPaquete("Ana/María: \"la\" <2>", "2026-09-04");

        Assert.IsFalse(
            nombre.Any(letra => Path.GetInvalidFileNameChars().Contains(letra)),
            $"El nombre «{nombre}» todavía lleva un carácter que Windows no admite.");
        Assert.Contains("Ana", nombre, StringComparison.Ordinal);
        Assert.Contains("María", nombre, StringComparison.Ordinal);
    }

    /// <summary>Un companero sin nombre no deja el archivo sin nombre.</summary>
    [TestMethod]
    public void UnCompaneroSinNombreNoDejaElArchivoSinNombre()
    {
        var nombre = NombreDeArchivo.DelPaquete("   ", "2026-09-04");

        Assert.AreEqual("Paquete de sin nombre 2026-09-04.xlsx", nombre);
    }

    /// <summary>Un nombre larguisimo se recorta: Windows no admite mas de 255 caracteres.</summary>
    [TestMethod]
    public void UnNombreLarguisimoSeRecorta()
    {
        var nombre = NombreDeArchivo.DelPaquete(new string('a', 400), "2026-09-04");

        Assert.IsLessThanOrEqualTo(255, nombre.Length, $"El nombre mide {nombre.Length} caracteres.");
        Assert.EndsWith(".xlsx", nombre, StringComparison.Ordinal);
    }

    /// <summary>El de la segunda vuelta lleva a quien la recibe y el dia.</summary>
    /// <remarks>
    /// Se llama «segunda vuelta» y no «paquete del gerente» a proposito: el dueno generalizo su
    /// peticion a una escalera de categorias, y un gerente es un peldano de esa escalera. Un
    /// nombre de archivo que dijera «gerente» envejeceria el dia que el anada una categoria 4.
    /// </remarks>
    [TestMethod]
    public void ElDeLaSegundaVueltaLlevaAQuienLaRecibeYElDia()
    {
        Assert.AreEqual(
            "Segunda vuelta de Marisol 2026-09-05.xlsx",
            NombreDeArchivo.DeLaSegundaVuelta("Marisol", "2026-09-05"));
    }

    /// <summary>El del informe de un agente lleva su nombre y las dos fechas del periodo.</summary>
    [TestMethod]
    public void ElDelInformeDeAgenteLlevaSuNombreYElPeriodo()
    {
        Assert.AreEqual(
            "Informe de Sandy 2026-08-01 a 2026-09-05.pdf",
            NombreDeArchivo.DelInformeDeAgente("Sandy", new PeriodoDeLaPantalla("2026-08-01", "2026-09-05")));
    }

    /// <summary>Los dos nuevos limpian lo que Windows no admite, como el del paquete.</summary>
    /// <remarks>
    /// Se comprueban los dos y no uno: son tres funciones que llaman al mismo limpiador, y una
    /// que se escribiera manana sin llamarlo pasaria desapercibida hasta que un nombre con
    /// barra rompiera la escritura con un mensaje del sistema que no dice que hacer.
    /// </remarks>
    [TestMethod]
    public void LosDosNuevosTambienLimpianLoQueWindowsNoAdmite()
    {
        foreach (var nombre in new[]
        {
            NombreDeArchivo.DeLaSegundaVuelta("Ana/María: \"la\" <2>", "2026-09-05"),
            NombreDeArchivo.DelInformeDeAgente("Ana/María: \"la\" <2>", new PeriodoDeLaPantalla("2026-08-01", "2026-09-05")),
        })
        {
            foreach (var prohibido in Path.GetInvalidFileNameChars())
            {
                Assert.DoesNotContain(
                    prohibido,
                    nombre,
                    $"«{nombre}» lleva un carácter que Windows no admite en un nombre de archivo.");
            }
        }
    }
}
