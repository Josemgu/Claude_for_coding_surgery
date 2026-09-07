using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Cuando varias hojas se juntan en un caso: de qué hoja sale cada campo, y qué pasa
/// cuando dos hojas no dicen lo mismo.
/// </summary>
/// <remarks>
/// <para><b>La pregunta que cierra.</b> Un formulario de grupo son seis hojas con el mismo
/// número de caso y <c>GuardadoDeHojas</c> las une en UN caso. Las personas de las seis
/// hojas quedan en ese caso, pero los campos del caso —fecha, unidad, templo— salen de una
/// sola hoja. ¿De cuál? ¿Y qué pasa con lo que leyeron las otras cinco?</para>
///
/// <para><b>Lo medido el 2026-09-07 sobre los diez PDF reales del dueño</b>, que es de donde
/// salen estas pruebas y no de leer el código:</para>
/// <code>
/// SURB2609 (6 hojas)  pág 1-4 · unidad n.º «21·····» · nombre «» · fecha 2026-09-05 · templo «Belem Temple Brazil»
///                     pág 5-6 · unidad n.º «»        · nombre «» · fecha «»         · templo «»
/// </code>
/// <para>O sea que en el documento de verdad hay DOS situaciones y no una, y confundirlas es
/// justo el error que hay que no cometer:</para>
/// <list type="number">
/// <item><b>Cuatro hojas leen lo MISMO.</b> No hay nada que decidir y no se avisa de nada.</item>
/// <item><b>Dos hojas no leen NADA.</b> «No lo leí» no contradice a nadie: sus personas
/// entran en el caso y el caso conserva lo que leyeron las otras cuatro. Tampoco se avisa,
/// y avisar aquí llenaría la franja de avisos falsos en cuanto un escaneo salga flojo.</item>
/// </list>
///
/// <para><b>La tercera situación NO aparece en los diez documentos —medida: 0 veces— pero el
/// código la tiene:</b> que la hoja que abrió el caso NO leyera un campo y otra hoja del
/// mismo documento SÍ. Hoy eso se tira en silencio: la hoja se une, su lectura no se guarda
/// en ninguna parte y nadie se entera de que el programa llegó a leerla. Eso es lo que estas
/// pruebas cierran.</para>
///
/// <para>⛔ <b>Y lo que NO se hace, con su motivo medido.</b> El valor de la otra hoja no se
/// mete solo en el caso. <c>procedencia_campo</c> no tiene columna de página —16 columnas,
/// ninguna es la hoja— y la pantalla de corrección dibuja la banda de un campo del caso
/// sobre <c>casos.pagina_pdf</c>, que es la hoja que lo abrió
/// (<c>ModeloDeCorreccion.Guardado.cs:464</c>, <c>PaginaPdf = caso.PaginaPdf</c>). Meter ahí
/// el valor de la página 4 pintaría el recuadro de la página 4 encima de la página 1: sería
/// volver a cruzar las informaciones, que es el daño que se está arreglando.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeDeQueHojaSaleCadaCampo : BaseDeImportacion
{
    private const string Documento = "C:/pdfs/grupo.pdf";

    /// <summary>
    /// Dadas seis hojas del mismo caso, cuando se guardan, entonces sale UN caso con las
    /// personas de las seis y sus cinco campos salen de la hoja que lo abrió.
    /// </summary>
    /// <remarks>
    /// Es el invariante del que vive todo lo demás: hoy <b>se puede</b> decir de qué hoja
    /// salió cada campo del caso, y se puede porque salen todos de la misma —la de
    /// <c>casos.pagina_pdf</c>—. No está escrito en ningún sitio y nada lo vigilaba: el día
    /// que alguien rellene un campo del caso desde otra hoja, esta prueba se pone roja y
    /// obliga a hablar de la columna que falta en vez de dejarlo pasar.
    /// </remarks>
    [TestMethod]
    public void LosCincoCamposDelCasoSalenDeLaHojaQueLoAbrio()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(SeisHojasComoLasDelDueno());

        var casoId = salida[0].CasoId!.Value;
        Assert.AreEqual(1L, Contar("casos"), "las seis hojas son UN caso.");
        Assert.AreEqual(6L, Contar("personas"), "y las seis personas están dentro.");
        Assert.IsTrue(salida.All(hoja => hoja.CasoId == casoId), "ninguna hoja se fue por su lado.");

        var caso = Datos.Casos.Obtener(casoId)!;
        Assert.AreEqual(1, caso.PaginaPdf, "el caso lo abrió la página 1.");

        // Y lo que el caso guarda es LO QUE LEYÓ ESA PÁGINA, campo por campo.
        Assert.AreEqual("2026-09-05", caso.FechaViaje);
        Assert.AreEqual("700003", caso.UnidadNumero);
        Assert.AreEqual("Belem Temple Brazil", caso.TemploNombre);
        Assert.IsNull(caso.UnidadNombre, "la página 1 no leyó el nombre de la unidad.");
    }

    /// <summary>
    /// Dadas dos hojas del mismo caso donde la primera NO leyó la unidad y la segunda SÍ,
    /// cuando se guardan, entonces queda dicho de qué página salió esa lectura.
    /// </summary>
    /// <remarks>
    /// El renglón lleva <c>pagina_pdf</c> y <c>caso_id</c>, que son las dos columnas que
    /// <c>procedencia_campo</c> no tiene: es el único sitio de la base donde hoy se puede
    /// atribuir a una hoja un valor que el caso no lleva.
    /// </remarks>
    [TestMethod]
    public void LoQueLeeOtraHojaYElCasoNoTieneQuedaDichoConSuPagina()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja(Documento, 1, "SURB2609", [("Uno", "055-0000-0001")], unidadNombre: null),
            Hoja(Documento, 2, "SURB2609", [("Dos", "055-0000-0002")], unidadNombre: "Paramaribo Branch"),
        ]);

        Assert.AreEqual(1L, Contar("casos"), "la hoja se une igual: no leer no separa.");
        Assert.AreEqual(salida[0].CasoId, salida[1].CasoId);

        var renglon = RenglonesDelDocumento().SingleOrDefault(uno => uno.Motivo == MotivosDeIlegible.LoLeyoOtraHoja);
        Assert.IsNotNull(renglon, "la lectura de la página 2 no puede desaparecer en silencio.");
        Assert.AreEqual(2, renglon.PaginaPdf, "el renglón dice DE QUÉ hoja salió.");
        Assert.AreEqual(salida[0].CasoId, renglon.CasoId, "y a qué caso le falta.");
        Assert.Contains("Paramaribo Branch", renglon.Detalle ?? "", "y qué decía el papel.");
    }

    /// <summary>
    /// Y esa hoja deja UN aviso, no uno por campo.
    /// </summary>
    /// <remarks>
    /// La franja enseña UNA línea y la cuenta de las que hay detrás
    /// (<c>FranjaDeAvisos.Repintar</c>). El dueño ya rechazó por escrito los avisos que
    /// ocupan media pantalla el 2026-09-04 —«las letras en amarillo toman todo el espacio»—,
    /// así que tres campos vacíos en la misma hoja son UN hecho y no tres.
    /// </remarks>
    [TestMethod]
    public void LaHojaQueAportaTresCamposDejaUnSoloAviso()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja(Documento, 1, "SURB2609", [("Uno", "055-0000-0001")],
                 fechaViaje: null, unidadNumero: null, temploNombre: null, unidadNombre: null),
            Hoja(Documento, 2, "SURB2609", [("Dos", "055-0000-0002")],
                 fechaViaje: "2026-09-05", unidadNumero: "700003",
                 temploNombre: "Belem Temple Brazil", unidadNombre: "Paramaribo Branch"),
        ]);

        Assert.HasCount(1, salida[1].Avisos, "cuatro campos son UN aviso, no cuatro.");
        Assert.HasCount(1, salida[1].Renglones, "y UN renglón, no cuatro.");
    }

    /// <summary>
    /// Una hoja que NO lee nada no deja ni aviso ni renglón, aunque el caso sí lo tenga.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta es la mitad que hace útil a la otra, y no es simétrica con ella.</b> «No lo
    /// leí» no es una contradicción ni una aportación: no hay nada que guardar y no hay nada
    /// que decidir. Las páginas 5 y 6 del grupo real del dueño son exactamente esto —los
    /// cuatro campos vacíos, medido—, así que avisar aquí pondría 2 avisos en CADA formulario
    /// de grupo que se importe. Un aviso que sale siempre no lo lee nadie.
    /// </remarks>
    [TestMethod]
    public void LaHojaQueNoLeeNadaSeUneEnSilencio()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja(Documento, 1, "SURB2609", [("Uno", "055-0000-0001")],
                 fechaViaje: "2026-09-05", unidadNumero: "700003",
                 temploNombre: "Belem Temple Brazil", unidadNombre: "Paramaribo Branch"),
            Hoja(Documento, 2, "SURB2609", [("Dos", "055-0000-0002")],
                 fechaViaje: null, unidadNumero: null, temploNombre: null, unidadNombre: null),
        ]);

        Assert.AreEqual(1L, Contar("casos"));
        Assert.IsEmpty(salida[1].Avisos, "callarse no es contradecir: ni un aviso.");
        Assert.IsEmpty(salida[1].Renglones, "ni un renglón.");
        Assert.AreEqual(2L, Contar("personas"), "y la persona de la hoja 2 entra igual.");
    }

    /// <summary>
    /// Decir OTRA cosa sí separa: la hoja entra en su propio caso, con su aviso.
    /// </summary>
    /// <remarks>
    /// Es el contraste que explica por qué las dos situaciones no se tratan igual. Aquí las
    /// dos lecturas no pueden ser ciertas a la vez y el programa NO elige entre ellas: cada
    /// una se queda con su hoja y el dueño decide. Allí no hay dos lecturas, hay una.
    /// </remarks>
    [TestMethod]
    public void DecirOtraCosaSeparaLaHojaYCallarseNo()
    {
        var contradice = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja(Documento, 1, "SURB2609", [("Uno", "055-0000-0001")], unidadNumero: "700003"),
            Hoja(Documento, 2, "SURB2609", [("Dos", "055-0000-0002")], unidadNumero: "999999"),
        ]);

        Assert.AreNotEqual(contradice[0].CasoId, contradice[1].CasoId, "contradecir separa.");
        Assert.Contains(MotivosDeIlegible.HojaAparte, contradice[1].Renglones.ToArray());
        Assert.HasCount(1, contradice[1].Avisos, "y lo dice en UNA línea.");
    }

    /// <summary>
    /// El grupo de seis del dueño, tal como se lee de verdad: UN aviso en toda la tanda.
    /// </summary>
    /// <remarks>
    /// <b>Aquí está la línea, y es una medición y no un gusto.</b> Sobre las 20 hojas reales
    /// de los diez PDF del dueño, el aviso nuevo salta <b>0 veces</b>: las hojas que leen la
    /// unidad leen todas lo mismo, y las que no la leen no leen ningún campo. El aviso solo
    /// aparece cuando una hoja aporta algo que el caso no tiene, que en los documentos de
    /// verdad no pasó ni una vez. Por eso no puede inundar la franja.
    /// </remarks>
    [TestMethod]
    public void ElGrupoDeSeisRealNoDejaNiUnAviso()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(SeisHojasComoLasDelDueno());

        var avisos = salida.Sum(hoja => hoja.Avisos.Count);
        Assert.AreEqual(0, avisos, $"seis hojas del grupo real dejaron {avisos} avisos.");
        Assert.IsEmpty(salida.SelectMany(hoja => hoja.Renglones), "y ningún renglón.");
    }

    /// <summary>
    /// Las seis hojas del <c>SURB2609</c> con lo que el lector saca de ellas de verdad.
    /// </summary>
    /// <remarks>
    /// Los valores son los medidos el 2026-09-07 con
    /// <c>PruebaDeLaUnidadEnLosDiezDocumentos.ElNombreDeUnidadNuncaEsElNumeroDeUnidad</c>
    /// sobre el PDF real. Los dígitos del número de unidad van cambiados: son datos
    /// personales y no entran en el repositorio.
    /// </remarks>
    private static IReadOnlyList<Fichas.Lectura.HojaLeida> SeisHojasComoLasDelDueno()
    {
        var leidas = Enumerable.Range(1, 4).Select(pagina => Hoja(
            Documento, pagina, "SURB2609", [($"Persona {pagina}", $"055-0000-000{pagina}")],
            fechaViaje: "2026-09-05", unidadNumero: "700003",
            temploNombre: "Belem Temple Brazil", unidadNombre: null));

        var vacias = Enumerable.Range(5, 2).Select(pagina => Hoja(
            Documento, pagina, "SURB2609", [($"Persona {pagina}", $"055-0000-000{pagina}")],
            fechaViaje: null, unidadNumero: null, temploNombre: null, unidadNombre: null));

        return [.. leidas, .. vacias];
    }

    /// <summary>Los renglones que este documento dejó, enteros y no solo su motivo.</summary>
    private RenglonIlegible[] RenglonesDelDocumento()
        => [.. Datos.Ilegibles
            .Listar(new FiltroDeIlegibles(RutaPdf: Documento), Pagina.Primera(50))
            .Elementos];
}
