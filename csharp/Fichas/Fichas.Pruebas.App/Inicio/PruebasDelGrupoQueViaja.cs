using Fichas.App.Vocabulario;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// El grupo que viaja un dia, probado SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// Sale de las palabras del dueno del 2026-09-05: <i>«Cuando el calendario ponga un grupo
/// de personas en una fecha, yo debo poder darle clic y ver solamente al grupo de personas
/// que viajará en esa fecha, con su PDF»</i>, y de los criterios C11 a C13 de
/// <c>PENDIENTES.md</c>.
/// </remarks>
[TestClass]
public sealed class PruebasDelGrupoQueViaja
{
    /// <summary>
    /// El grupo de un dia trae EXACTAMENTE los documentos y las personas de esa fecha, y
    /// dice su denominador.
    /// </summary>
    /// <remarks>
    /// <para>Es el criterio de aceptacion del pase: «el número de personas cuadra con la
    /// base, con el denominador dicho». Se comprueba contra los contratos, no contra el
    /// propio lector: si el grupo dijera una cosa y <c>ICasos</c> otra, la fase no cierra.</para>
    ///
    /// <para>⛔ <b>El denominador son los NO archivados</b> desde el 2026-09-06: lo archivado
    /// no se cuenta en ninguna parte. Antes esta misma prueba comparaba contra la base
    /// entera y comprobaba ademas <c>CuantosArchivados</c>.</para>
    /// </remarks>
    [TestMethod]
    public void ElGrupoDeUnDiaTraeLoDeEseDiaYCuadraConLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var fecha = new DateOnly(2026, 9, 8);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(fecha);

        var enLaBase = BaseDeInicio.TodosLosCasos(servicios)
            .Where(c => c.FechaViaje == "2026-09-08" && !c.Archivado)
            .ToList();
        var personasEnLaBase = enLaBase.Sum(c => Math.Max(1, servicios.Personas.DeCaso(c.Id).Count));

        Assert.AreEqual(enLaBase.Count, grupo.CuantosDocumentos, "El grupo no trae los documentos que hay en la base.");
        Assert.AreEqual(personasEnLaBase, grupo.CuantasPersonas, "El grupo no trae las personas que hay en la base.");
        StringAssert.Contains(grupo.LineaDelDenominador, "documentos", StringComparison.Ordinal);
        StringAssert.Contains(grupo.LineaDelDenominador, "personas", StringComparison.Ordinal);
        Assert.DoesNotContain("ARCHIVADO", grupo.LineaDelDenominador, StringComparison.Ordinal);
    }

    /// <summary>Ni un documento de otra fecha se cuela en el grupo.</summary>
    [TestMethod]
    public void NiUnDocumentoDeOtraFechaSeCuelaEnElGrupo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ESTE2609", "2026-09-08");
        BaseDeInicio.MeterCaso(servicios, "OTRO2609", "2026-09-09");
        BaseDeInicio.MeterCaso(servicios, "NADA0000", fechaViaje: null);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.AreEqual(1, grupo.CuantosDocumentos);
        Assert.AreEqual("ESTE2609", grupo.Unidades.Single().Personas.Single().NumeroCaso);
    }

    /// <summary>
    /// C11-3, la parte que se puede medir sin los PDF: dos documentos con el numero de caso
    /// distinto pero la MISMA fecha caen en el mismo grupo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Este es el caso real que la agrupacion por fecha arregla sola: uno de los siete
    /// escaneos del dueno lleva escrito <c>CASD2609</c> dentro de su propia anotacion en vez
    /// de <c>CASP2609</c> —error de quien lleno el formulario, comprobado por el supervisor
    /// con <c>pypdf</c> el 2026-09-04—. Hoy ese documento queda suelto; agrupando por fecha
    /// aparece con sus hermanos. La fecha lo recupera; el numero no lo recuperaba.
    ///
    /// ⚠️ <b>Lo que esta prueba NO comprueba:</b> los siete escaneos de verdad. NO estan en
    /// el repositorio —<c>.gitignore</c> excluye <c>*.pdf</c>— y nunca lo estaran. Aqui se
    /// reproduce la FORMA del caso con datos inventados; la medicion sobre los siete
    /// archivos reales la tiene que hacer quien los tenga delante.
    /// </remarks>
    [TestMethod]
    public void DosDocumentosConElNumeroDistintoYLaMismaFechaCaenEnElMismoGrupo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-08", unidadNumero: "700001");
        BaseDeInicio.MeterCaso(servicios, "CASD2609", "2026-09-08", unidadNumero: "700001");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.HasCount(1, grupo.Unidades, "Los dos son de la misma unidad: es un solo grupo.");
        var numeros = grupo.Unidades[0].Personas.Select(p => p.NumeroCaso).ToList();
        CollectionAssert.AreEquivalent(new[] { "CASD2609", "CASP2609" }, numeros);
    }

    /// <summary>
    /// El dia se parte por unidad, que es como el dueno habla con los lideres.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es una decision del dueno todavia ABIERTA</b> (ADR-0005 §2.5): ¿el 8 de
    /// septiembre viajan «un grupo de dos unidades» o «dos grupos el mismo dia»? Se
    /// construye partido porque es lo que recomendo el planificador —el habla con un lider
    /// por unidad, no con «el lider del martes»— y porque el dia entero sigue estando en
    /// <c>GrupoDelDia</c>, asi que juntarlo es quitar una cabecera. Si el prefiere lo
    /// contrario, esta prueba es lo primero que cambia.
    /// </remarks>
    [TestMethod]
    public void ElDiaSePartePorUnidadYCadaUnaTraeLoSuyo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "UNAA2609", "2026-09-08", unidadNumero: "700001", cuantasPersonas: 3);
        BaseDeInicio.MeterCaso(servicios, "UNAB2609", "2026-09-08", unidadNumero: "700001", cuantasPersonas: 1);
        BaseDeInicio.MeterCaso(servicios, "OTRA2609", "2026-09-08", unidadNumero: "100027", cuantasPersonas: 2);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.HasCount(2, grupo.Unidades);
        Assert.AreEqual(3, grupo.CuantosDocumentos);
        Assert.AreEqual(6, grupo.CuantasPersonas);

        // La unidad con mas documentos va primero: es con la que hay mas trabajo.
        Assert.AreEqual("700001", grupo.Unidades[0].UnidadNumero);
        Assert.AreEqual(2, grupo.Unidades[0].CuantosDocumentos);
        Assert.HasCount(4, grupo.Unidades[0].Personas);
        Assert.AreEqual("100027", grupo.Unidades[1].UnidadNumero);
    }

    /// <summary>
    /// C11-4. El estado de un grupo mixto es determinista y esta escrito: «N de M completas».
    /// </summary>
    /// <remarks>
    /// El criterio prohibe «el mas grave» a proposito: es una opinion y dos personas la
    /// leerian distinto.
    /// </remarks>
    [TestMethod]
    public void ElEstadoDeUnGrupoMixtoSeDiceComoNDeMCompletas()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "COMP2601", "2026-09-08", estado: "completa");
        BaseDeInicio.MeterCaso(servicios, "COMP2602", "2026-09-08", estado: "completa");
        BaseDeInicio.MeterCaso(servicios, "FALT2603", "2026-09-08", estado: "no_completa");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        // ⚠️ La cuenta de DOCUMENTOS completos sigue siendo la misma y sigue aparte de la de
        // personas; lo que cambió el 2026-09-07 es la redacción: decía «2 de 3 completas».
        Assert.AreEqual(2, grupo.CuantasCompletas);
        StringAssert.Contains(grupo.ComoVa, DosEstados.Cuenta(2, 3), StringComparison.Ordinal);
        StringAssert.Contains(grupo.ComoVa, "1 de 3", StringComparison.Ordinal);
    }

    /// <summary>Un grupo con todo completo lo dice y no anade ningun motivo.</summary>
    [TestMethod]
    public void UnGrupoConTodoCompletoNoAnadeNingunMotivo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "TODO2601", "2026-09-08", estado: "completa");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.AreEqual(DosEstados.Resuelto, grupo.ComoVa, "Sin motivo detrás: no falta ninguno.");
    }

    /// <summary>
    /// El desempate de motivos del C11-4 tiene un orden DECLARADO, no una opinion.
    /// </summary>
    /// <remarks>
    /// ⛔ Hoy los tres motivos no existen en la base: entran con la migracion 18, que la
    /// esta escribiendo otro programador (ADR-0005 §3.3). Lo que se comprueba aqui es la
    /// REGLA del desempate, que ya esta escrita y no depende de la columna: con la misma
    /// cuenta, gana el primero del orden declarado. El dia que la columna exista, esta
    /// prueba sigue valiendo tal cual y solo cambia <c>LectorDeGrupos.MotivoDe</c>.
    /// </remarks>
    [TestMethod]
    public void ElDesempateDeMotivosSigueUnOrdenDeclaradoYNoUnaOpinion()
    {
        var sinCompletar = new List<Caso>
        {
            new() { Id = 1, EstadoRecomendacion = "no_completa", CreadoEn = "2026-09-01" },
            new() { Id = 2, EstadoRecomendacion = null, CreadoEn = "2026-09-01" },
        };

        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, LectorDeGrupos.MotivoQueMasSeRepite(sinCompletar));

        // Un grupo entero completo no tiene motivo que decir.
        var todosCompletos = new List<Caso> { new() { Id = 3, EstadoRecomendacion = "completa", CreadoEn = "2026-09-01" } };
        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, LectorDeGrupos.MotivoQueMasSeRepite(todosCompletos));
    }

    /// <summary>Las tres frases del estado son las del dueno, literales (criterio C13-4).</summary>
    [TestMethod]
    public void LasFrasesDelEstadoSonLasDelDuenoLiterales()
    {
        Assert.AreEqual("completa",
            PalabrasDelEstado.Decir(EstadoDeRecomendacion.Completa, MotivoDeNoCompletar.SinMotivo));
        Assert.AreEqual("sin marcar",
            PalabrasDelEstado.Decir(EstadoDeRecomendacion.SinMarcar, MotivoDeNoCompletar.SinMotivo));
        Assert.AreEqual("no completado",
            PalabrasDelEstado.Decir(EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.SinMotivo));
        Assert.AreEqual("no se pudo comunicar con el líder",
            PalabrasDelEstado.Decir(EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.NoSePudoComunicar));
        Assert.AreEqual("el líder no lo hizo",
            PalabrasDelEstado.Decir(EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo));
    }

    /// <summary>
    /// C13-6. Un documento sin su PDF en la ruta se ve, lo dice, y no rompe la pantalla ni
    /// desaparece del grupo.
    /// </summary>
    [TestMethod]
    public void UnDocumentoSinSuPdfSeVeYLoDice()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SINP2609", "2026-09-08");

        var conPdf = BaseDeInicio.LectorDeGruposDe(servicios, losPdfExisten: true)
            .DelDia(new DateOnly(2026, 9, 8)).Unidades[0].Personas[0];
        var sinPdf = BaseDeInicio.LectorDeGruposDe(servicios, losPdfExisten: false)
            .DelDia(new DateOnly(2026, 9, 8)).Unidades[0].Personas[0];

        Assert.AreEqual("con su PDF", conPdf.PdfTexto);
        Assert.IsFalse(conPdf.ElPdfNoSePuedeAbrir);

        Assert.AreEqual("el PDF no está en su ruta", sinPdf.PdfTexto);
        Assert.IsTrue(sinPdf.ElPdfNoSePuedeAbrir);
        Assert.AreEqual("SINP2609", sinPdf.NumeroCaso, "El documento sin PDF no desaparece del grupo.");
    }

    /// <summary>Un documento del que no se leyo ninguna persona tampoco desaparece.</summary>
    /// <remarks>
    /// Si se cayera, el dueno veria un grupo mas pequeno que el numero que la cabecera
    /// acaba de decir, y no habria forma de saber cual falta.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinNingunaPersonaLeidaTampocoDesaparece()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VACI2609", "2026-09-08", cuantasPersonas: 0);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.AreEqual(1, grupo.CuantosDocumentos);
        var renglon = grupo.Unidades.Single().Personas.Single();
        Assert.AreEqual("sin ninguna persona leída", renglon.Nombre);
        Assert.AreEqual("VACI2609", renglon.NumeroCaso);
    }

    /// <summary>
    /// Un archivado NO sale en el grupo del dia en que viajaba, ni suma en su denominador.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Esto deshace la decision del dueno del 2026-09-03</b>, que pedia lo contrario
    /// —«lo que archivo debe verse en el calendario, debe decir archivado»—, y con ella se
    /// va la version anterior de esta prueba, que se llamaba
    /// <c>LosArchivadosSeVenEnElGrupoMarcadosYSePuedenDejarFuera</c>. Manda lo del
    /// 2026-09-06, con su motivo escrito: <i>«si se queda en el tablero y dice archivado, lo
    /// que hace es que me confunda. Debe pasar a archivado y no aparecer más en ningún
    /// lado»</i>.
    /// <para>Lo que esta prueba sigue vigilando es lo mismo que la anterior: que el grupo
    /// del dia cuente EXACTAMENTE lo que hay, sin colar ni perder documentos. Lo unico que
    /// cambio es de que lado cae el archivado.</para>
    /// </remarks>
    [TestMethod]
    public void UnArchivadoNoSaleEnElGrupoDelDiaEnQueViajaba()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2609", "2026-09-08");
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", archivado: true);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.AreEqual(1, grupo.CuantosDocumentos, "El archivado no se cuenta.");
        Assert.DoesNotContain("ARCHIVADO", grupo.LineaDelDenominador, StringComparison.Ordinal);
        CollectionAssert.AreEquivalent(
            new[] { "VIVO2609" },
            grupo.TodasLasPersonas.Select(p => p.NumeroCaso).ToList(),
            "El archivado no puede aparecer ni como renglón.");
    }

    /// <summary>
    /// Un dia en el que TODO lo que viajaba esta archivado se ensena vacio, no medio lleno.
    /// </summary>
    /// <remarks>
    /// Es el borde del cambio del 2026-09-06: si el ultimo documento vivo se archiva, el dia
    /// no puede quedar con una cabecera que promete un grupo y una lista sin nadie dentro.
    /// </remarks>
    [TestMethod]
    public void UnDiaConTodoArchivadoSeEnsenaVacio()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2601", "2026-09-08", archivado: true);
        BaseDeInicio.MeterCaso(servicios, "ARCH2602", "2026-09-08", archivado: true);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.IsTrue(grupo.EstaVacio);
        Assert.IsEmpty(grupo.Unidades);
        Assert.AreEqual("no viaja nadie este día", grupo.ComoVa);
    }

    /// <summary>Un dia en el que no viaja nadie se ensena vacio y no revienta.</summary>
    [TestMethod]
    public void UnDiaEnElQueNoViajaNadieSeEnsenaVacioYNoRevienta()
    {
        var grupo = BaseDeInicio.LectorDeGruposDe(BaseDeInicio.MontarServicios(0))
            .DelDia(new DateOnly(2026, 9, 8));

        Assert.IsTrue(grupo.EstaVacio);
        Assert.IsEmpty(grupo.Unidades);
        Assert.IsEmpty(grupo.TodosLosCasos);
        Assert.AreEqual("no viaja nadie este día", grupo.ComoVa);
        Assert.AreEqual("martes 8 de septiembre de 2026", grupo.Titulo);
    }

    // ---------------------------------------------- asignar desde el grupo

    /// <summary>
    /// C13-3. Asignar el grupo entero deja UNA fila viva por documento y ni una repetida.
    /// </summary>
    /// <remarks>
    /// Se asigna por <see cref="OperacionDeAsignar"/>, que es la unica puerta de asignar
    /// del programa (criterio C5-1). La pantalla del grupo no tiene una segunda media
    /// operacion: llama a esta y a ninguna otra.
    /// </remarks>
    [TestMethod]
    public void AsignarElGrupoEnteroDejaUnaFilaPorDocumentoYNingunaRepetida()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        for (var i = 1; i <= 7; i++) BaseDeInicio.MeterCaso(servicios, $"CASP260{i}", "2026-09-08", unidadNumero: "700001");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));
        var quien = servicios.Companeros.Activos()[0];
        var operacion = new OperacionDeAsignar(servicios.Asignaciones, servicios.Reloj, new BuzonDeAvisos());

        var resumen = operacion.AsignarVarios(grupo.LosQueSePuedenAsignar, quien.Id, quien.Nombre);

        Assert.AreEqual(7, resumen.Asignados);
        Assert.AreEqual(0, resumen.NoSePudieron);
        Assert.AreEqual(7, servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas));

        var casosConDueno = servicios.Asignaciones
            .Listar(FiltroDeAsignaciones.Activas, new Pagina(0, 100)).Elementos
            .Select(a => a.CasoId).ToList();
        Assert.AreEqual(7, casosConDueno.Distinct().Count(), "Un documento no puede tener dos asignaciones vivas.");
    }

    /// <summary>
    /// Asignar el grupo entero NO asigna los archivados: estan cerrados y ya ni se ven.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta prueba nace de un defecto MEDIDO, no de una idea.</b> El 2026-09-05, sobre
    /// el paquete publicado y con la base de verdad delante, «Asignar el grupo entero» sobre
    /// el 8 de septiembre dejo <b>8 filas</b> en <c>asignaciones</c> —siete documentos vivos
    /// y uno archivado—, y el archivado no debia entrar. <b>Ese defecto es lo que esta
    /// prueba vigila y sigue vigilandolo</b>: ni una fila de asignacion sobre algo cerrado.
    /// <para>⛔ Lo que cambio el 2026-09-06 es que el archivado ya no se VE en el grupo: la
    /// linea que decia «el archivado se sigue viendo en el grupo» afirmaba la decision del
    /// 2026-09-03, que el dueno deshizo.</para>
    /// </remarks>
    [TestMethod]
    public void AsignarElGrupoEnteroNoAsignaLosArchivados()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2601", "2026-09-08");
        BaseDeInicio.MeterCaso(servicios, "VIVO2602", "2026-09-08");
        var archivadoId = BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", archivado: true);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));
        var quien = servicios.Companeros.Activos()[0];
        var operacion = new OperacionDeAsignar(servicios.Asignaciones, servicios.Reloj, new BuzonDeAvisos());

        Assert.HasCount(2, grupo.TodosLosCasos, "El archivado ya no se ve en el grupo.");
        Assert.HasCount(2, grupo.LosQueSePuedenAsignar);

        var resumen = operacion.AsignarVarios(grupo.LosQueSePuedenAsignar, quien.Id, quien.Nombre);

        Assert.AreEqual(2, resumen.Asignados);
        Assert.AreEqual(0, servicios.Asignaciones.Contar(
            FiltroDeAsignaciones.Activas with { CasoId = archivadoId }),
            "Un documento archivado no puede acabar en manos de un compañero.");
    }

    /// <summary>Una unidad entera archivada no aparece en el grupo, y menos con boton.</summary>
    /// <remarks>
    /// La version anterior de esta prueba comprobaba que se veia apagada, con su documento
    /// contado y sin boton de asignar. Lo que vigilaba —que de ahi no salga trabajo para
    /// nadie— se conserva; lo que cambia es que ahora no llega ni a pintarse.
    /// </remarks>
    [TestMethod]
    public void UnaUnidadEnteraArchivadaNoAparece()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2601", "2026-09-08", archivado: true, unidadNumero: "990000");
        BaseDeInicio.MeterCaso(servicios, "VIVO2602", "2026-09-08", unidadNumero: "700001");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));

        Assert.HasCount(1, grupo.Unidades, "La unidad archivada entera no se ensena.");
        Assert.AreEqual("700001", grupo.Unidades[0].UnidadNumero);
        Assert.IsTrue(grupo.Unidades[0].SePuedeAsignar);

        var cabeceras = grupo.EnUnaSolaLista().Where(r => r.EsCabecera).ToList();
        Assert.HasCount(1, cabeceras);
        Assert.AreEqual(1, cabeceras.Count(r => r.SePuedeAsignarLaUnidad),
            "Solo la unidad viva ofrece su botón de asignar.");
    }

    /// <summary>Asignar dos veces el mismo grupo NO duplica: la segunda no entra.</summary>
    [TestMethod]
    public void AsignarDosVecesElMismoGrupoNoDuplicaLasAsignaciones()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        for (var i = 1; i <= 3; i++) BaseDeInicio.MeterCaso(servicios, $"CASP260{i}", "2026-09-08");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));
        var quien = servicios.Companeros.Activos()[0];
        var operacion = new OperacionDeAsignar(servicios.Asignaciones, servicios.Reloj, new BuzonDeAvisos());

        operacion.AsignarVarios(grupo.LosQueSePuedenAsignar, quien.Id, quien.Nombre);
        operacion.AsignarVarios(grupo.LosQueSePuedenAsignar, quien.Id, quien.Nombre);

        Assert.AreEqual(3, servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas),
            "El mismo compañero no puede tener dos asignaciones vivas del mismo documento.");
    }

    /// <summary>Se puede asignar SOLO una unidad del dia, sin tocar las otras.</summary>
    [TestMethod]
    public void SePuedeAsignarSoloUnaUnidadDelDiaSinTocarLasOtras()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "UNAA2609", "2026-09-08", unidadNumero: "700001");
        BaseDeInicio.MeterCaso(servicios, "UNAB2609", "2026-09-08", unidadNumero: "700001");
        BaseDeInicio.MeterCaso(servicios, "OTRA2609", "2026-09-08", unidadNumero: "100027");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8));
        var unidad = grupo.Unidades.Single(u => u.UnidadNumero == "700001");
        var quien = servicios.Companeros.Activos()[0];
        var operacion = new OperacionDeAsignar(servicios.Asignaciones, servicios.Reloj, new BuzonDeAvisos());

        operacion.AsignarVarios(unidad.CasosQueSePuedenAsignar, quien.Id, quien.Nombre);

        Assert.AreEqual(2, servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas));
        var otra = grupo.Unidades.Single(u => u.UnidadNumero == "100027");
        Assert.AreEqual(0, servicios.Asignaciones.Contar(
            FiltroDeAsignaciones.Activas with { CasoId = otra.CasoIds[0] }));
    }

    /// <summary>Tras asignar, el grupo dice quien lleva cada documento.</summary>
    [TestMethod]
    public void TrasAsignarElGrupoDiceQuienLlevaCadaDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-08");
        var quien = servicios.Companeros.Activos()[0];
        BaseDeInicio.Asignar(servicios, casoId, quien.Id);

        var persona = BaseDeInicio.LectorDeGruposDe(servicios)
            .DelDia(new DateOnly(2026, 9, 8)).Unidades[0].Personas[0];

        Assert.AreEqual(quien.Nombre, persona.Dueno);
        StringAssert.Contains(persona.Detalle, quien.Nombre, StringComparison.Ordinal);
    }

    // ---------------------------------------------- la lista que pinta la pantalla

    /// <summary>
    /// C13-5. La lista va aplanada: una cabecera por unidad y detras sus personas.
    /// </summary>
    /// <remarks>
    /// Es lo que permite que UN solo repetidor virtualice el grupo entero. Un repetidor
    /// dentro de otro construiria las 517 personas del dia mas cargado de una base de 3 000.
    /// </remarks>
    [TestMethod]
    public void LaListaDelGrupoVaAplanadaConUnaCabeceraPorUnidad()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "UNAA2609", "2026-09-08", unidadNumero: "700001", cuantasPersonas: 2);
        BaseDeInicio.MeterCaso(servicios, "OTRA2609", "2026-09-08", unidadNumero: "100027", cuantasPersonas: 1);

        var renglones = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8)).EnUnaSolaLista();

        // Las dos unidades traen un documento cada una, asi que el desempate es por su
        // titulo: «100027 …» va antes que «700001 …».
        Assert.HasCount(5, renglones, "Dos cabeceras y tres personas.");
        Assert.IsTrue(renglones[0].EsCabecera);
        StringAssert.StartsWith(renglones[0].Titulo, "100027", StringComparison.Ordinal);
        Assert.HasCount(1, renglones[0].CasosDeLaUnidad);
        Assert.IsFalse(renglones[1].EsCabecera);
        Assert.IsTrue(renglones[1].EsUnaPersona);
        Assert.IsTrue(renglones[2].EsCabecera);
        StringAssert.StartsWith(renglones[2].Titulo, "700001", StringComparison.Ordinal);
        Assert.AreEqual(2, renglones.Count(r => r.EsCabecera));
        Assert.AreEqual(3, renglones.Count(r => r.EsUnaPersona));
    }

    /// <summary>Las personas de un documento archivado no tienen renglon en la lista.</summary>
    /// <remarks>
    /// Antes esta prueba se llamaba <c>UnaPersonaDeUnDocumentoArchivadoLlevaLaPalabraArchivado</c>
    /// y exigia el texto «ARCHIVADO» en el renglon. Lo que vigila sigue siendo lo mismo —que
    /// la lista aplanada diga del archivado lo que el dueno pidio— y hoy pide que no diga nada
    /// porque no hay renglon (2026-09-06).
    /// </remarks>
    [TestMethod]
    public void UnDocumentoArchivadoNoPoneNingunRenglonEnLaLista()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2608", "2026-09-08");
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", archivado: true);

        var renglones = BaseDeInicio.LectorDeGruposDe(servicios)
            .DelDia(new DateOnly(2026, 9, 8)).EnUnaSolaLista();

        var personas = renglones.Where(r => r.EsUnaPersona).ToList();
        Assert.HasCount(1, personas);
        StringAssert.Contains(personas[0].Detalle, "VIVO2608", StringComparison.Ordinal);
        Assert.IsEmpty(renglones.Where(r => r.ParaElLector.Contains("ARCHIVADO", StringComparison.Ordinal)),
            "Ni el lector de pantalla dice la palabra.");
    }

    /// <summary>Cada renglon dice en voz alta lo suficiente para navegarlo sin ver.</summary>
    [TestMethod]
    public void CadaRenglonDelGrupoSeSabeDecirEnVozAlta()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-08", unidadNumero: "700001");

        var renglones = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8)).EnUnaSolaLista();

        StringAssert.Contains(renglones[0].ParaElLector, "700001", StringComparison.Ordinal);
        StringAssert.Contains(renglones[1].ParaElLector, "Pulse para verificar", StringComparison.Ordinal);
    }

    // ---------------------------------------------- lo que el calendario pinta

    /// <summary>
    /// C12-2. Una pastilla del calendario es un GRUPO —una unidad de ese dia— y no un
    /// documento suelto.
    /// </summary>
    [TestMethod]
    public void UnaPastillaDelCalendarioEsUnGrupoYNoUnDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        for (var i = 1; i <= 4; i++)
        {
            BaseDeInicio.MeterCaso(servicios, $"CASP260{i}", "2026-09-08", unidadNumero: "700001", cuantasPersonas: 2);
        }

        var dia = BaseDeInicio.LeerInicio(servicios).Mes.Dias.Single(d => d.EsDelMes && d.Numero == 8);

        Assert.HasCount(1, dia.Pastillas, "Cuatro documentos de la misma unidad son UN grupo, no cuatro pastillas.");
        Assert.AreEqual(0, dia.CuantasMas);
        Assert.AreEqual("700001", dia.Pastillas[0].Titulo);
        Assert.AreEqual(4, dia.Pastillas[0].CuantosDocumentos);
        Assert.AreEqual(8, dia.Pastillas[0].CuantasPersonas);

        // ⚠️ La etiqueta cambio de unidad el 2026-09-05 (criterio C20-3): antes decia
        // «8 pers. · 0/4», donde el 0 y el 4 eran DOCUMENTOS completos. Ahora cuenta
        // PERSONAS, que es lo que el dueno hace con esa cifra: «faltan 3, 4 o 5 personas que la
        // recomendación no está confirmada». Ninguna de las ocho tiene sus seis preguntas
        // contestadas, asi que le faltan las ocho.
        // ⛔ Y el 2026-09-07 dejo de decir «0 de 8 confirmadas»: la cifra se queda y la palabra
        // pasa a ser una de las dos.
        Assert.AreEqual("me falta 8 de 8", dia.Pastillas[0].Etiqueta);
        Assert.AreEqual(0, dia.Pastillas[0].CuantasPersonasConfirmadas, "La cifra de confirmadas no se toca.");
    }

    /// <summary>El calendario y la pantalla del grupo cuentan lo mismo para el mismo dia.</summary>
    /// <remarks>
    /// Si la pastilla dijera 7 documentos y al entrar hubiera 6, el dueno no tendria forma
    /// de saber cual de las dos cifras creerse. Se comprueba sobre la base grande.
    /// </remarks>
    [TestMethod]
    public void ElCalendarioYLaPantallaDelGrupoCuentanLoMismoParaElMismoDia()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var fecha = new DateOnly(2026, 9, 8);

        var dia = BaseDeInicio.LeerInicio(servicios).Mes.Dias.Single(d => d.Fecha == fecha);
        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(fecha);

        Assert.AreEqual(grupo.Unidades.Count, dia.Pastillas.Count + dia.CuantasMas,
            "El calendario y el grupo no cuentan el mismo número de unidades.");

        var documentosEnLasPastillas = dia.Pastillas.Sum(p => p.CuantosDocumentos);
        var documentosEnLasMismasUnidades = grupo.Unidades
            .Take(dia.Pastillas.Count).Sum(u => u.CuantosDocumentos);
        Assert.AreEqual(documentosEnLasMismasUnidades, documentosEnLasPastillas);
    }
}
