using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace Fichas.Paquetes;

/// <summary>Una hoja que tiene que ir en el PDF del paquete: de que caso es y de donde sale.</summary>
/// <param name="NumeroCaso">Como nombrarla si falla; es lo que el dueno reconoce.</param>
/// <param name="RutaPdf">El archivo escaneado del que se recorta.</param>
/// <param name="Pagina">Que hoja de ese archivo, contando desde 1 como el propio PDF.</param>
/// <param name="Personas">
/// Quienes viajan en ese documento. Solo se usan si la hoja NO se puede leer: entonces van
/// escritos en la hoja de aviso que ocupa su sitio, porque un aviso que no dice a quien
/// pertenece deja al companero sin saber que renglon del Excel le corresponde.
/// </param>
public sealed record HojaDelPaquete(string NumeroCaso, string RutaPdf, int Pagina, IReadOnlyList<string> Personas);

/// <summary>Una hoja que no pudo entrar, con el motivo escrito para leerlo.</summary>
/// <param name="NumeroCaso">De que caso era.</param>
/// <param name="Motivo">Por que no entro, en una linea.</param>
/// <param name="Detalle">Lo que hace falta para arreglarlo: el archivo, la hoja, lo que dijo el sistema.</param>
public sealed record HojaQueFalta(string NumeroCaso, string Motivo, string Detalle);

/// <summary>Lo que salio de unir: cuantas hojas entraron, cuales no y si se llego a escribir.</summary>
/// <param name="SeEscribio">Si el archivo quedo en disco.</param>
/// <param name="Hojas">Cuantas hojas tiene el PDF que se escribio.</param>
/// <param name="Faltan">Las que se quedaron fuera, en el orden en que se pidieron.</param>
/// <param name="Fallo">Lo que impidio escribir el archivo entero, o nulo si se escribio.</param>
public sealed record ResultadoDeLaUnion(
    bool SeEscribio,
    int Hojas,
    IReadOnlyList<HojaQueFalta> Faltan,
    string? Fallo);

/// <summary>
/// Junta hojas sueltas de varios PDF escaneados en UN solo archivo, en el orden que se pida.
/// </summary>
/// <remarks>
/// <para><b>Con que se hace y por que con eso.</b> Con <c>PdfPig</c> 0.1.11, licencia Apache-2.0
/// (leida en <c>pdfpig.nuspec</c> del paquete instalado, no de memoria), que YA estaba en el
/// arbol —<c>Fichas.Lectura</c> la usa para leer las anotaciones del formulario— y por tanto YA
/// viaja en el paquete publicado. Anadirla aqui no mete ni un archivo nuevo en la entrega. No se
/// trajo ninguna biblioteca de fuera: la regla del proyecto es mirar lo que hay antes.</para>
///
/// <para>⚠️ <b>Ninguna hoja que falle puede tumbar el paquete.</b> El companero recibe un PDF de
/// veinte hojas al que le falta una, con el aviso de cual, en vez de nada. Cada archivo se abre
/// dentro de su propio intento y el fallo se convierte en un renglon con su motivo; es la misma
/// regla que ya rige la importacion de una tanda de 500.</para>
///
/// <para>⚠️ <b>Una hoja repetida se repite.</b> Dos casos que apuntan a la misma hoja —un
/// documento duplicado— llevan cada uno la suya. Quitar la segunda descolocaria la
/// correspondencia «fila N del Excel → hoja N del PDF» de todo lo que va debajo, que es lo unico
/// que hace util al PDF.</para>
///
/// <para>Esta clase NO toca la base y NO sabe que es un compañero: recibe una lista de hojas y
/// escribe un archivo.</para>
/// </remarks>
public static class PdfDelPaquete
{
    /// <summary>Escribe en <paramref name="rutaDestino"/> un PDF con esas hojas, en ese orden.</summary>
    /// <remarks>
    /// Sale UNA hoja por cada una que se pida, sin excepcion: la del escaneo cuando se puede
    /// leer, y una hoja de aviso en su sitio exacto cuando no. Es lo que mantiene que la hoja N
    /// del PDF sea la fila N del Excel, que es todo lo que hace util al PDF.
    /// </remarks>
    /// <param name="hojas">Las hojas en el orden de los renglones del Excel; una lista vacía no escribe nada.</param>
    /// <param name="rutaDestino">Ruta del <c>.pdf</c> a escribir; se sobrescribe si ya existe.</param>
    /// <returns>Cuántas hojas salieron, cuáles llevan hoja de aviso y, si el archivo no se pudo escribir, por qué.</returns>
    public static ResultadoDeLaUnion Unir(IReadOnlyList<HojaDelPaquete> hojas, string rutaDestino)
    {
        ArgumentNullException.ThrowIfNull(hojas);

        var faltan = new List<HojaQueFalta>();
        var contenidos = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var paginas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tamanos = new Dictionary<string, TamanoDeHoja>(StringComparer.OrdinalIgnoreCase);

        // Una entrada por cada hoja pedida, EN SU SITIO. Un hueco es una entrada con su
        // motivo, no una entrada de menos.
        var enOrden = new List<(HojaDelPaquete Hoja, HojaQueFalta? Falta)>(hojas.Count);
        foreach (var hoja in hojas)
        {
            var falta = PorQueNoEntra(hoja, contenidos, paginas, tamanos);
            if (falta is not null) faltan.Add(falta);
            enOrden.Add((hoja, falta));
        }

        if (enOrden.Count == 0)
            return new ResultadoDeLaUnion(false, 0, faltan, "No había ninguna hoja que juntar.");

        try
        {
            Escribir(enOrden, contenidos, TamanoDeLasDemas(enOrden, tamanos), rutaDestino);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException
                                          or PdfDocumentFormatException or DirectoryNotFoundException)
        {
            return new ResultadoDeLaUnion(false, 0, faltan, causa.Message);
        }

        return new ResultadoDeLaUnion(true, enOrden.Count, faltan, null);
    }

    /// <summary>
    /// Cuanto miden las hojas de este paquete, para que la de aviso mida lo mismo.
    /// </summary>
    /// <remarks>
    /// Lo pidio el dueno: la hoja de aviso tiene que parecerse a las demas «para que al hojear
    /// el PDF no parezca que se acabo el paquete». Se toma la medida de la PRIMERA hoja que si
    /// se pudo leer; los escaneos de una misma tanda salen todos del mismo aparato y miden
    /// igual. Si no se pudo leer ninguna no hay de donde copiar, y entonces se usa A4, que es
    /// el tamano de los formularios del dueno.
    /// </remarks>
    /// <param name="enOrden">Las hojas pedidas con su motivo de falta, nulo en las que sí entran.</param>
    /// <param name="tamanos">El tamaño de la primera página de cada archivo que se pudo abrir, por ruta.</param>
    private static TamanoDeHoja TamanoDeLasDemas(
        IReadOnlyList<(HojaDelPaquete Hoja, HojaQueFalta? Falta)> enOrden,
        Dictionary<string, TamanoDeHoja> tamanos)
    {
        foreach (var (hoja, falta) in enOrden)
            if (falta is null && tamanos.TryGetValue(hoja.RutaPdf, out var medida))
                return medida;
        return TamanoDeHoja.A4;
    }

    /// <summary>El motivo por el que esa hoja no puede entrar, o nulo si si puede.</summary>
    /// <remarks>
    /// Se comprueba ANTES de unir y no dentro: si el archivo roto se descubriera a mitad de la
    /// union, lo ya juntado se perderia y el companero se quedaria sin paquete por una hoja.
    /// </remarks>
    /// <param name="hoja">La hoja pedida.</param>
    /// <param name="contenidos">Los archivos ya leídos por ruta (nulo el que falló); aquí se cargan los que faltan.</param>
    /// <param name="paginas">Cuántas páginas tiene cada archivo leído.</param>
    /// <param name="tamanos">El tamaño de la primera página de cada archivo leído.</param>
    /// <returns>Sin ruta, archivo ilegible o página fuera de rango: la falta con su motivo; si no, nulo.</returns>
    private static HojaQueFalta? PorQueNoEntra(
        HojaDelPaquete hoja,
        Dictionary<string, byte[]?> contenidos,
        Dictionary<string, int> paginas,
        Dictionary<string, TamanoDeHoja> tamanos)
    {
        if (string.IsNullOrWhiteSpace(hoja.RutaPdf))
        {
            return new HojaQueFalta(
                hoja.NumeroCaso,
                $"El documento del caso {hoja.NumeroCaso} no va en el PDF: no se sabe de qué archivo salió.",
                "La base no guarda ninguna ruta de escaneo para este caso. Pasa con los que se "
                + "teclearon a mano; no hay hoja que juntar y el resto del paquete sale igual.");
        }

        if (!contenidos.ContainsKey(hoja.RutaPdf))
            Cargar(hoja.RutaPdf, contenidos, paginas, tamanos);

        if (contenidos[hoja.RutaPdf] is null)
        {
            return new HojaQueFalta(
                hoja.NumeroCaso,
                $"El documento del caso {hoja.NumeroCaso} no va en el PDF: su archivo no se pudo leer.",
                $"Se buscó «{hoja.RutaPdf}». Si el archivo se movió o se borró después de importarlo, "
                + "vuelva a ponerlo en su sitio; si está donde debe, es que el escaneo está dañado.");
        }

        var cuantas = paginas[hoja.RutaPdf];
        if (hoja.Pagina < 1 || hoja.Pagina > cuantas)
        {
            return new HojaQueFalta(
                hoja.NumeroCaso,
                $"El documento del caso {hoja.NumeroCaso} no va en el PDF: su hoja no está en el archivo.",
                $"Se pidió la hoja {hoja.Pagina} de «{hoja.RutaPdf}», que tiene {cuantas}. "
                + "Casi siempre es que el archivo se reemplazó por otro con menos páginas.");
        }

        return null;
    }

    /// <summary>Lee el archivo entero y cuenta sus hojas; deja un nulo si no se pudo.</summary>
    /// <remarks>
    /// Se guarda el contenido en memoria a proposito: el mismo escaneo puede aportar varias
    /// hojas al paquete —un formulario de grupo ocupa seis paginas— y releerlo una vez por hoja
    /// multiplicaria por seis el trabajo del disco sin cambiar el resultado.
    /// </remarks>
    /// <param name="ruta">El archivo escaneado.</param>
    /// <param name="contenidos">Donde se deja el contenido, o nulo si no se pudo leer o abrir.</param>
    /// <param name="paginas">Donde se deja cuántas páginas tiene; 0 si falló.</param>
    /// <param name="tamanos">Donde se deja el tamaño de su primera página; no se toca si falló o no tiene páginas.</param>
    private static void Cargar(
        string ruta,
        Dictionary<string, byte[]?> contenidos,
        Dictionary<string, int> paginas,
        Dictionary<string, TamanoDeHoja> tamanos)
    {
        try
        {
            var contenido = File.ReadAllBytes(ruta);
            using var documento = PdfDocument.Open(contenido);
            paginas[ruta] = documento.NumberOfPages;
            contenidos[ruta] = contenido;
            if (documento.NumberOfPages > 0)
            {
                var primera = documento.GetPage(1);
                tamanos[ruta] = new TamanoDeHoja(primera.Width, primera.Height);
            }
        }
        catch (Exception causa) when (
            causa is IOException or UnauthorizedAccessException or PdfDocumentFormatException
                  or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // No se silencia: el nulo es lo que convierte este fallo en un renglon con su
            // motivo unas lineas mas abajo, que es donde Miguel lo lee.
            contenidos[ruta] = null;
            paginas[ruta] = 0;
        }
    }

    /// <summary>Escribe el archivo unido: una entrada por hoja, para que el orden sea exacto.</summary>
    /// <remarks>
    /// ⚠️ Se pasa UNA entrada por hoja y no una por archivo con su lista de paginas. Con una
    /// entrada por archivo el orden lo manda el archivo, y entonces dos casos del mismo escaneo
    /// saldrian juntos aunque en el Excel estuvieran separados. El orden del Excel es el unico
    /// que el companero puede seguir.
    /// </remarks>
    /// <param name="enOrden">Las hojas con su motivo de falta; las que faltan entran como hoja de aviso.</param>
    /// <param name="contenidos">Los archivos ya leídos por ruta.</param>
    /// <param name="tamano">El tamaño con el que se dibujan las hojas de aviso.</param>
    /// <param name="rutaDestino">Ruta del <c>.pdf</c> a escribir.</param>
    private static void Escribir(
        IReadOnlyList<(HojaDelPaquete Hoja, HojaQueFalta? Falta)> enOrden,
        Dictionary<string, byte[]?> contenidos,
        TamanoDeHoja tamano,
        string rutaDestino)
    {
        var fuentes = new List<Stream>(enOrden.Count);
        try
        {
            var cuales = new List<IReadOnlyList<int>>(enOrden.Count);
            foreach (var (hoja, falta) in enOrden)
            {
                // Un hueco entra como un PDF de UNA pagina fabricado al vuelo, en su sitio.
                // Asi el que junta no tiene que saber que unas hojas son escaneos y otras no.
                var contenido = falta is null
                    ? contenidos[hoja.RutaPdf]!
                    : HojaDeAviso.Dibujar(hoja, falta, tamano);
                fuentes.Add(new MemoryStream(contenido, writable: false));
                cuales.Add([falta is null ? hoja.Pagina : 1]);
            }

            using var salida = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None);
            PdfMerger.Merge(fuentes, salida, cuales);
        }
        finally
        {
            foreach (var fuente in fuentes) fuente.Dispose();
        }
    }
}
