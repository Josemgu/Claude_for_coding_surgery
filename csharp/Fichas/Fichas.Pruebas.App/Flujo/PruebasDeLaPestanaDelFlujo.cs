using System.Xml.Linq;
using Fichas.App.Vocabulario;

namespace Fichas.Pruebas.App.Flujo;

/// <summary>
/// Que las tres listas salieron de Inicio, que estan en la pestana nueva, y que la pestana
/// nueva tiene puerta en el menu.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Peticion del dueno del 2026-09-07, repetida el 2026-09-09
/// porque no se habia hecho: <i>«Listo para asignar debe ser una ventana, no debe estar en el
/// home del sistema. Tampoco asignar a los agentes, eso debe estar en otra ventana. Y listo
/// para viajar igual: los tres en una misma pestana. Lo unico que quiero [en Inicio] es el
/// calendario y un cuadro informando cuales hacen falta por completar, y cuantos casos tienen
/// los agentes»</i>.</para>
///
/// <para>⚠️ <b>Esto deshace en parte lo que el mismo pidio el 2026-09-05</b> —«lo unico que
/// quiero ver en Home es lo que esta listo para asignar y lo que esta asignado a los
/// agentes»—, que estaba construido y probado. Aquella prueba exigia esos dos rotulos EN
/// Inicio; esta exige que no esten. Es el mismo archivo de criterio con la decision nueva
/// dentro, no una prueba que se anade encima de la vieja.</para>
///
/// <para><b>Lo que estas pruebas defienden de verdad</b> no es la mudanza: es que nada se
/// quede sin puerta. Cada una de las tres listas era la UNICA forma de llegar a lo que
/// ensena, asi que se comprueba que la puerta nueva existe ANTES de dar por buena la
/// mudanza. Se mira el XAML porque es lo que el dueno lee: una regla que solo mirara los
/// datos no cazaria un rotulo olvidado ni una entrada de menu que falta.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaPestanaDelFlujo
{
    /// <summary>Los tres rotulos que el dueno saco de Inicio, y el cuadro que iba con ellos.</summary>
    private static readonly string[] LasTresListas =
    [
        "Listo para asignar",
        "Asignado a los agentes",
        "Listo para viajar",
        "Cuánto lleva cada compañero",
    ];

    /// <summary>Lo unico que el dueno quiere leer en Inicio.</summary>
    private static readonly string[] LoQueSeQuedaEnInicio =
    [
        "Me falta por completar",
        "Lo que tienen los agentes",
    ];

    /// <summary>
    /// Ninguna de las tres listas se lee ya en Inicio.
    /// </summary>
    /// <remarks>
    /// Sus palabras del 2026-09-09: <i>«Si, como te solicite, debe estar todo eso en una
    /// ventana aparte, no debe estar en Inicio»</i>.
    /// </remarks>
    [TestMethod]
    public void InicioYaNoEnsenaNingunaDeLasTresListas()
    {
        var rotulos = RotulosDe(Xaml("Inicio", "PaginaDeInicio.xaml"));

        var siguen = LasTresListas.Where(r => rotulos.Contains(r, StringComparer.Ordinal)).ToList();

        Console.WriteLine($"Rótulos leídos en Inicio: {rotulos.Count}. De las tres listas siguen: {siguen.Count}.");
        Assert.IsGreaterThanOrEqualTo(
            4, rotulos.Count, "Un barrido que no leyó rótulos no comprueba nada.");
        Assert.IsEmpty(siguen, "Listas que el dueño sacó de Inicio y siguen ahí: " + string.Join(" · ", siguen));
    }

    /// <summary>En Inicio se lee el cuadro que pidio: lo que falta por completar y lo de los agentes.</summary>
    [TestMethod]
    public void InicioEnsenaElCuadroDeLasDosCifrasQuePidio()
    {
        var rotulos = RotulosDe(Xaml("Inicio", "PaginaDeInicio.xaml"));

        foreach (var cual in LoQueSeQuedaEnInicio)
            Assert.Contains(cual, rotulos, $"Falta en Inicio el rótulo «{cual}» del cuadro que pidió el dueño.");
    }

    /// <summary>Las tres listas se leen enteras en la pestana nueva.</summary>
    [TestMethod]
    public void LaPestanaDelFlujoEnsenaLasTresListas()
    {
        var rotulos = RotulosDe(Xaml("Flujo", "PaginaDelFlujo.xaml"));

        Console.WriteLine($"Rótulos leídos en la pestaña del flujo: {rotulos.Count}.");
        foreach (var cual in LasTresListas)
            Assert.Contains(cual, rotulos, $"La pestaña del flujo no enseña «{cual}», que se quitó de Inicio.");
    }

    /// <summary>
    /// La pestana nueva tiene entrada propia en el menu, y el menu pasa de ocho a nueve.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es la unica puerta a las tres listas y por eso se comprueba aparte.</b> Sin
    /// entrada de menu, quitarlas de Inicio las borraria del programa: no hay ninguna otra
    /// pantalla que ensene lo mismo. Medido antes de mover nada, sobre el codigo del
    /// 2026-09-09: Asignar lista TODOS los casos sin filtrar por estado ni por dueno, y su
    /// cuadro de equipo cuenta otra cosa; «listo para viajar» no existe en ninguna otra parte.
    /// </remarks>
    [TestMethod]
    public void ElMenuTieneLaEntradaDeLaPestanaNueva()
    {
        var entradas = EntradasDelMenu();

        Console.WriteLine($"Entradas del menú: {entradas.Count} — {string.Join(" · ", entradas.Select(e => e.Rotulo))}.");
        Assert.HasCount(9, entradas, "El menú tiene que pasar de ocho entradas a nueve.");
        Assert.Contains("Flujo", entradas.Select(e => e.Tag).ToList(), "Falta la entrada «Flujo» en el menú.");
    }

    /// <summary>La cascara sabe traducir la entrada nueva a su pantalla; si no, el menu no lleva a nada.</summary>
    [TestMethod]
    public void LaCascaraTraduceLaEntradaNuevaASuPantalla()
    {
        var codigo = File.ReadAllText(Path.Combine(LaCarpeta("Cascara"), "VentanaPrincipal.xaml.cs"));

        Assert.Contains(
            "\"Flujo\" => typeof(",
            codigo,
            "El menú tiene la entrada «Flujo» pero la cáscara no la traduce: pulsarla llevaría a Inicio.");
    }

    /// <summary>
    /// Inicio conserva la puerta a la ventana de lo que no esta completo.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es la unica puerta de todo el programa a <c>PaginaDeIncompletos</c></b>, medido
    /// el 2026-09-09 buscando cada <c>Navigate(typeof(PaginaDeIncompletos)</c> del codigo: la
    /// referencia estaba solo en Inicio. Hasta hoy era un boton suelto; ahora es la cifra del
    /// cuadro, que ademas dice cuantos son. Si esta prueba se pone roja, esa ventana se quedo
    /// sin forma de abrirse.
    /// </remarks>
    [TestMethod]
    public void InicioSigueLlevandoALaVentanaDeLoQueNoEstaCompleto()
    {
        var codigo = File.ReadAllText(Path.Combine(LaCarpeta("Inicio"), "PaginaDeInicio.xaml.cs"));

        Assert.Contains(
            "typeof(PaginaDeIncompletos)",
            codigo,
            "Inicio dejó de llevar a la ventana de lo que no está completo, y no hay otra puerta.");
    }

    /// <summary>
    /// En las tres pantallas del terreno no se lee ninguna palabra de estado que no sean las dos.
    /// </summary>
    /// <remarks>
    /// Decision del dueno del 2026-09-07: <i>«Dos estados nada mas: resuelto y me falta»</i>.
    /// Se buscan las CUATRO palabras que retiro, como texto suelto en un rotulo. «Listo para
    /// asignar» y «Listo para viajar» siguen siendo el NOMBRE de dos listas —dicen que hay
    /// dentro, no en que estado esta un renglon—, y por eso lo que se persigue es la palabra
    /// de estado de un renglon: «confirmada», «completada» y «no completada».
    /// </remarks>
    [TestMethod]
    public void NoSeLeeNingunaPalabraDeEstadoQueNoSeanLasDos()
    {
        string[] lasRetiradas = ["confirmadas", "confirmada", "no completada", "completada"];

        var culpables = new List<string>();
        foreach (var (donde, rotulo) in RotulosDeLasTres())
        {
            foreach (var mala in lasRetiradas)
            {
                // Solo cuenta si el rótulo ES esa palabra o la lleva como estado de un renglón,
                // no si aparece dentro de una frase que explica qué se está contando.
                if (rotulo.Equals(mala, StringComparison.OrdinalIgnoreCase)
                    || rotulo.Contains($"· {mala}", StringComparison.OrdinalIgnoreCase))
                    culpables.Add($"{donde}: «{rotulo}» → «{mala}»");
            }
        }

        Console.WriteLine(
            $"Palabras de estado retiradas que siguen: {culpables.Count}. "
            + $"Las dos que valen: {string.Join(" · ", DosEstados.LasDos)}.");
        Assert.IsEmpty(culpables, "Palabras de estado que el dueño retiró:" + Environment.NewLine
            + string.Join(Environment.NewLine, culpables));
    }

    // ---- de donde salen los rotulos ------------------------------------------

    /// <summary>Los rotulos de las tres pantallas del terreno, con su archivo delante.</summary>
    private static List<(string Donde, string Rotulo)> RotulosDeLasTres()
    {
        var archivos = new[]
        {
            Xaml("Inicio", "PaginaDeInicio.xaml"),
            Xaml("Flujo", "PaginaDelFlujo.xaml"),
            Xaml("Grupo", "PaginaDeIncompletos.xaml"),
        };

        return [.. archivos.SelectMany(a => RotulosDe(a).Select(r => (Path.GetFileName(a), r)))];
    }

    /// <summary>Los textos escritos que un usuario lee en una pantalla, sin los enlaces.</summary>
    /// <param name="archivoXaml">La ruta del XAML que se lee.</param>
    private static List<string> RotulosDe(string archivoXaml)
        => [.. XDocument.Load(archivoXaml).Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName is "Text" or "Content")
            .Select(a => a.Value)
            .Where(v => v.Length > 0 && !v.StartsWith('{'))];

    /// <summary>Las entradas del menu de la izquierda, con su rotulo y su etiqueta.</summary>
    private static List<(string Rotulo, string Tag)> EntradasDelMenu()
    {
        var xaml = XDocument.Load(Xaml("Cascara", "VentanaPrincipal.xaml"));

        return [.. xaml.Descendants()
            .Where(e => e.Name.LocalName == "NavigationViewItem")
            .Select(e => (
                Rotulo: e.Attribute("Content")?.Value ?? string.Empty,
                Tag: e.Attribute("Tag")?.Value ?? string.Empty))
            .Where(e => e.Tag.Length > 0)];
    }

    /// <summary>La ruta de un XAML del terreno.</summary>
    /// <param name="carpeta">La carpeta de <c>Fichas.App</c>: «Inicio», «Flujo», «Grupo» o «Cascara».</param>
    /// <param name="archivo">El nombre del XAML dentro de ella.</param>
    private static string Xaml(string carpeta, string archivo) => Path.Combine(LaCarpeta(carpeta), archivo);

    /// <summary>Una carpeta de la app, buscada subiendo desde donde corre la prueba.</summary>
    /// <remarks>
    /// Si no la encuentra NO pasa en verde: se declara no concluyente. Una comprobacion que
    /// no encontro donde mirar no es una comprobacion.
    /// </remarks>
    /// <param name="cual">La carpeta de <c>Fichas.App</c> que se busca.</param>
    private static string LaCarpeta(string cual)
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var carpeta = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", cual);
            if (Directory.Exists(carpeta)) return carpeta;
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «csharp/Fichas/Fichas.App/{cual}» desde «{AppContext.BaseDirectory}». "
            + "Sin las fuentes delante esto no comprueba nada.");
        return string.Empty;
    }
}
