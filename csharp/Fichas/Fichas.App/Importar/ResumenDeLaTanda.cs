using System.Globalization;
using System.Text;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>
/// Las cifras de toda la tanda, sumadas segun van llegando.
/// </summary>
/// <remarks>
/// Es una clase y no un registro porque va creciendo mientras la tanda corre: la barra de
/// progreso ensena estas mismas cifras al vuelo, y al terminar son las del resumen. Dos
/// cuentas separadas —una para la pantalla y otra para el resumen— serian dos cuentas que
/// pueden no coincidir.
///
/// <para>Portado de <c>importacion/tanda.py</c> (<c>ResumenDeLaTanda</c>), con un cambio
/// que pidio el dueno: el resumen final cabe en UNA linea y lo largo va detras de «ver»
/// (requisito 4, «ni un parrafo en pantalla»). El texto de varias lineas del Python es
/// ahora <see cref="Detalle"/>.</para>
/// </remarks>
public sealed class ResumenDeLaTanda
{
    private readonly List<ResultadoDeUnDocumento> _documentos = [];

    /// <summary>Empieza el recuento de una tanda de tantos documentos.</summary>
    public ResumenDeLaTanda(int totalDeDocumentos) => TotalDeDocumentos = totalDeDocumentos;

    /// <summary>Cuantos documentos se pidieron importar.</summary>
    public int TotalDeDocumentos { get; }

    /// <summary>Cuantos van procesados.</summary>
    public int Documentos { get; private set; }

    /// <summary>Cuantas hojas se han leido.</summary>
    public int Hojas { get; private set; }

    /// <summary>Cuantos casos distintos han entrado.</summary>
    public int Casos { get; private set; }

    /// <summary>Cuantas personas han entrado.</summary>
    public int Personas { get; private set; }

    /// <summary>Cuantas hojas entraron sin numero de caso.</summary>
    public int Pendientes { get; private set; }

    /// <summary>Cuantos casos nacieron repitiendo a otro que ya estaba.</summary>
    public int Duplicados { get; private set; }

    /// <summary>Cuantas hojas no se pudieron leer.</summary>
    public int Ilegibles { get; private set; }

    /// <summary>Si la tanda se paro a mitad por decision de Miguel.</summary>
    public bool Cancelada { get; set; }

    /// <summary>Los casos que nacieron en esta tanda, por su numero interno.</summary>
    /// <remarks>
    /// No es una cifra del resumen: es con lo que la pantalla compone despues las carpetas
    /// que esta tanda va a formar en Revisar y lo que entro sin ninguna persona. Se acota a
    /// LA TANDA a proposito, porque de eso es de lo que habla esa pantalla.
    /// </remarks>
    public IReadOnlyList<long> CasosDeLaTanda => [.. _documentos.SelectMany(uno => uno.CasoIds).Distinct()];

    /// <summary>Los PDF que se intentaron leer en esta tanda, con su ruta entera.</summary>
    public IReadOnlyList<string> RutasDeLaTanda =>
        [.. _documentos.Select(uno => uno.RutaPdf).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Suma lo de un documento y devuelve el mismo resultado, para encadenar.</summary>
    public ResultadoDeUnDocumento Anotar(ResultadoDeUnDocumento resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        _documentos.Add(resultado);
        Documentos++;
        Hojas += resultado.Hojas;
        Casos += resultado.Casos;
        Personas += resultado.Personas;
        Pendientes += resultado.Pendientes;
        Duplicados += resultado.Duplicados;
        Ilegibles += resultado.Ilegibles;
        return resultado;
    }

    /// <summary>
    /// El resumen entero en UNA linea, con las cifras que cambian una decision.
    /// </summary>
    /// <remarks>
    /// Lleva SIEMPRE todas las cifras, tambien cuando alguna es cero. Un resumen que se
    /// calla los ceros obliga a preguntarse si es que no hubo o es que no se cuenta, y esa
    /// duda recae justo sobre los numeros que hay que creerse.
    /// </remarks>
    public string Linea()
    {
        var cabeza = Cancelada
            ? $"Importación detenida en {Documentos} de {TotalDeDocumentos}; lo procesado quedó guardado."
            : $"Importados {Documentos} de {TotalDeDocumentos} "
              + Plural.Palabra(TotalDeDocumentos, "documento", "documentos") + ".";

        return string.Join(" · ",
            cabeza,
            Plural.Con(Casos, "caso", "casos"),
            Plural.Con(Personas, "persona", "personas"),
            Plural.Con(Duplicados, "duplicado avisado", "duplicados avisados"),
            Plural.Con(Ilegibles, "ilegible", "ilegibles"),
            $"{Pendientes} sin número de caso");
    }

    /// <summary>Lo largo: documento a documento, con sus cifras y su fallo si lo hubo.</summary>
    /// <remarks>
    /// Solo se ve al pulsar «ver». Aqui SI caben varias lineas: lo que el requisito 4
    /// prohibe es el parrafo en pantalla, no que exista el detalle.
    /// </remarks>
    public string Detalle()
    {
        var texto = new StringBuilder();
        texto.AppendLine(Linea());
        texto.AppendLine();
        texto.AppendLine($"Hojas leídas: {Hojas}.");
        texto.AppendLine();

        foreach (var documento in _documentos)
        {
            var cifras = documento.Error is not null
                ? $"NO SE PUDO LEER — {documento.Error}"
                : string.Join(", ",
                    Plural.Con(documento.Hojas, "hoja", "hojas") + " →",
                    Plural.Con(documento.Casos, "caso", "casos"),
                    Plural.Con(documento.Personas, "persona", "personas"),
                    Plural.Con(documento.Duplicados, "duplicado", "duplicados"),
                    Plural.Con(documento.Ilegibles, "ilegible", "ilegibles"),
                    documento.Segundos.ToString("F1", CultureInfo.InvariantCulture) + " s");
            texto.AppendLine($"· {Path.GetFileName(documento.RutaPdf)}: {cifras}");
        }

        texto.AppendLine();
        texto.AppendLine(
            "Ningún documento se rechaza. El que repite a otro entra igual, aparte y " +
            "marcado: el caso que ya estaba NO se toca y nada se funde solo. El que no " +
            "se pudo leer deja su renglón con el motivo, y se consulta en «Lo que no entró».");
        return texto.ToString();
    }

}
