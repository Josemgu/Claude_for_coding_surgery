using System.Globalization;
using System.Text;

namespace Fichas.Reportes.Formato;

/// <summary>
/// El formato de archivo PDF: codificacion, objetos y tabla de referencias cruzadas.
/// </summary>
/// <remarks>
/// Este modulo sabe como se escribe un PDF y NO sabe nada del reporte. Lo que recibe son
/// lineas ya colocadas —cada una con su <c>y</c>, su fuente y sus trazos— y lo que devuelve
/// son bytes. La otra mitad, decidir que linea va donde, es de <see cref="Maqueta"/>.
///
/// ⚠️ <b>SIN NINGUNA BIBLIOTECA, y es la decision medida del criterio C8-4</b>
/// (PENDIENTES.md, FASE C8): «primero portar el motor de PDF crudo; PDFsharp solo si el
/// porte cuesta mas, con la cifra que lo diga». Lo que el informe necesita de un motor de
/// dibujo son DOS operadores —rellenar un rectangulo (<c>re f</c>) y trazar una raya
/// (<c>l S</c>)— mas poder cambiar el color del texto (<c>rg</c>), y los tres estan aqui
/// abajo en <see cref="FlujoDeLosAdornos"/> y <see cref="ColorDeRelleno"/>. El porte entero
/// son estas ~200 lineas; el paquete no crece ni un byte y no hay ninguna licencia de
/// terceros que auditar.
///
/// Lo que este modulo pinta y lo que NO: rectangulos rellenos, rayas rectas y texto de
/// color. No hay curvas, ni imagenes, ni transparencias, ni degradados. Si algun dia hiciera
/// falta un grafico de verdad, ESA si es una conversacion sobre una biblioteca.
///
/// Se usan las dos fuentes base que todo visor trae obligatoriamente, Helvetica y
/// Helvetica-Bold, con <c>/WinAnsiEncoding</c>. No se incrusta ninguna: incrustarla es lo
/// que hace pesados los PDF y aqui no hace falta.
/// </remarks>
public static class EscritorDePdf
{
    /// <summary>Ancho de la pagina en puntos. Carta VERTICAL, que es el papel del viejo.</summary>
    /// <remarks>
    /// ⚠️ <b>Estuvo apaisado (792 × 612) y volvio a vertical por decision del dueno</b>
    /// (DECISIONES.md, 2026-09-04): <i>«si el informe de los jefes como el viejo está bien»</i>.
    /// El informe del programa viejo era Carta vertical; el apaisado lo eligio el Python nuevo
    /// para que cupieran las ocho columnas de las tablas. El coste de volver esta medido y
    /// dicho en la entrega: la tabla mas ancha reparte menos sitio por columna y el informe
    /// ocupa mas paginas. No se sale nada por la derecha porque el reparto de
    /// <see cref="Maqueta"/> es proporcional al ancho de la pagina, no fijo.
    /// </remarks>
    public const int AnchoPagina = 612;

    /// <summary>Alto de la pagina en puntos.</summary>
    public const int AltoPagina = 792;

    private const string Negro = "#151515";

    // Los bytes se arman con Latin1, que es byte a byte identico a lo que ya devolvio
    // WinAnsi: aqui no se vuelve a codificar nada, solo se pegan literales ASCII.
    private static readonly Encoding Bytes = Encoding.Latin1;

    /// <summary>Escapa los tres caracteres que un literal de cadena de PDF no admite.</summary>
    public static byte[] Escapar(byte[] crudo)
    {
        var salida = new List<byte>(crudo.Length + 8);
        foreach (var octeto in crudo)
        {
            if (octeto is (byte)'\\' or (byte)'(' or (byte)')') salida.Add((byte)'\\');
            salida.Add(octeto);
        }
        return [.. salida];
    }

    /// <summary>El archivo PDF entero de esas paginas ya colocadas.</summary>
    /// <param name="paginas">Cada pagina, con sus lineas y la <c>y</c> de cada una.</param>
    /// <param name="marco">Lo que se repite en todas las paginas; nulo si no se repite nada.</param>
    public static byte[] BytesDelPdf(
        IReadOnlyList<IReadOnlyList<(double Y, Linea Linea)>> paginas,
        MarcoDePagina? marco)
        => Ensamblar(ObjetosDelPdf(paginas, marco));

    // ---- los objetos --------------------------------------------------------

    /// <summary>Los objetos del PDF en el orden en que se numeran, empezando por el 1.</summary>
    /// <remarks>
    /// La numeracion no es libre: cada pagina apunta a su flujo de contenido y a las dos
    /// fuentes POR NUMERO de objeto, asi que el reparto se calcula antes de escribir nada.
    /// Las paginas ocupan los pares (3, 5, 7...) y sus flujos los impares siguientes; las
    /// dos fuentes van al final.
    /// </remarks>
    private static List<byte[]> ObjetosDelPdf(
        IReadOnlyList<IReadOnlyList<(double Y, Linea Linea)>> paginas,
        MarcoDePagina? marco)
    {
        var total = paginas.Count;
        var numeroDeF1 = 3 + (2 * total);
        var numeroDeF2 = numeroDeF1 + 1;
        var hijos = string.Join(" ", Enumerable.Range(0, total).Select(i => $"{3 + (2 * i)} 0 R"));

        var objetos = new List<byte[]>
        {
            Bytes.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Bytes.GetBytes($"<< /Type /Pages /Kids [{hijos}] /Count {total} >>"),
        };

        for (var indice = 0; indice < total; indice++)
        {
            var (adornos, anadidas) = marco is null
                ? ((IReadOnlyList<Adorno>)[], (IReadOnlyList<(double Y, Linea Linea)>)[])
                : marco(indice + 1, total);

            var flujo = FlujoDeUnaPagina([.. paginas[indice], .. anadidas], adornos);

            objetos.Add(Bytes.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {AnchoPagina} {AltoPagina}] "
                + $"/Resources << /Font << /F1 {numeroDeF1} 0 R /F2 {numeroDeF2} 0 R >> >> "
                + $"/Contents {4 + (2 * indice)} 0 R >>"));

            objetos.Add([
                .. Bytes.GetBytes($"<< /Length {flujo.Length} >>\nstream\n"),
                .. flujo,
                .. Bytes.GetBytes("endstream"),
            ]);
        }

        objetos.Add(Bytes.GetBytes(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objetos.Add(Bytes.GetBytes(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        return objetos;
    }

    // ---- el flujo de contenido de una pagina --------------------------------

    /// <summary>El flujo que dibuja una pagina, en el lenguaje del PDF.</summary>
    /// <remarks>
    /// La instruccion de fuente solo se emite cuando cambia, y la de color tambien. No es
    /// por ahorrar bytes: un <c>Tf</c> por cada trazo llenaria el flujo de repeticiones y
    /// haria ilegible lo unico que se puede inspeccionar de un PDF escrito a mano.
    ///
    /// ⚠️ El color se fija a negro al abrir el bloque de texto SIEMPRE. El color de relleno
    /// es de estado, no de operacion: el que dejo puesto el ultimo rectangulo de la cinta
    /// seguiria vigente dentro del texto, y el informe entero saldria del color de la cinta.
    /// </remarks>
    private static byte[] FlujoDeUnaPagina(
        IReadOnlyList<(double Y, Linea Linea)> pagina,
        IReadOnlyList<Adorno> adornos)
    {
        var partes = new List<byte[]>
        {
            FlujoDeLosAdornos(adornos),
            Bytes.GetBytes("BT\n"),
            ColorDeRelleno(Negro),
        };

        (bool Negrita, int Tamano)? fuentePuesta = null;
        var colorPuesto = Negro;

        foreach (var (y, linea) in pagina)
        {
            if (linea.Trazos.Count == 0) continue;

            var fuente = (linea.Negrita, linea.Tamano);
            if (fuentePuesta != fuente)
            {
                partes.Add(Bytes.GetBytes($"{(linea.Negrita ? "/F2" : "/F1")} {linea.Tamano} Tf\n"));
                fuentePuesta = fuente;
            }

            foreach (var trazo in linea.Trazos)
            {
                var color = trazo.Color ?? Negro;
                if (!string.Equals(color, colorPuesto, StringComparison.Ordinal))
                {
                    partes.Add(ColorDeRelleno(color));
                    colorPuesto = color;
                }
                partes.Add(Bytes.GetBytes($"1 0 0 1 {(int)trazo.X} {(int)y} Tm\n"));
                partes.Add([
                    .. Bytes.GetBytes("("),
                    .. Escapar(WinAnsi.Codificar(trazo.Texto).Crudo),
                    .. Bytes.GetBytes(") Tj\n"),
                ]);
            }
        }

        partes.Add(Bytes.GetBytes("ET\n"));
        return Pegar(partes);
    }

    /// <summary>Lo que se pinta DETRAS del texto: la cinta de arriba y las rayas.</summary>
    /// <remarks>
    /// Va antes del bloque de texto a proposito. Un rectangulo dibujado despues taparia lo
    /// que hay debajo, y la cinta negra de la cabecera se comeria su propio titulo.
    /// </remarks>
    private static byte[] FlujoDeLosAdornos(IReadOnlyList<Adorno> adornos)
    {
        var partes = new List<byte[]>();
        foreach (var adorno in adornos)
        {
            switch (adorno)
            {
                case Rectangulo rectangulo:
                    partes.Add(ColorDeRelleno(rectangulo.Color));
                    partes.Add(Bytes.GetBytes(
                        $"{rectangulo.X} {rectangulo.Y} {rectangulo.Ancho} {rectangulo.Alto} re f\n"));
                    break;

                case Raya raya:
                    partes.Add(ColorDeTrazo(raya.Color));
                    partes.Add(Bytes.GetBytes(
                        raya.Grosor.ToString("0.00", CultureInfo.InvariantCulture) + " w\n"));
                    partes.Add(Bytes.GetBytes($"{raya.X} {raya.Y} m {raya.HastaX} {raya.Y} l S\n"));
                    break;
            }
        }
        return Pegar(partes);
    }

    // ---- color --------------------------------------------------------------

    /// <summary>La instruccion que fija el color con el que se rellena a partir de ahi.</summary>
    private static byte[] ColorDeRelleno(string? color) => Bytes.GetBytes(Componentes(color) + " rg\n");

    /// <summary>La instruccion que fija el color con el que se dibujan las rayas.</summary>
    private static byte[] ColorDeTrazo(string? color) => Bytes.GetBytes(Componentes(color) + " RG\n");

    /// <summary>Un <c>#RRGGBB</c> como los tres numeros de 0 a 1 que espera el PDF.</summary>
    /// <remarks>
    /// El PDF no entiende hexadecimal: sus operadores de color toman tres fracciones. Un
    /// color que no se puede leer sale NEGRO, que es el que ya tenia el documento antes de
    /// que existieran los colores: un fallo de formato no puede dejar un numero invisible.
    /// </remarks>
    private static string Componentes(string? color)
    {
        var crudo = (color ?? Negro).TrimStart('#');
        if (crudo.Length != 6 || !crudo.All(Uri.IsHexDigit)) crudo = Negro.TrimStart('#');

        var partes = new string[3];
        for (var i = 0; i < 3; i++)
        {
            var octeto = Convert.ToInt32(crudo.Substring(i * 2, 2), 16);
            partes[i] = (octeto / 255.0).ToString("0.000", CultureInfo.InvariantCulture);
        }
        return string.Join(' ', partes);
    }

    // ---- ensamblado ---------------------------------------------------------

    /// <summary>Pega los objetos con su tabla de referencias cruzadas y su trailer.</summary>
    /// <remarks>
    /// La tabla <c>xref</c> no es decorativa: un visor la usa para saltar a un objeto sin leer
    /// el archivo entero, y si los desplazamientos no cuadran AL BYTE el visor dice «archivo
    /// dañado». Por eso se anota la posicion real de cada objeto mientras se escribe, en vez
    /// de calcularla despues.
    /// </remarks>
    private static byte[] Ensamblar(List<byte[]> objetos)
    {
        var salida = new List<byte>(64 * 1024);
        salida.AddRange(Bytes.GetBytes("%PDF-1.4\n"));
        salida.AddRange([(byte)'%', 0xE1, 0xE9, 0xF1, (byte)'\n']);

        var desplazamientos = new List<int>(objetos.Count);
        for (var numero = 1; numero <= objetos.Count; numero++)
        {
            desplazamientos.Add(salida.Count);
            salida.AddRange(Bytes.GetBytes($"{numero} 0 obj\n"));
            salida.AddRange(objetos[numero - 1]);
            salida.AddRange(Bytes.GetBytes("\nendobj\n"));
        }

        var inicioXref = salida.Count;
        salida.AddRange(Bytes.GetBytes($"xref\n0 {objetos.Count + 1}\n"));
        salida.AddRange(Bytes.GetBytes("0000000000 65535 f \n"));
        foreach (var desplazamiento in desplazamientos)
        {
            salida.AddRange(Bytes.GetBytes($"{desplazamiento:D10} 00000 n \n"));
        }
        salida.AddRange(Bytes.GetBytes(
            $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF\n"));

        return [.. salida];
    }

    private static byte[] Pegar(List<byte[]> partes)
    {
        var salida = new byte[partes.Sum(p => p.Length)];
        var donde = 0;
        foreach (var parte in partes)
        {
            parte.CopyTo(salida, donde);
            donde += parte.Length;
        }
        return salida;
    }
}
