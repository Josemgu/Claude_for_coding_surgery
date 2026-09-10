namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Requisito 9 del dueño —«avisar, nunca impedir»— cazado sobre las fuentes de Importar:
/// cero cuadros que detengan al usuario, salvo el que borra.
/// </summary>
/// <remarks>
/// <para>Es la cuarta de esta familia. Las otras tres —Asignar y Revisar, Inicio, Reportes
/// y Paquetes— existían desde antes; <c>Importar</c> no tenía la suya porque hasta el
/// 2026-09-09 no abría ningún cuadro propio: el borrado de documentos sin persona pasa por
/// <c>Revisar/OperacionDeBorrar.cs</c>, que está vigilado por la de Asignar.</para>
///
/// <para>⛔ <b>Ahora sí abre uno</b>, y por eso hace falta esta prueba. Borrar los renglones
/// de los PDF que no se pudieron leer es otro puerto —<c>IIlegibles</c>— y aquella clase
/// está atada a <c>IMantenimiento</c> en su firma, así que no se pudo reutilizar sin tocar
/// <c>Fichas.App/Revisar/</c>. La excepción se declara por nombre, se comprueba que de
/// verdad pregunta, y se sigue exigiendo cero cuadros en todo lo demás de esta pantalla.</para>
/// </remarks>
[TestClass]
public sealed class PruebasSinCuadrosEnImportar
{
    /// <summary>Las cuatro palabras que abren un cuadro que detiene al usuario.</summary>
    private static readonly string[] LoProhibido =
        ["ContentDialog", "MessageDialog", "ShowAsync", "MessageBox"];

    /// <summary>El único archivo de esta pantalla al que se le permite preguntar.</summary>
    private const string ElUnicoQuePuedePreguntar = "OperacionDeBorrarLosPdfIlegibles.cs";

    /// <summary>Cero cuadros modales en las fuentes de Importar, con su denominador.</summary>
    [TestMethod]
    public void NiUnCuadroModalEnImportarSalvoElQueBorra()
    {
        var carpeta = CarpetaDeImportar();
        if (carpeta is null)
        {
            Assert.Inconclusive(
                "No se encontró «csharp/Fichas/Fichas.App/Importar» desde "
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

        Console.WriteLine($"Cuadros modales: {culpables.Count} en {archivos.Count} archivos de Importar.");
        Assert.IsGreaterThanOrEqualTo(15, archivos.Count, $"Se esperaban al menos 15 archivos y se leyeron {archivos.Count}.");
        Assert.IsEmpty(culpables, "Cuadros encontrados: " + string.Join(" · ", culpables));

        Assert.IsTrue(
            laExcepcionPregunta,
            $"«{ElUnicoQuePuedePreguntar}» es la única excepción permitida y ya no abre ningún "
            + "cuadro. O borrar dejó de preguntar —que el dueño no permite— o la excepción "
            + "sobra y hay que quitarla de esta prueba.");
    }

    /// <summary>La carpeta de Importar, buscada subiendo desde donde corre la prueba.</summary>
    private static string? CarpetaDeImportar()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var importar = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Importar");
            if (Directory.Exists(importar)) return importar;
            actual = actual.Parent;
        }

        return null;
    }
}
