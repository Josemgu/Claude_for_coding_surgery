using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Asignar;

/// <summary>
/// Un caso tal como se ve en la lista de Asignar: lo justo para reconocerlo y marcarlo.
/// </summary>
/// <remarks>
/// Lleva su estado ESCRITO, no para filtrar por el —la pantalla ofrece todos (requisito 8)—
/// sino para que Miguel vea a que le esta dando trabajo. Avisar, nunca impedir (requisito 9):
/// la palabra se ve, el caso se ofrece.
/// <para>⛔ <b>Ya no lleva marca de archivado</b> (2026-09-06). Esta lista nunca ofrecio un
/// archivado —eso se arreglo el 2026-09-05—, asi que la palabra solo podia salir de un caso
/// que ya no esta; y el dueno pidio que no aparezca en ningun lado: <i>«si se queda en el
/// tablero y dice archivado, lo que hace es que me confunda»</i>.</para>
/// </remarks>
public sealed record RenglonParaAsignar
{
    /// <summary>Numero interno del caso.</summary>
    public long CasoId { get; init; }

    /// <summary>El numero del papel, o «sin numero de caso» si no se pudo leer. Nunca se inventa.</summary>
    public string NumeroDeCaso { get; init; } = SinNumero;

    /// <summary>Si el numero se leyo de verdad; el que no lo tiene se pinta distinto.</summary>
    public bool TieneNumeroDeCaso { get; init; }

    /// <summary>Nombre de la unidad, o «sin unidad».</summary>
    public string Unidad { get; init; } = "sin unidad";

    /// <summary>La fecha de viaje, o «sin fecha de viaje».</summary>
    public string FechaDeViaje { get; init; } = SinFecha;

    /// <summary>Cuantas personas van en el formulario.</summary>
    public int Personas { get; init; }

    /// <summary>
    /// Quien viaja, en una linea: el primero del formulario y cuantos mas van con el.
    /// </summary>
    /// <remarks>
    /// Del dueno, 2026-09-06: <i>«en asignado a los agentes necesito ver cuando se completa el
    /// nombre de las personas, no de la rama o barrio»</i>. Y el motivo lo dejo escrito el dia
    /// antes: <i>«es por persona que se revisa la informacion»</i> — a quien va a llamar al
    /// obispo no le sirve un numero de expediente, necesita el nombre delante. Es el mismo
    /// razonamiento que ya aplico <c>ArmadoDeLaSegundaVuelta</c> en el reporte del gerente.
    /// </remarks>
    public string QuienViaja { get; init; } = SinPersonas;

    /// <summary>Todos los que viajan, uno por linea; es lo que se lee en el globo de ayuda.</summary>
    /// <remarks>
    /// La linea de la lista no se puede estirar, pero los diez nombres de una familia de diez no
    /// se pueden quedar dentro del documento: cada uno es un ticket. Aqui estan todos.
    /// </remarks>
    public string QuienesViajan { get; init; } = SinPersonas;

    /// <summary>La palabra del estado: «sin revisar», «completa» o «no esta completa».</summary>
    public string PalabraDelEstado { get; init; } = PalabraDe(EstadoDeRecomendacion.SinMarcar);

    /// <summary>Quien lo lleva vivo ahora mismo, o «sin asignar».</summary>
    public string AsignadoA { get; init; } = SinAsignar;

    /// <summary>La linea de arriba de la fila: el numero del papel y QUIEN viaja en el.</summary>
    /// <remarks>
    /// La unidad estaba aqui hasta el 2026-09-06 y bajo al detalle. No desaparecio —el dueno
    /// pidio ver el nombre, no perder el barrio—, pero deja de ocupar la linea que el mira.
    /// El numero del papel se queda porque es por donde el busca y por donde se ve que dos
    /// documentos son del mismo grupo de viaje (ADR-0006 §7).
    /// </remarks>
    public string Titulo => $"{NumeroDeCaso} · {QuienViaja}";

    /// <summary>La linea de abajo: unidad, cuantos van, fecha, estado y quien lo lleva.</summary>
    public string Detalle
        => $"{Unidad} · {Plural.Con(Personas, "persona", "personas")} · {FechaDeViaje} · {PalabraDelEstado} · {AsignadoA}";

    /// <summary>Lo que se pone donde deberia ir el numero cuando no se leyo ninguno.</summary>
    public const string SinNumero = "sin número de caso";

    /// <summary>Lo que se pone donde deberia ir la fecha cuando el caso no tiene ninguna.</summary>
    public const string SinFecha = "sin fecha de viaje";

    /// <summary>Lo que dice el renglon de un caso que no lleva nadie.</summary>
    public const string SinAsignar = "sin asignar";

    /// <summary>Lo que se dice de un documento del que no se leyo ni una persona.</summary>
    public const string SinPersonas = "sin personas leídas";

    /// <summary>
    /// Lo que se dice de una persona cuyo nombre no se leyo. La misma palabra que Inicio y Grupo.
    /// </summary>
    /// <remarks>
    /// Se dice y no se deja en blanco: un hueco se lee como «aqui no viaja nadie», y lo que pasa
    /// es que el reconocimiento no leyo ese nombre y nadie lo ha corregido todavia. La cedula NO
    /// ocupa su sitio: es el dato de una persona de verdad y esta lista se mira con gente
    /// alrededor; para llamar por telefono esta el reporte, que si la lleva en su columna.
    /// </remarks>
    public const string SinNombreLeido = "sin nombre leído";

    /// <summary>
    /// El nombre de una persona tal como se dice en esta lista, o la palabra si no se leyo.
    /// </summary>
    /// <remarks>
    /// Recibe el nombre y NADA MAS, a proposito: asi no hay forma de que la cedula acabe
    /// pintada en la lista por descuido. Es una decision, no un olvido — ver
    /// <see cref="SinNombreLeido"/>.
    /// </remarks>
    public static string NombreDeQuienViaja(string? nombre)
        => string.IsNullOrWhiteSpace(nombre) ? SinNombreLeido : nombre.Trim();

    /// <summary>
    /// Como se dice en UNA linea quien viaja: el primero del formulario y cuantos mas.
    /// </summary>
    /// <remarks>
    /// No se pegan los nombres uno detras de otro porque la fila recorta con puntos suspensivos
    /// y un renglon recortado no se distingue de uno que no tenia mas gente. La cuenta, en
    /// cambio, cabe siempre. Los diez enteros estan en <see cref="QuienesViajan"/>.
    /// </remarks>
    public static string ComoSeDiceQuienViaja(IReadOnlyList<string> nombres)
    {
        ArgumentNullException.ThrowIfNull(nombres);
        return nombres.Count switch
        {
            0 => SinPersonas,
            1 => nombres[0],
            _ => $"{nombres[0]} y {nombres.Count - 1} más",
        };
    }

    /// <summary>Todos los que viajan, uno por linea, para el globo de ayuda.</summary>
    public static string ComoSeDicenTodosLosQueViajan(IReadOnlyList<string> nombres)
    {
        ArgumentNullException.ThrowIfNull(nombres);
        return nombres.Count == 0 ? SinPersonas : string.Join('\n', nombres);
    }

    /// <summary>La palabra en espanol de cada estado; el color nunca va solo (mockup v2).</summary>
    public static string PalabraDe(EstadoDeRecomendacion estado) => estado switch
    {
        EstadoDeRecomendacion.Completa => "completa",
        EstadoDeRecomendacion.NoCompleta => "no está completa",
        _ => "sin revisar",
    };
}
