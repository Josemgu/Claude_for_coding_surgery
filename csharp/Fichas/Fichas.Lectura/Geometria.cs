using Fichas.Contratos.Lectura;

namespace Fichas.Lectura;

/// <summary>
/// Los dos sistemas de coordenadas, la escala del rasterizado y el traslape del 50%.
/// </summary>
/// <remarks>
/// Portado de `extraccion/geometria.py`. Un PDF mide en puntos con el origen ABAJO a la
/// izquierda; una imagen rasterizada mide en pixeles con el origen ARRIBA. Todo el
/// trabajo consiste en cruzar anotaciones del PDF con texto leido de la imagen, asi que
/// las dos cosas hablan el mismo idioma antes de compararse.
///
/// <para><b>Aqui se trabaja en FRACCIONES de pagina</b> y no en pixeles, porque es lo que
/// dice <see cref="BandaDeLaPagina"/>: un pixel depende de la escala con la que se
/// rasterizo ese dia, y una fraccion sigue valiendo a cualquier escala.</para>
/// </remarks>
public static class Geometria
{
    /// <summary>
    /// El tope del lado largo al rasterizar. Regla de no regresion: sin tope, un escaneo
    /// a 300 DPI tardaba entre 5 y 10 minutos por pagina.
    /// </summary>
    public const int LadoLargoMaximoPx = 3500;

    /// <summary>
    /// La escala que deja el lado largo justo EN el tope, nunca uno por encima.
    /// </summary>
    /// <remarks>
    /// El <c>BitDecrement</c> no es un adorno y no se puede simplificar a una division
    /// (`DECISIONES.md`, FASE 0): <c>3500/792</c> vale 3500,0000000000005 en coma
    /// flotante, el rasterizador redondea hacia arriba el tamano del mapa de bits, y la
    /// pagina sale a 3501 px. Bajar la escala al <c>double</c> inmediatamente anterior la
    /// deja en 3500. Es el equivalente exacto del <c>math.nextafter(..., 0.0)</c> del
    /// Python, y `PruebaDeLecturaDePdf` fija los dos numeros.
    /// </remarks>
    /// <param name="anchoPuntos">Ancho de la página en puntos PDF.</param>
    /// <param name="altoPuntos">Alto de la página en puntos PDF.</param>
    /// <param name="ladoLargoMaximoPx">El tope en píxeles del lado más largo; las pruebas lo cambian para medir el redondeo.</param>
    /// <returns>El factor por el que se multiplican los puntos para obtener píxeles.</returns>
    public static double EscalaDeRasterizado(double anchoPuntos, double altoPuntos, int ladoLargoMaximoPx = LadoLargoMaximoPx)
        => Math.BitDecrement(ladoLargoMaximoPx / Math.Max(anchoPuntos, altoPuntos));

    /// <summary>
    /// Pasa un <c>/Rect</c> de anotacion —en puntos, con la <c>y</c> creciendo hacia
    /// ARRIBA— a fracciones de la pagina, con la <c>y</c> creciendo hacia abajo.
    /// </summary>
    /// <remarks>
    /// Omitir la inversion de la <c>y</c> deja los rectangulos espejados respecto al
    /// centro de la pagina, y el sistema anula campos que estaban buenos. Por eso la
    /// conversion esta aislada aqui y tiene prueba propia.
    /// </remarks>
    /// <param name="izquierda">Primera coordenada <c>x</c> del <c>/Rect</c>; si viene al revés que <paramref name="derecha"/> se ordenan.</param>
    /// <param name="abajo">Primera coordenada <c>y</c> del <c>/Rect</c>, medida desde el pie de la página.</param>
    /// <param name="derecha">Segunda coordenada <c>x</c> del <c>/Rect</c>.</param>
    /// <param name="arriba">Segunda coordenada <c>y</c> del <c>/Rect</c>.</param>
    /// <param name="anchoPuntos">Ancho de la página en puntos.</param>
    /// <param name="altoPuntos">Alto de la página en puntos.</param>
    /// <returns>La caja en fracciones 0–1 con el origen arriba; con una página de tamaño cero o negativo, la caja nula en vez de dividir por cero.</returns>
    public static BandaDeLaPagina RectanguloPdfAFracciones(
        double izquierda, double abajo, double derecha, double arriba, double anchoPuntos, double altoPuntos)
    {
        if (anchoPuntos <= 0.0 || altoPuntos <= 0.0) return new BandaDeLaPagina(0.0, 0.0, 0.0, 0.0);

        return new BandaDeLaPagina(
            X0: Math.Min(izquierda, derecha) / anchoPuntos,
            Y0: (altoPuntos - Math.Max(abajo, arriba)) / altoPuntos,
            X1: Math.Max(izquierda, derecha) / anchoPuntos,
            Y1: (altoPuntos - Math.Min(abajo, arriba)) / altoPuntos);
    }

    /// <summary>La caja que envuelve una lista de puntos en pixeles, ya en fracciones.</summary>
    /// <param name="puntos">Las esquinas del polígono que devuelve el detector del OCR, en píxeles de la imagen.</param>
    /// <param name="anchoPx">Ancho de la imagen rasterizada.</param>
    /// <param name="altoPx">Alto de la imagen rasterizada.</param>
    /// <returns>El rectángulo mínimo que los contiene, en fracciones; la caja nula si no hay puntos o la imagen no tiene tamaño.</returns>
    public static BandaDeLaPagina BandaDesdePuntos(IEnumerable<(double X, double Y)> puntos, int anchoPx, int altoPx)
    {
        if (anchoPx <= 0 || altoPx <= 0) return new BandaDeLaPagina(0.0, 0.0, 0.0, 0.0);

        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var (x, y) in puntos)
        {
            if (x < x0) x0 = x;
            if (y < y0) y0 = y;
            if (x > x1) x1 = x;
            if (y > y1) y1 = y;
        }
        if (x0 > x1) return new BandaDeLaPagina(0.0, 0.0, 0.0, 0.0);

        return new BandaDeLaPagina(x0 / anchoPx, y0 / altoPx, x1 / anchoPx, y1 / altoPx);
    }

    /// <summary>Lo que los dos rectangulos comparten de alto. Nunca negativo.</summary>
    /// <param name="rectangulo">Una caja en fracciones de página.</param>
    /// <param name="banda">La otra; el orden no cambia el resultado.</param>
    /// <returns>El alto común en fracciones de página, o 0,0 si no se tocan en vertical.</returns>
    public static double TraslapeVertical(BandaDeLaPagina rectangulo, BandaDeLaPagina banda)
        => Math.Max(0.0, Math.Min(rectangulo.Y1, banda.Y1) - Math.Max(rectangulo.Y0, banda.Y0));

    /// <summary>
    /// Que parte del ALTO DE LA BANDA cubre el rectangulo, entre 0,0 y 1,0.
    /// </summary>
    /// <remarks>
    /// El denominador es la altura de la BANDA y no la del rectangulo: asi lo fija
    /// `DECISIONES.md`. Una banda de alto cero devuelve 0,0 en vez de reventar.
    /// </remarks>
    /// <param name="rectangulo">La caja de una línea del OCR o de una anotación.</param>
    /// <param name="banda">La fila de valor cuyo alto hace de denominador.</param>
    public static double FraccionDeTraslapeVertical(BandaDeLaPagina rectangulo, BandaDeLaPagina banda)
    {
        double altoDeLaBanda = banda.Y1 - banda.Y0;
        return altoDeLaBanda <= 0.0 ? 0.0 : TraslapeVertical(rectangulo, banda) / altoDeLaBanda;
    }

    /// <summary>
    /// Que parte del ALTO DEL RECTANGULO cae dentro de la banda, entre 0,0 y 1,0.
    /// </summary>
    /// <remarks>
    /// Es <see cref="FraccionDeTraslapeVertical"/> con el denominador al reves, y existe
    /// porque las dos preguntas son distintas y las dos hacen falta:
    /// <list type="bullet">
    ///   <item>«¿cubre este rectangulo la banda?» decide si un TACHON la anula, y por eso
    ///   mide contra el alto de la banda: un trazo fino tiene que poder anular una fila.</item>
    ///   <item>«¿cabe esta linea en la banda?» decide si un texto es de esa fila del papel,
    ///   y tiene que medir contra el alto de la LINEA: una caja del OCR que se tragó tres
    ///   renglones cubre el 100% de cualquier banda que cruce, y sin esta pregunta su texto
    ///   se le da al campo de cada una.</item>
    /// </list>
    /// <para>Medido el 2026-09-07 sobre las 20 hojas reales, en las bandas de los tres
    /// campos del caso que se extraen: las lineas que de verdad son de esa fila dan de
    /// <b>0,588 a 1,000</b>; las que son de otra fila dan de <b>0,354 a 0,481</b>.</para>
    /// <para>Un rectangulo de alto cero devuelve 0,0 en vez de reventar.</para>
    /// </remarks>
    /// <param name="rectangulo">La caja de una línea del OCR, cuyo alto hace de denominador.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    public static double FraccionDelRectanguloDentroDeLaBanda(BandaDeLaPagina rectangulo, BandaDeLaPagina banda)
    {
        double altoDelRectangulo = rectangulo.Y1 - rectangulo.Y0;
        return altoDelRectangulo <= 0.0 ? 0.0 : TraslapeVertical(rectangulo, banda) / altoDelRectangulo;
    }

    /// <summary>
    /// Cierto cuando los dos comparten al menos algo de ancho.
    /// </summary>
    /// <remarks>
    /// Sirve para no dejar que un tachon de la columna derecha anule un campo de la
    /// columna izquierda: el formulario tiene dos columnas y una banda no ocupa la
    /// pagina entera.
    /// </remarks>
    /// <param name="rectangulo">Una caja en fracciones de página.</param>
    /// <param name="banda">La otra; tocarse solo en el borde no cuenta como solapar.</param>
    public static bool SeSolapanEnHorizontal(BandaDeLaPagina rectangulo, BandaDeLaPagina banda)
        => Math.Min(rectangulo.X1, banda.X1) > Math.Max(rectangulo.X0, banda.X0);
}
