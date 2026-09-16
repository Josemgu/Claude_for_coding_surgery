using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Fichas.Reportes.Modelo;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Fichas.Reportes.Formato;

/// <summary>
/// El grafico de barras por mes, anadido al <c>.xlsx</c> ya guardado con el Open XML SDK.
/// </summary>
/// <remarks>
/// <para>ClosedXML no dibuja graficos. Se sigue el ejemplo de Microsoft Learn «How to: Insert a
/// chart into a spreadsheet document (Open XML SDK)», <c>ms.date 2025-01-14</c>, citado por el
/// disenador en la nota del mockup v3: un <c>DrawingsPart</c> con un <c>ChartPart</c>, un
/// <c>BarChart</c> de columnas agrupadas y un <c>TwoCellAnchor</c> que lo cuelga de la hoja. El
/// articulo avisa «this code can be run only once», y por eso esto corre una sola vez, sobre
/// los bytes que ClosedXML acaba de guardar, y ClosedXML no vuelve a abrir el resultado.</para>
///
/// <para><b>Los valores van literales</b> (<c>c:numLit</c>), no leidos de celdas: la hoja no
/// tiene una tabla por mes, y el grafico dice exactamente lo que conto el resumen. Es un objeto
/// de Excel de verdad —se pulsa, se recolorea, se copia a Word—, que es lo que el dueno llamo
/// «gráfico» en un reporte «profesional».</para>
///
/// <para><b>Regla 3 de CLAUDE.md:</b> el paquete <c>DocumentFormat.OpenXml</c> 3.1.1 ya viajaba
/// con ClosedXML 0.105.1; referenciarlo aqui no anade ni un archivo a lo publicado. Medido en la
/// carpeta publicada de master: <c>DocumentFormat.OpenXml.dll</c> y <c>.Framework.dll</c>
/// estaban antes de esta clase.</para>
/// </remarks>
public static class GraficoDelResumen
{
    /// <summary>El verde de las barras «Completos», el del mockup.</summary>
    private const string Verde = "1B6E3C";

    /// <summary>El rojo de las barras «Con dificultades», el del mockup.</summary>
    private const string Rojo = "B3261E";

    /// <summary>La columna J, base 0, donde empieza el grafico.</summary>
    private const int ColumnaDesde = 9;

    /// <summary>La columna O, base 0: el grafico acaba donde empieza, o sea al final de la N.</summary>
    private const int ColumnaHasta = 14;

    /// <summary>El id del eje de categorias; cualquier entero distinto del otro.</summary>
    private const uint EjeDeMeses = 1;

    /// <summary>El id del eje de valores.</summary>
    private const uint EjeDeCifras = 2;

    /// <summary>Anade el grafico al archivo y devuelve los bytes nuevos.</summary>
    /// <param name="xlsx">El <c>.xlsx</c> tal como lo guardo ClosedXML, con una sola hoja.</param>
    /// <param name="meses">Una barra doble por mes, en orden.</param>
    /// <param name="primeraFila">La primera fila, base 1, sobre la que flota.</param>
    /// <param name="ultimaFila">La ultima fila, base 1, sobre la que flota.</param>
    public static byte[] Anadir(byte[] xlsx, IReadOnlyList<MesDelResumen> meses, int primeraFila, int ultimaFila)
    {
        ArgumentNullException.ThrowIfNull(xlsx);
        ArgumentNullException.ThrowIfNull(meses);

        using var memoria = new MemoryStream();
        memoria.Write(xlsx);

        using (var documento = SpreadsheetDocument.Open(memoria, true))
        {
            var hoja = documento.WorkbookPart!.WorksheetParts.First();
            var dibujos = hoja.AddNewPart<DrawingsPart>();
            var grafico = dibujos.AddNewPart<ChartPart>();

            grafico.ChartSpace = EspacioDelGrafico(meses);
            dibujos.WorksheetDrawing = Anclaje(dibujos.GetIdOfPart(grafico), primeraFila, ultimaFila);
            ColgarDeLaHoja(hoja, SinTriangulosVerdes());
            ColgarDeLaHoja(hoja, new Drawing { Id = hoja.GetIdOfPart(dibujos) });
        }

        return memoria.ToArray();
    }

    // ---- el grafico ---------------------------------------------------------

    /// <summary>Todo el <c>chart1.xml</c>: barras agrupadas, dos series, ejes y leyenda abajo.</summary>
    /// <param name="meses">Una barra doble por mes.</param>
    private static C.ChartSpace EspacioDelGrafico(IReadOnlyList<MesDelResumen> meses)
    {
        var barras = new C.BarChart(
            new C.BarDirection { Val = C.BarDirectionValues.Column },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
            new C.VaryColors { Val = false },
            Serie(0, "Completos", Verde, meses, m => m.Completos),
            Serie(1, "Con dificultades", Rojo, meses, m => m.ConDificultades),
            new C.GapWidth { Val = 80 },
            new C.AxisId { Val = EjeDeMeses },
            new C.AxisId { Val = EjeDeCifras });

        var grafico = new C.Chart(
            new C.AutoTitleDeleted { Val = true },
            new C.PlotArea(new C.Layout(), barras, EjeDeCategorias(), EjeDeValores()),
            new C.Legend(new C.LegendPosition { Val = C.LegendPositionValues.Bottom }, new C.Overlay { Val = false }),
            new C.PlotVisibleOnly { Val = true },
            new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap });

        return new C.ChartSpace(
            new C.EditingLanguage { Val = "es-ES" },
            new C.RoundedCorners { Val = false },
            grafico);
    }

    /// <summary>Una serie: su nombre, su color, los meses como categorias y las cifras como valores literales.</summary>
    /// <param name="indice">0 o 1.</param>
    /// <param name="nombre">Lo que dice la leyenda.</param>
    /// <param name="color">El relleno de sus barras, en hexadecimal sin almohadilla.</param>
    /// <param name="meses">Una barra doble por mes.</param>
    /// <param name="cifra">Que cifra de cada mes lleva esta serie.</param>
    private static C.BarChartSeries Serie(
        uint indice, string nombre, string color, IReadOnlyList<MesDelResumen> meses, Func<MesDelResumen, int> cifra)
        => new(
            new C.Index { Val = indice },
            new C.Order { Val = indice },
            new C.SeriesText(new C.NumericValue(nombre)),
            new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = color })),
            new C.InvertIfNegative { Val = false },
            EtiquetasSobreLasBarras(),
            new C.CategoryAxisData(LosMeses(meses)),
            new C.Values(LasCifras(meses, cifra)));

    /// <summary>Los rotulos de los meses como literales de texto, uno por punto.</summary>
    /// <param name="meses">Una barra doble por mes.</param>
    private static C.StringLiteral LosMeses(IReadOnlyList<MesDelResumen> meses)
    {
        var literal = new C.StringLiteral(new C.PointCount { Val = (uint)meses.Count });
        for (var i = 0; i < meses.Count; i++)
            literal.Append(new C.StringPoint(new C.NumericValue(meses[i].Rotulo)) { Index = (uint)i });
        return literal;
    }

    /// <summary>Las cifras de una serie como literales numericos, uno por punto.</summary>
    /// <param name="meses">Una barra doble por mes.</param>
    /// <param name="cifra">Que cifra de cada mes lleva la serie.</param>
    private static C.NumberLiteral LasCifras(IReadOnlyList<MesDelResumen> meses, Func<MesDelResumen, int> cifra)
    {
        var literal = new C.NumberLiteral(new C.FormatCode("General"), new C.PointCount { Val = (uint)meses.Count });
        for (var i = 0; i < meses.Count; i++)
            literal.Append(new C.NumericPoint(new C.NumericValue(HojaDelResumen.Numero(cifra(meses[i])))) { Index = (uint)i });
        return literal;
    }

    /// <summary>La cifra encima de cada barra, y nada mas.</summary>
    private static C.DataLabels EtiquetasSobreLasBarras()
        => new(
            new C.DataLabelPosition { Val = C.DataLabelPositionValues.OutsideEnd },
            new C.ShowLegendKey { Val = false },
            new C.ShowValue { Val = true },
            new C.ShowCategoryName { Val = false },
            new C.ShowSeriesName { Val = false },
            new C.ShowPercent { Val = false },
            new C.ShowBubbleSize { Val = false });

    /// <summary>El eje de abajo, con los meses.</summary>
    private static C.CategoryAxis EjeDeCategorias()
        => new(
            new C.AxisId { Val = EjeDeMeses },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
            new C.MajorTickMark { Val = C.TickMarkValues.Outside },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = EjeDeCifras },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.AutoLabeled { Val = true },
            new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
            new C.LabelOffset { Val = 100 },
            new C.NoMultiLevelLabels { Val = false });

    /// <summary>El eje de la izquierda, con las cifras y sus lineas de guia.</summary>
    private static C.ValueAxis EjeDeValores()
        => new(
            new C.AxisId { Val = EjeDeCifras },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Left },
            new C.MajorGridlines(),
            new C.NumberingFormat { FormatCode = "General", SourceLinked = true },
            new C.MajorTickMark { Val = C.TickMarkValues.Outside },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = EjeDeMeses },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.CrossBetween { Val = C.CrossBetweenValues.Between });

    // ---- los triangulos verdes ----------------------------------------------

    /// <summary>
    /// Le dice a Excel que no marque «número guardado como texto» en las columnas de numeros de unidad y de caso.
    /// </summary>
    /// <remarks>
    /// El numero de unidad y el de caso van como texto A PROPOSITO —un cero de delante es parte
    /// del dato—, y Excel les pone un triangulo verde en la esquina a cada uno. En 193 filas son
    /// 193 triangulos sobre un reporte que tiene que ser «bonito, profesional». ClosedXML no
    /// escribe <c>ignoredErrors</c>; el SDK si. Las columnas enteras, para no depender de
    /// cuantas filas haya.
    /// </remarks>
    private static IgnoredErrors SinTriangulosVerdes()
        => new(new IgnoredError
        {
            NumberStoredAsText = true,
            SequenceOfReferences = new ListValue<StringValue> { InnerText = "B1:B1048576 L1:L1048576" },
        });

    // ---- donde cuelga -------------------------------------------------------

    /// <summary>El <c>drawing1.xml</c>: el marco del grafico anclado entre dos celdas de la hoja.</summary>
    /// <remarks>Los marcadores van en base 0 y el «hasta» es exclusivo: la fila de abajo es la siguiente a la ultima.</remarks>
    /// <param name="idDelGrafico">El id de relacion del <c>ChartPart</c> dentro del <c>DrawingsPart</c>.</param>
    /// <param name="primeraFila">La primera fila, base 1, sobre la que flota.</param>
    /// <param name="ultimaFila">La ultima fila, base 1, sobre la que flota.</param>
    private static Xdr.WorksheetDrawing Anclaje(string idDelGrafico, int primeraFila, int ultimaFila)
        => new(new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId(HojaDelResumen.Numero(ColumnaDesde)), new Xdr.ColumnOffset("0"),
                new Xdr.RowId(HojaDelResumen.Numero(primeraFila - 1)), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId(HojaDelResumen.Numero(ColumnaHasta)), new Xdr.ColumnOffset("0"),
                new Xdr.RowId(HojaDelResumen.Numero(ultimaFila)), new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Viajaron por mes" },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = 0, Cy = 0 }),
                new A.Graphic(new A.GraphicData(new C.ChartReference { Id = idDelGrafico })
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart",
                }))
            { Macro = string.Empty },
            new Xdr.ClientData())
        { EditAs = Xdr.EditAsValues.OneCell });

    /// <summary>
    /// Pone un elemento en su sitio del <c>sheet1.xml</c>, que no es al final.
    /// </summary>
    /// <remarks>
    /// El esquema de la hoja ordena sus hijos: <c>ignoredErrors</c> y <c>drawing</c> van detras
    /// de la impresion y delante de <c>legacyDrawing</c>, <c>tableParts</c> y <c>extLst</c>.
    /// Anadirlo al final, como hace el ejemplo de Microsoft, vale mientras ClosedXML no escriba
    /// ninguno de esos; buscar el primero que vaya detras y meterlo delante vale siempre. Se
    /// cuelgan en el orden del esquema: primero <c>ignoredErrors</c>, luego <c>drawing</c>.
    /// </remarks>
    /// <param name="hoja">La parte de la hoja.</param>
    /// <param name="elemento">Lo que se cuelga: los errores ignorados o el dibujo.</param>
    private static void ColgarDeLaHoja(WorksheetPart hoja, OpenXmlElement elemento)
    {
        var siguiente = hoja.Worksheet.ChildElements.FirstOrDefault(hijo => VaDetrasDe(elemento, hijo));
        if (siguiente is null) hoja.Worksheet.Append(elemento);
        else hoja.Worksheet.InsertBefore(elemento, siguiente);
        hoja.Worksheet.Save();
    }

    /// <summary>Si ese hijo de la hoja va, segun el esquema, detras del elemento que se cuelga.</summary>
    /// <param name="elemento">Lo que se cuelga.</param>
    /// <param name="hijo">Un hijo directo de <c>&lt;worksheet&gt;</c>.</param>
    private static bool VaDetrasDe(OpenXmlElement elemento, OpenXmlElement hijo)
        => hijo is LegacyDrawing or LegacyDrawingHeaderFooter or DrawingHeaderFooter or Picture
            or OleObjects or Controls or WebPublishItems or TableParts or WorksheetExtensionList
           || (elemento is IgnoredErrors && hijo is Drawing);
}
