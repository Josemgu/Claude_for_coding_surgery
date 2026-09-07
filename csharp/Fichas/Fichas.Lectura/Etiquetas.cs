using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Fichas.Lectura;

/// <summary>
/// Que dice cada etiqueta impresa del formulario, en cada idioma en que llega.
/// </summary>
/// <remarks>
/// Portado de `extraccion/etiquetas.py`, que es la especificacion. El mismo papel llega
/// en ingles y en espanol —el pie del formulario en blanco dice «Traduccion de General
/// Temple Patron Assistance Fund Request and Approval Form. Spanish»—, y sin las dos
/// formas no casa ni una etiqueta y la pagina entera sale vacia.
///
/// <para><b>Un campo NO es su etiqueta.</b> La clave con la que el resto del programa se
/// refiere a un campo es un nombre en espanol, no la cadena impresa, porque hay dos
/// cadenas impresas para lo mismo. Ese nombre es tambien lo que se le ensena a Miguel
/// cuando una etiqueta no aparece, y por eso esta en espanol.</para>
///
/// <para>Medido el 2026-09-03 sobre nueve paginas reales: el cruce maximo entre
/// CUALQUIER etiqueta inglesa y CUALQUIER espanola es <b>0,357</b>, muy por debajo del
/// 0,85 que exige el ancla. Por eso las dos formas conviven en la misma familia.
/// `PruebaDeAnclasYBandas` lo vigila.</para>
/// </remarks>
public static class Etiquetas
{
    /// <summary>La fecha en que se viaja al templo. Es la que decide si un caso se avisa.</summary>
    public const string CampoDeLaFechaDeViaje = "fecha de viaje al templo";

    /// <summary>La fecha de vuelta. No se extrae, pero hace falta como rival del ancla de ida.</summary>
    public const string CampoDeLaFechaDeRegreso = "fecha de regreso del templo";

    /// <summary>Nombre y numero de la unidad del barrio o rama.</summary>
    public const string CampoDeLaUnidad = "nombre y número de unidad del barrio o rama";

    /// <summary>Nombre y numero de la unidad de la estaca o distrito. Solo hace de rival.</summary>
    public const string CampoDeLaEstaca = "nombre y número de unidad de la estaca o distrito";

    /// <summary>La cabecera de la columna de nombres del bloque de personas.</summary>
    public const string CampoDeLosNombres = "nombre(s) de pila";

    /// <summary>La cabecera de la columna de cedulas de miembro.</summary>
    public const string CampoDeLaCedula = "número de cédula de miembro";

    /// <summary>El nombre del templo. Tambien cierra el bloque de personas por abajo.</summary>
    public const string CampoDelNombreDelTemplo = "nombre del templo";

    /// <summary>La fecha de la cita del templo. No es la de viaje: suele ser un RANGO.</summary>
    public const string CampoDeLaCitaDelTemplo = "fecha de la cita del templo";

    /// <summary>La cabecera de la tabla de costes. Cierra el bloque de personas.</summary>
    public const string CampoDeLosCostos = "costos asociados";

    private static readonly Dictionary<string, (string EnIngles, string EnEspanol)> FormasDeCadaCampo = new()
    {
        [CampoDeLaFechaDeViaje] = ("Date traveling to the temple", "Fecha de viaje al templo"),
        [CampoDeLaFechaDeRegreso] = ("Date traveling home from the temple", "Fecha de regreso del templo"),
        [CampoDeLaUnidad] = ("Ward/Branch Name and Unit Number", "Nombre y número de unidad del barrio / rama"),
        [CampoDeLaEstaca] = ("Stake/District Name and Unit Number", "Nombre y número de unidad de la estaca / distrito"),
        [CampoDeLosNombres] = ("Full Name(s)", "Nombre(s) de pila"),
        [CampoDeLaCedula] = ("Membership Record Number", "Número de cédula de miembro"),
        [CampoDelNombreDelTemplo] = ("Temple Name", "Nombre del templo"),
        [CampoDeLaCitaDelTemplo] = ("Temple Appointment Date", "Fecha de la cita del templo"),
        [CampoDeLosCostos] = ("Associated Costs", "Costos asociados"),
    };

    /// <summary>Los nueve campos del formulario, en el orden en que se declararon.</summary>
    public static IReadOnlyList<string> CamposDelFormulario { get; } = FormasDeCadaCampo.Keys.ToArray();

    /// <summary>
    /// Las etiquetas que cierran el bloque de personas por abajo.
    /// </summary>
    /// <remarks>
    /// Son varias porque ninguna aparece en las nueve paginas de referencia: «Temple
    /// Name» falto en dos escaneos malos y «Temple Appointment Date» en uno. Se toma la
    /// primera que aparezca por debajo de las cabeceras.
    /// </remarks>
    public static IReadOnlyList<string> CamposQueCierranLasPersonas { get; } =
        [CampoDelNombreDelTemplo, CampoDeLaCitaDelTemplo, CampoDeLosCostos];

    /// <summary>Las cadenas impresas que valen para ese campo, en los dos idiomas.</summary>
    public static IReadOnlyList<string> FormasDe(string campo)
    {
        var formas = FormasDeCadaCampo[campo];
        return [formas.EnIngles, formas.EnEspanol];
    }

    /// <summary>Como se llama esa etiqueta en un formulario ingles.</summary>
    public static string FormaInglesaDe(string campo) => FormasDeCadaCampo[campo].EnIngles;

    /// <summary>
    /// Como se llama esa etiqueta en un formulario espanol.
    /// </summary>
    /// <remarks>
    /// Existe para la INTERFAZ y no para la extraccion: la extraccion prueba todas las
    /// formas y gana la que mas se parezca, pero la pantalla escribe UNA, y por la regla
    /// permanente 4 es la espanola sea cual sea el idioma del papel.
    /// </remarks>
    public static string FormaEspanolaDe(string campo) => FormasDeCadaCampo[campo].EnEspanol;

    private static readonly Regex EspaciosSeguidos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Baja a minusculas, quita las tildes y junta los espacios de sobra.
    /// </summary>
    /// <remarks>
    /// Es SOLO para comparar. El valor que se guarda conserva sus tildes: aqui no se
    /// corrige nada de lo leido, se decide a que etiqueta se parece.
    ///
    /// <para>Quitar las tildes no es cosmetico y esta medido (2026-09-03): el OCR
    /// devuelve «Numero de cedula de miembro» sin acentos con frecuencia, y sin
    /// normalizar esa lectura se parece 0,926 a la etiqueta buena. Sigue pasando el
    /// 0,85, pero gasta casi toda la holgura en las tildes en vez de gastarla en el
    /// error de lectura de verdad. Normalizando da 1,000.</para>
    /// </remarks>
    public static string NormalizarParaComparar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var descompuesto = texto.Normalize(NormalizationForm.FormKD);
        var sinTildes = new StringBuilder(descompuesto.Length);
        foreach (var letra in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(letra);
            }
        }
        return EspaciosSeguidos.Replace(sinTildes.ToString(), " ").Trim().ToLowerInvariant();
    }
}
