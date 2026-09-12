using System.Globalization;

namespace Fichas.App.Actualizacion;

/// <summary>
/// Compara la etiqueta de un Release («v12») con la versión del programa («11») como
/// números y no como texto.
/// </summary>
/// <remarks>
/// <para>Como texto, «v9» es mayor que «v10» y el programa diría que hay versión nueva cuando
/// no la hay. Aquí se quita la «v», se parte por puntos y se compara número a número, con
/// ceros de relleno: «11» y «11.0» son la misma versión.</para>
///
/// <para>Una etiqueta que no es un número («beta», «v11-ensayo») no se interpreta: ni se
/// adivina ni se toma por nueva. Es la regla permanente 1 llevada a este rincón: no se
/// inventa un dato que no está.</para>
/// </remarks>
public static class EtiquetaDeVersion
{
    /// <summary>Si la etiqueta del Release es más nueva que la versión abierta.</summary>
    /// <param name="etiqueta">La etiqueta tal como la da GitHub: «v12».</param>
    /// <param name="versionActual">Lo que dice <c>VersionDelPrograma.Numero</c>: «11».</param>
    /// <returns>Verdadero solo si las dos se entienden y la etiqueta es mayor.</returns>
    public static bool EsMasNuevaQue(string etiqueta, string versionActual)
    {
        var nueva = Partes(etiqueta);
        var actual = Partes(versionActual);
        if (nueva is null || actual is null) return false;

        var largo = Math.Max(nueva.Length, actual.Length);
        for (var i = 0; i < largo; i++)
        {
            var a = i < nueva.Length ? nueva[i] : 0;
            var b = i < actual.Length ? actual[i] : 0;
            if (a != b) return a > b;
        }

        return false;
    }

    /// <summary>Si la etiqueta tiene forma de número: «v12», «12», «v9.1».</summary>
    /// <param name="etiqueta">La etiqueta tal como la da GitHub.</param>
    public static bool SeEntiende(string etiqueta) => Partes(etiqueta) is not null;

    /// <summary>La etiqueta sin la «v» ni los espacios: «v12» → «12».</summary>
    /// <param name="etiqueta">La etiqueta tal como la da GitHub.</param>
    public static string SoloElNumero(string etiqueta)
    {
        var limpia = etiqueta.Trim();
        return limpia.Length > 0 && (limpia[0] == 'v' || limpia[0] == 'V') ? limpia[1..] : limpia;
    }

    /// <summary>Los números de la etiqueta, o nulo si alguna parte no es un entero.</summary>
    /// <param name="etiqueta">La etiqueta o la versión, con o sin «v».</param>
    private static int[]? Partes(string etiqueta)
    {
        var numero = SoloElNumero(etiqueta);
        if (numero.Length == 0) return null;

        var trozos = numero.Split('.');
        var partes = new int[trozos.Length];
        for (var i = 0; i < trozos.Length; i++)
        {
            if (!int.TryParse(trozos[i], NumberStyles.None, CultureInfo.InvariantCulture, out partes[i])) return null;
        }

        return partes;
    }
}
