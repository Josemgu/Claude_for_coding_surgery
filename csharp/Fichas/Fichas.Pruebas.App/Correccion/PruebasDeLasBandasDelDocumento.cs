using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// De donde saca la pantalla la banda que ilumina en el documento.
/// </summary>
/// <remarks>
/// La banda manda desde <c>procedencia_campo</c>. Cuando ahi no hay nada —hoy es el caso
/// con los datos inventados, porque el generador no escribe procedencias— se pregunta al
/// documento a traves de <c>ILecturaDePdf</c> y <c>IExtraccion</c>.
/// <para>
/// ⚠️ <b>De la lectura del documento se toma la BANDA y nada mas.</b> Ni el valor ni la
/// confianza: un valor traido de ahi seria inventar (regla permanente 1), y una confianza
/// pegada a un dato que no salio de esa lectura seria decir que se leyo algo que no se
/// leyo. La banda es «donde mirar en el papel», que es informacion de sitio, no de dato.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasBandasDelDocumento
{
    private const long CasoDePrueba = 200;
    private const long PersonaDePrueba = 201;

    /// <summary>Monta un caso con ruta de PDF y una persona.</summary>
    private static ServiciosFalsos MontarConUnCasoConPdf()
    {
        var servicios = new ServiciosFalsos(2, 20260904, new RelojFijo("2026-09-04"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-12",
            TemploNombre = "Santo Domingo Dominican Republic",
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-01",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Monta el modelo sobre ese almacen.</summary>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
        => new(servicios.Casos, servicios.Personas, servicios.Procedencia,
               servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

    /// <summary>Sin procedencia guardada, al abrir un caso NINGUN campo trae banda.</summary>
    /// <remarks>
    /// Fija la premisa que se midio en este pase: <c>GeneradorFalso</c> no escribe ni una
    /// fila de <c>procedencia_campo</c>. Si algun dia la escribe, esta prueba lo dice.
    /// </remarks>
    [TestMethod]
    public void SinProcedenciaGuardadaAbrirNoTraeNingunaBanda()
    {
        var modelo = ModeloSobre(MontarConUnCasoConPdf());
        modelo.Cargar(CasoDePrueba);
        Assert.IsFalse(modelo.Campos.Any(campo => campo.Banda is not null));
    }

    /// <summary>Preguntar al documento rellena las bandas que faltaban.</summary>
    [TestMethod]
    public void PreguntarAlDocumentoRellenaLasBandasQueFaltan()
    {
        var modelo = ModeloSobre(MontarConUnCasoConPdf());
        modelo.Cargar(CasoDePrueba);
        modelo.CompletarBandasDesdeElDocumento();

        var conBanda = modelo.Campos.Count(campo => campo.Banda is not null);
        Assert.IsGreaterThan(0, conBanda, "La lectura inventada propone campos con banda; deberian llegar.");
        foreach (var campo in modelo.Campos.Where(c => c.Banda is not null))
        {
            var banda = campo.Banda!.Value;
            Assert.IsGreaterThan(0.0, banda.Ancho);
            Assert.IsGreaterThan(0.0, banda.Alto);
        }
    }

    /// <summary>Preguntar al documento NO cambia ni un valor: eso seria inventar.</summary>
    [TestMethod]
    public void PreguntarAlDocumentoNoCambiaNingunValor()
    {
        var servicios = MontarConUnCasoConPdf();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        var antes = modelo.Campos.Select(campo => campo.ValorGuardado).ToList();

        modelo.CompletarBandasDesdeElDocumento();

        CollectionAssert.AreEqual(antes, modelo.Campos.Select(campo => campo.ValorGuardado).ToList());
        Assert.AreEqual("Castries Branch", servicios.Almacen.Casos[CasoDePrueba].UnidadNombre);
        Assert.AreEqual("055-1111-3853", servicios.Almacen.Personas[PersonaDePrueba].Mrn);
    }

    /// <summary>Preguntar al documento tampoco escribe nada en el almacen.</summary>
    /// <remarks>
    /// Abrir una pantalla no puede cambiar la base. Y menos aun firmar: regla permanente 5.
    /// </remarks>
    [TestMethod]
    public void PreguntarAlDocumentoNoEscribeEnElAlmacen()
    {
        var servicios = MontarConUnCasoConPdf();
        var cuantasProcedencias = servicios.Almacen.Procedencias.Count;
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        modelo.CompletarBandasDesdeElDocumento();

        Assert.HasCount(cuantasProcedencias, servicios.Almacen.Procedencias);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>Una banda que YA venia guardada no se pisa con la del documento.</summary>
    /// <remarks>Manda lo que hay en la base, no lo que se acaba de mirar.</remarks>
    [TestMethod]
    public void UnaBandaGuardadaNoSePisa()
    {
        var servicios = MontarConUnCasoConPdf();
        servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = CasoDePrueba,
            Campo = "fecha_viaje",
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.93,
            BandaX0 = 0.01,
            BandaY0 = 0.02,
            BandaX1 = 0.03,
            BandaY1 = 0.04,
        });

        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        modelo.CompletarBandasDesdeElDocumento();

        var fecha = modelo.Campos.Single(c => c.Campo == "fecha_viaje");
        Assert.IsNotNull(fecha.Banda);
        Assert.AreEqual(0.01, fecha.Banda.Value.X0, 0.0001);
    }

    /// <summary>Un caso sin ruta de PDF no rompe nada: se dice y los campos se corrigen igual.</summary>
    [TestMethod]
    public void UnCasoSinEscaneoAvisaYNoRompeNada()
    {
        var servicios = MontarConUnCasoConPdf();
        servicios.Almacen.Casos[CasoDePrueba] = servicios.Almacen.Casos[CasoDePrueba] with { RutaPdf = null };
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var avisos = modelo.CompletarBandasDesdeElDocumento();
        Assert.HasCount(1, avisos);
        Assert.IsLessThanOrEqualTo(90, avisos[0].Linea.Length);
        Assert.IsFalse(modelo.Campos.Any(campo => campo.Banda is not null));
    }
}
