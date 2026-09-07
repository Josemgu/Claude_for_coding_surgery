namespace Fichas.Reportes.Formato;

/// <summary>
/// Lo que sale igual en TODAS las paginas: la cinta negra de arriba y el pie.
/// </summary>
/// <remarks>
/// Vive aparte de <see cref="Maqueta"/> porque es otra pregunta. Aquella decide donde va cada
/// linea del informe; esta decide lo que se dibuja encima de todas ellas y no depende del
/// contenido. Juntas pasaban de 400 lineas y habia que leerlas enteras para mover un pie.
///
/// Los colores son los del proyecto viejo, leidos de <c>salida/informe.py</c>: NEGRO #151515,
/// TINTA #16161A, ROJO #D0342C, VERDE #1F8A5F, GRIS #6F6F76, TENUE #A0A0A8, LINEA #E6E6E6, y
/// el #B9B9BE del subtitulo de la cinta. El rojo y el verde no son adorno: son la diferencia
/// entre «esto salio mal» y «esto salio bien» vista desde el otro lado de una mesa.
/// </remarks>
internal static class Marco
{
    internal const string Negro = "#151515";
    internal const string Tinta = "#16161A";
    internal const string Rojo = "#D0342C";
    internal const string Verde = "#1F8A5F";
    internal const string Gris = "#6F6F76";
    internal const string Tenue = "#A0A0A8";
    internal const string LineaFina = "#E6E6E6";

    private const string BlancoDeLaCinta = "#FFFFFF";
    private const string GrisDeLaCinta = "#B9B9BE";

    internal const int AltoDeLaCinta = 42;
    internal const int AlturaDeLaRayaDelPie = 34;

    private const int AlturaDelTextoDeLaCinta = 16;
    private const int AlturaDelTextoDelPie = 22;
    private const int TamanoDeLaCinta = 11;
    private const int TamanoDelPie = 7;

    private const string RotuloDeLaCinta = "Preparación para el templo";

    // El pie vuelve a la frase del viejo. Estuvo diciendo «La preparación es la que consta...»
    // desde el 2026-09-03 (criterio C8-2), para que «verificado» no significara dos cosas en el
    // mismo programa. El dueno pidio el informe del viejo el 2026-09-04 y con el vuelven sus
    // rotulos, asi que el pie tiene que decir lo mismo que las columnas o se contradicen. Lo
    // que impide que la confusion vuelva a colarse en silencio es la nota de
    // SeccionesDeDireccion, que dice DENTRO del informe que aqui son los seis pasos y no la
    // firma de Miguel.
    private const string NotaDelPie =
        "Lo verificado es lo que consta en el sistema del líder, no en el formulario.";

    /// <summary>La cinta de arriba y el pie, para el subtitulo que se le diga.</summary>
    /// <remarks>
    /// Devuelve una funcion porque el pie lleva el numero de pagina, y ese cambia. El «de M» no
    /// lo tenia el viejo y se anade aqui: un informe que se imprime y se reparte tiene que decir
    /// si esta entero, y una pagina 3 suelta sin el total no dice si faltan dos.
    /// </remarks>
    internal static MarcoDePagina DeLaPagina(string subtitulo, int margen)
    {
        return (numero, total) =>
        {
            IReadOnlyList<Adorno> adornos =
            [
                new Rectangulo(
                    0, EscritorDePdf.AltoPagina - AltoDeLaCinta,
                    EscritorDePdf.AnchoPagina, AltoDeLaCinta, Negro),
                new Raya(margen, AlturaDeLaRayaDelPie, EscritorDePdf.AnchoPagina - margen, LineaFina, 0.5),
            ];

            double yDeLaCinta = EscritorDePdf.AltoPagina - AltoDeLaCinta + AlturaDelTextoDeLaCinta;
            var paginaDe = $"Página {numero} de {total}";

            IReadOnlyList<(double Y, Linea Linea)> lineas =
            [
                (yDeLaCinta, new Linea(true, TamanoDeLaCinta,
                    [new Trazo(margen, RotuloDeLaCinta, BlancoDeLaCinta)], false)),
                (yDeLaCinta, new Linea(false, TamanoDelPie,
                    [new Trazo(ADerecha(subtitulo, TamanoDelPie, margen), subtitulo, GrisDeLaCinta)], false)),
                (AlturaDelTextoDelPie, new Linea(false, TamanoDelPie,
                    [new Trazo(margen, NotaDelPie, Tenue)], false)),
                (AlturaDelTextoDelPie, new Linea(false, TamanoDelPie,
                    [new Trazo(ADerecha(paginaDe, TamanoDelPie, margen), paginaDe, Tenue)], false)),
            ];

            return (adornos, lineas);
        };
    }

    /// <summary>Donde empezar un texto para que acabe en el margen derecho.</summary>
    /// <remarks>
    /// Es una estimacion: Helvetica es de ancho variable y aqui se usa el ancho medio, el mismo
    /// con el que se reparten las columnas. Basta para el pie —si sobra o falta un punto no se
    /// nota— y evita incrustar las metricas de la fuente solo para colocar un numero de pagina.
    /// </remarks>
    private static int ADerecha(string texto, int tamano, int margen)
    {
        var ancho = texto.Length * tamano * Maqueta.ProporcionDelAnchoDeCaracter;
        return Math.Max(margen, (int)(EscritorDePdf.AnchoPagina - margen - ancho));
    }
}
