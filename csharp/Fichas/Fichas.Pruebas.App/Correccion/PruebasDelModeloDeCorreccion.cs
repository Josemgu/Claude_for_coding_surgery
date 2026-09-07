using System.Diagnostics;
using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// La pantalla de correccion SIN VENTANA: cargar un caso, guardarlo y contar.
/// </summary>
/// <remarks>
/// Todo lo que decide esta pantalla vive en <see cref="ModeloDeCorreccion"/>, que no
/// conoce XAML. Es lo que hace que los criterios C4-7, C4-10, C4-11 y C4-13 se puedan
/// medir con un comando en vez de mirando una captura.
/// </remarks>
[TestClass]
public sealed class PruebasDelModeloDeCorreccion
{
    private const long CasoDePrueba = 100;
    private const long PersonaBuena = 101;
    private const long PersonaMala = 102;
    private const long CompaneroDePrueba = 1;

    /// <summary>Monta un almacen inventado con un caso conocido y dos personas.</summary>
    /// <remarks>
    /// El caso se pone a mano y no se toma del generador: una prueba que dependa del
    /// sorteo mide el sorteo, no la pantalla.
    /// </remarks>
    private static ServiciosFalsos MontarConUnCasoConocido()
    {
        var servicios = new ServiciosFalsos(3, 20260904, new RelojFijo("2026-09-04"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "7000011",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-12",
            TemploNombre = "Santo Domingo Dominican Republic",
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-01",
        };
        servicios.Almacen.Personas[PersonaBuena] = new Persona
        {
            Id = PersonaBuena,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };
        servicios.Almacen.Personas[PersonaMala] = new Persona
        {
            Id = PersonaMala,
            CasoId = CasoDePrueba,
            Mrn = null,
            Nombre = "Jose Fulano",
            FilaFormulario = 2,
            PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Monta el modelo sobre ese almacen.</summary>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
        => new(servicios.Casos, servicios.Personas, servicios.Procedencia,
               servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

    // ---- abrir un caso ---------------------------------------------------

    /// <summary>Dado un caso con dos personas, cuando se abre, entonces trae sus campos.</summary>
    [TestMethod]
    public void AbrirUnCasoTraeSusCamposYSusPersonas()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        Assert.IsTrue(modelo.Cargar(CasoDePrueba));
        Assert.IsNotNull(modelo.Caso);
        Assert.HasCount(2, modelo.Personas);

        // Cinco campos del caso mas dos por persona.
        Assert.HasCount(5 + (2 * 2), modelo.Campos);
        Assert.IsTrue(modelo.Campos.Any(c => c.Campo == "unidad_numero"));
        Assert.IsTrue(modelo.Campos.Any(c => c.Campo == "mrn" && c.RegistroId == PersonaMala));
    }

    /// <summary>Un caso que no esta NO lanza: devuelve falso y deja su aviso de una linea.</summary>
    /// <remarks>Requisito 9 del dueno: avisar, nunca impedir. Y nunca por excepcion.</remarks>
    [TestMethod]
    public void UnCasoQueNoEstaAvisaYNoLanza()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        Assert.IsFalse(modelo.Cargar(999_999));
        Assert.HasCount(1, modelo.AvisosDeLaCabecera);
        Assert.AreEqual(GravedadDeAviso.Problema, modelo.AvisosDeLaCabecera[0].Gravedad);
    }

    /// <summary>El numero de caso no se edita: es la mitad de la clave que cruza el Excel.</summary>
    [TestMethod]
    public void ElNumeroDeCasoNoSeEdita()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        modelo.Cargar(CasoDePrueba);
        var numero = modelo.Campos.Single(c => c.Campo == "numero_caso");
        Assert.IsTrue(numero.SoloLectura);
    }

    // ---- C4-13: lo dudoso primero ---------------------------------------

    /// <summary>
    /// Dado un caso con campos vacios y campos leidos, cuando se abre, lo dudoso va arriba.
    /// </summary>
    /// <remarks>
    /// Rossum: «prompts the user to inspect empty fields and review data with low
    /// confidence scores». Que Miguel no lea 26 campos buenos para encontrar el malo.
    /// </remarks>
    [TestMethod]
    public void LoDudosoSaleArribaConSuCuenta()
    {
        var servicios = MontarConUnCasoConocido();
        // Cuatro campos del caso salen leidos con confianza alta; el quinto no se anota.
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "numero_caso", 0.97);
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_numero", 0.95);
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_nombre", 0.93);
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "fecha_viaje", 0.91);
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "templo_nombre", 0.42);
        AnotarLeido(servicios, TablaDeProcedencia.Personas, PersonaBuena, "mrn", 0.96);
        AnotarLeido(servicios, TablaDeProcedencia.Personas, PersonaBuena, "nombre", 0.94);
        AnotarLeido(servicios, TablaDeProcedencia.Personas, PersonaMala, "nombre", 0.90);

        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        // Dudosos: el templo, que se leyo al 0,42; y la cedula de la persona 2, que esta
        // vacia y sin lectura guardada. Los otros siete se leyeron por encima del umbral.
        Assert.HasCount(2, modelo.Dudosos);
        Assert.HasCount(7, modelo.Resto);
        Assert.AreEqual(modelo.Campos.Count, modelo.Dudosos.Count + modelo.Resto.Count);
        Assert.IsTrue(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));
        Assert.IsTrue(modelo.Dudosos.Any(c => c.Campo == "mrn" && c.RegistroId == PersonaMala));
    }

    // ---- C4-8: nada se firma solo ---------------------------------------

    /// <summary>Al abrir un caso, ninguna fila de procedencia queda firmada.</summary>
    /// <remarks>Regla permanente 5: el sistema propone, Miguel confirma. Siempre.</remarks>
    [TestMethod]
    public void AbrirUnCasoNoFirmaNada()
    {
        var servicios = MontarConUnCasoConocido();
        AnotarLeido(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_numero", 0.95);
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, PersonaBuena));
        Assert.AreEqual(0, modelo.CuantosFirmados);
    }

    /// <summary>Guardar tampoco firma: escribir un dato no es darlo por bueno.</summary>
    [TestMethod]
    public void GuardarNoFirmaNada()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDelCaso("unidad_numero")] = "7000012",
        });
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    // ---- C4-7: guardar acusa recibo -------------------------------------

    /// <summary>
    /// Dado un valor valido, cuando se guarda, entonces cambia el dato y el pie lo dice.
    /// </summary>
    [TestMethod]
    public void GuardarUnValorValidoCambiaElDatoYAcusaRecibo()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDeLaPersona(PersonaMala, "mrn")] = "123-4567-8901",
        });

        Assert.AreEqual("123-4567-8901", servicios.Almacen.Personas[PersonaMala].Mrn);
        Assert.AreEqual(1, resultado.CamposGuardados);
        Assert.IsEmpty(resultado.CamposSenalados);
        StringAssert.Contains(resultado.LineaDelAcuse, "1 campo", StringComparison.Ordinal);
        StringAssert.Contains(resultado.LineaDelAcuse, "12:00", StringComparison.Ordinal);
    }

    /// <summary>Guardar sin tocar nada tambien acusa recibo, y no miente con la cuenta.</summary>
    [TestMethod]
    public void GuardarSinCambiosTambienAcusaRecibo()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        modelo.Cargar(CasoDePrueba);
        var resultado = modelo.Guardar(new Dictionary<string, string?>());
        Assert.AreEqual(0, resultado.CamposGuardados);
        StringAssert.Contains(resultado.LineaDelAcuse, "sin cambios", StringComparison.Ordinal);
    }

    // ---- C4-10 y C4-11: lo raro se guarda o se senala, y NO tumba nada ---

    /// <summary>
    /// Dados tres valores con formato inesperado a la vez, entonces se guardan LOS SEIS y
    /// los tres raros salen senalados en su campo.
    /// </summary>
    /// <remarks>
    /// Es el criterio C4-10 y C4-11 juntos, con los tres valores que nombra el plan:
    /// MRN de 9 digitos, unidad de 5 digitos, y una fecha cuyo mes no casa con el caso.
    /// La fecha del mes cruzado SI se guarda —es aviso, no pared (P-2 del dueno)—, asi que
    /// para el campo que no vale se usa una fecha que ademas no existe.
    /// <para>
    /// ⚠️ <b>Esta prueba cambio el 2026-09-04 y el cambio es el arreglo.</b> Hasta ese dia
    /// afirmaba que «los raros no pisaron lo que habia», o sea que la pantalla los
    /// rechazaba; eso contradice el requisito 9 del dueno y el texto de la migracion 17, y
    /// era el defecto que QA midio con la ventana abierta. Ahora los seis entran, y los
    /// tres raros se senalan. El criterio manda sobre la prueba que lo contradecia.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void TresValoresRarosEntranIgualYSalenSenalados()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            // Los tres raros.
            [ClaveDeLaPersona(PersonaBuena, "mrn")] = "123-4567-890",
            [ClaveDelCaso("unidad_numero")] = "12345",
            [ClaveDelCaso("fecha_viaje")] = "2026-02-31",
            // Los tres buenos, tecleados en la misma pasada.
            [ClaveDelCaso("unidad_nombre")] = "Paramaribo Branch",
            [ClaveDelCaso("templo_nombre")] = "Caracas Venezuela",
            [ClaveDeLaPersona(PersonaMala, "nombre")] = "Jose Miguel Fulano",
        });

        // Los buenos entraron: cero campos perdidos por culpa de otro.
        var caso = servicios.Almacen.Casos[CasoDePrueba];
        Assert.AreEqual("Paramaribo Branch", caso.UnidadNombre);
        Assert.AreEqual("Caracas Venezuela", caso.TemploNombre);
        Assert.AreEqual("Jose Miguel Fulano", servicios.Almacen.Personas[PersonaMala].Nombre);

        // Y los raros TAMBIEN entraron: avisar, nunca impedir.
        Assert.AreEqual("12345", caso.UnidadNumero);
        Assert.AreEqual("2026-02-31", caso.FechaViaje);
        Assert.AreEqual("123-4567-890", servicios.Almacen.Personas[PersonaBuena].Mrn);
        Assert.AreEqual(6, resultado.CamposGuardados);

        // Salen nombrados en el pie, y el pie no dice que se hayan perdido.
        Assert.HasCount(3, resultado.CamposSenalados);
        Assert.IsEmpty(resultado.CamposQueElAlmacenNoAdmitio);
        StringAssert.Contains(resultado.LineaDelAcuse, "por revisar", StringComparison.Ordinal);
        Assert.DoesNotContain("sin guardar", resultado.LineaDelAcuse);
    }

    /// <summary>C4-12: la senal va en el campo, no en un parrafo del cuerpo.</summary>
    [TestMethod]
    public void LaSenalDeUnValorRaroVaEnSuPropioCampo()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        modelo.Cargar(CasoDePrueba);
        modelo.Teclear(ClaveDelCaso("unidad_numero"), "12345");

        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_numero");
        Assert.IsNotNull(modelo.MotivoDe(unidad));
        Assert.AreEqual(EstadoDeCampo.NoValido, modelo.EstadoDe(unidad));
        // Y ningun otro campo se contagia.
        Assert.HasCount(1, modelo.Campos.Where(c => modelo.MotivoDe(c) is not null));
    }

    /// <summary>El mes cruzado avisa en la cabecera y AUN ASI la fecha se guarda.</summary>
    [TestMethod]
    public void ElMesCruzadoAvisaYLaFechaSeGuardaIgual()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        modelo.Teclear(ClaveDelCaso("fecha_viaje"), "2026-10-05");

        Assert.IsTrue(modelo.AvisosDeLaCabecera.Any(a => a.Campo == "fecha_viaje"));
        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDelCaso("fecha_viaje")] = "2026-10-05",
        });
        Assert.AreEqual("2026-10-05", servicios.Almacen.Casos[CasoDePrueba].FechaViaje);
        Assert.IsEmpty(resultado.CamposSenalados);
    }

    // ---- los avisos de la cabecera --------------------------------------

    /// <summary>Ningun aviso de la cabecera pasa de UNA linea: requisito 4 del dueno.</summary>
    /// <remarks>
    /// El tope son 90 caracteres, que es el mismo de <c>interfaz/avisos_de_correccion.py</c>:
    /// la franja pone la cuenta delante y con 90 los dos grupos caben sin partir. Lo largo
    /// va en <c>Aviso.Detalle</c>, que solo se ve al pulsar «ver».
    /// </remarks>
    [TestMethod]
    public void NingunAvisoDeLaCabeceraPasaDeUnaLinea()
    {
        var servicios = MontarConUnCasoConocido();
        servicios.Almacen.Casos[CasoDePrueba] = servicios.Almacen.Casos[CasoDePrueba] with
        {
            NumeroCaso = null,
            CapturaManual = true,
        };
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        Assert.IsNotEmpty(modelo.AvisosDeLaCabecera);
        foreach (var aviso in modelo.AvisosDeLaCabecera)
        {
            Assert.DoesNotContain("\n", aviso.Linea, $"«{aviso.Linea}» trae un salto de linea.");
            Assert.IsLessThanOrEqualTo(
                90, aviso.Linea.Length, $"«{aviso.Linea}» mide {aviso.Linea.Length} y el tope de la franja son 90.");
        }
    }

    // ---- la firma es de Miguel y se cae al cambiar el valor -------------

    /// <summary>Firmar exige quien y cuando; sin companero no se firma nada.</summary>
    [TestMethod]
    public void FirmarExigeQuienYCuando()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_numero");

        var sinCompanero = modelo.Firmar(unidad, companeroId: 999_999);
        Assert.IsFalse(sinCompanero.SeEscribio);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));

        var conCompanero = modelo.Firmar(unidad, CompaneroDePrueba);
        Assert.IsTrue(conCompanero.SeEscribio);
        Assert.AreEqual(1, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>
    /// ⚠️ Lo que HOY hace el almacen falso al cambiar un valor firmado: NO retira la firma.
    /// </summary>
    /// <remarks>
    /// El dueno pidio que la firma se caiga cuando el valor cambia, y en Python lo hace la
    /// capa de datos (<c>datos/repositorio.py</c>). Aqui NO lo hace nadie:
    /// <c>IProcedencia</c> no tiene por donde retirar una firma y
    /// <c>RepositorioDeProcedenciaFalso.Anotar</c> la conserva a proposito. Esta prueba fija
    /// la conducta de hoy para que el hueco no se tape en silencio; va nombrado en la
    /// entrega y lo cierra quien pueda tocar los contratos o la fase C2.
    /// </remarks>
    [TestMethod]
    public void HoyElAlmacenNoRetiraLaFirmaAlCambiarElValor()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_numero");
        modelo.Firmar(unidad, CompaneroDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDelCaso("unidad_numero")] = "7000012",
        });

        Assert.AreEqual("7000012", servicios.Almacen.Casos[CasoDePrueba].UnidadNumero);
        Assert.AreEqual(0, resultado.FirmasRetiradas, "Hoy no se retira ninguna; ver el comentario de esta prueba.");
        Assert.AreEqual(1, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    // ---- lo que cuesta abrir --------------------------------------------

    /// <summary>Abrir un caso con 3 000 documentos en la base cuesta menos de 0,8 s.</summary>
    /// <remarks>
    /// La cifra del pase. Se mide sobre el modelo, que es lo que hace el trabajo; lo que
    /// tarde en pintarse la ventana se mide aparte, con la ventana abierta.
    /// </remarks>
    [TestMethod]
    public void AbrirUnCasoConTresMilDocumentosCuestaMenosDeOchoDecimas()
    {
        var servicios = new ServiciosFalsos(3000, 20260904, new RelojFijo("2026-09-04"));
        var algunCaso = servicios.Almacen.Casos.Values.OrderBy(c => c.Id).First().Id;
        var modelo = ModeloSobre(servicios);

        var cronometro = Stopwatch.StartNew();
        var abrio = modelo.Cargar(algunCaso);
        cronometro.Stop();

        Assert.IsTrue(abrio);
        Assert.IsLessThanOrEqualTo(
            800.0,
            cronometro.Elapsed.TotalMilliseconds,
            $"Abrir costo {cronometro.Elapsed.TotalMilliseconds:F0} ms y el tope son 800.");
    }

    // ---- ayudas ----------------------------------------------------------

    /// <summary>La clave de un campo del caso de prueba.</summary>
    private static string ClaveDelCaso(string campo)
        => CampoEnPantalla.ClaveDe(TablaDeProcedencia.Casos, CasoDePrueba, campo);

    /// <summary>La clave de un campo de una persona.</summary>
    private static string ClaveDeLaPersona(long personaId, string campo)
        => CampoEnPantalla.ClaveDe(TablaDeProcedencia.Personas, personaId, campo);

    /// <summary>Deja anotado que ese campo lo leyo el OCR con esa confianza.</summary>
    private static void AnotarLeido(
        ServiciosFalsos servicios, TablaDeProcedencia tabla, long registroId, string campo, double confianza)
        => servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = OrigenDeCampo.Ocr,
            Confianza = confianza,
            BandaX0 = 0.08,
            BandaY0 = 0.20,
            BandaX1 = 0.92,
            BandaY1 = 0.245,
        });
}
