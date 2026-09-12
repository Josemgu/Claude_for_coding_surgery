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
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Datos.Repositorios.RepositorioDeAsignaciones</c>
/// sobre SQLite, y <c>Fichas.Datos.Falso.RepositorioDeAsignacionesFalso</c> en memoria para
/// el modo <c>--falso</c>. <b>Quién lo consume:</b> <c>Fichas.App/Asignar</c>
/// (<c>OperacionDeAsignar</c>, <c>ListaParaAsignar</c>), las lecturas de Inicio, Grupo y
/// Revisar, <c>Fichas.App/Paquetes</c> y <c>Fichas.Reportes</c>. Ninguna operación de aquí
/// lanza por un dato raro: se devuelve <see cref="ResultadoDeEscritura"/> con su aviso.
/// </para>
/// </remarks>
public interface IAsignaciones
{
    /// <summary>Devuelve un trozo de la lista de asignaciones que cumplen el filtro, con el total detras.</summary>
    /// <remarks>
    /// Orden: las más recientes primero (<c>asignado_en</c> descendente). ⚠️ El filtro
    /// <see cref="FiltroDeAsignaciones.SinDevolver"/> no pregunta lo mismo en las dos
    /// implementaciones (medido el 2026-09-11): la base real mira si el caso tiene
    /// <c>estado_del_companero</c> —si el compañero devolvió su hoja—, y el doble mira si el
    /// estado vigente sigue sin marcar, que también cambia cuando Miguel marca a mano. El
    /// contrato no lo fija; está apuntado en la entrega.
    /// </remarks>
    /// <param name="filtro">Qué asignaciones; <see cref="FiltroDeAsignaciones.Activas"/> es lo que pide casi toda pantalla.</param>
    /// <param name="trozo">Qué parte de la lista; un trozo más allá del final no falla, vuelve vacío.</param>
    /// <returns>Nunca nulo: sin nada que devolver, un trozo vacío con total 0.</returns>
    PaginaDe<Asignacion> Listar(FiltroDeAsignaciones filtro, Pagina trozo);

    /// <summary>Cuenta cuantas asignaciones cumplen el filtro, sin traerlas.</summary>
    /// <param name="filtro">El mismo filtro que en <see cref="Listar"/>; las dos cuentan lo mismo.</param>
    /// <returns>0 si ninguna. Es lo que da el <see cref="PaginaDe{T}.TotalDisponible"/> de <see cref="Listar"/> sin pagar la lista.</returns>
    int Contar(FiltroDeAsignaciones filtro);

    /// <summary>Devuelve las asignaciones vivas de un caso; hoy pueden ser mas de una (P-11 abierta).</summary>
    /// <param name="casoId">El caso; uno que no existe no lanza, da la lista vacía.</param>
    /// <returns>Solo las que tienen <see cref="Asignacion.Activa"/>; vacía si nadie lo lleva. No se pagina: son una o dos.</returns>
    IReadOnlyList<Asignacion> VivasDeCaso(long casoId);

    /// <summary>Asigna un caso a un companero activo. La unica puerta de asignar que existe.</summary>
    /// <remarks>
    /// <para><b>Escribe</b> una fila viva en <c>asignaciones</c>. <b>Es idempotente sobre el
    /// par caso-compañero:</b> si ese compañero ya llevaba ese caso vivo, no se crea otra
    /// fila y vuelve <see cref="ResultadoDeEscritura.SeEscribio"/> en verdadero con el id de
    /// la que ya había y un aviso informativo («ya llevaba este caso»).</para>
    /// <para><b>No se escribe, y se dice con un problema</b>, cuando la fecha va vacía, el
    /// compañero no existe o está desactivado, o el caso no existe. <b>No mira el estado del
    /// caso</b>: uno archivado o ya completo se asigna igual (requisito 8).</para>
    /// <para>⚠️ Si otro compañero ya llevaba el caso, se guarda igual y se avisa: el
    /// contrato no inventa la regla que la P-11 tiene abierta.</para>
    /// </remarks>
    /// <param name="casoId">El caso que se reparte.</param>
    /// <param name="companeroId">Quién lo va a llevar; tiene que existir y estar activo.</param>
    /// <param name="asignadoEn">Cuándo, ISO-8601; obligatoria, no se inventa con el reloj.</param>
    /// <returns>El id de la asignación viva (nueva o la que ya había), o no escrito con su motivo.</returns>
    ResultadoDeEscritura Asignar(long casoId, long companeroId, string asignadoEn);

    /// <summary>Retira una asignacion desactivandola con su fecha; nunca la borra.</summary>
    /// <remarks>
    /// <b>Escribe</b> <see cref="Asignacion.Activa"/> a falso y
    /// <see cref="Asignacion.DesactivadaEn"/>. Con la fecha vacía no se escribe y se dice.
    /// ⚠️ Retirarla dos veces no da lo mismo en las dos implementaciones (medido el
    /// 2026-09-11): la base real solo toca filas vivas y a la segunda contesta «no se
    /// encontró la fila»; el doble la vuelve a marcar retirada sin queja. El contrato no lo
    /// fija; quien llame debe partir de una asignación viva.
    /// </remarks>
    /// <param name="asignacionId">La asignación, no el caso.</param>
    /// <param name="desactivadaEn">Cuándo se retira, ISO-8601; obligatoria.</param>
    /// <returns>El id de la asignación retirada, o no escrito con su motivo.</returns>
    ResultadoDeEscritura Retirar(long asignacionId, string desactivadaEn);
}
