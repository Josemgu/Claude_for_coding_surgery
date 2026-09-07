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
    private const int Margen = 36;

    private const int TamanoDelTitulo = 14;
    private const int TamanoDelSubtitulo = 10;

    /// <summary>El tamano de letra de un titulo de seccion; lo mira el reparto en paginas.</summary>
    public const int TamanoDeSeccion = 11;

    /// <summary>El tamano de letra del cuerpo de las tablas.</summary>
    public const int TamanoNormal = 8;

    private const int TamanoDelTitular = 17;
    private const int TamanoDeLaFrase = 10;
    private const int TamanoDeUnaCifra = 22;
    private const int TamanoDelRotuloDeUnaCifra = 8;

    /// <summary>Donde empieza el texto de una pagina; arriba manda la cinta.</summary>
    public const double TopeSuperior = EscritorDePdf.AltoPagina - Marco.AltoDeLaCinta - 12;

    /// <summary>Donde acaba el texto de una pagina; debajo esta el pie.</summary>
    public const double TopeInferior = Marco.AlturaDeLaRayaDelPie + 12;

    private const double ProporcionDelInterlineado = 1.45;

    // Ancho medio de un caracter de Helvetica en proporcion al tamano. Es una aproximacion
    // —Helvetica es de ancho variable— y basta para repartir columnas y partir lineas: el
    // reparto es proporcional al ancho de la pagina, asi que una columna nunca se sale por
    // la derecha aunque la estimacion se quede corta.
    internal const double ProporcionDelAnchoDeCaracter = 0.5;

    private static int AnchoUtil => EscritorDePdf.AnchoPagina - (2 * Margen);

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
    private static int Capacidad(double anchoEnPuntos, int tamano)
        => Math.Max(1, (int)(anchoEnPuntos / (tamano * ProporcionDelAnchoDeCaracter)));

    /// <summary>Parte un texto en lineas de como mucho <paramref name="capacidad"/> caracteres.</summary>
    /// <remarks>
    /// Una palabra mas larga que la capacidad se parte por donde toque en vez de desbordar la
    /// columna: preferimos un MRN partido en dos lineas a un MRN escrito encima de la columna
    /// de al lado.
    /// </remarks>
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

    private static Linea UnaLinea(string texto, int tamano, bool negrita = false, bool repetir = false)
        => new(negrita, tamano, [new Trazo(Margen, texto)], repetir);

    /// <summary>El hueco entre bloques. Es una linea sin trazos: ocupa alto y no pinta.</summary>
    private static Linea EnBlanco(int tamano = TamanoNormal) => new(false, tamano, [], false);

    private static IEnumerable<Linea> LineasDeParrafo(string texto, int tamano)
        => PartirEnLineas(texto, Capacidad(AnchoUtil, tamano)).Select(t => UnaLinea(t, tamano));

    // ---- tablas -------------------------------------------------------------

    /// <summary>La x donde empieza cada columna y cuantos caracteres le caben.</summary>
    /// <remarks>
    /// El reparto es PROPORCIONAL al ancho que declara cada columna, no fijo: asi la tabla
    /// ocupa siempre el ancho de la pagina y nunca se sale por la derecha, tenga las columnas
    /// que tenga.
    /// </remarks>
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
    public static byte[] ConstruirPdf(Documento documento)
    {
        var perdidos = ContarCaracteresQueNoCaben(documento);
        IReadOnlyList<string> avisosExtra = perdidos > 0 ? [AvisoDeCaracteresPerdidos(perdidos)] : [];
        var paginas = RepartirEnPaginas(LineasDelDocumento(documento, avisosExtra));
        return EscritorDePdf.BytesDelPdf(paginas, Marco.DeLaPagina(documento.GeneradoEn, Margen));
    }
}
