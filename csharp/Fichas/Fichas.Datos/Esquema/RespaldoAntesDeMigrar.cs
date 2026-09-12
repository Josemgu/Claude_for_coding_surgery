using System.Globalization;
using Fichas.Datos.Conexion;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// La copia que se hace ANTES de migrar, y la vuelta atras si la migracion falla.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ Regla del dueno, DECISIONES.md 2026-09-04 18:05 («El programa nuevo migro la base
/// VIVA del dueno solo por abrirse»): alguien arranco el <c>.exe</c> sin
/// <c>--carpeta-de-datos</c>, el programa resolvio Documentos, abrio la base real y la
/// llevo de la 13 a la 15 <b>sin preguntar y sin copia previa</b>. Aquel dia no se perdio
/// nada porque esa base tenia dos casos. Textual: «que un programa migre la base de
/// trabajo de alguien por el mero hecho de abrirse, sin avisar y sin copia, es un riesgo
/// para el dueno, no una comodidad».
/// </para>
/// <para>
/// <b>Solo se copia cuando de verdad hay algo que migrar.</b> Copiar en cada apertura
/// llenaria el disco: una base de tres mil casos abierta cinco veces al dia deja cinco
/// copias diarias. Una base ya al dia no se toca y no se copia.
/// </para>
/// <para>
/// <b>Y si no hay sitio, no se migra.</b> Migrar sin haber podido copiar es justo lo que
/// la regla prohibe; preferir «migro igual, sin red» seria dejar la base sin salida el
/// dia que la migracion se rompa a mitad.
/// </para>
/// </remarks>
public sealed class RespaldoAntesDeMigrar
{
    /// <summary>
    /// Cuantas veces el tamano de la base tiene que haber libre para atreverse a migrar.
    /// </summary>
    /// <remarks>
    /// Dos, no una: una es la copia, y la otra es la propia migracion, que reconstruye
    /// tablas creando una version nueva al lado antes de tirar la vieja
    /// (<see cref="ReconstructorDeTablas"/>). Con sitio justo para la copia, la migracion
    /// se quedaria sin espacio a mitad, que es el fallo que esto viene a evitar.
    /// </remarks>
    public const int VecesElTamanoQueHacenFalta = 2;

    /// <summary>El trozo que marca una copia previa en el nombre del archivo.</summary>
    public const string MarcaDeLaCopia = "antes-de-migrar";

    /// <summary>Privado a propósito: solo <see cref="PrepararPara"/> sabe si hizo falta copiar, y es la única que construye.</summary>
    /// <param name="versionDeOrigen">La versión que la base tenía; 0 si no existía o no se pudo leer.</param>
    /// <param name="rutaDeLaCopia">Dónde quedó la copia, o nulo si no se hizo.</param>
    private RespaldoAntesDeMigrar(int versionDeOrigen, string? rutaDeLaCopia)
    {
        VersionDeOrigen = versionDeOrigen;
        RutaDeLaCopia = rutaDeLaCopia;
    }

    /// <summary>La version que la base tenia antes de tocar nada; 0 si no habia base.</summary>
    public int VersionDeOrigen { get; }

    /// <summary>Donde quedo la copia, o nulo si no hizo falta ninguna.</summary>
    public string? RutaDeLaCopia { get; }

    /// <summary>Si se llego a hacer una copia.</summary>
    public bool HayCopia => RutaDeLaCopia is not null;

    /// <summary>
    /// Mira la base, decide si hay que migrar y, si hay, la copia.
    /// </summary>
    /// <param name="rutaDeLaBase">El archivo <c>.db</c> que se va a abrir.</param>
    /// <param name="cuando">La hora que va en el nombre; por defecto, ahora.</param>
    /// <param name="espacioLibre">
    /// Cuantos bytes quedan en la unidad de esa ruta; por defecto se le pregunta al
    /// sistema. Se puede pasar para poder probar el caso de disco lleno sin llenar uno.
    /// </param>
    /// <exception cref="ErrorDeMigracion">Si no hay sitio, o si la copia no se pudo hacer.</exception>
    /// <returns>El respaldo hecho, o uno sin copia si la base no existía o ya estaba al día.</returns>
    public static RespaldoAntesDeMigrar PrepararPara(
        string rutaDeLaBase, DateTime? cuando = null, Func<string, long>? espacioLibre = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeLaBase);

        if (!File.Exists(rutaDeLaBase))
        {
            // Una base que todavia no existe no tiene nada que respaldar: se va a crear
            // entera y la version 0 dice exactamente eso.
            return new RespaldoAntesDeMigrar(0, null);
        }

        var versionDeOrigen = LeerLaVersionSinTocarNada(rutaDeLaBase);
        if (versionDeOrigen >= AplicadorDeEsquema.VersionAlDia)
        {
            return new RespaldoAntesDeMigrar(versionDeOrigen, null);
        }

        ComprobarQueHaySitio(rutaDeLaBase, espacioLibre ?? EspacioLibreDeLaUnidad);

        var destino = SitioLibreParaLaCopia(rutaDeLaBase, versionDeOrigen, cuando ?? DateTime.Now);
        try
        {
            File.Copy(rutaDeLaBase, destino);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
        {
            throw new ErrorDeMigracion(
                $"No se pudo copiar la base antes de migrarla: {causa.Message}. " +
                "NO se migro nada: la base se queda como estaba.",
                causa);
        }

        return new RespaldoAntesDeMigrar(versionDeOrigen, destino);
    }

    /// <summary>
    /// Devuelve la base a como estaba y explica donde quedo la copia.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada migracion es atomica por si sola, pero la cola no lo es: si la 14 y la 15
    /// pasan y la 16 revienta, la base se queda en la 15, o sea CAMBIADA. El criterio del
    /// dueno es que quede como estaba, y para eso hay que reponer el archivo entero.
    /// </para>
    /// <para>
    /// Los archivos satelite del diario se borran despues de reponer: un
    /// <c>fichas.db-journal</c> de la migracion rota contra la base repuesta es una via de
    /// corrupcion, y el archivo repuesto ya esta completo por si mismo.
    /// </para>
    /// </remarks>
    /// <param name="rutaDeLaBase">La base a reponer; tiene que estar ya cerrada.</param>
    /// <param name="causa">El fallo que obligo a deshacer.</param>
    /// <returns>El error que hay que lanzar, con la copia nombrada dentro.</returns>
    public ErrorDeMigracion Deshacer(string rutaDeLaBase, Exception causa)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeLaBase);
        ArgumentNullException.ThrowIfNull(causa);

        if (RutaDeLaCopia is null)
        {
            return new ErrorDeMigracion(
                $"La migracion fallo: {causa.Message}. No habia copia previa que reponer.",
                causa);
        }

        try
        {
            File.Copy(RutaDeLaCopia, rutaDeLaBase, overwrite: true);
            BorrarLosSatelitesDelDiario(rutaDeLaBase);
        }
        catch (Exception alReponer) when (alReponer is IOException or UnauthorizedAccessException)
        {
            return new ErrorDeMigracion(
                $"La migracion fallo ({causa.Message}) y ADEMAS no se pudo reponer la base " +
                $"desde la copia ({alReponer.Message}). La copia intacta esta en " +
                $"«{RutaDeLaCopia}»: no la borre.",
                causa);
        }

        return new ErrorDeMigracion(
            $"No se pudo migrar la base de la versión {VersionDeOrigen} a la " +
            $"{AplicadorDeEsquema.VersionAlDia}: {causa.Message} La base se repuso tal como " +
            $"estaba, en la versión {VersionDeOrigen}. La copia previa quedó en " +
            $"«{RutaDeLaCopia}».",
            causa);
    }

    /// <summary>La linea del registro: de que version venia la base y a cual fue.</summary>
    /// <remarks>
    /// Hasta el 2026-09-04 el registro escribia «Base abierta con esquema version 15»
    /// tanto si habia migrado como si no, y por eso no hubo forma de saber —leyendo el
    /// cuaderno del dueno— que su base se acababa de migrar de la 13 a la 15.
    /// </remarks>
    /// <param name="versionFinal">La version en la que la base quedo.</param>
    /// <returns>Una de tres frases: creada, abierta sin migrar, o migrada de X a Y con la ruta de la copia.</returns>
    public string LineaDeCierre(int versionFinal)
    {
        if (VersionDeOrigen == 0)
        {
            return $"Base creada con esquema versión {Numero(versionFinal)}.";
        }

        if (!HayCopia)
        {
            return $"Base abierta con esquema versión {Numero(versionFinal)} " +
                   "(ya estaba al día: no se migró nada).";
        }

        return $"Base migrada de la versión {Numero(VersionDeOrigen)} a la " +
               $"{Numero(versionFinal)}. Copia previa en «{RutaDeLaCopia}».";
    }

    /// <summary>La version que la base tiene ahora, abriendola SOLO PARA LEER.</summary>
    /// <remarks>
    /// Solo lectura a proposito: mirar que version tiene no puede ser la operacion que
    /// modifique el archivo del que todavia no hay copia.
    /// </remarks>
    /// <param name="rutaDeLaBase">El archivo <c>.db</c>, que tiene que existir.</param>
    /// <returns>La versión registrada, o 0 si el archivo no tiene <c>version_esquema</c> o no es una base.</returns>
    private static int LeerLaVersionSinTocarNada(string rutaDeLaBase)
    {
        try
        {
            using var conexion = FabricaDeConexiones.AbrirSoloLectura(rutaDeLaBase);
            return AplicadorDeEsquema.VersionDeLaBase(conexion) ?? 0;
        }
        catch (SqliteException)
        {
            // Un archivo sin tabla de versiones, o que ni siquiera es una base: se trata
            // como version 0, o sea que TODO lo que venga despues es un cambio y hay que
            // copiar antes. Es el lado prudente.
            return 0;
        }
    }

    /// <summary>Comprueba que cabe la copia y ademas la migracion.</summary>
    /// <param name="rutaDeLaBase">La base cuyo tamaño se multiplica por <see cref="VecesElTamanoQueHacenFalta"/>.</param>
    /// <param name="espacioLibre">Cómo saber los bytes libres de la unidad de una ruta.</param>
    /// <exception cref="ErrorDeMigracion">Si no caben la copia y la migración; el mensaje lleva las dos cifras en MiB.</exception>
    private static void ComprobarQueHaySitio(string rutaDeLaBase, Func<string, long> espacioLibre)
    {
        var tamano = new FileInfo(rutaDeLaBase).Length;
        var hacenFalta = tamano * VecesElTamanoQueHacenFalta;
        var libres = espacioLibre(rutaDeLaBase);

        if (libres >= hacenFalta) return;

        throw new ErrorDeMigracion(
            $"No hay sitio en el disco para copiar la base antes de migrarla: hacen falta " +
            $"{EnMiB(hacenFalta)} y quedan {EnMiB(libres)}. NO se migró nada y la base se " +
            "queda como estaba. Libere espacio y vuelva a abrir el programa.");
    }

    /// <summary>Cuantos bytes quedan libres en la unidad donde vive esa ruta.</summary>
    /// <param name="ruta">Cualquier ruta de la unidad que se quiere medir.</param>
    /// <returns>Los bytes libres, o <see cref="long.MaxValue"/> si no se pudo preguntar, para no bloquear el arranque por eso.</returns>
    private static long EspacioLibreDeLaUnidad(string ruta)
    {
        try
        {
            var raiz = Path.GetPathRoot(Path.GetFullPath(ruta));
            return string.IsNullOrEmpty(raiz) ? long.MaxValue : new DriveInfo(raiz).AvailableFreeSpace;
        }
        catch (Exception fallo) when (fallo is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Si no se puede preguntar, NO se bloquea el arranque por eso: la copia se
            // intenta igual y, si no cabe, fallara al copiar con su motivo de verdad.
            return long.MaxValue;
        }
    }

    /// <summary>Un nombre libre para la copia, en la misma carpeta que la base.</summary>
    /// <remarks>
    /// En la misma carpeta porque es donde el dueno la va a buscar. Si el nombre ya
    /// existe —dos aperturas en el mismo segundo— se numera: sobrescribir seria perder
    /// justo el respaldo recien hecho.
    /// </remarks>
    /// <param name="rutaDeLaBase">La base que se va a copiar.</param>
    /// <param name="version">La versión de origen, que va en el nombre.</param>
    /// <param name="cuando">La hora que va en el nombre.</param>
    /// <returns>Una ruta que todavía no existe en la carpeta de la base.</returns>
    private static string SitioLibreParaLaCopia(string rutaDeLaBase, int version, DateTime cuando)
    {
        var candidato = NombreDeLaCopia(rutaDeLaBase, version, cuando);
        var vuelta = 2;
        while (File.Exists(candidato))
        {
            candidato = NombreDeLaCopia(rutaDeLaBase, version, cuando, vuelta);
            vuelta++;
        }

        return candidato;
    }

    /// <summary>«fichas-antes-de-migrar-20260904-180537-v13.db», al lado de la base.</summary>
    /// <param name="rutaDeLaBase">La base original; la copia toma su carpeta, su nombre y su extensión.</param>
    /// <param name="version">La versión de origen, que va como <c>-vN</c>.</param>
    /// <param name="cuando">La hora, escrita como <c>yyyyMMdd-HHmmss</c>.</param>
    /// <param name="vuelta">1 para el primer intento; a partir de 2 se añade <c>-N</c> al final para no pisar una copia del mismo segundo.</param>
    /// <returns>La ruta completa de la copia; no comprueba si existe.</returns>
    public static string NombreDeLaCopia(
        string rutaDeLaBase, int version, DateTime cuando, int vuelta = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeLaBase);

        var carpeta = Path.GetDirectoryName(Path.GetFullPath(rutaDeLaBase)) ?? string.Empty;
        var nombre = Path.GetFileNameWithoutExtension(rutaDeLaBase);
        var extension = Path.GetExtension(rutaDeLaBase);
        var marca = cuando.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var repeticion = vuelta > 1
            ? "-" + vuelta.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        return Path.Combine(
            carpeta, $"{nombre}-{MarcaDeLaCopia}-{marca}-v{Numero(version)}{repeticion}{extension}");
    }

    /// <summary>Quita el diario de una migracion rota, que ya no vale para nada.</summary>
    /// <param name="rutaDeLaBase">La base repuesta; se borran <c>-journal</c>, <c>-wal</c> y <c>-shm</c> si existen.</param>
    private static void BorrarLosSatelitesDelDiario(string rutaDeLaBase)
    {
        foreach (var cola in new[] { "-journal", "-wal", "-shm" })
        {
            var satelite = rutaDeLaBase + cola;
            if (File.Exists(satelite)) File.Delete(satelite);
        }
    }

    /// <summary>Un entero en texto sin separador de miles ni cultura: «13», nunca «13,0» ni «1.300».</summary>
    /// <param name="valor">El número.</param>
    private static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>Bytes en mebibytes con un decimal, para el mensaje de disco lleno.</summary>
    /// <param name="bytes">La cifra en bytes.</param>
    /// <returns>Por ejemplo «12.5 MiB».</returns>
    private static string EnMiB(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
}
