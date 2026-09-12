using Fichas.App.Correccion;
using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Que Correccion se trabaje POR GRUPO y sin tope, que es lo que el dueno pidio el
/// 2026-09-07.
/// </summary>
/// <remarks>
/// <para>Sus palabras: <i>«En corrección, permite trabajar por grupo, no todos juntos. Eso es
/// súper incómodo. El día que tenga 800 solicitudes tendré problemas»</i>, y después: <i>«Esto
/// no me funciona para nada, no le veo función. Prefiero trabajar y corregir por grupo que
/// todos juntos»</i>.</para>
///
/// <para>Lo que habia hasta hoy, medido por el supervisor:
/// <c>PaginaDeCorreccion.xaml.cs:27</c> ofrecia <c>CuantosCasosSeOfrecen = 50</c> casos en un
/// desplegable plano, sin agrupar y sin ninguna forma de llegar al 51.</para>
///
/// <para>⛔ <b>El agrupado NO se inventa aqui.</b> Es el mismo arbol de Revisar
/// —<see cref="ArbolDeRevisar"/>— y esa es la mitad que importa: si Correccion agrupara por su
/// cuenta, el «grupo del 17 de septiembre» de una pantalla podria no ser el de la otra.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosGruposDeCorreccion
{
    /// <summary>Una tarjeta como las que arma Revisar, con lo justo para agrupar.</summary>
    private static TarjetaDeDocumento Tarjeta(
        long id, string numero, string fechaIso, string unidadNumero, string unidad, int personas = 2)
        => new()
        {
            CasoId = id,
            NumeroDeCaso = numero,
            TieneNumeroDeCaso = true,
            Archivo = $"{numero}_{id}.pdf",
            Personas = personas,
            FechaDeViajeIso = fechaIso,
            UnidadNumero = unidadNumero,
            Unidad = unidad.Length == 0 ? TarjetaDeDocumento.SinUnidad : unidad,
        };

    /// <summary>Vigila que un grupo es (fecha, unidad) y no (fecha): dos unidades del mismo dia son dos grupos, ordenados por numero de unidad.</summary>
    [TestMethod]
    public void CadaFechaYCadaUnidadEsUnGrupo()
    {
        var grupos = GruposParaCorregir.Armar(
        [
            Tarjeta(1, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
            Tarjeta(2, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
            Tarjeta(3, "CASP2609", "2026-09-17", "212233", "Vieux Fort Ward"),
            Tarjeta(4, "SURB2609", "2026-10-02", "700001", "Castries Branch"),
        ]);

        Assert.HasCount(3, grupos, "tres pares de (fecha, unidad) son tres grupos");

        // Dentro de un mismo dia las unidades van por su NUMERO, que es como las ordena el
        // arbol de Revisar: 212233 antes que 700001. Se busca por su etiqueta y no por el
        // puesto, para que la prueba diga de que grupo habla.
        Assert.AreEqual(2, DeLaUnidad(grupos, "700001").CuantosDocumentos);
        Assert.AreEqual(1, DeLaUnidad(grupos, "212233").CuantosDocumentos);
        Assert.HasCount(1, grupos.Where(g => g.FechaIso == "2026-10-02").ToList());
    }

    /// <summary>El grupo cuya etiqueta nombra esa unidad; falla la prueba si no hay ninguno.</summary>
    private static GrupoParaCorregir DeLaUnidad(IReadOnlyList<GrupoParaCorregir> grupos, string unidadNumero)
        => grupos.FirstOrDefault(g => g.CarpetaDeLaUnidad.Contains(unidadNumero, StringComparison.Ordinal))
           ?? throw new AssertFailedException($"no hay ningún grupo de la unidad {unidadNumero}");

    /// <summary>Vigila que repartir 137 documentos en grupos no pierde ni uno: la suma de los grupos es el total.</summary>
    [TestMethod]
    public void NingunDocumentoSeQuedaFueraDeUnGrupo()
    {
        var tarjetas = Enumerable.Range(1, 137)
            .Select(i => Tarjeta(i, "CASP2609", $"2026-09-{(i % 20) + 1:00}", $"70001{i % 4}", "Castries Branch"))
            .ToList();

        var grupos = GruposParaCorregir.Armar(tarjetas);

        var repartidos = grupos.SelectMany(g => g.Documentos).Select(d => d.CasoId).ToHashSet();
        Assert.HasCount(137, repartidos, "el reparto en grupos no puede perder ni un documento");
        Assert.AreEqual(137, grupos.Sum(g => g.CuantosDocumentos));
    }

    /// <summary>El defecto entero: un tope de 50 dejaba el documento 51 sin forma de abrirse.</summary>
    [TestMethod]
    public void UnGrupoDeSesentaDocumentosOfreceLosSesenta()
    {
        var tarjetas = Enumerable.Range(1, 60)
            .Select(i => Tarjeta(i, "CASP2609", "2026-09-17", "700001", "Castries Branch"))
            .ToList();

        var grupos = GruposParaCorregir.Armar(tarjetas);

        Assert.HasCount(1, grupos);
        Assert.AreEqual(60, grupos[0].CuantosDocumentos);
        Assert.IsTrue(
            grupos[0].Documentos.Any(d => d.CasoId == 51),
            "el documento que hace 51 tiene que estar entre los que se ofrecen");
    }

    /// <summary>Lo que viaja antes, arriba; lo que no tiene fecha, al final (regla de Revisar).</summary>
    [TestMethod]
    public void LoQueViajaAntesVaArribaYLoSinFechaAlFinal()
    {
        var grupos = GruposParaCorregir.Armar(
        [
            Tarjeta(1, "CASP2611", "2026-11-04", "700001", "Castries Branch"),
            Tarjeta(2, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
            Tarjeta(3, "CASP0000", string.Empty, "700001", "Castries Branch"),
            Tarjeta(4, "CASP2610", "2026-10-02", "700001", "Castries Branch"),
        ]);

        CollectionAssert.AreEqual(
            new[] { "2026-09-17", "2026-10-02", "2026-11-04", string.Empty },
            grupos.Select(g => g.FechaIso).ToArray());
        Assert.IsTrue(grupos[^1].HayQueRevisarlo, "el grupo sin fecha es el que hay que mirar");
    }

    /// <summary>La seccion sin fecha PIDE algo, con las palabras del dueno.</summary>
    [TestMethod]
    public void ElGrupoSinFechaLoDiceConLasPalabrasDelDueno()
    {
        var grupos = GruposParaCorregir.Armar([Tarjeta(1, "CASP0000", string.Empty, string.Empty, string.Empty)]);

        StringAssert.Contains(grupos[0].Etiqueta, ArbolDeRevisar.LlamadaARevisar);
        StringAssert.Contains(grupos[0].Etiqueta, ArbolDeRevisar.SinFecha);
    }

    /// <summary>Una cifra sin su denominador no se puede comprobar (CLAUDE.md §8).</summary>
    [TestMethod]
    public void LaEtiquetaDeUnGrupoDiceCuantosDocumentosTrae()
    {
        var grupos = GruposParaCorregir.Armar(
        [
            Tarjeta(1, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
            Tarjeta(2, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
        ]);

        StringAssert.Contains(grupos[0].Etiqueta, "Grupo del 17 de septiembre");
        StringAssert.Contains(grupos[0].Etiqueta, "700001");
        StringAssert.Contains(grupos[0].Etiqueta, "Castries Branch");
        StringAssert.Contains(grupos[0].Etiqueta, "2 documentos");
    }

    /// <summary>Sin documentos no hay ningun grupo, y no revienta.</summary>
    [TestMethod]
    public void SinDocumentosNoHayNingunGrupo()
        => Assert.IsEmpty(GruposParaCorregir.Armar([]));

    /// <summary>El grupo al que pertenece un documento, para volver a el sin buscarlo a mano.</summary>
    [TestMethod]
    public void SeSabeEnQueGrupoEstaCadaDocumento()
    {
        var grupos = GruposParaCorregir.Armar(
        [
            Tarjeta(1, "CASP2609", "2026-09-17", "700001", "Castries Branch"),
            Tarjeta(2, "SURB2610", "2026-10-02", "212233", "Vieux Fort Ward"),
        ]);

        Assert.AreEqual(0, GruposParaCorregir.DondeEsta(grupos, 1));
        Assert.AreEqual(1, GruposParaCorregir.DondeEsta(grupos, 2));
        Assert.AreEqual(-1, GruposParaCorregir.DondeEsta(grupos, 99), "un caso que no esta no se inventa");
    }
}
