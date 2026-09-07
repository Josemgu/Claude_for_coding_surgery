namespace Fichas.Contratos.Consultas;

/// <summary>Que trozo de una lista se pide. Todo lo que se lee se pide por trozos.</summary>
/// <remarks>
/// Requisito 5 del dueno: rapido con 3 000 documentos. Una interfaz de lectura que
/// devuelve «todo» obliga a la pantalla a pagar los 3 000 aunque quepan 20.
/// </remarks>
/// <param name="Desde">Cuantos elementos se saltan; 0 es el principio.</param>
/// <param name="Tamano">Cuantos se piden como maximo.</param>
public readonly record struct Pagina(int Desde, int Tamano)
{
    /// <summary>El primer trozo, del tamano que se diga.</summary>
    public static Pagina Primera(int tamano) => new(0, tamano);

    /// <summary>El trozo siguiente a este, del mismo tamano.</summary>
    public Pagina Siguiente() => new(Desde + Tamano, Tamano);
}

/// <summary>Un trozo de lista con el total disponible detras, para poder decir «N de M».</summary>
/// <typeparam name="T">Que hay dentro de la lista.</typeparam>
/// <param name="Elementos">Los elementos de este trozo.</param>
/// <param name="Trozo">Que trozo se pidio.</param>
/// <param name="TotalDisponible">Cuantos hay en total detras del filtro, no solo en el trozo.</param>
public sealed record PaginaDe<T>(
    IReadOnlyList<T> Elementos,
    Pagina Trozo,
    int TotalDisponible)
{
    /// <summary>Un trozo vacio, que es lo que se devuelve cuando no se puede leer nada.</summary>
    public static PaginaDe<T> Vacia(Pagina trozo) => new(Array.Empty<T>(), trozo, 0);

    /// <summary>Si detras de este trozo queda algo mas.</summary>
    public bool HayMas => Trozo.Desde + Elementos.Count < TotalDisponible;
}
