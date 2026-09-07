namespace Fichas.App.Inicio;

/// <summary>
/// Arma el calendario del mes con los GRUPOS que viajan cada dia.
/// </summary>
/// <remarks>
/// <para>Tres decisiones que son de rendimiento y no de gusto, y por eso se dicen aqui:</para>
/// <list type="number">
/// <item>El mes SIEMPRE trae 42 celdas —seis semanas de lunes a domingo—. Asi el alto del
/// calendario no salta al cambiar de mes, y el numero de elementos visuales del calendario
/// no depende de cuantos casos haya (criterio C1-5).</item>
/// <item>Cada dia ensena como mucho <see cref="PastillasPorDia"/> grupos y dice cuantos
/// quedan. Sin ese tope, un dia con 40 grupos mete 40 elementos en una celda y la cuenta de
/// elementos vivos vuelve a crecer con los datos, que es justo el defecto que originó el
/// cambio de lenguaje.</item>
/// <item>Una pastilla es un GRUPO —una unidad de ese dia— y ya no un documento suelto
/// (criterio C12-2). El dueno dijo cual es su unidad de trabajo el 2026-09-05: <i>«así puedo
/// ver los grupos por fechas y saber con qué grupo trabajar»</i>.</item>
/// </list>
///
/// <para>Quien agrupa los documentos en grupos es
/// <see cref="Fichas.App.Grupo.LectorDeGrupos.PorDia"/>: aqui solo se reparten por celdas.
/// ⛔ Los archivados NO entran (2026-09-06): hasta ese dia si, marcados con su palabra
/// (decision del dueno del 2026-09-03, criterio C7-2), y el mismo la deshizo porque verlos
/// marcados le confundia. Se ven en Revisar, con «Ver los archivados».</para>
/// </remarks>
public static class CalendarioDelMes
{
    /// <summary>Cuantas pastillas caben en una celda; el resto se resume en «+N más».</summary>
    public const int PastillasPorDia = 3;

    /// <summary>Cuantas celdas trae siempre el mes: seis semanas de siete dias.</summary>
    public const int CeldasDelMes = 42;

    /// <summary>
    /// Arma el mes que contiene <paramref name="unDiaDelMes"/> con los grupos que se le pasen.
    /// </summary>
    /// <param name="unDiaDelMes">Cualquier dia del mes que se quiere ensenar.</param>
    /// <param name="hoy">El dia de hoy segun el reloj del programa, para marcarlo.</param>
    /// <param name="finDeLaVentana">Ultimo dia de la ventana de 7 dias, para sombrearla.</param>
    /// <param name="gruposPorDia">Los grupos de cada fecha, ya agrupados y ordenados.</param>
    public static MesDelCalendario Armar(
        DateOnly unDiaDelMes,
        DateOnly hoy,
        DateOnly finDeLaVentana,
        IReadOnlyDictionary<DateOnly, List<PastillaDeDia>> gruposPorDia)
    {
        ArgumentNullException.ThrowIfNull(gruposPorDia);

        var primeroDelMes = new DateOnly(unDiaDelMes.Year, unDiaDelMes.Month, 1);
        var primeraCelda = primeroDelMes.AddDays(-DiasDesdeElLunes(primeroDelMes));

        var dias = new List<DiaDelCalendario>(CeldasDelMes);
        for (var i = 0; i < CeldasDelMes; i++)
        {
            var fecha = primeraCelda.AddDays(i);
            dias.Add(ArmarElDia(fecha, unDiaDelMes.Month, hoy, finDeLaVentana, gruposPorDia));
        }

        return new MesDelCalendario(FechasEnEspanol.DecirElMes(primeroDelMes), dias);
    }

    /// <summary>Arma una celda: su fecha, su numero, sus marcas y las pastillas que caben.</summary>
    private static DiaDelCalendario ArmarElDia(
        DateOnly fecha,
        int mesQueSeEnsena,
        DateOnly hoy,
        DateOnly finDeLaVentana,
        IReadOnlyDictionary<DateOnly, List<PastillaDeDia>> gruposPorDia)
    {
        var delDia = gruposPorDia.TryGetValue(fecha, out var lista) ? lista : [];
        var visibles = delDia.Count <= PastillasPorDia
            ? delDia
            : delDia.GetRange(0, PastillasPorDia);

        return new DiaDelCalendario(
            fecha,
            fecha.Day,
            fecha.Month == mesQueSeEnsena,
            fecha == hoy,
            fecha >= hoy && fecha <= finDeLaVentana,
            visibles,
            Math.Max(0, delDia.Count - PastillasPorDia));
    }

    /// <summary>Cuantos dias hay que retroceder para caer en lunes; domingo son seis.</summary>
    private static int DiasDesdeElLunes(DateOnly fecha)
        => fecha.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)fecha.DayOfWeek - 1;
}
