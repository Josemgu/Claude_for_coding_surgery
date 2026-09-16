using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// Dentro de la fecha, cada renglón va en VERDE si está resuelto y en ROJO si le falta algo,
/// con el mismo veredicto y el mismo par de colores del calendario de fuera.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-14:</b> <i>«Quiero que cuando des clic y entres a la
/// fecha, lo que esté completo se marque en verde y lo que no en rojo, para saber cuáles
/// fueron completados y cuáles no, como se muestra en el calendario afuera»</i>.</para>
///
/// <para><b>Lo medido antes de tocar nada:</b> la plantilla del renglón
/// (<c>PaginaDeGrupo.xaml</c>) solo usaba <c>Tinta</c>, <c>TintaSuave</c> y <c>PanelHondo</c>;
/// el dato estaba —<c>PersonaDelGrupo.Recomendacion</c> y su <c>PalabraDelEstado</c>— y el
/// color no.</para>
///
/// <para>⛔ <b>No hay un tercer criterio.</b> El color sale de la MISMA lectura que ya dice la
/// palabra del renglón —<see cref="LoQueSeLeeDeUnaPersona"/>, decisión del 2026-09-07— y de la
/// misma regla con la que el calendario cuenta «resueltas»: seis en sí, o archivado
/// (<c>LectorDeGrupos.ArmarLaPastilla</c>: <c>confirmada || caso.Archivado</c>). Estas pruebas
/// se escribieron ANTES del código y salieron rojas al escribirlas.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelColorDeCadaRenglon
{
    /// <summary>El día sobre el que se montan los casos de estas pruebas.</summary>
    private static readonly DateOnly ElDia = new(2026, 9, 17);

    /// <summary>Los renglones de personas del día, tal como los aplana la pantalla.</summary>
    /// <param name="servicios">Donde está montado el día.</param>
    private static List<RenglonDelGrupo> PersonasDelDia(ServiciosFalsos servicios)
        => BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia).EnUnaSolaLista()
            .Where(r => r.EsUnaPersona).ToList();

    /// <summary>Una persona con las seis preguntas en sí se lee «resuelto» y va en verde.</summary>
    [TestMethod]
    public void UnaPersonaConLasSeisEnSiVaEnVerde()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, caso, fila: 1);

        var renglon = PersonasDelDia(servicios).Single();

        Console.WriteLine($"«{renglon.Detalle}» → {renglon.Color}");
        Assert.AreEqual(DosEstados.Resuelto, renglon.PalabraDelEstado);
        Assert.AreEqual(ColorDeLaPastilla.Verde, renglon.Color);
    }

    /// <summary>Una persona con ninguna en sí y alguna en no se lee «me falta» y va en rojo.</summary>
    /// <remarks>
    /// ⚠️ Hasta el 2026-09-16 se probaba con cinco en sí y una en no (<c>DejarNoListaParaViajar</c>),
    /// y desde ese día eso es NARANJA —a medias— y no rojo: el naranja mide cuánto se avanzó
    /// (<c>PruebasDelRenglonNaranja</c>). Lo que esta prueba vigila —que un «no» sin nada en sí
    /// vaya en rojo con su palabra— sigue igual, con una persona que no ha avanzado nada.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaConUnaEnNoVaEnRojo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.ContestarLasSeisDe(servicios, caso, fila: 1, entrevistas: false);

        var renglon = PersonasDelDia(servicios).Single();

        Console.WriteLine($"«{renglon.Detalle}» → {renglon.Color}");
        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        Assert.AreEqual(ColorDeLaPastilla.Rojo, renglon.Color);
    }

    /// <summary>
    /// Una persona a la que nadie miró también va en rojo: «sin mirar» no es «resuelto».
    /// </summary>
    /// <remarks>
    /// Es lo mismo que ya dice su palabra —«me falta»— y lo mismo que hace el calendario, que
    /// solo cuenta resuelta a la que tiene las seis en sí. Pintarla de verde sería dar por
    /// lista a alguien de quien faltan preguntas por mirar.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaQueNadieMiroVaEnRojo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);

        var renglon = PersonasDelDia(servicios).Single();

        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        Assert.AreEqual(ColorDeLaPastilla.Rojo, renglon.Color);
    }

    /// <summary>El renglón del documento del que no se leyó a nadie va en rojo: le falta trabajo.</summary>
    /// <remarks>
    /// Su palabra ya es «me falta» y su detalle manda a Corrección a añadir a alguien. En el
    /// calendario esa unidad va en GRIS porque no hay a quién confirmar; aquí el renglón es un
    /// documento con trabajo pendiente, y el color sigue a la palabra que ya lleva.
    /// </remarks>
    [TestMethod]
    public void ElDocumentoSinNadieLeidoVaEnRojo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ELTC2609", "2026-09-17", cuantasPersonas: 0);

        var renglon = PersonasDelDia(servicios).Single();

        Assert.AreEqual("sin ninguna persona leída", renglon.Titulo);
        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        Assert.AreEqual(ColorDeLaPastilla.Rojo, renglon.Color);
    }

    /// <summary>
    /// Un archivado se lee resuelto y va en verde aunque sus seis preguntas no digan que sí.
    /// </summary>
    /// <remarks>
    /// <para>Es la regla del calendario, del dueño el 2026-09-07: <i>«aunque se archive, debe
    /// quedarse en el calendario marcado en verde»</i>. Se prueba sobre el modelo, a pelo, para
    /// que la regla del color quede fijada como UNA y la misma que la de fuera; que el archivado
    /// llegue de verdad por <c>LectorDeGrupos.DelDia</c> (desde el 2026-09-14) lo vigila
    /// <c>PruebasDeQueElArchivadoEntraEnLaFecha</c>.</para>
    /// </remarks>
    [TestMethod]
    public void UnArchivadoSeLeeResueltoYVaEnVerde()
    {
        Assert.AreEqual(ColorDeLaPastilla.Verde, ColorDelRenglon.De(LoQueSeLee.MeFalta, archivado: true));
        Assert.AreEqual(ColorDeLaPastilla.Verde, ColorDelRenglon.De(LoQueSeLee.Resuelto, archivado: true));
        Assert.AreEqual(ColorDeLaPastilla.Verde, ColorDelRenglon.De(LoQueSeLee.Resuelto, archivado: false));
        Assert.AreEqual(ColorDeLaPastilla.Rojo, ColorDelRenglon.De(LoQueSeLee.MeFalta, archivado: false));

        var archivada = new PersonaDelGrupo(
            PersonaId: 1, CasoId: 1, Nombre: "Ana", Cedula: "055-1111-3853", NumeroCaso: "ARCH2609",
            RutaPdf: null, HayPdf: false, EstadoDeRecomendacion.SinMarcar, MotivoDeNoCompletar.SinMotivo,
            CuantoLeFalta: 0, Dueno: string.Empty, Recomendacion: null, SeQuedoEn: null, Archivado: true);

        Assert.AreEqual(ColorDeLaPastilla.Verde, archivada.Color);
        Assert.AreEqual(ColorDeLaPastilla.Verde, RenglonDelGrupo.DeUnaPersona(archivada).Color);
        Assert.IsNull(archivada.Recomendacion, "Y sin decir que sus seis dicen que sí, que sería inventarlo.");
    }

    /// <summary>
    /// En toda la base inventada, verde es exactamente «resuelto» y rojo exactamente «me
    /// falta»: el color no puede decir una cosa y la palabra otra.
    /// </summary>
    [TestMethod]
    public void ElColorSigueALaPalabraEnTodaLaBaseInventada()
    {
        var servicios = BaseDeInicio.MontarServicios(200);
        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var dias = BaseDeInicio.TodosLosCasos(servicios)
            .Select(c => FechasEnEspanol.Leer(c.FechaViaje))
            .OfType<DateOnly>()
            .Distinct()
            .ToList();

        var verdes = 0;
        var rojos = 0;
        foreach (var renglon in dias.SelectMany(d => lector.DelDia(d).EnUnaSolaLista()).Where(r => r.EsUnaPersona))
        {
            // Desde el 2026-09-16 hay un tercer color, naranja, para «me falta · a medias»;
            // lo fija PruebasDelRenglonNaranja. Aquí sigue vigilándose lo del 14: verde es
            // exactamente «resuelto», y lo que no es resuelto nunca es verde.
            var esperado = renglon.PalabraDelEstado == DosEstados.Resuelto
                ? ColorDeLaPastilla.Verde
                : renglon.AMedias ? ColorDeLaPastilla.Naranja : ColorDeLaPastilla.Rojo;
            Assert.AreEqual(esperado, renglon.Color, $"«{renglon.Titulo} · {renglon.Detalle}»");
            if (renglon.Color == ColorDeLaPastilla.Verde) verdes++; else rojos++;
        }

        Console.WriteLine($"{dias.Count} días · {verdes} renglones verdes · {rojos} rojos o naranjas");
        Assert.IsGreaterThan(0, verdes, "La base inventada trae personas con las seis en sí.");
        Assert.IsGreaterThan(0, rojos, "Y personas a las que les falta algo.");
    }

    /// <summary>
    /// Un día con mezcla: los verdes y rojos de dentro cuadran con la pastilla de fuera, y la
    /// cabecera de la unidad va del mismo color que su pastilla.
    /// </summary>
    [TestMethod]
    public void LosVerdesYRojosDeDentroCuadranConLaPastillaDeFuera()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var uno = BaseDeInicio.MeterCaso(servicios, "CASP2601", "2026-09-17", cuantasPersonas: 2);
        var dos = BaseDeInicio.MeterCaso(servicios, "CASP2602", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, uno, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, uno, fila: 2);
        BaseDeInicio.DejarNoListaParaViajar(servicios, dos, fila: 1);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var renglones = lector.DelDia(ElDia).EnUnaSolaLista();
        var cabecera = renglones.Single(r => r.EsCabecera);
        var personas = renglones.Where(r => r.EsUnaPersona).ToList();

        var verdes = personas.Count(r => r.Color == ColorDeLaPastilla.Verde);
        // La que falta tiene cinco en sí y una en no: desde el 2026-09-16 va en naranja (a
        // medias). Lo que cuadra con la pastilla de fuera es «no verde», sea rojo o naranja.
        var sinResolver = personas.Count(r => r.Color != ColorDeLaPastilla.Verde);
        Console.WriteLine($"fuera: «{pastilla.Etiqueta}» {pastilla.Color} · dentro: {verdes} verdes, {sinResolver} sin resolver · cabecera {cabecera.Color}");

        Assert.AreEqual("me falta 1 de 3", pastilla.Etiqueta);
        Assert.AreEqual(pastilla.CuantasPersonasResueltas, verdes, "Los verdes de dentro son las resueltas de fuera.");
        Assert.AreEqual(pastilla.CuantasPersonas - pastilla.CuantasPersonasResueltas, sinResolver);
        Assert.AreEqual(ColorDeLaPastilla.Rojo, pastilla.Color);
        Assert.AreEqual(pastilla.Color, cabecera.Color, "La cabecera de la unidad va como su pastilla.");
    }

    /// <summary>Una unidad con todas resueltas: pastilla verde fuera y cabecera verde dentro.</summary>
    [TestMethod]
    public void UnaUnidadConTodasResueltasVaEnVerdeFueraYDentro()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2601", "2026-09-17", cuantasPersonas: 2);
        BaseDeInicio.DejarListaParaViajar(servicios, caso, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, caso, fila: 2);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var renglones = lector.DelDia(ElDia).EnUnaSolaLista();

        Assert.AreEqual(ColorDeLaPastilla.Verde, pastilla.Color);
        Assert.AreEqual(ColorDeLaPastilla.Verde, renglones.Single(r => r.EsCabecera).Color);
        Assert.IsTrue(renglones.Where(r => r.EsUnaPersona).All(r => r.Color == ColorDeLaPastilla.Verde));
    }

    /// <summary>
    /// La cabecera de una unidad de la que no se leyó a nadie va en GRIS, como su pastilla:
    /// ni verde —nadie dijo que esté resuelta— ni rojo —no va nadie a viajar sin confirmar—.
    /// </summary>
    [TestMethod]
    public void LaCabeceraDeUnaUnidadSinNadieLeidoVaEnGrisComoSuPastilla()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ELTC2609", "2026-09-17", cuantasPersonas: 0);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var cabecera = lector.DelDia(ElDia).EnUnaSolaLista().Single(r => r.EsCabecera);

        Assert.AreEqual(ColorDeLaPastilla.Gris, pastilla.Color);
        Assert.AreEqual(ColorDeLaPastilla.Gris, cabecera.Color);
    }

    /// <summary>
    /// En toda la base inventada, día a día y unidad a unidad, la cabecera de dentro va del
    /// color de la pastilla de fuera. También cuando la unidad tiene un archivado: hasta el
    /// 2026-09-14 ese contaba fuera y no entraba dentro, y esta prueba lo dejaba pasar contando
    /// cuántas veces; desde ese día entra, y ya no hay excepción que contar.
    /// </summary>
    [TestMethod]
    public void CadaCabeceraVaDelColorDeSuPastillaEnTodaLaBaseInventada()
    {
        var servicios = BaseDeInicio.MontarServicios(200);
        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var unidadesConArchivado = BaseDeInicio.TodosLosCasos(servicios)
            .Where(c => c.Archivado)
            .Select(c => (FechasEnEspanol.Leer(c.FechaViaje), c.UnidadNumero?.Trim() ?? string.Empty))
            .ToHashSet();

        var cuadran = 0;
        var conArchivado = 0;
        var pastillas = 0;
        foreach (var (dia, delDia) in lector.PorDia())
        {
            var unidades = lector.DelDia(dia).Unidades;
            foreach (var pastilla in delDia)
            {
                pastillas++;
                if (unidadesConArchivado.Contains((dia, pastilla.UnidadNumero))) conArchivado++;

                var unidad = unidades.Single(u => u.UnidadNumero == pastilla.UnidadNumero);
                Assert.AreEqual(pastilla.Color, unidad.Color, $"{dia} · {pastilla.Titulo}");
                cuadran++;
            }
        }

        Console.WriteLine($"{pastillas} pastillas · {cuadran} cuadran fuera y dentro · {conArchivado} de ellas con archivado");
        Assert.IsGreaterThan(0, conArchivado, "Si la base no trae ninguna unidad con archivado, no se probó lo del 14.");
        Assert.AreEqual(pastillas, cuadran);
    }
}
