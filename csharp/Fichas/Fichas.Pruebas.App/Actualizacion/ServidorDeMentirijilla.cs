using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// Un <see cref="HttpMessageHandler"/> que hace de GitHub sin salir a internet.
/// </summary>
/// <remarks>
/// <para>Regla del proyecto: sin red en las pruebas. Todo lo que el programa le pediría a
/// <c>api.github.com</c> pasa por aquí, y aquí se guarda cada petición —dirección y
/// cabeceras— para que una prueba pueda afirmar «se mandó la clave» o «no se llamó ni una
/// vez» con la cifra delante.</para>
///
/// <para>El guion se escribe con <c>AlPedir</c>: qué contestar a cada dirección. Lo
/// que no está en el guion contesta 404, que es lo que GitHub contesta cuando no encuentra
/// o cuando no se tiene permiso para ver un repositorio privado.</para>
/// </remarks>
internal sealed class ServidorDeMentirijilla : HttpMessageHandler
{
    /// <summary>Las respuestas preparadas, por dirección exacta.</summary>
    private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _guion = new(StringComparer.Ordinal);

    /// <summary>Cada petición que llegó, en orden, con sus cabeceras copiadas.</summary>
    public List<PeticionRecibida> Peticiones { get; } = [];

    /// <summary>Cuántas veces se le pidió algo, sea lo que sea.</summary>
    public int CuantasVecesSeLlamo => Peticiones.Count;

    /// <summary>Prepara qué contestar a una dirección.</summary>
    /// <param name="direccion">La dirección completa, tal como la pedirá el programa.</param>
    /// <param name="respuesta">Cómo fabricar la respuesta; recibe la petición por si quiere mirarla.</param>
    public void AlPedir(string direccion, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respuesta)
        => _guion[direccion] = respuesta;

    /// <summary>Prepara una respuesta fija, con su código y su cuerpo.</summary>
    /// <param name="direccion">La dirección completa.</param>
    /// <param name="codigo">El código HTTP que se devuelve.</param>
    /// <param name="cuerpo">El cuerpo, como texto (JSON) o como bytes (un activo).</param>
    public void AlPedir(string direccion, HttpStatusCode codigo, byte[] cuerpo)
        => AlPedir(direccion, (_, _) => Task.FromResult(Respuesta(codigo, cuerpo)));

    /// <summary>Prepara una respuesta fija con cuerpo de texto.</summary>
    /// <param name="direccion">La dirección completa.</param>
    /// <param name="codigo">El código HTTP que se devuelve.</param>
    /// <param name="cuerpo">El cuerpo como texto; se manda en UTF-8.</param>
    public void AlPedir(string direccion, HttpStatusCode codigo, string cuerpo)
        => AlPedir(direccion, codigo, Encoding.UTF8.GetBytes(cuerpo));

    /// <summary>Prepara un fallo de red: la petición lanza como si no hubiera conexión.</summary>
    /// <param name="direccion">La dirección completa.</param>
    /// <param name="mensaje">El texto del fallo; una prueba lo llena con la clave para ver que no se filtra.</param>
    public void AlPedirFallaLaRed(string direccion, string mensaje)
        => AlPedir(direccion, (_, _) => throw new HttpRequestException(mensaje));

    /// <summary>Prepara una espera que no acaba: solo termina cuando el programa se cansa y cancela.</summary>
    /// <param name="direccion">La dirección completa.</param>
    public void AlPedirNoContestaNunca(string direccion)
        => AlPedir(direccion, async (_, cancelacion) =>
        {
            await Task.Delay(Timeout.Infinite, cancelacion);
            return Respuesta(HttpStatusCode.OK, []);
        });

    /// <summary>Apunta la petición y contesta lo que diga el guion; 404 si no dice nada.</summary>
    /// <param name="peticion">La petición que hizo el programa.</param>
    /// <param name="cancelacion">La cancelación del programa, que aquí se respeta para poder simular un tiempo agotado.</param>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
    {
        Peticiones.Add(PeticionRecibida.De(peticion));

        var direccion = peticion.RequestUri?.ToString() ?? string.Empty;
        return _guion.TryGetValue(direccion, out var responder)
            ? responder(peticion, cancelacion)
            : Task.FromResult(Respuesta(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"u8.ToArray()));
    }

    /// <summary>Fabrica una respuesta con su código y su cuerpo en bytes.</summary>
    /// <param name="codigo">El código HTTP.</param>
    /// <param name="cuerpo">El cuerpo.</param>
    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, byte[] cuerpo)
    {
        var respuesta = new HttpResponseMessage(codigo) { Content = new ByteArrayContent(cuerpo) };
        respuesta.Content.Headers.ContentLength = cuerpo.Length;
        return respuesta;
    }
}

/// <summary>Lo que se guarda de cada petición: dirección y las dos cabeceras que importan.</summary>
/// <param name="Direccion">La dirección completa pedida.</param>
/// <param name="Autorizacion">La cabecera <c>Authorization</c> tal cual, o nula si no se mandó.</param>
/// <param name="Acepta">Los valores de la cabecera <c>Accept</c>, unidos por coma.</param>
internal sealed record PeticionRecibida(string Direccion, string? Autorizacion, string Acepta)
{
    /// <summary>Copia lo que importa de una petición antes de que se deseche.</summary>
    /// <param name="peticion">La petición recibida.</param>
    public static PeticionRecibida De(HttpRequestMessage peticion)
    {
        AuthenticationHeaderValue? auth = peticion.Headers.Authorization;
        return new PeticionRecibida(
            peticion.RequestUri?.ToString() ?? string.Empty,
            auth is null ? null : $"{auth.Scheme} {auth.Parameter}",
            string.Join(",", peticion.Headers.Accept.Select(a => a.MediaType)));
    }
}
