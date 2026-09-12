using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Cascara;

/// <summary>
/// La franja de avisos: una linea, la cuenta, «ver» y la X.
/// </summary>
/// <remarks>
/// Sustituye a los cuadros modales del programa viejo. No hay ni uno en toda la cascara:
/// el detalle se abre debajo y el programa sigue funcionando mientras esta abierto.
/// </remarks>
public sealed partial class FranjaDeAvisos : UserControl
{
    /// <summary>El buzón al que está atada la franja; nulo hasta <see cref="AtarA"/>, y entonces no se pinta nada.</summary>
    private BuzonDeAvisos? _buzon;

    /// <summary>Lo grave que es el aviso que se esta ensenando, para poder repintarlo.</summary>
    /// <remarks>
    /// Hace falta guardarlo porque el fondo se vuelve a pintar cuando el dueno cambia el
    /// tema con un aviso ya en pantalla, y en ese momento no se pasa por <c>Repintar</c>.
    /// </remarks>
    private GravedadDeAviso _gravedadEnPantalla = GravedadDeAviso.Advertencia;

    /// <summary>Monta el control; queda vacio hasta que se le ata un buzon.</summary>
    public FranjaDeAvisos()
    {
        InitializeComponent();

        // Si el dueno cambia el tema con un aviso puesto, el fondo se vuelve a resolver.
        // Sin esto se quedaria el del tema anterior y volveriamos al 1,70:1.
        ActualThemeChanged += (quien, cuando) => PintarElFondo(_gravedadEnPantalla);
    }

    /// <summary>Ata la franja al buzon de avisos y se pinta con lo que ya hubiera. Si ya estaba atada a otro, se suelta de él.</summary>
    /// <param name="buzon">El buzón de la app, el mismo que comparten todas las pantallas.</param>
    public void AtarA(BuzonDeAvisos buzon)
    {
        if (_buzon is not null) _buzon.Cambio -= AlCambiarElBuzon;
        _buzon = buzon;
        _buzon.Cambio += AlCambiarElBuzon;
        Repintar();
    }

    /// <summary>Se repinta cuando entra o se cierra un aviso.</summary>
    /// <param name="quien">El buzón; no se usa.</param>
    /// <param name="cuando">Vacío; no se usa.</param>
    private void AlCambiarElBuzon(object? quien, EventArgs cuando) => Repintar();

    /// <summary>Ensena el aviso mas nuevo y la cuenta de los que hay detras.</summary>
    private void Repintar()
    {
        var pendientes = _buzon?.Pendientes ?? Array.Empty<Aviso>();
        if (pendientes.Count == 0)
        {
            _franja.Visibility = Visibility.Collapsed;
            _detalle.Visibility = Visibility.Collapsed;
            return;
        }

        var aviso = pendientes[0];
        _franja.Visibility = Visibility.Visible;
        _cuenta.Text = pendientes.Count == 1 ? "1 aviso" : $"{pendientes.Count} avisos";
        _linea.Text = aviso.Linea;
        PintarElBotonDelAviso(_buzon?.AccionDe(aviso));
        _botonDeVer.Visibility = aviso.Detalle is null ? Visibility.Collapsed : Visibility.Visible;
        _botonDeCerrarTodo.Visibility = pendientes.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        PintarElFondo(aviso.Gravedad);

        // Al cambiar de aviso el detalle se cierra: si no, se enseñaria el del anterior.
        _detalle.Visibility = Visibility.Collapsed;
        _textoDelDetalle.Text = aviso.Detalle ?? string.Empty;
    }

    /// <summary>
    /// Pinta el fondo de la franja segun lo grave que sea el aviso y el tema que se este viendo.
    /// </summary>
    /// <remarks>
    /// ⛔ El diccionario se busca a mano por <see cref="FrameworkElement.ActualTheme"/> y NO
    /// se lee de <c>Application.Current.Resources</c>. El tema de la aplicacion se fija al
    /// arrancar y no cambia; el del elemento es el que el dueno eligio. Medido el 2026-09-05
    /// con la version anterior: fondo por un tema y letra por el otro daban 1,70:1 en claro.
    /// Hay una prueba que lo vigila: <c>PruebasDeLosDosTemas</c>.
    /// </remarks>
    /// <param name="gravedad">Lo grave que es el aviso; elige entre los tres fondos del diccionario.</param>
    private void PintarElFondo(GravedadDeAviso gravedad)
    {
        _gravedadEnPantalla = gravedad;

        var clave = gravedad switch
        {
            GravedadDeAviso.Problema => "FondoDeProblema",
            GravedadDeAviso.Advertencia => "FondoDeAdvertencia",
            _ => "FondoDeAviso",
        };

        var tema = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (Resources.ThemeDictionaries[tema] is ResourceDictionary diccionario
            && diccionario[clave] is Microsoft.UI.Xaml.Media.Brush fondo)
        {
            _barra.Background = fondo;
        }
    }

    /// <summary>Enseña el botón del aviso con su rótulo, o lo esconde si este aviso no trae ninguno.</summary>
    /// <remarks>
    /// El rótulo va también en <c>AutomationProperties.Name</c>: el contenido del botón se
    /// pone desde código y el barrido del XAML no lo ve, así que el nombre para el lector de
    /// pantalla se pone aquí, con el mismo texto que se lee.
    /// </remarks>
    /// <param name="accion">La acción del aviso que se está enseñando, o nula.</param>
    private void PintarElBotonDelAviso(AccionDelAviso? accion)
    {
        if (accion is null)
        {
            _botonDeAccion.Visibility = Visibility.Collapsed;
            _botonDeAccion.Content = string.Empty;
            return;
        }

        _botonDeAccion.Content = accion.Rotulo;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_botonDeAccion, accion.Rotulo);
        _botonDeAccion.Visibility = Visibility.Visible;
    }

    /// <summary>Hace lo que pidió el aviso que se está enseñando; si ya no trae acción, no hace nada.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarLaAccion(object quien, RoutedEventArgs cuando)
    {
        var pendientes = _buzon?.Pendientes;
        if (pendientes is null || pendientes.Count == 0) return;
        _buzon?.AccionDe(pendientes[0])?.Hacer();
    }

    /// <summary>Abre o cierra el detalle debajo de la linea, sin bloquear nada.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarVer(object quien, RoutedEventArgs cuando)
        => _detalle.Visibility = _detalle.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>Cierra el aviso que se esta ensenando y pasa al siguiente.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarCerrar(object quien, RoutedEventArgs cuando) => _buzon?.CerrarElPrimero();

    /// <summary>Cierra todos los avisos de golpe.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarCerrarTodo(object quien, RoutedEventArgs cuando) => _buzon?.CerrarTodos();
}
