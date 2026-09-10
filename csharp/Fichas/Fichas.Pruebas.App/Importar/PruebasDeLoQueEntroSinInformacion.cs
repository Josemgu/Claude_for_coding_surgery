using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Mantenimiento;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Los documentos que entraron sin informacion de ninguna persona, y borrarlos.
/// </summary>
/// <remarks>
/// <para>Del dueno, el 2026-09-07 y dicho dos veces el mismo dia: <i>«no se contempla
/// eliminar PDF o documentos que no tienen informacion; debe poder eliminarlo»</i> y
/// <i>«este documento no tiene informacion de ninguna persona, deberia permitirme
/// eliminarlo»</i>.</para>
///
/// <para>⚠️ <b>«Documento sin informacion» son DOS cosas distintas y hay que medirlas por
/// separado</b>, porque el arreglo de una no es el de la otra:</para>
/// <list type="number">
/// <item><b>Un caso del que no se leyo ninguna persona.</b> El caso existe, esta en
/// <c>casos</c>, y por tanto <c>IMantenimiento.PlanearDocumentos</c> ya lo alcanza: lo que
/// faltaba no era el borrado, era verlo desde donde se mira.</item>
/// <item><b>Un PDF que no se pudo leer en absoluto.</b> Deja un renglon en
/// <c>documentos_ilegibles</c> con <c>caso_id</c> NULO. NO tiene caso, asi que ningun
/// borrado del programa lo alcanza y queda registrado para siempre.</item>
/// </list>
///
/// <para>⛔ El renglon de ilegible que SI tiene caso no cuenta como huerfano: es informacion
/// sobre un documento que existe, y se va con el cuando el documento se borre.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueEntroSinInformacion : BaseDeImportacion
{
    // ==================================================================
    // Ver: las dos listas, separadas y contadas.
    // ==================================================================

    /// <summary>Un caso del que no se leyo ninguna persona sale en la lista; uno con gente, no.</summary>
    [TestMethod]
    public void SoloSalenLosCasosDeLosQueNoSeLeyoNingunaPersona()
    {
        var conGente = SembrarCasoConPersonas("CASP2609", 3);
        var vacio = SembrarCasoConPersonas("BALC2609", 0);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [conGente, vacio], []);

        Assert.HasCount(1, sinInformacion.CasosSinPersonas);
        Assert.AreEqual(vacio, sinInformacion.CasosSinPersonas[0].CasoId);
        Assert.AreEqual(0, sinInformacion.CasosSinPersonas[0].Personas);
    }

    /// <summary>El renglon de un PDF ilegible SIN caso sale; el que tiene caso, no.</summary>
    /// <remarks>
    /// La distincion es la que decide si hay algo huerfano: el renglon con caso se lo lleva
    /// el borrado del caso —<c>documentos_ilegibles</c> es uno de los pasos de
    /// <c>RepositorioDeMantenimiento</c>—, y el que no tiene caso no se lo lleva nadie.
    /// </remarks>
    [TestMethod]
    public void SoloSalenLosRenglonesDeIlegibleQueNoTienenCaso()
    {
        var caso = SembrarCasoConPersonas("CASP2609", 1);
        var huerfano = SembrarRenglon(@"C:\escaneos\roto.pdf", casoId: null);
        SembrarRenglon(@"C:\escaneos\CASP2609.pdf", casoId: caso);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [caso],
            [@"C:\escaneos\roto.pdf", @"C:\escaneos\CASP2609.pdf"]);

        Assert.HasCount(1, sinInformacion.RenglonesSinCaso);
        Assert.AreEqual(huerfano, sinInformacion.RenglonesSinCaso[0].RenglonId);
    }

    /// <summary>Solo se mira lo de ESTA tanda; lo que ya estaba en la base no se ofrece.</summary>
    /// <remarks>
    /// La pantalla de importar habla de lo que se acaba de importar. Ofrecer ahi para borrar
    /// un documento viejo que el dueno no tiene delante seria ofrecerle borrar a ciegas.
    /// </remarks>
    [TestMethod]
    public void NoSeOfreceLoQueNoEntroEnEstaTanda()
    {
        var deLaTanda = SembrarCasoConPersonas("CASP2609", 0);
        SembrarCasoConPersonas("BALC2609", 0);
        SembrarRenglon(@"C:\escaneos\viejo.pdf", casoId: null);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [deLaTanda], [@"C:\escaneos\CASP2609.pdf"]);

        Assert.HasCount(1, sinInformacion.CasosSinPersonas);
        Assert.AreEqual(deLaTanda, sinInformacion.CasosSinPersonas[0].CasoId);
        Assert.IsEmpty(sinInformacion.RenglonesSinCaso);
    }

    /// <summary>Cuando no hay nada sin información, la lista lo dice y no ofrece borrar nada.</summary>
    [TestMethod]
    public void CuandoTodoTraeInformacionNoHayNadaQueOfrecer()
    {
        var caso = SembrarCasoConPersonas("CASP2609", 2);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [caso], [@"C:\escaneos\CASP2609.pdf"]);

        Assert.IsFalse(sinInformacion.HayAlgo);
        Assert.AreEqual(0, sinInformacion.Cuantos);
    }

    /// <summary>La cuenta dice las dos cifras por separado, no una suma sin desglosar.</summary>
    [TestMethod]
    public void LaCuentaDiceCuantosSonDeCadaClase()
    {
        var vacio = SembrarCasoConPersonas("CASP2609", 0);
        SembrarRenglon(@"C:\escaneos\roto.pdf", casoId: null);
        SembrarRenglon(@"C:\escaneos\roto2.pdf", casoId: null);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [vacio],
            [@"C:\escaneos\roto.pdf", @"C:\escaneos\roto2.pdf"]);

        Assert.IsTrue(sinInformacion.HayAlgo);
        Assert.AreEqual(3, sinInformacion.Cuantos);
        Assert.HasCount(1, sinInformacion.CasosSinPersonas);
        Assert.HasCount(2, sinInformacion.RenglonesSinCaso);
        Assert.Contains("1 documento del que no se leyó ninguna persona", sinInformacion.Linea);
        Assert.Contains("2 PDF que no se pudieron leer", sinInformacion.Linea);
    }

    // ==================================================================
    // Borrar: con su cifra delante, su copia y su cancelacion.
    // ==================================================================

    /// <summary>
    /// Borrar un caso sin personas dice cuantos van a caer, dónde quedó la copia, y lo borra.
    /// </summary>
    /// <remarks>
    /// El plan se pide al MISMO puerto que borra en Revisar. No se inventa otro camino:
    /// el programa ya tiene una forma de deshacer —copia, cifra delante, cancelable— y dos
    /// formas serian dos sitios donde alguien puede olvidarse de la copia.
    /// </remarks>
    [TestMethod]
    public void BorrarUnCasoSinPersonasDiceCuantosYDondeQuedoLaCopia()
    {
        var vacio = SembrarCasoConPersonas("CASP2609", 0);
        var conGente = SembrarCasoConPersonas("BALC2609", 2);
        var mantenimiento = new RepositorioDeMantenimiento(Conexion);

        var plan = mantenimiento.PlanearDocumentos([vacio]);

        Assert.IsTrue(plan.SePuedeBorrar, "El plan no dio permiso.");
        Assert.AreEqual(1, plan.Documentos, "La cifra que se le enseña antes de borrar no es 1.");
        Assert.AreEqual(0, plan.Personas);
        Assert.IsNotNull(plan.RutaDeLaCopia, "No se hizo copia previa: sin copia no se borra.");
        Assert.Contains(RespaldoAntesDeBorrar.MarcaDeLaCopia, plan.RutaDeLaCopia);
        Assert.Contains(plan.RutaDeLaCopia, plan.Pregunta, "La pregunta no dice dónde quedó la copia.");

        var resultado = mantenimiento.Borrar(plan);

        Assert.IsTrue(resultado.SeBorro);
        Assert.IsNull(Datos.Casos.Obtener(vacio), "El documento vacío sigue en la base.");
        Assert.IsNotNull(Datos.Casos.Obtener(conGente), "Se llevó por delante el que sí tenía gente.");
    }

    /// <summary>Si se cancela, NO se borra nada, y se comprueba contándolo en la base.</summary>
    /// <remarks>
    /// Se cuenta preguntándole al motor y no al programa: lo que hay que demostrar es que la
    /// fila sigue ahí, no que un objeto diga que sigue ahí.
    /// </remarks>
    [TestMethod]
    public void SiSeCancelaNoSeBorraNadaDeLaBase()
    {
        var vacio = SembrarCasoConPersonas("CASP2609", 0);
        var mantenimiento = new RepositorioDeMantenimiento(Conexion);

        var antes = Contar("casos");
        var plan = mantenimiento.PlanearDocumentos([vacio]);

        // Cancelar es no llamar a Borrar: es lo que hace OperacionDeBorrar cuando el dueño
        // pulsa «No borrar nada». La copia ya está hecha, y eso es lo que se anuncia.
        Assert.AreEqual(antes, Contar("casos"), "Planear tocó la base, y planear no debe tocar nada.");
        Assert.IsNotNull(plan.RutaDeLaCopia);
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia), "La copia anunciada no está donde se dijo.");
    }

    /// <summary>Un plan sin copia previa NO borra, ni aunque venga con permiso.</summary>
    [TestMethod]
    public void SinCopiaPreviaNoSeBorraNada()
    {
        var vacio = SembrarCasoConPersonas("CASP2609", 0);
        var mantenimiento = new RepositorioDeMantenimiento(Conexion);
        var plan = mantenimiento.PlanearDocumentos([vacio]) with { RutaDeLaCopia = null };

        var resultado = mantenimiento.Borrar(plan);

        Assert.IsFalse(resultado.SeBorro);
        Assert.IsNotNull(Datos.Casos.Obtener(vacio));
    }

    /// <summary>Borrar el caso se lleva también su renglón de ilegible; no queda nada colgando.</summary>
    [TestMethod]
    public void BorrarElCasoSeLlevaSuRenglonDeIlegible()
    {
        var vacio = SembrarCasoConPersonas("CASP2609", 0);
        SembrarRenglon(@"C:\escaneos\CASP2609.pdf", casoId: vacio);
        Assert.AreEqual(1L, Contar("documentos_ilegibles"));

        var mantenimiento = new RepositorioDeMantenimiento(Conexion);
        Assert.IsTrue(mantenimiento.Borrar(mantenimiento.PlanearDocumentos([vacio])).SeBorro);

        Assert.AreEqual(0L, Contar("documentos_ilegibles"), "Quedó un renglón apuntando a un caso que ya no existe.");
        Assert.AreEqual(0L, Contar("casos"));
    }

    // ==================================================================
    // Utilidades de estas pruebas.
    // ==================================================================

    /// <summary>Un caso con tantas personas como se le diga; cero es un dato, no un descuido.</summary>
    private long SembrarCasoConPersonas(string numeroCaso, int cuantasPersonas)
    {
        var caso = Datos.Casos.Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            FechaViaje = "2026-09-20",
            UnidadNumero = "123456",
            UnidadNombre = "Barrio de prueba",
            RutaPdf = $@"C:\escaneos\{numeroCaso}.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(caso.SeEscribio, "No se pudo sembrar el caso de la prueba.");

        for (var fila = 1; fila <= cuantasPersonas; fila++)
        {
            var persona = Datos.Personas.Guardar(new Persona
            {
                CasoId = caso.Id,
                Nombre = $"Persona {fila} de {numeroCaso}",
                FilaFormulario = fila,
            });
            Assert.IsTrue(persona.SeEscribio, "No se pudo sembrar una persona.");
        }

        return caso.Id;
    }

    /// <summary>Un renglón de lo que no se pudo leer, con o sin caso detrás.</summary>
    private long SembrarRenglon(string rutaPdf, long? casoId)
    {
        var escrito = Datos.Ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = rutaPdf,
            Motivo = MotivosDeIlegible.NoSePudoAbrir,
            Detalle = "prueba",
            LineasLeidas = 0,
            CasoId = casoId,
            RegistradoEn = "2026-09-07T09:00:00",
        });
        Assert.IsTrue(escrito.SeEscribio, "No se pudo sembrar el renglón de la prueba.");
        return escrito.Id;
    }
}
