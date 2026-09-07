using System.Diagnostics;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// La rapidez de las dos pantallas con la cifra del dueno: 3 000 documentos, no 2.
/// </summary>
/// <remarks>
/// Requisito 5 y criterio C4-6: cada pantalla ≤ 0,2 s. Lo que se mide aqui es **la regla**,
/// que es lo unico que se puede medir sin ventana: leer la base, componer las tarjetas,
/// contar los seis tableros y servir un trozo al desplazarse. El pintado de los pixeles se
/// mide aparte, con la ventana abierta, y esa cifra NO la da esta prueba.
///
/// Cada medicion se hace despues de una pasada de calentamiento: la primera llamada paga el
/// compilador en caliente y mediria el arranque de .NET, no la pantalla.
/// </remarks>
[TestClass]
public sealed class PruebasDeRapidez
{
    /// <summary>La cifra del dueno: 3 000 documentos.</summary>
    private const int TresMil = 3000;

    /// <summary>El techo del criterio C4-6, en milisegundos.</summary>
    private const double Techo = 200.0;

    /// <summary>Cuantas tarjetas caben en una pantallada; es lo que sirve un desplazamiento.</summary>
    private const int UnaPantallada = 60;

    /// <summary>
    /// Con 3 000 documentos, cargar el tablero de Revisar entero cabe en 0,2 s.
    /// </summary>
    /// <remarks>
    /// Se mide con los archivados DENTRO a proposito, aunque la pantalla no los pida: es el
    /// peor caso y es donde estan los 3 000 del dueno. La vista de trabajo carga menos, y eso
    /// se comprueba aparte para que nadie confunda «tarda menos» con «se perdieron casos».
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosElTableroDeRevisarCargaEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Tablero.Cargar(conArchivados: true);   // calentamiento

        var cronometro = Stopwatch.StartNew();
        banco.Tablero.Cargar(conArchivados: true);
        cronometro.Stop();
        var conTodo = banco.Tablero.Total;

        banco.Tablero.Cargar();
        var deTrabajo = banco.Tablero.Total;

        Assert.AreEqual(TresMil, conTodo, "La base de la medicion tiene 3 000 documentos.");
        Assert.IsLessThan(conTodo, deTrabajo, "Y la vista de trabajo carga menos: los archivados salen de ella.");
        Console.WriteLine($"Revisar · cargar 3 000 tarjetas: {cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(
            Techo,
            cronometro.Elapsed.TotalMilliseconds,
            $"Cargar el tablero tardo {cronometro.Elapsed.TotalMilliseconds:F1} ms; el techo es {Techo:F0} ms.");
    }

    /// <summary>Con 3 000, contar los seis tableros a la vez cabe en 0,2 s.</summary>
    [TestMethod]
    public void ConTresMilDocumentosLosSeisTablerosSeCuentanEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Tablero.Cargar(conArchivados: true);
        banco.Tablero.Cuentas();   // calentamiento

        var cronometro = Stopwatch.StartNew();
        var cuentas = banco.Tablero.Cuentas();
        cronometro.Stop();

        Assert.AreEqual(TresMil, cuentas[FiltroDeTarjeta.Todo]);
        Console.WriteLine($"Revisar · contar los 6 tableros: {cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(
            Techo,
            cronometro.Elapsed.TotalMilliseconds,
            $"Contar los seis tableros tardo {cronometro.Elapsed.TotalMilliseconds:F1} ms.");
    }

    /// <summary>Desplazarse por el tablero sirve una pantallada muy por debajo del techo.</summary>
    [TestMethod]
    public void DesplazarsePorElTableroDeRevisarSirveUnaPantalladaEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Tablero.Cargar(conArchivados: true);
        banco.Tablero.Ver(FiltroDeTarjeta.Todo, new Pagina(0, UnaPantallada));   // calentamiento

        var peor = 0.0;
        // Cincuenta pantalladas seguidas, que es bajar por los 3 000 de arriba abajo.
        for (var desde = 0; desde < TresMil; desde += UnaPantallada)
        {
            var cronometro = Stopwatch.StartNew();
            var trozo = banco.Tablero.Ver(FiltroDeTarjeta.Todo, new Pagina(desde, UnaPantallada));
            cronometro.Stop();
            peor = Math.Max(peor, cronometro.Elapsed.TotalMilliseconds);
            Assert.IsNotEmpty(trozo, "Cada pantallada trae tarjetas.");
        }

        Console.WriteLine($"Revisar · la pantallada mas lenta de 50: {peor:F1} ms");
        Assert.IsLessThanOrEqualTo(Techo, peor, $"La pantallada mas lenta tardo {peor:F1} ms; el techo es {Techo:F0} ms.");
    }

    /// <summary>Con 3 000 en la base, la lista de Asignar sirve cada pantallada en menos de 0,2 s.</summary>
    /// <remarks>
    /// La lista ofrece los que no estan archivados, asi que el recorrido llega hasta esa cifra
    /// y no hasta 3 000: pedir mas alla del final devolveria trozos vacios y la prueba estaria
    /// midiendo el vacio, no la pantalla.
    /// </remarks>
    [TestMethod]
    public void ConTresMilCasosLaListaDeAsignarSirveUnaPantalladaEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Lista.Ofrecer(new Pagina(0, UnaPantallada));   // calentamiento
        var cuantos = banco.Lista.CuantosSePuedenOfrecer();

        var peor = 0.0;
        for (var desde = 0; desde < cuantos; desde += UnaPantallada)
        {
            var cronometro = Stopwatch.StartNew();
            var trozo = banco.Lista.Ofrecer(new Pagina(desde, UnaPantallada));
            cronometro.Stop();
            peor = Math.Max(peor, cronometro.Elapsed.TotalMilliseconds);
            Assert.IsNotEmpty(trozo.Elementos);
        }

        Assert.IsGreaterThan(2000, cuantos, "Con 3 000 en la base, sin archivar quedan miles: la medicion es de verdad.");
        Console.WriteLine($"Asignar · {cuantos} ofrecidos, la pantallada mas lenta: {peor:F1} ms");
        Assert.IsLessThanOrEqualTo(Techo, peor, $"La pantallada mas lenta tardo {peor:F1} ms; el techo es {Techo:F0} ms.");
    }

    /// <summary>
    /// Con 3 000 en la base, la pantalla de Asignar entera —que los pide TODOS— cabe en 0,2 s.
    /// </summary>
    /// <remarks>
    /// Es lo que de verdad hace la ventana: <c>PaginaDeAsignar.Repintar</c> llama a
    /// <c>Ofrecer(Pagina.Primera(int.MaxValue))</c> y deja que el <c>ItemsView</c> virtualice el
    /// pintado. La prueba de la pantallada de arriba mide el desplazamiento; esta mide lo que el
    /// dueno espera mirando la pantalla al entrar, que es otra cosa y es la que se le nota.
    ///
    /// Se anadio el 2026-09-06, cuando el renglon empezo a decir QUIEN viaja: hasta ese dia esta
    /// pantalla no leia ni una persona, y sin este numero el coste de los nombres se habria
    /// quedado sin medir donde mas se paga.
    /// </remarks>
    [TestMethod]
    public void ConTresMilCasosLaPantallaDeAsignarEnteraCargaEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue));   // calentamiento

        var cronometro = Stopwatch.StartNew();
        var todos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue));
        cronometro.Stop();

        Assert.IsGreaterThan(2000, todos.Elementos.Count, "Se cargaron miles, no un trozo.");
        Console.WriteLine(
            $"Asignar · la pantalla entera con {todos.Elementos.Count} casos: "
            + $"{cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(
            Techo,
            cronometro.Elapsed.TotalMilliseconds,
            $"Cargar la pantalla entera tardo {cronometro.Elapsed.TotalMilliseconds:F1} ms; el techo es {Techo:F0} ms.");
    }

    /// <summary>Con 3 000, el denominador de Asignar se dice sin traerse los 3 000 casos.</summary>
    [TestMethod]
    public void ConTresMilCasosElDenominadorSeDiceEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Lista.CuantosSePuedenOfrecer();   // calentamiento

        var cronometro = Stopwatch.StartNew();
        var sinArchivar = banco.Lista.CuantosSePuedenOfrecer();
        var archivados = banco.Lista.CuantosHayArchivados();
        var ofrecidos = banco.Lista.CuantosSeOfrecen();
        cronometro.Stop();

        Assert.AreEqual(TresMil, sinArchivar + archivados, "Las dos cifras del denominador suman los 3 000 de la base.");
        Assert.AreEqual(sinArchivar, ofrecidos, "Con 3 000 tambien: N sin archivar, N ofrecidos.");
        Console.WriteLine($"Asignar · el denominador de 3 000 ({sinArchivar} + {archivados}): {cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(Techo, cronometro.Elapsed.TotalMilliseconds);
    }

    /// <summary>Archivar 300 de golpe —los que le devolvieron— cabe en 0,2 s.</summary>
    [TestMethod]
    public void ArchivarTrescientosDeGolpeCabeEnMenosDeDosDecimas()
    {
        var banco = new BaseDePrueba(TresMil);
        banco.Tablero.Cargar();
        var trescientos = banco.Tablero.Todas(FiltroDeTarjeta.Todo)
            .Where(t => !t.Archivado)
            .Take(300)
            .Select(t => t.CasoId)
            .ToList();

        var cronometro = Stopwatch.StartNew();
        var resumen = banco.Acciones.ArchivarEnLote(trescientos);
        cronometro.Stop();

        Assert.AreEqual(300, resumen.Hechos, "«Me enviaron 300»: los 300 en un gesto.");
        Console.WriteLine($"Revisar · archivar 300 en lote: {cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(Techo, cronometro.Elapsed.TotalMilliseconds);
    }
}
