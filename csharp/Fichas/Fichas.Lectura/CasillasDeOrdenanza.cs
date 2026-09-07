namespace Fichas.Lectura;

/// <summary>
/// Las seis casillas de ordenanzas, y por que esta fase las entrega SIN leer.
/// </summary>
/// <remarks>
/// Portado de `extraccion/casillas.py`, con su estado intacto. Las casillas son marcas
/// dibujadas a mano, no texto: el OCR no las lee de forma fiable. El metodo previsto es
/// recortar cada celda, binarizarla, contar el pixel oscuro sobre el area total y
/// comparar contra un umbral. Ese umbral hay que CALIBRARLO sobre formularios reales;
/// elegirlo a ojo es exactamente lo que la fase prohibe.
///
/// <para>⛔ <b>Esto NO entra en la fase de lectura.</b> Es la FASE C3b y esta bloqueada
/// por su criterio C3b-0: la verdad conocida de las 42 casillas (7 documentos x 6),
/// anotada por una persona. Hoy hay <b>0</b> formularios con verdad conocida y hacen
/// falta <b>3</b>. Lo da el dueno; no es trabajo de programacion.</para>
///
/// <para>El OCR detecta algunas marcas como caracteres sueltos —en los siete escaneos
/// aparece una «V» con confianza 0,53 justo en la columna de ordenanzas—, pero eso no es
/// una verdad conocida: es otra lectura automatica, y calibrar un detector contra otro
/// detector no mide nada.</para>
///
/// <para>Para reactivarla hace falta, y en este orden: (1) tres o mas formularios con la
/// verdad de sus casillas anotada por una persona; (2) la medicion del pixel oscuro de
/// las marcadas y de las vacias; (3) el umbral escrito aqui con ese numero y con cuantos
/// formularios lo calibraron. Y ⚠️ el umbral se calibra sobre la imagen que produce el
/// rasterizador: si el rasterizador cambia, hay que recalibrarlo.</para>
/// </remarks>
public static class CasillasDeOrdenanza
{
    /// <summary>
    /// Las seis columnas en el orden literal de `DECISIONES.md`, que es el mismo que el
    /// de las columnas <c>ord_*</c> de la tabla <c>personas</c>.
    /// </summary>
    public static IReadOnlyList<string> Nombres { get; } =
    [
        "ord_recibir_propias",
        "ord_observar_sellamiento",
        "ord_traductor",
        "ord_investidura",
        "ord_sellamiento_esposos",
        "ord_sellamiento_hijo_padres",
    ];

    /// <summary>Cuantos formularios con verdad conocida exige el criterio de la fase C3b.</summary>
    public const int FormulariosMinimosParaCalibrar = 3;

    /// <summary>Cuantos hay hoy. Cuando llegue a tres, se puede calibrar y no antes.</summary>
    public const int FormulariosConVerdadConocida = 0;

    /// <summary>Si la lectura automatica esta activa. Se deduce, no se pone a mano.</summary>
    public static bool LecturaActiva => FormulariosConVerdadConocida >= FormulariosMinimosParaCalibrar;

    /// <summary>
    /// El umbral de pixel oscuro. Sigue nulo a proposito.
    /// </summary>
    /// <remarks>
    /// Mientras no haya con que calibrarlo, cualquier numero aqui seria inventado. Un
    /// nulo no se puede usar por accidente; un 0,15 puesto «de momento», si.
    /// </remarks>
    public static double? UmbralDePixelOscuro => null;

    /// <summary>
    /// Las seis casillas sin leer: nulo en cada una.
    /// </summary>
    /// <remarks>
    /// ⛔ Nulo no es lo mismo que 0 en este esquema, y la diferencia importa. 0 significa
    /// «se leyo y no estaba marcada»; nulo significa «no se leyo». Si se devolvieran
    /// ceros, Miguel veria seis ordenanzas negativas en firme y no tendria motivo para
    /// mirar el papel. Es el control negativo obligatorio del criterio C3b-4.
    /// </remarks>
    public static IReadOnlyDictionary<string, bool?> NoLeidas()
        => Nombres.ToDictionary(nombre => nombre, _ => (bool?)null);
}
