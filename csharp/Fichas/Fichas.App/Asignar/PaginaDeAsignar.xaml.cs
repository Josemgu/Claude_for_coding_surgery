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
    private ListaParaAsignar? _lectura;
    private OperacionDeAsignar? _asignar;
    private PanelDelEquipo? _equipo;

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
        if (_lista.ItemsSource is not null) _lista.DeselectAll();

        var texto = _buscador.Text ?? string.Empty;
        // Se piden todos porque ItemsView virtualiza: construye los renglones que se ven,
        // no los 3 000. El coste de la lista esta medido en PruebasDeRapidez.
        var pagina = _lectura.Ofrecer(Pagina.Primera(int.MaxValue), texto);
        _lista.ItemsSource = pagina.Elementos;
        PintarLosGrupos(pagina.Elementos);

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

    /// <summary>Dice en una linea cuantos hay marcados y a quien irian.</summary>
    private void ContarLoMarcado()
    {
        var cuantos = _lista.SelectedItems.Count;
        var destino = _destinos.SelectedItem as Companero;
        _botonDeAsignar.IsEnabled = cuantos > 0 && destino is not null;
        _botonDeRetirar.IsEnabled = cuantos > 0;
        _estadoDeLaSeleccion.Text = cuantos == 0
            ? "Marca los casos que quieras dar. Ctrl+A marca todos los que se ven."
            : Plural.Con(cuantos, "marcado", "marcados")
              + " · " + Plural.Palabra(cuantos, "iría", "irían")
              + $" a {destino?.Nombre ?? "nadie: elige un compañero"}";
    }

    /// <summary>Asigna lo marcado al companero elegido, por la unica puerta que hay.</summary>
    private void AlPulsarAsignar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || _destinos.SelectedItem is not Companero destino) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        var resumen = _asignar.AsignarVarios(casos, destino.Id, destino.Nombre);
        Acusar(resumen.Linea);
        _lista.DeselectAll();
        Repintar();
    }

    /// <summary>Retira lo marcado: desactiva las asignaciones vivas, nunca las borra.</summary>
    private void AlPulsarRetirar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        var retirados = casos.Count(caso => _asignar.RetirarDelCaso(caso).SeEscribio);
        Acusar(
            Plural.Con(retirados, "caso retirado", "casos retirados")
            + "; la fila de quien " + Plural.Palabra(retirados, "lo llevaba", "los llevaba") + " se conserva.");
        _lista.DeselectAll();
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
    private void AlPulsarNoAsignarElGrupo(object quien, RoutedEventArgs cuando)
    {
        EsconderLaFranjaDelGrupo();
        Acusar("No se asignó ningún documento.");
    }

    /// <summary>Ahora si: se asigna el grupo entero por la unica puerta y se dice cuantos fueron.</summary>
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

    /// <summary>Los numeros internos de los casos marcados ahora mismo.</summary>
    private List<long> CasosMarcados()
        => _lista.SelectedItems.OfType<RenglonParaAsignar>().Select(r => r.CasoId).ToList();

    /// <summary>Ctrl+A: marca todo lo que hay a la vista; si ya estaba todo, lo desmarca.</summary>
    private void AlPulsarMarcarTodo(KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        cuando.Handled = true;
        if (_lista.ItemsSource is not IReadOnlyList<RenglonParaAsignar> renglones) return;
        if (MarcarTodo.HayQueMarcar(_lista.SelectedItems.Count, renglones.Count)) _lista.SelectAll();
        else _lista.DeselectAll();
    }

    /// <summary>Al cambiar lo marcado, solo se recuenta: la lista no se reconstruye.</summary>
    private void AlCambiarLaSeleccion(ItemsView quien, ItemsViewSelectionChangedEventArgs cuando)
        => ContarLoMarcado();

    /// <summary>
    /// Al cambiar de destino tampoco se relee la base: el destino no filtra casos.
    /// </summary>
    /// <remarks>
    /// Lo unico que si se cierra es la pregunta del grupo: decia un nombre y el desplegable ya
    /// pone otro. Ver <see cref="EsconderLaFranjaDelGrupo"/>.
    /// </remarks>
    private void AlCambiarDeDestino(object quien, SelectionChangedEventArgs cuando)
    {
        EsconderLaFranjaDelGrupo();
        ContarLoMarcado();
    }

    /// <summary>Al escribir en el buscador se relee la lista con el texto puesto.</summary>
    private void AlEscribirEnElBuscador(object quien, TextChangedEventArgs cuando) => Repintar();

    /// <summary>Dice una linea en el acuse del pie; NO abre ningun cuadro (requisito 9).</summary>
    private static void Acusar(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }
}
