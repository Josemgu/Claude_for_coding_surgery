using System.Diagnostics;
using System.Globalization;

namespace Fichas.App.Cascara;

/// <summary>Las cuatro cifras de memoria del proceso en un instante, en bytes.</summary>
/// <param name="WorkingSet">Lo que el sistema tiene en memoria física para este proceso (<see cref="Environment.WorkingSet"/>); es la columna del Administrador de tareas.</param>
/// <param name="Administrada">Lo que el GC cree vivo en sus montones (<see cref="GC.GetTotalMemory(bool)"/> sin recoger).</param>
/// <param name="Comprometida">Lo que el GC tiene comprometido, vivo o no (<see cref="GCMemoryInfo.TotalCommittedBytes"/>).</param>
/// <param name="Privados">Los bytes privados del proceso, con lo nativo dentro: PDFium, ONNX Runtime, WinUI.</param>
/// <remarks>
/// Cuatro y no una porque la resta entre ellas es lo que dice DÓNDE está el gigabyte: si los
/// privados suben y lo administrado no, es nativo (el motor de OCR, las hojas de PDFium); si
/// lo comprometido sube y lo administrado no, es el GC guardando sitio.
/// </remarks>
public readonly record struct LecturaDeMemoria(long WorkingSet, long Administrada, long Comprometida, long Privados);

/// <summary>
/// El medidor permanente de memoria: lee las cuatro cifras y las escribe como UNA línea
/// <c>MEMORIA</c> del cuaderno (R-0 del plan del 2026-09-15).
/// </summary>
/// <remarks>
/// <para>Existe porque el «1 GB» del dueño era una captura del Administrador de tareas, sin
/// desglose y sin momento. Con esto, cada sesión suya deja escrito cuánto pesaba el programa
/// con la ventana lista, al terminar cada tanda, al abrir cada documento en Corrección y al
/// cerrar, y la resta entre dos líneas es una medición y no un recuerdo.</para>
///
/// <para>⛔ En la línea van solo cifras y el nombre del momento: ni la clave, ni rutas, ni
/// datos del dueño (regla del cuaderno del 2026-09-11).</para>
/// </remarks>
public static class MedidorDeMemoria
{
    /// <summary>La marca con la que empieza cada línea de memoria, para poder buscarlas.</summary>
    public const string Marca = "MEMORIA";

    /// <summary>Cómo empieza la línea con la que Importar cierra una tanda que terminó.</summary>
    private const string TandaTerminada = "IMPORTACIÓN  Importados ";

    /// <summary>Cómo empieza la línea con la que Importar cierra una tanda detenida a mano.</summary>
    private const string TandaDetenida = "IMPORTACIÓN  Importación detenida";

    /// <summary>Un mebibyte, para escribir las cifras en la unidad del Administrador de tareas.</summary>
    private const double MiB = 1024.0 * 1024.0;

    /// <summary>Lee las cuatro cifras del proceso ahora mismo, sin forzar ninguna recolección.</summary>
    public static LecturaDeMemoria Leer()
    {
        using var proceso = Process.GetCurrentProcess();
        return new LecturaDeMemoria(
            Environment.WorkingSet,
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetGCMemoryInfo().TotalCommittedBytes,
            proceso.PrivateMemorySize64);
    }

    /// <summary>Compone la línea del cuaderno para ese momento con esa lectura.</summary>
    /// <param name="momento">Qué estaba pasando: «ventana lista», «fin de tanda», «Corrección abre el caso 7», «cierre».</param>
    /// <param name="lectura">Las cuatro cifras.</param>
    /// <returns>«MEMORIA  momento  working set N MiB  administrada N MiB  comprometida N MiB  privados N MiB», con punto decimal pase lo que pase con la cultura.</returns>
    public static string Linea(string momento, LecturaDeMemoria lectura)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0}  {1}  working set {2:F1} MiB  administrada {3:F1} MiB  comprometida {4:F1} MiB  privados {5:F1} MiB",
            Marca, momento,
            lectura.WorkingSet / MiB, lectura.Administrada / MiB, lectura.Comprometida / MiB, lectura.Privados / MiB);

    /// <summary>
    /// Si esa línea del cuaderno es la que Importar escribe al terminar una tanda.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es un gancho por texto, y se declara: la carpeta Importar es terreno de otro pase
    /// (R-4) y no se toca, así que el momento «fin de tanda» se reconoce por la línea que
    /// Importar ya escribe (<c>PaginaDeImportar.EnsenarElResumen</c>, con
    /// <c>ResumenDeLaTanda.Linea()</c> detrás). Cuando Importar quede libre, lo limpio es que
    /// llame a <see cref="Registro.AnotarMemoria"/> ella misma y esto desaparezca; queda anotado
    /// en la entrega para <c>PENDIENTES.md</c>.
    /// </remarks>
    /// <param name="linea">La línea tal como se anota, sin la marca de tiempo.</param>
    public static bool EsFinDeTanda(string linea)
        => linea.StartsWith(TandaTerminada, StringComparison.Ordinal)
           || linea.StartsWith(TandaDetenida, StringComparison.Ordinal);
}
