using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El Excel espejo de la base: una hoja por tabla, sin calcular nada y sin traducir nada.
/// </summary>
/// <remarks>
/// Las dos pruebas que mas valen son las dos que se pusieron rojas solas en el programa en
/// Python: que a ninguna hoja le falte una columna de su tabla, y que un numero con ceros
/// delante no se estropee. Una columna que entra en el esquema y no llega al espejo lo
/// convierte en un espejo que miente.
/// </remarks>
[TestClass]
public class PruebasDelEspejo
{
    /// <summary>Un caso con todos los campos que el espejo vuelca, incluido un número de unidad con cero delante para ver que Excel no se lo come.</summary>
    private static readonly Caso UnCaso = new()
    {
        Id = 12,
        NumeroCaso = "BALC2609",
        UnidadNumero = "0700016",
        UnidadNombre = "Cuatricentenaria",
        TemploNombre = "Santo Domingo",
        FechaViaje = "2026-10-15",
        CreadoEn = "2026-09-04 10:30:00",
        EstadoRecomendacion = "completa",
        EstadoDelCompanero = "completa",
        EstadoDelCompaneroPor = 1,
        EstadoDelCompaneroEn = "2026-09-04 10:30:00",
        Archivado = false,
        CapturaManual = true,
    };

    /// <summary>Una persona de <see cref="UnCaso"/> con cédula terminada en letra y los pasos contestados, para ver cada clase de valor en su celda.</summary>
    private static readonly Persona UnaPersona = new()
    {
        Id = 30,
        CasoId = 12,
        Mrn = "055-1111-385A",
        Nombre = "Elena Rosa Muestra",
        FilaFormulario = 1,
        OrdInvestidura = true,
        PasoPreparacion = true,
        PasoEntrevistas = false,
        // Los demas pasos van a nulo a proposito: es el tercer estado.
    };

    /// <summary>Una base donde nadie ha contestado todavia las seis preguntas.</summary>
    private static readonly Dictionary<long, FirmaDeLosPasos> SinNingunaFirma = [];

    /// <summary>
    /// Quien contesto las seis preguntas de <see cref="UnaPersona"/>, cuando y desde donde.
    /// </summary>
    /// <remarks>
    /// Son las tres columnas de la migracion 19. No viven en <see cref="Persona"/> —
    /// <c>Fichas.Contratos/Modelos</c> esta congelado— sino en <see cref="FirmaDeLosPasos"/>,
    /// que es de donde las saca <c>IPersonas.FirmasDeLosPasosDelCaso</c>. Por eso al espejo
    /// se le pasan aparte, en un diccionario por numero interno de persona.
    /// </remarks>
    private static readonly Dictionary<long, FirmaDeLosPasos> LasFirmas = new()
    {
        [30] = new FirmaDeLosPasos(9, "2026-09-06 09:10:00", "a mano en la pantalla"),
    };

    /// <summary>El espejo en memoria con el caso, la persona y su firma, y las otras tres tablas vacías.</summary>
    private static XLWorkbook Libro() => Espejo.Construir([UnCaso], [UnaPersona], LasFirmas, [], [], []);

    /// <summary>Vigila que las cinco pestañas se llamen como sus tablas y salgan en el orden de las columnas declaradas.</summary>
    [TestMethod]
    public void HayUnaHojaPorTablaYEnElOrdenEnQueSeDeclaran()
    {
        using var libro = Libro();
        CollectionAssert.AreEqual(
            new[] { "casos", "personas", "asignaciones", "documentos_ilegibles", "filas_descartadas" },
            libro.Worksheets.Select(h => h.Name).ToArray());
    }

    /// <summary>Vigila que la cabecera de <c>casos</c> sea columna a columna la del esquema, las 22 de la migración 18.</summary>
    [TestMethod]
    public void ALaHojaDeCasosNoLeFaltaNingunaColumnaDeSuTabla()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("casos");
        for (var numero = 1; numero <= Espejo.ColumnasDeCasos.Count; numero++)
            Assert.AreEqual(Espejo.ColumnasDeCasos[numero - 1].Nombre, hoja.Cell(1, numero).GetString());
        // 22 desde la migracion 18: `motivo_no_completa` y `motivo_del_companero`. El
        // espejo es donde el dueno mira los datos en crudo, asi que una columna que esta
        // en la base y no esta aqui es un dato que el no puede ver.
        Assert.HasCount(22, Espejo.ColumnasDeCasos, "las 22 columnas de `casos`");
    }

    /// <summary>Vigila que la cabecera de <c>personas</c> sea columna a columna la del esquema, las 28 de la migración 19.</summary>
    [TestMethod]
    public void ALaHojaDePersonasNoLeFaltaNingunaColumnaDeSuTabla()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("personas");
        for (var numero = 1; numero <= Espejo.ColumnasDePersonas.Count; numero++)
            Assert.AreEqual(Espejo.ColumnasDePersonas[numero - 1].Nombre, hoja.Cell(1, numero).GetString());
        // 28 desde la migracion 19: `pasos_por`, `pasos_en` y `pasos_origen`. El espejo es
        // donde el dueno mira los datos en crudo, asi que una columna que esta en la base y
        // no esta aqui es un dato que el no puede ver: sin estas tres, quien contesto las
        // seis preguntas del sistema del lider no sale por ningun sitio.
        Assert.HasCount(28, Espejo.ColumnasDePersonas, "las 28 columnas de `personas`");
    }

    /// <summary>
    /// Las tres columnas de la migracion 19 llevan quien contesto las seis, cuando y por que via.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>No son <c>propuesto_por</c> / <c>propuesto_en</c>.</b> Aquellas son de quien
    /// propuso el ESTADO desde su Excel; estas, de quien contesto las seis preguntas. Pueden
    /// ser dos personas distintas en la misma fila, y ese es justo el caso que hay que poder
    /// mirar en el espejo sin abrir la base.
    /// </remarks>
    [TestMethod]
    public void LasTresColumnasDeLaFirmaDeLosPasosLlevanQuienCuandoYPorQueVia()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("personas");

        Assert.AreEqual("9", hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "pasos_por")).GetString());
        Assert.AreEqual(
            new DateTime(2026, 9, 6, 9, 10, 0),
            hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "pasos_en")).GetDateTime(),
            "una marca de tiempo en texto se ordena alfabéticamente");
        Assert.AreEqual(
            "a mano en la pantalla",
            hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "pasos_origen")).GetString());
    }

    /// <summary>De quien no ha contestado las seis, las tres celdas salen vacias.</summary>
    /// <remarks>
    /// ⛔ Vacio y no un cero ni un nombre: nadie ha contestado, y el espejo no inventa un
    /// dato que la base no tiene (regla permanente 1).
    /// </remarks>
    [TestMethod]
    public void DeQuienNoHaContestadoLasSeisLasTresCeldasSalenVacias()
    {
        using var libro = Espejo.Construir([UnCaso], [UnaPersona], SinNingunaFirma, [], [], []);
        var hoja = libro.Worksheet("personas");

        foreach (var columna in new[] { "pasos_por", "pasos_en", "pasos_origen" })
        {
            Assert.IsTrue(
                hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, columna)).IsEmpty(),
                $"«{columna}» trae algo de una persona por la que nadie contestó.");
        }
    }

    /// <summary>La defensa entera contra que Excel se coma los ceros de delante.</summary>
    [TestMethod]
    public void ElMrnYElNumeroDeUnidadSalenComoTextoConSusCerosDelante()
    {
        using var libro = Libro();
        var mrn = libro.Worksheet("personas").Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "mrn"));
        Assert.AreEqual("@", mrn.Style.NumberFormat.Format);
        Assert.AreEqual("055-1111-385A", mrn.GetString());

        var unidad = libro.Worksheet("casos").Cell(2, IndiceDe(Espejo.ColumnasDeCasos, "unidad_numero"));
        Assert.AreEqual("@", unidad.Style.NumberFormat.Format);
        Assert.AreEqual("0700016", unidad.GetString(), "un entero se habría comido el cero de delante");
    }

    /// <summary>Vigila que una fecha ISO y una marca de tiempo ISO entren como fecha de Excel, no como texto.</summary>
    [TestMethod]
    public void LasFechasEntranComoFechaDeVerdadYNoComoTexto()
    {
        using var libro = Libro();
        var viaje = libro.Worksheet("casos").Cell(2, IndiceDe(Espejo.ColumnasDeCasos, "fecha_viaje"));
        Assert.AreEqual(XLDataType.DateTime, viaje.DataType, "una cadena se ordena alfabéticamente; filtrar por mes necesita que Excel sepa que es una fecha");
        Assert.AreEqual(new DateTime(2026, 10, 15), viaje.GetDateTime());

        var creado = libro.Worksheet("casos").Cell(2, IndiceDe(Espejo.ColumnasDeCasos, "creado_en"));
        Assert.AreEqual(new DateTime(2026, 9, 4, 10, 30, 0), creado.GetDateTime());
    }

    /// <summary>Vigila que un texto que no es fecha en una columna temporal se escriba tal cual.</summary>
    [TestMethod]
    public void UnaFechaQueNoSePuedeLeerSeDejaTalCualYNoSeInventa()
    {
        using var libro = Espejo.Construir([UnCaso with { FechaViaje = "el mes que viene" }], [], SinNingunaFirma, [], [], []);
        var viaje = libro.Worksheet("casos").Cell(2, IndiceDe(Espejo.ColumnasDeCasos, "fecha_viaje"));
        Assert.AreEqual("el mes que viene", viaje.GetString(), "una fecha inventada en un espejo es peor que una fecha fea");
    }

    /// <summary>Tres estados y no dos: el vacío es el que se pierde al traducir a «Sí» y «No».</summary>
    [TestMethod]
    public void LosPasosSalenComoCeroUnoOVacioYNoComoSiONo()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("personas");
        Assert.AreEqual("1", hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "paso_preparacion")).GetString());
        Assert.AreEqual("0", hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "paso_entrevistas")).GetString());
        Assert.IsTrue(hoja.Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "paso_informacion")).IsEmpty(),
            "«nadie lo ha mirado» no es lo mismo que «lo miró y dijo que no»");
    }

    /// <summary>Vigila las dos marcas estructurales de la fila 1 y que no lleve ninguna decorativa.</summary>
    [TestMethod]
    public void LaCabeceraVaCongeladaYConAutofiltroYSinNingunColor()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("casos");
        Assert.AreEqual(1, hoja.SheetView.SplitRow);
        Assert.IsTrue(hoja.AutoFilter.IsEnabled);
        Assert.IsFalse(hoja.Cell(1, 1).Style.Font.Bold, "es una base de datos, no un reporte: un color no es un dato");
    }

    /// <summary>Vigila que un nombre que empieza por «=» entre como texto y no como fórmula.</summary>
    [TestMethod]
    public void UnTextoQueEmpiezaPorIgualNoSeConvierteEnFormula()
    {
        using var libro = Espejo.Construir([UnCaso], [UnaPersona with { Nombre = "=Elena" }], LasFirmas, [], [], []);
        var celda = libro.Worksheet("personas").Cell(2, IndiceDe(Espejo.ColumnasDePersonas, "nombre"));
        Assert.AreEqual(XLDataType.Text, celda.DataType, "una celda que se calcula sola deja de ser un espejo");
        Assert.AreEqual("=Elena", celda.GetString());
    }

    /// <summary>Vigila que una tabla sin filas tenga igualmente su pestaña con la cabecera y nada debajo.</summary>
    [TestMethod]
    public void UnaTablaVaciaSaleConSuCabeceraYSinFilas()
    {
        using var libro = Libro();
        var hoja = libro.Worksheet("asignaciones");
        Assert.AreEqual("companero_id", hoja.Cell(1, 3).GetString(), "una hoja vacía dice «esta tabla existe y está vacía»");
        Assert.IsTrue(hoja.Cell(2, 1).IsEmpty());
    }

    // ─────────────────────── el archivo, y el archivo bloqueado ───────────────────────

    /// <summary>Vigila que regenerar deje el <c>.xlsx</c> definitivo y ningún <c>.parcial</c> al lado.</summary>
    [TestMethod]
    public void RegenerarEscribeElArchivoYNoDejaNingunParcial()
    {
        var carpeta = BaseInventada.CarpetaDePruebas();
        try
        {
            var ruta = Path.Combine(carpeta, "fichas.xlsx");
            var resultado = Espejo.Regenerar(ruta, [UnCaso], [UnaPersona], LasFirmas, [], [], []);
            Assert.IsTrue(resultado.Escrito);
            Assert.IsEmpty(resultado.Avisos);
            Assert.IsTrue(File.Exists(ruta));
            Assert.IsFalse(File.Exists(ruta + ".parcial"), "lo que está a medias se llama .parcial, y al terminar no queda nada a medias");
        }
        finally { try { Directory.Delete(carpeta, true); } catch (IOException) { } }
    }

    /// <summary>Vigila que la segunda regeneración reemplace la primera: el dato nuevo está y las filas no se acumulan.</summary>
    [TestMethod]
    public void RegenerarDosVecesDejaElArchivoAlDiaYNoDuplicaNada()
    {
        var carpeta = BaseInventada.CarpetaDePruebas();
        try
        {
            var ruta = Path.Combine(carpeta, "fichas.xlsx");
            Espejo.Regenerar(ruta, [UnCaso], [UnaPersona], LasFirmas, [], [], []);
            Espejo.Regenerar(ruta, [UnCaso with { EstadoRecomendacion = "no_completa" }], [UnaPersona], LasFirmas, [], [], []);

            using var libro = new XLWorkbook(ruta);
            var hoja = libro.Worksheet("casos");
            Assert.AreEqual("no_completa", hoja.Cell(2, IndiceDe(Espejo.ColumnasDeCasos, "estado_recomendacion")).GetString());
            Assert.IsTrue(hoja.Cell(3, 1).IsEmpty(), "se regenera entero: no se añade encima de lo anterior");
        }
        finally { try { Directory.Delete(carpeta, true); } catch (IOException) { } }
    }

    /// <summary>Con el espejo abierto en otro programa: aviso en español, sin caída, y el que estaba intacto.</summary>
    [TestMethod]
    public void ConElEspejoAbiertoPorOtroProgramaSeAvisaYElQueEstabaSeQuedaIntacto()
    {
        var carpeta = BaseInventada.CarpetaDePruebas();
        try
        {
            var ruta = Path.Combine(carpeta, "fichas.xlsx");
            Espejo.Regenerar(ruta, [UnCaso], [UnaPersona], LasFirmas, [], [], []);
            var tamanoAntes = new FileInfo(ruta).Length;

            using (File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var resultado = Espejo.Regenerar(ruta, [UnCaso], [UnaPersona, UnaPersona with { Id = 31 }], LasFirmas, [], [], []);
                Assert.IsFalse(resultado.Escrito);
                StringAssert.Contains(resultado.Avisos[0].Detalle!, "LOS DATOS SÍ QUEDARON GUARDADOS EN LA BASE");
                Assert.AreEqual(GravedadDeAviso.Problema, resultado.Avisos[0].Gravedad);
            }

            Assert.AreEqual(tamanoAntes, new FileInfo(ruta).Length, "el espejo que ya estaba se queda intacto");
        }
        finally { try { Directory.Delete(carpeta, true); } catch (IOException) { } }
    }

    /// <summary>La posición de una columna del espejo, contando desde 1 como Excel; lanza si no existe.</summary>
    /// <param name="columnas">Las columnas de esa hoja, en su orden.</param>
    /// <param name="nombre">El nombre de la columna en la base.</param>
    private static int IndiceDe(IReadOnlyList<ColumnaDelEspejo> columnas, string nombre)
    {
        for (var numero = 0; numero < columnas.Count; numero++)
            if (columnas[numero].Nombre == nombre)
                return numero + 1;
        throw new KeyNotFoundException(nombre);
    }
}
