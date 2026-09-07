using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Fichas.App.Revisar;

/// <summary>
/// El trozo de la pantalla de Revisar que le da su fecha de viaje a un documento.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-05, petición 6: «lo que no se reconozca la fecha, ahí mismo en
/// revisión, en una sección que diga "revisar este documento" <b>para que se pueda ir a una
/// fecha</b>». La sección la pinta <see cref="ArbolDeRevisar"/>; esto es la otra mitad, que
/// es poder darle esa fecha sin salir de aquí.</para>
///
/// <para>Va en su propio archivo por lo de siempre en esta pantalla: es una responsabilidad
/// distinta de los tableros y de las carpetas, y <c>PaginaDeRevisar.xaml.cs</c> ya pasa del
/// límite blando de 300 líneas.</para>
///
/// <para><b>La regla no está aquí</b>: qué es una fecha y qué se escribe lo decide
/// <see cref="AccionesDeRevisar.PonerLaFechaDeViaje"/>, que se prueba sin abrir ninguna
/// ventana. Aquí solo se colocan controles y se pasan mensajes.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>Cómo se pide la fecha, y es lo único que se acepta.</summary>
    private const string FormatoDeLaFecha = "AAAA-MM-DD";

    /// <summary>Lo ancho que se hace el cuadro de escribir; cabe «2026-09-17» de sobra.</summary>
    private const int AnchoDeLaCaja = 160;

    /// <summary>
    /// Abre el cuadro para escribir la fecha de viaje de una tarjeta.
    /// </summary>
    /// <remarks>
    /// Es un <see cref="Flyout"/> y no un cuadro modal: se cierra pulsando fuera y no
    /// detiene la pantalla, que es la regla de los cuadros de este programa (requisito 9).
    /// Se arma en código y no en la plantilla porque tiene que quedarse con el número
    /// interno de ESA tarjeta, y una plantilla compartida por 3 000 tarjetas no puede.
    /// </remarks>
    private void AlAbrirElCuadroDeLaFecha(object quien, RoutedEventArgs cuando)
    {
        if (quien is not Button boton || boton.Tag is not long casoId) return;

        var caja = CajaDeLaFecha(_tablero?.De(casoId)?.FechaDeViajeIso ?? string.Empty);
        var poner = new Button { Content = "Poner esta fecha" };
        AutomationProperties.SetName(poner, "Poner esta fecha de viaje");

        var cuadro = new Flyout { Content = LoQueLleva(caja, poner) };

        // Las dos formas de confirmar hacen lo MISMO y por el mismo camino: pulsar el botón
        // y dar a Entrar en el cuadro. Escribir una fecha y que Entrar no haga nada es de
        // las cosas que se descubren tarde y con el trabajo ya perdido.
        poner.Click += (_, _) => PonerLaFechaYCerrar(cuadro, casoId, caja.Text);
        caja.KeyDown += (_, tecla) =>
        {
            if (tecla.Key != VirtualKey.Enter) return;
            tecla.Handled = true;
            PonerLaFechaYCerrar(cuadro, casoId, caja.Text);
        };

        // El foco se pide cuando el cuadro YA está abierto: pedirlo antes lo deja en un
        // control que todavía no está en el árbol visual y no pasa nada.
        cuadro.Opened += (_, _) => caja.Focus(FocusState.Programmatic);
        cuadro.ShowAt(boton);
    }

    /// <summary>El cuadro donde se escribe la fecha, con lo que ya tuviera dentro.</summary>
    /// <remarks>
    /// Un <c>TextBox</c> de ancho fijo y no un calendario, que es lo que ya usa la pantalla
    /// de corrección para cada campo: la fecha se copia de un papel que está delante, y
    /// escribirla es un gesto y navegar meses en un calendario son varios. Cambiar de
    /// control sería una decisión de diseño y no me toca tomarla.
    /// </remarks>
    private static TextBox CajaDeLaFecha(string loQueYaTenia)
    {
        var caja = new TextBox
        {
            Text = loQueYaTenia,
            PlaceholderText = FormatoDeLaFecha,
            FontFamily = new FontFamily("Consolas"),
            Width = AnchoDeLaCaja,
        };
        caja.SelectAll();
        AutomationProperties.SetName(caja, $"Fecha de viaje en {FormatoDeLaFecha}");
        return caja;
    }

    /// <summary>Lo que se ve dentro del cuadro: el formato, la caja y el botón.</summary>
    private static StackPanel LoQueLleva(TextBox caja, Button poner)
        => new()
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = $"Fecha de viaje ({FormatoDeLaFecha})", FontSize = 12 },
                caja,
                poner,
            },
        };

    /// <summary>Cierra el cuadro y escribe la fecha; el orden importa y por eso va junto.</summary>
    /// <remarks>
    /// Se cierra ANTES de escribir porque escribir repinta la rejilla entera, y un cuadro
    /// colgado del botón de una tarjeta que se está reconstruyendo es el mismo patrón que el
    /// 2026-09-04 tumbó el proceso sin dejar ni una línea en <c>fichas.log</c>.
    /// </remarks>
    private void PonerLaFechaYCerrar(Flyout cuadro, long casoId, string escrita)
    {
        cuadro.Hide();
        PonerLaFechaDeViaje(casoId, escrita);
    }

    /// <summary>
    /// Escribe la fecha de viaje de un documento y vuelve a pintar sus carpetas.
    /// </summary>
    /// <remarks>
    /// El repintado no es un adorno: es lo que hace que el documento SALGA de la sección de
    /// «revisar este documento» y aparezca en la carpeta de su día, que es lo que el dueño
    /// pidió ver. Los avisos de por qué no se escribió los deja
    /// <see cref="AccionesDeRevisar.PonerLaFechaDeViaje"/> en la franja, no esta pantalla.
    /// </remarks>
    private void PonerLaFechaDeViaje(long casoId, string escrita)
    {
        if (_acciones is null) return;

        if (_acciones.PonerLaFechaDeViaje(casoId, escrita).SeEscribio)
        {
            Acusar($"Fecha de viaje puesta en {escrita.Trim()}; el documento pasó a su grupo.");
        }

        Repintar();
    }
}
