using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Asignar;

/// <summary>
/// La regla de la pantalla de Asignar, sin ventana: que casos se ofrecen y a quien.
/// </summary>
/// <remarks>
/// Requisito 8 del dueno, con sus palabras: «el programa debe ser libre, no me deja
/// asignar a los agentes». Lo medido en el programa viejo el 2026-09-04
/// (<c>interfaz/asignacion.py</c>, lineas 193-209): <c>_casos_disponibles</c> devuelve
/// <c>[]</c> si no hay companero elegido, y con companero solo sirve los pendientes de
/// verificar mas los que viajan pronto. Un caso ya verificado, o archivado, no aparece.
///
/// Aqui eso no se puede repetir por como esta construido, no por disciplina:
/// **el companero no entra en la consulta de casos**. No hay parametro que lo meta. La
/// lista de casos y la lista de destinos son dos preguntas distintas y se leen aparte.
///
/// Lo unico que esta pantalla si deja fuera es el **archivado**, y no por su estado de
/// recomendacion sino porque el dueno lo saco del trabajo del dia el 2026-09-05: «cuando yo
/// archive, debe salir del sistema visible pero se queda como historico para los reportes».
/// No se esconde callando: <see cref="CuantosHayArchivados"/> los cuenta y la pantalla los
/// dice.
/// </remarks>
public sealed class ListaParaAsignar
{
    private readonly ICasos _casos;
    private readonly IAsignaciones _asignaciones;
    private readonly ICompaneros _companeros;
    private readonly IPersonas _personas;

    /// <summary>Ata la lista a los cuatro repositorios que necesita.</summary>
    public ListaParaAsignar(ICasos casos, IAsignaciones asignaciones, ICompaneros companeros, IPersonas personas)
    {
        _casos = casos;
        _asignaciones = asignaciones;
        _companeros = companeros;
        _personas = personas;
    }

    /// <summary>
    /// El filtro con el que esta pantalla lee los casos, y el unico que usa.
    /// </summary>
    /// <remarks>
    /// <para>Todos los campos van en su valor neutro a proposito: cada campo que se encendiera
    /// aqui seria un caso escondido por su <b>estado de recomendacion</b>, que es justo el
    /// defecto que el dueno pidio quitar. Hay una prueba que lo comprueba campo por campo.</para>
    ///
    /// <para><b>Los archivados quedan fuera</b>, que es el otro eje y no ese. Del dueno,
    /// 2026-09-05: «cuando yo archive, debe salir del sistema visible pero se queda como
    /// historico para los reportes». Hasta hoy esto llevaba <c>IncluirArchivados: true</c> y
    /// un caso archivado se seguia ofreciendo para asignar: se le podia dar trabajo a un
    /// companero sobre algo que Miguel ya habia cerrado.</para>
    ///
    /// <para>Los archivados no se esconden en silencio: <see cref="CuantosHayArchivados"/> los
    /// cuenta para poder decirlos, igual que se hace con los companeros desactivados.</para>
    /// </remarks>
    public static readonly FiltroDeCasos SinFiltroDeEstadoNiArchivados = new(IncluirArchivados: false);

    /// <summary>Cuantos casos se pueden ofrecer: los que no estan archivados. Es el denominador dicho.</summary>
    public int CuantosSePuedenOfrecer() => _casos.Contar(SinFiltroDeEstadoNiArchivados);

    /// <summary>
    /// Cuantos casos hay archivados, para poder decirlos en vez de esconderlos.
    /// </summary>
    /// <remarks>
    /// Sin esta cifra, un Miguel que archivo 40 casos veria la lista encoger sin explicacion y
    /// no tendria como distinguir «se archivaron» de «se perdieron». Se desarchivan desde la
    /// pantalla de Revisar, con la casilla «Ver los archivados» puesta.
    /// </remarks>
    public int CuantosHayArchivados()
        => _casos.Contar(SinFiltroDeEstadoNiArchivados with { IncluirArchivados = true })
           - CuantosSePuedenOfrecer();

    /// <summary>Cuantos casos ofrece la pantalla con lo que haya escrito en el buscador.</summary>
    public int CuantosSeOfrecen(string texto = "") => _casos.Contar(ConTexto(texto));

    /// <summary>Los companeros a los que se puede dar trabajo: los ACTIVOS, y esa es la unica condicion.</summary>
    public IReadOnlyList<Companero> Destinos() => _companeros.Activos();

    /// <summary>Cuantos companeros hay desactivados, para poder decirlo en vez de esconderlo.</summary>
    public int CuantosDestinosDesactivados()
        => _companeros.Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue))
            .Elementos.Count(c => !c.Activo);

    /// <summary>
    /// Los casos que se ofrecen, en el trozo que se pida. Todos los que no estan archivados:
    /// no hay filtro de estado y no hace falta haber elegido companero (criterios C5-4 y C5-5).
    /// </summary>
    public PaginaDe<RenglonParaAsignar> Ofrecer(Pagina trozo, string texto = "")
    {
        var pagina = _casos.Listar(ConTexto(texto), trozo);
        if (pagina.Elementos.Count == 0) return PaginaDe<RenglonParaAsignar>.Vacia(trozo);

        var ids = pagina.Elementos.Select(c => c.Id).ToList();
        var personas = _casos.ContarPersonasDe(ids);
        var quienViaja = QuienViajaEnCada(ids);
        var portadores = QuienLlevaCada(ids);

        var renglones = pagina.Elementos
            .Select(caso => Componer(caso, personas, quienViaja, portadores))
            .ToList();
        return new PaginaDe<RenglonParaAsignar>(renglones, trozo, pagina.TotalDisponible);
    }

    /// <summary>Compone el renglon de un caso con lo ya leido; no vuelve a preguntar por caso.</summary>
    private static RenglonParaAsignar Componer(
        Caso caso,
        IReadOnlyDictionary<long, int> personas,
        IReadOnlyDictionary<long, List<string>> quienViaja,
        IReadOnlyDictionary<long, string> portadores)
    {
        IReadOnlyList<string> nombres = quienViaja.TryGetValue(caso.Id, out var suyos) ? suyos : [];
        return new()
        {
            CasoId = caso.Id,
            NumeroDeCaso = string.IsNullOrWhiteSpace(caso.NumeroCaso) ? RenglonParaAsignar.SinNumero : caso.NumeroCaso,
            TieneNumeroDeCaso = !string.IsNullOrWhiteSpace(caso.NumeroCaso),
            Unidad = string.IsNullOrWhiteSpace(caso.UnidadNombre) ? "sin unidad" : caso.UnidadNombre,
            FechaDeViaje = string.IsNullOrWhiteSpace(caso.FechaViaje) ? RenglonParaAsignar.SinFecha : caso.FechaViaje,
            Personas = personas.TryGetValue(caso.Id, out var cuantas) ? cuantas : 0,
            QuienViaja = RenglonParaAsignar.ComoSeDiceQuienViaja(nombres),
            QuienesViajan = RenglonParaAsignar.ComoSeDicenTodosLosQueViajan(nombres),
            PalabraDelEstado = RenglonParaAsignar.PalabraDe(caso.Estado),
            AsignadoA = portadores.TryGetValue(caso.Id, out var quien) ? quien : RenglonParaAsignar.SinAsignar,
        };
    }

    /// <summary>
    /// Los nombres de quienes viajan en cada caso del trozo, en UNA pasada por las personas.
    /// </summary>
    /// <remarks>
    /// <para><b>De una vez, no una vez por fila.</b> Es la misma forma que ya tenia
    /// <c>ICasos.ContarPersonasDe</c> para las cuentas y la misma que usa
    /// <c>LectorDelInicio.AgruparLasPersonas</c>: con 3 000 documentos, preguntar caso por caso
    /// son 3 000 consultas al recorrer la lista entera. Lo vigila
    /// <c>PruebasDeQuienViaja.LosNombresDeUnaPantalladaSePidenDeUnaVezYNoUnaVezPorFila</c>
    /// contando las preguntas, porque el cronometro no caza esto: 60 consultas pequenas caben
    /// de sobra en el techo y el defecto solo aparece con la base llena.</para>
    ///
    /// <para>⚠️ <b>Se lee de mas y se dice.</b> Se piden TODAS las personas y se descartan las
    /// que no son del trozo, porque en <c>IPersonas</c> no hay ninguna forma de pedir las de una
    /// lista de casos —el filtro solo admite UN caso— y ese archivo esta congelado. Lo medido con
    /// 3 000 documentos esta en la entrega del 2026-09-06; el dia que se descongele el contrato,
    /// esto son dos lineas.</para>
    ///
    /// <para>El orden es el del formulario, que es el que trae <c>IPersonas.Listar</c>: por caso,
    /// por fila y por id. Asi «el primero» es siempre el mismo y es quien encabeza el papel.</para>
    /// </remarks>
    private Dictionary<long, List<string>> QuienViajaEnCada(IReadOnlyList<long> casoIds)
    {
        var queremos = casoIds.ToHashSet();
        var porCaso = new Dictionary<long, List<string>>(casoIds.Count);

        var cuantas = _personas.Contar(FiltroDePersonas.Todo);
        if (cuantas == 0) return porCaso;

        foreach (var persona in _personas.Listar(FiltroDePersonas.Todo, new Pagina(0, cuantas)).Elementos)
        {
            if (!queremos.Contains(persona.CasoId)) continue;
            if (!porCaso.TryGetValue(persona.CasoId, out var suyas))
            {
                suyas = [];
                porCaso[persona.CasoId] = suyas;
            }

            suyas.Add(RenglonParaAsignar.NombreDeQuienViaja(persona.Nombre));
        }

        return porCaso;
    }

    /// <summary>Quien lleva vivo cada caso del trozo, en UNA pasada por las asignaciones vivas.</summary>
    private Dictionary<long, string> QuienLlevaCada(IReadOnlyList<long> casoIds)
    {
        var pedidos = casoIds.ToHashSet();
        var nombres = _companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue))
            .Elementos.ToDictionary(c => c.Id, c => c.Nombre);

        var portadores = new Dictionary<long, string>();
        var vivas = _asignaciones.Listar(FiltroDeAsignaciones.Activas, Pagina.Primera(int.MaxValue)).Elementos;
        foreach (var asignacion in vivas)
        {
            if (!pedidos.Contains(asignacion.CasoId)) continue;
            var nombre = nombres.TryGetValue(asignacion.CompaneroId, out var n) ? n : "compañero borrado";
            // Dos companeros vivos sobre el mismo caso es posible hoy: se dicen los dos
            // en vez de ensenar solo uno y hacer creer que es el unico.
            portadores[asignacion.CasoId] = portadores.TryGetValue(asignacion.CasoId, out var ya)
                ? $"{ya}, {nombre}"
                : nombre;
        }
        return portadores;
    }

    /// <summary>El filtro de la pantalla con el texto del buscador puesto, y nada mas.</summary>
    private static FiltroDeCasos ConTexto(string texto)
        => string.IsNullOrWhiteSpace(texto)
            ? SinFiltroDeEstadoNiArchivados
            : SinFiltroDeEstadoNiArchivados with { Texto = texto };
}
