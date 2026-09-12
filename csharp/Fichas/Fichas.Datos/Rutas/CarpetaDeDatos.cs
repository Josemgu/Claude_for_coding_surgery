using System.Runtime.InteropServices;

namespace Fichas.Datos.Rutas;

/// <summary>
/// Donde viven la base y el Excel espejo, resuelto por la API de Windows.
/// </summary>
/// <remarks>
/// <para>
/// Portado de <c>datos/rutas.py</c>. DECISIONES.md (2026-09-02, «La carpeta de datos
/// se resuelve por API, nunca por nombre») es vinculante: la carpeta de Documentos se
/// le pide a <c>SHGetKnownFolderPath</c> con el identificador de carpeta conocida, y
/// NUNCA se compone pegando el nombre de la carpeta al perfil del usuario.
/// </para>
/// <para>
/// El motivo esta medido: en la maquina del dueno existen tres carpetas candidatas y
/// dos de ellas sincronizan con OneDrive. Componer la ruta por nombre puede aterrizar
/// en la de OneDrive y subir a la nube una base con MRN de personas reales.
/// </para>
/// <para>
/// ⚠️ Se usa el GUID y no <c>Environment.SpecialFolder.MyDocuments</c> a proposito.
/// Esa propiedad de .NET resuelve por una via distinta, no documenta que consulte la
/// misma carpeta conocida, y esta es justo la decision que el dueno tomo por escrito.
/// </para>
/// </remarks>
public static class CarpetaDeDatos
{
    /// <summary>El nombre de la carpeta del programa dentro de Documentos.</summary>
    public const string NombreDeLaCarpetaDeDatos = "Fichas";

    /// <summary>El nombre del archivo de base de datos.</summary>
    public const string NombreDeLaBase = "fichas.db";

    /// <summary>El argumento de linea de ordenes que manda sobre la carpeta resuelta.</summary>
    public const string ArgumentoDeCarpeta = "--carpeta-de-datos";

    /// <summary>
    /// Identificador de la carpeta conocida de Documentos, tal como lo publica la API
    /// de Windows. Es un GUID y no un nombre: por eso no depende del idioma del
    /// sistema ni de que existan carpetas homonimas.
    /// </summary>
    private static readonly Guid CarpetaConocidaDeDocumentos =
        new("FDD39AD0-238F-46AF-ADB4-6C85480369C7");

    // Va con `DllImport` y no con `LibraryImport` a proposito: el generador de
    // `LibraryImport` exige `AllowUnsafeBlocks` en todo el proyecto, y abrir el codigo
    // no seguro entero por dos llamadas al sistema es un precio que esta capa no tiene
    // por que pagar. Son dos firmas de tipos simples: no hay nada que el generador
    // pudiera hacer mejor aqui.
    /// <summary>
    /// <c>SHGetKnownFolderPath</c> de <c>shell32.dll</c>: la única vía admitida para saber dónde está Documentos.
    /// </summary>
    /// <param name="identificador">El GUID de la carpeta conocida.</param>
    /// <param name="banderas">Las <c>KNOWN_FOLDER_FLAG</c>; aquí siempre 0.</param>
    /// <param name="testigo">El testigo del usuario; <see cref="IntPtr.Zero"/> es el usuario actual.</param>
    /// <param name="ruta">Sale apuntando a una cadena UTF-16 que hay que liberar con <see cref="LiberarMemoriaDeLaTarea"/>.</param>
    /// <returns>Un <c>HRESULT</c>: 0 si fue bien.</returns>
    [DllImport("shell32.dll", EntryPoint = "SHGetKnownFolderPath", ExactSpelling = true)]
    private static extern int PedirCarpetaConocida(
        in Guid identificador, uint banderas, IntPtr testigo, out IntPtr ruta);

    /// <summary><c>CoTaskMemFree</c> de <c>ole32.dll</c>: libera la cadena que devolvió <see cref="PedirCarpetaConocida"/>.</summary>
    /// <param name="apuntador">El apuntador que salió por <c>ruta</c>; se ignora si es cero.</param>
    [DllImport("ole32.dll", EntryPoint = "CoTaskMemFree", ExactSpelling = true)]
    private static extern void LiberarMemoriaDeLaTarea(IntPtr apuntador);

    /// <summary>
    /// La carpeta de Documentos que el sistema declara vigente. No crea nada.
    /// </summary>
    /// <exception cref="ErrorDeRuta">
    /// Si Windows devuelve un error. Es preferible no arrancar a escribir la base en
    /// un sitio equivocado: esta es una de las excepciones que el requisito 9 SI
    /// reserva, porque ninguna pantalla puede seguir sin saber donde escribe.
    /// </exception>
    /// <returns>La ruta de Documentos tal como la devuelve Windows, sin barra final.</returns>
    public static string ResolverCarpetaDeDocumentos()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ErrorDeRuta(
                "La carpeta de datos solo se puede resolver en Windows: este sistema " +
                $"se identifica como '{RuntimeInformation.OSDescription}'.");
        }

        var apuntador = IntPtr.Zero;
        try
        {
            var codigo = PedirCarpetaConocida(CarpetaConocidaDeDocumentos, 0, IntPtr.Zero, out apuntador);
            if (codigo != 0 || apuntador == IntPtr.Zero)
            {
                throw new ErrorDeRuta(
                    "Windows no devolvio la carpeta de Documentos: " +
                    $"SHGetKnownFolderPath respondio con el codigo {codigo}.");
            }

            return Marshal.PtrToStringUni(apuntador)
                ?? throw new ErrorDeRuta(
                    "Windows devolvio una carpeta de Documentos vacia.");
        }
        finally
        {
            if (apuntador != IntPtr.Zero)
            {
                LiberarMemoriaDeLaTarea(apuntador);
            }
        }
    }

    /// <summary>La carpeta donde viven la base y el Excel espejo. No la crea.</summary>
    /// <returns>Documentos más <see cref="NombreDeLaCarpetaDeDatos"/>.</returns>
    public static string ResolverCarpetaDeDatos()
        => Path.Combine(ResolverCarpetaDeDocumentos(), NombreDeLaCarpetaDeDatos);

    /// <summary>La ruta del archivo de base de datos. No lo crea.</summary>
    /// <param name="carpetaDeDatos">
    /// La carpeta a usar; si es nula se resuelve por la API.
    /// </param>
    /// <returns>La carpeta más <see cref="NombreDeLaBase"/>.</returns>
    public static string RutaDeLaBase(string? carpetaDeDatos = null)
        => Path.Combine(carpetaDeDatos ?? ResolverCarpetaDeDatos(), NombreDeLaBase);

    /// <summary>
    /// La carpeta que mandan los argumentos de arranque, o nula si no la dicen.
    /// </summary>
    /// <remarks>
    /// Admite las dos formas de escribirlo, que son las dos que la gente teclea:
    /// <c>--carpeta-de-datos C:\ruta</c> y <c>--carpeta-de-datos=C:\ruta</c>.
    /// Un argumento presente pero sin valor detras devuelve nulo en vez de reventar:
    /// el programa sigue con la carpeta que resuelve la API, que es lo correcto.
    /// </remarks>
    /// <param name="argumentos">Los argumentos de línea de órdenes tal como llegan, sin el nombre del programa.</param>
    /// <returns>La carpeta que dicen, o nula si no está el argumento o viene sin valor.</returns>
    public static string? LeerCarpetaDeLosArgumentos(IReadOnlyList<string> argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);

        for (var posicion = 0; posicion < argumentos.Count; posicion++)
        {
            var argumento = argumentos[posicion];

            if (argumento.StartsWith(ArgumentoDeCarpeta + "=", StringComparison.Ordinal))
            {
                var valor = argumento[(ArgumentoDeCarpeta.Length + 1)..];
                return string.IsNullOrWhiteSpace(valor) ? null : valor;
            }

            if (string.Equals(argumento, ArgumentoDeCarpeta, StringComparison.Ordinal)
                && posicion + 1 < argumentos.Count
                && !string.IsNullOrWhiteSpace(argumentos[posicion + 1]))
            {
                return argumentos[posicion + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// La carpeta de datos que toca usar: la de los argumentos si la dicen, y si no
    /// la que resuelve la API de Windows.
    /// </summary>
    /// <param name="argumentos">Los argumentos de línea de órdenes.</param>
    /// <returns>Una carpeta, siempre; no comprueba que exista.</returns>
    public static string ResolverCarpetaDeDatos(IReadOnlyList<string> argumentos)
        => LeerCarpetaDeLosArgumentos(argumentos) ?? ResolverCarpetaDeDatos();

    /// <summary>
    /// Dice si la ruta cuelga de una carpeta de OneDrive.
    /// </summary>
    /// <remarks>
    /// Compara segmento a segmento y NO por subcadena: 'OneDriveViejo' no es OneDrive,
    /// y una carpeta llamada 'MisOneDrivers' tampoco.
    /// </remarks>
    /// <param name="ruta">Una ruta absoluta o relativa; se parte por las dos barras.</param>
    /// <returns>Verdadero si algún segmento es exactamente «OneDrive», sin distinguir mayúsculas.</returns>
    public static bool EstaBajoOneDrive(string ruta)
    {
        ArgumentException.ThrowIfNullOrEmpty(ruta);

        return ruta
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                   StringSplitOptions.RemoveEmptyEntries)
            .Any(segmento => segmento.Equals("OneDrive", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>No se pudo resolver la carpeta de datos.</summary>
public sealed class ErrorDeRuta : InvalidOperationException
{
    /// <summary>Con el motivo escrito en espanol.</summary>
    /// <param name="mensaje">Qué respondió Windows, o por qué no se pudo preguntar.</param>
    public ErrorDeRuta(string mensaje) : base(mensaje)
    {
    }

    /// <summary>Con el motivo y la causa de debajo.</summary>
    /// <param name="mensaje">Qué respondió Windows, o por qué no se pudo preguntar.</param>
    /// <param name="causa">La excepción de debajo.</param>
    public ErrorDeRuta(string mensaje, Exception causa) : base(mensaje, causa)
    {
    }

    /// <summary>Sin motivo; existe para cumplir el convenio de las excepciones.</summary>
    public ErrorDeRuta()
    {
    }
}
