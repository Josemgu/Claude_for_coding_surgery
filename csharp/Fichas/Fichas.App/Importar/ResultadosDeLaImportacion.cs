using Fichas.Contratos.Modelos;

namespace Fichas.App.Importar;

/// <summary>Que paso con UNA hoja de un PDF.</summary>
/// <remarks>
/// ⛔ <see cref="Entro"/> en falso NO significa «rechazada»: hoy solo lo devuelve la hoja
/// que no se pudo leer, que deja su renglon con el motivo. Ninguna hoja legible se
/// rechaza nunca (DECISIONES.md, 2026-09-03).
/// </remarks>
/// <param name="Entro">Si de esta hoja quedo algo guardado.</param>
/// <param name="CasoNuevo">Si abrio un caso propio, en vez de unirse a otro.</param>
/// <param name="CasoId">El caso al que fue a parar; nulo si no entro.</param>
/// <param name="NumeroCaso">El numero con el que quedo guardada; nulo si no se pudo.</param>
/// <param name="PaginaPdf">Que hoja del archivo es, base 1.</param>
/// <param name="Personas">Cuantas personas entraron de esta hoja.</param>
/// <param name="Avisos">Lo que hay que decir de ella; puede estar vacio.</param>
/// <param name="PendienteDeIdentificar">Entro sin numero de caso y alguien tiene que teclearlo.</param>
/// <param name="DuplicadoDe">El caso que esta hoja repite, o nulo.</param>
/// <param name="Renglones">Los renglones de ilegibles que dejo; pueden ser varios.</param>
public sealed record ResultadoDeLaHoja(
    bool Entro,
    bool CasoNuevo,
    long? CasoId,
    string? NumeroCaso,
    int PaginaPdf,
    int Personas,
    IReadOnlyList<Aviso> Avisos,
    bool PendienteDeIdentificar,
    long? DuplicadoDe,
    IReadOnlyList<string> Renglones);

/// <summary>Las cifras de UN documento, ya leido y guardado.</summary>
/// <param name="RutaPdf">De que archivo son.</param>
/// <param name="Hojas">Cuantas hojas traia.</param>
/// <param name="Casos">Cuantos CASOS distintos salieron; no es lo mismo que hojas.</param>
/// <param name="Personas">Cuantas personas entraron en total.</param>
/// <param name="Pendientes">Cuantas hojas entraron sin numero de caso.</param>
/// <param name="Duplicados">Cuantos casos nacieron repitiendo a otro que ya estaba.</param>
/// <param name="Ilegibles">Cuantas hojas no se pudieron leer.</param>
/// <param name="Error">El fallo que impidio leerlo entero, o nulo.</param>
/// <param name="Segundos">Lo que tardo de punta a punta.</param>
public sealed record ResultadoDeUnDocumento(
    string RutaPdf,
    int Hojas,
    int Casos,
    int Personas,
    int Pendientes,
    int Duplicados,
    int Ilegibles,
    string? Error,
    double Segundos)
{
    /// <summary>Los casos que nacieron de este documento, por su numero interno.</summary>
    /// <remarks>
    /// <para>Va fuera de la lista de arriba —y con valor por defecto— porque no es una cifra
    /// del resumen: es lo que hace falta DESPUES, cuando la pantalla ensena las carpetas que
    /// esta tanda va a formar en Revisar y lo que entro sin ninguna persona. Sin esto habria
    /// que volver a leer la tabla entera y filtrarla por la ruta de cada PDF.</para>
    /// <para><see cref="Casos"/> sigue siendo la cifra que se dice, y esta lista tiene
    /// exactamente esa longitud: las dos salen del mismo recuento.</para>
    /// </remarks>
    public IReadOnlyList<long> CasoIds { get; init; } = [];
}
