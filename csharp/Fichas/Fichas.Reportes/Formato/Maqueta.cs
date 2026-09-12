using Fichas.Reportes.Modelo;

namespace Fichas.Reportes.Formato;

/// <summary>
/// El reporte colocado sobre el papel: donde va cada linea y donde corta la pagina.
/// </summary>
/// <remarks>
/// Decide la MAQUETA —anchos de columna, partido de lineas, saltos de pagina, titulos de
/// columna repetidos— y no sabe nada del formato de archivo. Los bytes los pone
/// <see cref="EscritorDePdf"/>. Mezclados, cambiar el ancho de una columna obligaria a leer
/// el codigo que numera objetos.
///
/// ⚠️ <b>Papel Carta VERTICAL (612 × 792), que es el del informe del proyecto viejo.</b> Lo
/// decidio el dueno el 2026-09-04 (<c>DECISIONES.md</c>): <i>«si el informe de los jefes como
/// el viejo está bien»</i>. Estuvo apaisado porque el Python nuevo lo puso asi para que
/// cupieran las ocho columnas; el precio de volver es que el informe ocupa mas paginas, y esa
/// cifra va medida en la entrega. Ninguna columna se sale por la derecha: el reparto de
/// <see cref="RepartoDeColumnas"/> es proporcional al ancho de la pagina, no fijo.
/// </remarks>
public static class Maqueta
{
    /// <summary>El margen izquierdo y derecho en puntos: media pulgada, como el informe viejo.</summary>
    private const int Margen = 36;

    /// <summary>El tamaño de letra del título del documento, la primera línea de la página 1.</summary>
    private const int TamanoDelTitulo = 14;
    /// <summary>El tamaño de letra del subtítulo: el periodo o el compañero.</summary>
    private const int TamanoDelSubtitulo = 10;

    /// <summary>El tamano de letra de un titulo de seccion; lo mira el reparto en paginas.</summary>
    public const int TamanoDeSeccion = 11;

    /// <summary>El tamano de letra del cuerpo de las tablas.</summary>
    public const int TamanoNormal = 8;

    /// <summary>El tamaño de letra del titular de la portada, el más grande después de las cifras.</summary>
    private const int TamanoDelTitular = 17;
    /// <summary>El tamaño de letra de la frase de la portada.</summary>
    private const int TamanoDeLaFrase = 10;
    /// <summary>El tamaño de letra de las cifras grandes: tienen que leerse desde el otro lado de una mesa.</summary>
    private const int TamanoDeUnaCifra = 22;
    /// <summary>El tamaño de letra del rótulo que va debajo de cada cifra grande.</summary>
    private const int TamanoDelRotuloDeUnaCifra = 8;

    /// <summary>Donde empieza el texto de una pagina; arriba manda la cinta.</summary>
    public const double TopeSuperior = EscritorDePdf.AltoPagina - Marco.AltoDeLaCinta - 12;

    /// <summary>Donde acaba el texto de una pagina; debajo esta el pie.</summary>
    public const double TopeInferior = Marco.AlturaDeLaRayaDelPie + 12;

    // Ancho medio de un caracter de Helvetica en proporcion al tamano. Es una aproximacion
    // —Helvetica es de ancho variable— y basta para repartir columnas y partir lineas: el
    // reparto es proporcional al ancho de la pagina, asi que una columna nunca se sale por
    // la derecha aunque la estimacion se quede corta.
    /// <summary>Ancho medio de un carácter de Helvetica como fracción de su tamaño; ver el comentario de arriba.</summary>
    internal const double ProporcionDelAnchoDeCaracter = 0.5;

    /// <summary>Los puntos que quedan entre los dos márgenes; sobre esto se reparten columnas y cifras.</summary>
    private static int AnchoUtil => EscritorDePdf.AnchoPagina - (2 * Margen);

    /// <summary>El color con el que el PDF pinta cada tono: rojo lo malo, verde lo bueno, tinta lo neutro.</summary>
    /// <param name="tono">El tono de la cifra.</param>
    private static string ColorDelTono(TonoDeCifra tono) => tono switch
    {
        TonoDeCifra.Malo => Marco.Rojo,
        TonoDeCifra.Bueno => Marco.Verde,
        _ => Marco.Tinta,
    };

    // ---- partir texto -------------------------------------------------------

    /// <summary>Cuantos caracteres caben en ese ancho con esa letra. Al menos uno.</summary>
    /// <remarks>
    /// Al menos uno y no cero: con capacidad cero el partido de lineas no avanzaria nunca y
    /// se quedaria dando vueltas sobre la misma palabra.
    /// </remarks>
    /// <param name="anchoEnPuntos">El ancho disponible.</param>
    /// <param name="tamano">El tamaño de la letra en puntos.</param>
    private static int Capacidad(double anchoEnPuntos, int tamano)
        => Math.Max(1, (int)(anchoEnPuntos / (tamano * ProporcionDelAnchoDeCaracter)));

    /// <summary>Parte un texto en lineas de como mucho <paramref name="capacidad"/> caracteres.</summary>
    /// <remarks>
    /// Una palabra mas larga que la capacidad se parte por donde toque en vez de desbordar la
    /// columna: preferimos un MRN partido en dos lineas a un MRN escrito encima de la columna
    /// de al lado.
    /// </remarks>
    /// <param name="texto">Lo que se parte; nulo o vacío da una sola línea vacía, nunca cero líneas.</param>
    /// <param name="capacidad">Cuántos caracteres caben por línea; menos de uno se trata como uno.</param>
    /// <returns>Al menos una línea; los espacios repetidos se funden en uno.</returns>
    public static IReadOnlyList<string> PartirEnLineas(string? texto, int capacidad)
    {
        var cabe = Math.Max(1, capacidad);
        if (string.IsNullOrEmpty(texto)) return [""];

        var lineas = new List<string>();
        foreach (var trozo in texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var palabra = trozo;
            while (palabra.Length > cabe)
            {
                lineas.Add(palabra[..cabe]);
                palabra = palabra[cabe..];
            }
            if (lineas.Count > 0 && lineas[^1].Length > 0 && lineas[^1].Length + 1 + palabra.Length <= cabe)
            {
                lineas[^1] = $"{lineas[^1]} {palabra}";
            }
            else
            {
                lineas.Add(palabra);
            }
        }
        return lineas.Count > 0 ? lineas : [""];
    }

    // ---- lineas sueltas -----------------------------------------------------

    /// <summary>Una línea de un solo trazo que arranca en el margen izquierdo.</summary>
    /// <param name="texto">Lo que se escribe.</param>
    /// <param name="tamano">El tamaño de la letra en puntos.</param>
    /// <param name="negrita">Si va en Helvetica-Bold.</param>
    /// <param name="repetir">Si se repite arriba de cada página nueva.</param>
    private static Linea UnaLinea(string texto, int tamano, bool negrita = false, bool repetir = false)
        => new(negrita, tamano, [new Trazo(Margen, texto)], repetir);

    /// <summary>El hueco entre bloques. Es una linea sin trazos: ocupa alto y no pinta.</summary>
    /// <param name="tamano">Cuánto alto ocupa, medido como si fuera una línea de ese tamaño.</param>
    private static Linea EnBlanco(int tamano = TamanoNormal) => new(false, tamano, [], false);

    /// <summary>Un párrafo partido a lo ancho de la página, una línea por trozo.</summary>
    /// <param name="texto">El párrafo entero.</param>
    /// <param name="tamano">El tamaño de la letra, que decide cuántos caracteres caben.</param>
    private static IEnumerable<Linea> LineasDeParrafo(string texto, int tamano)
        => PartirEnLineas(texto, Capacidad(AnchoUtil, tamano)).Select(t => UnaLinea(t, tamano));

    // ---- tablas -------------------------------------------------------------

    /// <summary>La x donde empieza cada columna y cuantos caracteres le caben.</summary>
    /// <remarks>
    /// El reparto es PROPORCIONAL al ancho que declara cada columna, no fijo: asi la tabla
    /// ocupa siempre el ancho de la pagina y nunca se sale por la derecha, tenga las columnas
    /// que tenga.
    /// </remarks>
    /// <param name="columnas">Las columnas con su ancho relativo; una lista con todos los anchos a cero se reparte como si sumaran uno.</param>
    /// <returns>Por columna, la x de arranque y los caracteres que le caben menos uno, que hace de separación.</returns>
    private static List<(int X, int Capacidad)> RepartoDeColumnas(IReadOnlyList<Columna> columnas)
    {
        var total = columnas.Sum(c => c.Ancho);
        if (total == 0) total = 1;

        var posiciones = new List<(int, int)>(columnas.Count);
        double x = Margen;
        foreach (var columna in columnas)
        {
            var anchoEnPuntos = AnchoUtil * columna.Ancho / (double)total;
            posiciones.Add(((int)x, Math.Max(1, Capacidad(anchoEnPuntos, TamanoNormal) - 1)));
            x += anchoEnPuntos;
        }
        return posiciones;
    }

    /// <summary>Una fila de tabla, que ocupa tantas lineas como la celda mas larga.</summary>
    /// <remarks>
    /// Cada celda se parte por su cuenta y las celdas cortas dejan hueco en blanco debajo. Es
    /// la unica forma de que el motivo de por que alguien no viajo —texto libre de hasta 300
    /// caracteres— se lea entero sin recortarlo.
    /// </remarks>
    /// <param name="valores">Las celdas; una fila más corta que el reparto deja vacías las columnas que faltan, y una nula sale en blanco.</param>
    /// <param name="reparto">La x y la capacidad de cada columna.</param>
    /// <param name="negrita">Si la fila va en negrita, como los títulos de columna.</param>
    private static List<Linea> LineasDeUnaFila(
        IReadOnlyList<string?> valores, List<(int X, int Capacidad)> reparto, bool negrita)
    {
        var partidas = new List<IReadOnlyList<string>>(reparto.Count);
        for (var i = 0; i < reparto.Count; i++)
        {
            var valor = i < valores.Count ? valores[i] : null;
            partidas.Add(PartirEnLineas(valor, reparto[i].Capacidad));
        }

        var alto = partidas.Count == 0 ? 1 : partidas.Max(c => c.Count);
        var lineas = new List<Linea>(alto);
        for (var numero = 0; numero < alto; numero++)
        {
            var trazos = new List<Trazo>(partidas.Count);
            for (var i = 0; i < partidas.Count; i++)
            {
                if (numero >= partidas[i].Count || partidas[i][numero].Length == 0) continue;
                trazos.Add(new Trazo(reparto[i].X, partidas[i][numero]));
            }
            lineas.Add(new Linea(negrita, TamanoNormal, trazos, false));
        }
        return lineas;
    }

    /// <summary>Los titulos de columna —que se repiten en cada pagina— y todas las filas.</summary>
    /// <param name="seccion">La sección cuya tabla se coloca.</param>
    private static List<Linea> LineasDeLaTabla(Seccion seccion)
    {
        var reparto = RepartoDeColumnas(seccion.Columnas);
        var lineas = LineasDeUnaFila(
                seccion.Columnas.Select(c => (string?)c.Nombre).ToList(), reparto, negrita: true)
            .Select(l => l with { Repetir = true })
            .ToList();

        foreach (var fila in seccion.Filas)
        {
            lineas.AddRange(LineasDeUnaFila(fila, reparto, negrita: false));
        }
        return lineas;
    }

    /// <summary>Una sección entera: hueco, título, notas, hueco, tabla y, si lo lleva, el resumen en negrita.</summary>
    /// <param name="seccion">La sección.</param>
    private static IEnumerable<Linea> LineasDeLaSeccion(Seccion seccion)
    {
        yield return EnBlanco();
        yield return UnaLinea(seccion.Titulo, TamanoDeSeccion, negrita: true);
        foreach (var nota in seccion.Notas)
        {
            foreach (var linea in LineasDeParrafo(nota, TamanoNormal)) yield return linea;
        }
        yield return EnBlanco();
        foreach (var linea in LineasDeLaTabla(seccion)) yield return linea;
        if (seccion.Resumen is not null)
        {
            yield return EnBlanco();
            yield return UnaLinea(seccion.Resumen, TamanoNormal, negrita: true);
        }
    }

    // ---- portada ------------------------------------------------------------

    /// <summary>Las cifras grandes en una fila, cada una con su rotulo debajo.</summary>
    /// <remarks>
    /// Van repartidas por igual a lo ancho de la pagina, y no una detras de otra: las cuatro
    /// tienen que verse de un vistazo desde el otro lado de una mesa, que es para lo que
    /// existen. Son DOS lineas y no cuatro bloques: los numeros arriba, todos a la misma
    /// altura, y los rotulos abajo. Si cada cifra se colocara por su cuenta, un numero de
    /// cuatro digitos bajaria su rotulo y la fila quedaria escalonada.
    /// </remarks>
    /// <param name="cifras">Las cifras de la portada; con la lista vacía no sale ninguna línea.</param>
    private static IEnumerable<Linea> LineasDeLasCifras(IReadOnlyList<Cifra> cifras)
    {
        if (cifras.Count == 0) yield break;

        var paso = AnchoUtil / (double)cifras.Count;
        var numeros = cifras
            .Select((cifra, indice) => new Trazo(
                Margen + (int)(indice * paso),
                cifra.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ColorDelTono(cifra.Tono)))
            .ToList();
        var rotulos = cifras
            .Select((cifra, indice) => new Trazo(Margen + (int)(indice * paso), cifra.Rotulo, Marco.Gris))
            .ToList();

        yield return new Linea(true, TamanoDeUnaCifra, numeros, false);
        yield return new Linea(false, TamanoDelRotuloDeUnaCifra, rotulos, false);
    }

    /// <summary>Lo primero que se lee: el titular, la frase y las cifras.</summary>
    /// <remarks>
    /// Va antes que los avisos y que cualquier tabla. El informe abre por el numero que se
    /// mide y no por lo que luce, que es la decision del proyecto viejo y la del dueno.
    /// </remarks>
    /// <param name="portada">La portada del documento.</param>
    private static IEnumerable<Linea> LineasDeLaPortada(Portada portada)
    {
        yield return EnBlanco();
        yield return UnaLinea(portada.Titular, TamanoDelTitular, negrita: true);
        foreach (var linea in LineasDeParrafo(portada.Frase, TamanoDeLaFrase)) yield return linea;
        yield return EnBlanco();
        foreach (var linea in LineasDeLasCifras(portada.Cifras)) yield return linea;
    }

    // ---- el documento entero ------------------------------------------------

    /// <summary>El documento entero convertido en lineas, todavia sin repartir en paginas.</summary>
    /// <param name="documento">El documento armado.</param>
    /// <param name="avisosExtra">Avisos que van DELANTE de los del documento; hoy solo el de los caracteres que no caben en la fuente.</param>
    /// <returns>Título, subtítulo, «Generado el», portada, avisos y secciones, en ese orden.</returns>
    public static IReadOnlyList<Linea> LineasDelDocumento(Documento documento, IReadOnlyList<string> avisosExtra)
    {
        var lineas = new List<Linea>
        {
            UnaLinea(documento.Titulo, TamanoDelTitulo, negrita: true),
            UnaLinea(documento.Subtitulo, TamanoDelSubtitulo),
            UnaLinea($"Generado el {documento.GeneradoEn}", TamanoNormal),
        };

        lineas.AddRange(LineasDeLaPortada(documento.Portada));

        foreach (var aviso in avisosExtra.Concat(documento.Avisos))
        {
            lineas.Add(EnBlanco());
            lineas.AddRange(LineasDeParrafo(aviso, TamanoNormal));
        }

        foreach (var seccion in documento.Secciones)
        {
            lineas.AddRange(LineasDeLaSeccion(seccion));
        }

        return lineas;
    }

    /// <summary>Cuantos caracteres del documento entero no existen en WinAnsi.</summary>
    /// <remarks>Recorre todos los textos que se imprimen: título, portada, avisos, y de cada sección su título, notas, columnas, filas y resumen.</remarks>
    /// <param name="documento">El documento armado.</param>
    public static int ContarCaracteresQueNoCaben(Documento documento)
    {
        var perdidos = WinAnsi.Perdidos(documento.Titulo)
            + WinAnsi.Perdidos(documento.Subtitulo)
            + WinAnsi.Perdidos(documento.GeneradoEn)
            + WinAnsi.Perdidos(documento.Portada.Titular)
            + WinAnsi.Perdidos(documento.Portada.Frase)
            + documento.Portada.Cifras.Sum(c => WinAnsi.Perdidos(c.Rotulo))
            + documento.Avisos.Sum(WinAnsi.Perdidos);

        foreach (var seccion in documento.Secciones)
        {
            perdidos += WinAnsi.Perdidos(seccion.Titulo)
                + WinAnsi.Perdidos(seccion.Resumen)
                + seccion.Notas.Sum(WinAnsi.Perdidos)
                + seccion.Columnas.Sum(c => WinAnsi.Perdidos(c.Nombre))
                + seccion.Filas.Sum(f => f.Sum(WinAnsi.Perdidos));
        }
        return perdidos;
    }

    /// <summary>El aviso que encabeza el PDF cuando algun caracter no cupo en la fuente.</summary>
    /// <param name="cuantos">Cuántos caracteres se perdieron; con uno la frase va en singular.</param>
    public static string AvisoDeCaracteresPerdidos(int cuantos)
        => $"AVISO: {cuantos} {(cuantos == 1 ? "carácter" : "caracteres")} de este reporte no "
           + (cuantos == 1 ? "existe" : "existen")
           + " en la codificación que usan las fuentes básicas de PDF y "
           + (cuantos == 1 ? "sale escrito" : "salen escritos")
           + " como «?». Casi siempre es un nombre con un signo poco corriente. El archivo "
           + ".xlsx del mismo período los guarda todos sin alterar: para comprobar un nombre "
           + "o un MRN, mírelo allí.";

    // ---- reparto en paginas -------------------------------------------------

    /// <summary>Reparte las lineas en paginas y devuelve cada una con su <c>y</c> calculada.</summary>
    /// <remarks>
    /// Los titulos de columna llevan <c>Repetir</c> y se vuelven a dibujar arriba de cada
    /// pagina nueva: una tabla que sigue en la pagina 3 sin sus titulos es una rejilla de
    /// numeros sin nombre.
    /// </remarks>
    /// <param name="lineas">Las líneas del documento, en orden.</param>
    /// <returns>Al menos una página; cada línea con la <c>y</c> de su base, contando desde abajo.</returns>
    public static IReadOnlyList<IReadOnlyList<(double Y, Linea Linea)>> RepartirEnPaginas(
        IReadOnlyList<Linea> lineas)
    {
        var reparto = new RepartoEnPaginas();
        foreach (var linea in lineas)
        {
            reparto.AnotarElEncabezado(linea);
            reparto.Colocar(linea);
        }
        return reparto.Paginas();
    }

    /// <summary>El PDF entero del documento, como bytes. No toca el disco.</summary>
    /// <remarks>Es el único sitio que encadena las cuatro etapas: contar lo que no cabe en la fuente, hacer líneas, repartir en páginas y escribir bytes.</remarks>
    /// <param name="documento">El documento armado.</param>
    public static byte[] ConstruirPdf(Documento documento)
    {
        var perdidos = ContarCaracteresQueNoCaben(documento);
        IReadOnlyList<string> avisosExtra = perdidos > 0 ? [AvisoDeCaracteresPerdidos(perdidos)] : [];
        var paginas = RepartirEnPaginas(LineasDelDocumento(documento, avisosExtra));
        return EscritorDePdf.BytesDelPdf(paginas, Marco.DeLaPagina(documento.GeneradoEn, Margen));
    }
}
