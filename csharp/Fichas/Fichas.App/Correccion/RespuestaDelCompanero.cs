using System.Globalization;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Correccion;

/// <summary>Un paso del sistema del líder, con lo que el compañero contestó de él.</summary>
/// <param name="Rotulo">Como se llama el paso, con su numero delante.</param>
/// <param name="Respuesta">«sí», «no» o «sin contestar»; las tres son distintas.</param>
public sealed record PasoContestado(string Rotulo, string Respuesta);

/// <summary>
/// Lo que un companero contesto de UNA persona, ya en palabras para pintarlo.
/// </summary>
/// <param name="PersonaId">De quien se habla; el numero interno de la persona.</param>
/// <param name="DeQuien">Como se llama esa persona, con su fila del formulario.</param>
/// <param name="Quien">
/// Que companero propuso el ESTADO desde su Excel; su nombre, o su numero si ya no esta.
/// Vacio si su Excel todavia no ha vuelto. ⛔ NO es quien contesto las seis preguntas.
/// </param>
/// <param name="Cuando">Cuando lo propuso, en espanol; vacio si la fecha no se entiende.</param>
/// <param name="Estado">Lo que dijo del conjunto: «completa», «no completa» o lo que escribiera.</param>
/// <param name="Pasos">Los seis pasos con su respuesta, en el orden de la pantalla del lider.</param>
/// <param name="LineaDeLosPasos">
/// Quien contesto las seis, cuando y por que via, o que no las ha contestado nadie. Va
/// aparte de <paramref name="Resumen"/> porque son dos firmas distintas de la misma fila.
/// </param>
/// <param name="Nota">La nota libre que escribio; vacia si no escribio ninguna.</param>
/// <param name="LlamoAlLider">Si llamo al lider, dicho con palabras; vacio si no consta.</param>
/// <param name="Resumen">Lo que trajo el Excel del companero, en una linea.</param>
public sealed record RespuestaDelCompanero(
    long PersonaId,
    string DeQuien,
    string Quien,
    string Cuando,
    string Estado,
    IReadOnlyList<PasoContestado> Pasos,
    string LineaDeLosPasos,
    string Nota,
    string LlamoAlLider,
    string Resumen);

/// <summary>
/// Lee de una persona lo que el Excel del companero dejo escrito, y lo pone en palabras.
/// </summary>
/// <remarks>
/// <para>Existe por una queja del dueno: <i>«cuando tomar el Excel del agente de vuelta no
/// hace ningun cambio en el sistema; debe hacer un cambio de si esta completo o no de la
/// persona»</i>. Medido el 2026-09-05, la causa no era la que parecia: <b>el Excel SI
/// escribe</b> —sobre siete escaneos reales, 7 casos pasan de nulo a <c>completa</c> o
/// <c>no_completa</c> con quien y cuando, y 7 personas quedan con sus seis pasos—. Lo que
/// fallaba es que <b>ninguna pantalla lo leia</b>: un <c>grep</c> de
/// <c>PasoPreparacion|PasoListoParaElTemplo|EstadoPropuesto|PropuestoPor|NotaCompanero</c>
/// sobre todo <c>Fichas.App</c> daba <b>cero</b> coincidencias.</para>
///
/// <para>⛔ <b>Esto no firma nada y no toca ni una fila.</b> Solo lee. Lo que dice el
/// companero es suyo; la firma de campos —<c>verificado</c>— es de Miguel y nunca es
/// automatica (regla permanente 5, precisada por el dueno el 2026-09-03). Son dos cosas y
/// aqui no se mezclan: esta clase no conoce <c>IProcedencia</c> ni tiene por donde
/// escribir.</para>
///
/// <para>⚠️ <b>Los seis rotulos estan escritos aqui y tambien en
/// <c>Fichas.Reportes.Reglas.Pasos</c>.</b> No se referencia aquella porque las pantallas
/// solo conocen <c>Fichas.Contratos</c> (Fichas.App.csproj), y sus rotulos son privados.
/// Que no se separen NO se deja a la buena fe: la prueba
/// <c>LosSeisRotulosSonLosMismosQueLosDeReportes</c> los compara uno a uno.</para>
///
/// <para>Vive fuera de cualquier control de XAML a proposito: asi las frases que ve Miguel
/// se leen en una prueba sin abrir una ventana.</para>
/// </remarks>
public static class LoQueContestoElCompanero
{
    /// <summary>
    /// Lo que se dice de la via por la que el Excel del companero contesta las seis.
    /// </summary>
    /// <remarks>
    /// ⚠️ Esto NO se guarda en <c>pasos_origen</c>: es la frase con la que se cuenta lo que
    /// hizo <c>IPersonas.AnotarPropuesta</c>, que escribe los seis <c>paso_*</c> junto a
    /// <c>propuesto_por</c> en el mismo UPDATE y deja las tres columnas de la migracion 19
    /// vacias. Es la unica via que puede dejar las seis escritas sin firma, y por eso una
    /// fila asi se puede atribuir a quien devolvio el Excel sin adivinar nada.
    /// </remarks>
    public const string ElExcelDeVuelta = "su Excel de vuelta";

    /// <summary>Lo que se dice cuando el Excel del companero todavia no ha vuelto.</summary>
    /// <remarks>
    /// ⛔ Antes esta persona salia con «Contestó un compañero que no quedó anotado», que le
    /// atribuye al companero un trabajo que no hizo y hace creer que su Excel ya volvio.
    /// </remarks>
    public const string TodaviaNoHaVuelto = "El compañero todavía no ha devuelto su Excel de esta persona.";

    /// <summary>
    /// Los seis pasos de «Preparación para las ordenanzas», con su rótulo y en su orden.
    /// </summary>
    /// <remarks>
    /// El orden es el de la pantalla del líder, y no se altera: el compañero los copia de
    /// arriba abajo, y en otro orden hay que ir y venir para comprobarlos. Son las seis que
    /// el dueno confirmo el 2026-09-05 como las de su sistema (<c>DECISIONES.md</c>, «Las
    /// seis preguntas SON las del sistema del obispo»).
    /// <para>
    /// ⚠️ NO son las seis ordenanzas del formulario —las <c>Ord*</c> de <see cref="Persona"/>—:
    /// aquellas dicen a QUÉ va la persona al templo, y éstas si está en condiciones de ir.
    /// </para>
    /// <para>
    /// Hasta el 2026-09-11 este comentario colgaba de <see cref="ElExcelDeVuelta"/>, la
    /// constante de encima, porque se metio otra declaracion en medio sin mover el bloque.
    /// </para>
    /// </remarks>
    private static readonly (Func<Persona, bool?> Leer, string Rotulo)[] LosSeisPasos =
    [
        (persona => persona.PasoPreparacion, "1. Preparación"),
        (persona => persona.PasoInformacion, "2. Información"),
        (persona => persona.PasoCitaDelTemplo, "3. Cita del templo"),
        (persona => persona.PasoAccionesRequeridas, "4. Acciones requeridas"),
        (persona => persona.PasoEntrevistas, "5. Entrevistas"),
        (persona => persona.PasoListoParaElTemplo, "6. Listo para el templo"),
    ];

    /// <summary>Los rótulos de los seis pasos, para poder compararlos desde una prueba.</summary>
    public static IReadOnlyList<string> RotulosDeLosPasos { get; } =
        [.. LosSeisPasos.Select(paso => paso.Rotulo)];

    /// <summary>
    /// Si el compañero ya contestó algo de esta persona.
    /// </summary>
    /// <remarks>
    /// Basta con que haya contestado UNA cosa. Exigir el estado o los seis pasos dejaría
    /// «contestó a medias» viéndose igual que «no contestó», que es justo la distinción que
    /// el dueño pidió poder hacer sin abrir el documento.
    /// </remarks>
    /// <param name="persona">La persona tal como esta en la base.</param>
    /// <exception cref="ArgumentNullException">Si la persona es nula.</exception>
    public static bool YaContesto(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return persona.EstadoPropuesto is not null
            || persona.NotaCompanero is not null
            || persona.PropuestoPor is not null
            || persona.PropuestoEn is not null
            || persona.LlamoAlLider is not null
            || LosSeisPasos.Any(paso => paso.Leer(persona) is not null);
    }

    /// <summary>Por cuántas personas de esa lista contestó ya el compañero.</summary>
    /// <param name="personas">Las personas del documento.</param>
    /// <exception cref="ArgumentNullException">Si la lista es nula.</exception>
    public static int CuantasContestadas(IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(personas);
        return personas.Count(YaContesto);
    }

    /// <summary>
    /// Lo que el compañero contestó de esa persona, o nulo si todavía no contestó nada.
    /// </summary>
    /// <param name="persona">La persona de la que se pregunta.</param>
    /// <param name="companero">Quién propuso el estado desde su Excel, o nulo si ya no está.</param>
    /// <param name="firma">Quién contestó las seis dentro del programa; puede ir sin firmar.</param>
    /// <param name="equipo">
    /// Los compañeros que se conocen, para poner un nombre en vez de un número. Puede ir
    /// vacío: entonces se dice el número y no se pierde el rastro.
    /// </param>
    /// <returns>La respuesta ya en palabras, o nulo si de esa persona no consta nada.</returns>
    /// <exception cref="ArgumentNullException">Si la persona, la firma o el equipo son nulos.</exception>
    public static RespuestaDelCompanero? De(
        Persona persona,
        Companero? companero,
        FirmaDeLosPasos firma,
        IReadOnlyList<Companero> equipo)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(firma);
        ArgumentNullException.ThrowIfNull(equipo);
        if (!YaContesto(persona)) return null;

        var devolvioSuExcel = DevolvioSuExcel(persona);
        var quien = devolvioSuExcel ? Quien(persona, companero) : string.Empty;
        var cuando = Cuando(persona.PropuestoEn);
        var estado = Estado(persona.EstadoPropuesto);

        return new RespuestaDelCompanero(
            PersonaId: persona.Id,
            DeQuien: DeQuien(persona),
            Quien: quien,
            Cuando: cuando,
            Estado: estado,
            Pasos: [.. LosSeisPasos.Select(paso => new PasoContestado(paso.Rotulo, Respuesta(paso.Leer(persona))))],
            LineaDeLosPasos: LineaDeLosPasos(persona, firma, equipo),
            Nota: persona.NotaCompanero?.Trim() ?? string.Empty,
            LlamoAlLider: LlamoAlLider(persona.LlamoAlLider),
            Resumen: devolvioSuExcel ? Resumen(quien, cuando, estado) : TodaviaNoHaVuelto);
    }

    /// <summary>Si el Excel del compañero ya volvió con algo de esta persona.</summary>
    /// <remarks>
    /// ⛔ <b>Los seis <c>paso_*</c> NO cuentan aquí, y ese es el arreglo.</b> Desde la
    /// migración 19 los escribe también la pantalla, así que tomarlos como señal de que el
    /// Excel volvió es justo lo que hacía que las respuestas de Miguel salieran a nombre del
    /// compañero.
    /// </remarks>
    /// <param name="persona">La persona tal como esta en la base.</param>
    private static bool DevolvioSuExcel(Persona persona)
        => persona.EstadoPropuesto is not null
        || persona.NotaCompanero is not null
        || persona.PropuestoPor is not null
        || persona.PropuestoEn is not null
        || persona.LlamoAlLider is not null;

    /// <summary>
    /// Quién contestó las seis preguntas, cuándo y por qué vía; o que no las contestó nadie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Es la frase que cierra el defecto grave.</b> Las seis se pintaban bajo el nombre
    /// de <c>propuesto_por</c>, que es quien propuso el ESTADO desde su Excel. En cuanto
    /// Miguel contestara una pregunta, esa pantalla enseñaba SUS respuestas con el nombre de
    /// Sandy. Atribuirle a alguien algo que no hizo rompe la única separación que este
    /// programa vende: lo del compañero es suyo, lo de Miguel es suyo.
    /// </para>
    /// <para>
    /// La frase se compone con <see cref="PreguntasDeUnDocumento.LineaDeLaFirma"/>, que es la
    /// misma que ya usa la ventana de las seis preguntas. <b>Escribirla dos veces sería que
    /// las dos pantallas dijeran cosas distintas del mismo dato.</b>
    /// </para>
    /// </remarks>
    /// <param name="persona">La persona, de la que se cuentan los seis <c>paso_*</c> contestados.</param>
    /// <param name="firma">La firma de la pantalla; puede ir sin firmar.</param>
    /// <param name="equipo">Los companeros conocidos, para poner nombre.</param>
    private static string LineaDeLosPasos(
        Persona persona, FirmaDeLosPasos firma, IReadOnlyList<Companero> equipo)
    {
        var contestados = LosSeisPasos.Count(paso => paso.Leer(persona) is not null);
        var (queVale, laDeAntes) = QueFirmaEscribioLasSeis(persona, firma, contestados);

        if (queVale is null)
        {
            return contestados == 0
                ? PreguntasDeUnDocumento.NadieHaContestado
                : $"{Cuantas(contestados)}, pero no quedó anotado quién las contestó.";
        }

        var linea = $"{Cuantas(contestados)}. {PreguntasDeUnDocumento.LineaDeLaFirma(queVale, equipo)}";
        return laDeAntes is null
            ? linea
            : $"{linea} Antes las había contestado {NombreDe(laDeAntes, equipo)}.";
    }

    /// <summary>«4 de 6 contestadas»; el denominador va siempre (criterio C1-1).</summary>
    /// <param name="contestados">Cuantos de los seis tienen respuesta.</param>
    private static string Cuantas(int contestados)
        => $"{contestados.ToString(CultureInfo.InvariantCulture)} de "
         + $"{LosSeisPasos.Length.ToString(CultureInfo.InvariantCulture)} contestadas";

    /// <summary>
    /// Cuál de las dos vías escribió los seis <c>paso_*</c> que se están viendo, y cuál los
    /// había escrito antes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Las dos escriben las MISMAS seis columnas y ninguna borra la firma de la otra:
    /// <c>AnotarPropuesta</c> —el Excel— escribe los seis junto a <c>propuesto_por</c> y no
    /// toca <c>pasos_por</c>; <c>ResponderLosPasos</c> —la pantalla— escribe los seis y las
    /// tres de la migración 19 y no toca <c>propuesto_por</c>. Con las dos firmas puestas,
    /// <b>la última escritura es la que se está viendo</b>, y se decide por la fecha.
    /// </para>
    /// <para>
    /// ⚠️ <b>Esto tapa un agujero de la base, y no lo cierra.</b> Que <c>AnotarPropuesta</c>
    /// pise los seis dejando <c>pasos_por</c> apuntando a otro es un defecto de
    /// <c>Fichas.Datos/Repositorios/RepositorioDePersonas.cs</c>, que no es de esta pantalla.
    /// Aquí solo se evita afirmar lo que no consta, y por eso se nombra también a quien había
    /// contestado antes: su rastro no desaparece.
    /// </para>
    /// <para>
    /// Si alguna de las dos fechas no se entiende no se ordena nada y manda la firma de la
    /// pantalla, que es la única de las dos que se puso ahí para decir quién contestó las
    /// seis. Adivinar un orden con una fecha ilegible sería inventar.
    /// </para>
    /// <para>
    /// ⛔ <b>Y las dos firmas no valen lo mismo con las seis en blanco.</b> Un compañero
    /// puede devolver su Excel diciendo el estado y no contestar ninguna de las seis:
    /// <c>propuesto_por</c> queda puesto y las seis vacías, y decir «contestó Sandy» ahí le
    /// atribuye un trabajo que no hizo. <c>pasos_por</c> sí vale con las seis en blanco:
    /// esa columna existe justo para decir que alguien las tocó, y dejarlas en blanco a
    /// propósito es una respuesta (criterio C19-6).
    /// </para>
    /// </remarks>
    /// <param name="persona">La persona, de la que se lee <c>propuesto_por</c> y <c>propuesto_en</c>.</param>
    /// <param name="firma">La firma de la pantalla —<c>pasos_por</c> y <c>pasos_en</c>—.</param>
    /// <param name="contestados">Cuantos de los seis tienen respuesta; con cero, la del Excel no vale.</param>
    /// <returns>La firma que se esta viendo y, si hubo otra antes, cual era.</returns>
    private static (FirmaDeLosPasos? QueVale, FirmaDeLosPasos? LaDeAntes) QueFirmaEscribioLasSeis(
        Persona persona, FirmaDeLosPasos firma, int contestados)
    {
        var deLaPantalla = firma.YaContesto ? firma : null;
        var delExcel = persona.PropuestoPor is long por && contestados > 0
            ? new FirmaDeLosPasos(por, persona.PropuestoEn, ElExcelDeVuelta)
            : null;

        if (deLaPantalla is null) return (delExcel, null);
        if (delExcel is null) return (deLaPantalla, null);

        return ElExcelLlegoDespues(delExcel.En, deLaPantalla.En)
            ? (delExcel, deLaPantalla)
            : (deLaPantalla, delExcel);
    }

    /// <summary>Si la escritura del Excel es posterior; falso cuando no se puede saber.</summary>
    /// <param name="delExcel">La marca de tiempo del Excel de vuelta.</param>
    /// <param name="deLaPantalla">La marca de tiempo de la pantalla de las seis.</param>
    private static bool ElExcelLlegoDespues(string? delExcel, string? deLaPantalla)
        => Instante(delExcel) is DateTime uno
        && Instante(deLaPantalla) is DateTime otro
        && uno > otro;

    /// <summary>Una marca de tiempo leída, o nulo si no se entiende.</summary>
    /// <param name="texto">Una marca de tiempo tal como esta guardada; nula o en blanco devuelve nulo.</param>
    private static DateTime? Instante(string? texto)
    {
        if (ReglasDeCampo.Limpiar(texto) is not string limpio) return null;
        return DateTime.TryParse(limpio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante)
            ? instante
            : null;
    }

    /// <summary>El nombre de quien firmó, o su número si ya no está en la base.</summary>
    /// <param name="firma">De quien se busca el nombre.</param>
    /// <param name="equipo">Donde se busca; si no esta, se dice su numero.</param>
    private static string NombreDe(FirmaDeLosPasos firma, IReadOnlyList<Companero> equipo)
    {
        if (firma.Por is not long id) return "un compañero que no quedó anotado";
        var quien = equipo.FirstOrDefault(companero => companero.Id == id);
        return ReglasDeCampo.Limpiar(quien?.Nombre)
            ?? $"el compañero n.º {id.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>De quién se habla: su nombre y su fila del formulario.</summary>
    /// <remarks>
    /// La fila va siempre que se sepa, incluso con el nombre delante: dos personas del mismo
    /// formulario pueden llamarse igual, y entonces el nombre solo no dice de cuál se habla.
    /// </remarks>
    /// <param name="persona">La persona; sin nombre leido se dice asi, no se inventa uno.</param>
    private static string DeQuien(Persona persona)
    {
        var nombre = ReglasDeCampo.Limpiar(persona.Nombre) ?? "una persona sin nombre leído";
        return persona.FilaFormulario is int fila ? $"{nombre}, fila {fila}" : nombre;
    }

    /// <summary>
    /// Qué compañero contestó. Sin su fila, se dice su número y no se pierde la respuesta.
    /// </summary>
    /// <remarks>
    /// Tirar la respuesta entera por no poder poner un nombre sería perder justo el dato que
    /// esto viene a enseñar. Y decir «alguien» a secas no deja rastro por donde tirar.
    /// </remarks>
    /// <param name="persona">La persona, por si hay que decir el numero de <c>propuesto_por</c>.</param>
    /// <param name="companero">El companero ya resuelto, o nulo si no esta en la base.</param>
    private static string Quien(Persona persona, Companero? companero)
    {
        if (companero is not null && ReglasDeCampo.Limpiar(companero.Nombre) is string nombre) return nombre;
        return persona.PropuestoPor is long id
            ? $"el compañero n.º {id.ToString(CultureInfo.InvariantCulture)}"
            : "un compañero que no quedó anotado";
    }

    /// <summary>Cuándo lo contestó, en español; vacío si la fecha no se entiende.</summary>
    /// <remarks>
    /// Una fecha que no se entiende NO tumba la respuesta (requisito 9 del dueño): se queda
    /// sin fecha, y lo importante —qué contestó y quién— se sigue diciendo.
    /// </remarks>
    /// <param name="propuestoEn">La marca de tiempo del Excel, tal como esta guardada.</param>
    private static string Cuando(string? propuestoEn)
    {
        if (ReglasDeCampo.Limpiar(propuestoEn) is not string texto) return string.Empty;
        return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante)
            ? instante.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-ES"))
            : string.Empty;
    }

    /// <summary>
    /// Lo que dijo del conjunto, en palabras.
    /// </summary>
    /// <remarks>
    /// La columna es texto libre y NO una lista cerrada: lo que el compañero escriba se
    /// enseña tal cual. Solo se traducen los dos valores que escribe el propio programa al
    /// leer el Excel de vuelta, porque «no_completa» con guion bajo no es español.
    /// </remarks>
    /// <param name="estadoPropuesto">Lo que dice <c>estado_propuesto</c>; nulo dice que no lo dijo.</param>
    private static string Estado(string? estadoPropuesto)
    {
        var texto = ReglasDeCampo.Limpiar(estadoPropuesto);
        if (texto is null) return "no dijo si está completa";
        return texto switch
        {
            "no_completa" => "no completa",
            _ => texto,
        };
    }

    /// <summary>Las tres respuestas posibles, y la tercera no es «no».</summary>
    /// <remarks>
    /// ⛔ Una casilla en blanco es una pregunta que nadie miró, y esa persona queda a medias,
    /// no reprobada. Pintarla como «no» le inventaría al compañero una respuesta que no dio,
    /// y eso es exactamente lo que prohíbe la regla permanente 1.
    /// </remarks>
    /// <param name="contestado">Lo que hay en la columna del paso: si, no o nada.</param>
    private static string Respuesta(bool? contestado) => contestado switch
    {
        true => "sí",
        false => "no",
        null => "sin contestar",
    };

    /// <summary>Si llamó al líder, dicho con palabras; vacío si no consta.</summary>
    /// <remarks>NO es un séptimo paso y no cuenta como tal: por eso va aparte de la lista.</remarks>
    /// <param name="llamo">Lo que hay en <c>llamo_al_lider</c>: si, no o nada.</param>
    private static string LlamoAlLider(bool? llamo) => llamo switch
    {
        true => "El compañero llamó al líder.",
        false => "El compañero no llamó al líder.",
        null => string.Empty,
    };

    /// <summary>Lo que trajo el Excel del compañero: quién, cuándo y qué dijo.</summary>
    /// <remarks>
    /// ⛔ <b>Ya no cuenta pasos.</b> La cuenta de los seis va en
    /// <see cref="RespuestaDelCompanero.LineaDeLosPasos"/>, con el nombre de quien los
    /// contestó. Mezclarlas era lo que hacía que los pasos de Miguel se leyeran detrás del
    /// nombre de Sandy.
    /// </remarks>
    /// <param name="quien">Quien propuso el estado, ya en palabras.</param>
    /// <param name="cuando">Cuando, en espanol; vacio se omite.</param>
    /// <param name="estado">Lo que dijo del conjunto, ya en palabras.</param>
    private static string Resumen(string quien, string cuando, string estado)
    {
        var partes = new List<string> { $"Contestó {quien}" };
        if (cuando.Length > 0) partes.Add($"el {cuando}");
        partes.Add(estado);
        return string.Join(" · ", partes);
    }
}
