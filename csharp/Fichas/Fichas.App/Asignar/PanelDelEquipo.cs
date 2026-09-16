using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Fichas.App.Asignar;

/// <summary>
/// El equipo: quien esta, quien recibe trabajo, quien entra y quien se va.
/// </summary>
/// <remarks>
/// <para>
/// Del dueno, 2026-09-05: «tampoco tengo la opcion de eliminar o quitar agentes del
/// sistema, eso es super importante», y antes «yo debo tener el control de quien se anade
/// y quien no». Hasta hoy los companeros solo se podian dar de alta desde el programa
/// viejo. Nadie se crea solo: ni al importar, ni al leer una hoja devuelta.
/// </para>
/// <para>
/// Vive en <b>Asignar</b> porque es la unica pantalla donde los companeros se ven —el
/// desplegable «Dar a»— y la navegacion de la cascara esta congelada: no hay ni puede
/// haber una entrada «Equipo» sin descongelarla.
/// </para>
/// <para>
/// ⛔ Es un <see cref="Flyout"/> y NO un cuadro: se cierra pulsando fuera y no detiene el
/// trabajo (requisito 9). Lo unico que si detiene es la pregunta de borrar, y esa la
/// levanta <see cref="OperacionDeBorrar"/>, que es el unico sitio del programa autorizado
/// a preguntar.
/// </para>
/// <para>
/// <b>Las acciones no son intercambiables.</b> EDITAR cambia el nombre (y el puesto, en sus
/// desplegables) sin tocar nada mas. DESACTIVAR es lo normal: deja de
/// recibir trabajo nuevo y su nombre sigue en todo lo que ya firmo, y por eso no pregunta
/// —se deshace con REACTIVAR—. ELIMINAR borra la fila de verdad y solo se ofrece a quien no
/// lleva nada; a quien lleva algo se le dice cuanto lleva y por que no se puede.
/// </para>
/// </remarks>
public sealed partial class PanelDelEquipo
{
    /// <summary>El equipo: se lee entero, desactivados incluidos, y por aquí se desactiva.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>
    /// Reactivar, la carga de cada uno y el borrado de verdad; nulo con datos inventados
    /// (<c>--falso N</c>), y entonces esas tres cosas se dicen y no se hacen.
    /// </summary>
    private readonly IMantenimiento? _mantenimiento;
    /// <summary>El único sitio del programa autorizado a preguntar antes de borrar; aquí se le pide quitar a alguien.</summary>
    private readonly OperacionDeBorrar _borrar;
    /// <summary>Con qué instante se fecha una baja.</summary>
    private readonly IReloj _reloj;
    /// <summary>La franja de la cáscara, donde van los avisos de cada escritura.</summary>
    private readonly BuzonDeAvisos _avisos;
    /// <summary>Cómo se dice una línea en el acuse del pie; la pone la página que abre el panel.</summary>
    private readonly Action<string> _acusar;
    /// <summary>La regla del alta y del puesto, sin ventana; este panel solo la llama.</summary>
    private readonly PuestosDelEquipo _puestos;
    /// <summary>La lista de renglones, uno por compañero; se vacía y se rehace en cada <see cref="Repintar"/>.</summary>
    private readonly StackPanel _cuerpo = new() { Spacing = 6, MinWidth = 380 };
    /// <summary>La caja del nombre del alta; se vacía al dar de alta.</summary>
    private readonly TextBox _nombreNuevo = new() { PlaceholderText = "Nombre del compañero nuevo" };

    /// <summary>Que entra siendo el que se da de alta; el defecto es «Compañero».</summary>
    private readonly ComboBox _rolNuevo = new() { MinWidth = 150 };

    /// <summary>En que peldano entra; el defecto es el primero.</summary>
    private readonly NumberBox _categoriaNueva = new()
    {
        Minimum = PuestosDelEquipo.CategoriaMinima,
        Value = PuestosDelEquipo.CategoriaMinima,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        MinWidth = 120,
    };

    /// <summary>
    /// El panel flotante, construido UNA vez en el primer <see cref="Abrir"/> y reutilizado
    /// después; se cierra antes de preguntar por un borrado.
    /// </summary>
    /// <remarks>
    /// ⛔ Hasta el 2026-09-16 se hacía <c>new Flyout { Content = Construir() }</c> en CADA
    /// clic, y <see cref="Construir"/> volvía a colgar de un <c>Grid</c> nuevo los mismos
    /// controles de arriba —<see cref="_nombreNuevo"/>, <see cref="_rolNuevo"/>,
    /// <see cref="_categoriaNueva"/>, <see cref="_cuerpo"/>— que ya eran hijos del panel
    /// anterior. WinUI no admite un elemento con dos padres, y el segundo clic mataba el
    /// proceso: <i>«el segundo clic friza todo el programa; y lo cierra también»</i> (dueño).
    /// </remarks>
    private Flyout? _panel;

    /// <summary>La regla de abrir y cerrar con el mismo botón; probada sin ventana.</summary>
    private readonly InterruptorDelPanel _interruptor = new();

    /// <summary>Ata el panel a los puertos que necesita y a como se dice una linea.</summary>
    /// <param name="companeros">El equipo.</param>
    /// <param name="mantenimiento">Reactivar, carga y borrado; nulo con datos inventados.</param>
    /// <param name="borrar">Quien pregunta y borra de verdad.</param>
    /// <param name="reloj">El reloj del programa.</param>
    /// <param name="avisos">El buzón de la franja.</param>
    /// <param name="acusar">Cómo decir una línea en el acuse del pie.</param>
    public PanelDelEquipo(
        ICompaneros companeros,
        IMantenimiento? mantenimiento,
        OperacionDeBorrar borrar,
        IReloj reloj,
        BuzonDeAvisos avisos,
        Action<string> acusar)
    {
        _companeros = companeros;
        _mantenimiento = mantenimiento;
        _borrar = borrar;
        _reloj = reloj;
        _avisos = avisos;
        _acusar = acusar;
        _puestos = new PuestosDelEquipo(companeros, avisos);
    }

    /// <summary>Salta cuando algo del equipo cambio, para que la pantalla se repinte.</summary>
    public event EventHandler? Cambio;

    /// <summary>
    /// Un clic en el botón: abre el panel anclado a él, ya pintado con el equipo de ahora; y
    /// si ya estaba abierto, lo cierra.
    /// </summary>
    /// <remarks>
    /// El armazón se construye la primera vez y se reutiliza: ver <see cref="_panel"/>. El
    /// «cerrar» del interruptor vale también para el clic que WinUI deja pasar tras cerrar
    /// el panel por pulsar fuera: <c>Closed</c> ya puso el interruptor en cerrado, así que ese
    /// clic abre, que es lo que se espera de un clic sobre un panel cerrado.
    /// </remarks>
    /// <param name="anclaje">El botón bajo el que se abre.</param>
    public void Abrir(FrameworkElement anclaje)
    {
        ArgumentNullException.ThrowIfNull(anclaje);

        if (_interruptor.AlPulsar() == GestoDelPanel.Cerrar)
        {
            _panel?.Hide();
            return;
        }

        _panel ??= ArmarElPanel();
        Repintar();
        _panel.ShowAt(anclaje);
    }

    /// <summary>El panel flotante, una sola vez, con su contenido y con el aviso de cierre atado.</summary>
    private Flyout ArmarElPanel()
    {
        var panel = new Flyout { Content = Construir(), Placement = FlyoutPlacementMode.Bottom };
        // Se cierra por muchos caminos —Escape, clic fuera, la pregunta de borrar—, y por
        // todos el interruptor tiene que enterarse, o el siguiente clic «cerraria» un
        // panel que ya no esta y el dueno veria un boton que no hace nada.
        panel.Closed += (_, _) => _interruptor.AlCerrarse();
        return panel;
    }

    /// <summary>El armazon: el alta arriba, la lista debajo, y nada mas.</summary>
    private FrameworkElement Construir()
    {
        var alta = new Grid { ColumnSpacing = 6 };
        alta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        alta.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // El texto de sugerencia NO es el nombre accesible: medido el 2026-09-05, la caja
        // salia sin nombre en el arbol de accesibilidad y no habia forma de encontrarla ni
        // con lector de pantalla ni al comprobar.
        AutomationProperties.SetName(_nombreNuevo, "Nombre del compañero nuevo");

        var botonDeAnadir = new Button { Content = "Añadir" };
        botonDeAnadir.Click += (_, _) => Anadir();
        Grid.SetColumn(botonDeAnadir, 1);
        alta.Children.Add(_nombreNuevo);
        alta.Children.Add(botonDeAnadir);

        // El puesto se elige AL DAR DE ALTA y no despues. Es lo que faltaba: sin esto el
        // dueno leia «dese de alta usted como administrador» y no tenia donde decirlo.
        _rolNuevo.ItemsSource = PuestosDelEquipo.LosTresRoles.Select(PuestosDelEquipo.DecirElRol).ToList();
        _rolNuevo.SelectedIndex = 0;
        AutomationProperties.SetName(_rolNuevo, "Rol del compañero nuevo");
        AutomationProperties.SetName(_categoriaNueva, "Categoría del compañero nuevo");
        ToolTipService.SetToolTip(
            _categoriaNueva,
            "El peldaño de la escalera. Los de categoría 1 lo intentan primero; lo que no "
            + "consiguen pasa a los de la 2, y así. No hay tope arriba.");

        var elPuesto = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        elPuesto.Children.Add(_rolNuevo);
        elPuesto.Children.Add(_categoriaNueva);

        var todo = new StackPanel { Spacing = 10, MaxWidth = 460 };
        todo.Children.Add(new TextBlock { Text = "Equipo", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        todo.Children.Add(alta);
        todo.Children.Add(elPuesto);
        todo.Children.Add(new ScrollViewer { Content = _cuerpo, MaxHeight = 360 });
        return todo;
    }

    /// <summary>Vuelve a leer el equipo entero y a pintar un renglon por companero.</summary>
    private void Repintar()
    {
        _cuerpo.Children.Clear();

        var todos = _companeros.Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, 500));
        if (todos.Elementos.Count == 0)
        {
            _cuerpo.Children.Add(new TextBlock
            {
                Text = "Todavía no hay nadie en el equipo. Escribe un nombre arriba y pulsa Añadir.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.85,
            });
            return;
        }

        foreach (var companero in todos.Elementos)
        {
            _cuerpo.Children.Add(RenglonDe(companero));
        }
    }

    /// <summary>Un companero: su nombre, lo que lleva, y lo que se puede hacer con el.</summary>
    /// <param name="companero">El compañero del renglón.</param>
    private FrameworkElement RenglonDe(Companero companero)
    {
        var carga = _mantenimiento?.Carga(companero.Id);

        var titulo = new TextBlock
        {
            Text = companero.Nombre + (companero.Activo ? string.Empty : "  (desactivado)"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // «No lleva nada a su nombre» y no «Lleva nada a su nombre», que es lo que salia
        // al pegar el verbo delante de la frase de `Dicho`. Medido con la ventana abierta
        // el 2026-09-05: la frase se leia mal justo en el caso mas comun.
        // El motivo de no poder eliminar va AQUI, a la vista, y no solo en el globo del
        // boton apagado: un globo sobre un boton gris es lo ultimo que alguien mira.
        var detalle = new TextBlock
        {
            Text = carga is null
                ? "—"
                : carga.NoLlevaNada
                    ? "No lleva nada a su nombre: se puede eliminar."
                    : "Lleva " + carga.Dicho + ". No se puede eliminar; desactívalo para que no reciba más.",
            FontSize = 12,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        };

        // Las tres acciones que pidio el dueno el 2026-09-16, siempre a la vista y en este
        // orden: editar, desactivar o reactivar, eliminar.
        var acciones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        acciones.Children.Add(BotonDeEditar(companero));
        acciones.Children.Add(BotonDeBajaOAlta(companero));
        acciones.Children.Add(BotonDeEliminar(companero, carga));

        var renglon = new StackPanel { Spacing = 2 };
        renglon.Children.Add(titulo);
        if (_editando == companero.Id) renglon.Children.Add(FormularioDelNombre(companero));
        renglon.Children.Add(detalle);
        renglon.Children.Add(PuestoDe(companero));
        renglon.Children.Add(acciones);

        return new Border
        {
            Child = renglon,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.35 },
        };
    }

    /// <summary>
    /// El puesto de un companero que ya esta: su rol y su peldano, cambiables en el sitio.
    /// </summary>
    /// <remarks>
    /// <para>Los dos manejadores se atan <b>despues</b> de poner el valor de partida. Atados
    /// antes, construir el renglon disparia una escritura por companero cada vez que el panel
    /// se repinta, y el equipo entero se reescribiria solo al abrirlo.</para>
    ///
    /// <para>Los nombres de accesibilidad llevan DENTRO a quien afectan, por lo mismo que los
    /// botones: con cinco companeros hay cinco desplegables que dicen «Rol» y sin esto no hay
    /// forma de saber cual es cual, ni con lector de pantalla ni al comprobar.</para>
    /// </remarks>
    /// <param name="companero">El compañero cuyo puesto se enseña y se puede cambiar.</param>
    private FrameworkElement PuestoDe(Companero companero)
    {
        var rol = new ComboBox
        {
            ItemsSource = PuestosDelEquipo.LosTresRoles.Select(PuestosDelEquipo.DecirElRol).ToList(),
            SelectedIndex = PuestosDelEquipo.LosTresRoles.ToList().IndexOf(companero.Rol),
            MinWidth = 150,
            FontSize = 12,
        };
        AutomationProperties.SetName(rol, "Rol de " + companero.Nombre);

        var categoria = new NumberBox
        {
            Minimum = PuestosDelEquipo.CategoriaMinima,
            Value = companero.Categoria,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MinWidth = 120,
        };
        AutomationProperties.SetName(categoria, "Categoría de " + companero.Nombre);

        rol.SelectionChanged += (_, _) =>
            CambiarElPuesto(companero, PuestosDelEquipo.LosTresRoles[rol.SelectedIndex], LeerPeldano(categoria));

        categoria.ValueChanged += (_, _) =>
            CambiarElPuesto(companero, PuestosDelEquipo.LosTresRoles[rol.SelectedIndex], LeerPeldano(categoria));

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        fila.Children.Add(rol);
        fila.Children.Add(categoria);
        return fila;
    }

    /// <summary>Escribe el puesto por la unica puerta que hay y dice en una linea que paso.</summary>
    /// <param name="companero">El compañero tal como estaba; si no cambia nada, no se escribe.</param>
    /// <param name="rol">El rol elegido.</param>
    /// <param name="categoria">El peldaño elegido.</param>
    private void CambiarElPuesto(Companero companero, RolDeCompanero rol, int categoria)
    {
        if (rol == companero.Rol && categoria == companero.Categoria) return;

        if (!_puestos.Cambiar(companero.Id, rol, categoria).SeEscribio) return;

        _acusar($"{companero.Nombre} pasa a {PuestosDelEquipo.DecirElRol(rol)}, categoría {categoria}. "
                + "Lo que ya firmó y lo que lleva asignado no se toca.");
        DecirLoQuePasaConElAtajo();
        Repintar();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Un peldano leido de la caja; el vacio vale como el primero y no como cero.
    /// </summary>
    /// <remarks>
    /// <see cref="NumberBox.Value"/> vale <c>NaN</c> mientras la caja esta vacia, y convertir
    /// eso a entero da cero, que es un peldano que no existe. Se lee como el primero: lo que
    /// el dueno esta haciendo ahi es borrar para teclear otro numero, no pedir el cero.
    /// </remarks>
    /// <param name="caja">La caja numérica del peldaño.</param>
    private static int LeerPeldano(NumberBox caja)
        => double.IsNaN(caja.Value)
            ? PuestosDelEquipo.CategoriaMinima
            : (int)Math.Max(PuestosDelEquipo.CategoriaMinima, Math.Round(caja.Value));

    /// <summary>Deja en la franja lo que le pasa al atajo del administrador, si es que le pasa algo.</summary>
    private void DecirLoQuePasaConElAtajo()
    {
        if (_puestos.LoQueLePasaAlAtajo() is Aviso aviso) _avisos.Dejar([aviso]);
    }

    /// <summary>Desactivar o reactivar; ninguno de los dos pregunta, porque se deshacen.</summary>
    /// <param name="companero">A quién afecta el botón.</param>
    private Button BotonDeBajaOAlta(Companero companero)
    {
        var boton = new Button
        {
            Content = companero.Activo ? "Desactivar" : "Reactivar",
            FontSize = 12,
        };

        // El nombre para la via de accesibilidad lleva DENTRO a quien afecta: con cinco
        // companeros hay cinco botones que dicen «Desactivar» y sin esto no hay forma de
        // saber cual es cual, ni para quien usa lector de pantalla ni para quien comprueba.
        AutomationProperties.SetName(
            boton, (companero.Activo ? "Desactivar a " : "Reactivar a ") + companero.Nombre);

        boton.Click += (_, _) =>
        {
            var resultado = companero.Activo
                ? _companeros.Desactivar(companero.Id, _reloj.Ahora())
                : Reactivar(companero.Id);

            _avisos.Dejar(resultado.Avisos);
            if (resultado.SeEscribio)
            {
                _acusar(companero.Activo
                    ? $"{companero.Nombre} ya no recibe casos nuevos; su nombre sigue en todo lo que ya hizo."
                    : $"{companero.Nombre} vuelve a recibir casos.");
                Repintar();
                Cambio?.Invoke(this, EventArgs.Empty);
            }
        };

        return boton;
    }

    /// <summary>Eliminar de verdad; apagado —y con el motivo puesto— si lleva algo a su nombre.</summary>
    /// <remarks>
    /// Se llamaba «Quitar» hasta el 2026-09-16; el dueño lo pidió con su palabra —<i>«eliminar,
    /// editar o desactivar»</i>— y esa es la que se lee. Lo que hace no cambia: pregunta,
    /// copia y borra por <see cref="IMantenimiento.PlanearCompanero"/>.
    /// </remarks>
    /// <param name="companero">A quién afecta el botón.</param>
    /// <param name="carga">Lo que lleva a su nombre; nula con datos inventados, y entonces no se puede eliminar.</param>
    private Button BotonDeEliminar(Companero companero, CargaDeUnCompanero? carga)
    {
        var puede = carga is not null && carga.NoLlevaNada && _borrar.SePuedeBorrarAqui;

        var boton = new Button
        {
            Content = "Eliminar",
            FontSize = 12,
            IsEnabled = puede,
        };

        AutomationProperties.SetName(boton, "Eliminar a " + companero.Nombre + " del equipo");

        ToolTipService.SetToolTip(
            boton,
            puede
                ? $"Borra a {companero.Nombre} de la base. Pregunta antes y hace una copia."
                : carga is null
                    ? "Con datos inventados no se puede borrar nada."
                    : $"No se puede eliminar: lleva {carga.Dicho}. Desactívalo en vez de eliminarlo, "
                      + "para no perder el rastro de quién hizo qué.");

        boton.Click += async (_, _) =>
        {
            // El panel se cierra ANTES de preguntar: un cuadro levantado sobre un flyout
            // abierto deja los dos peleandose por la raiz visual.
            var raiz = boton.XamlRoot;
            _panel?.Hide();

            var linea = await _borrar.PreguntarYBorrar(
                mantenimiento => mantenimiento.PlanearCompanero(companero.Id), raiz);

            if (linea is not null) _acusar(linea);
            Cambio?.Invoke(this, EventArgs.Empty);
        };

        return boton;
    }

    /// <summary>
    /// Da de alta a mano, que es la unica forma en que alguien entra al equipo, con su puesto.
    /// </summary>
    /// <remarks>
    /// El rol sale del desplegable y no se deduce de nada: «yo debo tener el control de quien
    /// se anade y quien no», y eso incluye <b>como qué</b> entra. Nadie se asciende solo.
    /// </remarks>
    private void Anadir()
    {
        var nombre = (_nombreNuevo.Text ?? string.Empty).Trim();
        var rol = PuestosDelEquipo.LosTresRoles[Math.Max(0, _rolNuevo.SelectedIndex)];
        var categoria = LeerPeldano(_categoriaNueva);

        if (!_puestos.DarDeAlta(nombre, rol, categoria).SeEscribio) return;

        _nombreNuevo.Text = string.Empty;
        _rolNuevo.SelectedIndex = 0;
        _categoriaNueva.Value = PuestosDelEquipo.CategoriaMinima;
        _acusar($"{nombre} entra al equipo como {PuestosDelEquipo.DecirElRol(rol)}, "
                + $"categoría {categoria}, y ya puede recibir casos.");
        DecirLoQuePasaConElAtajo();
        Repintar();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reactivar va por el puerto de mantenimiento; sin el, se dice y no se hace.</summary>
    /// <param name="companeroId">A quién se reactiva.</param>
    private ResultadoDeEscritura Reactivar(long companeroId)
        => _mantenimiento is null
            ? ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Aquí no se puede reactivar: el programa abrió con datos inventados.",
                string.Empty,
                "Cierra el programa y ábrelo normal."))
            : _mantenimiento.Reactivar(companeroId);
}
