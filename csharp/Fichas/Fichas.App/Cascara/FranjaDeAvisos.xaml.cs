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

    /// <summary>Ata la franja al buzon de avisos y se pinta con lo que ya hubiera.</summary>
    public void AtarA(BuzonDeAvisos buzon)
    {
        if (_buzon is not null) _buzon.Cambio -= AlCambiarElBuzon;
        _buzon = buzon;
        _buzon.Cambio += AlCambiarElBuzon;
        Repintar();
    }

    /// <summary>Cuantos avisos hay sin cerrar; lo lee la prueba sin abrir ventana.</summary>
    public int CuantosPendientes => _buzon?.Pendientes.Count ?? 0;

    /// <summary>Se repinta cuando entra o se cierra un aviso.</summary>
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

    /// <summary>Abre o cierra el detalle debajo de la linea, sin bloquear nada.</summary>
    private void AlPulsarVer(object quien, RoutedEventArgs cuando)
        => _detalle.Visibility = _detalle.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>Cierra el aviso que se esta ensenando y pasa al siguiente.</summary>
    private void AlPulsarCerrar(object quien, RoutedEventArgs cuando) => _buzon?.CerrarElPrimero();

    /// <summary>Cierra todos los avisos de golpe.</summary>
    private void AlPulsarCerrarTodo(object quien, RoutedEventArgs cuando) => _buzon?.CerrarTodos();
}
