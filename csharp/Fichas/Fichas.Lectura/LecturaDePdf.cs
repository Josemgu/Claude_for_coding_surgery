using Fichas.Contratos.Lectura;
using Fichas.Contratos.Puertos;
using PDFtoImage;
using RapidOcrNet;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Tokens;

namespace Fichas.Lectura;

/// <summary>
/// Leer un PDF: rasterizar una hoja, sacar sus anotaciones y pasarle el OCR.
/// </summary>
/// <remarks>
/// ⛔ Regla permanente 1: aqui NO entra ninguna IA generativa. RapidOcrNet es un motor
/// determinista —tres modelos ONNX y una decodificacion CTC—, no un modelo de lenguaje:
/// la misma imagen da siempre el mismo texto y nunca completa lo que no vio.
///
/// <para>⛔ Regla permanente 2, sin red: los tres <c>.onnx</c> y el diccionario se
/// resuelven por RUTA ABSOLUTA junto al ejecutable y se comprueba que existen ANTES de
/// leer nada. Es el mismo motivo que lleva escrito `extraccion/ocr.py`: pasar la ruta
/// impide que la biblioteca vaya a buscar modelos a internet la primera vez que arranque
/// en la maquina de Miguel.</para>
///
/// <para><b>Las tres piezas y por que son estas.</b></para>
/// <list type="bullet">
///   <item><b>PDFium</b> (via PDFtoImage, MIT; PDFium BSD-3-Clause) rasteriza. Es EL
///   MISMO motor que usa `pypdfium2` en el Python de hoy, y esa es la razon de elegirlo
///   sobre `Windows.Data.Pdf`: el ADR-0004 §9.1 avisa de que los dos no producen la misma
///   imagen, y todo lo medido sobre la del Python —las bandas, y manana el umbral de las
///   casillas— habria que recalibrarlo.</item>
///   <item><b>PdfPig</b> (Apache-2.0) lee las anotaciones. PDFium rasteriza pero no da
///   acceso al <c>/C</c> ni al <c>/BS /W</c>, que es justo lo que separa un tachon de un
///   resaltador.</item>
///   <item><b>RapidOcrNet</b> (Apache-2.0) hace el OCR, con los modelos PP-OCRv5 del grupo
///   latino. Comprobado por SHA-256: son los MISMOS tres archivos que `modelos/` del
///   repositorio, byte a byte.</item>
/// </list>
/// </remarks>
public sealed class LecturaDePdf : ILecturaDePdf, IDisposable
{
    /// <summary>
    /// El detector de cajas de texto (PP-OCRv5, móvil), el mismo para todos los idiomas.
    /// Este nombre y los tres siguientes son los que reparte el paquete RapidOcrNet, tal cual.
    /// </summary>
    private const string ModeloDeDeteccion = "ch_PP-OCRv5_mobile_det.onnx";
    /// <summary>El clasificador de orientación de renglón, que endereza los renglones al revés antes de leerlos.</summary>
    private const string ModeloDeOrientacion = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    /// <summary>El reconocedor del grupo LATINO: el que sabe leer la ñ y las tildes.</summary>
    private const string ModeloDeReconocimiento = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    /// <summary>Los 502 caracteres que el reconocedor puede devolver, uno por línea.</summary>
    private const string DiccionarioLatino = "ppocrv5_latin_dict.txt";

    /// <summary>Dónde viven los cuatro archivos; fijada en el constructor y no se vuelve a resolver.</summary>
    private readonly string _carpetaDeModelos;

    /// <summary>Protege la carga del motor: dos hojas en paralelo no pueden cargar los modelos dos veces.</summary>
    private readonly Lock _cerrojo = new();

    /// <summary>El motor de OCR, nulo hasta la primera lectura o hasta <see cref="PrepararMotor"/>.</summary>
    private RapidOcr? _motor;

    /// <summary>Cierto después de <see cref="Dispose"/>: pedir el motor entonces lanza, no lo recrea.</summary>
    private bool _desechado;

    /// <summary>Crea el lector. Los modelos se cargan la primera vez que se lee, no ahora.</summary>
    /// <param name="carpetaDeModelos">
    /// Donde viven los cuatro archivos. Nula significa <c>models/v5</c> junto al
    /// ejecutable, que es donde el paquete NuGet los deja.
    /// </param>
    public LecturaDePdf(string? carpetaDeModelos = null)
        => _carpetaDeModelos = carpetaDeModelos ?? Path.Combine(AppContext.BaseDirectory, "models", "v5");

    /// <summary>Las cuatro rutas absolutas, comprobando que los archivos existen.</summary>
    /// <remarks>
    /// Se comprueba al arrancar el motor y no al primer uso de cada modelo: un fallo al
    /// arrancar se entiende; el mismo fallo a mitad de procesar veinte formularios, no.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Falta alguno de los cuatro archivos.</exception>
    public IReadOnlyList<string> RutasDeLosModelos()
    {
        string[] rutas =
        [
            Path.Combine(_carpetaDeModelos, ModeloDeDeteccion),
            Path.Combine(_carpetaDeModelos, ModeloDeOrientacion),
            Path.Combine(_carpetaDeModelos, ModeloDeReconocimiento),
            Path.Combine(_carpetaDeModelos, DiccionarioLatino),
        ];
        var faltan = rutas.Where(ruta => !File.Exists(ruta)).ToArray();
        if (faltan.Length > 0)
        {
            throw new FileNotFoundException(
                "No están los modelos de OCR y sin ellos no se puede leer nada. "
                + $"Faltan: {string.Join(", ", faltan)}.");
        }
        return rutas;
    }

    /// <summary>
    /// El motor, cargándolo la primera vez. Comprobación doble bajo cerrojo: la lectura
    /// rápida sin cerrojo evita pagarlo en cada hoja, y la segunda dentro evita que dos
    /// hilos carguen los modelos a la vez.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Ya se llamó a <see cref="Dispose"/>.</exception>
    /// <exception cref="FileNotFoundException">Falta alguno de los cuatro archivos (ver <see cref="RutasDeLosModelos"/>).</exception>
    private RapidOcr Motor()
    {
        ObjectDisposedException.ThrowIf(_desechado, this);
        if (_motor is not null) return _motor;

        lock (_cerrojo)
        {
            if (_motor is not null) return _motor;
            var rutas = RutasDeLosModelos();
            var motor = new RapidOcr();
            motor.InitModels(rutas[0], rutas[1], rutas[2], rutas[3]);
            _motor = motor;
            return motor;
        }
    }

    /// <summary>Carga los tres modelos ahora, para no pagarlos a mitad de la primera hoja.</summary>
    /// <remarks>
    /// Es opcional: si no se llama, los modelos se cargan solos la primera vez que se lee.
    /// Existe por dos motivos: poder MEDIR lo que cuesta el arranque —el Python paga 2,2 s
    /// una vez por sesion y hay que poder comparar— y poder enseñar «preparando el motor»
    /// en pantalla en vez de que la primera hoja parezca lenta sin explicacion.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Falta alguno de los cuatro archivos.</exception>
    public void PrepararMotor() => Motor();

    /// <inheritdoc />
    /// <remarks>Devuelve 0 si el archivo no se puede abrir: un PDF roto no lanza, se cuenta como cero.</remarks>
    public int ContarPaginas(string rutaPdf)
    {
        try
        {
            using var documento = PdfDocument.Open(rutaPdf);
            return documento.NumberOfPages;
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            // No se silencia: quien llama recibe 0, que significa «no se pudo abrir», y
            // deja su renglon de ilegible. Un `throw` aqui tumbaria la tanda entera.
            return 0;
        }
    }

    /// <summary>El tamano de la hoja en puntos PDF; nulo si no se pudo abrir.</summary>
    /// <remarks>
    /// Hace falta fuera para dos cosas: calcular la escala del rasterizado y saber la
    /// relacion de aspecto que las bandas necesitan.
    /// </remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco.</param>
    /// <param name="pagina">Número de hoja, base 1; fuera de rango devuelve nulo, no lanza.</param>
    public (double AnchoPuntos, double AltoPuntos)? TamanoDeLaPagina(string rutaPdf, int pagina)
    {
        try
        {
            using var documento = PdfDocument.Open(rutaPdf);
            if (pagina < 1 || pagina > documento.NumberOfPages) return null;
            var hoja = documento.GetPage(pagina);
            return (hoja.Width, hoja.Height);
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// El <paramref name="anchoMaximo"/> es el tope del LADO LARGO, no del ancho: es lo
    /// que hace la regla de no regresion de los 3 500 px, y en estas hojas verticales el
    /// lado largo es el alto. La escala se baja al <c>double</c> inmediatamente anterior
    /// (ver <see cref="Geometria.EscalaDeRasterizado"/>) para que el mapa de bits salga
    /// justo en el tope y no uno por encima.
    ///
    /// <para>⚠️ <b><c>WithFormFill</c> va a cierto, y no es un adorno</b> (2026-09-10). PDFium no
    /// pinta los campos de un formulario rellenable con solo <c>WithAnnotations</c>: hace
    /// falta el entorno de relleno de formulario. Medido sobre el PDF del dueño: la casilla
    /// del nombre salia con <b>0 de 58 011</b> pixeles oscuros sin esta opcion y con 4 431 con
    /// ella. Sin ella, Miguel veia la tabla en blanco con la persona ahi delante, y el OCR no
    /// tenia nada que leer. En un escaneo sin formulario no cambia ni un pixel: comprobado
    /// por SHA-256 del PNG sobre los diez documentos escaneados.</para>
    /// </remarks>
    public ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
    {
        var tamano = TamanoDeLaPagina(rutaPdf, pagina);
        if (tamano is null) return null;

        try
        {
            double escala = Geometria.EscalaDeRasterizado(tamano.Value.AnchoPuntos, tamano.Value.AltoPuntos, anchoMaximo);
            int anchoPx = (int)Math.Ceiling(tamano.Value.AnchoPuntos * escala);
            int altoPx = (int)Math.Ceiling(tamano.Value.AltoPuntos * escala);

            var bytes = File.ReadAllBytes(rutaPdf);
            using var mapa = Conversion.ToImage(
                bytes,
                page: new Index(pagina - 1),
                options: new RenderOptions(Width: anchoPx, Height: altoPx, WithAnnotations: true, WithFormFill: true));

            using var datos = mapa.Encode(SKEncodedImageFormat.Png, 100);
            return new ImagenDePagina(pagina, mapa.Width, mapa.Height, datos.ToArray());
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Salen las <c>/FreeText</c>, las <c>/Ink</c> y, desde el 2026-09-10, los campos de
    /// TEXTO del formulario rellenable —<c>/Widget</c> de tipo <c>/Tx</c>—, que es donde un
    /// formulario rellenado en el ordenador lleva tecleados el nombre, la cedula, las fechas
    /// y el templo. Los demas subtipos se ignoran a proposito: un <c>/Link</c> o un
    /// <c>/Popup</c> no dicen nada del formulario, y las casillas (<c>/Btn</c>) no entran
    /// todavia: la App no tiene por donde guardarlas. Lo que NO se ignora es un <c>/Ink</c>
    /// que no se sepa clasificar; ese vuelve igual y <see cref="Anotaciones.ClaseDe"/> lo
    /// llama desconocido.
    /// </remarks>
    public IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina)
    {
        try
        {
            using var documento = PdfDocument.Open(rutaPdf);
            if (pagina < 1 || pagina > documento.NumberOfPages) return [];

            var hoja = documento.GetPage(pagina);
            var leidas = new List<AnotacionDelPdf>(CamposDeTextoDelFormulario(documento, pagina, hoja.Width, hoja.Height));

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
    /// Los campos de texto del formulario que caen en esta hoja, vacios incluidos.
    /// </summary>
    /// <remarks>
    /// Se leen por el <c>AcroForm</c> del documento y no por el diccionario de cada
    /// <c>/Widget</c>, porque el tipo y el valor de un campo pueden venir HEREDADOS de su
    /// padre (<c>/Parent</c>), y PdfPig resuelve esa herencia al construir el arbol. Un
    /// campo con varios widgets es un nodo con hijos: se aplana y cada hijo trae su propio
    /// rectangulo. Los vacios se devuelven con texto nulo: sirven para saber que fila del
    /// formulario es cada una, aunque no propongan nada.
    ///
    /// <para>⛔ El texto sale TAL CUAL lo tecleo alguien (regla permanente 1): con sus
    /// espacios y sus mayusculas. Darle forma es cosa de <see cref="Normalizacion"/>, igual
    /// que a lo que lee el OCR.</para>
    /// </remarks>
    /// <param name="documento">El PDF abierto; sin <c>AcroForm</c> devuelve la lista vacía.</param>
    /// <param name="pagina">Número de hoja, base 1: solo salen los campos cuyo widget cae en ella.</param>
    /// <param name="anchoPuntos">Ancho de la hoja, para pasar los rectángulos a fracciones.</param>
    /// <param name="altoPuntos">Alto de la hoja, para lo mismo.</param>
    /// <returns>Una anotación de subtipo <see cref="Anotaciones.SubtipoDeCampoDeTexto"/> por campo, sin color ni grosor.</returns>
    private static IEnumerable<AnotacionDelPdf> CamposDeTextoDelFormulario(
        PdfDocument documento, int pagina, double anchoPuntos, double altoPuntos)
    {
        if (!documento.TryGetForm(out var formulario) || formulario is null) return [];

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

    /// <summary>El <c>/C</c> de la anotacion en RGB; nulo en los tres si no lo declara.</summary>
    /// <remarks>Un <c>/C</c> que no tenga exactamente tres números (gris o CMYK) también vuelve nulo: no se convierte, y así el trazo queda como desconocido.</remarks>
    /// <param name="diccionario">El diccionario de la anotación tal como lo da PdfPig.</param>
    private static (double? Rojo, double? Verde, double? Azul) ColorDe(DictionaryToken diccionario)
    {
        if (!diccionario.TryGet(NameToken.Create("C"), out ArrayToken? color) || color is null) return (null, null, null);

        var canales = color.Data.OfType<NumericToken>().Select(t => t.Data).ToArray();
        return canales.Length != 3 ? (null, null, null) : ((double?)canales[0], canales[1], canales[2]);
    }

    /// <summary>El <c>/BS /W</c> de la anotacion, o nulo si no declara estilo de borde.</summary>
    /// <param name="diccionario">El diccionario de la anotación tal como lo da PdfPig.</param>
    private static double? GrosorDe(DictionaryToken diccionario)
    {
        if (!diccionario.TryGet(NameToken.Create("BS"), out DictionaryToken? estilo) || estilo is null) return null;
        if (!estilo.TryGet(NameToken.Create("W"), out NumericToken? ancho) || ancho is null) return null;
        return (double)ancho.Data;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Se usa el preajuste <c>PythonCompat</c> del motor, y no el <c>Default</c>: es el
    /// que reproduce el preproceso del `rapidocr` de Python —remuestreo adaptativo del
    /// lado corto a 736 px, sin borde blanco anadido—, que es con el que se midio la
    /// linea base de los siete escaneos. Cambiarlo cambia lo que se lee.
    ///
    /// <para>Una pagina sin texto legible devuelve la lista vacia, no una excepcion: un
    /// escaneo en blanco es un caso normal, no un fallo del programa.</para>
    /// </remarks>
    public IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen)
    {
        ArgumentNullException.ThrowIfNull(imagen);
        if (imagen.Png.Length == 0) return [];

        // ⛔ El motor se pide FUERA del `try` a proposito. Si faltan los modelos, eso NO
        // puede parecer una hoja en blanco: son averias distintas con arreglos distintos,
        // y confundirlas es lo que hace que un fallo de instalacion se lea como «este
        // escaneo no tenia texto». Que la excepcion suba; quien llama la convierte en su
        // renglon de ilegible con el motivo de verdad.
        var motor = Motor();

        try
        {
            using var mapa = SKBitmap.Decode(imagen.Png);
            if (mapa is null) return [];

            var resultado = motor.Detect(mapa, RapidOcrOptions.PythonCompat);
            if (resultado.TextBlocks is null) return [];

            return resultado.TextBlocks
                .Select(bloque => new LineaDeOcr(
                    Texto: bloque.Text ?? string.Empty,
                    Confianza: ConfianzaDe(bloque),
                    Banda: Geometria.BandaDesdePuntos(
                        bloque.BoxPoints.Select(punto => ((double)punto.X, (double)punto.Y)),
                        imagen.Ancho,
                        imagen.Alto)))
                .OrderBy(linea => linea.Banda.Y0)
                .ThenBy(linea => linea.Banda.X0)
                .ToArray();
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException and not ObjectDisposedException)
        {
            return [];
        }
    }

    /// <summary>La confianza media de la linea, o nula si el motor no la dio.</summary>
    /// <remarks>No se inventa un 1,0 cuando falta: nulo significa «el motor no lo dijo».</remarks>
    /// <param name="bloque">Una línea detectada por el motor, con la puntuación de cada carácter.</param>
    private static double? ConfianzaDe(TextBlock bloque)
        => bloque.CharScores is null || bloque.CharScores.Length == 0 ? null : bloque.CharScores.Average();

    /// <inheritdoc />
    /// <remarks>
    /// El motor no tiene «idiomas instalados» como el OCR de Windows: tiene UN
    /// reconocedor, y el que carga este proyecto es el del grupo LATINO, con 502
    /// caracteres que incluyen la ñ y los acentos del espanol y del frances. Se declara
    /// asi para que quien pregunte «¿hay espanol?» tenga una respuesta verdadera.
    /// </remarks>
    public IReadOnlyList<string> IdiomasDisponibles()
        => File.Exists(Path.Combine(_carpetaDeModelos, ModeloDeReconocimiento))
            ? ["latin (PP-OCRv5)"]
            : [];

    /// <inheritdoc />
    public void Dispose()
    {
        if (_desechado) return;
        _desechado = true;
        _motor?.Dispose();
        _motor = null;
    }
}
