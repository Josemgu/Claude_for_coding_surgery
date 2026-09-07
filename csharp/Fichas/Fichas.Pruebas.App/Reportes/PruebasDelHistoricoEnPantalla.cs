using System.Diagnostics;
using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// El historico de la pantalla de Reportes: lo archivado, con la palabra y con su fecha.
/// </summary>
/// <remarks>
/// El criterio del pase es literal: «el histórico enseña lo archivado con la palabra
/// archivado y su fecha». Las dos cosas se comprueban en cada renglon, no en el primero:
/// un caso archivado al que le falte la fecha en la base tiene que salir diciendolo, no
/// callarse ni quedarse fuera.
/// </remarks>
[TestClass]
public sealed class PruebasDelHistoricoEnPantalla
{
    /// <summary>Cuenta exactamente los archivados que hay en la base.</summary>
    [TestMethod]
    public void CuentaLosArchivadosQueHayEnLaBase()
    {
        var servicios = new ServiciosFalsos(300, 7, new RelojFijo("2026-09-04"));
        var conArchivados = servicios.Casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true });
        var sinArchivados = servicios.Casos.Contar(FiltroDeCasos.Todo);

        var historico = ListaDeArchivados.Leer(servicios.Casos);

        Assert.AreEqual(conArchivados - sinArchivados, historico.CuantosHayArchivados);
        Assert.IsGreaterThan(0, historico.CuantosHayArchivados, "La base inventada de 300 casos no trajo ninguno archivado.");
    }

    /// <summary>Cada renglon dice la palabra «archivado» y lleva una fecha detras.</summary>
    [TestMethod]
    public void CadaRenglonDiceArchivadoYSuFecha()
    {
        var servicios = new ServiciosFalsos(300, 7, new RelojFijo("2026-09-04"));

        var historico = ListaDeArchivados.Leer(servicios.Casos);

        Assert.IsNotEmpty(historico.Renglones);
        foreach (var renglon in historico.Renglones)
        {
            Assert.Contains("archivado el ", renglon.Texto, StringComparison.Ordinal,
                $"El renglón «{renglon.Texto}» no dice cuándo se archivó.");
        }
    }

    /// <summary>Un archivado sin fecha en la base sale igual, y dice que le falta.</summary>
    /// <remarks>
    /// Requisito 9, «avisar, nunca impedir». Dejarlo fuera de la lista lo haria invisible
    /// justo en la pantalla que existe para verlo.
    /// </remarks>
    [TestMethod]
    public void UnArchivadoSinFechaSaleDiciendoQueLeFalta()
    {
        var servicios = new ServiciosFalsos(0, 3, new RelojFijo("2026-09-04"));
        servicios.Almacen.Casos[1] = new Caso
        {
            Id = 1,
            NumeroCaso = "CASP2609",
            Archivado = true,
            FechaArchivado = null,
            CreadoEn = "2026-08-01 09:00:00",
        };

        var historico = ListaDeArchivados.Leer(servicios.Casos);

        Assert.HasCount(1, historico.Renglones);
        Assert.Contains("archivado", historico.Renglones[0].Texto, StringComparison.Ordinal);
        Assert.Contains("sin fecha", historico.Renglones[0].Texto, StringComparison.Ordinal);
    }

    /// <summary>Con un tope pequeno se ensenan los del tope y se dice que quedaron fuera.</summary>
    /// <remarks>
    /// Requisito 1: nada se recorta SIN decirlo. La bandera existe para que la pantalla
    /// pueda escribir «se enseñan N de M», que es lo que evita creer que M es N.
    /// </remarks>
    [TestMethod]
    public void ConTopePequenoSeDiceQueQuedaronFuera()
    {
        var servicios = new ServiciosFalsos(300, 7, new RelojFijo("2026-09-04"));

        var historico = ListaDeArchivados.Leer(servicios.Casos, tope: 3);

        Assert.HasCount(3, historico.Renglones);
        Assert.IsTrue(historico.SeQuedaronFuera);
        Assert.IsGreaterThan(3, historico.CuantosHayArchivados);
    }

    /// <summary>Sin ningun archivado, la lista sale vacia y nadie se cae.</summary>
    [TestMethod]
    public void SinNingunArchivadoLaListaSaleVacia()
    {
        var servicios = new ServiciosFalsos(0, 3, new RelojFijo("2026-09-04"));

        var historico = ListaDeArchivados.Leer(servicios.Casos);

        Assert.AreEqual(0, historico.CuantosHayArchivados);
        Assert.IsEmpty(historico.Renglones);
        Assert.IsFalse(historico.SeQuedaronFuera);
    }

    /// <summary>
    /// Con 3 000 documentos el historico se lee en menos de dos segundos. Cifra medida.
    /// </summary>
    /// <remarks>
    /// Requisito 5 del dueno: rapido con 3 000, no con 2. El coste no es gratis y se dice
    /// por que: <c>FiltroDeCasos</c> —que esta CONGELADO— no tiene «solo archivados», asi
    /// que hay que recorrer la lista y quedarse con los que lo estan. El numero que sale
    /// aqui es sobre datos en memoria; sobre SQLite sera otro y no se ha medido.
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosElHistoricoSeLeeRapido()
    {
        var servicios = new ServiciosFalsos(3000, 11, new RelojFijo("2026-09-04"));

        var cronometro = Stopwatch.StartNew();
        var historico = ListaDeArchivados.Leer(servicios.Casos);
        cronometro.Stop();

        Console.WriteLine(
            $"Histórico con 3 000 documentos: {historico.CuantosHayArchivados} archivados, "
            + $"{historico.Renglones.Count} renglones, {cronometro.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.IsLessThan(2000, cronometro.Elapsed.TotalMilliseconds);
    }
}
