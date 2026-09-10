using Fichas.App.Revisar;
using Fichas.Pruebas.App.Asignar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Con que tablero abre Revisar, probado SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// <para>Palabras del dueno, 2026-09-06: <i>«O solo que me aparezca en "por corregir" los
/// documentos a corregir, no todos los documentos. Cuando doy click aparecen todos los PDF
/// para revisar; solo los que necesitan revisión son los que deben ser revisados, no
/// todos»</i>.</para>
///
/// <para><b>Por que se prueba aqui y no en la pagina.</b> Hasta el 2026-09-06 el tablero de
/// partida era un valor escrito a mano en un campo privado de
/// <c>Revisar/PaginaDeRevisar.xaml.cs:32</c>, donde ninguna prueba podia llegar: por eso
/// pudo estar en «Todo» meses sin que nada se pusiera rojo. Ahora la decision vive en
/// <see cref="TableroDeRevisar.ElTableroConElQueSeAbre"/> y la pagina la lee.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelTableroConElQueSeAbre
{
    /// <summary>
    /// Revisar abre en «Sin revisar», que es lo que hay que revisar, y no en «Todo».
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>La eleccion se justifica con sus palabras, no con un gusto.</b> El dice «solo
    /// los que necesitan revisión son los que deben ser revisados». De los seis tableros que
    /// existen, el unico que significa eso es <see cref="FiltroDeTarjeta.MeFalta"/>. Los otros
    /// tres son otra pregunta —lo que ya esta cerrado (Resuelto), quien lo lleva (SinAsignar) y
    /// cuando viaja (FechaPasada)—, y «Todo» es exactamente lo que el dijo que sobraba.
    /// <para>
    /// ⛔ <b>Este tablero se llamaba «Sin revisar» y eran seis</b>; el 2026-09-07 se juntó con
    /// «No completas», que con dos palabras enseñaba lo mismo. NO se anade un tablero nuevo, que
    /// es lo que dice la entrada del 2026-09-06: <i>«los seis tableros […] están bien; el
    /// defecto es el que sobra»</i>.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void RevisarAbreEnLoQueHayQueRevisarYNoEnTodo()
    {
        Assert.AreEqual(FiltroDeTarjeta.MeFalta, TableroDeRevisar.ElTableroConElQueSeAbre);
        Assert.AreNotEqual(FiltroDeTarjeta.Todo, TableroDeRevisar.ElTableroConElQueSeAbre);
    }

    /// <summary>
    /// Con documentos de varias clases dentro, abrir ensena MENOS de los que hay, y «Todo»
    /// los ensena todos.
    /// </summary>
    /// <remarks>
    /// Es el criterio del pase con su denominador: cuantos salen al abrir, cuantos hay, y
    /// cuantos de cada clase. Sin la segunda mitad —que «Todo» sigue trayendolos— el cambio
    /// se leeria como que los documentos desaparecieron.
    /// </remarks>
    [TestMethod]
    public void AlAbrirSalenSoloLosQueLeFaltanYPulsandoTodoSalenTodos()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        var alAbrir = banco.Tablero.CuantasEn(TableroDeRevisar.ElTableroConElQueSeAbre);
        var enTodo = banco.Tablero.CuantasEn(FiltroDeTarjeta.Todo);

        // ⛔ 2026-09-07: eran 4 —solo los «sin revisar»— y son 5, porque «No completas» se juntó
        // con este tablero. Sigue siendo MENOS que «Todo», que es lo que la prueba defiende: al
        // abrir no le salen todos los documentos delante.
        Assert.AreEqual(5, alAbrir, "Los 4 que nadie miró más el 1 que el compañero devolvió sin completar.");
        Assert.AreEqual(6, enTodo, "Seis sin archivar; el archivado no sale en ningún tablero.");
        Assert.IsLessThan(enTodo, alAbrir, $"Al abrir salen {alAbrir} de {enTodo}, no los {enTodo}.");
        Assert.AreEqual(banco.Tablero.Total, enTodo, "«Todo» sigue siendo exactamente lo cargado.");

        // El que NO sale al abrir es el que ya está cerrado: el que el compañero dio por
        // completo. Es «lo que ya se revisó», que es lo que el dueño dijo que le sobraba delante.
        Assert.AreEqual(1, banco.Tablero.CuantasEn(FiltroDeTarjeta.Resuelto));
    }

    /// <summary>
    /// «Todo» sigue existiendo como tablero: deja de ser el primero, no desaparece.
    /// </summary>
    /// <remarks>
    /// Decision del 2026-09-06: <i>«"Todo" sigue estando, se pulsa; deja de ser lo primero
    /// que ve»</i>. Si un dia alguien borrara el tablero, esta prueba lo diria.
    /// </remarks>
    [TestMethod]
    public void ElTableroDeTodoSigueExistiendo()
    {
        CollectionAssert.Contains(Enum.GetValues<FiltroDeTarjeta>(), FiltroDeTarjeta.Todo);
        Assert.AreEqual("Todo", TableroDeRevisar.NombreDe(FiltroDeTarjeta.Todo));
    }
}
