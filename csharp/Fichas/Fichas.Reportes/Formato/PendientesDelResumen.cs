using System.Globalization;
using ClosedXML.Excel;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;
using static Fichas.Reportes.Formato.PinturaDelResumen;

namespace Fichas.Reportes.Formato;

/// <summary>
/// La lista de solo los pendientes, debajo de todo en la hoja unica: una fila por persona.
/// </summary>
/// <remarks>
/// Del dueno, 2026-09-16: la lista de <b>solo los pendientes</b> debajo —segunda pagina al
/// imprimir—, porque «quiénes» de todos no cabe. Son exactamente las personas de «Quiénes
/// viajaron sin verificar» del PDF, con su unidad, que les falta, quien las lleva, cuando
/// viajaron, su caso y su templo. Sin templo va una raya: «no consta» no es una respuesta.
/// </remarks>
internal static class PendientesDelResumen
{
    /// <summary>Como llega una fecha dentro del resumen.</summary>
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <summary>Como se estampa una fecha; el mismo que pone el espejo y la hoja del companero.</summary>
    private const string FormatoDeFechaEnExcel = "yyyy-mm-dd";

    /// <summary>Cuantos caracteres caben en «Qué le falta» (G:H, 22 + 11 de ancho) a 11 puntos; medido en Excel: 32 caben en una linea, 36 no.</summary>
    private const int CaracteresEnQueLeFalta = 34;

    /// <summary>El alto de una linea de 11 puntos, en puntos de fila.</summary>
    private const double AltoDeUnaLinea = 15;

    /// <summary>La lista entera: titulo, rotulos y una fila por persona; «Nadie» si no hay ninguna.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="resumen">El resumen.</param>
    /// <param name="sitio">Donde cae cada fila.</param>
    internal static void Escribir(IXLWorksheet hoja, ResumenDelPeriodo resumen, DisposicionDelResumen sitio)
    {
        HojaDelResumen.TituloDeSeccion(
            hoja, sitio.FilaDelTituloDePendientes, "B", "N",
            $"Pendientes · {Plural.Con(resumen.Pendientes.Count, "persona viajó", "personas viajaron")} sin completar");

        var rotulos = sitio.FilaDeLosRotulosDePendientes;
        TablasDelResumen.Rotulos(hoja, rotulos, 2, ["N.º", "Unidad"]);
        TablasDelResumen.RotuloFundido(hoja, rotulos, "D", "F", "Persona");
        TablasDelResumen.RotuloFundido(hoja, rotulos, "G", "H", "Qué le falta");
        TablasDelResumen.Rotulos(hoja, rotulos, 10, ["Agente", "Viajó el", "Caso"]);
        TablasDelResumen.RotuloFundido(hoja, rotulos, "M", "N", "Templo");

        if (resumen.Pendientes.Count == 0)
        {
            HojaDelResumen.Escribir(hoja, $"B{sitio.PrimeraFilaDePendientes}:N{sitio.PrimeraFilaDePendientes}", "Nadie", 11, negrita: false, Apagado);
            return;
        }

        var fila = sitio.PrimeraFilaDePendientes;
        foreach (var pendiente in resumen.Pendientes)
        {
            UnPendiente(hoja, fila, pendiente);
            fila++;
        }
        hoja.Range(sitio.PrimeraFilaDePendientes, 2, fila - 1, 14).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    /// <summary>Una fila de la lista de pendientes.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    /// <param name="pendiente">La persona.</param>
    private static void UnPendiente(IXLWorksheet hoja, int fila, PendienteDelResumen pendiente)
    {
        TablasDelResumen.Texto(hoja.Cell(fila, 2), pendiente.NumeroDeUnidad);
        hoja.Cell(fila, 3).SetValue(pendiente.Unidad);
        hoja.Range($"D{fila}:F{fila}").Merge().FirstCell().SetValue(pendiente.Persona);
        var falta = hoja.Range($"G{fila}:H{fila}").Merge().FirstCell();
        falta.SetValue(pendiente.QueLeFalta);
        falta.Style.Alignment.WrapText = true;
        // Excel NO autoajusta el alto de una fila por una celda fundida (medido abriendo el
        // archivo: la fila se quedaba en 15 puntos y el texto cortado). Se calcula aqui.
        var lineas = (int)Math.Ceiling(pendiente.QueLeFalta.Length / (double)CaracteresEnQueLeFalta);
        if (lineas > 1) hoja.Row(fila).Height = AltoDeUnaLinea * lineas;
        hoja.Cell(fila, 10).SetValue(pendiente.Agente);
        if (pendiente.Agente == Vocabulario.SinAgente) hoja.Cell(fila, 10).Style.Font.FontColor = Apagado;
        Fecha(hoja.Cell(fila, 11), pendiente.ViajoEl);
        TablasDelResumen.Texto(hoja.Cell(fila, 12), pendiente.Caso ?? TablasDelResumen.Raya);
        hoja.Range($"M{fila}:N{fila}").Merge().FirstCell().SetValue(pendiente.Templo);
    }

    /// <summary>Una fecha legible entra como fecha de verdad; una que no se lee se deja tal cual, sin adivinar.</summary>
    /// <param name="celda">La celda.</param>
    /// <param name="valor">La fecha «AAAA-MM-DD», o nula.</param>
    private static void Fecha(IXLCell celda, string? valor)
    {
        if (DateTime.TryParseExact(valor, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            celda.Value = fecha;
            celda.Style.NumberFormat.Format = FormatoDeFechaEnExcel;
            return;
        }
        TablasDelResumen.Texto(celda, valor ?? TablasDelResumen.Raya);
    }
}
