using System.Diagnostics;
using Fichas.App.Cascara;
using Microsoft.UI.Xaml;

namespace Fichas.App;

/// <summary>
/// El arranque del programa: lee los argumentos, monta los servicios, abre la ventana
/// y anota en <c>fichas.log</c> cuanto tardo hasta tenerla lista.
/// </summary>
/// <remarks>
/// <para><b>El orden de arranque, en <see cref="OnLaunched"/>:</b></para>
/// <list type="number">
/// <item>Se leen los argumentos de la línea de órdenes con <see cref="ArgumentosDeArranque.Leer"/>
/// (saltando el ejecutable, que viene en la posición 0). De ahí sale la carpeta de datos, si
/// se pidieron datos inventados y el tamaño de la ventana.</item>
/// <item>Se abre el cuaderno de tiempos <see cref="Registro"/> en esa carpeta, y se engancha
/// <c>UnhandledException</c> para que un fallo que nadie recoja quede escrito antes de caerse.</item>
/// <item>Se montan los <see cref="Cascara.Servicios"/>: la base de verdad con sus migraciones,
/// o lo inventado con <c>--falso</c>, o lo falso vacío si la base no se pudo abrir.</item>
/// <item>Se construye y se activa la <see cref="VentanaPrincipal"/>; al cerrarse, libera la base.</item>
/// <item>Se para el cronómetro y se anota la cifra del arranque con qué datos y qué carpeta.</item>
/// </list>
/// <para>La medición de Inicio (<c>/medir-inicio</c>) no pasa por aquí: la lee
/// <c>Inicio.MedicionDeInicio</c> directamente de la línea de órdenes.</para>
/// </remarks>
public partial class App : Application
{
    /// <summary>La ventana principal, guardada para engancharle el cierre; nula hasta <see cref="OnLaunched"/>.</summary>
    private Window? _ventana;

    /// <summary>Cronometro del arranque; empieza en la primera linea que corre el programa.</summary>
    private readonly Stopwatch _cronometroDeArranque = Stopwatch.StartNew();

    /// <summary>Carga los recursos de <c>App.xaml</c>; el trabajo de verdad empieza en <see cref="OnLaunched"/>.</summary>
    public App() => InitializeComponent();

    /// <summary>Los servicios de toda la app; un solo sitio los registra (Cascara/Servicios.cs).</summary>
    public static Servicios? Servicios { get; private set; }

    /// <summary>La ventana, para que una prueba o una medicion puedan cerrarla.</summary>
    public static Window? Ventana { get; private set; }

    /// <summary>
    /// Abre la ventana principal y deja anotada la cifra del arranque. Es el punto de entrada
    /// real del programa; el orden está en los <c>remarks</c> de la clase.
    /// </summary>
    /// <param name="args">Lo que WinUI dice de la activación; no se usa: los argumentos se leen de <see cref="Environment.GetCommandLineArgs"/>.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Environment.GetCommandLineArgs trae el ejecutable en la posicion 0: se salta.
        var argumentos = ArgumentosDeArranque.Leer(Environment.GetCommandLineArgs().Skip(1).ToArray());
        var registro = new Registro(argumentos.CarpetaDeDatos);

        // Un fallo que nadie recoge cierra la ventana sin decir nada, y entonces no hay
        // forma de saber que paso. Aqui queda escrito en el cuaderno antes de caerse.
        UnhandledException += (quien, cuando) =>
        {
            registro.Anotar($"FALLO SIN RECOGER  {cuando.Exception}");
        };

        Servicios = Cascara.Servicios.Montar(argumentos, registro);

        _ventana = new VentanaPrincipal(Servicios);
        Ventana = _ventana;

        // La base tiene que quedar libre al salir. Sin esto, el archivo se queda tomado
        // por el proceso y la siguiente apertura —o un respaldo— falla sin decir por que.
        _ventana.Closed += (quien, cuando) =>
        {
            registro.Anotar("CIERRE  se cierra la base y se sale.");
            Servicios?.Dispose();
        };

        _ventana.Activate();

        _cronometroDeArranque.Stop();
        registro.AnotarArranque(
            _cronometroDeArranque.Elapsed.TotalMilliseconds,
            argumentos.CasosInventados,
            argumentos.CarpetaDeDatos);
    }
}
