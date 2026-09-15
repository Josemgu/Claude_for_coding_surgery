using Fichas.Contratos.Lectura;
using Fichas.Contratos.Puertos;
using Microsoft.ML.OnnxRuntime;
using RapidOcrNet;
using SkiaSharp;

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

    /// <summary>
    /// Quién está usando el motor: las lecturas lo toman como lectores, todas a la vez, y
    /// <see cref="SoltarElMotor"/> como escritor, para no desechar un motor con una hoja a medias.
    /// </summary>
    private readonly ReaderWriterLockSlim _usoDelMotor = new(LockRecursionPolicy.NoRecursion);

    /// <summary>El motor de OCR, nulo hasta la primera lectura o hasta <see cref="PrepararMotor"/>, y otra vez nulo tras <see cref="SoltarElMotor"/>.</summary>
    private RapidOcr? _motor;

    /// <summary>Ticks del sistema de la última vez que el motor se cargó o leyó; solo significa algo con motor cargado.</summary>
    private long _ultimoUsoDelMotorEnTicks;

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
            using var sesion = OpcionesDeSesionSinArena();
            motor.InitModels(rutas[0], rutas[1], rutas[2], rutas[3], sesion);
            AnotarUsoDelMotor();
            _motor = motor;
            return motor;
        }
    }

    /// <summary>
    /// Las opciones de sesión de ONNX Runtime con las que se cargan los tres modelos: las
    /// mismas que el motor monta por su cuenta, menos la arena de memoria de CPU.
    /// </summary>
    /// <remarks>
    /// <para><b>Por qué se apaga la arena (2026-09-15).</b> El dueño midió el programa en
    /// 1 GB de RAM. La sonda de consola sobre la hoja mayor del corpus (2 705×3 500 px)
    /// lo situó: motor cargado 71 MiB privados, tras el primer OCR 706, tras el segundo
    /// 1 300 y ahí se queda mientras el motor viva. Es la arena de ONNX Runtime: con
    /// <c>PythonCompat</c> el detector corre sobre la hoja entera y la arena reserva los
    /// tensores de esa pasada y no los devuelve nunca. Con la arena apagada el mismo
    /// recorrido da 175–192 MiB.</para>
    ///
    /// <para><b>Por qué esto y no reducir la hoja.</b> Bajar <c>MaxSideLen</c> a 1 024
    /// también baja la memoria, pero cambia lo que se lee (87 bloques en vez de 94 en esa
    /// hoja), y eso lo prohíbe la regla de no regresión. Apagar la arena solo cambia
    /// DÓNDE se reservan los tensores, no lo que el modelo calcula: la huella SHA-256 de
    /// los dieciséis documentos del corpus es la misma antes y después
    /// (<c>Fichas.Pruebas.Lectura.PruebaDeLaHuellaDeLaLectura</c>).</para>
    ///
    /// <para>Lo demás se deja como lo monta <c>RapidOcr.GetDefaultSessionOptions()</c>,
    /// medido por reflexión el 2026-09-15: optimización de grafo <c>ORT_ENABLE_EXTENDED</c>,
    /// ejecución secuencial e hilos a 0 (los que ONNX decida). La sobrecarga de
    /// <c>InitModels</c> sin opciones, que era la de antes, delega en esta misma con las
    /// suyas (se ve en la traza de pila del motor), y la huella idéntica es la prueba de
    /// que el único cambio con efecto es la arena. La sesión se desecha nada más cargar:
    /// las 26 hojas del corpus se leen igual después de desecharla, así que cada
    /// <c>InferenceSession</c> se queda con su copia.</para>
    /// </remarks>
    private static SessionOptions OpcionesDeSesionSinArena()
    {
        var sesion = RapidOcr.GetDefaultSessionOptions();
        sesion.EnableCpuMemArena = false;
        return sesion;
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

    /// <summary>Si el motor está cargado ahora mismo; sirve para decidir si vale la pena soltarlo.</summary>
    public bool TieneElMotorCargado => _motor is not null;

    /// <summary>
    /// Cuánto lleva el motor cargado sin leer nada, o nulo si no está cargado.
    /// </summary>
    /// <remarks>
    /// Cuenta desde la última hoja que leyó, o desde que se cargó si aún no leyó ninguna.
    /// Es lo que mira la cáscara para soltar un motor parado (<see cref="SoltarElMotor"/>).
    /// Se mide con el reloj de ticks del sistema, que no cambia si alguien mueve la hora.
    /// </remarks>
    public TimeSpan? TiempoSinLeer
        => _motor is null ? null : TimeSpan.FromMilliseconds(Environment.TickCount64 - Volatile.Read(ref _ultimoUsoDelMotorEnTicks));

    /// <summary>Anota que el motor acaba de usarse, para <see cref="TiempoSinLeer"/>.</summary>
    private void AnotarUsoDelMotor() => Volatile.Write(ref _ultimoUsoDelMotorEnTicks, Environment.TickCount64);

    /// <summary>
    /// Suelta el motor para devolver su memoria; la siguiente lectura lo vuelve a cargar sola.
    /// </summary>
    /// <remarks>
    /// <para>Existe por la memoria (2026-09-15). Aun sin la arena de ONNX, un motor cargado
    /// que ya leyó retiene lo suyo: los modelos y lo que el montón de C mantiene reservado
    /// tras las pasadas. Medido con la sonda de consola sobre la hoja mayor: soltarlo baja
    /// de 195 a 120 MiB privados, y volverlo a cargar cuesta entre 340 y 470 ms. Quien lo
    /// llama —la cáscara, cuando el motor lleva un rato parado, o cualquier pantalla que
    /// sepa que acaba de terminar con el OCR— paga esos milisegundos en la siguiente hoja
    /// a cambio de no tener el motor parado en memoria.</para>
    ///
    /// <para>Es distinto de <see cref="Dispose"/>: después de soltar, el lector sigue vivo
    /// y leyendo. Con una hoja a medias en otro hilo, espera a que termine antes de
    /// desechar el motor: nunca se le quita el motor a una lectura en marcha. Sin motor
    /// cargado, o después de <see cref="Dispose"/>, no hace nada.</para>
    /// </remarks>
    public void SoltarElMotor()
    {
        if (_desechado) return;
        _usoDelMotor.EnterWriteLock();
        try
        {
            DesecharElMotor();
        }
        finally
        {
            _usoDelMotor.ExitWriteLock();
        }
    }

    /// <summary>Desecha el motor si lo hay y lo deja en nulo. Quien llama ya tiene la exclusiva.</summary>
    private void DesecharElMotor()
    {
        lock (_cerrojo)
        {
            _motor?.Dispose();
            _motor = null;
        }
    }

    /// <summary>Cuántas veces se abrió un PDF con PdfPig desde que existe este lector.</summary>
    /// <remarks>
    /// Los tres contadores existen para la prueba de no regresión del plan R-3
    /// (<c>PruebaDeUnaSolaPasadaPorElPdf</c>): un documento de seis hojas tiene que abrirse
    /// una vez, rasterizarse seis y no decodificarse desde PNG ninguna. No los mira nadie
    /// más; contar es un incremento atómico y no cuesta nada en la lectura.
    /// </remarks>
    internal int AperturasConPdfPig => Volatile.Read(ref _aperturasConPdfPig);

    /// <summary>Cuántas hojas rasterizó PDFium desde que existe este lector.</summary>
    internal int RasterizacionesConPdfium => Volatile.Read(ref _rasterizacionesConPdfium);

    /// <summary>Cuántas imágenes PNG decodificó para el OCR desde que existe este lector; por el camino interno es cero.</summary>
    internal int DecodificacionesDePng => Volatile.Read(ref _decodificacionesDePng);

    /// <summary>Respaldo de <see cref="AperturasConPdfPig"/>.</summary>
    private int _aperturasConPdfPig;

    /// <summary>Respaldo de <see cref="RasterizacionesConPdfium"/>.</summary>
    private int _rasterizacionesConPdfium;

    /// <summary>Respaldo de <see cref="DecodificacionesDePng"/>.</summary>
    private int _decodificacionesDePng;

    /// <summary>
    /// Abre el PDF una sola vez para leerlo entero; nulo si no se pudo, sin lanzar.
    /// </summary>
    /// <remarks>
    /// Es el camino de dentro (plan R-3): <see cref="LectorDeFormularios"/> lo abre una
    /// vez por documento y saca de ahí hojas, tamaños, anotaciones y mapas de bits. Los
    /// métodos públicos del contrato, que trabajan por ruta y número de hoja, también pasan
    /// por aquí: cada uno abre y cierra lo suyo. Quien lo recibe lo desecha.
    /// </remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco.</param>
    /// <returns>El documento abierto, o nulo si el archivo no se pudo leer como PDF.</returns>
    internal DocumentoAbierto? AbrirDocumento(string rutaPdf)
    {
        Interlocked.Increment(ref _aperturasConPdfPig);
        return DocumentoAbierto.Abrir(rutaPdf);
    }

    /// <summary>
    /// Rasteriza una hoja de un documento ya abierto y devuelve el mapa de bits de PDFium
    /// tal cual, sin PNG; nulo si la hoja no existe o no se pudo.
    /// </summary>
    /// <remarks>Ver <see cref="DocumentoAbierto.RasterizarHoja"/>: el tope es del lado largo y <c>WithFormFill</c> va a cierto. Quien lo recibe lo desecha.</remarks>
    /// <param name="documento">El documento abierto con <see cref="AbrirDocumento"/>.</param>
    /// <param name="pagina">Número de hoja, base 1.</param>
    /// <param name="ladoLargoMaximo">Tope en píxeles del lado largo.</param>
    internal SKBitmap? RasterizarHoja(DocumentoAbierto documento, int pagina, int ladoLargoMaximo)
    {
        Interlocked.Increment(ref _rasterizacionesConPdfium);
        return documento.RasterizarHoja(pagina, ladoLargoMaximo);
    }

    /// <inheritdoc />
    /// <remarks>Devuelve 0 si el archivo no se puede abrir: un PDF roto no lanza, se cuenta como cero.</remarks>
    public int ContarPaginas(string rutaPdf)
    {
        using var documento = AbrirDocumento(rutaPdf);
        return documento?.Paginas ?? 0;
    }

    /// <summary>El tamaño de la hoja en puntos PDF; nulo si no se pudo abrir.</summary>
    /// <remarks>Abre el PDF para contestar; dentro de una lectura entera se pregunta al <see cref="DocumentoAbierto"/> y no aquí.</remarks>
    /// <param name="rutaPdf">Ruta del archivo en disco.</param>
    /// <param name="pagina">Número de hoja, base 1; fuera de rango devuelve nulo, no lanza.</param>
    public (double AnchoPuntos, double AltoPuntos)? TamanoDeLaPagina(string rutaPdf, int pagina)
    {
        using var documento = AbrirDocumento(rutaPdf);
        return documento?.TamanoDeLaHoja(pagina);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Es el camino del contrato, para quien pide la hoja desde fuera de <c>Fichas.Lectura</c>
    /// (el visor de Corrección): abre el PDF, rasteriza con <see cref="DocumentoAbierto.RasterizarHoja"/>
    /// —tope del lado largo, <c>WithFormFill</c> a cierto— y codifica a PNG, que es lo que el
    /// contrato promete. La lectura entera no pasa por aquí: va por <see cref="RasterizarHoja"/>
    /// y le da el mapa al OCR sin codificarlo.
    /// </remarks>
    public ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
    {
        using var documento = AbrirDocumento(rutaPdf);
        if (documento is null) return null;

        using var mapa = RasterizarHoja(documento, pagina, anchoMaximo);
        if (mapa is null) return null;

        try
        {
            using var datos = mapa.Encode(SKEncodedImageFormat.Png, 100);
            return new ImagenDePagina(pagina, mapa.Width, mapa.Height, datos.ToArray());
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>Abre el PDF para contestar; qué anotaciones salen y cuáles no lo dice <see cref="DocumentoAbierto.AnotacionesDeLaHoja"/>.</remarks>
    public IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina)
    {
        using var documento = AbrirDocumento(rutaPdf);
        return documento?.AnotacionesDeLaHoja(pagina) ?? [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Es el camino del contrato: decodifica el PNG y pasa por <see cref="LeerConOcr(SKBitmap)"/>.
    /// Una imagen con PNG vacío o que no se pueda decodificar devuelve la lista vacía, no una
    /// excepción: un escaneo en blanco es un caso normal, no un fallo del programa.
    /// </remarks>
    public IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen)
    {
        ArgumentNullException.ThrowIfNull(imagen);
        if (imagen.Png.Length == 0) return [];

        Interlocked.Increment(ref _decodificacionesDePng);
        SKBitmap? mapa;
        try
        {
            mapa = SKBitmap.Decode(imagen.Png);
        }
        catch (Exception excepcion) when (excepcion is not OutOfMemoryException)
        {
            // Un PNG que no se deja decodificar es una hoja que no se pudo leer, como antes;
            // no es un fallo de instalacion y no se confunde con uno.
            return [];
        }
        using (mapa)
        {
            return mapa is null ? [] : LeerConOcr(mapa);
        }
    }

    /// <summary>Pasa el OCR a un mapa de bits ya en memoria: el camino sin PNG (plan R-3).</summary>
    /// <remarks>
    /// Se usa el preajuste <c>PythonCompat</c> del motor, y no el <c>Default</c>: es el
    /// que reproduce el preproceso del `rapidocr` de Python —remuestreo adaptativo del
    /// lado corto a 736 px, sin borde blanco añadido—, que es con el que se midió la
    /// línea base de los siete escaneos. Cambiarlo cambia lo que se lee.
    ///
    /// <para>Una página sin texto legible devuelve la lista vacía, no una excepción.</para>
    /// </remarks>
    /// <param name="mapa">La hoja rasterizada; las bandas salen en fracciones de su ancho y su alto.</param>
    /// <returns>Las líneas leídas de arriba abajo y de izquierda a derecha; vacía si la hoja no tenía texto.</returns>
    /// <exception cref="FileNotFoundException">Faltan los modelos: sube a propósito, no se confunde con una hoja en blanco.</exception>
    internal IReadOnlyList<LineaDeOcr> LeerConOcr(SKBitmap mapa)
    {
        // Como lector: varias hojas pueden leer a la vez, y mientras alguna esté a medias
        // nadie puede soltar el motor (ver SoltarElMotor).
        _usoDelMotor.EnterReadLock();
        try
        {
            return LeerConElMotor(mapa);
        }
        finally
        {
            _usoDelMotor.ExitReadLock();
        }
    }

    /// <summary>Pide el motor y pasa el OCR; quien llama ya lo tiene tomado como lector.</summary>
    /// <param name="mapa">La hoja rasterizada.</param>
    /// <returns>Las líneas leídas de arriba abajo y de izquierda a derecha; vacía si la hoja no tenía texto.</returns>
    private IReadOnlyList<LineaDeOcr> LeerConElMotor(SKBitmap mapa)
    {
        // ⛔ El motor se pide FUERA del `try` a proposito. Si faltan los modelos, eso NO
        // puede parecer una hoja en blanco: son averias distintas con arreglos distintos,
        // y confundirlas es lo que hace que un fallo de instalacion se lea como «este
        // escaneo no tenia texto». Que la excepcion suba; quien llama la convierte en su
        // renglon de ilegible con el motivo de verdad.
        var motor = Motor();

        try
        {
            var resultado = motor.Detect(mapa, RapidOcrOptions.PythonCompat);
            AnotarUsoDelMotor();
            if (resultado.TextBlocks is null) return [];

            return resultado.TextBlocks
                .Select(bloque => new LineaDeOcr(
                    Texto: bloque.Text ?? string.Empty,
                    Confianza: ConfianzaDe(bloque),
                    Banda: Geometria.BandaDesdePuntos(
                        bloque.BoxPoints.Select(punto => ((double)punto.X, (double)punto.Y)),
                        mapa.Width,
                        mapa.Height)))
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

        // Como SoltarElMotor: si otra hoja está a medias en otro hilo, se la deja terminar.
        _usoDelMotor.EnterWriteLock();
        try
        {
            DesecharElMotor();
        }
        finally
        {
            _usoDelMotor.ExitWriteLock();
        }
        _usoDelMotor.Dispose();
    }
}
