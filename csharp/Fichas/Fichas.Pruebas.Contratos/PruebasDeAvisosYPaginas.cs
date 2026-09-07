using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.Contratos;

/// <summary>
/// Los avisos de una linea y los trozos de lista.
/// </summary>
/// <remarks>
/// Criterio del que salen: requisito 4 del dueno («ni un parrafo en pantalla») y
/// requisito 5 («rapido con 3 000 documentos»), que es lo que obliga a que toda lectura
/// se pida por trozos.
/// </remarks>
[TestClass]
public sealed class PruebasDeAvisosYPaginas
{
    /// <summary>Un aviso lleva su linea corta y lo largo aparte.</summary>
    [TestMethod]
    public void UnAvisoSeparaLaLineaDelDetalle()
    {
        var aviso = Aviso.Advierte("El numero de caso no tiene la forma esperada.", "NumeroCaso", "Aqui va lo largo.");

        Assert.AreEqual(GravedadDeAviso.Advertencia, aviso.Gravedad);
        Assert.AreEqual("NumeroCaso", aviso.Campo);
        Assert.AreEqual("Aqui va lo largo.", aviso.Detalle);
        Assert.DoesNotContain("\n", aviso.Linea, "La linea de la franja es una sola linea.");
    }

    /// <summary>Un aviso sin detalle no ensena el boton de «ver».</summary>
    [TestMethod]
    public void UnAvisoPuedeNoTenerDetalle()
    {
        var aviso = Aviso.Informa("Guardado.");

        Assert.IsNull(aviso.Detalle);
        Assert.AreEqual(string.Empty, aviso.Campo);
    }

    /// <summary>Una escritura que sale bien devuelve el id y ningun aviso.</summary>
    [TestMethod]
    public void UnaEscrituraQueSaleBienNoDiceNada()
    {
        var resultado = ResultadoDeEscritura.Bien(7);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual(7, resultado.Id);
        Assert.IsFalse(resultado.HayAvisos);
    }

    /// <summary>Una escritura que no se pudo hacer lo dice devolviendo, no lanzando.</summary>
    [TestMethod]
    public void UnaEscrituraQueNoSePudoHacerLoDiceSinLanzar()
    {
        var resultado = ResultadoDeEscritura.NoSeEscribio(Aviso.Problema("No hay ningun caso con ese numero."));

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, resultado.Id);
        Assert.HasCount(1, resultado.Avisos);
    }

    /// <summary>Un trozo del medio trae lo suyo y dice cuantos hay detras.</summary>
    [TestMethod]
    public void UnTrozoDelMedioSabeCuantosHayDetras()
    {
        var pagina = new PaginaDe<int>([4, 5, 6], new Pagina(3, 3), 10);

        Assert.HasCount(3, pagina.Elementos);
        Assert.AreEqual(10, pagina.TotalDisponible);
        Assert.IsTrue(pagina.HayMas);
    }

    /// <summary>El ultimo trozo dice que ya no queda nada detras.</summary>
    [TestMethod]
    public void ElUltimoTrozoDiceQueNoQuedaNada()
    {
        var pagina = new PaginaDe<int>([9, 10], new Pagina(8, 3), 10);

        Assert.IsFalse(pagina.HayMas);
    }

    /// <summary>El trozo siguiente empieza donde acabo el anterior.</summary>
    [TestMethod]
    public void ElTrozoSiguienteEmpiezaDondeAcaboElAnterior()
    {
        var primera = Pagina.Primera(50);
        var segunda = primera.Siguiente();

        Assert.AreEqual(0, primera.Desde);
        Assert.AreEqual(50, segunda.Desde);
        Assert.AreEqual(50, segunda.Tamano);
    }

    /// <summary>Un filtro sin nada puesto no filtra ni esconde archivados por accidente.</summary>
    [TestMethod]
    public void ElFiltroVacioNoPideNadaRaro()
    {
        var filtro = FiltroDeCasos.Todo;

        Assert.IsFalse(filtro.SoloDeHoy);
        Assert.IsNull(filtro.VentanaDeDias);
        Assert.IsNull(filtro.CompaneroId);
        Assert.IsFalse(filtro.IncluirArchivados, "Los archivados salen del trabajo del dia salvo que se pidan.");
    }
}
