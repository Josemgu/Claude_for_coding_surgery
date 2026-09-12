using Fichas.Contratos.Lectura;

namespace Fichas.App.Correccion;

/// <summary>Un rectangulo sobre la hoja, en pixeles de la hoja sin escalar.</summary>
/// <param name="X">Borde izquierdo.</param>
/// <param name="Y">Borde superior.</param>
/// <param name="Ancho">Lo ancho que es.</param>
/// <param name="Alto">Lo alto que es.</param>
public readonly record struct RectanguloEnPixeles(double X, double Y, double Ancho, double Alto)
{
    /// <summary>Donde acaba por la derecha.</summary>
    public double Derecha => X + Ancho;

    /// <summary>Donde acaba por abajo.</summary>
    public double Abajo => Y + Alto;
}

/// <summary>
/// La aritmetica del visor: donde cae la banda de un campo y cuanto hay que moverse.
/// </summary>
/// <remarks>
/// Vive aparte del control, y sin una sola linea de XAML, por lo mismo que
/// <c>interfaz/encuadre.py</c>: se puede probar en milisegundos y da el mismo numero en
/// cualquier maquina; las pruebas de la ventana tardan segundos y se omiten donde no hay
/// pantalla.
/// <para>
/// ⚠️ <b>Lo que NO esta aqui, y es a proposito:</b> el zoom y el arrastre los hace el
/// <c>ScrollView</c> de WinUI, que es lo que pide el criterio C4-4
/// (<c>ZoomMode="Enabled"</c> y <c>ContentOrientation="None"</c>). Portar tambien la
/// maquina de encuadre de Python dejaria dos motores mandando sobre la misma vista, que
/// es la forma segura de que la pantalla y el numero digan cosas distintas.
/// </para>
/// </remarks>
public static class Encuadre
{
    /// <summary>Papel que se deja alrededor de la banda al traerla a la vista, en pixeles.</summary>
    public const double MargenDelVisor = 12;

    /// <summary>Los pasos del zoom manual. Fijos y no continuos.</summary>
    /// <remarks>Con pasos, dos personas que dicen «ponlo al 200» ven lo mismo.</remarks>
    public static readonly double[] PasosDeZoom = [0.50, 0.75, 1.00, 1.50, 2.00, 3.00];

    /// <summary>El rectangulo de una banda sobre una hoja de ese tamano.</summary>
    /// <remarks>
    /// La banda viene en fracciones de la hoja (0 a 1) porque un pixel depende de la escala a
    /// la que se rasterizo; aqui se multiplica por el tamano real de la hoja que se esta viendo.
    /// </remarks>
    /// <param name="banda">Donde estaba el campo, en fracciones de la hoja.</param>
    /// <param name="ancho">Ancho de la hoja rasterizada, en pixeles.</param>
    /// <param name="alto">Alto de la hoja rasterizada, en pixeles.</param>
    public static RectanguloEnPixeles RectanguloDeLaBanda(BandaDeLaPagina banda, double ancho, double alto)
        => new(banda.X0 * ancho, banda.Y0 * alto, banda.Ancho * ancho, banda.Alto * alto);

    /// <summary>
    /// El zoom que mete la hoja entera a lo ancho del panel; 1 mientras no se sepa el tamano.
    /// </summary>
    /// <remarks>
    /// Es el modo con el que nace el visor, y lo eligio el dueno: «¿Para que me pones el PDF
    /// al lado si no puedo moverme dentro de el? Esta muy pequeno».
    /// </remarks>
    /// <param name="anchoVisible">Ancho del panel, en pixeles de pantalla; 0 o menos devuelve 1.</param>
    /// <param name="anchoDeLaHoja">Ancho de la hoja rasterizada; 0 o menos devuelve 1.</param>
    public static double ZoomParaElAncho(double anchoVisible, double anchoDeLaHoja)
        => anchoVisible <= 0 || anchoDeLaHoja <= 0 ? 1.0 : anchoVisible / anchoDeLaHoja;

    /// <summary>El paso de zoom siguiente hacia arriba (1) o hacia abajo (-1).</summary>
    /// <remarks>En los topes devuelve el tope: otro paso no se sale de la escala.</remarks>
    /// <param name="zoomActual">La escala de ahora, que puede no ser ninguno de los pasos.</param>
    /// <param name="direccion">Positivo para acercar, cero o negativo para alejar.</param>
    public static double PasoDeZoom(double zoomActual, int direccion)
    {
        if (direccion > 0)
        {
            foreach (var paso in PasosDeZoom)
                if (paso > zoomActual + 0.0001) return paso;
            return PasosDeZoom[^1];
        }

        for (var i = PasosDeZoom.Length - 1; i >= 0; i--)
            if (PasosDeZoom[i] < zoomActual - 0.0001) return PasosDeZoom[i];
        return PasosDeZoom[0];
    }

    /// <summary>
    /// A donde hay que mover la vista para que se vea esa banda. Si ya se ve, no mueve nada.
    /// </summary>
    /// <remarks>
    /// Que no se mueva cuando ya se ve no es un ahorro: una vista que salta en cada
    /// tabulacion marea y hace perder de vista el campo anterior, que es contra lo que se
    /// compara. Y no se centra la banda: se trae lo justo, con su margen.
    /// </remarks>
    /// <param name="rectangulo">La banda en pixeles de la hoja sin escalar.</param>
    /// <param name="anchoVisible">Ancho del panel, en pixeles de pantalla.</param>
    /// <param name="altoVisible">Alto del panel, en pixeles de pantalla.</param>
    /// <param name="zoom">La escala a la que se esta viendo la hoja.</param>
    /// <param name="offsetX">Donde esta la vista ahora, en pixeles de pantalla.</param>
    /// <param name="offsetY">Lo mismo en vertical.</param>
    /// <param name="margen">Papel alrededor; por defecto <see cref="MargenDelVisor"/>.</param>
    public static (double X, double Y) OffsetParaVer(
        RectanguloEnPixeles rectangulo,
        double anchoVisible,
        double altoVisible,
        double zoom,
        double offsetX,
        double offsetY,
        double margen = MargenDelVisor)
    {
        var x = OffsetDeUnEje(rectangulo.X * zoom, rectangulo.Derecha * zoom, anchoVisible, offsetX, margen);
        var y = OffsetDeUnEje(rectangulo.Y * zoom, rectangulo.Abajo * zoom, altoVisible, offsetY, margen);
        return (x, y);
    }

    /// <summary>
    /// La vista despues de cambiar de zoom dejando quieto lo que hay bajo ese punto del panel.
    /// </summary>
    /// <remarks>
    /// Es lo que pidio el dueno: «que pueda hacer zoom a cualquier parte». Sin esto, ampliar
    /// se lleva la vista a otro sitio y hay que volver a buscar el dato que se estaba mirando.
    /// </remarks>
    /// <param name="puntoX">Donde esta el cursor dentro del panel.</param>
    /// <param name="puntoY">Lo mismo en vertical.</param>
    /// <param name="offsetX">Donde esta la vista ahora.</param>
    /// <param name="offsetY">Lo mismo en vertical.</param>
    /// <param name="zoomViejo">La escala de antes; 1 si todavia no se sabe.</param>
    /// <param name="zoomNuevo">La escala a la que se va.</param>
    public static (double X, double Y) OffsetAlAmpliarEn(
        double puntoX,
        double puntoY,
        double offsetX,
        double offsetY,
        double zoomViejo,
        double zoomNuevo)
    {
        if (zoomViejo <= 0) return (offsetX, offsetY);
        var enLaHojaX = (offsetX + puntoX) / zoomViejo;
        var enLaHojaY = (offsetY + puntoY) / zoomViejo;
        return (
            Math.Max(0, (enLaHojaX * zoomNuevo) - puntoX),
            Math.Max(0, (enLaHojaY * zoomNuevo) - puntoY));
    }

    /// <summary>Un solo eje del calculo de arriba; los dos se comportan igual.</summary>
    /// <param name="principio">Donde empieza la banda en ese eje, ya en pixeles de pantalla.</param>
    /// <param name="final">Donde acaba la banda en ese eje, ya en pixeles de pantalla.</param>
    /// <param name="visible">Cuanto panel hay en ese eje; 0 o menos deja la vista donde esta.</param>
    /// <param name="offset">Donde esta la vista ahora en ese eje.</param>
    /// <param name="margen">Papel que se deja alrededor al traer la banda.</param>
    /// <returns>El offset nuevo; el mismo si la banda ya cabia entera.</returns>
    private static double OffsetDeUnEje(double principio, double final, double visible, double offset, double margen)
    {
        if (visible <= 0) return offset;
        if (principio < offset) return Math.Max(0, principio - margen);
        if (final > offset + visible) return Math.Max(0, final + margen - visible);
        return offset;
    }
}
