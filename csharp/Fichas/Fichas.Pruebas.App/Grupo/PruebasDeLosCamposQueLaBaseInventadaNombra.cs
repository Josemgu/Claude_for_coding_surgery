using Fichas.App.Correccion;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// La base inventada le pone procedencia a los MISMOS campos que la pantalla dibuja.
/// </summary>
/// <remarks>
/// <para><b>Existe por un defecto que ya pasó una vez.</b> La importación llevaba su lista de
/// campos y la pantalla de Corrección la suya, se separaron sin que nadie lo viera, y
/// <c>templo_nombre</c> acabó dibujado en la pantalla y sin fila de procedencia en la base
/// —0 de 7 casos, medido sobre los siete escaneos reales—. Un campo sin fila no se puede
/// firmar.</para>
///
/// <para><b>Ahora hay una tercera lista</b> —la de <see cref="ProcedenciaInventada"/>—, y no
/// puede importar las otras dos: <c>Fichas.Datos.Falso</c> solo referencia
/// <c>Fichas.Contratos</c>, que es lo que le permite sustituirse por <c>Fichas.Datos</c>
/// cambiando una línea. Esta prueba es el precio de esa independencia, y se pone roja el
/// mismo día que las dos listas se separen.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosCamposQueLaBaseInventadaNombra
{
    /// <summary>Los cinco campos del caso son los mismos, y en el mismo orden.</summary>
    [TestMethod]
    public void LaBaseInventadaYLaPantallaNombranLosMismosCamposDelCaso()
        => Assert.AreEqual(
            string.Join(", ", ModeloDeCorreccion.CamposDelCasoQueSeDibujan),
            string.Join(", ", ProcedenciaInventada.CamposDelCaso),
            "La base inventada y la pantalla de Corrección dejaron de hablar de los mismos campos del caso.");

    /// <summary>Y los dos campos de texto de cada persona, también.</summary>
    /// <remarks>
    /// El orden se compara ordenado y no como viene, porque en las personas ninguna de las dos
    /// listas promete un orden: lo que importa es que sean el mismo conjunto.
    /// </remarks>
    [TestMethod]
    public void LaBaseInventadaYLaPantallaNombranLosMismosCamposDeLaPersona()
        => Assert.AreEqual(
            string.Join(", ", ModeloDeCorreccion.CamposDeLaPersonaQueSeDibujan.Order(StringComparer.Ordinal)),
            string.Join(", ", ProcedenciaInventada.CamposDeLaPersona.Order(StringComparer.Ordinal)),
            "La base inventada y la pantalla de Corrección dejaron de hablar de los mismos campos de la persona.");
}
