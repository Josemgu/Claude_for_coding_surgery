namespace Fichas.Contratos.Modelos;

/// <summary>
/// Una persona de un formulario. Tabla <c>personas</c>, 25 columnas (ARQUITECTURA §2.3).
/// </summary>
/// <remarks>
/// Las seis <c>Ord*</c> son a que va la persona al templo, leidas del papel.
/// Los seis <c>Paso*</c> son los pasos del sistema del lider, que devuelve el companero.
/// NO son lo mismo y no se mezclan. Los tres estados de cada una (si, no, nadie lo miro)
/// se guardan como <c>bool?</c>: el nulo es un dato, no un hueco.
/// </remarks>
public sealed record Persona
{
    /// <summary>Clave primaria; 0 mientras la persona no se ha guardado.</summary>
    public long Id { get; init; }

    /// <summary>Caso al que pertenece; obligatorio.</summary>
    public long CasoId { get; init; }

    /// <summary>Numero de registro, patron 3-4-4 con guiones; TEXT para no perder el cero de delante.</summary>
    public string? Mrn { get; init; }

    /// <summary>Nombre tal como se leyo o se corrigio.</summary>
    public string? Nombre { get; init; }

    /// <summary>En que fila del formulario venia, base 1.</summary>
    public int? FilaFormulario { get; init; }

    /// <summary>Casilla 1: recibir ordenanzas propias.</summary>
    public bool? OrdRecibirPropias { get; init; }

    /// <summary>Casilla 2: observar ordenanza de sellamiento.</summary>
    public bool? OrdObservarSellamiento { get; init; }

    /// <summary>Casilla 3: traductor.</summary>
    public bool? OrdTraductor { get; init; }

    /// <summary>Casilla 4: investidura.</summary>
    public bool? OrdInvestidura { get; init; }

    /// <summary>Casilla 5: sellamiento esposa a esposo.</summary>
    public bool? OrdSellamientoEsposos { get; init; }

    /// <summary>Casilla 6: sellamiento hijo a padres.</summary>
    public bool? OrdSellamientoHijoPadres { get; init; }

    /// <summary>De que hoja del PDF salio esta persona, base 1; no es la del caso.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>Lo que el companero propone en su Excel; texto libre, sin lista cerrada.</summary>
    public string? EstadoPropuesto { get; init; }

    /// <summary>La nota libre que escribio el companero.</summary>
    public string? NotaCompanero { get; init; }

    /// <summary>Que companero lo propuso; id de <c>companeros</c>.</summary>
    public long? PropuestoPor { get; init; }

    /// <summary>Cuando lo propuso, ISO-8601.</summary>
    public string? PropuestoEn { get; init; }

    /// <summary>Por que no viajo; texto libre, no lista cerrada.</summary>
    public string? MotivoNoViajo { get; init; }

    /// <summary>Si viajo (true), no viajo (false) o nadie lo ha dicho (nulo).</summary>
    public bool? PudoViajar { get; init; }

    /// <summary>Paso 1: preparacion para las ordenanzas.</summary>
    public bool? PasoPreparacion { get; init; }

    /// <summary>Paso 2: informacion.</summary>
    public bool? PasoInformacion { get; init; }

    /// <summary>Paso 3: cita del templo.</summary>
    public bool? PasoCitaDelTemplo { get; init; }

    /// <summary>Paso 4: acciones requeridas.</summary>
    public bool? PasoAccionesRequeridas { get; init; }

    /// <summary>Paso 5: entrevistas.</summary>
    public bool? PasoEntrevistas { get; init; }

    /// <summary>Paso 6: listo para el templo.</summary>
    public bool? PasoListoParaElTemplo { get; init; }

    /// <summary>Si el companero llamo al lider; NO es un septimo paso y no cuenta como tal.</summary>
    public bool? LlamoAlLider { get; init; }
}
