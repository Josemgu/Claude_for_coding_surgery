using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Completar;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Eliminar desde Corrección: una persona del documento abierto, con su pregunta y su acuse,
/// y lo que el modelo decide sin abrir ninguna ventana.
/// </summary>
/// <remarks>
/// <para><b>El encargo del dueño, 2026-09-14:</b> <i>«Agrega un botón en la Corrección de
/// eliminar información también. No lo tengo y es importante tenerlo»</i>. Y lo que dijo antes
/// y sigue vivo: <i>«Si quiero eliminar un nombre puedo hacerlo»</i> (09-07), <i>«dentro quiero
/// que me permita eliminar personas o agregar personas que quizás el escáner no contempló»</i>
/// (09-10).</para>
///
/// <para><b>Medido antes de tocar nada:</b> en <c>Fichas.App/Correccion/</c> no había ningún
/// botón ni operación de borrar (grep de <c>Borrar|Eliminar</c> fuera de comentarios: cero), e
/// <c>IPersonas</c> no tenía <c>Borrar</c>.</para>
///
/// <para>⛔ <b>Lo que estas pruebas NO cubren, y se dice:</b> la pregunta se hace en la
/// pantalla —es la excepción declarada del programa, el único cuadro que detiene el trabajo— y
/// eso no se puede probar sin ventana. Lo que sí se prueba es todo lo que la pregunta dice y
/// todo lo que pasa después de que él conteste que sí.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeEliminarEnCorreccion
{
    /// <summary>Un documento abierto con tantas personas como se pidan, con su procedencia leída.</summary>
    /// <param name="cuantasPersonas">Cuántas personas trae el papel.</param>
    private static (ServiciosFalsos Servicios, ModeloDeCorreccion Modelo, long CasoId) DocumentoCon(int cuantasPersonas)
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "SURB2609", "2026-09-17", cuantasPersonas: cuantasPersonas);
        var modelo = BancoDeLaCola.ModeloDe(servicios);
        modelo.Cargar(casoId);
        return (servicios, modelo, casoId);
    }

    // ═══════════════════════ La pregunta, antes de tocar nada ═══════════════════════

    /// <summary>Dada una persona del documento, cuando se planea eliminarla, entonces la pregunta la nombra, dice que no hay vuelta atrás y el botón lleva su nombre.</summary>
    [TestMethod]
    public void LaPreguntaNombraALaPersonaYDiceQueNoHayVueltaAtras()
    {
        var (_, modelo, _) = DocumentoCon(3);
        var segunda = modelo.Personas[1];

        var pregunta = modelo.PlanearEliminarPersona(segunda.Id);

        Assert.IsNotNull(pregunta);
        StringAssert.Contains(pregunta.Titulo, "Persona 2");
        StringAssert.Contains(pregunta.Pregunta, TextoDeEliminar.NoSePuedeDeshacer);
        StringAssert.Contains(pregunta.Pregunta, "2 personas");
        Assert.AreEqual("Eliminar a Persona 2", pregunta.TextoDelBoton);
        Assert.IsFalse(pregunta.EsLaUltima);
    }

    /// <summary>Dada la única persona del documento, cuando se planea eliminarla, entonces la pregunta avisa de que el documento se quedará sin nadie.</summary>
    [TestMethod]
    public void LaPreguntaAvisaCuandoEsLaUltima()
    {
        var (_, modelo, _) = DocumentoCon(1);

        var pregunta = modelo.PlanearEliminarPersona(modelo.Personas[0].Id);

        Assert.IsNotNull(pregunta);
        Assert.IsTrue(pregunta.EsLaUltima);
        StringAssert.Contains(pregunta.Pregunta, TextoDeEliminar.SeQuedaraSinNadie);
    }

    /// <summary>Planear no escribe nada: la cuenta de personas y de firmas es la misma después.</summary>
    [TestMethod]
    public void PlanearNoTocaLaBase()
    {
        var (servicios, modelo, casoId) = DocumentoCon(3);
        var firmasAntes = BancoDeLaCola.CuantasFirmasHayEn(servicios);

        modelo.PlanearEliminarPersona(modelo.Personas[0].Id);

        Assert.HasCount(3, servicios.Personas.DeCaso(casoId));
        Assert.AreEqual(firmasAntes, BancoDeLaCola.CuantasFirmasHayEn(servicios));
    }

    /// <summary>Una persona que no es de este documento no se puede planear: no hay pregunta.</summary>
    [TestMethod]
    public void UnaPersonaDeOtroDocumentoNoSePlanea()
    {
        var (servicios, modelo, _) = DocumentoCon(2);
        var otroCaso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        var ajena = servicios.Personas.DeCaso(otroCaso)[0];

        Assert.IsNull(modelo.PlanearEliminarPersona(ajena.Id));
        Assert.IsNull(modelo.PlanearEliminarPersona(9_999));
    }

    // ═══════════════════════ Eliminar, después del «sí» ═══════════════════════

    /// <summary>
    /// Dado un documento de tres personas, cuando se elimina una, entonces la base tiene dos
    /// para ese caso, cero renglones de procedencia de la borrada, y la pantalla lo refleja.
    /// </summary>
    [TestMethod]
    public void EliminarUnaDeTresDejaDosEnLaBaseYSinHuerfanos()
    {
        var (servicios, modelo, casoId) = DocumentoCon(3);
        var segunda = modelo.Personas[1];
        var camposAntes = modelo.Campos.Count;
        var procedenciaAntes = servicios.Almacen.Procedencias.Count;

        var resultado = modelo.EliminarPersona(segunda.Id);

        Console.WriteLine("== {0} · procedencia {1} → {2} ==",
            resultado.LineaDelAcuse, procedenciaAntes, servicios.Almacen.Procedencias.Count);

        Assert.IsTrue(resultado.SeBorro);
        Assert.HasCount(2, servicios.Personas.DeCaso(casoId), "la base no se quedó con dos");
        Assert.IsEmpty(servicios.Procedencia.DeRegistro(TablaDeProcedencia.Personas, segunda.Id),
            "quedaron renglones de procedencia huérfanos");
        Assert.HasCount(procedenciaAntes - 2, servicios.Almacen.Procedencias,
            "cayó procedencia de más o de menos: la persona tenía dos renglones, nombre y cédula");
        Assert.HasCount(2, modelo.Personas, "la pantalla sigue enseñando a la persona borrada");
        Assert.HasCount(camposAntes - 2, modelo.Campos, "sus dos campos siguen en la pantalla");
        Assert.IsFalse(resultado.EraLaUltima);
        Assert.AreEqual(2, resultado.QuedanPersonas);
        Assert.IsNotNull(servicios.Casos.Obtener(casoId), "eliminar una persona no borra el documento");
    }

    /// <summary>El acuse dice a quién y cuántas quedan: «Eliminada Persona 2 (2 personas quedan en el documento)».</summary>
    [TestMethod]
    public void ElAcuseDiceAQuienYCuantasQuedan()
    {
        var (_, modelo, _) = DocumentoCon(3);

        var resultado = modelo.EliminarPersona(modelo.Personas[1].Id);

        Assert.AreEqual("Eliminada Persona 2 (2 personas quedan en el documento).", resultado.LineaDelAcuse);
    }

    /// <summary>
    /// Dada la última persona, cuando se elimina, entonces el resultado dice que era la última y
    /// el documento queda sin nadie: es la pantalla la que entonces pregunta por el documento.
    /// </summary>
    [TestMethod]
    public void EliminarLaUltimaLoDiceYElDocumentoSeQuedaSinNadie()
    {
        var (servicios, modelo, casoId) = DocumentoCon(1);

        var resultado = modelo.EliminarPersona(modelo.Personas[0].Id);

        Assert.IsTrue(resultado.SeBorro);
        Assert.IsTrue(resultado.EraLaUltima);
        Assert.AreEqual(0, resultado.QuedanPersonas);
        Assert.IsTrue(modelo.SinNingunaPersonaLeida);
        Assert.IsEmpty(servicios.Personas.DeCaso(casoId));
        Assert.IsNotNull(servicios.Casos.Obtener(casoId), "el documento no se borra aquí: eso se pregunta aparte");
        StringAssert.Contains(resultado.LineaDelAcuse, TextoDeEliminar.SinNingunaPersonaDentro);
    }

    /// <summary>Eliminar una persona que no es de este documento no borra nada y lo dice.</summary>
    [TestMethod]
    public void EliminarUnaAjenaNoBorraNadaYLoDice()
    {
        var (servicios, modelo, casoId) = DocumentoCon(2);
        var otroCaso = BaseDeInicio.MeterCaso(servicios, "CASP2609", "2026-09-17", cuantasPersonas: 1);
        var ajena = servicios.Personas.DeCaso(otroCaso)[0];

        var resultado = modelo.EliminarPersona(ajena.Id);

        Assert.IsFalse(resultado.SeBorro);
        Assert.IsNotEmpty(resultado.Avisos, "un botón que no hace nada y calla es un botón roto");
        Assert.HasCount(2, servicios.Personas.DeCaso(casoId));
        Assert.HasCount(1, servicios.Personas.DeCaso(otroCaso), "se borró a alguien de otro documento");
    }

    /// <summary>Sin documento abierto no se borra nada y se dice; nunca se lanza.</summary>
    [TestMethod]
    public void SinDocumentoAbiertoNoSeBorraNada()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var modelo = BancoDeLaCola.ModeloDe(servicios);

        var resultado = modelo.EliminarPersona(1);

        Assert.IsFalse(resultado.SeBorro);
        Assert.IsNotEmpty(resultado.Avisos);
    }

    /// <summary>Regla permanente 5: eliminar no firma nada de nadie.</summary>
    [TestMethod]
    public void EliminarNoFirmaNiUnCampo()
    {
        var (servicios, modelo, _) = DocumentoCon(3);
        var antes = BancoDeLaCola.CuantasFirmasHayEn(servicios);

        modelo.EliminarPersona(modelo.Personas[0].Id);

        Assert.AreEqual(antes, BancoDeLaCola.CuantasFirmasHayEn(servicios));
    }

    /// <summary>
    /// Lo tecleado en los campos de la persona borrada se tira con ella; lo tecleado en los de
    /// las demás se conserva.
    /// </summary>
    [TestMethod]
    public void LoTecleadoEnLaBorradaSeVaYLoDeLasDemasSeQueda()
    {
        var (_, modelo, _) = DocumentoCon(2);
        var primera = modelo.Personas[0];
        var segunda = modelo.Personas[1];
        var campoDeLaPrimera = modelo.Campos.First(c => c.Tabla == TablaDeProcedencia.Personas && c.RegistroId == primera.Id);
        var campoDeLaSegunda = modelo.Campos.First(c => c.Tabla == TablaDeProcedencia.Personas && c.RegistroId == segunda.Id);
        modelo.Teclear(campoDeLaPrimera.Clave, "cambiado y sin guardar");
        modelo.Teclear(campoDeLaSegunda.Clave, "también cambiado");

        modelo.EliminarPersona(primera.Id);

        Assert.IsFalse(modelo.Campos.Any(c => c.RegistroId == primera.Id && c.Tabla == TablaDeProcedencia.Personas));
        var deLaSegunda = modelo.Campos.First(c => c.Clave == campoDeLaSegunda.Clave);
        Assert.AreEqual("también cambiado", modelo.ValorDe(deLaSegunda), "eliminar a una tiró lo tecleado en la otra");
        Assert.IsTrue(modelo.HayCambiosSinGuardar);
    }

    /// <summary>Cerrar deja el modelo como antes de abrir nada, sin el aviso de «ese caso ya no está».</summary>
    [TestMethod]
    public void CerrarDejaElModeloComoAntesDeAbrirNada()
    {
        var (_, modelo, _) = DocumentoCon(2);
        modelo.Teclear(modelo.Campos[0].Clave, "algo");

        modelo.Cerrar();

        Assert.IsNull(modelo.Caso);
        Assert.IsEmpty(modelo.Personas);
        Assert.IsEmpty(modelo.Campos);
        Assert.IsEmpty(modelo.AvisosDeLaCabecera, "cerrar no es abrir un caso que no existe: no hay nada que avisar");
        Assert.IsFalse(modelo.HayCambiosSinGuardar);
    }

    // ═══════════════════════ Añadir a un documento que YA tiene gente ═══════════════════════

    /// <summary>
    /// Dado un documento que ya tiene dos personas, cuando se añade una a mano, entonces tiene
    /// tres y la nueva va en la fila 3. Es lo del dueño del 2026-09-10; hasta hoy la pantalla solo
    /// lo ofrecía cuando no había ninguna.
    /// </summary>
    [TestMethod]
    public void AnadirAUnDocumentoQueYaTieneDosDejaTres()
    {
        var (servicios, modelo, casoId) = DocumentoCon(2);

        var resultado = modelo.AnadirUnaPersonaAMano("Peter Alcindor", "055-1111-3899");

        Assert.IsTrue(resultado.SeEscribio);
        var tres = servicios.Personas.DeCaso(casoId);
        Assert.HasCount(3, tres);
        Assert.AreEqual(3, tres.Single(p => p.Nombre == "Peter Alcindor").FilaFormulario);
        Assert.HasCount(3, modelo.Personas);
    }

    /// <summary>El título del cuadro de añadir cambia según haya alguien dentro o no; el modelo lo dice para que la pantalla no lo decida.</summary>
    [TestMethod]
    public void ElCuadroDeAnadirSeOfreceSiempreYDiceDistintoSegunHayaGente()
    {
        var (_, conGente, _) = DocumentoCon(2);
        var (_, sinNadie, _) = DocumentoCon(0);

        Assert.AreEqual(TextoDeLaPersonaAMano.Titulo, sinNadie.TituloDelCuadroDeLaPersonaAMano);
        Assert.AreEqual(TextoDeLaPersonaAMano.TituloConGenteDentro, conGente.TituloDelCuadroDeLaPersonaAMano);
        Assert.AreNotEqual(sinNadie.TituloDelCuadroDeLaPersonaAMano, conGente.TituloDelCuadroDeLaPersonaAMano);
    }

    // ═══════════════════════ Los textos, fuera del XAML ═══════════════════════

    /// <summary>Las tres formas del acuse de eliminar una persona, con el plural bien hecho.</summary>
    [TestMethod]
    public void ElAcuseDeEliminarUnaPersonaHaceBienElPlural()
    {
        Assert.AreEqual("Eliminada Ana (2 personas quedan en el documento).", TextoDeEliminar.AlEliminarUnaPersona("Ana", 2));
        Assert.AreEqual("Eliminada Ana (1 persona queda en el documento).", TextoDeEliminar.AlEliminarUnaPersona("Ana", 1));
        StringAssert.Contains(TextoDeEliminar.AlEliminarUnaPersona("Ana", 0), TextoDeEliminar.SinNingunaPersonaDentro);
    }

    /// <summary>El acuse de eliminar el documento nombra el documento y dónde quedó la copia.</summary>
    [TestMethod]
    public void ElAcuseDeEliminarElDocumentoNombraLaCopia()
    {
        var linea = TextoDeEliminar.AlEliminarElDocumento("CASP2609", @"C:\datos\fichas-antes-de-borrar-20260914-101010.db");

        Assert.AreEqual(
            @"Documento CASP2609 eliminado; copia en «C:\datos\fichas-antes-de-borrar-20260914-101010.db».", linea);
        StringAssert.Contains(TextoDeEliminar.AlEliminarElDocumento("CASP2609", null), "sin copia");
    }

    /// <summary>Cómo se llama una persona en la pregunta: por su nombre; sin nombre, por su cédula; sin ninguna, por su fila.</summary>
    [TestMethod]
    public void ComoSeLlamaUnaPersonaEnLaPregunta()
    {
        Assert.AreEqual("Ana", TextoDeEliminar.ComoSeLlama(new Persona { Nombre = "Ana", Mrn = "055-1111-3853" }));
        Assert.AreEqual("la persona con cédula 055-1111-3853", TextoDeEliminar.ComoSeLlama(new Persona { Mrn = "055-1111-3853" }));
        Assert.AreEqual("la persona de la fila 4", TextoDeEliminar.ComoSeLlama(new Persona { FilaFormulario = 4 }));
    }
}
