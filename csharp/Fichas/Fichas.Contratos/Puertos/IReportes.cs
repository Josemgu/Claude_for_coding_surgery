using Fichas.Contratos.Consultas;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Los reportes en PDF para los jefes, como los del programa viejo.
/// </summary>
/// <remarks>
/// El periodo se dice con dos fechas ISO-8601 y no con un nombre: sobre que fecha se mide
/// «el periodo» sigue siendo la P-7 de ARQUITECTURA §7, abierta. Quien construya la C8
/// tiene que preguntarla, no elegirla.
/// </remarks>
public interface IReportes
{
    /// <summary>Genera el reporte del periodo en PDF y lo deja en la ruta que se diga.</summary>
    ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el reporte de un companero en PDF y lo deja en la ruta que se diga.</summary>
    ResultadoDeEscritura GenerarReporteDeCompanero(long companeroId, string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el historico completo en PDF y lo deja en la ruta que se diga.</summary>
    ResultadoDeEscritura GenerarHistorico(string rutaDestino);
}
