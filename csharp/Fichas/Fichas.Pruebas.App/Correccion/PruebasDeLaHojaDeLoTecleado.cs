using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Completar;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Lo que se teclea con la hoja 2 delante queda anotado como salido de la hoja 2: en
/// <c>pagina_pdf</c> del caso o de la persona a la que pertenece el campo, y en la persona
/// que se añade a mano.
/// </summary>
/// <remarks>
/// <para>Es la segunda mitad del defecto del 2026-09-15 (ver <see cref="PruebasDeLaHojaQueManda"/>):
/// que el visor se quede en la hoja elegida no basta si la base sigue diciendo que el dato
/// salió de la hoja 1. Revisar y la comprobación de papeles leen <c>pagina_pdf</c>, y una
/// persona tecleada mirando la hoja 2 con <c>pagina_pdf = 1</c> señalaría la factura.</para>
///
/// <para><b>Medido en la base del ensayo sobre master (b78c0e3)</b> tras importar el PDF de dos
/// hojas: <c>casos.pagina_pdf = 1</c>, <c>personas.pagina_pdf = 2</c> para la leída en la 2, y
/// la persona añadida a mano heredaba la del caso: 1. Con eso, enfocar su nombre devolvía el
/// visor a la factura.</para>
///
/// <para>La hoja se apunta <b>al teclear</b>, no al guardar: si el dueño teclea mirando la 2 y
/// después vuelve a la 1 para comprobar algo antes de pulsar Guardar, lo tecleado sigue siendo
/// de la 2. Y un guardado que no toca un registro no le cambia la hoja.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaHojaDeLoTecleado
{
    /// <summary>
    /// Un documento como el que deja la importación del PDF de la factura: el caso abierto en la
    /// hoja 1, con la unidad vacía, y una persona leída en la hoja 2.
    /// </summary>
    private static (ServiciosFalsos Servicios, ModeloDeCorreccion Modelo, long CasoId, long PersonaId) DocumentoDeDosHojas()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "HOJB2609", "2026-09-24", cuantasPersonas: 1);
        servicios.Almacen.Casos[casoId] = servicios.Almacen.Casos[casoId] with { UnidadNumero = null, UnidadNombre = null, PaginaPdf = 1 };
        var personaId = servicios.Almacen.Personas.Values.Single(p => p.CasoId == casoId).Id;
        servicios.Almacen.Personas[personaId] = servicios.Almacen.Personas[personaId] with { PaginaPdf = 2 };

        var modelo = BancoDeLaCola.ModeloDe(servicios);
        modelo.Cargar(casoId);
        return (servicios, modelo, casoId, personaId);
    }

    /// <summary>El campo de esa tabla y esa columna, buscado en el modelo y no por una clave copiada.</summary>
    private static CampoEnPantalla CampoDe(ModeloDeCorreccion modelo, TablaDeProcedencia tabla, string columna)
        => modelo.Campos.Single(campo => campo.Tabla == tabla && campo.Campo == columna);

    /// <summary>Al abrir un caso, la hoja de delante es la que abrió el caso: es la que enseña el visor.</summary>
    [TestMethod]
    public void AlAbrirElCasoLaHojaDeDelanteEsLaDelCaso()
    {
        var (_, modelo, _, _) = DocumentoDeDosHojas();

        Assert.AreEqual(1, modelo.HojaDelante);
    }

    /// <summary>Dado el caso abierto en la 1, cuando se teclea la unidad con la hoja 2 delante y se guarda, entonces el caso dice que salió de la hoja 2.</summary>
    [TestMethod]
    public void LaUnidadTecleadaConLaHoja2DelanteDejaElCasoEnLaHoja2()
    {
        var (servicios, modelo, casoId, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;

        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Casos, "unidad_nombre").Clave, "Rama Dos Hojas");
        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Casos, "unidad_numero").Clave, "111222");
        var resultado = modelo.Guardar();

        Assert.AreEqual(2, resultado.CamposGuardados);
        var caso = servicios.Almacen.Casos[casoId];
        Assert.AreEqual(2, caso.PaginaPdf, "lo tecleado mirando la hoja 2 salió de la hoja 2");
        Assert.AreEqual("Rama Dos Hojas", caso.UnidadNombre);
        Assert.AreEqual("111222", caso.UnidadNumero);
    }

    /// <summary>Un guardado que no toca el caso no le cambia la hoja, aunque haya otra delante.</summary>
    [TestMethod]
    public void GuardarSinTocarElCasoNoLeCambiaLaHoja()
    {
        var (servicios, modelo, casoId, personaId) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;

        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Personas, "nombre").Clave, "Gala Prueba Siete");
        modelo.Guardar();

        Assert.AreEqual(1, servicios.Almacen.Casos[casoId].PaginaPdf, "del caso no se tecleó nada: su hoja no se toca");
        Assert.AreEqual(2, servicios.Almacen.Personas[personaId].PaginaPdf);
    }

    /// <summary>
    /// Dada una persona leída en la 2 CON cédula, cuando se le corrige la cédula con la hoja 1
    /// delante, entonces sigue siendo de la hoja 2: el lector la leyó ahí, y arreglarle una
    /// cifra mirando otra hoja no la mueve de sitio.
    /// </summary>
    /// <remarks>
    /// Es la regla que separa «dato nuevo» de «dato corregido», y existe por los formularios de
    /// grupo de seis hojas: sin ella, retocar el nombre de alguien de la hoja 4 con la 1 delante
    /// lo mandaría a la 1, y Revisar señalaría una hoja donde esa persona no está.
    /// </remarks>
    [TestMethod]
    public void CorregirUnDatoLeidoNoLoMueveDeHoja()
    {
        var (servicios, modelo, _, personaId) = DocumentoDeDosHojas();
        modelo.HojaDelante = 1;

        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Personas, "mrn").Clave, "055-7777-8888");
        var resultado = modelo.Guardar();

        Assert.AreEqual(1, resultado.CamposGuardados);
        Assert.AreEqual(2, servicios.Almacen.Personas[personaId].PaginaPdf, "la cédula ya estaba leída en la 2");
    }

    /// <summary>Dada una persona leída en la 2 SIN cédula, cuando se le teclea la cédula con la 2 delante, entonces sigue en la 2 y con su cédula.</summary>
    [TestMethod]
    public void UnDatoNuevoTecleadoEnLaMismaHojaLaDejaIgual()
    {
        var (servicios, modelo, _, personaId) = DocumentoDeDosHojas();
        servicios.Almacen.Personas[personaId] = servicios.Almacen.Personas[personaId] with { Mrn = null };
        modelo.Cargar(servicios.Almacen.Personas[personaId].CasoId);
        modelo.HojaDelante = 2;

        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Personas, "mrn").Clave, "055-7777-8887");
        modelo.Guardar();

        Assert.AreEqual(2, servicios.Almacen.Personas[personaId].PaginaPdf);
        Assert.AreEqual("055-7777-8887", servicios.Almacen.Personas[personaId].Mrn);
    }

    /// <summary>La hoja se apunta AL TECLEAR: volver a otra hoja antes de pulsar Guardar no la cambia.</summary>
    [TestMethod]
    public void LaHojaSeApuntaAlTeclearYNoAlGuardar()
    {
        var (servicios, modelo, casoId, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;
        modelo.Teclear(CampoDe(modelo, TablaDeProcedencia.Casos, "unidad_nombre").Clave, "Rama Dos Hojas");

        modelo.HojaDelante = 1;
        modelo.Guardar();

        Assert.AreEqual(2, servicios.Almacen.Casos[casoId].PaginaPdf, "se tecleó mirando la 2 aunque se guardara mirando la 1");
    }

    /// <summary>Lo que llega a Guardar ya tecleado —el camino de las pruebas de la cola— también lleva la hoja de delante.</summary>
    [TestMethod]
    public void LoTecleadoQueLlegaConGuardarLlevaLaHojaDeDelante()
    {
        var (servicios, modelo, casoId, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;

        modelo.Guardar(new Dictionary<string, string?>
        {
            [CampoDe(modelo, TablaDeProcedencia.Casos, "unidad_nombre").Clave] = "Rama Dos Hojas",
        });

        Assert.AreEqual(2, servicios.Almacen.Casos[casoId].PaginaPdf);
    }

    /// <summary>Firmar lo que se acaba de teclear lo escribe antes, y esa escritura también lleva la hoja.</summary>
    [TestMethod]
    public void FirmarLoTecleadoConLaHoja2DelanteTambienDejaLaHoja2()
    {
        var (servicios, modelo, casoId, _) = DocumentoDeDosHojas();
        var unidad = CampoDe(modelo, TablaDeProcedencia.Casos, "unidad_nombre");
        modelo.HojaDelante = 2;
        modelo.Teclear(unidad.Clave, "Rama Dos Hojas");

        var firma = modelo.Firmar(unidad, companeroId: 0);

        Assert.AreEqual(2, servicios.Almacen.Casos[casoId].PaginaPdf, $"el guardado previo a la firma lleva la hoja; firma: {firma.SeEscribio}");
    }

    /// <summary>Dado un documento abierto en la 1, cuando se añade a mano una persona con la hoja 2 delante, entonces la persona nace en la hoja 2.</summary>
    [TestMethod]
    public void LaPersonaAnadidaAManoNaceEnLaHojaQueEstaDelante()
    {
        var (servicios, modelo, _, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;

        var resultado = modelo.AnadirUnaPersonaAMano("Hugo Prueba Ocho", "055-8888-9998");

        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual(2, servicios.Almacen.Personas[resultado.PersonaId].PaginaPdf,
            "se tecleó mirando la hoja 2, no la del caso");
    }

    /// <summary>Y sus dos campos en pantalla dicen hoja 2, así que enfocarlos no señala la factura.</summary>
    [TestMethod]
    public void LosCamposDeLaPersonaAnadidaDicenLaHojaEnQueSeAnadio()
    {
        var (_, modelo, _, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;

        var resultado = modelo.AnadirUnaPersonaAMano("Hugo Prueba Ocho", "055-8888-9998");

        var suyos = modelo.Campos.Where(campo => campo.Tabla == TablaDeProcedencia.Personas && campo.RegistroId == resultado.PersonaId).ToList();
        Assert.HasCount(2, suyos);
        foreach (var campo in suyos) Assert.AreEqual(2, campo.PaginaPdf, $"«{campo.Campo}» de la persona a mano");
    }

    /// <summary>Sin decirle nada al modelo, la persona a mano sigue naciendo en la hoja del caso: lo de siempre no cambia.</summary>
    [TestMethod]
    public void SinCambiarDeHojaLaPersonaAManoNaceEnLaDelCaso()
    {
        var (servicios, modelo, _, _) = DocumentoDeDosHojas();

        var resultado = modelo.AnadirUnaPersonaAMano("Hugo Prueba Ocho", null);

        Assert.AreEqual(1, servicios.Almacen.Personas[resultado.PersonaId].PaginaPdf);
    }

    /// <summary>Una hoja que no existe no se apunta: cero o negativa se rechaza con su nombre.</summary>
    [TestMethod]
    public void UnaHojaQueNoExisteSeRechaza()
    {
        var (_, modelo, _, _) = DocumentoDeDosHojas();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => modelo.HojaDelante = 0);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => modelo.HojaDelante = -3);
        Assert.AreEqual(1, modelo.HojaDelante, "la que había se queda");
    }

    /// <summary>Abrir otro caso olvida la hoja del anterior: la de delante vuelve a ser la del caso nuevo.</summary>
    [TestMethod]
    public void AbrirOtroCasoVuelveALaHojaDeEseCaso()
    {
        var (servicios, modelo, _, _) = DocumentoDeDosHojas();
        modelo.HojaDelante = 2;
        var otro = BaseDeInicio.MeterCaso(servicios, "OTRO2609", "2026-09-24");
        servicios.Almacen.Casos[otro] = servicios.Almacen.Casos[otro] with { PaginaPdf = 3 };

        modelo.Cargar(otro);

        Assert.AreEqual(3, modelo.HojaDelante);
    }
}
