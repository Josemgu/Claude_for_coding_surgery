using System.Globalization;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// La regla que convierte las tarjetas de Revisar en carpetas: mes → fecha de viaje →
/// unidad. Sin ventana, para poder medirla.
/// </summary>
/// <remarks>
/// <para>Nace de las palabras del dueño del 2026-09-05: «una carpeta con el mes, dentro
/// carpetas con "grupo de septiembre 17", "grupo de septiembre 2", y dentro de las carpetas
/// otras carpetas con el nombre y número de la unidad, y dentro el paquete de personas de
/// la unidad que viajará».</para>
///
/// <para><b>El orden lo manda su prioridad, no el alfabeto</b>: «no quiero revisar gente que
/// viaja en noviembre estando en septiembre» y «la prioridad son los que viajarán pronto».
/// Lo que viaja antes va arriba, y lo que no tiene fecha va al final porque de eso no se
/// sabe si es pronto — no se le inventa una (regla permanente 1).</para>
///
/// <para>Esta misma estructura es la que se vuelca al disco: ver <see cref="PlanDeVolcado"/>.
/// Se agrupa UNA vez y se usa para las dos cosas, para que la pantalla y las carpetas del
/// disco no puedan discrepar.</para>
/// </remarks>
public static class ArbolDeRevisar
{
    /// <summary>Cómo se llama la carpeta de lo que no tiene fecha de viaje.</summary>
    public const string SinFecha = "Sin fecha de viaje";

    /// <summary>
    /// Lo que la sección de los documentos sin fecha dice en el árbol, con las palabras del
    /// dueño: «una sección que diga "revisar este documento"».
    /// </summary>
    /// <remarks>
    /// <para>Va en la ETIQUETA y no en <see cref="GrupoDeMes.Carpeta"/> a propósito: la
    /// etiqueta es lo que se lee en la pantalla y el nombre de la carpeta es lo que
    /// <see cref="PlanDeVolcado"/> escribe en el disco del dueño. Una carpeta de su
    /// escritorio llamada «Revisar este documento» sería otra cosa, y no la pidió.</para>
    ///
    /// <para>No es un adorno: sin ella, la sección de lo que no tiene fecha se leía igual
    /// que las demás —un nombre y una cifra— y no pedía nada. Él dijo que colocarlo todo
    /// junto «me hace el trabajo difícil».</para>
    /// </remarks>
    public const string LlamadaARevisar = "Revisar este documento";

    /// <summary>Cómo se llama la carpeta de lo que no tiene ni número ni nombre de unidad.</summary>
    public const string SinUnidad = "Sin unidad";

    /// <summary>Los doce meses en español, con su tilde; no se piden a la cultura del sistema.</summary>
    /// <remarks>
    /// Con <c>CultureInfo</c> el nombre del mes depende del idioma de la máquina, y la
    /// interfaz de este programa es en español siempre (CLAUDE.md §1.4). Una carpeta que se
    /// llame «September» en la máquina de al lado es un defecto.
    /// </remarks>
    private static readonly string[] LosMeses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>Agrupa las tarjetas en meses, fechas y unidades, con lo que viaja antes arriba.</summary>
    /// <param name="tarjetas">Las tarjetas del tablero que se está mirando; no puede ser nulo.</param>
    /// <returns>Los meses en orden de viaje, y al final el de lo que no tiene fecha; vacío si no había tarjetas.</returns>
    public static IReadOnlyList<GrupoDeMes> Agrupar(IEnumerable<TarjetaDeDocumento> tarjetas)
    {
        ArgumentNullException.ThrowIfNull(tarjetas);

        return [.. tarjetas
            .GroupBy(t => ClaveDelMes(t.FechaDeViajeIso))
            .Select(porMes => new GrupoDeMes
            {
                MesIso = porMes.Key,
                Carpeta = NombreDelMes(porMes.Key),
                Fechas = FechasDe(porMes),
            })
            .OrderBy(m => m.MesIso.Length == 0 ? 1 : 0)
            .ThenBy(m => m.MesIso, StringComparer.Ordinal)];
    }

    /// <summary>Las carpetas de fecha de un mes, de la más cercana a la más lejana.</summary>
    /// <param name="delMes">Las tarjetas que cayeron en ese mes.</param>
    private static IReadOnlyList<GrupoDeFecha> FechasDe(IEnumerable<TarjetaDeDocumento> delMes)
        => [.. delMes
            .GroupBy(t => ClaveDeLaFecha(t.FechaDeViajeIso))
            .Select(porFecha => new GrupoDeFecha
            {
                FechaIso = porFecha.Key,
                Carpeta = NombreDeLaFecha(porFecha.Key),
                Unidades = UnidadesDe(porFecha),
            })
            .OrderBy(f => f.FechaIso.Length == 0 ? 1 : 0)
            .ThenBy(f => f.FechaIso, StringComparer.Ordinal)];

    /// <summary>Las carpetas de unidad de una fecha, por número y luego por nombre.</summary>
    /// <param name="deLaFecha">Las tarjetas que viajan ese día.</param>
    private static IReadOnlyList<GrupoDeUnidad> UnidadesDe(IEnumerable<TarjetaDeDocumento> deLaFecha)
        => [.. deLaFecha
            .GroupBy(t => (Numero: Limpio(t.UnidadNumero), Nombre: NombreDeLaUnidad(t)))
            .Select(porUnidad => new GrupoDeUnidad
            {
                Numero = porUnidad.Key.Numero,
                Nombre = porUnidad.Key.Nombre,
                Documentos = [.. porUnidad.OrderBy(t => t.NumeroDeCaso, StringComparer.Ordinal)
                                          .ThenBy(t => t.CasoId)],
            })
            .OrderBy(u => u.Numero.Length == 0 ? 1 : 0)
            .ThenBy(u => u.Numero, StringComparer.Ordinal)
            .ThenBy(u => u.Nombre, StringComparer.Ordinal)];

    /// <summary>«2026-09» de una fecha ISO legible; vacío si no se puede leer.</summary>
    /// <param name="fechaIso">La fecha de viaje tal como está en la base.</param>
    private static string ClaveDelMes(string fechaIso)
        => LeerFecha(fechaIso) is DateOnly dia
            ? dia.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>«2026-09-17» de una fecha ISO legible; vacío si no se puede leer.</summary>
    /// <param name="fechaIso">La fecha de viaje tal como está en la base.</param>
    private static string ClaveDeLaFecha(string fechaIso)
        => LeerFecha(fechaIso) is DateOnly dia
            ? dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>«Septiembre 2026», o la carpeta de lo que no tiene fecha.</summary>
    /// <param name="claveDelMes">«2026-09», o vacío para la carpeta de lo que no tiene fecha.</param>
    private static string NombreDelMes(string claveDelMes)
    {
        if (claveDelMes.Length == 0) return SinFecha;

        var mes = int.Parse(claveDelMes[5..7], CultureInfo.InvariantCulture);
        var nombre = LosMeses[mes - 1];
        return $"{char.ToUpperInvariant(nombre[0])}{nombre[1..]} {claveDelMes[..4]}";
    }

    /// <summary>«Grupo del 17 de septiembre», con las palabras que usó el dueño.</summary>
    /// <param name="claveDeLaFecha">«2026-09-17», o vacío para la carpeta de lo que no tiene fecha.</param>
    private static string NombreDeLaFecha(string claveDeLaFecha)
    {
        if (claveDeLaFecha.Length == 0) return SinFecha;

        var dia = int.Parse(claveDeLaFecha[8..10], CultureInfo.InvariantCulture);
        var mes = int.Parse(claveDeLaFecha[5..7], CultureInfo.InvariantCulture);
        return $"Grupo del {dia} de {LosMeses[mes - 1]}";
    }

    /// <summary>Lee una fecha ISO-8601 estricta; lo que no lo sea vuelve nulo y NO lanza.</summary>
    /// <remarks>
    /// La fecha sale del papel por OCR y puede venir con cualquier cosa dentro. Una fecha
    /// ilegible cae en la carpeta «sin fecha de viaje», que es un dato: no se adivina.
    /// </remarks>
    /// <param name="fechaIso">Lo que la base guarda como fecha de viaje; puede ser nulo o cualquier cosa.</param>
    private static DateOnly? LeerFecha(string? fechaIso)
        => DateOnly.TryParseExact(
            fechaIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? dia
            : null;

    /// <summary>
    /// Si de eso sale una fecha de verdad; es lo que decide si un documento hay que mirarlo.
    /// </summary>
    /// <remarks>
    /// Sale de aquí y no se vuelve a escribir en la tarjeta a propósito: si la tarjeta
    /// decidiera por su cuenta si tiene fecha, podría decir que sí de un documento que el
    /// árbol dejó en la sección de «revisar este documento», y los dos estarían mirando el
    /// mismo dato.
    /// </remarks>
    /// <param name="fechaIso">Lo que la base guarda como fecha de viaje.</param>
    public static bool EsFechaLegible(string? fechaIso) => LeerFecha(fechaIso) is not null;

    /// <summary>Un texto sin espacios de sobra, o vacío si no había nada.</summary>
    /// <param name="texto">Lo que se leyó del papel; puede ser nulo.</param>
    private static string Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? string.Empty : texto.Trim();

    /// <summary>
    /// El nombre de la unidad de una tarjeta, o vacío si el papel no traía ninguno.
    /// </summary>
    /// <remarks>
    /// Se lo pide a la tarjeta y no lo vuelve a decidir aquí: el árbol y la tarjeta leen el
    /// MISMO caso, y dos criterios separados sobre qué cuenta como nombre acaban discrepando
    /// sobre la misma unidad. Es la mitad de lo que arregla
    /// <see cref="TarjetaDeDocumento.ComponerUnidad"/>.
    /// </remarks>
    /// <param name="tarjeta">La tarjeta de la que se toma la unidad.</param>
    private static string NombreDeLaUnidad(TarjetaDeDocumento tarjeta) => tarjeta.NombreDeLaUnidad;

    /// <summary>La cifra que lleva cada carpeta en su etiqueta: «· 8 documentos».</summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la cifra.</param>
    /// <param name="cantidad">Cuántos documentos cuelgan de ella.</param>
    internal static string ConSuCifra(string carpeta, int cantidad)
        => $"{carpeta} · {Plural.Con(cantidad, "documento", "documentos")}";

    /// <summary>
    /// Lo que se lee en el árbol, con la llamada delante si esa rama hay que mirarla.
    /// </summary>
    /// <param name="carpeta">El nombre de la carpeta, sin la cifra.</param>
    /// <param name="cantidad">Cuántos documentos cuelgan de ella.</param>
    /// <param name="hayQueRevisarlo">Si es la sección de lo que no tiene fecha de viaje.</param>
    internal static string EtiquetaDe(string carpeta, int cantidad, bool hayQueRevisarlo)
        => hayQueRevisarlo
            ? $"{LlamadaARevisar} · {ConSuCifra(carpeta, cantidad)}"
            : ConSuCifra(carpeta, cantidad);
}

/// <summary>Una carpeta de mes: «Septiembre 2026», con sus fechas de viaje dentro.</summary>
public sealed record GrupoDeMes
{
    /// <summary>«2026-09», o vacío si es la carpeta de lo que no tiene fecha.</summary>
    public string MesIso { get; init; } = string.Empty;

    /// <summary>El nombre de la carpeta, tal cual: «Septiembre 2026».</summary>
    public string Carpeta { get; init; } = string.Empty;

    /// <summary>Las carpetas de fecha de viaje, de la más cercana a la más lejana.</summary>
    public IReadOnlyList<GrupoDeFecha> Fechas { get; init; } = [];

    /// <summary>Cuántos documentos hay en todo el mes.</summary>
    public int CuantosDocumentos => Fechas.Sum(f => f.CuantosDocumentos);

    /// <summary>
    /// Si es la sección que hay que mirar: la de los documentos cuya fecha no se sabe.
    /// </summary>
    /// <remarks>
    /// Se deduce de <see cref="MesIso"/> vacío y no de una marca aparte, porque son lo
    /// mismo: no hay mes que poner cuando la fecha no se pudo leer, y esos son justo los
    /// que el dueño pidió que le pidieran mirarlos.
    /// </remarks>
    public bool HayQueRevisarlo => MesIso.Length == 0;

    /// <summary>Lo que se lee en el árbol de la pantalla, en un renglón.</summary>
    public string Etiqueta => ArbolDeRevisar.EtiquetaDe(Carpeta, CuantosDocumentos, HayQueRevisarlo);
}

/// <summary>Una carpeta de fecha: «Grupo del 17 de septiembre», con sus unidades dentro.</summary>
public sealed record GrupoDeFecha
{
    /// <summary>«2026-09-17», o vacío si es la carpeta de lo que no tiene fecha.</summary>
    public string FechaIso { get; init; } = string.Empty;

    /// <summary>El nombre de la carpeta, tal cual: «Grupo del 17 de septiembre».</summary>
    public string Carpeta { get; init; } = string.Empty;

    /// <summary>Las carpetas de unidad de ese día.</summary>
    public IReadOnlyList<GrupoDeUnidad> Unidades { get; init; } = [];

    /// <summary>Cuántos documentos viajan ese día.</summary>
    public int CuantosDocumentos => Unidades.Sum(u => u.Documentos.Count);

    /// <summary>Si es la carpeta que hay que mirar: la de los que no tienen fecha.</summary>
    public bool HayQueRevisarlo => FechaIso.Length == 0;

    /// <summary>Lo que se lee en el árbol de la pantalla, en un renglón.</summary>
    public string Etiqueta => ArbolDeRevisar.EtiquetaDe(Carpeta, CuantosDocumentos, HayQueRevisarlo);
}

/// <summary>Una carpeta de unidad: «700001 · Castries Branch», con sus documentos dentro.</summary>
public sealed record GrupoDeUnidad
{
    /// <summary>El número de la unidad, o vacío si no se leyó.</summary>
    public string Numero { get; init; } = string.Empty;

    /// <summary>El nombre de la unidad, o vacío si no se leyó.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Los documentos de esa unidad que viajan ese día.</summary>
    public IReadOnlyList<TarjetaDeDocumento> Documentos { get; init; } = [];

    /// <summary>
    /// El nombre de la carpeta: «700001 · Castries Branch», el número Y el nombre.
    /// </summary>
    /// <remarks>
    /// El dueño lo pidió con las dos cosas —«el nombre y número de la unidad»— y hace falta:
    /// dos unidades pueden llamarse igual y el número es lo único que las distingue.
    /// </remarks>
    public string Carpeta => TarjetaDeDocumento.ComponerUnidad(Numero, Nombre, ArbolDeRevisar.SinUnidad);

    /// <summary>Lo que se lee en el árbol de la pantalla, en un renglón.</summary>
    public string Etiqueta => ArbolDeRevisar.ConSuCifra(Carpeta, Documentos.Count);
}
