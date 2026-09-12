using System.Globalization;
using ClosedXML.Excel;
using Fichas.Reportes.Modelo;

namespace Fichas.Reportes.Formato;

/// <summary>
/// El mismo informe del PDF, escrito como hoja de calculo: una pestana por seccion.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-07: <i>«está bien el de PDF, pero también quiero uno con
/// Excel»</i>. Sale del MISMO <see cref="Documento"/> que el PDF y no de un armado propio: con
/// dos armados, el dia que cambie una seccion uno de los dos se queda atras y nadie lo nota
/// hasta que el PDF y el Excel del mismo mes dicen cifras distintas.</para>
///
/// <para><b>Lo que cambia es la forma, y cambia por un motivo.</b> Un PDF es para ensenarselo
/// a alguien; una hoja de calculo es para filtrar, ordenar y sumar. Por eso cada pestana es
/// una tabla LIMPIA: la fila 1 son los rotulos, la 2 es la primera persona, y no hay ni un
/// titulo ni una nota encima. Una nota encima de los rotulos rompe el autofiltro y obliga a
/// saltarse tres filas para llegar al dato. Lo que en el PDF es prosa —el titular, los avisos,
/// las notas de cada seccion y su linea de resumen— vive entero en la pestana
/// <see cref="NombreDeHoja.DelResumen"/>, que ademas lleva el indice.</para>
///
/// <para>⚠️ <b>Este archivo NO se parece al Excel del companero, y es a proposito.</b> La hoja
/// «Por verificar» de <c>Fichas.Paquetes</c> se rellena y VUELVE al programa: va protegida,
/// con cinco filas de cabecera, fondo amarillo en lo que el companero escribe y menus de Sí/No.
/// Este no vuelve, asi que no lleva ninguna de las cuatro cosas y ademas lo dice con letra en
/// su primera pestana (<see cref="QueEsEsteArchivo"/>). El dueno dijo el 2026-09-07 que el
/// programa le confunde: dos Excel parecidos que se usan al reves es exactamente eso.</para>
///
/// <para>⚠️ <b>Y tampoco es el Excel espejo.</b> El espejo vuelca la base tal cual, con los
/// nombres de las columnas del esquema y los booleanos en 0/1; este dice las mismas palabras
/// que el PDF —«MRN», «¿Viajó?», «sí»— y cuenta lo que el informe cuenta.</para>
/// </remarks>
public static class LibroDelInforme
{
    /// <summary>La linea que separa este archivo del que el companero rellena y devuelve.</summary>
    public const string QueEsEsteArchivo =
        "Este archivo NO se rellena ni se devuelve al programa: es el mismo informe del PDF, "
        + "para filtrar, ordenar y sumar.";

    /// <summary>Como se lee: cada pestana es una tabla y su fila 1 son los rotulos.</summary>
    public const string ComoSeLee =
        "Cada pestaña es una tabla: la fila 1 son los títulos y está fija, con el filtro puesto.";

    /// <summary>El formato de texto, que es lo que impide que Excel se coma un cero de delante.</summary>
    private const string FormatoDeTexto = "@";

    /// <summary>Como se estampa una fecha; el mismo que pone el espejo y la hoja del companero.</summary>
    private const string FormatoDeFechaEnExcel = "yyyy-mm-dd";

    /// <summary>Como llega una fecha dentro del documento.</summary>
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <summary>Lo mas largo que puede ser un entero para escribirse como numero.</summary>
    /// <remarks>Nueve digitos caben de sobra en un recuento y no llegan a desbordar un <c>int</c>.</remarks>
    private const int DigitosDeUnRecuento = 9;

    /// <summary>La fila de la primera persona en cada pestaña de tabla: la 1 son los rótulos.</summary>
    private const int PrimeraFilaDeDatos = 2;

    /// <summary>Cuantas filas de datos lleva el informe entero, sin contar cabeceras.</summary>
    /// <remarks>Se dice en el aviso: es lo que se puede comprobar abriendo el archivo.</remarks>
    /// <param name="documento">El documento armado.</param>
    public static int CuantasFilasLleva(Documento documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return documento.Secciones.Sum(seccion => seccion.Filas.Count);
    }

    /// <summary>El libro entero. Quien lo recibe lo cierra; no toca el disco.</summary>
    /// <param name="documento">El documento armado, el mismo que va al PDF.</param>
    /// <returns>Una pestaña de resumen y una por sección, en el orden del documento.</returns>
    public static XLWorkbook Construir(Documento documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var libro = new XLWorkbook();
        var pestanas = NombreDeHoja.DeLasSecciones(documento.Secciones);

        EscribirElResumen(libro.AddWorksheet(NombreDeHoja.DelResumen), documento, pestanas);
        for (var indice = 0; indice < documento.Secciones.Count; indice++)
            EscribirLaSeccion(libro.AddWorksheet(pestanas[indice]), documento.Secciones[indice]);

        return libro;
    }

    /// <summary>El libro ya serializado, listo para escribirse de un golpe.</summary>
    /// <remarks>
    /// Contra un flujo en memoria y no contra la ruta, por lo mismo que el espejo: ClosedXML
    /// rechaza por extension cualquier archivo que no acabe en <c>.xlsx</c>, y quien escribe
    /// pasa antes por un <c>.parcial</c>.
    /// </remarks>
    /// <param name="documento">El documento armado.</param>
    public static byte[] EnBytes(Documento documento)
    {
        using var libro = Construir(documento);
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    // ─────────────────────── la pestaña de resumen ───────────────────────

    /// <summary>
    /// Lo que en el PDF es portada y prosa: de que es el informe, sus cifras y el indice.
    /// </summary>
    /// <remarks>
    /// Va en dos columnas y no en una tabla con autofiltro: no es una tabla de datos, es la
    /// caratula. Las cifras de la portada SÍ entran como numeros, que es lo que permite
    /// comprobarlas contra las hojas de al lado sin volver a teclearlas.
    /// </remarks>
    /// <param name="hoja">La pestaña de resumen, recién creada y vacía.</param>
    /// <param name="documento">El documento armado.</param>
    /// <param name="pestanas">El nombre de la pestaña de cada sección, en su orden, para el índice.</param>
    private static void EscribirElResumen(
        IXLWorksheet hoja, Documento documento, IReadOnlyList<string> pestanas)
    {
        var fila = 1;

        Rotulo(hoja, fila++, documento.Titulo, negrita: true);
        Rotulo(hoja, fila++, documento.Subtitulo);
        Rotulo(hoja, fila++, $"Generado el {documento.GeneradoEn}");
        Rotulo(hoja, fila++, QueEsEsteArchivo);
        Rotulo(hoja, fila++, ComoSeLee);
        fila++;

        Rotulo(hoja, fila++, documento.Portada.Titular, negrita: true);
        Rotulo(hoja, fila++, documento.Portada.Frase);
        foreach (var cifra in documento.Portada.Cifras)
        {
            hoja.Cell(fila, 1).SetValue(cifra.Rotulo);
            hoja.Cell(fila, 2).Value = cifra.Numero;
            fila++;
        }
        fila++;

        if (documento.Avisos.Count > 0)
        {
            Rotulo(hoja, fila++, "De qué no se fían estos números", negrita: true);
            foreach (var aviso in documento.Avisos) Rotulo(hoja, fila++, aviso);
            fila++;
        }

        Rotulo(hoja, fila++, "Qué hay en cada pestaña", negrita: true);
        for (var indice = 0; indice < documento.Secciones.Count; indice++)
            fila = EscribirLaEntradaDelIndice(hoja, fila, pestanas[indice], documento.Secciones[indice]);

        hoja.Column(1).Width = 60;
        hoja.Column(2).Width = 12;
    }

    /// <summary>Una seccion en el indice: su pestana, su titulo entero, sus notas y su resumen.</summary>
    /// <param name="hoja">La pestaña de resumen.</param>
    /// <param name="fila">La fila donde empieza esta entrada.</param>
    /// <param name="pestana">El nombre de la pestaña de la sección, en negrita en la columna 1.</param>
    /// <param name="seccion">La sección; su título, notas y resumen van en la columna 2.</param>
    /// <returns>La fila donde empieza la entrada siguiente, dejando una en blanco.</returns>
    private static int EscribirLaEntradaDelIndice(IXLWorksheet hoja, int fila, string pestana, Seccion seccion)
    {
        hoja.Cell(fila, 1).SetValue(pestana);
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 2).SetValue(seccion.Titulo);
        fila++;

        foreach (var nota in seccion.Notas)
        {
            hoja.Cell(fila, 2).SetValue(nota);
            fila++;
        }

        if (!string.IsNullOrWhiteSpace(seccion.Resumen))
        {
            hoja.Cell(fila, 2).SetValue(seccion.Resumen);
            fila++;
        }

        return fila + 1;
    }

    /// <summary>Una linea suelta de la caratula, en la columna 1.</summary>
    /// <param name="hoja">La pestaña de resumen.</param>
    /// <param name="fila">En qué fila.</param>
    /// <param name="texto">Lo que se escribe; entra con <c>SetValue</c>, nunca como fórmula.</param>
    /// <param name="negrita">Si va en negrita.</param>
    private static void Rotulo(IXLWorksheet hoja, int fila, string texto, bool negrita = false)
    {
        var celda = hoja.Cell(fila, 1);
        celda.SetValue(texto);
        if (negrita) celda.Style.Font.Bold = true;
    }

    // ─────────────────────── una pestaña de tabla ───────────────────────

    /// <summary>La tabla de una seccion: rotulos en la fila 1, filtro puesto y fila 1 fija.</summary>
    /// <remarks>
    /// Una seccion sin filas conserva su pestana con la cabecera: dice «esto existe y esta
    /// vacio», que no es lo mismo que no tener la pestana. Es la misma decision que el espejo.
    /// </remarks>
    /// <param name="hoja">La pestaña de la sección, recién creada y vacía.</param>
    /// <param name="seccion">La sección; el ancho de cada columna del documento se usa tal cual como ancho de Excel.</param>
    private static void EscribirLaSeccion(IXLWorksheet hoja, Seccion seccion)
    {
        for (var numero = 1; numero <= seccion.Columnas.Count; numero++)
        {
            var celda = hoja.Cell(1, numero);
            celda.SetValue(seccion.Columnas[numero - 1].Nombre);
            celda.Style.Font.Bold = true;
            hoja.Column(numero).Width = seccion.Columnas[numero - 1].Ancho;
        }

        for (var indice = 0; indice < seccion.Filas.Count; indice++)
        {
            var valores = seccion.Filas[indice];
            for (var numero = 1; numero <= seccion.Columnas.Count; numero++)
            {
                EscribirCelda(
                    hoja.Cell(PrimeraFilaDeDatos + indice, numero),
                    numero <= valores.Count ? valores[numero - 1] : null,
                    seccion.Columnas[numero - 1].Clase);
            }
        }

        hoja.SheetView.FreezeRows(1);
        hoja.Range(1, 1, Math.Max(PrimeraFilaDeDatos - 1 + seccion.Filas.Count, 1), seccion.Columnas.Count)
            .SetAutoFilter();
    }

    /// <summary>
    /// Mete un valor en su celda segun la clase de su columna.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>La regla que hace imposible que Excel se coma un cero de delante:</b> lo
    /// unico que entra como NUMERO es un entero pelado que no empieza por cero. Todo lo demas
    /// entra con <c>SetValue(string)</c> y con formato <c>@</c>, asi que
    /// <c>055-1111-3853</c> y <c>0700016</c> —tanto solos como dentro de «Cuatricentenaria ·
    /// 0700016»— salen tal como estan. Un MRN sin sus ceros deja de identificar a nadie.</para>
    ///
    /// <para>Los recuentos entran como numero a proposito: sin eso, «sumar» no se puede, y
    /// sumar es la mitad de para que sirve una hoja de calculo.</para>
    ///
    /// <para>Una fecha que no se puede leer se deja tal cual: no se adivina ni se descarta
    /// (regla permanente 1). Y todo texto entra con <c>SetValue</c> y no con <c>Value =</c>,
    /// que deja que ClosedXML adivine el tipo: un nombre leido por OCR que empiece por «=» no
    /// es una formula.</para>
    /// </remarks>
    /// <param name="celda">La celda destino.</param>
    /// <param name="valor">El texto tal como está en el documento; nulo o vacío deja la celda sin tocar.</param>
    /// <param name="clase">La clase de la columna, que decide si puede entrar como fecha o como número.</param>
    private static void EscribirCelda(IXLCell celda, string? valor, ClaseDeColumna clase)
    {
        if (string.IsNullOrEmpty(valor)) return;

        if (clase == ClaseDeColumna.Temporal
            && DateTime.TryParseExact(valor, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            celda.Value = fecha;
            celda.Style.NumberFormat.Format = FormatoDeFechaEnExcel;
            return;
        }

        if (clase == ClaseDeColumna.Crudo && EsUnRecuentoQueSeDejaSumar(valor))
        {
            celda.Value = int.Parse(valor, CultureInfo.InvariantCulture);
            return;
        }

        celda.Style.NumberFormat.Format = FormatoDeTexto;
        celda.SetValue(valor);
    }

    /// <summary>Si ese texto es un entero pelado que se puede escribir como numero sin perder nada.</summary>
    /// <remarks>
    /// Un valor que empieza por cero queda FUERA aunque sean todo digitos, y ahi esta la
    /// defensa: <c>0700016</c> no es un recuento, es un numero de unidad, y como numero
    /// perderia el cero.
    /// </remarks>
    /// <param name="valor">El texto de la celda, no vacío.</param>
    private static bool EsUnRecuentoQueSeDejaSumar(string valor)
    {
        if (valor.Length > DigitosDeUnRecuento) return false;
        if (!valor.All(char.IsAsciiDigit)) return false;
        return valor.Length == 1 || valor[0] != '0';
    }
}
