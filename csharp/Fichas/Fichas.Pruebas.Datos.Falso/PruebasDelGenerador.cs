using Fichas.Contratos.Consultas;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Datos.Falso;

/// <summary>
/// El generador de datos inventados: la misma semilla tiene que dar la misma base.
/// </summary>
/// <remarks>
/// El criterio es del pase: «un generador determinista con semilla». Sin eso, una cifra
/// de rendimiento tomada hoy y otra tomada manana no se pueden comparar, porque no se
/// midieron sobre la misma base — y ese es justo el error que la FASE C0 existe para no
/// repetir.
/// </remarks>
[TestClass]
public sealed class PruebasDelGenerador
{
    /// <summary>El dia en el que se paran todas las pruebas, para que no dependan del calendario.</summary>
    private const string ElDiaDeLaPrueba = "2026-09-04";

    /// <summary>La misma semilla da exactamente la misma base, caso por caso.</summary>
    [TestMethod]
    public void LaMismaSemillaDaLaMismaBase()
    {
        var primera = GeneradorFalso.Generar(200, 1234, new RelojFijo(ElDiaDeLaPrueba));
        var segunda = GeneradorFalso.Generar(200, 1234, new RelojFijo(ElDiaDeLaPrueba));

        Assert.HasCount(primera.Casos.Count, segunda.Casos);
        Assert.HasCount(primera.Personas.Count, segunda.Personas);
        Assert.HasCount(primera.Asignaciones.Count, segunda.Asignaciones);

        foreach (var (id, caso) in primera.Casos)
        {
            Assert.AreEqual(caso, segunda.Casos[id], $"El caso {id} salio distinto con la misma semilla.");
        }
        foreach (var (id, persona) in primera.Personas)
        {
            Assert.AreEqual(persona, segunda.Personas[id], $"La persona {id} salio distinta con la misma semilla.");
        }
    }

    /// <summary>Dos semillas distintas dan bases distintas; si no, la semilla no sirve para nada.</summary>
    [TestMethod]
    public void DosSemillasDistintasDanBasesDistintas()
    {
        var una = GeneradorFalso.Generar(200, 1, new RelojFijo(ElDiaDeLaPrueba));
        var otra = GeneradorFalso.Generar(200, 2, new RelojFijo(ElDiaDeLaPrueba));

        var iguales = una.Casos.Count(par => otra.Casos.TryGetValue(par.Key, out var caso) && caso == par.Value);
        Assert.IsLessThan(una.Casos.Count, iguales, "Con otra semilla la base tiene que salir distinta.");
    }

    /// <summary>Se generan exactamente los casos que se piden, tambien tres mil.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(40)]
    [DataRow(3000)]
    public void SeGeneranLosCasosQueSePiden(int cuantos)
    {
        var almacen = GeneradorFalso.Generar(cuantos, 99, new RelojFijo(ElDiaDeLaPrueba));

        Assert.HasCount(cuantos, almacen.Casos);
    }

    /// <summary>Con 3 000 casos salen del orden de 4 500 personas, como dice el pase de la C1.</summary>
    [TestMethod]
    public void ConTresMilCasosSalenDelOrdenDeCuatroMilQuinientasPersonas()
    {
        var almacen = GeneradorFalso.Generar(3000, 20260904, new RelojFijo(ElDiaDeLaPrueba));

        Assert.IsGreaterThan(4000, almacen.Personas.Count);
        Assert.IsLessThan(9000, almacen.Personas.Count);
    }

    /// <summary>Un numero de casos negativo no lanza: se toma como cero.</summary>
    [TestMethod]
    public void UnNumeroDeCasosNegativoNoTumbaNada()
    {
        var almacen = GeneradorFalso.Generar(-5, 7, new RelojFijo(ElDiaDeLaPrueba));

        Assert.IsEmpty(almacen.Casos);
        Assert.IsNotEmpty(almacen.Companeros, "Los companeros se dan de alta aunque no haya ni un caso.");
    }

    /// <summary>Los ids no se repiten entre tablas: el almacen los reparte de un solo contador.</summary>
    [TestMethod]
    public void LosIdsNoSeRepitenEntreTablas()
    {
        var almacen = GeneradorFalso.Generar(100, 55, new RelojFijo(ElDiaDeLaPrueba));

        var todos = almacen.Casos.Keys
            .Concat(almacen.Personas.Keys)
            .Concat(almacen.Companeros.Keys)
            .Concat(almacen.Asignaciones.Keys)
            .Concat(almacen.Ilegibles.Keys)
            .ToList();

        Assert.HasCount(todos.Count, todos.Distinct().ToList());
    }

    /// <summary>Toda persona apunta a un caso que existe.</summary>
    [TestMethod]
    public void TodaPersonaApuntaAUnCasoQueExiste()
    {
        var almacen = GeneradorFalso.Generar(300, 77, new RelojFijo(ElDiaDeLaPrueba));

        foreach (var persona in almacen.Personas.Values)
        {
            Assert.IsTrue(almacen.Casos.ContainsKey(persona.CasoId),
                $"La persona {persona.Id} apunta al caso {persona.CasoId}, que no existe.");
        }
    }

    /// <summary>Toda asignacion viva es de un companero activo; a uno desactivado no se le asigna.</summary>
    [TestMethod]
    public void TodaAsignacionVivaEsDeUnCompaneroActivo()
    {
        var almacen = GeneradorFalso.Generar(300, 88, new RelojFijo(ElDiaDeLaPrueba));

        foreach (var asignacion in almacen.Asignaciones.Values.Where(a => a.Activa))
        {
            Assert.IsTrue(almacen.Companeros[asignacion.CompaneroId].Activo,
                $"La asignacion {asignacion.Id} esta viva sobre un companero desactivado.");
        }
    }

    /// <summary>El reloj parado hace que las fechas no dependan del dia en que se corra la prueba.</summary>
    [TestMethod]
    public void ConElRelojParadoLaFranjaDeSieteDiasNoSeMueve()
    {
        var servicios = new ServiciosFalsos(500, 4321, new RelojFijo(ElDiaDeLaPrueba));

        var enSieteDias = servicios.Casos.Contar(FiltroDeCasos.Todo with { VentanaDeDias = 7 });
        var otraVez = servicios.Casos.Contar(FiltroDeCasos.Todo with { VentanaDeDias = 7 });

        Assert.AreEqual(enSieteDias, otraVez);
        Assert.IsGreaterThan(0, enSieteDias, "Con 500 casos alguno tiene que viajar en la proxima semana.");
    }

    /// <summary>Un caso archivado tiene fecha de archivado, y uno no archivado no la tiene.</summary>
    [TestMethod]
    public void ArchivadoYSuFechaVanJuntos()
    {
        var almacen = GeneradorFalso.Generar(500, 246, new RelojFijo(ElDiaDeLaPrueba));

        foreach (var caso in almacen.Casos.Values)
        {
            if (caso.Archivado) Assert.IsNotNull(caso.FechaArchivado, $"El caso {caso.Id} esta archivado sin fecha.");
            else Assert.IsNull(caso.FechaArchivado, $"El caso {caso.Id} no esta archivado y tiene fecha de archivado.");
        }
    }

    /// <summary>El MRN se guarda como texto y conserva los ceros de delante.</summary>
    [TestMethod]
    public void ElMrnConservaLosCerosDeDelante()
    {
        var almacen = GeneradorFalso.Generar(2000, 13579, new RelojFijo(ElDiaDeLaPrueba));

        var conCeroDelante = almacen.Personas.Values.Count(p => p.Mrn is not null && p.Mrn.StartsWith('0'));
        Assert.IsGreaterThan(0, conCeroDelante, "Con 2 000 casos tiene que salir algun MRN que empiece por cero.");

        foreach (var persona in almacen.Personas.Values.Where(p => p.Mrn is not null))
        {
            Assert.HasCount(13, persona.Mrn!, $"El MRN «{persona.Mrn}» no tiene la forma 3-4-4.");
        }
    }
}
