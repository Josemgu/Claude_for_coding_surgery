using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Importar;

/// <summary>
/// Lee la lista plana de campos propuestos como lo que es: un caso y sus personas.
/// </summary>
/// <remarks>
/// <c>Fichas.Lectura</c> devuelve los campos del caso y los de todas las personas en una
/// sola lista, cada uno con su tabla y su fila. Aqui se vuelven a montar sin perder nada:
/// la banda, la confianza y el valor crudo del OCR viajan pegados a cada campo porque son
/// lo que llena <c>procedencia_campo</c>, y sin ellos un dato apareceria en la base sin
/// saber de donde vino.
/// </remarks>
public sealed class CamposDeLaHoja
{
    private readonly Dictionary<string, CampoPropuesto> _delCaso;
    private readonly List<PersonaDeLaHoja> _personas;

    /// <summary>Reparte los campos de una hoja entre el caso y sus personas.</summary>
    public CamposDeLaHoja(IReadOnlyList<CampoPropuesto> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        _delCaso = campos
            .Where(campo => campo.Tabla == TablaDeProcedencia.Casos)
            .GroupBy(campo => campo.Campo, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First(), StringComparer.Ordinal);

        _personas = campos
            .Where(campo => campo.Tabla == TablaDeProcedencia.Personas)
            .GroupBy(campo => campo.FilaFormulario ?? 0)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new PersonaDeLaHoja(
                grupo.Key == 0 ? null : grupo.Key,
                grupo.FirstOrDefault(campo => campo.Campo == CampoNombre),
                grupo.FirstOrDefault(campo => campo.Campo == CampoMrn)))
            .Where(persona => persona.TieneAlgo)
            .ToList();
    }

    /// <summary>Nombre de la columna del numero de caso.</summary>
    public const string CampoNumeroCaso = "numero_caso";

    /// <summary>Nombre de la columna de la fecha de viaje.</summary>
    public const string CampoFechaViaje = "fecha_viaje";

    /// <summary>Nombre de la columna del numero de unidad.</summary>
    public const string CampoUnidadNumero = "unidad_numero";

    /// <summary>Nombre de la columna del nombre de la unidad.</summary>
    public const string CampoUnidadNombre = "unidad_nombre";

    /// <summary>Nombre de la columna del templo.</summary>
    public const string CampoTemploNombre = "templo_nombre";

    /// <summary>Nombre de la columna del nombre de la persona.</summary>
    public const string CampoNombre = "nombre";

    /// <summary>Nombre de la columna de la cedula de miembro.</summary>
    public const string CampoMrn = "mrn";

    /// <summary>
    /// Los cinco campos del caso que llevan procedencia, en el orden de la pantalla.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>El templo entro el 2026-09-05, y hasta ese dia faltaba.</b> El motivo escrito
    /// aqui antes era que la pantalla de correccion «hoy no lo dibuja», y eso dejo de ser
    /// cierto: <see cref="Fichas.App.Correccion.ModeloDeCorreccion.CamposDelCasoQueSeDibujan"/>
    /// lo dibuja, y son cinco. El razonamiento se quedo escrito mientras el hecho cambiaba.
    /// <para>
    /// Lo que costaba, medido con la ventana abierta sobre los siete escaneos reales del
    /// dueno importados en una carpeta de datos propia: <c>templo_nombre</c> con fila de
    /// procedencia en <b>0 de 7</b> casos, y los otros cuatro en 7 de 7. Sin fila,
    /// <c>IProcedencia.Firmar</c> —que es un <c>UPDATE</c>— cambia cero filas y contesta
    /// «No se encontro la fila que se queria cambiar»: el unico campo que quedaba por
    /// comprobar era justo el unico que no se podia dar por bueno.
    /// </para>
    /// <para>
    /// ⛔ Y la fila sigue naciendo SIN firma, como las otras cuatro: quien la escribe es
    /// <c>Anotar</c>, y ahi <c>verificado</c> no se toca nunca (regla permanente 5).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> CamposDelCasoConProcedencia { get; } =
        [CampoNumeroCaso, CampoFechaViaje, CampoUnidadNumero, CampoUnidadNombre, CampoTemploNombre];

    /// <summary>Los dos campos de una persona que se leen del papel.</summary>
    public static IReadOnlyList<string> CamposDeLaPersonaConProcedencia { get; } =
        [CampoNombre, CampoMrn];

    /// <summary>Las personas de la hoja, en el orden en que venian en el papel.</summary>
    public IReadOnlyList<PersonaDeLaHoja> Personas => _personas;

    /// <summary>El campo del caso que se pida, o nulo si esa hoja no lo trae.</summary>
    public CampoPropuesto? Del(string campo) => _delCaso.GetValueOrDefault(campo);

    /// <summary>El valor de un campo del caso, o nulo.</summary>
    /// <remarks>
    /// Un valor tachado NO se resucita: el papel dice que no vale, y darlo por bueno seria
    /// leer lo contrario de lo que esta escrito. El valor crudo sigue en la procedencia.
    /// </remarks>
    public string? ValorDe(string campo)
    {
        var propuesto = Del(campo);
        return propuesto is null || propuesto.AnuladoPorTachon ? null : propuesto.Valor;
    }
}

/// <summary>Una persona tal como venia en la hoja, con sus dos campos y su fila.</summary>
/// <param name="FilaFormulario">En que renglon del papel venia, base 1; nulo si no se supo.</param>
/// <param name="Nombre">El campo del nombre, con su banda y su confianza.</param>
/// <param name="Mrn">El campo de la cedula, con su banda y su confianza.</param>
public sealed record PersonaDeLaHoja(
    int? FilaFormulario,
    CampoPropuesto? Nombre,
    CampoPropuesto? Mrn)
{
    /// <summary>
    /// Si esta fila trae algo. Una fila en blanco no es un fallo: el formulario trae seis
    /// renglones y casi nunca vienen los seis llenos.
    /// </summary>
    public bool TieneAlgo => ValorDelNombre is not null || ValorDelMrn is not null;

    /// <summary>El nombre, o nulo; un tachon no se resucita.</summary>
    public string? ValorDelNombre => Nombre is null || Nombre.AnuladoPorTachon ? null : Nombre.Valor;

    /// <summary>La cedula, o nula; un tachon no se resucita.</summary>
    public string? ValorDelMrn => Mrn is null || Mrn.AnuladoPorTachon ? null : Mrn.Valor;

    /// <summary>El campo que se pida de esta persona.</summary>
    public CampoPropuesto? Del(string campo) => campo switch
    {
        CamposDeLaHoja.CampoNombre => Nombre,
        CamposDeLaHoja.CampoMrn => Mrn,
        _ => null,
    };
}
