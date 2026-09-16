using Fichas.Contratos.Modelos;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El resumen del periodo que va a la hoja unica del Excel: sus cifras son las del PDF.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-16 (<c>DECISIONES.md</c>, «EL REPORTE EN EXCEL ES UNA SOLA HOJA»):
/// <i>«Lo importante son las unidades: quiénes viajaron de esa unidad con todo completo y
/// quiénes no; a qué agente se le asignó y si lo completó; gráfico de la cantidad de personas
/// que viajaron en unos meses sin problemas o dificultades; números fríos.»</i></para>
///
/// <para>⚠️ <b>Lo que mas vale aqui es que el Excel y el PDF del mismo periodo digan LO
/// MISMO.</b> El resumen no vuelve a contar por su cuenta: parte de <c>Preparacion.Recontar</c>,
/// que es de donde salen las cifras de la portada del PDF. Estas pruebas lo miden cruzando las
/// dos salidas sobre la misma base inventada.</para>
/// </remarks>
[TestClass]
public class PruebaDelResumenDelPeriodo
{
    /// <summary>La marca fija con la que se arma; el «hoy» que sale de ella es el 20 de septiembre.</summary>
    private const string GeneradoEn = "2026-09-20 10:00:00";

    /// <summary>Tres meses, para que el grafico tenga tres barras dobles.</summary>
    private const string Desde = "2026-07-01";

    /// <summary>El ultimo dia del periodo de tres meses.</summary>
    private const string Hasta = "2026-09-30";

    /// <summary>El doble de casos de los que usa <c>--falso</c> por defecto, como pide el pase.</summary>
    private const int CuantosCasos = 3000;

    // ─────────────────────── las cifras son las del PDF ───────────────────────

    /// <summary>
    /// Dado el mismo periodo sobre 3 000 casos, las tres cifras de arriba son las de la portada del PDF.
    /// </summary>
    [TestMethod]
    public void LasTresCifrasDeArribaSonLasDeLaPortadaDelPdf()
    {
        var (resumen, documento) = ArmarLosDos();

        var sinVerificar = documento.Portada.Cifras[0].Numero;
        var verificadas = documento.Portada.Cifras[1].Numero;

        Assert.AreEqual(verificadas + sinVerificar, resumen.Viajaron, "viajaron = verificadas + sin verificar del PDF");
        Assert.AreEqual(verificadas, resumen.Completos);
        Assert.AreEqual(sinVerificar, resumen.SinCompletar);
        Assert.IsGreaterThan(0, resumen.SinCompletar, "con 3 000 casos tiene que haber alguien sin completar o esto no prueba nada");
    }

    /// <summary>La tabla por unidad suma exactamente las cifras de arriba: se comprueba SUMANDO.</summary>
    [TestMethod]
    public void LaTablaPorUnidadSumaLasCifrasDeArriba()
    {
        var (resumen, _) = ArmarLosDos();

        Assert.AreEqual(resumen.Viajaron, resumen.PorUnidad.Sum(u => u.Viajaron));
        Assert.AreEqual(resumen.Completos, resumen.PorUnidad.Sum(u => u.Completos));
        Assert.AreEqual(resumen.SinCompletar, resumen.PorUnidad.Sum(u => u.SinCompletar));
        foreach (var unidad in resumen.PorUnidad)
            Assert.AreEqual(unidad.Viajaron, unidad.Completos + unidad.SinCompletar, $"en {unidad.Nombre}");
    }

    /// <summary>Las barras por mes suman las cifras de arriba, y hay una por mes del periodo.</summary>
    [TestMethod]
    public void LasBarrasPorMesSumanLasCifrasDeArribaYHayUnaPorMesDelPeriodo()
    {
        var (resumen, _) = ArmarLosDos();

        Assert.HasCount(3, resumen.PorMes, "julio, agosto y septiembre");
        CollectionAssert.AreEqual(new[] { "jul", "ago", "sep" }, resumen.PorMes.Select(m => m.Rotulo).ToArray());
        Assert.AreEqual(resumen.Completos, resumen.PorMes.Sum(m => m.Completos));
        Assert.AreEqual(resumen.SinCompletar, resumen.PorMes.Sum(m => m.ConDificultades));
        Assert.AreEqual(3, resumen.Meses);
    }

    /// <summary>La lista de pendientes son exactamente los que viajaron sin completar: ni uno mas.</summary>
    [TestMethod]
    public void LosPendientesSonLosQueViajaronSinCompletar()
    {
        var (resumen, documento) = ArmarLosDos();

        var quienes = documento.Secciones.Single(s => s.Titulo == Vocabulario.QuienesViajaronSinVerificar);
        Assert.HasCount(quienes.Filas.Count, resumen.Pendientes, "la misma lista que abre el PDF");
        Assert.HasCount(resumen.SinCompletar, resumen.Pendientes);
    }

    /// <summary>Las cuentas de unidades de la tarjeta salen de la tabla, no de otra cuenta.</summary>
    [TestMethod]
    public void LasUnidadesConPendientesSeCuentanSobreLaTabla()
    {
        var (resumen, _) = ArmarLosDos();

        Assert.AreEqual(resumen.PorUnidad.Count, resumen.Unidades);
        Assert.AreEqual(resumen.PorUnidad.Count(u => u.SinCompletar > 0), resumen.UnidadesConPendientes);
        Assert.AreEqual(
            resumen.PorUnidad.Count(u => u.SinCompletar > 0 && u.Agente == Vocabulario.SinAgente),
            resumen.UnidadesSinAgente);
    }

    /// <summary>La cabecera dice el periodo con las palabras de siempre y el «generado el» que entro.</summary>
    [TestMethod]
    public void LaCabeceraLlevaElPeriodoYElGeneradoEn()
    {
        var (resumen, _) = ArmarLosDos();

        Assert.AreEqual(Vocabulario.Titulo, resumen.Titulo);
        Assert.AreEqual(Periodo.Leer(Desde, Hasta).Periodo!.EnTexto(), resumen.PeriodoEnTexto);
        Assert.AreEqual(GeneradoEn, resumen.GeneradoEn);
    }

    /// <summary>Con tres templos en el periodo, la cabecera los nombra a los tres: no elige uno.</summary>
    [TestMethod]
    public void ConVariosTemplosLaCabeceraLosNombraTodos()
    {
        var (resumen, _) = ArmarLosDos();

        StringAssert.StartsWith(resumen.Templo, "3 templos: ");
        StringAssert.Contains(resumen.Templo, "Santo Domingo Dominican Republic");
        StringAssert.Contains(resumen.Templo, "Caracas Venezuela");
    }

    // ─────────────────────── «devolvió», que no existe como dato ───────────────────────

    /// <summary>
    /// Dada una unidad cuyo unico caso con agente volvio contestado, «devolvió» es sí.
    /// </summary>
    /// <remarks>
    /// Del dueno, 2026-09-16: «devolvió el paquete» se deduce de si el agente contesto, porque
    /// no existe como dato. Contestar es que el caso lleve escrito el estado del companero.
    /// </remarks>
    [TestMethod]
    public void UnaUnidadCuyosCasosVolvieronContestadosDevolvio()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), ElPeriodoFijo(), GeneradoEn);

        var barahona = resumen.PorUnidad.Single(u => u.Nombre == "Barahona");
        Assert.AreEqual("Sandy", barahona.Agente);
        Assert.IsTrue(barahona.Devolvio);
        Assert.AreEqual(2, barahona.Viajaron);
        Assert.AreEqual(1, barahona.Completos);
        Assert.AreEqual(1, barahona.SinCompletar);
    }

    /// <summary>Dada una unidad con agente cuyo caso NO volvio, «devolvió» es no.</summary>
    [TestMethod]
    public void UnaUnidadConAgenteCuyoCasoNoVolvioNoDevolvio()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), ElPeriodoFijo(), GeneradoEn);

        var laVega = resumen.PorUnidad.Single(u => u.Nombre == "La Vega");
        Assert.AreEqual("Pedro", laVega.Agente);
        Assert.IsFalse(laVega.Devolvio);
    }

    /// <summary>Dada una unidad sin nadie asignado, «devolvió» no es ni sí ni no.</summary>
    [TestMethod]
    public void UnaUnidadSinAgenteNoTieneDevolvio()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), ElPeriodoFijo(), GeneradoEn);

        var puertoPlata = resumen.PorUnidad.Single(u => u.Nombre == "Puerto Plata");
        Assert.AreEqual(Vocabulario.SinAgente, puertoPlata.Agente);
        Assert.IsNull(puertoPlata.Devolvio);
    }

    /// <summary>Por agente: cuantos casos a su cargo volvieron de cuantos; «sin asignar» va el ultimo.</summary>
    [TestMethod]
    public void PorAgenteSeCuentanLosCasosDevueltosDeLosSuyosYSinAsignarVaElUltimo()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), ElPeriodoFijo(), GeneradoEn);

        var sandy = resumen.PorAgente.Single(a => a.Nombre == "Sandy");
        Assert.AreEqual(2, sandy.Asignados, "las dos personas de Barahona");
        Assert.AreEqual(1, sandy.Completos);
        Assert.AreEqual(1, sandy.SinCompletar);
        Assert.AreEqual(1, sandy.CasosDevueltos);
        Assert.AreEqual(1, sandy.CasosACargo);

        var pedro = resumen.PorAgente.Single(a => a.Nombre == "Pedro");
        Assert.AreEqual(0, pedro.CasosDevueltos);
        Assert.AreEqual(1, pedro.CasosACargo);

        Assert.AreEqual(Vocabulario.SinAgente, resumen.PorAgente[^1].Nombre);
        Assert.AreEqual(0, resumen.PorAgente[^1].CasosACargo, "sin agente no hay nada que devolver");

        Assert.AreEqual(1, resumen.CasosDevueltos);
        Assert.AreEqual(2, resumen.CasosConAgente);
        Assert.AreEqual(2, resumen.Agentes, "Sandy y Pedro; «sin asignar» no es un agente");
        Assert.AreEqual(0, resumen.AgentesSinPendientes);
    }

    /// <summary>Cada pendiente trae unidad, persona, que le falta (solo los pasos), agente, cuando viajo, caso y templo.</summary>
    [TestMethod]
    public void CadaPendienteDiceQuienEsQueLeFaltaYQuienLoLleva()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), ElPeriodoFijo(), GeneradoEn);

        var luis = resumen.Pendientes.Single(p => p.Persona == "Luis Anonimo");
        Assert.AreEqual("7000011", luis.NumeroDeUnidad);
        Assert.AreEqual("Barahona", luis.Unidad);
        Assert.AreEqual("Información", luis.QueLeFalta, "solo el paso que falta: la columna ya se llama «Qué le falta»");
        Assert.AreEqual("Sandy", luis.Agente);
        Assert.AreEqual("2026-09-05", luis.ViajoEl);
        Assert.AreEqual("CASP2609", luis.Caso);
        Assert.AreEqual("Santo Domingo Dominican Republic", luis.Templo);

        var rosa = resumen.Pendientes.Single(p => p.Persona == "Rosa Peralta");
        Assert.AreEqual(Preparacion.NadieLaMiro, rosa.QueLeFalta);
        Assert.AreEqual("—", rosa.Templo, "sin templo no se escribe «no consta»: el dueño dijo que no es una respuesta");
    }

    /// <summary>Con un solo templo la cabecera lo dice a secas; sin ninguno, lo dice tambien.</summary>
    [TestMethod]
    public void ConUnSoloTemploLaCabeceraLoDiceASecas()
    {
        var resumen = ArmadoDelResumen.DelPeriodo(JuegoFijo(), Periodo.Leer("2026-09-01", "2026-09-10").Periodo!, GeneradoEn);

        Assert.AreEqual("Santo Domingo Dominican Republic", resumen.Templo);
    }

    // ─────────────────────── el andamio ───────────────────────

    /// <summary>El resumen y el documento del PDF, armados sobre la MISMA lectura de 3 000 casos.</summary>
    private static (ResumenDelPeriodo Resumen, Documento Documento) ArmarLosDos()
    {
        var servicios = BaseDePrueba.Montar(CuantosCasos);
        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia);
        var periodo = Periodo.Leer(Desde, Hasta).Periodo!;

        return (
            ArmadoDelResumen.DelPeriodo(lectura, periodo, GeneradoEn),
            ArmadoDelDocumento.DelPeriodo(lectura, periodo, GeneradoEn));
    }

    /// <summary>Septiembre entero, que es donde caen los tres casos del juego fijo.</summary>
    private static Periodo ElPeriodoFijo() => Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

    /// <summary>
    /// Tres unidades a mano, una por cada respuesta de «devolvió»: sí, no y sin agente.
    /// </summary>
    /// <remarks>Todo viaja antes del 20 de septiembre para que cuente como viajado.</remarks>
    private static LecturaParaReportes JuegoFijo()
    {
        var casos = new List<Caso>
        {
            new()
            {
                Id = 1, NumeroCaso = "CASP2609", UnidadNombre = "Barahona", UnidadNumero = "7000011",
                TemploNombre = "Santo Domingo Dominican Republic", FechaViaje = "2026-09-05",
                EstadoDelCompanero = "no_completa", EstadoDelCompaneroPor = 1,
                EstadoDelCompaneroEn = "2026-09-04 10:00:00", CreadoEn = "2026-08-01 09:00:00",
            },
            new()
            {
                // Con agente pero sin contestar: la hoja no volvio.
                Id = 2, NumeroCaso = "LVEG2609", UnidadNombre = "La Vega", UnidadNumero = "7000012",
                TemploNombre = null, FechaViaje = "2026-09-12",
                EstadoDelCompanero = null, CreadoEn = "2026-08-15 11:30:00",
            },
            new()
            {
                // Sin nadie asignado.
                Id = 3, NumeroCaso = "PPLT2609", UnidadNombre = "Puerto Plata", UnidadNumero = "7000013",
                TemploNombre = "Caracas Venezuela", FechaViaje = "2026-09-15",
                EstadoDelCompanero = null, CreadoEn = "2026-07-20 08:00:00",
            },
        };

        var personas = new Dictionary<long, IReadOnlyList<Persona>>
        {
            [1] =
            [
                Con(1, 1, "Ana Anonimo", 1, todosLosPasos: true),
                Con(2, 1, "Luis Anonimo", 2, todosLosPasos: true) with { PasoInformacion = false },
            ],
            [2] = [Con(3, 2, "Rosa Peralta", 1, todosLosPasos: null)],
            [3] = [Con(4, 3, "Carmen Nuñez", 1, todosLosPasos: true)],
        };

        var verificacion = casos.Select(c => new CasoConSuVerificacion(c, 1, 0, 0, null)).ToList();

        return LecturaParaReportes.DeMemoria(
            casos,
            personas,
            new Dictionary<long, IReadOnlyList<string>> { [1] = ["Sandy"], [2] = ["Pedro"] },
            verificacion);
    }

    /// <summary>Una persona del juego fijo con los seis pasos al mismo valor.</summary>
    /// <param name="id">El id de la persona.</param>
    /// <param name="casoId">El caso al que pertenece.</param>
    /// <param name="nombre">El nombre.</param>
    /// <param name="fila">Su fila en el formulario, que fija el orden.</param>
    /// <param name="todosLosPasos">Sí, no o nulo para los seis pasos.</param>
    private static Persona Con(long id, long casoId, string nombre, int fila, bool? todosLosPasos)
        => new()
        {
            Id = id, CasoId = casoId, Nombre = nombre, FilaFormulario = fila,
            PasoPreparacion = todosLosPasos, PasoInformacion = todosLosPasos,
            PasoCitaDelTemplo = todosLosPasos, PasoAccionesRequeridas = todosLosPasos,
            PasoEntrevistas = todosLosPasos, PasoListoParaElTemplo = todosLosPasos,
        };
}
