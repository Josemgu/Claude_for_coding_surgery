namespace Fichas.Contratos.Modelos;

/// <summary>
/// Una linea de aviso. Es el unico canal por el que el programa dice que algo raro paso.
/// </summary>
/// <remarks>
/// Requisito 4 del dueno («ni un parrafo en pantalla»): <see cref="Linea"/> es lo que se
/// pinta, y cabe en un renglon. Lo largo va en <see cref="Detalle"/>, que solo se ve al
/// pulsar «ver». Requisito 9 («avisar, nunca impedir»): esto se devuelve, no se lanza.
/// </remarks>
/// <param name="Gravedad">Cuanto pesa; decide el color, no si la accion sigue.</param>
/// <param name="Linea">El texto de un renglon que se pinta en la franja.</param>
/// <param name="Campo">Que campo lo provoco, para poder senalarlo en su sitio; vacio si es general.</param>
/// <param name="Detalle">Lo largo, que solo aparece al pulsar «ver»; nulo si no hay mas que contar.</param>
public sealed record Aviso(
    GravedadDeAviso Gravedad,
    string Linea,
    string Campo = "",
    string? Detalle = null)
{
    /// <summary>Atajo para el aviso que solo informa de que algo salio bien.</summary>
    /// <param name="linea">Un renglón para la franja; sin saltos de línea.</param>
    /// <param name="campo">El campo al que se refiere, o vacío si es general.</param>
    /// <param name="detalle">Lo largo, para el «ver»; nulo si no hay más que contar.</param>
    public static Aviso Informa(string linea, string campo = "", string? detalle = null)
        => new(GravedadDeAviso.Informacion, linea, campo, detalle);

    /// <summary>Atajo para el aviso de un dato que se guardo igual y queda senalado.</summary>
    /// <param name="linea">Un renglón para la franja; sin saltos de línea.</param>
    /// <param name="campo">El campo al que se refiere, o vacío si es general.</param>
    /// <param name="detalle">Lo largo, para el «ver»; nulo si no hay más que contar.</param>
    public static Aviso Advierte(string linea, string campo = "", string? detalle = null)
        => new(GravedadDeAviso.Advertencia, linea, campo, detalle);

    /// <summary>Atajo para el aviso de algo que no se pudo hacer; sustituye a lanzar.</summary>
    /// <param name="linea">Un renglón para la franja; sin saltos de línea.</param>
    /// <param name="campo">El campo al que se refiere, o vacío si es general.</param>
    /// <param name="detalle">Lo largo, para el «ver»; nulo si no hay más que contar.</param>
    public static Aviso Problema(string linea, string campo = "", string? detalle = null)
        => new(GravedadDeAviso.Problema, linea, campo, detalle);
}
