using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Un <see cref="ICasos"/> que solo hace una cosa de más: contar cuántas veces le preguntan
/// por cada camino.
/// </summary>
/// <remarks>
/// <para>Lo que hay que fijar de la vuelta del Excel es que una hoja SIN id de caso —las
/// generadas antes del 2026-09-03— no lanza una lista paginada con filtro de texto POR CADA
/// FILA: con 300 filas eran 300 listas (medido el 2026-09-15 sobre <c>master</c>), y contra
/// SQLite cada lista son dos consultas con <c>LIKE</c> sobre casos y personas. Un cronómetro
/// se cae con la máquina cargada; un contador dice lo mismo en cualquier máquina.</para>
///
/// <para>⛔ No cambia ninguna respuesta: pasa todo tal cual al de debajo.</para>
/// </remarks>
internal sealed class CasosQueSeCuentan : ICasos
{
    /// <summary>A quien se le pasa todo tal cual después de contar.</summary>
    private readonly ICasos _deVerdad;

    /// <summary>Envuelve al de verdad.</summary>
    /// <param name="deVerdad">El almacén que contesta; este solo cuenta.</param>
    public CasosQueSeCuentan(ICasos deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuántas listas se pidieron; contra SQLite cada una son dos consultas (COUNT y SELECT).</summary>
    public int Listas { get; private set; }

    /// <summary>Cuántas de esas listas llevaban filtro de texto, que es el <c>LIKE</c> caro.</summary>
    public int ListasConTexto { get; private set; }

    /// <summary>Cuántas veces se pidió un caso por id.</summary>
    public int PorId { get; private set; }

    /// <summary>Cuántos recuentos se pidieron.</summary>
    public int Recuentos { get; private set; }

    /// <summary>Cuántas escrituras salieron: estados marcados, guardados y archivados.</summary>
    public int Escrituras { get; private set; }

    /// <inheritdoc />
    public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo)
    {
        Listas++;
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) ListasConTexto++;
        return _deVerdad.Listar(filtro, trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDeCasos filtro)
    {
        Recuentos++;
        return _deVerdad.Contar(filtro);
    }

    /// <inheritdoc />
    public Caso? Obtener(long id)
    {
        PorId++;
        return _deVerdad.Obtener(id);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds)
    {
        Recuentos++;
        return _deVerdad.ContarPersonasDe(casoIds);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Caso caso)
    {
        Escrituras++;
        return _deVerdad.Guardar(caso);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
    {
        Escrituras++;
        return _deVerdad.MarcarEstado(casoId, estado, companeroId, origen);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo, long companeroId, string origen)
    {
        Escrituras++;
        return _deVerdad.MarcarEstadoDelCompanero(casoId, estado, motivo, companeroId, origen);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
    {
        Escrituras++;
        return _deVerdad.Archivar(casoId, archivado, fechaDeArchivado);
    }
}
