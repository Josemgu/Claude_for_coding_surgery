using System.Diagnostics;
using System.Globalization;
using Fichas.Contratos.Lectura;
using Microsoft.UI.Dispatching;

namespace Fichas.App.Correccion;

/// <summary>
/// La mitad de la pantalla de correccion que trae el PAPEL: la hoja al visor y las bandas
/// a los campos, siempre del caso que esta delante y nunca de otro.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Existe por el defecto del 2026-09-15</b>, con las palabras del dueño: <i>«el
/// documento está pegado a TODOS los casos que tengo, y se friza y se cierra solo»</i>. Medido
/// en su cuaderno (su máquina, sesión 11:03–11:29) y reproducido aquí con PDF sintéticos:</para>
/// <list type="number">
/// <item><b>Dos aperturas por entrada.</b> Al llegar, la pantalla abría el primer documento
/// del primer grupo sin que nadie lo pidiera, y 250 ms después el que sí se pidió (54 veces en
/// una sesión; en la reproducción propia, «abre el caso 4» y 100 ms después «abre el caso 1»).</item>
/// <item><b>Sin orden ni turno.</b> Cada apertura rasterizaba su hoja en el hilo de la ventana
/// —1,9-2,3 s congelados por apertura con un PDF de 44 MiB— y lanzaba su OCR de bandas en otro
/// hilo sin cancelar el anterior; el visor componía la imagen con un <c>async</c> suelto. La
/// imagen del caso que nadie pidió podía llegar la última y quedarse pintada sobre los campos
/// del elegido: ese es «el documento que se superpone».</item>
/// <item><b>Sin límite.</b> Hasta 5 OCR a la vez, de 27 a 58 s cada uno, 29 sin fin registrado
/// de 54; el tiempo de abrir subió de 241 a 2 178 ms en la sesión.</item>
/// </list>
///
/// <para><b>Lo que hace ahora:</b> cada apertura pide un <see cref="TurnoDePintado"/>; la hoja
/// se rasteriza en otro hilo y solo se pinta si su turno sigue vigente y sigue siendo la hoja
/// pedida; las bandas pasan por <see cref="BusquedaDeBandas"/>, que no deja más de una lectura
/// en vuelo y tira lo viejo; y al llegar, el primer documento se abre después y solo si nadie
/// pidió otro. Todo lo que se tira se anota.</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    /// <summary>El turno de lo que está delante; lo pide cada apertura y lo comprueba todo lo que vuelve de otro hilo.</summary>
    private readonly TurnoDePintado _turno = new();

    /// <summary>La búsqueda de bandas con una sola lectura en vuelo; nula hasta que llegan los servicios.</summary>
    private BusquedaDeBandas? _busquedaDeBandas;

    /// <summary>Cierto mientras corre <see cref="AlLlegar"/>: entonces el primer documento se abre en diferido.</summary>
    private bool _llegando;

    /// <summary>La última hoja que se pidió al visor; una rasterizada de otra hoja que llegue después no se pinta.</summary>
    private int _hojaPedida;

    /// <summary>Las últimas hojas rasterizadas del caso de delante y cuántas tiene; nula hasta que llegan los servicios.</summary>
    /// <remarks>Se vacía en cada apertura (<see cref="AbrirElCaso"/>) y al irse de la pantalla; la regla está en <see cref="HojasDelDocumento"/>.</remarks>
    private HojasDelDocumento? _hojas;

    /// <summary>El turno vigente, para las hojas que se piden sin cambiar de caso (foco, flechas).</summary>
    private long TurnoVigente => _turno.Vigente;

    /// <summary>Pide un turno nuevo para la apertura que empieza; lo que vuelva con el anterior se tira.</summary>
    private long EmpezarOtroTurno()
    {
        _busquedaDeBandas?.AbrirOtroCaso();
        return _turno.Pedir();
    }

    /// <summary>
    /// Al irse de la pantalla, todo lo que vuelva de otro hilo para ella se tira: la hoja que
    /// se estaba rasterizando, la lectura de bandas en vuelo y la que esperaba turno.
    /// </summary>
    /// <remarks>
    /// Cada navegación crea otra instancia de esta página; sin esto, la instancia abandonada
    /// seguía leyendo bandas para un modelo que ya nadie mira, y con el turno de lectura
    /// compartido eso retrasaba a la instancia nueva.
    /// </remarks>
    /// <param name="cuando">Los datos de la navegación; no se usan.</param>
    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs cuando)
    {
        base.OnNavigatedFrom(cuando);
        EmpezarOtroTurno();
        // Las hojas guardadas son de esta instancia, que ya nadie va a mirar: se sueltan
        // ahora y no cuando el GC quiera.
        _hojas?.Abrir(null);
    }

    /// <summary>Monta la búsqueda de bandas y la memoria corta de hojas sobre los servicios y el cuaderno.</summary>
    /// <param name="servicios">Los servicios de la ventana.</param>
    private void PrepararElPapel(Cascara.Servicios servicios)
    {
        _busquedaDeBandas = new BusquedaDeBandas(
            peticion => Task.Run(() => _modelo!.LeerLasBandas(peticion)),
            servicios.Registro.Anotar,
            BusquedaDeBandas.TurnoDeLecturaDelPrograma);
        _hojas = new HojasDelDocumento(servicios.LecturaDePdf, AnchoDeLaHoja);
        _visor.HojaPintada += AlPintarUnaHoja;
    }

    /// <summary>
    /// Abre ese documento después y solo si para entonces nadie ha abierto otro.
    /// </summary>
    /// <remarks>
    /// <para>Quien llega desde el flujo de trabajo o desde el grupo del día encola SU documento
    /// con prioridad normal justo después de navegar. Esto se encola con prioridad BAJA, así
    /// que corre detrás: si aquel ya abrió el suyo, esto no hace nada; si se llegó por el menú
    /// y nadie pidió ninguno, esto abre el primero, como siempre.</para>
    ///
    /// <para>⛔ Es el arreglo de la doble apertura sin tocar a quien llama —Flujo y Grupo no son
    /// terreno de este pase—: la pantalla deja de suponer que llegar es abrir.</para>
    /// </remarks>
    /// <param name="casoId">El primer documento del grupo elegido.</param>
    private void AbrirSiNadiePideOtro(long casoId)
        => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_casoAbierto != 0 || _modelo is null) return;
            AbrirElCaso(casoId);
        });

    /// <summary>
    /// Enseña una hoja: de la memoria corta si ya se vio, y si no, rasterizada en otro hilo
    /// y dada al visor solo si sigue siendo la que toca.
    /// </summary>
    /// <remarks>
    /// <para>⛔ El <c>try</c> no esta para silenciar: esta para que el fallo salga como una linea
    /// en el pie en vez de tumbar la pantalla. Se atrapa <see cref="Exception"/> a secas por lo
    /// mismo que en <c>MotorDeImportacion</c>: por debajo estan PDFium y PdfPig.</para>
    ///
    /// <para>Lo que vuelve se pinta solo si el turno sigue vigente Y la hoja sigue siendo la
    /// última pedida: cambiar de caso o pulsar dos veces la flecha mientras se rasteriza deja
    /// una imagen vieja en camino, y esa se tira y se anota.</para>
    ///
    /// <para>Desde el plan del 2026-09-15 (R-1): volver a una hoja ya vista no rasteriza nada, y
    /// las hojas de un documento se cuentan una vez y no en cada cambio de hoja. La regla vive
    /// en <see cref="HojasDelDocumento"/>; aquí se mira antes de irse a otro hilo.</para>
    /// </remarks>
    /// <param name="hoja">Que hoja del PDF se pide, base 1; el visor la acota antes de pedirla.</param>
    /// <param name="turno">El turno de la apertura que la pide.</param>
    private void MostrarLaHoja(int hoja, long turno) => _ = MostrarLaHojaSinTumbarNada(hoja, turno);

    /// <summary>La parte que espera de <see cref="MostrarLaHoja"/>; NO es <c>async void</c>: todo lo que pueda fallar esta dentro de su <c>try</c>.</summary>
    /// <param name="hoja">Que hoja del PDF se pide, base 1.</param>
    /// <param name="turno">El turno de la apertura que la pide.</param>
    private async Task MostrarLaHojaSinTumbarNada(int hoja, long turno)
    {
        if (Servicios is null || _modelo?.Caso is null || _hojas is null) return;

        var ruta = _modelo.Caso.RutaPdf;
        if (string.IsNullOrWhiteSpace(ruta))
        {
            _visor.MostrarHoja(null, 0, TextoDelVisor.SinEscaneo);
            return;
        }

        _hojaPedida = hoja;
        if (_hojas.YaRasterizada(hoja) is { } yaVista)
        {
            EnsenarLaHojaTraida(new HojaTraida(yaVista, _hojas.TotalDeHojas ?? 0), hoja, ruta);
            Servicios.Registro.Anotar(string.Format(
                CultureInfo.InvariantCulture,
                "VISOR  hoja {0} del caso {1} servida de la memoria corta, 0 rasterizados nuevos ({2} guardadas, {3} rasterizadas en total)",
                hoja, _casoAbierto, _hojas.Guardadas, _hojas.Rasterizadas));
            return;
        }

        var totalSabido = _hojas.TotalDeHojas;
        var hojas = _hojas;
        var cronometro = Stopwatch.StartNew();
        try
        {
            var traida = await Task.Run(() => hojas.Traer(ruta, hoja, totalSabido)).ConfigureAwait(true);
            cronometro.Stop();

            if (!_turno.SigueVigente(turno) || hoja != _hojaPedida)
            {
                Servicios.Registro.Anotar(string.Format(
                    CultureInfo.InvariantCulture,
                    "VISOR  hoja {0} del turno {1} descartada tras {2:F0} ms: ya está delante el turno {3}, hoja {4}",
                    hoja, turno, cronometro.Elapsed.TotalMilliseconds, _turno.Vigente, _hojaPedida));
                return;
            }

            _hojas.Guardar(ruta, traida);
            EnsenarLaHojaTraida(traida, hoja, ruta);
            Servicios.Registro.Anotar(string.Format(
                CultureInfo.InvariantCulture,
                "VISOR  hoja {0} del caso {1} rasterizada en {2:F0} ms{3}{4} ({5} guardadas, {6} conteos de hojas de este documento)",
                hoja, _casoAbierto, cronometro.Elapsed.TotalMilliseconds,
                traida.Imagen is null ? " (no se pudo abrir)" : string.Empty,
                totalSabido is null ? ", contando sus hojas" : string.Empty,
                _hojas.Guardadas, totalSabido is null ? 1 : 0));
        }
        catch (Exception fallo)
        {
            var linea = TextoDelVisor.LineaDeFallo(hoja, fallo);
            if (_turno.SigueVigente(turno))
                _visor.MostrarHoja(null, 0, linea + " Los campos se corrigen igual, sin la imagen al lado.");
            Decir(linea);
            Servicios.Registro.Anotar($"VISOR  {linea}");
        }
    }

    /// <summary>Da la hoja al visor y le dice al modelo cuál está delante.</summary>
    /// <param name="traida">La hoja (o nula, si no se pudo dibujar) y el total de hojas.</param>
    /// <param name="hoja">Qué hoja se pidió, para el motivo si no hay imagen.</param>
    /// <param name="ruta">La ruta del escaneo, para el motivo si no hay imagen.</param>
    private void EnsenarLaHojaTraida(HojaTraida traida, int hoja, string ruta)
    {
        _visor.MostrarHoja(traida.Imagen, traida.Total, TextoDelVisor.HojaQueNoSePudoAbrir(hoja, ruta));
        DecirleAlModeloQueHojaEstaDelante();
    }

    /// <summary>El visor terminó de componer y pintar una hoja: se anota lo que costó desde que la recibió.</summary>
    /// <remarks>Es la otra mitad de la cifra: «rasterizada en N ms» dice lo que costó traerla; esto, lo que costó pintarla.</remarks>
    /// <param name="quien">El visor.</param>
    /// <param name="medida">Qué hoja y cuántos milisegundos desde que se le dio hasta que quedó pintada.</param>
    private void AlPintarUnaHoja(object? quien, MedidaDelPintado medida)
        => Servicios?.Registro.Anotar(string.Format(
            CultureInfo.InvariantCulture,
            "VISOR  hoja {0} pintada {1:F0} ms después de llegar, {2}",
            medida.Hoja, medida.Milisegundos, medida.Como));

    /// <summary>
    /// Le dice al modelo qué hoja enseña el visor ahora, para que lo que se teclee a partir
    /// de aquí quede anotado como salido de ella.
    /// </summary>
    /// <remarks>
    /// Se lee del visor y no de la hoja pedida: el visor solo cambia de hoja cuando la imagen
    /// llega, y hasta entonces lo que el dueño tiene delante sigue siendo la de antes. Es la
    /// mitad de datos del defecto del 2026-09-15 (<c>ModeloDeCorreccion.Hoja.cs</c>).
    /// </remarks>
    private void DecirleAlModeloQueHojaEstaDelante()
    {
        if (_modelo is not null && _visor.Hoja >= 1) _modelo.HojaDelante = _visor.Hoja;
    }

    /// <summary>
    /// Busca en el documento las bandas que faltan, por turno y sin congelar la ventana.
    /// </summary>
    /// <remarks>
    /// <para>La política —una lectura en vuelo, la última gana, lo viejo se tira— es de
    /// <see cref="BusquedaDeBandas"/>, que se prueba sin ventana. Aquí solo se reparte lo que
    /// vuelve, y solo si el turno sigue vigente: <c>Buscar</c> ya lo comprueba, pero entre su
    /// vuelta y este hilo cabe otra apertura.</para>
    ///
    /// <para>⛔ Y el <c>catch</c> no calla: escribe la linea en el pie y en el cuaderno. Es la
    /// regla que dejo el defecto que QA midio en Importar.</para>
    /// </remarks>
    /// <param name="casoId">El caso para el que se leen, para el cuaderno.</param>
    /// <param name="turno">El turno de la apertura que las pide.</param>
    /// <param name="peticion">Que archivo y que campos, tal como lo planeo el modelo.</param>
    private async Task BuscarLasBandas(long casoId, long turno, PeticionDeBandas peticion)
    {
        if (_busquedaDeBandas is null) return;

        try
        {
            var cronometro = Stopwatch.StartNew();
            var leidas = await _busquedaDeBandas.Buscar(turno, peticion).ConfigureAwait(true);
            cronometro.Stop();

            if (leidas is null || !_turno.SigueVigente(turno) || _modelo is null) return;

            var avisos = _modelo.AplicarLasBandas(leidas);
            foreach (var ficha in _fichas) ficha.Refrescar();
            if (avisos.Count > 0) Servicios?.Avisos.Dejar(avisos);

            Servicios?.Registro.Anotar(string.Format(
                CultureInfo.InvariantCulture,
                "VISOR  bandas del documento del caso {0}: {1} de {2} campos en {3:F0} ms",
                casoId, leidas.PorClave.Count, peticion.Faltantes.Count, cronometro.Elapsed.TotalMilliseconds));
        }
        catch (Exception fallo)
        {
            var linea = TextoDelVisor.LineaDeFallo(_visor.Hoja, fallo);
            Decir(linea);
            Servicios?.Registro.Anotar($"VISOR  buscando las bandas  {linea}");
        }
    }
}
