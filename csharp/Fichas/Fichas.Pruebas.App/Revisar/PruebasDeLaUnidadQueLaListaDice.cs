using Fichas.App.Revisar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// La lista y el documento abierto dicen la MISMA unidad. Siempre.
/// </summary>
/// <remarks>
/// <para><b>La queja del dueño</b>, con sus palabras: «muchos no tenían unidad; cuando entré
/// al documento, la unidad sí estaba». Otro programador no consiguió reproducirlo sobre los
/// diez PDF de esta máquina: dan unidad 10 de 10 en la base y 7 de 7 en la lista. Con ese
/// material el fallo <b>no aparece</b>, y por eso hacía falta averiguar bajo qué condición sí.</para>
///
/// <para><b>La condición, encontrada el 2026-09-05 leyendo el código y no adivinando:</b> la
/// tarjeta componía su unidad <b>solo con <c>unidad_nombre</c></b>
/// (<c>TableroDeRevisar.Componer</c>) y ponía «sin unidad» cuando ese campo venía vacío —
/// <b>aunque <c>unidad_numero</c> estuviera lleno</b>. El documento abierto sí enseña los dos
/// campos. Así que basta un caso con número de unidad y sin nombre para que la lista diga una
/// cosa y el documento otra. Los diez PDF de esta máquina no lo enseñaban porque en ellos el
/// nombre venía relleno; venía relleno <b>con el número</b>, que es el segundo defecto de este
/// mismo pase. Al corregir aquel, esta condición pasaba de rara a ser la de todos los grupos:
/// los dos defectos se tapaban el uno al otro.</para>
///
/// <para>Es también el mismo caso que <see cref="ArbolDeRevisar"/> ya trataba bien —su carpeta
/// se llama <c>«7000011»</c> a secas cuando no hay nombre—, y esa discrepancia entre el árbol y
/// la tarjeta es la señal de que el defecto estaba en la tarjeta.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaUnidadQueLaListaDice
{
    /// <summary>El número de unidad de los formularios de grupo reales del dueño.</summary>
    private const string NumeroDeUnidadDelGrupo = "7000011";

    /// <summary>
    /// Un caso con número de unidad y sin nombre NO se lee como «sin unidad» en la lista.
    /// </summary>
    [TestMethod]
    public void UnCasoConNumeroDeUnidadYSinNombreNoSeLeeComoSinUnidad()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("SURB2609", "2026-09-05", NumeroDeUnidadDelGrupo, unidadNombre: null);

        var tarjeta = banco.ComoLoVeLaPantalla().Single();

        Assert.AreNotEqual(TarjetaDeDocumento.SinUnidad, tarjeta.UnidadQueSeLee,
            "el documento abierto enseña la unidad 7000011; la lista no puede decir que no la tiene");
        Assert.Contains(NumeroDeUnidadDelGrupo, tarjeta.Datos,
            "la línea de la tarjeta es lo que el dueño lee sin abrir el documento");
    }

    /// <summary>Con las dos cosas, la tarjeta las dice las dos, como la carpeta del árbol.</summary>
    /// <remarks>
    /// El dueño pidió la carpeta con «el nombre y número de la unidad» y hace falta: dos
    /// unidades pueden llamarse igual y el número es lo único que las distingue. Lo que vale
    /// para la carpeta vale para la tarjeta, que es donde mira primero.
    /// </remarks>
    [TestMethod]
    public void ConNumeroYNombreLaTarjetaDiceLosDos()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-08", "700001", "Castries Branch");

        var tarjeta = banco.ComoLoVeLaPantalla().Single();

        Assert.Contains("700001", tarjeta.UnidadQueSeLee);
        Assert.Contains("Castries Branch", tarjeta.UnidadQueSeLee);
    }

    /// <summary>Con solo el nombre, se dice el nombre y no se inventa un número.</summary>
    [TestMethod]
    public void ConSoloElNombreSeDiceElNombre()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-08", unidadNumero: null, unidadNombre: "Castries Branch");

        var tarjeta = banco.ComoLoVeLaPantalla().Single();

        Assert.AreEqual("Castries Branch", tarjeta.UnidadQueSeLee);
    }

    /// <summary>
    /// Y sin ninguna de las dos SÍ se dice «sin unidad»: eso es verdad y hay que verlo.
    /// </summary>
    /// <remarks>
    /// Control negativo. Sin él, una corrección que nunca dijera «sin unidad» pasaría las tres
    /// pruebas de arriba y escondería justo los documentos que hay que ir a mirar.
    /// </remarks>
    [TestMethod]
    public void SinNumeroNiNombreLaTarjetaSiDiceSinUnidad()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("CASP2609", "2026-09-08", unidadNumero: null, unidadNombre: null);

        var tarjeta = banco.ComoLoVeLaPantalla().Single();

        Assert.AreEqual(TarjetaDeDocumento.SinUnidad, tarjeta.UnidadQueSeLee);
    }

    /// <summary>
    /// La tarjeta y la carpeta del árbol no pueden discrepar sobre la misma unidad.
    /// </summary>
    /// <remarks>
    /// Es la prueba que fija la condición entera, y la que habría delatado el defecto sin
    /// necesidad de los PDF del dueño: el árbol y la tarjeta leen el MISMO caso, así que si uno
    /// dice «7000011» y el otro «sin unidad», uno de los dos miente.
    /// </remarks>
    [TestMethod]
    public void LaTarjetaYLaCarpetaDelArbolDicenLaMismaUnidad()
    {
        var banco = new BancoDeCarpetas();
        banco.Meter("SURB2609", "2026-09-05", NumeroDeUnidadDelGrupo, unidadNombre: null);

        var tarjetas = banco.ComoLoVeLaPantalla();
        var carpeta = ArbolDeRevisar.Agrupar(tarjetas).Single().Fechas.Single().Unidades.Single();

        Assert.AreEqual(carpeta.Carpeta, tarjetas.Single().UnidadQueSeLee,
            "el árbol y la tarjeta leen el mismo caso: no pueden decir cosas distintas");
    }
}
