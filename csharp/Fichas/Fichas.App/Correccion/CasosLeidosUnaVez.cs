using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Correccion;

/// <summary>
/// Los casos leídos UNA vez por pasada: la misma lista sirve al tablero de Revisar y a lo
/// que le falta a cada documento, en vez de pedírsela a la base dos veces.
/// </summary>
/// <remarks>
/// <para><b>Existe por el plan del 2026-09-15 (R-5).</b> <c>LlenarLosGrupos</c> arma el tablero
/// con <c>TableroDeRevisar.Cargar</c> y el veredicto con
/// <c>LoQueLeFaltaACadaDocumento.DeTodaLaBase</c>, y cada uno pedía la base entera por su
/// cuenta con el mismo filtro y el mismo trozo: dos <c>Listar</c> —cuatro consultas SQL, con el
/// <c>COUNT</c> de cada una— por cada entrada a Corrección, por cada documento que se resuelve y
/// por cada eliminación.</para>
///
/// <para><b>Por qué un envoltorio y no un parámetro.</b> <c>TableroDeRevisar</c> es terreno de
/// Revisar y no expone los casos que leyó, solo tarjetas; y <c>Fichas.Contratos</c> está
/// congelado. Envolver <see cref="ICasos"/> deja a los dos lectores tal como están y les da
/// la misma lectura por debajo. Es el mismo camino que el plan propone para R-6, en pequeño.</para>
///
/// <para><b>Vive lo que dura una pasada.</b> Se crea al empezar <c>LlenarLosGrupos</c> y se
/// tira al terminar: dentro de una pasada no se escribe nada, así que lo leído sigue valiendo
/// hasta el final. Y por si alguien escribiera con él en la mano, cualquier escritura olvida
/// lo leído: la siguiente lectura vuelve a la base.</para>
///
/// <para>⛔ Aquí no se decide ningún veredicto: sigue siendo <c>LoQueLeFalta</c> (decisión del
/// 2026-09-06). Solo se evita leer dos veces lo mismo.</para>
/// </remarks>
public sealed class CasosLeidosUnaVez : ICasos
{
    /// <summary>El almacén que contesta de verdad.</summary>
    private readonly ICasos _deVerdad;

    /// <summary>Lo ya leído en esta pasada, por filtro y trozo; los dos son records y se comparan por valor.</summary>
    private readonly Dictionary<(FiltroDeCasos Filtro, Pagina Trozo), PaginaDe<Caso>> _leidas = [];

    /// <summary>Envuelve el almacén para una pasada.</summary>
    /// <param name="deVerdad">El almacén de casos de los servicios.</param>
    public CasosLeidosUnaVez(ICasos deVerdad)
    {
        ArgumentNullException.ThrowIfNull(deVerdad);
        _deVerdad = deVerdad;
    }

    /// <summary>Cuántas lecturas distintas se han hecho a la base en esta pasada; para el cuaderno.</summary>
    public int LecturasALaBase => _leidas.Count;

    /// <summary>La lista pedida: de la base la primera vez, y la misma después mientras nadie escriba.</summary>
    /// <param name="filtro">Qué casos entran.</param>
    /// <param name="trozo">Qué página se pide.</param>
    public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var clave = (filtro, trozo);
        if (_leidas.TryGetValue(clave, out var leida)) return leida;

        leida = _deVerdad.Listar(filtro, trozo);
        _leidas[clave] = leida;
        return leida;
    }

    /// <inheritdoc />
    public int Contar(FiltroDeCasos filtro) => _deVerdad.Contar(filtro);

    /// <inheritdoc />
    public Caso? Obtener(long id) => _deVerdad.Obtener(id);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds) => _deVerdad.ContarPersonasDe(casoIds);

    /// <summary>Escribe y olvida lo leído: la siguiente lista vuelve a la base.</summary>
    /// <param name="caso">El caso que se guarda.</param>
    public ResultadoDeEscritura Guardar(Caso caso) => Olvidando(() => _deVerdad.Guardar(caso));

    /// <summary>Escribe y olvida lo leído.</summary>
    /// <param name="casoId">El caso.</param>
    /// <param name="estado">El estado que se marca.</param>
    /// <param name="companeroId">Quién lo marca.</param>
    /// <param name="origen">De dónde vino la marca.</param>
    public ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
        => Olvidando(() => _deVerdad.MarcarEstado(casoId, estado, companeroId, origen));

    /// <summary>Escribe y olvida lo leído.</summary>
    /// <param name="casoId">El caso.</param>
    /// <param name="estado">El estado que dijo el compañero.</param>
    /// <param name="motivo">Su motivo si no está completa.</param>
    /// <param name="companeroId">Qué compañero.</param>
    /// <param name="origen">De dónde vino la marca.</param>
    public ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo, long companeroId, string origen)
        => Olvidando(() => _deVerdad.MarcarEstadoDelCompanero(casoId, estado, motivo, companeroId, origen));

    /// <summary>Escribe y olvida lo leído.</summary>
    /// <param name="casoId">El caso.</param>
    /// <param name="archivado">Si se archiva o se desarchiva.</param>
    /// <param name="fechaDeArchivado">Cuándo.</param>
    public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
        => Olvidando(() => _deVerdad.Archivar(casoId, archivado, fechaDeArchivado));

    /// <summary>Hace la escritura y vacía lo leído, pase lo que pase con ella.</summary>
    /// <param name="escritura">La escritura que se pasa al almacén.</param>
    private ResultadoDeEscritura Olvidando(Func<ResultadoDeEscritura> escritura)
    {
        _leidas.Clear();
        return escritura();
    }
}
