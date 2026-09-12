using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Fichas.Paquetes;

/// <summary>
/// Los seis pasos de «Preparación para las ordenanzas», y la llamada al lider.
/// </summary>
/// <remarks>
/// Que son, y que NO son: son los seis pasos que salen en la pantalla de esa persona
/// dentro del sistema del lider, y el companero los copia uno por uno tal como los ve. NO
/// son las seis ordenanzas del formulario (<c>Persona.Ord*</c>): aquellas dicen a QUE va
/// la persona al templo, y estas dicen si esta en condiciones de ir.
/// <para>
/// ⚠️ Sin contestar NO es «No». Las tres respuestas son si, no y nada, y la tercera es la
/// que importa: una casilla en blanco es una pregunta que nadie miro, y esa persona queda
/// a medias, no reprobada. Por eso <see cref="EstadoDeLosPasos"/> devuelve tres valores y
/// no dos.
/// </para>
/// </remarks>
public static class Pasos
{
    /// <summary>El nombre de la columna en <c>personas</c> y el rotulo con el que sale impresa.</summary>
    /// <remarks>El orden es el de la pantalla del lider, y no se altera: el companero los copia de arriba abajo sin ir y venir.</remarks>
    public static IReadOnlyList<(string Nombre, string Rotulo)> Todos { get; } =
    [
        ("paso_preparacion", "1. Preparación"),
        ("paso_informacion", "2. Información"),
        ("paso_cita_del_templo", "3. Cita del templo"),
        ("paso_acciones_requeridas", "4. Acciones requeridas"),
        ("paso_entrevistas", "5. Entrevistas"),
        ("paso_listo_para_el_templo", "6. Listo para el templo"),
    ];

    /// <summary>La septima pregunta de la hoja. NO es un paso: no sale en la pantalla del lider.</summary>
    public const string ColumnaDeLaLlamada = "llamo_al_lider";

    /// <summary>Como sale impresa la septima pregunta.</summary>
    public const string RotuloDeLaLlamada = "¿Llamó al líder?";

    /// <summary>
    /// Lo que ofrece el menu desplegable. Dos opciones y ninguna mas: la tercera respuesta
    /// —no contestar— se da dejando la celda en blanco, y un valor de menu que dijera «sin
    /// mirar» invitaria a rellenarlo por rellenar.
    /// </summary>
    public static IReadOnlyList<string> Respuestas { get; } = ["Sí", "No"];

    // Como llega escrito «si» y como llega escrito «no» cuando alguien no usa el menu. La
    // lista sale del programa viejo, donde se fue llenando con lo que los agentes escribian
    // de verdad.
    /// <summary>Las formas ya normalizadas que se leen como «Sí». Se compara con <see cref="Normalizar"/> delante, por eso van sin tilde.</summary>
    private static readonly HashSet<string> FormasDelSi =
        ["si", "si completa", "completa", "s", "x", "true", "1", "listo", "hecho"];

    /// <summary>Las formas ya normalizadas que se leen como «No».</summary>
    private static readonly HashSet<string> FormasDelNo =
        ["no", "no esta completa", "incompleta", "n", "false", "0", "falta", "pendiente"];

    /// <summary>Todo lo que no sea letra, dígito, guion bajo o blanco: la puntuación que se quita antes de comparar.</summary>
    private static readonly Regex LoQueNoEsLetraNiEspacio = new(@"[^\w\s]", RegexOptions.Compiled);

    /// <summary>Uno o más blancos seguidos, para dejarlos en un solo espacio.</summary>
    private static readonly Regex EspaciosDeSobra = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Un texto sin tildes, sin signos y en minusculas, para poder compararlo.
    /// </summary>
    /// <remarks>
    /// Se van los acentos Y los signos de puntuacion. Quien contesta a mano escribe «Sí,» o
    /// «Si.» segun le salga, y las dos quieren decir lo mismo; comparar con la coma puesta
    /// convertia una respuesta buena en un reparo.
    /// </remarks>
    /// <param name="texto">Lo que traía la celda; nulo cuenta como vacío.</param>
    /// <returns>Minúsculas, sin marcas diacríticas, sin puntuación y con un solo espacio entre palabras; puede ser la cadena vacía.</returns>
    public static string Normalizar(string? texto)
    {
        var descompuesto = (texto ?? string.Empty).Normalize(NormalizationForm.FormKD);
        var sinTildes = new StringBuilder(descompuesto.Length);
        foreach (var letra in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
                sinTildes.Append(letra);

        var sinSignos = LoQueNoEsLetraNiEspacio.Replace(sinTildes.ToString(), " ");
        return EspaciosDeSobra.Replace(sinSignos, " ").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Si → verdadero, no → falso, en blanco → nulo. Cualquier otra cosa devuelve falso y
    /// deja escrito el detalle.
    /// </summary>
    /// <remarks>
    /// Se devuelve en vez de levantar, y no es capricho: en este proyecto los valores raros
    /// se avisan, nunca se lanzan (requisito 9 del dueno). Y las dos cosas que devuelve el
    /// exito no significan lo mismo: un blanco es una pregunta sin contestar y se guarda
    /// como tal, y un «mas o menos» es una respuesta que alguien escribio y que NADIE puede
    /// interpretar sin inventar. Esa fila se devuelve con su reparo para que la mire una
    /// persona (regla permanente 1).
    /// </remarks>
    /// <param name="texto">Lo que traia la celda.</param>
    /// <param name="valor">Verdadero, falso o nulo si se entendio.</param>
    /// <param name="detalle">Por que no se entendio; nulo cuando si se entendio.</param>
    /// <returns>Si se entendio.</returns>
    public static bool SeEntiendeLaRespuesta(string? texto, out bool? valor, out string? detalle)
    {
        valor = null;
        detalle = null;
        if (texto is null)
            return true;

        var limpio = Normalizar(texto);
        if (limpio.Length == 0)
            return true;
        if (FormasDelSi.Contains(limpio))
        {
            valor = true;
            return true;
        }
        if (FormasDelNo.Contains(limpio))
        {
            valor = false;
            return true;
        }

        detalle = $"no se entiende «{texto}»: se esperaba «Sí» o «No», o la celda en blanco si todavía no se miró";
        return false;
    }

    /// <summary>
    /// Si esa persona esta lista: verdadero, falso, o nulo si todavia no se sabe.
    /// </summary>
    /// <remarks>
    /// Lista es tener los seis. Si alguno esta marcado que no, no lo esta. Y si alguno se
    /// quedo en blanco NO SE SABE, que es distinto de las otras dos: dar por lista a una
    /// persona de la que faltan preguntas por mirar es exactamente lo que manda a alguien
    /// al templo con la recomendacion mal.
    /// </remarks>
    /// <param name="respuestas">Las respuestas por nombre de columna; un paso que no esté en el diccionario cuenta como en blanco.</param>
    public static bool? EstadoDeLosPasos(IReadOnlyDictionary<string, bool?> respuestas)
    {
        var valores = Todos.Select(paso => respuestas.TryGetValue(paso.Nombre, out var v) ? v : null).ToArray();
        if (valores.Any(valor => valor == false))
            return false;
        return valores.All(valor => valor == true) ? true : null;
    }
}
