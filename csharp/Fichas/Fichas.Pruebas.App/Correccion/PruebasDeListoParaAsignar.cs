using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Quien dice «listo para asignar», y por que NO es lo mismo que la firma de Miguel.
/// </summary>
/// <remarks>
/// El 2026-09-05 se programo que el pie lo dijera <b>despues de que Miguel firmara todos los
/// campos</b>. <b>El dueno lo corrigio en el acto</b>, y estas pruebas son su correccion:
/// <para>
/// <i>«Yo doy ese veredicto cuando el sistema no completa todos los campos. Si el sistema
/// escanea y verifica todos los campos sin mi intervencion, debe decir "listo para asignar":
/// el sistema lleno todos los campos y a mi solo me deberia dejar verificarlo.»</i>
/// </para>
/// <para>
/// Lo que estaba mal: poner su firma como peaje le obliga a pulsar campo por campo en
/// documentos que el programa leyo enteros y bien. Con 3 000 documentos, eso son miles de
/// pulsaciones para confirmar lo que ya estaba bien.
/// </para>
/// <list type="table">
/// <item><term>«listo para asignar»</term><description>el programa lleno todos los campos:
/// ninguno vacio, ninguno dudoso, ninguno tachado sin corregir. <b>Lo dice el programa,
/// solo.</b></description></item>
/// <item><term><c>verificado = 1</c></term><description>Miguel miro ese campo y responde por
/// el. <b>Lo pone Miguel, siempre, y nunca es automatico</b> (regla permanente 5).</description></item>
/// </list>
/// <para>
/// Se usa <see cref="ProcedenciaComoLaDeVerdad"/> y no el almacen falso: el falso <b>crea</b>
/// la fila que le falta al firmar, y con el una prueba sobre un campo sin procedencia sale
/// verde estando mal.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeListoParaAsignar
{
    private const long CasoDePrueba = 300;
    private const long PersonaDePrueba = 301;
    private const long CompaneroDePrueba = 1;

    /// <summary>Cuantos campos dibuja la pantalla de este caso: cinco del caso y dos de la persona.</summary>
    private const int CamposDelCaso = 7;

    // ---- el montaje ------------------------------------------------------

    /// <summary>
    /// Un caso tal como lo deja la importacion de los siete escaneos reales del dueno.
    /// </summary>
    /// <remarks>
    /// Los origenes y las confianzas NO son inventados: son los medidos el 2026-09-05 sobre
    /// esos siete documentos, importados con la ventana abierta en una carpeta de datos
    /// propia. <c>numero_caso</c> y <c>fecha_viaje</c> vienen de una anotacion del PDF con
    /// confianza 1,00; <c>unidad_numero</c> y <c>unidad_nombre</c> del OCR con 0,99.
    /// </remarks>
    private static (ProcedenciaComoLaDeVerdad Procedencia, ModeloDeCorreccion Modelo) MontarLeidoEntero(
        double confianzaDelTemplo = 0.99,
        bool temploTachado = false,
        string? temploLeido = "Panama City, Panama")
    {
        var servicios = new ServiciosFalsos(3, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = temploLeido,
            RutaPdf = @"C:\no-se-abre.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Ana Prueba",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        var procedencia = new ProcedenciaComoLaDeVerdad(CompaneroDePrueba);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "numero_caso", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "fecha_viaje", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_numero", OrigenDeCampo.Ocr, 0.99);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_nombre", OrigenDeCampo.Ocr, 0.99);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "templo_nombre",
               OrigenDeCampo.Ocr, confianzaDelTemplo, temploTachado);
        Anotar(procedencia, TablaDeProcedencia.Personas, PersonaDePrueba, "nombre", OrigenDeCampo.Ocr, 0.97);
        Anotar(procedencia, TablaDeProcedencia.Personas, PersonaDePrueba, "mrn", OrigenDeCampo.Ocr, 0.95);

        return (procedencia, Cargar(servicios, procedencia));
    }

    /// <summary>El mismo caso, pero con un campo que el programa NO pudo leer.</summary>
    private static (ProcedenciaComoLaDeVerdad Procedencia, ModeloDeCorreccion Modelo) MontarConUnHueco()
    {
        var servicios = new ServiciosFalsos(3, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = null,
            RutaPdf = @"C:\no-se-abre.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Ana Prueba",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        var procedencia = new ProcedenciaComoLaDeVerdad(CompaneroDePrueba);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "numero_caso", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "fecha_viaje", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_numero", OrigenDeCampo.Ocr, 0.99);
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_nombre", OrigenDeCampo.Ocr, 0.99);
        // El templo entra con su fila y SIN valor: es lo que deja «no se pudo leer».
        Anotar(procedencia, TablaDeProcedencia.Casos, CasoDePrueba, "templo_nombre", OrigenDeCampo.Vacio, null);
        Anotar(procedencia, TablaDeProcedencia.Personas, PersonaDePrueba, "nombre", OrigenDeCampo.Ocr, 0.97);
        Anotar(procedencia, TablaDeProcedencia.Personas, PersonaDePrueba, "mrn", OrigenDeCampo.Ocr, 0.95);

        return (procedencia, Cargar(servicios, procedencia));
    }

    private static ModeloDeCorreccion Cargar(ServiciosFalsos servicios, ProcedenciaComoLaDeVerdad procedencia)
    {
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return modelo;
    }

    private static void Anotar(
        ProcedenciaComoLaDeVerdad procedencia, TablaDeProcedencia tabla, long registroId,
        string campo, OrigenDeCampo origen, double? confianza, bool tachado = false)
        => procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = origen,
            Confianza = confianza,
            AnuladoPorTachon = tachado,
        });

    // ---- el criterio de cierre -------------------------------------------

    /// <summary>
    /// Dado un documento que el programa leyo entero, cuando Miguel lo abre y NO toca nada,
    /// entonces ya sale como listo para asignar.
    /// </summary>
    /// <remarks>
    /// Es la frase del dueno hecha prueba: «si el sistema escanea y verifica todos los
    /// campos sin mi intervencion, debe decir "listo para asignar"». Se comprueba
    /// <b>antes de Guardar</b> a proposito: si hiciera falta pulsar algo, ese algo seria
    /// otra vez un peaje.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoLeidoEnteroYaEstaListoSinQueMiguelToqueNada()
    {
        var (_, modelo) = MontarLeidoEntero();

        Assert.IsTrue(modelo.ListoParaAsignar, ComoEstaCadaCampo(modelo));
        Assert.AreEqual(0, modelo.CamposQueLeFaltan);
        Assert.AreEqual(0, modelo.CuantosFirmados, "Nadie ha firmado nada, y aun asi esta listo.");
    }

    /// <summary>
    /// Y estar listo NO firma ni un campo: <c>verificado</c> sigue en cero en el almacen.
    /// </summary>
    /// <remarks>
    /// Es la mitad de la regla permanente 5 que este cambio podia romper, y por eso se
    /// comprueba contra el ALMACEN y no contra un contador de pantalla.
    /// </remarks>
    [TestMethod]
    public void EstarListoNoFirmaNiUnCampo()
    {
        var (procedencia, modelo) = MontarLeidoEntero();
        var resultado = modelo.Guardar();

        Assert.IsTrue(resultado.ListoParaAsignar);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Personas, PersonaDePrueba));
    }

    /// <summary>El pie lo dice con esas palabras cuando se guarda.</summary>
    [TestMethod]
    public void ElPieLoDiceAlGuardar()
    {
        var (_, modelo) = MontarLeidoEntero();
        var resultado = modelo.Guardar();

        StringAssert.Contains(resultado.LineaDelAcuse, "listo para asignar", StringComparison.Ordinal);
    }

    /// <summary>
    /// Dado un documento con un campo que el programa no pudo leer, entonces NO esta listo
    /// y dice cuantos le faltan.
    /// </summary>
    [TestMethod]
    public void ConUnHuecoNoEstaListoYDiceCuantosLeFaltan()
    {
        var (_, modelo) = MontarConUnHueco();

        Assert.IsFalse(modelo.ListoParaAsignar);
        Assert.AreEqual(1, modelo.CamposQueLeFaltan);

        var resultado = modelo.Guardar();
        Assert.DoesNotContain("listo para asignar", resultado.LineaDelAcuse);
        StringAssert.Contains(resultado.LineaDelAcuse, "falta 1 campo", StringComparison.Ordinal);
    }

    /// <summary>Un campo leido con poca confianza tampoco deja el documento listo.</summary>
    /// <remarks>
    /// «Ninguno dudoso» son las palabras del dueno, y dudoso es lo que se leyo por debajo
    /// del umbral: dar por bueno un dato del que la propia maquina desconfia es justo lo
    /// que manda a alguien al templo con la recomendacion mal.
    /// </remarks>
    [TestMethod]
    public void UnCampoLeidoConPocaConfianzaNoDejaElDocumentoListo()
    {
        var (_, modelo) = MontarLeidoEntero(confianzaDelTemplo: 0.42);

        Assert.IsFalse(modelo.ListoParaAsignar);
        Assert.AreEqual(1, modelo.CamposQueLeFaltan);
    }

    /// <summary>Un campo tachado en el papel y sin corregir tampoco.</summary>
    [TestMethod]
    public void UnCampoTachadoSinCorregirNoDejaElDocumentoListo()
    {
        var (_, modelo) = MontarLeidoEntero(temploTachado: true);

        Assert.IsFalse(modelo.ListoParaAsignar);
        Assert.AreEqual(1, modelo.CamposQueLeFaltan);
    }

    /// <summary>Un valor que no cumple su forma tampoco, aunque se leyera con confianza alta.</summary>
    [TestMethod]
    public void UnValorQueNoCumpleSuFormaNoDejaElDocumentoListo()
    {
        var (_, modelo) = MontarLeidoEntero();
        var unidad = modelo.Campos.Single(campo => campo.Campo == "unidad_numero");
        modelo.Teclear(unidad.Clave, "12345");

        Assert.IsFalse(modelo.ListoParaAsignar);
        Assert.AreEqual(1, modelo.CamposQueLeFaltan);
    }

    /// <summary>
    /// Lo que el programa no pudo, lo cierra Miguel: firmar el hueco deja el documento listo.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad de sus palabras —«yo doy ese veredicto cuando el sistema no completa
    /// todos los campos»—: su firma sigue valiendo, solo que ya no es obligatoria en los
    /// campos que el programa leyo bien.
    /// </remarks>
    [TestMethod]
    public void LoQueElProgramaNoPudoLoCierraLaFirmaDeMiguel()
    {
        var (procedencia, modelo) = MontarConUnHueco();
        var templo = modelo.Campos.Single(campo => campo.Campo == "templo_nombre");
        modelo.Teclear(templo.Clave, "Panama City, Panama");

        var firma = modelo.Firmar(templo, CompaneroDePrueba);

        Assert.IsTrue(firma.SeEscribio, string.Join(" | ", firma.Avisos.Select(aviso => aviso.Linea)));
        Assert.IsTrue(modelo.ListoParaAsignar, ComoEstaCadaCampo(modelo));
        Assert.AreEqual(1, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>Marcar «no está en el papel» tambien cierra el hueco, y NO es una firma.</summary>
    [TestMethod]
    public void MarcarQueNoEstaEnElPapelTambienCierraElHuecoYNoFirma()
    {
        var (procedencia, modelo) = MontarConUnHueco();
        var templo = modelo.Campos.Single(campo => campo.Campo == "templo_nombre");

        var marca = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsTrue(marca.SeEscribio, string.Join(" | ", marca.Avisos.Select(aviso => aviso.Linea)));
        Assert.IsTrue(modelo.ListoParaAsignar, ComoEstaCadaCampo(modelo));
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>Un caso sin ningun campo no esta listo: no hay nada que decir de el.</summary>
    [TestMethod]
    public void UnCasoSinCamposNoEstaListo()
    {
        var servicios = new ServiciosFalsos(0, 20260905, new RelojFijo("2026-09-05"));
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, new ProcedenciaComoLaDeVerdad(CompaneroDePrueba),
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

        Assert.IsFalse(modelo.ListoParaAsignar);
        Assert.AreEqual(0, modelo.CamposQueLeFaltan);
    }

    /// <summary>La pantalla dibuja los cinco campos del caso, no cuatro.</summary>
    /// <remarks>
    /// Va aqui porque es la premisa de todo lo demas: si <c>templo_nombre</c> dejara de
    /// dibujarse, «listo para asignar» pasaria a medir seis campos y nadie lo notaria.
    /// </remarks>
    [TestMethod]
    public void LaPantallaDibujaLosSieteCamposDeEsteCaso()
    {
        var (_, modelo) = MontarLeidoEntero();
        Assert.HasCount(CamposDelCaso, modelo.Campos);
        Assert.Contains("templo_nombre", modelo.Campos.Select(campo => campo.Campo).ToArray());
    }

    /// <summary>La linea del pie sigue siendo UNA linea: requisito 4 del dueno.</summary>
    [TestMethod]
    public void LaLineaDelPieNoPasaDeSuTope()
    {
        var (_, listo) = MontarLeidoEntero();
        var conListo = listo.Guardar();
        Assert.IsLessThanOrEqualTo(
            TextoDelAcuse.LargoMaximoDeLaLinea,
            conListo.LineaDelAcuse.Length,
            $"«{conListo.LineaDelAcuse}» mide {conListo.LineaDelAcuse.Length}.");

        var (_, conHueco) = MontarConUnHueco();
        var conFalta = conHueco.Guardar();
        Assert.IsLessThanOrEqualTo(
            TextoDelAcuse.LargoMaximoDeLaLinea,
            conFalta.LineaDelAcuse.Length,
            $"«{conFalta.LineaDelAcuse}» mide {conFalta.LineaDelAcuse.Length}.");
    }

    /// <summary>Como esta cada campo, para que un fallo diga cual falla y no solo que falla.</summary>
    private static string ComoEstaCadaCampo(ModeloDeCorreccion modelo)
        => string.Join(" | ", modelo.Campos.Select(campo =>
            $"{campo.Campo}={modelo.ValorDe(campo) ?? "(vacío)"} "
            + $"[{EstadosDeCampo.Palabra(modelo.EstadoDe(campo))}]"));
}
