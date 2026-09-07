using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Firmar un campo que llego vacio y que Miguel acaba de rellenar.
/// </summary>
/// <remarks>
/// Palabras del dueno el 2026-09-05: «solo me permite firmar en comprobar lo que hay;
/// cuando esta en blanco no me deja firmar si coloco la informacion que esta pidiendome».
/// <para>
/// <b>Lo medido con la ventana abierta antes de tocar nada</b>, sobre el paquete publicado
/// y una copia de la base del dueno con sus siete escaneos importados, caso 3
/// (CASP2609_Ana_Prueba), cuyo unico campo de «Por comprobar» es «Templo»:
/// </para>
/// <list type="number">
/// <item>pulsar «Está bien» sin tocar nada: aviso «No se encontro la fila que se queria
/// cambiar.» y NO se firma;</item>
/// <item>escribir el dato y pulsar: aviso «guarde «Templo» antes de darlo por bueno» y NO
/// se firma;</item>
/// <item>pulsar Guardar y volver a pulsar: entonces si.</item>
/// </list>
/// <para>O sea: dos avisos de error y cuatro pulsaciones para dar por bueno un campo.</para>
/// <para>
/// ⛔ Estas pruebas corren contra <see cref="ProcedenciaComoLaDeVerdad"/> y NO contra el
/// almacen falso: el falso crea la fila que le falta y con el nada de esto se ve.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeFirmarLoQueSeAcabaDeEscribir
{
    private const long CasoDePrueba = 100;
    private const long PersonaDePrueba = 101;
    private const long CompaneroDePrueba = 1;

    /// <summary>Lo que se monta en cada prueba: el almacen, la procedencia y el modelo.</summary>
    private sealed record Montaje(
        ServiciosFalsos Servicios, ProcedenciaComoLaDeVerdad Procedencia, ModeloDeCorreccion Modelo);

    /// <summary>
    /// Un caso con el numero ya leido, una persona, y NINGUNA fila de procedencia.
    /// </summary>
    /// <remarks>
    /// Sin filas de procedencia a proposito: es como llega <c>templo_nombre</c> en los nueve
    /// casos de la base del dueno.
    /// </remarks>
    private static Montaje Montar(string? templo = null, string? cedula = null)
    {
        var servicios = new ServiciosFalsos(3, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = templo,
            RutaPdf = @"C:\no-se-abre.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = cedula,
            Nombre = "Ana Prueba",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        var procedencia = new ProcedenciaComoLaDeVerdad(CompaneroDePrueba);
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return new Montaje(servicios, procedencia, modelo);
    }

    /// <summary>El campo del caso que se llama asi.</summary>
    private static CampoEnPantalla CampoDelCaso(ModeloDeCorreccion modelo, string campo)
        => modelo.Campos.Single(c => c.Tabla == TablaDeProcedencia.Casos && c.Campo == campo);

    // ---- el defecto del dueno -------------------------------------------

    /// <summary>
    /// Dado un campo que llego vacio, cuando Miguel escribe el dato y pulsa «Está bien»,
    /// entonces el campo queda firmado con su quien y su cuando.
    /// </summary>
    /// <remarks>
    /// Es el criterio de cierre del defecto 1, y ademas en UNA pulsacion: escribir el dato
    /// y darlo por bueno es un solo acto de Miguel, no dos con un error en medio.
    /// </remarks>
    [TestMethod]
    public void UnCampoVacioQueSeRellenaSeFirmaEnLaMismaPulsacion()
    {
        var (servicios, procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");

        modelo.Teclear(templo.Clave, "Santo Domingo Dominican Republic");
        var resultado = modelo.Firmar(templo, CompaneroDePrueba);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));

        // El valor entro en el almacen, no solo en la pantalla.
        Assert.AreEqual(
            "Santo Domingo Dominican Republic", servicios.Almacen.Casos[CasoDePrueba].TemploNombre);

        // Y al releer la procedencia, la firma esta con quien y cuando.
        var fila = procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
            .Single(p => p.Campo == "templo_nombre");
        Assert.IsTrue(fila.Verificado);
        Assert.AreEqual(CompaneroDePrueba, fila.VerificadoPor);
        Assert.AreEqual("2026-09-05 12:00:00", fila.VerificadoEn);
    }

    /// <summary>
    /// Dado un campo que ya trae valor y NO tiene fila de procedencia, cuando Miguel pulsa
    /// «Está bien» sin tocarlo, entonces se firma.
    /// </summary>
    /// <remarks>
    /// Antes contestaba «No se encontro la fila que se queria cambiar.», que no dice ni de
    /// que campo habla ni que hacer. Es el caso de <c>templo_nombre</c> en los siete
    /// escaneos del dueno.
    /// </remarks>
    [TestMethod]
    public void UnCampoConValorYSinFilaDeProcedenciaSePuedeFirmar()
    {
        var (_, procedencia, modelo) = Montar(templo: "Santo Domingo Dominican Republic");
        var templo = CampoDelCaso(modelo, "templo_nombre");
        Assert.IsNull(templo.Procedencia, "El montaje tiene que empezar SIN fila de procedencia.");

        var resultado = modelo.Firmar(templo, CompaneroDePrueba);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
        Assert.AreEqual(1, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    // ---- lo que NO cambia ------------------------------------------------

    /// <summary>
    /// Dado un campo vacio, cuando se pulsa «Está bien» sin escribir nada, entonces NO se
    /// firma y el aviso dice las dos salidas que hay.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Antes se firmaba.</b> Medido el 2026-09-05: firmar un campo vacio salia bien y
    /// dejaba <c>verificado = 1</c> sobre un hueco. Dar por bueno «no hay nada» es
    /// exactamente lo que hace el boton de «no está en el papel», y ese si lo dice.
    /// </remarks>
    [TestMethod]
    public void UnCampoVacioNoSeFirmaYElAvisoDiceQueHacer()
    {
        var (_, procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");

        var resultado = modelo.Firmar(templo, CompaneroDePrueba);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
        Assert.IsTrue(
            resultado.Avisos.Any(a => (a.Detalle ?? string.Empty).Contains("no está en el papel", StringComparison.Ordinal)),
            "El aviso tiene que nombrar la otra salida: marcar que no está en el papel.");
    }

    /// <summary>Un valor que no cumple su forma NO se firma, se escriba cuando se escriba.</summary>
    [TestMethod]
    public void UnValorQueNoValeSigueSinPoderseFirmar()
    {
        var (servicios, procedencia, modelo) = Montar();
        var cedula = modelo.Campos.Single(c => c.Campo == "mrn");

        modelo.Teclear(cedula.Clave, "123");
        var resultado = modelo.Firmar(cedula, CompaneroDePrueba);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Personas, PersonaDePrueba));
        // Y lo tecleado NO se guardo por la puerta de la firma: firmar no es guardar a la fuerza.
        Assert.IsNull(servicios.Almacen.Personas[PersonaDePrueba].Mrn);
    }

    /// <summary>Sin companero no se firma: la firma dice QUIEN, o no es una firma.</summary>
    [TestMethod]
    public void SinCompaneroNoSeFirmaNada()
    {
        var (_, procedencia, modelo) = Montar(templo: "Santo Domingo Dominican Republic");
        var templo = CampoDelCaso(modelo, "templo_nombre");

        var resultado = modelo.Firmar(templo, companeroId: 999_999);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>Un campo firmado cuyo valor cambia despues PIERDE la firma.</summary>
    /// <remarks>
    /// Regla permanente 5: una firma dice «di por bueno ESE valor». Con otro valor debajo
    /// deja de valer, y el caso vuelve a salir pendiente.
    /// </remarks>
    [TestMethod]
    public void CambiarElValorDespuesRetiraLaFirma()
    {
        var (_, procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");

        modelo.Teclear(templo.Clave, "Santo Domingo Dominican Republic");
        Assert.IsTrue(modelo.Firmar(templo, CompaneroDePrueba).SeEscribio);
        Assert.AreEqual(1, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));

        var guardado = modelo.Guardar(new Dictionary<string, string?>
        {
            [templo.Clave] = "Caracas Venezuela",
        });

        Assert.AreEqual(1, guardado.FirmasRetiradas);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>Ni cargar ni guardar firman nada. Regla permanente 5.</summary>
    [TestMethod]
    public void NiCargarNiGuardarFirmanNada()
    {
        var (_, procedencia, modelo) = Montar();

        Assert.AreEqual(0, modelo.CuantosFirmados);
        modelo.Guardar(new Dictionary<string, string?>
        {
            [CampoDelCaso(modelo, "templo_nombre").Clave] = "Santo Domingo Dominican Republic",
        });
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    // ---- lo firmado deja de estar «por comprobar» ------------------------

    /// <summary>Un campo firmado ya no cuenta como pendiente de comprobar.</summary>
    /// <remarks>
    /// ⚠️ Antes seguia contando: el pie decia «Quedan 1 campos por comprobar · 1 dados por
    /// buenos» sobre el MISMO campo. Un contador que no baja al trabajar no sirve de guia.
    /// </remarks>
    [TestMethod]
    public void UnCampoFirmadoDejaDeEstarPorComprobar()
    {
        var (_, _, modelo) = Montar(templo: "Santo Domingo Dominican Republic");
        var templo = CampoDelCaso(modelo, "templo_nombre");
        Assert.IsTrue(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));

        modelo.Firmar(templo, CompaneroDePrueba);

        Assert.IsFalse(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));
    }
}
