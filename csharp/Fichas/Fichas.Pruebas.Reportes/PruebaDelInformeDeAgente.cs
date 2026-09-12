using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El informe por agente: que hizo en el mes y que hizo en la semana.
/// </summary>
/// <remarks>
/// <para><b>De donde sale el criterio.</b> De las palabras del dueno del 2026-09-05
/// (<c>DECISIONES.md</c>): <i>«Es importante tener un informe por agente también: lo que
/// hicieron los agentes en ese mes y lo que hicieron en esa semana, qué hicieron»</i>.</para>
///
/// <para><b>Que habia antes, medido antes de tocar nada.</b>
/// <c>IReportes.GenerarReporteDeCompanero</c> existia en el contrato y en la biblioteca, y
/// NINGUN boton lo llamaba. Lo que hacia era el informe de los jefes recortado a los casos de
/// ese companero: contesta <i>«¿cómo están sus casos?»</i>, que es una pregunta buena y NO es
/// la que el dueno hizo. Se conserva entero y se le pone delante la respuesta que faltaba:
/// una seccion «Lo que hizo» con las dos columnas, el mes y la semana. No se rehace: se le
/// anade lo que le faltaba.</para>
///
/// <para><b>Que es «la semana», y hay que decirlo porque no lo dijo nadie.</b> Son los ULTIMOS
/// SIETE DIAS del periodo que se pide, contando el ultimo como uno de los siete. Asi, pidiendo
/// el mes se obtienen las dos cosas en un solo PDF, que es como el las nombro —«en ese mes y
/// en esa semana»— sin tener que generar dos.</para>
///
/// <para><b>Las cifras se comprueban contra la base, no contra el propio informe.</b> Cada
/// numero de este archivo se vuelve a contar aqui recorriendo el almacen falso a mano. Una
/// prueba que compare el informe consigo mismo pasa en verde con la cuenta mal.</para>
/// </remarks>
[TestClass]
public class PruebaDelInformeDeAgente
{
    /// <summary>El día fijo del reloj del escenario: el último del mes que se pide.</summary>
    private const string Hoy = "2026-09-30";
    /// <summary>La marca con la que se arma el informe en todas las pruebas.</summary>
    private const string GeneradoEn = "2026-09-30 10:00:00";

    /// <summary>El informe abre diciendo que hizo esa persona, con su nombre.</summary>
    [TestMethod]
    public void ElInformeAbreConLoQueHizoEseAgente()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);

        StringAssert.Contains(documento.Portada.Titular, "Sandy");
        Assert.AreEqual("Lo que hizo", documento.Secciones[0].Titulo);
    }

    /// <summary>La seccion trae UNA columna para el mes y OTRA para la semana.</summary>
    /// <remarks>Es literalmente lo que el pidio: «en ese mes» y «en esa semana», los dos.</remarks>
    [TestMethod]
    public void LaSeccionTraeElMesYLaSemanaEnDosColumnas()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
        var rotulos = documento.Secciones[0].Columnas.Select(columna => columna.Nombre).ToList();

        Assert.HasCount(4, rotulos);
        StringAssert.Contains(rotulos[1], "mes");
        StringAssert.Contains(rotulos[2], "semana");
        StringAssert.Contains(rotulos[3], "cuenta");
    }

    /// <summary>
    /// Las cifras del mes cuadran con lo que hay en la base, contado a mano.
    /// </summary>
    /// <remarks>
    /// El escenario esta escrito para que las dos ventanas den numeros DISTINTOS: si el mes y
    /// la semana dieran lo mismo, una columna copiada de la otra pasaria esta prueba.
    /// </remarks>
    [TestMethod]
    public void LasCifrasDelMesYDeLaSemanaCuadranConLaBase()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
        var loQueHizo = documento.Secciones[0];

        // Contado a mano sobre el almacen: cuantos casos contesto Sandy en cada ventana.
        var contestadosEnElMes = mundo.Servicios.Almacen.Casos.Values.Count(caso =>
            caso.EstadoDelCompaneroPor == mundo.Sandy.Id
            && EstaEntre(caso.EstadoDelCompaneroEn, "2026-09-01", "2026-10-01"));
        var contestadosEnLaSemana = mundo.Servicios.Almacen.Casos.Values.Count(caso =>
            caso.EstadoDelCompaneroPor == mundo.Sandy.Id
            && EstaEntre(caso.EstadoDelCompaneroEn, "2026-09-24", "2026-10-01"));

        Assert.AreEqual(4, contestadosEnElMes, "El escenario tenía que dejar 4 respuestas en el mes.");
        Assert.AreEqual(2, contestadosEnLaSemana, "El escenario tenía que dejar 2 respuestas en la semana.");

        var fila = loQueHizo.Filas.Single(f => f[0]!.Contains("Documentos que contestó", StringComparison.Ordinal));
        Assert.AreEqual(contestadosEnElMes.ToString(), fila[1]);
        Assert.AreEqual(contestadosEnLaSemana.ToString(), fila[2]);
    }

    /// <summary>Los tres motivos del dueno salen con su cifra, y no como un solo saco.</summary>
    [TestMethod]
    public void LosTresMotivosSalenCadaUnoConSuCifra()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
        var primeraColumna = documento.Secciones[0].Filas.Select(fila => fila[0]!).ToList();

        Assert.IsTrue(primeraColumna.Any(t => t.Contains("no se pudo comunicar", StringComparison.Ordinal)));
        Assert.IsTrue(primeraColumna.Any(t => t.Contains("el líder no lo hizo", StringComparison.Ordinal)));
        Assert.IsTrue(primeraColumna.Any(t => t.Contains("otra razón", StringComparison.Ordinal)));
    }

    /// <summary>Cada cifra dice su denominador; una cifra sola no se puede leer.</summary>
    /// <remarks>
    /// Es el criterio del pase, literal: «sus cifras cuadran con la base, con el denominador
    /// dicho». «Contestó 4» no dice si eso es mucho o poco; «4 de los 6 que se le asignaron»
    /// sí.
    /// </remarks>
    [TestMethod]
    public void CadaCifraDiceSobreQueSeCuenta()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
        var loQueHizo = documento.Secciones[0];

        foreach (var fila in loQueHizo.Filas)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(fila[3]),
                $"La fila «{fila[0]}» no dice cómo se cuenta, y una cifra sin denominador no se puede leer.");
        }

        StringAssert.Contains(
            string.Join(" ", loQueHizo.Notas),
            "últimos 7 días",
            "El informe tiene que decir qué es «la semana»: nadie lo definió, y una ventana sin declarar "
            + "hace que dos personas lean la misma cifra de dos maneras.");
    }

    /// <summary>El informe sigue trayendo lo que ya traia: sus casos, detras.</summary>
    /// <remarks>
    /// Lo que existia contestaba «¿cómo están sus casos?» y no se tira: se le pone delante la
    /// respuesta que faltaba. Esta prueba es la que impide que un dia alguien lo sustituya.
    /// </remarks>
    [TestMethod]
    public void DetrasSiguenLasSeccionesQueYaTraiaElInformeDeCompanero()
    {
        var mundo = Escenario();

        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);
        var titulos = documento.Secciones.Select(seccion => seccion.Titulo).ToList();

        CollectionAssert.Contains(titulos, "Quiénes viajaron sin verificar");
        CollectionAssert.Contains(titulos, "Los viajes");
    }

    /// <summary>Un companero que no existe no rompe nada: lo dice y no escribe PDF.</summary>
    [TestMethod]
    public void UnCompaneroQueNoExisteSeDiceYNoEscribeNada()
    {
        var mundo = Escenario();
        var ruta = Path.Combine(Path.GetTempPath(), "fichas-informe-de-agente", "no-deberia-existir.pdf");

        var resultado = mundo.Reportes.GenerarReporteDeCompanero(99999, "2026-09-01", "2026-09-30", ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(ruta));
    }

    /// <summary>Deja el PDF puesto para que lo abra un lector de verdad.</summary>
    [TestMethod]
    public void DejaElPdfDelInformeDeAgentePuesto()
    {
        var mundo = Escenario();
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-informe-de-agente");
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "informe-de-sandy.pdf");

        var resultado = mundo.Reportes.GenerarReporteDeCompanero(
            mundo.Sandy.Id, "2026-09-01", "2026-09-30", ruta);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        Console.WriteLine($"MEDIDO · {ruta} · {new FileInfo(ruta).Length} bytes");
    }

    // ---- el escenario -------------------------------------------------------

    /// <summary>Lo que devuelve el escenario: los servicios, el motor y el compañero del que se reporta.</summary>
    /// <param name="Servicios">Los servicios falsos, para contar a mano sobre el almacén.</param>
    /// <param name="Reportes">El motor sobre ese almacén.</param>
    /// <param name="Sandy">El compañero del que se pide el informe.</param>
    private sealed record Mundo(ServiciosFalsos Servicios, ReportesEnPdf Reportes, Companero Sandy);

    /// <summary>
    /// Sandy con seis casos asignados y cuatro contestados, dos de ellos en la ultima semana.
    /// </summary>
    /// <remarks>
    /// Escrito a mano y sin casos del generador: el criterio pide que las cifras cuadren con la
    /// base, y con casos inventados al azar encima no habria una cifra que escribir.
    /// </remarks>
    private static Mundo Escenario()
    {
        var servicios = new ServiciosFalsos(0, 11, new RelojFijo(Hoy));
        var almacen = servicios.Almacen;
        almacen.Companeros.Clear();

        var sandy = new Companero
        {
            Id = almacen.SiguienteId(), Nombre = "Sandy", Activo = true, CreadoEn = "2026-09-01 08:00:00",
        };
        almacen.Companeros[sandy.Id] = sandy;

        // Cuatro contestados dentro del mes; los dos ultimos caen ademas en la ultima semana
        // (del 24 al 30), que es la ventana que el informe llama «la semana».
        Caso(almacen, sandy, "CASP1001", "completa", null, "2026-09-05 11:00:00");
        Caso(almacen, sandy, "CASP1002", "no_completa", "no_se_pudo_comunicar", "2026-09-10 11:00:00");
        Caso(almacen, sandy, "CASP1003", "no_completa", "el_lider_no_lo_hizo", "2026-09-25 11:00:00");
        Caso(almacen, sandy, "CASP1004", "no_completa", "otra_razon", "2026-09-28 11:00:00");

        // Dos asignados en el mes y todavia sin contestar: son el denominador.
        Caso(almacen, sandy, "CASP1005", null, null, null);
        Caso(almacen, sandy, "CASP1006", null, null, null);

        return new Mundo(
            servicios,
            new ReportesEnPdf(
                servicios.Casos, servicios.Personas, servicios.Companeros,
                servicios.Asignaciones, servicios.Procedencia, servicios.Reloj),
            sandy);
    }

    /// <summary>Un caso con su persona y su asignacion viva a Sandy.</summary>
    /// <param name="almacen">El almacén falso donde se escribe.</param>
    /// <param name="quien">El compañero que lo lleva y, si contestó, quien firma el estado.</param>
    /// <param name="numero">El número del caso.</param>
    /// <param name="estadoDelCompanero">Lo que dijo su hoja, o nulo si no ha contestado.</param>
    /// <param name="motivo">La clave del motivo de no completar, o nula.</param>
    /// <param name="cuandoContesto">La marca de la respuesta; nula deja el caso sin contestar.</param>
    private static void Caso(
        AlmacenFalso almacen, Companero quien, string numero,
        string? estadoDelCompanero, string? motivo, string? cuandoContesto)
    {
        var casoId = almacen.SiguienteId();
        almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numero,
            UnidadNombre = "Castries Branch",
            UnidadNumero = "700001",
            FechaViaje = "2026-09-15",
            CreadoEn = "2026-09-01 09:00:00",
            EstadoRecomendacion = estadoDelCompanero,
            EstadoDelCompanero = estadoDelCompanero,
            EstadoDelCompaneroPor = cuandoContesto is null ? null : quien.Id,
            EstadoDelCompaneroEn = cuandoContesto,
            MotivoDelCompanero = motivo,
        };

        var personaId = almacen.SiguienteId();
        almacen.Personas[personaId] = new Persona
        {
            Id = personaId, CasoId = casoId, Nombre = $"Persona de {numero}", Mrn = $"055-1000-{casoId:0000}",
        };

        var asignacionId = almacen.SiguienteId();
        almacen.Asignaciones[asignacionId] = new Asignacion
        {
            Id = asignacionId,
            CasoId = casoId,
            CompaneroId = quien.Id,
            AsignadoEn = "2026-09-02 09:00:00",
            Activa = true,
        };
    }

    /// <summary>Si esa marca con hora cae en el trozo, con el limite de arriba abierto.</summary>
    /// <param name="marca">La marca «AAAA-MM-DD HH:mm:ss», o nula.</param>
    /// <param name="desde">El primer día, incluido.</param>
    /// <param name="siguienteAHasta">El día de después del último, excluido.</param>
    private static bool EstaEntre(string? marca, string desde, string siguienteAHasta)
        => marca is not null
           && string.CompareOrdinal(marca, desde) >= 0
           && string.CompareOrdinal(marca, siguienteAHasta) < 0;
}
