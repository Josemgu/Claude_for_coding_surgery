using Fichas.Contratos.Consultas;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App;

/// <summary>
/// El sitio donde va la prueba SIN VENTANA de cada pantalla.
/// </summary>
/// <remarks>
/// Nace con una sola prueba, la que comprueba que el andamio corre. Cada programador
/// anade AQUI la clase de SU pantalla —<c>PruebasDeInicio</c>, <c>PruebasDeCorreccion</c>…—
/// en su propio archivo, y no toca el de nadie.
///
/// Lo que se prueba aqui es la REGLA de la pantalla, no su dibujo: cuantos casos entran en
/// la franja de 7 dias, que un guardado con un campo malo no tumbe el resto, que asignar
/// desde tres sitios deje la misma fila. Para eso la regla tiene que vivir donde se pueda
/// llamar sin abrir ventana; si hay que abrir una ventana para probarla, esta en el sitio
/// equivocado (ADR-0003 §8.1).
/// </remarks>
[TestClass]
public sealed class PruebasDeLasPantallas
{
    /// <summary>El andamio corre y los servicios que veran las pantallas responden.</summary>
    [TestMethod]
    public void ElAndamioDeLasPruebasDePantallaCorre()
    {
        var servicios = new ServiciosFalsos(10, 1, new RelojFijo("2026-09-04"));

        Assert.AreEqual(10, servicios.Casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true }));
    }
}
