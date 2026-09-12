using System.ComponentModel;
using System.Diagnostics;

namespace Fichas.App.Actualizacion;

/// <summary>
/// Arranca el instalador de verdad, en silencio, como un proceso aparte.
/// </summary>
/// <remarks>
/// <para><b>Cómo vuelve a abrirse el programa después.</b> Fichas lanza el instalador con
/// <c>/SILENT</c> y se cierra. El instalador (Inno Setup, <c>instalador/Fichas.iss</c>)
/// cierra lo que quede abierto, vacía la carpeta del programa, copia la versión nueva y, al
/// terminar, ejecuta su entrada <c>[Run]</c> con <c>postinstall</c>, que abre
/// <c>Fichas.exe</c>. Para que eso ocurra en silencio se quitó de esa entrada la bandera
/// <c>skipifsilent</c> el 2026-09-11: con ella puesta, el instalador silencioso terminaba y
/// el programa se quedaba cerrado.</para>
///
/// <para><c>/SILENT</c> y no <c>/VERYSILENT</c>: con el primero se ve la barra de progreso,
/// y el dueño sabe que algo pasa mientras el programa está cerrado. <c>/SUPPRESSMSGBOXES</c>
/// para que ningún cuadro se quede esperando un clic. <c>/NORESTART</c> porque el programa
/// no toca nada que pida reiniciar Windows.</para>
///
/// <para><c>/carpetadedatos=</c> es un parámetro propio del guion (<c>{param:carpetadedatos}</c>
/// en Fichas.iss): el instalador se lo pasa como <c>--carpeta-de-datos</c> al Fichas que
/// reabre, para que un programa arrancado sobre otra carpeta no vuelva abierto sobre la de
/// por defecto. Medido el 2026-09-12 con Inno Setup 6.7.3: llega entero aunque lleve espacios.</para>
/// </remarks>
public sealed class LanzadorDelInstalador : ILanzadorDelInstalador
{
    /// <summary>Los argumentos fijos con los que se lanza el instalador; <see cref="ArgumentosPara"/> les añade la carpeta.</summary>
    private const string ArgumentosFijos = "/SILENT /SUPPRESSMSGBOXES /NORESTART";

    /// <summary>Dónde anotar si Windows no dejó arrancarlo.</summary>
    private readonly Action<string> _anotar;

    /// <summary>Monta el lanzador con su cuaderno.</summary>
    /// <param name="anotar">Dónde escribir el motivo si no arrancó.</param>
    public LanzadorDelInstalador(Action<string> anotar) => _anotar = anotar;

    /// <summary>La línea de argumentos completa: los fijos y la carpeta de datos entre comillas y sin barra final.</summary>
    /// <remarks>
    /// Sin la barra final porque <c>"C:\carpeta\"</c> es, para quien parte la línea de órdenes
    /// en Windows, una comilla escapada: el argumento se comería la comilla de cierre y
    /// seguiría hasta la siguiente. Una carpeta de Windows no puede llevar comillas dentro.
    /// </remarks>
    /// <param name="carpetaDeDatos">La carpeta de datos con la que está abierto el programa.</param>
    public static string ArgumentosPara(string carpetaDeDatos)
        => $"{ArgumentosFijos} /carpetadedatos=\"{Path.TrimEndingDirectorySeparator(carpetaDeDatos)}\"";

    /// <inheritdoc />
    public bool Lanzar(string rutaDelInstalador, string carpetaDeDatos)
    {
        try
        {
            var arranque = new ProcessStartInfo(rutaDelInstalador, ArgumentosPara(carpetaDeDatos))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(rutaDelInstalador) ?? Path.GetTempPath(),
            };
            using var proceso = Process.Start(arranque);
            return proceso is not null;
        }
        catch (Exception fallo) when (fallo is Win32Exception or InvalidOperationException or IOException)
        {
            _anotar($"ACTUALIZACION  no se pudo lanzar el instalador  {fallo.GetType().Name}: {fallo.Message}");
            return false;
        }
    }
}
