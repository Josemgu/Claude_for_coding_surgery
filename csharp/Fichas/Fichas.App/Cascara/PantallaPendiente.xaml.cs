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
    /// <param name="titulo">El nombre de la pantalla, el mismo que en el menú.</param>
    /// <param name="fase">De qué fase del plan es, para que se sepa cuándo llega.</param>
    /// <param name="cifras">Una línea con las cuentas de la base, para que se vea que la cáscara conecta.</param>
    public void Describir(string titulo, string fase, string cifras)
    {
        _titulo.Text = titulo;
        _duena.Text = fase;
        _cifras.Text = cifras;
    }
}
