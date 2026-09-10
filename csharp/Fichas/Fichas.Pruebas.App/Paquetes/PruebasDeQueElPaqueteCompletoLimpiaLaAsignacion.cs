using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Reportes;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.App.Paquetes;

/// <summary>
/// Un paquete que vuelve COMPLETO le quita al agente esa asignación; el que vuelve sin
/// completar se le queda, con lo que él escribió.
/// </summary>
/// <remarks>
/// <para><b>Lo pidió el dueño el 2026-09-07, con estas palabras:</b> <i>«No quiero que después
/// de subir el paquete de los agentes al sistema y le asigne más casos, se queden los casos que
/// él ya ha completado. Debe quedar limpio. Cuando él sube un paquete que completó, debe
/// quitarle que ese caso está asignado a él. Lo único que no se le quitan son los que no están
/// completos aún, pero debe quedar la información que él colocó en el paquete.»</i></para>
///
/// <para>⚠️ <b>Esto cambia lo que se decidió unas horas antes el mismo día</b>, y el motivo por
/// el que se decidió lo contrario era falso. Se eligió filtrar el paquete y NO cerrar la
/// asignación creyendo que cerrarla borraba del informe del agente el trabajo que acababa de
/// hacer. No lo borra: <c>ReportesEnPdf.DocumentoDeCompanero</c> pide sus asignaciones con
/// <c>SoloActivas: false</c> y arma sus casos con TODAS, vivas y retiradas. Eso no se cree, se
/// mide, y lo mide <see cref="ElInformeDelAgenteSigueTrayendoLoQueHizoDespuesDeQuitarleElCaso"/>
/// — la prueba que sostiene el cambio entero.</para>
///
/// <para>Todo va por el camino de verdad: se genera el Excel con el motor real, se contesta como
/// lo contestaría el agente, se aplica la vuelta, y lo que queda vivo <b>se consulta en la
/// base</b> por el mismo puerto que usan las pantallas. Escribir a mano la fila que decide sería
/// comprobar la suposición de la prueba.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueElPaqueteCompletoLimpiaLaAsignacion
{
    /// <summary>
    /// El caso que vuelve completo deja de estar asignado; el que vuelve sin completar se queda.
    /// </summary>
    /// <remarks>
    /// Es el criterio del dueño entero, medido en una sola vuelta con las dos mitades dentro:
    /// dos documentos del mismo agente, uno contestado que sí y otro que no.
    /// </remarks>
    [TestMethod]
    public void ElCompletoSaleDeSusAsignacionesYElNoCompletoSeQueda()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var completo = UnDocumentoConGente(banco, "AAAA0001");
        var sinCompletar = UnDocumentoConGente(banco, "AAAA0002");
        banco.Dar(sandy, completo, sinCompletar);

        banco.IdaYVuelta(sandy, "Sí", completo);
        banco.IdaYVuelta(sandy, "No", sinCompletar);

        var vivas = CasosVivosDe(banco, sandy.Id);

        Assert.HasCount(1, vivas, "le tiene que quedar viva una sola: la que no completó");
        CollectionAssert.AreEqual(new List<long> { sinCompletar }, vivas);
        CollectionAssert.DoesNotContain(vivas, completo, "el que devolvió completo debe quedar limpio");
    }

    /// <summary>
    /// ⛔ Quitarle la asignación NO borra la fila: se desactiva con su fecha y se conserva.
    /// </summary>
    /// <remarks>
    /// Es lo que distingue «quedar limpio» de «perder de quién era». Sin esta prueba, cerrar la
    /// asignación y borrarla se verían igual desde la pantalla.
    /// </remarks>
    [TestMethod]
    public void LaAsignacionRetiradaSigueEnLaBaseConSuFecha()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var completo = UnDocumentoConGente(banco, "AAAA0001");
        banco.Dar(sandy, completo);

        banco.IdaYVuelta(sandy, "Sí", completo);

        var todas = banco.TodasLasDe(sandy.Id);
        Assert.HasCount(1, todas, "la fila no se borra: se desactiva");
        Assert.IsFalse(todas[0].Activa);
        Assert.AreEqual(completo, todas[0].CasoId);
        Assert.AreEqual(BaseDelPaquete.ElInstante, todas[0].DesactivadaEn,
            "se desactiva con la fecha del día en que volvió el paquete");
    }

    /// <summary>
    /// ⚠️ Lo que el compañero escribió en el documento SIN completar se conserva entero.
    /// </summary>
    /// <remarks>
    /// Es la segunda mitad literal del encargo: <i>«debe quedar la información que él colocó en
    /// el paquete»</i>. Se mira en las personas —los siete pasos— y en el documento —el estado
    /// del compañero y quién lo puso—, que son los dos sitios donde queda escrito.
    /// </remarks>
    [TestMethod]
    public void LoQueEscribioElCompaneroEnElNoCompletoSeConserva()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var sinCompletar = UnDocumentoConGente(banco, "AAAA0002");
        banco.Dar(sandy, sinCompletar);

        banco.IdaYVuelta(sandy, "No", sinCompletar);

        var caso = banco.Casos.Obtener(sinCompletar)!;
        Assert.AreEqual(
            EstadoDeRecomendacion.NoCompleta,
            Caso.LeerEstado(caso.EstadoDelCompanero),
            "lo que dijo el compañero se queda escrito");
        Assert.AreEqual(sandy.Id, caso.EstadoDelCompaneroPor, "y con su nombre detrás");

        foreach (var persona in banco.Personas.DeCaso(sinCompletar))
        {
            Assert.IsFalse(persona.PasoPreparacion, $"«{persona.Nombre}»: el paso 1 que contestó");
            Assert.IsFalse(persona.PasoListoParaElTemplo, $"«{persona.Nombre}»: el paso 6 que contestó");
        }
    }

    /// <summary>
    /// Y lo que escribió en el que SÍ completó tampoco se borra al quitarle la asignación.
    /// </summary>
    [TestMethod]
    public void LoQueEscribioEnElCompletoTampocoSeBorraAlQuitarleLaAsignacion()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var completo = UnDocumentoConGente(banco, "AAAA0001");
        banco.Dar(sandy, completo);

        banco.IdaYVuelta(sandy, "Sí", completo);

        var caso = banco.Casos.Obtener(completo)!;
        Assert.AreEqual(EstadoDeRecomendacion.Completa, Caso.LeerEstado(caso.EstadoDelCompanero));
        Assert.AreEqual(sandy.Id, caso.EstadoDelCompaneroPor);
        Assert.IsTrue(banco.Personas.DeCaso(completo).All(persona => persona.PasoPreparacion == true));
    }

    /// <summary>
    /// ⚠️⚠️ La prueba que sostiene el cambio: su informe SIGUE trayendo lo que hizo después de
    /// quitarle las asignaciones.
    /// </summary>
    /// <remarks>
    /// <para>Es lo que el dueño confirmó con sus palabras —«en el informe del agente igual,
    /// aunque ya no lo tenga asignado»— y lo que la decisión anterior daba por imposible. Si
    /// esta prueba se pusiera en rojo, cerrar la asignación al volver dejaría de poderse hacer y
    /// habría que devolvérselo al dueño.</para>
    ///
    /// <para>Se mide sobre la sección «Lo que hizo» del informe REAL, no sobre un doble: dos
    /// documentos contestados, uno completo y otro no, y el informe tiene que seguir diciendo
    /// que contestó los dos aunque solo le quede una asignación viva.</para>
    /// </remarks>
    [TestMethod]
    public void ElInformeDelAgenteSigueTrayendoLoQueHizoDespuesDeQuitarleElCaso()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var completo = UnDocumentoConGente(banco, "AAAA0001");
        var sinCompletar = UnDocumentoConGente(banco, "AAAA0002");
        banco.Dar(sandy, completo, sinCompletar);

        banco.IdaYVuelta(sandy, "Sí", completo);
        banco.IdaYVuelta(sandy, "No", sinCompletar);

        Assert.HasCount(1, CasosVivosDe(banco, sandy.Id), "el montaje: ya se le quitó el completo");

        var loQueHizo = LoQueHizoEnSuInforme(banco, sandy.Id);

        Assert.AreEqual("2", CifraDelMes(loQueHizo, "Documentos que se le asignaron"),
            "las asignaciones retiradas siguen contando: el informe las pide vivas y retiradas");
        Assert.AreEqual("2", CifraDelMes(loQueHizo, "Documentos que contestó"),
            "contestó los dos, y quitarle uno de encima no le borra el trabajo");
        Assert.AreEqual("1", CifraDelMes(loQueHizo, "… y dijo que estaban completos"),
            "el que completó tiene que seguir constando como suyo");
    }

    /// <summary>
    /// ⛔ Si el documento lo llevan DOS, solo se le quita al que lo devolvió completo.
    /// </summary>
    /// <remarks>
    /// Hoy el motor admite dos asignaciones vivas sobre el mismo caso (la P-11 sigue abierta).
    /// Quitárselo también al otro por algo que él no ha hecho le borraría el trabajo de encima
    /// sin que nadie lo haya pedido, y ese es el daño concreto que esta prueba cierra.
    /// </remarks>
    [TestMethod]
    public void SiDosLlevanElMismoDocumentoSoloSeLeQuitaAlQueLoCompleto()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var otro = banco.Alta("Agente de prueba dos");
        var documento = UnDocumentoConGente(banco, "AAAA0001");
        banco.Dar(sandy, documento);
        banco.Dar(otro, documento);

        banco.IdaYVuelta(sandy, "Sí", documento);

        Assert.IsEmpty(CasosVivosDe(banco, sandy.Id), "al que lo completó se le quita");
        CollectionAssert.AreEqual(
            new List<long> { documento },
            CasosVivosDe(banco, otro.Id),
            "al otro NO: él no lo ha mirado y sigue siendo trabajo suyo");
    }

    /// <summary>
    /// La misma comprobación por el otro camino: quitándoselas A MANO con el botón que ya existía.
    /// </summary>
    /// <remarks>
    /// Esta es la que se corrió ANTES de escribir una sola línea del cambio, contra el código
    /// tal como estaba, para verificar la premisa del pase sin creérsela: si el informe
    /// aguantaba que se le quitaran todas las asignaciones con el botón de «quitarle todo»,
    /// aguantaba que se le quitaran solas al volver el paquete. Se queda porque vigila las dos
    /// puertas: si alguien cambia el informe para mirar solo las vivas, las dos se ponen rojas.
    /// </remarks>
    [TestMethod]
    public void QuitarleTodoAManoTampocoLeBorraElInforme()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var uno = UnDocumentoConGente(banco, "AAAA0001");
        var dos = UnDocumentoConGente(banco, "AAAA0002");
        banco.Dar(sandy, uno, dos);
        banco.IdaYVuelta(sandy, "Sí", uno);
        banco.IdaYVuelta(sandy, "No", dos);

        banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);

        Assert.IsEmpty(CasosVivosDe(banco, sandy.Id), "el montaje: no le queda ninguna viva");

        var loQueHizo = LoQueHizoEnSuInforme(banco, sandy.Id);
        Assert.AreEqual("2", CifraDelMes(loQueHizo, "Documentos que se le asignaron"));
        Assert.AreEqual("2", CifraDelMes(loQueHizo, "Documentos que contestó"));
        Assert.AreEqual("1", CifraDelMes(loQueHizo, "… y dijo que estaban completos"));
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>Los casos que ese compañero lleva VIVOS, leídos de la base y ordenados.</summary>
    /// <remarks>
    /// Por el puerto de asignaciones y con <c>SoloActivas: true</c>, que es exactamente lo que
    /// miran Asignar, Inicio y el paquete siguiente. Contarlo de otra manera mediría otra cosa.
    /// </remarks>
    private static List<long> CasosVivosDe(BaseDelPaquete banco, long companeroId)
        => [.. banco.Asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true), Pagina.Primera(int.MaxValue))
            .Elementos
            .Select(asignacion => asignacion.CasoId)
            .Order()];

    /// <summary>La sección «Lo que hizo» del informe real de ese agente.</summary>
    private static Seccion LoQueHizoEnSuInforme(BaseDelPaquete banco, long companeroId)
    {
        var reportes = new ReportesEnPdf(
            banco.Casos, banco.Personas, banco.Companeros,
            banco.Asignaciones, banco.Procedencia, banco.Reloj);
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

        return reportes.DocumentoDeCompanero(companeroId, periodo, banco.Reloj.Ahora()).Secciones[0];
    }

    /// <summary>La cifra del mes de ese renglón de «Lo que hizo».</summary>
    private static string? CifraDelMes(Seccion loQueHizo, string renglon)
        => loQueHizo.Filas.Single(fila => fila[0] == renglon)[1];

    /// <summary>Un documento con dos personas dentro, y devuelve su id.</summary>
    private static long UnDocumentoConGente(BaseDelPaquete banco, string numero)
    {
        var casoId = banco.Caso(numero, "2026-09-17");
        banco.Persona(casoId, "Persona de prueba A", "100" + numero[4..], 1);
        banco.Persona(casoId, "Persona de prueba B", "200" + numero[4..], 2);
        return casoId;
    }
}
