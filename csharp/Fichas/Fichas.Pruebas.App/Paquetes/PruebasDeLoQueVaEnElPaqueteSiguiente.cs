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

        // ⚠️ `YaLosDevolvioCompletos` sale VACIA desde el 2026-09-07, y no es que se haya
        // perdido: es que esos documentos ya no estan asignados a el. La lista solo cuenta lo que
        // lleva vivo, y devolverlos completos se los quito. Lo que el dueno pidio —que no vuelvan
        // a ir— se cumple por un camino mas corto, y se comprueba en la base.
        Assert.IsEmpty(carga.YaLosDevolvioCompletos, "ya no los lleva: la vuelta se los quitó");
        CollectionAssert.AreEquivalent(deOctubre.ToList(), banco.CasosVivosDe(sandy.Id).ToList());
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
    /// ⚠️ Devolverlo completo SI le retira la asignacion, y la fila retirada se conserva.
    /// </summary>
    /// <remarks>
    /// <para><b>Esta prueba afirmaba lo contrario hasta el 2026-09-07</b> —«quitarlo del paquete
    /// NO retira su asignacion»— con este motivo: que retirar al devolver borraria del informe
    /// del agente el trabajo que acababa de hacer. <b>El motivo era falso</b>: el informe pide
    /// las asignaciones con <c>SoloActivas: false</c>. Y el dueno pidio justo lo otro: <i>«debe
    /// quitarle que ese caso esta asignado a el. Debe quedar limpio»</i>. Manda el.</para>
    ///
    /// <para>Lo que sigue igual, y por eso se comprueba aqui tambien: la fila NO se borra, se
    /// desactiva. «Quedar limpio» no es «perder de quien era». El detalle esta en
    /// <see cref="PruebasDeQueElPaqueteCompletoLimpiaLaAsignacion"/>.</para>
    /// </remarks>
    [TestMethod]
    public void DevolverloCompletoLeQuitaLaAsignacionSinBorrarLaFila()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var documento = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        banco.Dar(sandy, documento);
        banco.IdaYVuelta(sandy, "Sí", documento);

        Assert.AreEqual(0, banco.VivasDe(sandy.Id), "lo devolvio completo: tiene que quedarle limpio");
        Assert.HasCount(1, banco.TodasLasDe(sandy.Id), "la fila se desactiva, no se borra: quien lo hizo se sigue viendo");
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

    /// <summary>
    /// La linea que ve el dueno dice, con numeros, lo que NO va y por que — cuando queda algo
    /// que decir.
    /// </summary>
    /// <remarks>
    /// <para><b>El montaje cambio el 2026-09-07 y el motivo importa.</b> Antes bastaba con que
    /// el agente devolviera un documento completo para que la linea dijera «1 ya devuelto
    /// completo que no vuelve a ir». Ahora devolverlo completo le QUITA la asignacion, asi que
    /// ese documento ya no le cuenta y no hay nada que decir de el.</para>
    ///
    /// <para>La linea no ha muerto: sigue haciendo falta el dia que Miguel <b>le vuelva a
    /// asignar a mano</b> algo que ese agente ya completo. Es lo que monta esta prueba, y es el
    /// unico camino que queda para llegar a ella.</para>
    /// </remarks>
    [TestMethod]
    public void SiSeLeVuelveAAsignarLoQueYaCompletoLaLineaLoDiceYNoSeLeManda()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var hecho = UnDocumentoConGente(banco, "2026-09-17", "AAAA0001");
        var pendiente = UnDocumentoConGente(banco, "2026-10-22", "BBBB0002");

        banco.Dar(sandy, hecho);
        banco.IdaYVuelta(sandy, "Sí", hecho);
        Assert.AreEqual(0, banco.VivasDe(sandy.Id), "el montaje: la vuelta ya se lo quitó");

        banco.Dar(sandy, hecho);
        banco.Dar(sandy, pendiente);

        var carga = banco.Ida.Carga(sandy.Id);

        // La larga es la que se pinta al lado del desplegable, que es donde el dueno mira antes
        // de generar. La corta viaja dentro del acuse del pie, que tiene tope de 160 caracteres.
        StringAssert.Contains(carga.LineaConLoQueNoVa, "1 caso");
        StringAssert.Contains(carga.LineaConLoQueNoVa, "1 ya devuelto completo");
        Assert.AreEqual("1 caso · 2 personas", carga.Linea);
        CollectionAssert.AreEquivalent(new List<long> { pendiente }, carga.CasoIds.ToList());
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>Un documento de esa fecha con dos personas dentro, y devuelve su id.</summary>
    /// <param name="banco">El banco de la prueba.</param>
    /// <param name="fechaViaje">La fecha de viaje del documento.</param>
    /// <param name="numero">El número de caso; sus cuatro cifras hacen distintas a las cédulas.</param>
    private static long UnDocumentoConGente(BaseDelPaquete banco, string fechaViaje, string numero)
    {
        var casoId = banco.Caso(numero, fechaViaje);
        banco.Persona(casoId, "Persona de prueba A", "100" + numero[4..], 1);
        banco.Persona(casoId, "Persona de prueba B", "200" + numero[4..], 2);
        return casoId;
    }

    /// <summary>Dos documentos de la misma fecha: el primero con dos personas y el segundo con una.</summary>
    /// <param name="banco">El banco de la prueba.</param>
    /// <param name="fechaViaje">La fecha de viaje de los dos.</param>
    /// <param name="primero">El número de caso del primero.</param>
    /// <param name="segundo">El número de caso del segundo.</param>
    private static IReadOnlyList<long> DosDocumentosConGente(
        BaseDelPaquete banco, string fechaViaje, string primero, string segundo)
    {
        var uno = UnDocumentoConGente(banco, fechaViaje, primero);
        var dos = banco.Caso(segundo, fechaViaje);
        banco.Persona(dos, "Persona de prueba C", "300" + segundo[4..], 1);
        return [uno, dos];
    }
}
