using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Revisar;

/// <summary>
/// El trozo de la pantalla de Revisar que abre el detalle de un estado: qué falta y a quién
/// le toca.
/// </summary>
/// <remarks>
/// <para><b>Es la otra mitad de la decisión del dueño del 2026-09-07.</b> Colapsó las cuatro
/// palabras a dos, y con la condición: <i>«me falta siempre puede decir qué falta y a quién le
/// toca, y lo dice cuando él lo pide, no de entrada. Una cifra a la vista, el detalle está a un
/// clic»</i>. La cifra es la pastilla; esto es el clic.</para>
///
/// <para><b>Por qué un <see cref="Flyout"/> y no una línea más en la tarjeta.</b> Él ya rechazó
/// por escrito los avisos que ocupan media pantalla, y con 3 000 documentos una línea de más por
/// tarjeta es media pantalla en cada una. Un <c>Flyout</c> además se cierra pulsando fuera y no
/// detiene la pantalla, que es la regla de los cuadros de este programa (requisito 9: avisar,
/// nunca impedir).</para>
///
/// <para>⛔ <b>Aquí no hay ni una regla.</b> Qué dice el detalle lo decide
/// <c>LoQueSeLeeDeUnDocumento</c>, que se prueba sin abrir ninguna ventana. Esto solo coloca un
/// texto y lo enseña al lado de la pastilla que se pulsó.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>Lo ancho que se hace el cuadro del detalle; una frase larga cabe en dos renglones.</summary>
    private const int AnchoDelDetalle = 320;

    /// <summary>
    /// Abre el detalle del estado de una tarjeta, al pulsar su pastilla.
    /// </summary>
    /// <remarks>
    /// Se arma en código y no en la plantilla porque tiene que quedarse con el número interno
    /// de ESA tarjeta, y una plantilla compartida por 3 000 tarjetas no puede. Es el mismo
    /// camino que ya usa <see cref="AlAbrirElCuadroDeLaFecha"/>.
    /// </remarks>
    private void AlAbrirElDetalleDelEstado(object quien, RoutedEventArgs cuando)
    {
        if (quien is not Button boton || boton.Tag is not long casoId) return;

        var tarjeta = _tablero?.De(casoId);
        if (tarjeta is null) return;

        new Flyout { Content = LoQueDiceElDetalle(tarjeta) }.ShowAt(boton);
    }

    /// <summary>
    /// Lo que se lee dentro del cuadro: la palabra arriba y debajo qué falta y a quién le toca.
    /// </summary>
    /// <remarks>
    /// La palabra se repite dentro a propósito, en grande: el cuadro se abre encima de la
    /// pastilla y la tapa, y un detalle suelto no dice de qué documento habla.
    /// </remarks>
    private static StackPanel LoQueDiceElDetalle(TarjetaDeDocumento tarjeta)
    {
        var cabecera = new TextBlock
        {
            Text = $"{tarjeta.NumeroDeCaso} · {tarjeta.PalabraDelEstado}",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
        };

        var detalle = new TextBlock
        {
            Text = tarjeta.DetalleDelEstado,
            TextWrapping = TextWrapping.Wrap,
        };

        var caja = new StackPanel { Spacing = 6, Width = AnchoDelDetalle };
        caja.Children.Add(cabecera);
        caja.Children.Add(detalle);

        // La firma va abajo y solo cuando la hay: es lo que dice QUIEN dio por bueno este
        // documento, y sin ella «resuelto» no distinguiria lo que marcó el Excel de un
        // compañero de lo que cerró el dueño. Es la regla permanente 5 dicha en la pantalla.
        if (tarjeta.PieDeLaTarjeta.Length > 0)
        {
            caja.Children.Add(new TextBlock
            {
                Text = tarjeta.PieDeLaTarjeta,
                Opacity = 0.8,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return caja;
    }
}
