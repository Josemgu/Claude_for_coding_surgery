using Fichas.App.Cascara;
using Fichas.Reportes.Reglas;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Fichas.App.Asignar;

/// <summary>
/// La pantalla de asignar: TODOS los casos a cualquier companero activo.
/// </summary>
/// <remarks>
/// ⛔ Terreno del programador de Asignar y Revisar (fase C5). Nadie mas escribe aqui.
///
/// Aqui no hay ni una regla: la pantalla lee de <see cref="ListaParaAsignar"/> y escribe por
/// <see cref="OperacionDeAsignar"/>, que son las dos clases que se prueban sin ventana. Lo
/// que queda en este archivo es colocar controles y pasar mensajes.
/// </remarks>
public sealed partial class PaginaDeAsignar : PaginaDeFichas
{
    /// <summary>Quien lee lo que se ofrece; nulo hasta que llegan los servicios en <see cref="AlLlegar"/>.</summary>
    private ListaParaAsignar? _lectura;
    /// <summary>La única puerta de asignar y retirar; nula hasta <see cref="AlLlegar"/>.</summary>
    private OperacionDeAsignar? _asignar;
    /// <summary>El panel flotante de altas y bajas del equipo; nulo hasta <see cref="AlLlegar"/>.</summary>
    private PanelDelEquipo? _equipo;

    /// <summary>
    /// Los casos marcados, APARTE de la lista: sobreviven a buscar, a borrar la búsqueda y a repintar.
    /// </summary>
    /// <remarks>
    /// ⛔ Hasta el 2026-09-16 las marcas vivían solo en <c>_lista.SelectedItems</c>, y cada
    /// tecleo en el buscador ponía una fuente nueva que nace sin marcas. Del dueño: <i>«elimino
    /// el nombre de búsqueda para buscar a otra persona, y el sistema desmarca a las personas
    /// que yo ya había marcado»</i>. La regla está probada sin ventana en
    /// <see cref="MarcasDeAsignar"/>; aquí solo se enchufa a la lista en los dos sentidos.
    /// </remarks>
    private readonly MarcasDeAsignar _marcas = new();

    /// <summary>
    /// Verdadero mientras la página está poniendo o quitando marcas en la lista por su
    /// cuenta, para que <see cref="AlCambiarLaSeleccion"/> no las vuelva a leer a medias.
    /// </summary>
    private bool _sincronizandoLasMarcas;

    /// <summary>Los renglones que la lista tiene pintados ahora mismo, para saber qué id hay en cada posición.</summary>
    private IReadOnlyList<RenglonParaAsignar> _renglonesALaVista = [];

    /// <summary>Los renglones del panel de grupos, en el mismo orden en que se pintaron.</summary>
    /// <remarks>
    /// Se guardan porque un <c>ItemsRepeater</c> no dice qué dato lleva el botón que se
    /// pulsó: dice en qué POSICIÓN está, y el dato se busca aquí. Es el mismo camino que ya
    /// usa <c>PaginaDeGrupo.QueSePulso</c>.
    /// </remarks>
    private IReadOnlyList<RenglonDeGrupoParaAsignar> _renglonesDeGrupos = [];

    /// <summary>El grupo que espera confirmación; nulo mientras no se haya pulsado ninguno.</summary>
    /// <remarks>
    /// Se guarda entre la pregunta y la respuesta porque son dos pulsaciones distintas: entre
    /// una y otra el dueño puede cambiar de compañero, y entonces la pregunta que hay pintada
    /// ya no dice la verdad. Por eso <see cref="EsconderLaFranjaDelGrupo"/> se llama también
    /// al cambiar de destino, igual que en Paquetes.
    /// </remarks>
    private RenglonDeGrupoParaAsignar? _grupoQueEspera;

    /// <summary>Monta la pantalla y ata Ctrl+A a marcar todo lo que se ve.</summary>
    public PaginaDeAsignar()
    {
        InitializeComponent();
        _lista.SelectionChanged += AlCambiarLaSeleccion;
        KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.A,
            Modifiers = VirtualKeyModifiers.Control,
        });
        KeyboardAccelerators[0].Invoked += AlPulsarMarcarTodo;
    }

    /// <summary>Lee la base entera y pinta la lista con su denominador.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _lectura = new ListaParaAsignar(
            Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Servicios.Personas);
        _asignar = new OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);

        _equipo = new PanelDelEquipo(
            Servicios.Companeros,
            Servicios.Mantenimiento,
            new Revisar.OperacionDeBorrar(Servicios.Mantenimiento, Servicios.Avisos, Servicios.Registro),
            Servicios.Reloj,
            Servicios.Avisos,
            Acusar);

        // Un alta o una baja cambian a quien se le puede dar trabajo: el desplegable y el
        // denominador tienen que enterarse en el momento, no en la proxima visita.
        _equipo.Cambio += (_, _) => RepintarConLosDestinos();

        _destinos.ItemsSource = _lectura.Destinos();
        if (_destinos.Items.Count > 0) _destinos.SelectedIndex = 0;

        Repintar();
    }

    /// <summary>Vuelve a leer la base y a pintar la lista con lo que haya en el buscador.</summary>
    private void Repintar()
    {
        if (_lectura is null) return;

        // Se desmarca ANTES de cambiar la lista, por lo mismo que en la pantalla de
        // Revisar: un ItemsView con elementos marcados al que se le cambia la fuente se
        // queda apuntando a lo que ya no esta, y el proceso muere sin dejar rastro.
        // Y solo si YA hay lista: desmarcar una que todavia no tiene fuente lo mata igual.
        // Las marcas NO se pierden con esto: viven en _marcas y se vuelven a poner abajo.
        var texto = _buscador.Text ?? string.Empty;
        // Se piden todos porque ItemsView virtualiza: construye los renglones que se ven,
        // no los 3 000. El coste de la lista esta medido en PruebasDeRapidez.
        var pagina = _lectura.Ofrecer(Pagina.Primera(int.MaxValue), texto);

        _sincronizandoLasMarcas = true;
        try
        {
            if (_lista.ItemsSource is not null) _lista.DeselectAll();

            _renglonesALaVista = pagina.Elementos;
            _lista.ItemsSource = pagina.Elementos;
            VolverAPonerLasMarcasEnLaLista();
            PintarLosGrupos(pagina.Elementos);
        }
        finally
        {
            _sincronizandoLasMarcas = false;
        }

        // ⚠️ 2026-09-06. Aqui se decia tambien «N archivados fuera de la lista», y el motivo
        // escrito era bueno: sin esa cifra, una lista que encoge no se distingue de una que
        // perdio casos. El dueno lo deshizo con un motivo que gana —«debe pasar a archivado y
        // no aparecer mas en ningun lado», porque verlo nombrado le confunde—. La cifra que
        // contesta «¿me falta algo?» vive ahora en Revisar, con «Ver los archivados».
        var sinArchivar = _lectura.CuantosSePuedenOfrecer();
        var desactivados = _lectura.CuantosDestinosDesactivados();
        _denominador.Text =
            Plural.Con(sinArchivar, "caso", "casos")
            + $" · {pagina.TotalDisponible} " + Plural.Palabra(pagina.TotalDisponible, "ofrecido", "ofrecidos")
            + " · " + Plural.Con(_lectura.Destinos().Count, "compañero activo", "compañeros activos")
            + " (" + Plural.Con(desactivados, "desactivado no recibe casos", "desactivados no reciben casos") + ")";

        ContarLoMarcado();
    }

    /// <summary>Dice en una linea cuantos hay marcados, cuantos fuera de la vista, y a quien irian.</summary>
    private void ContarLoMarcado()
    {
        var cuantos = _marcas.Cuantas;
        var destino = _destinos.SelectedItem as Companero;
        _botonDeAsignar.IsEnabled = cuantos > 0 && destino is not null;
        _botonDeRetirar.IsEnabled = cuantos > 0;
        _botonDeQuitarLasMarcas.Visibility = cuantos > 0 ? Visibility.Visible : Visibility.Collapsed;
        _estadoDeLaSeleccion.Text = _marcas.Dicho(destino?.Nombre);
    }

    /// <summary>
    /// Pone en la lista recien pintada las marcas que le tocan; lo que no se ve se queda marcado igual.
    /// </summary>
    /// <remarks>
    /// Se marca por POSICION, que es lo que <c>ItemsView.Select</c> admite: la posicion de
    /// cada renglon marcado se busca en la lista que se acaba de poner. Solo se llama con
    /// <see cref="_sincronizandoLasMarcas"/> puesto, para que la seleccion que esto provoca
    /// no se vuelva a leer como si la hubiera hecho el dueno.
    /// </remarks>
    private void VolverAPonerLasMarcasEnLaLista()
    {
        var idsPintados = _renglonesALaVista.Select(r => r.CasoId).ToList();
        var aMarcar = _marcas.AlRepintar(idsPintados).ToHashSet();
        if (aMarcar.Count == 0) return;

        for (var posicion = 0; posicion < idsPintados.Count; posicion++)
        {
            if (aMarcar.Contains(idsPintados[posicion])) _lista.Select(posicion);
        }
    }

    /// <summary>
    /// Lee lo que el dueno acaba de marcar o desmarcar en la lista y lo pasa al conjunto.
    /// </summary>
    /// <remarks>
    /// El <c>ItemsView</c> no dice QUE cambio, solo que cambio: se compara lo que la lista
    /// tiene marcado con los renglones a la vista, y solo esos se tocan en el conjunto. Los
    /// marcados que no estan a la vista no se rozan: no hay forma de que el dueno los haya
    /// desmarcado desde una lista en la que no salen.
    /// </remarks>
    private void LeerLasMarcasDeLaLista()
    {
        var marcadosEnLaLista = _lista.SelectedItems.OfType<RenglonParaAsignar>().Select(r => r.CasoId).ToHashSet();
        foreach (var renglon in _renglonesALaVista)
        {
            if (marcadosEnLaLista.Contains(renglon.CasoId)) _marcas.Marcar(renglon.CasoId);
            else _marcas.Desmarcar(renglon.CasoId);
        }
    }

    /// <summary>Asigna lo marcado al companero elegido, por la unica puerta que hay.</summary>
    /// <param name="quien">El botón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarAsignar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || _destinos.SelectedItem is not Companero destino) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        // El conjunto ENTERO, esten o no a la vista: es lo que la linea de arriba dice que
        // se va a dar («7 marcados, 3 fuera de la vista»), y dar menos seria mentirle.
        var resumen = _asignar.AsignarVarios(casos, destino.Id, destino.Nombre);
        Acusar(resumen.Linea);
        _marcas.QuitarTodas();
        Repintar();
    }

    /// <summary>Quita TODAS las marcas, tambien las que la busqueda dejo fuera de la vista.</summary>
    /// <param name="quien">El botón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarQuitarLasMarcas(object quien, RoutedEventArgs cuando)
    {
        var cuantas = _marcas.Cuantas;
        _marcas.QuitarTodas();
        DesmarcarLaListaSinLeerla();
        ContarLoMarcado();
        Acusar(Plural.Con(cuantas, "marca quitada", "marcas quitadas") + "; no se asignó nada.");
    }

    /// <summary>Desmarca la lista sin que eso se vuelva a leer como un gesto del dueno.</summary>
    private void DesmarcarLaListaSinLeerla()
    {
        _sincronizandoLasMarcas = true;
        try
        {
            if (_lista.ItemsSource is not null) _lista.DeselectAll();
        }
        finally
        {
            _sincronizandoLasMarcas = false;
        }
    }

    /// <summary>Retira lo marcado: desactiva las asignaciones vivas, nunca las borra.</summary>
    /// <param name="quien">El botón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarRetirar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        var retirados = casos.Count(caso => _asignar.RetirarDelCaso(caso).SeEscribio);
        Acusar(
            Plural.Con(retirados, "caso retirado", "casos retirados")
            + "; la fila de quien " + Plural.Palabra(retirados, "lo llevaba", "los llevaba") + " se conserva.");
        _marcas.QuitarTodas();
        Repintar();
    }

    // ---- repartir por grupo -------------------------------------------------

    /// <summary>
    /// Pinta el panel de la izquierda: los dias de viaje y, debajo de cada uno, sus unidades.
    /// </summary>
    /// <remarks>
    /// <para>Se agrupa lo que YA se leyo, no se vuelve a preguntar a la base: los renglones
    /// estan en la mano y agrupar es repartirlos. Por eso esto no cuesta ni una consulta mas.</para>
    ///
    /// <para>El titulo lleva las dos cifras porque son las que contestan «¿esta todo?»: si la
    /// suma de los grupos no fuera el total ofrecido, habria documentos que no estan en ningun
    /// grupo y nadie lo sabria.</para>
    /// </remarks>
    /// <param name="ofrecidos">Los renglones que acaba de servir la lista, todos.</param>
    private void PintarLosGrupos(IReadOnlyList<RenglonParaAsignar> ofrecidos)
    {
        var grupos = GruposParaAsignar.Armar(ofrecidos);
        _renglonesDeGrupos = GruposParaAsignar.EnUnaSolaLista(grupos);
        _grupos.ItemsSource = _renglonesDeGrupos;

        var enGrupos = grupos.Sum(grupo => grupo.CuantosDocumentos);
        _tituloDeLosGrupos.Text = grupos.Count == 0
            ? "Los grupos"
            : "Los grupos · " + Plural.Con(grupos.Count, "fecha de viaje", "fechas de viaje")
              + " · " + Plural.Con(enGrupos, "documento", "documentos");
        _sinGrupos.Visibility = grupos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // La pregunta que hubiera pintada hablaba de la lista de ANTES; dejarla seria
        // ofrecer confirmar un reparto que ya no es el que se ve.
        EsconderLaFranjaDelGrupo();
    }

    /// <summary>Ensena cuantos se van a asignar y a quien; todavia no toca la base.</summary>
    /// <remarks>
    /// Es la misma forma con la que se pregunta antes de quitarle todos los casos a alguien
    /// (<c>PaginaDePaquetes.AlPulsarQuitarleTodo</c>): mirar no escribe, y el que no asigna va
    /// primero. Un grupo son quince documentos de golpe, y en este programa todo lo que toca
    /// muchos a la vez dice cuantos antes de hacerlo.
    /// </remarks>
    /// <param name="quien">El botón del renglón del panel; por él se sabe qué grupo es.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPedirAsignarUnGrupo(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || QueGrupoSePulso(quien) is not RenglonDeGrupoParaAsignar renglon) return;

        if (_destinos.SelectedItem is not Companero destino)
        {
            EsconderLaFranjaDelGrupo();
            Acusar("Elige primero a qué compañero se le da el grupo.");
            return;
        }

        var loQueSeVa = _asignar.MirarLoQueSeVaAAsignar(renglon.Casos, destino.Nombre, renglon.Nombre);
        if (!loQueSeVa.HayAlgoQueAsignar)
        {
            EsconderLaFranjaDelGrupo();
            Acusar(loQueSeVa.Pregunta);
            return;
        }

        _grupoQueEspera = renglon;
        _lineaDeLoQueSeAsigna.Text = loQueSeVa.Pregunta;
        _zonaDeConfirmarElGrupo.Visibility = Visibility.Visible;
    }

    /// <summary>Se echa atras: se cierra la franja y en la base no se ha escrito nada.</summary>
    /// <param name="quien">El botón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarNoAsignarElGrupo(object quien, RoutedEventArgs cuando)
    {
        EsconderLaFranjaDelGrupo();
        Acusar("No se asignó ningún documento.");
    }

    /// <summary>Ahora si: se asigna el grupo entero por la unica puerta y se dice cuantos fueron.</summary>
    /// <param name="quien">El botón que se pulsó.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarConfirmarElGrupo(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || _grupoQueEspera is not RenglonDeGrupoParaAsignar renglon) return;
        if (_destinos.SelectedItem is not Companero destino) return;

        var resumen = _asignar.AsignarVarios(renglon.Casos, destino.Id, destino.Nombre);
        Acusar($"{renglon.Nombre}: {resumen.Linea}");
        EsconderLaFranjaDelGrupo();
        Repintar();
    }

    /// <summary>Cierra la franja del grupo; se llama tambien al cambiar de destino y al repintar.</summary>
    /// <remarks>
    /// Cerrarla al cambiar de companero no es cosmetica, y el motivo esta medido en Paquetes:
    /// una pregunta que dice «se le van a asignar 15 a Sandy» sobre un desplegable que ya pone
    /// otro nombre es la forma exacta de darle el grupo a quien no era.
    /// </remarks>
    private void EsconderLaFranjaDelGrupo()
    {
        _grupoQueEspera = null;
        _lineaDeLoQueSeAsigna.Text = string.Empty;
        _zonaDeConfirmarElGrupo.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Que renglon del panel de grupos lleva el boton que se acaba de pulsar.
    /// </summary>
    /// <remarks>
    /// Un <c>ItemsRepeater</c> no dice que dato lleva lo que se pulso: dice en que POSICION
    /// esta el elemento, y el dato se busca en la lista con la que se pinto. Es el mismo
    /// camino que <c>PaginaDeGrupo.QueSePulso</c>, y sube por el arbol visual porque entre el
    /// boton y el repetidor hay una rejilla de por medio.
    /// </remarks>
    /// <param name="donde">El control que se pulsó, a cualquier profundidad dentro del renglón.</param>
    /// <returns>El renglón del panel, o nulo si el control no cuelga del repetidor de grupos.</returns>
    private RenglonDeGrupoParaAsignar? QueGrupoSePulso(object? donde)
    {
        var actual = donde as DependencyObject;
        while (actual is not null)
        {
            var padre = VisualTreeHelper.GetParent(actual);
            if (actual is UIElement enPantalla && ReferenceEquals(padre, _grupos))
            {
                var posicion = _grupos.GetElementIndex(enPantalla);
                if (posicion >= 0 && posicion < _renglonesDeGrupos.Count) return _renglonesDeGrupos[posicion];
            }

            actual = padre;
        }

        return null;
    }

    /// <summary>Abre el panel del equipo anclado a su boton.</summary>
    /// <param name="quien">El botón «Equipo», al que se ancla el panel.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarEquipo(object quien, RoutedEventArgs cuando)
    {
        if (_equipo is null || quien is not FrameworkElement boton) return;
        _equipo.Abrir(boton);
    }

    /// <summary>
    /// Relee la lista Y el desplegable de destinos, que es lo que cambia un alta o una baja.
    /// </summary>
    /// <remarks>
    /// Se conserva a quien estuviera elegido: cambiar el equipo no puede mover el destino
    /// que el dueno ya habia puesto, salvo que sea justo a quien acaba de desactivar.
    /// </remarks>
    private void RepintarConLosDestinos()
    {
        if (_lectura is null) return;

        var elegido = (_destinos.SelectedItem as Companero)?.Id;
        var destinos = _lectura.Destinos();
        _destinos.ItemsSource = destinos;

        var sigue = destinos.ToList().FindIndex(c => c.Id == elegido);
        _destinos.SelectedIndex = sigue >= 0 ? sigue : (destinos.Count > 0 ? 0 : -1);

        Repintar();
    }

    /// <summary>Los numeros internos de los casos marcados ahora mismo, esten o no a la vista.</summary>
    private List<long> CasosMarcados() => _marcas.Ids.ToList();

    /// <summary>Ctrl+A: marca todo lo que hay a la vista; si ya estaba todo, lo desmarca. Lo de fuera no se toca.</summary>
    /// <param name="quien">El atajo que se pulsó.</param>
    /// <param name="cuando">Los datos del atajo; se marca como atendido para que no siga subiendo.</param>
    private void AlPulsarMarcarTodo(KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        cuando.Handled = true;
        if (_lista.ItemsSource is null) return;

        var marco = _marcas.MarcarODesmarcarLaVista();
        _sincronizandoLasMarcas = true;
        try
        {
            if (marco) _lista.SelectAll();
            else _lista.DeselectAll();
        }
        finally
        {
            _sincronizandoLasMarcas = false;
        }

        ContarLoMarcado();
    }

    /// <summary>Al cambiar lo marcado se pasa al conjunto y se recuenta: la lista no se reconstruye.</summary>
    /// <param name="quien">La lista.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarLaSeleccion(ItemsView quien, ItemsViewSelectionChangedEventArgs cuando)
    {
        // Mientras la pagina esta poniendo o quitando marcas por su cuenta, este evento
        // salta a medias y leerlo desmarcaria en el conjunto lo que todavia no se ha puesto.
        if (_sincronizandoLasMarcas) return;
        LeerLasMarcasDeLaLista();
        ContarLoMarcado();
    }

    /// <summary>
    /// Al cambiar de destino tampoco se relee la base: el destino no filtra casos.
    /// </summary>
    /// <remarks>
    /// Lo unico que si se cierra es la pregunta del grupo: decia un nombre y el desplegable ya
    /// pone otro. Ver <see cref="EsconderLaFranjaDelGrupo"/>.
    /// </remarks>
    /// <param name="quien">El desplegable de destinos.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarDeDestino(object quien, SelectionChangedEventArgs cuando)
    {
        EsconderLaFranjaDelGrupo();
        ContarLoMarcado();
    }

    /// <summary>Al escribir en el buscador se relee la lista con el texto puesto.</summary>
    /// <param name="quien">El buscador.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlEscribirEnElBuscador(object quien, TextChangedEventArgs cuando) => Repintar();

    /// <summary>Dice una linea en el acuse del pie; NO abre ningun cuadro (requisito 9).</summary>
    /// <param name="linea">La frase, ya en español y de una sola línea.</param>
    private static void Acusar(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }
}
