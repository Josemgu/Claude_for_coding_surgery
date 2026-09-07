using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Fichas.App.Correccion;

/// <summary>
/// Enseña un renglón solo cuando tiene algo que decir; vacío, no ocupa.
/// </summary>
/// <remarks>
/// <para>Es el primer convertidor del programa, y existe por un motivo concreto: dentro de
/// una <c>DataTemplate</c> anidada, <c>x:Bind</c> no puede llamar a un método de la página,
/// así que la única forma de decidir la visibilidad desde el dato es ésta. La alternativa
/// —dejar el renglón siempre puesto— deja un hueco en blanco bajo cada persona de la que el
/// compañero no escribió nota, y con doce personas eso son doce huecos.</para>
///
/// <para>⚠️ <b>La otra alternativa era peor:</b> poner una propiedad <c>Visibility</c> dentro
/// de <see cref="RespuestaDelCompanero"/>. Eso ataría a XAML una clase que hoy se prueba sin
/// abrir ninguna ventana, que es justo lo que hace que sus frases se puedan comprobar.</para>
///
/// <para>Solo hace una cosa y no la disimula: texto con algo dentro se ve, texto vacío o en
/// blanco no. Nada más.</para>
/// </remarks>
public sealed partial class HayTexto : IValueConverter
{
    /// <summary>Visible si el texto tiene algo; recogido si está vacío o en blanco.</summary>
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>No se usa: esto solo va del dato a la pantalla.</summary>
    /// <exception cref="NotSupportedException">Siempre. Nadie escribe hacia atrás por aquí.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("Este convertidor solo va del dato a la pantalla.");
}
