using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Conexion;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que borrar borra de verdad, que no deja nada colgando y que NUNCA borra sin copia.
/// </summary>
/// <remarks>
/// <para>
/// El criterio viene del pase del dueno del 2026-09-05, no del codigo: «necesito un boton
/// para dejar tambien todo en limpio y eliminar todo». Cada prueba de aqui es uno de sus
/// puntos escrito en Dado/Cuando/Entonces.
/// </para>
/// <para>
/// ⛔ La regla que estas pruebas guardan por encima de todas: <b>si no hubo copia, no se
/// borra</b>. Es lo unico de este pase que no admite un «avisar y seguir».
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDelBorrado
{
    // ==================================================================
    // Punto 1 del pase: un documento suelto, sin dejar nada colgando.
    // ==================================================================

    [TestMethod]
    public void BorrarUnDocumentoSeLlevaSuGenteYNoDejaNadaColgando()
    {
        // Dado un documento con personas, procedencia, asignacion, contacto e ilegible,
        // y OTRO documento al lado que no se toca.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var elQueSeVa = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 3);
        var elQueSeQueda = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "BALC2609", sandy.Id, 2);

        Assert.AreEqual(2, baseDePrueba.ContarFilasDe("casos"), "La siembra no dejo dos casos.");
        Assert.AreEqual(5, baseDePrueba.ContarFilasDe("personas"), "La siembra no dejo cinco personas.");

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        // Cuando se planea y se borra ese documento.
        var plan = mantenimiento.PlanearDocumentos([elQueSeVa]);
        Assert.IsTrue(plan.SePuedeBorrar, "El plan no dio permiso: " + Motivos(plan.Avisos));
        Assert.AreEqual(1, plan.Documentos, "El plan no conto un documento.");
        Assert.AreEqual(3, plan.Personas, "El plan no conto las tres personas del documento.");

        var resultado = mantenimiento.Borrar(plan);

        // Entonces se fue el, se fue lo suyo, y el otro sigue entero.
        Assert.IsTrue(resultado.SeBorro, "No se borro: " + Motivos(resultado.Avisos));
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "No quedo un solo caso.");
        Assert.AreEqual(2, baseDePrueba.ContarFilasDe("personas"), "Se llevo personas de mas o de menos.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("asignaciones"), "Quedo una asignacion de mas.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("contactos"), "Quedo un contacto de mas.");
        Assert.AreEqual(
            1, baseDePrueba.ContarFilasDe("documentos_ilegibles"), "Quedo un ilegible de mas.");

        Assert.AreEqual(
            0,
            SiembraParaBorrar.ProcedenciaQueCuelgaDe(baseDePrueba, elQueSeVa),
            "Quedo procedencia apuntando a un caso que ya no existe.");
        Assert.AreEqual(
            3,
            SiembraParaBorrar.ProcedenciaQueCuelgaDe(baseDePrueba, elQueSeQueda),
            "Se llevo por delante la procedencia del documento que NO se borraba.");

        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    [TestMethod]
    public void UnDocumentoQueOtroSenalaComoDuplicadoSePuedeBorrar()
    {
        // Es el caso que se rompe con los siete PDF de verdad del dueno: repiten entre
        // ellos, y `casos.duplicado_de` apunta a `casos` con RESTRICT. Sin soltar esa
        // punta, borrar el original falla en el motor.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var original = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 1);
        var repetido = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 1);
        SiembraParaBorrar.MarcarComoDuplicado(baseDePrueba, repetido, original);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var resultado = mantenimiento.Borrar(mantenimiento.PlanearDocumentos([original]));

        Assert.IsTrue(resultado.SeBorro, "No se borro el original: " + Motivos(resultado.Avisos));
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "No quedo el repetido.");
        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "SELECT duplicado_de FROM casos WHERE id = $id";
        orden.Parameters.AddWithValue("$id", repetido);
        Assert.AreEqual(
            DBNull.Value,
            orden.ExecuteScalar(),
            "El repetido siguio apuntando a un caso que ya no existe.");
    }

    // ==================================================================
    // Punto 2 del pase: varios de golpe, la misma seleccion de «archivar».
    // ==================================================================

    [TestMethod]
    public void BorrarVariosDeGolpeSeLlevaSoloLosMarcados()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var uno = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);
        var dos = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "BALC2609", sandy.Id, 2);
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "DOMC2609", sandy.Id, 2);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearDocumentos([uno, dos]);

        Assert.AreEqual(2, plan.Documentos, "El plan no conto los dos marcados.");
        Assert.AreEqual(4, plan.Personas, "El plan no conto las cuatro personas de los dos.");

        var resultado = mantenimiento.Borrar(plan);

        Assert.IsTrue(resultado.SeBorro, "No se borro: " + Motivos(resultado.Avisos));
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "No quedo el tercero solo.");
        Assert.AreEqual(2, baseDePrueba.ContarFilasDe("personas"), "No quedaron sus dos personas.");
        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    // ==================================================================
    // Punto 3 del pase: «dejar todo en limpio», con los companeros dentro.
    // ==================================================================

    [TestMethod]
    public void EmpezarDeCeroVaciaLosDocumentosYDejaLosCompaneros()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        companeros.Guardar(new Companero { Nombre = "Miguel" });

        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "BALC2609", sandy.Id, 3);
        SembrarUnaFilaDescartada(baseDePrueba, sandy.Id);
        SembrarUnIlegibleSinCaso(baseDePrueba);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearEmpezarDeCero();

        Assert.AreEqual(2, plan.Documentos, "El plan no conto los dos documentos.");
        Assert.AreEqual(5, plan.Personas, "El plan no conto las cinco personas.");

        var resultado = mantenimiento.Borrar(plan);
        Assert.IsTrue(resultado.SeBorro, "No se borro: " + Motivos(resultado.Avisos));

        foreach (var tabla in new[]
                 {
                     "casos", "personas", "asignaciones", "contactos", "procedencia_campo",
                     "documentos_ilegibles", "filas_descartadas",
                 })
        {
            Assert.AreEqual(0, baseDePrueba.ContarFilasDe(tabla), $"«{tabla}» no quedo vacia.");
        }

        Assert.AreEqual(
            2,
            baseDePrueba.ContarFilasDe("companeros"),
            "Se llevo por delante a los companeros, que es justo lo que NO se borra.");
        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    // ==================================================================
    // Punto 4 del pase: la copia previa, y que se pueda volver a abrir.
    // ==================================================================

    [TestMethod]
    public void AntesDeBorrarQuedaUnaCopiaConLaFechaEnElNombreQueSeVuelveAAbrir()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 4);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearEmpezarDeCero();

        Assert.IsNotNull(plan.RutaDeLaCopia, "No se hizo copia previa.");
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia), $"La copia «{plan.RutaDeLaCopia}» no existe.");
        StringAssert.Contains(
            Path.GetFileName(plan.RutaDeLaCopia),
            RespaldoAntesDeBorrar.MarcaDeLaCopia,
            "El nombre de la copia no dice que es de antes de borrar.");
        StringAssert.Matches(
            Path.GetFileName(plan.RutaDeLaCopia),
            new System.Text.RegularExpressions.Regex(@"\d{8}-\d{6}"),
            "El nombre de la copia no lleva la fecha dentro.");

        mantenimiento.Borrar(plan);
        Assert.AreEqual(0, baseDePrueba.ContarFilasDe("casos"), "La base no quedo vacia.");

        // Y lo que de verdad importa: la copia se abre con el programa y trae los datos.
        using var copia = FabricaDeConexiones.Abrir(plan.RutaDeLaCopia);
        using var orden = copia.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM casos";
        Assert.AreEqual(
            1L,
            Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture),
            "La copia no traia el documento que se borro.");

        orden.CommandText = "SELECT COUNT(*) FROM personas";
        Assert.AreEqual(
            4L,
            Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture),
            "La copia no traia las personas del documento que se borro.");
    }

    [TestMethod]
    public void LaPreguntaDiceCuantosDocumentosCuantasPersonasYDondeQuedoLaCopia()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 3);
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "BALC2609", sandy.Id, 4);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearEmpezarDeCero();

        // El numero DELANTE, no un «¿seguro?».
        StringAssert.Contains(plan.Titulo, "2 documentos", "El titulo no lleva el numero delante.");
        StringAssert.Contains(plan.TextoDelBoton, "2 documentos", "El boton no lleva el numero delante.");

        StringAssert.Contains(plan.Pregunta, "2 documentos", "La pregunta no dice cuantos documentos.");
        StringAssert.Contains(plan.Pregunta, "7 personas", "La pregunta no dice cuantas personas.");
        StringAssert.Contains(
            plan.Pregunta, plan.RutaDeLaCopia!, "La pregunta no dice donde quedo la copia.");
    }

    [TestMethod]
    public void SiElDuenoDiceQueNoNoSeBorraNada()
    {
        // «Si el dice que no, no se borra nada»: se planea —y por tanto se copia— y
        // simplemente no se llama a Borrar. La base tiene que quedar intacta.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var plan = mantenimiento.PlanearEmpezarDeCero();

        Assert.IsNotNull(plan.RutaDeLaCopia, "Ni siquiera se hizo la copia.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Planear ya se llevo el documento.");
        Assert.AreEqual(2, baseDePrueba.ContarFilasDe("personas"), "Planear ya se llevo las personas.");
    }

    [TestMethod]
    public void UnPlanSinPermisoNoBorraNada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var falsificado = mantenimiento.PlanearEmpezarDeCero() with { SePuedeBorrar = false };

        var resultado = mantenimiento.Borrar(falsificado);

        Assert.IsFalse(resultado.SeBorro, "Se ejecuto un plan que no tenia permiso.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Se borro sin permiso.");
        Assert.IsNotEmpty(resultado.Avisos, "No dijo por que no borro.");
    }

    [TestMethod]
    public void UnPlanSinCopiaNoBorraNunca()
    {
        // La regla que no se negocia: sin copia previa NO se borra, aunque el plan
        // llegara con permiso.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var sinCopia = mantenimiento.PlanearEmpezarDeCero() with { RutaDeLaCopia = null };

        var resultado = mantenimiento.Borrar(sinCopia);

        Assert.IsFalse(resultado.SeBorro, "Borro sin copia previa.");
        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "Borro sin copia previa.");
    }

    // ==================================================================
    // Punto 5 del pase: despues de borrar, una linea con su numero.
    // ==================================================================

    [TestMethod]
    public void DespuesDeBorrarLaLineaLlevaSuNumeroYElRegistroTambien()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 3);

        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var resultado = mantenimiento.Borrar(mantenimiento.PlanearEmpezarDeCero());

        StringAssert.Contains(resultado.Linea, "1 documento", "El acuse no dice cuantos documentos.");
        StringAssert.Contains(resultado.Linea, "3 personas", "El acuse no dice cuantas personas.");

        StringAssert.Contains(resultado.LineaDelRegistro, "BORRADO", "El registro no dice que borro.");
        StringAssert.Contains(resultado.LineaDelRegistro, "casos=1", "El registro no lleva las cifras.");
        StringAssert.Contains(
            resultado.LineaDelRegistro,
            resultado.RutaDeLaCopia!,
            "El registro no dice donde quedo la copia.");
    }

    [TestMethod]
    public void BorrarNadaNoPideNadaYNoTocaLaBase()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var plan = mantenimiento.PlanearDocumentos([]);

        Assert.IsTrue(plan.NoHayNadaQueBorrar, "Un plan sin documentos dice que hay algo que borrar.");
        Assert.IsFalse(plan.SePuedeBorrar, "Un plan sin documentos pide permiso para nada.");
        Assert.IsNull(plan.RutaDeLaCopia, "Copio la base para no borrar nada.");
    }

    private static string Motivos(IReadOnlyList<Aviso> avisos)
        => avisos.Count == 0 ? "(sin motivo)" : string.Join(" | ", avisos.Select(a => a.Linea));

    private static void SembrarUnaFilaDescartada(BaseDePrueba baseDePrueba, long companeroId)
    {
        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO filas_descartadas (companero_id, ruta_excel, fila_excel, motivo, registrado_en) " +
            "VALUES ($companero, $ruta, $fila, $motivo, $registrado)";
        orden.Parameters.AddWithValue("$companero", companeroId);
        orden.Parameters.AddWithValue("$ruta", @"C:\devuelto\sandy.xlsx");
        orden.Parameters.AddWithValue("$fila", 7);
        orden.Parameters.AddWithValue("$motivo", "la fila 7 no casa con ningun caso");
        orden.Parameters.AddWithValue("$registrado", "2026-09-05 10:00:00");
        orden.ExecuteNonQuery();
    }

    private static void SembrarUnIlegibleSinCaso(BaseDePrueba baseDePrueba)
    {
        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO documentos_ilegibles (ruta_pdf, pagina_pdf, motivo, registrado_en) " +
            "VALUES ($ruta, $pagina, $motivo, $registrado)";
        orden.Parameters.AddWithValue("$ruta", @"C:\escaneos\nada.pdf");
        orden.Parameters.AddWithValue("$pagina", 1);
        orden.Parameters.AddWithValue("$motivo", "pdf_no_abre");
        orden.Parameters.AddWithValue("$registrado", "2026-09-05 10:05:00");
        orden.ExecuteNonQuery();
    }
}
