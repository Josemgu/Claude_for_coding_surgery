using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fichas.App.Inicio;

/// <summary>
/// El banco de medidas de la pantalla de Inicio: cuanto tarda en pintarse, cuantos
/// elementos visuales quedan vivos y donde estan las barras de desplazamiento.
/// </summary>
/// <remarks>
/// Existe porque los criterios C1-2, C1-3 y C1-5 son CIFRAS, y una cifra tomada por
/// fuera con un cronometro de PowerShell mide otra cosa —el arranque de Windows, el
/// disco, el antivirus—. La cuenta de elementos vivos usa el MISMO recorrido del arbol
/// visual que la espiga C0 (<c>csharp/C0-espiga/Espiga/MainPage.xaml.cs</c>), para que
/// las dos cifras se puedan comparar: alli fueron 193-202 con 3 000 filas.
///
/// ⚠️ Como se enciende: leyendo <see cref="Environment.GetCommandLineArgs"/> aqui mismo.
/// Lo natural seria anadir la bandera a <c>Cascara/ArgumentosDeArranque.cs</c>, pero ese
/// archivo esta CONGELADO y es de otro agente. Se dice para que quien descongele la
/// cascara mueva la bandera a su sitio.
/// </remarks>
public static class MedicionDeInicio
{
    /// <summary>La bandera que pide medir y escribir el informe.</summary>
    /// <remarks>
    /// Empieza por «/» y NO por «--» a proposito: <c>ArgumentosDeArranque</c> deja un aviso
    /// por cada argumento que empieza por «--» y no conoce, y ese aviso saldria en la
    /// franja de la cascara justo encima de la pantalla que se esta retratando. Medido:
    /// con «--medir-inicio» la captura del 2026-09-04 salio con el aviso puesto.
    /// </remarks>
    private const string BanderaDeMedir = "/medir-inicio";

    /// <summary>La bandera que ademas cierra el programa en cuanto termina de medir.</summary>
    private const string BanderaDeCerrar = "/cerrar-al-medir";

    /// <summary>Si se pidio medir por la linea de ordenes.</summary>
    public static bool EstaPedida => Banderas.Contains(BanderaDeMedir);

    /// <summary>Si ademas hay que cerrar el programa al terminar de medir.</summary>
    public static bool CierraAlTerminar => Banderas.Contains(BanderaDeCerrar);

    /// <summary>Donde se escribe el informe: el valor que sigue a la bandera, o el escritorio de trabajo.</summary>
    public static string Ruta { get; } = LeerLaRuta();

    /// <summary>
    /// Los argumentos con los que arrancó el programa, leídos una vez; el primero es la ruta del
    /// ejecutable, como siempre en <see cref="Environment.GetCommandLineArgs"/>.
    /// </summary>
    private static readonly string[] Banderas = Environment.GetCommandLineArgs();

    /// <summary>Anexa una linea al informe; si no se puede escribir, no se detiene nada.</summary>
    /// <param name="linea">La línea, sin salto al final; se añade el del sistema.</param>
    public static void Anotar(string linea)
    {
        try
        {
            File.AppendAllText(Ruta, linea + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo anotar la medicion: {fallo.Message}");
        }
    }

    /// <summary>
    /// Recorre el arbol visual entero desde una raiz y cuenta lo que de verdad existe.
    /// </summary>
    /// <remarks>
    /// Es el criterio C1-5: si este numero crece con los datos, no hay virtualizacion.
    /// Se cuentan tambien los contenedores de fila realizados, que es lo que delata a una
    /// lista que construyo un renglon por cada dato en vez de por cada hueco visible.
    /// </remarks>
    /// <param name="raiz">Desde dónde se cuenta; la raíz misma cuenta como uno.</param>
    /// <returns>Cuántos elementos hay en total y cuántos de ellos son contenedores de fila.</returns>
    public static (int Todos, int Contenedores) ContarElementosVivos(DependencyObject raiz)
    {
        var todos = 0;
        var contenedores = 0;
        var pendientes = new Stack<DependencyObject>();
        pendientes.Push(raiz);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();
            todos++;

            // SelectorItem es el contenedor de fila de ListView; ItemContainer, el de
            // ItemsView, que es el que conto la espiga C0. Se cuentan los dos para que la
            // cifra sea comparable con la suya.
            if (actual is Microsoft.UI.Xaml.Controls.Primitives.SelectorItem or ItemContainer) contenedores++;

            var cuantosHijos = VisualTreeHelper.GetChildrenCount(actual);
            for (var i = 0; i < cuantosHijos; i++)
            {
                pendientes.Push(VisualTreeHelper.GetChild(actual, i));
            }
        }

        return (todos, contenedores);
    }

    /// <summary>
    /// Busca las barras de desplazamiento del arbol y dice donde estan y de que tamano.
    /// </summary>
    /// <remarks>
    /// Sin esto, «la barra se ve» seria una opinion sobre una captura. Es el mismo
    /// metodo de la espiga C0, que es lo que hace comparables las dos mediciones.
    /// </remarks>
    /// <param name="raiz">Desde dónde se buscan las barras.</param>
    /// <param name="referencia">Respecto a qué elemento se dan las coordenadas; normalmente la página.</param>
    /// <returns>Una línea legible con cada barra, o «NINGUNA» si no hay ninguna con tamaño.</returns>
    public static string DescribirLasBarras(DependencyObject raiz, UIElement referencia)
    {
        var descripciones = new List<string>();
        var pendientes = new Stack<DependencyObject>();
        pendientes.Push(raiz);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();

            if (actual is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar barra && barra.ActualHeight + barra.ActualWidth > 0)
            {
                var esquina = barra.TransformToVisual(referencia).TransformPoint(new Windows.Foundation.Point(0, 0));
                descripciones.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} en ({1:F0},{2:F0}) de {3:F0}x{4:F0} px, visible={5}, opacidad={6:F2}",
                    barra.Orientation, esquina.X, esquina.Y,
                    barra.ActualWidth, barra.ActualHeight, barra.Visibility, barra.Opacity));
            }

            var cuantosHijos = VisualTreeHelper.GetChildrenCount(actual);
            for (var i = 0; i < cuantosHijos; i++)
            {
                pendientes.Push(VisualTreeHelper.GetChild(actual, i));
            }
        }

        return descripciones.Count == 0
            ? "barras con tamano en el arbol: NINGUNA"
            : "barras: " + string.Join(" | ", descripciones);
    }

    /// <summary>
    /// Cuantos renglones tiene construidos de verdad un repetidor.
    /// </summary>
    /// <remarks>
    /// Es la cifra del C1-5 en una linea: un repetidor que virtualiza construye tantos
    /// renglones como huecos se ven, y no tantos como datos hay detras. Si esta cifra
    /// crece con los datos, no hay virtualizacion y la fase no cierra.
    /// </remarks>
    /// <param name="repetidor">El <c>ItemsRepeater</c> cuyos hijos se cuentan.</param>
    public static int RenglonesRealizados(DependencyObject repetidor)
        => VisualTreeHelper.GetChildrenCount(repetidor);

    /// <summary>
    /// El alto que de verdad pide el contenido mas alto de un repetidor, sin el minimo
    /// que se le haya impuesto por fuera.
    /// </summary>
    /// <remarks>
    /// Sirve para saber si una celda del calendario esta CORTANDO lo que lleva dentro.
    /// Se mira el hijo de dentro del recuadro y no el recuadro: el recuadro ya viene
    /// estirado al alto que le puso la rejilla, y preguntarle a el daria siempre que si.
    /// </remarks>
    /// <param name="repetidor">El repetidor cuyas celdas se miran.</param>
    /// <returns>El alto en píxeles que pide la celda más alta, con su relleno; 0 si no hay ninguna.</returns>
    public static double AltoQuePideElContenido(DependencyObject repetidor)
    {
        var mayor = 0.0;
        var cuantas = VisualTreeHelper.GetChildrenCount(repetidor);

        for (var i = 0; i < cuantas; i++)
        {
            if (VisualTreeHelper.GetChild(repetidor, i) is not FrameworkElement celda) continue;
            if (VisualTreeHelper.GetChildrenCount(celda) == 0) continue;
            if (VisualTreeHelper.GetChild(celda, 0) is not FrameworkElement dentro) continue;

            mayor = Math.Max(mayor, dentro.DesiredSize.Height + celda.Padding().Top + celda.Padding().Bottom);
        }

        return mayor;
    }

    /// <summary>El hueco interior de un recuadro, o cero si el elemento no es un recuadro.</summary>
    /// <param name="elemento">El elemento que se mira; solo un <see cref="Border"/> tiene relleno propio.</param>
    private static Thickness Padding(this FrameworkElement elemento)
        => elemento is Border recuadro ? recuadro.Padding : new Thickness(0);

    /// <summary>Escribe un numero con punto decimal, para que la cifra no cambie con el idioma.</summary>
    /// <param name="valor">El número.</param>
    /// <param name="decimales">Cuántos decimales; uno por defecto.</param>
    public static string Cifra(double valor, int decimales = 1)
        => valor.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Lee la ruta que sigue a la bandera; si no viene ninguna, deja el informe junto al ejecutable.</summary>
    private static string LeerLaRuta()
    {
        var banderas = Environment.GetCommandLineArgs();
        for (var i = 0; i < banderas.Length - 1; i++)
        {
            if (banderas[i] == BanderaDeMedir
                && !banderas[i + 1].StartsWith("--", StringComparison.Ordinal)
                && !banderas[i + 1].StartsWith('/'))
                return banderas[i + 1];
        }

        return Path.Combine(AppContext.BaseDirectory, "medicion-inicio.txt");
    }
}
