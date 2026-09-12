using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Fichas.Paquetes;

/// <summary>Cuanto mide una hoja, en puntos de PDF.</summary>
/// <param name="Ancho">Ancho en puntos.</param>
/// <param name="Alto">Alto en puntos.</param>
public readonly record struct TamanoDeHoja(double Ancho, double Alto)
{
    /// <summary>A4 en puntos, que es el tamano de los formularios del dueno.</summary>
    /// <remarks>Solo se usa cuando NINGUNA hoja del paquete se pudo leer y no hay de donde copiar.</remarks>
    public static TamanoDeHoja A4 { get; } = new(595.276, 841.89);
}

/// <summary>
/// La hoja que ocupa el sitio de un documento que no se pudo incluir en el paquete.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Decidido por el dueno el 2026-09-05: <i>«Sí, mete la hoja de
/// aviso en el hueco»</i>. Antes, un documento ilegible se quedaba fuera y el PDF salia con una
/// hoja menos — y desde ese hueco hacia abajo, la fila N del Excel dejaba de corresponder con
/// la hoja N del PDF. Medido con los siete escaneos del dueno y dos documentos rotos a
/// proposito: de 7 casos salian 5 hojas y solo las 2 anteriores al primer hueco seguian
/// cuadrando. Un companero contestando la fila 5 mirando la familia de la 4 es exactamente el
/// dano que este programa existe para evitar.</para>
///
/// <para><b>Que lleva escrito, y por que esas tres cosas.</b> El numero de caso, los nombres de
/// quienes viajan en el, y el motivo. Las tres las pidio el dueno, y las tres hacen falta: sin
/// el numero no se sabe cual es, sin los nombres el companero no sabe a que renglon del Excel
/// corresponde —que es todo lo que la hoja viene a resolver—, y sin el motivo no hay nada que
/// hacer con ella.</para>
///
/// <para>⚠️ Esta clase NO decide que hoja falta ni por que: recibe el motivo ya escrito. Aqui
/// solo se dibuja.</para>
/// </remarks>
public static class HojaDeAviso
{
    /// <summary>Margen a cada lado, en puntos. Poco mas de dos centimetros.</summary>
    private const double Margen = 64;

    /// <summary>Tamano del numero de caso, arriba. Es lo que se ve al hojear el paquete.</summary>
    private const double TamanoDeLaCabecera = 26;

    /// <summary>Tamano del titulo que dice lo que pasa.</summary>
    private const double TamanoDelTitulo = 16;

    /// <summary>Tamano del texto corrido.</summary>
    private const double TamanoDelTexto = 12;

    /// <summary>Lo que baja de una linea a la siguiente en el texto corrido.</summary>
    private const double AltoDeLinea = 18;

    /// <summary>Un PDF de UNA hoja que dice que documento falta y por que.</summary>
    /// <param name="hoja">La hoja que tenia que ir aqui: su caso y su gente.</param>
    /// <param name="falta">Por que no pudo ir, ya escrito para leerlo.</param>
    /// <param name="tamano">Cuanto miden las demas hojas del paquete, para medir igual.</param>
    /// <returns>Los bytes de un PDF de una sola página, listo para insertar en el paquete.</returns>
    public static byte[] Dibujar(HojaDelPaquete hoja, HojaQueFalta falta, TamanoDeHoja tamano)
    {
        ArgumentNullException.ThrowIfNull(hoja);
        ArgumentNullException.ThrowIfNull(falta);

        var constructor = new PdfDocumentBuilder();
        var pagina = constructor.AddPage(tamano.Ancho, tamano.Alto);
        var letras = Tipografias.Montar(constructor);

        // El numero de caso arriba y grande: es la cabecera que lleva el propio formulario, y
        // es lo que el companero busca al pasar hojas. Sin ella, esta hoja parece el final del
        // documento y el companero deja de pasar.
        var alto = tamano.Alto - Margen;
        Escribir(pagina, letras.Negrita, letras.ComoSePuede(hoja.NumeroCaso), TamanoDeLaCabecera, Margen, alto);
        alto -= 14;
        pagina.SetStrokeColor(120, 120, 120);
        pagina.DrawLine(new PdfPoint(Margen, alto), new PdfPoint(tamano.Ancho - Margen, alto));

        alto -= 44;
        Escribir(
            pagina, letras.Negrita, letras.ComoSePuede("Este documento no se pudo poner en el paquete"),
            TamanoDelTitulo, Margen, alto);

        var ancho = tamano.Ancho - (2 * Margen);
        alto -= 40;
        alto = Parrafo(pagina, letras, QuienesVan(hoja.Personas), alto, ancho);
        alto -= 12;
        alto = Parrafo(pagina, letras, "Por qué no está: " + falta.Motivo, alto, ancho);
        alto -= 12;
        alto = Parrafo(pagina, letras, falta.Detalle, alto, ancho);

        alto -= 24;
        Parrafo(
            pagina, letras,
            "Esta hoja ocupa el sitio del documento para que cada hoja de este PDF siga siendo "
            + "su misma fila del Excel. Conteste esa fila igual que las demás; si necesita ver "
            + "el papel, pídaselo a Miguel.",
            alto, ancho);

        return constructor.Build();
    }

    /// <summary>La linea que nombra a quien viaja en ese documento.</summary>
    /// <remarks>
    /// Sin nadie NO se calla el hueco: se dice que no consta. Una hoja de aviso que no nombra a
    /// nadie y tampoco lo advierte se lee como si el documento no llevara gente, que es un dato
    /// falso.
    /// </remarks>
    /// <param name="personas">Los nombres de la hoja; los vacíos se saltan.</param>
    private static string QuienesVan(IReadOnlyList<string> personas)
    {
        var conNombre = personas.Where(nombre => !string.IsNullOrWhiteSpace(nombre)).ToList();
        if (conNombre.Count == 0)
            return "No consta quién viaja en este documento.";

        var etiqueta = conNombre.Count == 1 ? "Quien viaja en él: " : "Quienes viajan en él: ";
        return etiqueta + string.Join(", ", conNombre);
    }

    /// <summary>Escribe una linea ya lista, con la letra que se pueda usar.</summary>
    /// <param name="pagina">La página en construcción.</param>
    /// <param name="letra">La tipografía ya montada en el documento.</param>
    /// <param name="texto">La línea, ya pasada por <see cref="Tipografias.ComoSePuede"/>.</param>
    /// <param name="tamano">Cuerpo de la letra en puntos.</param>
    /// <param name="izquierda">Coordenada <c>x</c> del inicio, en puntos desde el borde izquierdo.</param>
    /// <param name="alto">Coordenada <c>y</c> de la línea base, en puntos desde el pie de la hoja.</param>
    private static void Escribir(
        PdfPageBuilder pagina, PdfDocumentBuilder.AddedFont letra, string texto, double tamano, double izquierda, double alto)
        => pagina.AddText(texto, tamano, new PdfPoint(izquierda, alto), letra);

    /// <summary>Escribe un parrafo partiendolo por palabras, y devuelve a que altura se quedo.</summary>
    /// <remarks>
    /// Se parte a mano porque un PDF no ajusta texto solo: una linea mas larga que la hoja se
    /// sale por el borde y se pierde. Se mide con la propia tipografia que se va a usar, que es
    /// la unica medida que vale.
    /// </remarks>
    /// <param name="pagina">La página en construcción.</param>
    /// <param name="letras">Las tipografías montadas; se escribe con la normal.</param>
    /// <param name="texto">El párrafo entero, con tildes: aquí se le pasa <see cref="Tipografias.ComoSePuede"/>.</param>
    /// <param name="alto">La línea base de la primera línea, en puntos desde el pie.</param>
    /// <param name="ancho">El ancho útil entre márgenes.</param>
    /// <returns>La altura donde iría la línea siguiente, ya descontado el último salto.</returns>
    private static double Parrafo(PdfPageBuilder pagina, Tipografias letras, string texto, double alto, double ancho)
    {
        foreach (var linea in PartirEnLineas(pagina, letras, letras.ComoSePuede(texto), ancho))
        {
            Escribir(pagina, letras.Normal, linea, TamanoDelTexto, Margen, alto);
            alto -= AltoDeLinea;
        }
        return alto;
    }

    /// <summary>Parte el texto en lineas que caben en ese ancho.</summary>
    /// <param name="pagina">La página, que es quien sabe medir texto.</param>
    /// <param name="letras">Las tipografías montadas; se mide con la normal.</param>
    /// <param name="texto">El párrafo ya sin tildes si hace falta; se parte por espacios.</param>
    /// <param name="ancho">El ancho útil en puntos.</param>
    /// <returns>Las líneas en orden; vacía si el texto no tenía palabras.</returns>
    private static List<string> PartirEnLineas(
        PdfPageBuilder pagina, Tipografias letras, string texto, double ancho)
    {
        var lineas = new List<string>();
        var enCurso = string.Empty;

        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var intento = enCurso.Length == 0 ? palabra : enCurso + " " + palabra;
            if (enCurso.Length > 0 && MideMasDe(pagina, letras.Normal, intento, ancho))
            {
                lineas.Add(enCurso);
                enCurso = palabra;
            }
            else
            {
                enCurso = intento;
            }
        }

        if (enCurso.Length > 0) lineas.Add(enCurso);
        return lineas;
    }

    /// <summary>Si ese texto se sale del ancho dado.</summary>
    /// <remarks>
    /// Una palabra sola mas larga que la hoja —una ruta de archivo sin espacios— se deja
    /// sobresalir en vez de partirla: partir una ruta por la mitad la vuelve inservible para
    /// buscarla, y quien lee la hoja la necesita entera.
    /// </remarks>
    /// <param name="pagina">La página, que es quien sabe medir texto.</param>
    /// <param name="letra">La tipografía con la que se va a escribir.</param>
    /// <param name="texto">La línea candidata.</param>
    /// <param name="ancho">El ancho útil en puntos.</param>
    /// <returns>Cierto si el borde derecho del último glifo pasa del ancho; falso también para un texto vacío.</returns>
    private static bool MideMasDe(PdfPageBuilder pagina, PdfDocumentBuilder.AddedFont letra, string texto, double ancho)
    {
        var escritas = pagina.MeasureText(texto, TamanoDelTexto, new PdfPoint(0, 0), letra);
        return escritas.Count > 0 && escritas[^1].GlyphRectangle.Right > ancho;
    }

    /// <summary>
    /// Las dos letras con las que se dibuja la hoja, y si aguantan las tildes.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Las Standard 14 de PdfPig NO escriben tildes.</b> Medido el 2026-09-05:
    /// <c>MeasureText</c> con Helvetica levanta
    /// <c>InvalidOperationException: The font does not contain a character: 'ó' (0xF3)</c>. Con
    /// ellas, «Por qué no está» tumbaba la hoja entera.</para>
    ///
    /// <para>Por eso se incrusta Arial, leida de la carpeta de fuentes del propio Windows. NO
    /// se anade ningun archivo al programa y NO se sale a la red: se lee lo que la maquina ya
    /// tiene. Su permiso de incrustacion se comprobo leyendo el <c>fsType</c> de la tabla
    /// <c>OS/2</c> de <c>arial.ttf</c> y <c>arialbd.ttf</c>: vale <b>8</b>, que es «editable
    /// embedding» — incrustarla en un documento que se envia esta permitido.</para>
    ///
    /// <para>Si Arial no estuviera, la hoja NO se pierde: se cae a Helvetica y se le quitan las
    /// tildes al texto. Un aviso sin tildes se lee; una hoja que no sale devuelve el
    /// desplazamiento que todo esto viene a arreglar.</para>
    /// </remarks>
    private sealed class Tipografias
    {
        /// <summary>La carpeta de fuentes de Windows (<c>C:\Windows\Fonts</c> normalmente), resuelta por el sistema y no escrita a mano.</summary>
        private static readonly string CarpetaDeFuentes =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        /// <summary>Cierto con Arial incrustada; falso en el respaldo de Helvetica, que no sabe escribir «ó».</summary>
        private readonly bool _aguantaLasTildes;

        /// <summary>Solo desde <see cref="Montar"/>: las dos letras ya añadidas al documento y si aguantan tildes.</summary>
        /// <param name="normal">La letra del texto corrido.</param>
        /// <param name="negrita">La letra de la cabecera y del título.</param>
        /// <param name="aguantaLasTildes">Cierto si <paramref name="normal"/> y <paramref name="negrita"/> tienen los glifos acentuados.</param>
        private Tipografias(PdfDocumentBuilder.AddedFont normal, PdfDocumentBuilder.AddedFont negrita, bool aguantaLasTildes)
        {
            Normal = normal;
            Negrita = negrita;
            _aguantaLasTildes = aguantaLasTildes;
        }

        /// <summary>La letra del texto corrido, ya añadida al documento.</summary>
        internal PdfDocumentBuilder.AddedFont Normal { get; }

        /// <summary>La letra de la cabecera y del título, ya añadida al documento.</summary>
        internal PdfDocumentBuilder.AddedFont Negrita { get; }

        /// <summary>Monta Arial si se puede; si no, Helvetica.</summary>
        /// <param name="constructor">El documento en construcción, al que se añaden las letras.</param>
        /// <returns>Arial normal y negrita incrustadas si las dos se leyeron; si falta cualquiera, Helvetica y Helvetica-Bold sin tildes.</returns>
        internal static Tipografias Montar(PdfDocumentBuilder constructor)
        {
            var normal = Incrustar(constructor, "arial.ttf");
            var negrita = Incrustar(constructor, "arialbd.ttf");
            if (normal is not null && negrita is not null)
                return new Tipografias(normal, negrita, aguantaLasTildes: true);

            return new Tipografias(
                constructor.AddStandard14Font(Standard14Font.Helvetica),
                constructor.AddStandard14Font(Standard14Font.HelveticaBold),
                aguantaLasTildes: false);
        }

        /// <summary>El texto tal cual, o sin tildes si la letra que hay no las sabe escribir.</summary>
        /// <param name="texto">Cualquier texto que vaya a la hoja.</param>
        internal string ComoSePuede(string texto) => _aguantaLasTildes ? texto : SinTildes(texto);

        /// <summary>
        /// Lee un <c>.ttf</c> de la carpeta de fuentes y lo incrusta, o nulo si no se pudo
        /// (no existe, no se puede leer, o PdfPig no lo acepta).
        /// </summary>
        /// <param name="constructor">El documento en construcción.</param>
        /// <param name="archivo">El nombre del archivo dentro de <see cref="CarpetaDeFuentes"/>, por ejemplo <c>arial.ttf</c>.</param>
        private static PdfDocumentBuilder.AddedFont? Incrustar(PdfDocumentBuilder constructor, string archivo)
        {
            try
            {
                return constructor.AddTrueTypeFont(File.ReadAllBytes(Path.Combine(CarpetaDeFuentes, archivo)));
            }
            catch (Exception causa) when (
                causa is IOException or UnauthorizedAccessException or ArgumentException
                      or InvalidOperationException or NotSupportedException)
            {
                // No se silencia un fallo: se devuelve el nulo, y quien llama se cae a
                // Helvetica sin tildes en vez de quedarse sin hoja de aviso.
                return null;
            }
        }

        /// <summary>«Por qué no está» → «Por que no esta». Solo para el camino de respaldo.</summary>
        /// <remarks>Además de las marcas diacríticas, cambia la eñe por ene y las comillas angulares por rectas: tampoco están en Helvetica.</remarks>
        /// <param name="texto">El texto con tildes.</param>
        private static string SinTildes(string texto)
        {
            var descompuesto = texto.Normalize(NormalizationForm.FormD);
            var salida = new StringBuilder(descompuesto.Length);
            foreach (var letra in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(letra) == UnicodeCategory.NonSpacingMark) continue;
                salida.Append(letra switch { 'ñ' => 'n', 'Ñ' => 'N', '«' => '"', '»' => '"', _ => letra });
            }
            return salida.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
