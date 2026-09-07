using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Cascara;

/// <summary>
/// La parte de la ventana que se ocupa del tema: leerlo al abrir, pintarlo y guardarlo.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-05: poder elegir el tema en vez de seguir siempre al de
/// Windows, y que el programa lo recuerde entre sesiones.</para>
///
/// <para>Va aparte de <c>VentanaPrincipal.xaml.cs</c> —misma clase, otro archivo— para que la
/// cascara siga siendo legible de una lectura: alli esta la navegacion, aqui el tema.</para>
/// </remarks>
public sealed partial class VentanaPrincipal
{
    private PreferenciaDeTema? _preferenciaDeTema;

    /// <summary>
    /// Mientras se monta, marcar el circulo NO cuenta como que el dueno eligio.
    /// </summary>
    /// <remarks>
    /// ⚠️ Sin esto, poner <c>IsChecked</c> al abrir dispara <c>Checked</c> y el programa
    /// escribiria el archivo en cada arranque sin que nadie haya tocado nada.
    /// </remarks>
    private bool _montandoElTema;

    /// <summary>Lee el tema guardado, lo pinta y deja marcado el circulo que toca.</summary>
    private void MontarElTema()
    {
        _preferenciaDeTema = new PreferenciaDeTema(_servicios.Argumentos.CarpetaDeDatos);
        var tema = _preferenciaDeTema.Leer();

        _montandoElTema = true;
        _temaDeWindows.IsChecked = tema == TemaDeLaVentana.ElDeWindows;
        _temaClaro.IsChecked = tema == TemaDeLaVentana.Claro;
        _temaOscuro.IsChecked = tema == TemaDeLaVentana.Oscuro;
        _montandoElTema = false;

        PintarConElTema(tema);
    }

    /// <summary>El dueno eligio: se pinta, se guarda y, si no se pudo guardar, se dice.</summary>
    private void AlElegirElTema(object quien, RoutedEventArgs cuando)
    {
        if (_montandoElTema || quien is not RadioButton circulo) return;

        var tema = TemaDeEsteCirculo(circulo);
        PintarConElTema(tema);

        // Que no se pueda escribir el archivo NO impide cambiar el tema en esta sesion: se
        // cambia igual y lo unico que se pierde es que lo recuerde la proxima vez. Callarlo
        // seria peor, porque el dueno lo volveria a elegir cada vez sin saber por que.
        if (_preferenciaDeTema?.Guardar(tema) == false)
        {
            _servicios.Avisos.Dejar(Aviso.Advierte(
                "El tema cambió, pero no se pudo guardar para la próxima vez.",
                string.Empty,
                $"No se pudo escribir «{_preferenciaDeTema.Ruta}». El programa volverá a abrir "
                + "con el tema anterior. Compruebe que esa carpeta existe y se puede escribir."));
        }
    }

    /// <summary>Pinta la ventana entera con el tema y lo dice para el lector de pantalla.</summary>
    /// <remarks>
    /// Se pone en <c>_raiz</c>, la raiz del contenido de la ventana. Es lo que hace que lo
    /// tomen tambien los desplegables y los cuadros, que no cuelgan de este arbol pero si del
    /// mismo <c>XamlRoot</c>.
    /// </remarks>
    private void PintarConElTema(TemaDeLaVentana tema)
    {
        _raiz.RequestedTheme = ComoLoPintaXaml(tema);
        AutomationProperties.SetName(
            _botonDelTema, $"Tema de la ventana: {TemasDeLaVentana.ComoSeLee(tema)}");
    }

    /// <summary>Traduce la eleccion del dueno al valor que entiende XAML.</summary>
    /// <remarks>
    /// <c>Default</c> es «el de Windows»: es el unico de los tres que sigue al sistema, y por
    /// eso el programa se comportaba asi antes de que esto existiera.
    /// </remarks>
    private static ElementTheme ComoLoPintaXaml(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => ElementTheme.Light,
        TemaDeLaVentana.Oscuro => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>Que tema es el circulo que se acaba de marcar, segun su etiqueta.</summary>
    private static TemaDeLaVentana TemaDeEsteCirculo(RadioButton circulo)
        => Enum.TryParse<TemaDeLaVentana>(circulo.Tag as string, out var tema)
            ? tema
            : TemaDeLaVentana.ElDeWindows;
}
