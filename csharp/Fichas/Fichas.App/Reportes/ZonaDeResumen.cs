using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Reportes;

/// <summary>
/// El bloque de «una linea, ver y X» que usan las tres acciones de estas dos pantallas.
/// </summary>
/// <remarks>
/// <para>Es lo que sustituye al cuadro modal (requisito 4 del dueno): la linea se queda en la
/// pantalla, el detalle se abre DEBAJO al pulsar «ver», y la X lo cierra. Nada que haya que
/// aceptar para poder seguir.</para>
///
/// <para>Aqui no hay ninguna regla que probar: son cinco controles y tres visibilidades. Lo
/// que SI se prueba sin ventana es lo que se escribe dentro, que es
/// <see cref="ResumenEnPantalla"/>.</para>
/// </remarks>
public sealed class ZonaDeResumen
{
    /// <summary>El marco entero del bloque; la X lo esconde.</summary>
    private readonly Border _marco;
    /// <summary>El renglón de una línea que siempre se ve.</summary>
    private readonly TextBlock _linea;
    /// <summary>El botón que alterna el detalle; su texto dice «ver» u «ocultar».</summary>
    private readonly Button _ver;
    /// <summary>El marco del detalle, que empieza cerrado.</summary>
    private readonly Border _marcoDelDetalle;
    /// <summary>El texto largo de dentro del detalle.</summary>
    private readonly TextBlock _detalle;

    /// <summary>Se ata a los cinco controles que la pantalla ya tiene puestos en su XAML.</summary>
    /// <param name="marco">El marco entero del bloque.</param>
    /// <param name="linea">El renglón de una línea.</param>
    /// <param name="ver">El botón de «ver» / «ocultar».</param>
    /// <param name="marcoDelDetalle">El marco del detalle.</param>
    /// <param name="detalle">El texto del detalle.</param>
    public ZonaDeResumen(Border marco, TextBlock linea, Button ver, Border marcoDelDetalle, TextBlock detalle)
    {
        _marco = marco;
        _linea = linea;
        _ver = ver;
        _marcoDelDetalle = marcoDelDetalle;
        _detalle = detalle;
    }

    /// <summary>Ensena el resumen con el detalle cerrado, que es como tiene que empezar.</summary>
    /// <param name="resumen">La línea y el detalle que se pintan.</param>
    public void Ensenar(ResumenEnPantalla resumen)
    {
        ArgumentNullException.ThrowIfNull(resumen);

        _linea.Text = resumen.Linea;
        _detalle.Text = resumen.Detalle;
        _marco.Visibility = Visibility.Visible;
        _marcoDelDetalle.Visibility = Visibility.Collapsed;
        _ver.Content = "ver";
    }

    /// <summary>Abre el detalle si esta cerrado y lo cierra si esta abierto.</summary>
    public void AlternarElDetalle()
    {
        var abierto = _marcoDelDetalle.Visibility == Visibility.Visible;
        _marcoDelDetalle.Visibility = abierto ? Visibility.Collapsed : Visibility.Visible;
        _ver.Content = abierto ? "ver" : "ocultar";
    }

    /// <summary>Cierra el resumen entero, que es lo que hace la X.</summary>
    public void Cerrar() => _marco.Visibility = Visibility.Collapsed;
}
