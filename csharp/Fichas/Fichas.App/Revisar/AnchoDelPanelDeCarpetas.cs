namespace Fichas.App.Revisar;

/// <summary>
/// La regla del ancho del panel de carpetas de Revisar: cuánto mide, entre qué suelo y qué
/// techo se mueve, y qué pasa al arrastrar y al hacer doble clic en el tirador.
/// </summary>
/// <remarks>
/// <para>Lo pidió el dueño el 2026-09-14: <i>«Actualizar el menú de los grupos: que pueda
/// ser ajustado con el mouse, agrandar o reducir, en la ventana de Revisar»</i>. Hasta ese
/// día la columna llevaba <c>Width="290"</c> fijo en el XAML, y a 290 los nombres de nivel
/// dos y tres ya se cortaban —medido por UIA sobre master ese mismo día: «7000011 · Puerto
/// Plata · 1 docume», con el texto acabando en x=711 y el panel en 715—.</para>
///
/// <para>Sin ventana a propósito: el suelo, el techo y el recorte se leen desde una prueba,
/// y la página solo llama aquí y pone el número en la columna. Los tres números de abajo
/// están medidos, no supuestos, y quien los cambie tiene que volver a medir.</para>
/// </remarks>
public static class AnchoDelPanelDeCarpetas
{
    /// <summary>Lo de siempre: los 290 que el XAML llevaba fijos hasta el 2026-09-14. Es a lo que vuelve el doble clic.</summary>
    public const double ElDeSiempre = 290;

    /// <summary>
    /// El ancho por debajo del cual el panel deja de servir.
    /// </summary>
    /// <remarks>
    /// Sale de lo que hay dentro, medido por UIA el 2026-09-14: la casilla «Ver los
    /// archivados» ocupa 132 y la fila «Carpetas … Ver todo» unos 140; el botón «Volcar el
    /// mes a carpetas…» va estirado y su texto a 12 px ronda los 170. Con 200 los tres se
    /// leen enteros y todavía se puede reducir bastante desde 290.
    /// </remarks>
    public const double Minimo = 200;

    /// <summary>
    /// Lo que mide una tarjeta como mínimo: el <c>MinItemWidth="330"</c> de la rejilla.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es una copia del XAML, no un enlace: si alguien cambia el <c>MinItemWidth</c> de
    /// <c>PaginaDeRevisar.xaml</c> tiene que cambiar esto, o el techo dejará una tarjeta
    /// cortada a la derecha.
    /// </remarks>
    public const double AnchoDeUnaTarjeta = 330;

    /// <summary>Lo que ocupa el tirador entre el panel y las tarjetas: su columna del XAML, 16 px.</summary>
    public const double LoQueOcupaElTirador = 16;

    /// <summary>
    /// El techo para un marco dado: lo que queda tras reservar el tirador y una tarjeta entera.
    /// </summary>
    /// <remarks>
    /// El techo nunca baja del suelo: con una ventana tan estrecha que ni el mínimo cabe,
    /// manda el mínimo y es la rejilla la que se aprieta, porque un panel de 40 px no le
    /// sirve a nadie. Y con un marco que todavía no se ha medido —0 o NaN antes del primer
    /// pintado— no hay techo: se aplica cuando llegue <c>SizeChanged</c>.
    /// </remarks>
    /// <param name="anchoDelMarco">Lo que mide la fila entera de panel más tarjetas, o 0/NaN si aún no se pintó.</param>
    public static double MaximoPara(double anchoDelMarco)
    {
        if (!(anchoDelMarco > 0)) return double.PositiveInfinity;
        return Math.Max(Minimo, anchoDelMarco - LoQueOcupaElTirador - AnchoDeUnaTarjeta);
    }

    /// <summary>Deja el ancho pedido entre el suelo y el techo del marco.</summary>
    /// <param name="pedido">El ancho que se quiere, elegido o guardado.</param>
    /// <param name="anchoDelMarco">Lo que mide la fila entera de panel más tarjetas.</param>
    public static double Recortar(double pedido, double anchoDelMarco)
        => Math.Clamp(pedido, Minimo, MaximoPara(anchoDelMarco));

    /// <summary>El ancho tras arrastrar el tirador: lo que había al agarrarlo más lo que se movió el ratón, recortado.</summary>
    /// <param name="anchoAlEmpezar">Lo que medía el panel cuando se agarró el tirador.</param>
    /// <param name="desplazamiento">Cuánto se movió el ratón desde entonces; negativo hacia la izquierda.</param>
    /// <param name="anchoDelMarco">Lo que mide la fila entera de panel más tarjetas.</param>
    public static double TrasArrastrar(double anchoAlEmpezar, double desplazamiento, double anchoDelMarco)
        => Recortar(anchoAlEmpezar + desplazamiento, anchoDelMarco);

    /// <summary>El ancho tras el doble clic en el tirador: lo de siempre.</summary>
    public static double TrasElDobleClic() => ElDeSiempre;
}
