using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Consultas;

/// <summary>
/// Lo que devuelve cualquier operacion que escribe. NUNCA se lanza por un valor raro.
/// </summary>
/// <remarks>
/// Requisito 9 del dueno, «avisar, nunca impedir»: un numero de caso con la forma
/// equivocada se guarda y sale en <see cref="Avisos"/>; no levanta excepcion ni abre
/// cuadro. Las dos unicas excepciones del proyecto —nada se firma sin Miguel, nada se
/// borra sin preguntar— se contestan con <see cref="SeEscribio"/> en falso y su aviso,
/// tampoco lanzando.
/// </remarks>
/// <param name="SeEscribio">Si algo llego a cambiar en el almacen.</param>
/// <param name="Id">El id de la fila afectada; 0 si no se escribio ninguna.</param>
/// <param name="Avisos">Las lineas que hay que ensenar; puede estar vacia.</param>
public sealed record ResultadoDeEscritura(
    bool SeEscribio,
    long Id,
    IReadOnlyList<Aviso> Avisos)
{
    /// <summary>Salio bien y no hay nada que decir.</summary>
    /// <param name="id">La fila que se escribió; en un alta, el id recién asignado.</param>
    public static ResultadoDeEscritura Bien(long id) => new(true, id, Array.Empty<Aviso>());

    /// <summary>Salio bien y ademas hay algo que senalar.</summary>
    /// <param name="id">La fila que se escribió.</param>
    /// <param name="avisos">Lo que se guardó igual y queda señalado, o lo que ya estaba así; ninguno detiene nada.</param>
    public static ResultadoDeEscritura BienCon(long id, params Aviso[] avisos) => new(true, id, avisos);

    /// <summary>No se escribio nada, y aqui esta el porque en una linea.</summary>
    /// <param name="avisos">El motivo, normalmente un <see cref="Aviso.Problema"/>; es lo que sustituye a lanzar.</param>
    /// <returns><see cref="SeEscribio"/> en falso e <see cref="Id"/> en 0.</returns>
    public static ResultadoDeEscritura NoSeEscribio(params Aviso[] avisos) => new(false, 0, avisos);

    /// <summary>Si hay algo que ensenar en la franja.</summary>
    public bool HayAvisos => Avisos.Count > 0;
}
