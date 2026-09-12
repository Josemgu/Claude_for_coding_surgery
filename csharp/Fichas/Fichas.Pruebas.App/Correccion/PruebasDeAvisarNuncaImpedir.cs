using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// El requisito 9 del dueno en la pantalla de correccion: <b>avisar, nunca impedir</b>.
/// </summary>
/// <remarks>
/// <para>Las pruebas de esta clase NO salen del codigo: salen del criterio, y el criterio
/// esta escrito en tres sitios que decian lo mismo mientras la pantalla hacia lo
/// contrario.</para>
///
/// <list type="number">
/// <item>El requisito 9 del dueno: «avisar, nunca impedir».</item>
/// <item>El texto de la migracion 17, que le quito el <c>CHECK</c> a <c>personas.mrn</c>:
/// «una cedula mal leida se guarda y se senala en la pantalla, no se rechaza; lo que un
/// CHECK tira aqui no es un dato, es la fila de una persona».</item>
/// <item>La capa de datos, que ya contesta «La cedula no tiene la forma esperada. Se
/// guardo igual» (<c>Fichas.Datos/Validacion/ReglasDeFormato.cs</c>).</item>
/// </list>
///
/// <para>Lo que QA midio sobre el paquete publicado el 2026-09-04, con la ventana abierta:
/// tecleo <c>123</c> en la cedula del caso 3, pulso Guardar, el pie dijo «Guardado · 1
/// campo sin guardar: Cedula (1)» y en la base el MRN seguia siendo el de antes. Por SQL
/// directo <c>123</c> SI entra. La ultima pared que quedaba era esta pantalla.</para>
///
/// <para>⛔ Y lo que NO se relaja: la <b>firma</b>. Guardar es apuntar lo que dice el
/// papel; firmar es darlo por bueno. Un campo senalado no se puede dar por bueno hasta
/// arreglarlo (regla permanente 5).</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeAvisarNuncaImpedir
{
    /// <summary>El id fijo del caso que monta cada prueba; a mano, para no depender del sorteo del generador.</summary>
    private const long CasoDePrueba = 100;
    /// <summary>La persona que llega con una cedula bien formada; es la que se estropea a proposito.</summary>
    private const long PersonaConCedula = 101;
    /// <summary>La otra persona del caso, para comprobar que lo bueno de la misma pasada no se pierde.</summary>
    private const long OtraPersona = 102;
    /// <summary>El companero con el que se intenta firmar.</summary>
    private const long CompaneroDePrueba = 1;

    /// <summary>La cedula con la que entra la persona; es la forma buena del papel.</summary>
    private const string CedulaOriginal = "055-1111-3853";

    /// <summary>Un caso conocido con dos personas, una de ellas con cedula buena.</summary>
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
        servicios.Almacen.Personas[PersonaConCedula] = new Persona
        {
            Id = PersonaConCedula,
            CasoId = CasoDePrueba,
            Mrn = CedulaOriginal,
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };
        servicios.Almacen.Personas[OtraPersona] = new Persona
        {
            Id = OtraPersona,
            CasoId = CasoDePrueba,
            Mrn = null,
            Nombre = "Jose Fulano",
            FilaFormulario = 2,
            PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Monta el modelo sobre esos servicios, sin cargar ningun caso todavia.</summary>
    /// <param name="servicios">La base inventada de la prueba.</param>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
        => new(servicios.Casos, servicios.Personas, servicios.Procedencia,
               servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

    /// <summary>La clave con la que se teclea la cedula de esa persona, compuesta como la compone la pantalla.</summary>
    /// <param name="personaId">La persona.</param>
    private static string ClaveDeLaCedula(long personaId)
        => CampoEnPantalla.ClaveDe(TablaDeProcedencia.Personas, personaId, "mrn");

    /// <summary>
    /// Dado que tecleo <c>123</c> en la cedula, cuando pulso Guardar, entonces en la base
    /// ese campo vale <c>123</c>, queda senalado con su motivo, y el pie NO dice «sin
    /// guardar».
    /// </summary>
    /// <remarks>Es el criterio de cierre del defecto, en sus cuatro observables.</remarks>
    [TestMethod]
    public void UnaCedulaQueNoValeSeGuardaIgualYQuedaSenalada()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDeLaCedula(PersonaConCedula)] = "123",
        });

        // 1. El dato ENTRO.
        Assert.AreEqual("123", servicios.Almacen.Personas[PersonaConCedula].Mrn);
        Assert.AreEqual(1, resultado.CamposGuardados);

        // 2. Queda senalado, con su motivo, en su sitio.
        var cedula = modelo.Campos.Single(c => c.Campo == "mrn" && c.RegistroId == PersonaConCedula);
        Assert.IsNotNull(modelo.MotivoDe(cedula));
        Assert.AreEqual(EstadoDeCampo.NoValido, modelo.EstadoDe(cedula));
        Assert.Contains("Cédula (1)", resultado.CamposSenalados);

        // 3. El pie NO dice «sin guardar», y nada quedo sin admitir.
        Assert.IsEmpty(resultado.CamposQueElAlmacenNoAdmitio);
        Assert.DoesNotContain("sin guardar", resultado.LineaDelAcuse,
            $"El pie dijo «{resultado.LineaDelAcuse}» y el dato SI se guardo.");
    }

    /// <summary>La cadena vacia tambien entra: vaciar un campo es un dato, no un fallo.</summary>
    /// <remarks>
    /// Entra como <c>NULL</c> y no como <c>''</c>: en esta base <c>NULL</c> significa «no
    /// hay dato» y <c>''</c> no significa nada (<c>ReglasDeCampo.Limpiar</c>).
    /// </remarks>
    [TestMethod]
    public void LaCadenaVaciaTambienEntraYBorraLaCedula()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDeLaCedula(PersonaConCedula)] = "   ",
        });

        Assert.IsNull(servicios.Almacen.Personas[PersonaConCedula].Mrn);
        Assert.AreEqual(1, resultado.CamposGuardados);
        Assert.IsEmpty(resultado.CamposQueElAlmacenNoAdmitio);
    }

    /// <summary>Una cedula con letra al final vale, y ni siquiera sale senalada.</summary>
    /// <remarks>
    /// Es la segunda FORMA que trae el papel: <c>066-2222-133A</c>, terminada en letra. El
    /// valor va sustituido desde el 2026-09-07 y la forma no: cambiarla por una que acabe
    /// en digito deja esta prueba en verde sin medir nada.
    /// </remarks>
    [TestMethod]
    public void LaCedulaConLetraAlFinalValeYNoSeSenala()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDeLaCedula(PersonaConCedula)] = "066-2222-133A",
        });

        Assert.AreEqual("066-2222-133A", servicios.Almacen.Personas[PersonaConCedula].Mrn);
        Assert.IsEmpty(resultado.CamposSenalados);
        Assert.DoesNotContain("sin guardar", resultado.LineaDelAcuse);
    }

    /// <summary>
    /// Un valor raro entra Y los buenos de la misma pasada tambien: ninguno tumba a otro.
    /// </summary>
    [TestMethod]
    public void ElValorRaroEntraYNoTumbaALosBuenosDeLaMismaPasada()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var resultado = modelo.Guardar(new Dictionary<string, string?>
        {
            [ClaveDeLaCedula(PersonaConCedula)] = "123",
            [CampoEnPantalla.ClaveDe(TablaDeProcedencia.Casos, CasoDePrueba, "unidad_numero")] = "12345",
            [CampoEnPantalla.ClaveDe(TablaDeProcedencia.Casos, CasoDePrueba, "templo_nombre")] = "Caracas Venezuela",
        });

        Assert.AreEqual("123", servicios.Almacen.Personas[PersonaConCedula].Mrn);
        Assert.AreEqual("12345", servicios.Almacen.Casos[CasoDePrueba].UnidadNumero);
        Assert.AreEqual("Caracas Venezuela", servicios.Almacen.Casos[CasoDePrueba].TemploNombre);
        Assert.AreEqual(3, resultado.CamposGuardados);
        Assert.HasCount(2, resultado.CamposSenalados);
    }

    /// <summary>
    /// ⛔ Lo que NO se relaja: un campo senalado no se puede dar por bueno.
    /// </summary>
    /// <remarks>
    /// Regla permanente 5. Y no basta con que hoy se niegue: se comprueba tambien que
    /// despues de ARREGLARLO si se puede firmar, porque una negativa permanente seria
    /// otra pared.
    /// </remarks>
    [TestMethod]
    public void UnCampoSenaladoNoSePuedeDarPorBuenoHastaArreglarlo()
    {
        var servicios = MontarConUnCasoConocido();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);
        modelo.Guardar(new Dictionary<string, string?> { [ClaveDeLaCedula(PersonaConCedula)] = "123" });

        var cedula = modelo.Campos.Single(c => c.Campo == "mrn" && c.RegistroId == PersonaConCedula);
        var negado = modelo.Firmar(cedula, CompaneroDePrueba);

        Assert.IsFalse(negado.SeEscribio, "Un campo que no vale NO se firma: regla permanente 5.");
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, PersonaConCedula));

        // Y arreglandolo, si se firma: la negativa es a lo malo, no al camino.
        modelo.Guardar(new Dictionary<string, string?> { [ClaveDeLaCedula(PersonaConCedula)] = CedulaOriginal });
        var firmado = modelo.Firmar(cedula, CompaneroDePrueba);

        Assert.IsTrue(firmado.SeEscribio);
        Assert.AreEqual(1, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, PersonaConCedula));
    }

    /// <summary>
    /// Lo guardado deja de ser un cambio pendiente aunque no valga: si no, Guardar se
    /// quedaria pulsandose para siempre sobre lo mismo.
    /// </summary>
    [TestMethod]
    public void LoGuardadoDejaDeSerUnCambioPendienteAunqueNoValga()
    {
        var modelo = ModeloSobre(MontarConUnCasoConocido());
        modelo.Cargar(CasoDePrueba);
        modelo.Teclear(ClaveDeLaCedula(PersonaConCedula), "123");
        Assert.IsTrue(modelo.HayCambiosSinGuardar);

        modelo.Guardar();
        Assert.IsFalse(modelo.HayCambiosSinGuardar, "Ya se guardo: no queda nada pendiente.");

        // Y una segunda pulsacion no vuelve a contar el mismo campo.
        var segunda = modelo.Guardar();
        Assert.AreEqual(0, segunda.CamposGuardados);
        StringAssert.Contains(segunda.LineaDelAcuse, "sin cambios", StringComparison.Ordinal);
    }
}
