namespace Fichas.Reportes.Formato;

/// <summary>
/// La codificacion que entienden las fuentes basicas de PDF, y cuantos caracteres pierde.
/// </summary>
/// <remarks>
/// ⚠️ <c>/WinAnsiEncoding</c> cubre el alfabeto latino con tildes y enes —que es lo que traen
/// los formularios— pero no cubre todo Unicode. Un caracter que no quepa sale como <c>?</c>,
/// y aqui se CUENTA para que el PDF pueda avisar arriba de cuantos fueron. No se calla: un
/// nombre alterado en silencio es un dato falso sobre una persona.
///
/// <b>La tabla se escribe a mano y no se pide a <c>Encoding.GetEncoding(1252)</c>.</b> Esa
/// pagina de codigos NO viene en el marco de .NET: llega con el paquete
/// <c>System.Text.Encoding.CodePages</c>, y traer un paquete entero para 27 caracteres iria
/// contra la regla permanente 3 («sin dependencias que inflen sin aportar», requisito 7 del
/// dueno). WinAnsi es Latin-1 salvo el tramo 0x80–0x9F, y ese tramo son las 27 lineas de
/// <see cref="TramoAlto"/>. Latin-1 si viene de serie.
///
/// Se recorre por elementos de texto y no por <c>char</c> para que un par sustituto —un
/// emoji, que ocupa dos <c>char</c>— cuente como UN caracter perdido y no como dos.
/// </remarks>
public static class WinAnsi
{
    /// <summary>Los 27 caracteres en los que WinAnsi se separa de Latin-1 (0x80–0x9F).</summary>
    private static readonly Dictionary<char, byte> TramoAlto = new()
    {
        ['€'] = 0x80, // €
        ['‚'] = 0x82, // ‚
        ['ƒ'] = 0x83, // ƒ
        ['„'] = 0x84, // „
        ['…'] = 0x85, // …
        ['†'] = 0x86, // †
        ['‡'] = 0x87, // ‡
        ['ˆ'] = 0x88, // ˆ
        ['‰'] = 0x89, // ‰
        ['Š'] = 0x8A, // Š
        ['‹'] = 0x8B, // ‹
        ['Œ'] = 0x8C, // Œ
        ['Ž'] = 0x8E, // Ž
        ['‘'] = 0x91, // ‘
        ['’'] = 0x92, // ’
        ['“'] = 0x93, // “
        ['”'] = 0x94, // ”
        ['•'] = 0x95, // •
        ['–'] = 0x96, // –
        ['—'] = 0x97, // —
        ['˜'] = 0x98, // ˜
        ['™'] = 0x99, // ™
        ['š'] = 0x9A, // š
        ['›'] = 0x9B, // ›
        ['œ'] = 0x9C, // œ
        ['ž'] = 0x9E, // ž
        ['Ÿ'] = 0x9F, // Ÿ
    };

    /// <summary>Codifica a WinAnsi y devuelve los bytes con cuantos caracteres no cupieron.</summary>
    /// <param name="texto">El texto a codificar; un nulo cuenta como vacio.</param>
    /// <returns>Un byte por elemento de texto —«?» donde no cupo— y cuántos no cupieron.</returns>
    public static (byte[] Crudo, int Perdidos) Codificar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return ([], 0);

        var crudo = new List<byte>(texto.Length);
        var perdidos = 0;

        var recorrido = System.Globalization.StringInfo.GetTextElementEnumerator(texto);
        while (recorrido.MoveNext())
        {
            var elemento = (string)recorrido.Current;
            if (Cabe(elemento, out var octeto))
            {
                crudo.Add(octeto);
            }
            else
            {
                // Se sustituye por «?» y se CUENTA. No se traga el fallo: el conteo es
                // justo lo que hace que el informe pueda decir que perdio algo.
                crudo.Add((byte)'?');
                perdidos++;
            }
        }

        return ([.. crudo], perdidos);
    }

    /// <summary>Cuantos caracteres de ese texto no existen en WinAnsi.</summary>
    /// <param name="texto">El texto; un nulo cuenta como vacío y pierde cero.</param>
    public static int Perdidos(string? texto) => Codificar(texto).Perdidos;

    /// <summary>Si ese elemento de texto es un solo caracter que WinAnsi sabe escribir.</summary>
    /// <param name="elemento">Un elemento de texto de Unicode: un <c>char</c>, o varios si es un par sustituto o lleva marca combinante.</param>
    /// <param name="octeto">El byte WinAnsi si cabe; cero si no.</param>
    private static bool Cabe(string elemento, out byte octeto)
    {
        octeto = 0;
        if (elemento.Length != 1) return false;   // Par sustituto o letra con tilde suelta detras.

        var letra = elemento[0];

        // El tramo 0x80–0x9F de Latin-1 son controles y en WinAnsi son otra cosa: un texto
        // que traiga un control ahi NO se escribe, se cuenta como perdido.
        if (letra <= 0x7F || (letra >= 0x00A0 && letra <= 0x00FF))
        {
            octeto = (byte)letra;
            return true;
        }

        return TramoAlto.TryGetValue(letra, out octeto);
    }
}
