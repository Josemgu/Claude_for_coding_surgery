using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.App.Inicio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Grupo;

/// <summary>
/// La ventana de lo que no esta completo, agrupado por fecha de viaje.
/// </summary>
/// <remarks>
/// <para>Palabras del dueno el 2026-09-05: <i>«Luego crea una ventana de revisar lo que no
/// está completo, y ponlo por grupo de fechas»</i>, y el motivo: <i>«No quiero revisar
/// gente que viaja en noviembre estando en septiembre»</i>.</para>
///
/// <para>Aqui vinieron a parar las cifras que Inicio dejo de ensenar —«Personas por
/// viajar», «Con la recomendacion completa», «Viajaron sin verificar», «Por verificar antes
/// de que salgan» y «Sin fecha de viaje»—: todas eran preguntas sobre lo que no esta
/// resuelto. No se borro nada del programa: se movio a donde el pidio verlo.</para>
///
/// <para>⚠️ <b>Se entra por el boton de Inicio y no por el menu de la izquierda.</b> Anadir
/// una octava entrada a ese menu es tocar <c>Cascara/VentanaPrincipal.xaml</c>, que esta
/// CONGELADO y es de otro terreno. Va dicho en la entrega; el dia que se descongele, es
/// una entrada mas y este comentario sobra.</para>
/// </remarks>
public sealed partial class PaginaDeIncompletos : PaginaDeFichas
{
    /// <summary>Lo último que se pintó, en el orden del repetidor; por posición se sabe qué renglón se pulsó.</summary>
    private IReadOnlyList<RenglonDeLoIncompleto> _lo = [];
    /// <summary>Lo que tardó la última lectura; lo expone <see cref="MilisegundosDeLaLectura"/> para que una prueba lo mida.</summary>
    private double _milisegundosDeLaLectura;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeIncompletos() => InitializeComponent();

    /// <summary>Cuanto costo leer la ventana la ultima vez, en milisegundos.</summary>
    public double MilisegundosDeLaLectura => _milisegundosDeLaLectura;

    /// <summary>Lee y pinta en cuanto llegan los servicios.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        // El dueno eligio el tema (2026-09-05) y esta pantalla le hace caso.
        PonerLaPaletaDelTema();
        ActualThemeChanged += AlCambiarElTema;

        Pintar();
    }

    /// <summary>El dueno cambio de tema: se vuelve a pintar entera con la paleta nueva.</summary>
    /// <param name="quien">La página cuyo tema cambió.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarElTema(FrameworkElement quien, object cuando)
    {
        PonerLaPaletaDelTema();
        Pintar();
    }

    /// <summary>
    /// Pone la paleta en el tema de esta pantalla y vuelve a resolver lo ya pintado. El
    /// motivo de <c>Bindings.Update()</c> esta escrito en <c>Inicio/PaginaDeInicio.xaml.cs</c>.
    /// </summary>
    private void PonerLaPaletaDelTema()
    {
        PinturaDeInicio.Tema = ActualTheme;
        Bindings.Update();
    }

    /// <summary>Lee la ventana entera y la reparte, midiendo lo que cuesta.</summary>
    private void Pintar()
    {
        if (Servicios is null) return;

        var lector = new LectorDeIncompletos(
            Servicios.Casos, Servicios.Personas, Servicios.Companeros, Servicios.Asignaciones,
            Servicios.Reloj, Servicios.Procedencia);

        var cronometro = Stopwatch.StartNew();
        var resumen = lector.Leer();
        var renglones = resumen.EnUnaSolaLista();
        cronometro.Stop();
        _milisegundosDeLaLectura = cronometro.Elapsed.TotalMilliseconds;

        _lo = renglones;
        _denominador.Text = resumen.LineaDelDenominador;
        _renglones.ItemsSource = renglones;
        _noQuedaNada.Visibility = PinturaDelGrupo.SeVe(resumen.NoQuedaNada);

        Servicios.Registro.AnotarNavegacion("Lo que no está completo", _milisegundosDeLaLectura);

        // Requisito 4 y 9: una linea en la franja de la cascara y nada mas; ni un cuadro.
        Servicios.Avisos.Dejar(resumen.Avisos);
    }

    /// <summary>Vuelve a la pantalla de Inicio.</summary>
    /// <param name="quien">El botón de volver.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPedirVolver(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        Frame.Navigate(typeof(PaginaDeInicio), Servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>Abre el grupo entero del dia cuya cabecera se pulso.</summary>
    /// <param name="quien">El botón de la cabecera de fecha que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPedirElGrupoDeEseDia(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (QueSePulso(quien) is not RenglonDeLoIncompleto renglon || !renglon.SePuedeAbrirElGrupo) return;
        if (FechasEnEspanol.Leer(renglon.FechaIso) is not DateOnly fecha) return;

        Frame.Navigate(typeof(PaginaDeGrupo), new LlegadaAlGrupo(Servicios, fecha), new SuppressNavigationTransitionInfo());
    }

    /// <summary>Pulsar un documento lo abre en Correccion, que es donde se corrige.</summary>
    /// <remarks>
    /// ⚠️ Desde el 2026-09-07 se le dice ademas de DONDE viene, para que traiga su boton de
    /// volver. Palabras del dueno: <i>«No tengo opción de regresar a la ventana de atrás, que
    /// quiero seguir trabajando»</i>.
    /// </remarks>
    /// <param name="quien">El botón del renglón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarUnDocumento(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (QueSePulso(quien) is not RenglonDeLoIncompleto renglon) return;
        if (renglon.EsCabecera || renglon.CasoId == 0) return;

        var marco = Frame;
        marco.Navigate(typeof(PaginaDeCorreccion), Servicios, new SuppressNavigationTransitionInfo());
        DispatcherQueue.TryEnqueue(() =>
        {
            if (marco.Content is not PaginaDeCorreccion correccion) return;
            correccion.AbrirElCaso(renglon.CasoId);
            correccion.MostrarLaVueltaA("a lo que no está completo");
        });
    }

    /// <summary>
    /// Sube por el arbol desde el boton que se pulso hasta dar con el renglon, y devuelve
    /// el dato que le toca por su POSICION en la lista.
    /// </summary>
    /// <remarks>
    /// No se pregunta por el <c>DataContext</c>: un renglon realizado por un
    /// <c>ItemsRepeater</c> con una plantilla de <c>x:Bind</c> lo tiene vacio. Medido el
    /// 2026-09-04 en Inicio.
    /// </remarks>
    /// <param name="donde">El control que se pulsó, en cualquier profundidad dentro de un renglón.</param>
    /// <returns>El renglón pulsado, o nulo si el control no cuelga del repetidor.</returns>
    private RenglonDeLoIncompleto? QueSePulso(object? donde)
    {
        var actual = donde as DependencyObject;
        while (actual is not null)
        {
            var padre = VisualTreeHelper.GetParent(actual);

            if (actual is UIElement enPantalla && ReferenceEquals(padre, _renglones))
            {
                var posicion = _renglones.GetElementIndex(enPantalla);
                if (posicion >= 0 && posicion < _lo.Count) return _lo[posicion];
            }

            actual = padre;
        }

        return null;
    }
}
