using Fichas.Contratos.Consultas;

namespace Fichas.Reportes;

/// <summary>
/// Los tres informes de la pantalla de Reportes, en Excel en vez de en PDF.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-07: <i>«agregar un paquete de reporte en Excel; está
/// bien el de PDF, pero también quiero uno con Excel»</i>. «Esta bien el de PDF» quiere decir
/// que no se toca: esto se SUMA, y hay una prueba que guarda la huella de los tres PDF byte a
/// byte para que nadie los mueva sin querer al tocar aqui.</para>
///
/// <para>⚠️ <b>Por que este puerto vive AQUI y no en <c>Fichas.Contratos</c>, que es donde le
/// tocaria.</b> Por lo mismo que <see cref="IReportesDeLaEscalera"/>: lo suyo seria tres
/// lineas mas en <c>IReportes</c>, y <b>ese archivo esta congelado y lo lleva otro
/// programador</b>. Se declara el puerto en esta biblioteca y lo cumple el MISMO motor —
/// <see cref="ReportesEnPdf"/> cumple <c>IReportes</c>, <c>IReportesDeLaEscalera</c> y este—,
/// asi que no hay un segundo motor de informes en ninguna parte y el Excel sale del mismo
/// <c>Documento</c> que el PDF.</para>
///
/// <para><b>Es deuda declarada, no una decision de arquitectura.</b> Cuando
/// <c>Fichas.Contratos</c> se descongele, estos tres metodos se mueven a <c>IReportes</c>,
/// esta interfaz desaparece y el sondeo que hace la aplicacion —<c>is IReportesEnExcel</c>—
/// se borra con ella.</para>
///
/// <para>El almacen falso NO lo cumple, y por eso la aplicacion sondea en vez de exigirlo: con
/// <c>--falso</c> no hay motor de informes y la pantalla lo dice, en vez de fingir un archivo
/// que no existe.</para>
/// </remarks>
public interface IReportesEnExcel
{
    /// <summary>Genera el informe del periodo en <c>.xlsx</c> y lo deja en la ruta que se diga.</summary>
    /// <param name="desdeIso">El primer dia del periodo, incluido, en ISO-8601.</param>
    /// <param name="hastaIso">El ultimo dia del periodo, incluido, en ISO-8601.</param>
    /// <param name="rutaDestino">Donde queda el archivo.</param>
    /// <returns>Nunca lanza por un dato raro: con <c>SeEscribio</c> en falso y su aviso si el periodo no se lee o la ruta falla.</returns>
    ResultadoDeEscritura GenerarReporteDelPeriodoEnExcel(string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el informe de un companero en <c>.xlsx</c> y lo deja en la ruta que se diga.</summary>
    /// <param name="companeroId">El numero interno del companero.</param>
    /// <param name="desdeIso">El primer dia del periodo, incluido, en ISO-8601.</param>
    /// <param name="hastaIso">El ultimo dia del periodo, incluido, en ISO-8601.</param>
    /// <param name="rutaDestino">Donde queda el archivo.</param>
    /// <returns>Con <c>SeEscribio</c> en falso y su aviso si el compañero no existe, el periodo no se lee o la ruta falla.</returns>
    ResultadoDeEscritura GenerarReporteDeCompaneroEnExcel(
        long companeroId, string desdeIso, string hastaIso, string rutaDestino);

    /// <summary>Genera el historico completo en <c>.xlsx</c> y lo deja en la ruta que se diga.</summary>
    /// <param name="rutaDestino">Donde queda el archivo.</param>
    /// <returns>Con <c>SeEscribio</c> en falso solo si la ruta falla; sin archivados se escribe igual.</returns>
    ResultadoDeEscritura GenerarHistoricoEnExcel(string rutaDestino);
}
