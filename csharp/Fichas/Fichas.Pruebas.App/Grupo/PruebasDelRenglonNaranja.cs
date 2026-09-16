using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// Dentro de la fecha, la persona A MEDIAS —alguna de las seis en sí, no las seis— va en
/// NARANJA, sigue diciendo «me falta» y el renglón dice qué le falta.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-16:</b> <i>«Las personas que se han completado, por
/// ejemplo 4 preguntas de las 6, deben pasar a color naranja e indicar que le falta»</i>.</para>
///
/// <para><b>Lo medido antes de tocar nada:</b> <c>ColorDeLaPastilla</c> tenía tres valores
/// (verde, rojo, gris) y <c>ColorDelRenglon.De</c> dos respuestas; una persona con 4 en sí
/// salía en rojo con el detalle «nadie ha contestado sus seis preguntas».</para>
///
/// <para>⛔ <b>La regla vive en UN sitio</b>, <see cref="ColorDelRenglon"/>: verde si resuelto o
/// archivado, naranja si a medias, rojo si no. Y sale de la MISMA lectura que la palabra
/// (<see cref="LoQueSeLeeDeUnaPersona"/>): el color no puede decir una cosa y la palabra otra.
/// Estas pruebas se escribieron ANTES del código y salieron rojas al escribirlas.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelRenglonNaranja
{
    /// <summary>El día sobre el que se montan los casos de estas pruebas.</summary>
    private static readonly DateOnly ElDia = new(2026, 9, 17);

    /// <summary>Los renglones de personas del día, tal como los aplana la pantalla.</summary>
    /// <param name="servicios">Donde está montado el día.</param>
    private static List<RenglonDelGrupo> PersonasDelDia(ServiciosFalsos servicios)
        => BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia).EnUnaSolaLista()
            .Where(r => r.EsUnaPersona).ToList();

    /// <summary>Cuatro de seis en sí: naranja, «me falta · a medias», y el renglón dice cuáles faltan.</summary>
    [TestMethod]
    public void CuatroDeSeisEnSiVaEnNaranjaYDiceQueLeFalta()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.ContestarLasSeisDe(servicios, caso, fila: 1, preparacion: true, informacion: true, citaDelTemplo: true, accionesRequeridas: true);

        var renglon = PersonasDelDia(servicios).Single();

        Console.WriteLine($"«{renglon.Detalle}» → {renglon.Color} · «{renglon.DetalleDelEstado}»");
        Assert.AreEqual(ColorDeLaPastilla.Naranja, renglon.Color);
        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        Assert.IsTrue(renglon.AMedias);
        StringAssert.Contains(renglon.Detalle, $"{DosEstados.MeFalta} · {DosEstados.NotaDeAMedias}");
        StringAssert.Contains(renglon.DetalleDelEstado, "le faltan 2 de 6: Entrevistas, Listo para el templo");
    }

    /// <summary>Ninguna en sí: rojo, como hoy, y sin la nota.</summary>
    [TestMethod]
    public void CeroDeSeisVaEnRojoSinNota()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);

        var renglon = PersonasDelDia(servicios).Single();

        Assert.AreEqual(ColorDeLaPastilla.Rojo, renglon.Color);
        Assert.IsFalse(renglon.AMedias);
        Assert.IsFalse(renglon.Detalle.Contains(DosEstados.NotaDeAMedias, StringComparison.Ordinal));
    }

    /// <summary>Las seis en sí: verde, y no a medias.</summary>
    [TestMethod]
    public void SeisDeSeisVaEnVerde()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, caso, fila: 1);

        var renglon = PersonasDelDia(servicios).Single();

        Assert.AreEqual(ColorDeLaPastilla.Verde, renglon.Color);
        Assert.IsFalse(renglon.AMedias);
    }

    /// <summary>Un archivado va en verde aunque esté a medias: archivar cierra el documento.</summary>
    [TestMethod]
    public void UnArchivadoAMediasVaEnVerde()
    {
        var aMedias = LoQueSeLeeDeUnaPersona.De(null, [], ["Entrevistas", "Listo para el templo"]);
        Assert.IsTrue(aMedias.AMedias, "La lectura de partida tiene que estar a medias, o no se prueba nada.");

        Assert.AreEqual(ColorDeLaPastilla.Verde, ColorDelRenglon.De(aMedias, archivado: true));
        Assert.AreEqual(ColorDeLaPastilla.Naranja, ColorDelRenglon.De(aMedias, archivado: false));

        var archivada = new PersonaDelGrupo(
            PersonaId: 1, CasoId: 1, Nombre: "Ana", Cedula: "055-1111-3853", NumeroCaso: "ARCH2609",
            RutaPdf: null, HayPdf: false, EstadoDeRecomendacion.SinMarcar, MotivoDeNoCompletar.SinMotivo,
            CuantoLeFalta: 0, Dueno: string.Empty, Recomendacion: null, SeQuedoEn: null, Archivado: true,
            LasQueNoDicenSi: ["Entrevistas", "Listo para el templo"]);

        Assert.AreEqual(ColorDeLaPastilla.Verde, archivada.Color);
        Assert.IsFalse(RenglonDelGrupo.DeUnaPersona(archivada).AMedias);
    }

    /// <summary>
    /// En toda la base inventada, naranja es exactamente «me falta a medias», verde «resuelto»
    /// y rojo lo demás; y hay de los tres, o la base no sirve para probarlo.
    /// </summary>
    [TestMethod]
    public void ElColorSigueALaLecturaEnTodaLaBaseInventadaYHayDeLosTres()
    {
        var servicios = BaseDeInicio.MontarServicios(200);
        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var dias = BaseDeInicio.TodosLosCasos(servicios)
            .Select(c => FechasEnEspanol.Leer(c.FechaViaje))
            .OfType<DateOnly>()
            .Distinct()
            .ToList();

        int verdes = 0, naranjas = 0, rojos = 0;
        foreach (var renglon in dias.SelectMany(d => lector.DelDia(d).EnUnaSolaLista()).Where(r => r.EsUnaPersona))
        {
            var esperado = renglon.PalabraDelEstado == DosEstados.Resuelto ? ColorDeLaPastilla.Verde
                : renglon.AMedias ? ColorDeLaPastilla.Naranja
                : ColorDeLaPastilla.Rojo;
            Assert.AreEqual(esperado, renglon.Color, $"«{renglon.Titulo} · {renglon.Detalle}»");
            if (renglon.Color == ColorDeLaPastilla.Verde) verdes++;
            else if (renglon.Color == ColorDeLaPastilla.Naranja) naranjas++;
            else rojos++;
        }

        Console.WriteLine($"{dias.Count} días · {verdes} verdes · {naranjas} naranjas · {rojos} rojos");
        Assert.IsGreaterThan(0, verdes);
        Assert.IsGreaterThan(0, naranjas);
        Assert.IsGreaterThan(0, rojos);
    }

    /// <summary>
    /// La pastilla del calendario y la cabecera de la unidad NO se ponen naranjas: una unidad
    /// con todas sus personas a medias sigue en rojo fuera y dentro.
    /// </summary>
    /// <remarks>
    /// Decisión medida y escrita: la pastilla es la alarma del calendario —«alguien va a viajar
    /// sin la recomendación confirmada»— y una persona a medias sigue sin poder entrar. El
    /// naranja es de la PERSONA, que es donde el dueño lo pidió.
    /// </remarks>
    [TestMethod]
    public void LaPastillaYLaCabeceraSiguenEnRojoConTodasAMedias()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 2);
        BaseDeInicio.ContestarLasSeisDe(servicios, caso, fila: 1, preparacion: true, informacion: true);
        BaseDeInicio.ContestarLasSeisDe(servicios, caso, fila: 2, preparacion: true);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var renglones = lector.DelDia(ElDia).EnUnaSolaLista();

        Assert.AreEqual(ColorDeLaPastilla.Rojo, pastilla.Color);
        Assert.AreEqual(ColorDeLaPastilla.Rojo, renglones.Single(r => r.EsCabecera).Color);
        Assert.IsTrue(renglones.Where(r => r.EsUnaPersona).All(r => r.Color == ColorDeLaPastilla.Naranja));
    }

    /// <summary>El nombre va primero en el renglón y el detalle no lo pisa.</summary>
    [TestMethod]
    public void ElNombreEsElTituloDelRenglon()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var caso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        BaseDeInicio.ContestarLasSeisDe(servicios, caso, fila: 1, preparacion: true);

        var renglon = PersonasDelDia(servicios).Single();
        var persona = servicios.Almacen.Personas.Values.Single(p => p.CasoId == caso);

        Assert.AreEqual(persona.Nombre?.Trim(), renglon.Titulo);
        StringAssert.StartsWith(renglon.ParaElLector, renglon.Titulo);
    }
}
