using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Un documento que repite a otro tiene que decirlo EN LA TARJETA, y decir de cual.
/// </summary>
/// <remarks>
/// <para>Nace de las palabras del dueno del 2026-09-03 —«si hay documentos duplicados debe
/// decirlo y no rechazarlo»— y de un defecto MEDIDO por QA sobre el paquete publicado el
/// 2026-09-04: importados siete escaneos reales y vueltos a importar, el resumen de la
/// importacion dijo «7 duplicados avisados» y la base guardo <c>casos.duplicado_de</c>
/// apuntando al original, pero en Revisar aparecian tarjetas con el mismo nombre de archivo
/// repetido SIN NINGUNA marca. El aviso moria en el resumen de la tanda y no llegaba a donde
/// Miguel trabaja.</para>
///
/// <para>«No rechazarlo» ya se cumplia; «debe decirlo» no. Las dos mitades de la frase del
/// dueno se comprueban aqui: el duplicado entra —sigue en el tablero, con su tarjeta— y
/// ademas se distingue del original sin abrir nada.</para>
///
/// <para>⚠️ Lo que se ensena es el ARCHIVO y la hoja del original, no solo su numero de caso.
/// Medido en <c>BuscadorDeDuplicados</c>: el dueno tiene siete documentos con el mismo
/// <c>CASP2609</c>, de siete familias distintas, asi que el numero de caso NO distingue cual
/// es el original. El archivo y la hoja si.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelDuplicadoEnRevisar
{
    /// <summary>La tarjeta del duplicado dice que lo es y nombra el archivo y la hoja del original.</summary>
    [TestMethod]
    public void LaTarjetaDelDuplicadoDiceDeCualEsDuplicado()
    {
        var banco = new BaseDePrueba();
        var original = MeterHoja(banco, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 1, null);
        var repetido = MeterHoja(banco, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 1, original);

        banco.Tablero.Cargar();

        var tarjeta = banco.Tablero.De(repetido);
        Assert.IsNotNull(tarjeta, "El duplicado NO se rechaza: tiene que seguir teniendo su tarjeta.");
        Assert.IsTrue(tarjeta.EsDuplicado);
        Assert.Contains(
            "CASP2609_Ana_Prueba.pdf",
            tarjeta.MarcaDeDuplicado,
            "La marca tiene que nombrar el ARCHIVO del original: el número de caso no distingue, "
            + $"porque los siete escaneos comparten CASP2609. Marca: «{tarjeta.MarcaDeDuplicado}»");
        Assert.Contains("hoja 1", tarjeta.MarcaDeDuplicado, "Y la hoja, que es lo que lo hace único.");
    }

    /// <summary>El original no lleva marca: la marca es un hecho de uno de los dos, no de los dos.</summary>
    [TestMethod]
    public void ElOriginalNoSeMarcaComoDuplicadoDeNadie()
    {
        var banco = new BaseDePrueba();
        var original = MeterHoja(banco, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 1, null);
        MeterHoja(banco, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 1, original);

        banco.Tablero.Cargar();

        var tarjeta = banco.Tablero.De(original);
        Assert.IsNotNull(tarjeta);
        Assert.IsFalse(tarjeta.EsDuplicado, "El original no repite a nadie.");
        Assert.IsEmpty(tarjeta.MarcaDeDuplicado, "Y por tanto no lleva marca ninguna.");
    }

    /// <summary>
    /// Importados dos veces los mismos siete, se distinguen los siete duplicados de los siete
    /// originales. Es la medicion de QA, puesta donde no se pueda deshacer sin enterarse.
    /// </summary>
    [TestMethod]
    public void LosSieteDeLaSegundaImportacionSeDistinguenDeLosSietePrimeros()
    {
        var banco = new BaseDePrueba();
        var nombres = new[] { "Ana_Prueba", "Linda_Simulado", "Jonas_Ficticio", "Daniel_Jr", "Jorge_Supuesto", "Elena_Rosa_Muestra", "Julia_Luz_Inventada" };

        var primeros = nombres.Select(n => MeterHoja(banco, "CASP2609", $@"C:\escaneos\CASP2609_{n}.pdf", 1, null)).ToList();
        var segundos = nombres
            .Select((n, i) => MeterHoja(banco, "CASP2609", $@"C:\escaneos\CASP2609_{n}.pdf", 1, primeros[i]))
            .ToList();

        banco.Tablero.Cargar();

        Assert.AreEqual(14, banco.Tablero.Total, "Ninguno se rechaza: catorce casos en el tablero.");

        var marcados = banco.Tablero.Cargadas.Where(t => t.EsDuplicado).ToList();
        Assert.HasCount(7, marcados, "Siete y solo siete llevan marca de duplicado.");

        // Y cada uno nombra el archivo del SUYO, no el de otro cualquiera de los siete.
        for (var i = 0; i < nombres.Length; i++)
        {
            var tarjeta = banco.Tablero.De(segundos[i])!;
            Assert.Contains(
                $"CASP2609_{nombres[i]}.pdf",
                tarjeta.MarcaDeDuplicado,
                $"El duplicado de «{nombres[i]}» tiene que apuntar a SU original, no a otro. "
                + $"Marca: «{tarjeta.MarcaDeDuplicado}»");
        }
    }

    /// <summary>
    /// Si el original no esta a la vista, la marca lo dice en vez de callarse.
    /// </summary>
    /// <remarks>
    /// Pasa de verdad: al escribir en el buscador, el tablero se recarga con el filtro puesto
    /// y el original puede quedar fuera. Una marca que desapareciera en ese caso seria peor que
    /// no tenerla, porque solo fallaria cuando Miguel esta buscando algo.
    /// </remarks>
    [TestMethod]
    public void SiElOriginalNoEstaCargadoLaMarcaLoDiceYNoDesaparece()
    {
        var banco = new BaseDePrueba();
        var original = MeterHoja(banco, "AAAA1111", @"C:\escaneos\primero.pdf", 1, null);
        MeterHoja(banco, "BBBB2222", @"C:\escaneos\segundo.pdf", 1, original);

        // El buscador deja fuera al original: solo entra el duplicado.
        banco.Tablero.Cargar("BBBB2222");

        Assert.AreEqual(1, banco.Tablero.Total, "Solo el duplicado pasa el filtro del buscador.");
        var tarjeta = banco.Tablero.Cargadas[0];
        Assert.IsTrue(tarjeta.EsDuplicado);
        Assert.IsNotEmpty(
            tarjeta.MarcaDeDuplicado,
            "Con el original fuera del filtro la marca tiene que seguir diciendo algo.");
        Assert.Contains("primero.pdf", tarjeta.MarcaDeDuplicado, "Y seguir nombrando al original.");
    }

    /// <summary>Mete una hoja con su archivo, su pagina y, si lo repite, el caso del que es duplicado.</summary>
    /// <param name="banco">La base de prueba donde se mete.</param>
    /// <param name="numero">El número del papel.</param>
    /// <param name="rutaPdf">El archivo del que sale la hoja.</param>
    /// <param name="pagina">Qué página de ese archivo.</param>
    /// <param name="duplicadoDe">El caso del que es copia, o nulo si es el original.</param>
    /// <returns>El id del caso.</returns>
    private static long MeterHoja(BaseDePrueba banco, string numero, string rutaPdf, int pagina, long? duplicadoDe)
        => banco.Servicios.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            RutaPdf = rutaPdf,
            PaginaPdf = pagina,
            DuplicadoDe = duplicadoDe,
            UnidadNombre = "Rama de prueba",
            FechaViaje = banco.FechaEn(30),
            CreadoEn = banco.Reloj.Ahora(),
        }).Id;
}
