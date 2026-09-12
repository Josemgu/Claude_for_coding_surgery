using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Inicio;

/// <summary>
/// Calcula lo que la pantalla de Inicio ensena, hablando SOLO con los contratos.
/// </summary>
/// <remarks>
/// <para>No conoce ni un control de la interfaz ni la implementacion que hay detras de los
/// puertos, y por eso se puede probar sin abrir ventana (ADR-0003 §8.1).</para>
///
/// <para>Lee la lista de casos UNA sola vez y cuenta todo sobre ella. No es una
/// optimizacion: es lo que impide que el contador de arriba y la lista de abajo digan cosas
/// distintas si alguien cambia la base entre dos consultas.</para>
///
/// <para><b>⚠️ Este lector alimenta DOS pantallas desde el 2026-09-09, y hay que saberlo antes
/// de tocarlo:</b> el cuadro de dos cifras de Inicio y las tres listas de la pestana del flujo.
/// No se partio en dos a proposito. Las cifras del cuadro y las listas contestan la misma
/// pregunta con distinta letra, y dos lectores acaban contestando distinto; el dueno leeria un
/// numero en Inicio y contaria otro al abrir la pestana. Es la enfermedad que este proyecto ya
/// diagnostico el 2026-09-07 con las cuatro palabras de estado.</para>
///
/// <para><b>Que hay en cada una.</b> En Inicio, «me falta por completar» y «lo que tienen los
/// agentes», mas el calendario — palabras del dueno del 2026-09-07: <i>«Lo unico que quiero es
/// el calendario y un cuadro»</i>. En la pestana del flujo, lo listo para asignar, lo asignado
/// a los agentes y lo listo para viajar. ⚠️ Esto <b>deshace</b> lo que el mismo pidio el
/// 2026-09-05 —«lo unico que quiero ver en Home es lo que esta listo para asignar y lo que esta
/// asignado a los agentes»—, que estaba construido; manda la peticion nueva.</para>
///
/// <para>Lo que no esta completo sigue teniendo ademas su ventana entera, en
/// <see cref="LectorDeIncompletos"/>, agrupada por fecha de viaje, y la cifra del cuadro es la
/// puerta a ella.</para>
///
/// <para><b>Un caso archivado no sale de NADA de esta pantalla</b> —ni de las dos listas, ni
/// del calendario, ni de los avisos del sistema del obispo—. Regla del dueno del 2026-09-05
/// (<i>«cuando yo archive, debe salir del sistema visible pero se queda como histórico para
/// los reportes»</i>) llevada hasta el final el 2026-09-06, cuando deshizo lo que el mismo
/// habia pedido el 2026-09-03 —verlo en el calendario, marcado— porque le confundia. Sigue
/// contando en el denominador «N casos en la base · N sin archivar», que dice cuanto se
/// recorta y no ensena ningun caso.</para>
/// </remarks>
public sealed class LectorDelInicio
{
    /// <summary>Los dias de la ventana sombreada del calendario; el dueno pidio 7 (C1-4).</summary>
    public const int DiasDeLaVentana = 7;

    /// <summary>Los documentos; se leen enteros, archivados incluidos, en una sola consulta.</summary>
    private readonly ICasos _casos;
    /// <summary>Las personas de cada documento; también en una sola consulta, para contar sobre lo mismo.</summary>
    private readonly IPersonas _personas;
    /// <summary>El equipo, para poner nombre a quien lleva cada documento y contar los activos.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>Quién lleva cada documento ahora mismo y cuántas hojas no han vuelto.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>Qué día es hoy; nunca <c>DateTime.Today</c> directo, para que las pruebas paren el reloj.</summary>
    private readonly IReloj _reloj;
    /// <summary>De dónde salió cada campo; decide «listo para asignar» igual que la pantalla de Corrección.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>Quien agrupa los documentos por día y por unidad; el calendario y la pantalla del grupo leen por él.</summary>
    private readonly LectorDeGrupos _grupos;

    /// <summary>Se ata a los seis puertos que la pantalla necesita.</summary>
    /// <param name="casos">Los documentos.</param>
    /// <param name="personas">Las personas de cada documento.</param>
    /// <param name="companeros">El equipo.</param>
    /// <param name="asignaciones">Quien lleva cada documento.</param>
    /// <param name="reloj">Que dia es hoy.</param>
    /// <param name="procedencia">
    /// De donde salio cada campo. El sexto, y entro el 2026-09-06: «listo para asignar» no se
    /// puede contestar sin ella sin contestar distinto que la pantalla de Correccion.
    /// </param>
    public LectorDelInicio(
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
        _grupos = new LectorDeGrupos(casos, personas, asignaciones, companeros, procedencia);
    }

    /// <summary>La fecha de hoy en ISO-8601, la misma que ven las listas y el calendario.</summary>
    public string Hoy => _reloj.Hoy();

    /// <summary>
    /// Lee el panel entero. <paramref name="desplazamientoDeMes"/> mueve el calendario:
    /// -1 el mes anterior, 0 el de hoy, 1 el siguiente.
    /// </summary>
    /// <param name="desplazamientoDeMes">Cuántos meses mover el calendario respecto al de hoy; puede ser negativo.</param>
    /// <returns>Todo lo que Inicio y la pestaña del flujo pintan, leído de la base una sola vez.</returns>
    public ResumenDeInicio Leer(int desplazamientoDeMes = 0)
    {
        var avisos = new List<Aviso>();
        var hoy = FechasEnEspanol.Leer(_reloj.Hoy()) ?? DateOnly.FromDateTime(DateTime.Today);
        var finDeLaVentana = hoy.AddDays(DiasDeLaVentana);

        var todos = LeerTodosLosCasos();
        var deTrabajo = todos.Where(c => !c.Archivado).ToList();

        // ⚠️ Se agrupan TODAS las personas, tambien las de los archivados. Antes se agrupaban
        // solo las de los documentos vivos, y con eso no se puede contestar el criterio
        // C21-2: un ticket vencido no se cierra ni desaparece cuando el documento se archiva.
        // Sigue siendo UNA sola consulta; lo unico que crece es el conjunto que se filtra.
        var personasPorCaso = AgruparLasPersonas(todos);
        var fechas = LeerLasFechas(deTrabajo, avisos);
        var duenos = LeerLosDuenos();

        // ⚠️ La procedencia se lee UNA vez, en bloque, para los 3 000 documentos. Con
        // `IProcedencia.DeRegistro` documento a documento son 4,4 s medidos sobre las 18 000
        // lecturas reales, veintidos veces el presupuesto de esta pantalla; en bloque, 50 ms.
        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(_procedencia);

        var listos = new List<RenglonDeCaso>();
        var asignados = new List<RenglonDeCaso>();
        var porCompletar = new List<RenglonDeCaso>();
        Repartir(deTrabajo, fechas, personasPorCaso, duenos, hoy, procedencias, listos, asignados, porCompletar);

        var sinDevolver = _asignaciones.Contar(FiltroDeAsignaciones.Activas with { SinDevolver = true });
        var equipo = ArmarElEquipo(deTrabajo, duenos);

        return new ResumenDeInicio(
            new ContadoresDeInicio(
                listos.Count,
                asignados.Count,
                listos.Sum(r => r.CuantasPersonas),
                sinDevolver),
            ContarLosDenominadores(todos.Count, deTrabajo.Count),
            PorFechaDeViaje(listos),
            PorFechaDeViaje(asignados),
            equipo,
            ArmarElCalendario(hoy, finDeLaVentana, desplazamientoDeMes, personasPorCaso),
            avisos,
            ArmarLoDelSistemaDelObispo(todos, personasPorCaso, hoy),
            ArmarElCuadro(porCompletar, asignados, equipo, sinDevolver));
    }

    /// <summary>
    /// Las dos cifras que quedan a la vista en Inicio, armadas con lo que ya se conto.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>No lee la base ni una vez mas, y eso es el punto entero.</b> Lo facil habria
    /// sido montar aqui un <see cref="LectorDeIncompletos"/> para saber cuantos faltan por
    /// completar; eso recorre los casos, las personas y la procedencia por segunda vez en cada
    /// pintado, y el techo de esta pantalla son 200 ms (C20-5). Lo que se cuenta sale del
    /// mismo reparto que ya arma las listas. <c>PruebasDelCuadroDeInicio</c> lo cronometra.</para>
    ///
    /// <para>⚠️ <b>El companero de la fila «Sin asignar» no cuenta como companero.</b> Va en el
    /// equipo con id 0 para poder ensenar cuantos no lleva nadie, pero no es una persona: si
    /// contara, el cuadro diria un compañero de mas siempre.</para>
    /// </remarks>
    /// <param name="porCompletar">Los documentos vivos a los que les falta algo, ya repartidos.</param>
    /// <param name="asignados">Los documentos que ahora mismo lleva un compañero.</param>
    /// <param name="equipo">El cuadro del equipo, con la fila «Sin asignar» de id 0 al final.</param>
    /// <param name="sinDevolver">Cuántas hojas de los asignados no han vuelto todavía.</param>
    private static CuadroDeInicio ArmarElCuadro(
        IReadOnlyList<RenglonDeCaso> porCompletar,
        IReadOnlyList<RenglonDeCaso> asignados,
        IReadOnlyList<RenglonDeCompanero> equipo,
        int sinDevolver)
        => new(
            porCompletar.Count,
            porCompletar.Sum(r => r.CuantasPersonas),
            asignados.Count,
            equipo.Count(c => c.Id != 0 && c.Casos > 0),
            sinDevolver);

    /// <summary>
    /// Lo que hay que verificar en el sistema del obispo, contado en PERSONAS: el grupo que
    /// viene, quien ya viajo sin confirmar y quien viaja dentro de la ventana.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Se cuentan PERSONAS y no documentos</b>, y ese es el cambio entero. Sus
    /// palabras del 2026-09-05: <i>«faltan 3, 4 o 5 personas que la recomendación para el
    /// templo no está confirmada»</i>. Sobre sus siete escaneos reales las dos cuentas dan lo
    /// mismo —cada uno trae UNA persona—; sobre un formulario de grupo, no.</para>
    ///
    /// <para>⛔ <b>Los archivados NO entran, y eso deshace el criterio C21-2</b> (2026-09-06).
    /// Aquel decia que archivar no cierra un ticket —«salir de la vista de trabajo no es
    /// estar resuelto»—; el dueno decidio lo contrario con sus palabras: <i>«si ya resolví un
    /// archivo y lo archivo, no debe aparecer en notificaciones… no se cuenta ya»</i>. En su
    /// trabajo archivar ES cerrar el ticket (<c>DECISIONES.md</c>, 2026-09-05: «es un sistema
    /// de gestión de tickets y casos»). Siguen en la base y en los reportes.</para>
    ///
    /// <para><b>No se toca ni una fila.</b> Todo sale de leer las seis columnas que ya
    /// existen desde la migracion 9.</para>
    /// </remarks>
    /// <param name="todos">La base entera de casos; los archivados se saltan aquí dentro.</param>
    /// <param name="personasPorCaso">Las personas ya leídas, agrupadas por id de caso.</param>
    /// <param name="hoy">El día de hoy según el reloj del programa.</param>
    private LoDelSistemaDelObispo ArmarLoDelSistemaDelObispo(
        IReadOnlyList<Caso> todos,
        IReadOnlyDictionary<long, List<Persona>> personasPorCaso,
        DateOnly hoy)
    {
        var proxima = LaFechaDelProximoGrupo(todos, hoy);
        var conTicket = new List<PersonaConTicket>();
        var delProximoGrupo = new List<Persona>();

        foreach (var caso in todos)
        {
            // Un caso archivado es un ticket cerrado: no da aviso de ninguna de las tres
            // clases (2026-09-06). Se salta aqui, en un solo sitio, y no en cada cuenta.
            if (caso.Archivado) continue;
            if (FechasEnEspanol.Leer(caso.FechaViaje) is not DateOnly fecha) continue;

            var dias = fecha.DayNumber - hoy.DayNumber;
            var esDelProximoGrupo = fecha == proxima;
            var apremia = dias >= 0 && dias <= DiasDeLaVentana;
            var vencio = dias < 0;

            // ⚠️ Lo que no cae en ninguna de las tres NO se mira, y eso no es una
            // optimizacion prematura: es lo que hace que Inicio siga cabiendo en el tope del
            // criterio C20-5. Mirar las seis preguntas de las 16 500 personas de una base de
            // 3 000 documentos para acabar ensenando unas decenas costaba 263 ms medidos;
            // saltando lo que no se va a ver, la mitad. Un documento que viaja en noviembre
            // no aporta nada a ninguna de las tres cuentas, y el dueno lo dijo con esas
            // palabras: «No quiero revisar gente que viaja en noviembre estando en septiembre».
            if (!esDelProximoGrupo && !apremia && !vencio) continue;

            var suyas = personasPorCaso.GetValueOrDefault(caso.Id) ?? [];
            if (esDelProximoGrupo) delProximoGrupo.AddRange(suyas);
            if (!apremia && !vencio) continue;

            foreach (var persona in suyas)
            {
                if (LasDosPreguntas.EstadoDe(persona) == true) continue;
                conTicket.Add(ArmarElTicket(caso, persona, fecha, hoy));
            }
        }

        return new LoDelSistemaDelObispo(
            ElProximoGrupo(proxima, delProximoGrupo),
            LoVencidoPrimeroLoMasReciente(conTicket),
            LoQueApremiaPrimeroLoMasCercano(conTicket),
            DiasDeLaVentana);
    }

    /// <summary>
    /// La fecha del proximo grupo: el primer dia de hoy en adelante en el que viaja alguien.
    /// </summary>
    /// <remarks>
    /// «Proximo» es hoy o mas adelante: un grupo que viaja hoy todavia se puede salvar. Los
    /// archivados no cuentan —ese trabajo esta cerrado— pero siguen en la lista de lo
    /// vencido, que es otra pregunta.
    /// </remarks>
    /// <param name="todos">La base entera de casos.</param>
    /// <param name="hoy">El día de hoy; un grupo de hoy cuenta como próximo.</param>
    /// <returns>La fecha más cercana de hoy en adelante, o nulo si nadie viaja a partir de hoy.</returns>
    private static DateOnly? LaFechaDelProximoGrupo(IReadOnlyList<Caso> todos, DateOnly hoy)
    {
        DateOnly? proxima = null;
        foreach (var caso in todos)
        {
            if (caso.Archivado) continue;
            if (FechasEnEspanol.Leer(caso.FechaViaje) is not DateOnly fecha) continue;
            if (fecha < hoy) continue;
            if (proxima is null || fecha < proxima) proxima = fecha;
        }

        return proxima;
    }

    /// <summary>El reparto de estados de las personas de ese dia; nulo si no hay dia.</summary>
    /// <param name="fecha">El día del próximo grupo, o nulo si no hay ninguno.</param>
    /// <param name="personas">Las personas que viajan ese día, de todos sus documentos.</param>
    private static GrupoQueViene? ElProximoGrupo(DateOnly? fecha, IReadOnlyList<Persona> personas)
    {
        if (fecha is not DateOnly cual) return null;

        var estados = personas.Select(LasDosPreguntas.EstadoDe).ToList();
        return new GrupoQueViene(
            cual,
            personas.Count,
            estados.Count(e => e == true),
            estados.Count(e => e == false),
            estados.Count(e => e is null));
    }

    /// <summary>Compone el ticket de una persona: quien es, de que documento y en que paso se quedo.</summary>
    /// <param name="caso">El documento en el que va la persona.</param>
    /// <param name="persona">La persona sin la recomendación confirmada.</param>
    /// <param name="fecha">Su fecha de viaje, ya leída.</param>
    /// <param name="hoy">El día de hoy, para contar los días que faltan o que pasaron.</param>
    private static PersonaConTicket ArmarElTicket(Caso caso, Persona persona, DateOnly fecha, DateOnly hoy)
        => new(
            caso.Id,
            string.IsNullOrWhiteSpace(persona.Nombre) ? "sin nombre leído" : persona.Nombre.Trim(),
            caso.NumeroCaso ?? "sin número",
            caso.UnidadNombre ?? "sin unidad leída",
            FechasEnEspanol.Escribir(fecha),
            fecha.DayNumber - hoy.DayNumber,
            LasDosPreguntas.EstadoDe(persona),
            LasDosPreguntas.SeQuedoEn(persona));

    /// <summary>
    /// Quien ya viajo sin la recomendacion confirmada, lo que vencio hace menos primero.
    /// </summary>
    /// <remarks>
    /// ⛔ La regla, escrita para que dos personas la lean igual (C21-1): <b>un ticket esta
    /// vencido si su fecha de viaje ya paso y su estado no es «lista para viajar»</b>. Entran
    /// tambien las que nadie miro: dejarlas fuera seria dar por buenas a las que nadie miro,
    /// que es exactamente el daño que este programa existe para evitar.
    /// <para>
    /// ⚠️ El ADR-0006 §4.2 lo escribe como «no era lista para viajar EL DIA DEL VIAJE», y eso
    /// aqui no se puede cumplir al pie de la letra: el estado se deriva y no hay historial
    /// (decision A del ADR-0006 §2.4). Lo que el programa mira es el estado de AHORA.
    /// </para>
    /// <para>
    /// ⛔ Los archivados no llegan hasta aqui desde el 2026-09-06: para el dueno archivar ES
    /// resolver. Hasta ese dia entraban y se marcaban (criterio C21-2, ya deshecho).
    /// </para>
    /// </remarks>
    /// <param name="tickets">Todos los tickets armados, vencidos o no; aquí se filtran.</param>
    private static IReadOnlyList<PersonaConTicket> LoVencidoPrimeroLoMasReciente(
        IEnumerable<PersonaConTicket> tickets)
        => [.. tickets
            .Where(t => t.DiasHastaElViaje < 0)
            .OrderByDescending(t => t.DiasHastaElViaje)
            .ThenBy(t => t.NumeroCaso, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Quien viaja dentro de la ventana y sigue sin confirmar, lo que viaja antes primero.
    /// </summary>
    /// <remarks>
    /// ⛔ La ventana es <see cref="DiasDeLaVentana"/>, la MISMA que sombrea el calendario
    /// desde el criterio C1-4 (C21-4). No se inventa un segundo numero para «pronto»: dos
    /// numeros distintos en la misma pantalla es otra vez el programa diciendo dos cosas. Si
    /// el dueno quiere otro, es un numero en un sitio.
    /// <para>Los archivados no entran aqui, y desde el 2026-09-06 tampoco en lo vencido: ese
    /// trabajo esta cerrado. Se filtran antes, en <see cref="ArmarLoDelSistemaDelObispo"/>.</para>
    /// </remarks>
    /// <param name="tickets">Todos los tickets armados; aquí se quedan los de la ventana.</param>
    private static IReadOnlyList<PersonaConTicket> LoQueApremiaPrimeroLoMasCercano(
        IEnumerable<PersonaConTicket> tickets)
        => [.. tickets
            .Where(t => t.DiasHastaElViaje >= 0 && t.DiasHastaElViaje <= DiasDeLaVentana)
            .OrderBy(t => t.DiasHastaElViaje)
            .ThenBy(t => t.NumeroCaso, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// El grupo que viaja un dia, para que la pantalla del grupo lo lea por el mismo camino
    /// que el calendario que lleva hasta el.
    /// </summary>
    /// <param name="fecha">El día que se pulsó en el calendario.</param>
    public GrupoDelDia GrupoDelDia(DateOnly fecha) => _grupos.DelDia(fecha);

    /// <summary>
    /// Reparte cada documento en la lista que le toca: listo para asignar, o ya asignado.
    /// </summary>
    /// <remarks>
    /// <para><b>Las dos listas no se solapan y ninguna se traga a la otra.</b> Un documento
    /// que ya lleva alguien NO vuelve a salir como «listo para asignar» aunque no le falte
    /// nada: ofrecerselo otra vez seria pedirle que lo asigne dos veces. Y uno al que le
    /// falta algo no sale en ninguna de las dos: su sitio es la ventana de incompletos, que
    /// es donde el dueno pidio verlo.</para>
    ///
    /// <para><b>Tampoco sale como listo el que el companero devolvio marcado
    /// <c>no_completa</c></b>, aunque en el sistema no le falte ni un dato. Es la misma
    /// razon: el dueno llama «lo que no esta completo» exactamente a eso —la regla
    /// permanente 5 dice que ese estado lo escribe el Excel del companero, con su nombre—,
    /// y ponerlo a la vez en Inicio y en la ventana de incompletos seria decirle dos cosas
    /// distintas del mismo documento. Su camino es la segunda vuelta (fase C15), no
    /// volver a la cola de lo que esta listo.</para>
    /// </remarks>
    /// <param name="deTrabajo">Los casos sin archivar.</param>
    /// <param name="fechas">La fecha de viaje ya leída de cada caso, nula si no la tiene o no se entiende.</param>
    /// <param name="personasPorCaso">Las personas de cada caso, por id de caso.</param>
    /// <param name="duenos">Quién lleva cada caso, por id de caso; el que no está no lo lleva nadie.</param>
    /// <param name="hoy">El día de hoy.</param>
    /// <param name="procedencias">La procedencia de toda la base, leída de una pasada.</param>
    /// <param name="listos">Se llena con lo que nadie lleva y no le falta nada.</param>
    /// <param name="asignados">Se llena con lo que alguien lleva, le falte algo o no.</param>
    /// <param name="porCompletar">Se llena con lo que le falta algo, lo lleve alguien o no.</param>
    private static void Repartir(
        IReadOnlyList<Caso> deTrabajo,
        IReadOnlyDictionary<long, DateOnly?> fechas,
        IReadOnlyDictionary<long, List<Persona>> personasPorCaso,
        IReadOnlyDictionary<long, string> duenos,
        DateOnly hoy,
        ProcedenciasDeUnaPasada procedencias,
        List<RenglonDeCaso> listos,
        List<RenglonDeCaso> asignados,
        List<RenglonDeCaso> porCompletar)
    {
        foreach (var caso in deTrabajo)
        {
            var suyas = personasPorCaso.GetValueOrDefault(caso.Id) ?? [];
            var dueno = duenos.GetValueOrDefault(caso.Id, string.Empty);
            var renglon = ArmarRenglon(caso, fechas[caso.Id], hoy, suyas, dueno, procedencias);

            if (LeFaltaAlgo(renglon, caso)) porCompletar.Add(renglon);

            if (!string.IsNullOrEmpty(dueno)) asignados.Add(renglon);
            else if (!LeFaltaAlgo(renglon, caso)) listos.Add(renglon);
        }
    }

    /// <summary>
    /// Si a un documento le falta algo, con la MISMA regla que la ventana de incompletos.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Son dos cosas a la vez y las dos cuentan:</b> que le falte algun dato en el
    /// sistema —lo que impide armarle el paquete al companero— o que el Excel del companero lo
    /// haya devuelto marcado <c>no_completa</c>. Es palabra por palabra la condicion de
    /// <see cref="LectorDeIncompletos"/>, y esta escrita aparte para que se lea que es la misma
    /// y no una copia que alguien pueda cambiar en un sitio y en el otro no.</para>
    ///
    /// <para>⚠️ El «no cuenta como listo para asignar» ya era esta misma pregunta antes del
    /// 2026-09-07; lo unico que cambia es que ahora se le da nombre porque tambien la usa el
    /// cuadro. La lista de lo listo no cambia de contenido.</para>
    /// </remarks>
    /// <param name="renglon">El renglón ya armado, que trae cuántos datos le faltan.</param>
    /// <param name="caso">El caso, por el estado que dejó el Excel del compañero.</param>
    private static bool LeFaltaAlgo(RenglonDeCaso renglon, Caso caso)
        => renglon.CuantoLeFalta > 0 || caso.Estado == EstadoDeRecomendacion.NoCompleta;

    /// <summary>
    /// Ordena una lista por lo que viaja antes, que es la prioridad que el dueno declaro.
    /// </summary>
    /// <remarks>
    /// Sus palabras el 2026-09-05: <i>«la prioridad son los que viajarán pronto»</i>. El
    /// orden es el mismo que el criterio C1-4 fijo para la franja, y por el mismo motivo:
    /// primero lo que todavia se puede salvar, del mas cercano al mas lejano; detras lo que
    /// ya viajo, del que vencio hace menos al que vencio hace mas; y al final lo que no
    /// tiene fecha, que no desaparece (C7-4).
    /// </remarks>
    /// <param name="renglones">La lista sin ordenar; no se modifica, se devuelve otra.</param>
    private static IReadOnlyList<RenglonDeCaso> PorFechaDeViaje(List<RenglonDeCaso> renglones)
        => [.. renglones
            .OrderBy(r => r.SinFecha ? 2 : r.DiasHastaElViaje < 0 ? 1 : 0)
            .ThenBy(r => r.DiasHastaElViaje < 0 ? -r.DiasHastaElViaje : r.DiasHastaElViaje)
            .ThenBy(r => r.CasoId)];

    /// <summary>Trae la base entera de casos, archivados incluidos, en UNA sola consulta.</summary>
    private IReadOnlyList<Caso> LeerTodosLosCasos()
    {
        var filtro = FiltroDeCasos.Todo with { IncluirArchivados = true };
        return _casos.Listar(filtro, new Pagina(0, int.MaxValue)).Elementos;
    }

    /// <summary>
    /// Las personas de los documentos vivos, en UNA sola consulta.
    /// </summary>
    /// <remarks>
    /// Hace falta la persona entera —y no solo cuantas hay, que es lo que esta pantalla
    /// pedia antes— porque «listo para asignar» mira si cada persona trae su cedula y su
    /// nombre. Sigue siendo UNA consulta: preguntar por documento serian 2 766 idas a la
    /// base para pintar una pantalla.
    /// </remarks>
    /// <param name="deTrabajo">Los casos cuyas personas se quieren; las de otros casos se descartan.</param>
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

    /// <summary>
    /// Lee la fecha de viaje de cada caso una vez. Una fecha con forma rara NO tumba nada
    /// ni hace desaparecer el caso: se queda sin fecha y deja UNA linea de aviso (requisito 9).
    /// </summary>
    /// <param name="casos">Los casos cuya fecha se lee.</param>
    /// <param name="avisos">Donde se deja la línea de aviso si alguna fecha tiene forma rara.</param>
    private static Dictionary<long, DateOnly?> LeerLasFechas(IReadOnlyList<Caso> casos, List<Aviso> avisos)
    {
        var fechas = new Dictionary<long, DateOnly?>(casos.Count);
        var raras = 0;

        foreach (var caso in casos)
        {
            var fecha = FechasEnEspanol.Leer(caso.FechaViaje);
            fechas[caso.Id] = fecha;
            if (fecha is null && !string.IsNullOrWhiteSpace(caso.FechaViaje)) raras++;
        }

        if (raras > 0)
        {
            avisos.Add(Aviso.Advierte(
                Plural.Con(raras, "caso", "casos") + " con una fecha de viaje que no tiene la forma AAAA-MM-DD.",
                nameof(Caso.FechaViaje),
                "Se ensenan al final de sus listas y no salen en el calendario. "
                + "Nada se pierde: en cuanto se corrija la fecha, vuelven a su día."));
        }

        return fechas;
    }

    /// <summary>Contra que se comparan las dos cifras; el C1-1 exige decirlo siempre.</summary>
    /// <param name="enLaBase">Cuántos casos hay en total, archivados incluidos.</param>
    /// <param name="noArchivados">Cuántos quedan tras quitar los archivados.</param>
    private DenominadoresDeInicio ContarLosDenominadores(int enLaBase, int noArchivados)
        => new(
            enLaBase,
            noArchivados,
            _personas.Contar(FiltroDePersonas.Todo),
            _companeros.Activos().Count);

    /// <summary>Compone el renglon que se pinta, con sus textos ya en espanol.</summary>
    /// <param name="caso">El documento.</param>
    /// <param name="fecha">Su fecha de viaje ya leída, o nula.</param>
    /// <param name="hoy">El día de hoy, para los días que faltan.</param>
    /// <param name="suyas">Las personas del documento.</param>
    /// <param name="dueno">Quién lo lleva, o vacío si nadie.</param>
    /// <param name="procedencias">La procedencia de toda la base, para contar qué le falta.</param>
    private static RenglonDeCaso ArmarRenglon(
        Caso caso,
        DateOnly? fecha,
        DateOnly hoy,
        IReadOnlyList<Persona> suyas,
        string dueno,
        ProcedenciasDeUnaPasada procedencias)
        => new(
            caso.Id,
            caso.NumeroCaso ?? "sin número",
            caso.UnidadNombre ?? "sin unidad leída",
            fecha is DateOnly dia ? FechasEnEspanol.Escribir(dia) : null,
            fecha is DateOnly cuando ? cuando.DayNumber - hoy.DayNumber : 0,
            fecha is DateOnly paso && paso < hoy && caso.Estado != EstadoDeRecomendacion.Completa,
            fecha is null,
            suyas.Count,
            caso.Estado,
            LoQueLeFalta.DeUnDocumento(caso, suyas, procedencias).Count,
            dueno);

    /// <summary>El calendario del mes, con los grupos dentro y los archivados marcados (C7-2).</summary>
    /// <remarks>
    /// ⚠️ Se le pasan las personas YA LEIDAS. Desde el criterio C20-3 la pastilla cuenta
    /// personas confirmadas, y si el calendario volviera a leer la tabla, el mismo pintado
    /// la leeria dos veces: medido sobre 3 000 documentos y 16 500 personas, <b>362 ms</b>
    /// contra los <b>≤ 200 ms</b> del criterio C20-5. Pasandolas, <b>una sola lectura</b>.
    /// </remarks>
    /// <param name="hoy">El día de hoy, que se marca.</param>
    /// <param name="finDeLaVentana">El último día de la ventana sombreada.</param>
    /// <param name="desplazamientoDeMes">Cuántos meses mover respecto al de hoy.</param>
    /// <param name="personasPorCaso">Las personas ya leídas, para no volver a la base.</param>
    private MesDelCalendario ArmarElCalendario(
        DateOnly hoy,
        DateOnly finDeLaVentana,
        int desplazamientoDeMes,
        IReadOnlyDictionary<long, List<Persona>> personasPorCaso)
    {
        var porDia = _grupos.PorDia(personasPorCaso);
        var mesQueSeEnsena = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(desplazamientoDeMes);
        return CalendarioDelMes.Armar(mesQueSeEnsena, hoy, finDeLaVentana, porDia);
    }

    /// <summary>
    /// El cuadro de lo asignado: cada companero activo con lo que lleva, y al final los
    /// documentos que no lleva nadie. Los desactivados no salen: no reciben casos nuevos.
    /// </summary>
    /// <param name="deTrabajo">Los casos sin archivar, para contar los que no lleva nadie.</param>
    /// <param name="duenos">Quién lleva cada caso, por id de caso.</param>
    private IReadOnlyList<RenglonDeCompanero> ArmarElEquipo(
        IReadOnlyList<Caso> deTrabajo,
        IReadOnlyDictionary<long, string> duenos)
    {
        var equipo = _companeros.Activos()
            .Select(companero => new RenglonDeCompanero(
                companero.Id,
                companero.Nombre,
                _asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = companero.Id }),
                _asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = companero.Id, SinDevolver = true })))
            .OrderByDescending(r => r.SinDevolver)
            .ThenByDescending(r => r.Casos)
            .ThenBy(r => r.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        equipo.Add(new RenglonDeCompanero(
            0, "Sin asignar", deTrabajo.Count(c => !duenos.ContainsKey(c.Id)), 0));
        return equipo;
    }
}
