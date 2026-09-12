using Fichas.App.Inicio;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Grupo;

/// <summary>
/// Un grupo de fecha de la ventana de incompletos: todo lo que viaja el mismo dia y
/// todavia no esta resuelto.
/// </summary>
/// <param name="Fecha">El dia del viaje, o nulo para el grupo de los que no tienen fecha.</param>
/// <param name="DiasHastaElViaje">Cuantos dias faltan; negativo si ya paso.</param>
/// <param name="Documentos">Los documentos de ese dia que no estan completos.</param>
/// <param name="CuantasPersonas">Cuantas personas suman.</param>
public sealed record GrupoDeIncompletos(
    DateOnly? Fecha,
    int DiasHastaElViaje,
    IReadOnlyList<RenglonDeCaso> Documentos,
    int CuantasPersonas)
{
    /// <summary>«martes 8 de septiembre de 2026», o la frase de los que no tienen fecha.</summary>
    public string Titulo => Fecha is DateOnly dia
        ? FechasEnEspanol.DecirElDiaCompleto(dia)
        : "sin fecha de viaje";

    /// <summary>«en 3 días», «hoy», «hace 2 días», o nada si no hay fecha.</summary>
    public string CuandoViaja => Fecha is null ? "no se sabe cuándo viaja" : FechasEnEspanol.DecirLosDias(DiasHastaElViaje);

    /// <summary>Si ese dia ya paso; entonces el grupo se marca y baja del todo.</summary>
    public bool YaViajo => Fecha is not null && DiasHastaElViaje < 0;

    /// <summary>«3 documentos · 11 personas · en 4 días».</summary>
    public string Detalle
        => Plural.Con(Documentos.Count, "documento", "documentos")
           + " · " + Plural.Con(CuantasPersonas, "persona", "personas")
           + " · " + CuandoViaja;

    /// <summary>La fecha en ISO-8601, o vacio; es lo que se le pasa a la pantalla del grupo.</summary>
    public string FechaIso => Fecha is DateOnly dia ? FechasEnEspanol.Escribir(dia) : string.Empty;

    /// <summary>Si desde esta cabecera se puede saltar al grupo entero de ese dia.</summary>
    public bool SePuedeAbrirElGrupo => Fecha is not null;
}

/// <summary>Todo lo que la ventana de incompletos ensena, leido de una vez.</summary>
/// <param name="Grupos">Los grupos de fecha, lo que viaja antes arriba.</param>
/// <param name="CuantosDocumentos">Cuantos documentos incompletos hay en total.</param>
/// <param name="CuantasPersonas">Cuantas personas suman.</param>
/// <param name="CasosEnLaBase">El denominador: cuantos casos hay, archivados incluidos.</param>
/// <param name="CasosNoArchivados">Cuantos quedan tras quitar los archivados.</param>
/// <param name="Avisos">Lo que hay que decir en una linea; nunca detiene el pintado.</param>
public sealed record ResumenDeIncompletos(
    IReadOnlyList<GrupoDeIncompletos> Grupos,
    int CuantosDocumentos,
    int CuantasPersonas,
    int CasosEnLaBase,
    int CasosNoArchivados,
    IReadOnlyList<Aviso> Avisos)
{
    /// <summary>La linea del denominador: sin ella, la cifra de arriba no se puede comprobar.</summary>
    public string LineaDelDenominador
        => Plural.Con(CuantosDocumentos, "documento sin completar", "documentos sin completar")
           + " · " + Plural.Con(CuantasPersonas, "persona", "personas")
           // ⚠️ 2026-09-06. Decia «de N sin archivar, de N en la base». Fuera por lo mismo que
           // en Inicio y en Asignar: el dueno no quiere leer la palabra archivado en ningun
           // contador. Se queda la cifra de lo que hay que trabajar.
           + $" · de {CasosNoArchivados}";

    /// <summary>Si no queda nada por completar; se dice, no se deja la pantalla en blanco.</summary>
    public bool NoQuedaNada => CuantosDocumentos == 0;

    /// <summary>
    /// Todo puesto en UNA sola lista: una cabecera por fecha y detras sus documentos.
    /// </summary>
    /// <remarks>
    /// Plana por el mismo motivo que la pantalla del grupo: un repetidor dentro de otro no
    /// virtualiza porque su alto no esta acotado, y con 3 000 documentos construiria los
    /// cientos de renglones de golpe. Aplanada, un solo repetidor solo construye lo que se ve.
    /// </remarks>
    public IReadOnlyList<RenglonDeLoIncompleto> EnUnaSolaLista()
    {
        var renglones = new List<RenglonDeLoIncompleto>();

        foreach (var grupo in Grupos)
        {
            renglones.Add(RenglonDeLoIncompleto.Cabecera(grupo));
            renglones.AddRange(grupo.Documentos.Select(RenglonDeLoIncompleto.DeUnDocumento));
        }

        return renglones;
    }
}

/// <summary>
/// Un renglon de la ventana de incompletos: o la cabecera de una fecha, o un documento.
/// </summary>
/// <param name="EsCabecera">Si este renglon es la cabecera de un grupo de fecha.</param>
/// <param name="Titulo">La fecha escrita, o el numero del documento.</param>
/// <param name="Detalle">Lo que se lee debajo del titulo.</param>
/// <param name="CasoId">El documento que se abre al pulsar; 0 en una cabecera.</param>
/// <param name="FechaIso">La fecha del grupo en ISO-8601; vacio si no tiene o no es cabecera.</param>
/// <param name="CuandoViaja">«en 3 días», «hoy», «hace 2 días» o «sin fecha».</param>
/// <param name="EsVencido">Si ese dia ya paso.</param>
public sealed record RenglonDeLoIncompleto(
    bool EsCabecera,
    string Titulo,
    string Detalle,
    long CasoId,
    string FechaIso,
    string CuandoViaja,
    bool EsVencido)
{
    /// <summary>La cabecera de un grupo de fecha.</summary>
    /// <param name="grupo">El grupo de fecha del que es cabecera; no puede ser nulo.</param>
    public static RenglonDeLoIncompleto Cabecera(GrupoDeIncompletos grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        return new RenglonDeLoIncompleto(
            true, grupo.Titulo, grupo.Detalle, 0, grupo.FechaIso, grupo.CuandoViaja, grupo.YaViajo);
    }

    /// <summary>El renglon de un documento sin completar.</summary>
    /// <param name="caso">El renglón del documento, tal como lo arma el lector; no puede ser nulo.</param>
    public static RenglonDeLoIncompleto DeUnDocumento(RenglonDeCaso caso)
    {
        ArgumentNullException.ThrowIfNull(caso);
        return new RenglonDeLoIncompleto(
            false, caso.NumeroCaso, caso.DetalleDeLoIncompleto, caso.CasoId,
            string.Empty, caso.CuandoViaja, caso.EsVencido);
    }

    /// <summary>Si este renglon es un documento; lo lee la plantilla para ensenar su mitad.</summary>
    public bool EsUnDocumento => !EsCabecera;

    /// <summary>Si desde esta cabecera se puede saltar al grupo entero de ese dia.</summary>
    public bool SePuedeAbrirElGrupo => EsCabecera && FechaIso.Length > 0;

    /// <summary>Lo que lee en voz alta un lector de pantalla.</summary>
    public string ParaElLector => EsCabecera
        ? $"{Titulo}. {Detalle}"
        : $"{Titulo}. {Detalle}. Pulse para corregir este documento.";
}

/// <summary>
/// Arma la ventana de «lo que no esta completo», agrupada por fecha de viaje.
/// </summary>
/// <remarks>
/// <para>Palabras del dueno el 2026-09-05: <i>«Luego crea una ventana de revisar lo que no
/// está completo, y ponlo por grupo de fechas»</i>. Y el motivo, que vale mas que la
/// peticion: <i>«No quiero revisar gente que viaja en noviembre estando en septiembre»</i>.</para>
///
/// <para><b>Que cuenta como «no completo», y son dos cosas a la vez.</b> Un documento entra
/// si le falta algun dato en el sistema —lo que impide armarle el paquete al companero, que
/// es su peticion 5 del mismo dia— o si el Excel del companero lo devolvio marcado
/// <c>no_completa</c>. Cada renglon dice CUAL de las dos le pasa, porque no son lo mismo y
/// se arreglan por caminos distintos: la primera se arregla en Correccion, la segunda
/// hablando con el lider. Meterlas en una sola cuenta sin distinguirlas obligaria a abrir
/// documento por documento para saber cual es cual.</para>
///
/// <para><b>Los archivados no entran.</b> Regla del dueno del 2026-09-05: <i>«cuando yo
/// archive, debe salir del sistema visible pero se queda como histórico para los
/// reportes»</i>. Siguen enteros en la base y siguen en el calendario, marcados.</para>
/// </remarks>
/// <remarks>
/// ⚠️ La frase de arriba sobre el calendario es de antes del 2026-09-06, cuando el dueño
/// pidió que un archivado no apareciera «en ningún lado», y del 2026-09-07 (§5), cuando
/// pidió verlo en el calendario «marcado en verde» y sin etiqueta. Esta ventana no pinta el
/// calendario: lo suyo es que un archivado <b>no entra aquí</b>, y eso sigue siendo así.
/// </remarks>
public sealed class LectorDeIncompletos
{
    /// <summary>De dónde se leen los documentos vivos y se cuentan todos.</summary>
    private readonly ICasos _casos;
    /// <summary>De dónde se leen las personas, en una sola consulta.</summary>
    private readonly IPersonas _personas;
    /// <summary>De dónde salen los nombres de quienes llevan cada documento.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>De dónde se lee quién lleva cada documento.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>De dónde sale «hoy», para contar los días hasta el viaje.</summary>
    private readonly IReloj _reloj;
    /// <summary>De dónde salió cada campo; se lee en bloque una vez por ventana.</summary>
    private readonly IProcedencia _procedencia;

    /// <summary>Se ata a los seis puertos que hacen falta.</summary>
    /// <param name="casos">Los documentos.</param>
    /// <param name="personas">Las personas de cada documento.</param>
    /// <param name="companeros">Los nombres de quienes los llevan.</param>
    /// <param name="asignaciones">Quien lleva cada documento.</param>
    /// <param name="reloj">Que dia es hoy.</param>
    /// <param name="procedencia">
    /// De donde salio cada campo. El sexto, y entro el 2026-09-06: esta ventana es la que
    /// arma la cola de Completar, y hasta ese dia decidia con una regla distinta de la de
    /// Correccion. La peor consecuencia medida: un documento con un campo marcado como que no
    /// esta en el papel se quedaba en la cola <b>para siempre</b>.
    /// </param>
    public LectorDeIncompletos(
        ICasos casos,
        IPersonas personas,
        ICompaneros companeros,
        IAsignaciones asignaciones,
        IReloj reloj,
        IProcedencia procedencia)
    {
        _casos = casos;
        _personas = personas;
        _companeros = companeros;
        _asignaciones = asignaciones;
        _reloj = reloj;
        _procedencia = procedencia;
    }

    /// <summary>Lee la ventana entera.</summary>
    public ResumenDeIncompletos Leer()
    {
        var avisos = new List<Aviso>();
        var hoy = FechasEnEspanol.Leer(_reloj.Hoy()) ?? DateOnly.FromDateTime(DateTime.Today);

        var enLaBase = _casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true });
        var deTrabajo = _casos.Listar(FiltroDeCasos.Todo, new Pagina(0, int.MaxValue)).Elementos;
        var personasPorCaso = AgruparLasPersonas(deTrabajo);
        var duenos = LeerLosDuenos();

        // Una sola lectura en bloque para toda la ventana; documento a documento son 4,4 s.
        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(_procedencia);
        var raras = 0;

        var incompletos = new List<(DateOnly? Fecha, RenglonDeCaso Renglon)>();

        foreach (var caso in deTrabajo)
        {
            var suyas = personasPorCaso.GetValueOrDefault(caso.Id) ?? [];
            var leFalta = LoQueLeFalta.DeUnDocumento(caso, suyas, procedencias).Count;
            var loDijoElCompanero = caso.Estado == EstadoDeRecomendacion.NoCompleta;
            if (leFalta == 0 && !loDijoElCompanero) continue;

            var fecha = FechasEnEspanol.Leer(caso.FechaViaje);
            if (fecha is null && !string.IsNullOrWhiteSpace(caso.FechaViaje)) raras++;

            incompletos.Add((fecha, new RenglonDeCaso(
                caso.Id,
                caso.NumeroCaso ?? "sin número",
                caso.UnidadNombre ?? "sin unidad leída",
                fecha is DateOnly dia ? FechasEnEspanol.Escribir(dia) : null,
                fecha is DateOnly cuando ? cuando.DayNumber - hoy.DayNumber : 0,
                fecha is DateOnly paso && paso < hoy,
                fecha is null,
                suyas.Count,
                caso.Estado,
                leFalta,
                duenos.GetValueOrDefault(caso.Id, string.Empty))));
        }

        if (raras > 0)
        {
            avisos.Add(Aviso.Advierte(
                Plural.Con(raras, "caso", "casos") + " con una fecha de viaje que no tiene la forma AAAA-MM-DD.",
                nameof(Caso.FechaViaje),
                "Se ensenan en el grupo de «sin fecha de viaje», al final. Nada se pierde: "
                + "en cuanto se corrija la fecha, vuelven a su día."));
        }

        var grupos = Agrupar(incompletos, hoy);

        return new ResumenDeIncompletos(
            grupos,
            incompletos.Count,
            incompletos.Sum(i => i.Renglon.CuantasPersonas),
            enLaBase,
            deTrabajo.Count,
            avisos);
    }

    /// <summary>
    /// Junta los documentos por su fecha de viaje y pone lo que viaja antes arriba.
    /// </summary>
    /// <remarks>
    /// El orden es el mismo que el criterio C1-4 fijo para la franja de Inicio, y por el
    /// mismo motivo: primero lo que todavia se puede salvar, del mas cercano al mas lejano;
    /// detras lo que ya viajo, del que vencio hace menos al que vencio hace mas; y al final
    /// los que no tienen fecha, que no desaparecen (C7-4). Ordenar solo por fecha pondria
    /// agosto encima de septiembre, que es lo contrario de «la prioridad son los que
    /// viajarán pronto».
    /// </remarks>
    /// <param name="incompletos">Cada documento incompleto con su fecha leída, o nula si no la tiene.</param>
    /// <param name="hoy">El día de hoy, para saber qué ya viajó.</param>
    private static IReadOnlyList<GrupoDeIncompletos> Agrupar(
        List<(DateOnly? Fecha, RenglonDeCaso Renglon)> incompletos,
        DateOnly hoy)
        => [.. incompletos
            .GroupBy(i => i.Fecha)
            .Select(dia => new GrupoDeIncompletos(
                dia.Key,
                dia.Key is DateOnly fecha ? fecha.DayNumber - hoy.DayNumber : 0,
                [.. dia.Select(i => i.Renglon).OrderBy(r => r.NumeroCaso, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.CasoId)],
                dia.Sum(i => i.Renglon.CuantasPersonas)))
            .OrderBy(g => g.Fecha is null ? 2 : g.YaViajo ? 1 : 0)
            .ThenBy(g => g.YaViajo ? -g.DiasHastaElViaje : g.DiasHastaElViaje)];

    /// <summary>Las personas de los documentos vivos, en UNA sola consulta.</summary>
    /// <param name="deTrabajo">Los documentos vivos cuyas personas se quieren.</param>
    /// <returns>Las personas de cada documento por su número interno; un documento sin personas no aparece.</returns>
    private Dictionary<long, List<Persona>> AgruparLasPersonas(IReadOnlyList<Caso> deTrabajo)
    {
        var queremos = deTrabajo.Select(c => c.Id).ToHashSet();
        var cuantas = _personas.Contar(FiltroDePersonas.Todo);
        var porCaso = new Dictionary<long, List<Persona>>(deTrabajo.Count);
        if (cuantas == 0) return porCaso;

        foreach (var persona in _personas.Listar(FiltroDePersonas.Todo, new Pagina(0, cuantas)).Elementos)
        {
            if (!queremos.Contains(persona.CasoId)) continue;
            if (!porCaso.TryGetValue(persona.CasoId, out var lista))
            {
                lista = [];
                porCaso[persona.CasoId] = lista;
            }

            lista.Add(persona);
        }

        return porCaso;
    }

    /// <summary>Que companero lleva cada documento ahora mismo, por su nombre.</summary>
    private Dictionary<long, string> LeerLosDuenos()
    {
        var nombres = _companeros
            .Listar(FiltroDeCompaneros.Activos with { SoloActivos = false }, new Pagina(0, int.MaxValue))
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
}
