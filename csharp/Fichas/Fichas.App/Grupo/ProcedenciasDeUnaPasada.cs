using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Grupo;

/// <summary>
/// De donde salio cada campo de muchos documentos, leido de una vez y preguntado por campo.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Hasta el 2026-09-06 la cola de Completar decidia si a un
/// documento le faltaba algo mirando SOLO si sus campos estaban vacios y si cumplian su
/// forma, y la pantalla de Correccion miraba ademas la confianza, el tachon y la marca de
/// «no está en el papel». Eran dos veredictos distintos sobre el mismo documento y
/// <c>DECISIONES.md</c> midio seis casos donde contestaban al reves. Unificarlos exige que
/// la cola sepa lo que Correccion sabe, y eso es la procedencia.</para>
///
/// <para><b>Por que en bloque y no documento a documento.</b> Medido sobre los 3 000
/// documentos de la base inventada: preguntar con <c>IProcedencia.DeRegistro</c> uno a uno
/// cuesta unas diez veces mas que las dos consultas en bloque, y sobre las 18 000 lecturas
/// reales otros dos programadores midieron <b>4,4 s</b> contra <b>50 ms</b>. Una pantalla
/// que pinta 2 766 renglones no puede preguntar 2 766 veces.</para>
///
/// <para>⛔ <b>La respuesta la da <see cref="EstadosDeCampo.EsDudoso"/> y no esta clase.</b>
/// Es la MISMA funcion que llama la pantalla de Correccion, y ese es el punto entero: con
/// dos funciones, la divergencia vuelve el dia que alguien toque una; con una, no puede
/// volver. Aqui solo se reune lo que esa funcion necesita para contestar.</para>
///
/// <para>⛔ <b>De aqui no sale ni una escritura.</b> Es una lectura: la regla permanente 5
/// sigue entera y ni un campo pasa a <c>verificado = 1</c> por este camino.</para>
/// </remarks>
public sealed class ProcedenciasDeUnaPasada
{
    /// <summary>
    /// Las filas que se tienen en la mano, por la clave (tabla, registro, campo).
    /// </summary>
    /// <remarks>
    /// Por el camino en bloque estan SOLO las que pesan —unas 3 500 de las 29 784 de la base
    /// inventada—; por el de un documento estan todas las suyas. Las dos formas contestan lo
    /// mismo, y eso no es una suposicion: lo fija
    /// <c>PruebasDeLasDosPasadasDeProcedencia</c> comparando las dos sobre los 3 000
    /// documentos, campo por campo.
    /// </remarks>
    private readonly Dictionary<ClaveDeCampo, ProcedenciaDeCampo> _filas;

    /// <summary>Que registros tienen ALGUNA fila que pese; casi ninguno la tiene.</summary>
    /// <remarks>
    /// ⚠️ <b>Es un atajo medido, no una precaucion.</b> De los 10 297 registros de la base
    /// inventada, solo unos 2 700 traen alguna fila que pese: para los otros siete mil y pico
    /// la busqueda en <see cref="_filas"/> —que hay que hacer campo por campo, y son 28 900
    /// campos— siempre iba a fallar. Preguntando antes por el registro, que es una clave sin
    /// texto que resumir, esas busquedas no se hacen.
    /// </remarks>
    private readonly HashSet<(TablaDeProcedencia Tabla, long RegistroId)> _registrosQuePesan;

    /// <summary>Que campos del CASO tienen fila, por id de caso.</summary>
    /// <remarks>
    /// <para>Sin esto, un campo con valor y SIN fila —uno de los seis casos medidos— no se
    /// distingue de uno que se leyo bien: los dos faltarian de <see cref="_filas"/>. Y son lo
    /// contrario: del primero no consta de donde salio su valor, y encima no se puede firmar,
    /// porque <c>IProcedencia.Firmar</c> es un <c>UPDATE</c> y cambia cero filas.</para>
    ///
    /// <para>⚠️ <b>Se guarda tal como lo devuelve el puerto, y eso se midio.</b> Volcarlo a un
    /// conjunto de claves costaba montar <b>29 784</b> claves para consultar unas pocas por
    /// documento, y con eso Inicio subia a <b>170,0 ms</b> contra un techo de 200. Buscando en
    /// el diccionario que ya viene hecho no se construye ninguna: cada campo son una busqueda
    /// y como mucho cinco comparaciones de texto.</para>
    /// </remarks>
    private readonly IReadOnlyDictionary<long, IReadOnlyList<string>> _camposDeCasos;

    /// <summary>Que campos de cada PERSONA tienen fila, por id de persona.</summary>
    private readonly IReadOnlyDictionary<long, IReadOnlyList<string>> _camposDePersonas;

    /// <summary>Guarda lo leído y deriva qué registros pesan; solo se llega por las dos lecturas y por <see cref="Ninguna"/>.</summary>
    /// <param name="filas">Las filas que pesan en el veredicto, por su clave.</param>
    /// <param name="camposDeCasos">Qué campos de cada caso tienen fila.</param>
    /// <param name="camposDePersonas">Qué campos de cada persona tienen fila.</param>
    private ProcedenciasDeUnaPasada(
        Dictionary<ClaveDeCampo, ProcedenciaDeCampo> filas,
        IReadOnlyDictionary<long, IReadOnlyList<string>> camposDeCasos,
        IReadOnlyDictionary<long, IReadOnlyList<string>> camposDePersonas)
    {
        _filas = filas;
        _camposDeCasos = camposDeCasos;
        _camposDePersonas = camposDePersonas;
        _registrosQuePesan = [.. filas.Keys.Select(clave => (clave.Tabla, clave.RegistroId))];
    }

    /// <summary>La clave de la tabla <c>procedencia_campo</c>: (tabla, registro_id, campo).</summary>
    /// <remarks>
    /// ⚠️ <b>Es una estructura y no un texto, y eso tambien se midio.</b> Con la clave de
    /// texto de la pantalla de Correccion —<c>CampoEnPantalla.ClaveDe</c>— montar esta pasada
    /// costaba <b>113,8 ms</b> en vez de los <b>72,7 ms</b> de la estructura: se formateaban
    /// miles de cadenas para tirarlas acto seguido. La pantalla de Correccion se queda con la
    /// suya, que alli son once campos y ademas la lee el XAML.
    /// </remarks>
    /// <param name="Tabla">Si la fila es del caso o de una persona.</param>
    /// <param name="RegistroId">El número interno de la fila.</param>
    /// <param name="Campo">El nombre de la columna, en español y con guion bajo.</param>
    private readonly record struct ClaveDeCampo(TablaDeProcedencia Tabla, long RegistroId, string Campo);

    /// <summary>
    /// Una fila cualquiera de las que NO pesan, para preguntarle a la misma funcion.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esto no inventa un dato: nombra lo que el filtro ya dijo.</b> Que una fila no
    /// salga de <c>LasQuePesanEnElVeredicto</c> significa las cinco cosas a la vez —sin
    /// firma, presente en el papel, sin tachon, con confianza y por encima del umbral—, y
    /// para <see cref="EstadosDeCampo.EsDudoso"/> todas las filas asi contestan igual: no son
    /// dudosas mientras su valor este puesto y cumpla su forma. Esta es una de ellas.
    /// <para>
    /// Se pasa a la funcion de verdad en vez de escribir aqui esa conclusion porque escribirla
    /// seria una segunda copia de la regla, que es el defecto que este archivo cierra.
    /// </para>
    /// </remarks>
    private static readonly ProcedenciaDeCampo UnaQueNoPesa =
        new() { Origen = OrigenDeCampo.Ocr, Confianza = 1.0 };

    /// <summary>
    /// Lee de una vez la procedencia de TODA la base, con las consultas en bloque.
    /// </summary>
    /// <param name="procedencia">El puerto.</param>
    /// <param name="umbral">Por debajo de esta confianza una fila pesa; por defecto, 0,6.</param>
    public static ProcedenciasDeUnaPasada DeTodaLaBase(
        IProcedencia procedencia, double umbral = EstadosDeCampo.UmbralDeConfianzaBaja)
    {
        ArgumentNullException.ThrowIfNull(procedencia);

        var filas = new Dictionary<ClaveDeCampo, ProcedenciaDeCampo>();
        foreach (var fila in procedencia.LasQuePesanEnElVeredicto(umbral))
            filas[new ClaveDeCampo(fila.Tabla, fila.RegistroId, fila.Campo)] = fila;

        return new ProcedenciasDeUnaPasada(
            filas,
            procedencia.CamposAnotadosDe(TablaDeProcedencia.Casos),
            procedencia.CamposAnotadosDe(TablaDeProcedencia.Personas));
    }

    /// <summary>
    /// Lee la procedencia de UN documento, que son una consulta y una por persona.
    /// </summary>
    /// <remarks>
    /// Existe para quien tiene un solo documento delante —la cola de Correccion al avanzar—:
    /// leer la base entera para contestar por uno seria pagar 29 784 filas para mirar siete.
    /// Contesta exactamente lo mismo que <see cref="DeTodaLaBase"/>.
    /// </remarks>
    /// <param name="procedencia">El puerto.</param>
    /// <param name="caso">El documento.</param>
    /// <param name="personas">Sus personas, ya leidas.</param>
    public static ProcedenciasDeUnaPasada DeUnDocumento(
        IProcedencia procedencia, Caso caso, IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(procedencia);
        ArgumentNullException.ThrowIfNull(caso);
        ArgumentNullException.ThrowIfNull(personas);

        var filas = new Dictionary<ClaveDeCampo, ProcedenciaDeCampo>();
        var deCasos = new Dictionary<long, IReadOnlyList<string>>();
        var dePersonas = new Dictionary<long, IReadOnlyList<string>>();

        Apuntar(procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id), caso.Id, filas, deCasos);
        foreach (var persona in personas)
            Apuntar(
                procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id),
                persona.Id, filas, dePersonas);

        return new ProcedenciasDeUnaPasada(filas, deCasos, dePersonas);
    }

    /// <summary>Una pasada sin ninguna fila: todo campo con valor sale como que no consta.</summary>
    /// <remarks>
    /// No es un atajo para no leer: es lo que corresponde cuando de verdad no hay procedencia
    /// que leer, y deja el veredicto del lado prudente —«no consta de dónde salió»— en vez de
    /// dar por bueno lo que nadie miro.
    /// </remarks>
    public static ProcedenciasDeUnaPasada Ninguna { get; } =
        new([], new Dictionary<long, IReadOnlyList<string>>(), new Dictionary<long, IReadOnlyList<string>>());

    /// <summary>
    /// Si a ese campo hay que mirarlo. Contesta <see cref="EstadosDeCampo.EsDudoso"/>.
    /// </summary>
    /// <param name="tabla">Si el campo es del caso o de una persona.</param>
    /// <param name="registroId">El id de la fila; el del caso o el de la persona.</param>
    /// <param name="campo">El nombre de la columna, en espanol y con guion bajo.</param>
    /// <param name="valor">Lo que hay guardado en ese campo.</param>
    /// <param name="esValido">Si ese valor cumple su forma.</param>
    public bool EsDudoso(
        TablaDeProcedencia tabla, long registroId, string campo, string? valor, bool esValido)
    {
        if (_registrosQuePesan.Contains((tabla, registroId))
            && _filas.TryGetValue(new ClaveDeCampo(tabla, registroId, campo), out var fila))
            return EstadosDeCampo.EsDudoso(valor, fila, esValido);

        return TieneFila(tabla, registroId, campo)
            ? EstadosDeCampo.EsDudoso(valor, UnaQueNoPesa, esValido)
            : EstadosDeCampo.EsDudoso(valor, null, esValido);
    }

    /// <summary>Si de ese campo consta alguna fila, pese o no en el veredicto.</summary>
    /// <param name="tabla">Si el campo es del caso o de una persona.</param>
    /// <param name="registroId">El número interno de la fila.</param>
    /// <param name="campo">El nombre de la columna.</param>
    private bool TieneFila(TablaDeProcedencia tabla, long registroId, string campo)
    {
        var donde = tabla == TablaDeProcedencia.Casos ? _camposDeCasos : _camposDePersonas;
        if (!donde.TryGetValue(registroId, out var suyos)) return false;

        // Son cinco campos como mucho por registro: recorrerlos cuesta menos que montar un
        // conjunto de 29 784 claves para consultar siete.
        for (var i = 0; i < suyos.Count; i++)
            if (string.Equals(suyos[i], campo, StringComparison.Ordinal)) return true;

        return false;
    }

    /// <summary>Apunta las filas de un registro en los dos sitios.</summary>
    /// <param name="deUnRegistro">Las filas de procedencia de ese registro, tal como las dio el puerto.</param>
    /// <param name="registroId">El número interno del registro.</param>
    /// <param name="filas">Donde se apuntan por su clave.</param>
    /// <param name="campos">Donde se apunta qué campos tienen fila, por registro.</param>
    private static void Apuntar(
        IReadOnlyList<ProcedenciaDeCampo> deUnRegistro,
        long registroId,
        Dictionary<ClaveDeCampo, ProcedenciaDeCampo> filas,
        Dictionary<long, IReadOnlyList<string>> campos)
    {
        if (deUnRegistro.Count == 0) return;

        var suyos = new List<string>(deUnRegistro.Count);
        foreach (var fila in deUnRegistro)
        {
            filas[new ClaveDeCampo(fila.Tabla, fila.RegistroId, fila.Campo)] = fila;
            suyos.Add(fila.Campo);
        }

        campos[registroId] = suyos;
    }
}
