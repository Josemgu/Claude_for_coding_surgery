using System.Security.Cryptography;
using System.Text;
using Fichas.App.Actualizacion;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// Lo que comparten las pruebas del actualizador: una carpeta de datos propia, un GitHub
/// de mentirijilla, un lanzador que no lanza nada y un cuaderno en memoria.
/// </summary>
/// <remarks>
/// <para>El JSON que fabrica <see cref="ReleaseComoLoDaGitHub"/> copia la forma real medida
/// el 2026-09-11 con <c>gh api repos/Josemgu/Claude_for_coding_surgery/releases/latest</c>:
/// <c>tag_name</c>, y por activo <c>name</c>, <c>size</c>, <c>digest</c> («sha256:…») y
/// <c>url</c> (la de la API, no <c>browser_download_url</c>).</para>
///
/// <para>⛔ Ninguna clave de verdad: <see cref="LaClaveInventada"/> es texto de prueba.</para>
/// </remarks>
internal sealed class MontajeDelActualizador : IDisposable
{
    /// <summary>La dirección que el programa consulta, tal como la fija la decisión.</summary>
    public const string LaDireccionDelUltimoRelease =
        "https://api.github.com/repos/Josemgu/Claude_for_coding_surgery/releases/latest";

    /// <summary>Una clave inventada para las pruebas; no abre nada en ninguna parte.</summary>
    public const string LaClaveInventada = "ghp_INVENTADA_para_pruebas_0123456789";

    /// <summary>La carpeta de datos de esta prueba, temporal y propia.</summary>
    public string CarpetaDeDatos { get; }

    /// <summary>La carpeta donde se baja el instalador en esta prueba; temporal y propia.</summary>
    public string CarpetaTemporal { get; }

    /// <summary>El GitHub de mentirijilla.</summary>
    public ServidorDeMentirijilla Servidor { get; } = new();

    /// <summary>El lanzador que cuenta y no lanza.</summary>
    public LanzadorQueNoLanza Lanzador { get; } = new();

    /// <summary>Lo que el actualizador anotó en el cuaderno, línea a línea.</summary>
    public List<string> Cuaderno { get; } = [];

    /// <summary>Crea las dos carpetas y deja el montaje listo.</summary>
    public MontajeDelActualizador()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "Fichas-pruebas-actualizacion-" + Guid.NewGuid().ToString("N"));
        CarpetaDeDatos = Path.Combine(raiz, "datos");
        CarpetaTemporal = Path.Combine(raiz, "temporal");
        Directory.CreateDirectory(CarpetaDeDatos);
        Directory.CreateDirectory(CarpetaTemporal);
    }

    /// <summary>Monta un actualizador que cree tener abierta la versión dada.</summary>
    /// <param name="versionActual">Lo que diría <c>VersionDelPrograma.Numero</c>: «11».</param>
    /// <param name="tiempoMaximoDeConsulta">Cuánto espera la consulta antes de rendirse; corto en las pruebas de tiempo agotado.</param>
    public Actualizador Montar(string versionActual = "11", TimeSpan? tiempoMaximoDeConsulta = null)
        => new(
            new ServidorDeReleasesDeGitHub(Servidor),
            CarpetaDeDatos,
            versionActual,
            Lanzador,
            Cuaderno.Add,
            CarpetaTemporal,
            tiempoMaximoDeConsulta);

    /// <summary>Deja la clave inventada en el archivo que el programa lee.</summary>
    public void ConLaClavePegada()
        => File.WriteAllText(ClaveDeActualizacion.RutaEn(CarpetaDeDatos), LaClaveInventada + "\r\n");

    /// <summary>Prepara el último Release con esa etiqueta y un instalador de esos bytes.</summary>
    /// <param name="etiqueta">La etiqueta del Release: «v12».</param>
    /// <param name="instalador">Los bytes del instalador que se servirán al bajarlo.</param>
    /// <param name="conHuella">Si el JSON trae <c>digest</c>; las API viejas no lo traen.</param>
    /// <param name="tamanoDeclarado">El <c>size</c> que declara el JSON; por defecto el real.</param>
    /// <param name="huellaDeclarada">El <c>digest</c> que declara el JSON; por defecto el real.</param>
    /// <returns>La dirección de API del activo del instalador.</returns>
    public string ConElUltimoRelease(
        string etiqueta, byte[] instalador, bool conHuella = true, long? tamanoDeclarado = null, string? huellaDeclarada = null)
    {
        var nombre = $"Instalar-Fichas-{etiqueta}.exe";
        var url = $"https://api.github.com/repos/Josemgu/Claude_for_coding_surgery/releases/assets/{Random.Shared.Next(100000, 999999)}";
        var huella = huellaDeclarada ?? HuellaDe(instalador);

        Servidor.AlPedir(LaDireccionDelUltimoRelease, System.Net.HttpStatusCode.OK, ReleaseComoLoDaGitHub(
            etiqueta,
            (nombre, url, tamanoDeclarado ?? instalador.Length, conHuella ? huella : null),
            ($"Fichas-{etiqueta}.zip", url + "1", 1234, conHuella ? huella : null)));
        Servidor.AlPedir(url, System.Net.HttpStatusCode.OK, instalador);
        return url;
    }

    /// <summary>El SHA-256 en hexadecimal minúscula, como lo escribe GitHub tras «sha256:».</summary>
    /// <param name="bytes">Los bytes del activo.</param>
    public static string HuellaDe(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>El JSON de un Release con la forma que devuelve la API de GitHub.</summary>
    /// <param name="etiqueta">El <c>tag_name</c>.</param>
    /// <param name="activos">Por activo: nombre, url de API, tamaño y huella (nula si la API no la da).</param>
    public static string ReleaseComoLoDaGitHub(string etiqueta, params (string Nombre, string Url, long Tamano, string? Huella)[] activos)
    {
        var texto = new StringBuilder();
        texto.Append("{\"tag_name\":\"").Append(etiqueta).Append("\",\"name\":\"Fichas ").Append(etiqueta).Append("\",\"assets\":[");
        for (var i = 0; i < activos.Length; i++)
        {
            var (nombre, url, tamano, huella) = activos[i];
            if (i > 0) texto.Append(',');
            texto.Append("{\"name\":\"").Append(nombre).Append("\",\"url\":\"").Append(url)
                .Append("\",\"size\":").Append(tamano)
                .Append(",\"content_type\":\"application/x-msdownload\",\"state\":\"uploaded\"");
            if (huella is not null) texto.Append(",\"digest\":\"sha256:").Append(huella).Append('"');
            texto.Append(",\"browser_download_url\":\"https://github.com/Josemgu/Claude_for_coding_surgery/releases/download/")
                .Append(etiqueta).Append('/').Append(nombre).Append("\"}");
        }
        texto.Append("]}");
        return texto.ToString();
    }

    /// <summary>Borra las carpetas de la prueba.</summary>
    public void Dispose()
    {
        var raiz = Path.GetDirectoryName(CarpetaDeDatos)!;
        if (Directory.Exists(raiz)) Directory.Delete(raiz, recursive: true);
    }
}

/// <summary>Un lanzador que apunta qué se le pidió lanzar y no lanza nada.</summary>
internal sealed class LanzadorQueNoLanza : ILanzadorDelInstalador
{
    /// <summary>Las rutas que se le pidió lanzar, en orden.</summary>
    public List<string> Lanzados { get; } = [];

    /// <summary>La carpeta de datos que se le dio con cada lanzamiento, en el mismo orden.</summary>
    public List<string> CarpetasDeDatos { get; } = [];

    /// <summary>Qué contestar: por defecto que se lanzó bien.</summary>
    public bool Contesta { get; set; } = true;

    /// <summary>Apunta la ruta y la carpeta, y contesta lo preparado, sin abrir ningún proceso.</summary>
    /// <param name="rutaDelInstalador">La ruta que el programa querría lanzar.</param>
    /// <param name="carpetaDeDatos">La carpeta de datos que el programa le pasaría al instalador.</param>
    public bool Lanzar(string rutaDelInstalador, string carpetaDeDatos)
    {
        Lanzados.Add(rutaDelInstalador);
        CarpetasDeDatos.Add(carpetaDeDatos);
        return Contesta;
    }
}
