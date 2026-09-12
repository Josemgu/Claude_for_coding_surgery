using Fichas.App.Grupo;
using Fichas.Datos.Falso;
using Fichas.App.Vocabulario;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// El grupo de una fecha va DIVIDIDO POR UNIDAD, y cada unidad dice su numero, su nombre,
/// cuantas personas viajan ese dia y que le falta.
/// </summary>
/// <remarks>
/// <para><b>De donde sale, con sus palabras</b> (<c>DECISIONES.md</c>, 2026-09-07, «EL DUENO
/// DICTA EL FLUJO ENTERO», apartado 4): <i>«Rama San Juan No 325535, 10 personas viajarán el
/// 12 de septiembre. Barrio Marito 656351, 5 personas viajarán el 12 de septiembre, falta
/// verificar recomendaciones. Y ya.»</i></para>
///
/// <para><b>Lo que habia, medido antes de este pase.</b> La cabecera decia
/// <c>UnidadDelGrupo.Detalle</c> = «3 documentos · 7 personas · me falta 5 de 7». Traia el
/// numero, el nombre y la cuenta, y le faltaban las dos mitades que el nombra: <b>cuando
/// viajan</b> —la fecha estaba en el titulo de la pantalla, no en el renglon de la unidad— y
/// <b>que le falta</b>, que era una cifra sin decir de que.</para>
///
/// <para>⛔ <b>El idioma es el de dos palabras</b> (<c>DECISIONES.md</c>, 2026-09-07, «DOS
/// ESTADOS Y NO CUATRO»): «resuelto» y «me falta», y ninguna mas. Lo que falta se dice en el
/// detalle, que es la otra mitad de esa decision.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaCabeceraDeCadaUnidad
{
    /// <summary>El día del ejemplo que dictó el dueño el 2026-09-09.</summary>
    private const string ElDoceDeSeptiembre = "2026-09-12";
    /// <summary>El número de la primera unidad de su ejemplo, la de diez personas.</summary>
    private const string RamaSanJuan = "325535";
    /// <summary>El número de la segunda unidad de su ejemplo, la de cinco.</summary>
    private const string BarrioMarito = "656351";

    /// <summary>El dia del ejemplo del dueno, con sus dos unidades.</summary>
    private static DateOnly ElDia
        => DateOnly.Parse(ElDoceDeSeptiembre, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Monta el 12 de septiembre con las dos unidades que el nombro.</summary>
    /// <returns>Los servicios donde se montó; se devuelven para poder seguir tocándolos.</returns>
    private static ServiciosFalsos MontarElDoceDeSeptiembre()
    {
        var servicios = BaseDeInicio.MontarServicios(0);

        BaseDeInicio.MeterCaso(servicios, "CASP2609", ElDoceDeSeptiembre, cuantasPersonas: 5, unidadNumero: RamaSanJuan);
        BaseDeInicio.MeterCaso(servicios, "CASP2610", ElDoceDeSeptiembre, cuantasPersonas: 5, unidadNumero: RamaSanJuan);
        BaseDeInicio.MeterCaso(servicios, "CASP2611", ElDoceDeSeptiembre, cuantasPersonas: 5, unidadNumero: BarrioMarito);

        return servicios;
    }

    /// <summary>La cabecera de esa unidad dentro del dia.</summary>
    /// <param name="servicios">Donde está montado el día.</param>
    /// <param name="unidadNumero">El número de la unidad cuya cabecera se busca; si no está, la prueba falla aquí.</param>
    private static RenglonDelGrupo CabeceraDe(ServiciosFalsos servicios, string unidadNumero)
        => BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia).EnUnaSolaLista()
               .FirstOrDefault(r => r.EsCabecera && r.Titulo.Contains(unidadNumero, StringComparison.Ordinal))
           ?? throw new AssertFailedException($"no hay ninguna cabecera de la unidad {unidadNumero}");

    // ────────────────────────── lo que el dueno dicto, palabra por palabra ──────────────────────────

    /// <summary>El dia va partido por unidad y hay una cabecera por cada una.</summary>
    [TestMethod]
    public void ElDiaVaDivididoPorUnidad()
    {
        var grupo = BaseDeInicio.LectorDeGruposDe(MontarElDoceDeSeptiembre()).DelDia(ElDia);

        Assert.HasCount(2, grupo.Unidades, "dos unidades ese dia, dos cabeceras");
        Assert.HasCount(
            2,
            grupo.EnUnaSolaLista().Where(r => r.EsCabecera).ToList(),
            "cada unidad trae la suya");
    }

    /// <summary>La cabecera nombra la unidad con su NUMERO y su nombre.</summary>
    [TestMethod]
    public void LaCabeceraDiceElNumeroYElNombreDeLaUnidad()
    {
        var cabecera = CabeceraDe(MontarElDoceDeSeptiembre(), RamaSanJuan);

        StringAssert.Contains(cabecera.Titulo, RamaSanJuan, "el numero de unidad, que es lo que el dicta");
        StringAssert.Contains(cabecera.Titulo, "Rama de Prueba", "y su nombre");
    }

    /// <summary>
    /// La cabecera dice CUANTAS PERSONAS viajan y CUANDO, que es como el lo dice.
    /// </summary>
    /// <remarks>
    /// La fecha va en el renglon de la unidad y no solo en el titulo de la pantalla: el
    /// dueno lee una unidad y la lee entera —«10 personas viajarán el 12 de septiembre»—, y
    /// con la fecha solo arriba, un renglon copiado o leido en voz alta no dice de que dia
    /// habla.
    /// </remarks>
    [TestMethod]
    public void LaCabeceraDiceCuantasPersonasViajanYQueDia()
    {
        var cabecera = CabeceraDe(MontarElDoceDeSeptiembre(), RamaSanJuan);

        StringAssert.Contains(cabecera.Detalle, "10 personas", "cinco y cinco de sus dos documentos");
        StringAssert.Contains(cabecera.Detalle, "12 de septiembre", "y el dia en que viajan");
    }

    /// <summary>
    /// La cabecera dice QUE le falta a esa unidad, no solo cuanto.
    /// </summary>
    /// <remarks>
    /// Sus palabras para la unidad pequena: <i>«falta verificar recomendaciones»</i>. Una
    /// cifra sin decir de que —«me falta 5 de 10»— obliga a abrir la unidad para saber si lo
    /// que falta se arregla escribiendo en Correccion o llamando al lider, que son dos
    /// trabajos distintos.
    /// </remarks>
    [TestMethod]
    public void LaCabeceraDiceQueLeFaltaAEsaUnidad()
    {
        var cabecera = CabeceraDe(MontarElDoceDeSeptiembre(), BarrioMarito);

        StringAssert.Contains(
            cabecera.LoQueLeFaltaALaUnidad,
            "recomendaci",
            "nadie ha contestado las seis preguntas de esas personas: eso es lo que falta");
    }

    /// <summary>
    /// A una unidad cuyos documentos tienen huecos se le dice que el trabajo es de Correccion.
    /// </summary>
    /// <remarks>
    /// Son dos faltas distintas y se arreglan por caminos distintos —una escribiendo, la otra
    /// llamando al lider—, asi que la cabecera las nombra por separado.
    /// </remarks>
    [TestMethod]
    public void UnaUnidadConHuecosEnElPapelLoDiceAparte()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(
            servicios, "CASP2612", ElDoceDeSeptiembre, cuantasPersonas: 2,
            unidadNumero: RamaSanJuan, temploNombre: null);

        var cabecera = CabeceraDe(servicios, RamaSanJuan);

        StringAssert.Contains(cabecera.LoQueLeFaltaALaUnidad, "Corrección", "el hueco del papel se llena ahi");
    }

    /// <summary>Una unidad a la que no le falta nada lo dice, y no se queda muda.</summary>
    [TestMethod]
    public void UnaUnidadResueltaLoDice()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "CASP2613", ElDoceDeSeptiembre, cuantasPersonas: 2, unidadNumero: RamaSanJuan);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, 1);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, 2);

        var cabecera = CabeceraDe(servicios, RamaSanJuan);

        Assert.AreEqual(DosEstados.Resuelto, cabecera.LoQueLeFaltaALaUnidad);
    }

    /// <summary>Solo se leen «resuelto» y «me falta»: ninguna de las cuatro retiradas.</summary>
    [TestMethod]
    public void EnLaCabeceraSoloSeLeenLasDosPalabras()
    {
        var servicios = MontarElDoceDeSeptiembre();

        foreach (var cabecera in BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia)
                     .EnUnaSolaLista().Where(r => r.EsCabecera))
        {
            var linea = $"{cabecera.Titulo} · {cabecera.Detalle} · {cabecera.LoQueLeFaltaALaUnidad}";
            foreach (var retirada in new[] { "listo para asignar", "listo para viajar", "confirmadas", "completas" })
            {
                Assert.IsFalse(
                    linea.Contains(retirada, StringComparison.OrdinalIgnoreCase),
                    $"«{retirada}» es una de las cuatro palabras retiradas el 2026-09-07: «{linea}»");
            }
        }
    }

    /// <summary>Lo que lee en voz alta un lector de pantalla lleva tambien lo que falta.</summary>
    /// <remarks>
    /// Si solo estuviera pintado, quien no ve la pantalla no se enteraria de lo unico que dice
    /// por que hay que mirar esa unidad.
    /// </remarks>
    [TestMethod]
    public void ElLectorDePantallaTambienDiceQueLeFalta()
    {
        var cabecera = CabeceraDe(MontarElDoceDeSeptiembre(), BarrioMarito);

        StringAssert.Contains(cabecera.ParaElLector, cabecera.LoQueLeFaltaALaUnidad);
        StringAssert.Contains(cabecera.ParaElLector, "12 de septiembre");
    }

    /// <summary>Una persona no lleva la frase de la unidad: no es una unidad.</summary>
    [TestMethod]
    public void UnRenglonDePersonaNoLlevaLaFraseDeLaUnidad()
    {
        var persona = BaseDeInicio.LectorDeGruposDe(MontarElDoceDeSeptiembre()).DelDia(ElDia)
            .EnUnaSolaLista().First(r => r.EsUnaPersona);

        Assert.AreEqual(string.Empty, persona.LoQueLeFaltaALaUnidad);
    }
}
