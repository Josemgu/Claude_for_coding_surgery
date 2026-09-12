namespace Fichas.App.Actualizacion;

/// <summary>
/// Qué salió de preguntar por la última versión.
/// </summary>
/// <remarks>
/// Es la lista cerrada de cosas que la franja puede tener que decir (DECISIONES.md,
/// 2026-09-11, «EL DUEÑO PIDE QUE EL PROGRAMA SE ACTUALICE SOLO»). Ninguna lanza: aquí se
/// devuelve, como en el resto del programa (requisito 9, «avisar, nunca impedir»).
/// </remarks>
public enum QueSeEncontro
{
    /// <summary>No se preguntó: <c>--falso</c> o <c>--sin-actualizacion</c>.</summary>
    NoSeBusco,

    /// <summary>La etiqueta del Release es mayor que la versión abierta y trae instalador.</summary>
    HayVersionNueva,

    /// <summary>La etiqueta es igual o menor: lo que está abierto es lo último.</summary>
    EsLaUltima,

    /// <summary>GitHub contestó 404 y no se mandó clave: el repositorio es privado y falta pegarla.</summary>
    FaltaLaClave,

    /// <summary>Se mandó clave y GitHub la rechazó (401, 403, o 404 con clave).</summary>
    LaClaveNoVale,

    /// <summary>No hubo conexión o se agotó el tiempo de espera.</summary>
    SinRed,

    /// <summary>GitHub contestó algo que no se entiende, o el Release no trae instalador.</summary>
    NoSePudo,
}

/// <summary>Un archivo publicado en el Release, tal como lo describe la API de GitHub.</summary>
/// <param name="Nombre">El nombre del archivo: <c>Instalar-Fichas-v12.exe</c>.</param>
/// <param name="Url">La dirección de API del activo; con <c>Accept: application/octet-stream</c> da los bytes. No es <c>browser_download_url</c>, que en un repositorio privado no vale sin sesión.</param>
/// <param name="Tamano">Los bytes que declara GitHub.</param>
/// <param name="Huella">El SHA-256 en hexadecimal minúscula, sin el prefijo «sha256:»; nulo si la API no lo dio.</param>
public sealed record ActivoDelRelease(string Nombre, string Url, long Tamano, string? Huella);

/// <summary>Lo que se sabe del último Release: su etiqueta y sus archivos.</summary>
/// <param name="Etiqueta">El <c>tag_name</c>: «v12».</param>
/// <param name="Activos">Los archivos publicados con él.</param>
public sealed record ReleasePublicado(string Etiqueta, IReadOnlyList<ActivoDelRelease> Activos);

/// <summary>El resultado de buscar, con lo que la franja necesita para decirlo.</summary>
/// <param name="Que">Qué se encontró.</param>
/// <param name="Etiqueta">La etiqueta del Release, cuando GitHub contestó; vacía si no.</param>
/// <param name="Instalador">El activo del instalador, solo con <see cref="QueSeEncontro.HayVersionNueva"/>.</param>
/// <param name="RutaDeLaClave">Dónde va la clave, para decirlo con <see cref="QueSeEncontro.FaltaLaClave"/>.</param>
/// <param name="Motivo">Por qué, en una frase, cuando algo no se pudo; vacío si todo fue bien. Nunca contiene la clave.</param>
public sealed record ResultadoDeLaBusqueda(
    QueSeEncontro Que,
    string Etiqueta = "",
    ActivoDelRelease? Instalador = null,
    string RutaDeLaClave = "",
    string Motivo = "");

/// <summary>El resultado de bajar el instalador y comprobarlo.</summary>
/// <param name="Lista">Verdadero solo si el archivo está en disco y tamaño y huella cuadran con lo que declaró GitHub.</param>
/// <param name="Ruta">Dónde quedó el instalador; vacía si no quedó.</param>
/// <param name="HuellaComprobada">Falso si la API no dio huella y solo se pudo comparar el tamaño; la franja lo dice.</param>
/// <param name="Motivo">Por qué no quedó lista, en una frase; vacío si quedó.</param>
public sealed record ResultadoDeLaDescarga(bool Lista, string Ruta = "", bool HuellaComprobada = false, string Motivo = "");

/// <summary>Cómo contestó el servidor a una petición, sin HTTP a la vista.</summary>
public enum EstadoDeLaRespuesta
{
    /// <summary>Contestó lo que se le pidió.</summary>
    Encontrado,

    /// <summary>404: no existe o no se tiene permiso para verlo (así contesta GitHub en privado).</summary>
    NoExiste,

    /// <summary>401 o 403: la clave no vale o no alcanza.</summary>
    SinPermiso,

    /// <summary>No hubo conexión o se agotó el tiempo.</summary>
    SinRed,

    /// <summary>Otro código, o un cuerpo que no se pudo leer.</summary>
    OtroFallo,
}

/// <summary>La respuesta del servidor a la consulta del último Release.</summary>
/// <param name="Estado">Cómo contestó.</param>
/// <param name="Release">El Release leído, solo con <see cref="EstadoDeLaRespuesta.Encontrado"/>.</param>
/// <param name="Motivo">Por qué, cuando no se pudo; sin ninguna clave dentro.</param>
public sealed record RespuestaDelServidor(EstadoDeLaRespuesta Estado, ReleasePublicado? Release = null, string Motivo = "");
