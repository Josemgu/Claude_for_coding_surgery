using Fichas.Datos.Conexion;
using Fichas.Datos.Esquema;
using Fichas.Datos.Rutas;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos;

/// <summary>
/// El arranque de la capa de datos: primero se dice DONDE, despues se escribe.
/// </summary>
/// <remarks>
/// Portado de <c>datos/arranque.py</c>. DECISIONES.md (2026-09-02) obliga a que el
/// programa MUESTRE la ruta que resolvio antes de escribir nada, para que se vea si
/// cayo donde debia. El orden de esta clase es esa obligacion:
/// <see cref="MostrarRutaDeDatos"/> no crea nada, y
/// <see cref="PrepararLaBase(string?, Action{string}?)"/> la llama antes de tocar el disco.
/// </remarks>
public static class ArranqueDeLaBase
{
    /// <summary>
    /// Escribe la ruta resuelta y la devuelve. NO crea ni carpeta ni archivo.
    /// </summary>
    /// <remarks>
    /// Si la ruta cae bajo OneDrive lo dice, pero NO falla: puede ser la ruta legitima
    /// si el usuario tiene activada la copia de seguridad de carpetas conocidas. Lo que
    /// estaria mal es no decirlo, porque esa base lleva nombres y MRN de personas
    /// reales y se va a sincronizar con la nube.
    /// </remarks>
    /// <param name="carpetaDeDatos">La carpeta a usar; nula para resolverla por la API.</param>
    /// <param name="escribir">Donde va cada linea; por defecto, la consola.</param>
    /// <returns>La ruta completa del archivo <c>.db</c>, exista o no.</returns>
    public static string MostrarRutaDeDatos(
        string? carpetaDeDatos = null, Action<string>? escribir = null)
    {
        var decir = escribir ?? Console.WriteLine;
        var carpeta = carpetaDeDatos ?? CarpetaDeDatos.ResolverCarpetaDeDatos();
        var ruta = CarpetaDeDatos.RutaDeLaBase(carpeta);

        decir($"Carpeta de datos: {carpeta}");
        decir($"Base de datos:    {ruta}");
        decir($"Motor SQLite:     {LeerLaVersionDelMotor()}");

        if (CarpetaDeDatos.EstaBajoOneDrive(carpeta))
        {
            decir(
                $"AVISO: la carpeta de datos esta dentro de OneDrive ({carpeta}). " +
                "La base lleva nombres y MRN de personas reales y se va a sincronizar " +
                "con la nube. No se detiene el programa: hay que decidirlo a mano.");
        }

        return ruta;
    }

    /// <summary>
    /// Muestra la ruta, crea la carpeta si falta, COPIA la base si va a migrarla, aplica
    /// el esquema y devuelve la conexion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotente: sobre una base ya creada no duplica tablas ni filas y no toca los
    /// datos que ya estan. Quien la llama se queda con la conexion y es quien la cierra.
    /// </para>
    /// <para>
    /// ⛔ El orden NO es indiferente y lo fija el dueno (DECISIONES.md 2026-09-04 18:05):
    /// <b>primero la copia, despues la migracion</b>. Si la migracion falla, la base se
    /// repone tal como estaba y el mensaje dice donde quedo la copia; si no hay sitio en
    /// disco para copiar, no se migra y se avisa. Lo lleva
    /// <see cref="RespaldoAntesDeMigrar"/>.
    /// </para>
    /// </remarks>
    /// <param name="carpetaDeDatos">La carpeta a usar; nula para resolverla por la API.</param>
    /// <param name="escribir">Donde va cada linea; por defecto, la consola.</param>
    /// <exception cref="ErrorDeMigracion">
    /// Si no hay sitio para la copia, o si la migracion fallo. En los dos casos la base
    /// se queda —o se repone— como estaba.
    /// </exception>
    /// <returns>La conexión abierta, con las claves foráneas encendidas y el esquema al día.</returns>
    public static SqliteConnection PrepararLaBase(
        string? carpetaDeDatos = null, Action<string>? escribir = null)
    {
        var decir = escribir ?? Console.WriteLine;
        var ruta = MostrarRutaDeDatos(carpetaDeDatos, decir);

        var carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        var respaldo = RespaldoAntesDeMigrar.PrepararPara(ruta);
        if (respaldo.HayCopia)
        {
            decir($"Copia previa a la migración: {respaldo.RutaDeLaCopia}");
        }

        var conexion = FabricaDeConexiones.Abrir(ruta);
        int version;
        try
        {
            version = AplicadorDeEsquema.Aplicar(conexion);
        }
        catch (Exception fallo)
        {
            // La base tiene que quedar LIBRE antes de reponerla desde la copia: con el
            // archivo tomado por esta conexion, el File.Copy de vuelta no entraria.
            conexion.Close();
            conexion.Dispose();
            SqliteConnection.ClearAllPools();
            throw respaldo.Deshacer(ruta, fallo);
        }

        decir(respaldo.LineaDeCierre(version));
        decir($"Casos guardados: {ContarCasos(conexion)}");

        return conexion;
    }

    /// <summary>
    /// Lo mismo, leyendo la carpeta de los argumentos de arranque.
    /// </summary>
    /// <param name="argumentos">Los argumentos de línea de órdenes; se busca <see cref="CarpetaDeDatos.ArgumentoDeCarpeta"/>.</param>
    /// <param name="escribir">Donde va cada línea; por defecto, la consola.</param>
    /// <returns>La conexión abierta, igual que la otra sobrecarga.</returns>
    public static SqliteConnection PrepararLaBase(
        IReadOnlyList<string> argumentos, Action<string>? escribir = null)
        => PrepararLaBase(CarpetaDeDatos.LeerCarpetaDeLosArgumentos(argumentos), escribir);

    /// <summary>Que version del motor SQLite lleva dentro este ejecutable.</summary>
    /// <returns>Lo que devuelve <c>sqlite_version()</c>, o «desconocida» si el motor no contesta.</returns>
    private static string LeerLaVersionDelMotor()
    {
        // Se le pregunta al motor en vez de leer una constante del paquete: lo que
        // importa es el motor que de verdad se va a usar.
        using var conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT sqlite_version()";
        return Convert.ToString(orden.ExecuteScalar()) ?? "desconocida";
    }

    /// <summary>Cuántas filas tiene <c>casos</c>, archivadas incluidas, para la línea de arranque que dice si la base trae datos.</summary>
    /// <param name="conexion">La conexión ya migrada.</param>
    private static long ContarCasos(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM casos";
        return Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
