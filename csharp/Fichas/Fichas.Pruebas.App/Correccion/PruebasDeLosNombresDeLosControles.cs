using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Como se llama cada control de la pantalla para quien no ve la pantalla.
/// </summary>
/// <remarks>
/// Sale de un defecto MEDIDO por QA sobre el paquete publicado: los doce cuadros de texto
/// llegaban con el nombre vacio y con el mismo identificador —el <c>x:Name</c> de la
/// plantilla, <c>_valor</c>—, y los once botones de firma se llamaban todos «Esta bien».
/// Un lector de pantalla no puede decir que campo se esta editando ni que dato se esta
/// dando por bueno, y con cuatro personas en un formulario eso no es una molestia: es
/// firmar la cedula de otra persona.
/// <para>
/// Se prueba aqui, sin ventana, porque el nombre no es cosa del XAML: es un dato del campo.
/// Lo que la ficha hace con el —ponerlo en <c>AutomationProperties</c>— es una linea.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosNombresDeLosControles
{
    /// <summary>El id fijo del caso con dos personas.</summary>
    private const long CasoDePrueba = 300;
    /// <summary>La persona CON nombre leido.</summary>
    private const long PrimeraPersona = 301;
    /// <summary>La persona SIN nombre leido, que es el caso que importa.</summary>
    private const long SegundaPersona = 302;

    /// <summary>Un caso con dos personas: una con nombre leido y otra sin el.</summary>
    /// <remarks>
    /// Las dos hacen falta. Con nombre, el lector puede decir de quien es la cedula; sin el
    /// —que es lo normal cuando el escaneo no se dejo leer— tiene que quedar algo que
    /// distinga, porque dos «Cedula» a secas son el defecto que se esta arreglando.
    /// </remarks>
    private static ServiciosFalsos MontarUnCasoConDosPersonas()
    {
        var servicios = new ServiciosFalsos(2, 20260904, new RelojFijo("2026-09-04"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-12",
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-01",
        };
        servicios.Almacen.Personas[PrimeraPersona] = new Persona
        {
            Id = PrimeraPersona,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };
        servicios.Almacen.Personas[SegundaPersona] = new Persona
        {
            Id = SegundaPersona,
            CasoId = CasoDePrueba,
            Mrn = null,
            Nombre = null,
            FilaFormulario = 2,
            PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Carga el caso y devuelve sus campos.</summary>
    private static IReadOnlyList<CampoEnPantalla> CamposDelCaso()
    {
        var servicios = MontarUnCasoConDosPersonas();
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, servicios.Procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return modelo.Campos;
    }

    /// <summary>Ningun campo se queda sin nombre que decir.</summary>
    [TestMethod]
    public void NingunCampoSeQuedaSinNombreParaElLector()
    {
        foreach (var campo in CamposDelCaso())
            Assert.IsFalse(string.IsNullOrWhiteSpace(campo.NombreParaElLector), $"«{campo.Clave}» sin nombre.");
    }

    /// <summary>Dos campos distintos NUNCA se llaman igual.</summary>
    /// <remarks>Es el defecto entero: nueve controles con el mismo nombre no distinguen nada.</remarks>
    [TestMethod]
    public void DosCamposDistintosNoSeLlamanIgual()
    {
        var nombres = CamposDelCaso().Select(campo => campo.NombreParaElLector).ToList();
        Assert.AreEqual(nombres.Count, nombres.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Dos campos distintos NUNCA comparten identificador.</summary>
    /// <remarks>
    /// QA midio que los doce cuadros llevaban el mismo <c>AutomationId</c>, <c>_valor</c>,
    /// que es el <c>x:Name</c> de la plantilla del repetidor. El identificador que se pone a
    /// mano es la clave del campo, que ya es unica por (tabla, registro, columna).
    /// </remarks>
    [TestMethod]
    public void DosCamposDistintosNoComparteIdentificador()
    {
        var claves = CamposDelCaso().Select(campo => campo.Clave).ToList();
        Assert.AreEqual(claves.Count, claves.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>El campo de una persona con nombre dice de quien es.</summary>
    [TestMethod]
    public void ElCampoDeUnaPersonaConNombreDiceDeQuienEs()
    {
        var cedula = CamposDelCaso().First(campo =>
            campo.Tabla == TablaDeProcedencia.Personas && campo.RegistroId == PrimeraPersona && campo.Etiqueta == "Cédula");

        Assert.Contains("Cédula", cedula.NombreParaElLector, StringComparison.Ordinal);
        Assert.Contains("Maria Anonimo", cedula.NombreParaElLector, StringComparison.Ordinal);
    }

    /// <summary>El campo de una persona SIN nombre dice al menos en que fila venia.</summary>
    /// <remarks>
    /// Es el caso que importa: cuando el escaneo no se deja leer, el nombre viene vacio, y
    /// es justo entonces cuando hay cuatro cuadros llamados «Cedula» y hay que teclear en
    /// el que toca.
    /// </remarks>
    [TestMethod]
    public void ElCampoDeUnaPersonaSinNombreDiceEnQueFilaVenia()
    {
        var cedula = CamposDelCaso().First(campo =>
            campo.Tabla == TablaDeProcedencia.Personas && campo.RegistroId == SegundaPersona && campo.Etiqueta == "Cédula");

        Assert.Contains("Cédula", cedula.NombreParaElLector, StringComparison.Ordinal);
        Assert.Contains("2", cedula.NombreParaElLector, StringComparison.Ordinal);
    }

    /// <summary>Un campo del caso NO se inventa ninguna persona.</summary>
    [TestMethod]
    public void UnCampoDelCasoNoNombraANadie()
    {
        var fecha = CamposDelCaso().First(campo => campo.Etiqueta == "Fecha de viaje");
        Assert.AreEqual("Fecha de viaje", fecha.NombreParaElLector);
    }

    /// <summary>El boton de firma dice QUE dato se da por bueno, no solo «Esta bien».</summary>
    /// <remarks>
    /// Regla permanente 5: firmar es un acto de Miguel. Un boton que no dice sobre que
    /// firma convierte ese acto en una loteria para quien usa lector de pantalla.
    /// </remarks>
    [TestMethod]
    public void ElBotonDeFirmaDiceQueDatoSeDaPorBueno()
    {
        var campos = CamposDelCaso();
        var nombres = campos.Select(campo => campo.NombreDelBotonDeFirma).ToList();

        Assert.AreEqual(nombres.Count, nombres.Distinct(StringComparer.Ordinal).Count());
        foreach (var campo in campos)
        {
            Assert.Contains(campo.NombreParaElLector, campo.NombreDelBotonDeFirma, StringComparison.Ordinal);
            Assert.AreNotEqual("Está bien", campo.NombreDelBotonDeFirma);
        }
    }
}
