using Fichas.Contratos.Consultas;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Los reportes en PDF para los jefes, como los del programa viejo.
/// </summary>
/// <remarks>
/// El periodo se dice con dos fechas ISO-8601 y no con un nombre: sobre que fecha se mide
/// «el periodo» sigue siendo la P-7 de ARQUITECTURA §7, abierta. Quien construya la C8
/// tiene que preguntarla, no elegirla.
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Reportes.ReportesEnPdf</c>, que escribe el archivo
/// primero como <c>.parcial</c> y lo renombra al acabar, y
/// <c>Fichas.Datos.Falso.ReportesFalsos</c>, que cuenta lo que entraría y no escribe nada.
/// <b>Quién lo consume:</b> solo <c>Fichas.App/Reportes/OperacionDeReporte</c>. <b>Ninguno
/// escribe en la base</b>: leen y producen un archivo. El <see cref="ResultadoDeEscritura.Id"/>
/// que devuelven no es una fila: la base real pone 0, el doble el id del compañero.
/// </para>
/// </remarks>
public interface IReportes
{
    /// <summary>Genera el reporte del periodo en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="desdeIso">Primer día del periodo, <c>AAAA-MM-DD</c>, incluido.</param>
    /// <param name="hastaIso">Último día, <c>AAAA-MM-DD</c>, incluido. Un periodo que no se puede leer no escribe y lo dice.</param>
    /// <param name="rutaDestino">La ruta completa del PDF; vacía no escribe y lo dice.</param>
    /// <returns>Escrito, con un aviso informativo que dice cuántas páginas lleva el archivo para poder comprobarlo abriéndolo, y una advertencia si algún carácter no cupo en la fuente; o no escrito con el motivo.</returns>
    ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el reporte de un companero en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="companeroId">De quién; uno que no existe no escribe y lo dice.</param>
    /// <param name="desdeIso">Primer día del periodo, <c>AAAA-MM-DD</c>, incluido.</param>
    /// <param name="hastaIso">Último día, incluido.</param>
    /// <param name="rutaDestino">La ruta completa del PDF.</param>
    /// <returns>Como en <see cref="GenerarReporteDelPeriodo"/>.</returns>
    ResultadoDeEscritura GenerarReporteDeCompanero(long companeroId, string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el historico completo en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="rutaDestino">La ruta completa del PDF.</param>
    /// <returns>Como en <see cref="GenerarReporteDelPeriodo"/>; los archivados también cuentan, porque archivar no borra.</returns>
    ResultadoDeEscritura GenerarHistorico(string rutaDestino);
}
