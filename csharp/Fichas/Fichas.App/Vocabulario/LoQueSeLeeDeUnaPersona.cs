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
/// </remarks>
/// <param name="Lo">Si al dueno le queda algo que hacer con esta persona.</param>
/// <param name="Detalle">Que falta y a quien le toca.</param>
public sealed record LoQueSeLeeDeUnaPersona(LoQueSeLee Lo, string Detalle)
{
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
    /// </remarks>
    /// <param name="recomendacion">Lo que dicen sus seis: si, no, o nada si nadie las miro.</param>
    /// <param name="seQuedoEn">Los pasos marcados que NO, sin el numero de delante.</param>
    public static LoQueSeLeeDeUnaPersona De(bool? recomendacion, IReadOnlyList<string>? seQuedoEn)
    {
        if (recomendacion == true)
            return new LoQueSeLeeDeUnaPersona(LoQueSeLee.Resuelto, "sus seis preguntas del sistema del líder dicen que sí");

        if (recomendacion == false)
        {
            var donde = seQuedoEn is null || seQuedoEn.Count == 0
                ? "alguna de sus seis preguntas dice que no"
                : $"se quedó en {Enumerar(seQuedoEn)}";
            return new LoQueSeLeeDeUnaPersona(LoQueSeLee.MeFalta, $"{donde} · le toca al líder de su unidad");
        }

        return new LoQueSeLeeDeUnaPersona(
            LoQueSeLee.MeFalta,
            "nadie ha contestado sus seis preguntas · te toca a ti: mirarlas en el sistema del líder");
    }

    /// <summary>
    /// «Entrevistas», «Entrevistas y Preparación», «Entrevistas, Preparación y Cita del templo».
    /// </summary>
    /// <remarks>
    /// Se enumeran TODOS y no solo el primero: el dueno llama al lider una vez y le dice todo
    /// lo que falta. Como mucho son seis, asi que la linea no se dispara.
    /// </remarks>
    private static string Enumerar(IReadOnlyList<string> nombres)
    {
        if (nombres.Count == 1) return nombres[0];
        var todosMenosElUltimo = string.Join(", ", nombres.Take(nombres.Count - 1));
        return $"{todosMenosElUltimo} y {nombres[^1]}";
    }
}
