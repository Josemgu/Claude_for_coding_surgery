using Fichas.Contratos.Lectura;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Leer un PDF: rasterizar una hoja, sacar sus anotaciones y pasarle el OCR.
/// </summary>
/// <remarks>
/// ⛔ Regla permanente 1: aqui NO entra ninguna IA generativa, ni para leer ni para
/// «arreglar» lo leido. Lo que devuelve <see cref="LeerConOcr"/> es lo que el motor leyo,
/// tal cual. Un PDF que no se puede leer no lanza: devuelve la lista vacia y su aviso,
/// y quien llama deja el renglon en <see cref="IIlegibles"/>.
/// </remarks>
public interface ILecturaDePdf
{
    /// <summary>Cuantas hojas tiene el PDF; 0 si el archivo no se pudo abrir.</summary>
    int ContarPaginas(string rutaPdf);

    /// <summary>Convierte una hoja en imagen PNG, con el ancho maximo que se pida; nula si no se pudo.</summary>
    ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo);

    /// <summary>Devuelve las anotaciones de una hoja: lo que alguien escribio o dibujo encima.</summary>
    IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina);

    /// <summary>Pasa el OCR a una imagen y devuelve sus lineas con su confianza y su rectangulo.</summary>
    IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen);

    /// <summary>Los idiomas que el motor de OCR tiene instalados; si no hay espanol, hay que decirlo.</summary>
    IReadOnlyList<string> IdiomasDisponibles();
}
