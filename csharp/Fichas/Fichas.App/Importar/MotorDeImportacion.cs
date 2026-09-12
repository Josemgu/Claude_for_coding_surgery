using System.Diagnostics;
using Fichas.Lectura;

namespace Fichas.App.Importar;

/// <summary>
/// Cientos de PDF de una vez: leer, guardar, contar, y que ninguno tumbe al resto.
/// </summary>
/// <remarks>
/// <para>El dueno lo dijo con un numero: <b>«necesito cargar 500 pdf»</b>. Eso cambia tres
/// cosas respecto a importar uno, y este motor existe por las tres.</para>
///
/// <list type="number">
/// <item><b>Un fallo no puede tumbar la tanda.</b> Con 500 documentos, un archivo corrupto
/// en el puesto 12 tiraria 488 que estaban bien —y de los 11 anteriores nadie sabria si
/// quedaron guardados—. Aqui cada documento se lee dentro de su propio intento, el fallo se
/// convierte en un renglon con su motivo, y la tanda sigue en el siguiente.</item>
///
/// <item><b>Leer y guardar estan separados a proposito.</b> No es simetria: es lo que
/// permite que la lectura —que es lo lento, entre 9 y 13 segundos por hoja medidos— corra
/// en un hilo aparte mientras la ventana sigue respondiendo, y que <b>todo lo que escribe
/// en SQLite se quede en el hilo que abrio la conexion</b>.</item>
///
/// <item><b>Se puede parar a mitad.</b> Lo ya procesado queda guardado y el resumen dice
/// en cuantos se paro; una tanda de 500 que no se pudiera detener seria una ventana
/// secuestrada media hora.</item>
/// </list>
///
/// <para>Este motor NO dibuja nada y no sabe que existe WinUI: informa por
/// <c>alAvanzar</c>, y quien decide como pintarlo es la pantalla.</para>
/// </remarks>
public sealed class MotorDeImportacion
{
    /// <summary>Quien escribe en la base; se le llama siempre desde el hilo que llamó a <see cref="ImportarAsync"/>.</summary>
    private readonly GuardadoDeHojas _guardado;
    /// <summary>Quien lee un PDF entero y devuelve sus hojas; corre en un hilo aparte.</summary>
    private readonly Func<string, IReadOnlyList<HojaLeida>> _leerDocumento;

    /// <summary>Se ata al guardado y a quien sepa leer un PDF entero.</summary>
    /// <param name="guardado">Quien escribe en la base.</param>
    /// <param name="leerDocumento">
    /// Quien lee un PDF y devuelve sus hojas. Se inyecta —en vez de construir el lector
    /// aqui— porque leer los siete PDF de verdad tarda un minuto largo, y una prueba de que
    /// un fallo no tumba la tanda no tiene por que pagar el OCR.
    /// </param>
    public MotorDeImportacion(GuardadoDeHojas guardado, Func<string, IReadOnlyList<HojaLeida>> leerDocumento)
    {
        _guardado = guardado;
        _leerDocumento = leerDocumento;
    }

    /// <summary>
    /// Importa una tanda entera: lee cada PDF en un hilo aparte y guarda en este.
    /// </summary>
    /// <remarks>
    /// ⚠️ El <c>await</c> es lo que mantiene el guardado en el hilo que llamo, que en la
    /// ventana es el de la interfaz y es el que abrio la conexion. Cambiarlo por un
    /// <c>Task.Run</c> que tambien guarde partiria esa regla sin que ninguna prueba se
    /// entere hasta que la base se corrompiera.
    /// </remarks>
    /// <param name="rutas">Los PDF a importar, ya reunidos y sin repetidos.</param>
    /// <param name="alAvanzar">Se llama despues de cada documento, en el hilo que llamo.</param>
    /// <param name="cancelacion">Para poder parar a mitad sin perder lo hecho.</param>
    /// <returns>El resumen de la tanda, con <c>Cancelada</c> en verdadero si se paró antes de acabar.</returns>
    public async Task<ResumenDeLaTanda> ImportarAsync(
        IReadOnlyList<string> rutas,
        Action<ResumenDeLaTanda> alAvanzar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(rutas);
        ArgumentNullException.ThrowIfNull(alAvanzar);

        var resumen = new ResumenDeLaTanda(rutas.Count);
        _guardado.EmpezarUnaTanda();

        foreach (var ruta in rutas)
        {
            if (cancelacion.IsCancellationRequested)
            {
                resumen.Cancelada = true;
                break;
            }

            var crono = Stopwatch.StartNew();
            var lectura = await Task.Run(() => LeerSinQueTumbeLaTanda(ruta), cancelacion).ConfigureAwait(true);
            var resultados = _guardado.GuardarLasHojasDelDocumento(lectura.Hojas);
            crono.Stop();

            resumen.Anotar(CifrasDe(ruta, lectura, resultados, crono.Elapsed.TotalSeconds));
            alAvanzar(resumen);
        }

        return resumen;
    }

    /// <summary>Lo que sale de leer un documento: sus hojas y el fallo si lo hubo.</summary>
    /// <param name="Hojas">Las hojas leídas; con fallo, una sola hoja de mentira que deja el renglón.</param>
    /// <param name="Error">El tipo y el texto del fallo, o nulo si se leyó.</param>
    private readonly record struct LecturaDeUnDocumento(IReadOnlyList<HojaLeida> Hojas, string? Error);

    /// <summary>
    /// Lee un PDF entero y NUNCA levanta: el fallo viaja como dato.
    /// </summary>
    /// <remarks>
    /// Se atrapa <see cref="Exception"/> a secas y no una lista de tipos concretos, que es
    /// lo contrario de lo que este proyecto hace en todos los demas sitios. El motivo:
    /// aqui pasan por debajo PDFium, PdfPig y onnxruntime, tres bibliotecas que levantan
    /// cada una lo suyo. Una lista de tipos seria la lista de los fallos que se me
    /// ocurrieron, y el primero que no este tumba la tanda entera.
    ///
    /// <para>⚠️ Y <b>no se silencia</b>: el error se devuelve entero, con su tipo y su
    /// texto, y acaba escrito en la tabla. Atrapar para callar es lo que esta prohibido;
    /// atrapar para convertirlo en un renglon que Miguel puede leer es lo contrario.</para>
    /// </remarks>
    /// <param name="ruta">El PDF, con su ruta completa.</param>
    /// <returns>Las hojas leídas y error nulo; o una sola hoja de mentira y el error con su tipo y su texto.</returns>
    private LecturaDeUnDocumento LeerSinQueTumbeLaTanda(string ruta)
    {
        try
        {
            var hojas = _leerDocumento(ruta);
            return new LecturaDeUnDocumento(hojas, null);
        }
        catch (Exception causa)
        {
            var error = $"{causa.GetType().Name}: {causa.Message}";
            return new LecturaDeUnDocumento([HojaQueNoSePudoAbrir(ruta, error)], error);
        }
    }

    /// <summary>La hoja de mentira que representa un archivo que no se pudo abrir.</summary>
    /// <remarks>
    /// Se devuelve UNA hoja y no la lista vacia: una lista vacia se pierde en silencio, y
    /// ese renglon es la unica prueba de que ese archivo se intento. Sin el, un PDF
    /// corrupto desaparece de la tanda sin dejar rastro.
    /// </remarks>
    /// <param name="ruta">El PDF que no se pudo abrir.</param>
    /// <param name="error">El tipo y el texto del fallo, para el detalle del aviso.</param>
    private static HojaLeida HojaQueNoSePudoAbrir(string ruta, string error) => new(
        RutaPdf: ruta,
        Pagina: 0,
        Campos: [],
        Avisos: [Contratos.Modelos.Aviso.Problema(
            $"«{Path.GetFileName(ruta)}» no se pudo leer: entró en la lista de lo que no entró.",
            string.Empty,
            $"Archivo: {ruta}. Motivo: {error}")],
        Ilegible: null,
        CapturaManual: true,
        LineasLeidas: 0,
        TextoLeido: null,
        Segundos: 0.0);

    /// <summary>Las cifras de un documento ya guardado.</summary>
    /// <remarks>
    /// Los CASOS se cuentan por identificadores distintos y no por hojas: seis hojas de un
    /// formulario de grupo se unen en un caso, y contarlas como seis diria «6 casos de 6
    /// páginas», que es justo la frase que tiene que delatar cuando algo se pierde.
    /// </remarks>
    /// <param name="ruta">El PDF del que salen las cifras.</param>
    /// <param name="lectura">Lo que devolvió la lectura; con error, las hojas se cuentan como cero.</param>
    /// <param name="resultados">Lo que devolvió el guardado, una entrada por hoja.</param>
    /// <param name="segundos">Lo que tardó leer y guardar el documento.</param>
    private static ResultadoDeUnDocumento CifrasDe(
        string ruta, LecturaDeUnDocumento lectura, IReadOnlyList<ResultadoDeLaHoja> resultados, double segundos)
    {
        var entraron = resultados.Where(hoja => hoja.Entro).ToArray();
        var casoIds = entraron
            .Where(hoja => hoja.CasoId is not null)
            .Select(hoja => hoja.CasoId!.Value)
            .Distinct()
            .ToArray();

        return new ResultadoDeUnDocumento(
            RutaPdf: ruta,
            Hojas: lectura.Error is null ? lectura.Hojas.Count : 0,
            Casos: entraron.Select(hoja => hoja.CasoId).Distinct().Count(),
            Personas: entraron.Sum(hoja => hoja.Personas),
            Pendientes: entraron.Count(hoja => hoja.PendienteDeIdentificar),
            Duplicados: entraron.Count(hoja => hoja.DuplicadoDe is not null),
            Ilegibles: resultados.Count(hoja => !hoja.Entro),
            Error: lectura.Error,
            Segundos: segundos)
        {
            CasoIds = casoIds,
        };
    }
}
