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
    private readonly IReadOnlyDictionary<long, string> _porCaso;

    private LoQueLeFaltaACadaDocumento(IReadOnlyDictionary<long, string> porCaso) => _porCaso = porCaso;

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

        var porCaso = new Dictionary<long, string>(todos.Count);
        foreach (var caso in todos)
        {
            var gente = suyas.GetValueOrDefault(caso.Id) ?? [];
            porCaso[caso.Id] = LasDosPreguntas.LoQueLeFaltaAlDocumento(
                LoQueLeFalta.DeUnDocumento(caso, gente, procedencias).Count,
                gente.Count == 0);
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
    public string De(long casoId) => _porCaso.GetValueOrDefault(casoId, string.Empty);

    /// <summary>Las personas repartidas por documento, en UNA consulta.</summary>
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
