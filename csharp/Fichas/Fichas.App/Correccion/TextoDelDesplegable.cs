using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Como se lee cada caso en el desplegable de la correccion.
/// </summary>
/// <remarks>
/// <para>Sale de un defecto que QA midio sobre el paquete publicado el 2026-09-04: el
/// desplegable ofrecia 18 entradas y <b>catorce se llamaban igual</b>, «CASP2609», sin
/// nombre de persona, sin archivo y sin marca de duplicado. Elegir entre ellas es elegir a
/// ciegas, y el numero de caso <b>no distingue</b> desde la migracion 12: identifica una
/// unidad y un mes, no un formulario.</para>
///
/// <para>Lo que se pone, y por que cada cosa:</para>
/// <list type="bullet">
/// <item><b>El numero</b>, que es como Miguel llama al caso y como vuelve el Excel del
/// companero. Cuando no se pudo leer se dice «sin numero» con el numero interno detras,
/// que es lo unico que lo distingue entonces.</item>
/// <item><b>Quien va dentro</b>: el primer nombre y cuantos son. Es lo que Miguel busca de
/// verdad —«el de Ana»—, y con diez personas la cuenta dice que ese es el de grupo.</item>
/// <item><b>Que es un duplicado, y de cual</b>: la base lo sabe (<c>casos.duplicado_de</c>)
/// y hasta hoy no lo decia. Se nombra por el ARCHIVO del original y no por su numero
/// interno: «caso 10» no significa nada para quien mira la pantalla.</item>
/// <item><b>El archivo</b>, que es lo unico que separa con certeza dos formularios del
/// mismo mes y la misma unidad.</item>
/// </list>
///
/// <para>Vive fuera de cualquier control de XAML a proposito: asi la linea que ve Miguel se
/// lee en una prueba sin abrir una ventana.</para>
/// </remarks>
public static class TextoDelDesplegable
{
    /// <summary>Tope de la linea; mas que esto no cabe en el desplegable ni se lee.</summary>
    public const int LargoMaximoDeLaLinea = 120;

    /// <summary>
    /// Como se lee un caso en el desplegable.
    /// </summary>
    /// <param name="caso">El caso que se ofrece.</param>
    /// <param name="primerNombre">El nombre de la primera persona; nulo o vacio si no se leyo.</param>
    /// <param name="cuantasPersonas">Cuantas personas tiene el caso.</param>
    /// <param name="original">El caso del que este es duplicado, o nulo si no lo es.</param>
    /// <param name="contestadas">Por cuantas de sus personas contesto ya el companero.</param>
    /// <param name="quienMarcoElEstado">
    /// El nombre de quien puso el estado del documento; nulo si nadie lo puso o no se pudo
    /// resolver. De el sale la marca que distingue el atajo del administrador de lo que
    /// escribio el Excel de un companero, <b>sin abrir el documento</b>.
    /// </param>
    /// <param name="loQueLeFalta">
    /// Si al documento le falta algun dato, ya en palabras
    /// (<see cref="Grupo.LasDosPreguntas.LoQueLeFaltaAlDocumento"/>); vacio para no decir nada.
    /// <para>
    /// ⚠️ Entra el 2026-09-07 y va <b>pegado al numero</b>, delante de todo lo demas, por lo
    /// mismo que la marca del estado: el recorte de la linea muerde por el final, y esto es lo
    /// UNICO que cambia cuando el dueno corrige un documento y guarda. Un dato que solo se ve
    /// cuando la linea es corta no se ve. Que significa «listo para asignar» se dice una vez y
    /// para siempre al lado del desplegable, en la linea del denominador, para no repetir
    /// treinta caracteres en cada entrada (criterio C17-1).
    /// </para>
    /// </param>
    public static string Componer(
        Caso caso,
        string? primerNombre,
        int cuantasPersonas,
        Caso? original,
        int contestadas = 0,
        string? quienMarcoElEstado = null,
        string loQueLeFalta = "")
    {
        ArgumentNullException.ThrowIfNull(caso);

        var partes = new List<string> { Numero(caso) };
        if (loQueLeFalta.Length > 0) partes.Add(loQueLeFalta);
        if (caso.DuplicadoDe is not null) partes.Add(DeQuienEsDuplicado(original, caso.DuplicadoDe.Value));

        // ⛔ La marca del estado va DELANTE de todo lo demas y no al final: es lo primero que
        // el dueno tiene que poder distinguir de un vistazo —«el administrador lo hizo» no es
        // lo mismo que «lo marco el Excel de Sandy»—, y el recorte de la linea muerde por el
        // final. Un dato que solo se ve cuando la linea es corta no se ve.
        var marca = TextoDeLaMarcaDelEstado.Corta(caso, quienMarcoElEstado);
        if (marca.Length > 0) partes.Add(marca);

        partes.Add(SiYaContesto(contestadas, cuantasPersonas));
        partes.Add(Gente(primerNombre, cuantasPersonas));

        var archivo = Archivo(caso.RutaPdf);
        if (archivo.Length > 0) partes.Add(archivo);

        return Recortar(string.Join(" · ", partes));
    }

    /// <summary>El numero de caso, o lo unico que lo distingue cuando no lo tiene.</summary>
    private static string Numero(Caso caso)
        => ReglasDeCampo.Limpiar(caso.NumeroCaso) ?? $"sin número ({caso.Id})";

    /// <summary>
    /// Si el companero ya contesto por esta gente, dicho SIN abrir el documento.
    /// </summary>
    /// <remarks>
    /// <para>Sale de la queja del dueno del 2026-09-05: el Excel de vuelta escribia el
    /// estado de cada persona y <b>ninguna pantalla lo leia</b> —cero coincidencias de
    /// <c>EstadoPropuesto</c>, <c>PropuestoPor</c> y los seis <c>Paso*</c> en todo
    /// <c>Fichas.App</c>, medido—. Aqui es donde se distingue sin abrir nada.</para>
    /// <para>⚠️ <b>Las dos se dicen con palabras, y ninguna se dice callando.</b> Una marca
    /// que solo aparece cuando SI se contesto se confunde con una pantalla que todavia no
    /// sabe pintarla, y entonces «no contesto» y «no lo se» se ven igual.</para>
    /// <para>⛔ Esto NO dice nada de la firma de Miguel: es lo que dijo el companero, que
    /// es otra cosa (regla permanente 5).</para>
    /// </remarks>
    private static string SiYaContesto(int contestadas, int cuantasPersonas)
    {
        if (contestadas <= 0) return "sin contestar";
        if (cuantasPersonas > 0 && contestadas >= cuantasPersonas) return "ya contestó";
        return $"contestadas {contestadas} de {cuantasPersonas}";
    }

    /// <summary>Quien va dentro: el primero y cuantos son.</summary>
    /// <remarks>
    /// Un caso sin personas se dice tal cual y no se calla: es raro, y callarlo lo esconde
    /// justo en la pantalla donde habria que arreglarlo.
    /// </remarks>
    private static string Gente(string? primerNombre, int cuantasPersonas)
    {
        if (cuantasPersonas <= 0) return "sin ninguna persona";

        var nombre = ReglasDeCampo.Limpiar(primerNombre) ?? "sin nombre leído";
        return cuantasPersonas == 1 ? nombre : $"{nombre} y {cuantasPersonas - 1} más";
    }

    /// <summary>Que es un duplicado, y de cual. Por el archivo, que es lo que se reconoce.</summary>
    private static string DeQuienEsDuplicado(Caso? original, long originalId)
    {
        var archivo = original is null ? string.Empty : Archivo(original.RutaPdf);
        return archivo.Length > 0
            ? $"DUPLICADO de {archivo}"
            : $"DUPLICADO del caso {originalId}";
    }

    /// <summary>El nombre del archivo, sin la carpeta; vacio si no hay ruta.</summary>
    private static string Archivo(string? rutaPdf)
    {
        var ruta = ReglasDeCampo.Limpiar(rutaPdf);
        if (ruta is null) return string.Empty;
        try
        {
            return Path.GetFileName(ruta);
        }
        catch (ArgumentException)
        {
            // Una ruta con caracteres que el sistema no admite NO tumba el desplegable
            // (requisito 9): se ensena tal cual y ya.
            return ruta;
        }
    }

    /// <summary>Recorta por el final con puntos suspensivos; nunca parte el numero de caso.</summary>
    private static string Recortar(string linea)
        => linea.Length <= LargoMaximoDeLaLinea
            ? linea
            : string.Concat(linea.AsSpan(0, LargoMaximoDeLaLinea - 1), "…");
}
