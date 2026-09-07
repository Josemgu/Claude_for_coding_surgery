using Fichas.Contratos.Consultas;

namespace Fichas.App.Paquetes;

/// <summary>
/// Quien sabe escribir el reporte de lo que sube un peldano, visto desde esta pantalla.
/// </summary>
/// <remarks>
/// <para><b>Este puerto lo declara quien lo USA, no quien lo cumple, y eso es a proposito.</b>
/// La regla de <c>Fichas.App.csproj</c> —vigilada por
/// <c>PruebasSinCuadrosEnReportesYPaquetes.NingunaPantallaNombraUnaImplementacion</c>— dice que
/// las pantallas de Reportes y Paquetes solo pueden nombrar <c>Fichas.Contratos</c>: quien
/// nombra una implementacion es <c>Cascara/Servicios.cs</c> y nadie mas. Asi que aqui se
/// declara QUE hace falta, con tipos que esta carpeta puede nombrar, y el motor de PDF se
/// engancha en la cascara.</para>
///
/// <para>⚠️ <b>Es deuda declarada y tiene fecha de caducidad.</b> Donde le tocaria vivir a este
/// metodo es en <c>IReportes</c>, que es lo que pide el criterio C15-4 —«el mismo motor, un
/// método más, no un motor nuevo»—. <c>Fichas.Contratos</c> esta congelado y lo lleva otro
/// programador, asi que anadir la linea alli pisaria su trabajo. Cuando se descongele, este
/// metodo se muda a <c>IReportes</c>, esta interfaz desaparece y con ella el enganche de la
/// cascara.</para>
///
/// <para>Se recibe como NULO cuando el programa abrio con <c>--falso</c>: alli no hay reportes
/// de verdad y el paquete sale con su Excel diciendolo, en vez de fingir un PDF que no existe.
/// Es el mismo trato que <c>Servicios.Mantenimiento</c> le da al borrado.</para>
/// </remarks>
public interface IReporteDeLaSegundaVuelta
{
    /// <summary>Escribe el reporte de lo que sube a ese peldano y lo deja en esa ruta.</summary>
    /// <param name="categoria">El peldano que lo recibe.</param>
    /// <param name="casos">Los documentos que suben, con quien los intento y por que no salio.</param>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    ResultadoDeEscritura Escribir(int categoria, IReadOnlyList<CasoQueSube> casos, string rutaDestino);
}
