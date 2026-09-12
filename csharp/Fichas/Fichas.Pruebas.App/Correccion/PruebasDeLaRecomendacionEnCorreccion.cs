using Fichas.App.Vocabulario;
using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// C17-3 en la pantalla de Correccion: para CADA persona se dice si su recomendacion esta
/// confirmada en el sistema del obispo, o no lo esta.
/// </summary>
/// <remarks>
/// <para>La frase es del dueno, literal: <i>«la recomendación para el templo no está
/// confirmada»</i>. Y la unidad es la persona, tambien con sus palabras: <i>«Es por persona
/// que se revisa la información»</i>.</para>
///
/// <para><b>Por que es una lista aparte y no se pega a «Lo que contestó el compañero».</b>
/// Aquella solo trae a las personas por las que ALGUIEN contesto —y eso es deliberado: una
/// ficha vacia por cada una enterraria las que traen algo—. Pero la pregunta «¿esta
/// confirmada su recomendacion?» hay que contestarla para TODAS, y para la que nadie miro la
/// respuesta es «sin mirar», que no es lo mismo que «no». Sin esta lista, las personas que
/// nadie miro serian invisibles en Correccion, que es justo donde el dueno esta mirando
/// cuando pregunta.</para>
///
/// <para>⛔ <b>Aqui solo se LEE.</b> No hay ni un boton y no se escribe en ninguna fila:
/// contestar las seis preguntas dentro del programa es la FASE C19, y no es esta. La firma
/// de campos sigue siendo de Miguel y nunca automatica (regla permanente 5).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaRecomendacionEnCorreccion
{
    /// <summary>El id fijo del caso con tres personas.</summary>
    private const long CasoDePrueba = 500;
    /// <summary>La companera que contesta las seis; el id es distinto de 1 a proposito, para cazar un «primero de la lista».</summary>
    private const long Sandy = 9;

    /// <summary>Las respuestas con nombre; el analizador de pruebas rechaza el literal.</summary>
    private static readonly bool? Si = true;

    /// <summary>La segunda: alguna de las seis esta marcada que no.</summary>
    private static readonly bool? No = false;

    /// <summary>Un caso con tres personas y ninguna respuesta todavia.</summary>
    private static ServiciosFalsos MontarConTresPersonas()
    {
        var servicios = new ServiciosFalsos(0, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Companeros[Sandy] = new Companero
        {
            Id = Sandy, Nombre = "Sandy", Activo = true, CreadoEn = "2026-08-01",
        };
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "SURB2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-17",
            TemploNombre = "Panama City, Panama",
            RutaPdf = @"C:\pdfs\SURB2609_grupo.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };

        for (var fila = 1; fila <= 3; fila++)
        {
            servicios.Almacen.Personas[CasoDePrueba + fila] = new Persona
            {
                Id = CasoDePrueba + fila,
                CasoId = CasoDePrueba,
                Nombre = $"Persona {fila}",
                Mrn = $"055-1111-390{fila}",
                FilaFormulario = fila,
                PaginaPdf = 1,
            };
        }

        return servicios;
    }

    /// <summary>Deja las seis preguntas de una persona como se le pida.</summary>
    private static void ContestarLasSeis(ServiciosFalsos servicios, long personaId, bool? todas, bool? entrevistas = null)
        => servicios.Almacen.Personas[personaId] = servicios.Almacen.Personas[personaId] with
        {
            PasoPreparacion = todas,
            PasoInformacion = todas,
            PasoCitaDelTemplo = todas,
            PasoAccionesRequeridas = todas,
            PasoEntrevistas = entrevistas ?? todas,
            PasoListoParaElTemplo = todas,
        };

    /// <summary>Monta el modelo con el almacen de procedencia como el de verdad y abre el caso.</summary>
    /// <param name="servicios">La base inventada de la prueba.</param>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
    {
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, new ProcedenciaComoLaDeVerdad(Sandy),
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return modelo;
    }

    /// <summary>
    /// C17-3. Sale UNA linea por persona, tambien por las que nadie miro.
    /// </summary>
    [TestMethod]
    public void SaleUnaLineaPorPersonaIncluidaLaQueNadieMiro()
    {
        var servicios = MontarConTresPersonas();
        ContestarLasSeis(servicios, CasoDePrueba + 1, todas: true);
        ContestarLasSeis(servicios, CasoDePrueba + 2, todas: true, entrevistas: false);
        // La tercera se queda sin contestar.

        var recomendaciones = ModeloSobre(servicios).LaRecomendacionDeCadaPersona;

        Assert.HasCount(3, recomendaciones, "Tres personas, tres líneas: ninguna se cae.");
        Assert.AreEqual(Si, recomendaciones[0].Estado, "La primera tiene las seis en sí.");
        Assert.AreEqual(No, recomendaciones[1].Estado, "La segunda tiene una en no.");
        Assert.IsNull(recomendaciones[2].Estado, "Sin contestar no es «no».");
    }

    /// <summary>
    /// C17-3. Mientras las seis no esten todas en si, la linea dice «recomendación sin
    /// confirmar», con las palabras del dueno.
    /// </summary>
    [TestMethod]
    public void MientrasFalteUnaPreguntaDiceRecomendacionSinConfirmar()
    {
        var servicios = MontarConTresPersonas();
        ContestarLasSeis(servicios, CasoDePrueba + 1, todas: true);
        ContestarLasSeis(servicios, CasoDePrueba + 2, todas: true, entrevistas: false);

        var recomendaciones = ModeloSobre(servicios).LaRecomendacionDeCadaPersona;

        // ⛔ 2026-09-07: la frase decía «lista para viajar · recomendación confirmada» o
        // «sin mirar · recomendación sin confirmar», con TRES de las cuatro palabras que el
        // dueño retiró. Lo que estas pruebas defienden es lo que más importa de aquel criterio
        // y NO cambia: que «sin mirar» y «no lista» sigan siendo cosas distintas, porque dar
        // por lista a una persona de la que faltan preguntas por mirar es lo que manda a
        // alguien al templo con la recomendación mal. La palabra es la misma para las dos; el
        // detalle las separa, y eso es lo que se comprueba.
        StringAssert.StartsWith(recomendaciones[0].Frase, DosEstados.Resuelto, StringComparison.Ordinal);
        StringAssert.StartsWith(recomendaciones[1].Frase, DosEstados.MeFalta, StringComparison.Ordinal);
        StringAssert.StartsWith(recomendaciones[2].Frase, DosEstados.MeFalta, StringComparison.Ordinal);

        StringAssert.Contains(recomendaciones[1].Frase, "se quedó en Entrevistas", StringComparison.Ordinal);
        StringAssert.Contains(recomendaciones[2].Frase, "nadie ha contestado", StringComparison.Ordinal);
        Assert.AreNotEqual(
            recomendaciones[1].Frase,
            recomendaciones[2].Frase,
            "«sin mirar» sigue sin leerse igual que «no lista»: una es la ausencia de respuesta.");

        foreach (var una in recomendaciones)
        {
            Assert.DoesNotContain("\n", una.Frase, "Ni un párrafo: una línea.");
            Assert.DoesNotContain("asignar", una.Frase, "Ésta es la otra pregunta y no se mezclan.");
        }
    }

    /// <summary>Cada linea dice de QUIEN habla, con su nombre y su fila.</summary>
    /// <remarks>
    /// Dos personas del mismo formulario pueden llamarse igual; sin la fila no se sabe de
    /// cual se habla. Es la misma regla que ya tiene «Lo que contestó el compañero».
    /// </remarks>
    [TestMethod]
    public void CadaLineaDiceDeQuienHabla()
    {
        var servicios = MontarConTresPersonas();

        var recomendaciones = ModeloSobre(servicios).LaRecomendacionDeCadaPersona;

        StringAssert.Contains(recomendaciones[0].DeQuien, "Persona 1", StringComparison.Ordinal);
        StringAssert.Contains(recomendaciones[0].DeQuien, "fila 1", StringComparison.Ordinal);
        StringAssert.Contains(recomendaciones[0].ParaElLector, "Persona 1", StringComparison.Ordinal);
    }

    /// <summary>
    /// El resumen del documento cuenta PERSONAS y dice su denominador.
    /// </summary>
    [TestMethod]
    public void ElResumenDelDocumentoCuentaPersonasConSuDenominador()
    {
        var servicios = MontarConTresPersonas();
        ContestarLasSeis(servicios, CasoDePrueba + 1, todas: true);

        var modelo = ModeloSobre(servicios);

        Assert.AreEqual(1, modelo.CuantasPersonasConfirmadas);
        Assert.AreEqual(2, modelo.CuantasPersonasSinConfirmar);
        StringAssert.Contains(modelo.LineaDeLaRecomendacion, "1 de 3", StringComparison.Ordinal);
        StringAssert.Contains(modelo.LineaDeLaRecomendacion, "personas", StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ Leer la recomendacion NO firma ni un campo.
    /// </summary>
    /// <remarks>
    /// Regla permanente 5. Se mide igual que lo pide el criterio C19-8, aunque esta fase ni
    /// siquiera tiene por donde escribir: la cuenta de campos firmados no se mueve.
    /// </remarks>
    [TestMethod]
    public void LeerLaRecomendacionNoFirmaNiUnCampo()
    {
        var servicios = MontarConTresPersonas();
        ContestarLasSeis(servicios, CasoDePrueba + 1, todas: true);

        var procedencia = new ProcedenciaComoLaDeVerdad(Sandy);
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);

        var antes = modelo.CuantosFirmados;
        _ = modelo.LaRecomendacionDeCadaPersona;
        _ = modelo.LineaDeLaRecomendacion;

        Assert.AreEqual(antes, modelo.CuantosFirmados);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Personas, CasoDePrueba + 1));
    }

    /// <summary>Un caso sin ninguna persona leida no revienta y lo dice.</summary>
    [TestMethod]
    public void UnCasoSinNingunaPersonaLeidaLoDiceYNoRevienta()
    {
        var servicios = MontarConTresPersonas();
        foreach (var id in new[] { CasoDePrueba + 1, CasoDePrueba + 2, CasoDePrueba + 3 })
        {
            servicios.Almacen.Personas.Remove(id);
        }

        var modelo = ModeloSobre(servicios);

        Assert.IsEmpty(modelo.LaRecomendacionDeCadaPersona);
        Assert.IsGreaterThan(0, modelo.LineaDeLaRecomendacion.Length, "Un hueco no es una respuesta.");
    }
}
