using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Espiga;

/// <summary>
/// La espiga de la FASE C0. No es una pantalla del programa: es un banco de
/// medida. Cada boton contesta un criterio y escribe su cifra en el cuaderno.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>Cuantas filas pide el criterio C0-3. No se baja de aqui.</summary>
    private const int CuantasFilas = 3000;

    private readonly StringBuilder _cuaderno = new();

    private List<Fila>? _filas;

    private Stopwatch? _relojDelClic;

    private int _cuantosDesplazamientos;

    private double _desplazamientoMaximo;

    private string _estadoDelDesplazamiento = "C0-2 sin desplazar todavia.";

    public MainPage()
    {
        InitializeComponent();
        Loaded += AlCargarLaPagina;
    }

    // ----- arranque y disposicion -----

    private async void AlCargarLaPagina(object remitente, RoutedEventArgs argumentos)
    {
        HacerVisibleLaBarra();
        Anotar(MedicionDeArranque.EscribirLaCifra());
        Anotar(LectorDeOcr.ListarIdiomasDisponibles());

        if (Ajustes.SaleSolo)
        {
            CerrarLaVentana();
            return;
        }

        if (Ajustes.MideSolo)
        {
            await MedirTodoYSalirAsync();
        }
    }

    /// <summary>
    /// Requisito 1 del dueno: barra visible, no solo rueda. En ScrollView esta
    /// propiedad controla SOLO la visibilidad; no puede prohibir el desplazamiento
    /// (ADR-0003 §6.2), que es lo que fallaba en Tk.
    /// </summary>
    private void HacerVisibleLaBarra()
    {
        var desplazador = _vistaDeFilas.ScrollView;
        if (desplazador is null)
        {
            Anotar("C0-2 AVISO: ItemsView no expuso su ScrollView.");
            return;
        }

        desplazador.VerticalScrollBarVisibility = ScrollingScrollBarVisibility.Visible;
        desplazador.ViewChanged += AlCambiarLaVista;
    }

    /// <summary>
    /// Deja constancia de cada desplazamiento. Es la prueba del C0-2 que no depende
    /// de que alguien diga "si, baja": la cifra la escribe el propio programa.
    /// </summary>
    private void AlCambiarLaVista(ScrollView desplazador, object argumentos)
    {
        _cuantosDesplazamientos++;
        _desplazamientoMaximo = Math.Max(_desplazamientoMaximo, desplazador.VerticalOffset);

        _estadoDelDesplazamiento = string.Format(
            CultureInfo.InvariantCulture,
            "C0-2 desplazamientos: {0}; posicion actual {1:F0} px; maxima alcanzada {2:F0} px; "
            + "alto desplazable {3:F0} px; barra vertical: {4}",
            _cuantosDesplazamientos,
            desplazador.VerticalOffset,
            _desplazamientoMaximo,
            desplazador.ScrollableHeight,
            desplazador.VerticalScrollBarVisibility);

        RefrescarElCuaderno();
    }

    // ----- C0-3: pintar la lista -----

    private async void AlPedirLasFilas(object remitente, RoutedEventArgs argumentos)
    {
        await MedirElPintadoAsync();
    }

    /// <summary>Mide desde que se entregan las filas hasta que el marco esta dibujado.</summary>
    private async Task MedirElPintadoAsync()
    {
        _filas ??= GeneradorDeFilas.Generar(CuantasFilas);

        var reloj = Stopwatch.StartNew();
        _vistaDeFilas.ItemsSource = _filas;
        await EsperarAlDibujadoAsync();
        reloj.Stop();

        Anotar($"C0-3 pintar {CuantasFilas} filas: {Segundos(reloj)} s");
    }

    // ----- C0-3: el clic en una fila -----

    private void AlElegirUnaFila(ItemsView remitente, ItemsViewSelectionChangedEventArgs argumentos)
    {
        _relojDelClic = Stopwatch.StartNew();
        _ = TerminarDeMedirElClicAsync();
    }

    private async Task TerminarDeMedirElClicAsync()
    {
        await EsperarAlDibujadoAsync();

        if (_relojDelClic is null)
        {
            return;
        }

        _relojDelClic.Stop();
        Anotar($"C0-3 clic en una fila: {Segundos(_relojDelClic)} s");
        _relojDelClic = null;
    }

    // ----- C0-4: cuantos elementos vivos hay -----

    private void AlContarElementos(object remitente, RoutedEventArgs argumentos)
    {
        var totales = ContarElementosVivos(_vistaDeFilas);
        Anotar($"C0-4 elementos vivos bajo ItemsView: {totales.Todos} "
             + $"(contenedores de fila realizados: {totales.Contenedores}) con {CuantasFilas} filas");
        Anotar(DescribirLasBarras());
    }

    /// <summary>
    /// Busca las barras de desplazamiento en el arbol y dice donde estan y de que
    /// tamano. Sin esto, "la barra se ve" seria una opinion sobre una captura.
    /// </summary>
    private string DescribirLasBarras()
    {
        var descripciones = new List<string>();
        var pendientes = new Stack<DependencyObject>();
        pendientes.Push(_vistaDeFilas);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();

            if (actual is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar barra)
            {
                var esquina = barra.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point(0, 0));
                descripciones.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: en ({1:F0},{2:F0}) de {3:F0}x{4:F0} px, visible={5}, opacidad={6:F2}",
                    barra.Orientation, esquina.X, esquina.Y,
                    barra.ActualWidth, barra.ActualHeight, barra.Visibility, barra.Opacity));
            }

            var cuantosHijos = VisualTreeHelper.GetChildrenCount(actual);
            for (var i = 0; i < cuantosHijos; i++)
            {
                pendientes.Push(VisualTreeHelper.GetChild(actual, i));
            }
        }

        return descripciones.Count == 0
            ? "C0-2 barras encontradas en el arbol: NINGUNA"
            : "C0-2 barras: " + string.Join(" | ", descripciones);
    }

    /// <summary>Recorre el arbol visual y cuenta lo que de verdad existe.</summary>
    private static (int Todos, int Contenedores) ContarElementosVivos(DependencyObject raiz)
    {
        var todos = 0;
        var contenedores = 0;
        var pendientes = new Stack<DependencyObject>();
        pendientes.Push(raiz);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();
            todos++;

            if (actual is ItemContainer)
            {
                contenedores++;
            }

            var cuantosHijos = VisualTreeHelper.GetChildrenCount(actual);
            for (var i = 0; i < cuantosHijos; i++)
            {
                pendientes.Push(VisualTreeHelper.GetChild(actual, i));
            }
        }

        return (todos, contenedores);
    }

    // ----- C0-8: el OCR sobre los PDF reales -----

    private async void AlLeerLosPdf(object remitente, RoutedEventArgs argumentos)
    {
        await LeerLosPdfAsync();
    }

    /// <summary>
    /// El fallo se anota, no se traga: si el OCR revienta, la cifra que hace falta
    /// es el mensaje del error, no un silencio.
    /// </summary>
    private async Task LeerLosPdfAsync()
    {
        _botonDeOcr.IsEnabled = false;
        try
        {
            Anotar(await BancoDeOcr.MedirAsync());
        }
        catch (Exception fallo)
        {
            Anotar($"C0-8 FALLO: {fallo.GetType().Name}: {fallo.Message}");
        }
        finally
        {
            _botonDeOcr.IsEnabled = true;
        }
    }

    // ----- modo automatico, para medir sin manos -----

    private async Task MedirTodoYSalirAsync()
    {
        await MedirElPintadoAsync();
        AlContarElementos(this, new RoutedEventArgs());
        _vistaDeFilas.Select(0);
        await Task.Delay(300);

        if (Ajustes.LeeLosPdf)
        {
            await LeerLosPdfAsync();
        }

        if (Ajustes.CierraAlTerminar)
        {
            CerrarLaVentana();
        }
    }

    private void CerrarLaVentana()
    {
        MedicionDeArranque.Guardar(_cuaderno.ToString());
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => App.Ventana?.Close());
    }

    // ----- utilidades -----

    /// <summary>
    /// Espera a que la cola de la interfaz quede libre y a que pase un marco de
    /// dibujado. Es el equivalente del update_idletasks con el que se midio el Tk.
    /// </summary>
    private Task EsperarAlDibujadoAsync()
    {
        var espera = new TaskCompletionSource();

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            void AlDibujarElMarco(object? emisor, object argumentos)
            {
                CompositionTarget.Rendering -= AlDibujarElMarco;
                espera.TrySetResult();
            }

            CompositionTarget.Rendering += AlDibujarElMarco;
        });

        return espera.Task;
    }

    private static string Segundos(Stopwatch reloj) =>
        reloj.Elapsed.TotalSeconds.ToString("F4", CultureInfo.InvariantCulture);

    private void Anotar(string renglon)
    {
        _cuaderno.AppendLine(renglon);
        RefrescarElCuaderno();
    }

    /// <summary>Vuelca a la pantalla y al archivo lo anotado mas el estado vivo del desplazamiento.</summary>
    private void RefrescarElCuaderno()
    {
        var texto = _cuaderno.ToString() + _estadoDelDesplazamiento;
        _cuadernoDeCifras.Text = texto;
        MedicionDeArranque.Guardar(texto);
    }
}
