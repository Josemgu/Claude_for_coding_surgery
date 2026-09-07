using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;


namespace Fichas.App.Correccion;

/// <summary>
/// Lo que el raton hace sobre el documento: arrastrar, ampliar con doble clic, y cronometrar.
/// </summary>
/// <remarks>
/// Va en su propio archivo y no porque el otro fuera largo: son dos trabajos distintos. En
/// <c>VisorDelDocumento.xaml.cs</c> esta lo que se PINTA —la hoja, el resalte, los rotulos—;
/// aqui esta lo que se ATIENDE del raton. Mezclar el reparto de pixeles con el dibujo es
/// donde se esconden los fallos de esta clase de pantallas (<c>interfaz/encuadre.py</c>).
/// <para>
/// ⚠️ <b>Lo que se midio el 2026-09-04, y hay que dejarlo escrito porque contradice el
/// informe de QA.</b> QA dio por medido que el arrastre no movia el visor: al 200 %,
/// arrastrando de (900,700) a (500,420), el <c>ScrollPresenter</c> se quedaba en H=22,34 %
/// V=10,21 % antes y despues. Se reprodujo con su misma herramienta y salio igual. Y
/// despues se repitio moviendo el raton con <c>mouse_event</c> —que INYECTA un suceso de
/// entrada— en vez de con <c>SetCursorPos</c> —que solo teletransporta el cursor y no
/// inyecta nada, asi que una aplicacion WinUI 3, que lee la entrada por punteros, no se
/// entera de que el raton se movio—. Con el raton inyectado, el <b>codigo de antes de este
/// pase</b> movio el visor de H=22,34 % a H=51,05 % y de V=10,21 % a V=19,93 %. O sea:
/// <b>el arrastre ya funcionaba y lo que fallaba era la medicion</b>. El control negativo
/// de QA —un clic en «Alejar»— no lo cazaba porque un clic si se inyecta; lo que no se
/// inyectaba eran los movimientos.
/// </para>
/// <para>
/// ⛔ Aun asi las suscripciones se hacen aqui y con <c>handledEventsToo</c>, y no en el
/// XAML, porque lo que SI es cierto es que se pierden movimientos: con el manejador del
/// XAML llegaban 2 de 8, y el <c>ScrollPresenter</c> marca como atendidos los demas. Con
/// esto llegan todos, y ademas el papel va a una posicion absoluta, asi que un movimiento
/// perdido ya no se pierde para siempre.
/// </para>
/// <para>
/// ⛔ <b>Shift y la rueda para moverse a lo ancho: SE INTENTO Y NO SE PUEDE aqui.</b> Se
/// escribio el manejador, primero sobre el <c>ScrollView</c> y despues sobre la hoja —que
/// llega antes— marcando el suceso como atendido, y en las dos la hoja siguio bajando
/// igual: medido con la ventana abierta, V=25,52 % a 29,97 % y H clavado en 0,00 %. El
/// motivo es que el <c>ScrollPresenter</c> no atiende la rueda como un suceso de XAML sino
/// por su propia entrada de interaccion, que no mira si alguien lo dio por atendido.
/// Apagarsela seria apagar tambien Ctrl y la rueda, que es el zoom sobre el cursor y ya se
/// decidio dejarselo a el. Queda sin resolver y dicho en la entrega; a lo ancho se llega
/// arrastrando y por la barra de abajo, y las dos cosas estan medidas.
/// </para>
/// </remarks>
public sealed partial class VisorDelDocumento
{
    /// <summary>
    /// Ata los sucesos del raton pidiendo TAMBIEN los que ya vengan atendidos.
    /// </summary>
    /// <remarks>
    /// Se llama desde el constructor, despues de montar el XAML. El
    /// <see cref="UIElement.PointerCaptureLostEvent"/> entra en la lista porque sin el un
    /// arrastre que pierde la captura —otra ventana se pone delante, el raton sale de la
    /// pantalla— deja el visor creyendo que se sigue arrastrando para siempre.
    /// </remarks>
    private void AtarElRaton()
    {
        _lienzo.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(AlApretarElPuntero), true);
        _lienzo.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(AlMoverElPuntero), true);
        _lienzo.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(AlSoltarElPuntero), true);
        _lienzo.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(AlPerderLaCaptura), true);
    }

    /// <summary>Empieza el arrastre con el boton izquierdo. El ScrollView solo arrastra con el dedo.</summary>
    /// <remarks>
    /// La captura se pide sobre la hoja y no sobre el <c>ScrollView</c>: la hoja es contenido
    /// y no tiene motor de desplazamiento propio con el que discutirse el puntero.
    /// </remarks>
    private void AlApretarElPuntero(object quien, PointerRoutedEventArgs cuando)
    {
        var punto = cuando.GetCurrentPoint(_lienzo);
        if (punto.PointerDeviceType == PointerDeviceType.Mouse && !punto.Properties.IsLeftButtonPressed) return;

        _arrastrando = true;
        _tramosDeEsteArrastre = 0;
        _sumaDeEsteArrastre = 0;
        _peorDeEsteArrastre = 0;
        _punteroQueArrastra = cuando.Pointer.PointerId;
        _dondeEmpezoElArrastre = punto.Position;
        _offsetAlEmpezarX = _lienzo.HorizontalOffset;
        _offsetAlEmpezarY = _lienzo.VerticalOffset;
        _hoja.CapturePointer(cuando.Pointer);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }

    /// <summary>Lleva el papel a donde toca segun cuanto se ha movido el raton DESDE EL PRINCIPIO.</summary>
    /// <remarks>
    /// ⚠️ <b>Se va a una posicion absoluta, no se suman saltos.</b> Antes era
    /// <c>ScrollBy(-dx, -dy)</c> con la diferencia respecto al movimiento anterior, y eso
    /// tiene dos averias: un movimiento que se pierde por el camino se pierde para siempre
    /// —y se pierden, esta medido: seis de ocho—, y dos peticiones seguidas en el mismo
    /// turno se pisan. Con la posicion absoluta el papel siempre acaba donde dice el raton,
    /// lleguen los movimientos que lleguen.
    /// </remarks>
    private void AlMoverElPuntero(object quien, PointerRoutedEventArgs cuando)
    {
        if (!_arrastrando || cuando.Pointer.PointerId != _punteroQueArrastra) return;

        _cronometroDelArrastre.Restart();
        var punto = cuando.GetCurrentPoint(_lienzo).Position;

        // El papel va al reves que el raton: arrastrar hacia la izquierda ensena lo de la
        // derecha, que es lo que hace todo el mundo con una hoja encima de la mesa.
        var haciaX = _offsetAlEmpezarX + (_dondeEmpezoElArrastre.X - punto.X);
        var haciaY = _offsetAlEmpezarY + (_dondeEmpezoElArrastre.Y - punto.Y);
        _ultimoPedidoX = haciaX;
        _ultimoPedidoY = haciaY;
        _lienzo.ScrollTo(haciaX, haciaY, SinAnimacion);
        _cronometroDelArrastre.Stop();

        CuantosArrastres++;
        MilisegundosDelUltimoArrastre = _cronometroDelArrastre.Elapsed.TotalMilliseconds;
        _tramosDeEsteArrastre++;
        _sumaDeEsteArrastre += MilisegundosDelUltimoArrastre;
        _peorDeEsteArrastre = Math.Max(_peorDeEsteArrastre, MilisegundosDelUltimoArrastre);
    }

    /// <summary>Suelta el arrastre y devuelve el cursor.</summary>
    private void AlSoltarElPuntero(object quien, PointerRoutedEventArgs cuando)
    {
        if (!_arrastrando) return;
        _hoja.ReleasePointerCapture(cuando.Pointer);
        TerminarElArrastre();
    }

    /// <summary>La captura se perdio por lo que sea: el arrastre termina igual, sin quedarse colgado.</summary>
    private void AlPerderLaCaptura(object quien, PointerRoutedEventArgs cuando)
    {
        if (!_arrastrando) return;
        TerminarElArrastre();
    }

    /// <summary>
    /// Cierra el arrastre y AVISA con sus cifras: cuantos tramos, cuanto costo y cuanto se movio.
    /// </summary>
    /// <remarks>
    /// Lo que se movio va en la medida a proposito. Sin ese par de numeros, el cuaderno decia
    /// «arrastre de 2 tramos media 0,28 ms» y parecia que todo iba bien mientras el papel no
    /// se habia movido ni un pixel. Una medicion de lo que TARDA sin la de lo que HACE es
    /// justo la que deja pasar este defecto.
    /// </remarks>
    private void TerminarElArrastre()
    {
        // ⚠️ La guarda es necesaria: soltar el boton suelta la captura, y eso levanta
        // «PointerCaptureLost» ANTES de que vuelva el manejador de soltar. Sin ella, un
        // arrastre se anotaba dos veces en el cuaderno.
        if (!_arrastrando) return;
        _arrastrando = false;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

        if (_tramosDeEsteArrastre == 0) return;
        ArrastreTerminado?.Invoke(this, new MedidaDelArrastre(
            _tramosDeEsteArrastre,
            _sumaDeEsteArrastre / _tramosDeEsteArrastre,
            _peorDeEsteArrastre,
            _lienzo.HorizontalOffset - _offsetAlEmpezarX,
            _lienzo.VerticalOffset - _offsetAlEmpezarY,
            _ultimoPedidoX - _offsetAlEmpezarX,
            _ultimoPedidoY - _offsetAlEmpezarY));
    }

    /// <summary>Doble clic: alterna entre la hoja a lo ancho y el 100 %.</summary>
    private void AlDoblePulsacion(object quien, DoubleTappedRoutedEventArgs cuando)
    {
        var alAncho = Encuadre.ZoomParaElAncho(_lienzo.ViewportWidth, _anchoDeLaHoja);
        if (Math.Abs(_zoomPedido - alAncho) < 0.01) AjustarACien();
        else AjustarAlAncho();
        cuando.Handled = true;
    }
}
