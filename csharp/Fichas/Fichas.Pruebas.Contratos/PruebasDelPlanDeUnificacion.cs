using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.Contratos;

/// <summary>
/// Lo que se le dice al dueño antes y después de unificar un duplicado con su original.
/// </summary>
/// <remarks>
/// <para>El criterio sale del pase del 2026-09-14 y no del código: la pregunta tiene que decir
/// qué va a pasar y que el duplicado no vuelve; el acuse tiene que decir «Unificado con
/// CASP2609_… hoja 1: 1 persona y 2 campos pasaron al original; el duplicado se borró (copia
/// en …)»; y la línea de <c>fichas.log</c> no lleva nombres ni cédulas.</para>
///
/// <para>Se prueban aquí, en Contratos, porque son textos que salen de un registro sin
/// ventana y sin base: si el registro los compone bien, la pantalla solo los enseña.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelPlanDeUnificacion
{
    /// <summary>Un plan con permiso, como el que devolvería la base en el escenario del pase.</summary>
    private static PlanDeUnificacion UnPlanConPermiso() => new()
    {
        DuplicadoId = 15,
        OriginalId = 12,
        DuplicadoDicho = "CASP2609_Ana_Prueba.pdf hoja 2",
        OriginalDicho = "CASP2609_Ana_Prueba.pdf hoja 1",
        PersonasQuePasan = [new PersonaQuePasaAlOriginal(31, "Carla Prueba", "1234567890003", 2)],
        PersonasQueYaEstaban = 2,
        CamposQuePasan =
        [
            new CampoQuePasaAlOriginal("fecha_viaje", "fecha de viaje", "2026-10-08"),
            new CampoQuePasaAlOriginal("templo_nombre", "templo", "Santo Domingo"),
        ],
        AsignacionesVivasQueSeRetiran = 1,
        Hoy = "2026-09-14",
        RutaDeLaCopia = @"C:\Users\miguel\Documents\Fichas\fichas-antes-de-borrar-20260914-101500.db",
        SePuedeUnificar = true,
    };

    /// <summary>El título nombra el archivo y la hoja del original, que es a lo que se une.</summary>
    [TestMethod]
    public void ElTituloNombraAlOriginalConSuHoja()
    {
        var plan = UnPlanConPermiso();

        Assert.AreEqual("Unificar con CASP2609_Ana_Prueba.pdf hoja 1", plan.Titulo);
        Assert.DoesNotContain("\n", plan.Titulo);
    }

    /// <summary>El botón que unifica dice lo que hace, y el que no, que no.</summary>
    [TestMethod]
    public void LosDosBotonesDicenLoQueHacen()
    {
        var plan = UnPlanConPermiso();

        Assert.AreEqual("Unificar y borrar el duplicado", plan.TextoDelBoton);
        Assert.AreNotEqual(PlanDeUnificacion.TextoDelBotonQueNoUnifica, plan.TextoDelBoton, "Los dos botones no pueden decir lo mismo.");
        Assert.StartsWith("No ", PlanDeUnificacion.TextoDelBotonQueNoUnifica, "El que no unifica empieza por «No».");
    }

    /// <summary>
    /// La pregunta dice qué pasa al original, qué se retira, dónde quedó la copia y que el
    /// duplicado no vuelve.
    /// </summary>
    [TestMethod]
    public void LaPreguntaDiceQueVaAPasarYQueElDuplicadoNoVuelve()
    {
        var plan = UnPlanConPermiso();
        var pregunta = plan.Pregunta;

        Console.WriteLine(pregunta);

        Assert.Contains("CASP2609_Ana_Prueba.pdf hoja 2", pregunta, "Dice cuál es el duplicado.");
        Assert.Contains("CASP2609_Ana_Prueba.pdf hoja 1", pregunta, "Dice cuál es el original.");
        Assert.Contains("1 persona", pregunta, "Cuenta las personas que pasan.");
        Assert.Contains("Carla Prueba", pregunta, "Nombra a la persona que pasa.");
        Assert.Contains("2 personas", pregunta, "Cuenta las que ya estaban y se van con el duplicado.");
        Assert.Contains("fecha de viaje", pregunta, "Nombra los campos que pasan.");
        Assert.Contains("2026-10-08", pregunta, "Con su valor, para que se vea qué se va a escribir.");
        Assert.Contains("templo", pregunta);
        Assert.Contains("1 asignación", pregunta, "Dice que se retira la asignación viva.");
        Assert.Contains(plan.RutaDeLaCopia!, pregunta, "Dice dónde quedó la copia.");
        Assert.Contains("no vuelve", pregunta, "Dice que el duplicado no vuelve.");
    }

    /// <summary>Sin nada que pasar, la pregunta lo dice en vez de listar vacíos.</summary>
    [TestMethod]
    public void SinNadaQuePasarLaPreguntaLoDice()
    {
        var plan = UnPlanConPermiso() with
        {
            PersonasQuePasan = [],
            CamposQuePasan = [],
            AsignacionesVivasQueSeRetiran = 0,
        };

        var pregunta = plan.Pregunta;

        Assert.Contains("nada que pasar", pregunta);
        Assert.DoesNotContain("0 personas pasan", pregunta);
        Assert.DoesNotContain("asignación", pregunta, "Sin asignaciones vivas no se habla de ellas.");
    }

    /// <summary>Con el original archivado, la pregunta lo advierte: lo que pase quedará archivado.</summary>
    [TestMethod]
    public void ConElOriginalArchivadoLaPreguntaLoAdvierte()
    {
        var plan = UnPlanConPermiso() with { OriginalArchivado = true };

        Assert.Contains("archivado", plan.Pregunta);
        Assert.DoesNotContain("archivado", UnPlanConPermiso().Pregunta);
    }

    /// <summary>Un plan que no sale adelante no tiene permiso ni nada que listar.</summary>
    [TestMethod]
    public void UnPlanQueNoSePuedeLlevaSuMotivo()
    {
        var plan = PlanDeUnificacion.NoSePuede(15, null, Aviso.Problema("El original ya no está en la base.", "duplicado_de"));

        Assert.IsFalse(plan.SePuedeUnificar);
        Assert.AreEqual(15, plan.DuplicadoId);
        Assert.IsNull(plan.RutaDeLaCopia);
        Assert.HasCount(1, plan.Avisos);
        Assert.IsEmpty(plan.PersonasQuePasan);
        Assert.IsEmpty(plan.CamposQuePasan);
    }

    /// <summary>Los cinco campos del caso que se pueden pasar son los cinco de siempre, con su rótulo.</summary>
    [TestMethod]
    public void LosCincoCamposSonLosCincoDeSiempre()
    {
        var columnas = PlanDeUnificacion.LosCincoCampos.Select(c => c.Columna).ToList();

        CollectionAssert.AreEqual(
            new[] { "numero_caso", "unidad_numero", "unidad_nombre", "fecha_viaje", "templo_nombre" },
            columnas);
        Assert.IsTrue(PlanDeUnificacion.LosCincoCampos.All(c => c.Rotulo.Length > 0), "Cada uno con su rótulo en español.");
    }

    /// <summary>El acuse es la línea del pase: cuántas personas y campos pasaron, y dónde está la copia.</summary>
    [TestMethod]
    public void ElAcuseDiceCuantoPasoYDondeEstaLaCopia()
    {
        var resultado = new ResultadoDeUnificacion
        {
            SeUnifico = true,
            OriginalId = 12,
            DuplicadoId = 15,
            OriginalDicho = "CASP2609_Ana_Prueba.pdf hoja 1",
            PersonasQuePasaron = 1,
            CamposQuePasaron = 2,
            AsignacionesRetiradas = 1,
            RutaDeLaCopia = @"C:\datos\fichas-antes-de-borrar-20260914-101500.db",
            Borradas = [new ConteoDeTabla("casos", 1, "documento", "documentos")],
        };

        Assert.AreEqual(
            "Unificado con CASP2609_Ana_Prueba.pdf hoja 1: 1 persona y 2 campos pasaron al original; "
            + @"el duplicado se borró (copia en C:\datos\fichas-antes-de-borrar-20260914-101500.db).",
            resultado.Linea);
    }

    /// <summary>El acuse concuerda en número: «2 personas y 1 campo», y «nada pasó» cuando nada pasó.</summary>
    [TestMethod]
    public void ElAcuseConcuerdaEnNumero()
    {
        var dos = new ResultadoDeUnificacion
        {
            SeUnifico = true, OriginalDicho = "A.pdf", PersonasQuePasaron = 2, CamposQuePasaron = 1, RutaDeLaCopia = "c.db",
        };
        var nada = dos with { PersonasQuePasaron = 0, CamposQuePasaron = 0 };

        Assert.Contains("2 personas y 1 campo pasaron", dos.Linea);
        Assert.Contains("nada tenía que pasar al original", nada.Linea);
    }

    /// <summary>Lo que no se unificó lo dice, con el motivo delante en los avisos.</summary>
    [TestMethod]
    public void LoQueNoSeUnificoLoDice()
    {
        var resultado = ResultadoDeUnificacion.NoSeUnifico("c.db", Aviso.Problema("La base lo rechazó.", string.Empty));

        Assert.IsFalse(resultado.SeUnifico);
        Assert.AreEqual("No se unificó nada.", resultado.Linea);
        Assert.HasCount(1, resultado.Avisos);
    }

    /// <summary>La línea de <c>fichas.log</c> lleva ids y cifras, nunca nombres ni cédulas.</summary>
    [TestMethod]
    public void LaLineaDelRegistroNoLlevaNombresNiCedulas()
    {
        var resultado = new ResultadoDeUnificacion
        {
            SeUnifico = true,
            OriginalId = 12,
            DuplicadoId = 15,
            OriginalDicho = "CASP2609_Ana_Prueba.pdf hoja 1",
            DuplicadoDicho = "CASP2609_Ana_Prueba.pdf hoja 2",
            PersonasQuePasaron = 1,
            CamposQuePasaron = 2,
            AsignacionesRetiradas = 1,
            RutaDeLaCopia = @"C:\datos\copia.db",
            Borradas = [new ConteoDeTabla("casos", 1, "documento", "documentos"), new ConteoDeTabla("personas", 2, "persona", "personas")],
        };

        var linea = resultado.LineaDelRegistro;
        Console.WriteLine(linea);

        Assert.StartsWith("UNIFICADO", linea);
        Assert.Contains("15", linea);
        Assert.Contains("12", linea);
        Assert.Contains("personas=1", linea);
        Assert.Contains("campos=2", linea);
        Assert.Contains("asignaciones_retiradas=1", linea);
        Assert.Contains("casos=1", linea);
        Assert.Contains(@"C:\datos\copia.db", linea);
        Assert.DoesNotContain("Ana", linea, "Sin nombres: el cuaderno de tiempos no es sitio para datos de personas.");
    }
}
