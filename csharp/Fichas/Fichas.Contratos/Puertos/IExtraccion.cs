using Fichas.Contratos.Lectura;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// De las lineas que devolvio el OCR a los campos que se proponen, con su banda y su confianza.
/// </summary>
/// <remarks>
/// ⛔ Regla permanente 1: aqui se aplican anclas y reglas deterministas, nunca un modelo
/// que adivine. Un campo que no se puede deducir sale con <c>Origen = Vacio</c> y valor
/// nulo; no se inventa. Regla permanente 5: propone, no firma.
/// </remarks>
public interface IExtraccion
{
    /// <summary>Propone los campos del caso a partir de las lineas de OCR de una hoja.</summary>
    ResultadoDeExtraccion ProponerCamposDelCaso(IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones);

    /// <summary>Propone los campos de cada persona a partir de las mismas lineas.</summary>
    ResultadoDeExtraccion ProponerCamposDePersonas(IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones);

    /// <summary>Normaliza un valor leido a la forma que espera la columna; nunca lanza por una forma rara.</summary>
    ResultadoDeExtraccion Normalizar(string campo, string? valorLeido);
}
