using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Correccion;

/// <summary>
/// Lo que le falta a CADA documento de la base, leido de una sola pasada.
/// </summary>
/// <remarks>
/// <para><b>Para que.</b> Trabajar por grupo solo sirve si al elegir un grupo se ve cuales de
/// sus documentos siguen pidiendo algo. Si hay que abrirlos uno a uno para saberlo, el grupo
/// no es un grupo de trabajo: es otra lista.</para>
///
/// <para>⛔ <b>El veredicto NO se decide aqui.</b> Lo da <see cref="LoQueLeFalta"/>, que es la
/// misma funcion que usan la cola de Completar y la pantalla del grupo, y la frase la compone
/// <see cref="LasDosPreguntas.LoQueLeFaltaAlDocumento"/>. Con una sola regla y una sola frase,
/// el mismo documento no puede leerse distinto segun la pantalla desde la que se mire; con
/// dos, <c>DECISIONES.md</c> midio el 2026-09-06 <b>seis</b> casos contestados al reves.</para>
///
/// <para>⚠️ <b>Tres consultas en bloque y ni una por documento.</b> Preguntar la procedencia
/// documento a documento cuesta <b>4,4 s</b> con las 18 000 lecturas reales y <b>50 ms</b> en
/// bloque, medido por otros dos programadores; lo mismo con las personas. Con 3 000 documentos
/// la pasada entera cabe en 200 ms, y eso lo fija
/// <c>PruebasDeLoQueLeFaltaACadaDocumento.ConTresMilDocumentosLaPasadaEnteraCabeEnDosDecimas</c>.</para>
///
/// <para>⛔ De aqui no sale ni una escritura: es una lectura, y la regla permanente 5 sigue
/// entera.</para>
/// </remarks>
public sealed class LoQueLeFaltaACadaDocumento
{
    /// <summary>La respuesta de cada documento, por id de caso; un archivado no esta.</summary>
    private readonly IReadOnlyDictionary<long, Lectura> _porCaso;

    /// <summary>Solo se construye desde <see cref="DeTodaLaBase"/>: la pasada es la unica forma de llenarlo.</summary>
    /// <param name="porCaso">Lo ya contestado, por id de caso.</param>
    private LoQueLeFaltaACadaDocumento(IReadOnlyDictionary<long, Lectura> porCaso) => _porCaso = porCaso;

    /// <summary>Lo contestado de un documento: la frase que se lee y si le falta algo.</summary>
    /// <remarks>
    /// Las dos cosas van juntas y salen de la MISMA llamada a <see cref="LoQueLeFalta"/>. Si la
    /// frase se compusiera aqui y el booleano se recalculara en otro sitio, volveria a haber dos
    /// veredictos sobre el mismo documento, que es el agujero del 2026-09-06.
    /// </remarks>
    /// <param name="Frase">Que le falta, en palabras.</param>
    /// <param name="LeFalta">Si queda algo que hacer con el.</param>
    private readonly record struct Lectura(string Frase, bool LeFalta);

    /// <summary>Lee la base entera y deja contestada la pregunta de cada documento.</summary>
    /// <param name="casos">Los documentos.</param>
    /// <param name="personas">Las personas de cada documento.</param>
    /// <param name="procedencia">De donde salio cada campo.</param>
    public static LoQueLeFaltaACadaDocumento DeTodaLaBase(
        ICasos casos, IPersonas personas, IProcedencia procedencia)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(procedencia);

        var todos = casos.Listar(FiltroDeCasos.Todo, new Pagina(0, int.MaxValue)).Elementos;
        var suyas = PersonasPorCaso(personas);
        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(procedencia);

        var porCaso = new Dictionary<long, Lectura>(todos.Count);
        foreach (var caso in todos)
        {
            var gente = suyas.GetValueOrDefault(caso.Id) ?? [];
            var cuanto = LoQueLeFalta.DeUnDocumento(caso, gente, procedencias).Count;
            porCaso[caso.Id] = new Lectura(
                LasDosPreguntas.LoQueLeFaltaAlDocumento(cuanto, gente.Count == 0),
                cuanto > 0);
        }

        return new LoQueLeFaltaACadaDocumento(porCaso);
    }

    /// <summary>
    /// Que le falta a ese documento, en palabras; vacio si ese documento no esta en la base.
    /// </summary>
    /// <remarks>
    /// El vacio no es un descuido: pasa con un documento archivado, que no entra en esta
    /// pasada. Devolver «listo para asignar» de algo que no se ha mirado seria afirmar lo que
    /// no consta.
    /// </remarks>
    /// <param name="casoId">El documento.</param>
    public string De(long casoId) => _porCaso.GetValueOrDefault(casoId).Frase ?? string.Empty;

    /// <summary>
    /// Si a ese documento le queda algo que hacer; falso tambien si ese documento no esta en
    /// esta pasada.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>No es un veredicto nuevo</b>, y eso es lo importante: es el mismo
    /// <see cref="LoQueLeFalta.EstaListo"/> que ya se calculo arriba para componer la frase,
    /// solo que dicho en una palabra en vez de en una. Con un segundo criterio volveria el
    /// agujero que <c>DECISIONES.md</c> midio el 2026-09-06: documentos que se quedaban fuera de
    /// todas las listas porque una pantalla los daba por resueltos y otra no.</para>
    ///
    /// <para>⚠️ <b>El falso de un documento que no esta aqui no es un descuido.</b> Un archivado
    /// no entra en esta pasada, y archivar es el gesto con el que el dueno cierra un documento
    /// (2026-09-06): no le queda nada que hacer con el, asi que tampoco entra en Correccion.</para>
    ///
    /// <para>Es la respuesta con la que Correccion decide quien entra y quien sale, que es la
    /// decision del dueno del 2026-09-07 (<c>DECISIONES.md</c>, «Corrección es un sitio de
    /// paso, no un almacén»): verdadero entra, falso sale.</para>
    /// </remarks>
    /// <param name="casoId">El documento.</param>
    public bool LeFaltaAlgo(long casoId) => _porCaso.GetValueOrDefault(casoId).LeFalta;

    /// <summary>Las personas repartidas por documento, en UNA consulta.</summary>
    /// <remarks>Se cuenta primero para pedir la pagina justa; con cero no se pide nada.</remarks>
    /// <param name="personas">El almacen de personas.</param>
    private static Dictionary<long, List<Persona>> PersonasPorCaso(IPersonas personas)
    {
        var cuantas = personas.Contar(FiltroDePersonas.Todo);
        var porCaso = new Dictionary<long, List<Persona>>();
        if (cuantas == 0) return porCaso;

        foreach (var persona in personas.Listar(FiltroDePersonas.Todo, new Pagina(0, cuantas)).Elementos)
        {
            if (!porCaso.TryGetValue(persona.CasoId, out var lista))
            {
                lista = [];
                porCaso[persona.CasoId] = lista;
            }

            lista.Add(persona);
        }

        return porCaso;
    }
}
