using System.Diagnostics;
using System.Globalization;
using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Inicio;

/// <summary>
/// La pantalla de Inicio: lo que esta listo para asignar, lo que esta asignado a los
/// agentes, y el calendario por donde se entra al grupo que viaja cada dia.
/// </summary>
/// <remarks>
/// ⛔ Terreno del programador de Inicio (fases C1, C7 y C12). Nadie mas escribe aqui.
///
/// Esta clase solo REPARTE lo que calcula <see cref="LectorDelInicio"/>. Ni una regla de
/// negocio vive aqui: si la hubiera, habria que abrir una ventana para probarla, y las
/// pruebas de <c>PruebasDeInicio</c> dejarian de poder existir (ADR-0003 §8.1).
/// </remarks>
public sealed partial class PaginaDeInicio : PaginaDeFichas
{
    private IReadOnlyList<RenglonDeCaso> _loListo = [];
    private IReadOnlyList<RenglonDeCaso> _loAsignado = [];
    private IReadOnlyList<DiaDelCalendario> _losDias = [];
    private IReadOnlyList<PersonaConTicket> _losTickets = [];

    private LectorDelInicio? _lector;
    private int _mesQueSeEnsena;
    private int _cuantasVecesSeMidio;
    private double _milisegundosDelResumen;
    private Stopwatch? _relojDelPintado;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeInicio() => InitializeComponent();

    /// <summary>Cuanto costo la ultima lectura completa del panel, en milisegundos.</summary>
    public double MilisegundosDelResumen => _milisegundosDelResumen;

    /// <summary>Monta el lector con los cinco puertos que necesita y pinta el mes de hoy.</summary>
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

        if (MedicionDeInicio.EstaPedida)
        {
            _relojDelPintado = Stopwatch.StartNew();
            LayoutUpdated += AlTerminarDeColocar;
            Escuchar(_desplazamientoDeLaPantalla, "la pantalla");
            Escuchar(_listos, "listo para asignar");
            Escuchar(_asignados, "asignado a los agentes");
            Escuchar(_tickets, "listo para viajar");
            Escuchar(_equipo, "cuánto lleva cada compañero");
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

        EscribirLaCabecera(resumen);
        LlenarLoListo(resumen);
        LlenarLoAsignado(resumen);
        LlenarLoDelSistemaDelObispo(resumen);
        LlenarElCalendario(resumen);

        Servicios.Registro.AnotarNavegacion("Inicio calcula su resumen", _milisegundosDelResumen);

        // Requisito 4 y 9: lo que no cuadra se dice en UNA linea en la franja de la
        // cascara y no detiene nada. Aqui no se abre ni un cuadro.
        Servicios.Avisos.Dejar(resumen.Avisos);
    }

    /// <summary>La fecha de hoy en espanol y la linea del denominador que exige el C1-1.</summary>
    private void EscribirLaCabecera(ResumenDeInicio resumen)
    {
        var hoy = FechasEnEspanol.Leer(_lector!.Hoy);
        _hoy.Text = hoy is DateOnly dia ? FechasEnEspanol.DecirElDiaCompleto(dia) : _lector.Hoy;
        _denominador.Text = resumen.LineaDelDenominador;
    }

    /// <summary>La primera de las dos cosas que el dueno pidio ver en Inicio.</summary>
    private void LlenarLoListo(ResumenDeInicio resumen)
    {
        _loListo = resumen.Listos;
        _listosRenglones.ItemsSource = resumen.Listos;
        _cuantosListos.Text = EnCifras(resumen.Contadores.ListoParaAsignar);
        _deQueVanLosListos.Text = resumen.DeQueVaLoListo;
    }

    /// <summary>La segunda: lo que ahora mismo llevan los companeros, y cuanto lleva cada uno.</summary>
    private void LlenarLoAsignado(ResumenDeInicio resumen)
    {
        _loAsignado = resumen.Asignados;
        _asignadosRenglones.ItemsSource = resumen.Asignados;
        _cuantosAsignados.Text = EnCifras(resumen.Contadores.AsignadoALosAgentes);
        _deQueVanLosAsignados.Text = resumen.DeQueVaLoAsignado;
        _equipoRenglones.ItemsSource = resumen.Equipo;
    }

    /// <summary>
    /// La tercera cosa: lo que hay que verificar en el sistema del obispo, en PERSONAS.
    /// </summary>
    /// <remarks>
    /// ⛔ Las dos cifras de arriba no se tocan (C20-2). Esto es una tercera y habla de otro
    /// sistema: <i>«Ahí yo puedo verificarlos y ver en el sistema de la Iglesia»</i>.
    /// <para>
    /// La lista junta lo vencido y lo que apremia, con lo vencido delante: son dos cuentas
    /// distintas —cada una con su linea— pero una sola lista de personas, porque lo que el
    /// hace con las dos es lo mismo, llamar al obispo, y dos listas obligarian a mirar dos
    /// veces.
    /// </para>
    /// </remarks>
    private void LlenarLoDelSistemaDelObispo(ResumenDeInicio resumen)
    {
        var loSuyo = resumen.ElSistemaDelObispo;

        _elGrupoQueViene.Text = loSuyo.FraseDelProximoGrupo;
        _loVencido.Text = loSuyo.FraseDeLoVencido;
        _loQueApremia.Text = loSuyo.FraseDeLoQueApremia;

        _losTickets = [.. loSuyo.Vencidas, .. loSuyo.QueApremian];
        _ticketsRenglones.ItemsSource = _losTickets;
    }

    /// <summary>El calendario del mes que se ensena, con su titulo.</summary>
    private void LlenarElCalendario(ResumenDeInicio resumen)
    {
        _losDias = resumen.Mes.Dias;
        _tituloDelMes.Text = resumen.Mes.Titulo;
        _mes.ItemsSource = resumen.Mes.Dias;
    }

    /// <summary>Escribe un numero sin que el idioma de la maquina le cambie el separador.</summary>
    private static string EnCifras(int numero) => numero.ToString(CultureInfo.InvariantCulture);

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

    /// <summary>Abre la ventana de lo que no esta completo, agrupado por fecha de viaje.</summary>
    private void AlPedirLoIncompleto(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        Frame.Navigate(typeof(PaginaDeIncompletos), Servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>Pulsar un dia del calendario abre el grupo que viaja ese dia.</summary>
    private void AlPulsarUnDia(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_losDias, _mes, quien) is DiaDelCalendario dia) AbrirElGrupoDelDia(dia);
    }

    /// <summary>Pulsar un documento listo lo abre en Correccion.</summary>
    private void AlPulsarUnDocumentoListo(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_loListo, _listosRenglones, quien) is RenglonDeCaso renglon)
            AbrirElCasoEnCorreccion(renglon.CasoId);
    }

    /// <summary>Pulsar un documento asignado lo abre en Correccion.</summary>
    private void AlPulsarUnDocumentoAsignado(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_loAsignado, _asignadosRenglones, quien) is RenglonDeCaso renglon)
            AbrirElCasoEnCorreccion(renglon.CasoId);
    }

    /// <summary>Pulsar una persona con la recomendacion sin confirmar abre su documento.</summary>
    /// <remarks>
    /// Abre el DOCUMENTO y no a la persona porque la ventana de la persona todavia no
    /// existe: es la FASE C19. Cuando exista, esta linea la abre a ella.
    /// </remarks>
    private void AlPulsarUnaPersonaConTicket(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null) return;
        if (EnLaLista(_losTickets, _ticketsRenglones, quien) is PersonaConTicket ticket)
            AbrirElCasoEnCorreccion(ticket.CasoId);
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
    /// <c>Cascara/VentanaPrincipal.xaml.cs</c>, que esta congelado y es de otro terreno.
    /// Por eso la pantalla del grupo trae su propio boton de volver.</para>
    /// </remarks>
    private void AbrirElGrupoDelDia(DiaDelCalendario dia)
    {
        if (!dia.SePuedePulsar) return;
        Frame.Navigate(typeof(PaginaDeGrupo), new LlegadaAlGrupo(Servicios!, dia.Fecha), new SuppressNavigationTransitionInfo());
    }

    /// <summary>
    /// Pulsar un documento lo abre en Correccion, por el marco de la cascara.
    /// </summary>
    /// <remarks>
    /// ⚠️ Depende de que <c>PaginaDeCorreccion.AbrirElCaso(long)</c> siga siendo publico;
    /// es de otro terreno y aqui solo se llama, nunca se toca.
    ///
    /// ⚠️ La peticion va POR LA COLA del hilo de la interfaz. Medido el 2026-09-04 en
    /// fichas.log: al navegar, Correccion abre PRIMERO su propio caso y despues se le pide
    /// el que se pulso. Se encola para que siga saliendo en ese orden aunque manana el
    /// desplegable de Correccion tarde un turno mas en decidirse.
    /// </remarks>
    private void AbrirElCasoEnCorreccion(long casoId)
    {
        var marco = Frame;
        marco.Navigate(typeof(PaginaDeCorreccion), Servicios, new SuppressNavigationTransitionInfo());
        DispatcherQueue.TryEnqueue(() =>
        {
            if (marco.Content is PaginaDeCorreccion correccion) correccion.AbrirElCaso(casoId);
        });
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
        AnotarUnaLista("listo para asignar", _listos, _listosRenglones);
        AnotarUnaLista("asignado a los agentes", _asignados, _asignadosRenglones);
        AnotarUnaLista("listo para viajar", _tickets, _ticketsRenglones);
        AnotarUnaLista("cuánto lleva cada compañero", _equipo, _equipoRenglones);
        MedicionDeInicio.Anotar($"C20-1  «{_elGrupoQueViene.Text}»");
        MedicionDeInicio.Anotar($"C21-3  «{_loVencido.Text}»");
        MedicionDeInicio.Anotar($"C21-4  «{_loQueApremia.Text}»");
        MedicionDeInicio.Anotar($"C12-2  calendario del mes: {calendario.Todos} elementos con 42 celdas fijas, "
            + $"{MedicionDeInicio.RenglonesRealizados(_mes)} realizadas; la mas llena pide "
            + $"{MedicionDeInicio.Cifra(MedicionDeInicio.AltoQuePideElContenido(_mes))} px de alto");
        MedicionDeInicio.Anotar($"C1-3   la pantalla mide {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ExtentHeight)} px de alto "
            + $"y se ven {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ViewportHeight)} px: "
            + $"se pueden bajar {MedicionDeInicio.Cifra(_desplazamientoDeLaPantalla.ScrollableHeight)} px");
        MedicionDeInicio.Anotar($"C1-2   {MedicionDeInicio.DescribirLasBarras(_desplazamientoDeLaPantalla, this)}");
        MedicionDeInicio.Anotar(string.Empty);
    }

    /// <summary>
    /// Anota de una lista lo que decide el C1-5 y el C1-2: cuantos renglones construyo de
    /// verdad frente a cuantos datos tiene, y cuanto puede bajar por dentro.
    /// </summary>
    private static void AnotarUnaLista(string comoSeLlama, ScrollView desplazamiento, ItemsRepeater repetidor)
    {
        var arbol = MedicionDeInicio.ContarElementosVivos(desplazamiento);
        MedicionDeInicio.Anotar(
            $"C1-5   «{comoSeLlama}»: {arbol.Todos} elementos, {MedicionDeInicio.RenglonesRealizados(repetidor)} "
            + $"renglones realizados de {ContarLos(repetidor)} datos");
        MedicionDeInicio.Anotar(
            $"C1-2   «{comoSeLlama}» mide {MedicionDeInicio.Cifra(desplazamiento.ExtentHeight)} px por dentro "
            + $"y ensena {MedicionDeInicio.Cifra(desplazamiento.ViewportHeight)} px: "
            + $"se pueden bajar {MedicionDeInicio.Cifra(desplazamiento.ScrollableHeight)} px");
    }

    /// <summary>Cuantos datos tiene detras una lista, que no es lo mismo que cuantos renglones construyo.</summary>
    private static int ContarLos(ItemsRepeater repetidor)
        => repetidor.ItemsSource is System.Collections.ICollection datos ? datos.Count : 0;
}
