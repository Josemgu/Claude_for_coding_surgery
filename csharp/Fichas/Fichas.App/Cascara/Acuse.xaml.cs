using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Cascara;

/// <summary>
/// El acuse de recibo del pie: una linea que se ensena unos segundos y se apaga sola.
/// </summary>
/// <remarks>
/// Requisito 3 del dueno: guardar tiene que acusar recibo. NO es un cuadro modal, no
/// hay que cerrarlo y no interrumpe lo que se este haciendo.
/// </remarks>
public sealed partial class Acuse : UserControl
{
    /// <summary>El cronómetro de cuatro segundos que apaga la línea; se reinicia con cada acuse.</summary>
    private readonly DispatcherTimer _cronometro = new();

    /// <summary>Monta el control con su cronometro de cuatro segundos.</summary>
    public Acuse()
    {
        InitializeComponent();
        _cronometro.Interval = TimeSpan.FromSeconds(4);
        _cronometro.Tick += AlCumplirseElTiempo;
    }

    /// <summary>Ensena una linea en el pie y arranca el cronometro para apagarla. Un acuse nuevo sustituye al anterior y vuelve a contar desde cero.</summary>
    /// <param name="linea">Lo que se enseña, ya escrito en español y en una sola línea.</param>
    public void Decir(string linea)
    {
        _texto.Text = linea;
        _texto.Opacity = 1;
        _cronometro.Stop();
        _cronometro.Start();
    }

    /// <summary>
    /// Deja una linea FIJA, que no se apaga sola y vuelve despues de cada acuse.
    /// </summary>
    /// <remarks>
    /// Existe para una sola cosa y no conviene darle otras: avisar de que el programa
    /// abrio con datos inventados. Eso no puede desaparecer a los cuatro segundos, porque
    /// entonces cualquier cifra que se lea despues parece real.
    /// </remarks>
    /// <param name="linea">La línea que se queda fija hasta que se cierre el programa.</param>
    public void DecirSiempre(string linea)
    {
        LoQueSeQuedaFijo = linea;
        _texto.Text = linea;
        _texto.Opacity = 1;
    }

    /// <summary>La linea fija, o nula si no hay ninguna. La lee la prueba sin abrir ventana.</summary>
    public string? LoQueSeQuedaFijo { get; private set; }

    /// <summary>Apaga el acuse cuando se cumple el tiempo, o repone la linea fija.</summary>
    /// <param name="quien">El cronómetro; no se usa.</param>
    /// <param name="cuando">Los datos del tic; no se usan.</param>
    private void AlCumplirseElTiempo(object? quien, object cuando)
    {
        _cronometro.Stop();
        if (LoQueSeQuedaFijo is not null)
        {
            _texto.Text = LoQueSeQuedaFijo;
            return;
        }

        _texto.Opacity = 0;
    }
}
