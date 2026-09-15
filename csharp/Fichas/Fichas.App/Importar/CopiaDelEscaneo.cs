using System.Security.Cryptography;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Importar;

/// <summary>
/// La copia de cada escaneo que el programa guarda para sí, en la carpeta de datos.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Existe por el defecto del 2026-09-15</b>, con las palabras del dueño: <i>«un
/// PDF se queda pegado y se carga a lugares que no le corresponde»</i>. Medido en una base
/// propia con PDF sintéticos y la ventana abierta: el caso guardaba la RUTA del archivo
/// del escáner, y el escáner escribe siempre el mismo nombre —<c>Scan.pdf</c>—. Cada
/// escaneo nuevo sobrescribía el anterior, y TODOS los documentos que habían salido de esa
/// ruta pasaban a enseñar el papel nuevo: cuatro casos con <c>Scan.pdf|1</c> y el visor de
/// los cuatro con un impreso ajeno. Después el dueño lo vio en los 37.</para>
///
/// <para><b>Lo que hace:</b> antes de guardar un caso, copia el archivo a
/// <c>&lt;carpeta de datos&gt;\escaneos\</c> con su nombre original y una huella de su
/// contenido, y el caso guarda la ruta de ESA copia. Lo que el escáner haga después con su
/// archivo ya no le afecta a nadie. El mismo contenido da la misma huella, así que importar
/// dos veces el mismo archivo deja UNA copia y sigue avisándose como duplicado por la misma
/// hoja del mismo archivo.</para>
///
/// <para>⛔ <b>No se toca el archivo del escáner</b>: se lee y se copia. Y si la copia no se
/// puede hacer —disco lleno, sin permiso—, el documento entra igual con la ruta original y
/// lo dice (requisito 9: avisar, nunca impedir): mejor un papel que puede cambiar que un
/// documento que no entra.</para>
///
/// <para>Decisión que se devuelve al dueño: cada escaneo ocupa el doble en disco (el suyo y
/// la copia). Sobre sus tamaños medidos —decenas de KB a pocos MB por hoja— son cientos de
/// MB en miles de documentos, no gigas.</para>
/// </remarks>
public sealed partial class CopiaDelEscaneo
{
    /// <summary>La subcarpeta de la carpeta de datos donde van las copias.</summary>
    public const string NombreDeLaCarpeta = "escaneos";

    /// <summary>Cuántas letras de la huella SHA-256 van en el nombre: 12 hexadecimales, 48 bits.</summary>
    private const int LetrasDeLaHuella = 12;

    /// <summary>Se ata a la carpeta de datos; las copias van en su subcarpeta.</summary>
    /// <param name="carpetaDeDatos">La carpeta de datos del programa (la de <c>fichas.db</c>).</param>
    public CopiaDelEscaneo(string carpetaDeDatos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpetaDeDatos);
        Carpeta = Path.Combine(carpetaDeDatos, NombreDeLaCarpeta);
    }

    /// <summary>La carpeta donde quedan las copias.</summary>
    public string Carpeta { get; }

    /// <summary>
    /// Lo que identifica al papel de una ruta: su huella si es una copia, y la ruta entera
    /// si no lo es.
    /// </summary>
    /// <remarks>
    /// Es lo que permite que la misma ficha importada desde dos carpetas —mismo contenido,
    /// otro nombre— se reconozca como la misma hoja del mismo papel: las dos copias llevan la
    /// misma huella en el nombre. Un archivo que no es copia (los casos de antes del
    /// 2026-09-15, o uno que no se pudo copiar) se identifica por su ruta, como siempre.
    /// </remarks>
    /// <param name="ruta">La ruta guardada en un caso o la de una hoja recién leída.</param>
    /// <returns>«huella:» y las doce letras si el nombre lleva huella; si no, la ruta tal cual.</returns>
    public static string IdentidadDelPapel(string ruta)
    {
        var encaje = PatronDeLaCopia().Match(Path.GetFileName(ruta));
        return encaje.Success ? "huella:" + encaje.Groups["huella"].Value : ruta;
    }

    /// <summary>«nombre-3fa9c21b7e04.pdf»: cualquier nombre, un guion y doce hexadecimales en minúscula.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^.+-(?<huella>[0-9a-f]{12})\.pdf$")]
    private static partial System.Text.RegularExpressions.Regex PatronDeLaCopia();

    /// <summary>
    /// La copia del archivo, hecha si no estaba; o el original con su aviso si no se pudo.
    /// </summary>
    /// <remarks>
    /// Un archivo que no existe —el que no se pudo abrir y deja su renglón— vuelve tal cual y
    /// sin aviso: no hay nada que copiar y el renglón ya dice lo suyo. Y un archivo que YA es
    /// una copia —el dueño eligió la carpeta de datos— vuelve tal cual: copiar la copia
    /// dejaría dos papeles iguales con dos nombres.
    /// </remarks>
    /// <param name="rutaOriginal">El archivo tal como lo eligió el dueño, con su ruta completa.</param>
    /// <returns>La ruta que tiene que guardar el caso, y los avisos si no se pudo copiar.</returns>
    public CopiaGuardada Guardar(string rutaOriginal)
    {
        if (string.IsNullOrWhiteSpace(rutaOriginal) || !File.Exists(rutaOriginal)) return new(rutaOriginal, []);
        if (EstaDentroDeLaCarpeta(rutaOriginal)) return new(rutaOriginal, []);

        try
        {
            var destino = Path.Combine(Carpeta, NombreDeLaCopia(rutaOriginal, HuellaDe(rutaOriginal)));
            Directory.CreateDirectory(Carpeta);
            if (!File.Exists(destino)) File.Copy(rutaOriginal, destino, overwrite: false);
            return new(destino, []);
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            return new(rutaOriginal, [AvisoDeQueNoSeCopio(rutaOriginal, fallo)]);
        }
    }

    /// <summary>Si el archivo ya vive en la carpeta de las copias.</summary>
    /// <param name="ruta">El archivo, con su ruta completa.</param>
    private bool EstaDentroDeLaCarpeta(string ruta)
    {
        var carpetaDelArchivo = Path.GetDirectoryName(Path.GetFullPath(ruta))?.TrimEnd(Path.DirectorySeparatorChar);
        var laDeLasCopias = Path.GetFullPath(Carpeta).TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(carpetaDelArchivo, laDeLasCopias, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>«Scan-3fa9c21b7e04.pdf»: el nombre original y la huella del contenido.</summary>
    /// <remarks>
    /// El nombre original se conserva para que el dueño reconozca el archivo en las listas;
    /// la huella es lo que hace que dos papeles distintos con el mismo nombre no se pisen, y
    /// que el mismo papel dos veces sea UN archivo.
    /// </remarks>
    /// <param name="rutaOriginal">El archivo tal como llegó.</param>
    /// <param name="huella">Las primeras letras del SHA-256 de su contenido.</param>
    private static string NombreDeLaCopia(string rutaOriginal, string huella)
        => $"{Path.GetFileNameWithoutExtension(rutaOriginal)}-{huella}{Path.GetExtension(rutaOriginal).ToLowerInvariant()}";

    /// <summary>Las primeras letras del SHA-256 del archivo, en minúsculas.</summary>
    /// <param name="ruta">El archivo, que se lee entero por un flujo para no cargarlo en memoria.</param>
    private static string HuellaDe(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return Convert.ToHexStringLower(SHA256.HashData(flujo))[..LetrasDeLaHuella];
    }

    /// <summary>El SHA-256 entero del archivo, en minúsculas; nulo si el disco no lo deja leer.</summary>
    /// <remarks>Es con lo que la comprobación de los papeles encuentra dos archivos byte a byte iguales.</remarks>
    /// <param name="ruta">El archivo, con su ruta completa.</param>
    public static string? HuellaEnteraDe(string ruta)
    {
        try
        {
            using var flujo = File.OpenRead(ruta);
            return Convert.ToHexStringLower(SHA256.HashData(flujo));
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            return null;
        }
    }

    /// <summary>Lo que el disco puede contestar mal sin que sea un defecto del programa; la misma lista que el recorrido de carpetas.</summary>
    /// <param name="fallo">Lo que levantó el sistema de archivos.</param>
    private static bool EsUnFalloDeDisco(Exception fallo)
        => fallo is UnauthorizedAccessException
                 or IOException
                 or System.Security.SecurityException
                 or ArgumentException
                 or NotSupportedException;

    /// <summary>El aviso de que el documento se quedó con el archivo original porque la copia no se pudo hacer.</summary>
    /// <param name="rutaOriginal">El archivo que no se pudo copiar.</param>
    /// <param name="fallo">Por qué.</param>
    private Aviso AvisoDeQueNoSeCopio(string rutaOriginal, Exception fallo)
        => Aviso.Advierte(
            $"No se pudo guardar una copia del escaneo «{Path.GetFileName(rutaOriginal)}»: el documento apunta al archivo original.",
            string.Empty,
            $"El programa guarda una copia de cada escaneo en «{Carpeta}» para que el documento siga enseñando su papel "
            + "aunque el escáner sobrescriba el archivo. Esta vez no se pudo, así que si ese archivo cambia, el documento "
            + $"enseñará lo que haya en él. Motivo: {fallo.GetType().Name}: {fallo.Message}");
}

/// <summary>Lo que devuelve la copia: qué ruta guarda el caso y qué hay que decir.</summary>
/// <param name="RutaDelPapel">La copia si se hizo; si no, el archivo original.</param>
/// <param name="Avisos">Vacío si se copió o no había nada que copiar; el aviso si no se pudo.</param>
public sealed record CopiaGuardada(string RutaDelPapel, IReadOnlyList<Aviso> Avisos);
