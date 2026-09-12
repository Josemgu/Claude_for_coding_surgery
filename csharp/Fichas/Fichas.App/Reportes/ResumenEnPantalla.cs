using System.Globalization;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Reportes;

/// <summary>
/// Lo que una accion deja escrito en la pantalla: UNA linea, el detalle detras de «ver»,
/// y la ruta del archivo si se llego a escribir alguno.
/// </summary>
/// <remarks>
/// <para>Requisito 4 del dueno, «ni un parrafo en pantalla»: <see cref="Linea"/> es lo unico
/// que se pinta y cabe en un renglon; <see cref="Detalle"/> solo aparece al pulsar «ver».</para>
///
/// <para>⚠️ <b>Los avisos VIAJAN aqui dentro; no los deja la operacion en la franja.</b> Y es
/// por un fallo medido el 2026-09-04 abriendo la ventana: generar corre fuera del hilo de la
/// interfaz —tiene que correr fuera, son segundos—, y dejar un aviso hace que la franja se
/// repinte sola. Repintar desde otro hilo revienta con <c>COMException 0x8001010E</c>
/// (RPC_E_WRONG_THREAD): el PDF quedaba escrito y la pantalla no decia nada. Ninguna prueba
/// sin ventana podia verlo, porque sin ventana no hay franja que repintar.</para>
/// </remarks>
/// <param name="SalioBien">Si la accion termino con lo que prometia; decide el color, no si se sigue.</param>
/// <param name="Linea">El renglon que se pinta.</param>
/// <param name="Detalle">Lo largo, que solo se ve al pulsar «ver».</param>
/// <param name="Ruta">Donde quedo el archivo, o nulo si no hay ninguno que abrir.</param>
/// <param name="Avisos">Lo que hay que dejar en la franja, DESDE EL HILO DE LA VENTANA.</param>
public sealed record ResumenEnPantalla(
    bool SalioBien,
    string Linea,
    string Detalle,
    string? Ruta,
    IReadOnlyList<Aviso> Avisos)
{
    /// <summary>Junta las lineas y los detalles de unos avisos en un solo texto para «ver».</summary>
    /// <param name="avisos">Los avisos; cada uno aporta su línea y, si lo tiene, su detalle debajo.</param>
    /// <param name="alFinal">Párrafos que van después de los avisos; los vacíos se saltan.</param>
    /// <returns>Los trozos separados por una línea en blanco; vacío si no hay nada.</returns>
    public static string DetalleDe(IReadOnlyList<Aviso> avisos, params string[] alFinal)
    {
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(alFinal);

        var trozos = avisos
            .Select(aviso => string.IsNullOrWhiteSpace(aviso.Detalle)
                ? aviso.Linea
                : aviso.Linea + Environment.NewLine + aviso.Detalle)
            .Concat(alFinal.Where(texto => !string.IsNullOrWhiteSpace(texto)));

        return string.Join(Environment.NewLine + Environment.NewLine, trozos);
    }

    /// <summary>
    /// Cuanto ocupa ese archivo, o nulo si no esta o no se puede mirar.
    /// </summary>
    /// <remarks>
    /// La cifra no es un adorno: es la unica forma de que la pantalla afirme que el archivo
    /// existe habiendolo mirado, en vez de repetir lo que dijo quien lo escribio. El nulo se
    /// pinta como «no está», nunca como cero.
    /// </remarks>
    /// <param name="ruta">El archivo que se mira.</param>
    public static long? TamanoDe(string ruta)
    {
        try
        {
            var archivo = new FileInfo(ruta);
            return archivo.Exists ? archivo.Length : null;
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException
                                          or ArgumentException or NotSupportedException)
        {
            // No se calla: se devuelve el nulo, y quien llama lo pinta como «no se pudo mirar».
            return null;
        }
    }

    /// <summary>«1234 bytes», sin separador de miles.</summary>
    /// <remarks>
    /// Sin separador a proposito: el punto y la coma significan lo contrario en castellano y
    /// en ingles, y un «1.234 bytes» leido por la maquina equivocada es un archivo mil veces
    /// mas pequeno de lo que es.
    /// </remarks>
    /// <param name="cuantos">El tamaño en bytes.</param>
    public static string EnBytes(long cuantos)
        => cuantos.ToString(CultureInfo.InvariantCulture) + " bytes";
}
