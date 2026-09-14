using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Revisar;

/// <summary>
/// La franja vertical que se agarra con el ratón para agrandar o reducir el panel de carpetas.
/// </summary>
/// <remarks>
/// <para>Es una rejilla y no un control con plantilla: no tiene nada que pintar salvo la raya
/// que lleva dentro en el XAML, y lo que hace —capturar el puntero y mover la columna— lo
/// hace la página en <c>PaginaDeRevisar.Tirador.cs</c>. Existe como clase por dos cosas que
/// una rejilla a secas no puede dar:</para>
/// <list type="bullet">
///   <item>el cursor de redimensionar al pasar por encima, que en WinUI 3 solo se pone desde
///   dentro de la clase (<c>ProtectedCursor</c> es protegido);</item>
///   <item>un sitio en el árbol de accesibilidad, para que el lector de pantalla lo nombre y
///   para poder medirlo por UIA en la entrega. Un panel sin par de automatización es
///   invisible para los dos.</item>
/// </list>
///
/// <para>⚠️ Solo va con ratón. El marco del mockup v2 no trae ningún gesto de teclado para
/// esto y no se inventa uno: quien no use ratón sigue teniendo el panel a 290.</para>
///
/// <para>No se trae <c>CommunityToolkit</c> por un tirador (regla 3 de <c>CLAUDE.md</c>): es
/// poco código y no añade nada al programa.</para>
/// </remarks>
public sealed partial class TiradorDelPanel : Grid
{
    /// <summary>Pide el cursor de flecha doble horizontal, que es el que Windows usa para redimensionar.</summary>
    public TiradorDelPanel()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }

    /// <summary>Lo da de alta en el árbol de accesibilidad con el nombre que lleva en el XAML.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
