using Fichas.App.Paquetes;
using Fichas.Contratos.Consultas;
using Fichas.Reportes;
using Fichas.Reportes.Armado;

namespace Fichas.App.Cascara;

/// <summary>
/// Engancha el motor de PDF con lo que la pantalla de Paquetes pide, y traduce entre los dos.
/// </summary>
/// <remarks>
/// <para><b>Vive en la cascara porque es el UNICO sitio del programa que puede nombrar una
/// implementacion</b> (regla de <c>Fichas.App.csproj</c>, vigilada por
/// <c>PruebasSinCuadrosEnReportesYPaquetes.NingunaPantallaNombraUnaImplementacion</c>). La
/// pantalla declara QUE necesita —<see cref="IReporteDeLaSegundaVuelta"/>, con tipos de
/// <c>Fichas.Contratos</c>— y aqui se dice QUIEN lo cumple.</para>
///
/// <para>Lo unico que hace es traducir <see cref="CasoQueSube"/>, que es de la pantalla, a
/// <see cref="IntentoAnterior"/>, que es del motor de reportes. Son el mismo dato con dos
/// nombres, y tienen dos nombres porque las dependencias van en un solo sentido: la aplicacion
/// conoce a los reportes y los reportes no conocen a la aplicacion.</para>
///
/// <para>⚠️ <b>Es deuda declarada.</b> Cuando <c>Fichas.Contratos</c> se descongele, el metodo
/// se muda a <c>IReportes</c> —que es lo que pide el criterio C15-4— y este adaptador, la
/// interfaz de la pantalla y <see cref="IReportesDeLaEscalera"/> desaparecen los tres.</para>
/// </remarks>
internal sealed class ReporteDeLaSegundaVueltaEnPdf : IReporteDeLaSegundaVuelta
{
    /// <summary>El motor de PDF de <c>Fichas.Reportes</c>; es el mismo objeto que cumple <c>IReportes</c>.</summary>
    private readonly IReportesDeLaEscalera _motor;

    /// <summary>Se ata al motor de PDF de verdad.</summary>
    /// <param name="motor">El <c>ReportesEnPdf</c> ya montado en <see cref="Servicios"/>, por su segunda interfaz.</param>
    internal ReporteDeLaSegundaVueltaEnPdf(IReportesDeLaEscalera motor) => _motor = motor;

    /// <inheritdoc />
    /// <param name="categoria">La categoría del compañero que recibe la segunda vuelta.</param>
    /// <param name="casos">Los casos que suben, con quién los intentó antes y por qué no pudo.</param>
    /// <param name="rutaDestino">Dónde se escribe el PDF.</param>
    /// <returns>Lo que dijo el motor: escrito, o el motivo por el que no.</returns>
    public ResultadoDeEscritura Escribir(int categoria, IReadOnlyList<CasoQueSube> casos, string rutaDestino)
    {
        ArgumentNullException.ThrowIfNull(casos);

        var intentos = casos
            .Select(caso => new IntentoAnterior(
                caso.CasoId, caso.QuienLoIntento, caso.CategoriaDeQuienLoIntento, caso.Motivo))
            .ToList();

        return _motor.GenerarReporteDeLaSegundaVuelta(categoria, intentos, rutaDestino);
    }
}
