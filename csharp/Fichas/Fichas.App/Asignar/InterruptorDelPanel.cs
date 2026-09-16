namespace Fichas.App.Asignar;

/// <summary>Lo que toca hacer con el panel del equipo tras un clic en su botón.</summary>
public enum GestoDelPanel
{
    /// <summary>Estaba cerrado: se abre.</summary>
    Abrir,

    /// <summary>Estaba abierto: se cierra, y no se abre otro encima.</summary>
    Cerrar,
}

/// <summary>
/// La regla del botón «Equipo…»: un interruptor, no un botón de «abrir otro».
/// </summary>
/// <remarks>
/// <para>⛔ <b>El defecto que cierra, del dueño el 2026-09-16:</b> <i>«En el botón Equipo, el
/// segundo clic friza todo el programa; y lo cierra también»</i>. Medido en el código ese
/// día: cada clic creaba un <c>Flyout</c> nuevo y volvía a colgar de un <c>Grid</c> nuevo los
/// mismos controles —la caja del nombre, los desplegables, la lista— que ya eran hijos del
/// panel anterior. WinUI no admite un elemento con dos padres: el segundo clic lanzaba y el
/// proceso moría.</para>
///
/// <para>Dos cosas lo arreglan y esta clase es la mitad que se prueba sin ventana: el
/// armazón del panel se construye UNA vez y se reutiliza (eso vive en
/// <see cref="PanelDelEquipo"/>), y el clic con el panel abierto lo CIERRA en vez de abrir
/// otro (eso vive aquí). El panel avisa con <see cref="AlCerrarse"/> cuando se cierra por
/// fuera —Escape, clic fuera, la pregunta de borrar—, para que el siguiente clic abra.</para>
/// </remarks>
public sealed class InterruptorDelPanel
{
    /// <summary>Si el panel está abierto ahora mismo, según lo último que se le dijo.</summary>
    public bool EstaAbierto { get; private set; }

    /// <summary>Un clic en el botón: abre si estaba cerrado, cierra si estaba abierto.</summary>
    /// <returns>El gesto que toca; el estado queda ya cambiado.</returns>
    public GestoDelPanel AlPulsar()
    {
        EstaAbierto = !EstaAbierto;
        return EstaAbierto ? GestoDelPanel.Abrir : GestoDelPanel.Cerrar;
    }

    /// <summary>El panel se cerró, por el camino que fuera; repetirlo no cambia nada.</summary>
    public void AlCerrarse() => EstaAbierto = false;
}
