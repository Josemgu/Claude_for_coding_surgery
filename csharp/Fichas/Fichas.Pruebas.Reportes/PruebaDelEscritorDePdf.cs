using System.Text;
using Fichas.Reportes.Formato;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El formato de archivo PDF escrito a mano: WinAnsi, escapado, objetos y tabla xref.
/// </summary>
/// <remarks>
/// Portado de reportes/formato_pdf.py. La tabla `xref` no es decorativa: si los
/// desplazamientos no cuadran AL BYTE el visor dice «archivo dañado», asi que aqui se
/// comprueban los bytes y no el aspecto.
/// </remarks>
[TestClass]
public class PruebaDelEscritorDePdf
{
    /// <summary>Los bytes leídos como Latin-1, que es byte a byte lo que escribe el motor, para poder buscar texto dentro.</summary>
    /// <param name="crudo">Los bytes del PDF o de un trozo.</param>
    private static string ComoLatino(byte[] crudo) => Encoding.Latin1.GetString(crudo);

    /// <summary>Vigila que tildes y eñes se codifican sin perder ninguno.</summary>
    [TestMethod]
    public void WinAnsiGuardaLasTildesYLasEnesSinPerderNada()
    {
        var (crudo, perdidos) = WinAnsi.Codificar("Preparación para el niño");

        Assert.AreEqual(0, perdidos);
        Assert.AreEqual("Preparación para el niño", ComoLatino(crudo));
    }

    /// <summary>Vigila que un ideograma sale como «?» y se cuenta, uno por carácter.</summary>
    [TestMethod]
    public void WinAnsiCuentaLosCaracteresQueNoCaben()
    {
        // Un signo fuera de cp1252 sale como «?» y se CUENTA: un nombre alterado en
        // silencio es un dato falso sobre una persona.
        var (crudo, perdidos) = WinAnsi.Codificar("Ana 中文 Anonimo");

        Assert.AreEqual(2, perdidos);
        Assert.AreEqual("Ana ?? Anonimo", ComoLatino(crudo));
    }

    /// <summary>Vigila que la barra y los dos paréntesis salen escapados con barra.</summary>
    [TestMethod]
    public void EscaparCubreLosTresCaracteresQueUnLiteralDePdfNoAdmite()
    {
        var crudo = Encoding.Latin1.GetBytes(@"a\b(c)d");

        Assert.AreEqual(@"a\\b\(c\)d", ComoLatino(EscritorDePdf.Escapar(crudo)));
    }

    /// <summary>Vigila la cabecera, el cierre, el catálogo y las fuentes base con WinAnsi de un PDF de una página vacía.</summary>
    [TestMethod]
    public void ElPdfAbreConSuCabeceraYCierraConSuEof()
    {
        var bytes = EscritorDePdf.BytesDelPdf([[]], null);
        var texto = ComoLatino(bytes);

        StringAssert.StartsWith(texto, "%PDF-1.4");
        StringAssert.EndsWith(texto, "%%EOF\n");
        StringAssert.Contains(texto, "/Type /Catalog");
        StringAssert.Contains(texto, "/BaseFont /Helvetica");
        StringAssert.Contains(texto, "/Encoding /WinAnsiEncoding");
    }

    /// <summary>Vigila que cada desplazamiento de la tabla xref cae justo en el «N 0 obj» de su objeto.</summary>
    [TestMethod]
    public void LaTablaXrefApuntaAlByteExactoDeCadaObjeto()
    {
        var pagina = new List<(double Y, Linea Linea)>
        {
            (500, new Linea(false, 10, [new Trazo(36, "Hola")], false)),
        };

        var bytes = EscritorDePdf.BytesDelPdf([pagina], null);
        var texto = ComoLatino(bytes);

        // Se busca «\nxref\n» y no «xref\n»: la palabra «startxref» del final TERMINA en «xref»,
        // y buscar la corta encuentra esa. Es exactamente la clase de detalle por la que esta
        // prueba mira bytes y no aspecto.
        var inicio = texto.LastIndexOf("\nxref\n", StringComparison.Ordinal) + 1;
        Assert.IsGreaterThan(0, inicio, "No hay tabla xref.");

        var renglones = texto[inicio..].Split('\n');
        // renglones[0] = "xref", [1] = "0 N", [2] = la entrada libre, y desde [3] los objetos.
        var cuantos = int.Parse(renglones[1].Split(' ')[1]);
        for (var objeto = 1; objeto < cuantos; objeto++)
        {
            var desplazamiento = int.Parse(renglones[2 + objeto][..10]);
            var cabecera = $"{objeto} 0 obj";
            Assert.AreEqual(
                cabecera,
                texto.Substring(desplazamiento, cabecera.Length),
                $"El objeto {objeto} no empieza donde dice la xref.");
        }
    }

    /// <summary>Vigila que tres páginas dan /Count 3, los hijos 3, 5 y 7, y el papel Carta vertical.</summary>
    [TestMethod]
    public void UnaPaginaEsUnObjetoYSusFlujosVanEnLosImpares()
    {
        var vacia = new List<(double, Linea)>();
        var texto = ComoLatino(EscritorDePdf.BytesDelPdf([vacia, vacia, vacia], null));

        StringAssert.Contains(texto, "/Count 3");
        StringAssert.Contains(texto, "/Kids [3 0 R 5 0 R 7 0 R]");
        StringAssert.Contains(texto, "/MediaBox [0 0 612 792]");
    }

    /// <summary>Vigila que el rectángulo de la cinta va antes del bloque BT en el flujo.</summary>
    [TestMethod]
    public void LosAdornosSePintanAntesDelTextoParaNoTaparlo()
    {
        static (IReadOnlyList<Adorno> Adornos, IReadOnlyList<(double Y, Linea Linea)> Lineas) Marco(int numero, int total)
            => ([new Rectangulo(0, 570, 792, 42, "#151515")],
                [(586.0, new Linea(true, 11, [new Trazo(36, "Preparación para el templo", "#FFFFFF")], false))]);

        var texto = ComoLatino(EscritorDePdf.BytesDelPdf([new List<(double, Linea)>()], Marco));

        var rectangulo = texto.IndexOf("0 570 792 42 re f", StringComparison.Ordinal);
        var abreTexto = texto.IndexOf("BT\n", StringComparison.Ordinal);
        Assert.IsTrue(rectangulo > 0 && abreTexto > rectangulo,
            "El rectangulo tiene que ir ANTES del bloque de texto o se lo come.");
    }

    /// <summary>Vigila que lo primero tras BT es el «rg» del negro, para que la cinta no tiña el texto.</summary>
    [TestMethod]
    public void ElColorSeFijaANegroAlAbrirElTexto()
    {
        // El color de relleno es de estado: el que dejo puesto el ultimo rectangulo
        // seguiria vigente dentro del texto y el informe saldria del color de la cinta.
        var texto = ComoLatino(EscritorDePdf.BytesDelPdf([new List<(double, Linea)>()], null));
        var abreTexto = texto.IndexOf("BT\n", StringComparison.Ordinal);

        StringAssert.StartsWith(texto[(abreTexto + 3)..], "0.082 0.082 0.082 rg");
    }

    /// <summary>Vigila que un color que no es #RRGGBB cae a negro y el texto se escribe igual.</summary>
    [TestMethod]
    public void UnColorIlegibleSaleNegroYNoInvisible()
    {
        var pagina = new List<(double, Linea)>
        {
            (500, new Linea(false, 8, [new Trazo(36, "cifra", "no-es-un-color")], false)),
        };

        var texto = ComoLatino(EscritorDePdf.BytesDelPdf([pagina], null));

        StringAssert.Contains(texto, "(cifra) Tj");
        Assert.DoesNotContain("1.000 1.000 1.000 rg", texto, "Un color roto no puede dejar la cifra en blanco sobre blanco.");
    }
}
