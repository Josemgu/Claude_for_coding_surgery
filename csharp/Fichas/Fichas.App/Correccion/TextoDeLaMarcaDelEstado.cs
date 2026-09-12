using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Como se lee QUIEN puso el estado de un documento, y por que camino.
/// </summary>
/// <remarks>
/// Sobre un documento pueden convivir tres marcas, y el dueno las nombro como tres cosas
/// distintas. Confundirlas no es un detalle de redaccion: cada una dice quien responde de
/// que, y de eso vive el producto.
/// <list type="number">
/// <item><b>La firma de campos</b> —«Todo correcto»—, que es suya y va campo por campo
/// (regla permanente 5). <b>NO sale en esta frase</b>: vive en
/// <c>procedencia_campo.verificado</c> y la cuenta el pie de la pantalla, aparte.</item>
/// <item><b>El estado que escribe el Excel del companero</b>, con el nombre del companero:
/// <i>«el documento que ellos llenan es el que marca, y dice completado por Sandy»</i>
/// (2026-09-03).</item>
/// <item><b>El atajo del administrador</b>: <i>«cuando pase eso debe decir "el administrador
/// lo hizo"»</i>.</item>
/// </list>
/// <para>
/// Lo que separa la segunda de la tercera ya esta en la base y no hace falta anadir nada:
/// <c>casos.estado_marcado_origen</c> (migracion 14) y <c>casos.estado_del_companero</c>,
/// que solo escribe <c>MarcarEstadoDelCompanero</c>. Es la decision de ADR-0005 §6.1.
/// </para>
/// <para>
/// Va sin ventana a proposito, como <see cref="TextoDelAcuse"/> y
/// <see cref="TextoDelDesplegable"/>: asi la frase que ve el dueno se comprueba en una
/// prueba, que es la unica forma de que alguien la mire de verdad.
/// </para>
/// </remarks>
public static class TextoDeLaMarcaDelEstado
{
    /// <summary>Tope de la frase; mas que esto ya no es un renglon (requisito 4 del dueno).</summary>
    public const int LargoMaximoDeLaLinea = 120;

    /// <summary>
    /// La frase larga, para el pie del documento: en que estado esta y de donde vino.
    /// </summary>
    /// <remarks>
    /// Un caso que nadie marco devuelve la cadena vacia. Inventar aqui una frase —«sin
    /// marcar»— la leeria como un estado que alguien puso, y quien dice si el companero
    /// contesto o no es la linea del desplegable, que ya lo dice con palabras.
    /// </remarks>
    /// <param name="caso">El caso, tal como esta en la base.</param>
    /// <param name="quienMarco">El nombre de quien puso la marca; nulo si no se pudo resolver.</param>
    public static string Componer(Caso caso, string? quienMarco)
    {
        ArgumentNullException.ThrowIfNull(caso);
        if (caso.Estado == EstadoDeRecomendacion.SinMarcar) return string.Empty;

        return $"{ComoEsta(caso)} · {DeDondeVino(caso, quienMarco)}";
    }

    /// <summary>
    /// La frase corta, para la linea del desplegable, donde ya compiten seis datos.
    /// </summary>
    /// <remarks>
    /// Dice lo mismo en menos: la del administrador se queda con sus palabras exactas, que
    /// son lo unico que hay que reconocer de un vistazo; las otras dos llevan el estado y el
    /// nombre, porque ahi lo que importa es <b>quien</b> lo dijo.
    /// </remarks>
    /// <param name="caso">El caso, tal como esta en la base.</param>
    /// <param name="quienMarco">El nombre de quien puso la marca; nulo si no se pudo resolver, y entonces no se inventa.</param>
    /// <exception cref="ArgumentNullException">Si el caso es nulo.</exception>
    public static string Corta(Caso caso, string? quienMarco)
    {
        ArgumentNullException.ThrowIfNull(caso);
        if (caso.Estado == EstadoDeRecomendacion.SinMarcar) return string.Empty;

        if (LoMarcoElAdministrador(caso)) return ElAdministrador.Origen;

        var deQuien = quienMarco is null ? string.Empty : $" de {quienMarco}";
        return LoMarcoElExcelDeUnCompanero(caso)
            ? $"{ComoEsta(caso)} según el Excel{deQuien}"
            : $"{ComoEsta(caso)} a mano{(quienMarco is null ? string.Empty : $" por {quienMarco}")}";
    }

    /// <summary>Si esa marca la puso el atajo del administrador.</summary>
    /// <remarks>
    /// Se pregunta SIEMPRE la primera, y por eso: un caso que el Excel de un companero marco
    /// primero y el administrador remato despues conserva <c>estado_del_companero</c>, que
    /// es lo que quiso la migracion 14. Lo que vale ahora es lo ultimo que se escribio.
    /// </remarks>
    /// <param name="caso">El caso, del que se lee <c>estado_marcado_origen</c>.</param>
    /// <exception cref="ArgumentNullException">Si el caso es nulo.</exception>
    public static bool LoMarcoElAdministrador(Caso caso)
    {
        ArgumentNullException.ThrowIfNull(caso);
        return string.Equals(caso.EstadoMarcadoOrigen, ElAdministrador.Origen, StringComparison.Ordinal);
    }

    /// <summary>«completa» o «no está completa», que es lo que el estado significa.</summary>
    /// <param name="caso">El caso, del que se lee el estado vigente; aqui nunca llega sin marcar.</param>
    private static string ComoEsta(Caso caso)
        => caso.Estado == EstadoDeRecomendacion.Completa ? "completa" : "no está completa";

    /// <summary>
    /// De donde vino la marca, dicho de forma que las tres no se puedan confundir.
    /// </summary>
    /// <remarks>
    /// ⛔ La del administrador lleva <b>«sin verificar campo por campo»</b> pegado, y no es
    /// adorno: es lo unico que separa «este documento esta completo porque alguien lo
    /// comprobo» de «este documento esta completo porque el administrador lo dijo». Sin esa
    /// mitad, el atajo se leeria como una verificacion que nadie hizo.
    /// </remarks>
    /// <param name="caso">El caso, del que se leen el origen y <c>estado_del_companero</c>.</param>
    /// <param name="quienMarco">El nombre de quien puso la marca; nulo se calla.</param>
    private static string DeDondeVino(Caso caso, string? quienMarco)
    {
        var firma = quienMarco is null ? string.Empty : $" · {quienMarco}";

        if (LoMarcoElAdministrador(caso))
            return $"{ElAdministrador.Origen}, sin verificar campo por campo{firma}";

        if (LoMarcoElExcelDeUnCompanero(caso))
            return quienMarco is null ? "lo marcó el Excel de un compañero" : $"lo marcó el Excel de {quienMarco}";

        return quienMarco is null ? "marcado a mano" : $"marcado a mano por {quienMarco}";
    }

    /// <summary>
    /// Si esa marca la trajo la hoja que devolvio un companero.
    /// </summary>
    /// <remarks>
    /// Se mira <c>estado_del_companero</c> y no la forma del origen: esa columna la escribe
    /// UN solo camino —<c>ICasos.MarcarEstadoDelCompanero</c>—, mientras que el origen de una
    /// hoja es su ruta, que es texto libre y no se puede reconocer por su pinta sin acabar
    /// adivinando.
    /// </remarks>
    /// <param name="caso">El caso, del que se lee <c>estado_del_companero</c>.</param>
    private static bool LoMarcoElExcelDeUnCompanero(Caso caso)
        => !string.IsNullOrWhiteSpace(caso.EstadoDelCompanero);
}
