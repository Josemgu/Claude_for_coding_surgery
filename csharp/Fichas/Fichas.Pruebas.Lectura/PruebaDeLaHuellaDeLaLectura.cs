using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// La regla de no regresión de la lectura, fijada: la huella SHA-256 de lo que se lee de
/// cada documento del corpus del dueño no cambia.
/// </summary>
/// <remarks>
/// <para>Criterio: <b>dado</b> el corpus de dieciséis documentos (siete <c>CASP2609</c>,
/// un <c>PARB2609</c>, dos <c>SURB2609</c> de seis hojas y seis <c>ELTC2609</c>),
/// <b>cuando</b> se leen de punta a punta con el lector de hoy, <b>entonces</b> la huella
/// de cada documento (<see cref="HuellaDeLaLectura"/>) es exactamente la que se midió
/// sobre <c>master</c> el 2026-09-15, antes de tocar el motor de OCR. Ni un campo, ni un
/// aviso, ni una letra del texto leído distintos.</para>
///
/// <para>Hasta hoy la huella era una sonda temporal fuera de la suite (2026-09-11) que
/// se corría a mano antes y después de cada cambio. Con las dieciséis fijadas aquí, un
/// cambio que altere lo leído se ve en rojo sin que nadie tenga que acordarse.</para>
///
/// <para>⚠️ <b>Son datos personales.</b> No están en el repositorio y no se copian. Se
/// identifican por los ocho primeros caracteres del nombre del archivo, que son el prefijo
/// que puso la carpeta de subidas y no dicen nada de nadie. Si la carpeta no está o no
/// trae los dieciséis, la clase entera se declara no concluyente con su motivo.</para>
///
/// <para>Los dieciséis se leen UNA vez para toda la clase: es OCR de verdad sobre 26
/// hojas, y medido sobre master cuesta unos dos minutos.</para>
/// </remarks>
[TestClass]
public class PruebaDeLaHuellaDeLaLectura
{
    /// <summary>Dónde están los documentos en esta máquina. Fuera del repositorio: son datos personales.</summary>
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>Cuántos documentos tiene que haber para que la huella signifique algo.</summary>
    private const int DocumentosEsperados = 16;

    /// <summary>La huella medida por documento; vacía si no estaban los dieciséis.</summary>
    private static readonly Dictionary<string, string> Medidas = new(StringComparer.Ordinal);

    /// <summary>Lee los dieciséis una sola vez y guarda la huella de cada uno por su prefijo.</summary>
    /// <param name="contexto">Lo exige MSTest; no se usa.</param>
    [ClassInitialize]
    public static void LeerElCorpusUnaSolaVez(TestContext contexto)
    {
        _ = contexto;
        var rutas = Documentos();
        if (rutas.Length != DocumentosEsperados) return;

        using var lectura = new LecturaDePdf();
        var lector = new LectorDeFormularios(lectura);
        foreach (var ruta in rutas)
        {
            Medidas[PrefijoDe(ruta)] = HuellaDeLaLectura.DelDocumento(lector.LeerDocumento(ruta));
        }
    }

    /// <summary>Las rutas de todos los PDF de la carpeta, en orden ordinal; vacía si la carpeta no existe.</summary>
    private static string[] Documentos()
        => Directory.Exists(CarpetaDeLosDocumentos)
            ? Directory.GetFiles(CarpetaDeLosDocumentos, "*.pdf").Order(StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>Los ocho primeros caracteres del nombre del archivo: el prefijo de la carpeta de subidas, sin nombre de nadie.</summary>
    /// <param name="ruta">La ruta del PDF.</param>
    private static string PrefijoDe(string ruta) => Path.GetFileName(ruta)[..8];

    /// <summary>La huella medida de un documento, o no concluyente si el corpus no está entero.</summary>
    /// <param name="prefijo">El prefijo del documento.</param>
    private static string Medida(string prefijo)
    {
        if (Medidas.Count != DocumentosEsperados)
        {
            Assert.Inconclusive(
                $"No están los {DocumentosEsperados} documentos en «{CarpetaDeLosDocumentos}». "
                + "Son datos personales del dueño y no viven en el repositorio.");
        }
        return Medidas[prefijo];
    }

    /// <summary>Dado un documento del corpus, cuando se lee, entonces su huella es la medida sobre master el 2026-09-15.</summary>
    /// <param name="prefijo">El prefijo del documento en la carpeta de subidas.</param>
    /// <param name="esperada">La huella SHA-256 fijada.</param>
    [TestMethod]
    [DataRow("45ee5474", "F8AD313155D057D2E10BB44F61FA15DF6C52F9871DE2602CA3DF4695380A190C")]
    [DataRow("4899dc88", "E484A71C97408A0206EEE29F465890BC34C8390E590CED3DDEEECE9F923D9E9F")]
    [DataRow("4b085e4d", "4D987F646FA0F97F1D9D2D3DCB9447210B2D30CF9F5CA2EC98AA7C0F29F9E56F")]
    [DataRow("4e545614", "381983EB6848396CC42C92887D13C35FF747AF0D767C6E231C2CA69AD5411009")]
    [DataRow("59d87aad", "AE34D3A2734DBC61F5BD7F50A8E9B68A95E9FC1247BC1A76937BA015F425A15E")]
    [DataRow("5fa71e77", "64A0A89D23F84498A4BD0802523562929C9DA68978823227C9BCA774A3553F40")]
    [DataRow("60025df4", "B3937D4C2AEE4AF65602F5F0631D5956B274E8316C4F11721600D8E1B2F4AAE8")]
    [DataRow("87b3ffb0", "3AA2FB4789A419AFFA1FABF42DF8E699B7E8D0DEE22CA0089442E46B316B8E2C")]
    [DataRow("9a776b14", "7A8CC0AA2D324BC88F8070526F52CE55948B5F408C4148D4543C220AF69BAD7C")]
    [DataRow("a92cdf1a", "C2E53DB956017E96AB485AB25B3612C7CE31D8AEE2EBA6063883BD370F528A05")]
    [DataRow("aa038ec0", "8D06E172016F4A2B79C8835FDAF095D9E23950FD0E4D05EEEAA85A1212D13D2E")]
    [DataRow("ad536cd0", "28F9F22F83B354DD1774D279677DF72380F75EAA2A385CC81441728CD529A7EB")]
    [DataRow("b376e041", "3AA2FB4789A419AFFA1FABF42DF8E699B7E8D0DEE22CA0089442E46B316B8E2C")]
    [DataRow("bcd0d475", "ED105D80A5ACA66275C55626646A90966A9808839DE03C19295AA5668E41B7B4")]
    [DataRow("cb2f18be", "A41CA8A84563672A4DD9281D4B73116BAED81EE16404D994D5E2E50371477C99")]
    [DataRow("ed633b94", "A41CA8A84563672A4DD9281D4B73116BAED81EE16404D994D5E2E50371477C99")]
    public void LaHuellaDeCadaDocumentoNoCambia(string prefijo, string esperada)
        => Assert.AreEqual(esperada, Medida(prefijo),
            $"La lectura del documento {prefijo} cambió: algún campo, aviso o texto leído ya no es el de master.");

    /// <summary>Dado el corpus entero, cuando se lee, entonces la huella conjunta es la medida sobre master el 2026-09-15.</summary>
    [TestMethod]
    public void LaHuellaDelCorpusEnteroNoCambia()
    {
        _ = Medida("45ee5474");
        var conjunta = HuellaDeLaLectura.DelCorpus(Medidas.OrderBy(par => par.Key, StringComparer.Ordinal).Select(par => par.Value));
        Assert.AreEqual("E020F867A9C407052FF4DA3D42046D845D84A5A7701460E8C18DF85F56DD9CE4", conjunta);
    }
}
