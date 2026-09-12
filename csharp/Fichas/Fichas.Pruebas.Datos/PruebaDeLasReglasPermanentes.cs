using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Las reglas que no se negocian en ninguna fase, comprobadas contra el motor.
/// </summary>
/// <remarks>
/// Son las de CLAUDE.md §1. Aqui van las que esta capa puede sostener: la 5 —nada se
/// marca verificado automaticamente— y la linea base de seguridad de ARQUITECTURA §1.7.
/// </remarks>
[TestClass]
public sealed class PruebaDeLasReglasPermanentes
{
    /// <summary>
    /// Textos que intentan salirse de su casilla y volverse instruccion.
    /// </summary>
    private static readonly string[] IntentosDeInyeccion =
    [
        "'); DROP TABLE casos; --",
        "' OR '1'='1",
        "'; DELETE FROM companeros; --",
        "\"; DROP TABLE personas; --",
        "Robert'); DROP TABLE personas;--",
        "%' OR nombre LIKE '%",
    ];

    /// <summary>Vigila que cada intento de inyección vuelve letra por letra y las tablas siguen ahí.</summary>
    [TestMethod]
    public void UnIntentoDeInyeccionSeGuardaComoDatoInerteYSeReleeLiteral()
    {
        // ARQUITECTURA §1.7 y FASE 1 crit. 3: el motor recibe la logica y los datos POR
        // SEPARADO, asi que el intento llega como dato inerte. Se comprueba de las dos
        // maneras: que el texto vuelve LETRA POR LETRA, y que las tablas siguen ahi.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        foreach (var intento in IntentosDeInyeccion)
        {
            var guardada = personas.Guardar(new Persona { CasoId = caso.Id, Nombre = intento });

            Assert.IsTrue(guardada.SeEscribio, $"No se guardo el texto '{intento}'.");

            var leida = personas.Obtener(guardada.Id);
            Assert.IsNotNull(leida, $"No se pudo releer '{intento}'.");
            Assert.AreEqual(
                intento,
                leida.Nombre,
                "El texto no volvio letra por letra: alguien lo interpreto por el camino.");
        }

        // El control que de verdad importa: las tablas siguen existiendo.
        Assert.AreNotEqual(-1, baseDePrueba.ContarFilasDe("casos"), "La tabla 'casos' desaparecio.");
        Assert.AreNotEqual(-1, baseDePrueba.ContarFilasDe("personas"), "La tabla 'personas' desaparecio.");
        Assert.AreNotEqual(-1, baseDePrueba.ContarFilasDe("companeros"), "La tabla 'companeros' desaparecio.");
        Assert.AreEqual(
            IntentosDeInyeccion.Length,
            baseDePrueba.ContarFilasDe("personas"),
            "No estan las personas que se guardaron.");
    }

    /// <summary>Vigila que un intento de inyección en el filtro de texto no encuentra nada y no se lleva el caso.</summary>
    [TestMethod]
    public void UnIntentoDeInyeccionEnLaBusquedaNoRompeLaConsulta()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        foreach (var intento in IntentosDeInyeccion)
        {
            var encontrados = casos.Listar(new FiltroDeCasos(Texto: intento), Pagina.Primera(10));

            Assert.AreEqual(
                0,
                encontrados.TotalDisponible,
                $"La busqueda de '{intento}' encontro algo, y no deberia casar con nada. " +
                "Si encuentra el caso, es que el texto se volvio parte de la consulta.");
        }

        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "La busqueda se llevo el caso por delante.");
    }

    /// <summary>Vigila que anotar con <c>Verificado = true</c> deja el campo sin firmar y lo avisa (regla permanente 5).</summary>
    [TestMethod]
    public void AnotarLaProcedenciaNuncaMarcaUnCampoComoVerificado()
    {
        // Regla permanente 5. Se le pide a proposito con `Verificado = true`, que es lo
        // que haria una importacion mal escrita, y tiene que salir SIN firmar.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var anotada = procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "numero_caso",
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.91,
            ValorOcr = "BALC2609",
            Verificado = true,
            VerificadoPor = sandy.Id,
            VerificadoEn = "2026-09-04 10:00:00",
        });

        Assert.IsTrue(anotada.SeEscribio, "La procedencia no se anoto.");
        Assert.IsTrue(
            anotada.HayAvisos,
            "Se pidio dar el campo por bueno y no se dijo que eso no se hace asi.");

        Assert.AreEqual(
            0,
            procedencia.ContarVerificados(TablaDeProcedencia.Casos, caso.Id),
            "Anotar la procedencia marco el campo como verificado. Nada se marca " +
            "verificado automaticamente (regla permanente 5).");

        var campos = procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id);
        Assert.HasCount(1, campos, "No se guardo la procedencia.");
        Assert.IsFalse(campos[0].Verificado, "El campo quedo firmado sin que nadie lo firmara.");
        Assert.IsNull(campos[0].VerificadoPor, "Quedo un firmante en un campo sin firmar.");
        Assert.AreEqual(
            "BALC2609",
            campos[0].ValorOcr,
            "Se perdio lo que el OCR leyo, que es lo unico que queda si el campo se corrige.");
    }

    /// <summary>Vigila que solo <c>Firmar</c>, con quién y cuándo, deja <c>verificado = 1</c>.</summary>
    [TestMethod]
    public void FirmarEsElUnicoCaminoAVerificado()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var miguel = companeros.Guardar(new Companero { Nombre = "Miguel" });

        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "numero_caso",
            Origen = OrigenDeCampo.Ocr,
        });

        var firmada = procedencia.Firmar(
            TablaDeProcedencia.Casos, caso.Id, "numero_caso", miguel.Id, "2026-09-04 10:00:00");

        Assert.IsTrue(firmada.SeEscribio, "No se pudo firmar el campo.");
        Assert.AreEqual(
            1,
            procedencia.ContarVerificados(TablaDeProcedencia.Casos, caso.Id),
            "Firmar no dejo el campo verificado.");

        var campo = procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id)[0];
        Assert.IsTrue(campo.Verificado);
        Assert.AreEqual(miguel.Id, campo.VerificadoPor, "La firma no dice QUIEN.");
        Assert.AreEqual("2026-09-04 10:00:00", campo.VerificadoEn, "La firma no dice CUANDO.");
    }

    /// <summary>Vigila que firmar sin fecha o a nombre de un id que no existe no escribe, y el campo queda sin firmar.</summary>
    [TestMethod]
    public void NoSePuedeFirmarSinDecirQuienNiCuando()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "numero_caso",
            Origen = OrigenDeCampo.Ocr,
        });

        var sinFecha = procedencia.Firmar(
            TablaDeProcedencia.Casos, caso.Id, "numero_caso", 1, "");
        Assert.IsFalse(sinFecha.SeEscribio, "Se firmo sin decir cuando.");

        var sinNadie = procedencia.Firmar(
            TablaDeProcedencia.Casos, caso.Id, "numero_caso", 9999, "2026-09-04 10:00:00");
        Assert.IsFalse(sinNadie.SeEscribio, "Se firmo a nombre de alguien que no existe.");

        Assert.AreEqual(
            0,
            procedencia.ContarVerificados(TablaDeProcedencia.Casos, caso.Id),
            "Alguno de los dos intentos dejo el campo firmado.");
    }

    /// <summary>Vigila que anotar de nuevo un campo firmado lo devuelve a sin firmar.</summary>
    [TestMethod]
    public void VolverAExtraerUnCampoLeQuitaLaFirmaQueTenia()
    {
        // Si el valor de debajo cambia, la firma vieja ya no dice nada sobre el valor
        // nuevo. Dejarla puesta seria afirmar que Miguel dio por bueno algo que no vio.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var miguel = companeros.Guardar(new Companero { Nombre = "Miguel" });

        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "numero_caso",
            Origen = OrigenDeCampo.Ocr,
            ValorOcr = "BALC2609",
        });
        procedencia.Firmar(
            TablaDeProcedencia.Casos, caso.Id, "numero_caso", miguel.Id, "2026-09-04 10:00:00");
        Assert.AreEqual(1, procedencia.ContarVerificados(TablaDeProcedencia.Casos, caso.Id));

        // Se vuelve a extraer el mismo campo, con otro valor.
        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "numero_caso",
            Origen = OrigenDeCampo.Ocr,
            ValorOcr = "CASP2609",
        });

        Assert.AreEqual(
            0,
            procedencia.ContarVerificados(TablaDeProcedencia.Casos, caso.Id),
            "El campo se volvio a extraer y conservo la firma vieja.");
        Assert.HasCount(
            1,
            procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id),
            "Volver a anotar el mismo campo duplico la fila en vez de pisarla.");
    }

    /// <summary>Vigila que tachado, ausente en el papel y vacío se guardan y se releen como tres cosas distintas.</summary>
    [TestMethod]
    public void ElTachonYLoAusenteSonDistintosDeUnCampoVacio()
    {
        // Tres cosas que se ven igual si no se distinguen: el papel no lo traia, el
        // papel lo traia tachado, y el OCR no supo leerlo.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos, RegistroId = caso.Id,
            Campo = "tachado", Origen = OrigenDeCampo.Anotacion, AnuladoPorTachon = true,
        });
        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos, RegistroId = caso.Id,
            Campo = "ausente", Origen = OrigenDeCampo.Vacio, AusenteEnElPapel = true,
        });
        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos, RegistroId = caso.Id,
            Campo = "no_leido", Origen = OrigenDeCampo.Vacio,
        });

        var campos = procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id)
            .ToDictionary(c => c.Campo, StringComparer.Ordinal);

        Assert.IsTrue(campos["tachado"].AnuladoPorTachon, "Se perdio el tachon.");
        Assert.IsFalse(campos["tachado"].AusenteEnElPapel);

        Assert.IsTrue(campos["ausente"].AusenteEnElPapel, "Se perdio que no estaba en el papel.");
        Assert.IsFalse(campos["ausente"].AnuladoPorTachon);

        Assert.IsFalse(campos["no_leido"].AnuladoPorTachon, "Un campo no leido salio como tachado.");
        Assert.IsFalse(campos["no_leido"].AusenteEnElPapel, "Un campo no leido salio como ausente.");
    }

    /// <summary>Vigila que las cuatro coordenadas de la banda vuelven con el mismo valor fraccionario.</summary>
    [TestMethod]
    public void LaBandaSeGuardaEnFraccionesYVuelveIgual()
    {
        // ARQUITECTURA §2.7: en fracciones de 0,0 a 1,0 y nunca en pixeles, porque un
        // pixel depende de la escala del dia en que se rasterizo.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos, RegistroId = caso.Id,
            Campo = "numero_caso", Origen = OrigenDeCampo.Ocr,
            BandaX0 = 0.125, BandaY0 = 0.25, BandaX1 = 0.875, BandaY1 = 0.3125,
        });

        var campo = procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id)[0];

        Assert.AreEqual(0.125, campo.BandaX0!.Value, 1e-9, "Se perdio el borde izquierdo.");
        Assert.AreEqual(0.25, campo.BandaY0!.Value, 1e-9, "Se perdio el borde superior.");
        Assert.AreEqual(0.875, campo.BandaX1!.Value, 1e-9, "Se perdio el borde derecho.");
        Assert.AreEqual(0.3125, campo.BandaY1!.Value, 1e-9, "Se perdio el borde inferior.");
    }

    /// <summary>Vigila que el motor rechaza borrar un caso con personas: las claves foráneas están encendidas.</summary>
    [TestMethod]
    public void LasClavesForaneasImpidenBorrarUnCasoConPersonasDentro()
    {
        // ARQUITECTURA §1.5: sin el pragma encendido, esto pasaria sin decir nada.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Fulano" });

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "DELETE FROM casos WHERE id = $id";
        orden.Parameters.AddWithValue("$id", caso.Id);

        Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(
            () => orden.ExecuteNonQuery(),
            "El motor dejo borrar un caso con personas dentro: las claves foraneas " +
            "no estan haciendo su trabajo.");

        Assert.AreEqual(1, baseDePrueba.ContarFilasDe("casos"), "El caso se borro igual.");
    }

    /// <summary>Vigila que guardar una persona huérfana no escribe y devuelve el fallo como aviso.</summary>
    [TestMethod]
    public void UnaPersonaNoPuedeApuntarAUnCasoQueNoExiste()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var huerfana = personas.Guardar(new Persona { CasoId = 9999, Nombre = "Fulano" });

        Assert.IsFalse(huerfana.SeEscribio, "Entro una persona sin caso.");
        Assert.IsTrue(huerfana.HayAvisos, "No dijo por que no entro.");
        Assert.AreEqual(0, baseDePrueba.ContarFilasDe("personas"), "La huerfana se guardo igual.");
    }
}
