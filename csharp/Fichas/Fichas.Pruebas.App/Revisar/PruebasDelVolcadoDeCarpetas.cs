using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Las pruebas de volcar un mes al disco con la misma organización que se ve en pantalla.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-05: «permite descargarlas por mes en esa organización y carpeta
/// creada, para yo tener todo organizado. Me explico: descargar ese conjunto de carpetas a
/// mi escritorio en esa organización».</para>
///
/// <para>Elegir DÓNDE se vuelca es un cuadro del sistema que bloquea la automatización de
/// interfaz, así que aquí se mide la otra mitad —armar el plan y escribir las carpetas— con
/// una raíz temporal que la prueba crea y borra. Lo que NO se mide aquí es el cuadro de
/// elegir carpeta, y eso queda dicho en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelVolcadoDeCarpetas
{
    /// <summary>Una raíz temporal para cada prueba; se borra al terminar.</summary>
    private string _raiz = string.Empty;

    /// <summary>Crea la raíz temporal antes de cada prueba.</summary>
    [TestInitialize]
    public void Antes()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "fichas-volcado-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_raiz);
    }

    /// <summary>Borra la raíz temporal después de cada prueba, pase o falle.</summary>
    [TestCleanup]
    public void Despues()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    /// <summary>El plan de un mes lleva la ruta relativa mes / fecha / unidad, en ese orden.</summary>
    [TestMethod]
    public void ElPlanLlevaLaRutaMesFechaUnidadEnEseOrden()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        var plan = PlanDeVolcado.Para(mes, banco.Servicios.Personas);

        Assert.AreEqual(
            Path.Combine("Septiembre 2026", "Grupo del 17 de septiembre", "700001 · Castries Branch"),
            plan.Archivos.Single().CarpetaRelativa);
    }

    /// <summary>Volcar crea en el disco esa misma estructura, con el documento dentro.</summary>
    [TestMethod]
    public void VolcarCreaLaEstructuraEnElDiscoConElDocumentoDentro()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "origen.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", pdf);
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        var resumen = VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        var carpeta = Path.Combine(destino, "Septiembre 2026", "Grupo del 17 de septiembre", "700001 · Castries Branch");
        Assert.IsTrue(Directory.Exists(carpeta), $"No se creó «{carpeta}».");
        Assert.HasCount(1, Directory.GetFiles(carpeta, "*.pdf"));
        Assert.AreEqual(1, resumen.Copiados);
        Assert.AreEqual(0, resumen.SinOrigen);
    }

    /// <summary>Dentro de la unidad va el paquete de personas que viajará, con nombre y MRN.</summary>
    [TestMethod]
    public void DentroDeLaUnidadVaElPaqueteDePersonas()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "origen.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", pdf);
        banco.MeterPersona(caso, "Ana María Pérez", "055-1111-3853");
        banco.MeterPersona(caso, "Luis Rodríguez", "055-1111-3854");
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        var hoja = Path.Combine(
            destino, "Septiembre 2026", "Grupo del 17 de septiembre", "700001 · Castries Branch",
            PlanDeVolcado.NombreDeLaHojaDePersonas);
        Assert.IsTrue(File.Exists(hoja), $"Falta «{hoja}».");
        var texto = File.ReadAllText(hoja);
        StringAssert.Contains(texto, "Ana María Pérez");
        StringAssert.Contains(texto, "055-1111-3853");
        StringAssert.Contains(texto, "Luis Rodríguez");
    }

    /// <summary>Un PDF que ya no está en el disco NO se calla: sale contado en el resumen.</summary>
    [TestMethod]
    public void UnPdfQueYaNoEstaSeDiceYNoSeCalla()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", Path.Combine(_raiz, "no-existe.pdf"));
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        var resumen = VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        Assert.AreEqual(0, resumen.Copiados);
        Assert.AreEqual(1, resumen.SinOrigen);
        StringAssert.Contains(resumen.Linea, "1", "La línea dice el número, no «hubo problemas».");
        Assert.DoesNotContain("\n", resumen.Linea, "Un aviso de una línea.");
    }

    /// <summary>La carpeta se crea igual aunque el PDF falte: el hueco se ve, no se esconde.</summary>
    [TestMethod]
    public void LaCarpetaSeCreaAunqueFalteElPdf()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", Path.Combine(_raiz, "no-existe.pdf"));
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        Assert.IsTrue(Directory.Exists(Path.Combine(
            destino, "Septiembre 2026", "Grupo del 17 de septiembre", "700001 · Castries Branch")));
    }

    /// <summary>
    /// Un nombre de unidad con caracteres que Windows no admite no rompe el volcado.
    /// </summary>
    /// <remarks>
    /// El nombre de la unidad sale del OCR del papel: puede traer cualquier cosa. Una barra
    /// dentro del nombre crearía una carpeta de más sin que nadie lo pidiera.
    /// </remarks>
    [TestMethod]
    public void UnNombreDeUnidadConCaracteresProhibidosNoRompeElVolcado()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "origen.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        banco.Meter("AAAA0001", "2026-09-17", "700001", @"Rama A/B: ""la de arriba""?", pdf);
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        var resumen = VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        var delMes = Path.Combine(destino, "Septiembre 2026", "Grupo del 17 de septiembre");
        Assert.AreEqual(1, resumen.Copiados);
        Assert.HasCount(1, Directory.GetDirectories(delMes), "Una sola carpeta de unidad, no tres.");
        foreach (var prohibido in Path.GetInvalidFileNameChars())
        {
            Assert.DoesNotContain(
                prohibido.ToString(),
                Path.GetFileName(Directory.GetDirectories(delMes)[0]),
                "En el nombre de la carpeta no queda ningún carácter prohibido.");
        }
    }

    /// <summary>Un nombre reservado de Windows tampoco rompe: se le pone algo delante.</summary>
    [TestMethod]
    public void UnNombreReservadoDeWindowsNoRompe()
    {
        Assert.AreNotEqual("CON", VolcadoDeCarpetas.NombreSeguro("CON"));
        Assert.AreNotEqual("con", VolcadoDeCarpetas.NombreSeguro("con"));
        Assert.AreNotEqual("LPT1", VolcadoDeCarpetas.NombreSeguro("LPT1"));
        Assert.AreEqual("Castries Branch", VolcadoDeCarpetas.NombreSeguro("Castries Branch"), "Lo normal no se toca.");
    }

    /// <summary>Un nombre acabado en punto o en espacio se limpia: Windows no los admite.</summary>
    [TestMethod]
    public void UnNombreAcabadoEnPuntoOEspacioSeLimpia()
    {
        Assert.AreEqual("Rama", VolcadoDeCarpetas.NombreSeguro("Rama."));
        Assert.AreEqual("Rama", VolcadoDeCarpetas.NombreSeguro("Rama  "));
        Assert.AreEqual("sin nombre", VolcadoDeCarpetas.NombreSeguro("   "), "Un nombre vacío se dice, no se deja vacío.");
    }

    /// <summary>Dos documentos que se llamarían igual no se pisan: el segundo se distingue.</summary>
    [TestMethod]
    public void DosDocumentosQueSeLlamarianIgualNoSePisan()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "CASP2609.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", pdf, pagina: 1);
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", pdf, pagina: 1);
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        var destino = Path.Combine(_raiz, "salida");

        var resumen = VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        var carpeta = Path.Combine(destino, "Septiembre 2026", "Grupo del 17 de septiembre", "700001 · Castries Branch");
        Assert.AreEqual(2, resumen.Copiados);
        Assert.HasCount(2, Directory.GetFiles(carpeta, "*.pdf"), "Dos archivos, no uno pisado por el otro.");
    }

    /// <summary>Se vuelca UN mes, no la base entera: es lo que el dueño pidió.</summary>
    [TestMethod]
    public void SeVuelcaUnMesYNoLaBaseEntera()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "origen.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", pdf);
        banco.Meter("AAAA0002", "2026-10-02", "700001", "Castries Branch", pdf);
        var septiembre = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla())[0];
        var destino = Path.Combine(_raiz, "salida");

        VolcadoDeCarpetas.Volcar(destino, PlanDeVolcado.Para(septiembre, banco.Servicios.Personas));

        CollectionAssert.AreEqual(
            new[] { "Septiembre 2026" },
            Directory.GetDirectories(destino).Select(Path.GetFileName).ToArray());
    }

    /// <summary>El resumen dice lo que hizo en UNA línea, con cifras y sin párrafos.</summary>
    [TestMethod]
    public void ElResumenDiceLoQueHizoEnUnaLineaConCifras()
    {
        var banco = new BancoDeCarpetas();
        var pdf = Path.Combine(_raiz, "origen.pdf");
        File.WriteAllText(pdf, "esto hace de PDF");
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch", pdf);
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        var resumen = VolcadoDeCarpetas.Volcar(
            Path.Combine(_raiz, "salida"), PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        Assert.DoesNotContain("\n", resumen.Linea);
        StringAssert.Contains(resumen.Linea, "Septiembre 2026", "Dice qué mes se volcó.");
        Assert.IsLessThan(160, resumen.Linea.Length, $"«{resumen.Linea}» no es una línea.");
    }

    /// <summary>Una raíz a la que no se puede escribir se dice; no se cae ni se calla.</summary>
    [TestMethod]
    public void UnaRaizALaQueNoSePuedeEscribirSeDice()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        // Un archivo NO es una carpeta: crear algo dentro de él falla siempre, en cualquier
        // máquina y sin permisos especiales.
        var estorbo = Path.Combine(_raiz, "esto-es-un-archivo");
        File.WriteAllText(estorbo, "estorbo");

        var resumen = VolcadoDeCarpetas.Volcar(estorbo, PlanDeVolcado.Para(mes, banco.Servicios.Personas));

        Assert.AreEqual(0, resumen.Copiados);
        Assert.IsGreaterThan(0, resumen.Fallos, "El fallo se cuenta.");
        StringAssert.Contains(resumen.Linea, "no se pudo");
    }
}
