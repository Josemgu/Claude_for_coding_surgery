using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Todo lo que se puede hacer con las asignaciones. UNA sola operacion de asignar,
/// que es lo que hace posible el requisito 2 del dueno: asignar desde cualquier lugar.
/// </summary>
/// <remarks>
/// La tarjeta de Revisar, la correccion y la lista de Asignar llaman a <see cref="Asignar"/>
/// y no a tres cosas parecidas. El criterio de aceptacion del ADR-0003 §6.4 es una prueba
/// que la llame desde los tres sitios y compruebe que la fila sale identica.
/// Y no hay filtro de estado: cualquier caso se asigna a cualquier companero ACTIVO.
/// </remarks>
public interface IAsignaciones
{
    /// <summary>Devuelve un trozo de la lista de asignaciones que cumplen el filtro, con el total detras.</summary>
    PaginaDe<Asignacion> Listar(FiltroDeAsignaciones filtro, Pagina trozo);

    /// <summary>Cuenta cuantas asignaciones cumplen el filtro, sin traerlas.</summary>
    int Contar(FiltroDeAsignaciones filtro);

    /// <summary>Devuelve las asignaciones vivas de un caso; hoy pueden ser mas de una (P-11 abierta).</summary>
    IReadOnlyList<Asignacion> VivasDeCaso(long casoId);

    /// <summary>Asigna un caso a un companero activo. La unica puerta de asignar que existe.</summary>
    ResultadoDeEscritura Asignar(long casoId, long companeroId, string asignadoEn);

    /// <summary>Retira una asignacion desactivandola con su fecha; nunca la borra.</summary>
    ResultadoDeEscritura Retirar(long asignacionId, string desactivadaEn);
}
