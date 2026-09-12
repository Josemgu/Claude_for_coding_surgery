using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Completar;

/// <summary>
/// La pestana octava: la cola de los documentos a los que les falta informacion.
/// </summary>
/// <remarks>
/// <para>Pedida por el dueno el 2026-09-06: <i>«debe haber una tab solo para esto:
/// completar información de documentos que faltan»</i>, y con el motivo delante: <i>«un
/// flujo de trabajo donde yo vaya resolviendo casos de manera automática, vaya donde tenga
/// que ir»</i>.</para>
///
/// <para>⛔ <b>Esta pantalla no escribe NADA.</b> Lee la cola, la pinta y navega. Ni una
/// firma, ni un estado, ni una fila: la regla permanente 5 de <c>CLAUDE.md</c> entera. Lo
/// unico que escribe en toda la cadena es <c>ModeloDeCorreccion.Guardar</c>, que es lo que
/// ya escribia antes de que existiera la cola y que solo guarda lo que una mano tecleo.</para>
///
/// <para><b>Que NO sustituye.</b> La pestana de Correccion sigue existiendo para ir a un
/// documento suelto por su desplegable; la cola no la sustituye, la usa. Y la ventana de
/// «lo que no está completo» de Inicio sigue estando: aquella agrupa por fecha para MIRAR,
/// esta encadena para TRABAJAR, y ademas aquella incluye los que el companero marco
/// <c>no_completa</c>, que aqui no entran (el motivo esta en <see cref="ColaDeCompletar"/>).</para>
///
/// <para>⚠️ <b>Lo que hay que saber del menu de la izquierda.</b> Mientras se encadena, el
/// marco ensena la pantalla de Correccion pero la entrada marcada sigue siendo «Completar»,
/// que es donde el dueno esta trabajando. Volver a pulsar «Completar» estando ya marcada NO
/// dispara nada —<c>NavigationView</c> solo avisa cuando la seleccion CAMBIA—, y por eso la
/// banda de la cola en Correccion trae su propio boton de volver.</para>
/// </remarks>
public sealed partial class PaginaDeCompletar : PaginaDeFichas
{
    /// <summary>Los renglones tal como se pintan, numerados; por su POSICION se sabe cual se pulso.</summary>
    private IReadOnlyList<PuestoEnLaCola> _puestos = [];

    /// <summary>La cola leida al entrar; nula hasta que llegan los servicios. Es la que se le entrega a Correccion.</summary>
    private ColaDeCompletar? _cola;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeCompletar() => InitializeComponent();

    /// <summary>Cuanto costo leer la cola la ultima vez, en milisegundos.</summary>
    public double MilisegundosDeLaLectura { get; private set; }

    /// <summary>La cola tal como quedo leida; la piden las mediciones.</summary>
    public ColaDeCompletar? Cola => _cola;

    /// <summary>Lee la cola y la pinta en cuanto llegan los servicios.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _deQueVa.Text = TextoDeLaCola.DeQueVaLaPantalla;
        _noQuedaNada.Text = TextoDeLaCola.CuandoNoQuedaNada;
        _botonDeEmpezar.Content = TextoDeLaCola.BotonDeEmpezar;

        LeerLaCola();
    }

    /// <summary>Lee la base y compone la cola, midiendo lo que cuesta.</summary>
    /// <remarks>
    /// Se lee AQUI y una sola vez por entrada. Lo que cuesta se anota en el cuaderno para
    /// que se vea y no se suponga, igual que hacen Inicio y Correccion.
    /// </remarks>
    private void LeerLaCola()
    {
        if (Servicios is null) return;

        var lector = new LectorDeIncompletos(
            Servicios.Casos, Servicios.Personas, Servicios.Companeros, Servicios.Asignaciones,
            Servicios.Reloj, Servicios.Procedencia);

        var cronometro = Stopwatch.StartNew();
        var cola = ColaDeCompletar.Desde(lector.Leer());
        cronometro.Stop();
        MilisegundosDeLaLectura = cronometro.Elapsed.TotalMilliseconds;

        _cola = cola;
        Pintar(cola);

        Servicios.Registro.AnotarNavegacion("Completar lee la cola", MilisegundosDeLaLectura);
    }

    /// <summary>Reparte la cola por la pantalla: cifras, renglones y el mensaje de vacia.</summary>
    /// <param name="cola">La cola recien leida.</param>
    private void Pintar(ColaDeCompletar cola)
    {
        _puestos = PuestoEnLaCola.Numerar(cola.Documentos);

        _denominador.Text = cola.LineaDelDenominador;
        _renglones.ItemsSource = _puestos;
        _noQuedaNada.Visibility = cola.EstaVacia ? Visibility.Visible : Visibility.Collapsed;
        _botonDeEmpezar.IsEnabled = !cola.EstaVacia;
    }

    /// <summary>Arranca el flujo por el primero de la cola, que es el que viaja antes.</summary>
    private void AlPulsarEmpezar(object quien, RoutedEventArgs cuando)
    {
        if (_cola?.Primero is not long primero)
        {
            Decir(TextoDeLaCola.NoHayPorDondeEmpezar);
            return;
        }

        EntrarEnLaColaPor(primero);
    }

    /// <summary>Entra en la cola por el documento que se pulso, no por el primero.</summary>
    /// <remarks>
    /// Se puede empezar por cualquiera a proposito: el orden es una propuesta —lo que viaja
    /// antes, arriba— y no una jaula. Desde ahi el encadenado sigue el mismo orden.
    /// </remarks>
    private void AlPulsarUnDocumento(object quien, RoutedEventArgs cuando)
    {
        if (QueSePulso(quien) is not PuestoEnLaCola puesto) return;
        EntrarEnLaColaPor(puesto.CasoId);
    }

    /// <summary>Lleva a Correccion con la cola en la mano y ese documento abierto.</summary>
    /// <remarks>
    /// ⚠️ <b>La cola se le entrega DESPUES de navegar y no como parametro de la navegacion</b>,
    /// y es a proposito: <c>Cascara/PaginaDeFichas.cs</c> recoge el parametro y lo trata
    /// como los servicios, y ese archivo es de otro terreno. Es el mismo camino que ya usa
    /// <c>Grupo/PaginaDeIncompletos</c> para abrir un documento, medido el 2026-09-04.
    /// </remarks>
    /// <param name="casoId">El documento por el que se entra; el encadenado sigue desde su puesto.</param>
    private void EntrarEnLaColaPor(long casoId)
    {
        if (Frame is null || Servicios is null || _cola is null) return;

        var marco = Frame;
        var cola = _cola;
        marco.Navigate(typeof(PaginaDeCorreccion), Servicios, new SuppressNavigationTransitionInfo());
        DispatcherQueue.TryEnqueue(() =>
        {
            if (marco.Content is PaginaDeCorreccion correccion) correccion.EntrarEnLaCola(cola, casoId);
        });
    }

    /// <summary>Deja una linea en el pie de la ventana; nunca un cuadro que detenga nada.</summary>
    /// <remarks>Requisito 9 del dueno: lo que hay que decir sale en la franja y en el acuse.</remarks>
    /// <param name="linea">Lo que se lee en el acuse del pie.</param>
    private static void Decir(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }

    /// <summary>
    /// Sube por el arbol desde el boton que se pulso hasta dar con el renglon, y devuelve
    /// el dato que le toca por su POSICION en la lista.
    /// </summary>
    /// <remarks>
    /// No se pregunta por el <c>DataContext</c>: un renglon realizado por un
    /// <c>ItemsRepeater</c> con una plantilla de <c>x:Bind</c> lo tiene vacio. Medido el
    /// 2026-09-04 en Inicio, y repetido aqui por lo mismo.
    /// </remarks>
    /// <param name="donde">El control que disparo el suceso, en cualquier nivel dentro del renglon.</param>
    /// <returns>El puesto de ese renglon, o nulo si el control no esta dentro de ninguno.</returns>
    private PuestoEnLaCola? QueSePulso(object? donde)
    {
        var actual = donde as DependencyObject;
        while (actual is not null)
        {
            var padre = VisualTreeHelper.GetParent(actual);

            if (actual is UIElement enPantalla && ReferenceEquals(padre, _renglones))
            {
                var posicion = _renglones.GetElementIndex(enPantalla);
                if (posicion >= 0 && posicion < _puestos.Count) return _puestos[posicion];
            }

            actual = padre;
        }

        return null;
    }
}
