using Fichas.App.Grupo;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Como esta la recomendacion de UNA persona en el sistema del obispo, ya en palabras.
/// </summary>
/// <param name="PersonaId">De quien se habla; el numero interno de la persona.</param>
/// <param name="DeQuien">Su nombre y su fila del formulario.</param>
/// <param name="Estado">Si, no, o nada si nadie la miro; sale de sus seis preguntas.</param>
/// <param name="SeQuedoEn">Los pasos marcados que NO; vacio si no hay ninguno.</param>
public sealed record RecomendacionDeUnaPersona(
    long PersonaId,
    string DeQuien,
    bool? Estado,
    IReadOnlyList<string> SeQuedoEn)
{
    /// <summary>«no lista para viajar · recomendación sin confirmar · se quedó en Entrevistas».</summary>
    public string Frase => LasDosPreguntas.FraseDeUnaPersona(Estado, SeQuedoEn);

    /// <summary>Lo que lee en voz alta un lector de pantalla; son dos textos sueltos.</summary>
    public string ParaElLector => $"{DeQuien}. {Frase}.";
}

/// <summary>
/// Lee de cada persona del documento si su recomendacion esta confirmada en el sistema del
/// obispo, y lo pone en palabras.
/// </summary>
/// <remarks>
/// <para><b>Por que existe, y por que es una lista aparte de «Lo que contestó el
/// compañero».</b> Aquella solo trae a las personas por las que ALGUIEN contesto, y eso es
/// deliberado: una ficha vacia por cada una enterraria las que traen algo. Pero la pregunta
/// que el dueno contesta de verdad —<i>«verificar que los hermanos que están en el PDF tengan
/// la recomendación hecha en el sistema»</i>— hay que hacersela a TODAS, y para la que nadie
/// miro la respuesta es «sin mirar», que no es lo mismo que «no». Sin esta lista, las
/// personas que nadie miro serian invisibles justo en la pantalla donde el esta mirando.</para>
///
/// <para>⛔ <b>Solo se LEE.</b> No hay ni un boton y no se escribe en ninguna fila.
/// Contestar las seis preguntas dentro del programa es la FASE C19 y no es esta. La firma de
/// campos —<c>procedencia_campo.verificado</c>— es de Miguel y nunca automatica (regla
/// permanente 5), y <c>casos.estado_recomendacion</c> lo escribe el Excel del companero con
/// su nombre. Aqui no se toca ninguna de las dos.</para>
///
/// <para>Vive fuera de cualquier control de XAML a proposito: asi las frases que ve Miguel se
/// leen en una prueba sin abrir una ventana (ADR-0003 §8.1).</para>
/// </remarks>
public static class LaRecomendacionEnElSistemaDelObispo
{
    /// <summary>Una linea por persona, en el orden en que venian en el formulario.</summary>
    /// <param name="personas">Las personas del documento; vacia devuelve una lista vacia.</param>
    /// <exception cref="ArgumentNullException">Si la lista es nula.</exception>
    public static IReadOnlyList<RecomendacionDeUnaPersona> DeCadaPersona(IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(personas);
        return [.. personas.Select(De)];
    }

    /// <summary>La linea de una persona: su estado sale de sus seis preguntas y nada mas.</summary>
    /// <remarks>
    /// El estado y los pasos en que se quedo los decide <see cref="LasDosPreguntas"/>, que es
    /// la misma regla que usan Inicio y el grupo del dia: aqui no se calcula otro veredicto.
    /// </remarks>
    /// <param name="persona">La persona tal como esta en la base, con sus seis <c>paso_*</c>.</param>
    /// <exception cref="ArgumentNullException">Si la persona es nula.</exception>
    public static RecomendacionDeUnaPersona De(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return new RecomendacionDeUnaPersona(
            persona.Id,
            DeQuien(persona),
            LasDosPreguntas.EstadoDe(persona),
            LasDosPreguntas.SeQuedoEn(persona));
    }

    /// <summary>Cuantas de esas personas tienen las seis preguntas en si.</summary>
    /// <param name="personas">Las personas del documento.</param>
    /// <exception cref="ArgumentNullException">Si la lista es nula.</exception>
    public static int CuantasConfirmadas(IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(personas);
        return personas.Count(persona => LasDosPreguntas.EstadoDe(persona) == true);
    }

    /// <summary>
    /// El resumen del documento entero, en PERSONAS y con su denominador.
    /// </summary>
    /// <remarks>
    /// El denominador va siempre (criterio C1-1): una cifra sin el no se puede comprobar. Y
    /// se cuenta en personas y no en documentos porque esa es la unidad de trabajo del dueno,
    /// fijada el 2026-09-05 (<c>DECISIONES.md</c>, «El ticket es por persona»): <i>«Es por
    /// persona que se revisa la información»</i>. Un documento con diez personas son diez
    /// tickets, y esta linea dice cuantos de ellos estan cerrados.
    /// </remarks>
    /// <param name="personas">Las personas del documento; vacia devuelve la frase de que no se leyo ninguna.</param>
    /// <exception cref="ArgumentNullException">Si la lista es nula.</exception>
    public static string Linea(IReadOnlyList<Persona> personas)
    {
        ArgumentNullException.ThrowIfNull(personas);
        if (personas.Count == 0) return "De este documento no se leyó ninguna persona.";

        var confirmadas = CuantasConfirmadas(personas);
        return $"{confirmadas} de {personas.Count} "
               + Fichas.Reportes.Reglas.Plural.Palabra(personas.Count, "persona", "personas")
               + " con la recomendación confirmada en el sistema del obispo";
    }

    /// <summary>De quien se habla: su nombre y su fila del formulario.</summary>
    /// <remarks>
    /// La fila va siempre que se sepa, incluso con el nombre delante: dos personas del mismo
    /// formulario pueden llamarse igual, y entonces el nombre solo no dice de cual se habla.
    /// Es la misma regla que ya usa <see cref="LoQueContestoElCompanero"/>.
    /// </remarks>
    /// <param name="persona">La persona; sin nombre leido se dice asi, no se inventa uno.</param>
    private static string DeQuien(Persona persona)
    {
        var nombre = ReglasDeCampo.Limpiar(persona.Nombre) ?? "una persona sin nombre leído";
        return persona.FilaFormulario is int fila ? $"{nombre}, fila {fila}" : nombre;
    }
}
