using System.Globalization;
using System.Xml.Linq;
using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// En Revisar se ve de un vistazo si un documento está completo: color Y palabra, en los dos temas.
/// </summary>
/// <remarks>
/// <para>Palabras del dueño, 2026-09-06: <i>«Cuando estoy en revisar no hay nada que me indique
/// color o algo, si este está completo o no»</i>. Lo que faltaba era el color: la palabra ya
/// estaba (<c>TarjetaDeDocumento.PalabraDelEstado</c>).</para>
///
/// <para><b>La regla que se respeta al añadirlo ya estaba escrita en el código</b>, en el
/// comentario de <c>Asignar/RenglonParaAsignar.PalabraDe</c>: <i>«el color nunca va solo (mockup
/// v2)»</i>. Un color sin palabra deja la tarjeta muda para quien no distingue esos dos colores
/// o mira una pantalla mala, y este programa decide si alguien puede entrar al templo. Por eso
/// <see cref="ElColorNuncaVaSoloEnLaTarjeta"/> se pone roja si algún día alguien quita la
/// palabra y deja la pastilla de color.</para>
///
/// <para>⚠️ <b>Lo que NO miran:</b> cómo se ve. El contraste se calcula sobre los colores
/// declarados en el XAML, no sobre píxeles; las capturas de las dos pantallas van en la entrega.
/// El método es el mismo que ya usa <c>Cascara/PruebasDeLosDosTemas</c> con la paleta de
/// <c>App.xaml</c>, y por el mismo motivo: los colores son datos, y un dato se mide.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelColorDeLaTarjeta
{
    /// <summary>Lo mínimo que exige la WCAG para texto normal.</summary>
    private const double ContrasteMinimo = 4.5;

    /// <summary>
    /// Lo mínimo que se le pide a dos fondos para que no se confundan, en distancia RGB.
    /// </summary>
    /// <remarks>
    /// No hay norma que fije esto —la WCAG mide texto sobre fondo, no fondo contra fondo—, así
    /// que es un suelo elegido y medido, no heredado. 30 sobre un máximo posible de 441 deja
    /// fuera los pares que se leían igual en la primera paleta que se probó: gris #E7E9ED y
    /// azul pálido #DBE3F8 daban 17,3 y en una pantalla mala eran el mismo gris. El respaldo
    /// de verdad no es este número: es la palabra, que va siempre.
    /// </remarks>
    private const double SeparacionMinimaEntreFondos = 30.0;

    /// <summary>Las DOS lecturas que se ven en una tarjeta de Revisar.</summary>
    /// <remarks>
    /// ⛔ <b>Eran los cuatro <see cref="EstadoQueSeVe"/></b>, una pastilla por palabra, hasta el
    /// 2026-09-07. El dueno colapso las palabras a dos —<i>«Dos estados nada mas: resuelto y me
    /// falta»</i>— y con ellas los colores, porque la palabra y el color tienen que salir del
    /// mismo sitio. Lo que vigila esta clase no cambia: que cada pastilla traiga sus dos colores
    /// en los dos temas, que la palabra se lea encima, que los colores se distingan y que
    /// ninguna pastilla se quede muda.
    /// <para>Que los CUATRO estados de la base sigan siendo cuatro lo vigila
    /// <see cref="CadaTarjetaCaeEnElEstadoQueLeToca"/>, aqui abajo, que mira el enum y no la
    /// pantalla.</para>
    /// </remarks>
    private static readonly LoQueSeLee[] LasDos = Enum.GetValues<LoQueSeLee>();

    /// <summary>Cada lectura trae su fondo y su tinta en los DOS temas.</summary>
    /// <remarks>
    /// Una clave que solo esté en un tema deja esa pastilla pintada con el color del otro, que
    /// es el defecto que se midió el 2026-09-05 en la franja de avisos: 1,70:1.
    /// </remarks>
    [TestMethod]
    public void CadaEstadoTraeSuFondoYSuTintaEnLosDosTemas()
    {
        var claro = LosColoresDe("Light");
        var oscuro = LosColoresDe("Dark");

        Console.WriteLine($"Colores declarados: {claro.Count} en claro, {oscuro.Count} en oscuro.");

        var faltan = new List<string>();
        foreach (var estado in LasDos)
        {
            foreach (var clave in (string[])[EstadosQueSeVen.ClaveDelFondo(estado), EstadosQueSeVen.ClaveDeLaTinta(estado)])
            {
                if (!claro.ContainsKey(clave)) faltan.Add($"claro: falta «{clave}»");
                if (!oscuro.ContainsKey(clave)) faltan.Add($"oscuro: falta «{clave}»");
            }
        }

        Assert.HasCount(LasDos.Length * 2, claro, "Un fondo y una tinta por lectura, ni más ni menos.");
        CollectionAssert.AreEquivalent(claro.Keys.ToList(), oscuro.Keys.ToList());
        Assert.IsEmpty(faltan, string.Join(Environment.NewLine, faltan));
    }

    /// <summary>La palabra de cada lectura se lee sobre su color, en los dos temas.</summary>
    /// <param name="tema">«Light» o «Dark», el diccionario del XAML que se mide.</param>
    [TestMethod]
    [DataRow("Light")]
    [DataRow("Dark")]
    public void LaPalabraDeCadaLecturaSeLeeSobreSuColor(string tema)
    {
        var colores = LosColoresDe(tema);
        var malos = new List<string>();

        foreach (var estado in LasDos)
        {
            var fondo = colores[EstadosQueSeVen.ClaveDelFondo(estado)];
            var tinta = colores[EstadosQueSeVen.ClaveDeLaTinta(estado)];
            var razon = Contraste(tinta, fondo);

            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-5} {1,-22} «{2}» {3} sobre {4}  {5,5:N2}:1",
                tema, estado, DosEstados.Palabra(estado), tinta, fondo, razon));

            if (razon < ContrasteMinimo)
                malos.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} da {2:N2}:1 y hacen falta {3:N1}:1", tema, estado, razon, ContrasteMinimo));
        }

        Assert.IsEmpty(malos, string.Join(Environment.NewLine, malos));
    }

    /// <summary>Los dos colores se distinguen entre sí; si no, el color no indica nada.</summary>
    /// <param name="tema">«Light» o «Dark», el diccionario del XAML que se mide.</param>
    [TestMethod]
    [DataRow("Light")]
    [DataRow("Dark")]
    public void LosDosColoresSeDistinguenEntreSi(string tema)
    {
        var colores = LosColoresDe(tema);
        var juntos = new List<string>();

        foreach (var uno in LasDos)
        {
            foreach (var otro in LasDos.Where(e => e > uno))
            {
                var fondoUno = colores[EstadosQueSeVen.ClaveDelFondo(uno)];
                var fondoOtro = colores[EstadosQueSeVen.ClaveDelFondo(otro)];
                var separacion = Separacion(fondoUno, fondoOtro);

                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-5} {1,-22} {2} vs {3,-22} {4}  separación {5,5:N1}",
                    tema, uno, fondoUno, otro, fondoOtro, separacion));

                if (separacion < SeparacionMinimaEntreFondos)
                    juntos.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: {1} ({2}) y {3} ({4}) se separan {5:N1} y hace falta {6:N1}",
                        tema, uno, fondoUno, otro, fondoOtro, separacion, SeparacionMinimaEntreFondos));
            }
        }

        Assert.IsEmpty(juntos, string.Join(Environment.NewLine, juntos));
    }

    /// <summary>
    /// El color nunca va solo: cada pastilla de color lleva dentro la palabra del estado.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la regla del mockup v2, y se comprueba sobre el XAML porque es ahí donde se
    /// rompería: basta con que alguien borre el <c>TextBlock</c> de dentro del <c>Border</c>
    /// para dejar una mancha de color sin palabra. La prueba cuenta las pastillas y exige que
    /// sean las dos, para que no se compruebe con cero.
    /// </remarks>
    [TestMethod]
    public void ElColorNuncaVaSoloEnLaTarjeta()
    {
        var pantalla = XDocument.Load(LaPantallaDeRevisar());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var mudas = new List<string>();
        var pastillas = pantalla.Descendants()
            .Where(e => e.Name.LocalName == "Border"
                        && (e.Attribute("Background")?.Value ?? string.Empty).Contains("FondoDelEstado", StringComparison.Ordinal))
            .ToList();

        foreach (var pastilla in pastillas)
        {
            var fondo = pastilla.Attribute("Background")!.Value;
            var dicePalabra = pastilla.Descendants()
                .Any(e => e.Name.LocalName == "TextBlock"
                          && (e.Attribute("Text")?.Value ?? string.Empty)
                              .Contains(nameof(TarjetaDeDocumento.PalabraDelEstado), StringComparison.Ordinal));

            Console.WriteLine($"Pastilla {fondo}: {(dicePalabra ? "con palabra" : "MUDA")}.");
            if (!dicePalabra) mudas.Add($"La pastilla con {fondo} no lleva la palabra del estado.");
        }

        Assert.HasCount(
            LasDos.Length, pastillas, "Hace falta una pastilla por lectura; con menos no se comprueba nada.");
        Assert.IsEmpty(
            mudas,
            "El color nunca va solo (mockup v2): una pastilla de color sin palabra deja la "
            + "tarjeta muda para quien no distingue esos colores." + Environment.NewLine
            + string.Join(Environment.NewLine, mudas));
    }

    /// <summary>Cada tarjeta cae en el estado que le toca, y el archivado pasado en el suyo.</summary>
    [TestMethod]
    public void CadaTarjetaCaeEnElEstadoQueLeToca()
    {
        Assert.AreEqual(
            EstadoQueSeVe.SinRevisar,
            EstadosQueSeVen.DeLaTarjeta(EstadoDeRecomendacion.SinMarcar, archivado: false, fechaYaPasada: false));
        Assert.AreEqual(
            EstadoQueSeVe.NoCompleta,
            EstadosQueSeVen.DeLaTarjeta(EstadoDeRecomendacion.NoCompleta, archivado: false, fechaYaPasada: true));
        Assert.AreEqual(
            EstadoQueSeVe.Completa,
            EstadosQueSeVen.DeLaTarjeta(EstadoDeRecomendacion.Completa, archivado: false, fechaYaPasada: false));
        Assert.AreEqual(
            EstadoQueSeVe.FechaPasadaCompletada,
            EstadosQueSeVen.DeLaTarjeta(EstadoDeRecomendacion.NoCompleta, archivado: true, fechaYaPasada: true));
    }

    /// <summary>
    /// ⚠️ Los CUATRO estados que se ven siguen siendo cuatro, y cada uno cae en su lectura.
    /// </summary>
    /// <remarks>
    /// Es la mitad del pase del 2026-09-07 que se pone roja si alguien colapsa de más: las
    /// palabras son dos, el enum sigue teniendo cuatro valores y cada uno tiene que saber en
    /// cuál de las dos cae. «Completa» y «fecha pasada completada» no le dejan nada que hacer;
    /// las otras dos sí.
    /// </remarks>
    [TestMethod]
    public void LosCuatroEstadosSiguenSiendoCuatroYCadaUnoCaeEnSuLectura()
    {
        var cuatro = Enum.GetValues<EstadoQueSeVe>();
        Assert.HasCount(4, cuatro, "En la base siguen siendo cuatro; lo que se colapsa es la palabra.");

        Assert.AreEqual(LoQueSeLee.Resuelto, EstadosQueSeVen.LoQueSeLeeDe(EstadoQueSeVe.Completa));
        Assert.AreEqual(LoQueSeLee.Resuelto, EstadosQueSeVen.LoQueSeLeeDe(EstadoQueSeVe.FechaPasadaCompletada));
        Assert.AreEqual(LoQueSeLee.MeFalta, EstadosQueSeVen.LoQueSeLeeDe(EstadoQueSeVe.NoCompleta));
        Assert.AreEqual(LoQueSeLee.MeFalta, EstadosQueSeVen.LoQueSeLeeDe(EstadoQueSeVe.SinRevisar));
    }

    // ---- de donde salen las cifras -------------------------------------------

    /// <summary>Los colores de un tema, leídos del XAML de Revisar, por clave y color.</summary>
    /// <param name="tema">«Light» o «Dark»; si el XAML no declara ese diccionario, la prueba falla aquí.</param>
    private static Dictionary<string, string> LosColoresDe(string tema)
    {
        var doc = XDocument.Load(LaPantallaDeRevisar());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var diccionario = doc.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary")
            .FirstOrDefault(e => (string?)e.Attribute(x + "Key") == tema);

        Assert.IsNotNull(diccionario, $"La pantalla de Revisar no declara el diccionario del tema «{tema}».");

        return diccionario.Descendants()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                e => (string)e.Attribute(x + "Key")!,
                e => (string)e.Attribute("Color")!,
                StringComparer.Ordinal);
    }

    /// <summary>La razón de contraste de la WCAG entre dos colores «#RRGGBB».</summary>
    /// <param name="uno">Un color «#RRGGBB».</param>
    /// <param name="otro">El otro color «#RRGGBB».</param>
    private static double Contraste(string uno, string otro)
    {
        var a = Luminancia(uno);
        var b = Luminancia(otro);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>La luminancia relativa de la WCAG de un color «#RRGGBB».</summary>
    /// <param name="color">El color «#RRGGBB».</param>
    private static double Luminancia(string color)
    {
        double Canal(int desde)
        {
            var v = Byte(color, desde) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(0)) + (0.7152 * Canal(2)) + (0.0722 * Canal(4));
    }

    /// <summary>Cuánto se separan dos colores, en distancia euclídea sobre los tres canales.</summary>
    /// <param name="uno">Un color «#RRGGBB».</param>
    /// <param name="otro">El otro color «#RRGGBB».</param>
    private static double Separacion(string uno, string otro)
        => Math.Sqrt(
            Math.Pow(Byte(uno, 0) - Byte(otro, 0), 2)
            + Math.Pow(Byte(uno, 2) - Byte(otro, 2), 2)
            + Math.Pow(Byte(uno, 4) - Byte(otro, 4), 2));

    /// <summary>Un canal de un color «#RRGGBB», de 0 a 255.</summary>
    /// <param name="color">El color «#RRGGBB»; con otra forma la prueba falla aquí.</param>
    /// <param name="desde">En qué posición de los seis dígitos empieza el canal: 0, 2 o 4.</param>
    private static int Byte(string color, int desde)
    {
        var limpio = color.TrimStart('#');
        Assert.HasCount(6, limpio, $"«{color}» no tiene la forma #RRGGBB.");
        return int.Parse(limpio.Substring(desde, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>El XAML de la pantalla de Revisar, o no concluyente si no se encuentra.</summary>
    private static string LaPantallaDeRevisar()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var pantalla = Path.Combine(
                actual.FullName, "csharp", "Fichas", "Fichas.App", "Revisar", "PaginaDeRevisar.xaml");
            if (File.Exists(pantalla)) return pantalla;
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «Fichas.App/Revisar/PaginaDeRevisar.xaml» desde «{AppContext.BaseDirectory}». "
            + "Sin la pantalla delante esto no comprueba nada.");
        return string.Empty;
    }
}
