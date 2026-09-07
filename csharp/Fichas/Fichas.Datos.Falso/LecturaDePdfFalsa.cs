using Fichas.Contratos.Lectura;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Una lectura de PDF que no lee ningun PDF: devuelve siempre lo mismo, con la misma semilla.
/// </summary>
/// <remarks>
/// ⚠️ Esto NO es OCR y no lo pretende. Existe para que la pantalla de correccion se pueda
/// construir antes de que exista Fichas.Lectura (fase C3). El motor de verdad lo decide la
/// FASE C0 con los PDF reales del dueno; hasta entonces, aqui no hay nada medido.
/// </remarks>
public sealed class LecturaDePdfFalsa : ILecturaDePdf
{
    private readonly int _semilla;

    /// <summary>Se ata a la misma semilla que el resto de lo inventado.</summary>
    public LecturaDePdfFalsa(int semilla) => _semilla = semilla;

    /// <summary>Dice que todo PDF tiene seis hojas, que es el tamano de un grupo real.</summary>
    public int ContarPaginas(string rutaPdf) => string.IsNullOrWhiteSpace(rutaPdf) ? 0 : 6;

    /// <summary>Devuelve una imagen inventada del tamano que se pida; el PNG va vacio.</summary>
    public ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf) || pagina < 1) return null;
        var ancho = Math.Clamp(anchoMaximo, 1, 3500);
        // Proporcion carta: 8,5 x 11 pulgadas.
        var alto = (int)Math.Round(ancho * 11.0 / 8.5);
        return new ImagenDePagina(pagina, ancho, alto, Array.Empty<byte>());
    }

    /// <summary>Devuelve dos anotaciones inventadas: una roja fina y una verde gruesa.</summary>
    public IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf) || pagina < 1) return [];
        return
        [
            // Los dos colores son los que la sonda de la FASE C0 busca en los PDF reales.
            new AnotacionDelPdf("Ink", null, new BandaDeLaPagina(0.10, 0.20, 0.45, 0.23), 0.890, 0.094, 0.176, 1.65),
            new AnotacionDelPdf("Ink", null, new BandaDeLaPagina(0.10, 0.40, 0.62, 0.46), 0.494, 0.765, 0.0, 16.5),
        ];
    }

    /// <summary>Devuelve unas lineas inventadas con la forma de las de un formulario.</summary>
    public IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen)
    {
        var sorteo = new SorteoDeterminista(_semilla + imagen.Pagina);
        var lineas = new List<LineaDeOcr>();
        string[] textos =
        [
            "Temple Recommend / Recomendacion para el templo",
            "Ward/Branch Name and Unit Number: Castries Branch 7000011",
            "Temple Name: Santo Domingo Dominican Republic",
            "Travel Date / Fecha de viaje: 2026-08-25",
            "Membership Record Number",
        ];
        for (var i = 0; i < textos.Length; i++)
        {
            var y = 0.06 + (i * 0.07);
            lineas.Add(new LineaDeOcr(
                textos[i],
                0.70 + (sorteo.Hasta(30) / 100.0),
                new BandaDeLaPagina(0.08, y, 0.92, y + 0.045)));
        }
        return lineas;
    }

    /// <summary>Dice que hay espanol e ingles, que es lo medido en esta maquina el 2026-09-04.</summary>
    public IReadOnlyList<string> IdiomasDisponibles() => ["es-MX", "en-US"];
}
