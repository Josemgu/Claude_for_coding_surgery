using Fichas.App.Paquetes;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Paquetes;

/// <summary>
/// Que entra en el paquete SIGUIENTE de un companero que ya devolvio uno.
/// </summary>
/// <remarks>
/// <para><b>Lo que dijo el dueno el 2026-09-07:</b> <i>«Los paquetes completados: si eliges
/// otra fecha para el mismo agente, te carga todas las fechas pasadas que ya completó y las
/// fechas nuevas en un solo paquete. Es trabajar dos veces.»</i></para>
///
/// <para>⚠️ <b>La trampa que estas pruebas existen para cerrar.</b> «Devolver» y «completar»
/// no son lo mismo. Un documento que el agente devolvio diciendo que NO se pudo completar
/// —no se pudo comunicar con el lider, el lider no lo hizo— <b>no esta hecho</b>, y sacarlo
/// del paquete lo haria desaparecer del trabajo de alguien sin que nadie lo decida. Por eso
/// hay una prueba para cada mitad: el completado NO vuelve, y el no completado SI vuelve.</para>
///
/// <para>Todo va por el camino de verdad: se genera el Excel con el motor real, se contesta
/// como lo contestaria el agente y se aplica la vuelta. Escribir a mano la columna que decide
/// seria comprobar la propia suposicion de la prueba.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueVaEnElPaqueteSiguiente
{
    /// <summary>Dado un companero con dos documentos, su paquete lleva los dos y su gente.</summary>
    [TestMethod]
    public void ElPrimerPaqueteLlevaTodoLoQueSeLeAsigno()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var deSeptiembre = DosDocumentosConGente(banco, "2026-09-17", "AAAA0001", "AAAA0002");
        banco.Dar(sandy, [.. deSeptiembre]);

        var carga = banco.Ida.Carga(sandy.Id);

        CollectionAssert.AreEquivalent(deSeptiembre.ToList(), carga.CasoIds.ToList());
        Assert.AreEqual(3, carga.Personas, "Dos documentos con dos y una persona son tres personas.");
        Assert.IsEmpty(carga.YaLosDevolvioCompletos);
    }

    /// <summary>
    /// Devuelto completo y con casos de otra fecha encima: el siguiente lleva SOLO los nuevos.
    /// </summary>
    /// <remarks>
    /// Es el caso exacto que conto el dueno, con las dos fechas y el mismo agente.
    /// </remarks>
    [TestMethod]
    public void LoQueYaDevolvioCompletoNoVuelveEnElPaqueteSiguiente()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var deSeptiembre = DosDocumentosConGente(banco, "2026-09-17", "AAAA0001", "AAAA0002");
        banco.Dar(sandy, [.. deSeptiembre]);
        banco.IdaYVuelta(sandy, "Sí", [.. deSeptiembre]);

        var deOctubre = DosDocumentosConGente(banco, "2026-10-22", "BBBB0003", "BBBB0004");
        banco.Dar(sandy, [.. deOctubre]);

        var carga = banco.Ida.Carga(sandy.Id);

        CollectionAssert.AreEquivalent(deOctubre.ToList(), carga.CasoIds.ToList());
        foreach (var yaHecho in deSeptiembre)
            Assert.IsFalse(carga.CasoIds.Contains(yaHecho), $"El documento {yaHecho} ya lo devolvio completo y volvio a entrar.");

        CollectionAssert.AreEquivalent(deSeptiembre.ToList(), carga.YaLosDevolvioCompletos.ToList());
    }

    /// <summary>
    /// ⚠️ Lo devuelto SIN completar sigue en su paquete: no esta hecho y no puede desaparecer.
    /// </summary>
    [TestMethod]
    public void LoQueDevolvioSinCompletarSigueEnElPaqueteSiguiente()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var seCompleto = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        var noSeCompleto = UnDocumentoConGente(banco, "2026-09-17", "AAAA0002");

        banco.Dar(sandy, seCompleto);
        banco.IdaYVuelta(sandy, "Sí", seCompleto);
        banco.Dar(sandy, noSeCompleto);
        banco.IdaYVuelta(sandy, "No", noSeCompleto);

        var carga = banco.Ida.Carga(sandy.Id);

        Assert.IsTrue(
            carga.CasoIds.Contains(noSeCompleto),
            "El documento que volvio SIN completar no esta hecho: tiene que seguir en su paquete.");
        Assert.IsFalse(carga.CasoIds.Contains(seCompleto));
        CollectionAssert.AreEquivalent(new List<long> { noSeCompleto }, carga.VuelvenSinCompletar.ToList());
    }

    /// <summary>Lo que devolvio OTRO companero no es trabajo suyo, y a el si le llega.</summary>
    /// <remarks>
    /// La regla es «no vuelve a recibir trabajo que YA HIZO», no «lo que alguien hizo».
    /// Mirar solo el estado del documento le quitaria del paquete a este agente un caso que
    /// el nunca miro. Hoy el motor admite dos asignaciones vivas sobre el mismo caso (P-11).
    /// </remarks>
    [TestMethod]
    public void LoQueCompletoOtroCompaneroSigueEntrandoEnElPaqueteDeEste()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var otro = banco.Alta("Agente de prueba dos");
        var documento = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");

        banco.Dar(otro, documento);
        banco.IdaYVuelta(otro, "Sí", documento);
        banco.Dar(sandy, documento);

        var suya = banco.Ida.Carga(sandy.Id);
        var ajena = banco.Ida.Carga(otro.Id);

        Assert.IsTrue(suya.CasoIds.Contains(documento), "El no lo ha mirado nunca: le tiene que llegar.");
        Assert.IsFalse(ajena.CasoIds.Contains(documento), "El que si lo completo no lo recibe otra vez.");
    }

    /// <summary>
    /// ⛔ Quitarlo del paquete NO retira su asignacion: sigue viva y sigue contando.
    /// </summary>
    /// <remarks>
    /// Es la mitad medida de la decision. Retirar la asignacion al devolver cambiaria lo que
    /// se ve en Asignar, en Inicio y en el informe del agente —que se recorta a sus
    /// asignaciones VIVAS—; no meterlo en el paquete no cambia ninguna de las tres. El dueno
    /// pidio no trabajar dos veces, no borrar de quien es el trabajo.
    /// </remarks>
    [TestMethod]
    public void SacarloDelPaqueteNoLeQuitaLaAsignacion()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var documento = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        banco.Dar(sandy, documento);
        banco.IdaYVuelta(sandy, "Sí", documento);

        Assert.AreEqual(1, banco.VivasDe(sandy.Id), "La asignacion sigue viva: quien lo hizo se sigue viendo.");
        Assert.IsFalse(banco.Ida.Carga(sandy.Id).CasoIds.Contains(documento));
    }

    /// <summary>
    /// Lo devuelto sin completar se ve en la escalera: no desaparece de la vista de nadie.
    /// </summary>
    /// <remarks>
    /// Con motivo escrito sube al peldano siguiente; sin motivo se queda en el suyo y se
    /// cuenta aparte. En los dos casos hay alguien que lo ve, que es lo que el dueno exigio.
    /// </remarks>
    [TestMethod]
    public void LoDevueltoSinCompletarSigueVisibleEnLaSegundaVuelta()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var documento = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        banco.Dar(sandy, documento);
        banco.IdaYVuelta(sandy, "No", documento);

        var gerente = banco.Companeros.Obtener(
            banco.Companeros.Guardar(new Companero
            {
                Nombre = "Gerente de prueba",
                Activo = true,
                Rol = RolDeCompanero.Administrador,
                Categoria = 2,
                CreadoEn = banco.Reloj.Ahora(),
            }).Id)!;

        var sube = SegundaVuelta.Para(banco.Casos, banco.Asignaciones, banco.Companeros, gerente);

        Assert.AreEqual(
            1,
            sube.NoCompletosMirados,
            "El documento que volvio sin completar tiene que salir contado en la escalera.");
    }

    /// <summary>La linea que ve el dueno dice, con numeros, lo que NO va y por que.</summary>
    [TestMethod]
    public void LaLineaDiceCuantosSeQuedanFueraPorEstarYaHechos()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var hecho = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        var pendiente = UnDocumentoConGente(banco, "2026-10-22", "BBBB0002");

        banco.Dar(sandy, hecho);
        banco.IdaYVuelta(sandy, "Sí", hecho);
        banco.Dar(sandy, pendiente);

        var carga = banco.Ida.Carga(sandy.Id);

        // La larga es la que se pinta al lado del desplegable, que es donde el dueno mira antes
        // de generar. La corta viaja dentro del acuse del pie, que tiene tope de 160 caracteres.
        StringAssert.Contains(carga.LineaConLoQueNoVa, "1 caso");
        StringAssert.Contains(carga.LineaConLoQueNoVa, "1 ya devuelto completo");
        Assert.AreEqual("1 caso · 2 personas", carga.Linea);
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>Un documento de esa fecha con dos personas dentro, y devuelve su id.</summary>
    private static long UnDocumentoConGente(BaseDelPaquete banco, string fechaViaje, string numero)
    {
        var casoId = banco.Caso(numero, fechaViaje);
        banco.Persona(casoId, "Persona de prueba A", "100" + numero[4..], 1);
        banco.Persona(casoId, "Persona de prueba B", "200" + numero[4..], 2);
        return casoId;
    }

    /// <summary>Dos documentos de la misma fecha: el primero con dos personas y el segundo con una.</summary>
    private static IReadOnlyList<long> DosDocumentosConGente(
        BaseDelPaquete banco, string fechaViaje, string primero, string segundo)
    {
        var uno = UnDocumentoConGente(banco, fechaViaje, primero);
        var dos = banco.Caso(segundo, fechaViaje);
        banco.Persona(dos, "Persona de prueba C", "300" + segundo[4..], 1);
        return [uno, dos];
    }
}
