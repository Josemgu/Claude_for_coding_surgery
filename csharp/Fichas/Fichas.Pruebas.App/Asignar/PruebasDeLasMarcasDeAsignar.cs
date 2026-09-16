using Fichas.App.Asignar;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Las marcas de Asignar sobreviven a buscar, a borrar la búsqueda y a repintar la lista.
/// </summary>
/// <remarks>
/// <para>Nacen del dueño, 2026-09-16: <i>«Si yo busco el nombre de una persona, lo marco
/// para poder asignarlo en grupo, y elimino el nombre de búsqueda para buscar a otra
/// persona, el sistema desmarca a las personas que yo ya había marcado»</i>. Medido en el
/// código ese día: las marcas vivían solo en <c>ItemsView.SelectedItems</c>, y cada tecleo
/// en el buscador ponía una lista nueva, que nace sin marcas.</para>
///
/// <para>Lo que se mide aquí es el conjunto que vive APARTE de la lista: qué ids están
/// marcados, cuántos de ellos están a la vista, y cómo se dice. La pantalla solo lo
/// enchufa a la lista.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasMarcasDeAsignar
{
    /// <summary>
    /// Dados 2 marcados, cuando se busca otro nombre, se marca 1 y se borra la búsqueda,
    /// entonces hay 3 marcados. Es el gesto exacto del dueño.
    /// </summary>
    [TestMethod]
    public void MarcarDosBuscarOtroMarcarUnoYBorrarLaBusquedaDejaTresMarcados()
    {
        var marcas = new MarcasDeAsignar();

        marcas.Marcar(11);
        marcas.Marcar(12);
        marcas.AlRepintar([31, 32]);      // se buscó otro nombre: la lista cambia, las marcas no
        marcas.Marcar(31);
        marcas.AlRepintar([11, 12, 31, 32, 40]); // se borró la búsqueda

        Assert.AreEqual(3, marcas.Cuantas);
        CollectionAssert.AreEquivalent(new long[] { 11, 12, 31 }, marcas.Ids.ToList());
    }

    /// <summary>Al repintar, los renglones marcados que vuelven a salir se dicen para volver a marcarlos.</summary>
    [TestMethod]
    public void AlRepintarSeDiceQueRenglonesDeLaVistaHayQueVolverAMarcar()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.Marcar(12);
        marcas.Marcar(31);

        var aMarcar = marcas.AlRepintar([12, 31, 40]);

        CollectionAssert.AreEquivalent(new long[] { 12, 31 }, aMarcar.ToList());
        Assert.AreEqual(2, marcas.CuantasALaVista);
        Assert.AreEqual(1, marcas.CuantasFueraDeLaVista);
    }

    /// <summary>Desmarcar un renglón a la vista lo saca del conjunto; los demás siguen.</summary>
    [TestMethod]
    public void DesmarcarSacaSoloEseDelConjunto()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.Marcar(12);

        marcas.Desmarcar(11);

        CollectionAssert.AreEquivalent(new long[] { 12 }, marcas.Ids.ToList());
    }

    /// <summary>«Quitar las marcas» vacía el conjunto entero, también lo que no está a la vista.</summary>
    [TestMethod]
    public void QuitarLasMarcasVaciaTambienLoQueNoEstaALaVista()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.Marcar(12);
        marcas.AlRepintar([12]);

        marcas.QuitarTodas();

        Assert.AreEqual(0, marcas.Cuantas);
        Assert.AreEqual(0, marcas.CuantasFueraDeLaVista);
    }

    /// <summary>Marcar dos veces el mismo no lo cuenta dos veces.</summary>
    [TestMethod]
    public void MarcarDosVecesElMismoCuentaUna()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.Marcar(11);

        Assert.AreEqual(1, marcas.Cuantas);
    }

    /// <summary>Sin marcas, la línea invita a marcar y nombra Ctrl+A.</summary>
    [TestMethod]
    public void SinMarcasLaLineaInvitaAMarcar()
    {
        var marcas = new MarcasDeAsignar();

        StringAssert.Contains(marcas.Dicho("Sandy Inventada"), "Ctrl+A");
    }

    /// <summary>Con todas a la vista, la línea dice cuántas y a quién irían, sin hablar de fuera de la vista.</summary>
    [TestMethod]
    public void ConTodasALaVistaLaLineaNoHablaDeFueraDeLaVista()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.AlRepintar([11, 12]);

        var linea = marcas.Dicho("Sandy Inventada");

        Assert.AreEqual("1 marcado · iría a Sandy Inventada", linea);
    }

    /// <summary>Con marcas fuera de la vista, la línea lo dice con la cifra: «7 marcados, 3 fuera de la vista».</summary>
    [TestMethod]
    public void ConMarcasFueraDeLaVistaLaLineaLoDiceConLaCifra()
    {
        var marcas = new MarcasDeAsignar();
        foreach (var id in new long[] { 1, 2, 3, 4, 5, 6, 7 }) marcas.Marcar(id);
        marcas.AlRepintar([1, 2, 3, 4]);

        var linea = marcas.Dicho("Sandy Inventada");

        Assert.AreEqual("7 marcados, 3 fuera de la vista · irían a Sandy Inventada", linea);
    }

    /// <summary>Sin compañero elegido, la línea lo dice en vez de decir «a nadie» a secas.</summary>
    [TestMethod]
    public void SinDestinoLaLineaPideElegirUnCompanero()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(11);
        marcas.AlRepintar([11]);

        StringAssert.Contains(marcas.Dicho(null), "elige un compañero");
    }

    /// <summary>Ctrl+A con parte de la vista marcada marca lo que se ve; con todo marcado, lo desmarca (solo lo que se ve).</summary>
    [TestMethod]
    public void CtrlAMarcaLoQueSeVeYSiYaEstabaTodoLoDesmarcaSinTocarLoDeFuera()
    {
        var marcas = new MarcasDeAsignar();
        marcas.Marcar(99);            // fuera de la vista
        marcas.AlRepintar([1, 2, 3]);

        marcas.MarcarODesmarcarLaVista();
        CollectionAssert.AreEquivalent(new long[] { 99, 1, 2, 3 }, marcas.Ids.ToList());

        marcas.MarcarODesmarcarLaVista();
        CollectionAssert.AreEquivalent(new long[] { 99 }, marcas.Ids.ToList());
    }
}
