using System.Globalization;
using Fichas.App.Grupo;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>Una de las seis preguntas, con lo que hay contestado ahora mismo.</summary>
/// <param name="Rotulo">Como se llama, con su numero delante y en el orden del lider.</param>
/// <param name="Respuesta">Sí, no, o nulo: nadie la ha mirado.</param>
public sealed record PreguntaDeUnaPersona(string Rotulo, bool? Respuesta);

/// <summary>
/// El ticket de UNA persona: sus seis preguntas, quién las contestó y cómo está.
/// </summary>
/// <remarks>
/// Un documento puede traer diez personas y <b>cada una es un ticket aparte</b>: tres
/// pueden estar listas y siete no (ADR-0006 §2.1, y las palabras del dueño: <i>«es por
/// persona que se revisa la información»</i>).
/// </remarks>
/// <param name="PersonaId">El numero interno de la persona; es lo que se guarda.</param>
/// <param name="DeQuien">Su nombre, con su fila del formulario cuando se sabe.</param>
/// <param name="Cedula">Su MRN tal como esta leido, o que no se leyo. Nunca se inventa.</param>
/// <param name="Preguntas">Las seis, en el orden de la pantalla del lider.</param>
/// <param name="FraseDelEstado">Como esta, y en que paso se quedo si no esta lista.</param>
/// <param name="LineaDeLaFirma">Quien contesto y cuando, o que no ha contestado nadie.</param>
/// <param name="LoContestoOtro">Si lo que hay escrito lo puso alguien que no es quien mira.</param>
public sealed record TicketDeUnaPersona(
    long PersonaId,
    string DeQuien,
    string Cedula,
    IReadOnlyList<PreguntaDeUnaPersona> Preguntas,
    string FraseDelEstado,
    string LineaDeLaFirma,
    bool LoContestoOtro);

/// <summary>Un documento con los tickets de todas sus personas, ya en palabras.</summary>
/// <param name="CasoId">El documento del que se habla.</param>
/// <param name="Titulo">Lo que va en la barra de titulo de la ventana.</param>
/// <param name="Cabecera">Archivo, unidad y fecha de viaje, en una linea.</param>
/// <param name="Resumen">Cuantas personas de cuantas tienen la recomendacion confirmada.</param>
/// <param name="Personas">Un ticket por persona, en el orden del formulario.</param>
public sealed record DocumentoConPreguntas(
    long CasoId,
    string Titulo,
    string Cabecera,
    string Resumen,
    IReadOnlyList<TicketDeUnaPersona> Personas);

/// <summary>
/// Lo que se ve al abrir un documento desde Revisar para contestar sus seis preguntas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> El dueño lo pidió con estas palabras el 2026-09-05: <i>«en
/// Revisar ahí debe poder dar clic al documento, abrir otra ventana donde está la
/// información, comentarios o preguntas, para llegar a si se completó o no la información
/// en el sistema del obispo»</i>, y <i>«cada caso que tengo en revisión debe poder entrar y
/// completar las preguntas que dicen si está listo para viajar o no»</i>. Hasta hoy las seis
/// solo se podían contestar desde el Excel que va y vuelve.
/// </para>
/// <para>
/// ⛔ <b>Contestar estas seis NO es la firma de campos.</b> «Todo correcto» —
/// <c>procedencia_campo.verificado</c>— es de Miguel, campo por campo, y nunca automática
/// (regla permanente 5). Esta clase no conoce <c>IProcedencia</c> y no tiene por dónde
/// escribirla.
/// </para>
/// <para>
/// Vive fuera de cualquier control de XAML a propósito: así las frases que ve el dueño se
/// leen en una prueba sin abrir una ventana.
/// </para>
/// </remarks>
public static class PreguntasDeUnDocumento
{
    /// <summary>Los seis rótulos, en el orden de la pantalla del líder.</summary>
    /// <remarks>
    /// El orden no se altera: se copian de arriba abajo, y en otro orden hay que ir y venir.
    /// Están escritos aquí y también en <c>Fichas.Reportes.Reglas.Pasos</c>, cuyos rótulos
    /// son privados. Que no se separen no se deja a la buena fe: lo comprueba una prueba que
    /// los compara uno a uno.
    /// </remarks>
    public static IReadOnlyList<string> Rotulos { get; } =
    [
        "1. Preparación",
        "2. Información",
        "3. Cita del templo",
        "4. Acciones requeridas",
        "5. Entrevistas",
        "6. Listo para el templo",
    ];

    /// <summary>Lo que se lee cuando el papel no traía cédula.</summary>
    public const string SinCedula = "sin cédula leída";

    /// <summary>Lo que se lee cuando de esa persona no ha contestado nadie.</summary>
    public const string NadieHaContestado = "Nadie ha contestado estas seis preguntas todavía.";

    /// <summary>
    /// Monta el documento entero para la ventana.
    /// </summary>
    /// <param name="caso">El documento que se abrio.</param>
    /// <param name="personas">Sus personas, en el orden del formulario.</param>
    /// <param name="firmas">Quien contesto las seis de cada una; puede venir vacio.</param>
    /// <param name="equipo">Los companeros, para poder poner un nombre en vez de un numero.</param>
    /// <param name="quienMira">Quien tiene el programa abierto, o nulo si no se sabe.</param>
    public static DocumentoConPreguntas De(
        Caso caso,
        IReadOnlyList<Persona> personas,
        IReadOnlyDictionary<long, FirmaDeLosPasos> firmas,
        IReadOnlyList<Companero> equipo,
        Companero? quienMira)
    {
        ArgumentNullException.ThrowIfNull(caso);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(firmas);
        ArgumentNullException.ThrowIfNull(equipo);

        var tickets = personas
            .Select(persona => TicketDe(persona, Firma(firmas, persona.Id), equipo, quienMira))
            .ToList();

        return new DocumentoConPreguntas(
            CasoId: caso.Id,
            Titulo: Titulo(caso),
            Cabecera: Cabecera(caso),
            Resumen: Resumen(personas),
            Personas: tickets);
    }

    /// <summary>El ticket de una persona: sus seis, como esta y quien lo dijo.</summary>
    /// <param name="persona">La persona del ticket.</param>
    /// <param name="firma">Quién contestó sus seis; <c>SinFirmar</c> si nadie.</param>
    /// <param name="equipo">Los compañeros, para poner nombre a quien firmó.</param>
    /// <param name="quienMira">Quien tiene el programa abierto, o nulo; decide <c>LoContestoOtro</c>.</param>
    public static TicketDeUnaPersona TicketDe(
        Persona persona,
        FirmaDeLosPasos firma,
        IReadOnlyList<Companero> equipo,
        Companero? quienMira)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(firma);
        ArgumentNullException.ThrowIfNull(equipo);

        return new TicketDeUnaPersona(
            PersonaId: persona.Id,
            DeQuien: DeQuien(persona),
            Cedula: Cedula(persona),
            Preguntas: LasSeisDe(persona),
            FraseDelEstado: FraseDelEstadoDe(persona),
            LineaDeLaFirma: LineaDeLaFirma(firma, equipo),
            LoContestoOtro: firma.YaContesto && firma.Por != quienMira?.Id);
    }

    /// <summary>
    /// Cómo está esa persona, en la misma frase que la tarjeta de Revisar y el renglón del
    /// grupo: «resuelto · …», «me falta · le faltan 2 de 6: …», «me falta · se quedó en …».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El defecto que cierra, medido con la v16 y la v15 publicadas el 2026-09-17.</b> El
    /// dueño puso cuatro en «sí» y pulsó Guardar; la base escribió
    /// <c>paso_* = 1,1,1,1,NULL,NULL</c> con su firma, y esta ventana, al repintarse desde
    /// la base, decía de esa persona <i>«me falta · nadie ha contestado sus seis preguntas»</i>.
    /// Cuatro contestadas no es nadie, y una ventana que lo dice se lee como que no se guardó
    /// (<i>«las colocas, le das a Guardar y no se guarda»</i>). Se pasaban solo el estado y
    /// los «no»; sin las que no dicen «sí», <see cref="Fichas.App.Vocabulario.LoQueSeLeeDeUnaPersona"/>
    /// no puede saber que está a medias, y la tarjeta y el grupo ya se las pasaban desde el
    /// 2026-09-16.
    /// </para>
    /// <para>
    /// Es UNA frase para el ticket y para el acuse de «Guardadas las seis…»: dos frases de
    /// la misma persona en la misma ventana serían dos verdades.
    /// </para>
    /// </remarks>
    /// <param name="persona">La persona, releída de la base.</param>
    public static string FraseDelEstadoDe(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        var lectura = Fichas.App.Vocabulario.LoQueSeLeeDeUnaPersona.De(
            LasDosPreguntas.EstadoDe(persona),
            LasDosPreguntas.SeQuedoEn(persona),
            LasDosPreguntas.LasQueNoDicenSi(persona));

        return $"{lectura.Palabra} · {lectura.Detalle}";
    }

    /// <summary>Las seis de esa persona con su rótulo y su respuesta de ahora.</summary>
    /// <param name="persona">La persona cuyas seis columnas se leen.</param>
    public static IReadOnlyList<PreguntaDeUnaPersona> LasSeisDe(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        bool?[] respuestas =
        [
            persona.PasoPreparacion,
            persona.PasoInformacion,
            persona.PasoCitaDelTemplo,
            persona.PasoAccionesRequeridas,
            persona.PasoEntrevistas,
            persona.PasoListoParaElTemplo,
        ];

        return [.. Rotulos.Select((rotulo, i) => new PreguntaDeUnaPersona(rotulo, respuestas[i]))];
    }

    /// <summary>Las seis respuestas de la pantalla, listas para escribirse.</summary>
    /// <param name="preguntas">Las seis, en el orden de <see cref="Rotulos"/>; con otro número se lanza <see cref="ArgumentException"/>.</param>
    public static RespuestaALosPasos ComoSeGuarda(IReadOnlyList<PreguntaDeUnaPersona> preguntas)
    {
        ArgumentNullException.ThrowIfNull(preguntas);

        if (preguntas.Count != Rotulos.Count)
        {
            throw new ArgumentException(
                $"Son {Rotulos.Count} preguntas y llegaron {preguntas.Count}.", nameof(preguntas));
        }

        return new RespuestaALosPasos(
            preguntas[0].Respuesta,
            preguntas[1].Respuesta,
            preguntas[2].Respuesta,
            preguntas[3].Respuesta,
            preguntas[4].Respuesta,
            preguntas[5].Respuesta);
    }

    /// <summary>Las tres respuestas que se pueden elegir, en el orden del desplegable.</summary>
    /// <remarks>
    /// ⛔ Son TRES y no dos, y «sin mirar» va PRIMERA porque es como llegan todas: se puede
    /// volver a ella desde «sí» o desde «no». Si contestar fuera irreversible, nadie se
    /// atrevería a contestar (criterio C19-6).
    /// </remarks>
    public static IReadOnlyList<bool?> LasTresRespuestas { get; } = [null, true, false];

    /// <summary>«sin mirar», «sí» o «no»; la primera no es la tercera.</summary>
    /// <param name="respuesta">Sí, no, o nulo si nadie la ha mirado.</param>
    public static string DecirLaRespuesta(bool? respuesta) => respuesta switch
    {
        true => "sí",
        false => "no",
        _ => LasDosPreguntas.SinMirar,
    };

    /// <summary>Cuántas personas de cuántas tienen la recomendación confirmada.</summary>
    /// <remarks>
    /// Se cuenta en PERSONAS y no en documentos: una familia de cinco donde falla uno se
    /// contaría como «un documento no completo», y lo que él necesita saber es que es una
    /// persona de cinco. El denominador se ve siempre (criterio C1-1).
    /// </remarks>
    /// <param name="personas">Las personas del documento; con ninguna se dice que no se sacó ninguna.</param>
    public static string Resumen(IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(personas);

        if (personas.Count == 0) return "De este documento no se sacó ninguna persona.";

        var confirmadas = personas.Count(persona => Pasos.Estado(persona) == true);
        var cuantas = Plural.Con(confirmadas, "persona", "personas");

        return confirmadas == personas.Count
            ? $"Las {personas.Count.ToString(CultureInfo.InvariantCulture)} con la recomendación confirmada."
            : $"{cuantas} de {personas.Count.ToString(CultureInfo.InvariantCulture)} con la recomendación confirmada.";
    }

    /// <summary>Quién contestó y cuándo, o que no ha contestado nadie.</summary>
    /// <remarks>
    /// ⛔ Criterio C19-9. Si lo contestó un agente se dice CON SU NOMBRE antes de dejar
    /// cambiarlo: sobrescribir la respuesta de Sandy sin que se vea que era suya es la forma
    /// de que nadie sepa nunca de quién se fía. Y se dice también por qué vía, porque no es
    /// lo mismo que venga de su Excel que de esta pantalla.
    /// </remarks>
    /// <param name="firma">Quién contestó, cuándo y por qué vía.</param>
    /// <param name="equipo">Los compañeros, para decir el nombre y no el número.</param>
    public static string LineaDeLaFirma(FirmaDeLosPasos firma, IReadOnlyList<Companero> equipo)
    {
        ArgumentNullException.ThrowIfNull(firma);
        ArgumentNullException.ThrowIfNull(equipo);

        if (!firma.YaContesto) return NadieHaContestado;

        var partes = new List<string> { $"Contestó {Quien(firma.Por, equipo)}" };
        if (EnEspanol(firma.En) is string cuando) partes.Add($"el {cuando}");
        if (!string.IsNullOrWhiteSpace(firma.Origen)) partes.Add($"desde {firma.Origen.Trim()}");

        return string.Join(" · ", partes) + ".";
    }

    /// <summary>Qué compañero fue. Sin su fila, se dice su número y no se pierde el rastro.</summary>
    /// <param name="companeroId">El número del compañero que firmó, o nulo si no quedó anotado.</param>
    /// <param name="equipo">Los compañeros donde se busca su nombre.</param>
    private static string Quien(long? companeroId, IReadOnlyList<Companero> equipo)
    {
        if (companeroId is not long id) return "un compañero que no quedó anotado";

        var quien = equipo.FirstOrDefault(companero => companero.Id == id);
        return string.IsNullOrWhiteSpace(quien?.Nombre)
            ? $"el compañero n.º {id.ToString(CultureInfo.InvariantCulture)}"
            : quien.Nombre.Trim();
    }

    /// <summary>La fecha en español, o nulo si no se entiende.</summary>
    /// <remarks>
    /// Una fecha que no se entiende NO tumba la línea: lo importante —quién contestó— se
    /// sigue diciendo. Es la misma regla que ya sigue lo que contestó el compañero.
    /// </remarks>
    /// <param name="cuando">La fecha tal como la guardó la base.</param>
    private static string? EnEspanol(string? cuando)
    {
        if (string.IsNullOrWhiteSpace(cuando)) return null;
        return DateTime.TryParse(cuando, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante)
            ? instante.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-ES"))
            : null;
    }

    /// <summary>De quién se habla: su nombre y su fila del formulario.</summary>
    /// <remarks>
    /// La fila va siempre que se sepa: dos personas del mismo formulario pueden llamarse
    /// igual, y entonces el nombre solo no dice de cuál se habla.
    /// </remarks>
    /// <param name="persona">La persona de la que se habla.</param>
    private static string DeQuien(Persona persona)
    {
        var nombre = string.IsNullOrWhiteSpace(persona.Nombre)
            ? "una persona sin nombre leído"
            : persona.Nombre.Trim();

        return persona.FilaFormulario is int fila
            ? $"{nombre}, fila {fila.ToString(CultureInfo.InvariantCulture)}"
            : nombre;
    }

    /// <summary>Su cédula tal como está leída; nunca se inventa ni se completa.</summary>
    /// <param name="persona">La persona cuya cédula se lee.</param>
    private static string Cedula(Persona persona)
        => string.IsNullOrWhiteSpace(persona.Mrn) ? SinCedula : persona.Mrn.Trim();

    /// <summary>Lo que va en la barra de título: de qué documento es esta ventana.</summary>
    /// <remarks>
    /// Lleva el número de caso porque es lo que él lee primero en la tarjeta. Con varias
    /// ventanas abiertas a la vez, un título igual en todas no sirve para volver a ninguna.
    /// </remarks>
    /// <param name="caso">El documento de la ventana.</param>
    private static string Titulo(Caso caso)
    {
        var numero = string.IsNullOrWhiteSpace(caso.NumeroCaso)
            ? $"documento n.º {caso.Id.ToString(CultureInfo.InvariantCulture)}"
            : caso.NumeroCaso.Trim();

        return $"Las seis preguntas · {numero}";
    }

    /// <summary>Archivo, unidad y fecha de viaje, en una línea.</summary>
    /// <remarks>
    /// La fecha de viaje va aquí porque es lo que corre: él llama al obispo mirando cuántos
    /// días quedan. Lo que no se sabe se dice, no se deja en blanco.
    /// </remarks>
    /// <param name="caso">El documento de la ventana.</param>
    private static string Cabecera(Caso caso)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(caso.RutaPdf)) partes.Add(Path.GetFileName(caso.RutaPdf.Trim()));
        partes.Add(string.IsNullOrWhiteSpace(caso.UnidadNombre) ? "sin unidad leída" : caso.UnidadNombre.Trim());
        partes.Add(string.IsNullOrWhiteSpace(caso.FechaViaje) ? "sin fecha de viaje" : $"viaja el {caso.FechaViaje.Trim()}");

        return string.Join(" · ", partes);
    }

    /// <summary>La firma de una persona, o <c>SinFirmar</c> si el diccionario no la trae.</summary>
    /// <param name="firmas">Las firmas del documento, por número de persona.</param>
    /// <param name="personaId">La persona que se busca.</param>
    private static FirmaDeLosPasos Firma(IReadOnlyDictionary<long, FirmaDeLosPasos> firmas, long personaId)
        => firmas.TryGetValue(personaId, out var firma) ? firma : FirmaDeLosPasos.SinFirmar;
}
