using System.Diagnostics;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// El criterio de aceptacion de la fase, sobre los SIETE escaneos reales del dueno.
/// </summary>
/// <remarks>
/// Son los `CASP2609`: escaneos de verdad, sin capa de texto —comprobado con PdfPig: 0
/// letras en las siete hojas—, que es lo que los separa de todo el material anterior.
/// `DECISIONES.md`, 2026-09-04: «todas las cifras anteriores de lectura quedan anuladas
/// como criterio».
///
/// <para>⚠️ <b>Son datos personales.</b> No estan en el repositorio, no se copian a el y
/// no se commitean. La prueba los lee de la carpeta del dueno y, si no esta, se declara
/// no concluyente con su motivo en vez de fallar: nadie mas que esta maquina los tiene.</para>
///
/// <para>La linea base a batir, medida por el supervisor con el Python el mismo dia:
/// 7 de 7 leidos, 7 de 7 nombres, 7 de 7 fechas y unidades, <b>5 de 7 cedulas guardadas</b>,
/// y el numero de caso 7 de 7. Tiempo: 9,3 a 18,5 s por hoja.</para>
///
/// <para>Los siete se leen UNA vez para toda la clase. Con una lectura por prueba la
/// bateria tardaba 9 minutos y cada prueba pagaba el arranque del motor otra vez, lo que
/// ademas falseaba la medicion de segundos por hoja.</para>
/// </remarks>
[TestClass]
public class PruebaDeLosSieteDocumentosReales
{
    /// <summary>Dónde están los siete escaneos en esta máquina. Fuera del repositorio: son datos personales.</summary>
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>Los siete escaneos sueltos del dueño; con más o menos, la clase entera se declara no concluyente.</summary>
    private const int DocumentosEsperados = 7;

    /// <summary>Las siete hojas leídas una vez para toda la clase; vacía si no estaban los siete.</summary>
    private static IReadOnlyList<HojaLeida> _hojas = [];
    /// <summary>Lo que tardó cargar los modelos del OCR, medido antes de la primera hoja para no confundirlo con ella.</summary>
    private static double _segundosDeArranqueDelMotor;

    /// <summary>Las rutas de los escaneos <c>CASP2609</c>, ordenadas; vacía si la carpeta no existe.</summary>
    private static string[] Documentos()
        => Directory.Exists(CarpetaDeLosDocumentos)
            ? Directory.GetFiles(CarpetaDeLosDocumentos, "*CASP2609*.pdf").Order().ToArray()
            : [];

    /// <summary>Prepara el motor midiendo su arranque y lee los siete. Es OCR de verdad, por eso una sola vez.</summary>
    /// <param name="contexto">Lo exige MSTest; no se usa.</param>
    [ClassInitialize]
    public static void LeerLosSieteUnaSolaVez(TestContext contexto)
    {
        _ = contexto;
        var rutas = Documentos();
        if (rutas.Length != DocumentosEsperados) return;

        using var lectura = new LecturaDePdf();

        // El arranque del motor se mide aparte, y ANTES de leer nada: son los 2,2 s que el
        // Python paga una vez por sesión, y hay que poder compararlos. Medido después, se
        // confunde con el tiempo de la primera hoja y sale un 0,01 s que no significa nada.
        var crono = Stopwatch.StartNew();
        lectura.PrepararMotor();
        crono.Stop();
        _segundosDeArranqueDelMotor = crono.Elapsed.TotalSeconds;

        var lector = new LectorDeFormularios(lectura);
        _hojas = rutas.SelectMany(lector.LeerDocumento).ToArray();
    }

    /// <summary>Si el dueño no tiene los documentos en esta máquina, no hay nada que medir.</summary>
    private static IReadOnlyList<HojaLeida> Hojas()
    {
        if (_hojas.Count != DocumentosEsperados)
        {
            Assert.Inconclusive(
                $"No están los siete documentos en «{CarpetaDeLosDocumentos}». "
                + "Son datos personales del dueño y no viven en el repositorio.");
        }
        return _hojas;
    }

    /// <summary>El valor propuesto del primer campo con ese nombre en la hoja, o nulo si no lo hay.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campo">El nombre de columna, uno de los <c>Extraccion.Campo…</c>.</param>
    private static string? ValorDe(HojaLeida hoja, string campo)
        => hoja.Campos.FirstOrDefault(c => c.Campo == campo)?.Valor;

    /// <summary>C3-L1: 7 de 7 documentos leídos, 0 ilegibles, 0 rechazados.</summary>
    [TestMethod]
    public void LosSieteSeLeenYNingunoSaleIlegible()
    {
        var hojas = Hojas();

        Assert.HasCount(DocumentosEsperados, hojas, "cada documento tiene que dar exactamente una hoja");
        var ilegibles = hojas.Where(h => h.Ilegible is not null).ToArray();
        Assert.IsEmpty(ilegibles,
            $"salieron ilegibles: {string.Join("; ", ilegibles.Select(h => h.Ilegible!.Motivo))}");
    }

    /// <summary>C3-L2: 7 de 7 personas con nombre.</summary>
    [TestMethod]
    public void LosSieteDanElNombreDeSuPersona()
    {
        var conNombre = Hojas().Count(hoja =>
            !string.IsNullOrWhiteSpace(ValorDe(hoja, Extraccion.CampoNombreDePersona)));

        Assert.AreEqual(DocumentosEsperados, conNombre);
    }

    /// <summary>C3-L3: 7 de 7 fechas de viaje.</summary>
    /// <remarks>
    /// Las siete tienen que dar <c>2026-09-08</c>, y eso NO es lo que dice el texto
    /// impreso: el papel dice «September 7, 2026», encima hay un trazo rojo que lo cruza y
    /// al lado una anotación que dice «8 Sept 2026». Si la precedencia se rompiera, esta
    /// prueba devolvería el día 7 y alguien viajaría con la fecha equivocada.
    /// </remarks>
    [TestMethod]
    public void LosSieteDanLaFechaDeViajeYGanaLaCorreccionEscrita()
    {
        var fechas = Hojas().Select(hoja => ValorDe(hoja, Extraccion.CampoFechaDeViaje)).ToArray();

        Assert.AreEqual(DocumentosEsperados, fechas.Count(f => !string.IsNullOrWhiteSpace(f)));
        CollectionAssert.AreEqual(
            Enumerable.Repeat("2026-09-08", DocumentosEsperados).ToArray(), fechas,
            "la anotación «8 Sept 2026» tiene que ganarle al «September 7, 2026» tachado");
    }

    /// <summary>C3-L4: 7 de 7 unidades, número y nombre.</summary>
    [TestMethod]
    public void LosSieteDanElNumeroYElNombreDeLaUnidad()
    {
        var hojas = Hojas();

        Assert.AreEqual(DocumentosEsperados,
            hojas.Count(h => !string.IsNullOrWhiteSpace(ValorDe(h, Extraccion.CampoUnidadNumero))), "número de unidad");
        Assert.AreEqual(DocumentosEsperados,
            hojas.Count(h => !string.IsNullOrWhiteSpace(ValorDe(h, Extraccion.CampoUnidadNombre))), "nombre de unidad");
    }

    /// <summary>
    /// C3-L5, el único criterio que exige MÁS que el Python: 7 de 7 cédulas enseñadas.
    /// </summary>
    /// <remarks>
    /// El Python guarda 5 de 7 porque su validación de once dígitos tiraba las dos que
    /// terminan en letra. Aquí las siete tienen que volver con valor: las que encajan,
    /// normalizadas; las que no, tal como las leyó el papel y con su aviso. <b>Jamás vacía
    /// en silencio</b>, que es el criterio 1 del ADR-0004 §0.2.
    /// </remarks>
    [TestMethod]
    public void LasSieteCedulasVuelvenYNingunaSePierdeEnSilencio()
    {
        var hojas = Hojas();

        foreach (var hoja in hojas)
        {
            var cedula = hoja.Campos.FirstOrDefault(c => c.Campo == Extraccion.CampoCedula);
            Assert.IsNotNull(cedula, $"«{Path.GetFileName(hoja.RutaPdf)}» no propuso ni un campo de cédula");
            Assert.IsNotNull(cedula.Valor,
                $"«{Path.GetFileName(hoja.RutaPdf)}» perdió lo que el papel decía en la cédula");
        }

        // Y las dos que terminan en letra tienen que estar entre las siete, con su letra
        // intacta: es lo que confirmó el dueño el 2026-09-04, y lo que el Python tiraba.
        var terminadasEnLetra = hojas
            .Select(h => ValorDe(h, Extraccion.CampoCedula))
            .Count(valor => valor is not null && char.IsLetter(valor[^1]));
        Assert.AreEqual(2, terminadasEnLetra,
            "son dos de los siete documentos los que traen la cédula terminada en letra");
    }

    /// <summary>C3-L6: el número de caso de los 7, tal como lo dice cada documento.</summary>
    /// <remarks>
    /// ⚠️ <b>«Jonas Ficticio» es un nombre sustituido.</b> Desde el 2026-09-07 ningún
    /// nombre de persona real vive en el repositorio; este apunta a uno de los siete
    /// documentos del disco del dueño, y lo que se afirma de él se midió sobre el papel de
    /// verdad. Ver `EN-CURSO.md`, «Los datos de personas reales salen del repositorio».
    ///
    /// <para>⚠️ <b>Y aquí se corrige una premisa que venía escrita en `DECISIONES.md`.</b> Decía
    /// que el Python «leyó uno como CASD2609 en vez de CASP2609: una P por una D». Medido
    /// el 2026-09-04 con PdfPig, <b>sin OCR de por medio</b>: el documento de Jonas Ficticio
    /// lleva dentro una anotación `/FreeText` cuyo texto es literalmente
    /// <c>CASD2609</c>, y el escaneo por debajo <b>no trae ningún número de caso</b>
    /// —renderizado sin anotaciones, 0 candidatos con esa forma en 78 líneas—. El único
    /// sitio donde pone `CASP` en ese documento es <b>el nombre del archivo</b>.</para>
    ///
    /// <para>Consecuencia: no fue un fallo de lectura, ni del Python ni de éste. Es un
    /// error de tecleo dentro del documento, y <b>ningún motor de OCR lo puede arreglar</b>.
    /// Lo que hace falta es lo que el ADR-0004 §6ter ya pide: poder corregir el número a
    /// mano y unir los dos casos.</para>
    ///
    /// <para>Por eso el criterio que se puede exigir es «los 7 dan su número, y ninguno se
    /// pierde», no «los 7 dan CASP2609». Exigir lo segundo obligaría a corregir un dato,
    /// que es lo que la regla permanente 1 prohíbe.</para>
    /// </remarks>
    [TestMethod]
    public void LosSieteDanSuNumeroDeCasoTalComoLoDiceElDocumento()
    {
        var numeros = Hojas().Select(h => ValorDe(h, Extraccion.CampoNumeroDeCaso)).ToArray();

        Assert.AreEqual(DocumentosEsperados, numeros.Count(n => !string.IsNullOrWhiteSpace(n)),
            "los siete tienen que dar número de caso");
        Assert.AreEqual(6, numeros.Count(n => n == "CASP2609"));
        Assert.AreEqual(1, numeros.Count(n => n == "CASD2609"),
            "uno de los documentos lleva CASD2609 escrito dentro; se guarda tal cual y se corrige a mano");
    }

    /// <summary>C3-L8: las casillas de ordenanza NO entran en esta fase, y se ve.</summary>
    [TestMethod]
    public void NingunaHojaProponeCasillasDeOrdenanza()
    {
        var propuestas = Hojas()
            .SelectMany(h => h.Campos)
            .Where(c => c.Campo.StartsWith("ord_", StringComparison.Ordinal))
            .ToArray();

        Assert.IsEmpty(propuestas,
            "la lectura de casillas está apagada por diseño hasta que exista la verdad conocida (FASE C3b)");
    }

    /// <summary>
    /// Qué hacen los tachones REALES de los siete, ahora que la marca viaja con el dato.
    /// </summary>
    /// <remarks>
    /// Medido con `pypdf` sobre los siete el 2026-09-06 y vuelto a medir el mismo día: los
    /// siete traen tachones —30 <c>/Ink</c> en total, de los que <b>29</b> son rojos de grosor
    /// 1,65 y el otro es el resaltador verde del de Jonas Ficticio—, y
    /// <b>todos los que caen sobre un campo que esta fase extrae son el mismo</b>: el de
    /// la fila de la fecha de viaje, y ese SÍ lleva la corrección escrita al lado
    /// («8 Sept 2026»). Los demás cruzan las filas de dinero —vuelo, hotel, prendas, total,
    /// importe a la unidad—, que esta fase no lee.
    ///
    /// <para>O sea: <b>en los siete documentos del dueño no hay ni un tachón sin
    /// corrección sobre un campo extraído</b>. Por eso el caso del tachón que anula se
    /// prueba con una página construida —<c>PruebaDelTachonQueViajaConElCampo</c>— y aquí
    /// se fija lo contrario, que es lo que estos siete sí pueden demostrar: que la marca
    /// NO se pega al valor bueno. Si alguien la pegara, las siete fechas dejarían de
    /// llegar a la base.</para>
    /// </remarks>
    [TestMethod]
    public void EnLosSieteElTachonDeLaFechaNoAnulaLaCorreccionEscrita()
    {
        var hojas = Hojas();

        var anulados = hojas
            .SelectMany(h => h.Campos.Select(c => (Hoja: h, Campo: c)))
            .Where(par => par.Campo.AnuladoPorTachon)
            .ToArray();

        Console.WriteLine($"campos anulados por tachón en los siete: {anulados.Length}");
        foreach (var par in anulados)
        {
            Console.WriteLine($"  {par.Campo.Campo} · {Path.GetFileName(par.Hoja.RutaPdf)}");
        }

        Assert.IsEmpty(anulados,
            "el único tachón que cae sobre un campo extraído es el de la fecha, y lleva corrección escrita: "
            + "marcarlo anularía las siete fechas de viaje");
        CollectionAssert.AreEqual(
            Enumerable.Repeat("2026-09-08", DocumentosEsperados).ToArray(),
            hojas.Select(h => ValorDe(h, Extraccion.CampoFechaDeViaje)).ToArray(),
            "y por eso las siete fechas siguen llegando enteras");
    }

    /// <summary>
    /// Las siete DETECTAN el trazo rojo de la fila de la fecha, y las siete traen corrección.
    /// </summary>
    /// <remarks>
    /// <para>Es la otra mitad de la prueba de arriba, y hace falta para que aquélla
    /// signifique algo: que ningún campo salga anulado podría deberse a que el tachón no se
    /// detecta, en cuyo caso no probaría nada. Se detecta. Las siete emiten el aviso «venía
    /// tachado en el papel y alguien escribió el valor bueno al lado», que la extracción
    /// solo produce cuando el trazo cayó DENTRO de la banda <b>y</b> además había
    /// corrección escrita.</para>
    ///
    /// <para>Medido el 2026-09-06 con `pypdf` sobre los siete: 30 <c>/Ink</c> en total, 29
    /// rojos de grosor 1,65 y un resaltador verde. El único que cae sobre un campo de esta fase es
    /// el de la fila de la fecha, y en los siete lleva al lado una <c>/FreeText</c> con el
    /// día bueno. Por eso el caso del tachón que SÍ anula se prueba con una página
    /// construida, en <c>PruebaDelTachonQueViajaConElCampo</c>: los documentos del dueño no
    /// traen ninguno.</para>
    /// </remarks>
    [TestMethod]
    public void LasSieteDetectanElTrazoRojoDeLaFilaDeLaFecha()
    {
        var conAvisoDeTachon = Hojas()
            .Count(h => h.Avisos.Any(a => a.Linea.Contains("tachado", StringComparison.Ordinal)));

        Console.WriteLine($"hojas que avisan de un tachón: {conAvisoDeTachon} de {DocumentosEsperados}");

        Assert.AreEqual(DocumentosEsperados, conAvisoDeTachon,
            "si esta cifra baja, el tachón dejó de detectarse y la prueba de que la corrección "
            + "sobrevive al tachón se queda sin sujeto");
    }

    /// <summary>
    /// Ningún trazo rojo de los siete cae sobre la cédula ni sobre el nombre de una persona.
    /// </summary>
    /// <remarks>
    /// <para>Es el control negativo de la detección de tachón en las personas, que se añadió
    /// el 2026-09-06: hasta ese día <c>Personas.Extraer</c> no recibía las anotaciones y una
    /// cédula tachada entraba a la base indistinguible de una lectura limpia. Al conectarla,
    /// lo primero que hay que demostrar es que <b>no empieza a marcar de más</b> sobre el
    /// papel real, porque marcar una cédula buena la tira: <c>CamposDeLaHoja.cs:106</c>
    /// devuelve <c>null</c> en cuanto ve la marca, y la cédula es la mitad del par con el que
    /// se reconcilia todo el programa.</para>
    ///
    /// <para><b>Por qué se puede afirmar, con la medición delante</b> (2026-09-06, geometría
    /// sacada de la propia tubería sobre los siete): las filas de personas viven en
    /// y 0,287–0,305 y el trazo rojo más alto de los siete empieza en y 0,439 —la fila de la
    /// fecha de viaje—. Sobran más de 0,13 de página, unas ocho filas. Y en horizontal, las
    /// marcas de ordenanza que el OCR devuelve están en x 0,775–0,874, mientras la columna de
    /// la cédula acaba en el borde de su rótulo, x 0,654.</para>
    ///
    /// <para>Por eso el caso del tachón que SÍ anula una cédula se prueba con una página
    /// construida, en <c>PruebaDelTachonEnLasPersonas</c>: los siete del dueño no traen
    /// ninguno, igual que no traían ninguno sobre un campo del caso.</para>
    /// </remarks>
    [TestMethod]
    public void EnLosSieteNingunCampoDePersonaSaleAnuladoPorTachon()
    {
        var deLasPersonas = Hojas()
            .SelectMany(h => h.Campos.Select(c => (Hoja: h, Campo: c)))
            .Where(par => par.Campo.Campo is Extraccion.CampoCedula or Extraccion.CampoNombreDePersona)
            .ToArray();

        var anulados = deLasPersonas.Where(par => par.Campo.AnuladoPorTachon).ToArray();

        Console.WriteLine(
            $"campos de persona mirados: {deLasPersonas.Length}; anulados por tachón: {anulados.Length}");
        foreach (var par in anulados)
        {
            Console.WriteLine($"  {par.Campo.Campo} · {Path.GetFileName(par.Hoja.RutaPdf)}");
        }

        Assert.IsNotEmpty(deLasPersonas, "sin campos de persona esta prueba no mide nada");
        Assert.IsEmpty(anulados,
            "ningún trazo rojo de los siete cruza una fila de personas: si esto se marca, "
            + "la detección está anulando cédulas buenas");
    }

    /// <summary>C3-L7: los segundos por hoja, que se anotan y no tienen umbral.</summary>
    /// <remarks>
    /// El dueño no se ha quejado del tiempo de lectura, así que esto no aprueba ni
    /// suspende: solo deja el número escrito al lado del del Python (9,3 a 18,5 s por
    /// hoja). Lo único que se exige es que ninguna hoja se dispare a un minuto, que sería
    /// otra avería.
    /// </remarks>
    [TestMethod]
    public void LosSegundosPorHojaQuedanAnotados()
    {
        var hojas = Hojas();
        var segundos = hojas.Select(h => h.Segundos).Order().ToArray();

        Console.WriteLine($"arranque del motor : {_segundosDeArranqueDelMotor:F2} s (una vez por sesión)");
        Console.WriteLine($"segundos por hoja  : {segundos[0]:F2} a {segundos[^1]:F2} · mediana {segundos[segundos.Length / 2]:F2}");
        foreach (var hoja in hojas)
        {
            Console.WriteLine($"  {hoja.Segundos,6:F2} s · {hoja.LineasLeidas,3} líneas · {Path.GetFileName(hoja.RutaPdf)}");
        }

        Assert.IsLessThan(60.0, segundos[^1], "una hoja que tarda un minuto es otra avería, no lentitud");
    }
}
