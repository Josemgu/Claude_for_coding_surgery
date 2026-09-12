using System.Security.Cryptography;
using Fichas.App.Cascara;

namespace Fichas.App.Actualizacion;

/// <summary>
/// El control de versiones que pidió el dueño: pregunta si hay una versión nueva, baja el
/// instalador, lo comprueba y lo lanza. Cada paso es un método y ninguno se llama solo.
/// </summary>
/// <remarks>
/// <para>Sus palabras, 2026-09-11: «Haz un control de versiones que cuando llegue una nueva
/// versión se actualice todo.» Y la decisión que lo diseña, en DECISIONES.md del mismo día.</para>
///
/// <para><b>Lo que hace y lo que no:</b> <see cref="BuscarAlArrancar"/> se calla con
/// <c>--falso</c> y con <c>--sin-actualizacion</c>; <see cref="Buscar"/> pregunta siempre;
/// <see cref="DescargarElInstalador"/> baja y comprueba tamaño y huella;
/// <see cref="LanzarElInstalador"/> solo lanza lo que quedó listo. <b>Nunca instala sin el
/// clic</b>: la ventana llama a los dos últimos solo desde «Actualizar ahora».</para>
///
/// <para>⛔ La clave se lee en el momento de cada petición y va a la cabecera. No se guarda
/// en ningún campo, no se anota en el cuaderno y no viaja en ningún resultado.</para>
/// </remarks>
public sealed class Actualizador
{
    /// <summary>Cuánto se espera a GitHub antes de rendirse: corto, para no tener a nadie mirando.</summary>
    public static readonly TimeSpan TiempoMaximoDeConsultaPorDefecto = TimeSpan.FromSeconds(10);

    /// <summary>Cuánto puede tardar la descarga del instalador (unos 100 MB medidos en la v11).</summary>
    public static readonly TimeSpan TiempoMaximoDeDescarga = TimeSpan.FromMinutes(10);

    /// <summary>Con qué empieza y acaba el nombre del instalador en cada Release: <c>Instalar-Fichas-v11.exe</c>.</summary>
    private const string PrefijoDelInstalador = "Instalar-Fichas-";

    /// <summary>La puerta a GitHub, o al doble de las pruebas.</summary>
    private readonly IServidorDeReleases _servidor;

    /// <summary>La carpeta de datos, donde se busca la clave.</summary>
    private readonly string _carpetaDeDatos;

    /// <summary>La versión abierta: «11».</summary>
    private readonly string _versionActual;

    /// <summary>Quien arranca el instalador.</summary>
    private readonly ILanzadorDelInstalador _lanzador;

    /// <summary>Dónde anotar lo que pasó; nunca la clave.</summary>
    private readonly Action<string> _anotar;

    /// <summary>Dónde se baja el instalador: la temporal de Windows en el programa, una propia en las pruebas.</summary>
    private readonly string _carpetaTemporal;

    /// <summary>Cuánto se espera a la consulta.</summary>
    private readonly TimeSpan _tiempoMaximoDeConsulta;

    /// <summary>Monta el actualizador con todas sus piezas.</summary>
    /// <param name="servidor">La puerta a GitHub.</param>
    /// <param name="carpetaDeDatos">La carpeta de datos donde el dueño pega la clave.</param>
    /// <param name="versionActual">Lo que dice <c>VersionDelPrograma.Numero</c>.</param>
    /// <param name="lanzador">Quien arranca el instalador.</param>
    /// <param name="anotar">El cuaderno de tiempos, línea a línea.</param>
    /// <param name="carpetaTemporal">Dónde bajar el instalador; nula para la temporal de Windows.</param>
    /// <param name="tiempoMaximoDeConsulta">Nulo para los 10 segundos por defecto.</param>
    public Actualizador(
        IServidorDeReleases servidor,
        string carpetaDeDatos,
        string versionActual,
        ILanzadorDelInstalador lanzador,
        Action<string> anotar,
        string? carpetaTemporal = null,
        TimeSpan? tiempoMaximoDeConsulta = null)
    {
        _servidor = servidor;
        _carpetaDeDatos = carpetaDeDatos;
        _versionActual = versionActual;
        _lanzador = lanzador;
        _anotar = anotar;
        _carpetaTemporal = carpetaTemporal ?? Path.GetTempPath();
        _tiempoMaximoDeConsulta = tiempoMaximoDeConsulta ?? TiempoMaximoDeConsultaPorDefecto;
    }

    /// <summary>La versión abierta, para que la franja diga «Tienes la última versión, v11».</summary>
    public string VersionActual => _versionActual;

    /// <summary>Dónde va la clave, para decirlo en la franja cuando falta.</summary>
    public string RutaDeLaClave => ClaveDeActualizacion.RutaEn(_carpetaDeDatos);

    /// <summary>La búsqueda del arranque: se calla con <c>--falso</c> y con <c>--sin-actualizacion</c>, y entonces no sale a internet.</summary>
    /// <param name="argumentos">Lo que se pidió en la línea de órdenes.</param>
    public Task<ResultadoDeLaBusqueda> BuscarAlArrancar(ArgumentosDeArranque argumentos)
        => argumentos.SeBuscaActualizacionAlArrancar
            ? Buscar()
            : Task.FromResult(new ResultadoDeLaBusqueda(QueSeEncontro.NoSeBusco));

    /// <summary>Pregunta a GitHub por el último Release y lo compara con la versión abierta. Nunca lanza.</summary>
    public async Task<ResultadoDeLaBusqueda> Buscar()
    {
        var clave = ClaveDeActualizacion.Leer(_carpetaDeDatos);
        using var tiempo = new CancellationTokenSource(_tiempoMaximoDeConsulta);
        var respuesta = await _servidor.ConsultarElUltimoRelease(clave, tiempo.Token);

        var resultado = Interpretar(respuesta, hayClave: clave is not null);
        _anotar($"ACTUALIZACION  {ComoSeAnota(resultado)}");
        return resultado;
    }

    /// <summary>Baja el instalador del hallazgo a la carpeta temporal y comprueba tamaño y huella. Nunca lanza.</summary>
    /// <param name="hallazgo">Lo que devolvió <see cref="Buscar"/>; si no es una versión nueva, no se baja nada.</param>
    public async Task<ResultadoDeLaDescarga> DescargarElInstalador(ResultadoDeLaBusqueda hallazgo)
    {
        if (hallazgo.Que != QueSeEncontro.HayVersionNueva || hallazgo.Instalador is null)
            return new ResultadoDeLaDescarga(false, Motivo: "no hay ninguna versión nueva que bajar");

        var activo = hallazgo.Instalador;
        var ruta = Path.Combine(_carpetaTemporal, activo.Nombre);
        var clave = ClaveDeActualizacion.Leer(_carpetaDeDatos);

        using var tiempo = new CancellationTokenSource(TiempoMaximoDeDescarga);
        var respuesta = await _servidor.DescargarActivo(activo, ruta, clave, tiempo.Token);

        var resultado = respuesta.Estado == EstadoDeLaRespuesta.Encontrado
            ? Comprobar(ruta, activo)
            : new ResultadoDeLaDescarga(false, Motivo: $"no se pudo bajar el instalador: {respuesta.Motivo}");

        if (!resultado.Lista) Borrar(ruta);
        _anotar($"ACTUALIZACION  descarga de {activo.Nombre}: {(resultado.Lista ? "lista" : "NO: " + resultado.Motivo)}");
        return resultado;
    }

    /// <summary>Lanza el instalador solo si la descarga quedó lista; con una que no cuadra, no hace nada.</summary>
    /// <param name="descarga">Lo que devolvió <see cref="DescargarElInstalador"/>.</param>
    /// <returns>Verdadero si el instalador arrancó.</returns>
    public bool LanzarElInstalador(ResultadoDeLaDescarga descarga)
    {
        if (!descarga.Lista) return false;

        var lanzado = _lanzador.Lanzar(descarga.Ruta, _carpetaDeDatos);
        _anotar($"ACTUALIZACION  instalador {(lanzado ? "lanzado" : "NO lanzado")}: {descarga.Ruta}");
        return lanzado;
    }

    /// <summary>Traduce la respuesta del servidor a lo que la franja tiene que decir.</summary>
    /// <remarks>
    /// Un 401 o un 403 solo hablan de la clave si se mandó una: sin clave, GitHub contesta
    /// 403 por el límite de peticiones o por faltar el User-Agent, y decir «la clave no vale»
    /// mandaría al dueño a cambiar una clave que no existe. Eso es «no se pudo», con el código.
    /// </remarks>
    /// <param name="respuesta">Lo que contestó el servidor.</param>
    /// <param name="hayClave">Si se mandó clave; decide entre «falta la clave» y «la clave no vale».</param>
    private ResultadoDeLaBusqueda Interpretar(RespuestaDelServidor respuesta, bool hayClave) => respuesta.Estado switch
    {
        EstadoDeLaRespuesta.Encontrado => Comparar(respuesta.Release!),
        EstadoDeLaRespuesta.NoExiste when !hayClave => new ResultadoDeLaBusqueda(QueSeEncontro.FaltaLaClave, RutaDeLaClave: RutaDeLaClave, Motivo: respuesta.Motivo),
        EstadoDeLaRespuesta.SinPermiso when !hayClave => new ResultadoDeLaBusqueda(QueSeEncontro.NoSePudo, Motivo: respuesta.Motivo),
        EstadoDeLaRespuesta.NoExiste or EstadoDeLaRespuesta.SinPermiso => new ResultadoDeLaBusqueda(QueSeEncontro.LaClaveNoVale, RutaDeLaClave: RutaDeLaClave, Motivo: respuesta.Motivo),
        EstadoDeLaRespuesta.SinRed => new ResultadoDeLaBusqueda(QueSeEncontro.SinRed, Motivo: respuesta.Motivo),
        _ => new ResultadoDeLaBusqueda(QueSeEncontro.NoSePudo, Motivo: respuesta.Motivo),
    };

    /// <summary>Compara la etiqueta con la versión abierta y busca el instalador entre los activos.</summary>
    /// <param name="release">El Release leído.</param>
    private ResultadoDeLaBusqueda Comparar(ReleasePublicado release)
    {
        if (!EtiquetaDeVersion.SeEntiende(release.Etiqueta))
            return new ResultadoDeLaBusqueda(QueSeEncontro.NoSePudo, release.Etiqueta, Motivo: $"la etiqueta «{release.Etiqueta}» no es un número de versión");

        if (!EtiquetaDeVersion.EsMasNuevaQue(release.Etiqueta, _versionActual))
            return new ResultadoDeLaBusqueda(QueSeEncontro.EsLaUltima, release.Etiqueta);

        var instalador = release.Activos.FirstOrDefault(a => EsElInstalador(a.Nombre));
        return instalador is null
            ? new ResultadoDeLaBusqueda(QueSeEncontro.NoSePudo, release.Etiqueta, Motivo: $"el Release {release.Etiqueta} no trae instalador")
            : new ResultadoDeLaBusqueda(QueSeEncontro.HayVersionNueva, release.Etiqueta, instalador);
    }

    /// <summary>
    /// Si un nombre de activo es el del instalador: <c>Instalar-Fichas-vN.exe</c>, y solo un
    /// nombre de archivo, sin ruta dentro.
    /// </summary>
    /// <remarks>
    /// El instalador se escribe en la temporal con el nombre que diga el JSON. Un nombre con
    /// barras llevaría el archivo a otra carpeta, así que no se acepta: el nombre tiene que
    /// ser exactamente su propio nombre de archivo.
    /// </remarks>
    /// <param name="nombre">El <c>name</c> del activo.</param>
    private static bool EsElInstalador(string nombre)
        => nombre.StartsWith(PrefijoDelInstalador, StringComparison.Ordinal)
           && nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
           && string.Equals(Path.GetFileName(nombre), nombre, StringComparison.Ordinal)
           && nombre.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Compara el archivo bajado con lo que GitHub declaró: el tamaño siempre; la huella si la dio.</summary>
    /// <remarks>
    /// Sin tamaño declarado no hay nada seguro contra qué comparar, y un instalador sin
    /// comprobar no se lanza: comprobar nada y llamarlo comprobado sería peor que no comprobar.
    /// </remarks>
    /// <param name="ruta">El archivo bajado.</param>
    /// <param name="activo">Lo que GitHub declaró de él.</param>
    private static ResultadoDeLaDescarga Comprobar(string ruta, ActivoDelRelease activo)
    {
        if (activo.Tamano < 0)
            return new ResultadoDeLaDescarga(false, Motivo: "GitHub no declaró el tamaño del instalador y no se puede comprobar");

        long tamano;
        try
        {
            tamano = new FileInfo(ruta).Length;
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            return new ResultadoDeLaDescarga(false, Motivo: "el instalador no quedó en el disco: " + fallo.Message);
        }

        if (tamano != activo.Tamano)
            return new ResultadoDeLaDescarga(false, Motivo: $"el tamaño no cuadra: se esperaban {activo.Tamano} bytes y llegaron {tamano}");

        if (activo.Huella is null)
            return new ResultadoDeLaDescarga(true, ruta, HuellaComprobada: false);

        var huella = HuellaDe(ruta);
        return string.Equals(huella, activo.Huella, StringComparison.OrdinalIgnoreCase)
            ? new ResultadoDeLaDescarga(true, ruta, HuellaComprobada: true)
            : new ResultadoDeLaDescarga(false, Motivo: "la huella SHA-256 no cuadra con la que declara GitHub");
    }

    /// <summary>El SHA-256 de un archivo, en hexadecimal minúscula como lo escribe GitHub.</summary>
    /// <param name="ruta">El archivo.</param>
    private static string HuellaDe(string ruta)
    {
        using var archivo = File.OpenRead(ruta);
        return Convert.ToHexStringLower(SHA256.HashData(archivo));
    }

    /// <summary>Borra un archivo que no cuadró; si no se deja, se anota: no se lanza igualmente y la temporal se vacía sola.</summary>
    /// <param name="ruta">El archivo a borrar.</param>
    private void Borrar(string ruta)
    {
        try
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException)
        {
            _anotar($"ACTUALIZACION  no se pudo borrar {ruta}: {fallo.Message}");
        }
    }

    /// <summary>La línea del cuaderno para cada resultado; nunca la clave.</summary>
    /// <param name="resultado">Lo que se encontró.</param>
    private string ComoSeAnota(ResultadoDeLaBusqueda resultado) => resultado.Que switch
    {
        QueSeEncontro.HayVersionNueva => $"hay versión nueva {resultado.Etiqueta} (abierta v{_versionActual})",
        QueSeEncontro.EsLaUltima => $"la abierta v{_versionActual} es la última ({resultado.Etiqueta})",
        QueSeEncontro.FaltaLaClave => $"falta la clave en {resultado.RutaDeLaClave}",
        QueSeEncontro.LaClaveNoVale => $"la clave no vale ({resultado.Motivo})",
        QueSeEncontro.SinRed => $"sin red: {resultado.Motivo}",
        _ => $"no se pudo: {resultado.Motivo}",
    };
}
