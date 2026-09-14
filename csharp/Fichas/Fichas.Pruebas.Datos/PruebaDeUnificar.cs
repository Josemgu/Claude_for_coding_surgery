using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que unificar pasa al original lo que le falta, retira lo vivo, borra el duplicado y no
/// deja nada colgando; y que sin copia o sin permiso no toca nada.
/// </summary>
/// <remarks>
/// <para>El criterio es el del pase del 2026-09-14, escenario por escenario: original con 2
/// personas y sin fecha, duplicado con 3 (2 mismas cédulas + 1 nueva) y con fecha; tras
/// unificar, original con 3 personas, la fecha puesta, la procedencia de la persona nueva
/// apuntando a la hoja del duplicado, el duplicado fuera, y la copia en la carpeta de datos.</para>
///
/// <para>Se cuenta tabla por tabla antes y después (<c>casos</c>, <c>personas</c>,
/// <c>procedencia_campo</c>, <c>asignaciones</c>), que es lo que el criterio pide, y se le
/// pregunta al motor por referencias rotas con <c>PRAGMA foreign_key_check</c>.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDeUnificar
{
    /// <summary>El día en que corren estas pruebas; la retirada lleva esta fecha.</summary>
    private const string Hoy = "2026-09-14";

    /// <summary>El escenario del pase: original sin fecha con dos personas, duplicado con fecha y tres.</summary>
    /// <param name="baseDePrueba">La base.</param>
    /// <returns>Los ids del original y del duplicado, ya marcado.</returns>
    private static (long Original, long Duplicado) ElEscenarioDelPase(BaseDePrueba baseDePrueba)
    {
        var original = SiembraParaUnificar.UnDocumento(
            baseDePrueba, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 1, null, null,
            ("Ana Prueba", "1234567890001"), ("Beto Prueba", "1234567890002"));
        var duplicado = SiembraParaUnificar.UnDocumento(
            baseDePrueba, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 2, "2026-10-08", "Santo Domingo",
            ("Ana Prueba", "1234567890001"), ("Beto Prueba", "1234567890002"), ("Carla Prueba", "1234567890003"));
        SiembraParaBorrar.MarcarComoDuplicado(baseDePrueba, duplicado, original);
        return (original, duplicado);
    }

    /// <summary>El escenario del pase entero: 3 personas, la fecha, la hoja de la nueva, el duplicado fuera y la copia hecha.</summary>
    [TestMethod]
    public void UnificarPasaLaPersonaNuevaYLaFechaYBorraElDuplicado()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (original, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var antes = Cuentas(baseDePrueba);
        Console.WriteLine("Antes:   " + antes);
        Assert.AreEqual("casos=2 personas=5 procedencia_campo=7 asignaciones=0", antes);

        var plan = mantenimiento.PlanearUnificacion(duplicado, Hoy);
        Assert.IsTrue(plan.SePuedeUnificar, "El plan no dio permiso: " + Motivos(plan.Avisos));
        Assert.AreEqual(original, plan.OriginalId);
        Assert.HasCount(1, plan.PersonasQuePasan, "Solo Carla pasa; Ana y Beto ya estaban por cédula.");
        Assert.AreEqual("Carla Prueba", plan.PersonasQuePasan[0].Nombre);
        Assert.AreEqual(2, plan.PersonasQueYaEstaban);
        CollectionAssert.AreEqual(new[] { "fecha_viaje", "templo_nombre" }, plan.CamposQuePasan.Select(c => c.Columna).ToList());
        Assert.AreEqual("CASP2609_Ana_Prueba.pdf hoja 2", plan.DuplicadoDicho);
        Assert.AreEqual("CASP2609_Ana_Prueba.pdf hoja 1", plan.OriginalDicho);
        Assert.IsNotNull(plan.RutaDeLaCopia);
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia), "La copia previa tiene que estar en el disco antes de preguntar.");
        Assert.AreEqual(Path.GetDirectoryName(baseDePrueba.Ruta), Path.GetDirectoryName(plan.RutaDeLaCopia), "La copia va al lado de la base.");

        var resultado = mantenimiento.Unificar(plan);

        Assert.IsTrue(resultado.SeUnifico, "No se unificó: " + Motivos(resultado.Avisos));
        var despues = Cuentas(baseDePrueba);
        Console.WriteLine("Después: " + despues);
        Assert.AreEqual("casos=1 personas=3 procedencia_campo=4 asignaciones=0", despues);

        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        Assert.IsNull(casos.Obtener(duplicado), "El duplicado tiene que estar fuera.");
        var elOriginal = casos.Obtener(original)!;
        Assert.AreEqual("2026-10-08", elOriginal.FechaViaje, "La fecha del duplicado pasó al original.");
        Assert.AreEqual("Santo Domingo", elOriginal.TemploNombre);
        Assert.IsNull(elOriginal.DuplicadoDe);

        var personas = new RepositorioDePersonas(baseDePrueba.Conexion).DeCaso(original);
        Assert.HasCount(3, personas);
        var carla = personas.Single(p => p.Mrn == "1234567890003");
        Assert.AreEqual(2, carla.PaginaPdf, "La persona nueva sigue diciendo de qué hoja salió: la del duplicado.");

        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);
        var deCarla = procedencia.DeRegistro(TablaDeProcedencia.Personas, carla.Id);
        Assert.HasCount(1, deCarla, "La procedencia de la persona nueva viaja con ella.");
        Assert.AreEqual(OrigenDeCampo.Ocr, deCarla[0].Origen);

        var fecha = procedencia.DeRegistro(TablaDeProcedencia.Casos, original).Single(p => p.Campo == "fecha_viaje");
        Assert.AreEqual(OrigenDeCampo.Ocr, fecha.Origen, "La fila «vacío» del original se retiró y entró la del duplicado, que dice de dónde salió.");

        Assert.AreEqual(1, resultado.PersonasQuePasaron);
        Assert.AreEqual(2, resultado.CamposQuePasaron);
        Assert.AreEqual(plan.RutaDeLaCopia, resultado.RutaDeLaCopia);
        Assert.Contains("1 persona y 2 campos pasaron", resultado.Linea);
        Console.WriteLine(resultado.Linea);

        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    /// <summary>Un campo lleno en el original no se pisa, ni su procedencia.</summary>
    [TestMethod]
    public void UnCampoLlenoEnElOriginalNoSePisa()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var original = SiembraParaUnificar.UnDocumento(
            baseDePrueba, "CASP2609", @"C:\escaneos\a.pdf", 1, "2026-10-08", "Santo Domingo", ("Ana Prueba", "1234567890001"));
        var duplicado = SiembraParaUnificar.UnDocumento(
            baseDePrueba, "CASP2609", @"C:\escaneos\a.pdf", 1, "2026-11-30", "Otro templo", ("Ana Prueba", "1234567890001"));
        SiembraParaBorrar.MarcarComoDuplicado(baseDePrueba, duplicado, original);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var plan = mantenimiento.PlanearUnificacion(duplicado, Hoy);
        Assert.IsEmpty(plan.CamposQuePasan, "Nada que pasar: el original ya tiene los dos.");
        Assert.IsEmpty(plan.PersonasQuePasan);

        var resultado = mantenimiento.Unificar(plan);

        Assert.IsTrue(resultado.SeUnifico, Motivos(resultado.Avisos));
        var elOriginal = new RepositorioDeCasos(baseDePrueba.Conexion).Obtener(original)!;
        Assert.AreEqual("2026-10-08", elOriginal.FechaViaje);
        Assert.AreEqual("Santo Domingo", elOriginal.TemploNombre);
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"));
        Assert.Contains("nada tenía que pasar", resultado.Linea);
    }

    /// <summary>La asignación viva del duplicado queda en el original, retirada con la fecha de hoy.</summary>
    [TestMethod]
    public void LaAsignacionVivaDelDuplicadoQuedaRetiradaConFecha()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (original, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var sandy = new RepositorioDeCompaneros(baseDePrueba.Conexion).Guardar(new Companero { Nombre = "Sandy" });
        var asignaciones = new RepositorioDeAsignaciones(baseDePrueba.Conexion);
        Assert.IsTrue(asignaciones.Asignar(duplicado, sandy.Id, "2026-09-10 09:00:00").SeEscribio);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var plan = mantenimiento.PlanearUnificacion(duplicado, Hoy);
        Assert.AreEqual(1, plan.AsignacionesVivasQueSeRetiran);

        var resultado = mantenimiento.Unificar(plan);

        Assert.IsTrue(resultado.SeUnifico, Motivos(resultado.Avisos));
        Assert.AreEqual(1, resultado.AsignacionesRetiradas);
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("asignaciones"), "La fila no se borra: se conserva para el informe del agente.");
        var todas = asignaciones.Listar(new FiltroDeAsignaciones(SoloActivas: false), Pagina.Primera(10)).Elementos;
        Assert.HasCount(1, todas);
        Assert.AreEqual(original, todas[0].CasoId, "Pasa al original.");
        Assert.IsFalse(todas[0].Activa, "activa = 0.");
        Assert.AreEqual(Hoy, todas[0].DesactivadaEn, "Con la fecha de hoy, como al archivar.");
        Assert.IsEmpty(asignaciones.VivasDeCaso(original), "El original no hereda a nadie vivo: lo asigna Miguel.");
    }

    /// <summary>El contacto del duplicado pasa al original, y un tercero que señalaba al duplicado pasa a señalar al original.</summary>
    [TestMethod]
    public void ElContactoYElTerceroQueSenalabaPasanAlOriginal()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (original, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var sandy = new RepositorioDeCompaneros(baseDePrueba.Conexion).Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.SembrarUnContacto(baseDePrueba, duplicado, sandy.Id);
        var tercero = SiembraParaUnificar.UnDocumento(
            baseDePrueba, "CASP2609", @"C:\escaneos\CASP2609_Ana_Prueba.pdf", 3, null, null, ("Ana Prueba", "1234567890001"));
        SiembraParaBorrar.MarcarComoDuplicado(baseDePrueba, tercero, duplicado);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var resultado = mantenimiento.Unificar(mantenimiento.PlanearUnificacion(duplicado, Hoy));

        Assert.IsTrue(resultado.SeUnifico, Motivos(resultado.Avisos));
        Assert.AreEqual(1, SiembraParaUnificar.Contar(baseDePrueba, "SELECT COUNT(*) FROM contactos WHERE caso_id = $id", original));
        var elTercero = new RepositorioDeCasos(baseDePrueba.Conexion).Obtener(tercero)!;
        Assert.AreEqual(original, elTercero.DuplicadoDe, "El que señalaba al duplicado señala ahora al original, no a nada.");
        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    /// <summary>Un plan sin permiso no escribe nada: las cuentas quedan iguales.</summary>
    [TestMethod]
    public void UnPlanSinPermisoNoTocaNada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (_, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var antes = Cuentas(baseDePrueba);

        var sinPermiso = PlanDeUnificacion.NoSePuede(duplicado, null, Aviso.Problema("Sin copia.", string.Empty));
        var resultado = mantenimiento.Unificar(sinPermiso);

        Assert.IsFalse(resultado.SeUnifico);
        Assert.IsNotEmpty(resultado.Avisos);
        Assert.AreEqual(antes, Cuentas(baseDePrueba));
    }

    /// <summary>Un plan con permiso pero sin ruta de copia tampoco: nada se borra sin copia.</summary>
    [TestMethod]
    public void UnPlanSinCopiaNoTocaNadaAunqueTengaPermiso()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (_, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var antes = Cuentas(baseDePrueba);

        var plan = mantenimiento.PlanearUnificacion(duplicado, Hoy) with { RutaDeLaCopia = null };
        var resultado = mantenimiento.Unificar(plan);

        Assert.IsFalse(resultado.SeUnifico);
        Assert.Contains("copia", Motivos(resultado.Avisos));
        Assert.AreEqual(antes, Cuentas(baseDePrueba));
    }

    /// <summary>Un documento que no está marcado como duplicado no se puede unificar.</summary>
    [TestMethod]
    public void UnDocumentoQueNoEsDuplicadoNoSePuedeUnificar()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var solo = SiembraParaUnificar.UnDocumento(baseDePrueba, "CASP2609", @"C:\escaneos\a.pdf", 1, null, null, ("Ana Prueba", "1234567890001"));
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var plan = mantenimiento.PlanearUnificacion(solo, Hoy);

        Assert.IsFalse(plan.SePuedeUnificar);
        Assert.IsNull(plan.RutaDeLaCopia, "Sin nada que unificar no se copia la base.");
        Assert.Contains("no está marcado", Motivos(plan.Avisos));
    }

    /// <summary>Con el original fuera de la base, no se puede unificar y el aviso dice que se quite la marca; quitarla deja <c>duplicado_de</c> a NULL.</summary>
    [TestMethod]
    public void ConElOriginalFueraSeQuitaLaMarca()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var huerfano = SiembraParaUnificar.UnDocumento(baseDePrueba, "CASP2609", @"C:\escaneos\a.pdf", 1, null, null, ("Ana Prueba", "1234567890001"));
        SiembraParaUnificar.SenalarAUnOriginalQueNoEsta(baseDePrueba, huerfano, 9999);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        Assert.AreEqual(9999, casos.Obtener(huerfano)!.DuplicadoDe, "La siembra no dejó al caso señalando a un id que no está.");

        var plan = mantenimiento.PlanearUnificacion(huerfano, Hoy);
        Assert.IsFalse(plan.SePuedeUnificar);
        Assert.Contains("ya no está", Motivos(plan.Avisos));

        var quitada = mantenimiento.QuitarLaMarcaDeDuplicado(huerfano);

        Assert.IsTrue(quitada.SeEscribio, Motivos(quitada.Avisos));
        Assert.IsNull(casos.Obtener(huerfano)!.DuplicadoDe);
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Quitar la marca no borra nada.");
    }

    /// <summary>Quitar la marca a un id que no está no escribe y lo dice.</summary>
    [TestMethod]
    public void QuitarLaMarcaAUnIdQueNoEstaNoEscribe()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var resultado = mantenimiento.QuitarLaMarcaDeDuplicado(4242);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsNotEmpty(resultado.Avisos);
    }

    /// <summary>Si el duplicado se borró entre la pregunta y el «sí», no se unifica y se dice.</summary>
    [TestMethod]
    public void SiElDuplicadoYaNoEstaAlEjecutarNoSeUnifica()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var (_, duplicado) = ElEscenarioDelPase(baseDePrueba);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearUnificacion(duplicado, Hoy);
        Assert.IsTrue(mantenimiento.Borrar(mantenimiento.PlanearDocumentos([duplicado])).SeBorro);
        var antes = Cuentas(baseDePrueba);

        var resultado = mantenimiento.Unificar(plan);

        Assert.IsFalse(resultado.SeUnifico);
        Assert.Contains("ya no está", Motivos(resultado.Avisos));
        Assert.AreEqual(antes, Cuentas(baseDePrueba));
    }

    /// <summary>Las cuentas de las cuatro tablas del criterio, en una línea comparable.</summary>
    /// <param name="baseDePrueba">La base.</param>
    private static string Cuentas(BaseDePrueba baseDePrueba)
        => $"casos={baseDePrueba.ContarFilasDe("casos")} personas={baseDePrueba.ContarFilasDe("personas")} "
         + $"procedencia_campo={baseDePrueba.ContarFilasDe("procedencia_campo")} asignaciones={baseDePrueba.ContarFilasDe("asignaciones")}";

    /// <summary>Los avisos en una línea, para el mensaje de la aserción.</summary>
    /// <param name="avisos">Los avisos que devolvió el puerto.</param>
    private static string Motivos(IReadOnlyList<Aviso> avisos)
        => string.Join(" | ", avisos.Select(a => a.Linea + (a.Detalle is null ? string.Empty : " — " + a.Detalle)));
}
