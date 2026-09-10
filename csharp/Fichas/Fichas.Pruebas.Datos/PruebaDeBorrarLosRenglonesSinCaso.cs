using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Borrar el renglon de un PDF que no se pudo leer y no dejo ningun documento.
/// </summary>
/// <remarks>
/// <para>Del dueno, el 2026-09-07 y dicho dos veces el mismo dia: <i>«no se contempla
/// eliminar PDF o documentos que no tienen informacion; debe poder eliminarlo»</i>. La
/// mitad de los casos sin persona la cerro <c>IMantenimiento.PlanearDocumentos</c>; esta es
/// la otra mitad, la del PDF que no dejo NADA detras y por eso ningun borrado alcanzaba.</para>
///
/// <para>⛔ Estas pruebas corren contra SQLite de verdad y cuentan preguntandole AL MOTOR,
/// nunca al programa: lo que hay que demostrar es que la fila se fue o que sigue ahi, no
/// que un objeto diga que se fue.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDeBorrarLosRenglonesSinCaso
{
    // ══════════════════ Planear: la copia y la cifra, antes de preguntar ══════════════════

    /// <summary>
    /// Dados dos renglones sin documento, cuando se pide el plan, entonces dice cuantos
    /// caen, deja la copia hecha y NO ha tocado todavia la base.
    /// </summary>
    [TestMethod]
    public void PlanearDiceCuantosCaenYDejaLaCopiaHechaSinBorrarNada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        SembrarRenglon(ilegibles, @"C:\escaneos\roto1.pdf", casoId: null);
        SembrarRenglon(ilegibles, @"C:\escaneos\roto2.pdf", casoId: null);
        var todos = IdsDeLosRenglones(ilegibles);

        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso(todos);

        Assert.IsTrue(plan.SePuedeBorrar, "El plan no dio permiso.");
        Assert.AreEqual(2, plan.Cuantas(IIlegibles.TablaDeLosRenglones), "La cifra de la pregunta no es 2.");
        Assert.IsNotNull(plan.RutaDeLaCopia, "No se hizo copia previa: sin copia no se borra.");
        Assert.Contains(RespaldoAntesDeBorrar.MarcaDeLaCopia, plan.RutaDeLaCopia);
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia), "La copia anunciada no esta donde se dijo.");
        Assert.Contains(plan.RutaDeLaCopia, plan.Pregunta, "La pregunta no dice donde quedo la copia.");
        Assert.AreEqual(2L, baseDePrueba.ContarFilasDe("documentos_ilegibles"), "Planear borro algo, y planear no borra.");
    }

    /// <summary>
    /// Dado que se cancela —que es NO llamar a Borrar—, entonces los renglones siguen en la
    /// base y la copia sigue donde se anuncio.
    /// </summary>
    [TestMethod]
    public void SiSeCancelaLosRenglonesSiguenEnLaBase()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        SembrarRenglon(ilegibles, @"C:\escaneos\roto1.pdf", casoId: null);

        var antes = baseDePrueba.ContarFilasDe("documentos_ilegibles");
        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones(ilegibles));

        Assert.AreEqual(antes, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia!), "La copia anunciada al cancelar no esta.");
    }

    /// <summary>Dada una lista vacia, entonces no hay plan y no hay nada que preguntar.</summary>
    [TestMethod]
    public void SinRenglonesMarcadosNoHayPlan()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);

        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso([]);

        Assert.IsFalse(plan.SePuedeBorrar);
        Assert.IsNull(plan.RutaDeLaCopia, "Se copio la base sin que hubiera nada que borrar.");
    }

    // ══════════════════ Borrar: se fue, y solo se fue lo marcado ══════════════════

    /// <summary>
    /// Dado un plan con permiso, cuando se ejecuta, entonces el renglon marcado se fue y el
    /// que no se marco sigue.
    /// </summary>
    [TestMethod]
    public void BorrarSeLlevaSoloLosRenglonesMarcados()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var marcado = SembrarRenglon(ilegibles, @"C:\escaneos\roto1.pdf", casoId: null);
        var intacto = SembrarRenglon(ilegibles, @"C:\escaneos\roto2.pdf", casoId: null);

        var resultado = ilegibles.BorrarRenglonesSinCaso(
            ilegibles.PlanearBorradoDeRenglonesSinCaso([marcado]));

        Assert.IsTrue(resultado.SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
        Assert.HasCount(1, ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(10)).Elementos);
        Assert.AreEqual(
            intacto,
            ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(10)).Elementos[0].Id,
            "Se llevo por delante el renglon que no estaba marcado.");
    }

    /// <summary>
    /// Dado un renglon que SI tiene documento, cuando se marca, entonces NO entra en el plan
    /// y se dice por que.
    /// </summary>
    /// <remarks>
    /// Ese renglon es el motivo por el que su documento esta a medias, y se va con el cuando
    /// el documento se borre. Ofrecerlo suelto dejaria un caso sin su explicacion.
    /// </remarks>
    [TestMethod]
    public void UnRenglonQueTieneDocumentoNoEntraEnElPlan()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var casoId = SembrarUnCaso(baseDePrueba, "CASP2609");
        var conCaso = SembrarRenglon(ilegibles, @"C:\escaneos\CASP2609.pdf", casoId);
        var sinCaso = SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);

        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso([conCaso, sinCaso]);

        Assert.IsTrue(plan.SePuedeBorrar);
        Assert.AreEqual(1, plan.Cuantas(IIlegibles.TablaDeLosRenglones), "El renglon con documento entro en la cifra.");
        Assert.HasCount(1, plan.Ids);
        Assert.AreEqual(sinCaso, plan.Ids[0]);
        Assert.IsNotEmpty(plan.Avisos, "No se dijo por que se dejo fuera el renglon con documento.");

        Assert.IsTrue(ilegibles.BorrarRenglonesSinCaso(plan).SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"), "Se llevo el renglon que tenia documento.");
    }

    /// <summary>Dado que TODOS los marcados tienen documento, entonces no hay nada que borrar.</summary>
    [TestMethod]
    public void SiTodosLosMarcadosTienenDocumentoNoSeBorraNada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var casoId = SembrarUnCaso(baseDePrueba, "CASP2609");
        var conCaso = SembrarRenglon(ilegibles, @"C:\escaneos\CASP2609.pdf", casoId);

        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso([conCaso]);

        Assert.IsFalse(plan.SePuedeBorrar);
        Assert.IsNotEmpty(plan.Avisos);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
    }

    /// <summary>Dado un plan SIN copia previa, entonces no se borra, ni con permiso.</summary>
    [TestMethod]
    public void SinCopiaPreviaNoSeBorraNingunRenglon()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);
        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones(ilegibles))
            with { RutaDeLaCopia = null };

        var resultado = ilegibles.BorrarRenglonesSinCaso(plan);

        Assert.IsFalse(resultado.SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
    }

    /// <summary>Dado un plan sin permiso, entonces no se borra nada.</summary>
    [TestMethod]
    public void UnPlanSinPermisoNoBorraNingunRenglon()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);
        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones(ilegibles))
            with { SePuedeBorrar = false };

        Assert.IsFalse(ilegibles.BorrarRenglonesSinCaso(plan).SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
    }

    /// <summary>
    /// Dado un renglon que gano documento entre la pregunta y el «si», entonces no se borra.
    /// </summary>
    /// <remarks>
    /// Se vuelve a comprobar antes de borrar por lo mismo que
    /// <c>RepositorioDeMantenimiento.BorrarAlCompanero</c> vuelve a contar: el «si» del
    /// dueno se dio sobre una foto de hace un momento.
    /// </remarks>
    [TestMethod]
    public void UnRenglonQueGanoDocumentoDespuesDeLaPreguntaNoSeBorra()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var renglon = SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);
        var plan = ilegibles.PlanearBorradoDeRenglonesSinCaso([renglon]);

        var casoId = SembrarUnCaso(baseDePrueba, "CASP2609");
        PonerleDocumentoAlRenglon(baseDePrueba, renglon, casoId);

        var resultado = ilegibles.BorrarRenglonesSinCaso(plan);

        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"), "Se borro un renglon que ya tenia documento.");
        Assert.IsFalse(resultado.SeBorro);
        Assert.IsNotEmpty(resultado.Avisos);
    }

    // ══════════════════ Que un plan no se ejecute por la puerta del otro ══════════════════

    /// <summary>
    /// Dado un plan de RENGLONES, cuando se le pasa al borrado de documentos, entonces se
    /// niega y no se lleva ningun caso.
    /// </summary>
    /// <remarks>
    /// ⛔ Los dos devuelven el mismo <c>PlanDeBorrado</c>, asi que este cruce compila. Sin
    /// esta guarda, un plan de renglones ejecutado por el otro borraria CASOS cuyos ids
    /// coinciden con los de unos renglones: documentos que el dueno no vio en la pregunta.
    /// </remarks>
    [TestMethod]
    public void ElBorradoDeDocumentosRechazaUnPlanDeRenglones()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        SembrarUnCaso(baseDePrueba, "CASP2609");
        var renglon = SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);

        var resultado = mantenimiento.Borrar(ilegibles.PlanearBorradoDeRenglonesSinCaso([renglon]));

        Assert.IsFalse(resultado.SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("casos"), "El plan cruzado se llevo un documento.");
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
    }

    /// <summary>
    /// Dado un plan de DOCUMENTOS, cuando se le pasa al borrado de renglones, entonces se
    /// niega y no se lleva ningun renglon.
    /// </summary>
    [TestMethod]
    public void ElBorradoDeRenglonesRechazaUnPlanDeDocumentos()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var casoId = SembrarUnCaso(baseDePrueba, "CASP2609");
        SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);

        var resultado = ilegibles.BorrarRenglonesSinCaso(mantenimiento.PlanearDocumentos([casoId]));

        Assert.IsFalse(resultado.SeBorro);
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("casos"));
        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("documentos_ilegibles"), "El plan cruzado se llevo un renglon.");
    }

    // ══════════════════ Lo que este borrado NO hace ══════════════════

    /// <summary>
    /// Dado un renglon que apunta a un PDF que existe en el disco, cuando se borra el
    /// renglon, entonces el ARCHIVO sigue donde estaba.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es a proposito y hay que poder demostrarlo: el PDF es del dueno y esta en su
    /// carpeta. Este programa lee de ahi; no es quien para tirar sus escaneos. Quien llama
    /// tiene que decirlo en pantalla, o el dueno creera que borro un archivo que sigue ahi.
    /// </remarks>
    [TestMethod]
    public void BorrarElRenglonNoBorraElPdfDelDisco()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var rutaDelPdf = Path.Combine(Path.GetDirectoryName(baseDePrueba.Ruta)!, "roto.pdf");
        File.WriteAllText(rutaDelPdf, "no es un PDF de verdad, pero es un archivo");
        var renglon = SembrarRenglon(ilegibles, rutaDelPdf, casoId: null);

        Assert.IsTrue(ilegibles.BorrarRenglonesSinCaso(
            ilegibles.PlanearBorradoDeRenglonesSinCaso([renglon])).SeBorro);

        Assert.AreEqual(0L, baseDePrueba.ContarFilasDe("documentos_ilegibles"));
        Assert.IsTrue(File.Exists(rutaDelPdf), "Se borro el PDF del dueno, y eso no lo hace este programa.");
    }

    /// <summary>
    /// Dado un renglon borrado, entonces las filas descartadas del Excel siguen intactas.
    /// </summary>
    /// <remarks>
    /// Las dos listas viven en el mismo puerto y son cosas distintas: una es lo que no se
    /// pudo leer de un PDF y la otra lo que volvio del Excel de un companero.
    /// </remarks>
    [TestMethod]
    public void BorrarUnRenglonNoTocaLasFilasDescartadasDelExcel()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        Assert.IsTrue(sandy.SeEscribio);
        Assert.IsTrue(ilegibles.RegistrarDescartada(new FilaDescartada
        {
            CompaneroId = sandy.Id,
            RutaExcel = @"C:\paquetes\por_verificar.xlsx",
            FilaExcel = 7,
            Motivo = "Fila 7: sin par.",
        }).SeEscribio);
        var renglon = SembrarRenglon(ilegibles, @"C:\escaneos\roto.pdf", casoId: null);

        Assert.IsTrue(ilegibles.BorrarRenglonesSinCaso(
            ilegibles.PlanearBorradoDeRenglonesSinCaso([renglon])).SeBorro);

        Assert.AreEqual(1L, baseDePrueba.ContarFilasDe("filas_descartadas"));
    }

    // ══════════════════ Utilidades de estas pruebas ══════════════════

    private static long SembrarRenglon(IIlegibles ilegibles, string rutaPdf, long? casoId)
    {
        var escrito = ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = rutaPdf,
            Motivo = "no_se_pudo_abrir",
            Detalle = "prueba",
            LineasLeidas = 0,
            CasoId = casoId,
            RegistradoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(escrito.SeEscribio, "No se pudo sembrar el renglon de la prueba.");
        return escrito.Id;
    }

    private static long SembrarUnCaso(BaseDePrueba baseDePrueba, string numeroCaso)
    {
        var caso = new RepositorioDeCasos(baseDePrueba.Conexion).Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            UnidadNumero = "7000011",
            FechaViaje = "2026-10-08",
            RutaPdf = $@"C:\escaneos\{numeroCaso}.pdf",
            CreadoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(caso.SeEscribio, "No se pudo sembrar el caso de la prueba.");
        return caso.Id;
    }

    /// <summary>Le ata un documento al renglon por detras, como lo haria otra ventana.</summary>
    private static void PonerleDocumentoAlRenglon(BaseDePrueba baseDePrueba, long renglonId, long casoId)
    {
        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "UPDATE documentos_ilegibles SET caso_id = $caso WHERE id = $id";
        orden.Parameters.AddWithValue("$caso", casoId);
        orden.Parameters.AddWithValue("$id", renglonId);
        Assert.AreEqual(1, orden.ExecuteNonQuery());
    }

    private static IReadOnlyCollection<long> IdsDeLosRenglones(IIlegibles ilegibles)
        => [.. ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(100)).Elementos.Select(r => r.Id)];
}
