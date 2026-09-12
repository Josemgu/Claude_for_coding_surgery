using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Fichas.App.Cascara;

/// <summary>
/// La base de las seis pantallas: recibe los servicios y avisa cuando ya estan puestos.
/// </summary>
/// <remarks>
/// Una pantalla NO llama a <c>new</c> de ningun repositorio ni nombra <c>Fichas.Datos.Falso</c>:
/// pide lo que necesita por <see cref="Servicios"/>, que solo expone interfaces de
/// <c>Fichas.Contratos</c>. Asi la fase C2 sustituye los datos sin tocar ninguna pantalla.
/// </remarks>
public class PaginaDeFichas : Page
{
    /// <summary>Los servicios de la app; hay que esperar a <see cref="AlLlegar"/> para usarlos.</summary>
    public Servicios? Servicios { get; private set; }

    /// <summary>Recoge los servicios que trae la navegacion y avisa a la pantalla. Si el parámetro no es un <see cref="Cascara.Servicios"/>, la propiedad queda nula y <see cref="AlLlegar"/> corre igual.</summary>
    /// <param name="cuando">Los datos de la navegación; en <c>Parameter</c> viene el <see cref="Cascara.Servicios"/>.</param>
    protected override void OnNavigatedTo(NavigationEventArgs cuando)
    {
        base.OnNavigatedTo(cuando);
        Servicios = cuando.Parameter as Servicios;
        AlLlegar();
    }

    /// <summary>Lo que hace la pantalla cuando ya tiene sus servicios. Por defecto, nada.</summary>
    protected virtual void AlLlegar()
    {
    }
}
