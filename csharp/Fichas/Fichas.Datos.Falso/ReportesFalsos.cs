using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Los reportes inventados: no escriben ningun PDF y lo dicen.
/// </summary>
/// <remarks>
/// ⚠️ Existe para que la pantalla de Reportes se pueda construir antes que Fichas.Reportes
/// (fase C8). Con que fecha se mide «el periodo» sigue abierto (P-7 de ARQUITECTURA §7):
/// aqui se piden las dos fechas a la cara para no elegirla por nadie.
/// </remarks>
public sealed class ReportesFalsos : IReportes
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public ReportesFalsos(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Cuenta lo que entraria en el reporte del periodo, sin escribir el PDF.</summary>
    /// <param name="desdeIso">Primer día del periodo, ISO; se compara con <c>CreadoEn</c>.</param>
    /// <param name="hastaIso">Último día del periodo, ISO, incluido.</param>
    /// <param name="rutaDestino">Dónde iría el PDF; solo se nombra en el aviso.</param>
    public ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino)
    {
        var cuantos = _almacen.Casos.Values.Count(c =>
            c.CreadoEn.Length > 0
            && string.CompareOrdinal(c.CreadoEn, desdeIso) >= 0
            && string.CompareOrdinal(c.CreadoEn, hastaIso) <= 0);

        return ResultadoDeEscritura.BienCon(0, Aviso.Informa(
            $"El periodo {desdeIso} a {hastaIso} tiene {cuantos} caso(s): NO se escribio ningun PDF.",
            string.Empty,
            $"Los reportes de verdad llegan en la fase C8. La ruta pedida era «{rutaDestino}»."));
    }

    /// <summary>Cuenta lo que entraria en el reporte de un companero, sin escribir el PDF.</summary>
    /// <param name="companeroId">De quién; si no existe, no se escribe y se dice.</param>
    /// <param name="desdeIso">Primer día del periodo, ISO; aquí solo se nombra, no filtra.</param>
    /// <param name="hastaIso">Último día del periodo, ISO; aquí solo se nombra, no filtra.</param>
    /// <param name="rutaDestino">Dónde iría el PDF; solo se nombra en el aviso.</param>
    public ResultadoDeEscritura GenerarReporteDeCompanero(long companeroId, string desdeIso, string hastaIso, string rutaDestino)
    {
        if (!_almacen.Companeros.TryGetValue(companeroId, out var companero))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun companero con el numero interno {companeroId}."));

        var cuantos = _almacen.Asignaciones.Values.Count(a => a.CompaneroId == companeroId && a.Activa);
        return ResultadoDeEscritura.BienCon(companeroId, Aviso.Informa(
            $"{companero.Nombre} lleva {cuantos} caso(s) entre {desdeIso} y {hastaIso}: NO se escribio ningun PDF.",
            string.Empty,
            $"Los reportes de verdad llegan en la fase C8. La ruta pedida era «{rutaDestino}»."));
    }

    /// <summary>Cuenta lo que entraria en el historico, sin escribir el PDF.</summary>
    /// <param name="rutaDestino">Dónde iría el PDF; solo se nombra en el aviso.</param>
    public ResultadoDeEscritura GenerarHistorico(string rutaDestino)
        => ResultadoDeEscritura.BienCon(0, Aviso.Informa(
            $"El historico tendria {_almacen.Casos.Count} caso(s) y {_almacen.Personas.Count} persona(s): NO se escribio ningun PDF.",
            string.Empty,
            $"Los reportes de verdad llegan en la fase C8. La ruta pedida era «{rutaDestino}»."));
}
