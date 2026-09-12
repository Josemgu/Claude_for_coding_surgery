using Fichas.App.Asignar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// Lo que se le quita a un documento cuando se archiva: la asignación a quien lo llevara.
/// </summary>
/// <remarks>
/// <para><b>Lo pidió el dueño el 2026-09-11, literal:</b> <i>«Quiero que cuando los documentos
/// se archiven, ya no aparezcan asignados al agente. Porque llegará un punto en que, si no se
/// hace así, un agente puede tener 1 000 casos pero en la realidad solo tiene 10.»</i></para>
///
/// <para>Es el mismo gesto que <see cref="Paquetes.LimpiezaAlVolver"/> hace desde el
/// 2026-09-08 con lo que el compañero devuelve completo: la asignación <b>se desactiva con
/// su fecha y nunca se borra</b>, así que el informe del agente —que las pide vivas y
/// retiradas, <c>ReportesEnPdf.DocumentoDeCompanero</c>— sigue diciendo lo que hizo.</para>
///
/// <para>⛔ <b>Aquí no se decide qué documento se archiva ni se marca nada.</b> Archivar lo
/// escribe <see cref="AccionesDeRevisar"/> con lo que el dueño marcó; esta clase solo retira
/// lo que cuelga de un documento que ya quedó archivado. La regla permanente 5 sigue intacta:
/// quitar una asignación no marca nada ni firma nada.</para>
///
/// <para>Retira por <see cref="OperacionDeAsignar"/> y no llamando al puerto: quitarle un caso
/// a alguien al archivar tiene que dejar la base igual que quitárselo a mano desde Asignar, y
/// dos caminos distintos se separan solos.</para>
/// </remarks>
public sealed class RetiradaAlArchivar
{
    /// <summary>De dónde se leen las asignaciones vivas; nunca se escribe por aquí.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>De dónde se leen los documentos archivados, para el caso de los 1 000.</summary>
    private readonly ICasos _casos;
    /// <summary>La única puerta que retira: la misma que usa el botón de Asignar.</summary>
    private readonly OperacionDeAsignar _reparto;

    /// <summary>Se ata a los dos puertos que hacen falta para leer, y a la puerta de retirar.</summary>
    /// <param name="asignaciones">El puerto de asignaciones, solo para leer.</param>
    /// <param name="casos">El puerto de documentos, solo para leer.</param>
    /// <param name="reparto">La operación de asignar y retirar, por la que se retira.</param>
    public RetiradaAlArchivar(IAsignaciones asignaciones, ICasos casos, OperacionDeAsignar reparto)
    {
        _asignaciones = asignaciones;
        _casos = casos;
        _reparto = reparto;
    }

    /// <summary>
    /// Retira TODAS las asignaciones vivas de ese documento, y devuelve cuántas se retiraron.
    /// </summary>
    /// <remarks>
    /// <para>Todas y no solo la de uno: un archivado no es de nadie. Si lo llevaban dos (la
    /// P-11 sigue abierta), a los dos se les quita, porque el documento ya no es trabajo de
    /// ninguno.</para>
    ///
    /// <para>Se mira antes si hay algo vivo. <see cref="OperacionDeAsignar.RetirarDelCaso"/>
    /// deja un aviso de «no lo lleva nadie» cuando no encuentra nada, que está bien para el
    /// botón de la tarjeta y sería ruido aquí: archivar trescientos documentos sin asignar
    /// no puede dejar trescientas franjas.</para>
    /// </remarks>
    /// <param name="casoId">El documento que acaba de quedar archivado.</param>
    /// <returns>Cuántas asignaciones vivas tenía y se retiraron; cero si no tenía o si retirar no entró.</returns>
    public int QuitarLasDe(long casoId)
    {
        var vivas = _asignaciones.VivasDeCaso(casoId);
        if (vivas.Count == 0) return 0;

        return _reparto.RetirarDelCaso(casoId).SeEscribio ? vivas.Count : 0;
    }

    /// <summary>
    /// El caso de los 1 000: retira lo que sigue asignado en documentos que YA estaban archivados.
    /// </summary>
    /// <remarks>
    /// <para>Existe porque la regla nació el 2026-09-11 y el dueño puede tener archivados de
    /// antes con la asignación viva, que son justo los que inflan la cuenta de un agente. Se
    /// pasa al llegar a Inicio, que es la primera pantalla y la que enseña esa cuenta.</para>
    ///
    /// <para>Es idempotente y barato cuando no hay nada: dos lecturas —las asignaciones vivas
    /// y los documentos con los archivados dentro— y ninguna escritura si no se cruzan. Solo
    /// escribe en el cruce, y lo dice en una línea para que la cifra que baja no parezca
    /// perdida. La fecha de archivado no se toca: la retirada lleva la de hoy, que es cuando
    /// se retiró de verdad.</para>
    /// </remarks>
    public RetiradaDeLoDeAntes QuitarLasDeLoQueYaEstabaArchivado()
    {
        var vivas = _asignaciones
            .Listar(FiltroDeAsignaciones.Activas, Pagina.Primera(int.MaxValue))
            .Elementos;
        if (vivas.Count == 0) return RetiradaDeLoDeAntes.Nada;

        var conAlgunaViva = vivas.Select(asignacion => asignacion.CasoId).ToHashSet();
        var archivadosAsignados = _casos
            .Listar(new FiltroDeCasos(IncluirArchivados: true), Pagina.Primera(int.MaxValue))
            .Elementos
            .Where(caso => caso.Archivado && conAlgunaViva.Contains(caso.Id))
            .Select(caso => caso.Id)
            .ToList();

        var documentos = 0;
        var asignaciones = 0;
        foreach (var casoId in archivadosAsignados)
        {
            var retiradas = QuitarLasDe(casoId);
            if (retiradas == 0) continue;
            documentos++;
            asignaciones += retiradas;
        }

        return new RetiradaDeLoDeAntes(documentos, asignaciones);
    }
}

/// <summary>Lo que dejó la limpieza de lo archivado de antes, para decirlo en una línea.</summary>
/// <param name="Documentos">Cuántos archivados seguían asignados y ya no lo están.</param>
/// <param name="Asignaciones">Cuántas asignaciones se desactivaron en total.</param>
public sealed record RetiradaDeLoDeAntes(int Documentos, int Asignaciones)
{
    /// <summary>La que se devuelve cuando no había nada que hacer.</summary>
    public static RetiradaDeLoDeAntes Nada { get; } = new(0, 0);

    /// <summary>Si se escribió algo; con nada, la pantalla no dice nada.</summary>
    public bool HuboAlgo => Documentos > 0;

    /// <summary>La línea del acuse; vacía si no hubo nada.</summary>
    public string Linea
    {
        get
        {
            if (!HuboAlgo) return string.Empty;

            // Las formas se calculan ANTES y no dentro de la frase, como en las otras líneas
            // del programa: un condicional metido en medio ya no se lee como español.
            var seguiaAsignado = Plural.Palabra(Documentos, "seguía asignado", "seguían asignados");
            var archivado = Plural.Palabra(Documentos, "archivado", "archivados");
            return $"{Plural.Con(Documentos, "documento", "documentos")} {archivado} de antes {seguiaAsignado}; "
                 + "ya no. Lo que hizo cada agente con ellos se conserva en su informe.";
        }
    }
}
