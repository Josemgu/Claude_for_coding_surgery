using System.Security.AccessControl;
using System.Security.Principal;
using Fichas.App.Importar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Que pasa cuando dentro de la carpeta elegida hay algo que Windows no deja leer.
/// </summary>
/// <remarks>
/// <para>El caso NO es raro y no hace falta un permiso exotico para llegar a el: dentro de
/// la carpeta de un usuario de Windows viven uniones heredadas —«Application Data», «My
/// Documents», «Local Settings»— que existen para programas de hace veinte años y que el
/// propio Windows deniega. Medido en esta maquina el 2026-09-07:</para>
/// <code>
/// [System.IO.Directory]::EnumerateFiles("C:\Users\josem", "*", "AllDirectories")
///   → System.UnauthorizedAccessException
///     Access to the path 'C:\Users\josem\AppData\Local\Application Data' is denied.
/// </code>
/// <para>Lo que se exige aqui es el requisito 9 del proyecto —avisar, nunca impedir— y su
/// otra mitad: nunca fallar callado. Una carpeta que no se deja leer NO puede costar la
/// tanda entera, y lo que se quedo fuera tiene que poder NOMBRARSE. Con 3 000 formularios,
/// «no se pudo leer una carpeta» sin decir cual no es un aviso: es una adivinanza.</para>
///
/// <para>⚠️ Estas pruebas deniegan de verdad, con una ACE del sistema de archivos sobre el
/// propio usuario que corre las pruebas. Simular la denegacion con un doble seria probar el
/// doble: lo que rompio la tanda fue el sistema de archivos de verdad.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasCarpetasQueNoSeDejanLeer
{
    private string _carpeta = string.Empty;

    /// <summary>Una carpeta de trabajo nueva por prueba.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(
            Path.GetTempPath(), "fichas-pruebas-denegadas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>
    /// Devuelve el permiso antes de borrar: una carpeta denegada no se borra sola.
    /// </summary>
    [TestCleanup]
    public void Recoger()
    {
        foreach (var denegada in _denegadas) DevolverElPermiso(denegada);
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private readonly List<string> _denegadas = [];

    // ── El criterio ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dado PDF legibles y una subcarpeta denegada, cuando se reune, entran los legibles.
    /// </summary>
    /// <remarks>
    /// Es el corazon del pase: lo que se puede leer se lee. Antes de este arreglo la
    /// excepcion se escapaba de <c>Reunir</c> y la tanda entera moria en 0 documentos.
    /// </remarks>
    [TestMethod]
    public void LoQueSiSePuedeLeerEntraAunqueUnaSubcarpetaEsteDenegada()
    {
        PdfEn(_carpeta, "a.pdf");
        PdfEn(Path.Combine(_carpeta, "septiembre"), "b.pdf");
        DenegarLaCarpeta(Path.Combine(_carpeta, "prohibida"), "c.pdf");

        var encontrado = RutasDePdf.Reunir([_carpeta]);

        Assert.HasCount(2, encontrado.Pdf,
            "los dos PDF legibles tienen que entrar aunque una subcarpeta este denegada.");
    }

    /// <summary>Y la carpeta que no se dejo leer queda NOMBRADA, con su ruta entera.</summary>
    [TestMethod]
    public void LaCarpetaQueNoSeDejoLeerQuedaNombradaConSuRuta()
    {
        PdfEn(_carpeta, "a.pdf");
        var prohibida = DenegarLaCarpeta(Path.Combine(_carpeta, "prohibida"), "c.pdf");

        var encontrado = RutasDePdf.Reunir([_carpeta]);

        Assert.HasCount(1, encontrado.CarpetasQueNoSeDejaronLeer);
        Assert.AreEqual(prohibida, encontrado.CarpetasQueNoSeDejaronLeer[0].Ruta);
        Assert.Contains("permiso", encontrado.CarpetasQueNoSeDejaronLeer[0].Motivo,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// El aviso de una tanda a medias dice las dos cifras y nombra lo que se quedo fuera.
    /// </summary>
    /// <remarks>
    /// Requisito 4 del dueno («ni un parrafo en pantalla»): la linea cabe en un renglon y
    /// las rutas enteras van al detalle, que se abre con «ver».
    /// </remarks>
    [TestMethod]
    public void ElAvisoDiceCuantosEntraronCuantosNoYNombraLoQueNoEntro()
    {
        PdfEn(_carpeta, "a.pdf");
        var prohibida = DenegarLaCarpeta(Path.Combine(_carpeta, "prohibida"), "c.pdf");

        var aviso = RutasDePdf.Reunir([_carpeta]).AvisoDeLaBusqueda();

        Assert.IsNotNull(aviso);
        Assert.AreEqual(GravedadDeAviso.Advertencia, aviso.Gravedad);
        Assert.DoesNotContain("\n", aviso.Linea, "la franja pinta UN renglon.");
        Assert.IsLessThanOrEqualTo(
            AvisoDeUnFalloEnPantalla.LargoMaximoDeLaLinea, aviso.Linea.Length);
        Assert.Contains("1 PDF", aviso.Linea);
        Assert.Contains("prohibida", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(aviso.Detalle);
        Assert.Contains(prohibida, aviso.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cuando no se pudo leer NADA, el aviso NO dice que la carpeta este vacia.
    /// </summary>
    /// <remarks>
    /// «No habia ningun PDF» y «no me dejaron mirar» son cosas distintas, y decir la
    /// primera cuando pasa la segunda es mentirle al dueño: se quedaria tan tranquilo
    /// creyendo que ahi no habia nada que importar.
    /// </remarks>
    [TestMethod]
    public void CuandoNoSePudoLeerNadaElAvisoNoDiceQueEsteVacia()
    {
        var prohibida = DenegarLaCarpeta(Path.Combine(_carpeta, "prohibida"), "c.pdf");

        var encontrado = RutasDePdf.Reunir([_carpeta]);
        var aviso = encontrado.AvisoDeLaBusqueda();

        Assert.IsEmpty(encontrado.Pdf);
        Assert.IsNotNull(aviso);
        Assert.AreEqual(GravedadDeAviso.Problema, aviso.Gravedad,
            "no poder mirar pesa mas que no encontrar.");
        Assert.DoesNotContain("vac", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no había ningún PDF", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no se pudo entrar", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(aviso.Detalle);
        Assert.Contains(prohibida, aviso.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Una carpeta de verdad vacia SI dice que no habia ningun PDF, y no habla de permisos.
    /// </summary>
    [TestMethod]
    public void UnaCarpetaSinPdfSigueDiciendoQueNoHabiaNingunPdf()
    {
        var aviso = RutasDePdf.Reunir([_carpeta]).AvisoDeLaBusqueda();

        Assert.IsNotNull(aviso);
        Assert.AreEqual(GravedadDeAviso.Advertencia, aviso.Gravedad);
        Assert.Contains("ningún PDF", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permiso", aviso.Linea, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Una carpeta normal no deja ningun aviso: no hay nada que contar.</summary>
    [TestMethod]
    public void UnaCarpetaNormalNoDejaNingunAviso()
    {
        PdfEn(_carpeta, "a.pdf");
        PdfEn(Path.Combine(_carpeta, "septiembre"), "b.pdf");

        var encontrado = RutasDePdf.Reunir([_carpeta]);

        Assert.HasCount(2, encontrado.Pdf);
        Assert.IsEmpty(encontrado.CarpetasQueNoSeDejaronLeer);
        Assert.IsNull(encontrado.AvisoDeLaBusqueda());
    }

    /// <summary>
    /// Una subcarpeta denegada a media profundidad no corta las que van despues.
    /// </summary>
    /// <remarks>
    /// <c>Directory.EnumerateFiles(…, AllDirectories)</c> aborta en la primera carpeta que
    /// no le dejan y se lleva por delante TODO lo que faltaba por recorrer, no solo lo de
    /// dentro de esa carpeta. Por eso el recorrido se hace a mano, carpeta a carpeta.
    /// </remarks>
    [TestMethod]
    public void UnaSubcarpetaDenegadaAMedioCaminoNoCortaLasQueVienenDespues()
    {
        PdfEn(Path.Combine(_carpeta, "1-antes"), "a.pdf");
        DenegarLaCarpeta(Path.Combine(_carpeta, "2-prohibida"), "c.pdf");
        PdfEn(Path.Combine(_carpeta, "3-despues"), "b.pdf");
        PdfEn(Path.Combine(_carpeta, "3-despues", "mas-adentro"), "d.pdf");

        var encontrado = RutasDePdf.Reunir([_carpeta]);

        Assert.HasCount(3, encontrado.Pdf,
            "lo que viene despues de la carpeta denegada tiene que entrar igual.");
        Assert.HasCount(1, encontrado.CarpetasQueNoSeDejaronLeer);
    }

    /// <summary>
    /// Una union que apunta hacia atras no cuelga el programa ni repite los archivos.
    /// </summary>
    /// <remarks>
    /// Cualquiera puede crear una con <c>mklink /J</c> sin ser administrador, y el
    /// recorrido a mano seguiria dando vueltas para siempre si no llevara memoria de por
    /// donde ya paso. Colgar el programa seria peor que el fallo que este pase arregla.
    /// </remarks>
    [TestMethod]
    public void UnaUnionQueApuntaHaciaAtrasNiCuelgaNiRepite()
    {
        PdfEn(_carpeta, "a.pdf");
        var union = Path.Combine(_carpeta, "vuelta");
        if (!CrearUnion(union, _carpeta)) Assert.Inconclusive("mklink /J no se pudo crear aqui.");

        var reunion = Task.Run(() => RutasDePdf.Reunir([_carpeta]));

        Assert.IsTrue(reunion.Wait(TimeSpan.FromSeconds(20)),
            "el recorrido tiene que terminar, no dar vueltas.");
        Assert.HasCount(1, reunion.Result.Pdf, "el mismo PDF no entra dos veces por dos caminos.");
    }

    /// <summary>Un PDF suelto elegido a mano sigue entrando, con carpetas denegadas o sin ellas.</summary>
    [TestMethod]
    public void UnPdfSueltoElegidoAManoSigueEntrando()
    {
        var suelto = PdfEn(_carpeta, "a.pdf");
        DenegarLaCarpeta(Path.Combine(_carpeta, "prohibida"), "c.pdf");

        var encontrado = RutasDePdf.Reunir([suelto]);

        Assert.HasCount(1, encontrado.Pdf);
        Assert.IsEmpty(encontrado.CarpetasQueNoSeDejaronLeer,
            "elegir un archivo suelto no recorre ninguna carpeta.");
    }

    // ── Andamio ────────────────────────────────────────────────────────────────────

    /// <summary>Escribe un archivo con ese nombre y devuelve su ruta entera.</summary>
    private static string PdfEn(string carpeta, string nombre)
    {
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, "no se abre en estas pruebas");
        return Path.GetFullPath(ruta);
    }

    /// <summary>
    /// Crea una carpeta con un PDF dentro y le quita al usuario el permiso de listarla.
    /// </summary>
    /// <remarks>
    /// Una ACE de denegacion sobre el propio usuario no necesita ser administrador: se es
    /// dueño de la carpeta recien creada. Es lo mismo que Windows tiene puesto en las
    /// uniones heredadas de la carpeta del perfil.
    /// </remarks>
    private string DenegarLaCarpeta(string carpeta, string pdfDentro)
    {
        PdfEn(carpeta, pdfDentro);

        var quienCorre = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("No se pudo saber quien corre las pruebas.");
        var carpetaInfo = new DirectoryInfo(carpeta);
        var permisos = carpetaInfo.GetAccessControl();
        permisos.AddAccessRule(new FileSystemAccessRule(
            quienCorre,
            FileSystemRights.ListDirectory | FileSystemRights.ReadData,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Deny));
        carpetaInfo.SetAccessControl(permisos);

        _denegadas.Add(Path.GetFullPath(carpeta));
        return Path.GetFullPath(carpeta);
    }

    /// <summary>Quita la denegacion para que la limpieza pueda borrar la carpeta.</summary>
    private static void DevolverElPermiso(string carpeta)
    {
        try
        {
            var quienCorre = WindowsIdentity.GetCurrent().User;
            if (quienCorre is null) return;

            var carpetaInfo = new DirectoryInfo(carpeta);
            var permisos = carpetaInfo.GetAccessControl();
            permisos.RemoveAccessRuleAll(new FileSystemAccessRule(
                quienCorre,
                FileSystemRights.ListDirectory | FileSystemRights.ReadData,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Deny));
            carpetaInfo.SetAccessControl(permisos);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            // La limpieza de una prueba no puede tumbar la suite.
        }
    }

    /// <summary>Crea una union de directorio con <c>mklink /J</c>; falso si no se pudo.</summary>
    private static bool CrearUnion(string donde, string aQue)
    {
        var proceso = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{donde}\" \"{aQue}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        if (proceso is null) return false;

        proceso.WaitForExit(10_000);
        return proceso.HasExited && proceso.ExitCode == 0 && Directory.Exists(donde);
    }
}
