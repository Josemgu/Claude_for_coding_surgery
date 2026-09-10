using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El informe de un agente conserva lo que hizo, aunque le hayan retirado la asignación.
/// </summary>
/// <remarks>
/// <para><b>Del dueño</b>, cuando se le preguntó si quería que el informe siguiera contando el
/// trabajo de un caso ya devuelto: <i>«en el informe del agente igual, aunque ya no lo tenga
/// asignado»</i>, y el 2026-09-08: <i>«que conserve lo retirado»</i>.</para>
///
/// <para><b>Por qué hacía falta esta prueba.</b> Desde el 2026-09-07, devolver un caso completo
/// retira su asignación. La sección «Lo que hizo» ya se armaba con TODAS las asignaciones
/// —vivas y retiradas— pero las secciones de cola se recortaban con las vivas, así que el
/// mismo informe decía 7 arriba y 3 abajo: el trabajo terminado desaparecía justo por haberse
/// terminado.</para>
///
/// <para>⚠️ <b>Son DOS preguntas distintas y las dos siguen contestándose.</b> «Lo que hizo
/// este mes» conserva lo retirado; «lo que lleva encima ahora mismo» NO, y no es un descuido:
/// es el resumen de esa misma sección, que dice literalmente «Esa cifra es de hoy, no del
/// período». Si esa cifra pasara a contar los retirados, el informe dejaría de poder decir
/// cuánto trabajo tiene el agente por delante. Lo comprueba
/// <see cref="ElResumenSigueDiciendoSoloLoQueLlevaEncimaAhoraMismo"/>.</para>
/// </remarks>
[TestClass]
public class PruebaDeQueElInformeConservaLoRetirado
{
    private const string Desde = "2026-09-01";
    private const string Hasta = "2026-09-30";
    private const string Generado = BaseDePrueba.Hoy + " 10:00:00";

    /// <summary>Cuántos casos hizo Sandy en total: los que cuenta «lo que hizo».</summary>
    private const int SuyosDeSiempre = 7;

    /// <summary>Cuántos le quedan asignados hoy: los que cuenta «lo que lleva encima».</summary>
    private const int VivosAhora = 3;

    // ─────────────────────── lo que hizo, que ya conservaba ───────────────────────

    [TestMethod]
    public void LoQueHizoSigueContandoLosSieteYNoSoloLosTresVivos()
    {
        var documento = InformeDeSandy();
        var loQueHizo = Seccion(documento, ArmadoDelInformeDeAgente.TituloDeLaSeccion);

        Assert.AreEqual(
            SuyosDeSiempre.ToString(),
            loQueHizo.Filas[0][1],
            "«Documentos que se le asignaron» dejó de contar los que ya devolvió.");
    }

    // ─────────────────────── las secciones de cola, que NO conservaban ───────────────────────

    [TestMethod]
    public void LasSeccionesDeColaTraenLosSieteCasosYNoSoloLosTresVivos()
    {
        var documento = InformeDeSandy();

        // Una fila por persona, y en este escenario hay una persona por caso.
        foreach (var titulo in new[]
        {
            Vocabulario.QuienesViajaronSinVerificar,
            "Los viajes",
            "Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación",
        })
        {
            var seccion = Seccion(documento, titulo);
            Assert.HasCount(
                SuyosDeSiempre,
                seccion.Filas,
                $"«{titulo}» salió con {seccion.Filas.Count} filas: se recortó a los vivos y borró "
                + "del informe el trabajo que Sandy ya había terminado.");
        }
    }

    [TestMethod]
    public void LosCuatroCasosDevueltosSiguenSaliendoConSuNumeroDeCaso()
    {
        var documento = InformeDeSandy();
        var viajes = Seccion(documento, "Los viajes");
        var numeros = viajes.Filas.Select(f => f[0]).ToList();

        foreach (var devuelto in new[] { "CASP3004", "CASP3005", "CASP3006", "CASP3007" })
        {
            Assert.Contains(
                devuelto,
                numeros,
                $"«{devuelto}» se devolvió completo y desapareció del informe de quien lo hizo. "
                + $"Los que salieron: {string.Join(", ", numeros)}.");
        }
    }

    [TestMethod]
    public void LaTablaDelEquipoCuentaLasSietePersonasDeLasQueRespondio()
    {
        var documento = InformeDeSandy();
        var equipo = Seccion(documento, "El equipo");

        Assert.HasCount(1, equipo.Filas, "La tabla del equipo de SU informe debe traer una sola fila: la suya.");
        Assert.AreEqual("Sandy", equipo.Filas[0][0]);
        Assert.AreEqual(
            SuyosDeSiempre.ToString(),
            equipo.Filas[0][1],
            "«Personas a su cargo» dejó fuera a las que ya devolvió.");
    }

    // ─────────────────────── la pregunta que NO se contesta igual ───────────────────────

    [TestMethod]
    public void ElResumenSigueDiciendoSoloLoQueLlevaEncimaAhoraMismo()
    {
        var documento = InformeDeSandy();
        var loQueHizo = Seccion(documento, ArmadoDelInformeDeAgente.TituloDeLaSeccion);

        Assert.IsNotNull(loQueHizo.Resumen);
        StringAssert.Contains(
            loQueHizo.Resumen,
            $"Lleva {VivosAhora} documentos asignados ahora mismo",
            $"El resumen de «Lo que hizo» pasó a contar los {SuyosDeSiempre} de siempre. Esa cifra "
            + "es la única del informe que contesta «¿cuánto le queda por delante?», y con los "
            + "retirados dentro deja de poder contestarla.");
    }

    [TestMethod]
    public void LaPortadaSaleDeLoQueHizoYNoDeLoQueLeQueda()
    {
        var documento = InformeDeSandy();

        var asignados = documento.Portada.Cifras.Single(c => c.Rotulo == "se le asignaron");
        Assert.AreEqual(
            SuyosDeSiempre,
            asignados.Numero,
            "La portada contó solo los vivos; entonces no coincide con su propia tabla.");
    }

    // ---- el escenario -------------------------------------------------------

    private static Documento InformeDeSandy()
    {
        var (reportes, sandy) = Escenario();
        return reportes.DocumentoDeCompanero(sandy, Periodo.Leer(Desde, Hasta).Periodo!, Generado);
    }

    private static Seccion Seccion(Documento documento, string titulo)
    {
        var seccion = documento.Secciones.FirstOrDefault(s => s.Titulo == titulo);
        Assert.IsNotNull(
            seccion,
            $"El informe no trae ninguna sección «{titulo}». Las que trae: "
            + string.Join(" · ", documento.Secciones.Select(s => s.Titulo)));
        return seccion;
    }

    /// <summary>
    /// Sandy con siete casos: tres todavía asignados y cuatro devueltos y retirados.
    /// </summary>
    /// <remarks>
    /// Los cuatro retirados van con <c>Activa = false</c>, que es lo que deja el botón de retirar
    /// del 2026-09-07. Ninguna persona lleva un paso contestado: así todas caen en «quiénes
    /// viajaron sin verificar» y la cuenta de esa tabla es comprobable.
    ///
    /// Ningún dato es de nadie: nombres, números de caso y unidad son inventados.
    /// </remarks>
    private static (ReportesEnPdf Reportes, long Sandy) Escenario()
    {
        var servicios = new ServiciosFalsos(0, 11, new BaseDePrueba.RelojFijo(BaseDePrueba.Hoy));
        var almacen = servicios.Almacen;
        almacen.Companeros.Clear();

        var sandy = new Companero
        {
            Id = almacen.SiguienteId(), Nombre = "Sandy", Activo = true, CreadoEn = "2026-09-01 08:00:00",
        };
        almacen.Companeros[sandy.Id] = sandy;

        // Los tres que todavía lleva encima.
        Caso(almacen, sandy, "CASP3001", sigueAsignado: true);
        Caso(almacen, sandy, "CASP3002", sigueAsignado: true);
        Caso(almacen, sandy, "CASP3003", sigueAsignado: true);

        // Los cuatro que devolvió completos y por eso ya no tiene asignados.
        Caso(almacen, sandy, "CASP3004", sigueAsignado: false);
        Caso(almacen, sandy, "CASP3005", sigueAsignado: false);
        Caso(almacen, sandy, "CASP3006", sigueAsignado: false);
        Caso(almacen, sandy, "CASP3007", sigueAsignado: false);

        return (
            new ReportesEnPdf(
                servicios.Casos, servicios.Personas, servicios.Companeros,
                servicios.Asignaciones, servicios.Procedencia, servicios.Reloj),
            sandy.Id);
    }

    private static void Caso(AlmacenFalso almacen, Companero quien, string numero, bool sigueAsignado)
    {
        var casoId = almacen.SiguienteId();
        almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numero,
            UnidadNombre = "Castries Branch",
            UnidadNumero = "0700016",
            FechaViaje = "2026-09-15",
            CreadoEn = "2026-09-01 09:00:00",
            // Los devueltos volvieron completos; los tres vivos todavía no ha contestado nadie.
            EstadoRecomendacion = sigueAsignado ? null : "completa",
            EstadoDelCompanero = sigueAsignado ? null : "completa",
            EstadoDelCompaneroPor = sigueAsignado ? null : quien.Id,
            EstadoDelCompaneroEn = sigueAsignado ? null : "2026-09-12 11:00:00",
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
            Activa = sigueAsignado,
        };
    }
}
