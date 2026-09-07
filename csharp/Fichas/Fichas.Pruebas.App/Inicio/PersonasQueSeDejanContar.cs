using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// Un <see cref="IPersonas"/> que hace lo mismo que el de dentro y ademas cuenta cuantas
/// veces se le pregunta.
/// </summary>
/// <remarks>
/// <para>Existe porque «no se va a la base de mas» es la unica parte del rendimiento que se
/// puede comprobar de forma DETERMINISTA. Los milisegundos de esta maquina dependen de
/// cuantas pruebas corren al lado —medido: la misma llamada, 94 ms sola y 312 ms con la
/// suite entera encima—; el numero de consultas no depende de nada de eso.</para>
///
/// <para>Y el defecto que vigila es real, no hipotetico: al hacer que la pastilla del
/// calendario contara personas confirmadas (criterio C20-3), Inicio paso a leer la tabla de
/// personas DOS veces por pintado, y el resumen subio de <b>58 ms</b> a <b>362 ms</b> sobre
/// 3 000 documentos. Se arreglo pasandole a <c>LectorDeGrupos.PorDia</c> las personas ya
/// leidas; esta prueba es lo que impide que vuelva.</para>
///
/// <para>⛔ No cambia ni una respuesta: todo se delega tal cual. Si decidiera algo, la
/// prueba dejaria de medir el programa y pasaria a medir este archivo.</para>
/// </remarks>
public sealed class PersonasQueSeDejanContar : IPersonas
{
    private readonly IPersonas _dedentro;

    /// <summary>Envuelve al de verdad.</summary>
    public PersonasQueSeDejanContar(IPersonas dedentro) => _dedentro = dedentro;

    /// <summary>Cuantas veces se ha pedido la lista entera.</summary>
    public int CuantasVecesListo { get; private set; }

    /// <summary>Cuantas veces se han pedido las personas de UN caso; una por documento es el defecto.</summary>
    public int CuantasVecesPidioLasDeUnCaso { get; private set; }

    /// <summary>Cuantas veces se ha contado sin traer nada.</summary>
    public int CuantasVecesConto { get; private set; }

    /// <summary>Pone los contadores a cero, para medir un pintado y no el calentamiento.</summary>
    public void EmpezarDeCero()
    {
        CuantasVecesListo = 0;
        CuantasVecesPidioLasDeUnCaso = 0;
        CuantasVecesConto = 0;
    }

    /// <inheritdoc />
    public PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo)
    {
        CuantasVecesListo++;
        return _dedentro.Listar(filtro, trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDePersonas filtro)
    {
        CuantasVecesConto++;
        return _dedentro.Contar(filtro);
    }

    /// <inheritdoc />
    public Persona? Obtener(long id) => _dedentro.Obtener(id);

    /// <inheritdoc />
    public IReadOnlyList<Persona> DeCaso(long casoId)
    {
        CuantasVecesPidioLasDeUnCaso++;
        return _dedentro.DeCaso(casoId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Persona persona) => _dedentro.Guardar(persona);

    /// <inheritdoc />
    public ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId)
        => _dedentro.AnotarPropuesta(personaId, propuesta, companeroId);

    /// <inheritdoc />
    public ResultadoDeEscritura ResponderLosPasos(
        long personaId, RespuestaALosPasos respuesta, long companeroId, string origen)
        => _dedentro.ResponderLosPasos(personaId, respuesta, companeroId, origen);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId)
        => _dedentro.FirmasDeLosPasosDelCaso(casoId);
}
