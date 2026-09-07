using System.Globalization;
using System.Text.RegularExpressions;

namespace Fichas.Lectura;

/// <summary>
/// Convertir el texto leido en un valor con formato, o en nada.
/// </summary>
/// <remarks>
/// Portado de `extraccion/normalizacion.py`. Todo lo de aqui son expresiones regulares y
/// tablas. Ni una sola de estas funciones adivina: cuando el texto no encaja devuelven
/// nulo, y nulo significa «no se pudo leer», que es una respuesta correcta. La respuesta
/// incorrecta seria una cedula inventada.
///
/// <para>⛔ Regla permanente 1 del proyecto, aplicada donde mas tienta romperla.</para>
/// </remarks>
public static partial class Normalizacion
{
    // --- Los patrones ------------------------------------------------------------

    [GeneratedRegex(@"\b([A-Z]{4}[0-9]{4})\b")]
    private static partial Regex PatronNumeroDeCaso();

    /// <summary>
    /// El ultimo caracter de la cedula puede ser una LETRA (`DECISIONES.md`, 2026-09-04).
    /// El patron viejo pedia once digitos en 3-4-4 y perdia 2 de las 7 cedulas de los
    /// escaneos reales del dueno: el OCR las leia bien y la validacion las tiraba.
    /// </summary>
    [GeneratedRegex(@"\b([0-9]{3})-([0-9]{4})-([0-9]{3}[0-9A-Za-z])\b")]
    private static partial Regex PatronCedula();

    [GeneratedRegex(@"\b([0-9]{6,})\b")]
    private static partial Regex PatronUnidad();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosSeguidos();

    [GeneratedRegex(@"\b(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})\b")]
    private static partial Regex FechaIso();

    [GeneratedRegex(@"\b(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})\b")]
    private static partial Regex FechaEnNumeros();

    /// <summary>
    /// Los largos que el numero de unidad admite, en un solo sitio.
    /// </summary>
    /// <remarks>
    /// 6 o 7 digitos: `DECISIONES.md` 2026-09-02, «manda el papel». El patron captura 6 o
    /// mas a proposito, para ver una cifra de 8 entera, reconocer que NO es un numero de
    /// unidad y devolver nulo, en vez de recortarle los dos ultimos digitos y dar por
    /// bueno un numero que no existe.
    /// </remarks>
    private static readonly int[] LargosDeUnidad = [6, 7];

    // --- Numero de caso ----------------------------------------------------------

    /// <summary>
    /// 4 letras mayusculas y 4 digitos, o nulo.
    /// </summary>
    /// <remarks>
    /// No se pasa a mayusculas lo que venia en minusculas: si el OCR leyo `casp` puede
    /// haber leido mal tambien las cifras, y forzarlo a `CASP` esconderia el problema en
    /// vez de mandarlo a revision.
    /// </remarks>
    public static string? NormalizarNumeroDeCaso(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var coincidencia = PatronNumeroDeCaso().Match(texto);
        return coincidencia.Success ? coincidencia.Groups[1].Value : null;
    }

    // --- Cedula de miembro -------------------------------------------------------

    /// <summary>
    /// 3 digitos, guion, 4 digitos, guion, 3 digitos y un caracter mas. O nulo.
    /// </summary>
    /// <remarks>
    /// Ese ultimo caracter puede ser un digito o una LETRA: `055-1111-3853` y
    /// `066-2222-133A` son las dos FORMAS, y las dos salen de escaneos reales del dueno.
    /// Palabras suyas el 2026-09-04: «muchas cedulas de miembro tienen una A u otra letra
    /// al final».
    ///
    /// <para>⚠️ Los dos valores de arriba estan SUSTITUIDOS: lo que salio del papel fue la
    /// forma, y la forma esta entera; el ejemplar concreto no vive en el repositorio desde
    /// el 2026-09-07. Ver `EN-CURSO.md`, «Los datos de personas reales salen del
    /// repositorio». Vale para todos los ejemplos de cedula de este archivo.</para>
    ///
    /// <para>La letra se devuelve tal como venia: no se sube a mayuscula, no se cambia
    /// por un digito «parecido» y no se recorta. Cualquiera de las tres cosas es inventar
    /// un dato (regla permanente 1), y una cedula inventada manda a una persona al templo
    /// con la recomendacion equivocada.</para>
    /// </remarks>
    public static string? NormalizarCedula(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var coincidencia = PatronCedula().Match(texto);
        if (!coincidencia.Success) return null;

        return $"{coincidencia.Groups[1].Value}-{coincidencia.Groups[2].Value}-{coincidencia.Groups[3].Value}";
    }

    // --- Nombre de persona -------------------------------------------------------

    /// <summary>
    /// El nombre de la persona sin la cedula que el OCR le pego delante o detras.
    /// </summary>
    /// <remarks>
    /// <para><b>El caso medido</b>, en los siete escaneos del dueño: el OCR devolvio
    /// «Ejemplo, Daniel Jr. Damian Dorian |055-1111-3853 Verified √» como una sola caja.
    /// Cuando eso pasa, el nombre y la cedula viven en la misma linea, y sin quitarla el
    /// nombre de la persona entra a la base con su cedula dentro. Palabras del dueño el
    /// 2026-09-07: «siempre debe ser el nombre y debajo la cedula de miembro de la persona».
    /// </para>
    ///
    /// <para>⛔ <b>Esto NO es arreglar texto leido</b> (regla permanente 1). No se corrige
    /// ni una letra ni se adivina nada: se quita un trozo que tiene la forma exacta de una
    /// cedula, porque ese trozo se va a su propia columna. Es la misma operacion que
    /// <see cref="NormalizarUnidad"/> lleva haciendo desde el principio con el nombre y el
    /// numero del barrio, que tambien vienen pegados en una sola linea del papel.</para>
    ///
    /// <para>Se aplica SOLO al leer una fila del papel, no a lo que Miguel teclea: ver
    /// <c>Extraccion.NormalizadorDe</c>. Un nombre escrito a mano no trae una cedula dentro,
    /// y quitarle algo a lo que una persona escribio seria otra cosa.</para>
    /// </remarks>
    public static string? NombreSinLaCedula(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        string sinCedula = PatronCedula().Replace(texto, " ");
        string junto = EspaciosSeguidos().Replace(sinCedula, " ").Trim(' ', '|', '-', '–', ',', '\t');
        return junto.Length == 0 ? null : junto;
    }

    // --- Unidad ------------------------------------------------------------------

    /// <summary>
    /// El numero de unidad y el resto del texto. Cualquiera de los dos puede ser nulo.
    /// </summary>
    /// <remarks>
    /// Una cifra de otro largo NO se recorta ni se rellena: vuelve nula y el texto crudo
    /// viaja aparte para que se vea que habia algo.
    /// </remarks>
    public static (string? Numero, string? Nombre) NormalizarUnidad(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);

        var coincidencia = PatronUnidad().Match(texto);
        if (!coincidencia.Success)
        {
            string suelto = texto.Trim();
            return (null, suelto.Length == 0 ? null : suelto);
        }

        string digitos = coincidencia.Groups[1].Value;
        string nombre = (texto[..coincidencia.Index] + texto[(coincidencia.Index + coincidencia.Length)..])
            .Trim(' ', '-', '–', '\t');
        string? numero = LargosDeUnidad.Contains(digitos.Length) ? digitos : null;

        return (numero, nombre.Length == 0 ? null : nombre);
    }

    /// <summary>Solo el numero de unidad, o nulo.</summary>
    /// <remarks>
    /// Existe aparte porque el numero y el nombre se corrigen por separado en los
    /// formularios reales: hay anotaciones que reescriben el nombre y dejan el numero
    /// como estaba.
    /// </remarks>
    public static string? NormalizarNumeroDeUnidad(string? texto) => NormalizarUnidad(texto).Numero;

    /// <summary>Solo el nombre de la unidad, sin su numero, o nulo.</summary>
    public static string? NormalizarNombreDeUnidad(string? texto) => NormalizarUnidad(texto).Nombre;

    // --- Templo ------------------------------------------------------------------

    /// <summary>
    /// El nombre del templo tal como esta escrito, con los espacios juntados.
    /// </summary>
    /// <remarks>
    /// <b>No hay catalogo y no se corrige nada</b> (regla permanente 1). No se compara
    /// contra una lista de templos ni se elige «el mas parecido»: el catalogo es una
    /// decision del dueno que todavia no ha tomado, y hasta entonces un templo mal leido
    /// tiene que verse mal leido y no convertido en otro que si esta en la lista.
    /// </remarks>
    public static string? NormalizarNombreDelTemplo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        string junto = EspaciosSeguidos().Replace(texto, " ").Trim();
        return junto.Length == 0 ? null : junto;
    }

    // --- Fecha -------------------------------------------------------------------

    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["ene"] = 1, ["january"] = 1, ["jan"] = 1,
        ["febrero"] = 2, ["feb"] = 2, ["february"] = 2,
        ["marzo"] = 3, ["mar"] = 3, ["march"] = 3,
        ["abril"] = 4, ["abr"] = 4, ["april"] = 4, ["apr"] = 4,
        ["mayo"] = 5, ["may"] = 5,
        ["junio"] = 6, ["jun"] = 6, ["june"] = 6,
        ["julio"] = 7, ["jul"] = 7, ["july"] = 7,
        ["agosto"] = 8, ["ago"] = 8, ["august"] = 8, ["aug"] = 8,
        ["septiembre"] = 9, ["setiembre"] = 9, ["september"] = 9, ["sept"] = 9, ["sep"] = 9,
        ["octubre"] = 10, ["oct"] = 10, ["october"] = 10,
        ["noviembre"] = 11, ["nov"] = 11, ["november"] = 11,
        ["diciembre"] = 12, ["dic"] = 12, ["december"] = 12, ["dec"] = 12,
    };

    private const string SufijoOrdinal = "(?:st|nd|rd|th)?";

    private static readonly string NombresDeMes =
        string.Join("|", Meses.Keys.OrderByDescending(nombre => nombre.Length));

    /// <summary>«September 8, 2026», «Sept-7, 2026», «September 2nd, 2026».</summary>
    private static readonly Regex MesDiaAnio = new(
        $@"\b({NombresDeMes})\.?\s*[-\s]\s*(\d{{1,2}}){SufijoOrdinal}\s*,?\s*(\d{{4}})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>«8 Sept 2026», «14 de marzo de 2027». Los dos «de» son opcionales: el OCR se come uno.</summary>
    private static readonly Regex DiaMesAnio = new(
        $@"\b(\d{{1,2}}){SufijoOrdinal}\s+(?:de\s+)?({NombresDeMes})\.?\s*(?:de\s+|,\s*)?(\d{{4}})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Un rango —«September 8-11, 2026»— NO es una fecha de viaje: no se sabe cual de los
    /// dos dias vale. Se detecta para poder rechazarlo a proposito en vez de quedarse con
    /// el primero por casualidad. Es el texto real de la cita del templo de los siete.
    /// </summary>
    private static readonly Regex Rango = new(
        $@"\b\d{{1,2}}{SufijoOrdinal}\s*(?:-|–|to|till|until|through)\s*\d{{1,2}}{SufijoOrdinal}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>La fecha en ISO-8601, o nula si el dia no existe en ese mes.</summary>
    private static string? FechaIso8601(int anio, int mes, int dia)
    {
        if (mes < 1 || mes > 12 || anio < 1 || anio > 9999) return null;
        if (dia < 1 || dia > DateTime.DaysInMonth(anio, mes)) return null;
        return new DateOnly(anio, mes, dia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Cual de los dos numeros es el dia y cual el mes, o nada.
    /// </summary>
    /// <remarks>
    /// Solo responde cuando los DIGITOS lo deciden: un numero mayor que 12 no puede ser
    /// un mes, asi que el otro lo es. Cuando los dos son 12 o menos la fecha es ambigua
    /// de verdad —«05-10-2027» es el 5 de octubre o el 10 de mayo— y esta funcion se
    /// niega a elegir. Suponer «dia-mes porque el dueno es de un pais que lo escribe asi»
    /// acertaria la mayoria de las veces; las que fallara mueven una fecha de viaje
    /// varios meses, y es la fecha que decide si un caso sale avisado a tiempo.
    /// </remarks>
    private static (int Dia, int Mes)? DiaYMesSinAdivinar(int primero, int segundo)
    {
        if (primero > 12 && segundo <= 12) return (primero, segundo);
        if (segundo > 12 && primero <= 12) return (segundo, primero);
        return null;
    }

    /// <summary>
    /// La fecha escrita en cifras, o nula si no hay exactamente UNA sin ambiguedad.
    /// </summary>
    /// <remarks>
    /// Se juntan todas las que aparezcan y solo vale si todas dicen lo mismo: dos fechas
    /// distintas en la misma banda son un rango, y un rango no dice cuando se viaja.
    /// </remarks>
    private static string? FechaEnCifras(string texto)
    {
        var encontradas = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in FechaIso().Matches(texto))
        {
            var iso = FechaIso8601(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            if (iso is not null) encontradas.Add(iso);
        }
        foreach (Match m in FechaEnNumeros().Matches(texto))
        {
            var decidido = DiaYMesSinAdivinar(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            if (decidido is null) return null;
            var iso = FechaIso8601(int.Parse(m.Groups[3].Value), decidido.Value.Mes, decidido.Value.Dia);
            if (iso is not null) encontradas.Add(iso);
        }
        return encontradas.Count == 1 ? encontradas.First() : null;
    }

    /// <summary>
    /// «8 Sept 2026» y «08-09-2026» dan «2026-09-08». Nula si no hay UNA fecha clara.
    /// </summary>
    /// <remarks>
    /// Las cifras se miran ANTES que el rango, y no es un detalle de orden: el detector
    /// de rangos casa con «14-03-2027» —dos numeros de dos cifras con un guion en medio—
    /// y sin esto se comeria todas las fechas del formulario espanol dandolas por rangos.
    /// </remarks>
    public static string? NormalizarFecha(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var enCifras = FechaEnCifras(texto);
        if (enCifras is not null) return enCifras;
        if (Rango.IsMatch(texto)) return null;

        var conMesPrimero = MesDiaAnio.Match(texto);
        if (conMesPrimero.Success)
        {
            return FechaIso8601(
                int.Parse(conMesPrimero.Groups[3].Value),
                Meses[conMesPrimero.Groups[1].Value],
                int.Parse(conMesPrimero.Groups[2].Value));
        }

        var conDiaPrimero = DiaMesAnio.Match(texto);
        if (conDiaPrimero.Success)
        {
            return FechaIso8601(
                int.Parse(conDiaPrimero.Groups[3].Value),
                Meses[conDiaPrimero.Groups[2].Value],
                int.Parse(conDiaPrimero.Groups[1].Value));
        }
        return null;
    }
}
