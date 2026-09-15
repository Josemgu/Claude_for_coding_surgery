using Fichas.Contratos.Modelos;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Fichas.App.Cascara;

/// <summary>
/// La parte de la ventana que se ocupa del tema: leerlo al abrir, pintarlo —tambien en la
/// barra de titulo— y guardarlo.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-05: poder elegir el tema en vez de seguir siempre al de
/// Windows, y que el programa lo recuerde entre sesiones. Y el 2026-09-15 mando la captura
/// de la barra de titulo blanca con el programa en oscuro: «esta parte quiero que se ponga
/// de color de Wind…». Desde ese dia la barra es la del programa y se pinta con el tema.</para>
///
/// <para>Va aparte de <c>VentanaPrincipal.xaml.cs</c> —misma clase, otro archivo— para que la
/// cascara siga siendo legible de una lectura: alli esta la navegacion, aqui el tema.</para>
/// </remarks>
public sealed partial class VentanaPrincipal
{
    /// <summary>Un color sin nada: lo que se pone de fondo a un boton para que se vea la barra.</summary>
    private static readonly Color SinColor = Color.FromArgb(0, 0, 0, 0);

    /// <summary>El tema elegido, leído y guardado en la carpeta de datos; nulo hasta que la ventana lo monta.</summary>
    private PreferenciaDeTema? _preferenciaDeTema;

    /// <summary>
    /// Mientras se monta, marcar el circulo NO cuenta como que el dueno eligio.
    /// </summary>
    /// <remarks>
    /// ⚠️ Sin esto, poner <c>IsChecked</c> al abrir dispara <c>Checked</c> y el programa
    /// escribiria el archivo en cada arranque sin que nadie haya tocado nada.
    /// </remarks>
    private bool _montandoElTema;

    /// <summary>Lee el tema guardado, hace suya la barra de titulo, pinta y deja marcado el circulo que toca.</summary>
    private void MontarElTema()
    {
        _preferenciaDeTema = new PreferenciaDeTema(_servicios.Argumentos.CarpetaDeDatos);
        var tema = _preferenciaDeTema.Leer();

        _montandoElTema = true;
        _temaDeWindows.IsChecked = tema == TemaDeLaVentana.ElDeWindows;
        _temaClaro.IsChecked = tema == TemaDeLaVentana.Claro;
        _temaOscuro.IsChecked = tema == TemaDeLaVentana.Oscuro;
        _montandoElTema = false;

        // La barra se hace del programa ANTES del primer pintado, por lo mismo que el tema:
        // hacerlo despues ensena un instante la barra blanca de Windows y luego la cambia.
        MontarLaBarraDeTitulo();
        PintarConElTema(tema);
    }

    /// <summary>
    /// Convierte la fila «Fichas» del XAML en la barra de titulo de la ventana, en vez de
    /// una segunda fila debajo de la de Windows.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Las dos llamadas hacen falta y estan medidas.</b> Sin
    /// <c>ExtendsContentIntoTitleBar</c> Windows dibuja su propia barra encima —blanca aunque
    /// el programa este en oscuro, que es lo que el dueno fotografio el 2026-09-15— y la fila
    /// del XAML queda debajo como una segunda barra con el mismo titulo. Con ella y sin
    /// <c>SetTitleBar</c>, la fila no se puede arrastrar ni abre el menu de la ventana.</para>
    ///
    /// <para>Minimizar, maximizar y cerrar los sigue dibujando Windows encima del lado
    /// derecho de la fila; sus colores los pone <see cref="PintarLaBarraDeTitulo"/>.
    /// <c>Tall</c> es para que esos tres botones midan lo que mide la fila (48 px) y queden
    /// centrados en ella; con la altura normal (32 px) quedarian pegados arriba.</para>
    ///
    /// <para>Y se escucha <c>ActualThemeChanged</c> de la raiz ademas de pintar al elegir:
    /// con «el de Windows», el tema cambia cuando lo cambia Windows, sin que nadie toque el
    /// conmutador, y la barra tiene que seguirlo igual que el resto de la ventana.</para>
    /// </remarks>
    private void MontarLaBarraDeTitulo()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_barraDeTitulo);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _raiz.ActualThemeChanged += (_, _) => PintarLaBarraDeTitulo();
    }

    /// <summary>
    /// Pinta la barra de titulo y sus tres botones con el tema que la ventana tiene puesto.
    /// </summary>
    /// <remarks>
    /// <para>Lee <c>ActualTheme</c> y no la eleccion del dueno a proposito: con «el de
    /// Windows» la eleccion es <c>Default</c>, que no es un color, y lo que hay que pintar es
    /// lo que Windows haya resuelto —claro u oscuro— en ese momento.</para>
    ///
    /// <para>El fondo de los botones va sin color, en los dos estados, para que se vea la
    /// barra a traves; lo que cambia con el tema es la tinta y el fondo de pasar por encima
    /// y de pulsar. Los valores estan en <see cref="ColoresDeLaBarraDeTitulo"/>, que es lo
    /// que se prueba sin ventana.</para>
    ///
    /// <para>⚠️ <b>Si el dueno quiso decir «el color de acento de Windows» y no «el tema»,</b>
    /// el cambio es UNA linea, la del fondo: en vez de <c>paleta.Fondo</c>, poner
    /// <c>new Windows.UI.ViewManagement.UISettings().GetColorValue(UIColorType.Accent)</c>,
    /// y entonces la tinta de los tres botones tiene que pasar a blanco en los dos temas,
    /// porque el acento es un color saturado y la tinta oscura no se leeria encima. No se
    /// hizo: lo que el dueno fotografio fue una barra blanca sobre un programa oscuro, y lo
    /// que las ventanas de Windows hacen con eso es seguir el tema, no pintarse de acento.</para>
    /// </remarks>
    private void PintarLaBarraDeTitulo()
    {
        var paleta = ColoresDeLaBarraDeTitulo.Para(esOscuro: _raiz.ActualTheme == ElementTheme.Dark);
        _barraDeTitulo.Background = new SolidColorBrush(paleta.Fondo);

        var botones = AppWindow.TitleBar;
        botones.ButtonBackgroundColor = SinColor;
        botones.ButtonInactiveBackgroundColor = SinColor;
        botones.ButtonForegroundColor = paleta.Tinta;
        botones.ButtonHoverForegroundColor = paleta.Tinta;
        botones.ButtonPressedForegroundColor = paleta.Tinta;
        botones.ButtonInactiveForegroundColor = paleta.TintaInactiva;
        botones.ButtonHoverBackgroundColor = paleta.FondoDelBotonAlPasar;
        botones.ButtonPressedBackgroundColor = paleta.FondoDelBotonAlPulsar;
    }

    /// <summary>El dueno eligio: se pinta, se guarda y, si no se pudo guardar, se dice.</summary>
    private void AlElegirElTema(object quien, RoutedEventArgs cuando)
    {
        if (_montandoElTema || quien is not RadioButton circulo) return;

        var tema = TemaDeEsteCirculo(circulo);
        PintarConElTema(tema);

        // Que no se pueda escribir el archivo NO impide cambiar el tema en esta sesion: se
        // cambia igual y lo unico que se pierde es que lo recuerde la proxima vez. Callarlo
        // seria peor, porque el dueno lo volveria a elegir cada vez sin saber por que.
        if (_preferenciaDeTema?.Guardar(tema) == false)
        {
            _servicios.Avisos.Dejar(Aviso.Advierte(
                "El tema cambió, pero no se pudo guardar para la próxima vez.",
                string.Empty,
                $"No se pudo escribir «{_preferenciaDeTema.Ruta}». El programa volverá a abrir "
                + "con el tema anterior. Compruebe que esa carpeta existe y se puede escribir."));
        }
    }

    /// <summary>Pinta la ventana entera con el tema, marco y barra incluidos, y lo dice para el lector de pantalla.</summary>
    /// <remarks>
    /// <para>Se pone en <c>_raiz</c>, la raiz del contenido de la ventana. Es lo que hace que lo
    /// tomen tambien los desplegables y los cuadros, que no cuelgan de este arbol pero si del
    /// mismo <c>XamlRoot</c>.</para>
    ///
    /// <para><c>PreferredTheme</c> es lo que Windows dibuja FUERA del contenido: el borde de
    /// la ventana y el menu de la barra (Alt+Espacio). La barra en si y sus tres botones los
    /// pinta <see cref="PintarLaBarraDeTitulo"/>, que ademas se dispara sola cuando cambia
    /// el tema resuelto; se llama aqui tambien para no depender de que ese aviso llegue
    /// antes del primer pintado.</para>
    /// </remarks>
    private void PintarConElTema(TemaDeLaVentana tema)
    {
        _raiz.RequestedTheme = ComoLoPintaXaml(tema);
        AppWindow.TitleBar.PreferredTheme = TemasDeLaVentana.ComoLoPrefiereElMarco(tema);
        PintarLaBarraDeTitulo();
        AutomationProperties.SetName(
            _botonDelTema, $"Tema de la ventana: {TemasDeLaVentana.ComoSeLee(tema)}");
    }

    /// <summary>Traduce la eleccion del dueno al valor que entiende XAML.</summary>
    /// <remarks>
    /// <c>Default</c> es «el de Windows»: es el unico de los tres que sigue al sistema, y por
    /// eso el programa se comportaba asi antes de que esto existiera.
    /// </remarks>
    private static ElementTheme ComoLoPintaXaml(TemaDeLaVentana tema) => tema switch
    {
        TemaDeLaVentana.Claro => ElementTheme.Light,
        TemaDeLaVentana.Oscuro => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>Que tema es el circulo que se acaba de marcar, segun su etiqueta.</summary>
    private static TemaDeLaVentana TemaDeEsteCirculo(RadioButton circulo)
        => Enum.TryParse<TemaDeLaVentana>(circulo.Tag as string, out var tema)
            ? tema
            : TemaDeLaVentana.ElDeWindows;
}
