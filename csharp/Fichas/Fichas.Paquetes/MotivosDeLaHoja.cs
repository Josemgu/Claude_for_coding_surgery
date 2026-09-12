using Fichas.Contratos.Modelos;

namespace Fichas.Paquetes;

/// <summary>
/// Las dos columnas donde el compañero dice POR QUÉ no pudo, y cómo se leen al volver.
/// </summary>
/// <remarks>
/// De dónde salen, con las palabras del dueño: «en el calendario también puede decir el
/// estado: no completado, no se pudo comunicar con el líder, o el líder no lo hizo», y «si
/// el agente no pudo comunicarse con el líder y faltan cambios… debo crear reporte para
/// ellos con los comentarios de los agentes». O sea: el motivo y el comentario los pone el
/// agente en la hoja que se le manda, y vuelven al sistema.
/// <para>
/// ⚠️ Lo que se guarda es la CLAVE —<c>no_se_pudo_comunicar</c>— y no la frase. Eso ya lo
/// decide <see cref="Caso.EscribirMotivo"/>; aquí solo están las tres frases que el
/// compañero LEE en el menú de la hoja. Están escritas otra vez y no importadas de
/// <c>Fichas.App</c> a propósito: <c>Fichas.Paquetes</c> no depende de la aplicación —lo
/// contrario montaría una hoja de Excel encima de una pantalla—, y el sitio natural para
/// compartirlas sería <c>Fichas.Contratos</c>, que hoy está congelado. Queda dicho para
/// que el día que se descongele se junten en uno.
/// </para>
/// </remarks>
public static class MotivosDeLaHoja
{
    /// <summary>La columna de <c>casos</c> donde acaba lo que elija el compañero.</summary>
    public const string ColumnaDelMotivo = "motivo_del_companero";

    /// <summary>Como sale impresa la pregunta del motivo.</summary>
    public const string RotuloDelMotivo = "¿Por qué no se completó?";

    /// <summary>La columna de <c>personas</c> donde acaba lo que escriba de su puño.</summary>
    public const string ColumnaDelComentario = "nota_companero";

    /// <summary>Como sale impresa la casilla del comentario libre.</summary>
    public const string RotuloDelComentario = "Comentario";

    /// <summary>Lo ancha que va la columna del motivo: la opción más larga son 32 caracteres.</summary>
    public const int AnchoDelMotivo = 34;

    /// <summary>Lo ancha que va la del comentario; es donde se escribe una frase entera.</summary>
    public const int AnchoDelComentario = 40;

    /// <summary>
    /// Las tres opciones del menú, en las palabras del dueño y en su orden.
    /// </summary>
    /// <remarks>
    /// No hay una cuarta que diga «sin motivo»: eso se dice dejando la celda en blanco, y
    /// una opción que lo nombrara invitaría a rellenarla por rellenar. Es la misma razón
    /// por la que el menú de los pasos tiene dos y no tres (<see cref="Pasos.Respuestas"/>).
    /// </remarks>
    public static IReadOnlyList<string> Opciones { get; } =
    [
        "No se pudo comunicar con el líder",
        "El líder no lo hizo",
        "Otra razón",
    ];

    // La frase normalizada de cada opción, que es como se compara al volver. Se calcula del
    // propio menu para que cambiar una frase no obligue a acordarse de cambiar dos sitios.
    /// <summary>Cada opción del menú, normalizada con <see cref="Pasos.Normalizar"/>, y el motivo que significa.</summary>
    private static readonly Dictionary<string, MotivoDeNoCompletar> PorLaFrase = new()
    {
        [Pasos.Normalizar(Opciones[0])] = MotivoDeNoCompletar.NoSePudoComunicar,
        [Pasos.Normalizar(Opciones[1])] = MotivoDeNoCompletar.ElLiderNoLoHizo,
        [Pasos.Normalizar(Opciones[2])] = MotivoDeNoCompletar.OtraRazon,
    };

    /// <summary>La frase que se lee en el menú para un motivo; vacío para el que no lo tiene.</summary>
    /// <param name="motivo">El motivo guardado en la base; <see cref="MotivoDeNoCompletar.SinMotivo"/> da la cadena vacía.</param>
    public static string Decir(MotivoDeNoCompletar motivo) => motivo switch
    {
        MotivoDeNoCompletar.NoSePudoComunicar => Opciones[0],
        MotivoDeNoCompletar.ElLiderNoLoHizo => Opciones[1],
        MotivoDeNoCompletar.OtraRazon => Opciones[2],
        _ => string.Empty,
    };

    /// <summary>
    /// Lee la celda del motivo: en blanco es sin motivo, y lo que no está en la lista NO se
    /// adivina.
    /// </summary>
    /// <remarks>
    /// Devuelve si se entendió, igual que <see cref="Pasos.SeEntiendeLaRespuesta"/>, pero lo
    /// que quien llama hace con el «no» es lo contrario, y la diferencia está pensada: un
    /// paso que no se entiende descarta la fila entera porque podría ser justo el que dice
    /// que falta algo, y un motivo que no se entiende NO puede empeorar el estado de nadie.
    /// Tirar seis respuestas buenas por una frase escrita a mano sería perder el trabajo del
    /// compañero; se avisa con su fila y su texto, y quien lo lea decide.
    /// <para>
    /// Se aceptan también las tres claves de la base —<c>no_se_pudo_comunicar</c>— porque un
    /// Excel puede venir armado desde otro sitio con lo que la columna guarda de verdad.
    /// </para>
    /// </remarks>
    /// <param name="texto">Lo que traía la celda.</param>
    /// <param name="motivo">El motivo leído; sin motivo si la celda venía en blanco.</param>
    /// <param name="detalle">Qué venía escrito, cuando no se entendió; nulo si se entendió.</param>
    /// <returns>Si se entendió.</returns>
    public static bool SeEntiendeElMotivo(string? texto, out MotivoDeNoCompletar motivo, out string? detalle)
    {
        motivo = MotivoDeNoCompletar.SinMotivo;
        detalle = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var deLaBase = Caso.LeerMotivo(texto);
        if (deLaBase != MotivoDeNoCompletar.SinMotivo)
        {
            motivo = deLaBase;
            return true;
        }

        if (PorLaFrase.TryGetValue(Pasos.Normalizar(texto), out var deLaFrase))
        {
            motivo = deLaFrase;
            return true;
        }

        detalle = $"no se entiende «{texto}»: se esperaba una de las tres del menú "
                  + $"({string.Join(", ", Opciones)}), o la celda en blanco";
        return false;
    }
}
