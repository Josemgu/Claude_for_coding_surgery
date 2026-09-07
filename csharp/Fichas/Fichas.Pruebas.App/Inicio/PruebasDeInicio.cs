using Fichas.App.Cascara;
using Fichas.App.Inicio;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// Las reglas de la pantalla de Inicio, probadas SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// Cada prueba nace de un criterio de aceptacion o de una frase del dueno, NO de mirar el
/// codigo ya escrito. Si una prueba se escribiera mirando la implementacion solo
/// confirmaria lo que el programador entendio, y pasaria en verde estando mal.
///
/// La frase que ordena este archivo entero es del 2026-09-05: <i>«Lo único que quiero ver
/// en Home es lo que está listo para asignar y lo que está asignado a los agentes»</i>.
/// </remarks>
[TestClass]
public sealed class PruebasDeInicio
{
    // ---------------------------------------------- las dos cosas, y ninguna mas

    /// <summary>
    /// Un documento sin nada pendiente y sin nadie que lo lleve sale en «listo para asignar».
    /// </summary>
    /// <remarks>
    /// Es la lectura que el dueno describio el 2026-09-05: <i>«Si el sistema escanea y
    /// verifica todos los campos sin mi intervención, debe decir "listo para asignar"»</i>.
    /// No hace falta que el firme nada.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinHuecosYSinDuenoSaleComoListoParaAsignar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "LIST2609", "2026-09-08", cuantasPersonas: 2);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        var renglon = resumen.Listos.Single(r => r.NumeroCaso == "LIST2609");
        Assert.AreEqual(0, renglon.CuantoLeFalta);
        Assert.AreEqual("listo para asignar", renglon.LoQueFaltaTexto);
        Assert.AreEqual(2, renglon.CuantasPersonas);
        Assert.AreEqual(1, resumen.Contadores.ListoParaAsignar);
        Assert.AreEqual(2, resumen.Contadores.PersonasListas);
    }

    /// <summary>
    /// A un documento con un campo vacio NO se le llama listo, y no sale en Inicio.
    /// </summary>
    /// <remarks>
    /// Sus palabras: «ninguno vacío, ninguno dudoso, ninguno tachado sin corregir». Se
    /// comprueba con el templo, que es el campo que la importacion deja vacio mas a menudo.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoConUnCampoVacioNoEsListoYNoSaleEnInicio()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.IsEmpty(resumen.Listos, "Un documento con un hueco no puede salir como listo.");
        Assert.IsEmpty(resumen.Asignados, "Y tampoco como asignado: no lo lleva nadie.");
        Assert.AreEqual(0, resumen.Contadores.ListoParaAsignar);
    }

    /// <summary>Una cédula que no cumple su forma cuenta como hueco: «ninguno dudoso».</summary>
    [TestMethod]
    public void UnaCedulaQueNoCumpleSuFormaImpideQueSeaListo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "DUDA2609", "2026-09-08");
        var persona = servicios.Almacen.Personas.Values.Single(p => p.CasoId == casoId);
        servicios.Almacen.Personas[persona.Id] = persona with { Mrn = "123" };

        Assert.IsEmpty(BaseDeInicio.LeerInicio(servicios).Listos);
    }

    /// <summary>
    /// Un documento que ya lleva alguien sale como asignado y NO vuelve a salir como listo.
    /// </summary>
    /// <remarks>
    /// Las dos listas no se solapan: ofrecerle otra vez como «listo» un documento que ya
    /// esta en manos de un companero seria pedirle que lo asigne dos veces.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoAsignadoSaleComoAsignadoYNoComoListo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "TIEN2609", "2026-09-08");
        var quien = servicios.Companeros.Activos()[0];
        BaseDeInicio.Asignar(servicios, casoId, quien.Id);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.IsEmpty(resumen.Listos);
        var renglon = resumen.Asignados.Single(r => r.NumeroCaso == "TIEN2609");
        Assert.AreEqual(quien.Nombre, renglon.Dueno);
        Assert.AreEqual(1, resumen.Contadores.AsignadoALosAgentes);
    }

    /// <summary>
    /// Ni una de las dos listas se solapa con la otra, con la base grande.
    /// </summary>
    [TestMethod]
    public void LasDosListasDeInicioNoComparteNiUnDocumento()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba));

        var listos = resumen.Listos.Select(r => r.CasoId).ToHashSet();
        var asignados = resumen.Asignados.Select(r => r.CasoId).ToHashSet();

        Assert.IsGreaterThan(0, listos.Count, "No hay nada listo en la base de prueba.");
        Assert.IsGreaterThan(0, asignados.Count, "No hay nada asignado en la base de prueba.");
        Assert.IsEmpty(listos.Intersect(asignados), "Un documento no puede estar en las dos listas.");
    }

    /// <summary>El cuadro de cuanto lleva cada companero cuadra con lo que dicen los contratos.</summary>
    [TestMethod]
    public void CuantoLlevaCadaCompaneroCuadraConLoQueDicenLosContratos()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var resumen = BaseDeInicio.LeerInicio(servicios);

        foreach (var renglon in resumen.Equipo.Where(r => r.Id != 0))
        {
            Assert.AreEqual(
                servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = renglon.Id }),
                renglon.Casos,
                $"Los casos de {renglon.Nombre} no coinciden con IAsignaciones.Contar.");

            Assert.AreEqual(
                servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = renglon.Id, SinDevolver = true }),
                renglon.SinDevolver,
                $"Las hojas sin devolver de {renglon.Nombre} no coinciden con IAsignaciones.Contar.");
        }

        Assert.AreEqual(
            servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { SinDevolver = true }),
            resumen.Contadores.SinDevolver);
    }

    /// <summary>Un companero desactivado no sale; se desactivan, no se borran.</summary>
    [TestMethod]
    public void UnCompaneroDesactivadoNoSaleEnElCuadroDelEquipo()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var activos = servicios.Companeros.Activos().Select(c => c.Nombre).ToHashSet();

        foreach (var renglon in BaseDeInicio.LeerInicio(servicios).Equipo.Where(r => r.Id != 0))
        {
            Assert.Contains(renglon.Nombre, activos, $"{renglon.Nombre} está desactivado y no debería salir.");
        }
    }

    /// <summary>Los documentos que no lleva nadie salen como «Sin asignar», que es la fila con id 0.</summary>
    [TestMethod]
    public void LosDocumentosQueNoLlevaNadieSalenComoSinAsignar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SOLO2609", "2026-09-20");

        var sinAsignar = BaseDeInicio.LeerInicio(servicios).Equipo.Single(r => r.Id == 0);

        Assert.AreEqual("Sin asignar", sinAsignar.Nombre);
        Assert.AreEqual(1, sinAsignar.Casos);
    }

    // ---------------------------------------------- el orden y el archivado

    /// <summary>
    /// Lo que viaja antes sale arriba, y lo que ya viajo baja detras.
    /// </summary>
    /// <remarks>
    /// Sus palabras: <i>«la prioridad son los que viajarán pronto»</i>. Ordenar solo por
    /// fecha pondria agosto encima de septiembre, que es lo contrario.
    /// </remarks>
    [TestMethod]
    public void EnInicioLoQueViajaAntesSaleArribaYLoQueYaViajoBajaDetras()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "TARD2609", "2026-08-20");
        BaseDeInicio.MeterCaso(servicios, "LEJO2611", "2026-11-20");
        BaseDeInicio.MeterCaso(servicios, "PRON2609", "2026-09-06");

        var listos = BaseDeInicio.LeerInicio(servicios).Listos.Select(r => r.NumeroCaso).ToList();

        CollectionAssert.AreEqual(
            new[] { "PRON2609", "LEJO2611", "TARD2609" },
            listos,
            "El orden tiene que ser: lo que viaja pronto, lo que viaja lejos y detrás lo que ya viajó.");
    }

    /// <summary>
    /// Y en la lista de lo asignado, lo que no tiene fecha va al final y NO desaparece.
    /// </summary>
    /// <remarks>
    /// Criterio C7-4. Se comprueba en esta lista y no en la de lo listo porque un documento
    /// sin fecha de viaje nunca es «listo para asignar»: la fecha es uno de los cinco campos
    /// que el sistema tiene que traer, y sin ella no se sabe cuándo viaja esa gente.
    /// </remarks>
    [TestMethod]
    public void EnLoAsignadoLoQueNoTieneFechaVaAlFinalYNoDesaparece()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var quien = BaseDeInicio.PrimerCompaneroActivo(servicios);
        BaseDeInicio.Asignar(servicios, BaseDeInicio.MeterCaso(servicios, "NADA0000", fechaViaje: null), quien);
        BaseDeInicio.Asignar(servicios, BaseDeInicio.MeterCaso(servicios, "TARD2609", "2026-08-20"), quien);
        BaseDeInicio.Asignar(servicios, BaseDeInicio.MeterCaso(servicios, "PRON2609", "2026-09-06"), quien);

        var asignados = BaseDeInicio.LeerInicio(servicios).Asignados.Select(r => r.NumeroCaso).ToList();

        CollectionAssert.AreEqual(new[] { "PRON2609", "TARD2609", "NADA0000" }, asignados);
    }

    /// <summary>
    /// Un documento que el companero devolvio marcado «no completa» NO es «listo para
    /// asignar», aunque en el sistema no le falte ni un dato.
    /// </summary>
    /// <remarks>
    /// El dueno llama «lo que no está completo» exactamente a eso, y la regla permanente 5
    /// dice que ese estado lo escribe el Excel del companero, con su nombre. Ponerlo a la
    /// vez en Inicio y en la ventana de incompletos seria decirle dos cosas distintas del
    /// mismo documento.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoQueElCompaneroDevolvioNoCompletaNoEsListoParaAsignar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "DIJO2609", "2026-09-08", estado: "no_completa");

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.IsEmpty(resumen.Listos);
        Assert.AreEqual(0, resumen.Contadores.ListoParaAsignar);
    }

    /// <summary>
    /// Un documento archivado NO sale en ninguna de las dos listas de Inicio.
    /// </summary>
    /// <remarks>
    /// Regla del dueno del 2026-09-05: <i>«cuando yo archive, debe salir del sistema visible
    /// pero se queda como histórico para los reportes»</i>.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoArchivadoNoSaleEnNingunaDeLasDosListasDeInicio()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", archivado: true, cuantasPersonas: 5);
        BaseDeInicio.Asignar(servicios, casoId, BaseDeInicio.PrimerCompaneroActivo(servicios));

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.IsEmpty(resumen.Listos);
        Assert.IsEmpty(resumen.Asignados);
        Assert.AreEqual(0, resumen.Contadores.ListoParaAsignar);
        Assert.AreEqual(0, resumen.Contadores.AsignadoALosAgentes);
        Assert.AreEqual(0, resumen.Denominadores.CasosNoArchivados);
        Assert.AreEqual(1, resumen.Denominadores.CasosEnLaBase);
    }

    /// <summary>
    /// Y TAMPOCO sale en el calendario: ni pastilla, ni palabra, ni cuenta.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Esta prueba afirmaba justo lo contrario hasta el 2026-09-06</b>, y se llamaba
    /// <c>UnDocumentoArchivadoSiSaleEnElCalendarioConLaPalabraArchivado</c>: venia de los
    /// criterios C7-2 y C12-4, que recogian lo que el dueno pidio el 2026-09-03 —la PALABRA
    /// y no un color—. El dueno lo deshizo con su motivo: <i>«si se queda en el tablero y
    /// dice archivado, lo que hace es que me confunda»</i>.
    /// <para>Lo que sigue vigilando: que el calendario y la vista de trabajo digan lo MISMO
    /// de un documento archivado. Antes las dos mitades eran deliberadamente distintas;
    /// ahora coinciden, y esta prueba es la que lo ata.</para>
    /// </remarks>
    [TestMethod]
    public void UnDocumentoArchivadoTampocoSaleEnElCalendario()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-10", archivado: true, cuantasPersonas: 2, unidadNumero: "9999999");

        var mes = BaseDeInicio.LeerInicio(servicios).Mes;
        var pastillas = mes.Dias.SelectMany(d => d.Pastillas).ToList();

        Assert.IsEmpty(pastillas, "Un día cuyo único documento está archivado no pinta ninguna pastilla.");
        Assert.AreEqual(0, mes.Dias.Sum(d => d.CuantasMas), "Ni se cuenta en el «+N más».");
    }

    /// <summary>
    /// Un grupo con un archivado dentro cuenta SOLO lo vivo, y no dice la palabra.
    /// </summary>
    /// <remarks>
    /// ⛔ Antes se llamaba <c>UnGrupoMitadArchivadoDiceCuantasCompletasYCuantosArchivados</c> y
    /// exigia «1 de 2 confirmadas · 1 ARCHIVADO». Lo que vigila —que la pastilla no mienta
    /// sobre cuantas personas quedan por verificar— se conserva entero; lo que cambia es que
    /// el trozo archivado ya no entra en la cuenta en vez de entrar marcado.
    /// <para>El sitio donde esa cuenta sigue estando completa es Reportes, que no se toco.</para>
    /// </remarks>
    [TestMethod]
    public void UnGrupoMitadArchivadoCuentaSoloLoVivo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var uno = BaseDeInicio.MeterCaso(servicios, "MIXT2601", "2026-09-10", estado: "completa", unidadNumero: "9999999");
        BaseDeInicio.MeterCaso(servicios, "MIXT2602", "2026-09-10", archivado: true, unidadNumero: "9999999");
        BaseDeInicio.DejarListaParaViajar(servicios, uno, fila: 1);

        var pastilla = BaseDeInicio.LeerInicio(servicios).Mes.Dias
            .SelectMany(d => d.Pastillas)
            .Single(p => p.UnidadNumero == "9999999");

        Assert.AreEqual(1, pastilla.CuantosDocumentos, "El archivado no se cuenta como documento del grupo.");
        Assert.AreEqual(1, pastilla.CuantasCompletas, "El estado del documento se sigue contando aparte.");

        // ⚠️ La etiqueta cuenta PERSONAS confirmadas desde el criterio C20-3, no documentos
        // completos: queda una sola persona a la vista, con sus seis preguntas en sí.
        Assert.AreEqual("1 de 1 confirmadas", pastilla.Etiqueta);
    }

    /// <summary>
    /// Desarchivar lo devuelve a TODAS partes: al calendario, al grupo del día y a los avisos.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la otra mitad de la decision del 2026-09-06 y la que la hace reversible: si
    /// archivar sacara de la vista SIN vuelta, un clic mal dado escondería un caso para
    /// siempre. La vuelta se da desde Revisar, con «Ver los archivados» —el unico sitio a
    /// proposito— y por la misma puerta que archiva, <see cref="AccionesDeRevisar"/>.
    /// </remarks>
    [TestMethod]
    public void DesarchivarLoDevuelveAlCalendarioYAlGrupoYALosAvisos()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "VUEL2609", "2026-09-08", cuantasPersonas: 1, unidadNumero: "9999999");
        var acciones = new AccionesDeRevisar(servicios.Casos, servicios.Reloj, new BuzonDeAvisos());

        acciones.ArchivarEnLote([casoId]);
        var archivado = BaseDeInicio.LeerInicio(servicios);
        Assert.IsEmpty(archivado.Mes.Dias.SelectMany(d => d.Pastillas), "Archivado: no está en el calendario.");
        Assert.IsNull(archivado.ElSistemaDelObispo.ProximoGrupo, "Archivado: no hay grupo que venga.");
        Assert.IsEmpty(BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8)).Unidades);

        acciones.DesarchivarEnLote([casoId]);
        var devuelto = BaseDeInicio.LeerInicio(servicios);

        Assert.HasCount(1, devuelto.Mes.Dias.SelectMany(d => d.Pastillas).ToList(), "Desarchivado: vuelve al calendario.");
        Assert.IsNotNull(devuelto.ElSistemaDelObispo.ProximoGrupo, "Desarchivado: vuelve a ser el grupo que viene.");
        Assert.AreEqual(1, devuelto.Denominadores.CasosNoArchivados);
        Assert.HasCount(1, BaseDeInicio.LectorDeGruposDe(servicios).DelDia(new DateOnly(2026, 9, 8)).Unidades,
            "Desarchivado: vuelve al grupo de su día.");
    }

    // ---------------------------------------------- el calendario

    /// <summary>C7-5 y C1-5. El calendario es de tamano fijo: 6 semanas de 7 dias, empiece donde empiece el mes.</summary>
    [TestMethod]
    public void ElMesTieneSiempreSeisSemanasDeSieteDiasYEmpiezaEnLunes()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba));

        Assert.HasCount(42, resumen.Mes.Dias);
        Assert.AreEqual("septiembre de 2026", resumen.Mes.Titulo);
        Assert.AreEqual(1, resumen.Mes.Dias[1].Numero, "El 1 de septiembre de 2026 es martes: la segunda celda.");
        Assert.IsFalse(resumen.Mes.Dias[0].EsDelMes, "La primera celda es el 31 de agosto, de fuera del mes.");
        Assert.AreEqual(new DateOnly(2026, 8, 31), resumen.Mes.Dias[0].Fecha);
    }

    /// <summary>C7-2. Hoy viene marcado, y los siete dias siguientes vienen dentro de la ventana.</summary>
    [TestMethod]
    public void ElDiaDeHoyVieneMarcadoYLaVentanaDeSieteDiasTambien()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba));

        var hoy = resumen.Mes.Dias.Single(d => d.EsHoy);
        Assert.AreEqual(4, hoy.Numero);
        Assert.IsTrue(hoy.EnLaVentana);

        var enLaVentana = resumen.Mes.Dias.Where(d => d.EsDelMes && d.EnLaVentana).Select(d => d.Numero).ToList();
        CollectionAssert.AreEqual(new[] { 4, 5, 6, 7, 8, 9, 10, 11 }, enLaVentana,
            "La ventana va de hoy a hoy+7, los dos incluidos.");
    }

    /// <summary>
    /// C12-2 y C1-5. Un dia con muchos grupos ensena tres y dice cuantos quedan; el numero
    /// de elementos del calendario no crece con los datos.
    /// </summary>
    [TestMethod]
    public void UnDiaConMuchosGruposEnsenaTresPastillasYDiceCuantasQuedan()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        for (var i = 0; i < 10; i++)
        {
            BaseDeInicio.MeterCaso(servicios, $"LLEN260{i}", "2026-09-15", unidadNumero: $"800000{i}");
        }

        var dia = BaseDeInicio.LeerInicio(servicios).Mes.Dias.Single(d => d.EsDelMes && d.Numero == 15);

        Assert.HasCount(3, dia.Pastillas);
        Assert.AreEqual(7, dia.CuantasMas);
        Assert.AreEqual("+7 más", dia.TextoDeLasQueFaltan);
        Assert.IsTrue(dia.SePuedePulsar);
    }

    /// <summary>
    /// C12-6. Un dia sin grupos NO es pulsable: no hace nada y se ve que no hace nada.
    /// </summary>
    [TestMethod]
    public void UnDiaSinGruposNoEsPulsable()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SOLO2609", "2026-09-15");

        var dias = BaseDeInicio.LeerInicio(servicios).Mes.Dias;

        Assert.IsTrue(dias.Single(d => d.EsDelMes && d.Numero == 15).SePuedePulsar);
        Assert.IsFalse(dias.Single(d => d.EsDelMes && d.Numero == 16).SePuedePulsar);
        Assert.IsEmpty(dias.Single(d => d.EsDelMes && d.Numero == 16).Pastillas);
    }

    /// <summary>
    /// C12-5. La celda lleva su FECHA entera, que es lo que hace falta para abrir su grupo.
    /// </summary>
    /// <remarks>
    /// Antes del 2026-09-05 la celda solo sabia su numero del mes, y con eso no se puede
    /// abrir nada: el 8 de la celda de relleno de agosto y el 8 de septiembre son el mismo
    /// numero y grupos distintos.
    /// </remarks>
    [TestMethod]
    public void CadaCeldaDelCalendarioSabeQueDiaEsYNoSoloQueNumeroTiene()
    {
        var dias = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(0)).Mes.Dias;

        Assert.AreEqual(new DateOnly(2026, 9, 8), dias.Single(d => d.EsDelMes && d.Numero == 8).Fecha);
        Assert.AreEqual(new DateOnly(2026, 8, 31), dias[0].Fecha);
        Assert.AreEqual(42, dias.Select(d => d.Fecha).Distinct().Count(), "Las 42 celdas son 42 días distintos.");
    }

    /// <summary>El mes se puede mover adelante y atras y cambia su titulo.</summary>
    [TestMethod]
    public void ElMesSeMueveAdelanteYAtrasYCambiaSuTitulo()
    {
        var lector = BaseDeInicio.LectorDe(BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba));

        Assert.AreEqual("agosto de 2026", lector.Leer(-1).Mes.Titulo);
        Assert.AreEqual("septiembre de 2026", lector.Leer(0).Mes.Titulo);
        Assert.AreEqual("octubre de 2026", lector.Leer(1).Mes.Titulo);
    }

    // ---------------------------------------------- denominadores y avisos

    /// <summary>C1-1. El denominador se dice siempre, y desde el 2026-09-06 no nombra el archivo.</summary>
    /// <remarks>
    /// ⚠️ Esta prueba EXIGIA que la linea dijera «sin archivar». El dueno lo deshizo:
    /// «debe pasar a archivado y no aparecer mas en ningun lado», porque leer las dos cifras
    /// le obligaba a restar para saber cuantos habia archivado. Lo que sigue vigilando es lo
    /// mismo de antes —que el denominador se dice y que cuadra con la base—; lo que cambia es
    /// que ahora tambien vigila que la palabra NO vuelva.
    /// </remarks>
    [TestMethod]
    public void ElResumenDiceSuDenominadorYCuadraConLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.AreEqual(BaseDeInicio.CasosDePrueba, resumen.Denominadores.CasosEnLaBase);
        Assert.AreEqual(servicios.Casos.Contar(FiltroDeCasos.Todo), resumen.Denominadores.CasosNoArchivados);
        Assert.AreEqual(servicios.Personas.Contar(FiltroDePersonas.Todo), resumen.Denominadores.PersonasEnLaBase);
        Assert.AreEqual(servicios.Companeros.Activos().Count, resumen.Denominadores.CompanerosActivos);
        StringAssert.Contains(resumen.LineaDelDenominador, "casos", StringComparison.Ordinal);
        Assert.IsFalse(
            resumen.LineaDelDenominador.Contains("archiv", StringComparison.OrdinalIgnoreCase),
            "El denominador de Inicio volvio a nombrar el archivo: " + resumen.LineaDelDenominador);
    }

    /// <summary>
    /// Requisito 9: avisar, nunca impedir. Una fecha con forma rara no tumba la pantalla y
    /// deja UNA linea de aviso; el documento no se pierde, va a la ventana de incompletos.
    /// </summary>
    /// <remarks>
    /// Una fecha que no tiene la forma AAAA-MM-DD es un campo dudoso, asi que el documento
    /// NO es «listo para asignar». Lo que el criterio exige es que no desaparezca, y no
    /// desaparece: sale en la ventana de lo que no esta completo, en el grupo de «sin fecha
    /// de viaje», diciendo cuantos datos le faltan.
    /// </remarks>
    [TestMethod]
    public void UnaFechaConFormaRaraNoTumbaLaPantallaYDejaUnaLineaDeAviso()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "RARA2609", "08/09/2026");

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.HasCount(1, resumen.Avisos);
        Assert.AreEqual(GravedadDeAviso.Advertencia, resumen.Avisos[0].Gravedad);
        Assert.IsLessThanOrEqualTo(120, resumen.Avisos[0].Linea.Length,
            "Requisito 4: ni un párrafo. La línea del aviso cabe en un renglón.");
        Assert.HasCount(42, resumen.Mes.Dias, "La pantalla se pinta igual.");

        var incompletos = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();
        Assert.AreEqual("RARA2609", incompletos.Grupos.Single().Documentos.Single().NumeroCaso,
            "El documento de fecha rara no puede perderse.");
    }

    /// <summary>Requisito 4: ningun aviso de esta pantalla pasa de un renglon.</summary>
    [TestMethod]
    public void NingunAvisoDeLaPantallaPasaDeUnRenglon()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba));

        foreach (var aviso in resumen.Avisos)
        {
            Assert.IsLessThanOrEqualTo(120, aviso.Linea.Length, $"Aviso demasiado largo: «{aviso.Linea}».");
            Assert.DoesNotContain("\n", aviso.Linea, "Un aviso de una línea no lleva salto de línea.");
        }
    }

    /// <summary>
    /// Con «--falso 0» la base sale vacia a proposito. La pantalla tiene que abrir en
    /// ceros: ensenar vacio y seguir es la regla, nunca detenerse (requisito 9).
    /// </summary>
    [TestMethod]
    public void ConLaBaseVaciaLaPantallaSaleEnCerosYNoRevienta()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(0));

        Assert.AreEqual(0, resumen.Denominadores.CasosEnLaBase);
        Assert.AreEqual(0, resumen.Contadores.ListoParaAsignar);
        Assert.AreEqual(0, resumen.Contadores.AsignadoALosAgentes);
        Assert.IsEmpty(resumen.Listos);
        Assert.IsEmpty(resumen.Asignados);
        Assert.HasCount(42, resumen.Mes.Dias, "El calendario se pinta igual con la base vacía.");
    }

    // ---------------------------------------------- la base grande

    /// <summary>
    /// Con 3 000 documentos las dos listas siguen cuadrando con lo que dicen los contratos.
    /// </summary>
    [TestMethod]
    public void ConTresMilCasosLasDosListasSiguenCuadrandoConLosContratos()
    {
        var servicios = BaseDeInicio.MontarServicios(3000);
        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.AreEqual(3000, resumen.Denominadores.CasosEnLaBase);
        Assert.AreEqual(servicios.Casos.Contar(FiltroDeCasos.Todo), resumen.Denominadores.CasosNoArchivados);
        Assert.AreEqual(servicios.Personas.Contar(FiltroDePersonas.Todo), resumen.Denominadores.PersonasEnLaBase);
        Assert.AreEqual(servicios.Companeros.Activos().Count, resumen.Denominadores.CompanerosActivos);

        // Lo asignado tiene que ser exactamente lo que lleva alguien y no esta archivado.
        var vivas = servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas);
        var casosConDueno = servicios.Asignaciones
            .Listar(FiltroDeAsignaciones.Activas, new Pagina(0, vivas)).Elementos
            .Select(a => a.CasoId).ToHashSet();
        var sinArchivar = servicios.Casos
            .Listar(FiltroDeCasos.Todo, new Pagina(0, int.MaxValue)).Elementos
            .Select(c => c.Id).ToHashSet();

        Assert.AreEqual(
            casosConDueno.Intersect(sinArchivar).Count(),
            resumen.Contadores.AsignadoALosAgentes,
            "«Asignado a los agentes» no coincide con lo que dice IAsignaciones cruzado con ICasos.");
    }

    /// <summary>
    /// C1-1 con la base grande: las cifras que salen en la captura de la entrega son
    /// exactamente estas. Se clavan aqui para que un cambio que las mueva se vea.
    /// </summary>
    /// <remarks>
    /// La base inventada es determinista: la misma semilla y el mismo reloj dan siempre los
    /// mismos 3 000 casos. Si estas cifras cambian, o cambio el generador —y entonces la
    /// captura de la entrega ya no describe lo que el programa hace— o cambio una regla.
    /// </remarks>
    [TestMethod]
    public void LasCifrasDeLaCapturaDeLaEntregaSonEstasYNoOtras()
    {
        var resumen = BaseDeInicio.LeerInicio(BaseDeInicio.MontarServicios(3000));

        Assert.AreEqual(3000, resumen.Denominadores.CasosEnLaBase);
        Assert.AreEqual(2766, resumen.Denominadores.CasosNoArchivados);
        Assert.AreEqual(7531, resumen.Denominadores.PersonasEnLaBase);
        Assert.AreEqual(6, resumen.Denominadores.CompanerosActivos);

        Assert.AreEqual(CifrasMedidas.ListoParaAsignar, resumen.Contadores.ListoParaAsignar);
        Assert.AreEqual(CifrasMedidas.PersonasListas, resumen.Contadores.PersonasListas);
        Assert.AreEqual(CifrasMedidas.AsignadoALosAgentes, resumen.Contadores.AsignadoALosAgentes);
        Assert.AreEqual(CifrasMedidas.SinDevolver, resumen.Contadores.SinDevolver);

        Assert.HasCount(CifrasMedidas.ListoParaAsignar, resumen.Listos);
        Assert.HasCount(CifrasMedidas.AsignadoALosAgentes, resumen.Asignados);
    }

    /// <summary>
    /// Las cifras de la base inventada de 3 000, medidas y no estimadas.
    /// </summary>
    /// <remarks>
    /// Estan en una clase aparte para que la entrega pueda citarlas por su nombre y para
    /// que se vea de un vistazo que son datos y no reglas.
    /// </remarks>
    internal static class CifrasMedidas
    {
        /// <summary>
        /// Documentos sin archivar, sin dueno, sin ningun hueco y sin «no completa».
        /// </summary>
        /// <remarks>
        /// ⚠️ <b>Bajo de 621 a 447 el 2026-09-06, y no es una regresion: es el arreglo.</b>
        /// Hasta ese dia «listo para asignar» se contestaba aqui mirando solo si los campos
        /// estaban vacios y si cumplian su forma; desde ese dia mira lo mismo que la pantalla
        /// de Correccion, que es ademas la confianza, el tachon, la marca de «no está en el
        /// papel» y si consta de donde salio el valor. <b>174 documentos</b> que se ofrecian
        /// para mandar a un compañero llevaban dentro un dato que nadie habia mirado.
        /// </remarks>
        public const int ListoParaAsignar = 447;

        /// <summary>Personas que suman esos documentos.</summary>
        /// <remarks>Baja de 1 483 a 1 045 por lo mismo, y en el mismo cambio.</remarks>
        public const int PersonasListas = 1045;

        /// <summary>Documentos sin archivar que lleva ahora mismo un companero.</summary>
        public const int AsignadoALosAgentes = 1373;

        /// <summary>De esos, cuantos no han vuelto con su hoja.</summary>
        public const int SinDevolver = 830;
    }
}
