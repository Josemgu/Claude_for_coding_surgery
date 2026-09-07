using Fichas.App.Inicio;
using Fichas.Contratos.Consultas;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// FASE C21 — el ticket vencido: quien viajo sin la recomendacion confirmada.
/// </summary>
/// <remarks>
/// <para><b>Es el daño que este programa existe para evitar</b>, dicho en la primera linea
/// de <c>CLAUDE.md</c>: <i>«una persona que llega al templo y no puede entrar porque su
/// recomendación estaba mal y nadie lo vio a tiempo»</i>.</para>
///
/// <para>La regla, escrita para que dos personas la lean igual (C21-1): <b>un ticket esta
/// vencido si su fecha de viaje ya paso y su estado no es «lista para viajar»</b>.</para>
///
/// <para>⚠️ <b>Una precision que el ADR-0006 §4.2 deja escrita como «no era lista para
/// viajar EL DIA DEL VIAJE» y que aqui no se puede cumplir al pie de la letra:</b> el estado
/// no se guarda —se deriva de las seis columnas (decision A del ADR-0006 §2.4)— y no hay
/// historial, asi que lo unico que el programa puede mirar es el estado de AHORA. Se
/// implementa asi y se dice; contestar sus seis preguntas despues del viaje saca a esa
/// persona de la lista, que es lo que el dueno querria de todas formas.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelTicketVencido
{
    /// <summary>
    /// C21-1 y C21-3. Se cuentan PERSONAS, no documentos: una familia de cinco donde fallo
    /// uno es UNA persona.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la frase del criterio, literal, y es el motivo de que este bloque exista. Si el
    /// programa dijera «1 documento sin completar», el dueno no sabria que de cinco personas
    /// solo hay una que llamar al obispo.
    /// </remarks>
    [TestMethod]
    public void UnaFamiliaDeCincoDondeFalloUnoEsUnaPersonaVencidaYNoUnDocumento()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "FAMI2608", "2026-08-20", cuantasPersonas: 5);
        for (var fila = 1; fila <= 4; fila++) BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 5);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.HasCount(1, loDelObispo.Vencidas, "Una persona de cinco, no un documento.");
        Assert.AreEqual("Persona 5", loDelObispo.Vencidas[0].Nombre);
        StringAssert.Contains(loDelObispo.FraseDeLoVencido, "1 persona", StringComparison.Ordinal);
        Assert.DoesNotContain("documento", loDelObispo.FraseDeLoVencido);
    }

    /// <summary>
    /// C21-1. «Sin mirar» tambien vence: nadie confirmo su recomendacion y ya viajo.
    /// </summary>
    /// <remarks>
    /// Es la lectura estricta de la regla y es la unica segura: dejar fuera a las que nadie
    /// miro seria dar por buenas a las que nadie miro, que es exactamente el daño que se
    /// evita.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaQueNadieMiroYYaViajoTambienEstaVencida()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "MIRA2608", "2026-08-20", estado: "completa", cuantasPersonas: 3);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 2);
        // La tercera no la miro nadie.

        var vencidas = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.Vencidas;

        Assert.HasCount(2, vencidas, "La que está en «no» y la que nadie miró.");
        CollectionAssert.AreEquivalent(
            new[] { "Persona 2", "Persona 3" },
            vencidas.Select(v => v.Nombre).ToArray());
    }

    /// <summary>Una persona confirmada que ya viajo NO sale: su ticket se cerro a tiempo.</summary>
    [TestMethod]
    public void UnaPersonaConfirmadaQueYaViajoNoSale()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "BIEN2608", "2026-08-20", cuantasPersonas: 2);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 2);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.IsEmpty(loDelObispo.Vencidas);
        Assert.IsGreaterThan(0, loDelObispo.FraseDeLoVencido.Length, "«Ninguna» también se dice con palabras.");
        StringAssert.Contains(loDelObispo.FraseDeLoVencido, "ninguna", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Archivar un documento SI cierra el ticket vencido de sus personas: es cerrar el caso.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Esta prueba decia lo contrario hasta el 2026-09-06</b> —se llamaba
    /// <c>ArchivarNoCierraNiEscondeUnTicketVencido</c> y era el criterio C21-2—, y la razon
    /// que daba era buena: si archivar borrara la lista, el programa daria por resuelto lo
    /// que solo se guardo en un cajon. El dueno la deshizo con conocimiento de eso, porque
    /// para el archivar ES resolver: <i>«si ya resolví un archivo y lo archivo, no debe
    /// aparecer en notificaciones, debe salir del tablero, no se cuenta ya»</i>. Es un
    /// sistema de tickets y archivar es cerrar uno (<c>DECISIONES.md</c>, 2026-09-05).
    /// <para>Lo que sigue vigilando: que un documento archivado no genere ni un aviso. Antes
    /// se comprobaba que generase el aviso marcado; ahora, que no genere ninguno.</para>
    /// <para><b>Y lo que NO se pierde</b>, comprobado abajo: sigue en la base y sigue
    /// contando en los reportes, que es donde el dueno lo mira a proposito.</para>
    /// </remarks>
    [TestMethod]
    public void ArchivarCierraElTicketVencidoPeroNoBorraElCaso()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "ARCH2608", "2026-08-20", archivado: true, cuantasPersonas: 2);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 2);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.IsEmpty(loDelObispo.Vencidas, "Archivado es cerrado: no queda ticket abierto.");
        Assert.IsEmpty(loDelObispo.QueApremian);
        Assert.IsNull(loDelObispo.ProximoGrupo, "Tampoco es «el grupo que viene».");

        // Lo que no cambia: el caso sigue en la base, entero.
        Assert.AreEqual(1, servicios.Casos.Contar(
            FiltroDeCasos.Todo with { IncluirArchivados = true }),
            "Archivar saca de la vista; no borra.");
    }

    /// <summary>
    /// C21-4. Lo que apremia usa la ventana de 7 dias QUE YA EXISTE, y no un segundo numero.
    /// </summary>
    /// <remarks>
    /// ⛔ Dos numeros distintos para «pronto» en la misma pantalla es otra vez el programa
    /// diciendo dos cosas. Esta prueba lo ata al mismo sitio del que sale la franja
    /// sombreada del calendario: <c>LectorDelInicio.DiasDeLaVentana</c>.
    /// </remarks>
    [TestMethod]
    public void LoQueApremiaUsaLosMismosSieteDiasDelCalendario()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var dentro = BaseDeInicio.MeterCaso(servicios, "PRON2609", "2026-09-09", cuantasPersonas: 2);
        var justo = BaseDeInicio.MeterCaso(servicios, "JUST2609", "2026-09-11", cuantasPersonas: 1);
        var fuera = BaseDeInicio.MeterCaso(servicios, "LEJO2609", "2026-09-12", cuantasPersonas: 4);

        BaseDeInicio.DejarNoListaParaViajar(servicios, dentro, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, justo, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, fuera, fila: 1);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.AreEqual(LectorDelInicio.DiasDeLaVentana, loDelObispo.DiasDeLaVentana,
            "La ventana de lo que apremia es la MISMA que la del calendario, no otra.");

        // Hoy es el 2026-09-04: el 11 es el último día de los siete, el 12 ya no entra.
        Assert.HasCount(3, loDelObispo.QueApremian, "Dos del día 9 y una del 11.");
        Assert.IsFalse(loDelObispo.QueApremian.Any(p => p.NumeroCaso == "LEJO2609"));
        StringAssert.Contains(loDelObispo.FraseDeLoQueApremia, "7 días", StringComparison.Ordinal);
        StringAssert.Contains(loDelObispo.FraseDeLoQueApremia, "3 personas", StringComparison.Ordinal);
    }

    /// <summary>Lo que ya viajo y lo que apremia son dos listas y ninguna se traga a la otra.</summary>
    [TestMethod]
    public void LoVencidoYLoQueApremiaNoSeSolapan()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var viejo = BaseDeInicio.MeterCaso(servicios, "VIEJ2608", "2026-08-20", cuantasPersonas: 1);
        var pronto = BaseDeInicio.MeterCaso(servicios, "PRON2609", "2026-09-06", cuantasPersonas: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, viejo, fila: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, pronto, fila: 1);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.HasCount(1, loDelObispo.Vencidas);
        Assert.HasCount(1, loDelObispo.QueApremian);
        Assert.AreEqual("VIEJ2608", loDelObispo.Vencidas[0].NumeroCaso);
        Assert.AreEqual("PRON2609", loDelObispo.QueApremian[0].NumeroCaso);
    }

    /// <summary>
    /// C21-6. <c>pudo_viajar</c> y <c>motivo_no_viajo</c> no se tocan: son otra pregunta.
    /// </summary>
    /// <remarks>
    /// ⛔ Aquellas dicen QUE PASO DESPUES; esta dice que nadie confirmo la recomendacion
    /// ANTES. Confundirlas haria que el programa diera por explicado lo que solo esta
    /// avisado. Se comprueba que una persona con <c>PudoViajar = true</c> escrito sigue
    /// saliendo como vencida si sus seis preguntas no estan.
    /// </remarks>
    [TestMethod]
    public void PudoViajarNoBorraElTicketVencido()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "PUDO2608", "2026-08-20", cuantasPersonas: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 1);

        var cual = servicios.Almacen.Personas.First(p => p.Value.CasoId == casoId);
        servicios.Almacen.Personas[cual.Key] = cual.Value with { PudoViajar = true, MotivoNoViajo = null };

        var vencidas = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.Vencidas;

        Assert.HasCount(1, vencidas, "«Pudo viajar» es otra pregunta y no cierra este ticket.");
    }

    /// <summary>Cada persona vencida dice en que paso se quedo, para poder llamar al obispo.</summary>
    [TestMethod]
    public void CadaPersonaVencidaDiceEnQuePasoSeQuedo()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "LLAM2608", "2026-08-20", cuantasPersonas: 1);
        BaseDeInicio.ContestarLasSeisDe(
            servicios, casoId, fila: 1,
            preparacion: true, informacion: false, citaDelTemplo: true,
            accionesRequeridas: true, entrevistas: true, listoParaElTemplo: true);

        var vencida = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.Vencidas.Single();

        CollectionAssert.AreEqual(new[] { "Información" }, vencida.SeQuedoEn.ToArray());
        StringAssert.Contains(vencida.Detalle, "se quedó en Información", StringComparison.Ordinal);
        StringAssert.Contains(vencida.Detalle, "hace", StringComparison.Ordinal);
        Assert.DoesNotContain("\n", vencida.Detalle, "Ni un párrafo: una línea.");
        StringAssert.Contains(vencida.ParaElLector, "Persona 1", StringComparison.Ordinal);
    }

    /// <summary>
    /// C21-5. No hay ningun aviso que salga solo: el aviso es una lista que se ve al abrir.
    /// </summary>
    /// <remarks>
    /// Se comprueba por donde se puede comprobar sin ventana: leer el resumen NO deja ni un
    /// aviso nuevo en la franja por tener personas vencidas. Ni correo, ni sonido, ni
    /// servicio: regla permanente 2 y nadie lo ha pedido.
    /// </remarks>
    [TestMethod]
    public void TenerPersonasVencidasNoDisparaNingunAviso()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "AVIS2608", "2026-08-20", cuantasPersonas: 3);
        BaseDeInicio.DejarNoListaParaViajar(servicios, casoId, fila: 1);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.HasCount(3, resumen.ElSistemaDelObispo.Vencidas);
        Assert.IsEmpty(resumen.Avisos, "Lo vencido se ve en su lista, no en un aviso que salta.");
    }
}
