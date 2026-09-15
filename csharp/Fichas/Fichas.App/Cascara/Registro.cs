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
    /// <remarks>
    /// Si la línea es la con la que Importar cierra una tanda, detrás va la de memoria de
    /// «fin de tanda»: es uno de los cuatro momentos del medidor (R-0, 2026-09-15) y se
    /// reconoce aquí por texto porque Importar es terreno de otro pase; el motivo entero está
    /// en <see cref="MedidorDeMemoria.EsFinDeTanda"/>.
    /// </remarks>
    /// <param name="linea">Lo que se anota; la marca de tiempo la pone el cuaderno.</param>
    public void Anotar(string linea)
    {
        Escribir(linea);
        if (MedidorDeMemoria.EsFinDeTanda(linea)) AnotarMemoria("fin de tanda");
    }

    /// <summary>Escribe una línea con su marca de tiempo, y solo eso.</summary>
    /// <param name="linea">Lo que se escribe.</param>
    private void Escribir(string linea)
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

    /// <summary>
    /// Anota las cuatro cifras de memoria del proceso en ese momento (R-0 del plan del
    /// 2026-09-15): working set, administrada y comprometida por el GC, y privados.
    /// </summary>
    /// <remarks>
    /// Los cuatro momentos que la piden: «ventana lista» (desde <see cref="AnotarArranque"/>),
    /// «fin de tanda» (desde <see cref="Anotar"/>, al reconocer la línea de Importar),
    /// «Corrección abre el caso N» (desde la pantalla) y «cierre» (desde
    /// <see cref="Servicios.Dispose"/>). Solo cifras: ni clave ni datos del dueño.
    /// </remarks>
    /// <param name="momento">Qué estaba pasando, en palabras, sin ningún dato del dueño.</param>
    public void AnotarMemoria(string momento)
        => Escribir(MedidorDeMemoria.Linea(momento, MedidorDeMemoria.Leer()));

    /// <summary>Anota el arranque con su cifra en milisegundos y en segundos, y detrás la memoria de «ventana lista».</summary>
    /// <remarks>
    /// Dice de que datos se abrio, y no es un adorno: si el programa arranco con datos
    /// inventados, cualquier cifra que se mida despues es de mentira, y sin esta linea no
    /// habria forma de saberlo al leer el cuaderno una semana despues.
    /// </remarks>
    /// <param name="milisegundos">Lo que costó hasta que la ventana estuvo lista.</param>
    /// <param name="casosInventados">Cuántos casos inventados se pidieron con <c>--falso</c>, o nulo con la base de verdad.</param>
    /// <param name="carpetaDeDatos">De dónde se abrió la base.</param>
    public void AnotarArranque(double milisegundos, int? casosInventados, string carpetaDeDatos)
    {
        Escribir(string.Format(
            CultureInfo.InvariantCulture,
            "ARRANQUE  ventana lista en {0:F0} ms ({1:F2} s)  datos={2}  carpeta={3}",
            milisegundos,
            milisegundos / 1000.0,
            casosInventados is null ? "la base de verdad" : $"INVENTADOS ({casosInventados} casos)",
            carpetaDeDatos));
        AnotarMemoria("ventana lista");
    }

    /// <summary>Anota una navegacion con su cifra en milisegundos.</summary>
    public void AnotarNavegacion(string pantalla, double milisegundos)
        => Anotar(string.Format(
            CultureInfo.InvariantCulture,
            "NAVEGACION  {0}  {1:F0} ms ({2:F3} s)",
            pantalla, milisegundos, milisegundos / 1000.0));
}
