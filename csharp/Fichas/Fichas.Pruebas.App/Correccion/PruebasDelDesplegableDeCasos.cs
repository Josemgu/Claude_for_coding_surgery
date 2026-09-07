using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Que dos casos del mismo mes se distingan en el desplegable SIN abrirlos.
/// </summary>
/// <remarks>
/// Lo que QA midio sobre el paquete publicado el 2026-09-04: el desplegable ofrecia 18
/// entradas y catorce se llamaban igual, «CASP2609». El numero de caso no distingue desde
/// la migracion 12 —identifica una unidad y un mes, no un formulario—, asi que elegir
/// entre ellas era elegir a ciegas.
/// </remarks>
[TestClass]
public sealed class PruebasDelDesplegableDeCasos
{
    private static Caso Caso(long id, string? numero, string archivo, long? duplicadoDe = null) => new()
    {
        Id = id,
        NumeroCaso = numero,
        DuplicadoDe = duplicadoDe,
        FechaViaje = "2026-09-08",
        RutaPdf = $@"C:\Users\josem\Documents\Fichas\pdf\{archivo}",
        CreadoEn = "2026-09-01",
    };

    /// <summary>
    /// Dados dos casos con el MISMO numero, entonces sus dos lineas son distintas.
    /// </summary>
    /// <remarks>Es el criterio de cierre del defecto, tal cual.</remarks>
    [TestMethod]
    public void DosCasosConElMismoNumeroSeDistinguen()
    {
        var uno = TextoDelDesplegable.Componer(
            Caso(3, "CASP2609", "CASP2609_Ana_Prueba.pdf"), "Ana Prueba", 1, null);
        var otro = TextoDelDesplegable.Componer(
            Caso(4, "CASP2609", "CASP2609_Linda_Simulado.pdf"), "Linda Simulado", 1, null);

        Assert.AreNotEqual(uno, otro, "Dos casos del mismo mes tienen que leerse distinto.");
        StringAssert.Contains(uno, "Ana Prueba", StringComparison.Ordinal);
        StringAssert.Contains(otro, "Linda Simulado", StringComparison.Ordinal);
    }

    /// <summary>Aun sin nombre leido, el archivo sigue separando las dos entradas.</summary>
    /// <remarks>
    /// Es el caso peor y el que mas importa: cuando el escaneo no dejo leer el nombre es
    /// justo cuando hay que entrar a teclearlo, y hay que saber en cual se entra.
    /// </remarks>
    [TestMethod]
    public void SinNombreLeidoElArchivoSigueSeparandolos()
    {
        var uno = TextoDelDesplegable.Componer(
            Caso(3, "CASP2609", "CASP2609_Ana_Prueba.pdf"), null, 1, null);
        var otro = TextoDelDesplegable.Componer(
            Caso(4, "CASP2609", "CASP2609_Linda_Simulado.pdf"), "   ", 1, null);

        Assert.AreNotEqual(uno, otro);
        StringAssert.Contains(uno, "CASP2609_Ana_Prueba.pdf", StringComparison.Ordinal);
        StringAssert.Contains(uno, "sin nombre leído", StringComparison.Ordinal);
    }

    /// <summary>Un duplicado dice que lo es Y de cual, por el archivo del original.</summary>
    [TestMethod]
    public void UnDuplicadoDiceQueLoEsYDeCual()
    {
        var original = Caso(10, "SURB2609", "cb2f18be-SURB2609_Suriname_Group_Complete.pdf");
        var copia = Caso(11, "SURB2609", "ed633b94-SURB2609_Suriname_Group_Complete.pdf", duplicadoDe: 10);

        var linea = TextoDelDesplegable.Componer(copia, "Marcia Sandel", 10, original);

        StringAssert.Contains(linea, "DUPLICADO", StringComparison.Ordinal);
        StringAssert.Contains(linea, "cb2f18be-SURB2609_Suriname_Group_Complete.pdf", StringComparison.Ordinal);
        Assert.AreNotEqual(
            TextoDelDesplegable.Componer(original, "Marcia Sandel", 10, null),
            linea,
            "El duplicado y su original no pueden leerse igual.");
    }

    /// <summary>Si el original ya no esta, se dice que es duplicado igual y con su numero.</summary>
    /// <remarks>Callar que es duplicado por no encontrar al otro seria esconder lo que importa.</remarks>
    [TestMethod]
    public void UnDuplicadoSinOriginalSigueDiciendoQueLoEs()
    {
        var linea = TextoDelDesplegable.Componer(
            Caso(11, "SURB2609", "otro.pdf", duplicadoDe: 10), "Marcia Sandel", 10, null);

        StringAssert.Contains(linea, "DUPLICADO", StringComparison.Ordinal);
        StringAssert.Contains(linea, "10", StringComparison.Ordinal);
    }

    /// <summary>El de grupo dice cuantos van dentro; el de una persona no dice «y 0 mas».</summary>
    [TestMethod]
    public void ElDeGrupoDiceCuantosVanDentro()
    {
        var grupo = TextoDelDesplegable.Componer(
            Caso(10, "SURB2609", "grupo.pdf"), "Marcia Sandel", 10, null);
        var solo = TextoDelDesplegable.Componer(
            Caso(3, "CASP2609", "una.pdf"), "Ana Prueba", 1, null);

        StringAssert.Contains(grupo, "y 9 más", StringComparison.Ordinal);
        Assert.DoesNotContain("más", solo, "Con una sola persona no hay «y N más» que decir.");
    }

    /// <summary>Un caso sin numero se distingue por su numero interno, que es lo unico que hay.</summary>
    [TestMethod]
    public void UnCasoSinNumeroSeDistinguePorSuNumeroInterno()
    {
        var uno = TextoDelDesplegable.Componer(Caso(1, null, "a.pdf"), null, 0, null);
        var otro = TextoDelDesplegable.Componer(Caso(2, null, "b.pdf"), null, 0, null);

        StringAssert.Contains(uno, "sin número (1)", StringComparison.Ordinal);
        StringAssert.Contains(otro, "sin número (2)", StringComparison.Ordinal);
        Assert.AreNotEqual(uno, otro);
    }

    /// <summary>Un caso sin ninguna persona lo dice, en vez de callarlo.</summary>
    [TestMethod]
    public void UnCasoSinPersonasLoDice()
        => StringAssert.Contains(
            TextoDelDesplegable.Componer(Caso(1, "CASP2609", "a.pdf"), null, 0, null),
            "sin ninguna persona",
            StringComparison.Ordinal);

    /// <summary>Un caso sin PDF no tumba nada: se lee sin la parte del archivo.</summary>
    /// <remarks>Requisito 9: avisar, nunca impedir. Y menos por una ruta que falta.</remarks>
    [TestMethod]
    public void UnCasoSinArchivoNoTumbaLaLinea()
    {
        var caso = new Caso { Id = 7, NumeroCaso = "CASP2609", CreadoEn = "2026-09-01" };
        var linea = TextoDelDesplegable.Componer(caso, "Ana Prueba", 1, null);

        StringAssert.Contains(linea, "CASP2609", StringComparison.Ordinal);
        StringAssert.Contains(linea, "Ana Prueba", StringComparison.Ordinal);
    }

    /// <summary>Ninguna linea pasa del tope: una entrada que no cabe no se puede elegir.</summary>
    [TestMethod]
    public void NingunaLineaPasaDelTope()
    {
        var largo = new string('X', 200);
        var caso = Caso(11, "SURB2609", largo + ".pdf", duplicadoDe: 10);
        var linea = TextoDelDesplegable.Componer(caso, largo, 10, Caso(10, "SURB2609", largo + ".pdf"));

        Assert.IsLessThanOrEqualTo(TextoDelDesplegable.LargoMaximoDeLaLinea, linea.Length);
        StringAssert.Contains(linea, "SURB2609", StringComparison.Ordinal);
    }
}
