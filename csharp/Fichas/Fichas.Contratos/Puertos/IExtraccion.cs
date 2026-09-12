using Fichas.Contratos.Lectura;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// De las lineas que devolvio el OCR a los campos que se proponen, con su banda y su confianza.
/// </summary>
/// <remarks>
/// ⛔ Regla permanente 1: aqui se aplican anclas y reglas deterministas, nunca un modelo
/// que adivine. Un campo que no se puede deducir sale con <c>Origen = Vacio</c> y valor
/// nulo; no se inventa. Regla permanente 5: propone, no firma.
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Lectura.Extraccion</c>, las reglas de verdad
/// portadas del Python, y <c>Fichas.Datos.Falso.ExtraccionFalsa</c>, que solo reconoce tres
/// etiquetas y un MRN por línea. <b>Quién lo consume:</b> la pantalla de Corrección
/// (<c>ModeloDeCorreccion.Documento</c>) por el puerto, y <c>Fichas.Lectura.LectorDeFormularios</c>
/// por la clase concreta. <b>Nada de aquí escribe en la base</b>: lo propuesto viaja en el
/// resultado y lo guarda quien llama.
/// </para>
/// </remarks>
public interface IExtraccion
{
    /// <summary>Propone los campos del caso a partir de las lineas de OCR de una hoja.</summary>
    /// <param name="lineas">Lo que devolvió <see cref="ILecturaDePdf.LeerConOcr"/> para esa hoja; vacía no lanza, deja todos los campos vacíos.</param>
    /// <param name="anotaciones">Las anotaciones del PDF de esa hoja; una anotación manda sobre lo que el OCR lea debajo.</param>
    /// <returns>Un campo propuesto por cada columna del caso que se busca, también los que quedaron con origen vacío y valor nulo; y los avisos que van a la franja.</returns>
    ResultadoDeExtraccion ProponerCamposDelCaso(IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones);

    /// <summary>Propone los campos de cada persona a partir de las mismas lineas.</summary>
    /// <param name="lineas">Las mismas líneas que en <see cref="ProponerCamposDelCaso"/>.</param>
    /// <param name="anotaciones">Las mismas anotaciones.</param>
    /// <returns>Los campos de cada persona, cada uno con su <see cref="CampoPropuesto.FilaFormulario"/> para saber de qué renglón del papel viene; sin ninguna persona reconocida, la lista vacía con un aviso, nunca una persona inventada.</returns>
    ResultadoDeExtraccion ProponerCamposDePersonas(IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones);

    /// <summary>Normaliza un valor leido a la forma que espera la columna; nunca lanza por una forma rara.</summary>
    /// <remarks>Hoy no lo llama nadie en producción (grep del 2026-09-11): lo usan las pruebas. Se documenta y no se toca; ver PENDIENTES.</remarks>
    /// <param name="campo">La columna a la que va, con el nombre de la base (<c>fecha_viaje</c>, <c>mrn</c>).</param>
    /// <param name="valorLeido">Lo leído tal cual; nulo o vacío da un campo con origen vacío.</param>
    /// <returns>Un solo campo propuesto con el valor normalizado, o con valor nulo si no había nada; nunca un valor arreglado a ojo.</returns>
    ResultadoDeExtraccion Normalizar(string campo, string? valorLeido);
}
