using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>Todo lo que se puede hacer con los companeros. Se desactivan, no se borran.</summary>
/// <remarks>
/// <b>Quién lo implementa:</b> <c>Fichas.Datos.Repositorios.RepositorioDeCompaneros</c>
/// sobre SQLite, y <c>Fichas.Datos.Falso.RepositorioDeCompanerosFalso</c> en memoria.
/// <b>Quién lo consume:</b> el equipo de Asignar (<c>PanelDelEquipo</c>,
/// <c>PuestosDelEquipo</c>), y toda pantalla que necesita nombrar a un compañero: Inicio,
/// Grupo, Corrección, Revisar, Paquetes, Reportes, y <c>Fichas.Paquetes</c> y
/// <c>Fichas.Reportes</c>. Borrar a uno del todo no está aquí: es
/// <see cref="IMantenimiento.PlanearCompanero"/>, y solo si no lleva nada a su nombre.
/// </remarks>
public interface ICompaneros
{
    /// <summary>Devuelve un trozo de la lista de companeros que cumplen el filtro, con el total detras.</summary>
    /// <remarks>Orden: por nombre sin distinguir mayúsculas, y a igual nombre por id.</remarks>
    /// <param name="filtro">Qué compañeros; por defecto solo los activos.</param>
    /// <param name="trozo">Qué parte; un trozo más allá del final vuelve vacío.</param>
    /// <returns>Nunca nulo: sin nada, un trozo vacío con total 0.</returns>
    PaginaDe<Companero> Listar(FiltroDeCompaneros filtro, Pagina trozo);

    /// <summary>Devuelve todos los activos; son pocos y toda pantalla de asignar los necesita enteros.</summary>
    /// <returns>Los que tienen <see cref="Companero.Activo"/>, por nombre; vacía si no hay ninguno. Sin paginar: es un equipo, no una lista de casos.</returns>
    IReadOnlyList<Companero> Activos();

    /// <summary>Devuelve un companero por su id, o nulo si no esta.</summary>
    /// <param name="id">La clave primaria; 0 o un id que no existe da nulo, sin lanzar.</param>
    /// <returns>El compañero, activo o no; nulo si no hay fila.</returns>
    Companero? Obtener(long id);

    /// <summary>Da de alta un companero o cambia su nombre.</summary>
    /// <remarks>
    /// <b>Escribe</b> nombre, activo, fecha de baja, fecha de alta, rol y categoría. Con
    /// <see cref="Companero.Id"/> en 0 da de alta y devuelve el id nuevo; con un id cambia esa
    /// fila. <b>No se escribe</b> con el nombre vacío: es lo único que identifica a quien
    /// firma. El nombre NO es único, así que dos altas iguales son dos personas; no se avisa
    /// de un nombre repetido. Tampoco se corrige una categoría fuera de rango en silencio.
    /// </remarks>
    /// <param name="companero">El compañero completo; el nombre se guarda recortado.</param>
    /// <returns>El id (nuevo o el mismo), o no escrito con su motivo.</returns>
    ResultadoDeEscritura Guardar(Companero companero);

    /// <summary>Desactiva un companero dejando su fecha; nunca lo borra.</summary>
    /// <remarks>
    /// <b>Escribe</b> <see cref="Companero.Activo"/> a falso y
    /// <see cref="Companero.DesactivadoEn"/>. <b>No retira sus asignaciones vivas</b>: nada se
    /// quita sin preguntar; si le quedan casos, se escribe igual y se avisa de cuántos lleva
    /// para que Miguel los reasigne. Con la fecha vacía no se escribe y se dice. Repetirlo
    /// deja lo mismo, sin aviso de «ya estaba». La vuelta atrás es
    /// <see cref="IMantenimiento.Reactivar"/>.
    /// </remarks>
    /// <param name="companeroId">A quién se desactiva.</param>
    /// <param name="desactivadoEn">Cuándo, ISO-8601; obligatoria, no se inventa con el reloj.</param>
    /// <returns>El id del compañero, con aviso si todavía lleva casos; o no escrito con su motivo.</returns>
    ResultadoDeEscritura Desactivar(long companeroId, string desactivadoEn);
}
