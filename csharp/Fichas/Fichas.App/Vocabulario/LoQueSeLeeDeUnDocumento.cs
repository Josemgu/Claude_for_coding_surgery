using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Vocabulario;

/// <summary>
/// Lo que se lee de UN documento: una de las dos palabras, y detras lo que falta y a quien
/// le toca.
/// </summary>
/// <remarks>
/// <para>Los dos campos van juntos en un solo valor a proposito. La palabra sola pierde quien
/// dijo que, y el detalle solo no cabe en un renglon; separados en dos sitios, uno de los dos
/// se queda sin cambiar el dia que se toque el otro.</para>
///
/// <para>⛔ <b>Esto no firma nada ni escribe nada.</b> Es una lectura de lo que ya esta en la
/// base, igual que <c>LasDosPreguntas</c>.</para>
/// </remarks>
/// <param name="Lo">Si al dueno le queda algo que hacer con este documento.</param>
/// <param name="Detalle">Que falta y a quien le toca, o quien lo dio por bueno y cuando.</param>
public sealed record LoQueSeLeeDeUnDocumento(LoQueSeLee Lo, string Detalle)
{
    /// <summary>La palabra que se lee: «resuelto» o «me falta».</summary>
    public string Palabra => DosEstados.Palabra(Lo);

    /// <summary>La misma, para una cabecera.</summary>
    public string PalabraEnCabecera => DosEstados.PalabraEnCabecera(Lo);

    /// <summary>Si no queda nada que hacer con el.</summary>
    public bool EsResuelto => Lo == LoQueSeLee.Resuelto;

    /// <summary>Lo contrario; lo lee la plantilla para encender una pastilla y no la otra.</summary>
    public bool EsMeFalta => Lo == LoQueSeLee.MeFalta;

    /// <summary>«me falta · le faltan 3 datos · te toca a ti», para un lector de pantalla.</summary>
    /// <remarks>
    /// El detalle se ensena a un clic, pero quien no ve la pantalla no puede pulsar para
    /// enterarse de que le falta: en voz alta van los dos.
    /// </remarks>
    public string ParaElLector => $"{Palabra} · {Detalle}";

    /// <summary>
    /// Lee un documento a partir de las cuatro cosas que la base guarda por separado.
    /// </summary>
    /// <remarks>
    /// <para><b>El orden de las preguntas es la decision, y va de arriba abajo:</b></para>
    /// <list type="number">
    /// <item><b>Archivado gana a todo.</b> Archivar ES el gesto con el que el dueno dice que
    /// ya no tiene nada que hacer con ese documento —<i>«si ya resolvi un archivo y lo
    /// archivo…»</i>, 2026-09-06—, asi que vale aunque a los campos les falte algo. Ojo:
    /// «resuelto» no es «completo». El detalle dice que lo cerro el, y la base sigue
    /// guardando su <c>estado_recomendacion</c> sin tocar.</item>
    /// <item><b>Lo que el Excel del companero dio por completo.</b> Es la unica marca que
    /// escribe alguien de fuera con su nombre (regla permanente 5, precisada el 2026-09-03:
    /// <i>«el documento que ellos llenan es el que marca, y dice completado por Sandy»</i>).</item>
    /// <item><b>Todo lo demas es «me falta»</b>, y ahi el detalle tiene que decir QUE falta y
    /// a QUIEN le toca. Nunca se queda mudo.</item>
    /// </list>
    ///
    /// <para>⛔ <b>Que NO decide esto:</b> si un documento se puede asignar, si esta completo o
    /// si alguien puede viajar. Sigue habiendo cuatro respuestas guardadas y cada una en su
    /// columna; esta funcion solo elige cual de las dos palabras se pinta.</para>
    /// </remarks>
    /// <param name="estado">Lo que dice <c>casos.estado_recomendacion</c>.</param>
    /// <param name="archivado">Si el dueno lo archivo.</param>
    /// <param name="cuantoLeFalta">Cuantos campos del papel le faltan, contados por <c>LoQueLeFalta</c>.</param>
    /// <param name="sinNingunaPersonaLeida">Si el lector no saco a nadie de ese documento.</param>
    /// <param name="quienLoLleva">El companero que lo tiene vivo, o vacio.</param>
    /// <param name="firma">«Completada por Sandy · 2026-08-30», tal como la compone la tarjeta.</param>
    /// <param name="fechaDeArchivado">Cuando se archivo, para poder decirlo en el detalle.</param>
    /// <param name="motivo">Por que dijo el companero que no estaba completa.</param>
    public static LoQueSeLeeDeUnDocumento De(
        EstadoDeRecomendacion estado,
        bool archivado,
        int cuantoLeFalta,
        bool sinNingunaPersonaLeida,
        string quienLoLleva,
        string firma,
        string fechaDeArchivado = "",
        string motivo = "")
    {
        if (archivado) return Resuelto(DetalleDeLoArchivado(fechaDeArchivado, firma));
        if (estado == EstadoDeRecomendacion.Completa) return Resuelto(DetalleDeLoCompletado(firma));

        return new LoQueSeLeeDeUnDocumento(
            LoQueSeLee.MeFalta,
            DetalleDeLoQueFalta(estado, cuantoLeFalta, sinNingunaPersonaLeida, quienLoLleva, motivo));
    }

    /// <summary>Una lectura resuelta con su detalle ya compuesto.</summary>
    private static LoQueSeLeeDeUnDocumento Resuelto(string detalle)
        => new(LoQueSeLee.Resuelto, detalle);

    /// <summary>Lo que se dice de un documento que el dueno cerro.</summary>
    /// <remarks>
    /// ⛔ <b>No vuelve la etiqueta «ARCHIVADO» a la lista.</b> Lo que le molestaba al dueno el
    /// 2026-09-06 era verla en medio del trabajo; esta frase vive DENTRO del detalle, que solo
    /// se abre cuando el lo pide. Y lleva la firma detras cuando la hay: un archivado que
    /// ademas marco un companero conserva las dos cosas dichas.
    /// </remarks>
    private static string DetalleDeLoArchivado(string fechaDeArchivado, string firma)
    {
        var cerrado = string.IsNullOrWhiteSpace(fechaDeArchivado)
            ? "lo archivaste tú"
            : $"lo archivaste tú el {fechaDeArchivado}";
        return string.IsNullOrWhiteSpace(firma) ? cerrado : $"{cerrado} · {firma}";
    }

    /// <summary>Lo que se dice de un documento que el Excel de un companero dio por completo.</summary>
    /// <remarks>
    /// La firma —«Completada por Sandy · 2026-08-30»— es lo unico que dice QUIEN lo dio por
    /// bueno, y por eso es lo primero del detalle. Sin ella se diria «lo dio por completo el
    /// Excel de un companero», que sigue siendo cierto y ya no nombra a nadie: es lo que pasa
    /// cuando la fila de ese companero ya no esta.
    /// </remarks>
    private static string DetalleDeLoCompletado(string firma)
        => string.IsNullOrWhiteSpace(firma) ? "lo dio por completo el Excel de un compañero" : firma;

    /// <summary>
    /// Que falta y a quien le toca. Es la mitad que el dueno exigio: nunca vacio.
    /// </summary>
    /// <remarks>
    /// <para>El orden va de lo que le toca a EL a lo que le toca a otro, porque lo primero es
    /// lo que puede resolver ahora mismo sin llamar a nadie.</para>
    ///
    /// <para>⚠️ «Sin ninguna persona leida» va delante de la cuenta de campos y tiene frase
    /// propia: con los cinco campos del caso perfectos la cuenta es 0, y «le faltan 0 datos»
    /// de un documento parado son dos cifras de la misma pantalla que no encajan.</para>
    /// </remarks>
    private static string DetalleDeLoQueFalta(
        EstadoDeRecomendacion estado,
        int cuantoLeFalta,
        bool sinNingunaPersonaLeida,
        string quienLoLleva,
        string motivo)
    {
        var trozos = new List<string>();

        if (sinNingunaPersonaLeida) trozos.Add("no se leyó ninguna persona en este documento");
        else if (cuantoLeFalta > 0)
        {
            trozos.Add("le " + Plural.Palabra(cuantoLeFalta, "falta", "faltan")
                       + " " + Plural.Con(cuantoLeFalta, "dato", "datos") + " del papel");
        }

        if (estado == EstadoDeRecomendacion.NoCompleta)
        {
            trozos.Add(string.IsNullOrWhiteSpace(motivo)
                ? "el compañero dijo que no está completa"
                : $"el compañero dijo: {motivo}");
        }

        trozos.Add(AQuienLeToca(estado, cuantoLeFalta, sinNingunaPersonaLeida, quienLoLleva));
        return string.Join(" · ", trozos);
    }

    /// <summary>A quien le toca el siguiente movimiento, dicho en segunda persona cuando es a el.</summary>
    /// <remarks>
    /// Se le habla de tu —«te toca a ti»— porque es la unica persona que abre este programa, y
    /// «corresponde al usuario» seria la misma frase sin decir nada. Lo que hay que hacer va
    /// pegado al nombre de la pantalla donde se hace: sin eso, saber que te toca no dice por
    /// donde empezar.
    /// </remarks>
    private static string AQuienLeToca(
        EstadoDeRecomendacion estado, int cuantoLeFalta, bool sinNingunaPersonaLeida, string quienLoLleva)
    {
        if (sinNingunaPersonaLeida) return "te toca a ti: añadir la persona a mano en Corrección, o volver a importarlo";
        if (cuantoLeFalta > 0) return "te toca a ti, en Corrección";
        if (!string.IsNullOrWhiteSpace(quienLoLleva)) return $"le toca a {quienLoLleva}: aún no ha devuelto su hoja";
        return estado == EstadoDeRecomendacion.NoCompleta
            ? "te toca a ti: decidir si se archiva o se vuelve a mandar"
            : "te toca a ti: repartirlo a un compañero";
    }
}
