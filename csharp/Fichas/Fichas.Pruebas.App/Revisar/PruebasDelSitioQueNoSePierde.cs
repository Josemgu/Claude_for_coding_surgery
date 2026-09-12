using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Marcar un caso en Revisar sin perder el sitio, probado SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Las aserciones salen de sus palabras y de lo medido, no del código.</b> Del dueño,
/// 2026-09-07: <i>«cuando estoy trabajando en Revisar y le das clic a un paquete, te envía al
/// inicio otra vez de Revisar y te coloca todos juntos. Debe permitirme marcar un caso sin
/// moverse, para seguir trabajando con los otros sin revisar»</i>.
/// </para>
/// <para>
/// <b>Qué pasaba de verdad, medido con la ventana abierta el 2026-09-09</b> sobre el paquete
/// publicado, con <c>--falso 30</c>: con la carpeta «Enero 2027» elegida y abierta, el pie
/// decía <i>«viendo 5 de 18 · «Enero 2027» · 28 en la base»</i>; al pulsar «Resuelto» en una
/// tarjeta pasaba a <i>«viendo 17 de 28 documentos»</i>, la rama volvía a <c>Collapsed</c> y
/// ninguna quedaba elegida. Eso es «te coloca todos juntos». El <b>desplazamiento sí se
/// conservaba</b> —las seis primeras tarjetas seguían fuera de la vista antes y después—, así
/// que lo que se pierde es la carpeta y las ramas abiertas, y no la posición.
/// </para>
/// <para>
/// <b>Por qué se prueba aquí y no en la página.</b> La carpeta a la vista era un campo privado
/// de <c>PaginaDeRevisar</c> que <c>PintarLasCarpetas</c> ponía a nulo, donde ninguna prueba
/// llegaba. Ahora la decisión vive en <see cref="MemoriaDelSitio"/> y la página la lee.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelSitioQueNoSePierde
{
    /// <summary>Las claves de un árbol de dos meses, tal como las compone la pantalla.</summary>
    private static readonly string[] ArbolDeSiempre =
    [
        "mes|2026-09",
        "fecha|2026-09|2026-09-12",
        "unidad|2026-09|2026-09-12|325535|Rama San Juan",
        "mes|2027-01",
        "fecha|2027-01|2027-01-04",
    ];

    // ═════════ «Debe permitirme marcar un caso sin moverse» ═════════

    /// <summary>
    /// Dado que estoy mirando una carpeta, cuando se repinta la pantalla, entonces sigo en
    /// esa carpeta y no en todas juntas.
    /// </summary>
    [TestMethod]
    public void TrasRepintarSeVuelveALaMismaCarpetaYNoATodasJuntas()
    {
        var sitio = MemoriaDelSitio.Recordar("fecha|2026-09|2026-09-12", ["mes|2026-09"]);

        var vuelve = MemoriaDelSitio.CarpetaQueVuelve(sitio, ArbolDeSiempre);

        Console.WriteLine("== Se estaba en «{0}» y se vuelve a «{1}» ==", sitio.Carpeta, vuelve);

        Assert.AreEqual("fecha|2026-09|2026-09-12", vuelve, "Se perdió la carpeta y se verían todos juntos.");
    }

    /// <summary>
    /// Dadas las ramas que estaban abiertas, cuando se rehace el árbol, entonces se abren
    /// esas y solo esas.
    /// </summary>
    /// <remarks>
    /// El árbol se rehace de verdad —las cifras de cada carpeta cuentan lo que hay en el
    /// tablero, y tras marcar cambian—, así que lo que se conserva no son los nodos: es qué
    /// estaba abierto.
    /// </remarks>
    [TestMethod]
    public void LasRamasQueEstabanAbiertasSeVuelvenAAbrirYLasDemasNo()
    {
        var sitio = MemoriaDelSitio.Recordar(
            "fecha|2027-01|2027-01-04", ["mes|2027-01", "fecha|2027-01|2027-01-04"]);

        var abiertas = ArbolDeSiempre.Where(c => MemoriaDelSitio.SeAbre(sitio, c, esElPrimerMes: c == "mes|2026-09")).ToList();

        Console.WriteLine("== Se vuelven a abrir: {0} ==", string.Join(", ", abiertas));

        CollectionAssert.AreEquivalent(
            new[] { "mes|2027-01", "fecha|2027-01|2027-01-04" },
            abiertas,
            "El árbol no se abrió por donde estaba, o abrió de más.");
        Assert.IsFalse(
            MemoriaDelSitio.SeAbre(sitio, "mes|2026-09", esElPrimerMes: true),
            "Con un sitio recordado, el primer mes NO se abre solo: eso es volver al principio.");
    }

    /// <summary>
    /// Dadas tres marcas seguidas, cuando se repinta después de cada una, entonces la carpeta
    /// sigue siendo la misma las tres veces.
    /// </summary>
    /// <remarks>
    /// Es lo que él pidió con todas sus palabras: <i>«para seguir trabajando con los otros sin
    /// revisar»</i>. Una sola vez no lo demuestra: lo que rompía era que cada repintado
    /// devolvía la vista al principio.
    /// </remarks>
    [TestMethod]
    public void TresMarcasSeguidasDejanLaCarpetaDondeEstaba()
    {
        var sitio = MemoriaDelSitio.Recordar("mes|2026-09", ["mes|2026-09"]);

        for (var vuelta = 1; vuelta <= 3; vuelta++)
        {
            var vuelve = MemoriaDelSitio.CarpetaQueVuelve(sitio, ArbolDeSiempre);
            Assert.AreEqual("mes|2026-09", vuelve, $"En la marca {vuelta} se perdió la carpeta.");

            // Lo que hace la pantalla tras repintar: vuelve a recordar lo que quedó a la vista.
            sitio = MemoriaDelSitio.Recordar(vuelve, ["mes|2026-09"]);
        }

        Console.WriteLine("== Tras tres marcas seguidas sigue en «{0}» ==", sitio.Carpeta);
    }

    // ═════════ Lo que NO se inventa ═════════

    /// <summary>
    /// Dado que la carpeta donde estaba se quedó sin documentos y desapareció del árbol,
    /// entonces NO se elige otra: se vuelven a ver todos.
    /// </summary>
    /// <remarks>
    /// ⛔ Elegir «la de al lado» sería enseñarle documentos de otro grupo creyendo que son los
    /// suyos. Ver todos es lo que había antes de elegir carpeta, y es verdad.
    /// </remarks>
    [TestMethod]
    public void SiLaCarpetaDesaparecioNoSeInventaOtraYSeVuelveAVerTodo()
    {
        var sitio = MemoriaDelSitio.Recordar("fecha|2026-09|2026-09-12", ["mes|2026-09"]);

        var sinEsaFecha = ArbolDeSiempre.Where(c => c != "fecha|2026-09|2026-09-12").ToArray();
        var vuelve = MemoriaDelSitio.CarpetaQueVuelve(sitio, sinEsaFecha);

        Console.WriteLine("== La carpeta ya no está; se vuelve a «{0}» ==", vuelve ?? "todos los documentos");

        Assert.IsNull(vuelve, "Se eligió una carpeta que no es donde él estaba.");
    }

    /// <summary>
    /// Dado que la pantalla se pinta por primera vez, entonces se abre el primer mes, que es
    /// lo que hacía antes de todo esto.
    /// </summary>
    /// <remarks>
    /// Sin sitio recordado no hay nada que devolver, y el comportamiento de siempre —solo el
    /// primer mes abierto, que es el que viaja antes— no se toca. Con 3 000 documentos abrir
    /// todos construiría miles de filas de golpe.
    /// </remarks>
    [TestMethod]
    public void LaPrimeraVezSeAbreElPrimerMesYNadaMas()
    {
        var sitio = SitioDeRevisar.Ninguno;

        Assert.IsTrue(MemoriaDelSitio.SeAbre(sitio, "mes|2026-09", esElPrimerMes: true));
        Assert.IsFalse(MemoriaDelSitio.SeAbre(sitio, "mes|2027-01", esElPrimerMes: false));
        Assert.IsNull(MemoriaDelSitio.CarpetaQueVuelve(sitio, ArbolDeSiempre));
    }

    // ═════════ Las claves: sin ellas no se puede volver a ninguna parte ═════════

    /// <summary>
    /// Dadas dos carpetas que se LLAMAN igual, entonces sus claves son distintas.
    /// </summary>
    /// <remarks>
    /// ⛔ No es teórico: la carpeta de mes de lo que no tiene fecha y su carpeta de fecha se
    /// llaman las DOS «Sin fecha de viaje» (<see cref="ArbolDeRevisar.SinFecha"/>). Con el
    /// nombre por clave, volver a una devolvería a la otra.
    /// </remarks>
    [TestMethod]
    public void DosCarpetasQueSeLlamanIgualNoTienenLaMismaClave()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("SIN0001", fechaViaje: null, "700001", "Castries Branch");
        banco.Meter("SIN0002", fechaViaje: null, "700001", "Castries Branch");

        var carpetas = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());

        var mes = carpetas[0];
        var fecha = mes.Fechas[0];

        Console.WriteLine("== mes «{0}» clave {1} · fecha «{2}» clave {3} ==",
            mes.Carpeta, MemoriaDelSitio.ClaveDelMes(mes),
            fecha.Carpeta, MemoriaDelSitio.ClaveDeLaFecha(mes, fecha));

        Assert.AreEqual(ArbolDeRevisar.SinFecha, mes.Carpeta);
        Assert.AreEqual(ArbolDeRevisar.SinFecha, fecha.Carpeta);
        Assert.AreNotEqual(
            MemoriaDelSitio.ClaveDelMes(mes),
            MemoriaDelSitio.ClaveDeLaFecha(mes, fecha),
            "El mes y la fecha que se llaman igual comparten clave: volver a una llevaría a la otra.");
    }

    /// <summary>
    /// Dado el mismo tablero cargado dos veces, entonces las claves de sus carpetas son las
    /// mismas: si cambiaran, no se podría volver a ninguna.
    /// </summary>
    [TestMethod]
    public void LasClavesDeLasCarpetasNoCambianDeUnaVezALaOtra()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("SEP0001", "2026-09-12", "325535", "Rama San Juan");
        banco.Meter("SIN0002", fechaViaje: null, "700001", "Castries Branch");

        var primera = ClavesDe(ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()));
        var segunda = ClavesDe(ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()));

        Console.WriteLine("== {0} ==", string.Join(" · ", primera));

        CollectionAssert.AreEqual(primera, segunda, "Las claves cambian entre dos cargas del mismo tablero.");
    }

    /// <summary>Todas las claves de un árbol, en el orden en que se pintan.</summary>
    /// <param name="carpetas">El árbol agrupado, mes por mes.</param>
    private static List<string> ClavesDe(IReadOnlyList<GrupoDeMes> carpetas)
    {
        var claves = new List<string>();
        foreach (var mes in carpetas)
        {
            claves.Add(MemoriaDelSitio.ClaveDelMes(mes));
            foreach (var fecha in mes.Fechas)
            {
                claves.Add(MemoriaDelSitio.ClaveDeLaFecha(mes, fecha));
                foreach (var unidad in fecha.Unidades)
                {
                    claves.Add(MemoriaDelSitio.ClaveDeLaUnidad(mes, fecha, unidad));
                }
            }
        }

        return claves;
    }
}
