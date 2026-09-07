using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Las pruebas de la sección que pide mirar los documentos sin fecha, y de poder darles
/// esa fecha desde la misma pantalla de Revisar.
/// </summary>
/// <remarks>
/// <para>Nacen de las palabras del dueño del 2026-09-05, petición 6, y NO del código:
/// «colocarlo todo en Revisar me hace el trabajo difícil: debes agruparlo por fecha, y lo
/// que no se reconozca la fecha, ahí mismo en revisión, en una sección que diga "revisar
/// este documento" para que se pueda ir a una fecha».</para>
///
/// <para>La primera mitad —agrupar por fecha— ya estaba y la miden
/// <see cref="PruebasDelArbolDeRevisar"/>. Aquí se mide lo que faltaba: que esa sección
/// <b>llame a mirarla</b>, y que desde ella se pueda <b>ir a una fecha</b> sin salir de la
/// pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaFechaDesdeRevisar
{
    /// <summary>El grupo de lo que no tiene fecha pide que se mire, con esas palabras.</summary>
    [TestMethod]
    public void LaSeccionDeLoQueNoTieneFechaPideQueSeMire()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", null, "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");

        var meses = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());
        var sinFecha = meses[1];

        Assert.IsTrue(sinFecha.HayQueRevisarlo, "La sección de lo que no tiene fecha pide que se mire.");
        Assert.IsFalse(meses[0].HayQueRevisarlo, "La de septiembre no: esa ya tiene su fecha.");
        Assert.StartsWith(ArbolDeRevisar.LlamadaARevisar, sinFecha.Etiqueta);
        Assert.Contains("1 documento", sinFecha.Etiqueta, "Y dice cuántos hay que mirar.");
        Assert.DoesNotContain("\n", sinFecha.Etiqueta, "Ni un párrafo: una línea.");
    }

    /// <summary>Dentro de la sección, la carpeta de fecha también lo pide.</summary>
    [TestMethod]
    public void DentroDeLaSeccionLaCarpetaDeFechaTambienLoPide()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", null, "700001", "Castries Branch");

        var fecha = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single().Fechas.Single();

        Assert.IsTrue(fecha.HayQueRevisarlo);
        Assert.StartsWith(ArbolDeRevisar.LlamadaARevisar, fecha.Etiqueta);
    }

    /// <summary>
    /// El nombre que se vuelca al disco NO cambia: la llamada es de la pantalla.
    /// </summary>
    /// <remarks>
    /// La etiqueta es lo que se lee en el árbol; <c>Carpeta</c> es lo que
    /// <see cref="PlanDeVolcado"/> escribe en el disco. Meter una llamada a la acción en el
    /// nombre de una carpeta del escritorio del dueño sería otra cosa, y no la pidió.
    /// </remarks>
    [TestMethod]
    public void ElNombreDeLaCarpetaDelDiscoNoCambia()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("AAAA0001", null, "700001", "Castries Branch");

        var mes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        Assert.AreEqual(ArbolDeRevisar.SinFecha, mes.Carpeta);
        Assert.AreEqual(ArbolDeRevisar.SinFecha, mes.Fechas.Single().Carpeta);
    }

    /// <summary>
    /// Desde la sección se le da una fecha y el documento pasa al grupo de ese día.
    /// </summary>
    /// <remarks>
    /// Es el criterio de cierre entero, con las dos cuentas antes y después: «desde ahí le
    /// pongo una fecha y pasa al grupo de ese día, con el conteo de los dos grupos antes y
    /// después».
    /// </remarks>
    [TestMethod]
    public void AlPonerleLaFechaPasaAlGrupoDeEseDiaYLasDosCuentasCambian()
    {
        var banco = new BancoDeCarpetas();
        var sinFecha = banco.Meter("AAAA0001", null, "700001", "Castries Branch");
        banco.Meter("AAAA0002", "2026-09-17", "700001", "Castries Branch");

        var antes = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());
        Assert.AreEqual(1, antes.Single(m => m.HayQueRevisarlo).CuantosDocumentos, "Antes: 1 sin fecha.");
        Assert.AreEqual(1, antes.Single(m => !m.HayQueRevisarlo).CuantosDocumentos, "Antes: 1 el 17.");

        var puesta = banco.Acciones.PonerLaFechaDeViaje(sinFecha, "2026-09-17");

        Assert.IsTrue(puesta.SeEscribio, "La fecha se escribió.");
        var despues = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla());
        Assert.IsEmpty(despues.Where(m => m.HayQueRevisarlo), "Después: la sección de revisar se vació.");
        var septiembre = despues.Single();
        Assert.AreEqual(2, septiembre.CuantosDocumentos, "Después: los 2 en septiembre.");
        Assert.AreEqual("Grupo del 17 de septiembre", septiembre.Fechas.Single().Carpeta);
        Assert.AreEqual(2, septiembre.Fechas.Single().CuantosDocumentos, "Después: 2 el 17.");
    }

    /// <summary>La fecha queda escrita en la base, no solo en la pantalla.</summary>
    [TestMethod]
    public void LaFechaQuedaEscritaEnLaBase()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", null, "700001", "Castries Branch");

        banco.Acciones.PonerLaFechaDeViaje(caso, "2026-09-17");

        Assert.AreEqual("2026-09-17", banco.Servicios.Casos.Obtener(caso)!.FechaViaje);
    }

    /// <summary>
    /// Una fecha que no se puede leer NO se escribe: no se adivina nada.
    /// </summary>
    /// <remarks>
    /// Regla permanente 1. «17 de septiembre», «9/17/26» o un día que no existe no se
    /// interpretan: se dice que no se escribió y el documento se queda donde estaba.
    /// </remarks>
    [TestMethod]
    [DataRow("17 de septiembre")]
    [DataRow("9/17/26")]
    [DataRow("2026-02-31")]
    [DataRow("2026-9-17")]
    [DataRow("")]
    [DataRow("   ")]
    public void UnaFechaQueNoSePuedeLeerNoSeEscribe(string loQueSeEscribio)
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", null, "700001", "Castries Branch");

        var puesta = banco.Acciones.PonerLaFechaDeViaje(caso, loQueSeEscribio);

        Assert.IsFalse(puesta.SeEscribio, $"«{loQueSeEscribio}» no es una fecha y no se escribe.");
        Assert.IsTrue(puesta.HayAvisos, "Y se dice por qué, en la franja.");
        Assert.IsNull(banco.Servicios.Casos.Obtener(caso)!.FechaViaje, "El documento se queda sin fecha.");
        Assert.IsTrue(
            ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single().HayQueRevisarlo,
            "Y sigue en la sección que pide mirarlo.");
    }

    /// <summary>Un documento que ya no está en la base se dice, no se calla.</summary>
    [TestMethod]
    public void UnDocumentoQueYaNoEstaSeDice()
    {
        var banco = new BancoDeCarpetas();

        var puesta = banco.Acciones.PonerLaFechaDeViaje(9999, "2026-09-17");

        Assert.IsFalse(puesta.SeEscribio);
        Assert.IsTrue(puesta.HayAvisos);
    }

    /// <summary>
    /// Corregir la fecha de uno que ya la tenía lo mueve de grupo; es el mismo gesto.
    /// </summary>
    /// <remarks>
    /// No es una función aparte: el dueño corrige fechas mal leídas igual que rellena las
    /// que faltan, y partir eso en dos caminos daría dos comportamientos que mantener.
    /// </remarks>
    [TestMethod]
    public void CorregirLaFechaDeUnoQueYaLaTeniaLoMueveDeGrupo()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");

        Assert.IsTrue(banco.Acciones.PonerLaFechaDeViaje(caso, "2026-09-02").SeEscribio);

        var septiembre = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();
        Assert.AreEqual("Grupo del 2 de septiembre", septiembre.Fechas.Single().Carpeta);
    }

    /// <summary>Poner la misma fecha que ya tenía no escribe nada y tampoco es un fallo.</summary>
    [TestMethod]
    public void PonerLaMismaFechaNoEscribeNadaYNoEsUnFallo()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", "2026-09-17", "700001", "Castries Branch");

        var puesta = banco.Acciones.PonerLaFechaDeViaje(caso, "2026-09-17");

        Assert.IsTrue(puesta.SeEscribio, "Se da por hecha: la fecha que se pidió es la que hay.");
        Assert.AreEqual("2026-09-17", banco.Servicios.Casos.Obtener(caso)!.FechaViaje);
    }

    /// <summary>
    /// Poner una fecha ya pasada se escribe igual, y el documento cae en su tablero.
    /// </summary>
    /// <remarks>
    /// Requisito 9 del dueño, «avisar, nunca impedir»: si el papel dice una fecha que ya
    /// pasó, eso es un dato y se guarda; lo que hace la pantalla es enseñarlo en el tablero
    /// «Fecha pasada», que ya existía.
    /// </remarks>
    [TestMethod]
    public void UnaFechaYaPasadaSeEscribeYCaeEnSuTablero()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("AAAA0001", null, "700001", "Castries Branch");

        Assert.IsTrue(banco.Acciones.PonerLaFechaDeViaje(caso, "2026-08-01").SeEscribio);

        banco.ComoLoVeLaPantalla();
        Assert.IsTrue(banco.Tablero.De(caso)!.FechaYaPasada, "Se guarda y se avisa; no se impide.");
    }
}
