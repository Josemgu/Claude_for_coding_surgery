using Fichas.Contratos.Consultas;
using Fichas.Reportes.Armado;

namespace Fichas.Reportes;

/// <summary>
/// El reporte de la segunda vuelta, para quien recibe lo que el peldano de abajo no pudo.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Por que este puerto vive AQUI y no en <c>Fichas.Contratos</c>, que es donde le
/// tocaria.</b> El criterio C15-4 dice «el mismo motor de <c>IReportes</c>, un método más, no un
/// motor nuevo», y eso significa una linea mas en <c>IReportes</c>. <b>Ese archivo esta
/// congelado y lo lleva otro programador</b>, asi que anadirla desde aqui pisaria su trabajo.
/// Lo que se hace en su lugar es declarar el puerto en esta biblioteca, que es la mia, e
/// implementarlo en el mismo motor: <c>ReportesEnPdf</c> cumple <c>IReportes</c> y este a la
/// vez, y no hay un segundo motor de PDF en ninguna parte.</para>
///
/// <para><b>Es deuda declarada, no una decision de arquitectura.</b> Cuando
/// <c>Fichas.Contratos</c> se descongele, este metodo se mueve a <c>IReportes</c>, esta
/// interfaz desaparece y el sondeo que hace la aplicacion —<c>is IReportesDeLaEscalera</c>— se
/// borra con ella. Queda anotado en la entrega para que no se quede aqui por inercia.</para>
///
/// <para>El almacen falso NO lo cumple, y por eso la aplicacion sondea en vez de exigirlo: con
/// <c>--falso</c> no hay segunda vuelta que reportar y la pantalla lo dice, en vez de fingir un
/// PDF que no existe.</para>
/// </remarks>
public interface IReportesDeLaEscalera
{
    /// <summary>Genera el reporte de la segunda vuelta en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="categoria">El peldano que lo recibe.</param>
    /// <param name="intentos">Los documentos que suben, con quien los intento y por que no salio.</param>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    ResultadoDeEscritura GenerarReporteDeLaSegundaVuelta(
        int categoria, IReadOnlyList<IntentoAnterior> intentos, string rutaDestino);
}
