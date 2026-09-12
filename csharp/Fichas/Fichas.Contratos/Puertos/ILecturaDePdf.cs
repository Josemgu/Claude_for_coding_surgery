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
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Lectura.LecturaDePdf</c> (PDFium para rasterizar,
/// PdfPig para las anotaciones y RapidOcrNet, determinista, para el OCR),
/// <c>Fichas.Datos.Falso.LecturaDePdfFalsa</c> (no abre ningún archivo:
/// devuelve siempre lo mismo con la misma semilla) y <c>Fichas.App/Cascara/Servicios.LecturaPerezosa</c>,
/// que solo retrasa la carga del motor hasta la primera hoja. <b>Quién lo consume:</b> la
/// pantalla de Corrección para pintar la hoja y proponer campos, y
/// <c>Fichas.Lectura.LectorDeFormularios</c> al importar. Nada de aquí escribe en la base.
/// </para>
/// </remarks>
public interface ILecturaDePdf
{
    /// <summary>Cuantas hojas tiene el PDF; 0 si el archivo no se pudo abrir.</summary>
    /// <param name="rutaPdf">La ruta del archivo; vacía o inexistente da 0, sin lanzar.</param>
    /// <returns>El número de hojas, o 0 cuando no se pudo abrir: el 0 es la señal de ilegible.</returns>
    int ContarPaginas(string rutaPdf);

    /// <summary>Convierte una hoja en imagen PNG, con el ancho maximo que se pida; nula si no se pudo.</summary>
    /// <param name="rutaPdf">La ruta del archivo.</param>
    /// <param name="pagina">Qué hoja, base 1; 0 o fuera de rango da nulo.</param>
    /// <param name="anchoMaximo">Tope de ancho en píxeles; el alto sale de la proporción de la hoja.</param>
    /// <returns>La imagen con su tamaño real, o nulo si la hoja no se pudo rasterizar. Es la misma imagen que se pinta y que se le pasa al OCR: así la banda de cada línea cae donde se ve.</returns>
    ImagenDePagina? RasterizarPagina(string rutaPdf, int pagina, int anchoMaximo);

    /// <summary>Devuelve las anotaciones de una hoja: lo que alguien escribio o dibujo encima.</summary>
    /// <param name="rutaPdf">La ruta del archivo.</param>
    /// <param name="pagina">Qué hoja, base 1.</param>
    /// <returns>Las anotaciones con su banda en fracciones y su color, para distinguir un tachón de un subrayado; vacía si la hoja no tiene o no se pudo leer. Nunca nulo.</returns>
    IReadOnlyList<AnotacionDelPdf> LeerAnotaciones(string rutaPdf, int pagina);

    /// <summary>Pasa el OCR a una imagen y devuelve sus lineas con su confianza y su rectangulo.</summary>
    /// <param name="imagen">La hoja ya rasterizada por <see cref="RasterizarPagina"/>.</param>
    /// <returns>Las líneas tal como las leyó el motor, sin arreglar ninguna (regla permanente 1); vacía si no leyó nada. Es la señal de «sin texto» que va a <see cref="IIlegibles"/>.</returns>
    IReadOnlyList<LineaDeOcr> LeerConOcr(ImagenDePagina imagen);

    /// <summary>Los idiomas que el motor de OCR tiene instalados; si no hay espanol, hay que decirlo.</summary>
    /// <remarks>Hoy no lo llama ninguna pantalla (grep del 2026-09-11); se documenta y no se toca.</remarks>
    /// <returns>Los códigos de idioma que el motor reconoce (<c>es-MX</c>, <c>en-US</c>); vacía si el motor no está.</returns>
    IReadOnlyList<string> IdiomasDisponibles();
}
