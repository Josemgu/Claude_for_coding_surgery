using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Inicio;
using Fichas.App.Paquetes;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Pruebas.App.Paquetes;
using Fichas.Reportes;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Un documento que se archiva deja de estar asignado a nadie, en todos los sitios donde se
/// dice cuánto lleva un agente; y lo que hizo el agente con él no se pierde.
/// </summary>
/// <remarks>
/// <para><b>Lo pidió el dueño el 2026-09-11, con estas palabras:</b> <i>«Quiero que cuando los
/// documentos se archiven, ya no aparezcan asignados al agente. Porque llegará un punto en que,
/// si no se hace así, un agente puede tener 1 000 casos pero en la realidad solo tiene 10.»</i></para>
///
/// <para>Es el mismo gesto que el del 2026-09-08 para lo que el compañero devuelve completo
/// (<c>PruebasDeQueElPaqueteCompletoLimpiaLaAsignacion</c>): la asignación se desactiva con su
/// fecha y <b>nunca se borra</b>, y por eso el informe del agente sigue trayendo lo que hizo,
/// porque lo lee con todas, vivas y retiradas.</para>
///
/// <para>Todo por el camino de verdad: se archiva con <see cref="AccionesDeRevisar"/>, que es lo
/// que pulsa el botón de Revisar, y lo que queda se consulta por los MISMOS lectores que pintan
/// las pantallas —<see cref="LectorDelInicio"/> para Inicio y Flujo, <see cref="CargaDeUnCompanero"/>
/// para el desplegable de Paquetes, <see cref="OperacionDeAsignar.MirarLoQueSeLeQuitaria"/> para
/// el botón de quitarle todo, y <see cref="ReportesEnPdf"/> para el informe—. Mirar la base a
/// mano comprobaría una suposición de la prueba, no lo que ve el dueño.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueArchivarQuitaLaAsignacion
{
    /// <summary>Cuántos documentos lleva el agente al empezar cada prueba.</summary>
    private const int N = 5;

    /// <summary>Archivar dos de sus N le deja N−2 vivas, y las dos retiradas siguen en la base con su fecha.</summary>
    [TestMethod]
    public void ArchivarDosLeQuitaDosEnLaBaseYLasDosSiguenConSuFecha()
    {
        using var banco = new BancoDelArchivado(N);

        banco.Archivar(banco.Casos[0], banco.Casos[1]);

        var vivas = banco.Base.CasosVivosDe(banco.Sandy.Id);
        Assert.HasCount(N - 2, vivas, "le tienen que quedar vivas N−2");
        CollectionAssert.DoesNotContain(vivas.ToList(), banco.Casos[0]);
        CollectionAssert.DoesNotContain(vivas.ToList(), banco.Casos[1]);

        var todas = banco.Base.TodasLasDe(banco.Sandy.Id);
        Assert.HasCount(N, todas, "ninguna fila se borra: se desactivan");
        foreach (var archivado in new[] { banco.Casos[0], banco.Casos[1] })
        {
            var fila = todas.Single(a => a.CasoId == archivado);
            Assert.IsFalse(fila.Activa, $"la asignación del caso {archivado} tiene que quedar con activa = 0");
            Assert.AreEqual(BaseDelPaquete.ElInstante, fila.DesactivadaEn, "y con la fecha del día en que se archivó");
        }
    }

    /// <summary>El cuadro de Inicio y el equipo de Flujo dicen N−2 del agente, en las cuatro cifras.</summary>
    [TestMethod]
    public void ElCuadroDeInicioYElEquipoDeFlujoDicenNMenosDos()
    {
        using var banco = new BancoDelArchivado(N);
        var antes = banco.LeerInicio();
        Assert.AreEqual(N, antes.Equipo.Single(r => r.Id == banco.Sandy.Id).Casos, "el montaje: lleva N");

        banco.Archivar(banco.Casos[0], banco.Casos[1]);
        var despues = banco.LeerInicio();
        var renglon = despues.Equipo.Single(r => r.Id == banco.Sandy.Id);

        Console.WriteLine($"Equipo: {renglon.Casos} casos · {renglon.SinDevolver} sin devolver. "
            + $"Cuadro: {despues.Cuadro.CasosDeLosAgentes} de los agentes · {despues.Cuadro.SinDevolver} sin devolver.");
        Assert.AreEqual(N - 2, renglon.Casos, "el renglón del agente en Flujo");
        Assert.AreEqual(N - 2, renglon.SinDevolver, "y su «sin devolver»");
        Assert.AreEqual(N - 2, despues.Cuadro.CasosDeLosAgentes, "la cifra grande del cuadro de Inicio");
        Assert.AreEqual(N - 2, despues.Cuadro.SinDevolver, "y su «sin devolver»");
    }

    /// <summary>El desplegable de Paquetes y la pregunta de «quitarle todo» dicen N−2.</summary>
    [TestMethod]
    public void ElPaqueteSiguienteYLaPreguntaDeQuitarleTodoDicenNMenosDos()
    {
        using var banco = new BancoDelArchivado(N);

        banco.Archivar(banco.Casos[0], banco.Casos[1]);

        var carga = CargaDeUnCompanero.Leer(banco.Base.Asignaciones, banco.Base.Casos, banco.Sandy.Id);
        Assert.HasCount(N - 2, carga.CasoIds, "los que van en el paquete siguiente");
        Assert.AreEqual(N - 2, carga.CasosQueLlevaEnTotal, "y lo que lleva en total");
        Assert.AreEqual(
            N - 2,
            banco.Base.Reparto.MirarLoQueSeLeQuitaria(banco.Sandy.Id, banco.Sandy.Nombre).CasosQueLleva,
            "la pregunta de «quitarle todo»");
    }

    /// <summary>Desarchivar uno no le devuelve la asignación: queda sin asignar y el dueño decide.</summary>
    [TestMethod]
    public void DesarchivarNoLeDevuelveLaAsignacionSola()
    {
        using var banco = new BancoDelArchivado(N);
        banco.Archivar(banco.Casos[0], banco.Casos[1]);

        banco.Acciones.DesarchivarEnLote([banco.Casos[0]]);

        Assert.IsFalse(banco.Base.Casos.Obtener(banco.Casos[0])!.Archivado, "el montaje: ya no está archivado");
        Assert.HasCount(N - 2, banco.Base.CasosVivosDe(banco.Sandy.Id), "sigue sin ser suyo");
        Assert.IsEmpty(banco.Base.Asignaciones.VivasDeCaso(banco.Casos[0]), "y no lo lleva nadie");
        var sinAsignar = banco.LeerInicio().Equipo.Single(r => r.Id == 0);
        Assert.AreEqual(1, sinAsignar.Casos, "Inicio lo cuenta entre los que no lleva nadie");
    }

    /// <summary>⚠️ El informe del agente SIGUE trayendo los dos archivados en lo que hizo.</summary>
    /// <remarks>
    /// Es la mitad que no se pierde: la asignación se desactiva con su fecha y no se borra, y
    /// <c>ReportesEnPdf.DocumentoDeCompanero</c> las pide todas, vivas y retiradas.
    /// </remarks>
    [TestMethod]
    public void ElInformeDelAgenteSigueTrayendoLosDosArchivados()
    {
        using var banco = new BancoDelArchivado(N);
        banco.Base.IdaYVuelta(banco.Sandy, "Sí", banco.Casos[0]);
        banco.Base.IdaYVuelta(banco.Sandy, "No", banco.Casos[1]);
        Assert.HasCount(N - 1, banco.Base.CasosVivosDe(banco.Sandy.Id), "el montaje: el completo ya se le quitó al volver");

        banco.Archivar(banco.Casos[0], banco.Casos[1]);
        Assert.HasCount(N - 2, banco.Base.CasosVivosDe(banco.Sandy.Id), "el montaje: y el otro al archivar");

        var loQueHizo = banco.LoQueHizoEnSuInforme();
        Assert.AreEqual(N.ToString(), CifraDelMes(loQueHizo, "Documentos que se le asignaron"),
            "las retiradas siguen contando: el informe las pide vivas y retiradas");
        Assert.AreEqual("2", CifraDelMes(loQueHizo, "Documentos que contestó"), "contestó los dos archivados");
        Assert.AreEqual("1", CifraDelMes(loQueHizo, "… y dijo que estaban completos"));
    }

    /// <summary>⛔ Si el documento lo llevan DOS, archivarlo se lo quita a los dos: archivado no es de nadie.</summary>
    [TestMethod]
    public void SiLoLlevanDosArchivarloSeLoQuitaALosDos()
    {
        using var banco = new BancoDelArchivado(1);
        var otro = banco.Base.Alta("Agente de prueba dos");
        banco.Base.Dar(otro, banco.Casos[0]);

        banco.Archivar(banco.Casos[0]);

        Assert.IsEmpty(banco.Base.CasosVivosDe(banco.Sandy.Id));
        Assert.IsEmpty(banco.Base.CasosVivosDe(otro.Id));
    }

    /// <summary>Archivar un documento que no lleva nadie no deja ningún aviso de «no había nada que retirar».</summary>
    [TestMethod]
    public void ArchivarLoQueNoLlevaNadieNoDejaAvisos()
    {
        using var banco = new BancoDelArchivado(0);
        var suelto = banco.Base.Caso("SUEL0001");

        var resumen = banco.Acciones.ArchivarEnLote([suelto]);

        Assert.AreEqual(1, resumen.Hechos);
        Assert.AreEqual("1 documento archivado.", resumen.Linea);
        Assert.IsEmpty(banco.Base.Avisos.Pendientes, "ni una franja por un caso sin asignación");
    }

    /// <summary>La línea del acuse dice cuántos dejan de estar asignados, para que la cifra que baja no parezca perdida.</summary>
    [TestMethod]
    public void LaLineaDelAcuseDiceCuantosDejanDeEstarAsignados()
    {
        using var banco = new BancoDelArchivado(N);
        var suelto = banco.Base.Caso("SUEL0001");

        var resumen = banco.Acciones.ArchivarEnLote([banco.Casos[0], banco.Casos[1], suelto]);

        Assert.AreEqual("3 documentos archivados; 2 dejan de estar asignados.", resumen.Linea);
    }

    /// <summary>
    /// ⚠️ El caso de los 1 000: lo archivado ANTES de hoy con la asignación viva también se limpia.
    /// </summary>
    /// <remarks>
    /// Se archiva por el puerto a secas, que es como quedó lo de antes de esta regla, y luego se
    /// pasa la limpieza que Inicio corre al llegar. Lo que no estaba archivado no se toca.
    /// </remarks>
    [TestMethod]
    public void LoArchivadoDeAntesConAsignacionVivaTambienSeLimpia()
    {
        using var banco = new BancoDelArchivado(N);
        banco.Base.Casos.Archivar(banco.Casos[0], true, "2026-08-20");
        banco.Base.Casos.Archivar(banco.Casos[1], true, "2026-08-21");
        Assert.HasCount(N, banco.Base.CasosVivosDe(banco.Sandy.Id), "el montaje: archivados y siguen asignados");

        var limpieza = banco.Retirada.QuitarLasDeLoQueYaEstabaArchivado();

        Console.WriteLine(limpieza.Linea);
        Assert.AreEqual(2, limpieza.Documentos);
        Assert.AreEqual(2, limpieza.Asignaciones);
        Assert.HasCount(N - 2, banco.Base.CasosVivosDe(banco.Sandy.Id));
        Assert.AreEqual(N - 2, banco.LeerInicio().Equipo.Single(r => r.Id == banco.Sandy.Id).Casos);
        Assert.AreEqual("2026-08-20", banco.Base.Casos.Obtener(banco.Casos[0])!.FechaArchivado, "la fecha de archivado no se toca");

        var segunda = banco.Retirada.QuitarLasDeLoQueYaEstabaArchivado();
        Assert.AreEqual(0, segunda.Documentos, "la segunda pasada no encuentra nada: no escribe dos veces");
        Assert.IsFalse(segunda.HuboAlgo);
    }

    /// <summary>Con nada archivado la limpieza no toca nada y no dice nada.</summary>
    [TestMethod]
    public void SinArchivadosLaLimpiezaNoEscribeNada()
    {
        using var banco = new BancoDelArchivado(N);

        var limpieza = banco.Retirada.QuitarLasDeLoQueYaEstabaArchivado();

        Assert.IsFalse(limpieza.HuboAlgo);
        Assert.HasCount(N, banco.Base.CasosVivosDe(banco.Sandy.Id));
        Assert.IsEmpty(banco.Base.Avisos.Pendientes);
    }

    /// <summary>Cuánto cuesta la limpieza al llegar a Inicio con 3 000 documentos, medido y dicho.</summary>
    [TestMethod]
    public void LaLimpiezaDeLoDeAntesSeMideConTresMil()
    {
        var servicios = new Fichas.Datos.Falso.ServiciosFalsos(3000, 20260911, new Fichas.Datos.Falso.RelojFijo("2026-09-11"));
        var avisos = new BuzonDeAvisos();
        var retirada = new RetiradaAlArchivar(
            servicios.Asignaciones, servicios.Casos, new OperacionDeAsignar(servicios.Asignaciones, servicios.Reloj, avisos));

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var primera = retirada.QuitarLasDeLoQueYaEstabaArchivado();
        cronometro.Stop();
        var conTrabajo = cronometro.Elapsed.TotalMilliseconds;

        cronometro.Restart();
        var segunda = retirada.QuitarLasDeLoQueYaEstabaArchivado();
        cronometro.Stop();

        Console.WriteLine($"Con 3 000 documentos inventados: {primera.Linea} en {conTrabajo:F1} ms; "
            + $"la segunda pasada, sin nada que hacer, {cronometro.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.IsGreaterThan(0, primera.Documentos, "el generador inventa archivados asignados; si no, esto no mide nada");
        Assert.AreEqual(0, segunda.Documentos);
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>La cifra que el informe del agente pone al lado de ese renglón.</summary>
    /// <param name="loQueHizo">La sección «lo que hizo» del informe.</param>
    /// <param name="renglon">El rótulo de la fila que se busca; tiene que haber exactamente una.</param>
    private static string? CifraDelMes(Seccion loQueHizo, string renglon)
        => loQueHizo.Filas.Single(fila => fila[0] == renglon)[1];

    /// <summary>Un agente con N documentos, y las puertas de Revisar, Inicio y el informe montadas encima.</summary>
    private sealed class BancoDelArchivado : IDisposable
    {
        /// <summary>Da de alta al agente, le asigna esos documentos y monta las puertas encima.</summary>
        /// <param name="cuantos">Cuántos documentos con gente se le asignan.</param>
        public BancoDelArchivado(int cuantos)
        {
            Base = new BaseDelPaquete();
            Sandy = Base.Alta("Agente de prueba uno");
            Casos = Enumerable.Range(1, cuantos).Select(i => UnDocumentoConGente($"AAAA{i:D4}")).ToList();
            if (cuantos > 0) Base.Dar(Sandy, [.. Casos]);
            Retirada = new RetiradaAlArchivar(Base.Asignaciones, Base.Casos, Base.Reparto);
            Acciones = new AccionesDeRevisar(Base.Casos, Base.Reloj, Base.Avisos, Retirada);
        }

        /// <summary>La base SQLite de la prueba, con sus puertos y su reloj.</summary>
        public BaseDelPaquete Base { get; }
        /// <summary>El agente al que se le asignó todo.</summary>
        public Companero Sandy { get; }
        /// <summary>Los documentos asignados, en el orden en que se metieron.</summary>
        public List<long> Casos { get; }
        /// <summary>La retirada de verdad, para llamar aparte al caso de los 1 000.</summary>
        public RetiradaAlArchivar Retirada { get; }
        /// <summary>Las acciones de Revisar con la retirada puesta, como en el programa.</summary>
        public AccionesDeRevisar Acciones { get; }

        /// <summary>Archiva por la acción de verdad y exige que entren todos; si no, el montaje está mal.</summary>
        /// <param name="casoIds">Los documentos que se archivan.</param>
        public void Archivar(params long[] casoIds)
        {
            var resumen = Acciones.ArchivarEnLote(casoIds);
            Assert.AreEqual(casoIds.Length, resumen.Hechos, "no se pudieron archivar todos al montar la prueba");
        }

        /// <summary>Lo que vería Inicio, leído por el mismo lector que la pantalla.</summary>
        public ResumenDeInicio LeerInicio()
            => new LectorDelInicio(Base.Casos, Base.Personas, Base.Companeros, Base.Asignaciones, Base.Reloj, Base.Procedencia)
                .Leer();

        /// <summary>La primera sección del informe del agente en septiembre: lo que hizo.</summary>
        public Seccion LoQueHizoEnSuInforme()
        {
            var reportes = new ReportesEnPdf(
                Base.Casos, Base.Personas, Base.Companeros, Base.Asignaciones, Base.Procedencia, Base.Reloj);
            var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;
            return reportes.DocumentoDeCompanero(Sandy.Id, periodo, Base.Reloj.Ahora()).Secciones[0];
        }

        /// <summary>Un documento que viaja el 17 de septiembre con una persona dentro.</summary>
        /// <param name="numero">El número de caso, único en la prueba.</param>
        /// <returns>El número interno del documento.</returns>
        private long UnDocumentoConGente(string numero)
        {
            var casoId = Base.Caso(numero, "2026-09-17");
            Base.Persona(casoId, "Persona de prueba A", "100" + numero[4..], 1);
            return casoId;
        }

        /// <summary>Cierra la base de la prueba.</summary>
        public void Dispose() => Base.Dispose();
    }
}
