using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Paquetes;

/// <summary>
/// El boton que le quita de golpe TODOS los casos a un companero.
/// </summary>
/// <remarks>
/// <para><b>Lo que dijo el dueno el 2026-09-07:</b> <i>«Y no hay un botón para quitarle
/// todos los casos asignados a una persona.»</i> Hasta hoy habia que abrir caso por caso.</para>
///
/// <para>⚠️ <b>Quitar una asignacion no borra nada del caso.</b> Ni su estado, ni el motivo
/// que escribio el agente, ni la firma de Miguel, ni sus personas. El caso vuelve a estar sin
/// asignar y ya. Hay una prueba por cada una de esas cuatro cosas porque es lo unico que
/// separa «quitarle el trabajo a alguien» de «perder el trabajo».</para>
///
/// <para>Y la asignacion se DESACTIVA, no se borra: es lo que conserva quien llevo que caso,
/// que es la evidencia de trabajo individual que este programa existe para no perder.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQuitarleTodosLosCasos
{
    /// <summary>Antes de tocar nada dice cuantos va a quitar, con el numero delante.</summary>
    [TestMethod]
    public void DiceCuantosVaAQuitarAntesDeQuitarNada()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        banco.Dar(sandy, TresDocumentos(banco));

        var loQueLleva = banco.Reparto.MirarLoQueSeLeQuitaria(sandy.Id, sandy.Nombre);

        Assert.AreEqual(3, loQueLleva.CasosQueLleva);
        StringAssert.Contains(loQueLleva.Pregunta, "3 casos");
        StringAssert.Contains(loQueLleva.Pregunta, sandy.Nombre);
        Assert.AreEqual(3, banco.VivasDe(sandy.Id), "Mirar no quita nada.");
    }

    /// <summary>Si no se sigue adelante, en la base sigue teniendo exactamente lo mismo.</summary>
    [TestMethod]
    public void SiNoSeSigueAdelanteLosCasosSiguenSuyosEnLaBase()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var documentos = TresDocumentos(banco);
        banco.Dar(sandy, documentos);

        // Cancelar es NO llamar a la segunda mitad. Se comprueba en la base, que es lo unico
        // que cuenta: una pantalla puede pintar cualquier cosa.
        _ = banco.Reparto.MirarLoQueSeLeQuitaria(sandy.Id, sandy.Nombre);

        Assert.AreEqual(3, banco.VivasDe(sandy.Id));
        Assert.HasCount(3, banco.Ida.Carga(sandy.Id).CasoIds);
    }

    /// <summary>Al hacerlo de verdad se queda en cero, y lo dice con el numero.</summary>
    [TestMethod]
    public void AlQuitarlosDeVerdadSeQuedaEnCero()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        banco.Dar(sandy, TresDocumentos(banco));

        var resumen = banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);

        Assert.AreEqual(3, resumen.Retirados);
        Assert.AreEqual(0, resumen.NoSePudieron);
        Assert.AreEqual(0, banco.VivasDe(sandy.Id));
        Assert.IsEmpty(banco.Ida.Carga(sandy.Id).CasoIds);
        StringAssert.Contains(resumen.Linea, "3 casos");
    }

    /// <summary>⛔ Los casos siguen enteros: su estado, el motivo del agente y sus personas.</summary>
    [TestMethod]
    public void LosCasosSiguenEnterosConSuEstadoYSuMotivo()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var documento = banco.Caso("AAAA0001", "2026-09-17");
        banco.Persona(documento, "Persona de prueba A", "1000001", 1);
        banco.Dar(sandy, documento);
        banco.IdaYVuelta(sandy, "No", documento);

        var antes = banco.Casos.Obtener(documento)!;
        banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);
        var despues = banco.Casos.Obtener(documento)!;

        Assert.AreEqual(antes.EstadoRecomendacion, despues.EstadoRecomendacion);
        Assert.AreEqual(antes.EstadoDelCompanero, despues.EstadoDelCompanero);
        Assert.AreEqual(antes.EstadoDelCompaneroPor, despues.EstadoDelCompaneroPor);
        Assert.AreEqual(antes.MotivoDelCompanero, despues.MotivoDelCompanero);
        Assert.HasCount(1, banco.Personas.DeCaso(documento), "Las personas del documento no se tocan.");
    }

    /// <summary>⛔ La firma de Miguel sobre los campos del caso sigue donde estaba.</summary>
    [TestMethod]
    public void LaFirmaDeLosCamposNoSeToca()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var miguel = banco.Alta("Miguel de prueba", RolDeCompanero.Administrador);
        var documento = banco.Caso("AAAA0001", "2026-09-17");
        banco.Persona(documento, "Persona de prueba A", "1000001", 1);
        banco.Dar(sandy, documento);
        FirmarElNumeroDeCaso(banco, documento, miguel.Id);

        var firmadosAntes = banco.Firmados(TablaDeProcedencia.Casos, documento);
        banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);

        Assert.AreEqual(1, firmadosAntes, "El montaje de la prueba tenia que dejar un campo firmado.");
        Assert.AreEqual(firmadosAntes, banco.Firmados(TablaDeProcedencia.Casos, documento));
    }

    /// <summary>La asignacion se desactiva, nunca se borra: quien llevo que se sigue sabiendo.</summary>
    [TestMethod]
    public void LasAsignacionesSeDesactivanYNoSeBorran()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        banco.Dar(sandy, TresDocumentos(banco));

        banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);
        var todas = banco.TodasLasDe(sandy.Id);

        Assert.HasCount(3, todas, "Las tres filas siguen en la base.");
        Assert.IsTrue(todas.All(una => !una.Activa));
        Assert.IsTrue(
            todas.All(una => !string.IsNullOrWhiteSpace(una.DesactivadaEn)),
            "Cada retirada lleva su fecha; sin ella no se sabe cuando se le quito.");
    }

    /// <summary>A quien no lleva nada se le dice, y no se escribe nada en la base.</summary>
    [TestMethod]
    public void AQuienNoLlevaNadaSeLeDiceYNoSeTocaLaBase()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");

        var loQueLleva = banco.Reparto.MirarLoQueSeLeQuitaria(sandy.Id, sandy.Nombre);
        var resumen = banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);

        Assert.AreEqual(0, loQueLleva.CasosQueLleva);
        Assert.IsFalse(loQueLleva.HayAlgoQueQuitar);
        Assert.AreEqual(0, resumen.Retirados);
        Assert.IsEmpty(banco.TodasLasDe(sandy.Id));
    }

    /// <summary>Solo le quita lo suyo: lo de los demas se queda donde estaba.</summary>
    [TestMethod]
    public void NoLeQuitaNadaAlOtroCompanero()
    {
        using var banco = new BaseDelPaquete();
        var sandy = banco.Alta("Agente de prueba uno");
        var otro = banco.Alta("Agente de prueba dos");
        var documentos = TresDocumentos(banco);
        banco.Dar(sandy, documentos[0], documentos[1]);
        banco.Dar(otro, documentos[2]);

        banco.Reparto.QuitarleTodo(sandy.Id, sandy.Nombre);

        Assert.AreEqual(0, banco.VivasDe(sandy.Id));
        Assert.AreEqual(1, banco.VivasDe(otro.Id));
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>Tres documentos con una persona cada uno, y devuelve sus ids.</summary>
    /// <param name="banco">El banco de la prueba.</param>
    private static long[] TresDocumentos(BaseDelPaquete banco)
    {
        var ids = new long[3];
        for (var numero = 0; numero < ids.Length; numero++)
        {
            ids[numero] = banco.Caso($"AAAA000{numero + 1}", "2026-09-17");
            banco.Persona(ids[numero], $"Persona de prueba {numero + 1}", $"100000{numero + 1}", 1);
        }

        return ids;
    }

    /// <summary>Firma el numero de caso por el unico camino que hay: la procedencia.</summary>
    /// <param name="banco">El banco de la prueba.</param>
    /// <param name="casoId">El documento cuyo número se firma.</param>
    /// <param name="quienFirma">El id del compañero que firma.</param>
    private static void FirmarElNumeroDeCaso(BaseDelPaquete banco, long casoId, long quienFirma)
    {
        banco.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = nameof(Caso.NumeroCaso),
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.9,
        });
        banco.Procedencia.Firmar(
            TablaDeProcedencia.Casos, casoId, nameof(Caso.NumeroCaso), quienFirma, banco.Reloj.Ahora());
    }
}
