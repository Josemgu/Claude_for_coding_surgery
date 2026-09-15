using Microsoft.UI.Windowing;
using Windows.UI;

namespace Fichas.App.Cascara;

/// <summary>
/// Entre que temas elige el dueno.
/// </summary>
/// <remarks>
/// Son TRES y no dos. «El de Windows» es lo que hacia el programa antes del 2026-09-05, y
/// tiene que seguir estando: quitarlo obligaria a elegir a quien no quiere elegir.
/// </remarks>
public enum TemaDeLaVentana
{
    /// <summary>Lo que hacia el programa hasta el 2026-09-05: seguir al sistema.</summary>
    ElDeWindows = 0,

    /// <summary>Claro siempre, este Windows como este.</summary>
    Claro = 1,

    /// <summary>Oscuro siempre, este Windows como este.</summary>
    Oscuro = 2,
}

/// <summary>
/// El vocabulario del tema: como se guarda en el archivo y como se lee en la pantalla.
/// </summary>
/// <remarks>
/// Esta separado de <see cref="PreferenciaDeTema"/> a proposito: aqui no se toca el disco, y
/// por eso se puede probar entero sin carpeta y sin ventana.
/// </remarks>
public static class TemasDeLaVentana
{
    /// <summary>Como se escribe en <c>preferencias.txt</c>; en minusculas y sin tildes.</summary>
    /// <remarks>
    /// Sin tildes NO por descuido: es una clave de archivo, no un rotulo, y va como van los
    /// identificadores de todo el proyecto. Lo que lee el dueno es <see cref="ComoSeLee"/>.
    /// </remarks>
    /// <param name="tema">El tema; cualquier valor que no sea claro ni oscuro se guarda como «windows».</param>
    public static string ComoSeGuarda(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => "claro",
        TemaDeLaVentana.Oscuro => "oscuro",
        _ => "windows",
    };

    /// <summary>El rotulo que se lee en la pantalla, en español.</summary>
    /// <param name="tema">El tema; cualquier valor que no sea claro ni oscuro se lee «El de Windows».</param>
    public static string ComoSeLee(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => "Claro",
        TemaDeLaVentana.Oscuro => "Oscuro",
        _ => "El de Windows",
    };

    /// <summary>
    /// Traduce lo que habia escrito en el archivo; lo que no se entiende vuelve al de Windows.
    /// </summary>
    /// <remarks>
    /// No lanza nunca. Un archivo de preferencias roto no puede dejar al dueno con un icono
    /// que no abre: es la misma regla que ya siguen los argumentos de la linea de ordenes.
    /// </remarks>
    /// <param name="guardado">Lo que había detrás de <c>tema=</c> en el archivo; nulo, espacios y mayúsculas se toleran.</param>
    public static TemaDeLaVentana Interpretar(string? guardado) => guardado?.Trim().ToLowerInvariant() switch
    {
        "claro" => TemaDeLaVentana.Claro,
        "oscuro" => TemaDeLaVentana.Oscuro,
        _ => TemaDeLaVentana.ElDeWindows,
    };

    /// <summary>
    /// Traduce la eleccion del dueno a lo que el marco de la ventana le pide a Windows.
    /// </summary>
    /// <remarks>
    /// Es lo que va en <c>AppWindow.TitleBar.PreferredTheme</c>: el borde de la ventana, el
    /// menu de la barra (Alt+Espacio) y lo demas que dibuja Windows fuera del contenido.
    /// «El de Windows» se queda en <c>UseDefaultAppMode</c> y no se traduce a claro u oscuro
    /// fijo: si se tradujera, el marco dejaria de seguir al sistema justo en la parte que el
    /// dueno senalo.
    /// </remarks>
    /// <param name="tema">El tema elegido; cualquier valor que no sea claro ni oscuro sigue a Windows.</param>
    public static TitleBarTheme ComoLoPrefiereElMarco(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => TitleBarTheme.Light,
        TemaDeLaVentana.Oscuro => TitleBarTheme.Dark,
        _ => TitleBarTheme.UseDefaultAppMode,
    };
}

/// <summary>
/// Los cinco colores de la barra de titulo en un tema: el fondo, la tinta de los botones con
/// la ventana delante y detras, y el fondo del boton al pasar por encima y al pulsar.
/// </summary>
/// <param name="Fondo">El fondo de la barra entera.</param>
/// <param name="Tinta">La tinta de minimizar, maximizar y cerrar con la ventana delante.</param>
/// <param name="TintaInactiva">La misma tinta cuando la ventana esta detras de otra.</param>
/// <param name="FondoDelBotonAlPasar">El fondo del boton mientras el cursor esta encima.</param>
/// <param name="FondoDelBotonAlPulsar">El fondo del boton mientras se pulsa.</param>
public readonly record struct PaletaDeLaBarraDeTitulo(
    Color Fondo,
    Color Tinta,
    Color TintaInactiva,
    Color FondoDelBotonAlPasar,
    Color FondoDelBotonAlPulsar);

/// <summary>
/// Los colores de la barra de titulo, uno por tema, sin tocar la ventana.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-15 con una captura de la barra BLANCA sobre el programa
/// en oscuro: «esta parte quiero que se ponga de color de Wind…». Medido sobre <c>master</c>
/// ese dia con <c>tema=oscuro</c>: barra del sistema #F3F3F3 y boton cerrar #171818.</para>
///
/// <para>Esta separado de la ventana a proposito, como <see cref="TemasDeLaVentana"/>: aqui
/// no hay ventana ni pincel, solo <see cref="Color"/>, y por eso se prueba entero
/// (<c>PruebasDeLaBarraDeTitulo</c>) sin abrir nada.</para>
///
/// <para>⚠️ <b>El fondo es el mismo que pinta la raiz de la ventana</b>
/// (<c>ApplicationPageBackgroundThemeBrush</c> de WinUI: #F3F3F3 en claro y #202020 en
/// oscuro, medido por pixel en el rail el 2026-09-15). Va escrito aqui y no leido de ese
/// recurso porque un recurso de XAML no se puede leer sin ventana, y entonces no se podria
/// probar. Si algun dia la raiz cambiara de pincel, la barra dejaria de ser continua con el
/// rail y se veria la costura: la captura de la entrega es lo que lo vigila.</para>
///
/// <para>La tinta y la tinta inactiva son <c>Tinta</c> y <c>Apagado</c> de la paleta de
/// <c>App.xaml</c>, en su tema, para que los tres botones tengan el mismo color que el texto
/// que tienen debajo.</para>
/// </remarks>
public static class ColoresDeLaBarraDeTitulo
{
    /// <summary>La paleta del tema con el que se esta pintando.</summary>
    /// <param name="esOscuro">Verdadero si la ventana esta en oscuro. Recibe el tema YA RESUELTO: «el de Windows» ya es claro u oscuro cuando llega aqui.</param>
    public static PaletaDeLaBarraDeTitulo Para(bool esOscuro) => esOscuro ? Oscura : Clara;

    /// <summary>En claro: fondo #F3F3F3, tinta #1A1D21, inactiva #646C77, al pasar #E6E6E6, al pulsar #D9D9D9.</summary>
    private static readonly PaletaDeLaBarraDeTitulo Clara = new(
        Fondo: Opaco(0xF3, 0xF3, 0xF3),
        Tinta: Opaco(0x1A, 0x1D, 0x21),
        TintaInactiva: Opaco(0x64, 0x6C, 0x77),
        FondoDelBotonAlPasar: Opaco(0xE6, 0xE6, 0xE6),
        FondoDelBotonAlPulsar: Opaco(0xD9, 0xD9, 0xD9));

    /// <summary>En oscuro: fondo #202020, tinta #F2F3F5, inactiva #A7B0BB, al pasar #2D2D2D, al pulsar #383838.</summary>
    private static readonly PaletaDeLaBarraDeTitulo Oscura = new(
        Fondo: Opaco(0x20, 0x20, 0x20),
        Tinta: Opaco(0xF2, 0xF3, 0xF5),
        TintaInactiva: Opaco(0xA7, 0xB0, 0xBB),
        FondoDelBotonAlPasar: Opaco(0x2D, 0x2D, 0x2D),
        FondoDelBotonAlPulsar: Opaco(0x38, 0x38, 0x38));

    /// <summary>Un color opaco a partir de sus tres componentes.</summary>
    /// <param name="rojo">Componente rojo, 0 a 255.</param>
    /// <param name="verde">Componente verde, 0 a 255.</param>
    /// <param name="azul">Componente azul, 0 a 255.</param>
    private static Color Opaco(byte rojo, byte verde, byte azul) => Color.FromArgb(0xFF, rojo, verde, azul);
}
