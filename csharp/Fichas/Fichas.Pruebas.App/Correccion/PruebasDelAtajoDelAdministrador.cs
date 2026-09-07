using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// El atajo del dueno: dar un caso por completo sin haber firmado campo por campo.
/// </summary>
/// <remarks>
/// Sus palabras: <i>«Yo solo estoy para verificar que todo este correcto con el sistema y
/// los PDF. Yo puedo completarlos tambien, los paquetes, desde el sistema sin pasar la
/// verificacion, y cuando pase eso debe decir "el administrador lo hizo"»</i>.
/// <para>
/// ⛔ <b>Esto NO es un automatismo y NO rompe la regla permanente 5.</b> Lo pulsa el, y no
/// pone <c>verificado = 1</c> en ningun campo: escribe el ESTADO de la recomendacion, que
/// es la otra cosa que la regla 5 separa desde que el dueno la preciso el 2026-09-03. Que
/// no firma nada no se promete: se mide, y es la prueba
/// <see cref="ElAtajoNoFirmaNiUnSoloCampo"/>.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelAtajoDelAdministrador
{
    private const long CasoDePrueba = 400;
    private const long PersonaDePrueba = 401;
    private const long Sandy = 1;
    private const long ElAdmin = 2;

    /// <summary>Lo que monta cada prueba.</summary>
    private sealed record Montaje(ServiciosFalsos Servicios, ModeloDeCorreccion Modelo);

    /// <summary>
    /// Un caso con cuatro campos vacios, una persona, y la lista de companeros que se pida.
    /// </summary>
    /// <remarks>
    /// Los campos van VACIOS a proposito: el atajo tiene que servir justo en el documento
    /// que el programa no pudo cerrar, que es donde el dueno dice que no quiere pasar por la
    /// verificacion campo por campo.
    /// </remarks>
    private static Montaje Montar(bool conAdministrador)
    {
        var servicios = new ServiciosFalsos(0, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Companeros.Clear();
        servicios.Almacen.Companeros[Sandy] = new Companero
        {
            Id = Sandy,
            Nombre = "Sandy",
            Activo = true,
            Rol = RolDeCompanero.Companero,
            CreadoEn = "2026-09-01",
        };
        if (conAdministrador)
        {
            servicios.Almacen.Companeros[ElAdmin] = new Companero
            {
                Id = ElAdmin,
                Nombre = "Miguel",
                Activo = true,
                Rol = RolDeCompanero.Administrador,
                CreadoEn = "2026-09-01",
            };
        }

        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = null,
            FechaViaje = null,
            TemploNombre = null,
            RutaPdf = @"C:\no-se-abre.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = null,
            Nombre = "Ana Prueba",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, servicios.Procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return new Montaje(servicios, modelo);
    }

    // ---- 1. lo pulsa el, y el caso queda completo ------------------------

    /// <summary>
    /// Dado un caso con campos sin firmar y un administrador dado de alta, cuando el pulsa
    /// el atajo, entonces al releer la base el caso esta completo.
    /// </summary>
    [TestMethod]
    public void DarPorCompletoDejaElCasoCompleto()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);

        var resultado = modelo.DarPorCompletoComoAdministrador();

        Assert.IsTrue(resultado.SeEscribio);
        var releido = servicios.Casos.Obtener(CasoDePrueba);
        Assert.IsNotNull(releido);
        Assert.AreEqual(EstadoDeRecomendacion.Completa, releido.Estado);
    }

    /// <summary>Y queda escrito CON SU NOMBRE y con el origen que dice que fue el.</summary>
    /// <remarks>
    /// ⚠️ El origen se compara contra el TEXTO literal y no contra
    /// <see cref="ElAdministrador.Origen"/>: comparar la constante consigo misma pasaria en
    /// verde con una errata dentro. Son las palabras del dueno —«cuando pase eso debe decir
    /// "el administrador lo hizo"»— y acaban en su base y en lo que lean los reportes, asi
    /// que cambiarlas deja sin reconocer todo lo ya marcado.
    /// </remarks>
    [TestMethod]
    public void QuedaEscritoQueFueElAdministradorYQuienEs()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);
        modelo.DarPorCompletoComoAdministrador();

        var releido = servicios.Casos.Obtener(CasoDePrueba);
        Assert.AreEqual("el administrador lo hizo", releido?.EstadoMarcadoOrigen);
        Assert.AreEqual(ElAdmin, releido?.EstadoMarcadoPor);
        Assert.AreEqual("2026-09-05 12:00:00", releido?.EstadoMarcadoEn);
    }

    /// <summary>Y en la pantalla se lee «el administrador lo hizo».</summary>
    [TestMethod]
    public void EnLaPantallaSeLeeQueElAdministradorLoHizo()
    {
        var (_, modelo) = Montar(conAdministrador: true);
        modelo.DarPorCompletoComoAdministrador();

        Assert.Contains("el administrador lo hizo", modelo.ComoQuedoLaMarca, StringComparison.Ordinal);
        Assert.Contains("Miguel", modelo.ComoQuedoLaMarca, StringComparison.Ordinal);
    }

    // ---- 2. y NO firma nada ----------------------------------------------

    /// <summary>
    /// El atajo no pone <c>verificado</c> en ni un campo: la cuenta de antes y la de despues
    /// son la misma.
    /// </summary>
    /// <remarks>
    /// Es el criterio de ADR-0005 §6.2, y va medido y no prometido. Con los campos vacios
    /// que monta la prueba, ese numero es cero antes y cero despues.
    /// </remarks>
    [TestMethod]
    public void ElAtajoNoFirmaNiUnSoloCampo()
    {
        var (_, modelo) = Montar(conAdministrador: true);
        var firmadosAntes = modelo.CuantosFirmados;

        modelo.DarPorCompletoComoAdministrador();

        Assert.AreEqual(0, firmadosAntes);
        Assert.AreEqual(firmadosAntes, modelo.CuantosFirmados);
    }

    /// <summary>Y ninguna fila de procedencia queda verificada, mirando el almacen por dentro.</summary>
    /// <remarks>
    /// La cuenta de arriba pasa por el modelo; esta mira las filas directamente, que es
    /// donde vive <c>verificado</c>. Las dos hacen falta: un contador de pantalla que dijera
    /// otra cosa que el almacen seria justo la mentira que hay que cazar.
    /// </remarks>
    [TestMethod]
    public void NingunaFilaDeProcedenciaQuedaVerificada()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);
        modelo.DarPorCompletoComoAdministrador();

        var verificadas = servicios.Almacen.Procedencias.Values.Count(fila => fila.Verificado);
        Assert.AreEqual(0, verificadas);
    }

    /// <summary>Tampoco toca lo que dijo el companero: son dos registros distintos.</summary>
    /// <remarks>
    /// <c>estado_del_companero</c> es lo que la migracion 14 desdoblo para que la correccion
    /// de Miguel no se llevara por delante el nombre del agente. El atajo escribe el estado
    /// vigente, y ahi se queda.
    /// </remarks>
    [TestMethod]
    public void ElAtajoNoTocaLoQueDijoElCompanero()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);
        servicios.Almacen.Casos[CasoDePrueba] = servicios.Almacen.Casos[CasoDePrueba] with
        {
            EstadoDelCompanero = "no_completa",
            EstadoDelCompaneroPor = Sandy,
            EstadoDelCompaneroEn = "2026-09-04T08:00:00",
        };
        modelo.Cargar(CasoDePrueba);

        modelo.DarPorCompletoComoAdministrador();

        var releido = servicios.Casos.Obtener(CasoDePrueba);
        Assert.AreEqual("no_completa", releido?.EstadoDelCompanero);
        Assert.AreEqual(Sandy, releido?.EstadoDelCompaneroPor);
    }

    // ---- 3. nunca se firma a nombre de quien no fue ----------------------

    /// <summary>
    /// Dada la base del dueno tal cual —solo Sandy, sin administrador—, cuando se pulsa el
    /// atajo, entonces NO se escribe nada y NO se marca a nombre de Sandy.
    /// </summary>
    /// <remarks>
    /// Es el defecto de ADR-0005 §6.3, y aqui esta fijado: si esto se rompe, el reporte diria
    /// que Sandy completo algo que no toco.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorNoSeMarcaNadaANombreDeSandy()
    {
        var (servicios, modelo) = Montar(conAdministrador: false);

        var resultado = modelo.DarPorCompletoComoAdministrador();

        Assert.IsFalse(resultado.SeEscribio);
        var releido = servicios.Casos.Obtener(CasoDePrueba);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, releido?.Estado);
        Assert.IsNull(releido?.EstadoMarcadoPor);
        Assert.IsNull(releido?.EstadoMarcadoOrigen);
    }

    /// <summary>Y se dice por que, en UNA linea, sin cuadro modal.</summary>
    [TestMethod]
    public void SinAdministradorSeDicePorQueEnUnaLinea()
    {
        var (_, modelo) = Montar(conAdministrador: false);

        var resultado = modelo.DarPorCompletoComoAdministrador();

        Assert.HasCount(1, resultado.Avisos);
        Assert.DoesNotContain("\n", resultado.Avisos[0].Linea, StringComparison.Ordinal);
        Assert.DoesNotContain("Sandy", resultado.Avisos[0].Linea, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sin administrador el boton NO esta: no se ofrece lo que no se puede hacer.</summary>
    [TestMethod]
    public void SinAdministradorElAtajoNoSeOfrece()
    {
        Assert.IsFalse(Montar(conAdministrador: false).Modelo.HayAtajoDeAdministrador);
        Assert.IsTrue(Montar(conAdministrador: true).Modelo.HayAtajoDeAdministrador);
    }

    /// <summary>
    /// Y en el sitio del boton se lee POR QUE no esta, que es la otra mitad.
    /// </summary>
    /// <remarks>
    /// ⛔ Sale de una medicion con la ventana abierta el 2026-09-05 sobre el paquete
    /// publicado: escondido el boton, el hueco quedaba <b>en blanco</b> y no habia forma de
    /// enterarse de que faltaba un alta. ADR-0005 §6.3 pide las dos cosas —«el boton no esta
    /// y se dice por que»— y solo estaba la primera.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorSeLeeEnElSitioDelBotonPorQueNoEsta()
    {
        var sinEl = Montar(conAdministrador: false).Modelo.PorQueNoHayAtajo;

        Assert.IsFalse(string.IsNullOrWhiteSpace(sinEl));
        Assert.Contains("administrador", sinEl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sandy", sinEl, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Con administrador NO se lee ningun motivo: o esta el boton o esta el motivo.</summary>
    [TestMethod]
    public void ConAdministradorNoSeLeeNingunMotivo()
        => Assert.AreEqual(string.Empty, Montar(conAdministrador: true).Modelo.PorQueNoHayAtajo);

    // ---- 4. los bordes ----------------------------------------------------

    /// <summary>Sin ningun caso abierto no se escribe nada, y se dice.</summary>
    [TestMethod]
    public void SinCasoAbiertoNoSeEscribeNada()
    {
        var servicios = new ServiciosFalsos(0, 20260905, new RelojFijo("2026-09-05"));
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, servicios.Procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

        var resultado = modelo.DarPorCompletoComoAdministrador();

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos);
    }

    /// <summary>Un caso completado por el atajo se distingue de uno completado por el Excel.</summary>
    /// <remarks>
    /// Se comprueba de punta a punta y no solo en el texto: se marca un caso por cada
    /// camino, se releen los dos de la base, y las dos frases salen distintas.
    /// </remarks>
    [TestMethod]
    public void UnCasoDelExcelSigueDistinguiendoseDelDelAdministrador()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);
        modelo.DarPorCompletoComoAdministrador();

        const long OtroCaso = 402;
        servicios.Almacen.Casos[OtroCaso] = new Caso { Id = OtroCaso, NumeroCaso = "CASP2609", CreadoEn = "2026-09-05" };
        servicios.Casos.MarcarEstadoDelCompanero(
            OtroCaso, EstadoDeRecomendacion.Completa, MotivoDeNoCompletar.SinMotivo, Sandy,
            @"C:\Users\josem\Documents\Fichas\paquetes\vuelta-Sandy.xlsx");

        var delAdmin = TextoDeLaMarcaDelEstado.Componer(servicios.Casos.Obtener(CasoDePrueba)!, "Miguel");
        var delExcel = TextoDeLaMarcaDelEstado.Componer(servicios.Casos.Obtener(OtroCaso)!, "Sandy");

        Assert.AreNotEqual(delAdmin, delExcel);
        Assert.Contains("el administrador lo hizo", delAdmin, StringComparison.Ordinal);
        Assert.DoesNotContain("administrador", delExcel, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Y la lista lo dice sin abrir el documento.</summary>
    /// <remarks>
    /// El dueno pidio que se vea «en el documento, en la lista, y en lo que lean los
    /// reportes». Esta es la lista de la pantalla de correccion, que es la que se toca aqui.
    /// </remarks>
    [TestMethod]
    public void LaListaLoDiceSinAbrirElDocumento()
    {
        var (servicios, modelo) = Montar(conAdministrador: true);
        modelo.DarPorCompletoComoAdministrador();

        var linea = TextoDelDesplegable.Componer(
            servicios.Casos.Obtener(CasoDePrueba)!, "Ana Prueba", 1, null, 0, "Miguel");

        Assert.Contains("el administrador lo hizo", linea, StringComparison.Ordinal);
    }
}
