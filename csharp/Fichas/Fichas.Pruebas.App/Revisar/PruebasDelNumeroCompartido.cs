using Fichas.App.Asignar;
using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Las pruebas de ver que varios documentos comparten el número de caso, que es una pista
/// de que viajan en el mismo grupo.
/// </summary>
/// <remarks>
/// <para>Nacen de las palabras del dueño del 2026-09-05, petición 11, y NO del código:
/// «documentos que tienen el número de caso iguales puede significar que viajarán en el
/// mismo grupo; es importante saber esto».</para>
///
/// <para>⛔ <b>Esto NO deshace la decisión del 2026-09-03</b>, y la diferencia es toda la
/// entrega: el número de caso son cuatro letras de unidad más el año y el mes, así que
/// <b>no identifica a una familia</b> y por eso se le quitó la unicidad. Sigue sin ser
/// identidad: aquí solo se <b>enseña</b> que se repite. Ni junta documentos en un caso, ni
/// los marca duplicados, ni cambia el árbol de carpetas — y cada una de esas tres cosas
/// tiene su prueba abajo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelNumeroCompartido
{
    /// <summary>Los siete escaneos del dueño comparten CASP2609, y la tarjeta lo dice.</summary>
    [TestMethod]
    public void LosSieteQueComparteNumeroLoDicenYDicenCuantosSon()
    {
        var banco = new BancoDeCarpetas();
        for (var i = 1; i <= 7; i++) banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", $@"C:\pdf\{i}.pdf");

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.HasCount(7, tarjetas);
        foreach (var tarjeta in tarjetas)
        {
            Assert.AreEqual(7, tarjeta.CuantosCompartenElNumero, "Los siete se cuentan a sí mismos y a los otros seis.");
            Assert.IsTrue(tarjeta.CompartenElNumero);
            Assert.Contains("7 documentos", tarjeta.LineaDelNumeroCompartido);
            Assert.Contains("mismo grupo", tarjeta.LineaDelNumeroCompartido, "Es una pista de grupo de viaje.");
            Assert.DoesNotContain("\n", tarjeta.LineaDelNumeroCompartido, "Ni un párrafo: una línea.");
        }
    }

    /// <summary>
    /// Compartir número NO los junta en un caso: siguen siendo siete documentos distintos.
    /// </summary>
    /// <remarks>
    /// Es la mitad del criterio que protege la decisión del 2026-09-03: «sin que eso los
    /// mezcle en un mismo caso».
    /// </remarks>
    [TestMethod]
    public void CompartirNumeroNoLosJuntaEnUnCaso()
    {
        var banco = new BancoDeCarpetas();
        for (var i = 1; i <= 7; i++) banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", $@"C:\pdf\{i}.pdf");

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.HasCount(7, tarjetas.Select(t => t.CasoId).Distinct().ToList(), "Siete números internos distintos.");
        Assert.AreEqual(7, banco.Tablero.Total);
    }

    /// <summary>Compartir número NO es ser duplicado: son dos cosas y se dicen aparte.</summary>
    [TestMethod]
    public void CompartirNumeroNoEsSerDuplicado()
    {
        var banco = new BancoDeCarpetas();
        for (var i = 1; i <= 7; i++) banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", $@"C:\pdf\{i}.pdf");

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.IsEmpty(tarjetas.Where(t => t.EsDuplicado), "Ninguno se marca duplicado por compartir número.");
        Assert.IsEmpty(tarjetas.Where(t => t.MarcaDeDuplicado.Length > 0));
    }

    /// <summary>Con números distintos no se agrupan: ninguna tarjeta lleva la línea.</summary>
    [TestMethod]
    public void ConNumerosDistintosNoSeAgrupan()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("PARB2609", "2026-09-17", "700001", "Castries Branch");
        banco.Meter("SURB2609", "2026-09-17", "700001", "Castries Branch");

        var tarjetas = banco.ComoLoVeLaPantalla();

        foreach (var tarjeta in tarjetas)
        {
            Assert.AreEqual(1, tarjeta.CuantosCompartenElNumero, "Cada uno está solo con su número.");
            Assert.IsFalse(tarjeta.CompartenElNumero);
            Assert.AreEqual(string.Empty, tarjeta.LineaDelNumeroCompartido, "Sin nadie con quien compartir, no hay línea.");
        }
    }

    /// <summary>
    /// Los que no traen número NO se agrupan entre ellos: «sin número» no es un número.
    /// </summary>
    /// <remarks>
    /// Sin esta guarda, todos los papeles cuyo número no se leyó saldrían diciendo que
    /// viajan juntos, que es exactamente la clase de dato inventado que prohíbe la regla
    /// permanente 1.
    /// </remarks>
    [TestMethod]
    public void LosQueNoTraenNumeroNoSeAgrupanEntreEllos()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter(null, "2026-09-17", "700001", "Castries Branch", @"C:\pdf\a.pdf");
        banco.Meter(null, "2026-09-17", "700001", "Castries Branch", @"C:\pdf\b.pdf");

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.HasCount(2, tarjetas);
        foreach (var tarjeta in tarjetas)
        {
            Assert.AreEqual(RenglonParaAsignar.SinNumero, tarjeta.NumeroDeCaso);
            Assert.IsFalse(tarjeta.CompartenElNumero, "«Sin número de caso» no es un número compartido.");
            Assert.AreEqual(string.Empty, tarjeta.LineaDelNumeroCompartido);
        }
    }

    /// <summary>Las mayúsculas y los espacios del papel no parten un grupo en dos.</summary>
    [TestMethod]
    public void LasMayusculasYLosEspaciosNoPartenElGrupo()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\a.pdf");
        banco.Meter(" casp2609 ", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\b.pdf");

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.IsTrue(tarjetas.All(t => t.CuantosCompartenElNumero == 2), "Es el mismo número escrito de dos formas.");
    }

    /// <summary>Compartir número NO cambia el árbol: se sigue agrupando por fecha y unidad.</summary>
    [TestMethod]
    public void CompartirNumeroNoCambiaElArbolDeCarpetas()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\a.pdf");
        banco.Meter("CASP2609", "2026-09-02", "700020", "Rama Los Alcarrizos", @"C:\pdf\b.pdf");

        var septiembre = ArbolDeRevisar.Agrupar(banco.ComoLoVeLaPantalla()).Single();

        Assert.HasCount(2, septiembre.Fechas, "Mismo número y días distintos: dos carpetas, no una.");
        Assert.AreEqual("Grupo del 2 de septiembre", septiembre.Fechas[0].Carpeta);
        Assert.AreEqual("Grupo del 17 de septiembre", septiembre.Fechas[1].Carpeta);
    }

    /// <summary>
    /// Con el buscador puesto la cifra sigue siendo la de la base, no la de lo que se ve.
    /// </summary>
    /// <remarks>
    /// ⛔ Es lo que hace que la cifra no mienta. Buscando por el nombre de una familia queda
    /// una sola tarjeta a la vista, y contar sobre lo visible diría «1 documento con este
    /// número» de siete que hay. Un número falso en pantalla es peor que ninguno.
    /// </remarks>
    [TestMethod]
    public void ConElBuscadorPuestoLaCifraSigueSiendoLaDeLaBase()
    {
        var banco = new BancoDeCarpetas();
        for (var i = 1; i <= 7; i++)
        {
            var caso = banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", $@"C:\pdf\{i}.pdf");
            banco.MeterPersona(caso, i == 1 ? "Ana Prueba" : $"Familia {i}", $"055-1111-385{i}");
        }

        var tarjetas = banco.ComoLoVeLaPantalla("Ana Prueba");

        Assert.HasCount(1, tarjetas, "El buscador deja una a la vista.");
        Assert.AreEqual(7, tarjetas[0].CuantosCompartenElNumero, "Y sigue diciendo que son siete los que comparten.");
    }

    /// <summary>Un archivado deja de contar en el grupo, porque deja de estar a la vista.</summary>
    /// <remarks>
    /// Del dueño, 2026-09-05: «cuando yo archive, debe salir del sistema visible». Si
    /// siguiera contando, la tarjeta diría «7 documentos» y en la pantalla habría seis.
    /// </remarks>
    [TestMethod]
    public void UnArchivadoDejaDeContarEnElGrupo()
    {
        var banco = new BancoDeCarpetas();
        var ids = new List<long>();
        for (var i = 1; i <= 7; i++) ids.Add(banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", $@"C:\pdf\{i}.pdf"));
        banco.Acciones.ArchivarEnLote([ids[0]]);

        var tarjetas = banco.ComoLoVeLaPantalla();

        Assert.HasCount(6, tarjetas);
        Assert.IsTrue(tarjetas.All(t => t.CuantosCompartenElNumero == 6), "Seis a la vista, seis dice la tarjeta.");
    }

    /// <summary>Dos que comparten número dicen «2 documentos», en plural y bien escrito.</summary>
    [TestMethod]
    public void DosQueComparteNumeroLoDicenEnPlural()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\a.pdf");
        banco.Meter("CASP2609", "2026-09-17", "700001", "Castries Branch", @"C:\pdf\b.pdf");

        var tarjeta = banco.ComoLoVeLaPantalla()[0];

        Assert.AreEqual(
            "2 documentos con este número: puede que viajen en el mismo grupo",
            tarjeta.LineaDelNumeroCompartido);
    }
}
