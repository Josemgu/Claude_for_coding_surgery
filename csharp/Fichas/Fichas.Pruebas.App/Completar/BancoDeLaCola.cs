using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Completar;

/// <summary>
/// Lo comun a las pruebas de la cola que necesitan un documento COMO LO DEJA LA IMPORTACION.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Existe por un hallazgo que hay que dejar escrito, porque no es obvio y costo
/// una prueba roja.</b> <c>BaseDeInicio.MeterCaso</c> mete el caso y sus personas pero NO
/// escribe ni una fila de <c>procedencia_campo</c>, y sobre esa base
/// <see cref="ModeloDeCorreccion.ListoParaAsignar"/> es SIEMPRE falso: <c>EstadosDeCampo</c>
/// da por dudoso todo campo sin procedencia —«procedencia is null → dudoso»—, aunque tenga
/// valor y sea valido. Un documento asi no puede salir de la cola por mucho que se rellene.</para>
///
/// <para>La importacion de verdad SI escribe esas filas, con su origen y su confianza, asi
/// que el caso de prueba tiene que traerlas para parecerse a lo que el dueno tiene delante.
/// Aqui se anotan sobre los campos que el propio modelo dibuja, sin copiar ningun nombre de
/// columna: copiarlos seria una segunda lista que se queda vieja sin avisar.</para>
///
/// <para>⛔ Lo que NO se escribe aqui es ninguna firma: <c>Verificado</c> se queda en falso
/// en todas las filas. La regla permanente 5 tambien vale en las pruebas, y si el montaje
/// firmara, las pruebas de que la cola no firma saldrian verdes estando mal.</para>
/// </remarks>
internal static class BancoDeLaCola
{
    /// <summary>Un modelo de correccion atado a unos servicios falsos.</summary>
    public static ModeloDeCorreccion ModeloDe(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, servicios.Procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
    }

    /// <summary>
    /// Le da a ese documento la procedencia que le habria dejado la importacion: leido de
    /// una anotacion del PDF, con confianza 1,00 y SIN firmar.
    /// </summary>
    /// <remarks>
    /// Confianza 1,00 y origen «anotacion» son los valores que el supervisor midio el
    /// 2026-09-05 sobre los siete escaneos reales del dueno para <c>numero_caso</c> y
    /// <c>fecha_viaje</c>. Se usan para todos los campos porque lo que la prueba necesita es
    /// que NINGUN campo sea dudoso por su procedencia: asi el unico dudoso que queda es el
    /// hueco que la prueba dejo a proposito, y cuando falla se sabe cual era.
    /// </remarks>
    /// <param name="servicios">Donde vive la base inventada.</param>
    /// <param name="casoId">El documento al que se le anota la procedencia.</param>
    public static void DarleLaProcedenciaDeLaImportacion(ServiciosFalsos servicios, long casoId)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        var modelo = ModeloDe(servicios);
        modelo.Cargar(casoId);

        foreach (var campo in modelo.Campos)
        {
            servicios.Procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = campo.Tabla,
                RegistroId = campo.RegistroId,
                Campo = campo.Campo,
                Origen = OrigenDeCampo.Anotacion,
                Confianza = 1.0,
            });
        }
    }

    /// <summary>Lo que se teclea para tapar el unico hueco: el nombre del templo.</summary>
    /// <remarks>
    /// Se busca la clave del campo en el modelo en vez de escribirla a mano: la clave es un
    /// detalle de <see cref="ModeloDeCorreccion"/> y una prueba que la copiara se quedaria
    /// vieja sin avisar el dia que cambie.
    /// </remarks>
    public static Dictionary<string, string?> RellenarElTemplo(ModeloDeCorreccion modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        var templo = modelo.Campos.First(c => c.Etiqueta.Contains("templo", StringComparison.OrdinalIgnoreCase));
        return new Dictionary<string, string?> { [templo.Clave] = "Santo Domingo Dominican Republic" };
    }

    /// <summary>Cuantas filas de procedencia estan firmadas ahora mismo en el almacen.</summary>
    public static int CuantasFirmasHayEn(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return servicios.Almacen.Procedencias.Values.Count(p => p.Verificado);
    }
}
