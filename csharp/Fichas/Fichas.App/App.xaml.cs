using System.Diagnostics;
using Fichas.App.Cascara;
using Microsoft.UI.Xaml;

namespace Fichas.App;

/// <summary>
/// El arranque del programa: lee los argumentos, monta los servicios, abre la ventana
/// y anota en <c>fichas.log</c> cuanto tardo hasta tenerla lista.
/// </summary>
public partial class App : Application
{
    private Window? _ventana;

    /// <summary>Cronometro del arranque; empieza en la primera linea que corre el programa.</summary>
    private readonly Stopwatch _cronometroDeArranque = Stopwatch.StartNew();

    /// <summary>Monta la aplicacion.</summary>
    public App() => InitializeComponent();

    /// <summary>Los servicios de toda la app; un solo sitio los registra (Cascara/Servicios.cs).</summary>
    public static Servicios? Servicios { get; private set; }

    /// <summary>La ventana, para que una prueba o una medicion puedan cerrarla.</summary>
    public static Window? Ventana { get; private set; }

    /// <summary>Abre la ventana principal y deja anotada la cifra del arranque.</summary>
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
