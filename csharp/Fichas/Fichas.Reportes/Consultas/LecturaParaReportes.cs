using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Consultas;

/// <summary>
/// Lo que los puertos dicen para el reporte. Solo lectura: aqui no se cuenta nada.
/// </summary>
/// <remarks>
/// Portado de <c>reportes/consultas.py</c> y de <c>datos/archivo.py</c>
/// (<c>casos_archivados</c>).
///
/// ⚠️ <b>NINGUNA lectura de esta clase filtra por archivado</b>, y esa ausencia es la decision
/// de DECISIONES.md (2026-09-02, «Histórico») y el criterio C8-3: «los archivados salen de las
/// listas de trabajo pero SIGUEN CONTANDO en los reportes». Es justo al reves que el
/// calendario y la lista de pendientes, que los excluyen. Las dos mitades de la misma frase,
/// cada una en su sitio. La consecuencia comprobable: archivar un caso del periodo NO cambia
/// ningun total de aqui, y hay una prueba que lo mide.
///
/// ⚠️ <b>Y una limitacion del contrato que hay que decir, no tapar.</b> <see cref="IProcedencia"/>
/// no tiene ninguna lectura en bloque: para saber cuantos campos tiene cada caso y cuando se
/// firmo el ultimo hay que llamar a <c>DeRegistro</c> UNA VEZ POR CASO Y UNA POR PERSONA. Con
/// 3 000 casos son unas 10 500 llamadas. Con el almacen en memoria se aguanta y esta medido;
/// contra SQLite serian 10 500 consultas, y eso pide un metodo nuevo en Fichas.Contratos que NO
/// se toca aqui porque esta congelado. Va en la entrega.
/// </remarks>
public sealed class LecturaParaReportes
{
    /// <summary>
    /// La página que lo pide todo de una vez. Los puertos paginan siempre, y un reporte no puede
    /// quedarse con la primera página de los casos del periodo.
    /// </summary>
    private static readonly Pagina Entera = new(0, int.MaxValue);

    /// <summary>Guarda las cuatro lecturas; solo entran por <see cref="Leer"/>, <see cref="DeMemoria"/> o <see cref="SoloEstosCasos"/>.</summary>
    /// <param name="casos">Todos los casos, archivados incluidos.</param>
    /// <param name="personasPorCaso">Las personas de cada caso, por id de caso.</param>
    /// <param name="companerosPorCaso">Los nombres de quien lleva cada caso, por id de caso.</param>
    /// <param name="casosConSuVerificacion">Cada caso con su recuento de campos y su última firma.</param>
    private LecturaParaReportes(
        IReadOnlyList<Caso> casos,
        IReadOnlyDictionary<long, IReadOnlyList<Persona>> personasPorCaso,
        IReadOnlyDictionary<long, IReadOnlyList<string>> companerosPorCaso,
        IReadOnlyList<CasoConSuVerificacion> casosConSuVerificacion)
    {
        Casos = casos;
        PersonasPorCaso = personasPorCaso;
        CompanerosPorCaso = companerosPorCaso;
        CasosConSuVerificacion = casosConSuVerificacion;
    }

    /// <summary>Todos los casos, ARCHIVADOS INCLUIDOS.</summary>
    public IReadOnlyList<Caso> Casos { get; }

    /// <summary>Las personas de cada caso, en el orden del formulario.</summary>
    public IReadOnlyDictionary<long, IReadOnlyList<Persona>> PersonasPorCaso { get; }

    /// <summary>Quien lleva cada caso HOY; solo las asignaciones vivas.</summary>
    /// <remarks>
    /// Quien llevo un caso y ya no lo lleva no es a quien hay que preguntarle por el. Los
    /// nombres van ordenados: dos informes del mismo periodo que listan los mismos dos nombres
    /// en distinto orden se leen como si algo hubiera cambiado.
    /// </remarks>
    public IReadOnlyDictionary<long, IReadOnlyList<string>> CompanerosPorCaso { get; }

    /// <summary>Todos los casos con cuantos campos tienen, cuantos verificados, y cuando.</summary>
    /// <remarks>
    /// Sin filtrar por periodo ni por archivado: quien necesite un trozo lo recorta despues, y
    /// asi las tres metricas parten de la MISMA lectura y no pueden discrepar entre ellas.
    /// </remarks>
    public IReadOnlyList<CasoConSuVerificacion> CasosConSuVerificacion { get; }

    /// <summary>Arma una lectura con datos ya en memoria, sin pasar por ningun puerto.</summary>
    /// <remarks>
    /// Existe para poder armar un documento sobre un juego de datos escrito a mano y
    /// compararlo linea a linea con el que produce el Python. No es un atajo de produccion: el
    /// programa siempre entra por <see cref="Leer"/>.
    /// </remarks>
    /// <param name="casos">Todos los casos, archivados incluidos.</param>
    /// <param name="personasPorCaso">Las personas de cada caso, por id de caso.</param>
    /// <param name="companerosPorCaso">Los nombres de quien lleva cada caso, por id de caso.</param>
    /// <param name="casosConSuVerificacion">Cada caso con su recuento de campos y su última firma.</param>
    public static LecturaParaReportes DeMemoria(
        IReadOnlyList<Caso> casos,
        IReadOnlyDictionary<long, IReadOnlyList<Persona>> personasPorCaso,
        IReadOnlyDictionary<long, IReadOnlyList<string>> companerosPorCaso,
        IReadOnlyList<CasoConSuVerificacion> casosConSuVerificacion)
        => new(casos, personasPorCaso, companerosPorCaso, casosConSuVerificacion);

    /// <summary>Lee de los puertos todo lo que un reporte necesita, de una sola vez.</summary>
    /// <remarks>
    /// Es la única entrada de producción. Pide los casos con archivados incluidos y todas las
    /// personas en una página entera, y ordena las personas de cada caso por su fila del
    /// formulario con el id de desempate.
    /// </remarks>
    /// <param name="casos">El puerto de casos.</param>
    /// <param name="personas">El puerto de personas.</param>
    /// <param name="companeros">El puerto de compañeros; se leen activos e inactivos.</param>
    /// <param name="asignaciones">El puerto de asignaciones; solo se leen las vivas.</param>
    /// <param name="procedencia">El puerto de procedencia; se consulta una vez por caso y una por persona.</param>
    public static LecturaParaReportes Leer(
        ICasos casos, IPersonas personas, ICompaneros companeros,
        IAsignaciones asignaciones, IProcedencia procedencia)
    {
        var todosLosCasos = casos.Listar(new FiltroDeCasos(IncluirArchivados: true), Entera).Elementos;

        var personasPorCaso = personas
            .Listar(FiltroDePersonas.Todo, Entera).Elementos
            .GroupBy(p => p.CasoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Persona>)g
                    .OrderBy(p => p.FilaFormulario ?? int.MaxValue)
                    .ThenBy(p => p.Id)
                    .ToList());

        return new LecturaParaReportes(
            todosLosCasos,
            personasPorCaso,
            LeerLosCompaneros(companeros, asignaciones),
            LeerLaVerificacion(todosLosCasos, personasPorCaso, procedencia));
    }

    /// <summary>Las personas cuyo caso viaja dentro del periodo, con lo anotado del viaje.</summary>
    /// <remarks>
    /// Sale UNA sola lectura y de ella salen las dos mitades del reporte —quien viajo y quien no
    /// pudo—, separadas despues por <c>PudoViajar</c>. Es a proposito: con dos consultas, un
    /// filtro escrito distinto en cada una podria dejar a alguien fuera de las dos listas a la
    /// vez, y esa persona no apareceria en ninguna parte del reporte sin que nadie lo notara.
    ///
    /// El orden es el de lectura: por fecha de viaje, luego por caso, luego por la fila del
    /// formulario. Con dos casos del mismo numero, el id desempata (migracion 12).
    /// </remarks>
    /// <param name="periodo">El periodo; se compara contra la fecha de viaje del caso, sin hora.</param>
    /// <returns>Las personas en orden de lectura; vacía si ningún caso viaja en el periodo.</returns>
    public IReadOnlyList<PersonaConSuCaso> PersonasDelPeriodo(Periodo periodo)
        => Casos
            .Where(c => periodo.ContieneFecha(c.FechaViaje))
            .OrderBy(c => c.FechaViaje, StringComparer.Ordinal)
            .ThenBy(c => c.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .SelectMany(caso => PersonasDe(caso.Id).Select(p => new PersonaConSuCaso(p, caso)))
            .ToList();

    /// <summary>Las anotadas como que no pudieron viajar cuyo caso NO tiene fecha de viaje.</summary>
    /// <remarks>
    /// No caben en ningun periodo —no hay con que compararlas— y por eso salen aparte: una
    /// persona que no pudo viajar y no aparece en ningun reporte es exactamente la que se
    /// pierde. El reporte las cuenta en su aviso aunque no sean del periodo, para que el numero
    /// de arriba no mienta por omision.
    /// </remarks>
    public IReadOnlyList<PersonaConSuCaso> PersonasQueNoViajaronSinFecha()
        => Casos
            .Where(c => c.FechaViaje is null)
            .OrderBy(c => c.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .SelectMany(caso => PersonasDe(caso.Id)
                .Where(p => p.PudoViajar == false)
                .Select(p => new PersonaConSuCaso(p, caso)))
            .ToList();

    /// <summary>El historico: los casos archivados, el ultimo archivado primero.</summary>
    public IReadOnlyList<CasoArchivado> Archivados()
        => Casos
            .Where(c => c.Archivado)
            .OrderByDescending(c => c.FechaArchivado ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .Select(caso =>
            {
                var suyas = PersonasDe(caso.Id);
                return new CasoArchivado(caso, suyas.Count, suyas.Count(p => p.PudoViajar == false));
            })
            .ToList();

    /// <summary>Las personas de un caso; una lista vacia si no tiene ninguna.</summary>
    /// <param name="casoId">El id del caso, no su número.</param>
    public IReadOnlyList<Persona> PersonasDe(long casoId)
        => PersonasPorCaso.TryGetValue(casoId, out var suyas) ? suyas : [];

    /// <summary>La misma lectura recortada a unos pocos casos, y a un solo nombre detras.</summary>
    /// <remarks>
    /// Es lo que hace posible el reporte de UN companero sin escribir un segundo armado: el
    /// informe es el mismo, con menos casos dentro. Si hubiera dos armados, uno de los dos se
    /// quedaria atras el dia que cambie una seccion, y nadie lo notaria hasta que los dos
    /// informes dijeran cosas distintas del mismo mes.
    ///
    /// El nombre se pone en TODOS los casos que quedan, y a proposito: un caso puede llevarlo
    /// mas de un companero (la P-11 sigue abierta), y en SU informe la tabla del equipo tiene
    /// que hablar de el. La pregunta que contesta es «¿de qué respondo yo?».
    /// </remarks>
    /// <param name="casoIds">Los ids de los casos que se quedan; el resto desaparece de las cuatro lecturas.</param>
    /// <param name="nombreDelCompanero">El nombre que se pone como único responsable de todos los que quedan.</param>
    /// <returns>Una lectura nueva; esta no se toca.</returns>
    public LecturaParaReportes SoloEstosCasos(IReadOnlySet<long> casoIds, string nombreDelCompanero)
    {
        var casos = Casos.Where(c => casoIds.Contains(c.Id)).ToList();

        return new LecturaParaReportes(
            casos,
            PersonasPorCaso
                .Where(par => casoIds.Contains(par.Key))
                .ToDictionary(par => par.Key, par => par.Value),
            casos.ToDictionary(c => c.Id, _ => (IReadOnlyList<string>)[nombreDelCompanero]),
            CasosConSuVerificacion.Where(c => casoIds.Contains(c.Caso.Id)).ToList());
    }

    // ---- lo que se lee una sola vez -----------------------------------------

    /// <summary>Los nombres de quien lleva cada caso hoy, por id de caso y en orden ordinal.</summary>
    /// <remarks>
    /// Solo entran las asignaciones vivas, pero los nombres se buscan entre activos e inactivos:
    /// un caso que lleva alguien que ya se fue sigue teniendo un nombre al que preguntar. Una
    /// asignación cuyo compañero no aparece sale como <see cref="Vocabulario.SinAgente"/>.
    /// </remarks>
    /// <param name="companeros">El puerto de compañeros.</param>
    /// <param name="asignaciones">El puerto de asignaciones.</param>
    /// <returns>Solo los casos que tienen alguien; un caso sin asignación no está en el diccionario.</returns>
    private static Dictionary<long, IReadOnlyList<string>> LeerLosCompaneros(
        ICompaneros companeros, IAsignaciones asignaciones)
    {
        // Los inactivos entran tambien: un caso que lleva alguien que ya se fue sigue
        // teniendo un nombre al que preguntar, y dejarlo como «sin asignar» seria mentir.
        var nombres = companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Entera).Elementos
            .ToDictionary(c => c.Id, c => c.Nombre);

        return asignaciones
            .Listar(FiltroDeAsignaciones.Activas, Entera).Elementos
            .GroupBy(a => a.CasoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(a => nombres.TryGetValue(a.CompaneroId, out var nombre) ? nombre : Vocabulario.SinAgente)
                    .Order(StringComparer.Ordinal)
                    .ToList());
    }

    /// <summary>Cuenta los campos de cada caso —los suyos y los de sus personas— y su firma.</summary>
    /// <remarks>
    /// La marca es la del ULTIMO campo verificado del caso: el instante en que alguien termino
    /// de mirarlo. Es nula mientras no haya ni un campo verificado.
    /// </remarks>
    /// <param name="casos">Todos los casos.</param>
    /// <param name="personasPorCaso">Las personas de cada caso, por id de caso.</param>
    /// <param name="procedencia">El puerto de procedencia; se llama una vez por caso y una por persona.</param>
    /// <returns>Un registro por caso, ordenados por número de caso y luego por id.</returns>
    private static List<CasoConSuVerificacion> LeerLaVerificacion(
        IReadOnlyList<Caso> casos,
        IReadOnlyDictionary<long, IReadOnlyList<Persona>> personasPorCaso,
        IProcedencia procedencia)
    {
        var lectura = new List<CasoConSuVerificacion>(casos.Count);

        foreach (var caso in casos)
        {
            var suyas = personasPorCaso.TryGetValue(caso.Id, out var lista) ? lista : [];

            var campos = 0;
            var verificados = 0;
            string? ultimaFirma = null;

            void Sumar(IReadOnlyList<ProcedenciaDeCampo> filas)
            {
                campos += filas.Count;
                foreach (var fila in filas)
                {
                    if (!fila.Verificado) continue;
                    verificados++;
                    if (fila.VerificadoEn is null) continue;
                    if (ultimaFirma is null || string.CompareOrdinal(fila.VerificadoEn, ultimaFirma) > 0)
                    {
                        ultimaFirma = fila.VerificadoEn;
                    }
                }
            }

            Sumar(procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id));
            foreach (var persona in suyas)
            {
                Sumar(procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id));
            }

            lectura.Add(new CasoConSuVerificacion(caso, suyas.Count, campos, verificados, ultimaFirma));
        }

        return lectura
            .OrderBy(c => c.Caso.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Caso.Id)
            .ToList();
    }
}
