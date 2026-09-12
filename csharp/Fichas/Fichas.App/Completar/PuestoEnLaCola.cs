using Fichas.App.Inicio;

namespace Fichas.App.Completar;

/// <summary>
/// Un renglon de la cola: que puesto ocupa, cuando viaja, que documento es y que le falta.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Lleva el numero de orden a proposito, y no es adorno.</b> El dueno pidio un
/// flujo —<i>«un flujo de trabajo donde yo vaya resolviendo casos de manera automática»</i>—
/// y sin el puesto no hay forma de comprobar que el encadenado va por donde debe: «el
/// siguiente» solo se puede verificar contra un orden que se vea.</para>
///
/// <para>Se compone sobre <see cref="RenglonDeCaso"/>, que es lo que ya devuelve
/// <c>LectorDeIncompletos</c>, y no sobre una lectura propia: dos formas de decir lo mismo
/// se separan el dia que alguien toque una.</para>
/// </remarks>
/// <param name="Puesto">Que numero hace en la cola, contando desde 1.</param>
/// <param name="Documento">El documento tal como lo leyo el lector de incompletos.</param>
public sealed record PuestoEnLaCola(int Puesto, RenglonDeCaso Documento)
{
    /// <summary>El id del documento que se abre al pulsar.</summary>
    public long CasoId => Documento.CasoId;

    /// <summary>El puesto tal como se pinta: «1», «2», «17».</summary>
    public string NumeroDeOrden => Puesto.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>El numero del documento, o la frase de que no traia ninguno.</summary>
    public string NumeroCaso => Documento.NumeroCaso;

    /// <summary>«en 3 días», «hoy», «hace 2 días» o «sin fecha».</summary>
    public string CuandoViaja => Documento.CuandoViaja;

    /// <summary>«Rama de Prueba · me falta · 3 personas».</summary>
    /// <remarks>
    /// <para>La unidad va delante porque es lo que le dice a QUIEN hay que llamar, que es su
    /// trabajo de verdad: <i>«yo debo llamar al obispo, saber cómo le puedo ayudar a
    /// completar ese caso»</i> (2026-09-05).</para>
    ///
    /// <para>⛔ <b>2026-09-07:</b> aquí iba «le faltan 2 datos». Ahora va la palabra, y cuántos
    /// datos son va en <see cref="DetalleDeLoQueLeFalta"/>. Esta cola ES la de lo que no está
    /// completo, así que la palabra se repite en todos los renglones y la cifra es lo que los
    /// distingue: por eso el detalle sigue estando y a un clic.</para>
    /// </remarks>
    public string LoQueLeFalta
        => $"{Documento.Unidad} · {Documento.PalabraDelEstado} · {Documento.CuantasPersonasTexto}";

    /// <summary>Qué le falta a este documento y a quién le toca; se lee cuando él lo pide.</summary>
    public string DetalleDeLoQueLeFalta => Documento.DetalleDelEstado;

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre el renglon entero.</summary>
    /// <remarks>
    /// Un renglon son cuatro trozos de texto sueltos; sin esto, el lector los lee uno a uno
    /// y quien no ve la pantalla no sabe que forman una sola cosa que ademas se puede pulsar.
    /// </remarks>
    public string ParaElLector
        => $"Puesto {Puesto}. {NumeroCaso}, {Documento.Unidad}, {CuandoViaja}, "
           + $"{Documento.Lectura.ParaElLector}. Pulse para entrar en la cola por este documento.";

    /// <summary>Numera una cola entera, del puesto 1 en adelante.</summary>
    /// <param name="documentos">Los documentos de la cola, ya en su orden.</param>
    /// <exception cref="ArgumentNullException">Si la lista es nula.</exception>
    public static IReadOnlyList<PuestoEnLaCola> Numerar(IReadOnlyList<RenglonDeCaso> documentos)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        return [.. documentos.Select((documento, donde) => new PuestoEnLaCola(donde + 1, documento))];
    }
}
