namespace Fichas.App.Cascara;

/// <summary>
/// La parte de la ventana que se ocupa del icono de la barra de titulo y de la barra de tareas.
/// </summary>
/// <remarks>
/// <para>⚠️ Hace falta a proposito, y esto esta MEDIDO el 2026-09-05 sobre el paquete
/// publicado, no supuesto. Con el icono ya dentro de <c>Fichas.exe</c> —lo declara
/// <c>ApplicationIcon</c> en el <c>.csproj</c> y <c>ExtractAssociatedIcon</c> lo saca de
/// ahi—, la ventana seguia ensenando el icono verde de la plantilla de Microsoft. El motivo
/// tambien esta medido: la ventana NO tenia icono propio
/// (<c>WM_GETICON</c> y <c>GCLP_HICON</c> devolvian 0), asi que quien pintaba la barra de
/// titulo escogia por su cuenta.</para>
///
/// <para>La cura es decirlo explicitamente con <c>AppWindow.SetIcon</c>. El mismo icono vale
/// para la barra de titulo y para la barra de tareas.</para>
///
/// <para>⚠️ Desde el 2026-09-15 la barra de titulo es la del programa
/// (<c>VentanaPrincipal.Tema.cs</c>, <c>MontarLaBarraDeTitulo</c>) y el icono que se ve en
/// ella lo pone <c>TitleBar.IconSource</c> en el XAML. Esto sigue haciendo falta: es lo que
/// ensena la barra de tareas y Alt+Tab, que Windows sigue dibujando por su cuenta.</para>
/// </remarks>
public sealed partial class VentanaPrincipal
{
    /// <summary>Pone el icono de la ventana; si no se puede, se anota y no se detiene nada.</summary>
    /// <remarks>
    /// Que falte el icono no puede impedir abrir el programa: seria cambiar un defecto de
    /// aspecto por uno de «no arranca». Queda escrito en <c>fichas.log</c> para poder verlo.
    /// </remarks>
    private void PonerElIcono()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        try
        {
            if (!File.Exists(ruta))
            {
                _servicios.Registro.Anotar($"SIN ICONO  no existe {ruta}");
                return;
            }

            AppWindow.SetIcon(ruta);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _servicios.Registro.Anotar($"SIN ICONO  {fallo.GetType().Name}: {fallo.Message}");
        }
    }
}
