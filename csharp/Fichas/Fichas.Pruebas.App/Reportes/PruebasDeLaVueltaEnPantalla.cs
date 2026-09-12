
using Fichas.App.Paquetes;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// La vuelta: leer el Excel del companero, aplicar sus marcas y decirlo en UNA linea.
/// </summary>
/// <remarks>
/// Es el criterio que el dueno dijo con un numero: «imagina que tenga 3 000 formularios, me
/// enviaron 300, no me puedo poner a ver uno a uno cuál se completó y cuál no». Asi que lo
/// que se comprueba aqui es que la cuenta sea EXACTA y que ni un motivo se pierda: el detalle
/// es lo unico que queda de una fila que el companero trabajo y que no entro.
///
/// <para>Que una fila que no casa NUNCA se aplique a otra familia esta probado donde vive esa
/// regla —<c>Fichas.Pruebas.Paquetes/PruebasDeLaVuelta.cs</c>, «una clave ambigua NO se
/// aplica a ninguna de las dos familias»—. Aqui se comprueba lo de la pantalla: que las
/// descartadas NO llegan siquiera a la llamada de aplicar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaVueltaEnPantalla
{
    /// <summary>La ruta de mentira del Excel devuelto; el doble no la abre.</summary>
    private const string RutaDelExcel = @"C:\no-importa\devuelto.xlsx";

    /// <summary>La linea dice cuantas filas venian, cuantas entraron y cuantas no.</summary>
    [TestMethod]
    public void LaLineaDiceCuantasVenianCuantasEntraronYCuantasNo()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(3), Descartadas(2));
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.AreEqual("Sandy: 5 filas · 3 entraron · 2 no entraron.", resumen.Linea);
    }

    /// <summary>Cuando entran todas, la linea lo dice y no habla de descartes.</summary>
    [TestMethod]
    public void CuandoEntranTodasLaLineaNoHablaDeDescartes()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, Marcas(4), []);
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.IsTrue(resumen.SalioBien, resumen.Linea);
        Assert.AreEqual("Sandy: 4 filas · 4 entraron · ninguna se quedó fuera.", resumen.Linea);
    }

    /// <summary>La linea cabe en un renglon aunque vengan 300 filas (requisito 4).</summary>
    [TestMethod]
    public void LaLineaCabeEnUnRenglonConTrescientasFilas()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(297), Descartadas(3));
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.AreEqual("Sandy: 300 filas · 297 entraron · 3 no entraron.", resumen.Linea);
        Assert.IsLessThanOrEqualTo(160, resumen.Linea.Length);
        Assert.DoesNotContain("\n", resumen.Linea, StringComparison.Ordinal);
    }

    /// <summary>Detrás de «ver» esta el motivo de CADA fila que no entro, con su numero de fila.</summary>
    [TestMethod]
    public void DetrasDeVerEstaElMotivoDeCadaFilaQueNoEntro()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(1), Descartadas(3));
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        for (var fila = 1; fila <= 3; fila++)
        {
            Assert.Contains($"motivo de la fila {fila}", resumen.Detalle, StringComparison.Ordinal,
                $"Se perdió el motivo de la fila {fila}.");
        }
    }

    /// <summary>Las que se caen AL APLICAR tambien cuentan como que no entraron.</summary>
    /// <remarks>
    /// Pasa cuando la base cambio entre leer el Excel y aplicarlo. Contarlas como entradas
    /// daria un numero que no corresponde con nada, y el dueno mide su trabajo con ese numero.
    /// </remarks>
    [TestMethod]
    public void LasQueSeCaenAlAplicarTambienCuentanComoQueNoEntraron()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(5), Descartadas(1), cuantasSeCaenAlAplicar: 2);
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.AreEqual("Sandy: 6 filas · 3 entraron · 3 no entraron.", resumen.Linea);
        Assert.Contains("ya no está en la base", resumen.Detalle, StringComparison.Ordinal);
    }

    /// <summary>Una fila que no casa NO llega siquiera a la llamada de aplicar.</summary>
    [TestMethod]
    public void UnaFilaQueNoCasaNoLlegaALaLlamadaDeAplicar()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(2), Descartadas(4));
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.HasCount(2, paquetes.UltimasMarcasAplicadas);
    }

    /// <summary>Solo se cuentan las descartadas de ESTA carga, no las que ya estaban.</summary>
    /// <remarks>
    /// Una segunda carga del mismo archivo deja sus renglones otra vez, a proposito y
    /// documentado en <c>Fichas.Paquetes</c>. Si la pantalla contara todas las de la tabla,
    /// la segunda vez diria el doble de descartes de los que hubo.
    /// </remarks>
    [TestMethod]
    public void SoloSeCuentanLasDescartadasDeEstaCarga()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(
            servicios.Ilegibles, servicios.Reloj, Marcas(3), Descartadas(2));
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var primera = operacion.Aplicar(Sandy(servicios), RutaDelExcel);
        var segunda = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.AreEqual(primera.Linea, segunda.Linea);
        Assert.HasCount(4, servicios.Almacen.Descartadas, "La segunda carga tiene que dejar sus renglones otra vez.");
    }

    /// <summary>
    /// Los avisos de leer Y los de aplicar salen dentro del resumen; no se pierde ninguno.
    /// </summary>
    /// <remarks>
    /// Viajan dentro y no se dejan en la franja desde aqui: aplicar corre fuera del hilo de la
    /// ventana, y tocar la franja desde fuera revienta con <c>COMException 0x8001010E</c>.
    /// Medido el 2026-09-04 abriendo la ventana de verdad.
    /// </remarks>
    [TestMethod]
    public void LosAvisosDeLeerYLosDeAplicarSalenDentroDelResumen()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, Marcas(1), []);
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.IsGreaterThanOrEqualTo(2, resumen.Avisos.Count, "Faltan avisos: los de leer y los de aplicar.");
    }

    /// <summary>Un Excel que no trae ni una fila se dice, y no se llama a aplicar.</summary>
    [TestMethod]
    public void UnExcelSinNiUnaFilaSeDice()
    {
        var servicios = Base();
        var paquetes = new PaquetesDeMentirijilla(servicios.Ilegibles, servicios.Reloj, [], []);
        var operacion = new OperacionDeLaVuelta(paquetes, servicios.Ilegibles);

        var resumen = operacion.Aplicar(Sandy(servicios), RutaDelExcel);

        Assert.IsFalse(resumen.SalioBien);
        Assert.Contains("ninguna fila", resumen.Linea, StringComparison.Ordinal);
        Assert.IsEmpty(paquetes.UltimasMarcasAplicadas);
    }

    // ---- lo que arman las pruebas -----------------------------------------

    /// <summary>Servicios falsos con cero casos, tres compañeros del generador y el reloj parado.</summary>
    private static ServiciosFalsos Base() => new(0, 3, new RelojFijo("2026-09-04"));

    /// <summary>Escribe a Sandy como compañera 1 en el almacén y la devuelve leída por el puerto.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    private static Companero Sandy(ServiciosFalsos servicios)
    {
        servicios.Almacen.Companeros[1] = new Companero { Id = 1, Nombre = "Sandy", Activo = true };
        return servicios.Companeros.Obtener(1)!;
    }

    /// <summary>Tantas marcas que casan como se pidan, del mismo caso y con cédulas correlativas.</summary>
    /// <param name="cuantas">Cuántas marcas.</param>
    private static List<MarcaDelCompanero> Marcas(int cuantas) =>
    [
        .. Enumerable.Range(1, cuantas).Select(numero => new MarcaDelCompanero(
            "CASP2609", $"055-0000-{numero:D4}", $"Persona {numero}",
            EstadoDeRecomendacion.SinMarcar, null, null,
            null, null, null, null, null, null, null,
            FilaExcel: 100 + numero, CasoId: numero)),
    ];

    /// <summary>Tantas filas descartadas como se pidan, cada una con su motivo numerado.</summary>
    /// <param name="cuantas">Cuántas filas.</param>
    private static List<FilaDescartada> Descartadas(int cuantas) =>
    [
        .. Enumerable.Range(1, cuantas).Select(numero => new FilaDescartada
        {
            FilaExcel = numero,
            NumeroCaso = "CASP2609",
            Mrn = $"999-0000-{numero:D4}",
            Nombre = $"Sin par {numero}",
            Motivo = $"Este es el motivo de la fila {numero}.",
            RegistradoEn = "2026-09-04 12:00:00",
        }),
    ];
}
