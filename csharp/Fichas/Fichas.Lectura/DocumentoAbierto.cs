using Fichas.Contratos.Lectura;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Tokens;

namespace Fichas.Lectura;

/// <summary>
/// Un PDF abierto UNA vez para leerlo entero: los bytes del archivo, el documento de PdfPig
/// y, a partir de ellos, el tamaño, las anotaciones y el mapa de bits de cada hoja.
/// </summary>
/// <remarks>
/// <para>Existe por el plan R-3 del 2026-09-15. Antes, leer un PDF de seis hojas abría el
/// archivo con PdfPig diecinueve veces (una para contar y tres por hoja: tamaño para
/// rasterizar, anotaciones y tamaño para la extracción) y lo leía entero del disco seis
/// veces. Aquí se abre una vez y se lee del disco una vez; cada pregunta sobre una hoja
/// se contesta desde lo que ya está en memoria.</para>
///
/// <para>Es de un solo hilo, como el <see cref="PdfDocument"/> que envuelve. Quien lo
/// abre lo desecha; mientras vive retiene los bytes del archivo (0,6 a 3 MB en el corpus)
/// y lo que PdfPig haya analizado.</para>
///
/// <para>Es interno a <c>Fichas.Lectura</c> a propósito: el contrato
/// <c>ILecturaDePdf</c> está congelado y sigue trabajando por ruta y número de hoja;
/// este es el camino de dentro, el que usa <see cref="LectorDeFormularios"/>.</para>
/// </remarks>
internal sealed class DocumentoAbierto : IDisposable
{
    /// <summary>El archivo entero, leído del disco una sola vez; es lo que PDFium rasteriza.</summary>
    private readonly byte[] _bytes;

    /// <summary>El documento analizado por PdfPig, de donde salen hojas, tamaños, anotaciones y campos.</summary>
    private readonly PdfDocument _documento;

    /// <summary>Envuelve lo ya abierto; se construye solo desde <see cref="Abrir"/>.</summary>
    /// <param name="bytes">Los bytes del archivo.</param>
    /// <param name="documento">El documento de PdfPig abierto sobre esos mismos bytes.</param>
    private DocumentoAbierto(byte[] bytes, PdfDocument documento)
    {
        _bytes = bytes;
        _documento = documento;
    }

    /// <summary>Cuántas hojas tiene el documento.</summary>
    public int Paginas => _documento.NumberOfPages;

    /// <summary>
    /// Lee el archivo del disco y lo abre con PdfPig; nulo si no se pudo, sin lanzar.
    /// </summary>
    /// <remarks>
    /// Un PDF roto o un archivo que no es PDF devuelven nulo, que quien llama convierte en
    /// su renglón de ilegible: aquí no se tumba una tanda. Lo único que sube es la falta
    /// de memoria, que no es un archivo malo.
    /// </remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco.</param>
    /// <returns>El documento abierto, o nulo si no se pudo leer o analizar.</returns>
    public static DocumentoAbierto? Abrir(string rutaPdf)
    {
        try
        {
            var bytes = File.ReadAllBytes(rutaPdf);
            return new DocumentoAbierto(bytes, PdfDocument.Open(bytes));
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Cierto si el número de hoja existe en este documento (base 1).</summary>
    /// <param name="pagina">Número de hoja, base 1.</param>
    public bool TieneLaHoja(int pagina) => pagina >= 1 && pagina <= Paginas;

    /// <summary>El tamaño de la hoja en puntos PDF; nulo si la hoja no existe o no se pudo leer.</summary>
    /// <remarks>
    /// Hace falta para dos cosas: calcular la escala del rasterizado y saber la relación
    /// de aspecto que las bandas necesitan.
    /// </remarks>
    /// <param name="pagina">Número de hoja, base 1; fuera de rango devuelve nulo, no lanza.</param>
    public (double AnchoPuntos, double AltoPuntos)? TamanoDeLaHoja(int pagina)
    {
        if (!TieneLaHoja(pagina)) return null;
        try
        {
            var hoja = _documento.GetPage(pagina);
            return (hoja.Width, hoja.Height);
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rasteriza una hoja con PDFium a un mapa de bits cuyo lado largo cabe en
    /// <paramref name="ladoLargoMaximo"/>; nulo si la hoja no existe o no se pudo.
    /// </summary>
    /// <remarks>
    /// <para>El tope es del LADO LARGO, no del ancho: es lo que hace la regla de no
    /// regresión de los 3 500 px, y en estas hojas verticales el lado largo es el alto.
    /// La escala se baja al <c>double</c> inmediatamente anterior
    /// (<see cref="Geometria.EscalaDeRasterizado"/>) para que el mapa salga justo en el
    /// tope y no uno por encima.</para>
    ///
    /// <para>⚠️ <b><c>WithFormFill</c> va a cierto, y no es un adorno</b> (2026-09-10).
    /// PDFium no pinta los campos de un formulario rellenable con solo
    /// <c>WithAnnotations</c>: hace falta el entorno de relleno de formulario. Medido
    /// sobre el PDF del dueño: la casilla del nombre salía con <b>0 de 58 011</b> píxeles
    /// oscuros sin esta opción y con 4 431 con ella. En un escaneo sin formulario no
    /// cambia ni un píxel: comprobado por SHA-256 del PNG sobre los diez documentos
    /// escaneados.</para>
    ///
    /// <para>El mapa que vuelve es el de PDFium tal cual, sin pasar por PNG: es el que
    /// recibe el OCR (plan R-3), y <c>PruebaDeLaImagenQueVeElOcr</c> fija que sus bytes son
    /// los mismos que daba el camino con PNG. Quien lo recibe lo desecha: son 36 MiB por
    /// hoja a 3 500 px.</para>
    /// </remarks>
    /// <param name="pagina">Número de hoja, base 1.</param>
    /// <param name="ladoLargoMaximo">Tope en píxeles del lado largo.</param>
    /// <returns>El mapa de bits, o nulo si la hoja no existe o PDFium no pudo con ella.</returns>
    public SKBitmap? RasterizarHoja(int pagina, int ladoLargoMaximo)
    {
        var tamano = TamanoDeLaHoja(pagina);
        if (tamano is null) return null;

        try
        {
            double escala = Geometria.EscalaDeRasterizado(tamano.Value.AnchoPuntos, tamano.Value.AltoPuntos, ladoLargoMaximo);
            int anchoPx = (int)Math.Ceiling(tamano.Value.AnchoPuntos * escala);
            int altoPx = (int)Math.Ceiling(tamano.Value.AltoPuntos * escala);

            return Conversion.ToImage(
                _bytes,
                page: new Index(pagina - 1),
                options: new RenderOptions(Width: anchoPx, Height: altoPx, WithAnnotations: true, WithFormFill: true));
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Las anotaciones de una hoja: lo que alguien escribió o dibujó encima del papel.</summary>
    /// <remarks>
    /// Salen las <c>/FreeText</c>, las <c>/Ink</c> y, desde el 2026-09-10, los campos de
    /// TEXTO del formulario rellenable —<c>/Widget</c> de tipo <c>/Tx</c>—, que es donde un
    /// formulario rellenado en el ordenador lleva tecleados el nombre, la cédula, las fechas
    /// y el templo. Los demás subtipos se ignoran a propósito: un <c>/Link</c> o un
    /// <c>/Popup</c> no dicen nada del formulario, y las casillas (<c>/Btn</c>) no entran
    /// todavía: la App no tiene por dónde guardarlas. Lo que NO se ignora es un <c>/Ink</c>
    /// que no se sepa clasificar; ese vuelve igual y <see cref="Anotaciones.ClaseDe"/> lo
    /// llama desconocido.
    /// </remarks>
    /// <param name="pagina">Número de hoja, base 1.</param>
    /// <returns>Las anotaciones con su banda en fracciones; vacía si la hoja no existe, no tiene o no se pudo leer. Nunca nulo.</returns>
    public IReadOnlyList<AnotacionDelPdf> AnotacionesDeLaHoja(int pagina)
    {
        if (!TieneLaHoja(pagina)) return [];
        try
        {
            var hoja = _documento.GetPage(pagina);
            var leidas = new List<AnotacionDelPdf>(CamposDeTextoDelFormulario(pagina, hoja.Width, hoja.Height));

            foreach (var anotacion in hoja.GetAnnotations())
            {
                string subtipo = anotacion.Type.ToString();
                if (subtipo != Anotaciones.SubtipoDeTexto && subtipo != Anotaciones.SubtipoDeTrazo) continue;

                var rectangulo = anotacion.Rectangle;
                var banda = Geometria.RectanguloPdfAFracciones(
                    rectangulo.Left, rectangulo.Bottom, rectangulo.Right, rectangulo.Top, hoja.Width, hoja.Height);

                var (rojo, verde, azul) = ColorDe(anotacion.AnnotationDictionary);
                leidas.Add(new AnotacionDelPdf(
                    Subtipo: subtipo,
                    Texto: string.IsNullOrEmpty(anotacion.Content) ? null : anotacion.Content,
                    Banda: banda,
                    Rojo: rojo,
                    Verde: verde,
                    Azul: azul,
                    Grosor: GrosorDe(anotacion.AnnotationDictionary)));
            }
            return leidas;
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return [];
        }
    }

    /// <summary>
    /// Los campos de texto del formulario que caen en esta hoja, vacíos incluidos.
    /// </summary>
    /// <remarks>
    /// Se leen por el <c>AcroForm</c> del documento y no por el diccionario de cada
    /// <c>/Widget</c>, porque el tipo y el valor de un campo pueden venir HEREDADOS de su
    /// padre (<c>/Parent</c>), y PdfPig resuelve esa herencia al construir el árbol. Un
    /// campo con varios widgets es un nodo con hijos: se aplana y cada hijo trae su propio
    /// rectángulo. Los vacíos se devuelven con texto nulo: sirven para saber qué fila del
    /// formulario es cada una, aunque no propongan nada.
    ///
    /// <para>⛔ El texto sale TAL CUAL lo tecleó alguien (regla permanente 1): con sus
    /// espacios y sus mayúsculas. Darle forma es cosa de <see cref="Normalizacion"/>, igual
    /// que a lo que lee el OCR.</para>
    /// </remarks>
    /// <param name="pagina">Número de hoja, base 1: solo salen los campos cuyo widget cae en ella.</param>
    /// <param name="anchoPuntos">Ancho de la hoja, para pasar los rectángulos a fracciones.</param>
    /// <param name="altoPuntos">Alto de la hoja, para lo mismo.</param>
    /// <returns>Una anotación de subtipo <see cref="Anotaciones.SubtipoDeCampoDeTexto"/> por campo, sin color ni grosor; vacía sin <c>AcroForm</c>.</returns>
    private IEnumerable<AnotacionDelPdf> CamposDeTextoDelFormulario(int pagina, double anchoPuntos, double altoPuntos)
    {
        if (!_documento.TryGetForm(out var formulario) || formulario is null) return [];

        return formulario.Fields
            .SelectMany(AplanarCampo)
            .OfType<AcroTextField>()
            .Where(campo => campo.PageNumber == pagina && campo.Bounds is not null)
            .Select(campo => new AnotacionDelPdf(
                Subtipo: Anotaciones.SubtipoDeCampoDeTexto,
                Texto: string.IsNullOrEmpty(campo.Value) ? null : campo.Value,
                Banda: Geometria.RectanguloPdfAFracciones(
                    campo.Bounds!.Value.Left, campo.Bounds.Value.Bottom,
                    campo.Bounds.Value.Right, campo.Bounds.Value.Top, anchoPuntos, altoPuntos),
                Rojo: null,
                Verde: null,
                Azul: null,
                Grosor: null))
            .ToArray();
    }

    /// <summary>El campo y, si tiene hijos, todos sus descendientes.</summary>
    /// <param name="campo">Un nodo del árbol del <c>AcroForm</c>; los no terminales no se devuelven, solo sus hojas.</param>
    private static IEnumerable<AcroFieldBase> AplanarCampo(AcroFieldBase campo)
        => campo is AcroNonTerminalField padre ? padre.Children.SelectMany(AplanarCampo) : [campo];

    /// <summary>El <c>/C</c> de la anotación en RGB; nulo en los tres si no lo declara.</summary>
    /// <remarks>Un <c>/C</c> que no tenga exactamente tres números (gris o CMYK) también vuelve nulo: no se convierte, y así el trazo queda como desconocido.</remarks>
    /// <param name="diccionario">El diccionario de la anotación tal como lo da PdfPig.</param>
    private static (double? Rojo, double? Verde, double? Azul) ColorDe(DictionaryToken diccionario)
    {
        if (!diccionario.TryGet(NameToken.Create("C"), out ArrayToken? color) || color is null) return (null, null, null);

        var canales = color.Data.OfType<NumericToken>().Select(t => t.Data).ToArray();
        return canales.Length != 3 ? (null, null, null) : ((double?)canales[0], canales[1], canales[2]);
    }

    /// <summary>El <c>/BS /W</c> de la anotación, o nulo si no declara estilo de borde.</summary>
    /// <param name="diccionario">El diccionario de la anotación tal como lo da PdfPig.</param>
    private static double? GrosorDe(DictionaryToken diccionario)
    {
        if (!diccionario.TryGet(NameToken.Create("BS"), out DictionaryToken? estilo) || estilo is null) return null;
        if (!estilo.TryGet(NameToken.Create("W"), out NumericToken? ancho) || ancho is null) return null;
        return (double)ancho.Data;
    }

    /// <summary>Cierra el documento de PdfPig; los bytes se van con el objeto.</summary>
    public void Dispose() => _documento.Dispose();
}
