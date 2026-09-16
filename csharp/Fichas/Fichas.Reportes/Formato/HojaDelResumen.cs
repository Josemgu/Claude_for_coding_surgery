using ClosedXML.Excel;
using Fichas.Reportes.Modelo;
using static Fichas.Reportes.Formato.PinturaDelResumen;

namespace Fichas.Reportes.Formato;

/// <summary>
/// El reporte del periodo como UNA hoja de Excel, celda a celda, tal como esta en el mockup v3.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-16 (<c>DECISIONES.md</c>, «EL REPORTE EN EXCEL ES UNA SOLA HOJA,
/// COMO EL MOCKUP v3»): <i>«El reporte debe ser bonito, profesional, en un solo worksheet, con
/// colores como el azul marino […] Así mismo es que quiero el reporte, como está en el
/// mockup.»</i> Sustituye entero a lo que <see cref="LibroDelInforme"/> hacia para el periodo
/// —una pestana «Resumen» con parrafos y una por seccion—, que es lo que el llamo «no
/// profesional». <see cref="LibroDelInforme"/> sigue escribiendo el historico y el informe de
/// agente, que no se pidieron.</para>
///
/// <para><b>Las celdas y sus medidas son las del mockup</b> (<c>mockups/mockup-v3-reporte-excel.html</c>,
/// «Lo que el programa tiene que escribir»): cabecera en A1:N2, cinco tarjetas en las filas 4 a
/// 6, «Por unidad» desde B8, «Por agente» desde J8, el grafico bajo la tabla de agentes y la
/// lista de solo los pendientes debajo de todo. Las filas de las tablas dependen de cuantas
/// unidades y agentes haya; lo demas es fijo. Sin parrafos: titulos, cifras y tablas.</para>
///
/// <para><b>El grafico no lo dibuja ClosedXML</b>, que no sabe: se anade despues del
/// <c>SaveAs</c> con el Open XML SDK en <see cref="GraficoDelResumen"/>.</para>
/// </remarks>
public static class HojaDelResumen
{
    /// <summary>Como se llama la unica pestana.</summary>
    public const string NombreDeLaHoja = "Reporte";

    /// <summary>El ancho de cada columna en caracteres de Excel, de la A a la N, medido en el mockup.</summary>
    private static readonly double[] Anchos = [2, 9, 24, 10, 11, 13, 22, 11, 2, 22, 11, 11, 13, 11];

    /// <summary>El tamano de letra corriente de la hoja.</summary>
    private const double LetraNormal = 11;

    /// <summary>Un centimetro de margen al imprimir, en pulgadas, que es como lo cuenta Excel.</summary>
    private const double MargenDeImpresion = 0.39;

    /// <summary>El archivo <c>.xlsx</c> entero, con el grafico dentro. No toca el disco.</summary>
    /// <param name="resumen">El resumen ya armado.</param>
    public static byte[] EnBytes(ResumenDelPeriodo resumen)
    {
        ArgumentNullException.ThrowIfNull(resumen);

        var sitio = DisposicionDelResumen.De(resumen);
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(NombreDeLaHoja);

        PrepararLaHoja(hoja);
        EscribirLaCabecera(hoja, resumen);
        EscribirLasTarjetas(hoja, resumen);
        TablasDelResumen.PorUnidad(hoja, resumen, sitio);
        TablasDelResumen.PorAgente(hoja, resumen, sitio);
        TablasDelResumen.Leyenda(hoja, sitio.FilaDeLaLeyenda);
        TituloDeSeccion(hoja, sitio.FilaDelTituloDelGrafico, "J", "N", "Viajaron por mes");
        PendientesDelResumen.Escribir(hoja, resumen, sitio);
        PrepararLaLecturaYLaImpresion(hoja, sitio.UltimaFila);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return GraficoDelResumen.Anadir(memoria.ToArray(), resumen.PorMes, sitio.PrimeraFilaDelGrafico, sitio.UltimaFilaDelGrafico);
    }

    // ---- la hoja ----------------------------------------------------------

    /// <summary>La letra de toda la hoja y los anchos fijos; sin <c>AdjustToContents</c>, el diseno es fijo.</summary>
    /// <param name="hoja">La hoja recien creada.</param>
    private static void PrepararLaHoja(IXLWorksheet hoja)
    {
        hoja.Style.Font.FontName = Letra;
        hoja.Style.Font.FontSize = LetraNormal;
        hoja.Style.Font.FontColor = Tinta;
        for (var columna = 1; columna <= Anchos.Length; columna++)
            hoja.Column(columna).Width = Anchos[columna - 1];
    }

    /// <summary>Las filas 1 y 2 fijas al desplazar, y la impresion: A4 apaisada a una pagina de ancho.</summary>
    /// <remarks>
    /// <c>FitToPages(1, 0)</c> es UNA pagina de ancho y las de alto que hagan falta: la lista de
    /// pendientes va en la segunda. Las filas 1 y 2 se repiten arriba de cada pagina.
    /// </remarks>
    /// <param name="hoja">La hoja ya escrita.</param>
    /// <param name="ultimaFila">Hasta donde llega lo escrito; cierra el area de impresion.</param>
    private static void PrepararLaLecturaYLaImpresion(IXLWorksheet hoja, int ultimaFila)
    {
        hoja.SheetView.FreezeRows(2);

        var impresion = hoja.PageSetup;
        impresion.PaperSize = XLPaperSize.A4Paper;
        impresion.PageOrientation = XLPageOrientation.Landscape;
        impresion.FitToPages(1, 0);
        impresion.PrintAreas.Clear();
        impresion.PrintAreas.Add($"A1:N{ultimaFila}");
        impresion.SetRowsToRepeatAtTop(1, 2);
        impresion.ShowGridlines = false;
        impresion.Margins.Left = MargenDeImpresion;
        impresion.Margins.Right = MargenDeImpresion;
        impresion.Margins.Top = MargenDeImpresion;
        impresion.Margins.Bottom = MargenDeImpresion;
    }

    // ---- la cabecera ------------------------------------------------------

    /// <summary>La banda marino A1:N2: titulo y periodo a la izquierda, templo y «generado el» a la derecha.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="resumen">El resumen, del que salen los cuatro textos.</param>
    private static void EscribirLaCabecera(IXLWorksheet hoja, ResumenDelPeriodo resumen)
    {
        Pintar(hoja.Range("A1:N2"), Marino, Blanco);
        hoja.Row(1).Height = 27;
        hoja.Row(2).Height = 18;
        hoja.Row(3).Height = 7.5;

        Escribir(hoja, "B1:H1", resumen.Titulo, 16, negrita: true, Blanco);
        Escribir(hoja, "I1:N1", resumen.Templo, 12, negrita: false, Blanco);
        Escribir(hoja, "B2:H2", $"{resumen.PeriodoEnTexto} · {Meses(resumen.Meses)}", 10, negrita: false, GrisClaro);
        Escribir(hoja, "I2:N2", $"Generado el {resumen.GeneradoEn[..Math.Min(10, resumen.GeneradoEn.Length)]}", 10, negrita: false, GrisClaro);
        hoja.Range("I1:N2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        // Con tres templos el nombre no cabe en I1:N1 a 12 puntos: se encoge antes que cortarse.
        hoja.Cell("I1").Style.Alignment.ShrinkToFit = true;
    }

    // ---- las tarjetas -----------------------------------------------------

    /// <summary>Las cinco tarjetas de cifras de las filas 4 a 6, en las columnas del mockup.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="resumen">El resumen, del que salen las cifras.</param>
    private static void EscribirLasTarjetas(IXLWorksheet hoja, ResumenDelPeriodo resumen)
    {
        hoja.Row(4).Height = 13.5;
        hoja.Row(5).Height = 33;
        hoja.Row(6).Height = 13.5;
        hoja.Row(7).Height = 7.5;

        Tarjeta(hoja, "B", "C", "VIAJARON", Marino, $"personas · {Meses(resumen.Meses)}")
            .SetValue(resumen.Viajaron);
        Tarjeta(hoja, "D", "F", "COMPLETOS", VerdeMarca, Porcentaje(resumen.Completos, resumen.Viajaron, "de las que viajaron"))
            .SetValue(resumen.Completos);
        Tarjeta(hoja, "G", "H", "SIN COMPLETAR", RojoMarca, Porcentaje(resumen.SinCompletar, resumen.Viajaron, Plural(resumen.UnidadesConPendientes, "unidad", "unidades")))
            .SetValue(resumen.SinCompletar);

        CifraDeDos(
            Tarjeta(hoja, "J", "K", "UNIDADES CON PENDIENTES", NaranjaMarca, $"{resumen.UnidadesSinAgente} sin agente"),
            resumen.UnidadesConPendientes, resumen.Unidades, NaranjaMarca);
        CifraDeDos(
            Tarjeta(hoja, "L", "N", "PAQUETES DEVUELTOS", Marino, $"{Plural(resumen.Agentes, "agente", "agentes")} · {resumen.AgentesSinPendientes} sin nada pendiente"),
            resumen.CasosDevueltos, resumen.CasosConAgente, Marino);
    }

    /// <summary>Una tarjeta: rotulo arriba, cifra grande en medio, detalle abajo, con su marco.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="desde">La primera columna de la tarjeta.</param>
    /// <param name="hasta">La ultima columna de la tarjeta.</param>
    /// <param name="rotulo">El rotulo, en mayusculas y pequeno.</param>
    /// <param name="color">El color de la cifra y de la raya de arriba.</param>
    /// <param name="detalle">La linea pequena de debajo de la cifra.</param>
    /// <returns>La celda de la cifra, para que quien llama ponga el numero.</returns>
    private static IXLCell Tarjeta(IXLWorksheet hoja, string desde, string hasta, string rotulo, XLColor color, string detalle)
    {
        var marco = hoja.Range($"{desde}4:{hasta}6");
        marco.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        marco.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D3D7DE");
        RayaEncima(hoja.Range($"{desde}4:{hasta}4"), XLBorderStyleValues.Thick, color);

        Escribir(hoja, $"{desde}4:{hasta}4", rotulo, 8, negrita: true, Apagado);
        Escribir(hoja, $"{desde}6:{hasta}6", detalle, 9, negrita: false, TintaSuave);

        var cifra = hoja.Range($"{desde}5:{hasta}5").Merge().FirstCell();
        cifra.Style.Font.FontSize = 24;
        cifra.Style.Font.Bold = true;
        cifra.Style.Font.FontColor = color;
        cifra.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        cifra.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        return cifra;
    }

    /// <summary>Una cifra «6 de 14»: el 6 grande y el «de 14» pequeno, en la misma celda.</summary>
    /// <param name="celda">La celda de la cifra.</param>
    /// <param name="cuantos">El numero grande.</param>
    /// <param name="deCuantos">El total, en pequeno.</param>
    /// <param name="color">El color del numero grande.</param>
    private static void CifraDeDos(IXLCell celda, int cuantos, int deCuantos, XLColor color)
    {
        var texto = celda.CreateRichText();
        texto.AddText(Numero(cuantos)).SetFontSize(24).SetBold().SetFontColor(color);
        texto.AddText($" de {Numero(deCuantos)}").SetFontSize(10).SetBold(false).SetFontColor(TintaSuave);
    }

    // ---- lo compartido ------------------------------------------------------

    /// <summary>Un titulo de seccion: letra marino en negrita con una raya media debajo.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    /// <param name="desde">La primera columna.</param>
    /// <param name="hasta">La ultima columna.</param>
    /// <param name="titulo">El titulo, corto.</param>
    internal static void TituloDeSeccion(IXLWorksheet hoja, int fila, string desde, string hasta, string titulo)
    {
        hoja.Row(fila).Height = 16.5;
        var direccion = $"{desde}{fila}:{hasta}{fila}";
        Escribir(hoja, direccion, titulo, 12, negrita: true, Marino);
        RayaDebajo(hoja.Range(direccion), XLBorderStyleValues.Medium, Marino);
    }

    /// <summary>Un texto en un rango fundido, con su tamano, su peso y su color. Siempre <c>SetValue</c>, nunca formula.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="rango">El rango, como «B1:H1».</param>
    /// <param name="texto">Lo que se escribe.</param>
    /// <param name="tamano">El tamano de letra.</param>
    /// <param name="negrita">Si va en negrita.</param>
    /// <param name="color">El color de la letra.</param>
    internal static void Escribir(IXLWorksheet hoja, string rango, string texto, double tamano, bool negrita, XLColor color)
    {
        var celda = hoja.Range(rango).Merge().FirstCell();
        celda.SetValue(texto);
        celda.Style.Font.FontSize = tamano;
        celda.Style.Font.Bold = negrita;
        celda.Style.Font.FontColor = color;
        celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    /// <summary>«76 % de las que viajaron», o solo el complemento cuando nadie viajo y no hay porcentaje que dar.</summary>
    /// <param name="parte">La parte.</param>
    /// <param name="todo">El total; con cero no se divide.</param>
    /// <param name="complemento">Lo que va detras del porcentaje.</param>
    private static string Porcentaje(int parte, int todo, string complemento)
        => todo == 0 ? complemento : $"{(int)Math.Round(100.0 * parte / todo)} % · {complemento}";

    /// <summary>«3 meses», o «1 mes».</summary>
    /// <param name="meses">Cuantos.</param>
    private static string Meses(int meses) => Plural(meses, "mes", "meses");

    /// <summary>Una cifra con su palabra en singular o plural.</summary>
    /// <param name="cuantos">La cifra.</param>
    /// <param name="singular">La palabra con uno.</param>
    /// <param name="plural">La palabra con los demas.</param>
    private static string Plural(int cuantos, string singular, string plural)
        => Reglas.Plural.Con(cuantos, singular, plural);

    /// <summary>Un entero escrito siempre igual, sin depender del idioma del sistema.</summary>
    /// <param name="valor">La cifra.</param>
    internal static string Numero(int valor) => valor.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
