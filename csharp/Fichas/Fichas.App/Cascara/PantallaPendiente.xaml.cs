using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Cascara;

/// <summary>
/// El relleno de una pantalla que todavia no existe: su nombre, la palabra «pendiente»,
/// de que fase es, y cuantos datos hay detras para que se vea que la cascara conecta.
/// </summary>
public sealed partial class PantallaPendiente : UserControl
{
    /// <summary>Monta el control vacio.</summary>
    public PantallaPendiente() => InitializeComponent();

    /// <summary>Escribe el nombre de la pantalla, de que fase es y las cifras que hay detras.</summary>
    public void Describir(string titulo, string fase, string cifras)
    {
        _titulo.Text = titulo;
        _duena.Text = fase;
        _cifras.Text = cifras;
    }
}
