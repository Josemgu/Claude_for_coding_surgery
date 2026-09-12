using Fichas.Contratos.Consultas;

namespace Fichas.Datos.Falso;

/// <summary>Corta una lista ya filtrada en el trozo que se pidio, sin que nadie repita el calculo.</summary>
internal static class Trozos
{
    /// <summary>Devuelve el trozo pedido de una lista, con el total de la lista entera detras.</summary>
    /// <typeparam name="T">El tipo de los elementos.</typeparam>
    /// <param name="todos">La lista entera, ya filtrada y ordenada.</param>
    /// <param name="trozo">Desde dónde y cuántos; se acota a lo que hay, nunca lanza.</param>
    public static PaginaDe<T> Cortar<T>(IReadOnlyList<T> todos, Pagina trozo)
    {
        var desde = Math.Clamp(trozo.Desde, 0, todos.Count);
        var tamano = trozo.Tamano <= 0 ? 0 : Math.Min(trozo.Tamano, todos.Count - desde);
        var elementos = new T[tamano];
        for (var i = 0; i < tamano; i++) elementos[i] = todos[desde + i];
        return new PaginaDe<T>(elementos, trozo, todos.Count);
    }

    /// <summary>Compara sin distinguir mayusculas; el texto vacio no filtra nada.</summary>
    /// <remarks>
    /// ⚠️ NO ignora los acentos: <c>OrdinalIgnoreCase</c> distingue «Jose» de «José». Hasta el
    /// 2026-09-11 este comentario decía «ni acentos de mas», y no era verdad; el código no se toca
    /// aquí, se apunta en la entrega.
    /// </remarks>
    /// <param name="donde">El texto en el que se busca; nulo no contiene nada.</param>
    /// <param name="que">Lo buscado, recortado de espacios; nulo o en blanco da verdadero.</param>
    public static bool Contiene(string? donde, string? que)
        => string.IsNullOrWhiteSpace(que)
           || (donde is not null && donde.Contains(que.Trim(), StringComparison.OrdinalIgnoreCase));
}
