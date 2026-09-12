using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.App.Inicio;
using Fichas.App.Revisar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Fichas.App.Flujo;

/// <summary>
/// La pestana del flujo de trabajo: las TRES listas que estaban en Inicio.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Palabras del dueno el 2026-09-07, repetidas el 2026-09-09
/// porque todavia no estaba hecho: <i>«Listo para asignar debe ser una ventana, no debe estar
/// en el home del sistema. Tampoco asignar a los agentes, eso debe estar en otra ventana. Y
/// listo para viajar igual: los tres en una misma pestana»</i>.</para>
///
/// <para>⛔ <b>Lee por el MISMO <see cref="LectorDelInicio"/> que Inicio, y eso no es
/// pereza.</b> El cuadro de cifras que queda en Inicio y estas tres listas contestan la misma
/// pregunta con distinta letra: si cada pantalla montara su propio lector, el dia que alguien
/// cambiara una regla el dueno leeria un numero en Inicio y contaria otro aqui. Es la
/// enfermedad que este proyecto ya diagnostico el 2026-09-07 con las cuatro palabras de
/// estado, y no se vuelve a abrir la puerta.</para>
///
/// <para>Esta clase solo REPARTE lo que calcula el lector. Ni una regla de negocio vive aqui:
/// si la hubiera, habria que abrir una ventana para probarla (ADR-0003 §8.1).</para>
/// </remarks>
public sealed partial class PaginaDelFlujo : PaginaDeFichas
{
    /// <summary>Lo último que se pintó en la primera lista, en el mismo orden que el repetidor; por posición se sabe qué renglón se pulsó.</summary>
    private IReadOnlyList<RenglonDeCaso> _loListo = [];
    /// <summary>Lo último que se pintó en la segunda lista, en el orden del repetidor.</summary>
    private IReadOnlyList<RenglonDeCaso> _loAsignado = [];
    /// <summary>Las personas de la tercera lista: lo vencido delante y lo que apremia detrás, en el orden del repetidor.</summary>
    private IReadOnlyList<PersonaConTicket> _losTickets = [];

    /// <summary>
    /// Las ventanas de preguntas abiertas, por documento.
    /// </summary>
    /// <remarks>
    /// Dos ventanas del MISMO documento se pisarian: al guardar en una, la otra seguiria
    /// ensenando lo de antes y quien mirara la segunda creeria que no se guardo. De documentos
    /// distintos si puede haber varias. Es la misma disciplina que ya usa Revisar.
    /// </remarks>
    private readonly Dictionary<long, VentanaDeLasPreguntas> _ventanasDePreguntas = [];

    /// <summary>El mismo lector que usa Inicio; nulo hasta que llegan los servicios en <see cref="AlLlegar"/>.</summary>
    private LectorDelInicio? _lector;
    /// <summary>Lo que tardó la última lectura completa; lo expone <see cref="MilisegundosDeLaLectura"/> para que una prueba lo mida.</summary>
    private double _milisegundosDeLaLectura;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDelFlujo() => InitializeComponent();

    /// <summary>Cuanto costo la ultima lectura completa, en milisegundos.</summary>
    public double MilisegundosDeLaLectura => _milisegundosDeLaLectura;

    /// <summary>Monta el lector y pinta las tres listas.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        // El dueno eligio el tema (2026-09-05) y esta pantalla le hace caso.
        PonerLaPaletaDelTema();
        ActualThemeChanged += AlCambiarElTema;

        _lector = new LectorDelInicio(
            Servicios.Casos, Servicios.Personas, Servicios.Companeros, Servicios.Asignaciones,
            Servicios.Reloj, Servicios.Procedencia);

        Pintar();
    }

    /// <summary>Lee el panel entero y lo reparte por la pantalla, midiendo lo que cuesta.</summary>
    private void Pintar()
    {
        if (_lector is null || Servicios is null) return;

        var cronometro = Stopwatch.StartNew();
        var resumen = _lector.Leer();
        cronometro.Stop();
        _milisegundosDeLaLectura = cronometro.Elapsed.TotalMilliseconds;

        _denominador.Text = resumen.LineaDelDenominador;
        LlenarLoListo(resumen);
        LlenarLoAsignado(resumen);
        LlenarLoDelSistemaDelObispo(resumen);

        Servicios.Registro.AnotarNavegacion("Flujo de trabajo", _milisegundosDeLaLectura);

        // Requisito 4 y 9: lo que no cuadra se dice en UNA linea en la franja de la cascara y
        // no detiene nada. Aqui no se abre ni un cuadro.
        Servicios.Avisos.Dejar(resumen.Avisos);
    }

    /// <summary>La primera lista: lo que no le falta nada y no lo lleva nadie.</summary>
    /// <param name="resumen">Lo que acaba de leer el lector de Inicio.</param>
    private void LlenarLoListo(ResumenDeInicio resumen)
    {
        _loListo = resumen.Listos;
        _listosRenglones.ItemsSource = resumen.Listos;
        _cuantosListos.Text = EnCifras(resumen.Contadores.ListoParaAsignar);
        _deQueVanLosListos.Text = resumen.DeQueVaLoListo;
    }

    /// <summary>La segunda: lo que ahora mismo llevan los companeros, y cuanto lleva cada uno.</summary>
    /// <param name="resumen">Lo que acaba de leer el lector de Inicio.</param>
    private void LlenarLoAsignado(ResumenDeInicio resumen)
    {
        _loAsignado = resumen.Asignados;
        _asignadosRenglones.ItemsSource = resumen.Asignados;
        _cuantosAsignados.Text = EnCifras(resumen.Contadores.AsignadoALosAgentes);
        _deQueVanLosAsignados.Text = resumen.DeQueVaLoAsignado;
        _equipoRenglones.ItemsSource = resumen.Equipo;
    }

    /// <summary>La tercera: lo que hay que verificar en el sistema del obispo, en PERSONAS.</summary>
    /// <remarks>
    /// La lista junta lo vencido y lo que apremia, con lo vencido delante: son dos cuentas
    /// distintas —cada una con su linea— pero una sola lista de personas, porque lo que el hace
    /// con las dos es lo mismo, llamar al obispo, y dos listas obligarian a mirar dos veces.
    /// </remarks>
    /// <param name="resumen">Lo que acaba de leer el lector de Inicio.</param>
    private void LlenarLoDelSistemaDelObispo(ResumenDeInicio resumen)
    {
        var loSuyo = resumen.ElSistemaDelObispo;

        _elGrupoQueViene.Text = loSuyo.FraseDelProximoGrupo;
        _loVencido.Text = loSuyo.FraseDeLoVencido;
        _loQueApremia.Text = loSuyo.FraseDeLoQueApremia;

        _losTickets = [.. loSuyo.Vencidas, .. loSuyo.QueApremian];
        _ticketsRenglones.ItemsSource = _losTickets;
    }

    /// <summary>Escribe un numero sin que el idioma de la maquina le cambie el separador.</summary>
    /// <param name="numero">La cifra que va en un contador.</param>
    private static string EnCifras(int numero)
        => numero.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>El dueno cambio de tema: se vuelve a pintar entera con la paleta nueva.</summary>
    /// <param name="quien">La página cuyo tema cambió.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarElTema(FrameworkElement quien, object cuando)
    {
        PonerLaPaletaDelTema();
        Pintar();
    }

    /// <summary>
    /// Pone la paleta en el tema de esta pantalla y vuelve a resolver lo ya pintado. El motivo
    /// de <c>Bindings.Update()</c> esta escrito entero en <c>Inicio/PaginaDeInicio.xaml.cs</c>.
    /// </summary>
    private void PonerLaPaletaDelTema()
    {
        PinturaDeInicio.Tema = ActualTheme;
        Bindings.Update();
    }

    /// <summary>Pulsar un documento listo lo abre en Correccion.</summary>
    /// <param name="quien">El botón del renglón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarUnDocumentoListo(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_loListo, _listosRenglones, quien) is RenglonDeCaso renglon)
            AbrirElCasoEnCorreccion(renglon.CasoId);
    }

    /// <summary>Pulsar un documento asignado lo abre en Correccion.</summary>
    /// <param name="quien">El botón del renglón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarUnDocumentoAsignado(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_loAsignado, _asignadosRenglones, quien) is RenglonDeCaso renglon)
            AbrirElCasoEnCorreccion(renglon.CasoId);
    }

    /// <summary>
    /// Pulsar una persona sin la recomendacion confirmada abre donde se CONTESTAN sus seis
    /// preguntas, no donde solo se leen.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Esto arregla un defecto medido, y conviene saber cual era.</b> En Inicio,
    /// esta misma pulsacion abria el documento en Correccion, donde las seis preguntas son de
    /// SOLO LECTURA. El comentario de aquella linea decia que era provisional <i>«porque la
    /// ventana de la persona todavia no existe»</i>: existe desde entonces
    /// (<see cref="VentanaDeLasPreguntas"/>), y nadie volvio a cambiar la linea. El dueno
    /// pulsaba lo que le falta y llegaba a un sitio donde no lo podia arreglar.</para>
    ///
    /// <para>⛔ <b>Se ABRE la ventana de Revisar, no se toca.</b> <c>Revisar/</c> es otro
    /// terreno: aqui se llama a un constructor publico y nada mas, igual que esta pantalla
    /// llama a <c>PaginaDeCorreccion.AbrirElCaso</c> sin entrar en Correccion.</para>
    ///
    /// <para>⚠️ La ventana es de un DOCUMENTO y aqui se pulsa una PERSONA. Se abre el documento
    /// de esa persona, que trae a todas las suyas con sus seis preguntas cada una: es lo mas
    /// cerca que se llega sin tocar el otro terreno, y ya se contesta ahi mismo. Que la ventana
    /// pudiera abrirse directamente en la persona pulsada queda dicho y NO hecho.</para>
    /// </remarks>
    /// <param name="quien">El botón del renglón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarUnaPersonaConTicket(object quien, RoutedEventArgs cuando)
    {
        if (Servicios is null) return;
        if (EnLaLista(_losTickets, _ticketsRenglones, quien) is not PersonaConTicket ticket) return;

        if (_ventanasDePreguntas.TryGetValue(ticket.CasoId, out var abierta))
        {
            abierta.Activate();
            return;
        }

        var ventana = new VentanaDeLasPreguntas(Servicios, ticket.CasoId, ActualTheme);
        _ventanasDePreguntas[ticket.CasoId] = ventana;

        // Al cerrarse se olvida, o el segundo intento activaria una ventana que ya no existe y
        // el programa se caeria con el documento delante.
        ventana.Closed += (_, _) => _ventanasDePreguntas.Remove(ticket.CasoId);

        ventana.Activate();
    }

    /// <summary>
    /// Al salir de esta pestana se cierran las ventanas de preguntas que queden abiertas.
    /// </summary>
    /// <remarks>
    /// Una ventana de preguntas viva sobre una pantalla que ya no existe seguiria leyendo y
    /// escribiendo en la base sin que nadie la vea desde el programa.
    /// </remarks>
    /// <param name="cuando">Los datos de la navegación, que se pasan a la base.</param>
    protected override void OnNavigatedFrom(NavigationEventArgs cuando)
    {
        foreach (var ventana in _ventanasDePreguntas.Values.ToList()) ventana.Close();
        _ventanasDePreguntas.Clear();
        base.OnNavigatedFrom(cuando);
    }

    /// <summary>
    /// Sube por el arbol desde el boton que se pulso hasta el renglon del repetidor, y
    /// devuelve el dato que le toca por su POSICION en la lista.
    /// </summary>
    /// <remarks>
    /// ⚠️ No se pregunta por el <c>DataContext</c>: medido el 2026-09-04, un renglon realizado
    /// por un <c>ItemsRepeater</c> con una plantilla de <c>x:Bind</c> lo tiene VACIO, porque
    /// las ataduras compiladas se actualizan por otro camino. La posicion si la sabe el
    /// repetidor.
    /// </remarks>
    /// <typeparam name="T">El tipo de dato de cada renglón.</typeparam>
    /// <param name="lista">Los datos que se le dieron al repetidor, en su mismo orden.</param>
    /// <param name="repetidor">El repetidor que pintó la lista.</param>
    /// <param name="quien">El control que se pulsó, en cualquier profundidad dentro de un renglón.</param>
    /// <returns>El dato del renglón pulsado, o nulo si el control no cuelga de este repetidor.</returns>
    private static T? EnLaLista<T>(IReadOnlyList<T> lista, ItemsRepeater repetidor, object? quien)
        where T : class
    {
        var actual = quien as DependencyObject;
        while (actual is not null)
        {
            var padre = VisualTreeHelper.GetParent(actual);

            if (actual is UIElement enPantalla && ReferenceEquals(padre, repetidor))
            {
                var posicion = repetidor.GetElementIndex(enPantalla);
                return posicion >= 0 && posicion < lista.Count ? lista[posicion] : null;
            }

            actual = padre;
        }

        return null;
    }

    /// <summary>
    /// Pulsar un documento lo abre en Correccion, por el marco de la cascara.
    /// </summary>
    /// <remarks>
    /// ⚠️ La peticion va POR LA COLA del hilo de la interfaz: al navegar, Correccion abre
    /// PRIMERO su propio caso y despues se le pide el que se pulso. Medido el 2026-09-04.
    /// </remarks>
    /// <param name="casoId">El documento que se abre.</param>
    private void AbrirElCasoEnCorreccion(long casoId)
    {
        var marco = Frame;
        marco.Navigate(typeof(PaginaDeCorreccion), Servicios, new SuppressNavigationTransitionInfo());
        DispatcherQueue.TryEnqueue(() =>
        {
            if (marco.Content is not PaginaDeCorreccion correccion) return;
            correccion.AbrirElCaso(casoId);
            correccion.MostrarLaVueltaA("al flujo de trabajo");
        });
    }
}
