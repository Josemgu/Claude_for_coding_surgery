namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Requisito 4 y 9 del dueno, cazados con una busqueda sobre las fuentes de estas dos
/// pantallas: cero cuadros que detengan al usuario.
/// </summary>
/// <remarks>
/// «No pongas tanto texto, que se pueda cerrar.» Lo que sustituye al cuadro es la franja de
/// avisos de la cascara —una linea, «ver» y X— y el acuse del pie. Esta prueba lee los
/// archivos de <c>Fichas.App/Asignar/</c> y <c>Fichas.App/Revisar/</c> y cuenta con su
/// denominador dicho, para que la cifra signifique algo: «0 de N archivos».
///
/// Si no encuentra las carpetas —porque las pruebas corren desde un sitio del que no se
/// puede llegar al repositorio— NO pasa en verde: se declara no concluyente. Una
/// comprobacion que no encontro donde mirar no es una comprobacion.
///
/// ⛔ <b>Una sola excepcion, y esta declarada.</b> Desde el 2026-09-05 hay exactamente un
/// archivo que SI puede abrir un cuadro: <see cref="ElUnicoQuePuedePreguntar"/>. Es el que
/// borra, y borrar es una de las dos excepciones que el dueno reservo en persona
/// (DECISIONES.md 2026-09-04, «Requisitos del dueno para el programa nuevo», punto 9: la
/// firma de Miguel y borrar sin preguntar). La prueba NO se relajo para dejarlo pasar: se
/// apreto. Ahora ademas comprueba que ese archivo de verdad pregunta —una excepcion que
/// nadie usa es una puerta abierta que nadie vigila— y sigue exigiendo cero cuadros en
/// todos los demas.
/// </remarks>
[TestClass]
public sealed class PruebasSinCuadros
{
    /// <summary>Las cuatro palabras que abren un cuadro que detiene al usuario.</summary>
    private static readonly string[] LoProhibido =
        ["ContentDialog", "MessageDialog", "ShowAsync", "MessageBox"];

    /// <summary>El unico archivo de estas dos pantallas al que se le permite preguntar.</summary>
    private const string ElUnicoQuePuedePreguntar = "OperacionDeBorrar.cs";

    /// <summary>Cero cuadros modales en las fuentes de Asignar y de Revisar, con su denominador.</summary>
    [TestMethod]
    public void NiUnCuadroModalEnLasDosPantallas()
    {
        var carpetas = CarpetasDeLasDosPantallas();
        if (carpetas.Count == 0)
        {
            Assert.Inconclusive(
                "No se encontro «csharp/Fichas/Fichas.App/Asignar» desde " +
                $"«{AppContext.BaseDirectory}». Sin las fuentes delante esto no comprueba nada.");
        }

        var archivos = carpetas
            .SelectMany(c => Directory.EnumerateFiles(c, "*.*", SearchOption.AllDirectories))
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

        Console.WriteLine($"Cuadros modales: {culpables.Count} en {archivos.Count} archivos de Asignar y Revisar.");
        Assert.IsGreaterThanOrEqualTo(6, archivos.Count, $"Se esperaban al menos 6 archivos y se leyeron {archivos.Count}.");
        Assert.IsEmpty(culpables, "Cuadros encontrados: " + string.Join(" · ", culpables));

        // La otra mitad de la guarda: la excepcion tiene que seguir siendo de verdad. Si
        // «OperacionDeBorrar.cs» dejara de preguntar, o le cambiaran el nombre, esta prueba
        // se pone roja en vez de quedarse permitiendo algo que ya no existe.
        Assert.IsTrue(
            laExcepcionPregunta,
            $"«{ElUnicoQuePuedePreguntar}» es la única excepción permitida y ya no abre ningún "
            + "cuadro. O borrar dejó de preguntar —que el dueño no permite— o la excepción "
            + "sobra y hay que quitarla de esta prueba.");
    }

    /// <summary>Las dos carpetas de estas pantallas, buscadas subiendo desde donde corre la prueba.</summary>
    private static List<string> CarpetasDeLasDosPantallas()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var asignar = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Asignar");
            if (Directory.Exists(asignar))
            {
                var revisar = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Revisar");
                return Directory.Exists(revisar) ? [asignar, revisar] : [asignar];
            }
            actual = actual.Parent;
        }
        return [];
    }
}
