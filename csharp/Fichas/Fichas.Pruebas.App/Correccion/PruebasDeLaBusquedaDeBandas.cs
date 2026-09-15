using Fichas.App.Correccion;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// La búsqueda de bandas en el documento: una sola lectura en vuelo, la última petición
/// gana, y lo que se leyó para un caso que ya no está delante se descarta y se dice.
/// </summary>
/// <remarks>
/// <para>Medido en el cuaderno del dueño el 2026-09-15 (su máquina, sesión 11:03–11:29): 54
/// aperturas, 25 lecturas de bandas terminadas y 29 sin fin registrado; el OCR de bandas
/// tardaba 11–18 s suelto y 27–58 s con varios en vuelo (hasta 5 a la vez). Cada apertura
/// lanzaba su OCR sin cancelar el anterior, y nada impedía que el resultado de un caso
/// viejo llegara cuando otro estaba delante.</para>
///
/// <para>Estas pruebas se escribieron en rojo. Lo que fijan: que dos peticiones seguidas no
/// corren a la vez, que la petición que quedó en espera se cancela si antes se abre otro
/// caso, y que lo leído para una generación que ya no es la vigente no vuelve.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaBusquedaDeBandas
{
    /// <summary>Una petición de mentira para el archivo y la hoja que se digan.</summary>
    /// <param name="ruta">El archivo.</param>
    private static PeticionDeBandas Peticion(string ruta)
        => new(ruta, [new CampoSinBanda("casos/numero_caso", TablaDeProcedencia.Casos, "numero_caso", 1, null)]);

    /// <summary>Unas bandas de mentira que dicen de qué archivo salieron.</summary>
    /// <param name="ruta">El archivo.</param>
    private static BandasLeidas BandasDe(string ruta)
        => new(new Dictionary<string, BandaDeLaPagina> { [ruta] = new(0.1, 0.1, 0.2, 0.2) }, []);

    /// <summary>
    /// Dada una lectura en vuelo; cuando llegan dos peticiones más de dos casos nuevos;
    /// entonces solo la última llega a leerse, la de en medio se cancela sin leer, y la
    /// primera —ya vieja— se descarta al terminar. Una lectura a la vez, nunca cinco.
    /// </summary>
    [TestMethod]
    public async Task UnaLecturaEnVueloYLaUltimaPeticionGana()
    {
        var leidas = new List<string>();
        var primera = new TaskCompletionSource<BandasLeidas>();
        var cuaderno = new List<string>();
        var busqueda = new BusquedaDeBandas(
            peticion =>
            {
                leidas.Add(peticion.RutaPdf);
                return peticion.RutaPdf == "a.pdf" ? primera.Task : Task.FromResult(BandasDe(peticion.RutaPdf));
            },
            cuaderno.Add, new SemaphoreSlim(1, 1));

        var generacionA = busqueda.AbrirOtroCaso();
        var tareaA = busqueda.Buscar(generacionA, Peticion("a.pdf"));
        var generacionB = busqueda.AbrirOtroCaso();
        var tareaB = busqueda.Buscar(generacionB, Peticion("b.pdf"));
        var generacionC = busqueda.AbrirOtroCaso();
        var tareaC = busqueda.Buscar(generacionC, Peticion("c.pdf"));

        Assert.HasCount(1, leidas, "mientras «a» está en vuelo no arranca ninguna otra lectura.");
        primera.SetResult(BandasDe("a.pdf"));
        var resultados = await Task.WhenAll(tareaA, tareaB, tareaC);

        CollectionAssert.AreEqual(new[] { "a.pdf", "c.pdf" }, leidas, "«b» nunca se leyó: ya había otro caso delante.");
        Assert.IsNull(resultados[0], "lo leído para «a» llegó tarde: se descarta.");
        Assert.IsNull(resultados[1], "«b» se canceló antes de leerse.");
        Assert.IsNotNull(resultados[2]);
        Assert.IsTrue(cuaderno.Any(linea => linea.Contains("cancelad", StringComparison.OrdinalIgnoreCase)), "la cancelación se anota.");
        Assert.IsTrue(cuaderno.Any(linea => linea.Contains("descart", StringComparison.OrdinalIgnoreCase)), "el descarte se anota.");
    }

    /// <summary>Dada una petición del caso vigente; cuando termina; entonces vuelven sus bandas.</summary>
    [TestMethod]
    public async Task UnaPeticionVigenteDevuelveSusBandas()
    {
        var busqueda = new BusquedaDeBandas(peticion => Task.FromResult(BandasDe(peticion.RutaPdf)), _ => { }, new SemaphoreSlim(1, 1));
        var generacion = busqueda.AbrirOtroCaso();

        var bandas = await busqueda.Buscar(generacion, Peticion("a.pdf"));

        Assert.IsNotNull(bandas);
        Assert.IsTrue(bandas.PorClave.ContainsKey("a.pdf"));
        Assert.AreEqual(0, busqueda.EnVuelo, "al terminar no queda nada en vuelo.");
    }

    /// <summary>Dada una lectura en vuelo; cuando se abre otro caso sin pedir bandas; entonces lo leído se descarta igual.</summary>
    [TestMethod]
    public async Task AbrirOtroCasoMientrasSeLeeDescartaLoLeido()
    {
        var primera = new TaskCompletionSource<BandasLeidas>();
        var busqueda = new BusquedaDeBandas(_ => primera.Task, _ => { }, new SemaphoreSlim(1, 1));
        var generacion = busqueda.AbrirOtroCaso();
        var tarea = busqueda.Buscar(generacion, Peticion("a.pdf"));

        busqueda.AbrirOtroCaso();
        primera.SetResult(BandasDe("a.pdf"));

        Assert.IsNull(await tarea);
    }

    /// <summary>Un lector que revienta no tumba nada: la búsqueda devuelve nulo, lo anota y queda libre para la siguiente.</summary>
    [TestMethod]
    public async Task UnLectorQueRevientaNoDejaLaBusquedaOcupada()
    {
        var cuaderno = new List<string>();
        var busqueda = new BusquedaDeBandas(_ => throw new InvalidOperationException("PDFium se cayó"), cuaderno.Add, new SemaphoreSlim(1, 1));
        var generacion = busqueda.AbrirOtroCaso();

        var bandas = await busqueda.Buscar(generacion, Peticion("a.pdf"));

        Assert.IsNull(bandas);
        Assert.AreEqual(0, busqueda.EnVuelo);
        Assert.IsTrue(cuaderno.Any(linea => linea.Contains("PDFium se cayó", StringComparison.Ordinal)), "el fallo no se calla.");
    }

    /// <summary>Cada apertura da una generación mayor que la anterior, y solo la última es la vigente.</summary>
    [TestMethod]
    public void CadaAperturaEsUnaGeneracionNuevaYSoloLaUltimaVale()
    {
        var turno = new TurnoDePintado();

        var primero = turno.Pedir();
        var segundo = turno.Pedir();

        Assert.IsGreaterThan(primero, segundo);
        Assert.IsFalse(turno.SigueVigente(primero));
        Assert.IsTrue(turno.SigueVigente(segundo));
    }
}
