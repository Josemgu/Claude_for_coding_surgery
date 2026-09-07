using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Paquetes;

/// <summary>Un campo concreto de una fila concreta, con el valor que hay guardado.</summary>
/// <param name="Tabla">A que tabla apunta: casos o personas.</param>
/// <param name="RegistroId">La fila de esa tabla.</param>
/// <param name="Campo">El nombre de la COLUMNA, tal como se escribe en <c>procedencia_campo.campo</c>.</param>
/// <param name="Valor">Lo que hay guardado hoy; nulo o vacio si no hay nada.</param>
public readonly record struct CampoDelPaquete(
    TablaDeProcedencia Tabla, long RegistroId, string Campo, string? Valor);

/// <summary>
/// Que campos abarca dar por bueno en bloque lo que trajo un paquete.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Son los MISMOS siete que dibuja la pantalla de Correccion</b>, y se leen de
/// alli —<see cref="ModeloDeCorreccion.CamposDelCasoQueSeDibujan"/> y
/// <see cref="ModeloDeCorreccion.CamposDeLaPersonaQueSeDibujan"/>—, no se copian. Firmar en
/// bloque es el mismo acto que firmar uno a uno, solo que sin recorrerlos: si las dos listas
/// se separaran, el boton estaria firmando campos que Miguel no ve en ninguna pantalla, o
/// dejando sin firmar los que si ve. Hay una prueba que compara las dos listas.</para>
///
/// <para><b>Los cinco del documento se cuentan UNA vez por documento.</b> Un caso con cuatro
/// hermanos vuelve en cuatro filas del Excel y su numero de caso sigue siendo uno: contarlo
/// cuatro veces daria un numero que no corresponde con nada, y el dueno decide con ese
/// numero.</para>
/// </remarks>
public static class CamposDelPaquete
{
    /// <summary>Los campos del documento y de cada persona que abarca el boton, sin repetir.</summary>
    /// <remarks>
    /// El orden es estable —documento y luego sus personas, en el orden en que volvieron—
    /// para que la cuenta que se ensena antes de firmar y lo que se firma despues recorran
    /// lo mismo.
    /// </remarks>
    public static IReadOnlyList<CampoDelPaquete> De(IReadOnlyList<InformacionQueVolvio> informaciones)
    {
        ArgumentNullException.ThrowIfNull(informaciones);

        var campos = new List<CampoDelPaquete>();
        var casosYaPuestos = new HashSet<long>();

        foreach (var una in informaciones)
        {
            if (casosYaPuestos.Add(una.CasoId)) campos.AddRange(DelDocumento(una));
            campos.AddRange(DeLaPersona(una));
        }

        return campos;
    }

    /// <summary>Los cinco campos del documento, con el valor que hay en la base.</summary>
    private static IEnumerable<CampoDelPaquete> DelDocumento(InformacionQueVolvio una)
        => ModeloDeCorreccion.CamposDelCasoQueSeDibujan.Select(campo =>
            new CampoDelPaquete(TablaDeProcedencia.Casos, una.CasoId, campo, ValorDelDocumento(una, campo)));

    /// <summary>Los dos campos de la persona, con el valor que hay en la base.</summary>
    private static IEnumerable<CampoDelPaquete> DeLaPersona(InformacionQueVolvio una)
        => ModeloDeCorreccion.CamposDeLaPersonaQueSeDibujan.Select(campo =>
            new CampoDelPaquete(TablaDeProcedencia.Personas, una.PersonaId, campo, ValorDeLaPersona(una, campo)));

    /// <summary>El valor guardado de un campo del documento; levanta si la lista crece sin avisar.</summary>
    /// <remarks>
    /// Levanta a proposito y no devuelve nulo: un campo nuevo en la lista de Correccion que
    /// aqui saliera vacio se quedaria sin firmar en silencio, y nadie lo notaria hasta que
    /// alguien se preguntara por que ese campo nunca esta dado por bueno.
    /// </remarks>
    private static string? ValorDelDocumento(InformacionQueVolvio una, string campo) => campo switch
    {
        Fichas.Lectura.Extraccion.CampoNumeroDeCaso => una.NumeroCaso,
        Fichas.Lectura.Extraccion.CampoUnidadNumero => una.UnidadNumero,
        Fichas.Lectura.Extraccion.CampoUnidadNombre => una.UnidadNombre,
        Fichas.Lectura.Extraccion.CampoFechaDeViaje => una.FechaViaje,
        Fichas.Lectura.Extraccion.CampoTemploNombre => una.TemploNombre,
        _ => throw new KeyNotFoundException(
            $"La pantalla de Corrección dibuja el campo «{campo}» del documento y aquí no hay de "
            + "dónde sacar su valor. Añádalo a InformacionQueVolvio antes de que el botón lo deje "
            + "sin firmar sin decirlo."),
    };

    /// <summary>El valor guardado de un campo de la persona; levanta por lo mismo.</summary>
    private static string? ValorDeLaPersona(InformacionQueVolvio una, string campo) => campo switch
    {
        Fichas.Lectura.Extraccion.CampoCedula => una.Mrn,
        Fichas.Lectura.Extraccion.CampoNombreDePersona => una.Nombre,
        _ => throw new KeyNotFoundException(
            $"La pantalla de Corrección dibuja el campo «{campo}» de la persona y aquí no hay de "
            + "dónde sacar su valor. Añádalo a InformacionQueVolvio antes de que el botón lo deje "
            + "sin firmar sin decirlo."),
    };
}
