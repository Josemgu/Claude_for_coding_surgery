using ClosedXML.Excel;
using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El informe en Excel: una hoja por seccion, cada hoja una tabla que se filtra y se ordena.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-07: <i>«está bien el de PDF, pero también quiero uno con
/// Excel»</i>. Un PDF y una hoja de calculo no sirven para lo mismo: el PDF es para
/// ensenarselo a alguien y el Excel es para filtrar, ordenar y sumar. Por eso lo que se prueba
/// aqui no es que «salga bonito» sino que se pueda TRABAJAR con el: fila 1 congelada,
/// autofiltro, fechas que Excel sabe que son fechas y numeros que se dejan sumar.</para>
///
/// <para>⚠️ <b>La prueba que mas vale es la de los ceros de delante.</b> Es la misma que en
/// Python se puso roja sola: sin formato de texto, Excel lee <c>055-1111-3853</c> o
/// <c>0700016</c> como algo que puede normalizar y se come el cero, y un MRN sin sus ceros
/// deja de identificar a nadie. Va con dos afirmaciones —el formato <c>@</c> Y el valor leido
/// de vuelta— porque una sola de las dos se puede cumplir estando mal.</para>
/// </remarks>
[TestClass]
public class PruebaDelLibroDelInforme
{
    /// <summary>Un MRN con cero delante y con letra al final, que son los dos casos raros.</summary>
    private const string ElMrn = "055-1111-385A";

    /// <summary>
    /// La pestana de la Parte 1, escrita a mano y no calculada.
    /// </summary>
    /// <remarks>
    /// Se escribe entera aqui a proposito. Si saliera de <c>NombreDeHoja</c>, la prueba diria
    /// «la hoja se llama como la llama el codigo», que es cierto siempre: el titulo de la
    /// seccion mide 70 caracteres, Excel admite 31, y hay que ver con los ojos donde queda el
    /// corte.
    /// </remarks>
    private const string HojaDeLaTabla = "1. Parte 1 — Personas que viaja";

    /// <summary>Una unidad cuyo numero empieza por cero, que es lo que Excel se come.</summary>
    private const string LaUnidad = "Cuatricentenaria · 0700016";

    private static readonly Columna[] ColumnasDeLaTabla =
    [
        new("N.º de caso", ClaseDeColumna.Texto, 12),
        new("Persona", ClaseDeColumna.Crudo, 30),
        new("MRN", ClaseDeColumna.Texto, 14),
        new("Unidad", ClaseDeColumna.Crudo, 22),
        new("Fecha de viaje", ClaseDeColumna.Temporal, 14),
        new("Personas", ClaseDeColumna.Crudo, 10),
    ];

    private static readonly Seccion LaTabla = new(
        "Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación",
        ["Entran todas las personas cuyo caso tenía fecha de viaje dentro del período."],
        ColumnasDeLaTabla,
        [
            ["BALC2609", "Elena Rosa Muestra", ElMrn, LaUnidad, "2026-10-15", "3"],
            ["BALC2609", "=Elena", null, LaUnidad, "el mes que viene", null],
        ],
        "2 personas · 1 con la recomendación completa");

    private static readonly Seccion LaVacia = new(
        "Dónde se traban",
        [],
        [new("Paso del sistema del líder", ClaseDeColumna.Crudo, 34), new("Veces sin completar", ClaseDeColumna.Crudo, 20)],
        [],
        null);

    private static readonly Documento ElInforme = new(
        "Fichas — Reporte de recomendaciones al templo",
        "Período: del 1 al 30 de septiembre de 2026",
        "2026-09-20 10:00:00",
        new Portada(
            "Cuántas personas viajaron sin estar listas",
            "De las 9 personas que ya viajaron, 2 lo hicieron sin la recomendación completa.",
            [
                new Cifra(2, "viajaron sin estar listas", TonoDeCifra.Malo),
                new Cifra(7, "Verificadas", TonoDeCifra.Bueno),
            ]),
        ["No se puede fechar cuándo se vio el problema en 3 casos."],
        [LaTabla, LaVacia]);

    private static XLWorkbook Libro() => LibroDelInforme.Construir(ElInforme);

    // ─────────────────────── la forma del libro ───────────────────────

    [TestMethod]
    public void HayUnaHojaDeResumenYUnaHojaPorSeccion()
    {
        using var libro = Libro();

        CollectionAssert.AreEqual(
            new[] { NombreDeHoja.DelResumen, HojaDeLaTabla, "2. Dónde se traban" },
            libro.Worksheets.Select(h => h.Name).ToArray());
    }

    /// <summary>La fila 1 son los rotulos del PDF, en el mismo orden y con las mismas palabras.</summary>
    /// <remarks>
    /// Los mismos y no otros a proposito: si el Excel llamara «Cédula» a lo que el PDF llama
    /// «MRN», el dueno tendria dos documentos del mismo periodo que no se pueden comparar, y
    /// eso es peor que no tener el Excel.
    /// </remarks>
    [TestMethod]
    public void LaFilaUnoSonLosRotulosDelPdfEnSuOrden()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet(HojaDeLaTabla);

        var rotulos = Enumerable.Range(1, ColumnasDeLaTabla.Length).Select(n => hoja.Cell(1, n).GetString()).ToArray();
        CollectionAssert.AreEqual(ColumnasDeLaTabla.Select(c => c.Nombre).ToArray(), rotulos);
    }

    [TestMethod]
    public void CadaFilaDeLaSeccionEsUnaFilaDeLaHojaYEmpiezanEnLaDos()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet(HojaDeLaTabla);

        Assert.AreEqual("Elena Rosa Muestra", hoja.Cell(2, 2).GetString());
        Assert.AreEqual("=Elena", hoja.Cell(3, 2).GetString());
        Assert.IsTrue(hoja.Cell(4, 1).IsEmpty(), "dos filas de datos, ni una más");
    }

    [TestMethod]
    public void LaCabeceraVaCongeladaYConAutofiltro()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet(HojaDeLaTabla);

        Assert.AreEqual(1, hoja.SheetView.SplitRow, "sin la fila 1 fija, al bajar no se sabe qué columna se está leyendo");
        Assert.IsTrue(hoja.AutoFilter.IsEnabled, "sin autofiltro esto es el PDF con bordes");
    }

    /// <summary>Una seccion sin filas conserva su hoja con la cabecera.</summary>
    [TestMethod]
    public void UnaSeccionSinFilasSaleConSuCabeceraYSinFilas()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("2. Dónde se traban");

        Assert.AreEqual("Paso del sistema del líder", hoja.Cell(1, 1).GetString(),
            "una hoja vacía dice «esto existe y está vacío», que no es lo mismo que no tener la hoja");
        Assert.IsTrue(hoja.Cell(2, 1).IsEmpty());
    }

    // ─────────────────────── los ceros de delante ───────────────────────

    /// <summary>La defensa entera contra que Excel se coma los ceros de delante.</summary>
    [TestMethod]
    public void ElMrnYElNumeroDeUnidadSalenComoTextoConSusCerosDelante()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet(HojaDeLaTabla);

        var mrn = hoja.Cell(2, 3);
        Assert.AreEqual("@", mrn.Style.NumberFormat.Format);
        Assert.AreEqual(ElMrn, mrn.GetString());

        var unidad = hoja.Cell(2, 4);
        Assert.AreEqual("@", unidad.Style.NumberFormat.Format, "el número de la unidad viaja dentro de esta celda");
        Assert.AreEqual(LaUnidad, unidad.GetString(), "un entero se habría comido el cero de delante de 0700016");

        var caso = hoja.Cell(2, 1);
        Assert.AreEqual("@", caso.Style.NumberFormat.Format);
        Assert.AreEqual("BALC2609", caso.GetString());
    }

    /// <summary>Un valor que empieza por cero NUNCA entra como número, sea de la columna que sea.</summary>
    /// <remarks>
    /// Es la regla que hace imposible el fallo, no la que lo evita en las columnas que hoy
    /// conocemos: si manana una columna «cruda» trae un numero de unidad a secas, tampoco se
    /// le come el cero.
    /// </remarks>
    [TestMethod]
    public void UnValorCrudoQueEmpiezaPorCeroSeEscribeComoTextoYNoComoNumero()
    {
        var documento = ElInforme with
        {
            Secciones = [LaTabla with { Filas = [["BALC2609", "Quien sea", ElMrn, "0700016", "2026-10-15", "3"]] }, LaVacia],
        };

        using var libro = LibroDelInforme.Construir(documento);
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(2, 4);

        Assert.AreEqual(XLDataType.Text, celda.DataType);
        Assert.AreEqual("@", celda.Style.NumberFormat.Format);
        Assert.AreEqual("0700016", celda.GetString());
    }

    [TestMethod]
    public void UnTextoQueEmpiezaPorIgualNoSeConvierteEnFormula()
    {
        using var libro = Libro();
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(3, 2);

        Assert.AreEqual(XLDataType.Text, celda.DataType, "un nombre leído por OCR no es una fórmula");
        Assert.AreEqual("=Elena", celda.GetString());
    }

    // ─────────────────────── lo que hace que sirva de hoja de cálculo ───────────────────────

    [TestMethod]
    public void LasFechasEntranComoFechaDeVerdadYNoComoTexto()
    {
        using var libro = Libro();
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(2, 5);

        Assert.AreEqual(XLDataType.DateTime, celda.DataType,
            "una cadena se ordena alfabéticamente; filtrar por mes necesita que Excel sepa que es una fecha");
        Assert.AreEqual(new DateTime(2026, 10, 15), celda.GetDateTime());
        Assert.AreEqual("yyyy-mm-dd", celda.Style.NumberFormat.Format,
            "sin formato escrito, la fecha se ve según la configuración regional de la máquina");
    }

    [TestMethod]
    public void UnaFechaQueNoSePuedeLeerSeDejaTalCualYNoSeInventa()
    {
        using var libro = Libro();
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(3, 5);

        Assert.AreEqual("el mes que viene", celda.GetString(), "una fecha inventada es peor que una fecha fea");
    }

    /// <summary>Un recuento entra como numero: sin eso, «sumar» no se puede.</summary>
    [TestMethod]
    public void UnRecuentoEntraComoNumeroYSeDejaSumar()
    {
        using var libro = Libro();
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(2, 6);

        Assert.AreEqual(XLDataType.Number, celda.DataType, "un «3» en texto no se suma");
        Assert.AreEqual(3, celda.GetDouble());
    }

    [TestMethod]
    public void UnaCeldaSinDatoSeQuedaVaciaYNoDiceNone()
    {
        using var libro = Libro();
        var celda = libro.Worksheet(HojaDeLaTabla).Cell(3, 3);

        Assert.IsTrue(celda.IsEmpty(), "un hueco es un hueco; «None» sería un dato que nadie escribió");
    }

    // ─────────────────────── que no se confunda con el Excel del compañero ───────────────────────

    /// <summary>
    /// El Excel del companero se rellena y VUELVE; este no vuelve. Se distinguen por dentro.
    /// </summary>
    /// <remarks>
    /// El dueno ya dijo el 2026-09-07 que el programa le confunde. La hoja «Por verificar»
    /// esta PROTEGIDA, lleva cinco filas de cabecera antes de la tabla, fondo amarillo en lo
    /// que el companero rellena y menus desplegables. El informe no tiene ninguna de las
    /// cuatro cosas, y ademas lo dice con letra en su primera hoja.
    /// </remarks>
    [TestMethod]
    public void NoSeParecePorDentroAlExcelQueElCompaneroRellenaYDevuelve()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet(HojaDeLaTabla);

        Assert.IsFalse(hoja.Protection.IsProtected, "la hoja del compañero va protegida; ésta no se rellena");
        Assert.AreEqual(0, hoja.DataValidations.Count(), "los menús de Sí/No son de la hoja que vuelve");
        Assert.AreEqual(XLFillPatternValues.None, hoja.Cell(2, 1).Style.Fill.PatternType,
            "el fondo de color es lo que le dice al compañero «esto lo rellenas tú»");
    }

    [TestMethod]
    public void LaPrimeraHojaDiceQueEsteArchivoNoSeDevuelve()
    {
        using var libro = Libro();
        var resumen = libro.Worksheet(NombreDeHoja.DelResumen);

        var todo = TextoDeLaHoja(resumen);
        StringAssert.Contains(todo, LibroDelInforme.QueEsEsteArchivo);
    }

    // ─────────────────────── la hoja de resumen ───────────────────────

    [TestMethod]
    public void ElResumenLlevaElTituloElPeriodoYCuandoSeGenero()
    {
        using var libro = Libro();
        var todo = TextoDeLaHoja(libro.Worksheet(NombreDeHoja.DelResumen));

        StringAssert.Contains(todo, ElInforme.Titulo);
        StringAssert.Contains(todo, ElInforme.Subtitulo);
        StringAssert.Contains(todo, ElInforme.GeneradoEn);
    }

    [TestMethod]
    public void ElResumenLlevaLaPortadaConSusCifras()
    {
        using var libro = Libro();
        var todo = TextoDeLaHoja(libro.Worksheet(NombreDeHoja.DelResumen));

        StringAssert.Contains(todo, ElInforme.Portada.Titular);
        StringAssert.Contains(todo, ElInforme.Portada.Frase);
        foreach (var cifra in ElInforme.Portada.Cifras)
        {
            StringAssert.Contains(todo, cifra.Rotulo);
            StringAssert.Contains(todo, cifra.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Lo que en el PDF es prosa —avisos, notas, la línea de resumen— vive aquí.</summary>
    /// <remarks>
    /// Y no en la hoja de la tabla: una nota encima de la fila de titulos rompe el autofiltro y
    /// obliga a quien filtra a saltarse tres filas. El PDF no pierde nada; el Excel gana la
    /// tabla limpia desde la fila 1.
    /// </remarks>
    [TestMethod]
    public void ElResumenLlevaLosAvisosLasNotasYElIndiceDeLasHojas()
    {
        using var libro = Libro();
        var todo = TextoDeLaHoja(libro.Worksheet(NombreDeHoja.DelResumen));

        StringAssert.Contains(todo, ElInforme.Avisos[0]);
        StringAssert.Contains(todo, LaTabla.Notas[0]);
        StringAssert.Contains(todo, LaTabla.Resumen!);
        StringAssert.Contains(todo, LaTabla.Titulo, "el título entero vive aquí porque en la pestaña no cabe");
        StringAssert.Contains(todo, HojaDeLaTabla);
    }

    // ─────────────────────── el archivo ───────────────────────

    [TestMethod]
    public void EnBytesDevuelveUnXlsxQueSeVuelveAAbrir()
    {
        var bytes = LibroDelInforme.EnBytes(ElInforme);

        Assert.IsGreaterThan(1000, bytes.Length);
        using var flujo = new MemoryStream(bytes);
        using var relectura = new XLWorkbook(flujo);
        Assert.AreEqual(3, relectura.Worksheets.Count);
        Assert.AreEqual(ElMrn, relectura.Worksheet(HojaDeLaTabla).Cell(2, 3).GetString());
    }

    [TestMethod]
    public void CuantasFilasLlevaCuentaLasDeTodasLasSeccionesYNoLasCabeceras()
        => Assert.AreEqual(2, LibroDelInforme.CuantasFilasLleva(ElInforme));

    // ─────────────────────── el andamio ───────────────────────

    /// <summary>Todo lo escrito en una hoja, junto, para poder buscar dentro sin fijar filas.</summary>
    private static string TextoDeLaHoja(IXLWorksheet hoja)
        => string.Join("\n", hoja.CellsUsed().Select(c => c.GetFormattedString()));
}
