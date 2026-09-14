using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// El panel de carpetas de Revisar se agranda y se reduce con el ratón, probado SIN VENTANA.
/// </summary>
/// <remarks>
/// <para>El criterio del que salen estas pruebas son las palabras del dueño, 2026-09-14:
/// <i>«Actualizar el menú de los grupos: que pueda ser ajustado con el mouse, agrandar o
/// reducir, en la ventana de Revisar»</i>. De ahí, y del pase, se derivan cinco cosas
/// comprobables sin abrir ninguna ventana:</para>
/// <list type="number">
///   <item>Sin haber tocado nada, el panel mide lo de siempre: 290, que es lo que decía el
///   XAML fijo hasta hoy.</item>
///   <item>Arrastrar suma o resta al ancho, y hay un mínimo que no deja el panel inútil y un
///   máximo que no aplasta las tarjetas.</item>
///   <item>El doble clic vuelve a lo de siempre.</item>
///   <item>Lo elegido sobrevive a cerrar y volver a abrir, en la CARPETA DE DATOS y en el
///   mismo <c>preferencias.txt</c> del tema, sin pisar la línea del tema.</item>
///   <item>Con la ventana estrecha (1100×700) un ancho guardado que no cabe se recorta y las
///   tarjetas siguen viéndose.</item>
/// </list>
///
/// <para>⚠️ Lo que estas pruebas NO miran, porque no hay ventana: que el tirador se dibuje,
/// que el cursor cambie y que el arrastre real mueva la columna. Eso se mide con el paquete
/// publicado y va en la entrega.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelAnchoDelPanelDeCarpetas
{
    /// <summary>
    /// El ancho del marco —panel más rejilla— con la ventana a 1100×700, medido por UIA el
    /// 2026-09-14 sobre el paquete publicado de master: el árbol empezaba en x=425 y la
    /// rejilla acababa en x=1224, es decir, 799. Se redondea a 800 para no depender del píxel.
    /// </summary>
    private const double MarcoA1100 = 800;

    /// <summary>
    /// El mismo marco con la ventana de siempre, 1730×770: árbol en x=399 y rejilla hasta
    /// x=1828, 1 429; redondeado a 1 430.
    /// </summary>
    private const double MarcoA1730 = 1430;

    /// <summary>La carpeta de datos de cada prueba, temporal y propia; nunca la del dueño.</summary>
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta propia por prueba: nunca la del dueño.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-ancho-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>Se recoge lo que se creó, para no dejar carpetas sueltas.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try
        {
            if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            // Que no se pueda borrar una carpeta temporal no invalida la prueba.
        }
    }

    // ═════════ 1. Lo de siempre ═════════

    /// <summary>Dado que nunca se tocó el tirador, cuando se abre Revisar, entonces el panel mide 290.</summary>
    [TestMethod]
    public void SinHaberTocadoNadaElPanelMideLoDeSiempre()
    {
        var preferencia = new PreferenciaDelAnchoDelPanel(_carpeta);

        Assert.AreEqual(290d, preferencia.Leer(), "El XAML llevaba Width=\"290\" fijo hasta hoy.");
        Assert.IsFalse(File.Exists(preferencia.Ruta), "Leer no escribe: hasta que no arrastra, no hay archivo.");
    }

    // ═════════ 2. Arrastrar, con su suelo y su techo ═════════

    /// <summary>Dado el panel en 290, cuando se arrastra 150 a la derecha, entonces mide 440.</summary>
    [TestMethod]
    public void ArrastrarALaDerechaAgrandaElPanel()
        => Assert.AreEqual(440d, AnchoDelPanelDeCarpetas.TrasArrastrar(290, +150, MarcoA1730));

    /// <summary>Dado el panel en 440, cuando se arrastra 150 a la izquierda, entonces vuelve a 290.</summary>
    [TestMethod]
    public void ArrastrarALaIzquierdaReduceElPanel()
        => Assert.AreEqual(290d, AnchoDelPanelDeCarpetas.TrasArrastrar(440, -150, MarcoA1730));

    /// <summary>
    /// Dado el panel en 290, cuando se arrastra más a la izquierda de lo que cabe, entonces se
    /// queda en el mínimo y no desaparece.
    /// </summary>
    /// <remarks>
    /// El mínimo lo marca lo que hay dentro del panel: el botón «Volcar el mes a carpetas…»
    /// y la casilla «Ver los archivados» tienen que seguir leyéndose enteros. Sus anchos
    /// naturales están medidos por UIA en la entrega; aquí solo se exige que el mínimo esté
    /// por encima de los dos.
    /// </remarks>
    [TestMethod]
    public void ArrastrarDeMasALaIzquierdaSeQuedaEnElMinimo()
    {
        var ancho = AnchoDelPanelDeCarpetas.TrasArrastrar(290, -1000, MarcoA1730);

        Console.WriteLine("== mínimo = {0} ==", AnchoDelPanelDeCarpetas.Minimo);

        Assert.AreEqual(AnchoDelPanelDeCarpetas.Minimo, ancho);
        Assert.IsGreaterThanOrEqualTo(180d, AnchoDelPanelDeCarpetas.Minimo, "Por debajo el botón de volcar se corta.");
        Assert.IsLessThan(AnchoDelPanelDeCarpetas.ElDeSiempre, AnchoDelPanelDeCarpetas.Minimo, "Reducir tiene que ser posible.");
    }

    /// <summary>
    /// Dado el panel en 290, cuando se arrastra más a la derecha de lo que cabe, entonces se
    /// queda en el máximo y a las tarjetas les queda sitio para al menos una entera.
    /// </summary>
    [TestMethod]
    public void ArrastrarDeMasALaDerechaSeQuedaEnElMaximoYDejaSitioAUnaTarjeta()
    {
        var ancho = AnchoDelPanelDeCarpetas.TrasArrastrar(290, +5000, MarcoA1730);

        Console.WriteLine("== máximo a {0} de marco = {1} ==", MarcoA1730, ancho);

        Assert.AreEqual(AnchoDelPanelDeCarpetas.MaximoPara(MarcoA1730), ancho);
        Assert.IsGreaterThanOrEqualTo(
            AnchoDelPanelDeCarpetas.AnchoDeUnaTarjeta,
            MarcoA1730 - ancho - AnchoDelPanelDeCarpetas.LoQueOcupaElTirador,
            "Con el panel al máximo tiene que caber una tarjeta entera (MinItemWidth=330) a la derecha.");
    }

    /// <summary>Dado un ancho dentro del margen, cuando se recorta, entonces no cambia.</summary>
    [TestMethod]
    [DataRow(290)]
    [DataRow(400)]
    [DataRow(600)]
    public void UnAnchoQueCabeNoSeToca(int pedido)
        => Assert.AreEqual((double)pedido, AnchoDelPanelDeCarpetas.Recortar(pedido, MarcoA1730));

    // ═════════ 3. El doble clic ═════════

    /// <summary>Dado el panel en cualquier ancho, cuando se hace doble clic en el tirador, entonces vuelve a 290.</summary>
    [TestMethod]
    public void ElDobleClicVuelveALoDeSiempre()
        => Assert.AreEqual(290d, AnchoDelPanelDeCarpetas.TrasElDobleClic());

    // ═════════ 4. Se recuerda entre sesiones ═════════

    /// <summary>Dado que se dejó el panel en 440, cuando se vuelve a abrir, entonces sigue en 440.</summary>
    [TestMethod]
    public void LoElegidoSobreviveACerrarYVolverAAbrir()
    {
        Assert.IsTrue(new PreferenciaDelAnchoDelPanel(_carpeta).Guardar(440));

        var otraSesion = new PreferenciaDelAnchoDelPanel(_carpeta);

        Assert.AreEqual(440d, otraSesion.Leer());
    }

    /// <summary>Se guarda en la carpeta de datos y en el MISMO archivo del tema, no en otro.</summary>
    [TestMethod]
    public void SeGuardaEnPreferenciasTxtDeLaCarpetaDeDatos()
    {
        var preferencia = new PreferenciaDelAnchoDelPanel(_carpeta);
        preferencia.Guardar(350);

        Assert.AreEqual(Path.Combine(_carpeta, "preferencias.txt"), preferencia.Ruta);
        Assert.IsTrue(File.Exists(preferencia.Ruta));
        Assert.HasCount(1, Directory.GetFiles(_carpeta), "Un solo archivo de preferencias, no uno por preferencia.");
    }

    /// <summary>Dado que el tema ya estaba guardado, cuando se guarda el ancho, entonces el tema sigue ahí.</summary>
    [TestMethod]
    public void GuardarElAnchoNoPisaElTema()
    {
        var ruta = Path.Combine(_carpeta, "preferencias.txt");
        File.WriteAllLines(ruta, ["tema=oscuro"]);

        new PreferenciaDelAnchoDelPanel(_carpeta).Guardar(500);
        new PreferenciaDelAnchoDelPanel(_carpeta).Guardar(520);

        var lineas = File.ReadAllLines(ruta);
        Console.WriteLine("== preferencias.txt: {0} ==", string.Join(" | ", lineas));

        CollectionAssert.Contains(lineas, "tema=oscuro", "La línea del tema se perdió.");
        Assert.HasCount(1, lineas.Where(l => l.StartsWith("ancho_del_panel_de_carpetas=", StringComparison.Ordinal)).ToList(),
            "Guardar dos veces tiene que dejar UNA línea de ancho, no dos.");
        Assert.AreEqual(520d, new PreferenciaDelAnchoDelPanel(_carpeta).Leer());
    }

    /// <summary>Dado un archivo con un ancho que no es un número, cuando se lee, entonces se abre con lo de siempre.</summary>
    [TestMethod]
    [DataRow("ancho_del_panel_de_carpetas=ancho")]
    [DataRow("ancho_del_panel_de_carpetas=")]
    [DataRow("ancho_del_panel_de_carpetas=-40")]
    public void UnAnchoEstropeadoEnElArchivoNoRompeNada(string linea)
    {
        File.WriteAllLines(Path.Combine(_carpeta, "preferencias.txt"), ["tema=claro", linea]);

        Assert.AreEqual(AnchoDelPanelDeCarpetas.ElDeSiempre, new PreferenciaDelAnchoDelPanel(_carpeta).Leer());
    }

    /// <summary>Un ancho con decimales, que es lo que da el ratón, se guarda redondeado a píxel entero.</summary>
    [TestMethod]
    public void ElAnchoSeGuardaEnPixelesEnteros()
    {
        new PreferenciaDelAnchoDelPanel(_carpeta).Guardar(333.6);

        var lineas = File.ReadAllLines(Path.Combine(_carpeta, "preferencias.txt"));

        CollectionAssert.Contains(lineas, "ancho_del_panel_de_carpetas=334");
        Assert.AreEqual(334d, new PreferenciaDelAnchoDelPanel(_carpeta).Leer());
    }

    /// <summary>Dada una carpeta que no existe todavía, cuando se guarda, entonces se crea y no lanza.</summary>
    [TestMethod]
    public void GuardarCreaLaCarpetaSiNoExiste()
    {
        var nueva = Path.Combine(_carpeta, "todavia-no");

        Assert.IsTrue(new PreferenciaDelAnchoDelPanel(nueva).Guardar(300));
        Assert.AreEqual(300d, new PreferenciaDelAnchoDelPanel(nueva).Leer());
    }

    // ═════════ 5. La ventana estrecha ═════════

    /// <summary>
    /// Dado un ancho guardado de 700 y la ventana a 1100×700, cuando se abre Revisar, entonces
    /// el panel se recorta al máximo permitido y a la derecha sigue cabiendo una tarjeta entera.
    /// </summary>
    [TestMethod]
    public void ConLaVentanaEstrechaUnAnchoGuardadoQueNoCabeSeRecorta()
    {
        var enPantalla = AnchoDelPanelDeCarpetas.Recortar(700, MarcoA1100);

        Console.WriteLine("== guardado 700, marco {0}: en pantalla {1} ==", MarcoA1100, enPantalla);

        Assert.IsLessThan(700d, enPantalla, "A 1100×700 no caben 700 de panel y una tarjeta de 330.");
        Assert.AreEqual(AnchoDelPanelDeCarpetas.MaximoPara(MarcoA1100), enPantalla);
        Assert.IsGreaterThanOrEqualTo(
            AnchoDelPanelDeCarpetas.AnchoDeUnaTarjeta,
            MarcoA1100 - enPantalla - AnchoDelPanelDeCarpetas.LoQueOcupaElTirador);
    }

    /// <summary>
    /// Recortar en pantalla NO cambia lo guardado: al volver a la ventana grande, el panel
    /// recupera lo que él eligió.
    /// </summary>
    [TestMethod]
    public void RecortarEnPantallaNoCambiaLoElegido()
    {
        var elegido = 700d;

        var enEstrecha = AnchoDelPanelDeCarpetas.Recortar(elegido, MarcoA1100);
        var enGrande = AnchoDelPanelDeCarpetas.Recortar(elegido, MarcoA1730);

        Assert.IsLessThan(elegido, enEstrecha);
        Assert.AreEqual(elegido, enGrande, "Lo elegido cabe en la ventana grande y tiene que volver tal cual.");
    }

    /// <summary>
    /// Dado un marco tan estrecho que ni el mínimo cabe, cuando se recorta, entonces manda el
    /// mínimo: el panel no se hace inútil por que la ventana sea diminuta.
    /// </summary>
    [TestMethod]
    public void ConUnMarcoDiminutoMandaElMinimo()
    {
        var enPantalla = AnchoDelPanelDeCarpetas.Recortar(290, 400);

        Assert.AreEqual(AnchoDelPanelDeCarpetas.Minimo, enPantalla);
        Assert.AreEqual(AnchoDelPanelDeCarpetas.Minimo, AnchoDelPanelDeCarpetas.MaximoPara(400), "El techo nunca baja del suelo.");
    }

    /// <summary>Un marco que todavía no se ha medido (0 o NaN, antes del primer pintado) no rompe: manda lo de siempre.</summary>
    [TestMethod]
    [DataRow(0d)]
    [DataRow(double.NaN)]
    public void UnMarcoSinMedirNoRompe(double marco)
        => Assert.AreEqual(290d, AnchoDelPanelDeCarpetas.Recortar(290, marco));
}
