using Fichas.App.Importar;

namespace Fichas.Pruebas.App.Importar;

/// <summary>La tanda de cientos de documentos: que ninguno tumbe al resto.</summary>
[TestClass]
public sealed class PruebasDelMotor : BaseDeImportacion
{
    /// <summary>
    /// Un documento corrupto en medio no impide que entren los de antes y los de despues.
    /// </summary>
    /// <remarks>
    /// Es la razon de ser de este motor: con 500 documentos, un fallo en el puesto 12
    /// tiraria 488 que estaban bien. Y el que fallo deja su renglon: sin el, desaparece
    /// de la tanda sin dejar rastro.
    /// </remarks>
    [TestMethod]
    public async Task UnDocumentoCorruptoEnMedioNoTumbaLaTanda()
    {
        var motor = new MotorDeImportacion(Guardado, ruta => ruta.Contains("roto", StringComparison.Ordinal)
            ? throw new InvalidDataException("El archivo no es un PDF válido.")
            : [Hoja(ruta, 1, null, [("Alguien", null)])]);

        var resumen = await motor.ImportarAsync(
            ["C:/pdfs/a.pdf", "C:/pdfs/roto.pdf", "C:/pdfs/b.pdf"], _ => { }, CancellationToken.None);

        Assert.AreEqual(3, resumen.Documentos);
        Assert.AreEqual(2, resumen.Casos, "los dos buenos entraron.");
        Assert.AreEqual(1, resumen.Ilegibles);
        Assert.AreEqual(2L, Contar("casos"));
        Assert.Contains(MotivosDeIlegible.NoSePudoAbrir, MotivosGuardados());
    }

    /// <summary>El error del documento que fallo se escribe entero, no se silencia.</summary>
    [TestMethod]
    public async Task ElErrorDelDocumentoQueFalloQuedaEscrito()
    {
        var motor = new MotorDeImportacion(Guardado,
            _ => throw new InvalidDataException("Encabezado de PDF ilegible."));

        var resumen = await motor.ImportarAsync(["C:/pdfs/roto.pdf"], _ => { }, CancellationToken.None);

        Assert.Contains("Encabezado de PDF ilegible.", resumen.Detalle());
    }

    /// <summary>Se puede parar a mitad, y lo ya procesado queda guardado.</summary>
    /// <remarks>
    /// Una tanda de 500 que no se pudiera detener seria una ventana secuestrada media hora.
    /// </remarks>
    [TestMethod]
    public async Task PararAMitadConservaLoQueYaEntro()
    {
        using var freno = new CancellationTokenSource();
        var motor = new MotorDeImportacion(Guardado, ruta => [Hoja(ruta, 1, null, [("Alguien", null)])]);

        var resumen = await motor.ImportarAsync(
            ["C:/pdfs/a.pdf", "C:/pdfs/b.pdf", "C:/pdfs/c.pdf"],
            enCurso => { if (enCurso.Documentos == 1) freno.Cancel(); },
            freno.Token);

        Assert.IsTrue(resumen.Cancelada);
        Assert.AreEqual(1, resumen.Documentos);
        Assert.AreEqual(1L, Contar("casos"), "lo que ya se habia procesado quedó guardado.");
        Assert.Contains("detenida", resumen.Linea());
    }

    /// <summary>
    /// La barra avanza documento a documento, y sus cifras son las mismas del resumen.
    /// </summary>
    /// <remarks>
    /// Dos cuentas separadas —una para la barra y otra para el resumen— serian dos cuentas
    /// que pueden no coincidir, y entonces habria que preguntarse cual creerse.
    /// </remarks>
    [TestMethod]
    public async Task LaBarraAvanzaDocumentoADocumentoConLasMismasCifras()
    {
        var vistos = new List<int>();
        var motor = new MotorDeImportacion(Guardado,
            ruta => [Hoja(ruta, 1, null, [("Alguien", "055-0000-0001")])]);

        var resumen = await motor.ImportarAsync(
            ["C:/pdfs/a.pdf", "C:/pdfs/b.pdf", "C:/pdfs/c.pdf"],
            enCurso => vistos.Add(enCurso.Documentos), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, vistos);
        Assert.AreEqual(3, resumen.Documentos);
        Assert.AreEqual(3, resumen.TotalDeDocumentos);
    }

    /// <summary>
    /// Seis hojas de un grupo cuentan como UN caso, no como seis.
    /// </summary>
    /// <remarks>
    /// Contarlas como seis diria «6 casos de 6 páginas», que es la frase que tiene que
    /// delatar cuando algo se pierde. Con las dos cifras separadas, salta a la vista.
    /// </remarks>
    [TestMethod]
    public async Task SeisHojasDeUnGrupoCuentanComoUnCaso()
    {
        var motor = new MotorDeImportacion(Guardado, ruta =>
            Enumerable.Range(1, 6)
                .Select(pagina => Hoja(ruta, pagina, "SURB2609", [($"Persona {pagina}", $"055-0000-000{pagina}")]))
                .ToArray());

        var resumen = await motor.ImportarAsync(["C:/pdfs/grupo.pdf"], _ => { }, CancellationToken.None);

        Assert.AreEqual(1, resumen.Casos);
        Assert.AreEqual(6, resumen.Personas);
        Assert.AreEqual(6, resumen.Hojas);
    }

    /// <summary>Los motivos que quedaron escritos en la tabla de lo que no entró.</summary>
    private string[] MotivosGuardados()
        => Datos.Ilegibles
            .Listar(Contratos.Consultas.FiltroDeIlegibles.Todo, Contratos.Consultas.Pagina.Primera(50))
            .Elementos.Select(renglon => renglon.Motivo).ToArray();
}
