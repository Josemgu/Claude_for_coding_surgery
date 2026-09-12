using Fichas.Contratos.Lectura;

namespace Fichas.Lectura;

/// <summary>Que clase de trazo a mano es una anotacion `/Ink`.</summary>
public enum ClaseDeTrazo
{
    /// <summary>No se pudo clasificar. No se interpreta: se registra y ya.</summary>
    Desconocida = 0,

    /// <summary>Un tachon rojo fino. ANULA lo que hubiera debajo.</summary>
    Tachon = 1,

    /// <summary>Un resaltador verde grueso. No anula nada.</summary>
    Resaltador = 2,
}

/// <summary>
/// La capa de anotaciones del PDF: texto que se lee sin OCR, y trazos a mano.
/// </summary>
/// <remarks>
/// Portado de `extraccion/anotaciones.py`. Los formularios llegan escaneados —la hoja es
/// una imagen sin capa de texto: comprobado, 0 letras en los siete— pero encima llevan
/// anotaciones que SI son objetos reales del PDF. Ahi esta lo que el revisor corrigio, y
/// se lee exacto, sin pasar por el OCR y sin margen de error.
///
/// <para>Un `/Ink` que no encaje en ninguna de las dos familias conocidas se registra
/// como DESCONOCIDO y no se interpreta. No se adivina que quiso decir una marca: si el
/// sistema tratara un trazo raro como tachon, anularia un campo bueno; si lo tratara como
/// resaltador, dejaria pasar un dato tachado.</para>
/// </remarks>
public static class Anotaciones
{
    /// <summary>El subtipo de una correccion escrita. Lleva su texto en `/Contents`.</summary>
    public const string SubtipoDeTexto = "FreeText";

    /// <summary>El subtipo de un trazo a mano. Sin texto, pero con color y grosor.</summary>
    public const string SubtipoDeTrazo = "Ink";

    /// <summary>
    /// Un campo de texto del formulario rellenable, con lo que alguien tecleo dentro.
    /// </summary>
    /// <remarks>
    /// En el PDF es una anotacion <c>/Widget</c> cuyo campo es de tipo <c>/Tx</c>; el nombre
    /// junta las dos cosas para que no se confunda con una casilla, que tambien es un
    /// <c>/Widget</c>. Es la segunda clase de documento que recibe el dueño, medida el
    /// 2026-09-10: un formulario rellenado en el ordenador, donde el nombre, la cedula, las
    /// fechas y el templo no estan en la imagen ni en una <c>/FreeText</c>, sino tecleados en
    /// estos campos. Su texto es exacto —no pasa por el OCR— y se trata como una correccion
    /// escrita: misma precedencia, mismo origen y misma confianza.
    /// </remarks>
    public const string SubtipoDeCampoDeTexto = "Widget/Tx";

    // Las dos familias, MEDIDAS con `pypdf` sobre los cuatro documentos de referencia el
    // 2026-09-02 (45 tachones y 1 resaltador; 0 sin clasificar) y vueltas a ver el
    // 2026-09-04 en los siete escaneos del dueno, donde los 30 /Ink son todos tachones
    // con exactamente estos valores.
    /// <summary>Canal rojo del tachón, en la escala 0–1 del diccionario <c>/C</c> del PDF (≈ 227/255).</summary>
    private const double RojoDelTachon = 0.8902;
    /// <summary>Canal verde del tachón (≈ 24/255): casi nulo, es lo que lo separa del resaltador.</summary>
    private const double VerdeDelTachon = 0.0941;
    /// <summary>Canal azul del tachón (≈ 45/255).</summary>
    private const double AzulDelTachon = 0.1765;

    /// <summary>Grosor medido del tachon rojo.</summary>
    public const double GrosorDelTachon = 1.65;

    /// <summary>Canal rojo del resaltador verde (≈ 126/255).</summary>
    private const double RojoDelResaltador = 0.4941;
    /// <summary>Canal verde del resaltador (≈ 196/255): el canal que manda en esta familia.</summary>
    private const double VerdeDelResaltador = 0.7686;
    /// <summary>Canal azul del resaltador: exactamente cero en el único ejemplar medido.</summary>
    private const double AzulDelResaltador = 0.0;

    /// <summary>Grosor medido del resaltador verde.</summary>
    public const double GrosorDelResaltador = 16.5;

    /// <summary>
    /// Cuanto puede alejarse un canal de color y seguir siendo la misma familia.
    /// </summary>
    /// <remarks>
    /// 0,02 sobre 1,0 son unos 5 valores de 255: mas que suficiente para el redondeo de
    /// un visor, y muy lejos de confundir el rojo con el verde, que se separan en 0,40 en
    /// el canal rojo y 0,67 en el verde.
    /// </remarks>
    public const double ToleranciaDeColor = 0.02;

    /// <summary>
    /// Cuanto puede alejarse el grosor, en relativo.
    /// </summary>
    /// <remarks>
    /// Se compara en relativo porque los dos valores conocidos se separan en un factor de
    /// 10 (1,65 frente a 16,5). Un 20% sobre 1,65 es 0,33 y sobre 16,5 es 3,3: ninguna de
    /// las dos ventanas alcanza a la otra.
    /// </remarks>
    public const double ToleranciaRelativaDeGrosor = 0.20;

    /// <summary>
    /// Cierto cuando los tres canales del trazo caen a menos de <see cref="ToleranciaDeColor"/>
    /// de los de una familia. Falso si falta cualquier canal: un <c>/Ink</c> sin <c>/C</c> no se
    /// clasifica, no se supone.
    /// </summary>
    /// <param name="rojo">Canal rojo leído del PDF, o <c>null</c> si la anotación no trae color.</param>
    /// <param name="verde">Canal verde leído del PDF, o <c>null</c>.</param>
    /// <param name="azul">Canal azul leído del PDF, o <c>null</c>.</param>
    /// <param name="r">Canal rojo de referencia de la familia que se compara.</param>
    /// <param name="v">Canal verde de referencia.</param>
    /// <param name="a">Canal azul de referencia.</param>
    private static bool ColorSeParece(double? rojo, double? verde, double? azul, double r, double v, double a)
        => rojo is not null && verde is not null && azul is not null
           && Math.Abs(rojo.Value - r) <= ToleranciaDeColor
           && Math.Abs(verde.Value - v) <= ToleranciaDeColor
           && Math.Abs(azul.Value - a) <= ToleranciaDeColor;

    /// <summary>
    /// Cierto cuando el grosor cae dentro del ±20 % (<see cref="ToleranciaRelativaDeGrosor"/>)
    /// del de referencia. Falso si el trazo no trae <c>/BS /W</c>.
    /// </summary>
    /// <param name="grosor">Grosor leído del PDF, o <c>null</c> si la anotación no lo trae.</param>
    /// <param name="referencia">Grosor medido de la familia que se compara.</param>
    private static bool GrosorSeParece(double? grosor, double referencia)
        => grosor is not null && Math.Abs(grosor.Value - referencia) <= referencia * ToleranciaRelativaDeGrosor;

    /// <summary>
    /// Devuelve tachon, resaltador o desconocida. Nunca adivina.
    /// </summary>
    /// <remarks>
    /// Exige que coincidan las DOS cosas, color y grosor. Con solo el color, un trazo
    /// rojo grueso hecho para resaltar se leeria como tachon y anularia un campo bueno.
    /// </remarks>
    /// <param name="rojo">Canal rojo del trazo (0–1), o <c>null</c> si no trae color.</param>
    /// <param name="verde">Canal verde del trazo (0–1), o <c>null</c>.</param>
    /// <param name="azul">Canal azul del trazo (0–1), o <c>null</c>.</param>
    /// <param name="grosor">Grosor del trazo en puntos, o <c>null</c> si no lo trae.</param>
    /// <returns>La familia reconocida, o <see cref="ClaseDeTrazo.Desconocida"/> si no encaja en ninguna o falta un dato.</returns>
    public static ClaseDeTrazo ClasificarTrazo(double? rojo, double? verde, double? azul, double? grosor)
    {
        if (ColorSeParece(rojo, verde, azul, RojoDelTachon, VerdeDelTachon, AzulDelTachon)
            && GrosorSeParece(grosor, GrosorDelTachon))
        {
            return ClaseDeTrazo.Tachon;
        }
        if (ColorSeParece(rojo, verde, azul, RojoDelResaltador, VerdeDelResaltador, AzulDelResaltador)
            && GrosorSeParece(grosor, GrosorDelResaltador))
        {
            return ClaseDeTrazo.Resaltador;
        }
        return ClaseDeTrazo.Desconocida;
    }

    /// <summary>Que clase de trazo es esta anotacion; desconocida si no es un `/Ink`.</summary>
    /// <param name="anotacion">La anotación tal como la leyó <see cref="LecturaDePdf"/>.</param>
    public static ClaseDeTrazo ClaseDe(AnotacionDelPdf anotacion)
        => anotacion.Subtipo != SubtipoDeTrazo
            ? ClaseDeTrazo.Desconocida
            : ClasificarTrazo(anotacion.Rojo, anotacion.Verde, anotacion.Azul, anotacion.Grosor);

    /// <summary>Cierto cuando la anotacion es una nota escrita a mano encima del papel, con texto.</summary>
    /// <param name="anotacion">La anotación a examinar; una <c>/FreeText</c> vacía cuenta como no-corrección.</param>
    public static bool EsCorreccionAMano(AnotacionDelPdf anotacion)
        => anotacion.Subtipo == SubtipoDeTexto && !string.IsNullOrWhiteSpace(anotacion.Texto);

    /// <summary>
    /// Cierto cuando la anotacion trae texto exacto que corrige o rellena un campo.
    /// </summary>
    /// <remarks>
    /// Son dos cosas y las dos cuentan: una nota escrita a mano (<c>/FreeText</c>) y un campo
    /// tecleado del formulario. Las dos son texto del propio PDF, sin OCR de por medio, y por
    /// eso entran por la misma puerta de la precedencia. Cuando coinciden en una banda, la
    /// nota a mano gana: <see cref="Campos.ResolverCampo"/> lleva escrito por que.
    /// </remarks>
    /// <param name="anotacion">La anotación a examinar.</param>
    public static bool EsCorreccionEscrita(AnotacionDelPdf anotacion)
        => EsCorreccionAMano(anotacion) || EsCampoTecleado(anotacion);

    /// <summary>Cierto cuando la anotacion es un campo del formulario con algo tecleado dentro.</summary>
    /// <param name="anotacion">La anotación a examinar; un campo <c>/Tx</c> en blanco cuenta como no tecleado.</param>
    public static bool EsCampoTecleado(AnotacionDelPdf anotacion)
        => anotacion.Subtipo == SubtipoDeCampoDeTexto && !string.IsNullOrWhiteSpace(anotacion.Texto);

    /// <summary>Cierto cuando la anotacion es un tachon que anula lo que hay debajo.</summary>
    /// <param name="anotacion">La anotación a examinar; solo un <c>/Ink</c> con el color y el grosor medidos da cierto.</param>
    public static bool EsTachon(AnotacionDelPdf anotacion) => ClaseDe(anotacion) == ClaseDeTrazo.Tachon;
}
