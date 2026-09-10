using Fichas.App.Importar;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Ver y borrar los PDF que no se pudieron leer y no dejaron ningun documento.
/// </summary>
/// <remarks>
/// <para>Son la mitad que faltaba de lo que el dueno pidio dos veces el 2026-09-07:
/// <i>«no se contempla eliminar PDF o documentos que no tienen informacion; debe poder
/// eliminarlo»</i>. La otra mitad —el documento sin ninguna persona— la cubre
/// <see cref="PruebasDeLoQueEntroSinInformacion"/>.</para>
///
/// <para>⚠️ <b>Primero verlos, despues poder borrarlos.</b> Un boton de borrar sobre una
/// lista que nadie ve no sirve de nada, asi que aqui se mide lo que la pantalla ENSENA de
/// cada uno —su ruta y su motivo— antes que ningun borrado.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeBorrarLosPdfIlegibles : BaseDeImportacion
{
    // ══════════════════════════ Verlos: ruta y motivo ══════════════════════════

    /// <summary>
    /// Dado un PDF que no se pudo leer, cuando se mira lo que entro sin informacion,
    /// entonces su renglon trae la ruta ENTERA y el motivo dicho en espanol.
    /// </summary>
    /// <remarks>
    /// La ruta entera y no solo el nombre: es lo que hace falta para ir a abrir el papel y
    /// mirarlo antes de decidir. Borrar sin poder comprobar es borrar a ciegas.
    /// </remarks>
    [TestMethod]
    public void CadaPdfIlegibleSeVeConSuRutaYSuMotivo()
    {
        SembrarRenglon(@"C:\escaneos\enero\roto.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [], [@"C:\escaneos\enero\roto.pdf"]);

        Assert.HasCount(1, sinInformacion.RenglonesSinCaso);
        var renglon = sinInformacion.RenglonesSinCaso[0];
        Assert.AreEqual(@"C:\escaneos\enero\roto.pdf", renglon.RutaPdf);
        Assert.AreEqual("roto.pdf", renglon.Archivo);
        Assert.AreEqual(MotivosDeIlegible.NoSePudoAbrir, renglon.Motivo);
        Assert.Contains(@"C:\escaneos\enero\roto.pdf", renglon.Etiqueta, "El renglón no enseña la ruta.");
        Assert.Contains(renglon.MotivoEnEspanol, renglon.Etiqueta, "El renglón no enseña el motivo.");
        Assert.AreNotEqual(renglon.Motivo, renglon.MotivoEnEspanol, "El motivo se enseña en clave, no en español.");
    }

    /// <summary>
    /// Dado un motivo que esta guardado con un codigo que nadie previo, entonces se enseña
    /// el codigo tal cual en vez de dejar el renglon mudo.
    /// </summary>
    /// <remarks>
    /// La base del dueno lleva renglones escritos por el programa viejo en Python. Un
    /// codigo feo delante es mejor que un renglon vacio: al menos se puede buscar.
    /// </remarks>
    [TestMethod]
    public void UnMotivoQueNadieHaPrevistoSeEnsenaTalCual()
    {
        SembrarRenglon(@"C:\escaneos\raro.pdf", "un_motivo_del_python_viejo", casoId: null);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [], [@"C:\escaneos\raro.pdf"]);

        Assert.HasCount(1, sinInformacion.RenglonesSinCaso);
        Assert.Contains("un_motivo_del_python_viejo", sinInformacion.RenglonesSinCaso[0].Etiqueta);
    }

    /// <summary>Dado un renglon con hoja, entonces la hoja se dice; sin hoja, no se inventa.</summary>
    [TestMethod]
    public void LaHojaSeDiceSoloCuandoLaHay()
    {
        var conHoja = Datos.Ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = @"C:\escaneos\tanda.pdf",
            PaginaPdf = 4,
            Motivo = MotivosDeIlegible.SinTexto,
            RegistradoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(conHoja.SeEscribio);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [], [@"C:\escaneos\tanda.pdf"]);

        Assert.HasCount(1, sinInformacion.RenglonesSinCaso);
        Assert.Contains("hoja 4", sinInformacion.RenglonesSinCaso[0].Etiqueta);
    }

    // ══════════════════════════ Los textos de la pregunta ══════════════════════════

    /// <summary>
    /// Dado un plan de dos renglones, entonces el titulo y el boton llevan LA CIFRA delante
    /// y no un «¿seguro?».
    /// </summary>
    /// <remarks>
    /// ⚠️ El titulo NO se pide al plan. <c>PlanDeBorrado.Titulo</c> y
    /// <c>TextoDelBoton</c> cuentan la tabla <c>casos</c>, que aqui no entra, y dirian
    /// «Borrar 0 documentos». Esta prueba existe para que eso no se cuele nunca.
    /// </remarks>
    [TestMethod]
    public void ElTituloYElBotonLlevanLaCifraDelante()
    {
        SembrarRenglon(@"C:\escaneos\roto1.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);
        SembrarRenglon(@"C:\escaneos\roto2.pdf", MotivosDeIlegible.SinTexto, casoId: null);
        var plan = Datos.Ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones());

        Assert.Contains("2", TextosDeBorrarLosPdfIlegibles.Titulo(plan));
        Assert.Contains("2", TextosDeBorrarLosPdfIlegibles.TextoDelBoton(plan));
        Assert.DoesNotContain("0 documentos", TextosDeBorrarLosPdfIlegibles.Titulo(plan));
        Assert.DoesNotContain("0 documentos", TextosDeBorrarLosPdfIlegibles.TextoDelBoton(plan));
        Assert.DoesNotContain("seguro", TextosDeBorrarLosPdfIlegibles.Titulo(plan), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dada la pregunta, entonces dice donde quedo la copia, que no hay vuelta atras, y
    /// —lo que nadie mas dice— que el PDF del disco NO se borra.
    /// </summary>
    /// <remarks>
    /// ⛔ Es el punto que el dueno tiene que ver antes de contestar: lo que cae es la
    /// anotacion de que un archivo no se pudo leer, no el archivo. Sin esta frase creeria
    /// que borro un escaneo suyo que sigue en su carpeta.
    /// </remarks>
    [TestMethod]
    public void LaPreguntaDiceQueElPdfDelDiscoNoSeBorra()
    {
        SembrarRenglon(@"C:\escaneos\roto.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);
        var plan = Datos.Ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones());

        var pregunta = TextosDeBorrarLosPdfIlegibles.Pregunta(plan);

        Assert.Contains(plan.RutaDeLaCopia!, pregunta, "La pregunta no dice dónde quedó la copia.");
        Assert.Contains("no se puede deshacer", pregunta);
        Assert.Contains(TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra, pregunta);
        Assert.Contains("NO se borran", TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra);
    }

    /// <summary>Dado lo que se enseña en la zona, entonces tambien dice que el PDF se queda.</summary>
    /// <remarks>
    /// La frase va en la pantalla ADEMAS de en la pregunta: el dueno tiene que poder leerla
    /// antes de marcar nada, no solo cuando ya pulso el boton.
    /// </remarks>
    [TestMethod]
    public void LaPantallaDiceQueElPdfSeQuedaAntesDePulsarNada()
    {
        Assert.Contains("NO se borran", TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra);
        Assert.Contains("carpeta", TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra);
        Assert.Contains("volverán a aparecer", TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra);
    }

    // ══════════════════════════ Borrar de verdad, desde la pantalla ══════════════════════════

    /// <summary>
    /// Dado un PDF ilegible de la tanda, cuando se planea y se borra, entonces se fue de la
    /// base y deja de salir en la lista de la pantalla.
    /// </summary>
    [TestMethod]
    public void BorrarloLoQuitaDeLaBaseYDeLaLista()
    {
        SembrarRenglon(@"C:\escaneos\roto.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);
        var rutas = new[] { @"C:\escaneos\roto.pdf" };

        var antes = LoQueEntroSinInformacion.Ver(Datos.Casos, Datos.Ilegibles, [], rutas);
        Assert.HasCount(1, antes.RenglonesSinCaso);

        var plan = Datos.Ilegibles.PlanearBorradoDeRenglonesSinCaso(
            [antes.RenglonesSinCaso[0].RenglonId]);
        Assert.IsTrue(Datos.Ilegibles.BorrarRenglonesSinCaso(plan).SeBorro);

        Assert.AreEqual(0L, Contar("documentos_ilegibles"));
        Assert.IsEmpty(LoQueEntroSinInformacion.Ver(Datos.Casos, Datos.Ilegibles, [], rutas).RenglonesSinCaso);
    }

    /// <summary>
    /// Dado que se cancela, entonces el renglon sigue en la base y la copia esta donde se dijo.
    /// </summary>
    /// <remarks>Cancelar es no llamar a Borrar: es lo que hace la operacion cuando el dueño
    /// pulsa «No borrar nada». La copia ya está hecha, y eso es justo lo que se anuncia.</remarks>
    [TestMethod]
    public void SiSeCancelaElPdfIlegibleSigueEnLaBase()
    {
        SembrarRenglon(@"C:\escaneos\roto.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);

        var plan = Datos.Ilegibles.PlanearBorradoDeRenglonesSinCaso(IdsDeLosRenglones());

        Assert.AreEqual(1L, Contar("documentos_ilegibles"), "Planear borró algo, y planear no borra.");
        Assert.IsTrue(File.Exists(plan.RutaDeLaCopia!), "La copia anunciada no está donde se dijo.");
        Assert.Contains(RespaldoAntesDeBorrar.MarcaDeLaCopia, plan.RutaDeLaCopia);
    }

    /// <summary>
    /// Dado un renglon que SI tiene documento, entonces la pantalla no lo ofrece y el
    /// borrado tampoco lo alcanza.
    /// </summary>
    [TestMethod]
    public void ElRenglonQueTieneDocumentoNiSeOfreceNiSeBorra()
    {
        var caso = Datos.Casos.Guardar(new Caso
        {
            NumeroCaso = "CASP2609",
            FechaViaje = "2026-09-20",
            UnidadNumero = "123456",
            RutaPdf = @"C:\escaneos\CASP2609.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(caso.SeEscribio);
        var conCaso = SembrarRenglon(@"C:\escaneos\CASP2609.pdf", MotivosDeIlegible.SinTexto, caso.Id);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [caso.Id], [@"C:\escaneos\CASP2609.pdf"]);

        Assert.IsEmpty(sinInformacion.RenglonesSinCaso, "La pantalla ofrece un renglón que tiene documento.");
        Assert.IsFalse(Datos.Ilegibles.PlanearBorradoDeRenglonesSinCaso([conCaso]).SePuedeBorrar);
        Assert.AreEqual(1L, Contar("documentos_ilegibles"));
    }

    /// <summary>
    /// Dado un PDF ilegible que ya estaba en la base antes de esta tanda, entonces no se
    /// ofrece: la pantalla habla de lo que se acaba de importar.
    /// </summary>
    [TestMethod]
    public void NoSeOfreceBorrarUnPdfIlegibleDeOtraTanda()
    {
        SembrarRenglon(@"C:\escaneos\viejo.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);
        SembrarRenglon(@"C:\escaneos\deAhora.pdf", MotivosDeIlegible.NoSePudoAbrir, casoId: null);

        var sinInformacion = LoQueEntroSinInformacion.Ver(
            Datos.Casos, Datos.Ilegibles, [], [@"C:\escaneos\deAhora.pdf"]);

        Assert.HasCount(1, sinInformacion.RenglonesSinCaso);
        Assert.AreEqual(@"C:\escaneos\deAhora.pdf", sinInformacion.RenglonesSinCaso[0].RutaPdf);
    }

    // ══════════════════════════ Utilidades de estas pruebas ══════════════════════════

    private long SembrarRenglon(string rutaPdf, string motivo, long? casoId)
    {
        var escrito = Datos.Ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = rutaPdf,
            Motivo = motivo,
            Detalle = "prueba",
            LineasLeidas = 0,
            CasoId = casoId,
            RegistradoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(escrito.SeEscribio, "No se pudo sembrar el renglón de la prueba.");
        return escrito.Id;
    }

    private IReadOnlyCollection<long> IdsDeLosRenglones()
        => [.. Datos.Ilegibles
                .Listar(Contratos.Consultas.FiltroDeIlegibles.Todo, Contratos.Consultas.Pagina.Primera(100))
                .Elementos.Select(renglon => renglon.Id)];
}
