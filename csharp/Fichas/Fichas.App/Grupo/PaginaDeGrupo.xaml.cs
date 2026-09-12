using System.Diagnostics;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.App.Inicio;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Fichas.App.Grupo;

/// <summary>
/// La pantalla del grupo que viaja un dia: quien viaja, con su documento, desde donde se
/// verifica y desde donde se asigna.
/// </summary>
/// <remarks>
/// <para>Palabras del dueno el 2026-09-05: <i>«yo debo poder darle clic y ver solamente al
/// grupo de personas que viajará en esa fecha, con su PDF. Y al darle clic me despliega la
/// opción de verificar la información, también permitirme asignarlos a los agentes»</i>.</para>
///
/// <para>⛔ <b>No duplica la maquina de Revisar</b> (criterio C13-2): asignar va por
/// <see cref="OperacionDeAsignar"/>, que es la unica puerta de asignar del programa
/// (C5-1), y verificar abre la pantalla de Correccion que ya existe. Aqui no hay una
/// segunda media operacion de nada.</para>
///
/// <para>Hereda de <see cref="Page"/> y no de <see cref="PaginaDeFichas"/> porque necesita
/// llevar QUE dia, y la navegacion de la cascara solo sabe pasar los servicios. El motivo
/// entero esta en <see cref="LlegadaAlGrupo"/>.</para>
/// </remarks>
public sealed partial class PaginaDeGrupo : Page
{
    /// <summary>Lo último que se pintó, en el orden del repetidor; por posición se sabe qué renglón se pulsó.</summary>
    private IReadOnlyList<RenglonDelGrupo> _renglonesDelGrupo = [];
    /// <summary>Los compañeros activos, en el mismo orden que el desplegable; el índice elegido apunta aquí.</summary>
    private IReadOnlyList<Companero> _elEquipo = [];

    /// <summary>Los servicios del programa; llegan con <see cref="LlegadaAlGrupo"/> y hasta entonces son nulos.</summary>
    private Servicios? _servicios;
    /// <summary>El día cuyo grupo se enseña; llega con <see cref="LlegadaAlGrupo"/>.</summary>
    private DateOnly _fecha;
    /// <summary>El grupo tal como se leyó la última vez; nulo hasta el primer pintado.</summary>
    private GrupoDelDia? _grupo;
    /// <summary>Lo que tardó la última lectura del grupo, para el registro y el informe de medición.</summary>
    private double _milisegundosDelGrupo;
    /// <summary>Cuántas veces se ha medido el pintado; se mide dos y a la segunda se deja de escuchar.</summary>
    private int _cuantasVecesSeMidio;
    /// <summary>El cronómetro del pintado; solo existe cuando la medición está pedida.</summary>
    private Stopwatch? _relojDelPintado;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeGrupo() => InitializeComponent();

    /// <summary>Recoge el dia y los servicios que trae la navegacion, y pinta.</summary>
    /// <param name="cuando">Trae en <c>Parameter</c> la <see cref="LlegadaAlGrupo"/>; con otra cosa no se pinta nada.</param>
    protected override void OnNavigatedTo(NavigationEventArgs cuando)
    {
        base.OnNavigatedTo(cuando);
        if (cuando.Parameter is not LlegadaAlGrupo llegada) return;

        _servicios = llegada.Servicios;
        _fecha = llegada.Fecha;

        // El dueno eligio el tema (2026-09-05) y esta pantalla le hace caso.
        PonerLaPaletaDelTema();
        ActualThemeChanged += AlCambiarElTema;

        LlenarElDesplegableDeCompaneros();

        if (MedicionDeInicio.EstaPedida)
        {
            _relojDelPintado = Stopwatch.StartNew();
            LayoutUpdated += AlTerminarDeColocar;
        }

        Pintar();
    }

    /// <summary>Lee el grupo del dia y lo reparte por la pantalla, midiendo lo que cuesta.</summary>
    private void Pintar()
    {
        if (_servicios is null) return;

        var lector = new LectorDeGrupos(
            _servicios.Casos, _servicios.Personas, _servicios.Asignaciones, _servicios.Companeros,
            _servicios.Procedencia);

        var cronometro = Stopwatch.StartNew();
        var grupo = lector.DelDia(_fecha);
        var renglones = grupo.EnUnaSolaLista();
        cronometro.Stop();
        _milisegundosDelGrupo = cronometro.Elapsed.TotalMilliseconds;

        _grupo = grupo;
        _renglonesDelGrupo = renglones;

        _titulo.Text = grupo.Titulo;
        _denominador.Text = grupo.LineaDelDenominador;
        // ⛔ DOS lineas y no una, con su palabra delante cada una, porque son dos
        // respuestas de dos sitios distintos y hasta el 2026-09-05 se leian como si fueran
        // la misma: las PERSONAS confirmadas salen de las seis preguntas del sistema del
        // obispo, y los DOCUMENTOS completos, de lo que escribio el Excel del companero.
        _comoVa.Text = grupo.ComoVanLasPersonas;
        _comoVanLosDocumentos.Text = $"documentos: {grupo.ComoVa}";
        _renglones.ItemsSource = renglones;
        _sinNadie.Visibility = PinturaDelGrupo.SeVe(grupo.EstaVacio);
        _asignarElGrupo.IsEnabled = grupo.LosQueSePuedenAsignar.Count > 0;

        _servicios.Registro.AnotarNavegacion($"Grupo del {grupo.FechaIso}", _milisegundosDelGrupo);
    }

    /// <summary>
    /// Pone en el desplegable a los companeros ACTIVOS, que son los que pueden recibir.
    /// </summary>
    /// <remarks>
    /// Un companero desactivado no sale: se desactivan y no se borran, y no reciben trabajo
    /// nuevo. Si no hay ninguno activo, el boton de asignar se apaga y se DICE por que: un
    /// boton que no hace nada y no explica por que es un boton roto.
    /// </remarks>
    private void LlenarElDesplegableDeCompaneros()
    {
        if (_servicios is null) return;

        _elEquipo = _servicios.Companeros.Activos();
        _companero.ItemsSource = _elEquipo.Select(c => c.Nombre).ToList();
        if (_elEquipo.Count > 0) _companero.SelectedIndex = 0;
        else _loQuePaso.Text = "No hay ningún compañero activo al que asignar.";
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

    /// <summary>Vuelve a la pantalla de Inicio.</summary>
    /// <param name="quien">El botón de volver.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPedirVolver(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || _servicios is null) return;
        Frame.Navigate(typeof(PaginaDeInicio), _servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>Asigna al companero elegido todos los documentos del dia.</summary>
    /// <param name="quien">El botón de asignar el grupo entero.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    /// <remarks>
    /// Es la decisión del dueño del 2026-09-07 (§9, «Asignar por grupo»): <i>«en asignar debe
    /// poder asignarlo por grupo. Cargar todos los documentos en un solo lugar no me conviene
    /// para nada»</i>. El lote sale de <see cref="GrupoDelDia.LosQueSePuedenAsignar"/>, que
    /// desde el 2026-09-06 no trae ningún archivado.
    /// </remarks>
    private void AlPedirAsignarElGrupo(object quien, RoutedEventArgs cuando)
    {
        if (_grupo is null) return;
        Asignar(_grupo.LosQueSePuedenAsignar, "el grupo entero");
    }

    /// <summary>Asigna al companero elegido los documentos de la unidad cuya cabecera se pulso.</summary>
    /// <param name="quien">El botón de la cabecera de la unidad que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    /// <remarks>
    /// La misma decisión del 2026-09-07 (§9), un escalón más abajo: el dueño habla con un
    /// líder por unidad, y asignar la unidad entera es el gesto que corresponde a esa llamada.
    /// </remarks>
    private void AlPedirAsignarLaUnidad(object quien, RoutedEventArgs cuando)
    {
        if (QueSePulso(quien) is not RenglonDelGrupo renglon || !renglon.EsCabecera) return;
        Asignar(renglon.CasosDeLaUnidad, renglon.Titulo);
    }

    /// <summary>
    /// Asigna un lote por la UNICA puerta de asignar del programa y dice en una linea que paso.
    /// </summary>
    /// <remarks>
    /// Cero cuadros modales (requisito 4 y 9): lo que salio bien se dice aqui mismo en una
    /// linea, y lo que no se pudo lo deja <see cref="OperacionDeAsignar"/> en la franja de
    /// la cascara con su motivo. Despues se vuelve a pintar, porque asignar cambia quien
    /// lleva cada documento y la lista lo ensena.
    /// </remarks>
    /// <param name="casoIds">Los documentos que se asignan.</param>
    /// <param name="deQue">De qué se habla en el acuse: «el grupo entero» o el título de la unidad.</param>
    private void Asignar(IReadOnlyCollection<long> casoIds, string deQue)
    {
        if (_servicios is null) return;

        if (_companero.SelectedIndex < 0 || _companero.SelectedIndex >= _elEquipo.Count)
        {
            _loQuePaso.Text = "Elija primero a qué compañero se le asigna.";
            return;
        }

        if (casoIds.Count == 0)
        {
            // Se DICE por qué, en vez de dejar un botón que no hace nada: un botón silencioso
            // es un botón roto. Antes del 2026-09-06 esto pasaba cuando lo que quedaba en el
            // grupo estaba archivado; ahora un archivado ni llega al grupo, así que si no hay
            // nada que asignar es que el día está vacío.
            _loQuePaso.Text = "No hay ningún documento que asignar en este grupo.";
            return;
        }

        var aQuien = _elEquipo[_companero.SelectedIndex];
        var operacion = new OperacionDeAsignar(_servicios.Asignaciones, _servicios.Reloj, _servicios.Avisos);
        var resumen = operacion.AsignarVarios(casoIds, aQuien.Id, aQuien.Nombre);

        _loQuePaso.Text = $"{deQue}: {resumen.Linea}";
        Pintar();
    }

    /// <summary>Pulsar una persona abre su documento en Correccion, que es donde se verifica.</summary>
    /// <param name="quien">El botón del renglón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarUnaPersona(object quien, RoutedEventArgs cuando)
    {
        if (QueSePulso(quien) is not RenglonDelGrupo renglon) return;
        if (renglon.EsCabecera || renglon.CasoId == 0) return;

        Verificar(renglon.CasoId);
    }

    /// <summary>
    /// Abre ese documento en la pantalla de Correccion, que es donde se verifica.
    /// </summary>
    /// <remarks>
    /// Es la segunda de las «dos pulsaciones hasta el PDF» del criterio C13-1: la primera
    /// es el dia del calendario, y esta es la persona. Correccion abre el documento con su
    /// PDF al lado.
    ///
    /// ⚠️ Depende de que <c>PaginaDeCorreccion.AbrirElCaso(long)</c> siga siendo publico;
    /// es de otro terreno y aqui solo se llama, nunca se toca. La peticion va por la cola
    /// del hilo de la interfaz por lo mismo que en Inicio: Correccion abre primero su
    /// propio caso y despues se le pide el que se pulso.
    ///
    /// ⚠️ <b>Y se le dice de DONDE viene</b>, desde el 2026-09-07. Palabras del dueno: <i>«No
    /// tengo opción de regresar a la ventana de atrás, que quiero seguir trabajando»</i>.
    /// Medido antes de tocar nada: entrando aqui, en toda la pantalla de Correccion no habia
    /// un solo boton de volver, y la unica salida era el menu de la izquierda, que lleva a
    /// otra pestana y pierde el grupo. Se le pasa el nombre del sitio y no solo un aviso de
    /// que hay vuelta: «‹ Volver» a secas no dice a donde se va.
    /// </remarks>
    /// <param name="casoId">El documento que se abre en Corrección.</param>
    private void Verificar(long casoId)
    {
        if (Frame is null || _servicios is null) return;

        var marco = Frame;
        var deDondeSeViene = _grupo?.Titulo ?? FechasEnEspanol.DecirElDiaCompleto(_fecha);
        marco.Navigate(typeof(PaginaDeCorreccion), _servicios, new SuppressNavigationTransitionInfo());
        DispatcherQueue.TryEnqueue(() =>
        {
            if (marco.Content is not PaginaDeCorreccion correccion) return;
            correccion.AbrirElCaso(casoId);
            correccion.MostrarLaVueltaA($"al grupo del {deDondeSeViene}");
        });
    }

    /// <summary>
    /// Sube por el arbol desde el boton que se pulso hasta dar con el renglon, y devuelve
    /// el dato que le toca por su POSICION en la lista.
    /// </summary>
    /// <remarks>
    /// No se pregunta por el <c>DataContext</c>: un renglon realizado por un
    /// <c>ItemsRepeater</c> con una plantilla de <c>x:Bind</c> lo tiene vacio, porque las
    /// ataduras compiladas se actualizan por otro camino. Medido el 2026-09-04 en Inicio.
    /// </remarks>
    /// <param name="donde">El control que se pulsó, en cualquier profundidad dentro de un renglón.</param>
    /// <returns>El renglón pulsado, o nulo si el control no cuelga del repetidor.</returns>
    private RenglonDelGrupo? QueSePulso(object? donde)
    {
        var actual = donde as DependencyObject;
        while (actual is not null)
        {
            var padre = VisualTreeHelper.GetParent(actual);

            if (actual is UIElement enPantalla && ReferenceEquals(padre, _renglones))
            {
                var posicion = _renglones.GetElementIndex(enPantalla);
                if (posicion >= 0 && posicion < _renglonesDelGrupo.Count) return _renglonesDelGrupo[posicion];
            }

            actual = padre;
        }

        return null;
    }

    /// <summary>
    /// Escribe el informe del criterio C13-5 cuando la pantalla queda colocada.
    /// </summary>
    /// <remarks>
    /// Mide DOS veces por lo mismo que Inicio: la primera trae el coste de una sola vez
    /// —inflar las plantillas—; la segunda, lo que cuesta cada vez. Se mide al quedar
    /// COLOCADA y no al asignar la lista: cronometrar lo segundo daria una cifra bonita
    /// y falsa.
    /// </remarks>
    /// <param name="quien">La página que terminó de colocarse.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
                Pintar();
            });
            return;
        }

        LayoutUpdated -= AlTerminarDeColocar;
        EscribirElInforme("repintado con los mismos datos", milisegundos);
    }

    /// <summary>Vuelca al informe las cifras del C13-5: cuanto tarda y cuantos elementos quedan vivos.</summary>
    /// <param name="deQuePasada">Si es la primera vez o el repintado, dicho en palabras para el informe.</param>
    /// <param name="milisegundosHastaColocar">Lo que tardó el pintado hasta quedar colocado.</param>
    private void EscribirElInforme(string deQuePasada, double milisegundosHastaColocar)
    {
        var pantalla = MedicionDeInicio.ContarElementosVivos(_desplazamientoDeLaPantalla);
        var lista = MedicionDeInicio.ContarElementosVivos(_lista);

        MedicionDeInicio.Anotar($"===== Grupo del {_grupo?.FechaIso} con {_grupo?.CuantosDocumentos} documentos "
            + $"y {_grupo?.CuantasPersonas} personas, {ActualWidth:F0}x{ActualHeight:F0} px — {deQuePasada} =====");
        MedicionDeInicio.Anotar($"C13-5  grupo leido en {MedicionDeInicio.Cifra(_milisegundosDelGrupo)} ms");
        MedicionDeInicio.Anotar($"C13-5  pintado hasta quedar colocado: {MedicionDeInicio.Cifra(milisegundosHastaColocar)} ms "
            + $"({MedicionDeInicio.Cifra(milisegundosHastaColocar / 1000.0, 3)} s)");
        MedicionDeInicio.Anotar($"C13-5  elementos vivos en toda la pantalla: {pantalla.Todos}");

        // ⛔ Las DOS lineas de la cabecera van al informe tal como se leen en la pantalla.
        // Sin esto, «se ve que las dos preguntas no se confunden» seria una opinion sobre una
        // captura; con esto es una linea de texto que cualquiera puede volver a sacar.
        MedicionDeInicio.Anotar($"C18-2  «{_comoVa.Text}»");
        MedicionDeInicio.Anotar($"C18-5  «{_comoVanLosDocumentos.Text}»");
        foreach (var renglon in _renglonesDelGrupo.Where(r => r.EsUnaPersona).Take(12))
        {
            MedicionDeInicio.Anotar($"C18-3  «{renglon.Titulo}» · «{renglon.Detalle}»");
        }
        MedicionDeInicio.Anotar($"C13-5  «quién viaja»: {lista.Todos} elementos, "
            + $"{MedicionDeInicio.RenglonesRealizados(_renglones)} renglones realizados de {_renglonesDelGrupo.Count} datos");
        MedicionDeInicio.Anotar($"C13-5  la lista mide {MedicionDeInicio.Cifra(_lista.ExtentHeight)} px por dentro "
            + $"y ensena {MedicionDeInicio.Cifra(_lista.ViewportHeight)} px: "
            + $"se pueden bajar {MedicionDeInicio.Cifra(_lista.ScrollableHeight)} px");
        MedicionDeInicio.Anotar($"C13-5  {MedicionDeInicio.DescribirLasBarras(_desplazamientoDeLaPantalla, this)}");
        MedicionDeInicio.Anotar(string.Empty);
    }
}
