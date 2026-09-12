using Fichas.App.Importar;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Lo que el nombre de una carpeta dice, y lo que NO se deja decir.
/// </summary>
/// <remarks>
/// <para>Nace de las palabras del dueño del 2026-09-05, peticion 8: <i>«permíteme nombrar
/// las carpetas; y si cargo un grupo de carpetas, que tome la información de las carpetas
/// que cargo, porque muchas veces yo mismo organizo la carpeta y lo único que hace falta es
/// cargarla al sistema»</i>.</para>
///
/// <para>La forma que se reconoce es <b>exactamente la que el programa ya escribe</b> al
/// volcar a disco —<c>Septiembre 2026 / Grupo del 8 de septiembre / 700001 · Castries
/// Branch</c>, ver <c>Fichas.App.Revisar.ArbolDeRevisar</c>—, para que lo que sale y lo que
/// entra sean la misma cosa.</para>
///
/// <para>⛔ <b>Aqui no se adivina nada</b> (regla permanente 1). Una carpeta que no encaja
/// no se interpreta «por parecido» ni rechaza el PDF: se nombra y se sigue. Y lo que falta
/// —el año cuando solo hay dia y mes— NO se rellena con el año de hoy.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueDicenLasCarpetas
{
    /// <summary>La carpeta que Miguel «eligió» en estas pruebas; lo de fuera de ella no se mira.</summary>
    private const string Raiz = @"C:\escaneos";

    /// <summary>Lee las carpetas de un camino que cuelga de <see cref="Raiz"/>; el último trozo es el archivo.</summary>
    /// <param name="carpetasYArchivo">Los nombres de carpeta, de fuera adentro, y el nombre del PDF al final.</param>
    private static LoQueDicenLasCarpetas Leer(params string[] carpetasYArchivo)
        => LoQueDicenLasCarpetas.Leer(Path.Combine([Raiz, .. carpetasYArchivo]), Raiz);

    /// <summary>El arbol que el propio programa escribe se lee entero y sin sobras.</summary>
    [TestMethod]
    public void ElArbolQueElProgramaEscribeSeAprovechaEntero()
    {
        var dicen = Leer("Septiembre 2026", "Grupo del 8 de septiembre", "700001 · Castries Branch", "x.pdf");

        Assert.AreEqual("2026-09-08", dicen.FechaDeViaje);
        Assert.AreEqual("2026-09", dicen.MesDeViaje);
        Assert.AreEqual("700001", dicen.UnidadNumero);
        Assert.AreEqual("Castries Branch", dicen.UnidadNombre);
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas);
        Assert.IsTrue(dicen.DiceAlgo);
    }

    /// <summary>Nombres que no siguen ninguna forma: nada se rechaza y nada se inventa.</summary>
    [TestMethod]
    public void UnArbolConNombresCualesquieraNoAportaNadaYSeDiceCual()
    {
        var dicen = Leer("cosas mías", "lo de la semana pasada", "x.pdf");

        Assert.IsNull(dicen.FechaDeViaje);
        Assert.IsNull(dicen.MesDeViaje);
        Assert.IsNull(dicen.UnidadNumero);
        Assert.IsNull(dicen.UnidadNombre);
        Assert.IsFalse(dicen.DiceAlgo);
        CollectionAssert.AreEqual(
            new[] { "cosas mías", "lo de la semana pasada" }, dicen.CarpetasNoAprovechadas.ToArray());
    }

    /// <summary>Sin carpeta de mes NO hay año, y el año no se inventa.</summary>
    /// <remarks>
    /// Es la diferencia entre una fecha de viaje y una fecha de viaje inventada. Con el año
    /// de hoy puesto a mano, un grupo de enero saldria ordenado once meses antes de cuando
    /// viaja, y el programa existe justo para avisar de quien viaja pronto.
    /// </remarks>
    [TestMethod]
    public void SinCarpetaDeMesNoHayAnioYPorTantoNoHayFecha()
    {
        var dicen = Leer("Grupo del 8 de septiembre", "x.pdf");

        Assert.IsNull(dicen.FechaDeViaje, "sin año no hay fecha, y el año no se pone a ojo");
        Assert.IsNull(dicen.MesDeViaje);
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas, "la carpeta se entendió; lo que falta es el año");
        Assert.IsTrue(dicen.DiceAlgo);
        Assert.AreEqual(9, dicen.MesDelDia);
        Assert.AreEqual(8, dicen.DiaDelMes);
    }

    /// <summary>Con mes pero sin dia se sabe el mes y no la fecha.</summary>
    [TestMethod]
    public void ConMesYSinDiaSeSabeElMesYNoLaFecha()
    {
        var dicen = Leer("Septiembre 2026", "700001 · Castries Branch", "x.pdf");

        Assert.AreEqual("2026-09", dicen.MesDeViaje);
        Assert.IsNull(dicen.FechaDeViaje);
        Assert.AreEqual("700001", dicen.UnidadNumero);
    }

    /// <summary>Las dos carpetas que el volcado escribe cuando NO sabe: no son sobras.</summary>
    /// <remarks>
    /// «Sin fecha de viaje» y «Sin unidad» las escribe el propio programa
    /// (<c>ArbolDeRevisar.SinFecha</c> y <c>SinUnidad</c>). Contarlas como carpetas que no se
    /// pudieron aprovechar seria mentir: se entendieron perfectamente, y lo que dicen es que
    /// ahi no hay dato.
    /// </remarks>
    [TestMethod]
    public void LasCarpetasQueDicenQueNoSeSabeSeEntiendenYNoSonSobras()
    {
        var dicen = Leer("Sin fecha de viaje", "Sin unidad", "x.pdf");

        Assert.IsNull(dicen.FechaDeViaje);
        Assert.IsNull(dicen.UnidadNumero);
        Assert.IsNull(dicen.UnidadNombre);
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas);
        Assert.IsFalse(dicen.DiceAlgo, "entendidas, pero no aportan ningún dato");
    }

    /// <summary>El numero de unidad solo, sin nombre, tambien vale.</summary>
    [TestMethod]
    public void ElNumeroDeUnidadSoloSeAprovecha()
    {
        var dicen = Leer("7000011", "x.pdf");

        Assert.AreEqual("7000011", dicen.UnidadNumero, "el esquema admite 6 o 7 dígitos");
        Assert.IsNull(dicen.UnidadNombre);
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas);
    }

    /// <summary>Una carpeta con solo un nombre NO se toma por unidad.</summary>
    /// <remarks>
    /// ⚠️ Es el limite que impide que el programa se invente unidades. «Castries Branch»
    /// sin numero delante es indistinguible de «Escaneos de Marta»: si se aceptara, cualquier
    /// carpeta del disco acabaria escrita en la columna <c>unidad_nombre</c> de un caso, y el
    /// dueño no tendria forma de saber cual salio del papel y cual del nombre de su carpeta.
    /// </remarks>
    [TestMethod]
    public void UnNombreSinNumeroNoSeTomaPorUnidad()
    {
        var dicen = Leer("Castries Branch", "x.pdf");

        Assert.IsNull(dicen.UnidadNumero);
        Assert.IsNull(dicen.UnidadNombre);
        CollectionAssert.AreEqual(new[] { "Castries Branch" }, dicen.CarpetasNoAprovechadas.ToArray());
    }

    /// <summary>Un numero que no tiene 6 ni 7 digitos tampoco es una unidad.</summary>
    [TestMethod]
    public void UnNumeroQueNoEsDeUnidadNoSeToma()
    {
        var dicen = Leer("2026", "x.pdf");

        Assert.IsNull(dicen.UnidadNumero);
        CollectionAssert.AreEqual(new[] { "2026" }, dicen.CarpetasNoAprovechadas.ToArray());
    }

    /// <summary>Las mayusculas y el guion de teclado tambien se entienden.</summary>
    /// <remarks>
    /// Las carpetas las escribe Miguel a mano, y el punto medio «·» no esta en el teclado.
    /// Lo que ancla la lectura es el numero, no el separador: sin 6 o 7 digitos delante no
    /// hay unidad, se ponga el separador que se ponga.
    /// </remarks>
    [TestMethod]
    public void SeEntiendeComoLoEscribeUnaPersonaConSuTeclado()
    {
        var conGuion = Leer("septiembre 2026", "grupo del 08 de septiembre", "700001 - Castries Branch", "x.pdf");

        Assert.AreEqual("2026-09-08", conGuion.FechaDeViaje);
        Assert.AreEqual("700001", conGuion.UnidadNumero);
        Assert.AreEqual("Castries Branch", conGuion.UnidadNombre);
    }

    /// <summary>Una fecha en ISO tambien se entiende, que es como se nombra media oficina.</summary>
    [TestMethod]
    public void UnaCarpetaEnIsoSeEntiende()
    {
        var dicen = Leer("2026-09", "2026-09-08", "x.pdf");

        Assert.AreEqual("2026-09-08", dicen.FechaDeViaje);
        Assert.AreEqual("2026-09", dicen.MesDeViaje);
    }

    /// <summary>Si el mes de arriba y el dia de abajo no cuadran, NO se compone una fecha.</summary>
    /// <remarks>
    /// Componer «2026-09-08» de una carpeta que dice octubre seria fabricar un dato que no
    /// esta escrito en ningun sitio. Se dice que no cuadran y se deja sin fecha.
    /// </remarks>
    [TestMethod]
    public void UnMesYUnDiaQueNoCuadranNoComponenFecha()
    {
        var dicen = Leer("Septiembre 2026", "Grupo del 8 de octubre", "x.pdf");

        Assert.IsNull(dicen.FechaDeViaje);
        Assert.AreEqual("2026-09", dicen.MesDeViaje);
        Assert.IsTrue(dicen.MesYDiaSeContradicen);
    }

    /// <summary>Un dia que no existe en ese mes no compone fecha.</summary>
    [TestMethod]
    public void UnDiaQueNoExisteNoComponeFecha()
    {
        var dicen = Leer("Febrero 2026", "Grupo del 30 de febrero", "x.pdf");

        Assert.IsNull(dicen.FechaDeViaje);
        Assert.AreEqual("2026-02", dicen.MesDeViaje);
    }

    /// <summary>Las carpetas por encima de la que Miguel eligio NO se miran.</summary>
    /// <remarks>
    /// Si se miraran, la ruta del propio disco —<c>C:\Users\josem\Documents</c>— entraria en
    /// la lista de carpetas que no se pudieron aprovechar y el aviso seria ilegible. Y peor:
    /// una carpeta personal llamada «700001» que estuviera tres niveles por encima acabaria
    /// poniendole unidad a documentos que no tienen nada que ver.
    /// </remarks>
    [TestMethod]
    public void SoloSeMiranLasCarpetasDeDentroDeLaQueSeEligio()
    {
        var dicen = LoQueDicenLasCarpetas.Leer(
            @"C:\escaneos\700001 · Castries Branch\x.pdf", @"C:\escaneos\700001 · Castries Branch");

        Assert.IsNull(dicen.UnidadNumero, "esa carpeta es la raíz elegida, no está «dentro» de nada");
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas);
        Assert.IsFalse(dicen.DiceAlgo);
    }

    /// <summary>Un archivo suelto, elegido con Ctrl, no tiene arbol que leer.</summary>
    [TestMethod]
    public void UnArchivoSueltoNoTieneArbolQueLeer()
    {
        var dicen = LoQueDicenLasCarpetas.Leer(@"C:\escaneos\x.pdf", carpetaElegida: null);

        Assert.IsFalse(dicen.DiceAlgo);
        Assert.IsEmpty(dicen.CarpetasNoAprovechadas);
    }

    /// <summary>La unidad mas cercana al PDF manda sobre la de mas arriba.</summary>
    /// <remarks>
    /// Un arbol puede tener una carpeta de unidad dentro de otra si Miguel reorganiza a
    /// medias. La de dentro es la que describe a ESTE documento; la de fuera describe al
    /// monton. Se elige la de dentro y NO se calla que habia otra.
    /// </remarks>
    [TestMethod]
    public void LaUnidadMasCercanaAlDocumentoManda()
    {
        var dicen = Leer("700001 · Castries Branch", "700005 · Calliaqua", "x.pdf");

        Assert.AreEqual("700005", dicen.UnidadNumero);
        Assert.AreEqual("Calliaqua", dicen.UnidadNombre);
        Assert.IsTrue(dicen.HuboMasDeUnaUnidad);
    }

    /// <summary>Lo que se leyo se puede contar en una linea que el dueño entienda.</summary>
    [TestMethod]
    public void SeExplicaEnUnaLineaQueSeAprovechoYQueNo()
    {
        var dicen = Leer("Septiembre 2026", "Grupo del 8 de septiembre", "cosas mías", "x.pdf");

        StringAssert.Contains(dicen.Explicacion, "2026-09-08");
        StringAssert.Contains(dicen.Explicacion, "cosas mías");
    }

    /// <summary>Una ruta vacia no levanta: contesta que no dice nada.</summary>
    [TestMethod]
    public void UnaRutaVaciaNoLevanta()
    {
        var dicen = LoQueDicenLasCarpetas.Leer(string.Empty, Raiz);

        Assert.IsFalse(dicen.DiceAlgo);
    }
}
