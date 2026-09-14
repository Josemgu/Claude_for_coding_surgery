using System.Runtime.InteropServices;
using Fichas.App.Inicio;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Revisar;

/// <summary>
/// La parte de Revisar que atiende a los duplicados: el botón de unificar, el de quitar la
/// marca, y la paleta con la que la tarjeta se pinta de rojo.
/// </summary>
/// <remarks>
/// <para>Va en archivo aparte porque <c>PaginaDeRevisar.xaml.cs</c> ya pasa del límite blando
/// de 300 líneas y esta pantalla se parte por gestos, igual que la fecha o las preguntas.</para>
///
/// <para>⚠️ <b>La tarjeta roja se pinta con <see cref="PinturaDeInicio"/></b>, que resuelve sus
/// colores con <see cref="PinturaDeInicio.Tema"/>. Esta pantalla lo pone al llegar y cada vez que
/// el dueño cambia de tema, y vuelve a montar las tarjetas: un <c>x:Bind</c> de una sola vez no
/// se entera solo del cambio. Es el mismo gesto que hacen Inicio, Grupo y el flujo.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>La única puerta de unificar y de quitar la marca; nula hasta <c>AlLlegar</c>.</summary>
    private OperacionDeUnificar? _unificar;

    /// <summary>Monta la operación de unificar y ata la paleta al tema de la pantalla.</summary>
    private void MontarLoDeLosDuplicados()
    {
        if (Servicios is null) return;

        _unificar = new OperacionDeUnificar(Servicios.Mantenimiento, Servicios.Reloj, Servicios.Avisos, Servicios.Registro);
        PinturaDeInicio.Tema = ActualTheme;
        ActualThemeChanged += AlCambiarElTema;
    }

    /// <summary>El dueño cambió de tema: la paleta se pone al día y las tarjetas se vuelven a montar.</summary>
    /// <param name="quien">La página cuyo tema cambió.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarElTema(FrameworkElement quien, object cuando)
    {
        PinturaDeInicio.Tema = ActualTheme;
        PintarLasTarjetas();
    }

    /// <summary>«Unificar con el original»: copia, pregunta con lo que va a pasar delante, unifica y repinta.</summary>
    /// <param name="quien">El botón; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlPulsarUnificar(object quien, RoutedEventArgs cuando)
    {
        if (quien is not Button boton || boton.Tag is not long casoId || _unificar is null) return;
        await ConUnSoloCuadro(() => _unificar.PreguntarYUnificar(casoId, XamlRoot));
    }

    /// <summary>
    /// «Quitar la marca de duplicado»: abre el menú ligero que pregunta; con su botón, el
    /// documento pasa a ser normal y se repinta.
    /// </summary>
    /// <param name="quien">El botón; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarQuitarLaMarcaDeDuplicado(object quien, RoutedEventArgs cuando)
    {
        if (quien is not Button boton || boton.Tag is not long casoId || _unificar is null || _tablero is null) return;
        if (_tablero.De(casoId) is not TarjetaDeDocumento tarjeta) return;

        _unificar.PreguntarYQuitarLaMarca(tarjeta, boton, linea =>
        {
            if (linea is not null) Acusar(linea);
            Repintar();
        });
    }

    /// <summary>
    /// Levanta un cuadro con el mismo cerrojo que borrar: dos a la vez tumban la ventana.
    /// </summary>
    /// <param name="preguntar">La operación que levanta el cuadro y devuelve la línea del acuse, o nula.</param>
    private async Task ConUnSoloCuadro(Func<Task<string?>> preguntar)
    {
        if (_hayUnCuadroAbierto) return;

        _hayUnCuadroAbierto = true;
        try
        {
            var linea = await preguntar();
            if (linea is not null) Acusar(linea);
        }
        catch (Exception fallo) when (fallo is InvalidOperationException or COMException)
        {
            // No se silencia: se DICE. Es lo que lanza WinUI cuando ya hay un cuadro
            // levantado o cuando la raíz visual se fue mientras se preguntaba.
            Servicios?.Avisos.Dejar(Aviso.Problema(
                "No se pudo abrir la pregunta, así que no se cambió nada.",
                string.Empty,
                $"Detalle: {fallo.Message}. Cierra cualquier otro cuadro abierto y vuelve a intentarlo."));
        }
        finally
        {
            _hayUnCuadroAbierto = false;
        }

        Repintar();
    }
}
