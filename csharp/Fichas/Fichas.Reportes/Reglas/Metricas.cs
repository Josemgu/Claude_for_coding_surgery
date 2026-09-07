using System.Globalization;
using Fichas.Reportes.Consultas;

namespace Fichas.Reportes.Reglas;

/// <summary>Cuanto se tarda desde que un caso entra hasta que queda verificado.</summary>
/// <param name="CasosMedidos">Sobre cuantos casos se calculo.</param>
/// <param name="PromedioEnHoras">El promedio; nulo si no se pudo medir ninguno.</param>
/// <param name="MinimoEnHoras">El mas rapido; nulo si no se pudo medir ninguno.</param>
/// <param name="MaximoEnHoras">El mas lento; nulo si no se pudo medir ninguno.</param>
/// <param name="DescartadosPorFechasIncoherentes">Cuantos se dejaron fuera y por que se dice.</param>
public sealed record Demora(
    int CasosMedidos,
    double? PromedioEnHoras,
    double? MinimoEnHoras,
    double? MaximoEnHoras,
    int DescartadosPorFechasIncoherentes);

/// <summary>De los que viajaban en el periodo, en cuantos se vio el problema a tiempo.</summary>
/// <remarks>
/// Los cinco cubos —<see cref="DetectadosATiempo"/>, <see cref="DetectadosDespuesDelViaje"/>,
/// <see cref="ConProblemaSinFechaDeDeteccion"/>, <see cref="SinEstadoRegistrado"/> y
/// <see cref="ConRecomendacionResuelta"/>— son EXCLUYENTES y suman
/// <see cref="CasosDelPeriodo"/>. Es lo que permite comprobar el reporte sumando: si los cinco
/// no suman el universo, algo se conto dos veces.
/// </remarks>
/// <param name="CasosDelPeriodo">Cuantos casos viajaban en el periodo. Es el denominador.</param>
/// <param name="ConProblemaRegistrado">Cuantos llevan un problema escrito, en cualquier cubo.</param>
/// <param name="DetectadosATiempo">Vistos el dia anterior al viaje o antes.</param>
/// <param name="DetectadosDespuesDelViaje">Vistos el mismo dia del viaje o despues.</param>
/// <param name="ConProblemaSinFechaDeDeteccion">Con problema y sin ningun campo verificado.</param>
/// <param name="SinEstadoRegistrado">De los que nadie ha dicho nada.</param>
/// <param name="ConRecomendacionResuelta">Los que ya estan resueltos.</param>
public sealed record Deteccion(
    int CasosDelPeriodo,
    int ConProblemaRegistrado,
    int DetectadosATiempo,
    int DetectadosDespuesDelViaje,
    int ConProblemaSinFechaDeDeteccion,
    int SinEstadoRegistrado,
    int ConRecomendacionResuelta);

/// <summary>
/// Las tres metricas de trabajo del equipo. Solo aritmetica: aqui no se consulta.
/// </summary>
/// <remarks>
/// Portado de <c>reportes/metricas.py</c>. Las tres parten de la MISMA lectura y por eso no
/// pueden discrepar entre ellas. Cada una recorta el trozo que le toca, y las tres dicen a las
/// claras que columna de fecha usan, porque CADA UNA USA UNA DISTINTA y confundirlas es leer
/// el reporte al reves:
/// <list type="number">
///   <item>Casos verificados: por <c>verificado_en</c> (cuando se termino).</item>
///   <item>Demora de importar a verificar: por <c>verificado_en</c>, midiendo hasta <c>creado_en</c>.</item>
///   <item>Deteccion antes del viaje: por <c>fecha_viaje</c> (cuando viajaba la gente).</item>
/// </list>
///
/// ⚠️ <b>La metrica 3 y el hueco que hoy tiene.</b> «Detectado con problema» significa que el
/// caso lleva un estado escrito y ese valor no es de los que resuelven. Un estado vacio NO
/// cuenta como problema: cuenta como que nadie ha dicho nada, y sale en su propio numero. Esa
/// distincion es lo contrario de lo que hace el calendario —alli un estado vacio pinta en
/// rojo, que es el lado seguro para una alarma— y es a proposito: una alarma de mas se descarta
/// mirandola, pero un numero de mas en un reporte a los jefes es una cifra falsa.
/// </remarks>
public static class Metricas
{
    private const string FormatoDeMarcaDeTiempo = "yyyy-MM-dd HH:mm:ss";
    private const int LargoDeLaFecha = 10;

    /// <summary>Dice si a ese caso no le queda ni un campo por verificar.</summary>
    public static bool CasoVerificado(CasoConSuVerificacion caso)
        => caso.Campos > 0 && caso.CamposVerificados == caso.Campos;

    /// <summary>Metrica 1: los casos que quedaron verificados dentro del periodo.</summary>
    /// <remarks>Se ordenan por el instante en que se terminaron, que es como se lee trabajo hecho.</remarks>
    public static IReadOnlyList<CasoConSuVerificacion> CasosVerificadosEnElPeriodo(
        IReadOnlyList<CasoConSuVerificacion> casos, Periodo periodo)
        => casos
            .Where(c => CasoVerificado(c) && periodo.ContieneMarcaConHora(c.VerificadoEn))
            .OrderBy(c => c.VerificadoEn, StringComparer.Ordinal)
            .ToList();

    /// <summary>Metrica 2: cuanto se tarda desde que un caso entra hasta que queda verificado.</summary>
    /// <remarks>
    /// Mide los MISMOS casos que la metrica 1, asi que el denominador de las dos es el mismo
    /// numero y se pueden leer juntas.
    ///
    /// Una demora negativa se descarta y se cuenta aparte en vez de promediarse. Una fecha de
    /// verificacion anterior a la de importacion no es un trabajo hecho en tiempo negativo: es
    /// un reloj que se movio, y meterla en la media bajaria el promedio de todos los demas sin
    /// que nadie sepa por que.
    /// </remarks>
    public static Demora DemoraDeImportarAVerificar(
        IReadOnlyList<CasoConSuVerificacion> casos, Periodo periodo)
    {
        var horas = new List<double>();
        var descartados = 0;

        foreach (var caso in CasosVerificadosEnElPeriodo(casos, periodo))
        {
            var medida = HorasEntre(caso.Caso.CreadoEn, caso.VerificadoEn);
            if (medida is null or < 0)
            {
                descartados++;
                continue;
            }
            horas.Add(medida.Value);
        }

        return horas.Count == 0
            ? new Demora(0, null, null, null, descartados)
            : new Demora(horas.Count, horas.Average(), horas.Min(), horas.Max(), descartados);
    }

    /// <summary>Metrica 3: de los que viajaban en el periodo, en cuantos se vio el problema a tiempo.</summary>
    /// <remarks>
    /// Es la metrica por la que existe el programa: un problema visto el dia antes se puede
    /// arreglar, y visto el dia despues ya mando a alguien al templo para nada.
    ///
    /// «A tiempo» es EL DIA ANTERIOR O ANTES, no el mismo dia. Detectar el problema la manana
    /// del viaje no deja margen para arreglar una recomendacion, y contarlo como exito seria
    /// contar como salvado a alguien que no se salvo.
    ///
    /// La fecha de deteccion es el instante en que se termino de verificar el caso: es lo mas
    /// cercano a «cuando alguien miro esto» que la base guarda hoy. Un caso sin ningun campo
    /// verificado no tiene fecha de deteccion y sale en su propio numero, no repartido.
    /// </remarks>
    public static Deteccion DeteccionAntesDelViaje(
        IReadOnlyList<CasoConSuVerificacion> casos, Periodo periodo)
    {
        var delPeriodo = casos.Where(c => periodo.ContieneFecha(c.Caso.FechaViaje)).ToList();

        var cubos = new Dictionary<string, int>
        {
            ["sin_estado"] = 0,
            ["resuelta"] = 0,
            ["sin_fecha_de_deteccion"] = 0,
            ["a_tiempo"] = 0,
            ["tarde"] = 0,
        };
        foreach (var caso in delPeriodo) cubos[Clasificar(caso)]++;

        var conProblema = cubos["a_tiempo"] + cubos["tarde"] + cubos["sin_fecha_de_deteccion"];

        return new Deteccion(
            CasosDelPeriodo: delPeriodo.Count,
            ConProblemaRegistrado: conProblema,
            DetectadosATiempo: cubos["a_tiempo"],
            DetectadosDespuesDelViaje: cubos["tarde"],
            ConProblemaSinFechaDeDeteccion: cubos["sin_fecha_de_deteccion"],
            SinEstadoRegistrado: cubos["sin_estado"],
            ConRecomendacionResuelta: cubos["resuelta"]);
    }

    /// <summary>En cual de los cinco cubos cae un caso del periodo. Uno y solo uno.</summary>
    private static string Clasificar(CasoConSuVerificacion caso)
    {
        if (Estados.SinEstadoEscrito(caso.Caso.EstadoRecomendacion)) return "sin_estado";
        if (!Estados.SinResolver(caso.Caso.EstadoRecomendacion)) return "resuelta";

        var diaDeLaDeteccion = DiaDe(caso.VerificadoEn);
        if (diaDeLaDeteccion is null) return "sin_fecha_de_deteccion";

        return string.CompareOrdinal(diaDeLaDeteccion, caso.Caso.FechaViaje) < 0 ? "a_tiempo" : "tarde";
    }

    /// <summary>Cuantas horas pasaron entre las dos marcas, o nulo si alguna no se puede leer.</summary>
    /// <remarks>
    /// Devuelve nulo y NO cero cuando una fecha esta mal escrita: un cero se sumaria al promedio
    /// como si el caso se hubiera verificado al instante, que es una cifra inventada. Sin fecha
    /// legible el caso se descarta y se cuenta aparte.
    /// </remarks>
    private static double? HorasEntre(string? creadoEn, string? verificadoEn)
    {
        if (!DateTime.TryParseExact(creadoEn, FormatoDeMarcaDeTiempo, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var entrada))
        {
            return null;
        }
        if (!DateTime.TryParseExact(verificadoEn, FormatoDeMarcaDeTiempo, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var salida))
        {
            return null;
        }
        return (salida - entrada).TotalHours;
    }

    /// <summary>El dia de una marca de tiempo, para compararlo con una fecha de viaje.</summary>
    private static string? DiaDe(string? marca)
        => marca is null || marca.Length < LargoDeLaFecha ? null : marca[..LargoDeLaFecha];
}
