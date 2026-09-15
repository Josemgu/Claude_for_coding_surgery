using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>
/// El arreglo de datos para una base que ya tiene el daño: que cada documento tenga su papel
/// y solo el suyo.
/// </summary>
/// <remarks>
/// <para>Existe por lo que se midió el 2026-09-15 en la base del dueño (en su máquina, solo
/// lectura, sin nombres): 76 casos, ninguna ruta compartida, pero <b>cuatro pares de casos
/// con el mismo archivo byte a byte</b> importados desde dos carpetas sin marca de duplicado,
/// y <b>nueve rutas que ya no existen</b> porque sus carpetas pasaron a llamarse «… Complete».
/// Y por lo que se reprodujo aquí con PDF sintéticos: un escáner que escribe siempre
/// <c>Scan.pdf</c> deja varios documentos apuntando a la misma hoja del mismo archivo con
/// números distintos, y el archivo solo puede ser de uno.</para>
///
/// <para>Son tres cosas y se tratan aparte, en tres pasos que la pantalla encadena:</para>
/// <list type="number">
/// <item><see cref="Planear"/> lee la base y pregunta si cada archivo existe, y dice qué hay:
/// <b>disputas</b> (misma hoja del mismo archivo, números distintos) y <b>papeles perdidos</b>
/// (la ruta no existe). No escribe nada ni lee ningún archivo: corre al llegar a Importar.</item>
/// <item><see cref="Leer"/> lee con OCR SOLO lo que hace falta para decidir —el número de caso
/// del archivo de cada disputa, y el del único candidato de cada papel perdido— y calcula la
/// huella de cada escaneo para encontrar los <b>pares idénticos</b> (mismo contenido y misma
/// hoja en dos rutas, sin marca de duplicado). Corre en otro hilo; no toca la base.</item>
/// <item><see cref="Aplicar"/> escribe, en el hilo de la base: quien no es del papel lo pierde
/// con su renglón, quien sí lo es se queda con él en su copia, el papel perdido se recupera si
/// su candidato dice su número, y el par idéntico se marca como duplicado del más antiguo.</item>
/// </list>
///
/// <para>⛔ <b>Nada se adivina</b> (regla permanente 1): si lo leído no identifica a uno de los
/// que disputan, si hay dos candidatos con el mismo nombre, o si el candidato dice otro número,
/// no se decide y se dice. Y es idempotente: la segunda pasada no encuentra nada que hacer.</para>
///
/// <para>⛔ <b>Aquí no hay ventana</b>: se prueba sin abrir ninguna, con un lector de mentira
/// que lee el rótulo de la hoja.</para>
/// </remarks>
public sealed partial class ComprobacionDeLosPapeles
{
    /// <summary>Por donde se leen y se escriben los casos.</summary>
    private readonly ICasos _casos;
    /// <summary>Por donde se escribe el renglón del papel perdido y el del duplicado marcado.</summary>
    private readonly IIlegibles _ilegibles;
    /// <summary>La fecha de hoy para los renglones.</summary>
    private readonly IReloj _reloj;
    /// <summary>Quien copia el papel que se conserva o se recupera a la carpeta de datos.</summary>
    private readonly CopiaDelEscaneo _copias;
    /// <summary>Quien lee el número de caso de una hoja de un archivo; nulo si no lo lee. Es el OCR, inyectado para poder probar sin él.</summary>
    private readonly Func<string, int, string?> _leerElNumero;

    /// <summary>Se ata a los puertos, a las copias y al lector del número.</summary>
    /// <param name="casos">Repositorio de casos.</param>
    /// <param name="ilegibles">Repositorio de renglones.</param>
    /// <param name="reloj">De dónde sale la fecha de hoy.</param>
    /// <param name="copias">Quien guarda copias en la carpeta de datos.</param>
    /// <param name="leerElNumero">Quien lee el número de caso de (ruta, hoja); en el programa es el lector de formularios.</param>
    public ComprobacionDeLosPapeles(
        ICasos casos, IIlegibles ilegibles, IReloj reloj, CopiaDelEscaneo copias, Func<string, int, string?> leerElNumero)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(ilegibles);
        ArgumentNullException.ThrowIfNull(reloj);
        ArgumentNullException.ThrowIfNull(copias);
        ArgumentNullException.ThrowIfNull(leerElNumero);

        _casos = casos;
        _ilegibles = ilegibles;
        _reloj = reloj;
        _copias = copias;
        _leerElNumero = leerElNumero;
    }

    /// <summary>Lee la base y pregunta al disco si cada archivo existe. No escribe nada y no lee ningún archivo.</summary>
    /// <remarks>
    /// Barato a propósito, porque corre al llegar a Importar: listar los casos y un
    /// <c>File.Exists</c> por cada uno. Las huellas de los pares idénticos se calculan en
    /// <see cref="Leer"/>, que corre en otro hilo: leer miles de archivos enteros no cabe al abrir.
    /// </remarks>
    /// <returns>Las disputas, los papeles perdidos y los casos con escaneo que quedan por mirar; todo vacío si no hay nada.</returns>
    public PlanDeLosPapeles Planear()
    {
        var conPapel = TodosLosCasos()
            .Where(caso => !string.IsNullOrWhiteSpace(caso.RutaPdf) && caso.PaginaPdf is not null)
            .ToList();

        var disputas = Disputas(conPapel);
        var enDisputa = disputas.SelectMany(disputa => disputa.Casos.Select(caso => caso.Id)).ToHashSet();
        var restantes = conPapel.Where(caso => !enDisputa.Contains(caso.Id)).ToList();

        var perdidos = restantes.Where(caso => !File.Exists(caso.RutaPdf!)).ToList();
        var existentes = restantes.Where(caso => File.Exists(caso.RutaPdf!)).ToList();

        return new PlanDeLosPapeles(disputas, perdidos, existentes, conPapel.Count);
    }

    /// <summary>Misma hoja del mismo papel con números de caso distintos: el archivo solo puede ser de uno.</summary>
    /// <param name="conPapel">Los casos con ruta y hoja.</param>
    private static List<PapelEnDisputa> Disputas(IReadOnlyList<Caso> conPapel)
        => conPapel
            .GroupBy(caso => (Papel: CopiaDelEscaneo.IdentidadDelPapel(caso.RutaPdf!).ToUpperInvariant(), Hoja: caso.PaginaPdf!.Value))
            .Where(grupo => grupo.Select(caso => caso.NumeroCaso).Where(numero => numero is not null).Distinct(StringComparer.Ordinal).Count() >= 2)
            .Select(grupo => new PapelEnDisputa(grupo.First().RutaPdf!, grupo.Key.Hoja, [.. grupo.OrderBy(caso => caso.Id)]))
            .ToList();

    /// <summary>
    /// Casos cuyo archivo es byte a byte el mismo y de la misma hoja, y que no están unidos por
    /// una marca de duplicado.
    /// </summary>
    /// <remarks>
    /// Se lee cada archivo entero para su huella: con los 76 del dueño son unos 50 MB; con
    /// miles, un rato, y por eso lo llama <see cref="Leer"/> en otro hilo y no <see cref="Planear"/>.
    /// Un archivo que no se deje leer se salta: no puede formar par.
    /// </remarks>
    /// <param name="existentes">Los casos cuyo archivo existe y no está en disputa.</param>
    private static List<ParDeIguales> ParesIdenticos(IReadOnlyList<Caso> existentes)
    {
        var huellas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ruta in existentes.Select(caso => caso.RutaPdf!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var huella = CopiaDelEscaneo.HuellaEnteraDe(ruta);
            if (huella is not null) huellas[ruta] = huella;
        }

        return existentes
            .Where(caso => huellas.ContainsKey(caso.RutaPdf!))
            .GroupBy(caso => (Huella: huellas[caso.RutaPdf!], Hoja: caso.PaginaPdf!.Value))
            .Select(grupo => grupo.OrderBy(caso => caso.Id).ToList())
            .Where(grupo => grupo.Count >= 2)
            .Select(grupo => new ParDeIguales(grupo[0], [.. grupo.Skip(1).Where(caso => caso.DuplicadoDe is null)]))
            .Where(par => par.Repetidos.Count > 0)
            .ToList();
    }

    /// <summary>Todos los casos, archivados incluidos, de 500 en 500.</summary>
    /// <remarks>El contrato no ofrece «los que tienen ruta»; se recorren todos, como hace el buscador de duplicados.</remarks>
    private IEnumerable<Caso> TodosLosCasos()
    {
        var trozo = Pagina.Primera(500);
        while (true)
        {
            var pagina = _casos.Listar(new FiltroDeCasos(IncluirArchivados: true), trozo);
            foreach (var caso in pagina.Elementos) yield return caso;
            if (!pagina.HayMas) yield break;
            trozo = trozo.Siguiente();
        }
    }
}

/// <summary>Una hoja de un archivo que reclaman varios documentos con números distintos.</summary>
/// <param name="RutaPdf">El archivo tal como lo guarda el primero de ellos.</param>
/// <param name="PaginaPdf">La hoja, base 1.</param>
/// <param name="Casos">Los que la reclaman, del más antiguo al más nuevo.</param>
public sealed record PapelEnDisputa(string RutaPdf, int PaginaPdf, IReadOnlyList<Caso> Casos);

/// <summary>Un papel repetido byte a byte: el original y los que lo repiten sin marca.</summary>
/// <param name="Original">El más antiguo, que es del que los demás son duplicados.</param>
/// <param name="Repetidos">Los demás, sin <c>duplicado_de</c>; los que ya lo tienen no están aquí.</param>
public sealed record ParDeIguales(Caso Original, IReadOnlyList<Caso> Repetidos);

/// <summary>Lo que hay que comprobar, tal como lo dejó <see cref="ComprobacionDeLosPapeles.Planear"/>.</summary>
/// <param name="Disputas">Las hojas reclamadas por varios números.</param>
/// <param name="Perdidos">Los casos cuyo archivo ya no está donde decían.</param>
/// <param name="ConEscaneo">Los casos cuyo archivo existe y no está en disputa; entre ellos se buscan los pares idénticos al leer.</param>
/// <param name="DocumentosConPapel">Cuántos casos tienen ruta y hoja, en disputa o no.</param>
public sealed record PlanDeLosPapeles(
    IReadOnlyList<PapelEnDisputa> Disputas,
    IReadOnlyList<Caso> Perdidos,
    IReadOnlyList<Caso> ConEscaneo,
    int DocumentosConPapel)
{
    /// <summary>Si hay algo que comprobar: una disputa, un perdido, o al menos dos escaneos que podrían ser el mismo.</summary>
    public bool HayAlgo => Disputas.Count > 0 || Perdidos.Count > 0 || ConEscaneo.Count >= 2;

    /// <summary>Lo que hay, en una línea, con sus tres cifras aunque sean cero.</summary>
    public string Linea => string.Join(" · ",
        Plural.Con(DocumentosConPapel, "documento con escaneo", "documentos con escaneo"),
        Plural.Con(Disputas.Count, "hoja que reclaman varios", "hojas que reclaman varios"),
        Plural.Con(Perdidos.Count, "escaneo que ya no está", "escaneos que ya no están"));
}
