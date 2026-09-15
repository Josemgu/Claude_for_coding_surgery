using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Importar;

/// <summary>
/// De que caso ya guardado repite una hoja, cuando repite a alguno.
/// </summary>
/// <remarks>
/// Portado de <c>importacion/guardado.py</c>. ⛔ Encontrar un duplicado NO es rechazarlo:
/// el dueno lo dijo el 2026-09-03, «si hay documentos duplicados debe decirlo y no
/// rechazarlo». Esta clase solo contesta «¿de cual?»; quien guarda lo marca y sigue.
///
/// <para>Dos vias, en este orden:</para>
/// <list type="number">
/// <item><b>MRN en comun dentro del mismo numero de caso.</b> El numero solo dice unidad
/// y mes —el dueno tiene siete documentos con `CASP2609`, de siete familias distintas—,
/// pero la cedula es de UNA persona. Es la senal que Miguel puede comprobar mirando el
/// papel.</item>
/// <item><b>La misma hoja del mismo archivo.</b> Es la red de abajo, para el archivo
/// repetido cuyos MRN el lector no pudo leer.</item>
/// </list>
///
/// <para>⚠️ Lo que NO atrapa, dicho para que nadie lo descubra despues: un archivo
/// copiado a otra ruta, renombrado y SIN ningun MRN legible entra como caso nuevo y sin
/// marca. La base no guarda ninguna huella del contenido del archivo y este pase no la
/// anade.</para>
/// </remarks>
public sealed class BuscadorDeDuplicados
{
    /// <summary>Por donde se listan los casos guardados.</summary>
    private readonly ICasos _casos;
    /// <summary>Por donde se leen las personas de un caso, para comparar cédulas.</summary>
    private readonly IPersonas _personas;

    // El indice de (ruta, pagina) -> caso se construye PEREZOSAMENTE y una sola vez por
    // tanda, no por documento. El contrato congelado no ofrece filtrar casos por ruta de
    // PDF, asi que la unica forma es recorrerlos; hacerlo por documento serian 500
    // recorridos de 3 000 casos. Perezoso, ademas, casi nunca se construye: solo hace
    // falta cuando una hoja no trajo NI UN MRN legible.
    /// <summary>El índice (ruta|página) → id del caso más antiguo que salió de esa hoja; nulo hasta que haga falta.</summary>
    private Dictionary<string, long>? _porHoja;

    /// <summary>Se ata a los dos repositorios que necesita para preguntar.</summary>
    /// <param name="casos">Repositorio de casos.</param>
    /// <param name="personas">Repositorio de personas.</param>
    public BuscadorDeDuplicados(ICasos casos, IPersonas personas)
    {
        _casos = casos;
        _personas = personas;
    }

    /// <summary>El id del caso que esta hoja repite, o nulo si no repite a ninguno.</summary>
    /// <param name="numeroCaso">El número de caso leído en la hoja, o nulo.</param>
    /// <param name="mrnDeLaHoja">Las cédulas legibles de la hoja; vacía salta la primera vía.</param>
    /// <param name="rutaPdf">El archivo del que salió la hoja.</param>
    /// <param name="paginaPdf">La página del PDF, base 1.</param>
    public long? CasoDelQueEsDuplicado(string? numeroCaso, IReadOnlyList<string> mrnDeLaHoja, string rutaPdf, int paginaPdf)
    {
        ArgumentNullException.ThrowIfNull(mrnDeLaHoja);

        var porMrn = CasoQueComparteMrn(numeroCaso, mrnDeLaHoja);
        return porMrn ?? CasoDeLaMismaHoja(rutaPdf, paginaPdf);
    }

    /// <summary>
    /// Apunta un caso que acaba de nacer en ESTA tanda, para que la siguiente hoja igual lo
    /// encuentre.
    /// </summary>
    /// <remarks>
    /// ⚠️ Sin esto, dos copias de la misma ficha dentro de la MISMA tanda no se marcaban:
    /// el indice se construye una vez por tanda, antes de que nazca ninguno de sus casos, y
    /// la segunda copia buscaba a la primera en un indice que no la tenia. Es lo que dejo
    /// cuatro pares sin marca en la base del dueño el 2026-09-14 (medido en su maquina el
    /// 2026-09-15: misma ficha en «HAITI OCtubre\» y en «Octubre\», importadas en una sola
    /// tanda de 66 documentos). Si el indice todavia no se construyo, no hay nada que
    /// apuntar: se construira despues y ya vera el caso en la base.
    /// </remarks>
    /// <param name="rutaPdf">La ruta que guardo el caso.</param>
    /// <param name="paginaPdf">La hoja, base 1; cero o menos no se apunta.</param>
    /// <param name="casoId">El caso recién nacido.</param>
    public void Recordar(string rutaPdf, int paginaPdf, long casoId)
    {
        if (_porHoja is null || string.IsNullOrWhiteSpace(rutaPdf) || paginaPdf < 1) return;
        _porHoja.TryAdd(Clave(rutaPdf, paginaPdf), casoId);
    }

    /// <summary>Se olvida de lo que tenia apuntado; se llama al empezar cada tanda.</summary>
    /// <remarks>
    /// Sin esto, el indice de una tanda anterior no veria los casos que acaba de crear
    /// esta, y un archivo importado tres veces solo se marcaria la segunda.
    /// </remarks>
    public void Olvidar() => _porHoja = null;

    /// <summary>El caso mas antiguo con ese numero que comparta alguna cedula, o nulo.</summary>
    /// <param name="numeroCaso">El número de caso leído; nulo devuelve nulo.</param>
    /// <param name="mrnDeLaHoja">Las cédulas de la hoja; vacía devuelve nulo.</param>
    private long? CasoQueComparteMrn(string? numeroCaso, IReadOnlyList<string> mrnDeLaHoja)
    {
        if (numeroCaso is null || mrnDeLaHoja.Count == 0) return null;

        var buscados = new HashSet<string>(mrnDeLaHoja, StringComparer.Ordinal);

        foreach (var caso in CasosConEseNumero(numeroCaso))
        {
            if (_personas.DeCaso(caso.Id).Any(persona => persona.Mrn is not null && buscados.Contains(persona.Mrn)))
            {
                return caso.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// Los casos guardados que llevan ese numero, del mas antiguo al mas nuevo.
    /// </summary>
    /// <remarks>
    /// El filtro de texto busca por numero, nombre y MRN, asi que puede traer de mas: se
    /// afina aqui comparando la columna. Traer de mas y filtrar es correcto; armar el SQL
    /// a mano seria mas rapido y esta prohibido (<c>pruebas/auditoria_sql.py</c>).
    /// </remarks>
    /// <param name="numeroCaso">El número exacto; la comparación es ordinal.</param>
    private IEnumerable<Caso> CasosConEseNumero(string numeroCaso)
        => _casos
            .Listar(new FiltroDeCasos(Texto: numeroCaso, IncluirArchivados: true), Pagina.Primera(500))
            .Elementos
            .Where(caso => string.Equals(caso.NumeroCaso, numeroCaso, StringComparison.Ordinal))
            .OrderBy(caso => caso.Id);

    /// <summary>El caso que ya salio de ESTA hoja de ESTE archivo, o nulo.</summary>
    /// <param name="rutaPdf">El archivo; vacío devuelve nulo sin construir el índice.</param>
    /// <param name="paginaPdf">La página del PDF, base 1.</param>
    private long? CasoDeLaMismaHoja(string rutaPdf, int paginaPdf)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf)) return null;
        _porHoja ??= ConstruirElIndicePorHoja();
        return _porHoja.TryGetValue(Clave(rutaPdf, paginaPdf), out var casoId) ? casoId : null;
    }

    /// <summary>Recorre los casos una vez y apunta que hoja de que archivo abrio cada uno.</summary>
    private Dictionary<string, long> ConstruirElIndicePorHoja()
    {
        var indice = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var trozo = Pagina.Primera(500);

        while (true)
        {
            var pagina = _casos.Listar(new FiltroDeCasos(IncluirArchivados: true), trozo);
            foreach (var caso in pagina.Elementos)
            {
                if (caso.RutaPdf is null || caso.PaginaPdf is null) continue;
                // El primero gana: es el mas antiguo, que es del que los demas son duplicados.
                indice.TryAdd(Clave(caso.RutaPdf, caso.PaginaPdf.Value), caso.Id);
            }

            if (!pagina.HayMas) return indice;
            trozo = trozo.Siguiente();
        }
    }

    /// <summary>La clave del índice por hoja: lo que identifica al papel y la página, separados por una barra vertical.</summary>
    /// <remarks>
    /// Desde el 2026-09-15 el papel se identifica por <see cref="CopiaDelEscaneo.IdentidadDelPapel"/>:
    /// la huella del contenido cuando la ruta es una copia, y la ruta entera cuando no. Así la
    /// misma ficha importada desde dos carpetas con otro nombre es la misma hoja del mismo papel.
    /// </remarks>
    /// <param name="rutaPdf">El archivo.</param>
    /// <param name="paginaPdf">La página del PDF, base 1.</param>
    private static string Clave(string rutaPdf, int paginaPdf) => $"{CopiaDelEscaneo.IdentidadDelPapel(rutaPdf)}|{paginaPdf}";
}
