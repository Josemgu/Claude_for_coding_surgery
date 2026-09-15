using System.Diagnostics;
using System.Globalization;

namespace Fichas.App.Correccion;

/// <summary>
/// La búsqueda de bandas en el documento, con una sola lectura en vuelo: la última
/// petición gana, la que quedó en espera se cancela si se abre otro caso, y lo leído para un
/// caso que ya no está delante se descarta y se dice.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Medido en el cuaderno del dueño el 2026-09-15</b> (su máquina, sesión
/// 11:03–11:29): 54 aperturas, 25 lecturas de bandas terminadas y <b>29 sin fin
/// registrado</b>; el OCR de bandas tardaba 11–18 s suelto y <b>27–58 s con varios en vuelo,
/// hasta 5 a la vez</b>. Cada apertura lanzaba su OCR sin cancelar el anterior, y el tiempo
/// de abrir un caso subió de 241 ms a 2 178 ms a lo largo de la sesión. Eso es «está lento,
/// consume más», con sus palabras.</para>
///
/// <para><b>Lo que hace:</b> una lectura a la vez, por un semáforo de uno. Mientras una lee,
/// las peticiones nuevas esperan su turno; al llegarles, si su caso ya no es el vigente no
/// arrancan —«canceladas antes de leerlas»—. Y lo que una lectura devuelve para un caso que
/// dejó de ser el vigente mientras leía se descarta —«descartadas»—. Cada cosa se anota.</para>
///
/// <para>⛔ <b>Lo que NO puede hacer:</b> abortar un OCR a medias. <c>RapidOcrNet</c> no
/// tiene cancelación, y eso es <c>Fichas.Lectura</c>; aquí lo que se garantiza es que nunca
/// hay más de UNA lectura en vuelo y que ninguna vieja se pinta. El OCR de una hoja son
/// 2,5 s medidos aquí con una hoja sintética, y 11–18 s en la máquina del dueño.</para>
///
/// <para>El lector se inyecta como una función que devuelve una tarea, para poder probar la
/// política con un lector de mentira que termina cuando la prueba quiere. La página le pasa
/// <c>Task.Run(() => modelo.LeerLasBandas(peticion))</c>.</para>
/// </remarks>
public sealed class BusquedaDeBandas
{
    /// <summary>Quien lee de verdad; devuelve una tarea porque corre en otro hilo.</summary>
    private readonly Func<PeticionDeBandas, Task<BandasLeidas>> _leer;
    /// <summary>Donde se anota cada cancelación, cada descarte y cada fallo.</summary>
    private readonly Action<string> _anotar;
    /// <summary>El semáforo de uno con el que se lee: solo una lectura en vuelo entre todos los que lo compartan.</summary>
    private readonly SemaphoreSlim _turnoDeLectura;

    /// <summary>
    /// El semáforo de TODO el programa: una sola lectura de bandas en vuelo aunque haya varias
    /// instancias de la pantalla.
    /// </summary>
    /// <remarks>
    /// Es uno para el proceso a propósito. Cada navegación a Corrección crea una instancia
    /// nueva de la página, y el dueño entra y sale muchas veces por sesión: con un semáforo por
    /// instancia, cada entrada podría dejar su OCR en vuelo y volverían los cinco a la vez. El
    /// OCR es uno (<c>RapidOcrNet</c>, compartido por <c>LecturaDePdf</c>), así que el turno
    /// también. Las pruebas NO lo usan: cada una construye el suyo, para no esperarse entre sí.
    /// </remarks>
    public static SemaphoreSlim TurnoDeLecturaDelPrograma { get; } = new(1, 1);
    /// <summary>La generación del caso que está delante; cada apertura pide una nueva.</summary>
    private readonly TurnoDePintado _generacion = new();

    /// <summary>Se ata al lector, al cuaderno y al semáforo con el que se lee.</summary>
    /// <param name="leer">Quien lee las bandas de una petición, en otro hilo.</param>
    /// <param name="anotar">Quien escribe una línea en el cuaderno.</param>
    /// <param name="turnoDeLectura">El semáforo de uno; el programa pasa <see cref="TurnoDeLecturaDelPrograma"/>, una prueba el suyo.</param>
    public BusquedaDeBandas(Func<PeticionDeBandas, Task<BandasLeidas>> leer, Action<string> anotar, SemaphoreSlim turnoDeLectura)
    {
        ArgumentNullException.ThrowIfNull(leer);
        ArgumentNullException.ThrowIfNull(anotar);
        ArgumentNullException.ThrowIfNull(turnoDeLectura);
        _leer = leer;
        _anotar = anotar;
        _turnoDeLectura = turnoDeLectura;
    }

    /// <summary>Cuántas lecturas hay en vuelo: 0 o 1, nunca más.</summary>
    public int EnVuelo => 1 - _turnoDeLectura.CurrentCount;

    /// <summary>Se abrió otro caso: pide su generación, que deja viejas a las anteriores.</summary>
    /// <returns>La generación del caso nuevo, para pasársela a <see cref="Buscar"/>.</returns>
    public long AbrirOtroCaso() => _generacion.Pedir();

    /// <summary>
    /// Busca las bandas cuando le toque el turno; nulo si se canceló antes de leer o si lo
    /// leído ya no es del caso que está delante.
    /// </summary>
    /// <param name="generacion">La generación del caso para el que se piden, la que dio <see cref="AbrirOtroCaso"/>.</param>
    /// <param name="peticion">Qué archivo y qué campos.</param>
    /// <returns>Las bandas si siguen valiendo; nulo si no, y el motivo queda en el cuaderno.</returns>
    public async Task<BandasLeidas?> Buscar(long generacion, PeticionDeBandas peticion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        await _turnoDeLectura.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_generacion.SigueVigente(generacion))
            {
                _anotar($"VISOR  bandas del caso de la generación {generacion} canceladas antes de leerlas: ya está delante la {_generacion.Vigente}");
                return null;
            }

            return await LeerYDecidir(generacion, peticion).ConfigureAwait(false);
        }
        finally
        {
            _turnoDeLectura.Release();
        }
    }

    /// <summary>Lee y, al volver, decide si lo leído todavía es de quien está delante.</summary>
    /// <remarks>
    /// El <c>catch</c> de <see cref="Exception"/> a secas es el mismo de <c>MotorDeImportacion</c>
    /// y por lo mismo: por debajo están PDFium y onnxruntime. ⛔ No calla: el fallo va entero
    /// al cuaderno y la búsqueda queda libre para la siguiente.
    /// </remarks>
    /// <param name="generacion">La generación de la petición.</param>
    /// <param name="peticion">Qué archivo y qué campos.</param>
    private async Task<BandasLeidas?> LeerYDecidir(long generacion, PeticionDeBandas peticion)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var leidas = await _leer(peticion).ConfigureAwait(false);
            cronometro.Stop();

            if (_generacion.SigueVigente(generacion)) return leidas;

            _anotar(string.Format(
                CultureInfo.InvariantCulture,
                "VISOR  bandas de la generación {0} descartadas tras {1:F0} ms: ya está delante la {2}",
                generacion, cronometro.Elapsed.TotalMilliseconds, _generacion.Vigente));
            return null;
        }
        catch (Exception fallo)
        {
            _anotar($"VISOR  buscando las bandas de la generación {generacion}: {fallo.GetType().Name}: {fallo.Message}");
            return null;
        }
    }
}
