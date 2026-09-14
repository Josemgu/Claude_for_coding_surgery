using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Fichas.App.Revisar;

/// <summary>
/// La parte de la pantalla de Revisar que mueve el panel de carpetas con el ratón: agarrar el
/// tirador, arrastrarlo, soltarlo, el doble clic y recordar el ancho entre sesiones.
/// </summary>
/// <remarks>
/// <para>Lo pidió el dueño el 2026-09-14: <i>«que pueda ser ajustado con el mouse, agrandar o
/// reducir»</i>. Va en su propio archivo por el mismo reparto que <c>.Carpetas.cs</c>: es
/// otra responsabilidad, no la pantalla de siempre creciendo.</para>
///
/// <para>La regla no está aquí: el suelo, el techo y el recorte los decide
/// <see cref="AnchoDelPanelDeCarpetas"/> y el disco lo toca
/// <see cref="PreferenciaDelAnchoDelPanel"/>, y los dos se prueban sin ventana. Aquí solo se
/// leen posiciones del puntero y se pone un número en la columna.</para>
///
/// <para>Las posiciones se miden contra <c>_marcoDeCarpetasYTarjetas</c>, que no se mueve, y
/// no contra el tirador, que se desplaza con cada píxel que se arrastra: medirlas contra lo
/// que se mueve es el fallo clásico de los tiradores hechos a mano, y da saltos.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>El ancho recordado en la carpeta de datos; nulo hasta <see cref="AlLlegar"/>.</summary>
    private PreferenciaDelAnchoDelPanel? _preferenciaDelAncho;

    /// <summary>
    /// Lo que él eligió, sin recortar. Lo que se ve es esto recortado a la ventana de ahora.
    /// </summary>
    /// <remarks>
    /// Se guardan aparte a propósito: si la ventana se estrecha y el panel se recorta, al
    /// volver a agrandarla el panel recupera lo elegido, no lo recortado.
    /// </remarks>
    private double _anchoElegido = AnchoDelPanelDeCarpetas.ElDeSiempre;

    /// <summary>Lo que medía el panel al agarrar el tirador, o nulo si no se está arrastrando.</summary>
    private double? _anchoAlAgarrar;

    /// <summary>Dónde estaba el ratón, contra el marco, al agarrar el tirador.</summary>
    private double _xAlAgarrar;

    /// <summary>Lee el ancho guardado y lo pone en la columna, recortado a la ventana de ahora.</summary>
    private void MontarElAnchoDelPanel()
    {
        if (Servicios is null) return;

        _preferenciaDelAncho = new PreferenciaDelAnchoDelPanel(Servicios.Argumentos.CarpetaDeDatos);
        _anchoElegido = _preferenciaDelAncho.Leer();
        PintarElAnchoDelPanel();
    }

    /// <summary>Pone en la columna lo elegido, recortado al marco de este momento.</summary>
    private void PintarElAnchoDelPanel()
        => _columnaDeCarpetas.Width = new GridLength(
            AnchoDelPanelDeCarpetas.Recortar(_anchoElegido, _marcoDeCarpetasYTarjetas.ActualWidth));

    /// <summary>
    /// Al cambiar el tamaño de la ventana se vuelve a recortar: un ancho guardado que ya no
    /// cabe se acorta, y uno que vuelve a caber se recupera.
    /// </summary>
    /// <param name="quien">El marco de carpetas y tarjetas.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarElTamanoDelMarco(object quien, SizeChangedEventArgs cuando) => PintarElAnchoDelPanel();

    /// <summary>Agarra el tirador: captura el puntero y apunta desde dónde se arrastra.</summary>
    /// <param name="quien">El tirador.</param>
    /// <param name="cuando">Trae el puntero y su posición.</param>
    private void AlAgarrarElTirador(object quien, PointerRoutedEventArgs cuando)
    {
        if (!_tirador.CapturePointer(cuando.Pointer)) return;

        _anchoAlAgarrar = _columnaDeCarpetas.ActualWidth;
        _xAlAgarrar = cuando.GetCurrentPoint(_marcoDeCarpetasYTarjetas).Position.X;
        cuando.Handled = true;
    }

    /// <summary>Mientras se arrastra, la columna sigue al ratón dentro del suelo y el techo.</summary>
    /// <param name="quien">El tirador.</param>
    /// <param name="cuando">Trae la posición del puntero.</param>
    private void AlArrastrarElTirador(object quien, PointerRoutedEventArgs cuando)
    {
        if (_anchoAlAgarrar is not double alAgarrar) return;

        var x = cuando.GetCurrentPoint(_marcoDeCarpetasYTarjetas).Position.X;
        _anchoElegido = AnchoDelPanelDeCarpetas.TrasArrastrar(alAgarrar, x - _xAlAgarrar, _marcoDeCarpetasYTarjetas.ActualWidth);
        PintarElAnchoDelPanel();
        cuando.Handled = true;
    }

    /// <summary>Al soltar se libera el puntero y se guarda lo elegido, si cambió.</summary>
    /// <param name="quien">El tirador.</param>
    /// <param name="cuando">Trae el puntero que se suelta.</param>
    private void AlSoltarElTirador(object quien, PointerRoutedEventArgs cuando)
    {
        if (_anchoAlAgarrar is not double alAgarrar) return;

        _tirador.ReleasePointerCapture(cuando.Pointer);
        _anchoAlAgarrar = null;
        if (_anchoElegido != alAgarrar) GuardarElAnchoDelPanel();
        cuando.Handled = true;
    }

    /// <summary>
    /// Si Windows le quita el puntero a medio arrastre —otra ventana, un cuadro—, se termina
    /// como si hubiera soltado: lo que se ve es lo que queda.
    /// </summary>
    /// <param name="quien">El tirador.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPerderElTirador(object quien, PointerRoutedEventArgs cuando)
    {
        if (_anchoAlAgarrar is not double alAgarrar) return;

        _anchoAlAgarrar = null;
        if (_anchoElegido != alAgarrar) GuardarElAnchoDelPanel();
    }

    /// <summary>Doble clic: el panel vuelve a lo de siempre, y se guarda.</summary>
    /// <remarks>
    /// Llega entre la segunda pulsación y su suelta, con el puntero ya capturado por
    /// <see cref="AlAgarrarElTirador"/>: se suelta aquí para que el resto del gesto no
    /// arrastre nada.
    /// </remarks>
    /// <param name="quien">El tirador.</param>
    /// <param name="cuando">Los datos del gesto; se marca atendido.</param>
    private void AlHacerDobleClicEnElTirador(object quien, DoubleTappedRoutedEventArgs cuando)
    {
        _tirador.ReleasePointerCaptures();
        _anchoAlAgarrar = null;
        _anchoElegido = AnchoDelPanelDeCarpetas.TrasElDobleClic();
        PintarElAnchoDelPanel();
        GuardarElAnchoDelPanel();
        cuando.Handled = true;
    }

    /// <summary>Escribe lo elegido en la carpeta de datos; si el disco no deja, se dice en la franja.</summary>
    /// <remarks>
    /// Que no se pueda escribir NO deshace el arrastre: el panel se queda como él lo dejó en
    /// esta sesión, y lo único que se pierde es que lo recuerde la próxima. Es lo mismo que
    /// hace el tema, y por la misma razón: callarlo sería peor.
    /// </remarks>
    private void GuardarElAnchoDelPanel()
    {
        if (_preferenciaDelAncho is null || Servicios is null) return;
        if (_preferenciaDelAncho.Guardar(_anchoElegido)) return;

        Servicios.Avisos.Dejar(Aviso.Advierte(
            "El panel de carpetas cambió de ancho, pero no se pudo guardar para la próxima vez.",
            string.Empty,
            $"No se pudo escribir «{_preferenciaDelAncho.Ruta}». Revisar volverá a abrir con el ancho "
            + "anterior. Compruebe que esa carpeta existe y se puede escribir."));
    }
}
