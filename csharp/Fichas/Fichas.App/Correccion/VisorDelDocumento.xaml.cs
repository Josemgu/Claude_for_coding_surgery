using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Fichas.Contratos.Lectura;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Fichas.App.Correccion;

/// <summary>Lo que costo un arrastre entero y lo que MOVIO, para decir cifras y no adjetivos.</summary>
/// <param name="Tramos">Cuantos movimientos del raton tuvo el arrastre.</param>
/// <param name="MediaMs">Lo que costo de media cada tramo, en milisegundos.</param>
/// <param name="MaximoMs">El peor tramo, que es el que se nota.</param>
/// <param name="MovidoX">Cuantos pixeles se movio la vista a lo ancho, de verdad.</param>
/// <param name="MovidoY">Cuantos pixeles se movio la vista a lo alto, de verdad.</param>
/// <param name="PedidoX">Cuantos pixeles se le PIDIERON a lo ancho.</param>
/// <param name="PedidoY">Cuantos pixeles se le PIDIERON a lo alto.</param>
/// <remarks>
/// Los cuatro ultimos entraron el 2026-09-04 y no son adorno: sin ellos el cuaderno decia
/// «arrastre de 2 tramos, media 0,28 ms» mientras el papel no se habia movido ni un pixel.
/// Medir lo que TARDA sin medir lo que HACE es lo que dejo pasar ese defecto, y separar lo
/// PEDIDO de lo MOVIDO es lo que dice de quien es la culpa cuando no coinciden.
/// </remarks>
public readonly record struct MedidaDelArrastre(
    int Tramos, double MediaMs, double MaximoMs,
    double MovidoX, double MovidoY, double PedidoX, double PedidoY);

/// <summary>
/// El documento al lado de los campos: se arrastra, se acerca y se ilumina la banda del campo.
/// </summary>
/// <remarks>
/// Palabras del dueno: «Debo poder ver el pdf completo al lado para confirmar» y «que pueda
/// hacer zoom a cualquier parte y con el mouse desplazarme libremente».
/// <para>
/// El zoom y el desplazamiento los hace el <c>ScrollView</c> (criterio C4-4). Lo unico que
/// se programa aqui es lo que WinUI no trae: <b>arrastrar con el boton izquierdo</b> —el
/// <c>ScrollView</c> arrastra con el dedo, no con el raton— y <b>el zoom por pasos</b> de la
/// barra. La aritmetica de las dos cosas esta en <see cref="Encuadre"/>, que se prueba sin
/// ventana.
/// </para>
/// <para>
/// ⚠️ <b>Ctrl y la rueda NO se programan aqui, y esta medido.</b> Se escribio un manejador
/// propio que marcaba el suceso como atendido, y aun asi el <c>ScrollView</c> amplio por su
/// cuenta: con la hoja al 36 %, dos muescas de Ctrl+rueda dejaron el visor al <b>43 %</b>,
/// que no es ninguno de los pasos de <see cref="Encuadre.PasosDeZoom"/> sino el factor
/// continuo del propio control. Dos motores sobre la misma rueda dan un numero que no es de
/// ninguno de los dos, asi que se quita el nuestro: la rueda con Ctrl la lleva el
/// <c>ScrollView</c>, que ya amplia donde apunta el cursor, y los pasos fijos se quedan en
/// los botones «+» y «−».
/// </para>
/// </remarks>
public sealed partial class VisorDelDocumento : UserControl
{
    /// <summary>Desplazar sin animacion: dos ordenes en el mismo turno se pisarian a media transicion.</summary>
    private static readonly ScrollingScrollOptions SinAnimacion =
        new(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore);

    /// <summary>Ampliar sin animacion, por lo mismo.</summary>
    private static readonly ScrollingZoomOptions SinAnimacionAlAmpliar =
        new(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore);

    /// <summary>Mide cada tramo del arrastre; se reinicia en cada movimiento del raton.</summary>
    private readonly Stopwatch _cronometroDelArrastre = new();

    /// <summary>Ancho de la hoja rasterizada, en pixeles; 850 hasta que llega la de verdad.</summary>
    private double _anchoDeLaHoja = 850;

    /// <summary>Alto de la hoja rasterizada, en pixeles; 1100 hasta que llega la de verdad.</summary>
    private double _altoDeLaHoja = 1100;

    /// <summary>La banda iluminada ahora, en fracciones de la hoja; nula apaga el resalte.</summary>
    private BandaDeLaPagina? _banda;

    /// <summary>Si hay un arrastre en curso; lo enciende apretar y lo apaga soltar o perder la captura.</summary>
    private bool _arrastrando;

    // De donde arranco el arrastre, y donde estaba la vista entonces. El papel se lleva a
    // una posicion ABSOLUTA calculada desde aqui, y no sumando saltos: un movimiento del
    // raton que se pierda por el camino —y se pierden— no puede perderse para siempre.
    /// <summary>Donde estaba el puntero al apretar, en coordenadas del lienzo.</summary>
    private Point _dondeEmpezoElArrastre;

    /// <summary>Donde estaba la vista a lo ancho al apretar; el arrastre se calcula desde aqui.</summary>
    private double _offsetAlEmpezarX;

    /// <summary>Donde estaba la vista a lo alto al apretar.</summary>
    private double _offsetAlEmpezarY;

    /// <summary>La ultima posicion a lo ancho que se le PIDIO al lienzo; se compara con la que movio.</summary>
    private double _ultimoPedidoX;

    /// <summary>La ultima posicion a lo alto que se le PIDIO al lienzo.</summary>
    private double _ultimoPedidoY;

    /// <summary>Que puntero arrastra; los movimientos de otro puntero se ignoran.</summary>
    private uint _punteroQueArrastra;

    // Si la hoja sigue «a lo ancho». Empieza en si, y solo lo apaga un zoom a mano. Medido en
    // este pase: con una bandera de «ya se ajusto una vez», al llegar la hoja de verdad —que
    // mide 1700 px y no los 850 de arranque— el ajuste NO se rehacia y el visor se quedaba
    // al 73 % de una hoja que ya no era esa.
    /// <summary>Si la hoja sigue «a lo ancho»; mientras lo este, cambiar de tamano o de hoja vuelve a ajustarla.</summary>
    private bool _enModoAncho = true;

    /// <summary>Cuantos movimientos del raton lleva el arrastre en curso.</summary>
    private int _tramosDeEsteArrastre;

    /// <summary>Lo que han costado entre todos, en milisegundos; de aqui sale la media.</summary>
    private double _sumaDeEsteArrastre;

    /// <summary>El tramo mas lento del arrastre en curso, en milisegundos.</summary>
    private double _peorDeEsteArrastre;

    // La escala PEDIDA, que no es la que tiene el ScrollView hasta el turno siguiente.
    //
    // ⚠️ Medido con la ventana abierta el 2026-09-04: «ZoomTo» es asincrono, y leer
    // «_lienzo.ZoomFactor» justo despues devuelve todavia la escala ANTERIOR. Con eso, el
    // rotulo iba un paso por detras —al abrir decia 36 %, tras pulsar «+» seguia diciendo
    // 36 % y tras «Ancho» decia 50 %— y, peor, dos pulsaciones seguidas de «+» calculaban
    // las dos el mismo paso. La escala pedida se guarda aqui y se sincroniza con la de
    // verdad en «ViewChanged», que es cuando la vista ya cambio.
    /// <summary>La escala PEDIDA al lienzo, que va un turno por delante de la suya; ver la nota de arriba.</summary>
    private double _zoomPedido = 1.0;

    /// <summary>El turno de la ultima hoja pedida; una composicion que vuelva con un turno viejo no se pinta.</summary>
    private readonly TurnoDePintado _turnoDeLaImagen = new();

    /// <summary>Monta el visor y ata el raton. Nace vacio hasta que le den una hoja.</summary>
    public VisorDelDocumento()
    {
        InitializeComponent();
        AtarElRaton();
    }

    /// <summary>Que hoja del PDF se esta viendo, base 1.</summary>
    public int Hoja { get; private set; } = 1;

    /// <summary>Cuantas hojas tiene el PDF; 0 mientras no se sepa.</summary>
    public int TotalDeHojas { get; private set; }

    /// <summary>La escala a la que se ve la hoja ahora mismo.</summary>
    public double Zoom => _lienzo.ZoomFactor;

    /// <summary>Cuantos arrastres se han hecho; lo cuenta la medicion.</summary>
    public int CuantosArrastres { get; private set; }

    /// <summary>Lo que costo el ultimo tramo de arrastre, en milisegundos.</summary>
    /// <remarks>
    /// Esta aqui para poder decir una cifra en vez de un adjetivo. En Tk el mismo arrastre
    /// costaba 26,8 ms y bajo a 1,4 cacheando la hoja reescalada; aqui la hoja la compone la
    /// GPU y el numero hay que medirlo, no suponerlo.
    /// </remarks>
    public double MilisegundosDelUltimoArrastre { get; private set; }

    /// <summary>Salta cuando el visor pide otra hoja, para que la pantalla la rasterice.</summary>
    public event EventHandler<int>? HojaPedida;

    /// <summary>
    /// Salta al soltar el arrastre, con lo que costo. Es el canal por el que se MIDE.
    /// </summary>
    /// <remarks>
    /// Se avisa al soltar y no en cada tramo a proposito: anotar en el cuaderno una vez por
    /// movimiento del raton escribiria en disco cien veces por arrastre, y entonces lo que se
    /// mide es el disco.
    /// </remarks>
    public event EventHandler<MedidaDelArrastre>? ArrastreTerminado;

    /// <summary>Salta cuando la hoja no se pudo pintar, con la linea que hay que ensenar.</summary>
    /// <remarks>
    /// ⛔ Es lo que impide que un fallo se quede callado. Sale del defecto que QA midio en la
    /// pantalla de Importar: manejador <c>async void</c> que se traga la excepcion, botones
    /// que parecen muertos, y ni una linea que diga por que.
    /// </remarks>
    public event EventHandler<string>? NoSePudoPintar;

    /// <summary>
    /// Ensena una hoja ya rasterizada. Una imagen sin bytes NO es un fallo: se dice y ya.
    /// </summary>
    /// <remarks>
    /// Un PNG vacio o una imagen nula pintan la hoja en blanco con su motivo escrito encima:
    /// los campos se corrigen igual, y negarse a pintar seria impedir por no poder.
    /// <para>
    /// ⚠️ <b>Esto ya NO es <c>async void</c>.</b> Lo era, y con ello cualquier fallo al
    /// componer el mapa de bits se iba por un camino que nadie mira. Ahora la parte que
    /// espera vive en <see cref="ComponerYPintar"/>, que atrapa y AVISA por
    /// <see cref="NoSePudoPintar"/>.
    /// </para>
    /// </remarks>
    /// <param name="imagen">La hoja rasterizada, o nula si no se pudo abrir.</param>
    /// <param name="totalDeHojas">Cuantas hojas tiene el PDF; 0 si no se sabe.</param>
    /// <param name="motivoSiNoHay">Lo que se escribe sobre el papel en blanco cuando no hay imagen.</param>
    public void MostrarHoja(ImagenDePagina? imagen, int totalDeHojas, string motivoSiNoHay)
    {
        // El turno se pide SIEMPRE, tambien cuando no hay imagen: una composicion en camino de
        // la hoja anterior tiene que quedarse vieja aunque lo que venga ahora sea «sin escaneo».
        var turno = _turnoDeLaImagen.Pedir();
        TotalDeHojas = totalDeHojas;
        if (imagen is not null)
        {
            Hoja = imagen.Pagina;
            _anchoDeLaHoja = Math.Max(1, imagen.Ancho);
            _altoDeLaHoja = Math.Max(1, imagen.Alto);
            _hoja.Width = _anchoDeLaHoja;
            _hoja.Height = _altoDeLaHoja;
        }

        PintarLosRotulos();
        ColocarElResalte();
        if (_enModoAncho) AjustarAlAncho();

        if (imagen is null || imagen.Png.Length == 0)
        {
            _imagen.Source = null;
            Decir(motivoSiNoHay);
            return;
        }

        Decir(string.Empty);
        _ = ComponerYPintar(imagen, turno);
    }

    /// <summary>
    /// Compone el PNG y lo pone en la hoja, si para entonces sigue siendo la hoja que toca. Un
    /// fallo NO se calla: se escribe y se avisa.
    /// </summary>
    /// <remarks>
    /// <para>Se atrapa <see cref="Exception"/> a secas, que es lo contrario de lo que este proyecto
    /// hace en casi todos los demas sitios, y por el mismo motivo que
    /// <c>MotorDeImportacion</c>: por debajo hay codigo nativo de composicion de mapas de
    /// bits que levanta lo suyo, y una lista de tipos seria la lista de los fallos que se me
    /// ocurrieron. <b>Atrapar para callar esta prohibido; atrapar para convertirlo en una
    /// linea que Miguel puede leer es lo contrario.</b></para>
    ///
    /// <para>⚠️ <b>El turno es el arreglo del 2026-09-15.</b> Dos hojas pedidas seguidas —la del
    /// caso que se abria sin pedirlo y la del elegido— componian sus mapas de bits a la vez, y
    /// ganaba la que terminaba la ULTIMA, no la ultima pedida: el papel de un caso sobre los
    /// campos de otro. Ahora lo que vuelve con un turno viejo no se pinta.</para>
    /// </remarks>
    /// <param name="imagen">La hoja con sus bytes PNG; aqui ya se sabe que trae alguno.</param>
    /// <param name="turno">El turno que pidio <see cref="MostrarHoja"/> para esta hoja.</param>
    private async Task ComponerYPintar(ImagenDePagina imagen, long turno)
    {
        try
        {
            var mapa = new BitmapImage();
            using var flujo = new InMemoryRandomAccessStream();
            var escritor = new DataWriter(flujo);
            escritor.WriteBytes(imagen.Png);
            await escritor.StoreAsync();
            await escritor.FlushAsync();
            escritor.DetachStream();
            flujo.Seek(0);
            await mapa.SetSourceAsync(flujo);
            if (!_turnoDeLaImagen.SigueVigente(turno)) return;
            _imagen.Source = mapa;
        }
        catch (Exception fallo)
        {
            var linea = TextoDelVisor.LineaDeFallo(imagen.Pagina, fallo);
            _imagen.Source = null;
            Decir(linea + " Los campos se corrigen igual, sin la imagen al lado.");
            NoSePudoPintar?.Invoke(this, linea);
        }
    }

    /// <summary>Escribe el motivo sobre el panel, o lo quita si no hay motivo que dar.</summary>
    /// <param name="motivo">El texto para el panel; vacio esconde el marco.</param>
    private void Decir(string motivo)
    {
        _sinImagen.Text = motivo;
        _marcoDelMotivo.Visibility = string.IsNullOrEmpty(motivo) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Ilumina la banda de un campo y la trae a la vista. Es lo que llama el foco.
    /// </summary>
    /// <remarks>
    /// La funcion que el planificador encontro en Rossum: al poner el foco en un campo, se
    /// ilumina su renglon en la imagen del documento. Si el campo no tiene banda guardada, se
    /// apaga el resalte y la hoja se queda donde estaba: no se mueve la vista a ciegas.
    /// </remarks>
    /// <param name="banda">Donde estaba el campo, en fracciones de la hoja; nula apaga el resalte y no mueve la vista.</param>
    public void Enfocar(BandaDeLaPagina? banda)
    {
        _banda = banda;
        ColocarElResalte();
        if (banda is not BandaDeLaPagina cual) return;

        var rectangulo = Encuadre.RectanguloDeLaBanda(cual, _anchoDeLaHoja, _altoDeLaHoja);
        var (x, y) = Encuadre.OffsetParaVer(
            rectangulo, _lienzo.ViewportWidth, _lienzo.ViewportHeight,
            _zoomPedido, _lienzo.HorizontalOffset, _lienzo.VerticalOffset);
        _lienzo.ScrollTo(x, y, SinAnimacion);
    }

    /// <summary>Devuelve la hoja al ancho del panel, que es como nace el visor.</summary>
    public void AjustarAlAncho()
    {
        if (_lienzo.ViewportWidth <= 0) return;
        AmpliarA(Encuadre.ZoomParaElAncho(_lienzo.ViewportWidth, _anchoDeLaHoja), new Vector2(0, 0));
        _enModoAncho = true;
    }

    /// <summary>Pone la hoja al 100 %, que es el tamano al que se rasterizo.</summary>
    public void AjustarACien()
    {
        AmpliarA(1.0, CentroDelPanel());
        _enModoAncho = false;
    }

    /// <summary>Lleva el zoom a esa escala dejando quieto lo que hay bajo ese punto del panel.</summary>
    /// <param name="zoomNuevo">La escala a la que se va.</param>
    /// <param name="punto">El punto del panel que tiene que quedarse quieto.</param>
    private void AmpliarA(double zoomNuevo, Vector2 punto)
    {
        // La escala de partida es la PEDIDA, no la que tiene el control: la suya todavia
        // puede ser la anterior porque «ZoomTo» no cambia la vista en el acto.
        var zoomViejo = _zoomPedido;
        _zoomPedido = zoomNuevo;
        var (x, y) = Encuadre.OffsetAlAmpliarEn(
            punto.X, punto.Y, _lienzo.HorizontalOffset, _lienzo.VerticalOffset, zoomViejo, zoomNuevo);

        // Las dos ordenes van sin animacion y en el mismo turno: la primera cambia la escala
        // y la segunda manda sobre la vista. Con animacion se pisarian a media transicion.
        _lienzo.ZoomTo((float)zoomNuevo, new Vector2(0, 0), SinAnimacionAlAmpliar);
        _lienzo.ScrollTo(x, y, SinAnimacion);
        PintarLosRotulos();
        ColocarElResalte();
    }

    /// <summary>Cuando la vista ya cambio de verdad, se sincroniza la escala y se repinta.</summary>
    /// <remarks>
    /// Es el unico sitio donde el rotulo dice la escala de VERDAD. Sin esto iba un paso por
    /// detras, y se veia: 36 % al abrir, 36 % tras pulsar «+», 50 % tras pulsar «Ancho».
    /// </remarks>
    private void AlCambiarLaVista(ScrollView quien, object cuando)
    {
        _zoomPedido = _lienzo.ZoomFactor;
        PintarLosRotulos();
    }

    /// <summary>El centro del panel, que es donde se amplia cuando no hay cursor que mandar.</summary>
    private Vector2 CentroDelPanel()
        => new((float)(_lienzo.ViewportWidth / 2), (float)(_lienzo.ViewportHeight / 2));

    /// <summary>Coloca el rectangulo del resalte sobre la banda; lo apaga si no hay banda.</summary>
    private void ColocarElResalte()
    {
        if (_banda is not BandaDeLaPagina banda)
        {
            _resalte.Visibility = Visibility.Collapsed;
            return;
        }

        var rectangulo = Encuadre.RectanguloDeLaBanda(banda, _anchoDeLaHoja, _altoDeLaHoja);
        _resalte.Margin = new Thickness(rectangulo.X, rectangulo.Y, 0, 0);
        _resalte.Width = rectangulo.Ancho;
        _resalte.Height = rectangulo.Alto;
        _resalte.Visibility = Visibility.Visible;
    }

    /// <summary>Escribe el zoom y la hoja; nada mas, y las dos en su hueco fijo.</summary>
    private void PintarLosRotulos()
    {
        _cuantoZoom.Text = string.Format(CultureInfo.GetCultureInfo("es-ES"), "{0:P0}", _lienzo.ZoomFactor);
        _queHoja.Text = TotalDeHojas > 0 ? $"{Hoja} / {TotalDeHojas}" : $"hoja {Hoja}";
    }


    /// <summary>Cuando el panel cambia de ancho, la hoja vuelve a caber si estaba a lo ancho.</summary>
    private void AlCambiarDeTamano(object quien, SizeChangedEventArgs cuando)
    {
        if (_enModoAncho) AjustarAlAncho();
        PintarLosRotulos();
    }

    // ---- los botones de la barra -----------------------------------------

    /// <summary>Un paso mas de zoom, desde el centro del panel.</summary>
    private void AlPulsarAcercar(object quien, RoutedEventArgs cuando)
        => AmpliarA(Encuadre.PasoDeZoom(_zoomPedido,1), CentroDelPanel());

    /// <summary>Un paso menos de zoom, desde el centro del panel.</summary>
    private void AlPulsarAlejar(object quien, RoutedEventArgs cuando)
        => AmpliarA(Encuadre.PasoDeZoom(_zoomPedido,-1), CentroDelPanel());

    /// <summary>La hoja entera a lo ancho.</summary>
    private void AlPulsarAncho(object quien, RoutedEventArgs cuando) => AjustarAlAncho();

    /// <summary>La hoja al 100 %.</summary>
    private void AlPulsarCien(object quien, RoutedEventArgs cuando) => AjustarACien();

    /// <summary>Pide la hoja anterior; en la primera no hace nada.</summary>
    private void AlPulsarHojaAnterior(object quien, RoutedEventArgs cuando) => PedirLaHoja(Hoja - 1);

    /// <summary>Pide la hoja siguiente; en la ultima no hace nada.</summary>
    private void AlPulsarHojaSiguiente(object quien, RoutedEventArgs cuando) => PedirLaHoja(Hoja + 1);

    /// <summary>Pide una hoja acotando a lo que el PDF tiene.</summary>
    /// <param name="hoja">La hoja que se quiere, base 1; fuera de rango no se pide nada.</param>
    private void PedirLaHoja(int hoja)
    {
        if (hoja < 1) return;
        if (TotalDeHojas > 0 && hoja > TotalDeHojas) return;
        HojaPedida?.Invoke(this, hoja);
    }
}
