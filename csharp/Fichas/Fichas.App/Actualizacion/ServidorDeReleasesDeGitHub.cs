using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fichas.App.Actualizacion;

/// <summary>
/// GitHub como <see cref="IServidorDeReleases"/>: el último Release del repositorio del
/// proyecto y sus activos, por la API oficial.
/// </summary>
/// <remarks>
/// <para><b>Lo medido el 2026-09-11</b> (supervisor y programador, con <c>gh api</c>): el
/// repositorio es privado; sin cabecera <c>Authorization: Bearer</c> GitHub contesta 404, no
/// 401. Los activos se bajan por su <c>url</c> de API con <c>Accept: application/octet-stream</c>
/// —<c>browser_download_url</c> no vale sin sesión—. Cada activo trae <c>size</c> y
/// <c>digest</c> («sha256:…»).</para>
///
/// <para><c>User-Agent</c> es obligatorio: sin él la API contesta 403 a cualquiera
/// (documentación de la API REST de GitHub, «User agent required»).</para>
///
/// <para>⛔ La clave entra por parámetro, va a la cabecera y no se guarda en ningún campo.
/// Los motivos que se devuelven se limpian por si algún texto de fallo la trajera dentro.</para>
/// </remarks>
public sealed class ServidorDeReleasesDeGitHub : IServidorDeReleases
{
    /// <summary>La dirección del último Release, fijada por la decisión del 2026-09-11.</summary>
    public const string DireccionDelUltimoRelease =
        "https://api.github.com/repos/Josemgu/Claude_for_coding_surgery/releases/latest";

    /// <summary>Cómo se presenta el programa a GitHub; la API exige un User-Agent.</summary>
    private const string QuienPregunta = "Fichas";

    /// <summary>Con qué empieza el <c>digest</c> de un activo en la API: «sha256:» y la huella en hexadecimal.</summary>
    private const string PrefijoDeLaHuella = "sha256:";

    /// <summary>El cliente HTTP; sin tiempo máximo propio: cada llamada trae el suyo en su cancelación.</summary>
    private readonly HttpClient _http;

    /// <summary>Monta el cliente sobre el manejador que se le dé: el de verdad en el programa, uno de mentirijilla en las pruebas.</summary>
    /// <param name="manejador">De dónde salen las respuestas; el cliente lo libera al liberarse.</param>
    public ServidorDeReleasesDeGitHub(HttpMessageHandler manejador)
    {
        _http = new HttpClient(manejador, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(QuienPregunta);
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    /// <inheritdoc />
    public async Task<RespuestaDelServidor> ConsultarElUltimoRelease(string? clave, CancellationToken cancelacion)
    {
        try
        {
            using var peticion = Preparar(DireccionDelUltimoRelease, "application/vnd.github+json", clave);
            using var respuesta = await _http.SendAsync(peticion, HttpCompletionOption.ResponseContentRead, cancelacion);

            if (!respuesta.IsSuccessStatusCode) return SegunElCodigo(respuesta.StatusCode);

            var cuerpo = await respuesta.Content.ReadAsByteArrayAsync(cancelacion);
            return new RespuestaDelServidor(EstadoDeLaRespuesta.Encontrado, LeerElRelease(cuerpo));
        }
        catch (OperationCanceledException)
        {
            return new RespuestaDelServidor(EstadoDeLaRespuesta.SinRed, Motivo: "se agotó el tiempo de espera");
        }
        catch (HttpRequestException fallo)
        {
            return new RespuestaDelServidor(EstadoDeLaRespuesta.SinRed, Motivo: Limpio(fallo.Message, clave));
        }
        catch (JsonException fallo)
        {
            return new RespuestaDelServidor(EstadoDeLaRespuesta.OtroFallo, Motivo: "la respuesta no se entendió: " + Limpio(fallo.Message, clave));
        }
    }

    /// <inheritdoc />
    public async Task<RespuestaDelServidor> DescargarActivo(ActivoDelRelease activo, string rutaDestino, string? clave, CancellationToken cancelacion)
    {
        try
        {
            using var peticion = Preparar(activo.Url, "application/octet-stream", clave);
            using var respuesta = await _http.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, cancelacion);

            if (!respuesta.IsSuccessStatusCode) return SegunElCodigo(respuesta.StatusCode);

            await using var desdeLaRed = await respuesta.Content.ReadAsStreamAsync(cancelacion);
            await using var alDisco = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
            await desdeLaRed.CopyToAsync(alDisco, cancelacion);
            return new RespuestaDelServidor(EstadoDeLaRespuesta.Encontrado);
        }
        catch (OperationCanceledException)
        {
            return new RespuestaDelServidor(EstadoDeLaRespuesta.SinRed, Motivo: "se agotó el tiempo de la descarga");
        }
        catch (Exception fallo) when (fallo is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return new RespuestaDelServidor(EstadoDeLaRespuesta.SinRed, Motivo: Limpio(fallo.Message, clave));
        }
    }

    /// <summary>Una petición GET con el <c>Accept</c> pedido y, si hay clave, la cabecera <c>Authorization: Bearer</c>.</summary>
    /// <param name="direccion">La dirección completa.</param>
    /// <param name="acepta">El <c>Accept</c>: JSON para el Release, binario para un activo.</param>
    /// <param name="clave">La clave, o nula para no mandar cabecera.</param>
    private static HttpRequestMessage Preparar(string direccion, string acepta, string? clave)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, direccion);
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acepta));
        if (clave is not null) peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clave);
        return peticion;
    }

    /// <summary>Traduce un código que no es de éxito al estado que la búsqueda entiende.</summary>
    /// <param name="codigo">El código HTTP recibido.</param>
    private static RespuestaDelServidor SegunElCodigo(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.NotFound => new RespuestaDelServidor(EstadoDeLaRespuesta.NoExiste, Motivo: "GitHub contestó 404"),
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            => new RespuestaDelServidor(EstadoDeLaRespuesta.SinPermiso, Motivo: $"GitHub contestó {(int)codigo}"),
        _ => new RespuestaDelServidor(EstadoDeLaRespuesta.OtroFallo, Motivo: $"GitHub contestó {(int)codigo}"),
    };

    /// <summary>Lee del JSON del Release lo que hace falta: la etiqueta y, por activo, nombre, url, tamaño y huella.</summary>
    /// <param name="cuerpo">El JSON tal como llegó.</param>
    /// <exception cref="JsonException">Si el cuerpo no es JSON o no tiene la forma de un Release.</exception>
    private static ReleasePublicado LeerElRelease(byte[] cuerpo)
    {
        using var json = JsonDocument.Parse(cuerpo);
        var raiz = json.RootElement;
        var etiqueta = raiz.TryGetProperty("tag_name", out var tag) ? tag.GetString() ?? string.Empty : string.Empty;

        var activos = new List<ActivoDelRelease>();
        if (raiz.TryGetProperty("assets", out var lista) && lista.ValueKind == JsonValueKind.Array)
        {
            foreach (var activo in lista.EnumerateArray()) activos.Add(LeerElActivo(activo));
        }

        return new ReleasePublicado(etiqueta, activos);
    }

    /// <summary>Un activo del JSON; la huella se guarda sin el prefijo «sha256:» y en minúscula.</summary>
    /// <param name="activo">El objeto JSON del activo.</param>
    private static ActivoDelRelease LeerElActivo(JsonElement activo)
    {
        var nombre = activo.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var url = activo.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        var tamano = activo.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : -1;
        var huella = activo.TryGetProperty("digest", out var d) ? d.GetString() : null;

        if (huella is not null && huella.StartsWith(PrefijoDeLaHuella, StringComparison.OrdinalIgnoreCase))
            huella = huella[PrefijoDeLaHuella.Length..].Trim().ToLowerInvariant();
        else
            huella = null;

        return new ActivoDelRelease(nombre, url, tamano, huella);
    }

    /// <summary>Quita la clave de un texto de fallo, por si algún mensaje la trajera dentro.</summary>
    /// <param name="texto">El mensaje del fallo.</param>
    /// <param name="clave">La clave que no puede salir de aquí, o nula.</param>
    private static string Limpio(string texto, string? clave)
        => string.IsNullOrEmpty(clave) ? texto : texto.Replace(clave, "[clave]", StringComparison.Ordinal);
}
