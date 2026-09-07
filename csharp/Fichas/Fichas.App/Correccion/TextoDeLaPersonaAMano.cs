namespace Fichas.App.Correccion;

/// <summary>
/// Las palabras del cuadro de «escribir a mano quien va en este documento», en un solo sitio
/// y fuera de todo XAML, para que se puedan leer en una prueba.
/// </summary>
/// <remarks>
/// <para><b>Por que existe este cuadro.</b> Palabras del dueno el 2026-09-07, ensenando un
/// renglon que dice «sin ninguna persona leída · sin cédula leída»: <i>«¿Cómo voy a confirmar
/// la recomendación si no me da la opción?»</i>. Medido antes de tocar nada, con la ventana
/// abierta: sobre ese documento la pantalla de Correccion no tenia <b>ni un boton</b> con el
/// que decir quien es, y sin persona no hay seis preguntas que contestar, asi que la
/// recomendacion no se podia confirmar nunca.</para>
///
/// <para>⛔ <b>Por que el programa NO propone el nombre del archivo, aunque el nombre este
/// ahi.</b> El archivo de su caso se llama <c>ELTC2609_Maria_Clarisa_Simulado.pdf</c> y una
/// regla podria sacar de ahi «Maria Clarisa Simulado». No se hace, y no es formalismo:</para>
/// <list type="number">
///   <item>Entre los siete escaneos reales del dueno hay
///   <c>SURB2609_Suriname_Group_Complete.pdf</c>: esa misma regla propondria una persona
///   llamada «Suriname Group Complete», que no existe. Y
///   <c>CASP2609_Daniel_Jr._Damian_Dorian_Ejemplo.pdf</c>, del que la regla no sabe si es
///   una persona o son cuatro.</item>
///   <item>Una propuesta puesta DENTRO de la casilla se acepta por cansancio. Un dato
///   inventado que se acepta sin mirar es exactamente lo que la regla permanente 1 de
///   <c>CLAUDE.md</c> prohibe, y el dano esta escrito alli: <i>«un MRN o una fecha de viaje
///   inventados causan daño real»</i>.</item>
///   <item>Y la <b>cedula</b> —que es la que manda a alguien al templo con la recomendacion
///   equivocada— no esta en el nombre del archivo de ninguna forma. O sea que la propuesta
///   ahorraria como mucho medio gesto y compraria un riesgo entero.</item>
/// </list>
///
/// <para><b>Lo que si se hace</b>, y es lo que el pidio de verdad: se le ENSENA como se llama
/// el archivo, al lado de la casilla y fuera de ella. Eso es evidencia —lo mismo que tener el
/// papel delante—, no un dato leido, y quien decide es el. Si prefiere que el programa
/// rellene la casilla y el acepte o rechace, es una decision suya y va nombrada en la
/// entrega: no la toma quien programa.</para>
/// </remarks>
public static class TextoDeLaPersonaAMano
{
    /// <summary>El titulo del cuadro.</summary>
    public const string Titulo = "De este documento no se leyó ninguna persona";

    /// <summary>De que va, con la consecuencia delante para que se entienda por que importa.</summary>
    public const string DeQueVa =
        "Mientras no haya nadie dentro, no hay ninguna recomendación que confirmar y este "
        + "documento no puede avanzar. Escriba quién va en el papel que tiene delante.";

    /// <summary>Lo que se lee bajo la referencia del archivo; es la regla permanente 1 en una linea.</summary>
    public const string NiUnDatoSeAdivina =
        "el programa no rellena esto por usted: lo que entre aquí es lo que usted escriba";

    /// <summary>El rotulo de la casilla del nombre; el mismo que usa la ficha de campo.</summary>
    public const string RotuloDelNombre = "Nombre";

    /// <summary>El rotulo de la casilla de la cedula.</summary>
    public const string RotuloDeLaCedula = "Cédula";

    /// <summary>Lo que dice el boton. Nombra lo que hace y no «Aceptar».</summary>
    public const string BotonDeAnadir = "Añadir esta persona";

    /// <summary>Como se llama ese boton para quien no ve la pantalla.</summary>
    public const string BotonParaElLector =
        "Añadir a mano una persona a este documento con el nombre y la cédula escritos";

    /// <summary>Lo que se dice cuando el nombre viene vacio.</summary>
    public static string SinNombre =>
        "escriba el nombre de la persona: sin nombre no se puede añadir";

    /// <summary>El detalle del aviso anterior; dice que hacer y no solo que pasa.</summary>
    public static string PorQueHaceFaltaElNombre =>
        "El nombre es lo único imprescindible: es lo que identifica a la persona cuya "
        + "recomendación hay que confirmar. La cédula puede quedarse en blanco y el documento "
        + "seguirá diciendo que le falta.";

    /// <summary>Lo que se dice cuando no hay ningun documento abierto.</summary>
    public static string SinDocumentoAbierto =>
        "no hay ningún documento abierto: no hay dónde añadir a nadie";

    /// <summary>La linea del pie cuando la persona entro.</summary>
    /// <param name="nombre">El nombre tal como se escribio.</param>
    /// <param name="conCedula">Si ademas se escribio una cedula.</param>
    public static string AlAnadirla(string nombre, bool conCedula)
        => conCedula
            ? $"«{nombre}» añadida a mano, con su cédula. Escrito por usted, no leído del papel."
            : $"«{nombre}» añadida a mano, SIN cédula. Escrito por usted, no leído del papel.";

    /// <summary>La referencia del archivo, que se ensena y no se copia a ninguna casilla.</summary>
    /// <param name="rutaPdf">La ruta del PDF del documento; nula o vacia si no hay ninguna.</param>
    public static string DeDondeSacarElNombre(string? rutaPdf)
    {
        var archivo = NombreDelArchivo(rutaPdf);
        return archivo.Length == 0
            ? $"Este documento no tiene ningún archivo anotado · {NiUnDatoSeAdivina}"
            : $"El archivo se llama «{archivo}» · {NiUnDatoSeAdivina}";
    }

    /// <summary>El nombre del archivo sin su carpeta; vacio si no hay ruta o no se puede leer.</summary>
    /// <remarks>
    /// Una ruta con caracteres que el sistema no admite NO tumba el cuadro (requisito 9): se
    /// ensena tal cual, que es mas de lo que se sabria callandola.
    /// </remarks>
    private static string NombreDelArchivo(string? rutaPdf)
    {
        var ruta = ReglasDeCampo.Limpiar(rutaPdf);
        if (ruta is null) return string.Empty;

        try
        {
            return Path.GetFileName(ruta);
        }
        catch (ArgumentException)
        {
            return ruta;
        }
    }
}
