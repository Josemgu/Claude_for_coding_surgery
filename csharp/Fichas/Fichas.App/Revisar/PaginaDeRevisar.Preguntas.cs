using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Fichas.App.Revisar;

/// <summary>
/// El trozo de Revisar que abre la ventana de las seis preguntas de un documento.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-05: <i>«en Revisar ahí debe poder dar clic al documento, abrir
/// otra ventana donde está la información, comentarios o preguntas, para llegar a si se
/// completó o no la información en el sistema del obispo»</i>. Es su petición 3, y la 4 es
/// la misma ventana: <i>«cada caso que tengo en revisión debe poder entrar y completar las
/// preguntas que dicen si está listo para viajar o no»</i>.</para>
///
/// <para>Va en su propio archivo por lo de siempre en esta pantalla: es una responsabilidad
/// distinta de los tableros y de las carpetas, y <c>PaginaDeRevisar.xaml.cs</c> ya pasa del
/// límite blando de 300 líneas.</para>
///
/// <para><b>La regla no está aquí</b>: qué se ve y qué se escribe lo deciden
/// <see cref="PreguntasDeUnDocumento"/> y <see cref="AccionesDeLasPreguntas"/>, que se
/// prueban sin abrir ninguna ventana. Aquí solo se abre una y se le pasa un número.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>Las ventanas abiertas, por documento: dos del mismo serían dos verdades.</summary>
    /// <remarks>
    /// ⚠️ Con dos ventanas del MISMO documento abiertas, guardar en una deja a la otra
    /// enseñando lo de antes, y quien mire la segunda creerá que no se guardó. Se guarda la
    /// que ya está abierta y se vuelve a ella. De documentos DISTINTOS sí puede haber
    /// varias: es lo que él pidió al decir «abrir otra ventana».
    /// </remarks>
    private readonly Dictionary<long, VentanaDeLasPreguntas> _ventanasDePreguntas = [];

    /// <summary>Abre —o trae al frente— la ventana de las seis preguntas de ese documento.</summary>
    /// <param name="quien">El botón de las preguntas; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlAbrirLasSeisPreguntas(object quien, RoutedEventArgs cuando)
    {
        if (Servicios is null || quien is not Button boton || boton.Tag is not long casoId) return;

        if (_ventanasDePreguntas.TryGetValue(casoId, out var abierta))
        {
            abierta.Activate();
            return;
        }

        var ventana = new VentanaDeLasPreguntas(Servicios, casoId, ActualTheme);
        _ventanasDePreguntas[casoId] = ventana;

        // Al cerrarse se olvida, o el segundo intento de abrirla activaria una ventana que
        // ya no existe y el programa se caeria con el documento delante. Y se repinta
        // Revisar: desde el 2026-09-14 esa ventana puede dejar el documento completado, y
        // la tarjeta tiene que cambiar de tablero sin que el tenga que tocar nada mas.
        // Si ya no estaba apuntada es que la cerro OnNavigatedFrom, y ahi no se repinta.
        ventana.Closed += (_, _) =>
        {
            if (_ventanasDePreguntas.Remove(casoId)) Repintar();
        };

        ventana.Activate();
    }

    /// <summary>Al salir de Revisar se cierran las ventanas de preguntas que queden abiertas.</summary>
    /// <remarks>
    /// Una ventana de preguntas viva sobre una pantalla que ya no existe seguiria leyendo y
    /// escribiendo en la base sin que nadie la vea desde el programa.
    /// </remarks>
    /// <param name="cuando">Los datos de la navegación, que se pasan a la base.</param>
    protected override void OnNavigatedFrom(NavigationEventArgs cuando)
    {
        // Se olvidan ANTES de cerrarlas: asi su Closed no repinta una pagina que se va.
        var abiertas = _ventanasDePreguntas.Values.ToList();
        _ventanasDePreguntas.Clear();
        foreach (var ventana in abiertas) ventana.Close();
        base.OnNavigatedFrom(cuando);
    }
}
