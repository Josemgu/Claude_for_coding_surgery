using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que los seis repositorios cumplen lo que sus contratos prometen.
/// </summary>
[TestClass]
public sealed class PruebaDeLosRepositorios
{
    [TestMethod]
    public void UnCasoSeGuardaYSeReleeConSusVeinteColumnas()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var guardado = casos.Guardar(new Caso
        {
            NumeroCaso = "BALC2609",
            UnidadNumero = "7000011",
            FechaViaje = "2026-09-08",
            CapturaManual = true,
            RutaPdf = @"C:\escaneos\hoja.pdf",
            CreadoEn = "2026-09-04 10:00:00",
            EstadoRecomendacion = "completa",
            PaginaPdf = 3,
            UnidadNombre = "Paramaribo Branch",
            TemploNombre = "Templo de Santo Domingo",
            EstadoDelCompanero = "completa",
            EstadoDelCompaneroPor = sandy.Id,
            EstadoDelCompaneroEn = "2026-09-04 11:00:00",
        });

        Assert.IsTrue(guardado.SeEscribio, "El caso no se guardo.");

        var leido = casos.Obtener(guardado.Id);
        Assert.IsNotNull(leido, "El caso no se pudo releer.");
        Assert.AreEqual("BALC2609", leido.NumeroCaso);
        Assert.AreEqual("7000011", leido.UnidadNumero, "Se perdio la unidad de 7 digitos.");
        Assert.AreEqual("2026-09-08", leido.FechaViaje);
        Assert.IsTrue(leido.CapturaManual);
        Assert.IsFalse(leido.Archivado);
        Assert.AreEqual(3, leido.PaginaPdf);
        Assert.AreEqual("Paramaribo Branch", leido.UnidadNombre);
        Assert.AreEqual("Templo de Santo Domingo", leido.TemploNombre);
        Assert.AreEqual(sandy.Id, leido.EstadoDelCompaneroPor);
        Assert.AreEqual(EstadoDeRecomendacion.Completa, leido.Estado);
    }

    [TestMethod]
    public void LaUnidadDeSieteDigitosEntraYLaDeCincoAvisa()
    {
        // La leccion de la version 2, comprobada en los dos sentidos.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var siete = casos.Guardar(new Caso { NumeroCaso = "BALC2609", UnidadNumero = "7000011" });
        Assert.IsTrue(siete.SeEscribio, "La unidad de 7 digitos no entro.");
        Assert.IsFalse(siete.HayAvisos, "La unidad de 7 digitos aviso de algo.");

        var seis = casos.Guardar(new Caso { NumeroCaso = "BALC2609", UnidadNumero = "123456" });
        Assert.IsTrue(seis.SeEscribio, "La unidad de 6 digitos no entro.");

        var cinco = casos.Guardar(new Caso { NumeroCaso = "BALC2609", UnidadNumero = "12345" });
        Assert.IsTrue(cinco.HayAvisos, "La unidad de 5 digitos no aviso de nada.");
    }

    [TestMethod]
    public void DosCasosPuedenCompartirElMismoNumero()
    {
        // La version 12: el numero es una unidad y un mes, no una familia. Antes, seis
        // de diez documentos del dueno se rechazaban por esto.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var primero = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var segundo = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        Assert.IsTrue(primero.SeEscribio, "El primer caso no entro.");
        Assert.IsTrue(
            segundo.SeEscribio,
            "El segundo caso con el mismo numero se rechazo, y la version 12 quito ese UNIQUE.");
        Assert.AreNotEqual(primero.Id, segundo.Id, "Los dos casos son el mismo.");
    }

    [TestMethod]
    public void LaListaDeCasosSePaginaYDiceCuantosHayDetras()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        for (var i = 0; i < 25; i++)
        {
            casos.Guardar(new Caso
            {
                NumeroCaso = "BALC2609",
                FechaViaje = "2026-09-" + (i + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        var primera = casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(10));

        Assert.HasCount(10, primera.Elementos, "El primer trozo no trajo 10.");
        Assert.AreEqual(25, primera.TotalDisponible, "El total detras del filtro no es 25.");
        Assert.IsTrue(primera.HayMas, "Deberia quedar mas detras del primer trozo.");

        var ultima = casos.Listar(FiltroDeCasos.Todo, new Pagina(20, 10));
        Assert.HasCount(5, ultima.Elementos, "El ultimo trozo no trajo los 5 que quedaban.");
        Assert.IsFalse(ultima.HayMas, "Detras del ultimo trozo no deberia quedar nada.");
    }

    [TestMethod]
    public void UnTrozoDePedidoAbsurdoNoTumbaLaConsulta()
    {
        // Requisito 9 aplicado a la paginacion: un filtro que no se entiende se corrige
        // y se sigue, no rechaza la consulta.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        var pagina = casos.Listar(FiltroDeCasos.Todo, new Pagina(-5, 0));

        Assert.AreEqual(1, pagina.TotalDisponible, "La consulta con un trozo absurdo se perdio el caso.");
    }

    [TestMethod]
    public void ContarPersonasDeDevuelveCeroParaUnCasoSinPersonas()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var conGente = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var vacio = casos.Guardar(new Caso { NumeroCaso = "CASP2609" });

        personas.Guardar(new Persona { CasoId = conGente.Id, Nombre = "Fulano" });
        personas.Guardar(new Persona { CasoId = conGente.Id, Nombre = "Mengano" });

        var cuenta = casos.ContarPersonasDe([conGente.Id, vacio.Id]);

        Assert.AreEqual(2, cuenta[conGente.Id], "No conto las dos personas del caso.");
        Assert.AreEqual(
            0,
            cuenta[vacio.Id],
            "Un caso sin personas tiene que traer su cero: sin el, la pantalla no " +
            "distingue «no tiene» de «no vino».");
    }

    [TestMethod]
    public void ArchivarExigeFechaYDesarchivarLaQuita()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });

        var sinFecha = casos.Archivar(caso.Id, archivado: true, fechaDeArchivado: "");
        Assert.IsFalse(sinFecha.SeEscribio, "Archivo un caso sin fecha.");
        Assert.IsTrue(sinFecha.HayAvisos, "No dijo por que no se pudo archivar.");

        var conFecha = casos.Archivar(caso.Id, archivado: true, fechaDeArchivado: "2026-09-04");
        Assert.IsTrue(conFecha.SeEscribio, "No se pudo archivar con fecha.");

        var archivado = casos.Obtener(caso.Id);
        Assert.IsNotNull(archivado);
        Assert.IsTrue(archivado.Archivado);
        Assert.AreEqual("2026-09-04", archivado.FechaArchivado);

        casos.Archivar(caso.Id, archivado: false, fechaDeArchivado: "2026-09-04");
        var desarchivado = casos.Obtener(caso.Id);
        Assert.IsNotNull(desarchivado);
        Assert.IsFalse(desarchivado.Archivado);
        Assert.IsNull(desarchivado.FechaArchivado, "Desarchivar tiene que quitar la fecha.");
    }

    [TestMethod]
    public void UnCasoArchivadoNoSaleEnLaListaSalvoQueSePida()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        casos.Archivar(caso.Id, archivado: true, fechaDeArchivado: "2026-09-04");

        Assert.AreEqual(0, casos.Contar(FiltroDeCasos.Todo), "El archivado salio sin pedirlo.");
        Assert.AreEqual(
            1,
            casos.Contar(new FiltroDeCasos(IncluirArchivados: true)),
            "El archivado no salio ni pidiendolo.");
    }

    [TestMethod]
    public void LasSeisCasillasYLosSeisPasosGuardanSusTresEstados()
    {
        // El nulo es un DATO: «no leida» no es lo mismo que «leida y no marcada».
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var guardada = personas.Guardar(new Persona
        {
            CasoId = caso.Id,
            Nombre = "Fulano",
            OrdRecibirPropias = true,
            OrdObservarSellamiento = false,
            OrdTraductor = null,
            PasoPreparacion = true,
            PasoInformacion = false,
            PasoCitaDelTemplo = null,
            LlamoAlLider = true,
        });

        var leida = personas.Obtener(guardada.Id);
        Assert.IsNotNull(leida);

        Assert.IsTrue(leida.OrdRecibirPropias, "Se perdio la casilla marcada.");
        Assert.IsFalse(leida.OrdObservarSellamiento, "Se perdio la casilla sin marcar.");
        Assert.IsNull(leida.OrdTraductor, "La casilla NO LEIDA se convirtio en un 0 o un 1.");

        Assert.IsTrue(leida.PasoPreparacion, "Se perdio el paso marcado.");
        Assert.IsFalse(leida.PasoInformacion, "Se perdio el paso sin marcar.");
        Assert.IsNull(leida.PasoCitaDelTemplo, "El paso NO MIRADO se convirtio en un 0 o un 1.");
        Assert.IsTrue(leida.LlamoAlLider, "Se perdio que llamo al lider.");
    }

    [TestMethod]
    public void LasPersonasDeUnCasoVienenEnElOrdenDelFormulario()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Tercero", FilaFormulario = 3 });
        personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Primero", FilaFormulario = 1 });
        personas.Guardar(new Persona { CasoId = caso.Id, Nombre = "Segundo", FilaFormulario = 2 });

        var enOrden = personas.DeCaso(caso.Id).Select(p => p.Nombre).ToArray();

        CollectionAssert.AreEqual(
            new[] { "Primero", "Segundo", "Tercero" },
            enOrden,
            "Las personas no vienen en el orden en que estaban en el papel.");
    }

    [TestMethod]
    public void UnCompaneroSeDesactivaYNuncaSeBorra()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        Assert.HasCount(1, companeros.Activos(), "Sandy no salio entre los activos.");

        var retirada = companeros.Desactivar(sandy.Id, "2026-09-04 12:00:00");
        Assert.IsTrue(retirada.SeEscribio, "No se pudo desactivar.");

        Assert.IsEmpty(companeros.Activos(), "Sandy sigue entre los activos.");

        var leida = companeros.Obtener(sandy.Id);
        Assert.IsNotNull(leida, "Sandy desaparecio de la base, y se desactiva, no se borra.");
        Assert.IsFalse(leida.Activo);
        Assert.AreEqual("2026-09-04 12:00:00", leida.DesactivadoEn);
    }

    [TestMethod]
    public void NoSeAsignaUnCasoAUnCompaneroDesactivado()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var asignaciones = new RepositorioDeAsignaciones(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        companeros.Desactivar(sandy.Id, "2026-09-04 12:00:00");

        var intento = asignaciones.Asignar(caso.Id, sandy.Id, "2026-09-04");

        Assert.IsFalse(intento.SeEscribio, "Se asigno un caso a un companero desactivado.");
        Assert.IsTrue(intento.HayAvisos, "No dijo por que no se pudo asignar.");
    }

    [TestMethod]
    public void UnaAsignacionSeRetiraDesactivandolaYNoBorrandola()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var asignaciones = new RepositorioDeAsignaciones(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso { NumeroCaso = "BALC2609" });
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        var asignada = asignaciones.Asignar(caso.Id, sandy.Id, "2026-09-04");

        Assert.HasCount(1, asignaciones.VivasDeCaso(caso.Id), "La asignacion no quedo viva.");

        asignaciones.Retirar(asignada.Id, "2026-09-05");

        Assert.IsEmpty(asignaciones.VivasDeCaso(caso.Id), "La asignacion sigue viva.");
        Assert.AreEqual(
            1,
            asignaciones.Contar(new FiltroDeAsignaciones(SoloActivas: false)),
            "La asignacion se borro, y se conserva para saber quien llevo que.");
    }

    [TestMethod]
    public void UnRenglonIlegibleSeGuardaYSePuedeConsultarDespues()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);

        ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = @"C:\escaneos\rota.pdf",
            PaginaPdf = 2,
            Motivo = "sin_texto",
            Detalle = "El OCR no leyo ni una letra.",
            LineasLeidas = 0,
        });

        var lista = ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(10));

        Assert.AreEqual(1, lista.TotalDisponible, "El renglon no se guardo.");
        Assert.AreEqual(
            0,
            lista.Elementos[0].LineasLeidas,
            "El cero de lineas leidas es un DATO y se perdio.");
        Assert.AreEqual(1, ilegibles.Contar(new FiltroDeIlegibles(Motivo: "sin_texto")));
        Assert.AreEqual(0, ilegibles.Contar(new FiltroDeIlegibles(Motivo: "otro_motivo")));
    }

    [TestMethod]
    public void UnaFilaDescartadaGuardaLoQueVeniaEscritoSinValidarlo()
    {
        // La tabla existe para guardar lo que NO entro: no puede rechazar lo que no entro.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        var guardada = ilegibles.RegistrarDescartada(new FilaDescartada
        {
            CompaneroId = sandy.Id,
            RutaExcel = @"C:\excel\sandy.xlsx",
            FilaExcel = 7,
            NumeroCaso = "esto no es un numero de caso",
            Mrn = "ni esto un MRN",
            Nombre = "Fulano",
            Motivo = "La fila 7 no caso con nadie: la clave 'X' no tiene la forma esperada.",
        });

        Assert.IsTrue(guardada.SeEscribio, "La fila descartada se rechazo por su contenido.");

        var lista = ilegibles.ListarDescartadas(sandy.Id, Pagina.Primera(10));
        Assert.AreEqual(1, lista.TotalDisponible);
        Assert.AreEqual("esto no es un numero de caso", lista.Elementos[0].NumeroCaso);
        Assert.AreEqual("ni esto un MRN", lista.Elementos[0].Mrn);
        Assert.AreEqual(7, lista.Elementos[0].FilaExcel, "Se perdio la fila con la que choco.");
    }
}
