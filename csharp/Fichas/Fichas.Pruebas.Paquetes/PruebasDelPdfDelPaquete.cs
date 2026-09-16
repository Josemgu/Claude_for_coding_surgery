using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El PDF unido que va con el Excel en el paquete de un companero.
/// </summary>
/// <remarks>
/// <para>El criterio del dueno, en observables: <i>«generar el paquete de un companero con N
/// casos produce un Excel y un PDF; ese PDF se abre y tiene las hojas de esos N casos, en el
/// mismo orden que el Excel»</i>, y <i>«si el PDF de un caso no se puede leer o su ruta ya no
/// existe, el paquete NO se cae: sale igual, sin esa hoja, y se dice cual falta y por que»</i>.</para>
///
/// <para>Cada prueba de aqui ABRE el archivo generado y lee sus hojas. Contar paginas no
/// bastaria: dos hojas en el orden cambiado dan el mismo numero y rompen el encargo entero,
/// porque el companero mira la fila 3 del Excel y espera la hoja 3 del PDF.</para>
/// </remarks>
[TestClass]
public class PruebasDelPdfDelPaquete
{
    /// <summary>La base en memoria de esta prueba, montada en <see cref="Montar"/>.</summary>
    private BaseInventada _banco = null!;
    /// <summary>La carpeta temporal donde se fabrican los PDF y se escribe el unido; se borra en <see cref="Recoger"/>.</summary>
    private string _carpeta = null!;
    /// <summary>El número interno del compañero de prueba al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Monta la base en memoria, la carpeta temporal y el compañero de cada prueba.</summary>
    [TestInitialize]
    public void Montar()
    {
        _banco = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _banco.Companero("Sandy");
    }

    /// <summary>Borra la carpeta temporal con los PDF fabricados.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* una carpeta temporal que no se deja borrar no invalida la medicion */ }
    }

    /// <summary>La ruta de un archivo de salida dentro de la carpeta temporal.</summary>
    /// <param name="nombre">El nombre del archivo, con su extensión.</param>
    private string Destino(string nombre) => Path.Combine(_carpeta, nombre);

    // ─────────────── el criterio principal: N casos, N hojas, en orden ───────────────

    /// <summary>Vigila el criterio principal: tres casos del mismo número dan tres hojas, en el orden de sus renglones y no en el del archivo.</summary>
    [TestMethod]
    public void ConTresCasosElPdfTraeLasTresHojasEnElOrdenDelExcel()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS", "HOJA-TRES");
        var primero = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 3);
        var segundo = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        var tercero = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 2);
        _banco.Persona(primero, "Uno", "055-1111-3853");
        _banco.Persona(segundo, "Dos", "055-1111-3854");
        _banco.Persona(tercero, "Tres", "055-1111-3855");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [primero, segundo, tercero], ruta);

        Assert.IsTrue(resultado.SeEscribio, "el PDF del paquete tiene que escribirse");
        Assert.IsTrue(File.Exists(ruta), "que diga que se escribio no es que el archivo este");
        CollectionAssert.AreEqual(
            new[] { "HOJA-TRES", "HOJA-UNO", "HOJA-DOS" },
            PdfDePrueba.RotulosDe(ruta).ToArray(),
            "las hojas van en el orden de los casos, que es el del Excel; no en el del archivo de origen");
    }

    /// <summary>Vigila que las hojas sigan el orden de los casos pedidos aunque sus páginas en el escaneo vayan al revés.</summary>
    [TestMethod]
    public void ElOrdenDelPdfEsElMismoQueElDeLasFilasDelExcel()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS", "HOJA-TRES");
        var primero = _banco.Caso("AAAA2609", rutaPdf: papel, paginaPdf: 2);
        var segundo = _banco.Caso("BBBB2609", rutaPdf: papel, paginaPdf: 3);
        var tercero = _banco.Caso("CCCC2609", rutaPdf: papel, paginaPdf: 1);
        _banco.Persona(primero, "Uno", "055-1111-3853");
        _banco.Persona(segundo, "Dos", "055-1111-3854");
        _banco.Persona(tercero, "Tres", "055-1111-3855");
        IReadOnlyList<long> casos = [primero, segundo, tercero];

        var excel = Destino("paquete.xlsx");
        var pdf = Destino("paquete.pdf");
        _banco.Paquetes.GenerarExcelDeCompanero(_sandy, casos, excel);
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, casos, pdf);

        using var libro = new XLWorkbook(excel);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var columna = Columnas.IndiceDe("numero_caso");
        var enElExcel = new[]
        {
            hoja.Cell(Columnas.PrimeraFilaDeDatos, columna).GetString(),
            hoja.Cell(Columnas.PrimeraFilaDeDatos + 1, columna).GetString(),
            hoja.Cell(Columnas.PrimeraFilaDeDatos + 2, columna).GetString(),
        };

        CollectionAssert.AreEqual(new[] { "AAAA2609", "BBBB2609", "CCCC2609" }, enElExcel);
        CollectionAssert.AreEqual(
            new[] { "HOJA-DOS", "HOJA-TRES", "HOJA-UNO" },
            PdfDePrueba.RotulosDe(pdf).ToArray(),
            "fila 1 del Excel -> hoja 1 del PDF, y asi con todas");
    }

    /// <summary>Vigila que un escaneo de grupo aporte una hoja distinta a cada caso que la pida.</summary>
    [TestMethod]
    public void DosCasosDelMismoArchivoEnHojasDistintasSalenLosDos()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "grupo.pdf", "FAMILIA-A", "FAMILIA-B");
        var uno = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        var otro = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 2);
        _banco.Persona(uno, "Uno", "055-1111-3853");
        _banco.Persona(otro, "Dos", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [uno, otro], ruta);

        CollectionAssert.AreEqual(new[] { "FAMILIA-A", "FAMILIA-B" }, PdfDePrueba.RotulosDe(ruta).ToArray());
    }

    /// <summary>Vigila que un documento duplicado lleve su hoja dos veces, para no descolocar lo de abajo.</summary>
    [TestMethod]
    public void DosCasosQueApuntanALaMismaHojaLaRepitenEnVezDeQuitarla()
    {
        // Un duplicado es un caso propio con su fila en el Excel. Si se quitara su hoja del
        // PDF, la fila 2 del Excel dejaria de corresponder a la hoja 2 y se descolocarian
        // TODAS las de abajo, que es peor que una hoja repetida.
        var papel = PdfDePrueba.Escribir(_carpeta, "repetido.pdf", "LA-MISMA");
        var uno = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        var copia = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        _banco.Persona(uno, "Uno", "055-1111-3853");
        _banco.Persona(copia, "Uno otra vez", "055-1111-3853");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [uno, copia], ruta);

        CollectionAssert.AreEqual(new[] { "LA-MISMA", "LA-MISMA" }, PdfDePrueba.RotulosDe(ruta).ToArray());
    }

    // ─────────────── lo que falla y NO tumba el paquete ───────────────

    /// <summary>Vigila que un archivo que ya no está deje una hoja de aviso en su sitio y el aviso nombre el caso.</summary>
    [TestMethod]
    public void SiLaRutaDeUnCasoYaNoExisteElPdfLlevaSuAvisoYSeDiceCual()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS");
        var bueno = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var perdido = _banco.Caso("PERD2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        var otroBueno = _banco.Caso("BUEN2610", rutaPdf: papel, paginaPdf: 2);
        _banco.Persona(bueno, "Uno", "055-1111-3853");
        _banco.Persona(perdido, "Dos", "055-1111-3854");
        _banco.Persona(otroBueno, "Tres", "055-1111-3855");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [bueno, perdido, otroBueno], ruta);
        var hojas = PdfDePrueba.RotulosDe(ruta);

        Assert.IsTrue(resultado.SeEscribio, "una hoja que falta no puede tumbar el paquete entero");
        Assert.HasCount(3, hojas);
        Assert.AreEqual("HOJA-UNO", hojas[0]);
        Assert.Contains("PERD2609", hojas[1], StringComparison.Ordinal);
        Assert.AreEqual("HOJA-DOS", hojas[2]);
        Assert.IsTrue(
            resultado.Avisos.Any(aviso => aviso.Linea.Contains("PERD2609", StringComparison.Ordinal)),
            "el aviso tiene que NOMBRAR el caso que falta; «falta 1 hoja» obliga a buscarla y entonces no se busca");
        Assert.IsTrue(
            resultado.Avisos.Any(aviso => (aviso.Detalle ?? string.Empty).Contains("ya-no-esta.pdf", StringComparison.Ordinal)),
            "y tiene que decir el archivo que se buscó, que es el motivo");
    }

    /// <summary>
    /// Una hoja que falta NO desplaza a las de abajo: en su sitio va una hoja de aviso.
    /// </summary>
    /// <remarks>
    /// <para>Decidido por el dueño el 2026-09-05, con estas palabras: <i>«Sí, mete la hoja de
    /// aviso en el hueco»</i>. Sustituye a la prueba que documentaba el desplazamiento.</para>
    ///
    /// <para>⛔ Es la prueba que impide que esto se vuelva a romper, y por eso no mira solo el
    /// número de hojas: mira la POSICIÓN. Con la hoja de aviso puesta al final en vez de en su
    /// sitio, el número de hojas seguiría siendo 3 y el compañero seguiría contestando la fila
    /// 3 mirando el documento de la 2. La medición que importa es que la hoja 3 del PDF sea la
    /// del tercer renglón del Excel.</para>
    /// </remarks>
    [TestMethod]
    public void UnaHojaQueFaltaNoDesplazaALasDeAbajoPorqueEnSuSitioVaUnAviso()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS", "HOJA-TRES");
        var primero = _banco.Caso("AAAA2609", rutaPdf: papel, paginaPdf: 1);
        var roto = _banco.Caso("BBBB2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        var tercero = _banco.Caso("CCCC2609", rutaPdf: papel, paginaPdf: 3);
        _banco.Persona(primero, "Uno", "055-1111-3853");
        _banco.Persona(roto, "Dos", "055-1111-3854");
        _banco.Persona(tercero, "Tres", "055-1111-3855");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [primero, roto, tercero], ruta);
        var hojas = PdfDePrueba.RotulosDe(ruta);

        Assert.HasCount(3, hojas, "tres renglones en el Excel, tres hojas en el PDF");
        Assert.AreEqual("HOJA-UNO", hojas[0]);
        Assert.Contains("BBBB2609", hojas[1], StringComparison.Ordinal, "en el hueco va el aviso del que falta");
        Assert.AreEqual("HOJA-TRES", hojas[2], "y la fila 3 del Excel sigue siendo la hoja 3 del PDF");
    }

    /// <summary>La hoja de aviso dice a quién se le está contestando y por qué falta el papel.</summary>
    /// <remarks>
    /// El dueño lo pidió con esas tres cosas: número de caso, nombre de la persona o personas, y
    /// el motivo. Sin los nombres, el compañero tiene una hoja que dice «falta un documento» y
    /// no sabe qué renglón del Excel le corresponde, que es justo lo que la hoja viene a evitar.
    /// </remarks>
    [TestMethod]
    public void LaHojaDeAvisoNombraElCasoALasPersonasYElMotivo()
    {
        var perdido = _banco.Caso("PERD2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        _banco.Persona(perdido, "Elena Rosa Muestra", "055-1111-3853");
        _banco.Persona(perdido, "Ana Prueba", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [perdido], ruta);
        var aviso = PdfDePrueba.RotulosDe(ruta)[0];

        Assert.Contains("PERD2609", aviso, StringComparison.Ordinal, "el número de caso");
        Assert.Contains("Elena Rosa Muestra", aviso, StringComparison.Ordinal, "la primera persona");
        Assert.Contains("Ana Prueba", aviso, StringComparison.Ordinal, "y la segunda");
        Assert.Contains("no se pudo leer", aviso, StringComparison.Ordinal, "y el motivo");
    }

    /// <summary>La hoja de aviso mide lo mismo que las demás del paquete.</summary>
    /// <remarks>
    /// Lo pidió el dueño: <i>«su cabecera igual que las demás, para que al hojear el PDF no
    /// parezca que se acabó el paquete»</i>. Una hoja de otro tamaño entre veinte iguales se
    /// lee como el final del documento.
    /// </remarks>
    [TestMethod]
    public void LaHojaDeAvisoMideLoMismoQueLasDemas()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS");
        var bueno = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var perdido = _banco.Caso("PERD2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        _banco.Persona(bueno, "Uno", "055-1111-3853");
        _banco.Persona(perdido, "Dos", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [bueno, perdido], ruta);
        var tamanos = PdfDePrueba.TamanosDe(ruta);

        Assert.HasCount(2, tamanos);
        Assert.AreEqual(
            tamanos[0], tamanos[1],
            $"la hoja buena mide {tamanos[0]} y la de aviso {tamanos[1]}");
    }

    /// <summary>Un caso sin personas tampoco lleva hoja de aviso: no tiene renglón que sostener.</summary>
    [TestMethod]
    public void UnCasoSinPersonasNoLlevaNiSiquieraHojaDeAviso()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO");
        var conGente = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var vacioYRoto = _banco.Caso("VACI2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        _banco.Persona(conGente, "Uno", "055-1111-3853");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [conGente, vacioYRoto], ruta);

        CollectionAssert.AreEqual(
            new[] { "HOJA-UNO" },
            PdfDePrueba.RotulosDe(ruta).ToArray(),
            "sin renglón en el Excel no hay correspondencia que sostener, así que no hay hoja");
    }

    /// <summary>Vigila que un archivo que no es un PDF no tumbe el paquete: su hoja se sustituye y las demás salen.</summary>
    [TestMethod]
    public void UnPdfQueNoSePuedeLeerSeQuedaFueraYElRestoSale()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO");
        var roto = Path.Combine(_carpeta, "roto.pdf");
        File.WriteAllText(roto, "esto no es un PDF, es texto");

        var bueno = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var malo = _banco.Caso("ROTO2609", rutaPdf: roto, paginaPdf: 1);
        _banco.Persona(bueno, "Uno", "055-1111-3853");
        _banco.Persona(malo, "Dos", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [bueno, malo], ruta);
        var hojas = PdfDePrueba.RotulosDe(ruta);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.HasCount(2, hojas);
        Assert.AreEqual("HOJA-UNO", hojas[0]);
        Assert.Contains("ROTO2609", hojas[1], StringComparison.Ordinal);
        Assert.IsTrue(resultado.Avisos.Any(aviso => aviso.Linea.Contains("ROTO2609", StringComparison.Ordinal)));
    }

    /// <summary>Vigila que pedir la hoja 9 de un archivo de 2 deje una hoja de aviso que diga cuántas tiene.</summary>
    [TestMethod]
    public void UnCasoCuyaHojaNoExisteDentroDelArchivoSeQuedaFueraConSuMotivo()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS");
        var bueno = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 2);
        var fuera = _banco.Caso("FUER2609", rutaPdf: papel, paginaPdf: 9);
        _banco.Persona(bueno, "Uno", "055-1111-3853");
        _banco.Persona(fuera, "Dos", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [bueno, fuera], ruta);
        var hojas = PdfDePrueba.RotulosDe(ruta);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.HasCount(2, hojas);
        Assert.AreEqual("HOJA-DOS", hojas[0]);
        Assert.Contains("FUER2609", hojas[1], StringComparison.Ordinal);
        Assert.IsTrue(resultado.Avisos.Any(aviso => aviso.Linea.Contains("FUER2609", StringComparison.Ordinal)));
    }

    /// <summary>Vigila que un caso tecleado a mano, sin escaneo, deje una hoja de aviso que diga que no hay archivo.</summary>
    [TestMethod]
    public void UnCasoSinRutaDePdfSeQuedaFueraConSuMotivo()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO");
        var bueno = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var sinPapel = _banco.Caso("MANO2609");
        _banco.Persona(bueno, "Uno", "055-1111-3853");
        _banco.Persona(sinPapel, "Dos", "055-1111-3854");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [bueno, sinPapel], ruta);
        var hojas = PdfDePrueba.RotulosDe(ruta);

        Assert.IsTrue(resultado.SeEscribio);
        Assert.HasCount(2, hojas);
        Assert.AreEqual("HOJA-UNO", hojas[0]);
        Assert.Contains("MANO2609", hojas[1], StringComparison.Ordinal);
        Assert.IsTrue(resultado.Avisos.Any(aviso => aviso.Linea.Contains("MANO2609", StringComparison.Ordinal)));
    }

    /// <summary>Si NINGÚN documento se puede leer, el PDF sale con sus avisos y no vacío.</summary>
    /// <remarks>
    /// ⚠️ Esto cambió el 2026-09-05 y no es un descuido. Antes no se escribía nada, porque un
    /// PDF de cero hojas es un archivo que el compañero abre para nada. Con la hoja de aviso en
    /// el hueco, un PDF de N avisos NO es un archivo vacío: le dice qué documentos pedirle a
    /// Miguel y mantiene la correspondencia con su Excel, que sigue teniendo sus N renglones.
    /// </remarks>
    [TestMethod]
    public void SiNingunaHojaSePuedeLeerElPdfSaleConSusAvisosYNoVacio()
    {
        var perdido = _banco.Caso("PERD2609", rutaPdf: Path.Combine(_carpeta, "ya-no-esta.pdf"), paginaPdf: 1);
        _banco.Persona(perdido, "Uno", "055-1111-3853");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [perdido], ruta);

        Assert.IsTrue(resultado.SeEscribio);
        var hojas = PdfDePrueba.RotulosDe(ruta);
        Assert.HasCount(1, hojas, "un renglón en el Excel, una hoja en el PDF");
        Assert.Contains("PERD2609", hojas[0], StringComparison.Ordinal);
        Assert.IsGreaterThan(0, resultado.Avisos.Count, "y el aviso de la pantalla sigue nombrándolo");
    }

    // ─────────────── las fronteras que no se cruzan ───────────────

    /// <summary>Vigila que un caso sin gente no aporte hoja: tampoco tiene renglón en el Excel.</summary>
    [TestMethod]
    public void UnCasoSinPersonasNoLlevaHojaPorqueTampocoTieneFilaEnElExcel()
    {
        // El Excel es la referencia del orden. Un caso sin gente no produce ninguna fila, asi
        // que su hoja en el PDF descolocaria la correspondencia fila-hoja de todo lo de abajo.
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO", "HOJA-DOS");
        var conGente = _banco.Caso("BUEN2609", rutaPdf: papel, paginaPdf: 1);
        var vacio = _banco.Caso("VACI2609", rutaPdf: papel, paginaPdf: 2);
        _banco.Persona(conGente, "Uno", "055-1111-3853");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [conGente, vacio], ruta);

        Assert.IsTrue(resultado.SeEscribio);
        CollectionAssert.AreEqual(new[] { "HOJA-UNO" }, PdfDePrueba.RotulosDe(ruta).ToArray());
    }

    /// <summary>Vigila que una familia de varias personas lleve una sola hoja aunque tenga varios renglones.</summary>
    [TestMethod]
    public void UnCasoConVariasPersonasLlevaSuHojaUnaSolaVez()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "familia.pdf", "LA-FAMILIA");
        var caso = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        _banco.Persona(caso, "Padre", "055-1111-3853");
        _banco.Persona(caso, "Madre", "055-1111-3854");
        _banco.Persona(caso, "Hija", "055-1111-3855");

        var ruta = Destino("paquete.pdf");
        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [caso], ruta);

        CollectionAssert.AreEqual(
            new[] { "LA-FAMILIA" },
            PdfDePrueba.RotulosDe(ruta).ToArray(),
            "el documento es uno aunque viajen tres: tres filas del Excel, una sola hoja");
    }

    /// <summary>Vigila la regla permanente 5: generar el PDF lee la base y no escribe en ella.</summary>
    [TestMethod]
    public void GenerarElPdfNoTocaElEstadoDeNingunCaso()
    {
        // La regla permanente 5: nada se marca solo. Este metodo LEE la base y no escribe en ella.
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO");
        var caso = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        _banco.Persona(caso, "Uno", "055-1111-3853");
        var antes = _banco.Almacen.Casos[caso];

        _banco.Paquetes.GenerarPdfDeCompanero(_sandy, [caso], Destino("paquete.pdf"));

        Assert.AreEqual(antes, _banco.Almacen.Casos[caso], "generar el PDF no puede cambiar ni una columna");
    }

    /// <summary>Vigila que un compañero inexistente no deje archivo y el aviso lo diga.</summary>
    [TestMethod]
    public void SinCompaneroNoSeGeneraNadaYSeDice()
    {
        var papel = PdfDePrueba.Escribir(_carpeta, "escaneos.pdf", "HOJA-UNO");
        var caso = _banco.Caso("CASP2609", rutaPdf: papel, paginaPdf: 1);
        _banco.Persona(caso, "Uno", "055-1111-3853");

        var ruta = Destino("paquete.pdf");
        var resultado = _banco.Paquetes.GenerarPdfDeCompanero(9999, [caso], ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(ruta));
        Assert.IsGreaterThan(0, resultado.Avisos.Count);
    }

    /// <summary>Vigila que el Excel y el PDF sean independientes: sin ningún escaneo, el Excel sale y el PDF avisa.</summary>
    [TestMethod]
    public void ElExcelSigueSaliendoIgualAunqueElPdfNoSePuedaHacer()
    {
        var sinPapel = _banco.Caso("MANO2609");
        _banco.Persona(sinPapel, "Uno", "055-1111-3853");

        var excel = Destino("paquete.xlsx");
        var deLaIda = _banco.Paquetes.GenerarExcelDeCompanero(_sandy, [sinPapel], excel);
        // El PDF se manda a una carpeta que no existe: es el fallo de escritura de verdad
        // —disco lleno, carpeta borrada— ahora que un documento ilegible ya no impide el PDF,
        // porque en su sitio va la hoja de aviso.
        var delPdf = _banco.Paquetes.GenerarPdfDeCompanero(
            _sandy, [sinPapel], Path.Combine(_carpeta, "no-existe", "paquete.pdf"));

        Assert.IsTrue(deLaIda.SeEscribio, "el Excel no depende del PDF y no cambia");
        Assert.IsFalse(delPdf.SeEscribio);
        using var libro = new XLWorkbook(excel);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        // Se cuentan las columnas ESCRITAS y no las de la lista: lo que dice que el Excel no
        // cambió es la hoja del disco, y contar `Columnas.Todas` aquí solo repetía la lista
        // desde otro archivo. Fueron 16 desde el 2026-09-06, cuando el dueño quitó dos, y son
        // 17 desde el 2026-09-07, cuando pidió el número de la unidad en su propia columna, y
        // 18 desde el 2026-09-16, cuando pidió el templo en cada fila.
        Assert.AreEqual(Columnas.Todas.Count, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.AreEqual(18, hoja.LastColumnUsed()!.ColumnNumber(),
            "18 desde el 2026-09-16: entró «Templo»");
        Assert.AreEqual("MANO2609", hoja.Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe("numero_caso")).GetString());
    }
}
