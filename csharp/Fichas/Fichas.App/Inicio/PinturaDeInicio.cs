using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Text;

namespace Fichas.App.Inicio;

/// <summary>
/// El color y el grosor de cada cosa de Inicio, del grupo y de lo incompleto, en un solo sitio
/// y con sus dos temas.
/// </summary>
/// <remarks>
/// <para>Las plantillas de XAML llaman a estas propiedades y funciones con <c>x:Bind</c>. Estan
/// aqui y no dentro de los registros de <c>ModelosDeInicio.cs</c> a proposito: un modelo que
/// devuelve un <see cref="Brush"/> ya no se puede probar sin abrir ventana, y esa es la regla
/// que sostiene las pruebas de estas pantallas (ADR-0003 §8.1).</para>
///
/// <para>Los valores claros salen uno a uno de <c>mockups/mockup-v2-inicio.html</c>, que es el
/// aspecto que aprobo el dueno; el nombre de cada variable de aquel archivo va en el
/// comentario.</para>
///
/// <para>⛔ <b>Por que la paleta esta en C# y no en un <c>ThemeDictionaries</c> del XAML.</b>
/// Estas mismas parejas de colores las necesitan DOS sitios: los atributos del XAML y las
/// funciones que deciden el color de una pastilla segun su estado. Un diccionario de tema del
/// XAML solo alcanza al primero, asi que habria que escribir la paleta dos veces, y dos
/// verdades se separan el dia que alguien toque una. Aqui se escribe una vez y la leen los
/// dos.</para>
///
/// <para>⚠️ <b>Y por eso hay que decir cuando cambia.</b> <see cref="Tema"/> lo pone cada
/// pantalla al llegar, leyendo su propio <c>ActualTheme</c>, y lo vuelve a poner cuando el
/// dueno cambia de tema; entonces la pantalla se vuelve a montar entera. Sin eso, cambiar el
/// tema dejaria los colores anteriores hasta el siguiente pintado.</para>
/// </remarks>
public static class PinturaDeInicio
{
    /// <summary>Con que tema se pinta ahora mismo; lo pone la pantalla al llegar.</summary>
    /// <remarks>
    /// <para>Es un valor compartido por las tres pantallas a proposito: las tres viven en la
    /// misma ventana y no puede haber dos temas a la vez. <c>Default</c> —«el de Windows»—
    /// nunca llega aqui: la pantalla resuelve su <c>ActualTheme</c>, que ya es claro u
    /// oscuro.</para>
    ///
    /// <para>⚠️ <b>Ponerla no basta: hay que llamar despues a <c>Bindings.Update()</c></b>,
    /// y las tres pantallas lo hacen en su <c>PonerLaPaletaDelTema</c>. El marco de la
    /// pantalla —cabecera, bordes, fondo— se resuelve en <c>InitializeComponent()</c>, que
    /// corre en el constructor, cuando la pagina todavia no esta en el arbol y no se sabe su
    /// tema.</para>
    ///
    /// <para>⛔ <b>Y volver a navegar a la misma pantalla NO sirve, medido el 2026-09-05
    /// sobre el paquete publicado:</b> <c>Frame.Navigate</c> con el mismo tipo y el mismo
    /// parametro no hace nada, y la pagina se quedaba montada a medias —la cabecera puesta y
    /// 31 nombres en el arbol de accesibilidad, en vez de los 584 de una pantalla llena—.</para>
    /// </remarks>
    public static ElementTheme Tema { get; set; } = ElementTheme.Light;

    /// <summary>Si se esta pintando con el tema oscuro.</summary>
    public static bool EsOscuro => Tema == ElementTheme.Dark;


    // ---- las superficies -----------------------------------------------------

    /// <summary>--papel: el fondo de una tarjeta y del dia del mes que se ensena.</summary>
    public static Brush Papel => Elegir(ClaroPapel, OscuroPapel);

    /// <summary>--panel: el fondo de la pantalla, del dia de fuera del mes y de la pastilla archivada.</summary>
    public static Brush Panel => Elegir(ClaroPanel, OscuroPanel);

    /// <summary>--panel-hondo: la cabecera de una unidad o de una fecha dentro de una lista.</summary>
    public static Brush PanelHondo => Elegir(ClaroPanelHondo, OscuroPanelHondo);

    /// <summary>--linea: el borde normal de una tarjeta o de una celda del calendario.</summary>
    public static Brush Linea => Elegir(ClaroLinea, OscuroLinea);

    // ---- la tinta ------------------------------------------------------------

    /// <summary>--tinta: el texto normal.</summary>
    public static Brush Tinta => Elegir(ClaroTinta, OscuroTinta);

    /// <summary>--tinta-suave: el texto secundario.</summary>
    public static Brush TintaSuave => Elegir(ClaroTintaSuave, OscuroTintaSuave);

    /// <summary>--apagado: el dia que no es de este mes y las notas al pie.</summary>
    public static Brush Apagado => Elegir(ClaroApagado, OscuroApagado);

    /// <summary>--gris-marca: el archivado, que se ve pero no llama.</summary>
    public static Brush GrisMarca => Elegir(ClaroGrisMarca, OscuroGrisMarca);

    // ---- los acentos sobre la superficie -------------------------------------

    /// <summary>La recomendacion completa, escrita sobre el papel.</summary>
    public static Brush VerdeMarca => Elegir(ClaroVerdeMarca, OscuroVerdeMarca);

    /// <summary>Lo que esta en manos de un companero, escrito sobre el papel.</summary>
    public static Brush AmbarMarca => Elegir(ClaroAmbarMarca, OscuroAmbarMarca);

    /// <summary>Lo que ya viajo y sigue sin resolver, escrito sobre el papel.</summary>
    public static Brush RojoMarca => Elegir(ClaroRojoMarca, OscuroRojoMarca);

    /// <summary>El azul de lo que acaba de pasar; solo se usa para el acuse de asignar.</summary>
    public static Brush AzulMarca => Elegir(ClaroAzulMarca, OscuroAzulMarca);

    /// <summary>El fondo de la pastilla de un grupo completo.</summary>
    public static Brush VerdeFondo => Elegir(ClaroVerdeFondo, OscuroVerdeFondo);

    /// <summary>El fondo de la pastilla de un grupo al que le falta alguna.</summary>
    public static Brush RojoFondo => Elegir(ClaroRojoFondo, OscuroRojoFondo);

    // ---- las cabeceras de color, que NO cambian con el tema -------------------

    /// <summary>
    /// El verde de la cabecera «Listo para asignar».
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Los tres colores de cabecera son los MISMOS en los dos temas, y es a proposito.</b>
    /// Llevan texto blanco encima, y un color que se aclarara con el tema oscuro dejaria ese
    /// blanco ilegible: el contraste de un texto blanco sobre #4BC07A es de 2,0:1, por debajo
    /// del 4,5:1 que hace falta. Un color que solo funciona en un tema no es un color.
    /// </remarks>
    public static Brush CabeceraVerde { get; } = Pintar(0x1B, 0x6E, 0x3C);

    /// <summary>El borde inferior de la cabecera verde.</summary>
    public static Brush CabeceraVerdeOscura { get; } = Pintar(0x14, 0x52, 0x2C);

    /// <summary>El ambar de la cabecera «Asignado a los agentes».</summary>
    public static Brush CabeceraAmbar { get; } = Pintar(0x8A, 0x5B, 0x00);

    /// <summary>El borde inferior de la cabecera ambar.</summary>
    public static Brush CabeceraAmbarOscura { get; } = Pintar(0x6B, 0x47, 0x00);

    /// <summary>El rojo de la cabecera «Sin completar».</summary>
    public static Brush CabeceraRoja { get; } = Pintar(0xB3, 0x26, 0x1E);

    /// <summary>El borde inferior de la cabecera roja.</summary>
    public static Brush CabeceraRojaOscura { get; } = Pintar(0x8E, 0x1B, 0x15);

    /// <summary>El texto de una cabecera de color: blanco en los dos temas.</summary>
    public static Brush TintaDeCabecera { get; } = Pintar(0xFF, 0xFF, 0xFF);

    // ---- lo que decide un color segun el estado ------------------------------

    /// <summary>La fecha del renglon: en rojo cuando el caso ya viajo y sigue abierto.</summary>
    public static Brush TintaDeLaFecha(bool esVencido) => esVencido ? RojoMarca : TintaSuave;

    /// <summary>Y en negrita, para que el vencido se distinga sin depender solo del color.</summary>
    public static FontWeight GrosorDeLaFecha(bool esVencido)
        => esVencido ? FontWeights.Bold : FontWeights.Normal;

    /// <summary>El fondo de una celda: papel, o el sombreado de los 7 dias de la ventana.</summary>
    public static Brush FondoDelDia(bool esDelMes, bool enLaVentana)
    {
        if (enLaVentana) return Elegir(ClaroFondoDeLaVentana, OscuroFondoDeLaVentana);
        return esDelMes ? Papel : Panel;
    }

    /// <summary>El borde de una celda; el de hoy es la tinta, para que se vea de lejos.</summary>
    public static Brush BordeDelDia(bool esHoy) => esHoy ? Tinta : Linea;

    /// <summary>Hoy lleva 2 px de borde; el resto, 1 px.</summary>
    public static Thickness GrosorDelDia(bool esHoy) => esHoy ? BordeDeHoy : BordeNormal;

    /// <summary>El numero del dia: apagado si la celda es relleno de otro mes.</summary>
    public static Brush TintaDelDia(bool esDelMes) => esDelMes ? TintaSuave : Apagado;

    /// <summary>
    /// El fondo de la pastilla de un GRUPO en el calendario.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Ya no hay un gris de «grupo archivado»</b> (2026-09-06): un grupo entero
    /// archivado no pinta pastilla ninguna. Hasta ese dia iba en gris —se veia, sin competir
    /// con lo abierto— porque el dueno pidio verlo el 2026-09-03, y lo deshizo.</para>
    ///
    /// <para>El verde exige que esten confirmadas TODAS las personas del grupo, no la
    /// mayoria: si bastara con la mayoria, un grupo de siete con uno sin resolver saldria
    /// verde y ese uno es exactamente el que no puede entrar al templo.</para>
    ///
    /// <para>⚠️ <b>Desde el 2026-09-05 el color va por PERSONAS y no por documentos</b>
    /// (criterio C20-3), igual que la etiqueta que lleva debajo. Antes el color decia una
    /// cosa —documentos completos— y ahora la etiqueta dice otra —personas confirmadas—; un
    /// color que no acompaña a su propia cifra es otra vez el programa diciendo dos cosas.
    /// </para>
    /// </remarks>
    /// <param name="cuantasConfirmadas">Cuantas de sus personas tienen la recomendacion confirmada.</param>
    /// <param name="cuantasPersonas">Cuantas personas trae el grupo.</param>
    public static Brush FondoDeLaPastilla(int cuantasConfirmadas, int cuantasPersonas)
        => EstaTodoCompleto(cuantasConfirmadas, cuantasPersonas) ? VerdeFondo : RojoFondo;

    /// <summary>La raya de la izquierda de la pastilla, que es lo que da el color.</summary>
    /// <param name="cuantasConfirmadas">Cuantas de sus personas tienen la recomendacion confirmada.</param>
    /// <param name="cuantasPersonas">Cuantas personas trae el grupo.</param>
    public static Brush BordeDeLaPastilla(int cuantasConfirmadas, int cuantasPersonas)
        => EstaTodoCompleto(cuantasConfirmadas, cuantasPersonas) ? VerdeMarca : RojoMarca;

    /// <summary>
    /// El detalle de un renglon; en rojo cuando el PDF no esta donde el documento dice.
    /// </summary>
    /// <remarks>
    /// Criterio C13-6: un documento sin su PDF se ve y lo dice; no rompe la pantalla ni
    /// desaparece del grupo. El color acompana a la frase, no la sustituye: la frase
    /// («el PDF no está en su ruta») es lo que se lee, y por eso se puede probar sin ventana.
    /// </remarks>
    public static Brush TintaDelDetalle(bool hayQueMirarlo) => hayQueMirarlo ? RojoMarca : TintaSuave;

    /// <summary>
    /// Traduce un si/no a que se vea o no. Existe porque <c>x:Bind</c> no convierte solo
    /// de <c>bool</c> a <see cref="Visibility"/> de forma garantizada entre versiones, y
    /// una conversion implicita que un dia deje de funcionar esconderia media pantalla sin
    /// que nadie lo note.
    /// </summary>
    public static Visibility SeVe(bool si) => si ? Visibility.Visible : Visibility.Collapsed;

    // ---- las dos paletas -----------------------------------------------------

    private static readonly SolidColorBrush ClaroPapel = Pintar(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush ClaroPanel = Pintar(0xF4, 0xF5, 0xF7);
    private static readonly SolidColorBrush ClaroPanelHondo = Pintar(0xE9, 0xEB, 0xEF);
    private static readonly SolidColorBrush ClaroLinea = Pintar(0xD3, 0xD7, 0xDE);
    private static readonly SolidColorBrush ClaroTinta = Pintar(0x1A, 0x1D, 0x21);
    private static readonly SolidColorBrush ClaroTintaSuave = Pintar(0x46, 0x50, 0x5C);
    private static readonly SolidColorBrush ClaroApagado = Pintar(0x64, 0x6C, 0x77);
    private static readonly SolidColorBrush ClaroGrisMarca = Pintar(0x5B, 0x64, 0x70);
    private static readonly SolidColorBrush ClaroVerdeMarca = Pintar(0x1B, 0x6E, 0x3C);
    private static readonly SolidColorBrush ClaroAmbarMarca = Pintar(0x8A, 0x5B, 0x00);
    private static readonly SolidColorBrush ClaroRojoMarca = Pintar(0xB3, 0x26, 0x1E);
    private static readonly SolidColorBrush ClaroAzulMarca = Pintar(0x1B, 0x4F, 0x8A);
    private static readonly SolidColorBrush ClaroVerdeFondo = Pintar(0xE7, 0xF5, 0xEC);
    private static readonly SolidColorBrush ClaroRojoFondo = Pintar(0xFD, 0xE7, 0xE7);
    private static readonly SolidColorBrush ClaroFondoDeLaVentana = Pintar(0xFF, 0xF7, 0xF7);

    private static readonly SolidColorBrush OscuroPapel = Pintar(0x1F, 0x23, 0x28);
    private static readonly SolidColorBrush OscuroPanel = Pintar(0x14, 0x17, 0x1A);
    private static readonly SolidColorBrush OscuroPanelHondo = Pintar(0x2A, 0x30, 0x37);
    private static readonly SolidColorBrush OscuroLinea = Pintar(0x3C, 0x44, 0x4D);
    private static readonly SolidColorBrush OscuroTinta = Pintar(0xF2, 0xF4, 0xF7);
    private static readonly SolidColorBrush OscuroTintaSuave = Pintar(0xC3, 0xCB, 0xD4);
    private static readonly SolidColorBrush OscuroApagado = Pintar(0x9C, 0xA6, 0xB1);
    private static readonly SolidColorBrush OscuroGrisMarca = Pintar(0xAD, 0xB6, 0xC0);
    private static readonly SolidColorBrush OscuroVerdeMarca = Pintar(0x63, 0xD2, 0x93);
    private static readonly SolidColorBrush OscuroAmbarMarca = Pintar(0xEB, 0xB5, 0x4D);
    private static readonly SolidColorBrush OscuroRojoMarca = Pintar(0xF5, 0x86, 0x7C);
    private static readonly SolidColorBrush OscuroAzulMarca = Pintar(0x8C, 0xBC, 0xEE);
    private static readonly SolidColorBrush OscuroVerdeFondo = Pintar(0x14, 0x33, 0x1F);
    private static readonly SolidColorBrush OscuroRojoFondo = Pintar(0x3B, 0x1C, 0x1A);
    private static readonly SolidColorBrush OscuroFondoDeLaVentana = Pintar(0x2E, 0x1F, 0x20);

    /// <summary>Borde de 1 px, el de cualquier dia.</summary>
    private static readonly Thickness BordeNormal = new(1);

    /// <summary>Borde de 2 px, el que marca el dia de hoy.</summary>
    private static readonly Thickness BordeDeHoy = new(2);

    /// <summary>Un grupo esta completo cuando lo estan todos sus documentos y hay alguno.</summary>
    private static bool EstaTodoCompleto(int cuantasCompletas, int cuantosDocumentos)
        => cuantosDocumentos > 0 && cuantasCompletas == cuantosDocumentos;

    /// <summary>El de la paleta que toca segun el tema con el que se esta pintando.</summary>
    private static Brush Elegir(Brush claro, Brush oscuro) => EsOscuro ? oscuro : claro;

    /// <summary>Compone un pincel a partir de sus tres componentes, opaco.</summary>
    private static SolidColorBrush Pintar(byte rojo, byte verde, byte azul)
        => new(Color.FromArgb(0xFF, rojo, verde, azul));
}
