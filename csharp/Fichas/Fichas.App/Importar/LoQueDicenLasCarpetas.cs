using System.Globalization;
using System.Text.RegularExpressions;

namespace Fichas.App.Importar;

/// <summary>
/// Lo que dicen los nombres de las carpetas por las que llega un PDF.
/// </summary>
/// <remarks>
/// <para>Peticion 8 del dueño, con sus palabras (2026-09-05): <i>«permíteme nombrar las
/// carpetas; y si cargo un grupo de carpetas, que tome la información de las carpetas que
/// cargo, porque muchas veces yo mismo organizo la carpeta y lo único que hace falta es
/// cargarla al sistema»</i>.</para>
///
/// <para>Es <b>la vuelta</b> del volcado a disco: la forma que se reconoce es la misma que
/// <c>Fichas.App.Revisar.ArbolDeRevisar</c> escribe —<c>Septiembre 2026 / Grupo del 8 de
/// septiembre / 700001 · Castries Branch</c>—, para que lo que el programa saca al disco lo
/// pueda volver a leer sin que nadie renombre nada.</para>
///
/// <para>⛔ <b>Esto NO lee el papel y NO decide nada.</b> Devuelve lo que la carpeta dice y
/// quien lo llame decide que hacer con ello. Lo que sale de aqui es de otro origen que lo
/// que sale del escaneo, y por eso viaja aparte y con su nombre: una carpeta mal nombrada no
/// puede mandar sobre lo que dice el documento.</para>
///
/// <para>⛔ <b>Nada se adivina</b> (regla permanente 1). Sin año no se compone fecha aunque
/// se sepan el dia y el mes; un nombre sin numero delante no es una unidad; y una carpeta
/// que no encaja NO rechaza el PDF: se nombra en
/// <see cref="CarpetasNoAprovechadas"/> y la importacion sigue leyendo el papel como
/// siempre.</para>
/// </remarks>
public sealed partial class LoQueDicenLasCarpetas
{
    /// <summary>Como llama el volcado a la carpeta de lo que no tiene fecha de viaje.</summary>
    /// <remarks>
    /// Se repite el literal en vez de leerlo de <c>ArbolDeRevisar</c> a proposito: la
    /// importacion no depende de la pantalla de Revisar. Si el volcado cambiara el nombre,
    /// esta constante se queda vieja, y por eso hay una prueba que la fija.
    /// </remarks>
    public const string CarpetaSinFecha = "Sin fecha de viaje";

    /// <summary>Como llama el volcado a la carpeta de lo que no tiene unidad.</summary>
    public const string CarpetaSinUnidad = "Sin unidad";

    /// <summary>Los nombres de carpeta que no encajaron en ninguna forma, en el orden del camino.</summary>
    private readonly List<string> _noAprovechadas = [];
    /// <summary>Cuántas carpetas del camino dijeron unidad; más de una es un aviso, no un error.</summary>
    private int _cuantasUnidades;

    /// <summary>Solo se construye desde <see cref="Leer"/>: fuera de ahí no hay forma de llenarla.</summary>
    private LoQueDicenLasCarpetas()
    {
    }

    /// <summary>Los doce meses en español, como los escribe el volcado.</summary>
    private static readonly string[] LosMeses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>El mes de viaje que dicen las carpetas, en <c>AAAA-MM</c>, o nulo.</summary>
    public string? MesDeViaje { get; private set; }

    /// <summary>El dia del mes que dice la carpeta de grupo, o nulo.</summary>
    public int? DiaDelMes { get; private set; }

    /// <summary>El mes que dice la carpeta de grupo, de 1 a 12, o nulo.</summary>
    public int? MesDelDia { get; private set; }

    /// <summary>El numero de unidad que dicen las carpetas, o nulo.</summary>
    public string? UnidadNumero { get; private set; }

    /// <summary>El nombre de unidad que dicen las carpetas, o nulo.</summary>
    public string? UnidadNombre { get; private set; }

    /// <summary>Si dos carpetas del camino decian unidad; manda la mas cercana al PDF.</summary>
    public bool HuboMasDeUnaUnidad => _cuantasUnidades > 1;

    /// <summary>Si la carpeta del mes y la del dia dicen meses distintos.</summary>
    public bool MesYDiaSeContradicen =>
        MesDeViaje is not null && MesDelDia is not null && MesDelMesDeViaje() != MesDelDia;

    /// <summary>Las carpetas del camino cuyo nombre no dice nada que se pueda usar.</summary>
    public IReadOnlyList<string> CarpetasNoAprovechadas => _noAprovechadas;

    /// <summary>
    /// La fecha de viaje completa en <c>AAAA-MM-DD</c>, o nulo si no se sabe entera.
    /// </summary>
    /// <remarks>
    /// Hacen falta las dos carpetas: el año solo esta en la del mes y el dia solo en la del
    /// grupo. Con una de las dos se sabe menos, y menos no es una fecha.
    /// </remarks>
    public string? FechaDeViaje { get; private set; }

    /// <summary>Si las carpetas aportaron algun dato aprovechable.</summary>
    public bool DiceAlgo =>
        MesDeViaje is not null || DiaDelMes is not null || UnidadNumero is not null;

    /// <summary>Lo que se aprovecho y lo que no, en una linea para el dueño.</summary>
    public string Explicacion => string.Join(" ", TrozosDeLaExplicacion());

    /// <summary>
    /// Lee las carpetas que hay entre la que Miguel eligio y el PDF.
    /// </summary>
    /// <param name="rutaDelPdf">El archivo, con su ruta completa.</param>
    /// <param name="carpetaElegida">
    /// La carpeta que Miguel eligio en el cuadro de archivos, o nulo si eligio el archivo
    /// suelto. Solo se miran las carpetas de DENTRO de esta: las de fuera son la ruta del
    /// disco y no las nombro el.
    /// </param>
    /// <returns>Nunca nulo: si no hay carpetas entre medias, una lectura que no dice nada.</returns>
    public static LoQueDicenLasCarpetas Leer(string? rutaDelPdf, string? carpetaElegida)
    {
        var dicen = new LoQueDicenLasCarpetas();
        foreach (var carpeta in CarpetasEntre(rutaDelPdf, carpetaElegida))
        {
            dicen.Entender(carpeta);
        }

        dicen.ComponerLaFecha();
        return dicen;
    }

    /// <summary>Los nombres de carpeta que hay entre la elegida y el archivo, de fuera adentro.</summary>
    /// <param name="rutaDelPdf">El archivo con su ruta completa; nulo o vacío devuelve la lista vacía.</param>
    /// <param name="carpetaElegida">La raíz elegida; nula, vacía o que no contenga al PDF devuelve la lista vacía.</param>
    /// <returns>Vacía también cuando el sistema no sabe normalizar una de las dos rutas: eso no tumba la tanda.</returns>
    private static IReadOnlyList<string> CarpetasEntre(string? rutaDelPdf, string? carpetaElegida)
    {
        if (string.IsNullOrWhiteSpace(rutaDelPdf) || string.IsNullOrWhiteSpace(carpetaElegida)) return [];

        string completa, raiz;
        try
        {
            completa = Path.GetFullPath(rutaDelPdf);
            raiz = Path.GetFullPath(carpetaElegida).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception fallo) when (fallo is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Una ruta que el sistema no sabe normalizar no puede tumbar una tanda de 500.
            return [];
        }

        var carpetaDelPdf = Path.GetDirectoryName(completa);
        if (carpetaDelPdf is null) return [];

        var relativa = carpetaDelPdf.TrimEnd(Path.DirectorySeparatorChar);
        if (!relativa.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) return [];

        return relativa[raiz.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Clasifica UN nombre de carpeta; lo que no encaje se anota como sobra.</summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la ruta.</param>
    private void Entender(string carpeta)
    {
        if (EsUnaCarpetaQueDiceQueNoSeSabe(carpeta)) return;
        if (EntenderComoUnidad(carpeta)) return;
        if (EntenderComoMes(carpeta)) return;
        if (EntenderComoDia(carpeta)) return;

        _noAprovechadas.Add(carpeta);
    }

    /// <summary>Las dos carpetas que el propio volcado escribe cuando no sabe el dato.</summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la ruta.</param>
    private static bool EsUnaCarpetaQueDiceQueNoSeSabe(string carpeta)
        => carpeta.Equals(CarpetaSinFecha, StringComparison.OrdinalIgnoreCase)
        || carpeta.Equals(CarpetaSinUnidad, StringComparison.OrdinalIgnoreCase);

    /// <summary>«700001 · Castries Branch», «700001 - Castries Branch» o «700001» a secas.</summary>
    /// <remarks>
    /// Lo que ancla la lectura son los 6 o 7 digitos del principio, que es lo que el esquema
    /// admite en <c>unidad_numero</c>. Sin ellos NO hay unidad, aunque el nombre parezca uno.
    /// </remarks>
    /// <param name="carpeta">El nombre de la carpeta, sin la ruta.</param>
    /// <returns>Si encajó; al encajar pisa la unidad de una carpeta anterior, porque manda la más cercana al PDF.</returns>
    private bool EntenderComoUnidad(string carpeta)
    {
        var encaje = PatronDeUnidad().Match(carpeta);
        if (!encaje.Success) return false;

        _cuantasUnidades++;
        UnidadNumero = encaje.Groups["numero"].Value;
        var nombre = encaje.Groups["nombre"].Value.Trim();
        UnidadNombre = nombre.Length == 0 ? null : nombre;
        return true;
    }

    /// <summary>«Septiembre 2026» o «2026-09».</summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la ruta.</param>
    /// <returns>Si encajó; un nombre de mes que no es de los doce no encaja.</returns>
    private bool EntenderComoMes(string carpeta)
    {
        var iso = PatronDeMesIso().Match(carpeta);
        if (iso.Success)
        {
            MesDeViaje = $"{iso.Groups["anio"].Value}-{iso.Groups["mes"].Value}";
            return true;
        }

        var conNombre = PatronDeMesConNombre().Match(carpeta);
        if (!conNombre.Success) return false;

        var mes = NumeroDelMes(conNombre.Groups["mes"].Value);
        if (mes is null) return false;

        MesDeViaje = $"{conNombre.Groups["anio"].Value}-{mes.Value:D2}";
        return true;
    }

    /// <summary>«Grupo del 8 de septiembre» o «2026-09-08».</summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la ruta.</param>
    /// <returns>Si encajó; la forma ISO aporta también el mes de viaje si ninguna carpeta lo dijo antes.</returns>
    private bool EntenderComoDia(string carpeta)
    {
        var iso = PatronDeFechaIso().Match(carpeta);
        if (iso.Success)
        {
            MesDeViaje ??= $"{iso.Groups["anio"].Value}-{iso.Groups["mes"].Value}";
            MesDelDia = int.Parse(iso.Groups["mes"].Value, CultureInfo.InvariantCulture);
            DiaDelMes = int.Parse(iso.Groups["dia"].Value, CultureInfo.InvariantCulture);
            return true;
        }

        var grupo = PatronDeGrupo().Match(carpeta);
        if (!grupo.Success) return false;

        var mes = NumeroDelMes(grupo.Groups["mes"].Value);
        if (mes is null) return false;

        MesDelDia = mes;
        DiaDelMes = int.Parse(grupo.Groups["dia"].Value, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>El numero de un mes escrito en español, o nulo si no es ninguno.</summary>
    /// <param name="nombre">El mes tal como venía en la carpeta; se compara sin distinguir mayúsculas.</param>
    private static int? NumeroDelMes(string nombre)
    {
        var indice = Array.FindIndex(
            LosMeses, mes => mes.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        return indice < 0 ? null : indice + 1;
    }

    /// <summary>El mes, de 1 a 12, de <see cref="MesDeViaje"/>.</summary>
    private int MesDelMesDeViaje()
        => int.Parse(MesDeViaje![5..7], CultureInfo.InvariantCulture);

    /// <summary>
    /// Junta el año de la carpeta del mes con el dia de la del grupo, si cuadran.
    /// </summary>
    /// <remarks>
    /// Si no cuadran —la de arriba dice septiembre y la de abajo octubre— NO se elige una:
    /// las dos las escribio la misma persona y no hay forma de saber cual quiso decir.
    /// </remarks>
    private void ComponerLaFecha()
    {
        if (MesDeViaje is null || DiaDelMes is null || MesYDiaSeContradicen) return;

        var anio = int.Parse(MesDeViaje[..4], CultureInfo.InvariantCulture);
        var mes = MesDelMesDeViaje();
        if (DiaDelMes.Value < 1 || DiaDelMes.Value > DateTime.DaysInMonth(anio, mes)) return;

        FechaDeViaje = new DateOnly(anio, mes, DiaDelMes.Value)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>Las frases de <see cref="Explicacion"/>, cada una con su hecho.</summary>
    /// <returns>Al menos una frase: si nada se aprovechó y nada sobró, lo dice.</returns>
    private IEnumerable<string> TrozosDeLaExplicacion()
    {
        if (FechaDeViaje is not null) yield return $"Las carpetas dicen que viaja el {FechaDeViaje}.";
        else if (MesDeViaje is not null) yield return $"Las carpetas dicen el mes {MesDeViaje}, sin día.";
        else if (DiaDelMes is not null) yield return $"Las carpetas dicen el día {DiaDelMes} del mes {MesDelDia}, sin año.";

        if (MesYDiaSeContradicen) yield return "La carpeta del mes y la del grupo dicen meses distintos: no se compuso fecha.";
        if (UnidadNumero is not null) yield return $"Las carpetas dicen la unidad {UnidadNumero}{(UnidadNombre is null ? string.Empty : " · " + UnidadNombre)}.";
        if (HuboMasDeUnaUnidad) yield return "Había más de una carpeta de unidad en el camino: manda la más cercana al documento.";
        if (_noAprovechadas.Count > 0)
        {
            yield return "No se pudo aprovechar el nombre de estas carpetas: "
                + string.Join(", ", _noAprovechadas.Select(nombre => $"«{nombre}»")) + ".";
        }

        if (!DiceAlgo && _noAprovechadas.Count == 0) yield return "Las carpetas no dicen nada aprovechable.";
    }

    /// <summary>«700001 · Castries Branch»: 6 o 7 cifras y, si hay, un separador y el nombre.</summary>
    [GeneratedRegex(@"^(?<numero>\d{6,7})(?:\s*[·\-—_]\s*(?<nombre>.+))?$")]
    private static partial Regex PatronDeUnidad();

    /// <summary>«2026-09»: año de cuatro cifras y mes de dos.</summary>
    [GeneratedRegex(@"^(?<anio>\d{4})-(?<mes>0[1-9]|1[0-2])$")]
    private static partial Regex PatronDeMesIso();

    /// <summary>«Septiembre 2026»: el nombre del mes, con o sin tilde, y el año.</summary>
    [GeneratedRegex(@"^(?<mes>[A-Za-zÁÉÍÓÚáéíóú]+)\s+(?<anio>\d{4})$")]
    private static partial Regex PatronDeMesConNombre();

    /// <summary>«2026-09-08»: una fecha ISO entera.</summary>
    [GeneratedRegex(@"^(?<anio>\d{4})-(?<mes>0[1-9]|1[0-2])-(?<dia>0[1-9]|[12]\d|3[01])$")]
    private static partial Regex PatronDeFechaIso();

    /// <summary>«Grupo del 8 de septiembre», como lo escribe el volcado de Revisar.</summary>
    [GeneratedRegex(@"^[Gg]rupo\s+del\s+(?<dia>\d{1,2})\s+de\s+(?<mes>[A-Za-zÁÉÍÓÚáéíóú]+)$")]
    private static partial Regex PatronDeGrupo();
}
