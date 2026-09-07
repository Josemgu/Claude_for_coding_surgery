using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Datos.Falso;

/// <summary>
/// Lo que los repositorios falsos tienen que cumplir, que es lo mismo que cumplira
/// Fichas.Datos: avisar sin impedir, no firmar solo, y no borrar nada.
/// </summary>
/// <remarks>
/// Las pruebas salen de los requisitos del dueno del 2026-09-04 y de las reglas
/// permanentes, NO del codigo que hay debajo. Cuando llegue Fichas.Datos (fase C2), este
/// archivo se copia y se apunta a el: si el comportamiento cambia, se ve aqui.
/// </remarks>
[TestClass]
public sealed class PruebasDeLosRepositorios
{
    private const string ElDiaDeLaPrueba = "2026-09-04";

    /// <summary>Monta unos servicios falsos parados en el dia de la prueba.</summary>
    private static ServiciosFalsos Montar(int casos = 100, int semilla = 2468)
        => new(casos, semilla, new RelojFijo(ElDiaDeLaPrueba));

    // ---- Requisito 9: avisar, nunca impedir -------------------------------------------

    /// <summary>Un numero de caso con la forma equivocada se guarda igual y sale avisado.</summary>
    [TestMethod]
    [DataRow("CASP26O9")]
    [DataRow("CAS P2609")]
    [DataRow("casp2609")]
    public void UnNumeroDeCasoConLaFormaMalaEntraYSeAvisa(string numero)
    {
        var servicios = Montar();

        var resultado = servicios.Casos.Guardar(new Caso { NumeroCaso = numero, CreadoEn = ElDiaDeLaPrueba });

        Assert.IsTrue(resultado.SeEscribio, "El dato entra: no se rechaza (requisito 9 del dueno).");
        Assert.IsTrue(resultado.HayAvisos, "Y queda senalado, que es la otra mitad de la regla.");
        Assert.AreEqual(numero, servicios.Casos.Obtener(resultado.Id)!.NumeroCaso,
            "Se guarda tal como se leyo: el programa no lo arregla (regla permanente 1).");
    }

    /// <summary>El numero de caso bueno del dueno entra sin avisar de nada.</summary>
    [TestMethod]
    public void ElNumeroDeCasoBuenoEntraSinAvisos()
    {
        var servicios = Montar();

        var resultado = servicios.Casos.Guardar(new Caso { NumeroCaso = "CASD2609", CreadoEn = ElDiaDeLaPrueba });

        Assert.IsTrue(resultado.SeEscribio);
        Assert.IsFalse(resultado.HayAvisos);
    }

    /// <summary>Un MRN corto entra igual y sale avisado, porque romperia la reconciliacion.</summary>
    [TestMethod]
    public void UnMrnCortoEntraYSeAvisa()
    {
        var servicios = Montar();
        var unCaso = servicios.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(1)).Elementos[0];

        var resultado = servicios.Personas.Guardar(new Persona { CasoId = unCaso.Id, Nombre = "Ana", Mrn = "055-111" });

        Assert.IsTrue(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos);
        Assert.AreEqual("055-111", servicios.Personas.Obtener(resultado.Id)!.Mrn);
    }

    /// <summary>Un caso que no existe no lanza: devuelve que no se escribio y su motivo.</summary>
    [TestMethod]
    public void PedirUnCasoQueNoExisteNoLanza()
    {
        var servicios = Montar();

        Assert.IsNull(servicios.Casos.Obtener(999999));

        var resultado = servicios.Casos.Archivar(999999, true, ElDiaDeLaPrueba);
        Assert.IsFalse(resultado.SeEscribio);
        Assert.HasCount(1, resultado.Avisos);
    }

    // ---- Regla permanente 5: nada se firma sin Miguel ---------------------------------

    /// <summary>Anotar la procedencia de un campo NUNCA lo marca como verificado.</summary>
    [TestMethod]
    public void AnotarUnCampoNoLoFirma()
    {
        var servicios = Montar();

        // Se intenta colar un «verificado = 1» por la puerta de anotar. No entra.
        var resultado = servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = 1,
            Campo = "fecha_viaje",
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.9,
            Verificado = true,
            VerificadoPor = 1,
            VerificadoEn = ElDiaDeLaPrueba,
        });

        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, 1),
            "Regla permanente 5: el sistema propone; Miguel confirma.");
    }

    /// <summary>Firmar exige quien; sin un companero que exista no se firma nada.</summary>
    [TestMethod]
    public void NoSeFirmaSinDecirQuienFirma()
    {
        var servicios = Montar();

        var resultado = servicios.Procedencia.Firmar(TablaDeProcedencia.Casos, 1, "fecha_viaje", 999999, ElDiaDeLaPrueba);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, 1));
    }

    /// <summary>Firmar con un companero de verdad deja quien y cuando.</summary>
    [TestMethod]
    public void FirmarDejaQuienYCuando()
    {
        var servicios = Montar();
        var miguel = servicios.Companeros.Activos()[0];

        servicios.Procedencia.Firmar(TablaDeProcedencia.Casos, 1, "fecha_viaje", miguel.Id, ElDiaDeLaPrueba);

        var campo = servicios.Procedencia.DeRegistro(TablaDeProcedencia.Casos, 1)[0];
        Assert.IsTrue(campo.Verificado);
        Assert.AreEqual(miguel.Id, campo.VerificadoPor);
        Assert.AreEqual(ElDiaDeLaPrueba, campo.VerificadoEn);
    }

    /// <summary>Volver a anotar un campo firmado no le quita la firma a Miguel.</summary>
    [TestMethod]
    public void VolverAAnotarNoBorraLaFirma()
    {
        var servicios = Montar();
        var miguel = servicios.Companeros.Activos()[0];
        servicios.Procedencia.Firmar(TablaDeProcedencia.Casos, 1, "fecha_viaje", miguel.Id, ElDiaDeLaPrueba);

        servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = 1,
            Campo = "fecha_viaje",
            Origen = OrigenDeCampo.Ocr,
            ValorOcr = "2026-08-25",
        });

        Assert.AreEqual(1, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, 1));
    }

    // ---- Requisitos 2 y 8: asignar desde cualquier sitio, sin filtro de estado --------

    /// <summary>Cualquier caso se asigna, incluso uno archivado o ya completo.</summary>
    [TestMethod]
    public void CualquierCasoSeAsignaSinMirarSuEstado()
    {
        var servicios = Montar(300);
        var activo = servicios.Companeros.Activos()[0];
        var archivados = servicios.Casos.Listar(
            FiltroDeCasos.Todo with { IncluirArchivados = true }, Pagina.Primera(3000)).Elementos
            .Where(c => c.Archivado)
            .ToList();

        Assert.IsNotEmpty(archivados, "Hace falta al menos un caso archivado para que la prueba diga algo.");

        var resultado = servicios.Asignaciones.Asignar(archivados[0].Id, activo.Id, ElDiaDeLaPrueba);

        Assert.IsTrue(resultado.SeEscribio, "Requisito 8: no hay filtro de estado para asignar.");
    }

    /// <summary>A un companero desactivado no se le asigna, y se dice por que.</summary>
    [TestMethod]
    public void AUnCompaneroDesactivadoNoSeLeAsigna()
    {
        var servicios = Montar();
        var desactivado = servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(50)).Elementos
            .First(c => !c.Activo);
        var unCaso = servicios.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(1)).Elementos[0];

        var resultado = servicios.Asignaciones.Asignar(unCaso.Id, desactivado.Id, ElDiaDeLaPrueba);

        Assert.IsFalse(resultado.SeEscribio, "Es la unica condicion que queda para asignar.");
        Assert.HasCount(1, resultado.Avisos);
    }

    /// <summary>Asignar dos veces al mismo companero no duplica la asignacion.</summary>
    [TestMethod]
    public void AsignarDosVecesAlMismoNoDuplica()
    {
        var servicios = Montar();
        var activo = servicios.Companeros.Activos()[0];
        var unCaso = servicios.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(1)).Elementos[0];

        var primera = servicios.Asignaciones.Asignar(unCaso.Id, activo.Id, ElDiaDeLaPrueba);
        var segunda = servicios.Asignaciones.Asignar(unCaso.Id, activo.Id, ElDiaDeLaPrueba);

        Assert.AreEqual(primera.Id, segunda.Id);
        Assert.HasCount(1, servicios.Asignaciones.VivasDeCaso(unCaso.Id).Where(a => a.CompaneroId == activo.Id).ToList());
    }

    /// <summary>Retirar una asignacion la desactiva; no la borra.</summary>
    [TestMethod]
    public void RetirarUnaAsignacionNoLaBorra()
    {
        var servicios = Montar();
        var viva = servicios.Asignaciones.Listar(FiltroDeAsignaciones.Activas, Pagina.Primera(1)).Elementos[0];

        servicios.Asignaciones.Retirar(viva.Id, ElDiaDeLaPrueba);

        var todas = servicios.Asignaciones.Listar(new FiltroDeAsignaciones(SoloActivas: false), Pagina.Primera(5000));
        Assert.IsTrue(todas.Elementos.Any(a => a.Id == viva.Id && !a.Activa && a.DesactivadaEn == ElDiaDeLaPrueba),
            "La asignacion sigue ahi, desactivada y con su fecha.");
    }

    /// <summary>Desactivar un companero no le quita los casos: avisa de que los sigue llevando.</summary>
    [TestMethod]
    public void DesactivarUnCompaneroNoLeQuitaLosCasos()
    {
        var servicios = Montar(300);
        var conCasos = servicios.Companeros.Activos()
            .First(c => servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = c.Id }) > 0);
        var cuantos = servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = conCasos.Id });

        var resultado = servicios.Companeros.Desactivar(conCasos.Id, ElDiaDeLaPrueba);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos, "Nada se borra sin preguntar: se avisa de los casos que quedan.");
        Assert.AreEqual(cuantos, servicios.Asignaciones.Contar(FiltroDeAsignaciones.Activas with { CompaneroId = conCasos.Id }));
    }

    // ---- Lectura: filtros y trozos ----------------------------------------------------

    /// <summary>Los archivados no salen salvo que se pidan.</summary>
    [TestMethod]
    public void LosArchivadosNoSalenSalvoQueSePidan()
    {
        var servicios = Montar(500);

        var sinArchivados = servicios.Casos.Contar(FiltroDeCasos.Todo);
        var conArchivados = servicios.Casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true });

        Assert.IsGreaterThan(sinArchivados, conArchivados);
    }

    /// <summary>La ventana de siete dias trae solo lo que viaja de hoy a hoy mas siete.</summary>
    [TestMethod]
    public void LaVentanaDeSieteDiasTraeSoloLoQueViajaEnEsaSemana()
    {
        var servicios = Montar(800);
        var tope = servicios.Reloj.HoyMasDias(7);

        var enLaSemana = servicios.Casos.Listar(FiltroDeCasos.Todo with { VentanaDeDias = 7 }, Pagina.Primera(2000));

        Assert.IsNotEmpty(enLaSemana.Elementos);
        foreach (var caso in enLaSemana.Elementos)
        {
            Assert.IsNotNull(caso.FechaViaje);
            Assert.IsGreaterThanOrEqualTo(0, string.CompareOrdinal(caso.FechaViaje, ElDiaDeLaPrueba));
            Assert.IsLessThanOrEqualTo(0, string.CompareOrdinal(caso.FechaViaje, tope));
        }
    }

    /// <summary>Los vencidos son los que ya viajaron y siguen sin estar completos.</summary>
    [TestMethod]
    public void LosVencidosSonLosQueYaViajaronYSiguenSinResolver()
    {
        var servicios = Montar(800);

        var vencidos = servicios.Casos.Listar(FiltroDeCasos.Todo with { SoloVencidos = true }, Pagina.Primera(2000));

        Assert.IsNotEmpty(vencidos.Elementos);
        foreach (var caso in vencidos.Elementos)
        {
            Assert.IsLessThan(0, string.CompareOrdinal(caso.FechaViaje, ElDiaDeLaPrueba));
            Assert.AreNotEqual(EstadoDeRecomendacion.Completa, caso.Estado);
        }
    }

    /// <summary>Los trozos recorren la lista entera sin repetir ni perder ninguno.</summary>
    [TestMethod]
    public void LosTrozosRecorrenLaListaEnteraSinRepetir()
    {
        var servicios = Montar(500);
        var total = servicios.Casos.Contar(FiltroDeCasos.Todo);

        var vistos = new List<long>();
        var trozo = Pagina.Primera(37);
        while (true)
        {
            var pagina = servicios.Casos.Listar(FiltroDeCasos.Todo, trozo);
            if (pagina.Elementos.Count == 0) break;
            vistos.AddRange(pagina.Elementos.Select(c => c.Id));
            if (!pagina.HayMas) break;
            trozo = trozo.Siguiente();
        }

        Assert.HasCount(total, vistos);
        Assert.HasCount(total, vistos.Distinct().ToList());
    }

    /// <summary>Pedir un trozo mas alla del final devuelve vacio, no un fallo.</summary>
    [TestMethod]
    public void UnTrozoMasAllaDelFinalDevuelveVacio()
    {
        var servicios = Montar(10);

        var pagina = servicios.Casos.Listar(FiltroDeCasos.Todo, new Pagina(10_000, 20));

        Assert.IsEmpty(pagina.Elementos);
        Assert.IsFalse(pagina.HayMas);
    }

    /// <summary>Buscar por texto encuentra el caso por el nombre de una de sus personas.</summary>
    [TestMethod]
    public void BuscarPorTextoEncuentraPorElNombreDeLaPersona()
    {
        var servicios = Montar(200);
        var conNombre = servicios.Personas.Listar(FiltroDePersonas.Todo, Pagina.Primera(1)).Elementos[0];

        var encontrados = servicios.Casos.Listar(
            FiltroDeCasos.Todo with { Texto = conNombre.Nombre, IncluirArchivados = true }, Pagina.Primera(500));

        Assert.IsTrue(encontrados.Elementos.Any(c => c.Id == conNombre.CasoId));
    }

    // ---- Paquetes: el Excel marca estado, no firma campos ------------------------------

    /// <summary>Aplicar la hoja de un companero escribe el estado con su nombre.</summary>
    [TestMethod]
    public void LaHojaDelCompaneroEscribeElEstadoConSuNombre()
    {
        var servicios = Montar(100);
        var sandy = servicios.Companeros.Activos()[1];
        var persona = servicios.Personas.Listar(FiltroDePersonas.Todo, Pagina.Primera(50)).Elementos
            .First(p => p.Mrn is not null);

        servicios.Paquetes.AplicarMarcas(
            [new Contratos.Lectura.MarcaDelCompanero(
                null, persona.Mrn, persona.Nombre, EstadoDeRecomendacion.Completa,
                "listo", null, true, true, true, true, true, true, false, 2)],
            sandy.Id,
            @"C:\paquetes\sandy.xlsx");

        var caso = servicios.Casos.Obtener(persona.CasoId)!;
        Assert.AreEqual(EstadoDeRecomendacion.Completa, caso.Estado);
        Assert.AreEqual(sandy.Id, caso.EstadoDelCompaneroPor);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, persona.Id),
            "El Excel marca el estado; NO firma ningun campo (regla permanente 5).");
    }

    /// <summary>Una fila sin MRN no se casa por nombre: se guarda descartada con su motivo.</summary>
    [TestMethod]
    public void UnaFilaSinMrnSeGuardaDescartada()
    {
        var servicios = Montar(50);
        var sandy = servicios.Companeros.Activos()[1];

        var resultado = servicios.Paquetes.AplicarMarcas(
            [new Contratos.Lectura.MarcaDelCompanero(
                "CASP2609", null, "Alguien Sin MRN", EstadoDeRecomendacion.NoCompleta,
                null, null, null, null, null, null, null, null, null, 5)],
            sandy.Id,
            @"C:\paquetes\sandy.xlsx");

        Assert.IsTrue(resultado.SeEscribio);
        var descartadas = servicios.Ilegibles.ListarDescartadas(sandy.Id, Pagina.Primera(10));
        Assert.HasCount(1, descartadas.Elementos);
        Assert.AreEqual(5, descartadas.Elementos[0].FilaExcel, "El renglon dice con que fila del Excel choco.");
    }
}
