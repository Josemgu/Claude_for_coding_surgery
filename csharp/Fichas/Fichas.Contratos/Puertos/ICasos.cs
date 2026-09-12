using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>Todo lo que se puede hacer con los casos. Las pantallas no ven otra cosa.</summary>
/// <remarks>
/// <b>Quién lo implementa:</b> <c>Fichas.Datos.Repositorios.RepositorioDeCasos</c> sobre
/// SQLite, y <c>Fichas.Datos.Falso.RepositorioDeCasosFalso</c> en memoria para el modo
/// <c>--falso</c>. <b>Quién lo consume:</b> casi toda la app —Inicio, Revisar, Asignar,
/// Corrección, Importar, Grupo, Paquetes y Reportes—, además de <c>Fichas.Paquetes</c> y
/// <c>Fichas.Reportes</c>. Ninguna escritura lanza por un valor raro (requisito 9): entra y
/// sale avisado, o no entra y se dice por qué.
/// </remarks>
public interface ICasos
{
    /// <summary>Devuelve un trozo de la lista de casos que cumplen el filtro, con el total detras.</summary>
    /// <remarks>
    /// Orden: por fecha de viaje ascendente, y los que no tienen fecha al final, en su
    /// propia cola, como pidió el dueño. Los archivados no salen salvo
    /// <see cref="FiltroDeCasos.IncluirArchivados"/>. La búsqueda por texto mira el número
    /// de caso, el nombre de la unidad y el nombre y el MRN de sus personas.
    /// </remarks>
    /// <param name="filtro">Qué casos; <see cref="FiltroDeCasos.Todo"/> es todo lo no archivado.</param>
    /// <param name="trozo">Qué parte de la lista; un trozo más allá del final vuelve vacío, no falla.</param>
    /// <returns>Nunca nulo: sin nada que devolver, un trozo vacío con total 0.</returns>
    PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo);

    /// <summary>Cuenta cuantos casos cumplen el filtro, sin traerlos.</summary>
    /// <param name="filtro">El mismo filtro que en <see cref="Listar"/>; cuentan lo mismo.</param>
    /// <returns>0 si ninguno.</returns>
    int Contar(FiltroDeCasos filtro);

    /// <summary>Devuelve un caso por su id, o nulo si no esta.</summary>
    /// <param name="id">La clave primaria; con 0 o con un id que no existe vuelve nulo, sin lanzar.</param>
    /// <returns>El caso tal como está en la base, archivado o no; nulo si no hay fila.</returns>
    Caso? Obtener(long id);

    /// <summary>Cuenta las personas de cada caso de la lista que se le pase, en una sola pasada.</summary>
    /// <remarks>
    /// Existe para que la tarjeta de cada caso diga «3 personas» sin una consulta por caso:
    /// una pantalla de 20 tarjetas es una consulta, no 20.
    /// </remarks>
    /// <param name="casoIds">Los casos que se van a pintar; vacía da un diccionario vacío.</param>
    /// <returns>Una entrada por cada id pedido, también con 0 para el que no tiene personas o no existe; nunca falta una clave que se pidió.</returns>
    IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds);

    /// <summary>Guarda un caso nuevo o cambia uno existente; un valor raro entra y sale avisado.</summary>
    /// <remarks>
    /// <para><b>Escribe</b> las 21 columnas del caso. Con <see cref="Caso.Id"/> en 0 da de
    /// alta y devuelve el id nuevo; con un id, cambia esa fila entera. No busca duplicados
    /// por número de caso: dos altas iguales son dos casos, que es lo que la migración 12
    /// permite a propósito.</para>
    /// <para><b>Avisa sin impedir</b> (advertencias) cuando el número de caso no es cuatro
    /// letras y cuatro dígitos o la fecha de viaje no es <c>AAAA-MM-DD</c>; la base real
    /// avisa además de la fecha que no existe, del número de unidad que no tiene 6 o 7
    /// dígitos y del mes de la fecha que no cuadra con el del número. Un campo nulo no
    /// avisa. <b>Solo no escribe</b> cuando el id no existe o el motor rechaza la fila.</para>
    /// </remarks>
    /// <param name="caso">El caso completo; lo que no venga se guarda como nulo, no se conserva lo anterior.</param>
    /// <returns>El id (nuevo o el mismo) con sus avisos, o no escrito con su motivo.</returns>
    ResultadoDeEscritura Guardar(Caso caso);

    /// <summary>Escribe el estado de la recomendacion con quien lo marca y de donde vino la marca.</summary>
    /// <remarks>
    /// Escribe SOLO el estado vigente y su firma; es el camino de Miguel marcando a mano.
    /// Si escribiera tambien lo que dijo la hoja del companero, la primera correccion de
    /// Miguel se llevaria por delante el nombre del companero y nadie sabria que
    /// discreparon, que es justo lo que la migracion 14 desdoblo para evitar. Lo que dice
    /// una hoja se escribe con <see cref="MarcarEstadoDelCompanero"/>.
    /// <para>
    /// <b>Escribe</b> <c>estado_recomendacion</c>, <c>estado_marcado_por</c>,
    /// <c>estado_marcado_en</c> (con el reloj, no con un parámetro) y
    /// <c>estado_marcado_origen</c>. <b>No toca</b> ningún motivo ni la procedencia: esto no
    /// firma campos. Marcar dos veces lo mismo deja el mismo estado con la marca de tiempo
    /// nueva; no hay aviso de «ya estaba así». No se escribe si el caso no existe.
    /// </para>
    /// </remarks>
    /// <param name="casoId">El caso que se marca.</param>
    /// <param name="estado">El estado vigente; <see cref="EstadoDeRecomendacion.SinMarcar"/> deja la columna a nulo.</param>
    /// <param name="companeroId">Quién marca; para Miguel a mano, su propia fila de <c>companeros</c>.</param>
    /// <param name="origen">De dónde vino la marca; texto libre («a mano en Revisar», la ruta de un Excel).</param>
    /// <returns>El id del caso, o no escrito con su motivo.</returns>
    ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen);

    /// <summary>
    /// Escribe lo que dijo la hoja de un companero: el estado vigente Y el registro de lo
    /// que el dijo, con su motivo, que ya no se borra aunque Miguel corrija encima.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad de la particion que hizo la migracion 14 y que llevaba desde
    /// entonces sin usarse: <c>estado_del_companero</c>, <c>_por</c> y <c>_en</c> estaban
    /// nulas en la base del dueno porque ningun camino las escribia.
    /// <para>
    /// <b>Escribe</b> las cuatro columnas de <see cref="MarcarEstado"/> más
    /// <c>estado_del_companero</c>, <c>_por</c>, <c>_en</c> y <c>motivo_del_companero</c>.
    /// <b>No toca</b> <c>motivo_no_completa</c>, que es el motivo vigente de Miguel
    /// (criterio C14-4), ni la procedencia. Lo llama solo <c>Fichas.Paquetes</c> al aplicar
    /// un Excel devuelto. No se escribe si el caso no existe.
    /// </para>
    /// </remarks>
    /// <param name="casoId">El caso que la hoja marca.</param>
    /// <param name="estado">Lo que la hoja dice de la recomendacion.</param>
    /// <param name="motivo">Por que no esta completa, si la hoja lo dice; <see cref="MotivoDeNoCompletar.SinMotivo"/> deja la columna a nulo.</param>
    /// <param name="companeroId">Quien devolvio la hoja.</param>
    /// <param name="origen">De donde salio la marca; para una hoja, su ruta.</param>
    /// <returns>El id del caso, o no escrito con su motivo.</returns>
    ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId,
        EstadoDeRecomendacion estado,
        MotivoDeNoCompletar motivo,
        long companeroId,
        string origen);

    /// <summary>Archiva o desarchiva un caso; archivar exige fecha y desarchivar la quita.</summary>
    /// <remarks>
    /// <b>Escribe</b> solo <see cref="Caso.Archivado"/> y <see cref="Caso.FechaArchivado"/>,
    /// que el esquema ata: o las dos o ninguna. No borra nada y el caso sigue contando en los
    /// reportes; por eso archivar no pregunta (requisito 9). <b>Tampoco retira la
    /// asignación</b>: eso lo pidió el dueño el 2026-09-11 y lo hace quien llama, en la app
    /// (<c>Fichas.App/Revisar/RetiradaAlArchivar</c>), no este puerto. Repetirlo con lo mismo deja lo mismo,
    /// sin aviso de «ya estaba». ⚠️ Con la fecha vacía al archivar, la base real no escribe y
    /// lo dice; el doble pone la de hoy (medido el 2026-09-11, apuntado en la entrega).
    /// </remarks>
    /// <param name="casoId">El caso.</param>
    /// <param name="archivado">Verdadero para archivar, falso para devolverlo a la vista.</param>
    /// <param name="fechaDeArchivado">ISO-8601; obligatoria al archivar, ignorada al desarchivar.</param>
    /// <returns>El id del caso, o no escrito con su motivo.</returns>
    ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado);
}
