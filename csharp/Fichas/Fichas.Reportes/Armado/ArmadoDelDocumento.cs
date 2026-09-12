using System.Globalization;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>
/// El reporte armado una sola vez: secciones, columnas, filas y avisos.
/// </summary>
/// <remarks>
/// Portado de <c>reportes/documento.py</c>. El PDF sale de aqui y no vuelve a contar nada: dos
/// codigos que cuentan lo mismo por su cuenta acaban dando dos numeros distintos, y un reporte
/// con dos totales no sirve para lo unico que sirve un reporte.
///
/// ⚠️ <b>«Verificado» significa aqui la firma de Miguel, y SOLO aqui.</b> Las tres metricas del
/// final cuentan campos de procedencia firmados, o sea lo que Miguel dio por bueno mirando el
/// papel. Las secciones de la direccion hablan de otra cosa —los seis pasos que contesta un
/// companero— y la llaman por su nombre. Las dos palabras conviven en el mismo documento a
/// proposito porque son dos hechos distintos; lo que no puede volver es que se llamen igual.
/// </remarks>
public static class ArmadoDelDocumento
{
    /// <summary>Las nueve columnas de la parte 1. El MRN, el número de caso y el de unidad van como texto para que el Excel no se coma sus ceros.</summary>
    private static readonly Columna[] ColumnasDeQuienViajo =
    [
        new("N.º de caso", ClaseDeColumna.Texto, 12),
        new("Persona", ClaseDeColumna.Crudo, 30),
        new("MRN", ClaseDeColumna.Texto, 14),
        // ⚠️ 2026-09-08: la unidad va en DOS columnas por orden del dueno —«Sí, pártelo en
        // dos»—. El numero PRIMERO y como Texto: pegado detras del nombre no se podia filtrar
        // por el, y como Crudo el .xlsx se comeria su cero de delante.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Unidad", ClaseDeColumna.Crudo, 22),
        new("Fecha de viaje", ClaseDeColumna.Temporal, 14),
        new("¿Viajó?", ClaseDeColumna.Crudo, 12),
        new("Estado de la recomendación", ClaseDeColumna.Crudo, 22),
        new("¿Recomendación completa?", ClaseDeColumna.Crudo, 22),
    ];

    /// <summary>Las ocho columnas de la parte 2: las mismas seis de identidad que la parte 1, más el motivo y si se vio a tiempo.</summary>
    private static readonly Columna[] ColumnasDeQuienNoViajo =
    [
        new("N.º de caso", ClaseDeColumna.Texto, 12),
        new("Persona", ClaseDeColumna.Crudo, 30),
        new("MRN", ClaseDeColumna.Texto, 14),
        // ⚠️ 2026-09-08: la unidad va en DOS columnas por orden del dueno —«Sí, pártelo en
        // dos»—. El numero PRIMERO y como Texto: pegado detras del nombre no se podia filtrar
        // por el, y como Crudo el .xlsx se comeria su cero de delante.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Unidad", ClaseDeColumna.Crudo, 22),
        new("Fecha de viaje", ClaseDeColumna.Temporal, 14),
        new("Motivo por el que no pudo viajar", ClaseDeColumna.Crudo, 46),
        new("¿Se detectó antes del viaje?", ClaseDeColumna.Crudo, 24),
    ];

    /// <summary>Las tres columnas de la tabla de métricas; la tercera dice con qué columna de fecha se contó cada una.</summary>
    private static readonly Columna[] ColumnasDeLasMetricas =
    [
        new("Métrica", ClaseDeColumna.Crudo, 40),
        new("Número", ClaseDeColumna.Crudo, 16),
        new("Cómo se cuenta", ClaseDeColumna.Crudo, 60),
    ];

    /// <summary>Arma el reporte entero de ese periodo. Devuelve el documento, sin escribirlo.</summary>
    /// <remarks>
    /// <paramref name="generadoEn"/> entra desde fuera y no se lee del reloj aqui: una funcion
    /// que mira el reloj por dentro no se puede probar, y de que dia se considera «ya viajó»
    /// depende la cifra de la portada.
    /// </remarks>
    /// <param name="lectura">Todo lo leído de los puertos, sin filtrar; aquí se recorta al periodo.</param>
    /// <param name="periodo">El periodo que se reporta, ya validado.</param>
    /// <param name="generadoEn">La marca «AAAA-MM-DD HH:mm:ss» con la que se genera; de sus diez primeros caracteres sale el «hoy».</param>
    /// <returns>El documento con portada, avisos y las nueve secciones como mucho.</returns>
    public static Documento DelPeriodo(LecturaParaReportes lectura, Periodo periodo, string generadoEn)
    {
        var personas = lectura.PersonasDelPeriodo(periodo);
        var sinFecha = lectura.PersonasQueNoViajaronSinFecha();
        var casos = lectura.CasosConSuVerificacion;

        var viajaron = personas.Where(f => f.Persona.PudoViajar != false).ToList();
        var noViajaron = personas.Where(f => f.Persona.PudoViajar == false).ToList();
        var deteccion = Metricas.DeteccionAntesDelViaje(casos, periodo);

        var diaDeHoy = DiaDe(generadoEn);
        var recuento = Preparacion.Recontar(personas, diaDeHoy);

        return new Documento(
            Vocabulario.Titulo,
            $"Período: {periodo.EnTexto()}",
            generadoEn,
            SeccionesDeDireccion.Portada(recuento),
            Avisos.DelReporte(deteccion, sinFecha),
            Secciones(lectura, periodo, personas, viajaron, noViajaron, casos, deteccion, diaDeHoy, recuento));
    }

    /// <summary>Las secciones del reporte, en el orden en que se leen.</summary>
    /// <remarks>
    /// Las tres primeras son las del informe del proyecto viejo, en su orden, y van DELANTE:
    /// quien lo lee tiene que tropezarse con el numero que duele antes que con nada mas. Las
    /// tres de la FASE 8 se quedan detras, que es donde las busca quien ya sabe que quiere mirar.
    ///
    /// «Dónde se traban» y «Unidades» devuelven nulo cuando no hay nada pendiente: una tabla de
    /// ceros ocupa el sitio de lo que si dice algo. «El equipo» sale siempre, y eso es a
    /// proposito: que nadie tenga nada asignado es justamente lo que la direccion tiene que ver.
    /// </remarks>
    /// <param name="lectura">La lectura entera, de la que se toman los compañeros por caso.</param>
    /// <param name="periodo">El periodo que se reporta.</param>
    /// <param name="personas">Todas las personas del periodo.</param>
    /// <param name="viajaron">Las que no constan como que no pudieron viajar.</param>
    /// <param name="noViajaron">Las anotadas como que no pudieron viajar.</param>
    /// <param name="casos">Todos los casos con su verificación, sin filtrar por periodo.</param>
    /// <param name="deteccion">La métrica 3, ya calculada.</param>
    /// <param name="diaDeHoy">El «hoy» en «AAAA-MM-DD».</param>
    /// <param name="recuento">Las cuatro cifras de la portada, ya contadas.</param>
    /// <returns>Entre siete y nueve secciones, según haya pasos trabados y unidades con pendientes.</returns>
    private static List<Seccion> Secciones(
        LecturaParaReportes lectura,
        Periodo periodo,
        IReadOnlyList<PersonaConSuCaso> personas,
        IReadOnlyList<PersonaConSuCaso> viajaron,
        IReadOnlyList<PersonaConSuCaso> noViajaron,
        IReadOnlyList<CasoConSuVerificacion> casos,
        Deteccion deteccion,
        string diaDeHoy,
        Recuento recuento)
    {
        var secciones = new List<Seccion>
        {
            SeccionesDeDireccion.QuienViajoSinVerificar(recuento.SinCompletar),
            SeccionesDeDireccion.LosViajes(
                Preparacion.ResumenPorCaso(personas, diaDeHoy, lectura.CompanerosPorCaso)),
            SeccionesDeDireccion.AQueVan(personas),
        };

        foreach (var seccion in new[]
        {
            SeccionesDeDireccion.DondeSeTraban(personas),
            SeccionesDeDireccion.LasUnidades(personas),
            SeccionesDeDireccion.ElEquipo(personas, lectura.CompanerosPorCaso),
        })
        {
            if (seccion is not null) secciones.Add(seccion);
        }

        var casosPorId = casos.ToDictionary(c => c.Caso.Id);
        secciones.Add(DeQuienViajo(viajaron));
        secciones.Add(DeQuienNoViajo(noViajaron, casosPorId));
        secciones.Add(DeLasMetricas(
            Metricas.CasosVerificadosEnElPeriodo(casos, periodo),
            Metricas.DemoraDeImportarAVerificar(casos, periodo),
            deteccion));

        return secciones;
    }

    /// <summary>Parte 1: las personas cuyo caso viajaba en el periodo, y como quedo.</summary>
    /// <param name="personas">Las del periodo que no constan como que no pudieron viajar; una por fila.</param>
    private static Seccion DeQuienViajo(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var constanComoQueViajaron = personas.Count(f => f.Persona.PudoViajar == true);
        var completas = personas.Count(f => Estados.TextoDeRecomendacionCompleta(f.Caso.EstadoRecomendacion) == "sí");

        return new Seccion(
            "Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación",
            [
                "Entran todas las personas cuyo caso tenía fecha de viaje dentro del período, "
                + "estén archivadas o no. Archivar un caso no cambia este total.",
            ],
            ColumnasDeQuienViajo,
            personas
                .Select(f => (IReadOnlyList<string?>)new string?[]
                {
                    f.Caso.NumeroCaso,
                    Vocabulario.PersonaOSinNombre(f.Persona.Nombre),
                    string.IsNullOrWhiteSpace(f.Persona.Mrn) ? Vocabulario.SinDato : f.Persona.Mrn,
                    Vocabulario.NumeroDeUnidad(f.Caso.UnidadNumero),
                    Vocabulario.NombreDeUnidad(f.Caso.UnidadNombre),
                    f.FechaViaje,
                    Estados.TextoDeSiViajo(f.Persona.PudoViajar),
                    Vocabulario.TextoDelEstado(f.Caso.EstadoRecomendacion),
                    Estados.TextoDeRecomendacionCompleta(f.Caso.EstadoRecomendacion),
                })
                .ToList(),
            Plural.Con(personas.Count, "persona", "personas")
            + $" · {constanComoQueViajaron} "
            + Plural.Palabra(constanComoQueViajaron, "consta", "constan")
            + " como que sí "
            + Plural.Palabra(constanComoQueViajaron, "viajó", "viajaron")
            + $" · {completas} con la recomendación completa");
    }

    /// <summary>Parte 2: quien no pudo viajar, con su motivo tal como lo escribio Miguel.</summary>
    /// <param name="personas">Las anotadas como que no pudieron viajar; una por fila.</param>
    /// <param name="casosPorId">La verificación de cada caso, por id, para fechar cuándo se vio el problema.</param>
    private static Seccion DeQuienNoViajo(
        IReadOnlyList<PersonaConSuCaso> personas,
        IReadOnlyDictionary<long, CasoConSuVerificacion> casosPorId)
        => new(
            "Parte 2 — Personas que NO pudieron viajar",
            [
                "Solo aparece quien está anotado a mano como que no pudo viajar. El programa no "
                + "lo deduce de ningún dato: alguien tuvo que escribirlo, y el motivo es lo que "
                + "esa persona escribió, sin retocar.",
            ],
            ColumnasDeQuienNoViajo,
            personas
                .Select(f => (IReadOnlyList<string?>)new string?[]
                {
                    f.Caso.NumeroCaso,
                    Vocabulario.PersonaOSinNombre(f.Persona.Nombre),
                    string.IsNullOrWhiteSpace(f.Persona.Mrn) ? Vocabulario.SinDato : f.Persona.Mrn,
                    Vocabulario.NumeroDeUnidad(f.Caso.UnidadNumero),
                    Vocabulario.NombreDeUnidad(f.Caso.UnidadNombre),
                    f.FechaViaje,
                    f.Persona.MotivoNoViajo,
                    TextoDeLaDeteccion(casosPorId.GetValueOrDefault(f.CasoId), f.FechaViaje),
                })
                .ToList(),
            Plural.Con(personas.Count, "persona no pudo viajar", "personas no pudieron viajar")
            + " en este período");

    /// <summary>Si el problema de ese caso estaba visto antes del dia del viaje.</summary>
    /// <param name="caso">El caso con su última firma; nulo o sin firma da «no se verificó ningún campo».</param>
    /// <param name="fechaViaje">La fecha «AAAA-MM-DD» del viaje contra la que se compara el día de la firma.</param>
    /// <returns>«sí, el DÍA» si la firma es anterior al viaje; «no, se vio el DÍA» si es el mismo día o después.</returns>
    private static string TextoDeLaDeteccion(CasoConSuVerificacion? caso, string? fechaViaje)
    {
        if (caso?.VerificadoEn is null || caso.VerificadoEn.Length < 10) return "no se verificó ningún campo";

        var dia = caso.VerificadoEn[..10];
        return string.CompareOrdinal(dia, fechaViaje) < 0 ? $"sí, el {dia}" : $"no, se vio el {dia}";
    }

    /// <summary>Las metricas de trabajo del equipo, con sus denominadores a la vista.</summary>
    /// <param name="verificados">Métrica 1: los casos verificados en el periodo.</param>
    /// <param name="demora">Métrica 2: la demora de importar a verificar.</param>
    /// <param name="deteccion">Métrica 3: la detección antes del viaje.</param>
    /// <returns>Una sección de tres filas fijas y sin resumen.</returns>
    private static Seccion DeLasMetricas(
        IReadOnlyList<CasoConSuVerificacion> verificados, Demora demora, Deteccion deteccion)
        => new(
            "Métricas del trabajo del equipo",
            [
                "Cada métrica usa una columna de fecha distinta y por eso los tres números no son "
                + "comparables entre sí: la columna «Cómo se cuenta» dice cuál usa cada una.",
                $"Casos del período por fecha de viaje: {deteccion.CasosDelPeriodo}. "
                + $"Sin estado de recomendación escrito: {deteccion.SinEstadoRegistrado}. "
                + $"Con la recomendación resuelta: {deteccion.ConRecomendacionResuelta}.",
            ],
            ColumnasDeLasMetricas,
            [
                [
                    "1. Casos verificados en el período",
                    SeccionesDeDireccion.Numero(verificados.Count),
                    "Un caso cuenta cuando tiene campos leídos y TODOS quedaron verificados. Se "
                    + "sitúa en el período por la fecha del último campo verificado.",
                ],
                [
                    "2. Tiempo promedio de importar a verificar",
                    EnHoras(demora.PromedioEnHoras),
                    $"Promedio sobre {demora.CasosMedidos} casos de los de arriba. "
                    + $"Mínimo {EnHoras(demora.MinimoEnHoras)}, máximo {EnHoras(demora.MaximoEnHoras)}. "
                    + $"Descartados por fechas incoherentes: {demora.DescartadosPorFechasIncoherentes}.",
                ],
                [
                    "3. Casos con el problema detectado ANTES de la fecha de viaje",
                    SeccionesDeDireccion.Numero(deteccion.DetectadosATiempo),
                    $"De los {deteccion.CasosDelPeriodo} casos que viajaban en el período, "
                    + $"{deteccion.ConProblemaRegistrado} tienen un problema escrito. De esos: "
                    + $"{deteccion.DetectadosATiempo} vistos antes del día del viaje, "
                    + $"{deteccion.DetectadosDespuesDelViaje} el mismo día o después, "
                    + $"{deteccion.ConProblemaSinFechaDeDeteccion} sin ningún campo verificado con "
                    + "el que fechar cuándo se vio.",
                ],
            ],
            null);

    /// <summary>Las horas con un decimal y su equivalente en dias, en espanol.</summary>
    /// <param name="horas">La medida; nula da <see cref="Vocabulario.NoSePuedeSaber"/>.</param>
    /// <returns>Por ejemplo «36,5 h (1,5 días)», con coma decimal aunque la máquina esté en inglés.</returns>
    private static string EnHoras(double? horas)
    {
        if (horas is null) return Vocabulario.NoSePuedeSaber;

        var enHoras = horas.Value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
        var enDias = (horas.Value / 24).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
        return $"{enHoras} h ({enDias} días)";
    }

    /// <summary>El dia de la marca de tiempo con la que se genera el informe.</summary>
    /// <remarks>
    /// Se saca de la marca y no del reloj: una funcion que mira el reloj por dentro no se puede
    /// probar, y de que dia se considera «ya viajó» depende la cifra de la portada.
    /// </remarks>
    /// <param name="generadoEn">La marca completa; si es más corta que una fecha, se devuelve tal cual.</param>
    private static string DiaDe(string generadoEn)
        => generadoEn.Length >= 10 ? generadoEn[..10] : generadoEn;
}
