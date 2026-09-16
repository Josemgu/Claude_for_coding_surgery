using Fichas.App.Inicio;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Grupo;

/// <summary>
/// Arma el grupo que viaja un dia hablando SOLO con los contratos, para que se pueda
/// probar sin abrir ventana (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Un documento archivado ENTRA en el grupo de su fecha desde el 2026-09-14</b>, en
/// verde y con su nota de archivado, igual que ya contaba en el calendario desde el 2026-09-07
/// (<i>«aunque se archive, debe quedarse en el calendario marcado en verde»</i>). Hasta hoy
/// contaba fuera y no entraba dentro —2026-09-06, <i>«no aparecer más en ningún lado»</i>—, y
/// eso se midio: un dia decia fuera «me falta 4 de 5» y dentro «me falta 4 de 4», y un dia
/// resuelto solo por archivados se abria vacio. Se le pregunto al dueno con las dos opciones
/// —(A) entra en verde, (B) el calendario deja de contarlo— y contesto «Dale» sin elegir; el
/// supervisor decidio la A y la dejo escrita en <c>DECISIONES.md</c> para que el la cambie si
/// quiere. <b>Lo que sigue en pie del 06:</b> Flujo, Correccion, Asignar y la ventana de
/// incompletos siguen sin archivados, y desde el grupo un archivado ni se asigna ni se
/// verifica (<see cref="UnidadDelGrupo.CasosQueSePuedenAsignar"/>,
/// <see cref="RenglonDelGrupo.SePuedeVerificar"/>). Verlos y desarchivarlos sigue siendo cosa
/// de Revisar, con «Ver los archivados».</para>
///
/// <para><b>El grupo es una consulta, no una tabla</b> (ADR-0005 §2.4): son todos los
/// documentos que comparten fecha de viaje. Nadie mantiene nada, y el dia
/// que el OCR corrija una fecha el documento cambia de grupo solo. Eso arregla ademas un
/// caso real: uno de los siete escaneos del dueno lleva escrito <c>CASD2609</c> en vez de
/// <c>CASP2609</c>, y agrupando por fecha aparece con sus hermanos aunque su numero este
/// mal.</para>
///
/// <para>⚠️ <b>El filtro por fecha exacta se hace aqui y no en la base, y es una limitacion
/// del pase, no una eleccion.</b> El criterio C11-1 pide que <c>FiltroDeCasos</c> gane la
/// fecha de viaje exacta, y ese registro vive en <c>Fichas.Contratos</c>, que esta
/// congelado y lo esta tocando otro programador en este mismo ciclo. Asi que se trae la
/// lista de casos —que es lo que la pantalla de Inicio ya hacia desde el primer dia— y se
/// parte aqui. Lo que cuesta esta medido en la entrega. El dia que el filtro exista, este
/// archivo cambia dos lineas y ni las pruebas ni la pantalla se enteran.</para>
/// </remarks>
public sealed class LectorDeGrupos
{
    /// <summary>De dónde se leen los documentos, en una sola consulta por pintado.</summary>
    private readonly ICasos _casos;
    /// <summary>De dónde se leen las personas, en una sola consulta por pintado.</summary>
    private readonly IPersonas _personas;
    /// <summary>De dónde se lee quién lleva cada documento.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>De dónde salen los nombres de quienes llevan cada documento.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>De dónde salió cada campo; se lee en bloque una vez por día.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>Cómo se comprueba que el PDF está en su ruta; el disco de verdad salvo que la prueba pase otra cosa.</summary>
    private readonly Func<string, bool> _elArchivoExiste;

    /// <summary>Se ata a los cinco puertos que hacen falta para armar un grupo.</summary>
    /// <param name="casos">Los documentos.</param>
    /// <param name="personas">Las personas de cada documento.</param>
    /// <param name="asignaciones">Quien lleva cada documento ahora mismo.</param>
    /// <param name="companeros">Los nombres de quienes lo llevan.</param>
    /// <param name="procedencia">
    /// De donde salio cada campo. Entro el 2026-09-06: sin ella, esta pantalla decia que a un
    /// documento no le faltaba nada mirando solo si sus campos estaban vacios, y Correccion
    /// decia lo contrario del mismo documento.
    /// </param>
    /// <param name="elArchivoExiste">
    /// Como se comprueba que el PDF esta donde dice; se pasa desde fuera para que la prueba
    /// no dependa del disco de nadie. Por defecto, el disco de verdad.
    /// </param>
    public LectorDeGrupos(
        ICasos casos,
        IPersonas personas,
        IAsignaciones asignaciones,
        ICompaneros companeros,
        IProcedencia procedencia,
        Func<string, bool>? elArchivoExiste = null)
    {
        _casos = casos;
        _personas = personas;
        _asignaciones = asignaciones;
        _companeros = companeros;
        _procedencia = procedencia;
        _elArchivoExiste = elArchivoExiste ?? File.Exists;
    }

    /// <summary>
    /// El grupo que viaja ese dia: sus unidades, sus documentos y sus personas.
    /// </summary>
    /// <param name="fecha">El dia del viaje.</param>
    public GrupoDelDia DelDia(DateOnly fecha)
    {
        var todos = LeerLosCasosConLosArchivados();
        var delDia = todos.Where(c => FechasEnEspanol.Leer(c.FechaViaje) == fecha).ToList();
        if (delDia.Count == 0) return GrupoDelDia.Vacio(fecha);

        var personasPorCaso = AgruparLasPersonas(delDia);
        var duenos = LeerLosDuenos();

        // La procedencia se lee UNA vez para el dia entero, con las dos consultas en bloque.
        // Preguntarla documento a documento cuesta 4,4 s con las 18 000 lecturas reales.
        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(_procedencia);

        var unidades = delDia
            .GroupBy(ClaveDeUnidad, StringComparer.Ordinal)
            .Select(grupo => ArmarLaUnidad(fecha, grupo, personasPorCaso, duenos, procedencias))
            .OrderByDescending(u => u.CuantosDocumentos)
            .ThenBy(u => u.Titulo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GrupoDelDia(
            fecha,
            unidades,
            delDia.Count,
            unidades.Sum(u => u.Personas.Count),
            delDia.Count(EstaCompleto),
            MotivoQueMasSeRepite(delDia));
    }

    /// <summary>
    /// Los grupos de cada dia de un mes, para pintar el calendario.
    /// </summary>
    /// <remarks>
    /// Devuelve TODOS los dias que tienen algo, no solo los del mes que se ensena: el
    /// calendario trae 42 celdas y las de los bordes son de los meses de al lado, y si esas
    /// salieran vacias el dueno veria un mes con agujeros que no son reales.
    /// </remarks>
    /// <param name="personasYaLeidas">
    /// Las personas de cada documento, si quien llama ya las tiene. Se pasan desde fuera para
    /// no leer la tabla dos veces en el mismo pintado: Inicio las necesita ademas para los
    /// tickets, y medido sobre 3 000 documentos y 16 500 personas, leerlas dos veces costaba
    /// <b>362 ms</b> contra los <b>≤ 200 ms</b> que exige el criterio C20-5.
    /// </param>
    public IReadOnlyDictionary<DateOnly, List<PastillaDeDia>> PorDia(
        IReadOnlyDictionary<long, List<Persona>>? personasYaLeidas = null)
    {
        var todos = LeerLosCasosConLosArchivados();

        // ⚠️ Antes se pedia solo CUANTAS personas hay (ICasos.ContarPersonasDe). Desde el
        // criterio C20-3 la pastilla dice cuantas de ellas tienen la recomendacion
        // CONFIRMADA, y para saberlo hay que mirar sus seis preguntas, o sea la persona
        // entera. Sigue siendo UNA sola consulta —la misma que ya hacia DelDia—, y no una
        // por documento: con 3 000 casos eso serian 3 000 idas a la base para pintar un mes.
        var personasPorCaso = personasYaLeidas ?? AgruparLasPersonas(todos);

        var porDia = new Dictionary<DateOnly, List<PastillaDeDia>>();

        foreach (var delDia in todos.GroupBy(c => FechasEnEspanol.Leer(c.FechaViaje)))
        {
            if (delDia.Key is not DateOnly fecha) continue;

            porDia[fecha] = [.. delDia
                .GroupBy(ClaveDeUnidad, StringComparer.Ordinal)
                .Select(unidad => ArmarLaPastilla(unidad, personasPorCaso))
                .OrderByDescending(p => p.CuantosDocumentos)
                .ThenBy(p => p.Titulo, StringComparer.OrdinalIgnoreCase)];
        }

        return porDia;
    }

    /// <summary>
    /// Por que un documento no esta completo.
    /// </summary>
    /// <remarks>
    /// <para>Manda <c>motivo_no_completa</c> —el que vale ahora— y, si nadie lo ha escrito,
    /// se cae a <c>motivo_del_companero</c>, que es lo que dijo su hoja. Es la misma
    /// precedencia que ya tiene el estado desde la migracion 14, y por el mismo motivo: lo
    /// que dijo el agente NO se borra cuando Miguel corrige encima (ADR-0005 §3.3).</para>
    ///
    /// <para>⚠️ <b>Lo que todavia NO existe es quien escriba esas columnas.</b> Las dos
    /// entraron con la migracion 18 y hoy vienen nulas en toda la base: quien las llena es
    /// la vuelta del Excel del companero (fase C14), que aun no esta hecha. O sea que en
    /// pantalla se lee «no completado» hasta que esa fase cierre, y no porque aqui falte
    /// nada.</para>
    /// </remarks>
    /// <param name="caso">El documento; no puede ser nulo.</param>
    public static MotivoDeNoCompletar MotivoDe(Caso caso)
    {
        ArgumentNullException.ThrowIfNull(caso);
        return caso.Motivo != MotivoDeNoCompletar.SinMotivo ? caso.Motivo : caso.MotivoQueDijoElCompanero;
    }

    /// <summary>
    /// El motivo que mas se repite entre los que no estan completos, y en empate el primero
    /// de un orden declarado.
    /// </summary>
    /// <remarks>
    /// Es el criterio C11-4 tal como esta escrito, y el orden del desempate va declarado a
    /// proposito: «el mas grave» seria una opinion y dos personas lo leerian distinto.
    /// </remarks>
    /// <param name="casos">Los documentos entre los que se cuenta; los completos no cuentan.</param>
    /// <returns>El motivo más repetido, o <c>SinMotivo</c> si todos están completos.</returns>
    public static MotivoDeNoCompletar MotivoQueMasSeRepite(IEnumerable<Caso> casos)
    {
        ArgumentNullException.ThrowIfNull(casos);

        var cuenta = new Dictionary<MotivoDeNoCompletar, int>();
        foreach (var caso in casos.Where(c => !EstaCompleto(c)))
        {
            var motivo = MotivoDe(caso);
            cuenta[motivo] = cuenta.GetValueOrDefault(motivo) + 1;
        }

        if (cuenta.Count == 0) return MotivoDeNoCompletar.SinMotivo;

        return OrdenDelDesempate
            .Where(cuenta.ContainsKey)
            .OrderByDescending(m => cuenta[m])
            .ThenBy(m => Array.IndexOf(OrdenDelDesempate, m))
            .First();
    }

    /// <summary>El orden declarado del desempate del C11-4; nada de «el mas grave».</summary>
    private static readonly MotivoDeNoCompletar[] OrdenDelDesempate =
    [
        MotivoDeNoCompletar.ElLiderNoLoHizo,
        MotivoDeNoCompletar.NoSePudoComunicar,
        MotivoDeNoCompletar.OtraRazon,
        MotivoDeNoCompletar.SinMotivo,
    ];

    /// <summary>Un documento esta completo cuando su recomendacion lo dice.</summary>
    /// <param name="caso">El documento que se mira.</param>
    private static bool EstaCompleto(Caso caso) => caso.Estado == EstadoDeRecomendacion.Completa;

    /// <summary>Por que clave se junta un dia en unidades; sin numero, por el nombre.</summary>
    /// <param name="caso">El documento del que se saca la unidad.</param>
    private static string ClaveDeUnidad(Caso caso)
        => string.IsNullOrWhiteSpace(caso.UnidadNumero)
            ? "sin número·" + (caso.UnidadNombre?.Trim() ?? string.Empty)
            : caso.UnidadNumero.Trim();

    /// <summary>
    /// Trae los casos de la base en UNA sola consulta, archivados incluidos: es la lista de la
    /// que salen el calendario y el grupo de cada fecha.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Es la unica lista del programa que trae archivados, y son dos pantallas: el
    /// calendario y el grupo de la fecha.</b> Al calendario volvieron el 2026-09-07 con estas
    /// palabras del dueno: <i>«Cuando un paquete entra y marca todo completo, debe salir de
    /// todos lados EXCEPTO del calendario. Aunque se archive, debe quedarse en el calendario
    /// marcado en verde, porque estan completos, pero se mueve para abrir espacio a otros PDF
    /// que necesitan ser procesados»</i>. Al grupo de la fecha entran desde el 2026-09-14, y
    /// por una razon medida: la pastilla de fuera y la cabecera de dentro salen de esta MISMA
    /// lista, y con dos listas distintas decian dos cuentas distintas del mismo dia.</para>
    ///
    /// <para><b>Esto deshace a medias lo del 2026-09-06</b>, y hay que decir cual mitad. Aquel
    /// dia dijo <i>«debe pasar a archivado y no aparecer mas en ningun lado»</i>, y su motivo
    /// era otro: <i>«si se queda en el tablero y DICE ARCHIVADO, lo que hace es que me
    /// confunda»</i>. Lo que le estorbaba era la ETIQUETA en medio del trabajo, no verlo
    /// resuelto. Asi que la pastilla dice una de las dos palabras y nada mas
    /// (<see cref="PastillaDeDia.Etiqueta"/>), y el renglon del grupo dice «resuelto» con la
    /// nota «archivado» al lado, que no es una etiqueta en medio del trabajo: el dueno ya
    /// entro a la fecha para ver «cuales fueron completados».</para>
    ///
    /// <para>⛔ <b>Y a ninguna otra pantalla.</b> Inicio, Asignar, Correccion, Revisar y la
    /// ventana de incompletos siguen sin traerlos, que es la otra mitad de su frase: sale de
    /// todos lados para abrir espacio. Hasta el 2026-09-14 aqui habia DOS metodos —uno sin
    /// archivados para el grupo y otro con ellos para el calendario— y se juntan en uno porque
    /// ya no hay dos listas que distinguir; el nombre lleva la palabra a proposito, para que
    /// quien lo llame desde otro sitio tenga que leer esto antes.</para>
    /// </remarks>
    private IReadOnlyList<Caso> LeerLosCasosConLosArchivados()
        => _casos.Listar(
            FiltroDeCasos.Todo with { IncluirArchivados = true },
            new Pagina(0, int.MaxValue)).Elementos;

    /// <summary>
    /// Las personas de esos documentos, en UNA sola consulta.
    /// </summary>
    /// <remarks>
    /// Preguntar <c>DeCaso</c> documento a documento seria una ida a la base por cada uno;
    /// con el dia mas cargado de una base de 3 000 eso son 98 consultas para pintar una
    /// pantalla. Se trae la lista entera y se parte aqui, que es lo mismo que ya hace Inicio.
    /// </remarks>
    /// <param name="casos">Los documentos cuyas personas se quieren; las de otros se descartan.</param>
    /// <returns>Las personas de cada documento por su número interno; un documento sin personas no aparece.</returns>
    private Dictionary<long, List<Persona>> AgruparLasPersonas(IReadOnlyList<Caso> casos)
        => LeerLasPersonasDe(_personas, casos.Select(c => c.Id));

    /// <summary>
    /// Las personas de esos documentos, en UNA sola consulta, desde cualquier pantalla.
    /// </summary>
    /// <remarks>
    /// Es <see cref="AgruparLasPersonas"/> puesto donde Revisar tambien lo alcance: desde el
    /// 2026-09-16 las tarjetas necesitan las seis de cada persona para saber si el documento
    /// esta a medias, y copiar este bucle alli seria la segunda version de la misma lectura.
    /// </remarks>
    /// <param name="personas">El puerto de personas.</param>
    /// <param name="casoIds">Los documentos cuyas personas se quieren; las de otros se descartan.</param>
    /// <returns>Las personas de cada documento por su número interno; un documento sin personas no aparece.</returns>
    public static Dictionary<long, List<Persona>> LeerLasPersonasDe(IPersonas personas, IEnumerable<long> casoIds)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(casoIds);

        var queremos = casoIds.ToHashSet();
        var cuantas = personas.Contar(FiltroDePersonas.Todo);
        var porCaso = new Dictionary<long, List<Persona>>(queremos.Count);

        if (cuantas > 0)
        {
            foreach (var persona in personas.Listar(FiltroDePersonas.Todo, new Pagina(0, cuantas)).Elementos)
            {
                if (!queremos.Contains(persona.CasoId)) continue;
                if (!porCaso.TryGetValue(persona.CasoId, out var lista))
                {
                    lista = [];
                    porCaso[persona.CasoId] = lista;
                }

                lista.Add(persona);
            }
        }

        return porCaso;
    }

    /// <summary>Que companero lleva cada documento ahora mismo, por su nombre.</summary>
    private Dictionary<long, string> LeerLosDuenos()
    {
        var nombres = _companeros.Listar(FiltroDeCompaneros.Activos with { SoloActivos = false }, new Pagina(0, int.MaxValue))
            .Elementos.ToDictionary(c => c.Id, c => c.Nombre);

        var duenos = new Dictionary<long, string>();
        var filtro = FiltroDeAsignaciones.Activas;
        var cuantas = _asignaciones.Contar(filtro);
        if (cuantas == 0) return duenos;

        foreach (var asignacion in _asignaciones.Listar(filtro, new Pagina(0, cuantas)).Elementos)
        {
            var nombre = nombres.GetValueOrDefault(asignacion.CompaneroId, "sin nombre");
            duenos[asignacion.CasoId] = duenos.TryGetValue(asignacion.CasoId, out var yaHabia)
                ? $"{yaHabia} y {nombre}"
                : nombre;
        }

        return duenos;
    }

    /// <summary>Compone la cabecera de una unidad con sus personas dentro.</summary>
    /// <remarks>
    /// La fecha entra desde el 2026-09-09 porque la cabecera de la unidad la DICE: el dueno lee
    /// el renglon entero —«10 personas viajaran el 12 de septiembre»— y con la fecha solo en el
    /// titulo de la pantalla, ese renglon no dice de que dia habla. Se pasa desde
    /// <see cref="DelDia"/>, que es quien la sabe, y no se vuelve a leer del caso: dentro de un
    /// dia todos los documentos tienen la misma, y leerla otra vez seria una segunda fuente que
    /// algun dia diria otra cosa.
    /// </remarks>
    /// <param name="fecha">El día del viaje, que la cabecera dice.</param>
    /// <param name="unidad">Los documentos de esa unidad, agrupados por su clave.</param>
    /// <param name="personasPorCaso">Las personas de cada documento, ya leídas.</param>
    /// <param name="duenos">Quién lleva cada documento, por número interno.</param>
    /// <param name="procedencias">De dónde salió cada campo, leída una vez para el día.</param>
    private UnidadDelGrupo ArmarLaUnidad(
        DateOnly fecha,
        IGrouping<string, Caso> unidad,
        IReadOnlyDictionary<long, List<Persona>> personasPorCaso,
        IReadOnlyDictionary<long, string> duenos,
        ProcedenciasDeUnaPasada procedencias)
    {
        var casos = unidad.OrderBy(c => c.NumeroCaso, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id).ToList();
        var personas = new List<PersonaDelGrupo>();

        foreach (var caso in casos)
        {
            var suyas = personasPorCaso.GetValueOrDefault(caso.Id) ?? [];
            var leFalta = LoQueLeFalta.DeUnDocumento(caso, suyas, procedencias).Count;
            var hayPdf = caso.RutaPdf is not null && _elArchivoExiste(caso.RutaPdf);
            var dueno = duenos.GetValueOrDefault(caso.Id, string.Empty);

            // Un documento del que no se leyo ninguna persona NO desaparece del grupo: se
            // ensena con una fila que dice que no se leyo nadie. Si se cayera, el dueno
            // veria un grupo mas pequeno que el numero que la cabecera acaba de decir.
            if (suyas.Count == 0)
            {
                personas.Add(ArmarLaPersona(caso, persona: null, leFalta, hayPdf, dueno));
                continue;
            }

            foreach (var persona in suyas.OrderBy(p => p.FilaFormulario ?? int.MaxValue).ThenBy(p => p.Id))
            {
                personas.Add(ArmarLaPersona(caso, persona, leFalta, hayPdf, dueno));
            }
        }

        var primero = casos[0];
        return new UnidadDelGrupo(
            primero.UnidadNumero?.Trim() ?? string.Empty,
            primero.UnidadNombre?.Trim() ?? string.Empty,
            fecha,
            [.. casos.Select(c => c.Id)],
            personas,
            casos.Count(EstaCompleto),
            MotivoQueMasSeRepite(casos));
    }

    /// <summary>
    /// Compone el renglon de una persona, o el de un documento del que no se leyo ninguna.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>El estado de la persona sale de SUS seis preguntas y no de <c>caso.Estado</c></b>
    /// (criterio C18-1). Hasta el 2026-09-05 aqui se copiaba el del documento, y cinco
    /// personas del mismo papel salian iguales aunque tres estuvieran resueltas y dos no.
    /// <para>
    /// El del documento SIGUE viniendo, en <see cref="PersonaDelGrupo.EstadoDelDocumento"/>:
    /// no se borro, se separo. Es lo que escribe el Excel del companero con su nombre y no
    /// se toca (regla permanente 5).
    /// </para>
    /// <para>
    /// Un documento del que no se leyo a NADIE queda «sin mirar», que es la verdad: no hay
    /// ninguna persona cuya recomendacion se pudiera mirar. No se pinta como «no lista».
    /// </para>
    /// </remarks>
    /// <param name="caso">El documento del que sale.</param>
    /// <param name="persona">La persona, o nulo para el renglón de un documento sin ninguna.</param>
    /// <param name="leFalta">Cuántos datos le faltan al documento.</param>
    /// <param name="hayPdf">Si el PDF está en su ruta.</param>
    /// <param name="dueno">Quién lleva el documento, o vacío.</param>
    private static PersonaDelGrupo ArmarLaPersona(Caso caso, Persona? persona, int leFalta, bool hayPdf, string dueno)
        => new(
            persona?.Id ?? 0,
            caso.Id,
            persona is null
                ? "sin ninguna persona leída"
                : string.IsNullOrWhiteSpace(persona.Nombre) ? "sin nombre leído" : persona.Nombre.Trim(),
            persona?.Mrn?.Trim() ?? string.Empty,
            caso.NumeroCaso ?? "sin número",
            caso.RutaPdf,
            hayPdf,
            caso.Estado,
            MotivoDe(caso),
            leFalta,
            dueno,
            LasDosPreguntas.EstadoDe(persona),
            LasDosPreguntas.SeQuedoEn(persona),
            caso.Archivado,
            LasDosPreguntas.LasQueNoDicenSi(persona));

    /// <summary>Compone la pastilla que el calendario ensena para una unidad de ese dia.</summary>
    /// <remarks>
    /// Cuenta PERSONAS confirmadas y no documentos completos (C20-3). Las 42 celdas fijas y
    /// las 3 pastillas por dia no cambian: el numero de elementos visuales sigue sin depender
    /// de cuantos casos haya (C12-2, que sigue mandando).
    /// </remarks>
    /// <param name="unidad">Los documentos de esa unidad ese día, agrupados por su clave.</param>
    /// <param name="personasPorCaso">Las personas de cada documento, ya leídas.</param>
    private static PastillaDeDia ArmarLaPastilla(
        IGrouping<string, Caso> unidad,
        IReadOnlyDictionary<long, List<Persona>> personasPorCaso)
    {
        var casos = unidad.ToList();
        var primero = casos[0];

        // Se cuenta en UNA pasada y sin armar ninguna lista intermedia. Con 3 000 documentos
        // el calendario arma una pastilla por unidad y por dia, y una lista por pastilla
        // costaba 36 ms medidos que no compraban nada: aqui solo hacen falta dos cifras.
        var cuantasPersonas = 0;
        var confirmadas = 0;
        var resueltas = 0;
        foreach (var caso in casos)
        {
            if (!personasPorCaso.TryGetValue(caso.Id, out var suyas)) continue;
            cuantasPersonas += suyas.Count;
            foreach (var persona in suyas)
            {
                var confirmada = LasDosPreguntas.EstadoDe(persona) == true;
                if (confirmada) confirmadas++;

                // ⚠️ Las dos cuentas van aparte y no se pisan. «Confirmada» es una afirmacion
                // sobre las seis preguntas de esa persona y no se toca: decir que estan
                // confirmadas las de un archivado seria inventarlo. «Resuelta» es otra cosa —si
                // al dueno le queda algo que hacer— y ahi archivar SI cuenta, porque archivar es
                // el gesto con el que el cierra un documento. El calendario pinta la segunda:
                // «aunque se archive, debe quedarse en el calendario marcado en verde»
                // (2026-09-07).
                if (confirmada || caso.Archivado) resueltas++;
            }
        }

        return new PastillaDeDia(
            primero.UnidadNumero?.Trim() ?? string.Empty,
            primero.UnidadNombre?.Trim() ?? string.Empty,
            casos.Count,
            cuantasPersonas,
            casos.Count(EstaCompleto),
            MotivoQueMasSeRepite(casos),
            confirmadas,
            resueltas);
    }
}
