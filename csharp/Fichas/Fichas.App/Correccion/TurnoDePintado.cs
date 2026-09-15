namespace Fichas.App.Correccion;

/// <summary>
/// El número de generación de lo que está delante: cada apertura pide un turno nuevo, y lo
/// que vuelve de otro hilo con un turno viejo no se pinta.
/// </summary>
/// <remarks>
/// <para>Existe por el defecto del 2026-09-15, medido en el cuaderno del dueño: al entrar a
/// Corrección se abrían DOS casos seguidos —el primero del primer grupo, sin pedirlo, y 250 ms
/// después el elegido—, cada uno lanzaba su rasterizado y su OCR de bandas, y nada impedía
/// que la imagen o las bandas del primero llegaran cuando el segundo ya estaba delante. Eso es
/// «el documento que se superpone»: el papel de un caso sobre los campos de otro.</para>
///
/// <para>Es una clase de dos métodos y no un entero suelto en la página, porque así se prueba
/// sin ventana y la regla queda en UN sitio: la usan el visor para la imagen y la página para
/// las bandas y la hoja.</para>
/// </remarks>
public sealed class TurnoDePintado
{
    /// <summary>El último turno pedido; cero mientras nadie pidió ninguno.</summary>
    private long _vigente;

    /// <summary>Pide un turno nuevo, que deja viejos a todos los anteriores.</summary>
    /// <returns>El turno, siempre mayor que el anterior.</returns>
    public long Pedir() => ++_vigente;

    /// <summary>Si ese turno sigue siendo el que está delante.</summary>
    /// <param name="turno">El turno que se pidió antes de irse a otro hilo.</param>
    public bool SigueVigente(long turno) => turno == _vigente;

    /// <summary>El turno vigente, para poder decirlo en el cuaderno.</summary>
    public long Vigente => _vigente;
}
