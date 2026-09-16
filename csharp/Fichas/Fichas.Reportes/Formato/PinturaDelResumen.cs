using ClosedXML.Excel;

namespace Fichas.Reportes.Formato;

/// <summary>
/// Los colores y los trazos de la hoja unica del reporte, tal como estan en el mockup v3.
/// </summary>
/// <remarks>
/// Los valores son los del <c>:root</c> de <c>mockups/mockup-v3-reporte-excel.html</c>, con los
/// contrastes que el disenador midio (blanco sobre marino 15,72:1; verde tinta sobre verde
/// fondo 12,01:1; naranja tinta sobre naranja fondo 9,07:1; rojo tinta sobre rojo fondo
/// 13,02:1). El marino es el mismo de la fila de titulos de la hoja del companero.
/// </remarks>
internal static class PinturaDelResumen
{
    /// <summary>El azul marino de la cabecera y de las filas de rotulos.</summary>
    internal static readonly XLColor Marino = XLColor.FromHtml("#16233A");

    /// <summary>La letra sobre marino.</summary>
    internal static readonly XLColor Blanco = XLColor.FromHtml("#FFFFFF");

    /// <summary>La letra secundaria sobre marino: el periodo y el «generado el».</summary>
    internal static readonly XLColor GrisClaro = XLColor.FromHtml("#C9D3E6");

    /// <summary>La letra corriente.</summary>
    internal static readonly XLColor Tinta = XLColor.FromHtml("#1A1D21");

    /// <summary>La letra de los detalles de las tarjetas.</summary>
    internal static readonly XLColor TintaSuave = XLColor.FromHtml("#46505C");

    /// <summary>La letra de los rotulos pequenos y de la raya.</summary>
    internal static readonly XLColor Apagado = XLColor.FromHtml("#646C77");

    /// <summary>El fondo de la fila de totales.</summary>
    internal static readonly XLColor Panel = XLColor.FromHtml("#F4F5F7");

    /// <summary>El fondo del semaforo en verde.</summary>
    internal static readonly XLColor VerdeFondo = XLColor.FromHtml("#E7F5EC");

    /// <summary>La cifra en verde y la marca de «devolvió».</summary>
    internal static readonly XLColor VerdeMarca = XLColor.FromHtml("#1B6E3C");

    /// <summary>La letra sobre el verde del semaforo.</summary>
    internal static readonly XLColor VerdeTinta = XLColor.FromHtml("#12351F");

    /// <summary>El fondo del semaforo en naranja.</summary>
    internal static readonly XLColor NaranjaFondo = XLColor.FromHtml("#FFE0B3");

    /// <summary>La cifra en naranja.</summary>
    internal static readonly XLColor NaranjaMarca = XLColor.FromHtml("#9A4A00");

    /// <summary>La letra sobre el naranja del semaforo.</summary>
    internal static readonly XLColor NaranjaTinta = XLColor.FromHtml("#5A2E00");

    /// <summary>El fondo del semaforo en rojo.</summary>
    internal static readonly XLColor RojoFondo = XLColor.FromHtml("#FDE7E7");

    /// <summary>La cifra en rojo y la marca de «no devolvió».</summary>
    internal static readonly XLColor RojoMarca = XLColor.FromHtml("#B3261E");

    /// <summary>La letra sobre el rojo del semaforo.</summary>
    internal static readonly XLColor RojoTinta = XLColor.FromHtml("#4A0F0B");

    /// <summary>La letra de toda la hoja; Calibri es la de Excel y la del mockup.</summary>
    internal const string Letra = "Calibri";

    /// <summary>La letra de ✓ y ✗: Calibri no las trae y esta va en todo Windows.</summary>
    internal const string LetraDeLasMarcas = "Segoe UI Symbol";

    /// <summary>El formato de texto, que es lo que impide que Excel se coma un cero de delante.</summary>
    internal const string FormatoDeTexto = "@";

    /// <summary>Un rango entero de un color de fondo con letra encima, como una fila de rotulos.</summary>
    /// <param name="rango">El rango.</param>
    /// <param name="fondo">El color de relleno.</param>
    /// <param name="letra">El color de la letra.</param>
    internal static void Pintar(IXLRange rango, XLColor fondo, XLColor letra)
    {
        rango.Style.Fill.BackgroundColor = fondo;
        rango.Style.Font.FontColor = letra;
    }

    /// <summary>Una raya debajo de un rango, del grosor y color que se diga.</summary>
    /// <param name="rango">El rango.</param>
    /// <param name="grosor">El grosor del borde.</param>
    /// <param name="color">El color del borde.</param>
    internal static void RayaDebajo(IXLRange rango, XLBorderStyleValues grosor, XLColor color)
    {
        rango.Style.Border.BottomBorder = grosor;
        rango.Style.Border.BottomBorderColor = color;
    }

    /// <summary>Una raya encima de un rango, del grosor y color que se diga.</summary>
    /// <param name="rango">El rango.</param>
    /// <param name="grosor">El grosor del borde.</param>
    /// <param name="color">El color del borde.</param>
    internal static void RayaEncima(IXLRange rango, XLBorderStyleValues grosor, XLColor color)
    {
        rango.Style.Border.TopBorder = grosor;
        rango.Style.Border.TopBorderColor = color;
    }

    /// <summary>Las celdas de cifras van a la derecha, como en cualquier tabla de numeros.</summary>
    /// <param name="rango">El rango de cifras.</param>
    internal static void ALaDerecha(IXLRange rango)
        => rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

    /// <summary>Un texto centrado en su celda: las marcas de «devolvió».</summary>
    /// <param name="celda">La celda.</param>
    internal static void AlCentro(IXLCell celda)
        => celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
}
