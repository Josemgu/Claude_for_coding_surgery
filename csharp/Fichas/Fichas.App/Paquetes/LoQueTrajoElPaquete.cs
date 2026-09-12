using Fichas.Contratos.Modelos;

namespace Fichas.App.Paquetes;

/// <summary>
/// Una persona que volvio en el Excel del agente y que caso con una fila de la base.
/// </summary>
/// <remarks>
/// <para>Lleva los SIETE campos que la pantalla de Correccion dibuja —los cinco del
/// documento y los dos de la persona— y no otros. Es a proposito: son exactamente los que
/// Miguel firmaria uno a uno, y por eso son los que el boton de darlo por bueno en bloque
/// firma de golpe. Ensenar unos y firmar otros seria pedirle que de por buena una cosa
/// mirando otra.</para>
///
/// <para>⛔ <b><see cref="EstadoDelDocumento"/> NO se firma ni se toca.</b> Lo escribio el
/// Excel del companero con SU nombre (DECISIONES.md, 2026-09-03: «el documento que ellos
/// llenan es el que marca, y dice completado por Sandy»). Viaja aqui solo para pintarse.</para>
/// </remarks>
public sealed record InformacionQueVolvio
{
    /// <summary>De que fila del Excel salio, base 1; nulo si la hoja no lo dijo.</summary>
    public int? FilaExcel { get; init; }

    /// <summary>El documento al que pertenece.</summary>
    public long CasoId { get; init; }

    /// <summary>La persona de la base con la que caso la fila.</summary>
    public long PersonaId { get; init; }

    /// <summary>Numero de caso guardado en la base, no el que venia escrito en la hoja.</summary>
    public string? NumeroCaso { get; init; }

    /// <summary>Numero de unidad guardado en la base.</summary>
    public string? UnidadNumero { get; init; }

    /// <summary>Nombre de la unidad guardado en la base.</summary>
    public string? UnidadNombre { get; init; }

    /// <summary>Fecha de viaje guardada en la base, ISO-8601.</summary>
    public string? FechaViaje { get; init; }

    /// <summary>Templo guardado en la base.</summary>
    public string? TemploNombre { get; init; }

    /// <summary>Nombre de la persona guardado en la base.</summary>
    public string? Nombre { get; init; }

    /// <summary>Cedula de miembro guardada en la base.</summary>
    public string? Mrn { get; init; }

    /// <summary>Lo que el Excel del companero dejo escrito sobre el documento.</summary>
    public EstadoDeRecomendacion EstadoDelDocumento { get; init; }

    /// <summary>El renglon que se pinta: los siete campos y el estado, en uno.</summary>
    public string Renglon
        => $"{ODice(NumeroCaso)} · {ODice(Nombre)} · {ODice(Mrn)} · "
         + $"unidad {ODice(UnidadNumero)} {ODice(UnidadNombre)} · viaje {ODice(FechaViaje)} · "
         + $"{ODice(TemploNombre)} · {PalabraDelEstado}";

    /// <summary>La palabra del estado que puso el companero; nunca un color solo.</summary>
    public string PalabraDelEstado => EstadoDelDocumento switch
    {
        EstadoDeRecomendacion.Completa => "completa",
        EstadoDeRecomendacion.NoCompleta => "no completa",
        _ => "sin marcar",
    };

    /// <summary>Lo que se pinta donde la base no tiene el dato. Nunca un hueco en blanco.</summary>
    /// <param name="valor">El valor guardado; nulo o en blanco se pinta como «no consta».</param>
    private static string ODice(string? valor) => string.IsNullOrWhiteSpace(valor) ? "no consta" : valor;
}

/// <summary>
/// Lo que trajo el Excel que devolvio un agente: lo que se puede mirar y lo que no.
/// </summary>
/// <remarks>
/// <para>Existe por lo que el dueno movio el 2026-09-06: <i>«lo que debo revisar son los
/// paquetes de los agentes. Solo verificar las informaciones. Y ya.»</i> Deja de firmar
/// documento a documento lo que el escaneo leyo entero, y firma aqui, que es donde puede
/// entrar el error de otra persona.</para>
///
/// <para>⚠️ <b>Las tres listas van separadas y no se suman nunca.</b> Solo
/// <see cref="Informaciones"/> se puede dar por bueno en bloque. Lo que no caso
/// (<see cref="NoEntraron"/>) y lo que no se pudo mirar
/// (<see cref="NoSePudieronMirar"/>) se ensena aparte y se queda fuera del boton: firmar en
/// bloque algo que no vino es justo el error que el boton existe para evitar.</para>
/// </remarks>
/// <param name="DeQuien">El nombre del companero que devolvio la hoja.</param>
/// <param name="Informaciones">Lo que caso, una por persona.</param>
/// <param name="NoEntraron">Las filas descartadas, con su motivo escrito.</param>
/// <param name="NoSePudieronMirar">Las filas que entraron pero que no se pueden dar por buenas, y por que.</param>
public sealed record LoQueTrajoElPaquete(
    string DeQuien,
    IReadOnlyList<InformacionQueVolvio> Informaciones,
    IReadOnlyList<FilaDescartada> NoEntraron,
    IReadOnlyList<string> NoSePudieronMirar)
{
    /// <summary>Una vuelta de la que no hay nada que revisar, con el nombre de quien la trajo.</summary>
    /// <param name="deQuien">El nombre del compañero.</param>
    public static LoQueTrajoElPaquete Nada(string deQuien) => new(deQuien, [], [], []);

    /// <summary>Cuantos documentos distintos volvieron.</summary>
    public int Documentos => Informaciones.Select(una => una.CasoId).Distinct().Count();

    /// <summary>Cuantos de esos documentos marco completos el Excel del companero.</summary>
    public int DocumentosCompletos => Informaciones
        .Where(una => una.EstadoDelDocumento == EstadoDeRecomendacion.Completa)
        .Select(una => una.CasoId)
        .Distinct()
        .Count();

    /// <summary>Cuantos volvieron sin que el companero pudiera darlos por completos.</summary>
    public int DocumentosSinCompletar => Documentos - DocumentosCompletos;

    /// <summary>Si hay algo que el dueno pueda mirar y dar por bueno.</summary>
    public bool HayQueRevisar => Informaciones.Count > 0;

    /// <summary>La linea de la cabecera: cuantas informaciones, de cuantos documentos.</summary>
    public string Linea
        => $"{DeQuien} trajo {Informaciones.Count} "
         + $"{(Informaciones.Count == 1 ? "información" : "informaciones")} de {Documentos} "
         + $"{(Documentos == 1 ? "documento" : "documentos")} · {DocumentosCompletos} "
         + $"{(DocumentosCompletos == 1 ? "completo" : "completos")} · {DocumentosSinCompletar} sin completar.";

    /// <summary>
    /// Lo que el sistema dice solo cuando el agente no pudo con todos, o nulo si pudo.
    /// </summary>
    /// <remarks>
    /// Son las palabras del dueno del 2026-09-05: <i>«Sandy no pudo verificar todos, por
    /// favor verifica qué falta»</i>. Va con el nombre del companero porque el lo dijo con
    /// el nombre: quien tiene que volver a mirar necesita saber de quien es el paquete.
    /// </remarks>
    public string? LoQueFalta
        => Documentos == 0 || DocumentosSinCompletar == 0
            ? null
            : $"{DeQuien} no pudo verificar todos: {DocumentosSinCompletar} "
            + $"{(DocumentosSinCompletar == 1 ? "documento" : "documentos")} sin completar. "
            + "Por favor verifique qué falta.";

    /// <summary>La linea de lo que queda fuera del boton, o nulo si no queda nada fuera.</summary>
    public string? LoQueQuedaFuera
    {
        get
        {
            if (NoEntraron.Count == 0 && NoSePudieronMirar.Count == 0) return null;

            var trozos = new List<string>();
            if (NoEntraron.Count > 0)
                trozos.Add($"{NoEntraron.Count} no {(NoEntraron.Count == 1 ? "casó" : "casaron")}");
            if (NoSePudieronMirar.Count > 0)
                trozos.Add($"{NoSePudieronMirar.Count} sin poder dar por {(NoSePudieronMirar.Count == 1 ? "buena" : "buenas")}");

            return "Fuera del botón: " + string.Join(" · ", trozos)
                 + ". Nada de esto se firma, y todo queda guardado con su motivo.";
        }
    }
}
