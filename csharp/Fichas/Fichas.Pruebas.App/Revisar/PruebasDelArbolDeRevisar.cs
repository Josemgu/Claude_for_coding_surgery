using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Las pruebas de la agrupación por carpetas de la pantalla de Revisar: mes, fecha de
/// viaje y unidad, en ese orden y con lo que viaja antes arriba.
/// </summary>
/// <remarks>
/// <para>Nacen de las palabras del dueño del 2026-09-05 y NO del código: «una carpeta con
/// el mes, dentro carpetas con "grupo de septiembre 17", "grupo de septiembre 2", y dentro
/// de las carpetas otras carpetas con el nombre y número de la unidad, y dentro el paquete
/// de personas de la unidad que viajará».</para>
///
/// <para>Y el porqué, que es lo que decide el orden: «no quiero revisar gente que viaja en
/// noviembre estando en septiembre» y «la prioridad son los que viajarán pronto».</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelArbolDeRevisar
{
    /// <summary>Agrupa por mes, y el mes que viaja antes va arriba.</summary>
    [TestMethod]
    public void AgrupaPorMesYElQueViajaAntesVaArriba()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-11-04", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0003", "2026-10-02", "700001", "Castries Branch");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());

        Assert.HasCount(3, meses);
        Assert.AreEqual("Septiembre 2026", meses[0].Carpeta, "Septiembre primero: es lo que viaja antes.");
        Assert.AreEqual("Octubre 2026", meses[1].Carpeta);
        Assert.AreEqual("Noviembre 2026", meses[2].Carpeta);
    }

    /// <summary>Dentro del mes, una carpeta por fecha de viaje, y la más cercana arriba.</summary>
    [TestMethod]
    public void DentroDelMesUnaCarpetaPorFechaYLaMasCercanaArriba()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-02", "700001", "Castries Branch");

        var septiembre = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        Assert.HasCount(2, septiembre.Fechas);
        Assert.AreEqual("Grupo del 2 de septiembre", septiembre.Fechas[0].Carpeta);
        Assert.AreEqual("Grupo del 17 de septiembre", septiembre.Fechas[1].Carpeta);
    }

    /// <summary>Dentro de la fecha, la carpeta de la unidad lleva su número Y su nombre.</summary>
    [TestMethod]
    public void LaCarpetaDeLaUnidadLlevaSuNumeroYSuNombre()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700020", "Rama Los Alcarrizos");

        var fecha = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single().Fechas.Single();

        Assert.HasCount(2, fecha.Unidades);
        CollectionAssert.AreEquivalent(
            new[] { "700020 · Rama Los Alcarrizos", "700001 · Castries Branch" },
            fecha.Unidades.Select(u => u.Carpeta).ToArray());
    }

    /// <summary>Los documentos de una unidad quedan dentro de su carpeta, y solo esos.</summary>
    [TestMethod]
    public void LosDocumentosQuedanDentroDeLaCarpetaDeSuUnidad()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0003", "2026-09-17", "700020", "Rama Los Alcarrizos");

        var fecha = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single().Fechas.Single();
        var castries = fecha.Unidades.Single(u => u.Numero == "700001");

        Assert.HasCount(2, castries.Documentos);
        CollectionAssert.AreEquivalent(
            new[] { "AAAA0001", "AAAA0002" },
            castries.Documentos.Select(d => d.NumeroDeCaso).ToArray());
    }

    /// <summary>
    /// Cada grupo dice cuántos documentos tiene, y las cuentas cuadran de abajo arriba.
    /// </summary>
    /// <remarks>
    /// Es la mitad del criterio: «el número de documentos de cada grupo cuadra con la base,
    /// con el denominador dicho». Si un grupo miente en su cifra, la pantalla miente.
    /// </remarks>
    [TestMethod]
    public void LasCuentasDeCadaGrupoCuadranDeAbajoArriba()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0003", "2026-09-02", "700020", "Rama Los Alcarrizos");
        banco.Meter("AAAA0004", "2026-10-02", "700020", "Rama Los Alcarrizos");

        var tarjetas = banco.ComoLoVeLaPantalla();
        var meses = ArbolDeRevisar.Agrupar(tarjetas);

        Assert.HasCount(4, tarjetas, "El denominador: cuatro documentos en la base.");
        Assert.AreEqual(4, meses.Sum(m => m.CuantosDocumentos), "Los meses suman los cuatro.");
        foreach (var mes in meses)
        {
            Assert.AreEqual(mes.Fechas.Sum(f => f.CuantosDocumentos), mes.CuantosDocumentos);
            foreach (var fecha in mes.Fechas)
            {
                Assert.AreEqual(fecha.Unidades.Sum(u => u.Documentos.Count), fecha.CuantosDocumentos);
            }
        }
    }

    /// <summary>
    /// Lo que no tiene fecha de viaje tiene su propia carpeta y va al FINAL.
    /// </summary>
    /// <remarks>
    /// No se inventa una fecha ni se cuela en el mes de al lado (regla permanente 1). Y va
    /// al final porque el orden lo manda la prioridad del dueño —«los que viajarán pronto»—
    /// y de lo que no tiene fecha no se sabe si es pronto.
    /// </remarks>
    [TestMethod]
    public void LoQueNoTieneFechaDeViajeVaEnSuPropioGrupoYAlFinal()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", null, "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());

        Assert.HasCount(2, meses);
        Assert.AreEqual("Septiembre 2026", meses[0].Carpeta);
        Assert.AreEqual(ArbolDeRevisar.SinFecha, meses[1].Carpeta, "Lo que no tiene fecha, al final.");
        Assert.AreEqual(ArbolDeRevisar.SinFecha, meses[1].Fechas.Single().Carpeta);
    }

    /// <summary>Una fecha que no se puede leer NO tumba el agrupado: cae en «sin fecha».</summary>
    [TestMethod]
    public void UnaFechaIlegibleCaeEnSinFechaYNoTumbaNada()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "no es una fecha", "700001", "Castries Branch");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());

        Assert.AreEqual(ArbolDeRevisar.SinFecha, meses.Single().Carpeta);
    }

    /// <summary>Una unidad sin número ni nombre se dice, no se inventa.</summary>
    [TestMethod]
    public void UnaUnidadSinNumeroNiNombreSeDiceYNoSeInventa()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", null, null);

        var unidad = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla())
            .Single().Fechas.Single().Unidades.Single();

        Assert.AreEqual(ArbolDeRevisar.SinUnidad, unidad.Carpeta);
        Assert.AreEqual(string.Empty, unidad.Numero);
    }

    /// <summary>
    /// Un caso archivado NO aparece en la pantalla de Revisar.
    /// </summary>
    /// <remarks>
    /// Del dueño, 2026-09-05: «cuando yo archive, debe salir del sistema visible, pero se
    /// queda como histórico para los reportes». Lo segundo lo prueba
    /// <see cref="ArchivarSigueSinBorrarNadaDeLaBase"/>: aquí solo se mide que sale de la
    /// vista.
    /// </remarks>
    [TestMethod]
    public void UnCasoArchivadoNoAparece()
    {
        var banco = new BancoDeCarpetas();
        var visible = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        var archivado = banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");
        banco.Acciones.ArchivarEnLote([archivado]);

        var tarjetas = banco.ComoLoVeLaPantalla();
        var meses = ArbolDeRevisar.Agrupar(tarjetas);

        Assert.HasCount(1, tarjetas, "Uno de los dos, y el archivado no.");
        Assert.AreEqual(visible, tarjetas[0].CasoId);
        Assert.AreEqual(1, meses.Single().CuantosDocumentos);
        Assert.IsNull(banco.Tablero.De(archivado), "El archivado no está ni en el tablero.");
    }

    /// <summary>Archivar sigue sin borrar: el caso sigue entero en la base para los reportes.</summary>
    [TestMethod]
    public void ArchivarSigueSinBorrarNadaDeLaBase()
    {
        var banco = new BancoDeCarpetas();
        var archivado = banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");

        banco.Acciones.ArchivarEnLote([archivado]);

        var enLaBase = banco.Servicios.Casos.Obtener(archivado);
        Assert.IsNotNull(enLaBase, "Sigue en la base: archivar no borra.");
        Assert.IsTrue(enLaBase.Archivado);
        Assert.AreEqual(banco.Reloj.Hoy(), enLaBase.FechaArchivado, "Con la fecha en la que se archivó.");
    }

    /// <summary>
    /// Con el interruptor de «ver los archivados» puesto, el archivado vuelve a la vista.
    /// </summary>
    /// <remarks>
    /// Existe porque desarchivar tiene que ser posible y para desarchivar algo hay que poder
    /// marcarlo antes. Va apagado por defecto, que es la regla del dueño.
    /// </remarks>
    [TestMethod]
    public void ConElInterruptorPuestoElArchivadoSeVuelveAVer()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Acciones.ArchivarEnLote([caso]);

        banco.Tablero.Cargar(conArchivados: true);

        Assert.HasCount(1, banco.Tablero.Todas(FiltroDeTarjeta.Todo));
        Assert.IsTrue(banco.Tablero.De(caso)!.Archivado);
        Assert.AreEqual(1, ArbolDeRevisar.Agrupar(banco.Tablero.Todas(FiltroDeTarjeta.Todo))
            .Single().CuantosDocumentos, "Y sale en su carpeta, como cualquier otro.");
    }

    /// <summary>Desarchivar lo devuelve a la vista; es la vuelta atrás y ya existía.</summary>
    [TestMethod]
    public void DesarchivarLoDevuelveALaVista()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Acciones.ArchivarEnLote([caso]);
        Assert.IsEmpty(banco.ComoLoVeLaPantalla(), "Archivado, no se ve.");

        banco.Acciones.DesarchivarEnLote([caso]);

        Assert.HasCount(1, banco.ComoLoVeLaPantalla(), "Desarchivado, vuelve.");
    }

    /// <summary>La etiqueta de cada carpeta lleva su cifra y cabe en un renglón.</summary>
    [TestMethod]
    public void LaEtiquetaDeCadaCarpetaLlevaSuCifraYCabeEnUnRenglon()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");

        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        Assert.AreEqual("Septiembre 2026 · 2 documentos", mes.Etiqueta);
        Assert.AreEqual("Grupo del 17 de septiembre · 2 documentos", mes.Fechas[0].Etiqueta);
        Assert.AreEqual("700001 · Castries Branch · 2 documentos", mes.Fechas[0].Unidades[0].Etiqueta);
        Assert.DoesNotContain("\n", mes.Etiqueta, "Ni un párrafo: una línea.");
    }

    /// <summary>Con 3 000 documentos el agrupado cabe de sobra en dos décimas.</summary>
    /// <remarks>
    /// La cifra del dueño es 3 000, y el techo de la pantalla son 0,2 s. Aquí se mide LA
    /// REGLA, no el pintado: el pintado se mide con la ventana abierta y esa cifra no sale
    /// de esta prueba.
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosElAgrupadoCabeEnDosDecimas()
    {
        var banco = new BancoDeCarpetas();
        for (var i = 0; i < 3000; i++)
        {
            banco.Meter($"CCCC{i:D4}", $"2026-{9 + (i % 3):D2}-{1 + (i % 28):D2}", $"70002{i % 50:D2}", $"Unidad {i % 50}");
        }
        var tarjetas = banco.ComoLoVeLaPantalla();
        ArbolDeRevisar.Agrupar(tarjetas);   // calentamiento

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var meses = ArbolDeRevisar.Agrupar(tarjetas);
        cronometro.Stop();

        Assert.AreEqual(3000, meses.Sum(m => m.CuantosDocumentos), "Los 3 000, sin perder ni uno.");
        Console.WriteLine($"Revisar · agrupar 3 000 documentos: {cronometro.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThanOrEqualTo(200.0, cronometro.Elapsed.TotalMilliseconds);
    }

    /// <summary>El buscador y el agrupado se combinan: se agrupa lo que quedó del buscador.</summary>
    [TestMethod]
    public void ElBuscadorYElAgrupadoSeCombinan()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("BBBB0002", "2026-10-02", "700020", "Rama Los Alcarrizos");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla("AAAA0001"));

        Assert.AreEqual("Septiembre 2026", meses.Single().Carpeta);
        Assert.AreEqual(1, meses.Single().CuantosDocumentos);
    }

    /// <summary>Los doce meses se dicen en español y con su tilde donde toca.</summary>
    [TestMethod]
    public void LosDoceMesesSeDicenEnEspanol()
    {
        var esperados = new[]
        {
            "Enero 2026", "Febrero 2026", "Marzo 2026", "Abril 2026", "Mayo 2026", "Junio 2026",
            "Julio 2026", "Agosto 2026", "Septiembre 2026", "Octubre 2026", "Noviembre 2026",
            "Diciembre 2026",
        };
        var banco = new BancoDeCarpetas();
        for (var mes = 1; mes <= 12; mes++) banco.Meter($"MMMM{mes:D4}", $"2026-{mes:D2}-15", "700001", "Rama");

        var carpetas = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Select(m => m.Carpeta).ToArray();

        CollectionAssert.AreEqual(esperados, carpetas);
    }

    /// <summary>Un documento marcado completa sigue estando en su carpeta: agrupar no filtra.</summary>
    [TestMethod]
    public void AgruparNoFiltraPorEstado()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");
        var quien = banco.Servicios.Companeros.Activos()[0];
        banco.Servicios.Casos.MarcarEstado(caso, EstadoDeRecomendacion.Completa, quien.Id, "hoja devuelta");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());

        Assert.AreEqual(1, meses.Single().CuantosDocumentos);
    }
}
