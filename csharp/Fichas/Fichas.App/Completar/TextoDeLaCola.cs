using Fichas.App.Grupo;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Completar;

/// <summary>
/// Todo lo que la pestana de completar DICE, en un solo sitio y sin abrir ventana.
/// </summary>
/// <remarks>
/// <para>Se separa del dibujo porque es el texto lo que el dueno lee, y porque asi se puede
/// probar palabra por palabra. Su frase del 2026-09-05 sobre el programa entero fue <i>«el
/// programa es confuso, muy confuso»</i>, y la causa medida en el ADR-0006 §1.3 es que la
/// pantalla ensena una pregunta y el lee la otra.</para>
///
/// <para>⛔ <b>«Listo para asignar» nunca sale a secas.</b> Va siempre con lo que significa
/// —«el sistema llenó todos los campos»—, que es el criterio C17-1: «listo» a secas se lee
/// como «listo para viajar», que es otra pregunta y la contesta otra persona. Las palabras
/// se toman de <see cref="LasDosPreguntas"/> y no se copian aqui: escritas en dos sitios, el
/// dia que se cambie una quedan dos redacciones para la misma cosa.</para>
/// </remarks>
public static class TextoDeLaCola
{
    /// <summary>El titulo de la pestana y de la pantalla, con las palabras del dueno.</summary>
    /// <remarks>Literal suya, 2026-09-06: <i>«completar información de documentos que faltan»</i>.</remarks>
    public const string TituloDeLaPestana = "Completar";

    /// <summary>Lo que se lee bajo el titulo, para que nadie confunda la pestana con Correccion.</summary>
    public const string DeQueVaLaPantalla =
        "Los documentos a los que les falta información, del viaje más cercano al más lejano. "
        + "Al guardar uno que quede sin huecos, sale de la cola y se abre el siguiente. "
        + "Los archivados no entran.";

    /// <summary>
    /// «8 documentos a los que les falta información · de 24 sin archivar, de 26 en la base».
    /// </summary>
    /// <remarks>
    /// El denominador va pegado a la cifra por la regla §8 de <c>CLAUDE.md</c>: un numero
    /// suelto no se puede comprobar. Y son DOS denominadores porque el dueno decidio el
    /// 2026-09-06 que lo archivado no cuenta en ningun sitio: sin la segunda cifra no se
    /// podria ver cuanto se ha archivado.
    /// </remarks>
    /// <param name="quedan">Cuantos documentos hay en la cola.</param>
    /// <param name="casosNoArchivados">Cuantos casos quedan tras quitar los archivados.</param>
    /// <param name="casosEnLaBase">Cuantos casos hay en la base, archivados incluidos.</param>
    public static string LineaDelDenominador(int quedan, int casosNoArchivados, int casosEnLaBase)
        => Plural.Con(quedan,
               "documento al que le falta información",
               "documentos a los que les falta información")
           + $" · de {casosNoArchivados} sin archivar, de {casosEnLaBase} en la base";

    /// <summary>
    /// Lo que se dice del documento que acaba de quedar sin huecos y sale de la cola.
    /// </summary>
    /// <remarks>
    /// ⛔ Dice «listo para asignar», que es una lectura del estado, y NO dice «verificado»:
    /// la firma sigue siendo de Miguel y sigue sin ser automatica (regla permanente 5).
    /// </remarks>
    /// <param name="numeroCaso">El numero del documento que sale.</param>
    /// <param name="quedan">Cuantos quedan en la cola despues de sacarlo.</param>
    public static string AlSalirDeLaCola(string numeroCaso, int quedan)
        => $"{numeroCaso} · {LasDosPreguntas.ListoParaAsignarConSuSignificado}. "
           + $"Sale de la cola; quedan {quedan}.";

    /// <summary>«CASP2609 sigue en la cola: le faltan 2 datos.»</summary>
    /// <param name="numeroCaso">El numero del documento que se queda.</param>
    /// <param name="cuantoLeFalta">Cuantos datos le siguen faltando.</param>
    public static string AlSeguirEnLaCola(string numeroCaso, int cuantoLeFalta)
        => $"{numeroCaso} sigue en la cola: le "
           + Plural.Palabra(cuantoLeFalta, "falta", "faltan") + " "
           + Plural.Con(cuantoLeFalta, "dato", "datos") + ".";

    /// <summary>«CASP2609 sigue en la cola: no se leyó ninguna persona…»</summary>
    /// <remarks>
    /// ⚠️ Tiene su propia frase porque no le falta un CAMPO: le falta la gente. Con los cinco
    /// campos del caso perfectos, la frase de arriba diria «le faltan 0 datos», que es una
    /// cifra que no explica nada y que ademas se lee como un fallo del programa.
    /// <para>
    /// Y dice lo que hay que hacer, porque desde Correccion no se puede: esta pantalla no
    /// anade personas, asi que el documento se queda en la cola hasta que se vuelva a
    /// importar. Callarselo dejaria a Miguel pulsando Guardar sin que nada cambiara.
    /// </para>
    /// </remarks>
    /// <param name="numeroCaso">El numero del documento que se queda.</param>
    public static string AlSeguirSinNingunaPersona(string numeroCaso)
        => $"{numeroCaso} sigue en la cola: no se leyó ninguna persona en este documento, "
           + "así que no hay a quién recomendar. Hay que volver a importarlo.";

    /// <summary>«Se resolvieron 3 documentos y la cola quedó vacía.»</summary>
    /// <remarks>
    /// Terminar sin cifra deja al dueno sin saber si trabajo tres documentos o treinta, que
    /// es lo unico que puede comprobar de una sesion de trabajo.
    /// </remarks>
    /// <param name="cuantosSeResolvieron">Cuantos salieron de la cola en esta vuelta.</param>
    public static string AlVaciarseLaCola(int cuantosSeResolvieron)
        => "Se " + Plural.Palabra(cuantosSeResolvieron, "resolvió", "resolvieron") + " "
           + Plural.Con(cuantosSeResolvieron, "documento", "documentos")
           + " y la cola quedó vacía.";

    /// <summary>
    /// Lo que se lee cuando no queda nada, con lo que eso NO significa.
    /// </summary>
    /// <remarks>
    /// ⚠️ La segunda frase es la que evita el defecto de fondo del 2026-09-05: sin ella,
    /// «no queda nada» se lee como «ya pueden viajar», y el dueno dijo con todas las letras
    /// que eso lo decide otra cosa —<i>«no se ha verificado la recomendación en el sistema
    /// del obispo, que es lo que realmente verifico yo»</i>—.
    /// </remarks>
    /// <remarks>
    /// ⚠️ La frase dice «cada persona esté» y no «estén», y no es un capricho de estilo: la
    /// constante <see cref="LasDosPreguntas.ListaParaViajar"/> esta en SINGULAR porque es el
    /// estado de UNA persona —la unidad de trabajo es la persona, fijado el 2026-09-05—, y
    /// pegarla detras de un plural daba «que estén lista para viajar». Salio medido con la
    /// ventana abierta el 2026-09-06, no leyendo el codigo.
    /// </remarks>
    public const string CuandoNoQuedaNada =
        "No queda ningún documento con información que falte. "
        + "Eso no quiere decir que cada persona esté " + LasDosPreguntas.ListaParaViajar
        + ": la recomendación se confirma en el sistema del obispo, y eso no lo ve este programa.";

    /// <summary>El rotulo del boton que arranca el flujo desde el primero de la cola.</summary>
    public const string BotonDeEmpezar = "Empezar por el que viaja antes";

    /// <summary>Lo que se dice si se pulsa empezar con la cola ya vacia.</summary>
    public const string NoHayPorDondeEmpezar = "No hay ningún documento en la cola por el que empezar.";
}
