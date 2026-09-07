using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Un <see cref="IReportes"/> de prueba que SI toca el disco, o que falla a la orden.
/// </summary>
/// <remarks>
/// No sustituye a <c>ReportesFalsos</c> y no compite con el: aquel dice que genero sin
/// escribir nada —para poder abrir la pantalla sin base— y este escribe de verdad unos bytes
/// en la ruta pedida, que es lo unico que hace falta para comprobar que la pantalla mide el
/// archivo, lo nombra y sabe decir cuando NO esta.
/// </remarks>
internal sealed class ReportesDeMentirijilla : IReportes
{
    private readonly byte[] _contenido;
    private readonly bool _escribeDeVerdad;
    private readonly Exception? _seRompeCon;

    /// <summary>Escribe esos bytes en la ruta que le pidan.</summary>
    internal ReportesDeMentirijilla(byte[] contenido)
    {
        _contenido = contenido;
        _escribeDeVerdad = true;
    }

    /// <summary>Dice que escribio pero no escribe nada, como hacen los reportes inventados.</summary>
    internal ReportesDeMentirijilla()
    {
        _contenido = [];
        _escribeDeVerdad = false;
    }

    /// <summary>Se rompe con esa excepcion en cuanto le pidan algo.</summary>
    internal ReportesDeMentirijilla(Exception seRompeCon)
    {
        _contenido = [];
        _seRompeCon = seRompeCon;
    }

    /// <summary>La ultima ruta que le pidieron, para poder comprobar el nombre propuesto.</summary>
    internal string? UltimaRuta { get; private set; }

    /// <summary>El ultimo periodo que le pidieron, en las dos fechas tal como llegaron.</summary>
    internal (string Desde, string Hasta)? UltimoPeriodo { get; private set; }

    /// <inheritdoc />
    public ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino)
    {
        UltimoPeriodo = (desdeIso, hastaIso);
        return Escribir(rutaDestino, $"Reporte de {desdeIso} a {hastaIso}");
    }

    /// <inheritdoc />
    public ResultadoDeEscritura GenerarReporteDeCompanero(long companeroId, string desdeIso, string hastaIso, string rutaDestino)
    {
        UltimoPeriodo = (desdeIso, hastaIso);
        return Escribir(rutaDestino, $"Reporte del compañero {companeroId}");
    }

    /// <inheritdoc />
    public ResultadoDeEscritura GenerarHistorico(string rutaDestino) => Escribir(rutaDestino, "Histórico completo");

    private ResultadoDeEscritura Escribir(string rutaDestino, string queEs)
    {
        if (_seRompeCon is not null) throw _seRompeCon;

        UltimaRuta = rutaDestino;
        if (_escribeDeVerdad) File.WriteAllBytes(rutaDestino, _contenido);

        return ResultadoDeEscritura.BienCon(0, Aviso.Informa($"{queEs} escrito.", string.Empty, "Lo dice la prueba."));
    }
}
