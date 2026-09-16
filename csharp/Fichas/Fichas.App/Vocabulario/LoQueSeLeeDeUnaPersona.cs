namespace Fichas.App.Vocabulario;

/// <summary>
/// Lo que se lee de UNA persona: una de las dos palabras, y detras sus seis preguntas.
/// </summary>
/// <remarks>
/// <para>La unidad de trabajo del dueno es la persona y no el documento, fijado por el mismo
/// el 2026-09-05: <i>«es por persona que se revisa la informacion»</i>. Un documento de cinco
/// personas tiene cinco lecturas y no una.</para>
///
/// <para>⛔ <b>Aqui no vive la regla de las seis preguntas</b>, que es
/// <c>Fichas.Reportes.Reglas.Pasos</c> y esta congelada. Esto solo traduce su respuesta —si,
/// no o nada— a la palabra que el dueno pidio ver.</para>
///
/// <para>⚠️ <b>Desde el 2026-09-16 lleva ademas si esta A MEDIAS</b>: alguna de las seis en si
/// pero no las seis. No es una tercera palabra —<see cref="DosEstados.NotaDeAMedias"/>—: la
/// palabra sigue siendo «me falta», y a medias es el matiz que la pinta de naranja y hace que
/// el detalle diga cuantas van y cuales faltan.</para>
/// </remarks>
/// <param name="Lo">Si al dueno le queda algo que hacer con esta persona.</param>
/// <param name="Detalle">Que falta y a quien le toca.</param>
/// <param name="AMedias">Si alguna de sus seis dice que si y no las seis; solo con «me falta».</param>
/// <param name="Resumen">
/// Lo mismo que <paramref name="Detalle"/> pero sin a quien le toca, para nombrarla dentro
/// del detalle de su documento: «le faltan 2 de 6: Entrevistas, Listo para el templo».
/// </param>
public sealed record LoQueSeLeeDeUnaPersona(LoQueSeLee Lo, string Detalle, bool AMedias = false, string Resumen = "")
{
    /// <summary>Cuantas son las preguntas del sistema del lider; el «6» de «le faltan 2 de 6».</summary>
    /// <remarks>
    /// Que sea el mismo seis que el de la ventana de las preguntas lo vigila
    /// <c>PruebasDeLaPersonaAMedias.ElSeisDelDetalleEsElDeLasSeisPreguntas</c>.
    /// </remarks>
    public const int LasSeis = 6;

    /// <summary>La palabra que se lee: «resuelto» o «me falta».</summary>
    public string Palabra => DosEstados.Palabra(Lo);

    /// <summary>Si no queda nada que hacer con ella.</summary>
    public bool EsResuelto => Lo == LoQueSeLee.Resuelto;

    /// <summary>Lo contrario.</summary>
    public bool EsMeFalta => Lo == LoQueSeLee.MeFalta;

    /// <summary>«me falta · se quedó en Entrevistas · le toca al líder», para un lector de pantalla.</summary>
    public string ParaElLector => $"{Palabra} · {Detalle}";

    /// <summary>
    /// Lee a una persona a partir de lo que dicen sus seis preguntas.
    /// </summary>
    /// <remarks>
    /// <para><b>Resuelto es tener las seis en si</b>, y nada mas. Ni una en no, ni una en
    /// blanco.</para>
    ///
    /// <para>⛔ <b>«Sin mirar» NO es «no lista», y las dos dicen «me falta» sin confundirse.</b>
    /// Lo dice el comentario de <c>Pasos.Estado</c>: <i>«dar por lista a una persona de la que
    /// faltan preguntas por mirar es exactamente lo que manda a alguien al templo con la
    /// recomendacion mal»</i>. Una es una respuesta y la otra es la ausencia de respuesta, y
    /// eso se conserva DONDE importa: en el detalle, que dice cual de las dos es y a quien hay
    /// que llamar. La palabra se colapsa porque para el dueno las dos significan lo mismo —le
    /// queda trabajo—; el motivo no, porque en una hay que llamar al lider y en la otra hay
    /// que ir a mirar.</para>
    ///
    /// <para>⚠️ <b>A MEDIAS (2026-09-16): alguna en si y no las seis.</b> Con
    /// <paramref name="lasQueNoDicenSi"/> a la vista se sabe cuantas van en si, y si son entre
    /// una y cinco la persona esta a medias: sigue en «me falta», y el detalle dice «le faltan
    /// 2 de 6: Entrevistas, Listo para el templo». <b>El naranja mide cuanto se AVANZO</b>
    /// —cuantas dicen si—, no si alguna dice no: cinco en si y una en no tambien esta a medias,
    /// y el detalle conserva el «se quedo en» para que se sepa que hay que llamar al lider.</para>
    ///
    /// <para><b>Lo medido antes de tocar esto:</b> una persona con cuatro en si y dos en blanco
    /// llegaba aqui como <c>recomendacion == null</c> con <c>seQuedoEn</c> vacio, y se decia de
    /// ella <i>«nadie ha contestado sus seis preguntas»</i>. Cuatro contestadas no es nadie.</para>
    ///
    /// <para>⚠️ <b>Sin las seis a la vista (<paramref name="lasQueNoDicenSi"/> nulo) se lee como
    /// hasta hoy</b>, sin naranja: no se puede saber cuantas van en si, y adivinarlo seria
    /// inventar. Es el camino de las pantallas que todavia no las pasan (tickets de Inicio,
    /// recomendacion en Correccion); <c>LasDosPreguntas.LasQueNoDicenSi</c> las saca de una
    /// persona en una linea.</para>
    /// </remarks>
    /// <param name="recomendacion">Lo que dicen sus seis: si, no, o nada si nadie las miro.</param>
    /// <param name="seQuedoEn">Los pasos marcados que NO, sin el numero de delante.</param>
    /// <param name="lasQueNoDicenSi">
    /// Los pasos que no dicen que si —en no o en blanco—, sin el numero de delante; nulo si
    /// quien llama no los tiene, y entonces no se puede saber si esta a medias.
    /// </param>
    public static LoQueSeLeeDeUnaPersona De(
        bool? recomendacion,
        IReadOnlyList<string>? seQuedoEn,
        IReadOnlyList<string>? lasQueNoDicenSi = null)
    {
        if (recomendacion == true)
            return new LoQueSeLeeDeUnaPersona(LoQueSeLee.Resuelto, "sus seis preguntas del sistema del líder dicen que sí", Resumen: "las seis en sí");

        if (EstaAMedias(lasQueNoDicenSi))
            return AMediasCon(lasQueNoDicenSi!, seQuedoEn);

        if (recomendacion == false)
        {
            var donde = seQuedoEn is null || seQuedoEn.Count == 0
                ? "alguna de sus seis preguntas dice que no"
                : $"se quedó en {Enumerar(seQuedoEn)}";
            return new LoQueSeLeeDeUnaPersona(LoQueSeLee.MeFalta, $"{donde} · {LeTocaAlLider}", Resumen: donde);
        }

        return new LoQueSeLeeDeUnaPersona(
            LoQueSeLee.MeFalta,
            $"nadie ha contestado sus seis preguntas · {TeTocaMirarlas}",
            Resumen: "sin contestar");
    }

    /// <summary>A quien le toca cuando alguna dice que no.</summary>
    private const string LeTocaAlLider = "le toca al líder de su unidad";

    /// <summary>A quien le toca cuando solo hay blancos.</summary>
    private const string TeTocaMirarlas = "te toca a ti: mirarlas en el sistema del líder";

    /// <summary>Si con las seis a la vista alguna dice que si y no las seis.</summary>
    /// <param name="lasQueNoDicenSi">Las que no dicen si; nulo si no se sabe.</param>
    private static bool EstaAMedias(IReadOnlyList<string>? lasQueNoDicenSi)
        => lasQueNoDicenSi is not null && lasQueNoDicenSi.Count > 0 && lasQueNoDicenSi.Count < LasSeis;

    /// <summary>
    /// La lectura de una persona a medias: cuantas le faltan, cuales, donde se quedo si dijo
    /// no en alguna, y a quien le toca.
    /// </summary>
    /// <param name="lasQueNoDicenSi">Las que no dicen si, entre una y cinco.</param>
    /// <param name="seQuedoEn">Las que dicen no, si hay alguna.</param>
    private static LoQueSeLeeDeUnaPersona AMediasCon(IReadOnlyList<string> lasQueNoDicenSi, IReadOnlyList<string>? seQuedoEn)
    {
        var cuantas = lasQueNoDicenSi.Count;
        var resumen = $"le {(cuantas == 1 ? "falta" : "faltan")} {cuantas} de {LasSeis}: {string.Join(", ", lasQueNoDicenSi)}";

        var hayAlgunNo = seQuedoEn is not null && seQuedoEn.Count > 0;
        var detalle = hayAlgunNo
            ? $"{resumen} · se quedó en {Enumerar(seQuedoEn!)} · {LeTocaAlLider}"
            : $"{resumen} · {TeTocaMirarlas}";

        return new LoQueSeLeeDeUnaPersona(LoQueSeLee.MeFalta, detalle, AMedias: true, Resumen: resumen);
    }

    /// <summary>
    /// «Entrevistas», «Entrevistas y Preparación», «Entrevistas, Preparación y Cita del templo».
    /// </summary>
    /// <remarks>
    /// Se enumeran TODOS y no solo el primero: el dueno llama al lider una vez y le dice todo
    /// lo que falta. Como mucho son seis, asi que la linea no se dispara.
    /// </remarks>
    /// <param name="nombres">Los pasos en los que se quedó, en orden; al menos uno.</param>
    private static string Enumerar(IReadOnlyList<string> nombres)
    {
        if (nombres.Count == 1) return nombres[0];
        var todosMenosElUltimo = string.Join(", ", nombres.Take(nombres.Count - 1));
        return $"{todosMenosElUltimo} y {nombres[^1]}";
    }
}
