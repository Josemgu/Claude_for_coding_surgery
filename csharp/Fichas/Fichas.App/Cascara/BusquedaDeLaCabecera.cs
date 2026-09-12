using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Cascara;

/// <summary>Un documento encontrado, tal como se ensena en el desplegable del buscador.</summary>
/// <param name="CasoId">El caso, para poder decir cual se eligio.</param>
/// <param name="NumeroCaso">Su numero, que es como lo llama el dueno.</param>
/// <param name="Unidad">La unidad del documento, o que no se leyo.</param>
/// <param name="CuandoViaja">Cuando viaja, ya escrito en espanol.</param>
public sealed record DocumentoEncontrado(long CasoId, string NumeroCaso, string Unidad, string CuandoViaja)
{
    /// <summary>El renglon entero, que es lo unico que llega a la pantalla.</summary>
    public string ComoSeLee => $"{NumeroCaso} · {Unidad} · {CuandoViaja}";
}

/// <summary>Lo que devolvio una busqueda: lo que cabe, cuantos hay, y como se resume.</summary>
/// <param name="Renglones">Los que caben en el desplegable.</param>
/// <param name="Total">Cuantos casan en total, que casi siempre son mas.</param>
/// <param name="Resumen">Una linea que dice cuantos casan; vacia si no casa ninguno.</param>
public sealed record LoQueSeEncontro(
    IReadOnlyList<DocumentoEncontrado> Renglones, int Total, string Resumen)
{
    /// <summary>Nada: ni se pregunto a la base.</summary>
    public static LoQueSeEncontro Nada { get; } = new([], 0, string.Empty);
}

/// <summary>
/// El buscador de la cabecera: encuentra un documento por su numero, o por el nombre o el
/// MRN de alguien que viaje en el.
/// </summary>
/// <remarks>
/// <para><b>Por que existe y que hace de verdad.</b> El mockup dibuja el buscador en la
/// cabecera pero declara en su tabla de controles que esta «pendiente del frontend real»: no
/// dice que hace. Lo que si deja escrito el mockup es la regla general, que un control que no
/// hace nada esta roto. Asi que hace algo, y lo que hace se eligio con estas mediciones del
/// 2026-09-09:</para>
///
/// <list type="bullet">
///   <item><b>Busca de verdad.</b> <c>FiltroDeCasos.Texto</c> ya existe y
///   <c>RepositorioDeCasos</c> lo resuelve por <c>numero_caso</c>, <c>personas.nombre</c> y
///   <c>personas.mrn</c>, con el texto como parametro y nunca pegado a la consulta. Buscar de
///   verdad no pidio ni una linea fuera de <c>Cascara/</c>.</item>
///
///   <item><b>Responde en el sitio.</b> El renglon trae numero de caso, unidad y fecha de
///   viaje: eso ya contesta «donde esta este documento y cuando sale». ⚠️ <b>No navega al
///   caso</b>, y esto es una frontera, no un olvido: <c>PaginaDeFichas.OnNavigatedTo</c> solo
///   recoge un <c>Servicios</c> del parametro de navegacion, asi que entregarle un caso a una
///   pantalla exige tocar las pantallas, que son de otros. Va dicho en la entrega.</item>
///
///   <item><b>No pinta nombres ni MRN</b>, aunque busque por ellos. Un desplegable que al
///   teclear tres letras suelta nombres y numeros de expediente ensena datos de personas
///   reales a cualquiera que pase por delante, y nadie lo pidio.</item>
///
///   <item><b>No promete la unidad.</b> El mockup escribe «Buscar caso, unidad o cedula», y
///   el filtro NO mira <c>unidad_numero</c>; anadirlo es tocar <c>Fichas.Datos</c>. El
///   marcador dice lo que hay.</item>
/// </list>
/// </remarks>
/// <param name="casos">De donde salen los documentos.</param>
public sealed class BusquedaDeLaCabecera(ICasos casos)
{
    /// <summary>Con menos letras que esto no se pregunta a la base.</summary>
    /// <remarks>
    /// Con 3 000 documentos, una sola letra casa con casi todos y el recorrido se paga
    /// entero para ensenar ocho renglones inservibles.
    /// </remarks>
    public const int LetrasMinimas = 2;

    /// <summary>Cuantos renglones caben en el desplegable.</summary>
    /// <remarks>
    /// Es tambien el tamano del trozo que se le pide a la base: pedir mas seria traerse
    /// filas para tirarlas.
    /// </remarks>
    public const int RenglonesQueSeEnsenan = 8;

    /// <summary>Lo que el cuadro dice cuando esta vacio. Nombra los campos que de verdad se buscan.</summary>
    public const string ElMarcador = "Buscar por caso, nombre o MRN";

    /// <summary>Lo que se ensena en la unidad cuando el documento no la trae leída.</summary>
    private const string CuandoNoHayUnidad = "sin unidad leída";

    /// <summary>
    /// Los comodines de <c>LIKE</c>, que un usuario teclea sin saber que lo son.
    /// </summary>
    /// <remarks>
    /// ⚠️ Esto solo tapa el caso entero —teclear <c>%</c> y pedir la base sin querer—. Un
    /// <c>a%b</c> sigue siendo comodin: escaparlo pide un <c>ESCAPE</c> dentro de la
    /// consulta, que vive en <c>Fichas.Datos</c> y no en este terreno. Va dicho en la
    /// entrega en vez de arreglado a escondidas.
    /// </remarks>
    private static readonly char[] LosComodinesDeLike = ['%', '_'];

    /// <summary>Busca lo tecleado, o devuelve nada sin tocar la base si no merece la pena.</summary>
    /// <param name="tecleado">Lo que hay escrito en el cuadro.</param>
    public LoQueSeEncontro Buscar(string? tecleado)
    {
        var limpio = (tecleado ?? string.Empty).Trim();
        if (!MerecePreguntar(limpio)) return LoQueSeEncontro.Nada;

        var trozo = casos.Listar(
            new FiltroDeCasos(Texto: limpio), Pagina.Primera(RenglonesQueSeEnsenan));

        var renglones = trozo.Elementos.Select(Renglon).ToList();
        return new LoQueSeEncontro(renglones, trozo.TotalDisponible, Resumen(trozo.TotalDisponible));
    }

    /// <summary>Si lo tecleado da para una consulta que sirva de algo: llega al mínimo de letras y no es solo comodines.</summary>
    /// <param name="limpio">Lo tecleado ya sin espacios por los lados.</param>
    private static bool MerecePreguntar(string limpio)
        => limpio.Length >= LetrasMinimas
           && limpio.Any(letra => !LosComodinesDeLike.Contains(letra));

    /// <summary>Un caso, escrito como se lee en el desplegable; sin número de caso se dice, no se deja en blanco.</summary>
    /// <param name="caso">El caso tal como vino de la base.</param>
    private static DocumentoEncontrado Renglon(Caso caso) => new(
        caso.Id,
        string.IsNullOrWhiteSpace(caso.NumeroCaso) ? "sin número de caso" : caso.NumeroCaso,
        LaUnidadDe(caso),
        FechaDeLaCabecera.CortaDeLoGuardado(caso.FechaViaje));

    /// <summary>La unidad del caso: su nombre, su numero, o que no se leyó.</summary>
    /// <param name="caso">El caso; se miran <c>UnidadNombre</c> y <c>UnidadNumero</c>, en ese orden.</param>
    private static string LaUnidadDe(Caso caso)
    {
        if (!string.IsNullOrWhiteSpace(caso.UnidadNombre)) return caso.UnidadNombre;
        return string.IsNullOrWhiteSpace(caso.UnidadNumero) ? CuandoNoHayUnidad : caso.UnidadNumero;
    }

    /// <summary>
    /// La linea que dice cuantos casan, para que ocho no se lean como «solo hay ocho».
    /// </summary>
    /// <remarks>
    /// <para>Es <b>publica a proposito</b>, y el motivo es el mismo por el que
    /// <c>ResumenDeLote</c> lo es en <c>Importar</c>: es una frase que depende de UN numero, y
    /// la forma de comprobar que concuerda con el uno es llamarla con el uno. Intentar
    /// provocarlo desde la base no funciona y se midio: el numero de caso NO es unico desde la
    /// migracion 12, asi que en una base de 200 casos inventados hay 24 numeros distintos y
    /// <b>ninguno</b> casa exactamente una vez. Una prueba que pelea con los datos para llegar
    /// al uno acaba comprobando los datos, no la frase.</para>
    ///
    /// <para>La regla que protege esta en <c>Cascara/PruebasDeLaConcordanciaConUno.cs</c>:
    /// «1 documento», no «1 documentos» ni «1 documento(s)».</para>
    /// </remarks>
    /// <param name="total">Cuantos documentos casan en total, no cuantos se ensenan.</param>
    public static string Resumen(int total) => total switch
    {
        <= 0 => "No hay ningún documento que case.",
        1 => "1 documento.",
        _ when total <= RenglonesQueSeEnsenan => $"{total} documentos.",
        _ => $"{total} documentos; se ven los {RenglonesQueSeEnsenan} primeros.",
    };
}
