using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Un <see cref="IPersonas"/> que cuenta cuantas veces le preguntan, y por delante deja pasar
/// todo al de verdad.
/// </summary>
/// <remarks>
/// Existe para una sola cosa: que la lista de Asignar no pueda volver a leer las personas
/// <b>una vez por fila</b>. Medir milisegundos no lo caza —una pantallada de 60 filas con 60
/// consultas sigue cabiendo en el techo— y sin embargo con 3 000 documentos son 3 000
/// consultas al recorrer la lista entera. Lo que se vigila es la FORMA de preguntar, que es
/// lo que no se degrada solo.
/// </remarks>
internal sealed class EspiaDePersonas : IPersonas
{
    /// <summary>El repositorio que de verdad contesta; todo se le pasa tal cual después de contar.</summary>
    private readonly IPersonas _deVerdad;

    /// <summary>Envuelve al repositorio que de verdad contesta.</summary>
    /// <param name="deVerdad">El repositorio que contesta; normalmente el falso.</param>
    public EspiaDePersonas(IPersonas deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuantas veces se ha preguntado por las personas de UN caso.</summary>
    public int VecesQueSePreguntoPorUnCaso { get; private set; }

    /// <summary>Cuantas veces se ha pedido una lista de personas.</summary>
    public int VecesQueSePidioLaLista { get; private set; }

    /// <summary>Cuantas preguntas se le han hecho en total.</summary>
    public int PreguntasEnTotal { get; private set; }

    /// <summary>Pone los tres contadores a cero, para medir solo lo que viene despues.</summary>
    public void EmpezarDeCero()
    {
        VecesQueSePreguntoPorUnCaso = 0;
        VecesQueSePidioLaLista = 0;
        PreguntasEnTotal = 0;
    }

    /// <inheritdoc />
    public PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo)
    {
        VecesQueSePidioLaLista++;
        PreguntasEnTotal++;

        // Un filtro por caso dentro de `Listar` es la misma pregunta por fila con otro
        // nombre: se cuenta como tal para que no se cuele por la puerta de al lado.
        if (filtro?.CasoId is not null) VecesQueSePreguntoPorUnCaso++;
        return _deVerdad.Listar(filtro!, trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDePersonas filtro)
    {
        PreguntasEnTotal++;
        if (filtro?.CasoId is not null) VecesQueSePreguntoPorUnCaso++;
        return _deVerdad.Contar(filtro!);
    }

    /// <inheritdoc />
    public Persona? Obtener(long id)
    {
        PreguntasEnTotal++;
        return _deVerdad.Obtener(id);
    }

    /// <inheritdoc />
    public IReadOnlyList<Persona> DeCaso(long casoId)
    {
        VecesQueSePreguntoPorUnCaso++;
        PreguntasEnTotal++;
        return _deVerdad.DeCaso(casoId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Persona persona) => _deVerdad.Guardar(persona);

    /// <inheritdoc />
    public ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId)
        => _deVerdad.AnotarPropuesta(personaId, propuesta, companeroId);

    /// <inheritdoc />
    public ResultadoDeEscritura ResponderLosPasos(
        long personaId, RespuestaALosPasos respuesta, long companeroId, string origen)
        => _deVerdad.ResponderLosPasos(personaId, respuesta, companeroId, origen);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId)
        => _deVerdad.FirmasDeLosPasosDelCaso(casoId);

    /// <inheritdoc />
    /// <remarks>Reenvío sin contar ni espiar: entró el 2026-09-14 con <c>IPersonas.Borrar</c>, solo para que este doble siga compilando.</remarks>
    public ResultadoDeBorrado Borrar(long personaId) => _deVerdad.Borrar(personaId);
}
