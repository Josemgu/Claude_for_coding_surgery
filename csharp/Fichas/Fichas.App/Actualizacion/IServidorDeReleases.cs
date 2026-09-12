namespace Fichas.App.Actualizacion;

/// <summary>
/// La única puerta por la que el programa habla con quien publica los Releases.
/// </summary>
/// <remarks>
/// <para>Existe para que las pruebas no salgan a internet (regla: sin red en pruebas). La
/// implementación de verdad es <see cref="ServidorDeReleasesDeGitHub"/>; las pruebas le
/// enchufan un <c>HttpMessageHandler</c> de mentirijilla y cuentan las peticiones.</para>
///
/// <para>Es una llamada saliente por HTTPS y nada más: no escucha, no abre puertos, no manda
/// datos del dueño (regla permanente 2 y §3 de CLAUDE.md, precisados el 2026-09-11).</para>
/// </remarks>
public interface IServidorDeReleases
{
    /// <summary>Pide el último Release publicado.</summary>
    /// <param name="clave">La clave de solo lectura, o nula para pedir sin cabecera.</param>
    /// <param name="cancelacion">Con el tiempo máximo de espera ya dentro.</param>
    /// <returns>Cómo contestó y, si contestó bien, el Release. Nunca lanza.</returns>
    Task<RespuestaDelServidor> ConsultarElUltimoRelease(string? clave, CancellationToken cancelacion);

    /// <summary>Baja un activo del Release a un archivo, en bloques, sin cargarlo entero en memoria.</summary>
    /// <param name="activo">El activo, con su dirección de API.</param>
    /// <param name="rutaDestino">Dónde escribirlo; se sobrescribe si ya está.</param>
    /// <param name="clave">La clave de solo lectura, o nula.</param>
    /// <param name="cancelacion">Con el tiempo máximo de la descarga ya dentro.</param>
    /// <returns>Cómo contestó; con <see cref="EstadoDeLaRespuesta.Encontrado"/> el archivo está escrito entero. Nunca lanza.</returns>
    Task<RespuestaDelServidor> DescargarActivo(ActivoDelRelease activo, string rutaDestino, string? clave, CancellationToken cancelacion);
}

/// <summary>Quien arranca el instalador ya comprobado. Detrás de una interfaz para que las pruebas no abran ningún proceso.</summary>
public interface ILanzadorDelInstalador
{
    /// <summary>Arranca el instalador en silencio, diciéndole la carpeta de datos para que el Fichas que reabra siga con la misma.</summary>
    /// <param name="rutaDelInstalador">La ruta del instalador ya comprobado.</param>
    /// <param name="carpetaDeDatos">La carpeta de datos con la que está abierto el programa.</param>
    /// <returns>Verdadero si el proceso arrancó; falso si Windows no lo dejó. Nunca lanza.</returns>
    bool Lanzar(string rutaDelInstalador, string carpetaDeDatos);
}
