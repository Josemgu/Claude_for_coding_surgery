using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// La segunda clase de documento: formularios rellenables, rellenados en el ordenador.
/// </summary>
/// <remarks>
/// <para><b>El defecto que fija, medido el 2026-09-10 sobre el PDF del dueño.</b> Sus
/// palabras: «ese PDF no lo lee el motor de lectura, y crea varios que nada que ver». El
/// archivo tiene UNA hoja y 93 anotaciones <c>/Widget</c>: el nombre, la cédula, la fecha
/// de viaje y el templo están <b>tecleados dentro de los campos del formulario</b>, no en
/// la imagen ni en una <c>/FreeText</c>. El lector de entonces ignoraba los <c>/Widget</c>
/// y, además, PDFium no los pinta si no se le pide el relleno de formulario: medido, la
/// casilla del nombre tenía <b>0 de 58 011</b> píxeles oscuros. Resultado: 5 campos, 0
/// personas, fecha y templo vacíos, con la persona ahí delante.</para>
///
/// <para><b>Dónde está cada dato en ese archivo, medido con <c>pypdf</c> y con PdfPig</b>
/// (rectángulos en puntos PDF, origen abajo a la izquierda):</para>
/// <list type="bullet">
///   <item>nombre → <c>Full NamesRow1</c>, <c>/Tx</c>, [36.4, 545.8, 251.5, 559.4]</item>
///   <item>cédula → <c>Membership Record NumberRow1</c>, <c>/Tx</c>, [252.6, 545.8, 450.7, 559.4]</item>
///   <item>templo → <c>Temple Name</c>, <c>/Tx</c>, [37.2, 450.2, 251.3, 463.6]</item>
///   <item>cita del templo → <c>Temple Appointment Date</c>, <c>/Tx</c>, [252.3, 450.2, 450.5, 463.6]</item>
///   <item>fecha de viaje → <c>Date traveling to the temple</c>, <c>/Tx</c>, [36.6, 427.0, 350.9, 440.1]</item>
///   <item>número de caso → sigue en una <c>/FreeText</c>, como en los escaneos</item>
///   <item>unidad → NO está en un campo (<c>WardBranch Name and Unit Number</c> viene vacío):
///   está en la capa de texto de la página, y la lee el OCR como hasta ahora</item>
/// </list>
///
/// <para><b>Y la clase entera.</b> A las 14:43 llegó ese archivo y a las 15:20 cinco más del
/// mismo caso <c>ELTC2609</c>, uno de ellos copia byte a byte del primero. Entre los cinco
/// distintos hay dos maneras de teclear: tres llevan la fecha como <c>dd-mm-aa</c> —año de dos
/// cifras— y la unidad tecleada en su campo; dos llevan la fecha como <c>dd/mm/aaaa</c> y la
/// unidad en la capa de texto. Lo que se exige a todos es lo que no depende de cómo se
/// tecleó; y de la fecha, lo que manda la regla permanente 1: con cuatro cifras de año sale
/// normalizada; con dos, <b>no se adivina el siglo</b> y va a revisión con lo tecleado a la
/// vista.</para>
///
/// <para>⛔ <b>Regla permanente 1, con toda su fuerza.</b> Un campo tecleado es texto exacto:
/// se normaliza con las mismas reglas que lo leído y no se inventa nada. Y regla 5: sale con
/// origen anotación y confianza 1,0, pero <b>no</b> verificado.</para>
///
/// <para>⚠️ <b>Son datos personales.</b> Cada archivo lleva el nombre de una persona real en
/// su propio nombre: se buscan por el número de caso, no se copian al repositorio, y lo que
/// se imprime va enmascarado. El valor esperado del nombre y de la cédula del archivo del
/// pase se lee del propio formulario con PdfPig —un oráculo que no pasa por
/// <c>Fichas.Lectura</c>—, para no escribirlo aquí.</para>
///
/// <para>Se leen UNA vez para toda la clase, y la clase no corre en paralelo con las demás:
/// el OCR ya usa todos los núcleos, y con las clases de documentos reales compitiendo la
/// medida de segundos por hoja de los siete escaneos se cuadruplicaba y pasaba del minuto.</para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class PruebaDeLosFormulariosRellenadosAMaquina
{
    /// <summary>Dónde están los formularios reales en esta máquina. Fuera del repositorio: son datos personales.</summary>
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>El archivo del pase, por el prefijo que le puso la carpeta de subidas.</summary>
    private const string PrefijoDelArchivoDelPase = "b376e041-";

    /// <summary>El nombre del campo <c>/Tx</c> de la primera fila de nombres, tal como lo llama el formulario.</summary>
    private const string CampoDelNombreEnElFormulario = "Full NamesRow1";
    /// <summary>El nombre del campo <c>/Tx</c> de la primera fila de cédulas.</summary>
    private const string CampoDeLaCedulaEnElFormulario = "Membership Record NumberRow1";
    /// <summary>El nombre del campo <c>/Tx</c> del templo.</summary>
    private const string CampoDelTemploEnElFormulario = "Temple Name";
    /// <summary>El nombre del campo <c>/Tx</c> de la fecha de viaje.</summary>
    private const string CampoDeLaFechaDeViajeEnElFormulario = "Date traveling to the temple";

    /// <summary>Las hojas de los formularios distintos, leídas una vez para toda la clase; vacía si no había material.</summary>
    private static IReadOnlyList<HojaLeida> _hojas = [];
    /// <summary>La hoja del archivo del pase, sobre el que se mide el criterio; nula si no está.</summary>
    private static HojaLeida? _hojaDelPase;
    /// <summary>Las anotaciones de la hoja del pase leídas aparte, para contar los campos tecleados sin pasar por la extracción.</summary>
    private static IReadOnlyList<AnotacionDelPdf> _anotacionesDelPase = [];

    /// <summary>Los formularios distintos de la carpeta: una copia byte a byte no se lee dos veces.</summary>
    private static string[] Documentos()
    {
        if (!Directory.Exists(CarpetaDeLosDocumentos)) return [];
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        // El del pase va primero: si su copia byte a byte se ordenara antes, seria la copia la
        // que quedara y el del pase el descartado (paso: 7 pruebas no concluyentes).
        return Directory.GetFiles(CarpetaDeLosDocumentos, "*ELTC2609*.pdf")
            .OrderBy(ruta => Path.GetFileName(ruta).StartsWith(PrefijoDelArchivoDelPase, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(ruta => ruta, StringComparer.Ordinal)
            .Where(ruta => vistos.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta)))))
            .ToArray();
    }

    /// <summary>Lee todos los formularios distintos y guarda aparte la hoja del pase con sus anotaciones. Es OCR de verdad, por eso una sola vez.</summary>
    /// <param name="contexto">Lo exige MSTest; no se usa.</param>
    [ClassInitialize]
    public static void LeerLosFormulariosUnaSolaVez(TestContext contexto)
    {
        _ = contexto;
        var rutas = Documentos();
        if (rutas.Length == 0) return;

        using var lectura = new LecturaDePdf();
        var lector = new LectorDeFormularios(lectura);
        _hojas = rutas.SelectMany(lector.LeerDocumento).ToArray();

        _hojaDelPase = _hojas.FirstOrDefault(h => Path.GetFileName(h.RutaPdf).StartsWith(PrefijoDelArchivoDelPase, StringComparison.Ordinal));
        if (_hojaDelPase is not null) _anotacionesDelPase = lectura.LeerAnotaciones(_hojaDelPase.RutaPdf, 1);
    }

    /// <summary>Las hojas leídas, o la prueba se declara no concluyente si no hubo material. Nunca falla por eso.</summary>
    private static IReadOnlyList<HojaLeida> Hojas()
    {
        if (_hojas.Count == 0)
        {
            Assert.Inconclusive(
                $"No hay formularios rellenados a máquina en «{CarpetaDeLosDocumentos}». "
                + "Son datos personales del dueño y no viven en el repositorio.");
        }
        return _hojas;
    }

    /// <summary>La hoja del archivo del pase, o la prueba se declara no concluyente si ese archivo no está.</summary>
    private static HojaLeida LaHojaDelPase()
    {
        _ = Hojas();
        if (_hojaDelPase is null)
        {
            Assert.Inconclusive($"No está el archivo del pase («{PrefijoDelArchivoDelPase}…») en «{CarpetaDeLosDocumentos}».");
        }
        return _hojaDelPase;
    }

    /// <summary>Lo tecleado en un campo del archivo del pase, leído con PdfPig directamente: el oráculo.</summary>
    /// <param name="nombreDelCampo">El nombre parcial del campo en el <c>AcroForm</c>, uno de los <c>CampoDe…EnElFormulario</c>.</param>
    private static string? ValorTecleadoEn(string nombreDelCampo)
    {
        using var documento = PdfDocument.Open(LaHojaDelPase().RutaPdf);
        Assert.IsTrue(documento.TryGetForm(out var formulario), "el archivo tiene que traer un AcroForm");

        static IEnumerable<AcroFieldBase> Aplanar(AcroFieldBase campo)
            => campo is AcroNonTerminalField padre ? padre.Children.SelectMany(Aplanar) : [campo];

        return formulario.Fields.SelectMany(Aplanar)
            .OfType<AcroTextField>()
            .Single(c => c.Information?.PartialName == nombreDelCampo)
            .Value;
    }

    /// <summary>El nombre del archivo con la parte que lleva el nombre de la persona tapada, para poder imprimirlo.</summary>
    /// <param name="hoja">La hoja leída.</param>
    private static string Archivo(HojaLeida hoja)
        => Regex.Replace(Path.GetFileName(hoja.RutaPdf), @"_.*\.pdf$", "_<enmascarado>.pdf");

    /// <summary>Tapa los dígitos con «#» y todas las letras menos la primera de cada palabra con «·»: son datos de personas reales.</summary>
    /// <param name="texto">Lo que se va a imprimir; nulo sale como «∅».</param>
    private static string Enmascarar(string? texto)
        => texto is null ? "∅" : Regex.Replace(Regex.Replace(texto, @"\d", "#"), @"(?<=\p{L})\p{L}", "·");

    /// <summary>El único campo propuesto de esa tabla con ese nombre; falla la prueba si hay cero o varios.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="tabla">Casos o personas.</param>
    /// <param name="campo">El nombre de columna.</param>
    private static CampoPropuesto Unico(HojaLeida hoja, TablaDeProcedencia tabla, string campo)
    {
        var propuestos = hoja.Campos.Where(c => c.Tabla == tabla && c.Campo == campo).ToArray();
        Assert.HasCount(1, propuestos, $"«{Archivo(hoja)}»: un solo «{campo}»");
        return propuestos[0];
    }

    /// <summary>El único campo propuesto de <c>casos</c> con ese nombre.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campo">El nombre de columna.</param>
    private static CampoPropuesto CampoDelCaso(HojaLeida hoja, string campo) => Unico(hoja, TablaDeProcedencia.Casos, campo);

    /// <summary>El único campo propuesto de <c>personas</c> con ese nombre: estos formularios traen una sola persona.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campo">El nombre de columna.</param>
    private static CampoPropuesto CampoDeLaPersona(HojaLeida hoja, string campo) => Unico(hoja, TablaDeProcedencia.Personas, campo);

    // --- El criterio del pase, sobre SU archivo --------------------------------------------

    /// <summary>La hoja entra entera: ni ilegible ni floja.</summary>
    [TestMethod]
    public void ElArchivoDelPaseSeLeeYNoSaleIlegibleNiFlojo()
    {
        var hoja = LaHojaDelPase();

        Assert.AreEqual(1, _hojas.Count(h => h.RutaPdf == hoja.RutaPdf), "el archivo tiene una sola hoja");
        Assert.IsNull(hoja.Ilegible, hoja.Ilegible?.Motivo);
        Assert.IsFalse(hoja.CapturaManual, "con nombre, cédula, fecha y caso exactos la hoja no es floja");
    }

    /// <summary>
    /// El nombre y la cédula salen de sus campos del formulario, tal cual y con origen anotación.
    /// </summary>
    /// <remarks>
    /// Antes del cambio: 0 personas y el aviso «No se pudo leer ninguna persona en esta hoja».
    /// </remarks>
    [TestMethod]
    public void ElNombreYLaCedulaSalenDeSusCamposDelFormularioTalCual()
    {
        var hoja = LaHojaDelPase();
        var nombre = CampoDeLaPersona(hoja, Extraccion.CampoNombreDePersona);
        var cedula = CampoDeLaPersona(hoja, Extraccion.CampoCedula);

        string nombreTecleado = ValorTecleadoEn(CampoDelNombreEnElFormulario)!.Trim();
        string cedulaTecleada = ValorTecleadoEn(CampoDeLaCedulaEnElFormulario)!.Trim();
        Console.WriteLine($"nombre : {Enmascarar(nombre.Valor)} · origen {nombre.Origen} · fila {nombre.FilaFormulario}");
        Console.WriteLine($"cédula : {Enmascarar(cedula.Valor)} · origen {cedula.Origen} · fila {cedula.FilaFormulario}");

        Assert.AreEqual(nombreTecleado, nombre.Valor, "el nombre es el tecleado, sin cambiarle una letra");
        Assert.AreEqual(OrigenDeCampo.Anotacion, nombre.Origen, "un campo del formulario es un objeto del PDF, no una lectura");
        Assert.AreEqual(Campos.ConfianzaDeUnaAnotacion, nombre.Confianza);

        Assert.AreEqual(cedulaTecleada, cedula.Valor, "la cédula tecleada ya tiene su forma y sale igual");
        Assert.IsTrue(Regex.IsMatch(cedula.Valor!, @"^\d{3}-\d{4}-\d{4}$"), "tiene la forma de cédula");
        Assert.AreEqual(OrigenDeCampo.Anotacion, cedula.Origen);
        Assert.AreEqual(Campos.ConfianzaDeUnaAnotacion, cedula.Confianza);
        Assert.IsFalse(cedula.NecesitaRevision, "pasó la forma: no hay nada que revisar por forma");
    }

    /// <summary>La cédula es de la fila del nombre: lo dice la geometría, no el orden de los campos.</summary>
    [TestMethod]
    public void ElNombreYLaCedulaCaenEnLaMismaFilaDelPapel()
    {
        var hoja = LaHojaDelPase();
        var nombre = CampoDeLaPersona(hoja, Extraccion.CampoNombreDePersona);
        var cedula = CampoDeLaPersona(hoja, Extraccion.CampoCedula);

        Assert.AreEqual(1, nombre.FilaFormulario, "está tecleada en la primera fila del bloque");
        Assert.AreEqual(nombre.FilaFormulario, cedula.FilaFormulario);
        Assert.IsNotNull(nombre.Banda);
        Assert.IsNotNull(cedula.Banda);
        Assert.IsGreaterThan(0.5, Geometria.FraccionDeTraslapeVertical(cedula.Banda.Value, nombre.Banda.Value),
            "las dos bandas comparten renglón");
    }

    /// <summary>La fecha de viaje es la tecleada, normalizada sin adivinar.</summary>
    /// <remarks>
    /// «17/09/2026»: el 17 no puede ser un mes, así que la fecha no es ambigua. Antes del
    /// cambio el campo salía vacío con «no se pudo leer del papel».
    /// </remarks>
    [TestMethod]
    public void LaFechaDeViajeDelPaseSaleDelCampoTecleado()
    {
        var fecha = CampoDelCaso(LaHojaDelPase(), Extraccion.CampoFechaDeViaje);

        Assert.AreEqual("2026-09-17", fecha.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, fecha.Origen);
        Assert.AreEqual(Campos.ConfianzaDeUnaAnotacion, fecha.Confianza);
        Assert.StartsWith("17/09/", ValorTecleadoEn(CampoDeLaFechaDeViajeEnElFormulario)!,
            "y viene del campo del formulario, no de otro sitio");
    }

    /// <summary>El templo es el tecleado, y la única normalización es quitar el espacio que sobra.</summary>
    [TestMethod]
    public void ElTemploDelPaseSaleDelCampoTecleadoSinElEspacioDeMas()
    {
        var templo = CampoDelCaso(LaHojaDelPase(), Extraccion.CampoTemploNombre);
        string tecleado = ValorTecleadoEn(CampoDelTemploEnElFormulario)!;

        Assert.EndsWith(" ", tecleado, "el campo se tecleó con un espacio al final; es lo único que se quita");
        Assert.AreEqual("Caracas", templo.Valor);
        Assert.AreEqual(tecleado.Trim(), templo.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, templo.Origen);
    }

    /// <summary>Lo que ya salía sigue saliendo de donde estaba: el caso de la anotación y la unidad del texto.</summary>
    [TestMethod]
    public void ElNumeroDeCasoYLaUnidadDelPaseSiguenSaliendoDeDondeEstaban()
    {
        var hoja = LaHojaDelPase();
        var caso = CampoDelCaso(hoja, Extraccion.CampoNumeroDeCaso);
        var numero = CampoDelCaso(hoja, Extraccion.CampoUnidadNumero);
        var nombre = CampoDelCaso(hoja, Extraccion.CampoUnidadNombre);

        Assert.AreEqual("ELTC2609", caso.Valor);
        Assert.AreEqual(OrigenDeCampo.Anotacion, caso.Origen, "sigue viniendo de la /FreeText");
        Assert.IsTrue(Regex.IsMatch(numero.Valor ?? "", @"^\d{6}$"), $"unidad_numero: {Enmascarar(numero.Valor)}");
        Assert.AreEqual(OrigenDeCampo.Ocr, numero.Origen, "la unidad no está en un campo: la lee el OCR de la capa de texto");
        Assert.IsNotNull(nombre.Valor);
        Assert.IsFalse(nombre.Valor.Any(char.IsDigit), "el nombre de la unidad no lleva el número dentro");
    }

    /// <summary>
    /// Las anotaciones de la hoja traen los campos tecleados junto a las de siempre.
    /// </summary>
    /// <remarks>
    /// Medido con <c>pypdf</c>: 93 <c>/Widget</c>, de los que 14 son de texto con algo
    /// tecleado (los demás son casillas o campos de texto vacíos), 7 <c>/FreeText</c> y 3
    /// <c>/Ink</c>. Los de texto vacíos vuelven con texto nulo —sirven para numerar las filas
    /// del formulario— y no cuentan como tecleados.
    /// </remarks>
    [TestMethod]
    public void LasAnotacionesDelPaseTraenLosCatorceCamposTecleadosYLasDeSiempre()
    {
        _ = LaHojaDelPase();

        var tecleados = _anotacionesDelPase.Where(Anotaciones.EsCampoTecleado).ToArray();
        Console.WriteLine($"campos tecleados: {tecleados.Length} · FreeText: {_anotacionesDelPase.Count(a => a.Subtipo == Anotaciones.SubtipoDeTexto)} · Ink: {_anotacionesDelPase.Count(a => a.Subtipo == Anotaciones.SubtipoDeTrazo)}");

        Assert.HasCount(14, tecleados);
        Assert.IsTrue(tecleados.All(a => !string.IsNullOrWhiteSpace(a.Texto)), "todos con texto");
        Assert.AreEqual(7, _anotacionesDelPase.Count(a => a.Subtipo == Anotaciones.SubtipoDeTexto));
        Assert.AreEqual(3, _anotacionesDelPase.Count(a => a.Subtipo == Anotaciones.SubtipoDeTrazo));
    }

    // --- La clase entera ------------------------------------------------------------------

    /// <summary>Ninguna hoja sale ilegible ni floja, y cada una trae UNA persona exacta.</summary>
    [TestMethod]
    public void CadaFormularioDaSuPersonaConNombreYCedulaExactos()
    {
        var hojas = Hojas();
        Console.WriteLine($"formularios distintos: {hojas.Count}");

        foreach (var hoja in hojas)
        {
            Assert.IsNull(hoja.Ilegible, $"«{Archivo(hoja)}»: {hoja.Ilegible?.Motivo}");
            Assert.IsFalse(hoja.CapturaManual, $"«{Archivo(hoja)}» salió floja");

            var nombre = CampoDeLaPersona(hoja, Extraccion.CampoNombreDePersona);
            var cedula = CampoDeLaPersona(hoja, Extraccion.CampoCedula);
            Console.WriteLine($"  {Archivo(hoja)} · fila {nombre.FilaFormulario} · {Enmascarar(nombre.Valor)} · {Enmascarar(cedula.Valor)}");

            Assert.IsFalse(string.IsNullOrWhiteSpace(nombre.Valor), $"«{Archivo(hoja)}» sin nombre");
            Assert.AreEqual(OrigenDeCampo.Anotacion, nombre.Origen);
            Assert.AreEqual(1, nombre.FilaFormulario);
            Assert.IsTrue(Regex.IsMatch(cedula.Valor ?? "", @"^\d{3}-\d{4}-\d{4}$"), $"«{Archivo(hoja)}» cédula {Enmascarar(cedula.Valor)}");
            Assert.AreEqual(OrigenDeCampo.Anotacion, cedula.Origen);
            Assert.AreEqual(1, cedula.FilaFormulario);
        }
    }

    /// <summary>El número de caso, el templo y la unidad salen en todos.</summary>
    [TestMethod]
    public void CadaFormularioDaSuCasoSuTemploYSuUnidad()
    {
        foreach (var hoja in Hojas())
        {
            var caso = CampoDelCaso(hoja, Extraccion.CampoNumeroDeCaso);
            var templo = CampoDelCaso(hoja, Extraccion.CampoTemploNombre);
            var numero = CampoDelCaso(hoja, Extraccion.CampoUnidadNumero);
            var nombre = CampoDelCaso(hoja, Extraccion.CampoUnidadNombre);
            Console.WriteLine($"  {Archivo(hoja)} · {caso.Valor} · templo {Enmascarar(templo.Valor)} ({templo.Origen}) · unidad {Enmascarar(numero.Valor)} {Enmascarar(nombre.Valor)} ({numero.Origen})");

            Assert.AreEqual("ELTC2609", caso.Valor, Archivo(hoja));
            Assert.IsFalse(string.IsNullOrWhiteSpace(templo.Valor), $"«{Archivo(hoja)}» sin templo");
            Assert.AreEqual(OrigenDeCampo.Anotacion, templo.Origen, "el templo va tecleado en su campo en todos");
            Assert.IsTrue(Regex.IsMatch(numero.Valor ?? "", @"^\d{6}$"), $"«{Archivo(hoja)}» unidad_numero {Enmascarar(numero.Valor)}");
            Assert.IsTrue(numero.Origen is OrigenDeCampo.Anotacion or OrigenDeCampo.Ocr,
                "la unidad va tecleada en su campo o impresa en la capa de texto, según quién rellenó");
            Assert.IsFalse(string.IsNullOrWhiteSpace(nombre.Valor), $"«{Archivo(hoja)}» sin nombre de unidad");
        }
    }

    /// <summary>
    /// La fecha: con cuatro cifras de año sale normalizada; con dos, a revisión y sin adivinar.
    /// </summary>
    /// <remarks>
    /// «17-09-26» puede ser el 17 de septiembre de 2026 o el 26 de septiembre de 2017 (o
    /// el siglo que sea). Elegir el siglo «porque es obvio» es inventar la fecha que decide
    /// si un caso se avisa a tiempo. Se enseña tal cual, sin la confianza de una lectura
    /// limpia, y con su aviso.
    /// </remarks>
    [TestMethod]
    public void LaFechaSaleNormalizadaConCuatroCifrasDeAnioYARevisionConDos()
    {
        int normalizadas = 0, aRevision = 0;
        foreach (var hoja in Hojas())
        {
            var fecha = CampoDelCaso(hoja, Extraccion.CampoFechaDeViaje);
            Console.WriteLine($"  {Archivo(hoja)} · fecha {Enmascarar(fecha.Valor)} · origen {fecha.Origen} · confianza {fecha.Confianza?.ToString("F2") ?? "-"}");

            if (Regex.IsMatch(fecha.Valor ?? "", @"^\d{4}-\d{2}-\d{2}$"))
            {
                normalizadas++;
                Assert.AreEqual(OrigenDeCampo.Anotacion, fecha.Origen, Archivo(hoja));
                Assert.AreEqual(Campos.ConfianzaDeUnaAnotacion, fecha.Confianza);
                continue;
            }

            aRevision++;
            Assert.IsTrue(Regex.IsMatch(fecha.Valor ?? "", @"^\d{2}-\d{2}-\d{2}$"),
                $"«{Archivo(hoja)}»: lo que no se normaliza tiene que ser lo tecleado tal cual, y era dd-mm-aa: {Enmascarar(fecha.Valor)}");
            Assert.AreNotEqual(OrigenDeCampo.Anotacion, fecha.Origen, "sin forma no pasa por dato exacto");
            Assert.IsNull(fecha.Confianza, "sin la confianza de una lectura limpia");
            Assert.IsTrue(hoja.Avisos.Any(a => a.Campo == Extraccion.CampoFechaDeViaje
                                             && a.Linea.Contains("no tiene la forma esperada", StringComparison.Ordinal)),
                $"«{Archivo(hoja)}» sin aviso de la fecha");
        }
        Console.WriteLine($"fechas normalizadas: {normalizadas} · a revisión: {aRevision}");
        Assert.AreEqual(Hojas().Count, normalizadas + aRevision, "cada hoja cae en una de las dos");
    }
}
