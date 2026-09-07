using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>Todo lo que se puede hacer con los casos. Las pantallas no ven otra cosa.</summary>
public interface ICasos
{
    /// <summary>Devuelve un trozo de la lista de casos que cumplen el filtro, con el total detras.</summary>
    PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo);

    /// <summary>Cuenta cuantos casos cumplen el filtro, sin traerlos.</summary>
    int Contar(FiltroDeCasos filtro);

    /// <summary>Devuelve un caso por su id, o nulo si no esta.</summary>
    Caso? Obtener(long id);

    /// <summary>Cuenta las personas de cada caso de la lista que se le pase, en una sola pasada.</summary>
    IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds);

    /// <summary>Guarda un caso nuevo o cambia uno existente; un valor raro entra y sale avisado.</summary>
    ResultadoDeEscritura Guardar(Caso caso);

    /// <summary>Escribe el estado de la recomendacion con quien lo marca y de donde vino la marca.</summary>
    /// <remarks>
    /// Escribe SOLO el estado vigente y su firma; es el camino de Miguel marcando a mano.
    /// Si escribiera tambien lo que dijo la hoja del companero, la primera correccion de
    /// Miguel se llevaria por delante el nombre del companero y nadie sabria que
    /// discreparon, que es justo lo que la migracion 14 desdoblo para evitar. Lo que dice
    /// una hoja se escribe con <see cref="MarcarEstadoDelCompanero"/>.
    /// </remarks>
    ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen);

    /// <summary>
    /// Escribe lo que dijo la hoja de un companero: el estado vigente Y el registro de lo
    /// que el dijo, con su motivo, que ya no se borra aunque Miguel corrija encima.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad de la particion que hizo la migracion 14 y que llevaba desde
    /// entonces sin usarse: <c>estado_del_companero</c>, <c>_por</c> y <c>_en</c> estaban
    /// nulas en la base del dueno porque ningun camino las escribia.
    /// </remarks>
    /// <param name="casoId">El caso que la hoja marca.</param>
    /// <param name="estado">Lo que la hoja dice de la recomendacion.</param>
    /// <param name="motivo">Por que no esta completa, si la hoja lo dice.</param>
    /// <param name="companeroId">Quien devolvio la hoja.</param>
    /// <param name="origen">De donde salio la marca; para una hoja, su ruta.</param>
    ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId,
        EstadoDeRecomendacion estado,
        MotivoDeNoCompletar motivo,
        long companeroId,
        string origen);

    /// <summary>Archiva o desarchiva un caso; archivar exige fecha y desarchivar la quita.</summary>
    ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado);
}
