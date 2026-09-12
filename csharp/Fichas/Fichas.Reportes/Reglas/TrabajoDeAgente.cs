using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Reglas;

/// <summary>
/// Lo que hizo un agente en una ventana de tiempo. Solo cuentas; ni una palabra en espanol.
/// </summary>
/// <param name="SeLeAsignaron">Documentos que se le pasaron dentro de la ventana.</param>
/// <param name="Contesto">Documentos cuya hoja devolvio el, fechada dentro de la ventana.</param>
/// <param name="Completas">De esos, cuantos dijo que estaban completos.</param>
/// <param name="NoCompletas">De esos, cuantos dijo que NO.</param>
/// <param name="NoSePudoComunicar">De los no completos, con ese motivo.</param>
/// <param name="ElLiderNoLoHizo">De los no completos, con ese motivo.</param>
/// <param name="OtraRazon">De los no completos, con ese motivo.</param>
/// <param name="SinMotivoEscrito">De los no completos, los que no dicen por que.</param>
public sealed record TrabajoDeUnAgente(
    int SeLeAsignaron,
    int Contesto,
    int Completas,
    int NoCompletas,
    int NoSePudoComunicar,
    int ElLiderNoLoHizo,
    int OtraRazon,
    int SinMotivoEscrito);

/// <summary>
/// Que hizo un agente en un trozo de calendario, contado sobre lo que dejo escrito en la base.
/// </summary>
/// <remarks>
/// <para><b>Lo pidio el dueno el 2026-09-05</b> (<c>DECISIONES.md</c>): <i>«Es importante tener
/// un informe por agente también: lo que hicieron los agentes en ese mes y lo que hicieron en
/// esa semana, qué hicieron»</i>.</para>
///
/// <para><b>«Qué hizo» se mide por lo que CONTESTO, no por lo que se le dio.</b> La columna que
/// lo dice es <c>casos.estado_del_companero_por</c> con su fecha
/// <c>casos.estado_del_companero_en</c>: la escribio la vuelta de SU hoja de Excel y no se
/// borra cuando Miguel corrige encima (migracion 14). Contar por asignaciones diria lo que se
/// le mando, que es trabajo de quien reparte y no suyo.</para>
///
/// <para><b>Las marcas llevan la hora pegada.</b> Por eso el limite de arriba es
/// <see cref="Periodo.SiguienteAHasta"/> y no <c>Hasta</c>: con <c>&lt;= Hasta</c> se caeria
/// todo lo que ese agente contesto el ultimo dia del periodo despues de medianoche, que es casi
/// todo lo de ese dia.</para>
///
/// <para><b>Aqui no se escribe nada en espanol.</b> Esto cuenta; las palabras las pone
/// <c>ArmadoDelInformeDeAgente</c>.</para>
/// </remarks>
public static class TrabajoDeAgente
{
    /// <summary>Lo que hizo ese agente dentro de esa ventana.</summary>
    /// <param name="periodo">La ventana; los dos extremos entran.</param>
    /// <param name="companeroId">De quien se cuenta.</param>
    /// <param name="susCasos">Los casos que le tocan, sin filtrar por fecha.</param>
    /// <param name="susAsignaciones">Sus asignaciones, vivas y retiradas, sin filtrar por fecha.</param>
    public static TrabajoDeUnAgente En(
        Periodo periodo,
        long companeroId,
        IReadOnlyList<Caso> susCasos,
        IReadOnlyList<Asignacion> susAsignaciones)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(susCasos);
        ArgumentNullException.ThrowIfNull(susAsignaciones);

        var seLeAsignaron = susAsignaciones
            .Where(asignacion => Cae(asignacion.AsignadoEn, periodo))
            .Select(asignacion => asignacion.CasoId)
            .Distinct()
            .Count();

        var contestados = susCasos
            .Where(caso => caso.EstadoDelCompaneroPor == companeroId && Cae(caso.EstadoDelCompaneroEn, periodo))
            .ToList();

        var noCompletos = contestados
            .Where(caso => Caso.LeerEstado(caso.EstadoDelCompanero) == EstadoDeRecomendacion.NoCompleta)
            .ToList();

        return new TrabajoDeUnAgente(
            seLeAsignaron,
            contestados.Count,
            contestados.Count(caso => Caso.LeerEstado(caso.EstadoDelCompanero) == EstadoDeRecomendacion.Completa),
            noCompletos.Count,
            ConMotivo(noCompletos, MotivoDeNoCompletar.NoSePudoComunicar),
            ConMotivo(noCompletos, MotivoDeNoCompletar.ElLiderNoLoHizo),
            ConMotivo(noCompletos, MotivoDeNoCompletar.OtraRazon),
            ConMotivo(noCompletos, MotivoDeNoCompletar.SinMotivo));
    }

    /// <summary>Los ultimos N dias del periodo, contando el ultimo como uno de los N.</summary>
    /// <remarks>
    /// Es lo que el informe llama «la semana», y hace falta declararlo porque NADIE lo definio:
    /// el dueno dijo «lo que hicieron en esa semana» y no dijo cual. Se toma la cola del periodo
    /// que se pide para que un solo PDF conteste las dos preguntas que el hizo en la misma
    /// frase, el mes y la semana, sin generar dos.
    ///
    /// Un periodo mas corto que N dias devuelve el periodo entero: no se puede mirar mas atras
    /// de lo que se pidio sin contar dias que el informe dice que no mira.
    /// </remarks>
    /// <param name="periodo">El periodo entero que se pidió.</param>
    /// <param name="dias">Cuántos días de cola; menos de uno se trata como uno.</param>
    /// <returns>El periodo que termina donde el pedido y empieza N-1 días antes, o en <c>Desde</c> si eso cae antes.</returns>
    public static Periodo LaColaDe(Periodo periodo, int dias)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        var ultimo = DateOnly.ParseExact(periodo.Hasta, "yyyy-MM-dd");
        var candidato = ultimo.AddDays(-(Math.Max(1, dias) - 1)).ToString("yyyy-MM-dd");
        var desde = string.CompareOrdinal(candidato, periodo.Desde) < 0 ? periodo.Desde : candidato;

        return Periodo.Leer(desde, periodo.Hasta).Periodo!;
    }

    /// <summary>Cuántos de esos casos llevan ese motivo escrito por el compañero.</summary>
    /// <param name="casos">Los casos que el agente marcó como no completos.</param>
    /// <param name="motivo">El motivo que se cuenta.</param>
    private static int ConMotivo(IReadOnlyList<Caso> casos, MotivoDeNoCompletar motivo)
        => casos.Count(caso => caso.MotivoQueDijoElCompanero == motivo);

    /// <summary>Si esa marca con hora cae dentro del periodo; nula nunca cae.</summary>
    /// <param name="marca">Una marca «AAAA-MM-DD HH:mm:ss», o nula o vacía.</param>
    /// <param name="periodo">La ventana; el límite de arriba es <see cref="Periodo.SiguienteAHasta"/>.</param>
    private static bool Cae(string? marca, Periodo periodo)
        => !string.IsNullOrEmpty(marca)
           && string.CompareOrdinal(marca, periodo.Desde) >= 0
           && string.CompareOrdinal(marca, periodo.SiguienteAHasta) < 0;
}
