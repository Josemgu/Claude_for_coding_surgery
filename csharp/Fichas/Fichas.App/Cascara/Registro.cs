using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Fichas.App.Cascara;

/// <summary>
/// El cuaderno de tiempos: <c>fichas.log</c> en la carpeta de datos.
/// </summary>
/// <remarks>
/// Es el equivalente del registro de tiempos del programa en Python. Dos cifras y nada
/// mas: cuanto tardo el arranque hasta que la ventana estuvo lista, y cuanto tarda cada
/// navegacion. Sin numero no hay forma de saber si una pantalla cumple los 0,2 s del
/// requisito 5, y una fase se cierra «rapida» sin haber medido nada.
/// Si el archivo no se puede escribir, el programa NO se detiene: dejar de anotar es
/// peor que nada, pero no arrancar es peor todavia.
/// </remarks>
public sealed class Registro
{
    /// <summary>La ruta de <c>fichas.log</c>, o nula si la carpeta no se pudo crear: entonces no se anota nada.</summary>
    private readonly string? _ruta;

    /// <summary>Una línea cada vez: dos hilos anotando a la vez se pisarían dentro del archivo.</summary>
    private readonly Lock _candado = new();

    /// <summary>Abre el cuaderno en la carpeta de datos; si no se puede, se queda callado.</summary>
    public Registro(string carpetaDeDatos)
    {
        try
        {
            Directory.CreateDirectory(carpetaDeDatos);
            _ruta = Path.Combine(carpetaDeDatos, "fichas.log");
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // No se puede escribir ahi. Se sigue sin cuaderno, y se dice por consola.
            _ruta = null;
            Debug.WriteLine($"No se pudo abrir fichas.log en «{carpetaDeDatos}»: {fallo.Message}");
        }
    }

    /// <summary>Donde quedo el cuaderno, o nulo si no se pudo abrir.</summary>
    public string? Ruta => _ruta;

    /// <summary>Anota una linea con su marca de tiempo.</summary>
    public void Anotar(string linea)
    {
        if (_ruta is null) return;
        var marca = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        try
        {
            lock (_candado)
            {
                File.AppendAllText(_ruta, $"{marca}  {linea}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"No se pudo anotar en fichas.log: {fallo.Message}");
        }
    }

    /// <summary>Anota el arranque con su cifra en milisegundos y en segundos.</summary>
    /// <remarks>
    /// Dice de que datos se abrio, y no es un adorno: si el programa arranco con datos
    /// inventados, cualquier cifra que se mida despues es de mentira, y sin esta linea no
    /// habria forma de saberlo al leer el cuaderno una semana despues.
    /// </remarks>
    public void AnotarArranque(double milisegundos, int? casosInventados, string carpetaDeDatos)
        => Anotar(string.Format(
            CultureInfo.InvariantCulture,
            "ARRANQUE  ventana lista en {0:F0} ms ({1:F2} s)  datos={2}  carpeta={3}",
            milisegundos,
            milisegundos / 1000.0,
            casosInventados is null ? "la base de verdad" : $"INVENTADOS ({casosInventados} casos)",
            carpetaDeDatos));

    /// <summary>Anota una navegacion con su cifra en milisegundos.</summary>
    public void AnotarNavegacion(string pantalla, double milisegundos)
        => Anotar(string.Format(
            CultureInfo.InvariantCulture,
            "NAVEGACION  {0}  {1:F0} ms ({2:F3} s)",
            pantalla, milisegundos, milisegundos / 1000.0));
}
