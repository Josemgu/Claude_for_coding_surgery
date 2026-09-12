namespace Fichas.App.Importar;

/// <summary>
/// Los PDF que hay en lo que se eligio: archivos sueltos y carpetas enteras.
/// </summary>
/// <remarks>
/// Portado de <c>importacion/tanda.py</c> (<c>rutas_de_pdf</c>). Acepta las dos cosas
/// mezcladas porque las dos las elige Miguel: unos cuantos archivos con Ctrl, o la
/// carpeta del mes entera.
///
/// <para>Una carpeta se recorre CON SUS SUBCARPETAS: los escaneos llegan repartidos por
/// meses, y obligar a entrar carpeta por carpeta seria devolverle el problema que la
/// pantalla viene a resolver —«necesito cargar 500 pdf», dijo el dueno—.</para>
///
/// <para>⚠️ Aqui NO se abre ningun archivo para ver si de verdad es un PDF. Eso solo se
/// sabe abriendolo, y abrir 500 archivos dos veces cuesta el doble; el que no se pueda
/// abrir deja su renglon cuando le toque.</para>
/// </remarks>
public static class RutasDePdf
{
    /// <summary>La extension que se busca, mirada sin distinguir mayusculas.</summary>
    public const string ExtensionDePdf = ".pdf";

    /// <summary>
    /// Los PDF de esos origenes, ordenados y sin repetidos, y lo que se quedo fuera.
    /// </summary>
    /// <remarks>
    /// Ordenados porque la barra de progreso tiene que avanzar por un orden que Miguel
    /// pueda seguir en su carpeta; sin repetidos porque elegir un archivo y ademas su
    /// carpeta es un descuido normal, y procesarlo dos veces produciria un duplicado
    /// avisado que parece un fallo del programa cuando no lo es.
    ///
    /// <para>Un origen que no existe se ignora. Una carpeta en la que el sistema no deja
    /// entrar tampoco detiene nada (requisito 9), pero NO se calla: sale nombrada en
    /// <see cref="LoQueSeEncontro.CarpetasQueNoSeDejaronLeer"/>.</para>
    /// </remarks>
    /// <param name="origenes">Archivos y carpetas mezclados, tal como salieron del selector.</param>
    public static LoQueSeEncontro Reunir(IEnumerable<string> origenes)
    {
        ArgumentNullException.ThrowIfNull(origenes);

        var recorrido = new RecorridoDeLoElegido();
        foreach (var origen in origenes) recorrido.Agregar(origen);

        return recorrido.LoQueSalio();
    }

    /// <summary>Si el nombre acaba en «.pdf», da igual como este escrito.</summary>
    /// <param name="ruta">La ruta o el nombre del archivo.</param>
    internal static bool EsPdf(string ruta)
        => Path.GetExtension(ruta).Equals(ExtensionDePdf, StringComparison.OrdinalIgnoreCase);
}
