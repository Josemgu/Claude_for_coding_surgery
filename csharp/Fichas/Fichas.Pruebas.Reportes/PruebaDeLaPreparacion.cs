using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La cuenta por la que existe el programa: cuantos viajaron sin la preparacion completa.
/// </summary>
/// <remarks>
/// Portado de reportes/preparacion.py y de datos/pasos.py. «Con la preparacion completa»
/// es los SEIS pasos en «Si» y nada mas; no se mira `estado_recomendacion`, que es del
/// caso entero. Una persona sin ningun paso contestado cuenta como SIN la preparacion
/// completa, que es el lado seguro del error (criterio C8-2).
/// </remarks>
[TestClass]
public class PruebaDeLaPreparacion
{
    /// <summary>Una persona con los seis pasos puestos al mismo valor.</summary>
    /// <param name="valor">Sí, no o nulo, para los seis.</param>
    /// <param name="id">El id de la persona, que se usa también como id de su caso.</param>
    private static Persona ConLosPasos(bool? valor, long id = 1) => new()
    {
        Id = id,
        CasoId = id,
        Nombre = "Ana Anonimo",
        PasoPreparacion = valor,
        PasoInformacion = valor,
        PasoCitaDelTemplo = valor,
        PasoAccionesRequeridas = valor,
        PasoEntrevistas = valor,
        PasoListoParaElTemplo = valor,
    };

    /// <summary>Vigila que con los seis pasos en sí el estado es verdadero.</summary>
    [TestMethod]
    public void LosSeisPasosEnSiEsLaPreparacionCompleta()
        => Assert.IsTrue(Pasos.Estado(ConLosPasos(true)));

    /// <summary>Vigila que un solo paso en no deja el estado en falso.</summary>
    [TestMethod]
    public void UnPasoEnNoBastaParaQueNoLoEste()
    {
        var persona = ConLosPasos(true) with { PasoEntrevistas = false };
        Assert.IsFalse(Pasos.Estado(persona));
    }

    /// <summary>Vigila que un paso en blanco da estado nulo, y que nulo cuenta como sin la preparación completa.</summary>
    [TestMethod]
    public void UnPasoSinContestarNoEsUnNo_EsQueNoSeSabe()
    {
        var persona = ConLosPasos(true) with { PasoEntrevistas = null };
        Assert.IsNull(Pasos.Estado(persona), "Sin contestar NO es «No»; son tres respuestas, no dos.");
        Assert.IsFalse(Preparacion.TieneLaPreparacionCompleta(persona));
    }

    /// <summary>Vigila que los pasos en no salen por su nombre, sin el «2. » de delante.</summary>
    [TestMethod]
    public void LosPasosSinCompletarSalenSinElNumeroDeDelante()
    {
        var persona = ConLosPasos(true) with { PasoInformacion = false, PasoEntrevistas = false };

        CollectionAssert.AreEqual(new[] { "Información", "Entrevistas" }, Pasos.SinCompletar(persona).ToArray());
    }

    /// <summary>Vigila que el mismo día del viaje aún no ha viajado, el anterior sí, y sin fecha no.</summary>
    [TestMethod]
    public void ElDiaDelViajeNoCuentaComoViajado()
    {
        Assert.IsFalse(Preparacion.YaViajo("2026-09-30", "2026-09-30"));
        Assert.IsTrue(Preparacion.YaViajo("2026-09-29", "2026-09-30"));
        Assert.IsFalse(Preparacion.YaViajo(null, "2026-09-30"));
    }

    /// <summary>Vigila que sin pasos contestados dice «nadie la miró» y con uno en no dice «no está completa: falta …».</summary>
    [TestMethod]
    public void QuePasoDistingueNadieLaMiroDeNoEstaCompleta()
    {
        Assert.AreEqual(Preparacion.NadieLaMiro, Preparacion.QuePaso(ConLosPasos(null)));

        var incompleta = ConLosPasos(true) with { PasoEntrevistas = false };
        StringAssert.StartsWith(Preparacion.QuePaso(incompleta), Preparacion.NoEstaCompleta);
        StringAssert.Contains(Preparacion.QuePaso(incompleta), "Entrevistas");
    }

    /// <summary>Vigila que viajaron = completas + sin completar, y que por viajar es el resto.</summary>
    [TestMethod]
    public void LosTresGruposSonExcluyentesYLosDosPrimerosSumanLosQueViajaron()
    {
        var filas = new List<PersonaConSuCaso>
        {
            Fila(ConLosPasos(true, 1), "2026-09-01"),   // ya viajo, completa
            Fila(ConLosPasos(null, 2), "2026-09-02"),   // ya viajo, sin completar
            Fila(ConLosPasos(true, 3), "2026-09-29"),   // todavia no viajo
        };

        var recuento = Preparacion.Recontar(filas, "2026-09-15");

        Assert.HasCount(2, recuento.Viajaron);
        Assert.HasCount(1, recuento.Completas);
        Assert.HasCount(1, recuento.SinCompletar);
        Assert.HasCount(1, recuento.PorViajar);
        Assert.AreEqual(recuento.Viajaron.Count, recuento.Completas.Count + recuento.SinCompletar.Count);
    }

    /// <summary>Vigila que dos casos con el mismo número cuentan como dos, no como uno.</summary>
    [TestMethod]
    public void SeCuentanCasosYNoNumerosDeCaso()
    {
        // Desde la migracion 12 dos casos distintos pueden llevar el MISMO numero.
        // Contar numeros distintos diria «1 caso en el período» donde hay dos.
        var filas = new List<PersonaConSuCaso>
        {
            Fila(ConLosPasos(true, 1), "2026-09-01", casoId: 10, numeroCaso: "CASP2609"),
            Fila(ConLosPasos(true, 2), "2026-09-01", casoId: 11, numeroCaso: "CASP2609"),
        };

        Assert.HasCount(2, Preparacion.Recontar(filas, "2026-09-15").Casos);
    }

    /// <summary>Vigila que un caso sin número se cuenta igual y no tumba el recuento.</summary>
    [TestMethod]
    public void UnCasoSinNumeroNoRevientaElRecuento()
    {
        var filas = new List<PersonaConSuCaso>
        {
            Fila(ConLosPasos(true, 1), "2026-09-01", casoId: 10, numeroCaso: null),
            Fila(ConLosPasos(true, 2), "2026-09-01", casoId: 11, numeroCaso: "CASP2609"),
        };

        Assert.HasCount(2, Preparacion.Recontar(filas, "2026-09-15").Casos);
    }

    /// <summary>Vigila que el titular dice «1 de las 2 personas» y no lleva ningún «%».</summary>
    [TestMethod]
    public void ElTitularNuncaLlevaPorcentajes()
    {
        var filas = new List<PersonaConSuCaso>
        {
            Fila(ConLosPasos(null, 1), "2026-09-01"),
            Fila(ConLosPasos(true, 2), "2026-09-01"),
        };

        var titular = Preparacion.Titular(Preparacion.Recontar(filas, "2026-09-15"));

        StringAssert.StartsWith(titular, "1 de las 2 personas");
        Assert.DoesNotContain("%", titular, "El informe del viejo no lleva porcentajes en ningun sitio.");
    }

    /// <summary>Vigila que con nadie viajado el titular lo dice en vez de poner «0 de las 0».</summary>
    [TestMethod]
    public void SinNadieQueHayaViajadoElTitularLoDice()
    {
        var recuento = Preparacion.Recontar([Fila(ConLosPasos(true, 1), "2026-09-29")], "2026-09-15");
        StringAssert.Contains(Preparacion.Titular(recuento), "Todavía no ha viajado nadie");
    }

    /// <summary>Vigila que un caso que aún no salió lleva nulo en «sin completar» y «sin asignar» si nadie lo lleva.</summary>
    [TestMethod]
    public void SinLaPreparacionCompletaSaleConRayaEnLosCasosQueNoHanSalido()
    {
        var filas = new List<PersonaConSuCaso> { Fila(ConLosPasos(null, 1), "2026-09-29", casoId: 10) };

        var renglones = Preparacion.ResumenPorCaso(filas, "2026-09-15", new Dictionary<long, IReadOnlyList<string>>());

        Assert.HasCount(1, renglones);
        Assert.IsNull(renglones[0].SinCompletar, "En un caso que no ha salido, lo que falta es trabajo por hacer, no un fallo.");
        Assert.AreEqual(Vocabulario.SinAgente, renglones[0].AsignadoA);
    }

    /// <summary>La persona metida en un caso con esa fecha de viaje y ese número.</summary>
    /// <param name="persona">La persona.</param>
    /// <param name="fechaViaje">La fecha de viaje del caso, o nula.</param>
    /// <param name="casoId">El id del caso; cero toma el <c>CasoId</c> de la persona.</param>
    /// <param name="numeroCaso">El número del caso; nulo para probar el caso sin número.</param>
    private static PersonaConSuCaso Fila(Persona persona, string? fechaViaje, long casoId = 0, string? numeroCaso = "CASP2609")
    {
        var idDelCaso = casoId == 0 ? persona.CasoId : casoId;
        return new PersonaConSuCaso(
            persona with { CasoId = idDelCaso },
            new Caso { Id = idDelCaso, NumeroCaso = numeroCaso, FechaViaje = fechaViaje, TemploNombre = "Santo Domingo" });
    }
}
