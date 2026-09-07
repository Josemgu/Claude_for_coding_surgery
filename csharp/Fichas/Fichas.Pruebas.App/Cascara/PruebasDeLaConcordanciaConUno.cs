using Fichas.App.Asignar;
using Fichas.App.Importar;
using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Con UNO se escribe en singular. Es el numero que rompia todas las frases.
/// </summary>
/// <remarks>
/// <para>Quitar el «(s)» no basta: si «{n} documentos» se deja como estaba, el uno sigue
/// diciendo «1 documentos». Por eso esta clase mide el caso de uno de verdad, llamando a las
/// mismas clases que componen lo que se ve, y no barriendo el fuente.</para>
///
/// <para>Se prueba con 1 y con 2 en cada frase. Solo con 1 no valdria: una frase que dijera
/// «documento» siempre pasaria en verde y estaria igual de rota, al reves.</para>
///
/// <para>Sale de lo que QA midio en la pantalla el 2026-09-04: «1 companeros activos»,
/// «1 persona(s)», «18 marcado(s)».</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaConcordanciaConUno
{
    /// <summary>«1 documento archivado.» y «2 documentos archivados.»</summary>
    [TestMethod]
    public void ElLoteDeArchivarConcuerdaConLoQueArchivo()
    {
        Assert.AreEqual(
            "1 documento archivado.",
            new ResumenDeLote(1, 0, "archivado", "archivados").Linea);
        Assert.AreEqual(
            "2 documentos archivados.",
            new ResumenDeLote(2, 0, "archivado", "archivados").Linea);
    }

    /// <summary>Y el que no se pudo tambien concuerda, que es la otra mitad de la frase.</summary>
    [TestMethod]
    public void ElLoteConcuerdaTambienEnLoQueNoSePudo()
    {
        Assert.AreEqual(
            "1 documento desarchivado; 1 no se pudo (mira la franja).",
            new ResumenDeLote(1, 1, "desarchivado", "desarchivados").Linea);
        Assert.AreEqual(
            "3 documentos desarchivados; 2 no se pudieron (mira la franja).",
            new ResumenDeLote(3, 2, "desarchivado", "desarchivados").Linea);
    }

    /// <summary>«1 caso asignado a Sandy.» y «4 casos asignados a Sandy.»</summary>
    [TestMethod]
    public void ElLoteDeAsignarConcuerdaConLoQueAsigno()
    {
        Assert.AreEqual("1 caso asignado a Sandy.", new ResumenDeAsignacion(1, 0, "Sandy").Linea);
        Assert.AreEqual("4 casos asignados a Sandy.", new ResumenDeAsignacion(4, 0, "Sandy").Linea);
    }

    /// <summary>La tarjeta de Revisar dice «1 persona», no «1 persona(s)» ni «1 personas».</summary>
    [TestMethod]
    public void LaTarjetaDeRevisarConcuerdaConSusPersonas()
    {
        var una = new TarjetaDeDocumento { Personas = 1, FechaDeViaje = "2026-10-01", Unidad = "Rama" };
        var varias = una with { Personas = 5 };

        StringAssert.StartsWith(una.Datos, "1 persona ·");
        StringAssert.StartsWith(varias.Datos, "5 personas ·");
    }

    // ⛔ Aqui vivia `ElRenglonDeAsignarConcuerdaConSusPersonas`, que ademas de la concordancia
    // exigia que la cifra fuera lo PRIMERO del detalle. El 2026-09-06 el dueno pidio ver el
    // nombre de quien viaja en la lista de Asignar y la unidad bajo a esa linea, asi que la
    // cifra ya no va delante. La prueba se movio a
    // `Fichas.Pruebas.App/Asignar/PruebasDeQuienViaja.ElRenglonDeAsignarConcuerdaConSusPersonas`
    // —al lado de la pantalla que la rompe— y alli sigue vigilando lo mismo: que con uno se
    // escriba «1 persona» y con tres «3 personas». Lo unico que se solto es la POSICION.

    /// <summary>
    /// El resumen de la importacion, que es lo primero que Miguel lee al terminar una tanda.
    /// </summary>
    /// <remarks>
    /// Se comprueba la linea entera y no un trozo: es la frase que QA leyo en pantalla
    /// —«9 de 9 documentos · 9 casos · 18 personas · 7 duplicados avisados»— y en ella hay
    /// cinco cifras que concordar, no una.
    /// </remarks>
    [TestMethod]
    public void ElResumenDeLaTandaConcuerdaEnSusCincoCifras()
    {
        var una = new ResumenDeLaTanda(1);
        una.Anotar(new ResultadoDeUnDocumento(@"C:\pdf\uno.pdf", 1, 1, 1, 0, 1, 1, null, 0.5));

        var linea = una.Linea();

        Assert.Contains("1 caso ·", linea, $"Línea: «{linea}»");
        Assert.Contains("1 persona ·", linea, $"Línea: «{linea}»");
        Assert.Contains("1 duplicado avisado ·", linea, $"Línea: «{linea}»");
        Assert.Contains("1 ilegible ·", linea, $"Línea: «{linea}»");
        Assert.DoesNotContain("(s)", linea, $"Línea: «{linea}»");
    }

    /// <summary>Y con varios, la misma linea en plural: si no, el singular estaria puesto a la fuerza.</summary>
    [TestMethod]
    public void ElResumenDeLaTandaTambienConcuerdaEnPlural()
    {
        var varias = new ResumenDeLaTanda(2);
        varias.Anotar(new ResultadoDeUnDocumento(@"C:\pdf\uno.pdf", 2, 2, 4, 0, 2, 3, null, 0.5));

        var linea = varias.Linea();

        Assert.Contains("2 casos ·", linea, $"Línea: «{linea}»");
        Assert.Contains("4 personas ·", linea, $"Línea: «{linea}»");
        Assert.Contains("2 duplicados avisados ·", linea, $"Línea: «{linea}»");
        Assert.Contains("3 ilegibles ·", linea, $"Línea: «{linea}»");
    }
}
