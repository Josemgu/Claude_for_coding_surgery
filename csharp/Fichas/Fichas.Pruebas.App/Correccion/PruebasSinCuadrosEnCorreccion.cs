using Fichas.App.Correccion;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Requisito 9 del dueño —«avisar, nunca impedir»— cazado sobre las fuentes de Corrección:
/// cero cuadros que detengan al usuario, salvo el que elimina a una persona.
/// </summary>
/// <remarks>
/// <para>Es la quinta de esta familia (Asignar y Revisar, Inicio, Reportes y Paquetes,
/// Importar). Corrección no tenía la suya porque hasta el 2026-09-14 no abría ningún cuadro:
/// lo decía la cabecera de <c>PaginaDeCorreccion.xaml.cs</c> y lo cumplía.</para>
///
/// <para>⛔ <b>Ahora sí abre uno</b>, y por eso hace falta esta prueba. Eliminar a una persona
/// es un borrado —la excepción declarada del programa (DECISIONES.md 2026-09-04, punto 9)— y
/// va por otro puerto que el de los documentos, <c>IPersonas.Borrar</c>, así que no se pudo
/// reutilizar <c>Revisar/OperacionDeBorrar.cs</c>, atada a <c>IMantenimiento</c> en su firma.
/// Borrar el documento entero SÍ va por aquella, y por eso esta carpeta no necesita un segundo
/// cuadro para eso. La excepción se declara por nombre, se comprueba que de verdad pregunta, y
/// se sigue exigiendo cero cuadros en todo lo demás de esta pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasSinCuadrosEnCorreccion
{
    /// <summary>Las cuatro palabras que abren un cuadro que detiene al usuario.</summary>
    private static readonly string[] LoProhibido =
        ["ContentDialog", "MessageDialog", "ShowAsync", "MessageBox"];

    /// <summary>El único archivo de esta pantalla al que se le permite preguntar.</summary>
    private const string ElUnicoQuePuedePreguntar = "OperacionDeEliminarUnaPersona.cs";

    /// <summary>Cero cuadros modales en las fuentes de Corrección, con su denominador, salvo el declarado.</summary>
    [TestMethod]
    public void NiUnCuadroModalEnCorreccionSalvoElQueElimina()
    {
        var carpeta = CarpetaDeCorreccion();
        if (carpeta is null)
        {
            Assert.Inconclusive(
                "No se encontró «csharp/Fichas/Fichas.App/Correccion» desde "
                + $"«{AppContext.BaseDirectory}». Sin las fuentes delante esto no comprueba nada.");
        }

        var archivos = Directory
            .EnumerateFiles(carpeta, "*.*", SearchOption.AllDirectories)
            .Where(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || a.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var culpables = new List<string>();
        var laExcepcionPregunta = false;
        foreach (var archivo in archivos)
        {
            var nombre = Path.GetFileName(archivo);
            var texto = File.ReadAllText(archivo);
            var esLaExcepcion = string.Equals(nombre, ElUnicoQuePuedePreguntar, StringComparison.Ordinal);

            foreach (var palabra in LoProhibido)
            {
                if (!texto.Contains(palabra, StringComparison.Ordinal)) continue;

                if (esLaExcepcion) laExcepcionPregunta = true;
                else culpables.Add($"{nombre} → {palabra}");
            }
        }

        Console.WriteLine($"Cuadros modales: {culpables.Count} en {archivos.Count} archivos de Corrección.");
        Assert.IsGreaterThanOrEqualTo(20, archivos.Count, $"Se esperaban al menos 20 archivos y se leyeron {archivos.Count}.");
        Assert.IsEmpty(culpables, "Cuadros encontrados: " + string.Join(" · ", culpables));
        Assert.IsTrue(
            laExcepcionPregunta,
            $"«{ElUnicoQuePuedePreguntar}» es la única excepción permitida y ya no abre ningún "
            + "cuadro. O eliminar dejó de preguntar —que el dueño no permite— o la excepción "
            + "sobra y hay que quitarla de esta prueba.");
    }

    /// <summary>El botón de eliminar existe en el XAML y su nombre para el lector es el del código.</summary>
    [TestMethod]
    public void ElBotonDeEliminarEstaEnElXamlConSuNombreParaElLector()
    {
        var carpeta = CarpetaDeCorreccion();
        if (carpeta is null) Assert.Inconclusive("No se encontró la carpeta de Corrección.");

        var xaml = System.Xml.Linq.XDocument.Load(Path.Combine(carpeta, "PaginaDeCorreccion.xaml"));
        var boton = xaml.Descendants()
            .FirstOrDefault(e => e.Attribute(
                System.Xml.Linq.XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
                ?.Value == "_botonDeEliminar");

        Assert.IsNotNull(boton, "no se encontró el botón «_botonDeEliminar» en el XAML");
        Assert.AreEqual(TextoDeEliminar.BotonParaElLector, boton.Attribute("AutomationProperties.Name")?.Value);
        Assert.AreEqual(TextoDeEliminar.Boton, boton.Attribute("Content")?.Value);
    }

    /// <summary>La carpeta de Corrección, buscada subiendo desde donde corre la prueba.</summary>
    private static string? CarpetaDeCorreccion()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var carpeta = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Correccion");
            if (Directory.Exists(carpeta)) return carpeta;
            actual = actual.Parent;
        }

        return null;
    }
}
