using Fichas.Contratos.Consultas;

namespace Fichas.Datos.Falso;

/// <summary>Corta una lista ya filtrada en el trozo que se pidio, sin que nadie repita el calculo.</summary>
internal static class Trozos
{
    /// <summary>Devuelve el trozo pedido de una lista, con el total de la lista entera detras.</summary>
    public static PaginaDe<T> Cortar<T>(IReadOnlyList<T> todos, Pagina trozo)
    {
        var desde = Math.Clamp(trozo.Desde, 0, todos.Count);
        var tamano = trozo.Tamano <= 0 ? 0 : Math.Min(trozo.Tamano, todos.Count - desde);
        var elementos = new T[tamano];
        for (var i = 0; i < tamano; i++) elementos[i] = todos[desde + i];
        return new PaginaDe<T>(elementos, trozo, todos.Count);
    }

    /// <summary>Compara sin distinguir mayusculas ni acentos de mas; el texto vacio no filtra nada.</summary>
    public static bool Contiene(string? donde, string? que)
        => string.IsNullOrWhiteSpace(que)
           || (donde is not null && donde.Contains(que.Trim(), StringComparison.OrdinalIgnoreCase));
}
