namespace Fichas.Datos.Falso;

/// <summary>
/// Un sorteo de numeros que sale SIEMPRE igual con la misma semilla.
/// </summary>
/// <remarks>
/// No se usa <c>System.Random</c> a proposito: su sucesion con semilla no esta garantizada
/// entre versiones del entorno, y entonces una prueba que compara dos generaciones dejaria
/// de valer al actualizar .NET sin que nadie tocara nada. Esto es un xorshift de 32 bits,
/// veinte lineas y sin sorpresas. No sirve para nada criptografico y no lo pretende.
/// </remarks>
public sealed class SorteoDeterminista
{
    /// <summary>El estado del xorshift; nunca vale cero, porque el cero se queda pegado.</summary>
    private uint _estado;

    /// <summary>Arranca el sorteo con su semilla; la misma semilla da la misma sucesion.</summary>
    /// <param name="semilla">Cualquier entero; el cero se cambia por una constante para que el sorteo no se quede quieto.</param>
    public SorteoDeterminista(int semilla)
    {
        // El cero se queda pegado en el cero con xorshift; se cambia por una constante.
        _estado = semilla == 0 ? 0x9E3779B9u : unchecked((uint)semilla);
    }

    /// <summary>Devuelve el siguiente entero sin signo de la sucesion.</summary>
    public uint Siguiente()
    {
        _estado ^= _estado << 13;
        _estado ^= _estado >> 17;
        _estado ^= _estado << 5;
        return _estado;
    }

    /// <summary>Devuelve un entero entre 0 (incluido) y el tope (excluido).</summary>
    /// <param name="tope">El tope; cero o negativo da siempre 0.</param>
    public int Hasta(int tope) => tope <= 0 ? 0 : (int)(Siguiente() % (uint)tope);

    /// <summary>Devuelve un entero entre dos limites, el de abajo incluido y el de arriba excluido.</summary>
    /// <param name="desde">El de abajo, incluido.</param>
    /// <param name="hasta">El de arriba, excluido; si no es mayor que <paramref name="desde"/>, devuelve <paramref name="desde"/>.</param>
    public int Entre(int desde, int hasta) => desde + Hasta(hasta - desde);

    /// <summary>Devuelve verdadero con la probabilidad que se pida, dada en porcentaje entero.</summary>
    /// <param name="porCiento">De 0 (nunca) a 100 (siempre).</param>
    public bool ConProbabilidad(int porCiento) => Hasta(100) < porCiento;

    /// <summary>Elige un elemento de una lista; la lista vacia devuelve el valor por defecto.</summary>
    /// <typeparam name="T">El tipo de los elementos.</typeparam>
    /// <param name="lista">De dónde se elige.</param>
    public T Elegir<T>(IReadOnlyList<T> lista) => lista.Count == 0 ? default! : lista[Hasta(lista.Count)];
}
