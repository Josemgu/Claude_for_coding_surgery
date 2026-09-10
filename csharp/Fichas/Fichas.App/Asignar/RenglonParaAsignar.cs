using Fichas.App.Vocabulario;
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

    /// <summary>
    /// El número de la unidad, 6 o 7 dígitos, o vacío si el papel no traía ninguno.
    /// </summary>
    /// <remarks>
    /// Va aparte de <see cref="Unidad"/>, que es texto para leer y lleva «sin unidad» dentro
    /// cuando no hay nombre. Agrupar por unidad necesita el DATO: el dueño reparte por unidad
    /// —«Rama San Juan No 325535», «Barrio Marito 656351»— y dos unidades pueden llamarse
    /// igual, así que el número es lo único que las distingue.
    /// </remarks>
    public string UnidadNumero { get; init; } = string.Empty;

    /// <summary>La fecha de viaje, o «sin fecha de viaje».</summary>
    public string FechaDeViaje { get; init; } = SinFecha;

    /// <summary>
    /// La fecha de viaje TAL COMO está en la base, ISO-8601, o vacía si no la hay.
    /// </summary>
    /// <remarks>
    /// Va aparte de <see cref="FechaDeViaje"/> por lo mismo que en
    /// <c>TarjetaDeDocumento.FechaDeViajeIso</c>: agrupar por día necesita el dato, no la
    /// frase, y de «sin fecha de viaje» no sale ninguna fecha. Lo usa
    /// <see cref="GruposParaAsignar"/> para repartir por grupo.
    /// </remarks>
    public string FechaDeViajeIso { get; init; } = string.Empty;

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

    /// <summary>El estado tal como esta guardado; de aqui salen la palabra y el detalle.</summary>
    /// <remarks>
    /// ⚠️ Se guarda el VALOR y no la palabra ya escrita, que es lo que habia hasta el
    /// 2026-09-07. Con la palabra dentro, quien arma el renglon decide como se lee, y eso es
    /// justamente lo que hacia que dos pantallas dijeran cosas distintas del mismo documento.
    /// </remarks>
    public EstadoDeRecomendacion Estado { get; init; } = EstadoDeRecomendacion.SinMarcar;

    /// <summary>Si el dueno lo archivo; archivar es su forma de decir que ya no le queda nada.</summary>
    public bool Archivado { get; init; }

    /// <summary>Cuantos datos del papel le faltan; 0 si no le falta ninguno.</summary>
    public int CuantoLeFalta { get; init; }

    /// <summary>Lo que se lee de este documento: una de las dos palabras, y su detalle.</summary>
    public LoQueSeLeeDeUnDocumento Lectura => LoQueSeLeeDeUnDocumento.De(
        Estado,
        Archivado,
        CuantoLeFalta,
        sinNingunaPersonaLeida: Personas == 0,
        quienLoLleva: AsignadoA == SinAsignar ? string.Empty : AsignadoA,
        firma: string.Empty);

    /// <summary>La palabra del estado: «resuelto» o «me falta», y no hay una tercera.</summary>
    /// <remarks>
    /// ⛔ Hasta el 2026-09-07 decia una de tres —«sin revisar», «completa» o «no esta
    /// completa»—, y venia escrita desde fuera. Lo que decia no se pierde: va en
    /// <see cref="DetalleDelEstado"/>.
    /// </remarks>
    public string PalabraDelEstado => Lectura.Palabra;

    /// <summary>Que falta y a quien le toca; se lee cuando el dueno lo pide, no de entrada.</summary>
    public string DetalleDelEstado => Lectura.Detalle;

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

    /// <summary>
    /// La palabra de la BASE para cada estado; ya no es la que se lee en las listas.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Esto era el vocabulario de la pantalla hasta el 2026-09-07</b>, y dejo de
    /// serlo cuando el dueno colapso las cuatro palabras a dos. Se queda, y con un solo uso: los
    /// mensajes que nombran lo que se iba a ESCRIBIR en <c>casos.estado_recomendacion</c>
    /// —<c>AccionesDeRevisar.MarcarAMano</c> avisa de que «completa» y un motivo se
    /// contradicen—. Ahi «resuelto» seria mentir sobre que columna se estaba tocando.</para>
    ///
    /// <para>Lo que se LEE de un documento sale ahora de <see cref="Lectura"/>, y de ningun
    /// otro sitio.</para>
    /// </remarks>
    public static string PalabraDe(EstadoDeRecomendacion estado) => estado switch
    {
        EstadoDeRecomendacion.Completa => "completa",
        EstadoDeRecomendacion.NoCompleta => "no está completa",
        _ => "sin revisar",
    };
}
