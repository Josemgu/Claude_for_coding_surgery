using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Paquetes;

/// <summary>
/// Contesta «¿qué casos llevan este número?» durante UNA vuelta del Excel, leyendo los casos
/// una sola vez.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe.</b> Una hoja generada antes del 2026-09-03 vuelve sin el id del
/// caso en la clave, y cada fila se resuelve por <c>numero_caso</c> + <c>mrn</c>. Hasta el
/// 2026-09-15 cada fila lanzaba <c>Listar(Texto: número)</c> —300 listas para 300 filas, medidas
/// con un contador; contra SQLite, dos consultas con <c>LIKE</c> sobre casos y personas cada
/// una—. Aquí los casos se leen de una vez, la primera vez que hacen falta, y el número se
/// busca en un diccionario. Con el contrato congelado no hay una consulta por igualdad en
/// <see cref="ICasos"/>, así que la igualdad se hace en memoria (R-7 del plan del 2026-09-15).</para>
///
/// <para><b>Lo que no cambia.</b> Los archivados entran, porque el compañero pudo recibir el
/// caso antes de que se archivara; y el número se compara ENTERO y sin distinguir mayúsculas,
/// que es lo que el código de antes hacía letra por letra después del filtro de texto. Un
/// filtro que sobra se recortaba; uno que falta no se podía recuperar. Ahora no hay filtro.</para>
///
/// <para><b>Se lee tarde a propósito.</b> Una hoja con id en todas sus filas nunca pregunta
/// por un número, y entonces no se lee ningún caso: el camino nuevo no paga nada por el
/// viejo. Y se lee UNA vez por vuelta: un objeto de estos vive lo que dura leer o aplicar un
/// Excel, no más, para que un caso dado de alta entre dos vueltas se vea en la segunda.</para>
/// </remarks>
internal sealed class CasosPorNumero
{
    /// <summary>La página que lo pide todo de una vez; los puertos paginan siempre.</summary>
    private static readonly Pagina Entera = new(0, int.MaxValue);

    /// <summary>De dónde se leen los casos, archivados incluidos.</summary>
    private readonly ICasos _casos;

    /// <summary>Los casos por número, sin distinguir mayúsculas; nulo hasta que alguien pregunta.</summary>
    private Dictionary<string, List<Caso>>? _porNumero;

    /// <summary>Se ata al puerto; no lee nada todavía.</summary>
    /// <param name="casos">El puerto de casos.</param>
    public CasosPorNumero(ICasos casos) => _casos = casos;

    /// <summary>Los casos que llevan ese número, archivados incluidos; vacío si ninguno.</summary>
    /// <param name="numeroCaso">El número tal como vino en la clave; se compara entero y sin distinguir mayúsculas.</param>
    public IReadOnlyList<Caso> ConEseNumero(string numeroCaso)
    {
        _porNumero ??= LeerTodosPorNumero();
        return _porNumero.TryGetValue(numeroCaso, out var suyos) ? suyos : [];
    }

    /// <summary>Lee todos los casos de una vez y los agrupa por número; los que no tienen número no entran.</summary>
    private Dictionary<string, List<Caso>> LeerTodosPorNumero()
    {
        var porNumero = new Dictionary<string, List<Caso>>(StringComparer.OrdinalIgnoreCase);
        foreach (var caso in _casos.Listar(new FiltroDeCasos(IncluirArchivados: true), Entera).Elementos)
        {
            if (caso.NumeroCaso is null) continue;
            if (!porNumero.TryGetValue(caso.NumeroCaso, out var suyos))
            {
                suyos = [];
                porNumero[caso.NumeroCaso] = suyos;
            }

            suyos.Add(caso);
        }

        return porNumero;
    }
}
