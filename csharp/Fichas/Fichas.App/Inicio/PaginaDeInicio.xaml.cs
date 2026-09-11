using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.App.Grupo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Inicio;

/// <summary>
/// La pantalla de Inicio: el cuadro de dos cifras y el calendario. Nada mas.
/// </summary>
/// <remarks>
/// <para>⛔ Terreno del programador de Inicio (fases C1, C7 y C12). Nadie mas escribe aqui.</para>
///
/// <para><b>Que hay y por que hay solo eso.</b> Palabras del dueno el 2026-09-07, repetidas el
/// 2026-09-09 porque no se habia hecho: <i>«Lo unico que quiero [en Inicio] es el calendario y
/// un cuadro informando cuales hacen falta por completar, y cuantos casos tienen los
/// agentes»</i>. Las tres listas que esta pantalla tenia —lo listo para asignar, lo asignado a
/// los agentes y lo listo para viajar— viven enteras en <c>Flujo/PaginaDelFlujo.xaml</c>, con
/// entrada propia en el menu.</para>
///
/// <para>Esta clase solo REPARTE lo que calcula <see cref="LectorDelInicio"/>. Ni una regla de
/// negocio vive aqui: si la hubiera, habria que abrir una ventana para probarla, y las pruebas
/// de <c>PruebasDeInicio</c> dejarian de poder existir (ADR-0003 §8.1).</para>
/// </remarks>
public sealed partial class PaginaDeInicio : PaginaDeFichas
{
    private IReadOnlyList<DiaDelCalendario> _losDias = [];

    private LectorDelInicio? _lector;
    private int _mesQueSeEnsena;
    private int _cuantasVecesSeMidio;
    private double _milisegundosDelResumen;
    private Stopwatch? _relojDelPintado;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeInicio() => InitializeComponent();

    /// <summary>Cuanto costo la ultima lectura completa del panel, en milisegundos.</summary>
    public double MilisegundosDelResumen => _milisegundosDelResumen;

    /// <summary>Monta el lector con los seis puertos que necesita y pinta el mes de hoy.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        // El dueno eligio el tema (2026-09-05) y esta pantalla le hace caso.
        PonerLaPaletaDelTema();
        ActualThemeChanged += AlCambiarElTema;

        _lector = new LectorDelInicio(
            Servicios.Casos, Servicios.Personas, Servicios.Companeros, Servicios.Asignaciones,
            Servicios.Reloj, Servicios.Procedencia);

        _inicialesDeLosDias.ItemsSource = FechasEnEspanol.InicialesDeLosDias;

        QuitarLaAsignacionALoArchivadoDeAntes();

        if (MedicionDeInicio.EstaPedida)
        {
            _relojDelPintado = Stopwatch.StartNew();
            LayoutUpdated += AlTerminarDeColocar;
            Escuchar(_desplazamientoDeLaPantalla, "la pantalla");
        }

        Pintar(_mesQueSeEnsena);
    }

    /// <summary>Lee el panel entero y lo reparte por la pantalla, midiendo lo que cuesta.</summary>
    private void Pintar(int desplazamientoDeMes)
    {
        if (_lector is null || Servicios is null) return;

        var cronometro = Stopwatch.StartNew();
        var resumen = _lector.Leer(desplazamientoDeMes);
        cronometro.Stop();
        _milisegundosDelResumen = cronometro.Elapsed.TotalMilliseconds;

        LlenarElCuadro(resumen);
        LlenarElCalendario(resumen);

        Servicios.Registro.AnotarNavegacion("Inicio calcula su resumen", _milisegundosDelResumen);

        // Requisito 4 y 9: lo que no cuadra se dice en UNA linea en la franja de la
        // cascara y no detiene nada. Aqui no se abre ni un cuadro.
        Servicios.Avisos.Dejar(resumen.Avisos);
    }

    /// <summary>
    /// El caso de los 1 000: lo archivado ANTES del 2026-09-11 con la asignacion viva se limpia
    /// aqui, al llegar, porque esta es la primera pantalla y la que ensena cuanto lleva cada uno.
    /// </summary>
    /// <remarks>
    /// <para>La regla no vive aqui: es <see cref="Revisar.RetiradaAlArchivar"/>, y se prueba sin
    /// ventana. Esta pantalla solo la llama ANTES de leer, para que el cuadro que se pinta ya
    /// diga la cifra buena, y deja una linea en la franja SOLO si retiro algo: una limpieza que
    /// no encuentra nada no tiene por que saludar.</para>
    ///
    /// <para>Se anota lo que costo en el registro por lo mismo que se anota el resumen: esta
    /// pantalla tiene techo de 200 ms y una pasada que escribe no puede colarse sin medirse.</para>
    /// </remarks>
    private void QuitarLaAsignacionALoArchivadoDeAntes()
    {
        if (Servicios is null) return;

        var reparto = new Asignar.OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);
        var retirada = new Revisar.RetiradaAlArchivar(Servicios.Asignaciones, Servicios.Casos, reparto);

        var cronometro = Stopwatch.StartNew();
        var limpieza = retirada.QuitarLasDeLoQueYaEstabaArchivado();
        cronometro.Stop();

        Servicios.Registro.AnotarNavegacion("Inicio limpia lo archivado de antes", cronometro.Elapsed.TotalMilliseconds);
        if (limpieza.HuboAlgo) Servicios.Avisos.Dejar(Contratos.Modelos.Aviso.Informa(limpieza.Linea));
    }

    /// <summary>
    /// El cuadro de las dos cifras: lo que falta por completar y lo que llevan los agentes.
    /// </summary>
    /// <remarks>
    /// Las dos salen de la MISMA lectura que arma el calendario, sin una segunda pasada por la
    /// base; el motivo esta en <see cref="LectorDelInicio"/> y lo cronometra
    /// <c>PruebasDelCuadroDeInicio</c>.
    /// </remarks>
    private void LlenarElCuadro(ResumenDeInicio resumen)
    {
        var cuadro = resumen.Cuadro;

        _cuantosPorCompletar.Text = cuadro.CifraDeLoQueFalta;
        _deQueVaLoQueFalta.Text = cuadro.LineaDeLoQueFalta;
        _cuantosEnLosAgentes.Text = cuadro.CifraDeLosAgentes;
        _deQueVanLosAgentes.Text = cuadro.LineaDeLosAgentes;

        // El nombre para el lector de pantalla se pone aqui y no en el XAML: lleva la cifra
        // dentro, y quien no ve la pantalla no puede leer el numero grande por separado.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_verLoIncompleto, cuadro.LoQueFaltaParaElLector);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_verElFlujo, cuadro.LosAgentesParaElLector);
    }

    /// <summary>El calendario del mes que se ensena, con su titulo, la fecha de hoy y el denominador.</summary>
    private void LlenarElCalendario(ResumenDeInicio resumen)
    {
        var hoy = FechasEnEspanol.Leer(_lector!.Hoy);
        _hoy.Text = "Hoy es " + (hoy is DateOnly dia ? FechasEnEspanol.DecirElDiaCompleto(dia) : _lector.Hoy);
        _denominador.Text = resumen.LineaDelDenominador;

        _losDias = resumen.Mes.Dias;
        _tituloDelMes.Text = resumen.Mes.Titulo;
        _mes.ItemsSource = resumen.Mes.Dias;
    }

    /// <summary>Retrocede un mes en el calendario.</summary>
    private void AlPedirElMesAnterior(object quien, RoutedEventArgs cuando) => MoverElMes(_mesQueSeEnsena - 1);

    /// <summary>Vuelve al mes de hoy.</summary>
    private void AlPedirElMesDeHoy(object quien, RoutedEventArgs cuando) => MoverElMes(0);

    /// <summary>Avanza un mes en el calendario.</summary>
    private void AlPedirElMesSiguiente(object quien, RoutedEventArgs cuando) => MoverElMes(_mesQueSeEnsena + 1);

    /// <summary>Cambia el mes que se ensena y vuelve a pintar.</summary>
    private void MoverElMes(int desplazamiento)
    {
        _mesQueSeEnsena = desplazamiento;
        Pintar(_mesQueSeEnsena);
    }

    /// <summary>El dueno cambio de tema: se vuelve a pintar entera con la paleta nueva.</summary>
    private void AlCambiarElTema(FrameworkElement quien, object cuando)
    {
        PonerLaPaletaDelTema();
        Pintar(_mesQueSeEnsena);
    }

    /// <summary>
    /// Pone la paleta en el tema de esta pantalla y vuelve a resolver lo ya pintado.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>Bindings.Update()</c> hace falta y no sobra: la cabecera, los bordes y el fondo
    /// se resolvieron en <c>InitializeComponent()</c>, que corrio en el constructor, cuando
    /// la pagina todavia no estaba en el arbol y no se sabia su <c>ActualTheme</c>. Sin esta
    /// linea, cambiar el tema dejaria los renglones del color nuevo y el marco del viejo.
    /// Los renglones se resuelven solos al volver a pintar, porque cada pintado arma una
    /// lista nueva y el repetidor infla las plantillas otra vez.
    /// </remarks>
    private void PonerLaPaletaDelTema()
    {
        PinturaDeInicio.Tema = ActualTheme;
        Bindings.Update();
    }

    /// <summary>
    /// La primera cifra del cuadro abre la ventana de lo que no esta completo.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es la unica puerta de todo el programa a esa ventana</b>, medido el 2026-09-09
    /// buscando cada <c>Navigate(typeof(PaginaDeIncompletos)</c> del codigo. Hasta hoy era un
    /// boton suelto con un rotulo; ahora es la cifra, que ademas dice cuantos son antes de
    /// pulsar. <c>PruebasDeLaPestanaDelFlujo</c> vigila que la puerta siga aqui.
    /// </remarks>
    private void AlPedirLoIncompleto(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        Frame.Navigate(typeof(PaginaDeIncompletos), Servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>
    /// La segunda cifra abre la pestana del flujo, donde estan las tres listas.
    /// </summary>
    /// <remarks>
    /// ⚠️ La navegacion va por el marco de esta pagina, no por el menu de la izquierda: la
    /// entrada de ese menu no se marca al llegar asi, porque eso lo decide
    /// <c>Cascara/VentanaPrincipal.xaml.cs</c>. La puerta principal del flujo es su entrada del
    /// menu, que si marca; esta es el atajo desde la cifra que se acaba de leer.
    /// </remarks>
    private void AlPedirElFlujo(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        Frame.Navigate(typeof(Flujo.PaginaDelFlujo), Servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>Pulsar un dia del calendario abre el grupo que viaja ese dia.</summary>
    private void AlPulsarUnDia(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_losDias, _mes, quien) is DiaDelCalendario dia) AbrirElGrupoDelDia(dia);
    }

    /// <summary>
    /// Sube por el arbol desde el boton que se pulso hasta el renglon del repetidor, y
    /// devuelve el dato que le toca por su POSICION en la lista.
    /// </summary>
    /// <remarks>
    /// ⚠️ No se pregunta por el <c>DataContext</c>, y se dice por que: medido el
    /// 2026-09-04, un renglon realizado por un <c>ItemsRepeater</c> con una plantilla de
    /// <c>x:Bind</c> tiene el <c>DataContext</c> VACIO —la busqueda devolvia «nada»—,
    /// porque las ataduras compiladas se actualizan por otro camino y no pasan por ahi.
    /// La posicion sí la sabe el repetidor.
    /// </remarks>
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
    /// Pulsar un dia abre el grupo que viaja ese dia.
    /// </summary>
    /// <remarks>
    /// <para>Es lo que pidio el dueno el 2026-09-05: <i>«yo debo poder darle clic y ver
    /// solamente al grupo de personas que viajará en esa fecha, con su PDF»</i>. Hasta ese
    /// dia el calendario no tenia ni un manejador: era adorno, y el tenia razon.</para>
    ///
    /// <para>Un dia SIN grupos no hace nada y no da error (criterio C12-6). No se deja un
    /// aviso: pulsar un dia vacio no es un fallo, y la franja se llenaria de lineas por
    /// cada clic perdido.</para>
    ///
    /// <para>⚠️ La navegacion va por el marco de esta pagina, no por el menu de la
    /// izquierda: la entrada de ese menu no se marca al llegar asi porque eso lo decide
    /// <c>Cascara/VentanaPrincipal.xaml.cs</c>. Por eso la pantalla del grupo trae su propio
    /// boton de volver.</para>
    /// </remarks>
    private void AbrirElGrupoDelDia(DiaDelCalendario dia)
    {
        if (!dia.SePuedePulsar) return;
        Frame.Navigate(typeof(PaginaDeGrupo), new LlegadaAlGrupo(Servicios!, dia.Fecha), new SuppressNavigationTransitionInfo());
    }

    /// <summary>
    /// Anota lo que un desplazamiento puede bajar y cada vez que baja.
    /// </summary>
    /// <remarks>
    /// Es lo que convierte «se puede bajar» en una cifra: sin esto, una rueda de raton
    /// empujada desde fuera no dejaria ninguna huella y «la lista tambien baja» seria
    /// una afirmacion.
    /// </remarks>
    private static void Escuchar(ScrollView cual, string comoSeLlama)
        => cual.ViewChanged += (quien, cuando) => MedicionDeInicio.Anotar(
            $"RUEDA   «{comoSeLlama}» se movio a {MedicionDeInicio.Cifra(cual.VerticalOffset)} px "
            + $"de {MedicionDeInicio.Cifra(cual.ScrollableHeight)} px que se pueden bajar");

    /// <summary>
    /// Cada vez que la pantalla queda colocada tras un pintado cronometrado, escribe su
    /// informe. Mide DOS veces: la primera trae el coste de una sola vez —inflar las
    /// plantillas y compilar el codigo al vuelo—; la segunda, lo que cuesta cada vez.
    /// </summary>
    /// <remarks>
    /// Se mide AQUI y no al terminar <see cref="Pintar"/>: lo que el dueno ve pintado no
    /// es el momento en que se asignan las listas, sino aquel en que WinUI ha medido y
    /// colocado los elementos. Cronometrar lo primero daria una cifra bonita y falsa.
    /// </remarks>
    private void AlTerminarDeColocar(object? quien, object cuando)
    {
        if (_relojDelPintado is null) return;

        _relojDelPintado.Stop();
        var milisegundos = _relojDelPintado.Elapsed.TotalMilliseconds;
        _relojDelPintado = null;
        _cuantasVecesSeMidio++;

        if (_cuantasVecesSeMidio == 1)
        {
            EscribirElInforme("primera vez, con las plantillas todavía sin inflar", milisegundos);
            DispatcherQueue.TryEnqueue(() =>
            {
                _relojDelPintado = Stopwatch.StartNew();
                Pintar(_mesQueSeEnsena);
            });
            return;
        }

        if (_cuantasVecesSeMidio == 2)
        {
            EscribirElInforme("repintado con los mismos datos", milisegundos);

            // Tercera pasada con la pantalla abajo del todo. Sin ella, el calendario
            // queda fuera de la vista, el repetidor solo construye UNA celda y la cuenta
            // de elementos vivos saldria mas baja de lo que de verdad llega a ser.
            //
            // ⚠️ Para quien vaya a probar el clic desde fuera: esto MUEVE la pantalla. Un
            // guion que pulse en unas coordenadas fijas y ademas pida medir estara
            // pulsando sobre otra cosa. Se aprendio perdiendo dos vueltas el 2026-09-04.
            DispatcherQueue.TryEnqueue(() =>
            {
                _relojDelPintado = Stopwatch.StartNew();
                _desplazamientoDeLaPantalla.ScrollTo(
                    0,
                    _desplazamientoDeLaPantalla.ScrollableHeight,
                    new ScrollingScrollOptions(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
            });
            return;
        }

        LayoutUpdated -= AlTerminarDeColocar;
        EscribirElInforme("con la pantalla abajo del todo y el calendario a la vista", milisegundos);

        // El cierre va por la cola del hilo de la interfaz: cerrar desde dentro de
        // LayoutUpdated mata el proceso con 0xC0000602 antes de vaciar el informe.
        if (MedicionDeInicio.CierraAlTerminar) DispatcherQueue.TryEnqueue(() => Application.Current.Exit());
    }

    /// <summary>Vuelca las cifras de los criterios C1-2, C1-3, C1-5 y C12-3 al informe.</summary>
    /// <remarks>
    /// ⚠️ Desde el 2026-09-09 ya no anota las cuatro listas: se fueron a la pestana del flujo.
    /// Lo que queda medido es lo que queda en pantalla —el cuadro y el calendario—, y las dos
    /// cifras del cuadro se escriben enteras para poder comprobarlas sin abrir la ventana.
    /// </remarks>
    private void EscribirElInforme(string deQuePasada, double milisegundosHastaColocar)
    {
        var casos = Servicios?.Argumentos.CasosInventados ?? 0;
        var pantalla = MedicionDeInicio.ContarElementosVivos(_desplazamientoDeLaPantalla);
        var calendario = MedicionDeInicio.ContarElementosVivos(_mes);

        MedicionDeInicio.Anotar($"===== Inicio con {casos} casos inventados, {ActualWidth:F0}x{ActualHeight:F0} px de pantalla — {deQuePasada} =====");
        MedicionDeInicio.Anotar($"C1-5   resumen calculado en {MedicionDeInicio.Cifra(_milisegundosDelResumen)} ms");
        MedicionDeInicio.Anotar($"C12-3  pintado hasta quedar colocado: {MedicionDeInicio.Cifra(milisegundosHastaColocar)} ms "
            + $"({MedicionDeInicio.Cifra(milisegundosHastaColocar / 1000.0, 3)} s)");
        MedicionDeInicio.Anotar($"C1-5   elementos vivos en toda la pantalla: {pantalla.Todos}");
        MedicionDeInicio.Anotar($"CUADRO «Me falta por completar»: {_cuantosPorCompletar.Text} — {_deQueVaLoQueFalta.Text}");
        MedicionDeInicio.Anotar($"CUADRO «Lo que tienen los agentes»: {_cuantosEnLosAgentes.Text} — {_deQueVanLosAgentes.Text}");
        MedicionDeInicio.Anotar($"C1-1   denominador: «{_denominador.Text}»");
        MedicionDeInicio.Anotar($"C12-2  calendario del mes: {calendario.Todos} elementos con 42 celdas fijas, "
            + $"{MedicionDeInicio.RenglonesRealizados(_mes)} realizadas; la mas llena pide "
            + $"{MedicionDeInicio.Cifra(MedicionDeInicio.AltoQuePideElContenido(_mes))} px de alto");
        MedicionDeInicio.Anotar($"C1-3   la pantalla mide {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ExtentHeight)} px de alto "
            + $"y se ven {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ViewportHeight)} px: "
            + $"se pueden bajar {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ScrollableHeight)} px");
        MedicionDeInicio.Anotar($"C1-2   {MedicionDeInicio.DescribirLasBarras(_desplazamientoDeLaPantalla, this)}");
        MedicionDeInicio.Anotar(string.Empty);
    }
}
