using Fichas.App.Vocabulario;
using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// FASE C18 — el estado de una persona sale de SUS seis preguntas.
/// </summary>
/// <remarks>
/// <para>Es donde el programa deja de mentir. Hasta hoy
/// <c>LectorDeGrupos.ArmarLaPersona</c> metia <c>caso.Estado</c> dentro de cada
/// <see cref="PersonaDelGrupo"/>: cinco personas del mismo documento salian con el mismo
/// estado aunque tres estuvieran resueltas y dos no.</para>
///
/// <para>Palabras del dueno el 2026-09-05: <i>«El ticket es de cada persona o familia… Es
/// por persona que se revisa la información»</i>.</para>
///
/// <para>⚠️ <b>Todas las pruebas de aqui fabrican un documento con VARIAS personas, y no es
/// un capricho.</b> Los siete escaneos reales del dueno traen UNA persona cada uno (medicion
/// del supervisor, 2026-09-04), asi que sobre sus datos de hoy contar por documentos y
/// contar por personas da el mismo numero y este cambio no se ve. Criterio C18-4.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelEstadoDeUnaPersona
{
    /// <summary>El dia en el que viaja el grupo de estas pruebas.</summary>
    private static readonly DateOnly ElDiaDelViaje = new(2026, 9, 17);

    /// <summary>Las tres respuestas posibles, con nombre para que la prueba se lea.</summary>
    /// <remarks>
    /// Van como variables y no como literales porque el analizador de pruebas rechaza
    /// <c>Assert.AreEqual(true, …)</c> con un booleano escrito a mano. Y con nombre se lee
    /// mejor: «Si» dice mas que «true» cuando lo que se compara son tres estados y no dos.
    /// </remarks>
    private static readonly bool? Si = true;

    /// <summary>La segunda respuesta: alguna de las seis esta marcada que no.</summary>
    private static readonly bool? No = false;

    /// <summary>
    /// C18-1 y C18-4. Cinco personas del MISMO documento pintan cinco renglones con estados
    /// distintos.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es la prueba que demuestra el cambio de unidad.</b> Antes de esta fase, las
    /// cinco habrian salido iguales, porque las cinco copiaban el estado del documento.
    /// </remarks>
    [TestMethod]
    public void CincoPersonasDelMismoDocumentoPintanCincoRenglonesConDosEstados()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "FAMI2609", "2026-09-17", estado: "no_completa", cuantasPersonas: 5);

        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 2);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 3);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 4);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 5);

        var personas = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje).Unidades[0].Personas;

        Assert.HasCount(5, personas, "Un documento con cinco personas son cinco renglones.");
        Assert.AreEqual(1, personas.Select(p => p.EstadoDelDocumento).Distinct().Count(),
            "El documento es uno solo: su estado no cambia de renglón a renglón.");
        Assert.AreEqual(2, personas.Select(p => p.Recomendacion).Distinct().Count(),
            "Las personas NO son una sola: sus seis preguntas dan dos estados distintos.");
        Assert.AreEqual(2, personas.Count(p => p.Recomendacion == true));
        Assert.AreEqual(3, personas.Count(p => p.Recomendacion == false));
    }

    /// <summary>
    /// C18-2. Tres estados en pantalla y no dos, y «sin mirar» NO se pinta como «no lista».
    /// </summary>
    /// <remarks>
    /// El comentario de <c>Pasos.Estado</c> lo justifica: <i>«dar por lista a una persona de
    /// la que faltan preguntas por mirar es exactamente lo que manda a alguien al templo con
    /// la recomendación mal»</i>. Son cosas distintas y se dicen distinto.
    /// </remarks>
    [TestMethod]
    public void SonTresEstadosYSinMirarNoEsNoLista()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "TRES2609", "2026-09-17", cuantasPersonas: 3);

        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 2);
        // La tercera se queda sin contestar: nadie la miro.

        var personas = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje).Unidades[0].Personas;

        Assert.AreEqual(Si, personas[0].Recomendacion, "La primera tiene las seis en sí.");
        Assert.AreEqual(No, personas[1].Recomendacion, "La segunda tiene una en no.");
        Assert.IsNull(personas[2].Recomendacion, "Sin contestar no es «no».");

        // ⛔ 2026-09-07: la frase decía «lista para viajar · recomendación confirmada» o
        // «sin mirar · recomendación sin confirmar», con TRES de las cuatro palabras que el
        // dueño retiró. Lo que estas pruebas defienden es lo que más importa de aquel criterio
        // y NO cambia: que «sin mirar» y «no lista» sigan siendo cosas distintas, porque dar
        // por lista a una persona de la que faltan preguntas por mirar es lo que manda a
        // alguien al templo con la recomendación mal. La palabra es la misma para las dos; el
        // detalle las separa, y eso es lo que se comprueba.
        Assert.AreEqual(DosEstados.Resuelto, personas[0].PalabraDelEstado);
        Assert.AreEqual(DosEstados.MeFalta, personas[1].PalabraDelEstado);
        Assert.AreEqual(DosEstados.MeFalta, personas[2].PalabraDelEstado);
        StringAssert.Contains(personas[1].DetalleDelEstado, "se quedó en", StringComparison.Ordinal);
        StringAssert.Contains(personas[2].DetalleDelEstado, "nadie ha contestado", StringComparison.Ordinal);

        Assert.AreNotEqual(personas[1].RecomendacionTexto, personas[2].RecomendacionTexto,
            "«No lista» y «sin mirar» no pueden leerse igual.");
    }

    /// <summary>C18-3. Cuando una persona no esta lista, se dice EN QUE PASO se quedo.</summary>
    /// <remarks>
    /// Es lo que el dueno le dice al obispo por telefono, y sale de <c>Pasos.SinCompletar</c>,
    /// que ya estaba construido y no lo usaba ninguna pantalla.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaQueNoEstaListaDiceEnQuePasoSeQuedo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "PASO2609", "2026-09-17", cuantasPersonas: 2);

        BaseDeInicio.ContestarLasSeisDe(
            servicios, casoId, fila: 1,
            preparacion: true, informacion: true, citaDelTemplo: true,
            accionesRequeridas: true, entrevistas: false, listoParaElTemplo: true);
        BaseDeInicio.ContestarLasSeisDe(
            servicios, casoId, fila: 2,
            preparacion: false, informacion: true, citaDelTemplo: false,
            accionesRequeridas: true, entrevistas: true, listoParaElTemplo: true);

        var personas = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje).Unidades[0].Personas;

        CollectionAssert.AreEqual(new[] { "Entrevistas" }, personas[0].PasosSinCompletar.ToArray());
        StringAssert.Contains(personas[0].RecomendacionTexto, "se quedó en Entrevistas", StringComparison.Ordinal);

        CollectionAssert.AreEqual(new[] { "Preparación", "Cita del templo" }, personas[1].PasosSinCompletar.ToArray());
        StringAssert.Contains(personas[1].RecomendacionTexto, "Preparación y Cita del templo", StringComparison.Ordinal);
    }

    /// <summary>El renglon de la pantalla lleva la frase de la persona, no la del documento.</summary>
    /// <remarks>
    /// Es lo que se ve de verdad: <c>PaginaDeGrupo.xaml</c> pinta <c>Detalle</c> debajo del
    /// nombre. Si la frase de la persona no llegara ahi, el cambio existiria en el modelo y
    /// no en la pantalla.
    /// </remarks>
    [TestMethod]
    public void ElRenglonDeLaPantallaLlevaLaFraseDeLaPersona()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "RENG2609", "2026-09-17", estado: "completa", cuantasPersonas: 2);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 2);

        var renglones = BaseDeInicio.LectorDeGruposDe(servicios)
            .DelDia(ElDiaDelViaje).EnUnaSolaLista().Where(r => r.EsUnaPersona).ToList();

        Assert.HasCount(2, renglones);
        // ⛔ 2026-09-07: el renglón decía «recomendación confirmada» / «recomendación sin
        // confirmar». Ahora dice una de las dos palabras, y lo que las separaba sigue separado
        // en el DETALLE, que es lo que esta prueba comprueba a continuación: una habla de las
        // seis preguntas contestadas que sí, la otra de dónde se quedó.
        Assert.AreEqual(DosEstados.Resuelto, renglones[0].PalabraDelEstado);
        Assert.AreEqual(DosEstados.MeFalta, renglones[1].PalabraDelEstado);
        StringAssert.Contains(renglones[0].DetalleDelEstado, "dicen que sí", StringComparison.Ordinal);
        StringAssert.Contains(renglones[1].DetalleDelEstado, "líder", StringComparison.Ordinal);
        Assert.AreEqual(Si, renglones[0].Recomendacion, "El primer renglón es de una persona lista.");
        Assert.AreEqual(No, renglones[1].Recomendacion, "El segundo, de una que no lo está.");

        foreach (var renglon in renglones)
        {
            Assert.DoesNotContain("\n", renglon.Detalle, "Ni un párrafo: una línea.");
        }
    }

    /// <summary>
    /// Un documento del que no se leyo a nadie sigue en el grupo, y su renglon dice «sin mirar».
    /// </summary>
    /// <remarks>
    /// No desaparece —eso ya lo garantizaba el C13— y ahora ademas no miente: nadie miro su
    /// recomendacion porque no hay ninguna persona que mirar.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinNingunaPersonaLeidaDiceSinMirarYNoDesaparece()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "NADIE260", "2026-09-17", estado: "completa", cuantasPersonas: 0);

        var personas = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje).Unidades[0].Personas;

        Assert.HasCount(1, personas);
        Assert.IsNull(personas[0].Recomendacion);
        Assert.AreEqual(DosEstados.MeFalta, personas[0].PalabraDelEstado);
        StringAssert.Contains(personas[0].DetalleDelEstado, "nadie ha contestado", StringComparison.Ordinal);
    }

    /// <summary>
    /// C18-5. <c>casos.estado_recomendacion</c> no se toca ni se recalcula: leer el grupo no
    /// escribe nada.
    /// </summary>
    /// <remarks>
    /// ⛔ Regla permanente 5 tal como el dueno la preciso el 2026-09-03: ese estado lo
    /// escribe el Excel del companero, con su nombre. Se mide igual que lo pide el criterio:
    /// el reparto de estados antes y despues tiene que ser el mismo.
    /// </remarks>
    [TestMethod]
    public void LeerElGrupoNoRecalculaNiUnEstadoDeDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var antes = RepartoDeEstados(servicios);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));
        Assert.IsGreaterThan(0, grupo.CuantosDocumentos, "Sin documentos, la medición no diría nada.");

        var despues = RepartoDeEstados(servicios);

        CollectionAssert.AreEquivalent(antes.Keys, despues.Keys, "Apareció o desapareció un estado.");
        foreach (var (estado, cuantos) in antes)
        {
            Assert.AreEqual(cuantos, despues[estado], $"El estado «{estado}» cambió de cuenta.");
        }
    }

    /// <summary>
    /// El estado del DOCUMENTO se sigue diciendo, aparte y con su nombre.
    /// </summary>
    /// <remarks>
    /// No se borro: se separo. Un documento devuelto «no completa» por el Excel de Sandy
    /// sigue diciendolo aunque sus personas esten todas listas, porque son dos respuestas de
    /// dos sitios distintos y taparlas una con otra volveria a mezclar las dos preguntas.
    /// </remarks>
    [TestMethod]
    public void ElEstadoDelDocumentoSigueDiciendoseAparteYConSuNombre()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "APAR2609", "2026-09-17", estado: "no_completa", cuantasPersonas: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);

        var persona = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje).Unidades[0].Personas[0];

        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, persona.EstadoDelDocumento);
        Assert.AreEqual("no completado", persona.EstadoDelDocumentoTexto);
        Assert.AreEqual(Si, persona.Recomendacion, "La persona sí está lista; el documento es otra cosa.");
    }

    /// <summary>La unidad y el dia cuentan PERSONAS confirmadas, no documentos completos.</summary>
    /// <remarks>
    /// El grupo no tiene estado propio (ADR-0006 §3.1): dice cuantas personas de cuantas
    /// estan confirmadas. Un grupo no se «completa»: se vacia de personas sin confirmar.
    /// </remarks>
    [TestMethod]
    public void LaUnidadYElDiaCuentanPersonasConfirmadasYNoDocumentos()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var uno = BaseDeInicio.MeterCaso(servicios, "GRUA2609", "2026-09-17", unidadNumero: "700001", cuantasPersonas: 4);
        var dos = BaseDeInicio.MeterCaso(servicios, "GRUB2609", "2026-09-17", unidadNumero: "700001", cuantasPersonas: 2);

        BaseDeInicio.DejarListaParaViajar(servicios, uno, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, uno, fila: 2);
        BaseDeInicio.DejarNoListaParaViajar(servicios, uno, fila: 3);
        // La cuarta del primero y las dos del segundo se quedan sin mirar.

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDiaDelViaje);

        Assert.AreEqual(2, grupo.CuantosDocumentos);
        Assert.AreEqual(6, grupo.CuantasPersonas);
        Assert.AreEqual(2, grupo.CuantasPersonasConfirmadas);
        Assert.AreEqual(4, grupo.CuantasPersonasSinConfirmar, "Una en «no» y tres sin mirar.");
        Assert.AreEqual(1, grupo.CuantasPersonasNoListas);
        Assert.AreEqual(3, grupo.CuantasPersonasSinMirar);

        // ⚠️ La CIFRA sigue contándose igual y sigue siendo la de personas, que es lo que esta
        // prueba defiende. Lo que cambió el 2026-09-07 es cómo se escribe: la línea decía
        // «2 de 6 personas confirmadas» y ahora dice «me falta 4 de 6», porque «confirmadas»
        // es una de las cuatro palabras que el dueño retiró.
        Assert.AreEqual(2, grupo.Unidades[0].CuantasPersonasConfirmadas);
        StringAssert.Contains(grupo.Unidades[0].Detalle, DosEstados.Cuenta(2, 6), StringComparison.Ordinal);
        StringAssert.Contains(grupo.Unidades[0].Detalle, "4 de 6", StringComparison.Ordinal);

        Assert.AreNotEqual(grupo.CuantosDocumentos, grupo.CuantasPersonasSinConfirmar,
            "Si las dos cifras coincidieran, la prueba no demostraría el cambio de unidad.");
    }

    /// <summary>Cuantos documentos hay con cada estado de recomendacion, ahora mismo.</summary>
    private static Dictionary<string, int> RepartoDeEstados(Fichas.Datos.Falso.ServiciosFalsos servicios)
    {
        var filtro = FiltroDeCasos.Todo with { IncluirArchivados = true };
        return servicios.Casos.Listar(filtro, new Pagina(0, int.MaxValue)).Elementos
            .GroupBy(c => c.EstadoRecomendacion ?? "sin marcar", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }
}
