using Fichas.App.Asignar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Revisar;

/// <summary>
/// Los CINCO tableros de la barra de Revisar.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Eran SEIS hasta el 2026-09-07</b>, y dos de ellos —<c>SinRevisar</c> («nadie ha
/// dicho nada todavía de este documento») e <c>Incompletas</c> («el Excel del compañero dijo
/// que no está completa»)— se juntan en <see cref="MeFalta"/>. El dueño colapsó las palabras a
/// dos, y con dos palabras aquellos dos tableros enseñaban exactamente lo mismo: una lista de
/// tarjetas que dicen «me falta». Dos pestañas con la misma palabra y distinta cuenta obligan a
/// abrir las dos para saber cuál mirar, que es el trabajo que este programa le quita.</para>
///
/// <para><b>Y por qué SOLO esos dos se juntan.</b> Los otros tres no contestan la pregunta del
/// estado: <see cref="SinAsignar"/> contesta QUIÉN lo lleva, <see cref="FechaPasada"/> contesta
/// CUÁNDO viaja, y <see cref="Todo"/> es el denominador que exige el criterio C1-1 —una cifra
/// sin su denominador no se puede comprobar—. Colapsarlos habría sido perder respuestas, no
/// palabras.</para>
///
/// <para>⚠️ <b>Lo que separaba a los dos que se juntan sigue en la base y en el detalle</b>: la
/// tarjeta que el compañero marcó «no completa» lleva su firma —«Marcada no completa por
/// Sandy»— y su motivo, y la que nadie tocó no lleva ninguna de las dos. La cuenta de cada uno
/// se puede seguir haciendo sobre <c>casos.estado_recomendacion</c>, que no se ha tocado.</para>
/// </remarks>
public enum FiltroDeTarjeta
{
    /// <summary>Todo lo que se cargo; es el denominador de los otros cuatro, no de la base.</summary>
    Todo = 0,

    /// <summary>Al dueno le queda algo que hacer con este documento.</summary>
    /// <remarks>
    /// Junta lo que nadie ha mirado y lo que el companero devolvio sin completar. El valor 1 es
    /// el que tenia <c>SinRevisar</c> a proposito: es el tablero con el que abre la pantalla, y
    /// asi un valor guardado en otro sitio no cambia de significado por debajo.
    /// </remarks>
    MeFalta = 1,

    /// <summary>No le queda nada: lo dio por completo el companero, o lo archivo el.</summary>
    Resuelto = 3,

    /// <summary>No lo lleva nadie ahora mismo.</summary>
    SinAsignar = 4,

    /// <summary>La fecha de viaje ya paso y hay que decidir: archivar o completar.</summary>
    FechaPasada = 5,
}

/// <summary>
/// La regla de la pantalla de Revisar, sin ventana: que tarjetas hay, en que tablero y cuantas.
/// </summary>
/// <remarks>
/// Nace de lo que el dueno puso en numeros el 2026-09-03: «imagina que tenga 3 000
/// formularios, me enviaron 300, no me puedo poner a ver uno a uno cual se completo». Por
/// eso la base se lee UNA vez y los seis tableros se cuentan sobre lo ya leido, igual que
/// <c>cuentas_de_los_filtros</c> en <c>datos/revision.py</c>, y no con seis consultas mas.
///
/// **Los archivados NO entran** salvo que se pidan. Del dueno, 2026-09-05: «cuando yo
/// archive, debe salir del sistema visible pero se queda como historico para los reportes».
/// Se pueden ver a proposito —la casilla «Ver los archivados»— porque para desarchivar algo
/// hay que poder verlo primero, y el mockup dibuja su tarjeta con su marca.
/// </remarks>
public sealed class TableroDeRevisar
{
    private readonly ICasos _casos;
    private readonly IAsignaciones _asignaciones;
    private readonly ICompaneros _companeros;
    private readonly IReloj _reloj;
    private List<TarjetaDeDocumento> _tarjetas = [];

    /// <summary>Ata el tablero a los tres repositorios y al reloj.</summary>
    public TableroDeRevisar(ICasos casos, IAsignaciones asignaciones, ICompaneros companeros, IReloj reloj)
    {
        _casos = casos;
        _asignaciones = asignaciones;
        _companeros = companeros;
        _reloj = reloj;
    }

    /// <summary>
    /// Con que tablero abre la pantalla de Revisar.
    /// </summary>
    /// <remarks>
    /// <para>Palabras del dueno, 2026-09-06: <i>«O solo que me aparezca en "por corregir"
    /// los documentos a corregir, no todos los documentos. Cuando doy click aparecen todos
    /// los PDF para revisar; solo los que necesitan revisión son los que deben ser
    /// revisados, no todos»</i>.</para>
    ///
    /// <para><b>Por que «Me falta» y no otro de los cinco, con sus palabras y no con un
    /// gusto.</b> El dice «los que NECESITAN revisión». De los cinco tableros, el unico que
    /// significa eso es <see cref="FiltroDeTarjeta.MeFalta"/>. Los otros tres contestan otra
    /// pregunta —lo que ya esta cerrado (Resuelto), quien lo lleva (SinAsignar) y cuando viaja
    /// (FechaPasada)—, y «Todo» es exactamente lo que el dijo que sobraba.</para>
    ///
    /// <para>⛔ <b>Hasta el 2026-09-07 este tablero se llamaba «Sin revisar»</b> y era uno de
    /// seis. No se anade ninguno nuevo: se juntan dos que con dos palabras decian lo mismo. La
    /// decision del 2026-09-06 —<i>«los seis tableros […] están bien; el defecto es el que
    /// sobra»</i>— hablaba de que NO se anadieran; juntar dos que ahora se leen igual va en la
    /// misma direccion, y «Todo» sigue estando a un clic.</para>
    ///
    /// <para>⚠️ <b>Y esta aqui, y no escrito a mano en la pagina, porque ahi no se podia
    /// probar.</b> Hasta hoy el tablero de partida era un valor en un campo privado de
    /// <c>PaginaDeRevisar.xaml.cs:32</c>, donde ninguna prueba llegaba: por eso pudo estar
    /// en «Todo» sin que nada se pusiera rojo. Lo vigila
    /// <c>PruebasDelTableroConElQueSeAbre</c>.</para>
    ///
    /// <para><b>Lo que hay que saber:</b> si un dia no queda nada sin revisar, la pantalla
    /// abre en un tablero con cero tarjetas. Es cierto y es la respuesta correcta —no hay
    /// nada que revisar—, y las pastillas de al lado siguen diciendo cuantas hay en cada uno
    /// de los otros cinco, asi que se ve de un vistazo donde esta el trabajo. NO se abre en
    /// otro tablero automaticamente: abrir unos dias en uno y otros dias en otro seria un
    /// programa que cambia de sitio solo, que es lo contrario de lo que el pidio.</para>
    /// </remarks>
    public static FiltroDeTarjeta ElTableroConElQueSeAbre => FiltroDeTarjeta.MeFalta;

    /// <summary>Lo que hay cargado ahora mismo, ya con el texto del buscador aplicado.</summary>
    public IReadOnlyList<TarjetaDeDocumento> Cargadas => _tarjetas;

    /// <summary>Cuantos documentos hay cargados en total, sin tablero.</summary>
    public int Total => _tarjetas.Count;

    /// <summary>
    /// Lee la base entera y compone las tarjetas. Una pasada, no una consulta por tarjeta.
    /// </summary>
    /// <param name="texto">Lo que hay escrito en el buscador; vacio para no filtrar.</param>
    /// <param name="conArchivados">
    /// Si entran tambien los archivados. Por defecto <b>no</b>, que es lo que el dueno pidio
    /// el 2026-09-05: «cuando yo archive, debe salir del sistema visible, pero se queda como
    /// historico para los reportes». Se pide en cierto solo para poder verlos y desarchivarlos
    /// a proposito, que es lo que hace la casilla «Ver los archivados».
    /// </param>
    /// <remarks>
    /// El valor por defecto estuvo en «si» hasta el 2026-09-05 porque seis pruebas de
    /// <c>Fichas.Pruebas.App/Asignar/</c> fijaban lo contrario y eran de otro terreno. Ya no:
    /// se movieron con este mismo cambio, y el defecto dice ahora lo que el dueno pidio.
    /// </remarks>
    public void Cargar(string texto = "", bool conArchivados = false)
    {
        var basico = ListaParaAsignar.SinFiltroDeEstadoNiArchivados with { IncluirArchivados = conArchivados };
        var filtro = string.IsNullOrWhiteSpace(texto) ? basico : basico with { Texto = texto };

        var casos = _casos.Listar(filtro, Pagina.Primera(int.MaxValue)).Elementos;
        var personas = _casos.ContarPersonasDe(casos.Select(c => c.Id).ToList());
        var nombres = NombresDeLosCompaneros();
        var portadores = QuienLlevaCada(nombres);
        var originales = LosOriginalesDeLosDuplicados(casos);
        var comparten = CuantosPorNumeroDeCaso(casos, filtro);
        var hoy = _reloj.Hoy();

        _tarjetas = casos
            .Select(caso => Componer(caso, personas, nombres, portadores, originales, comparten, hoy))
            .ToList();
    }

    /// <summary>
    /// Cuantos documentos hay con cada numero de caso, para la pista de grupo de viaje.
    /// </summary>
    /// <remarks>
    /// <para>Del dueno, 2026-09-05, peticion 11: «documentos que tienen el numero de caso
    /// iguales puede significar que viajaran en el mismo grupo». Se cuenta aqui y no en la
    /// tarjeta porque una tarjeta no puede saber cuantas hermanas tiene.</para>
    ///
    /// <para><b>Con el buscador puesto se vuelve a preguntar SIN el texto</b>, y esa es la
    /// mitad que evita un numero falso: buscando el nombre de una familia queda una tarjeta
    /// a la vista, y contar sobre lo filtrado diria «1 documento con este numero» de siete
    /// que hay. Sin texto no se pregunta nada: lo ya cargado es todo lo que hay.</para>
    ///
    /// <para>Los que no traen numero se quedan fuera a proposito: «sin numero de caso» no es
    /// un numero, y agruparlos diria que viajan juntos papeles que solo comparten que no se
    /// les pudo leer nada.</para>
    /// </remarks>
    private Dictionary<string, int> CuantosPorNumeroDeCaso(IReadOnlyList<Caso> cargados, FiltroDeCasos filtro)
    {
        var todos = string.IsNullOrWhiteSpace(filtro.Texto)
            ? cargados
            : _casos.Listar(filtro with { Texto = null }, Pagina.Primera(int.MaxValue)).Elementos;

        var cuantos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var caso in todos)
        {
            var clave = ClaveDelNumero(caso.NumeroCaso);
            if (clave.Length == 0) continue;
            cuantos[clave] = cuantos.GetValueOrDefault(clave) + 1;
        }
        return cuantos;
    }

    /// <summary>El numero de caso sin espacios de sobra; vacio si el papel no traia ninguno.</summary>
    private static string ClaveDelNumero(string? numeroCaso)
        => string.IsNullOrWhiteSpace(numeroCaso) ? string.Empty : numeroCaso.Trim();

    /// <summary>
    /// El caso del que repite cada duplicado, por su id.
    /// </summary>
    /// <remarks>
    /// <para>Casi siempre sale de lo YA leido: el original y su copia entran en la misma
    /// tanda. Solo se pregunta por los que faltan, que es lo que pasa cuando el buscador tiene
    /// texto puesto y el original queda fuera del filtro; y se pregunta una vez por id
    /// distinto, no una vez por tarjeta.</para>
    ///
    /// <para>Un id que ni asi se pueda leer se queda fuera del diccionario a proposito:
    /// <see cref="TarjetaDeDocumento.ComponerMarcaDeDuplicado"/> lo dice en la tarjeta en vez
    /// de callarse, porque un duplicado sin marca es justo el defecto que esto cierra.</para>
    /// </remarks>
    private Dictionary<long, Caso> LosOriginalesDeLosDuplicados(IReadOnlyList<Caso> casos)
    {
        var originales = new Dictionary<long, Caso>();
        var yaLeidos = casos.ToDictionary(c => c.Id);

        foreach (var id in casos.Where(c => c.DuplicadoDe is not null).Select(c => c.DuplicadoDe!.Value).Distinct())
        {
            if (yaLeidos.TryGetValue(id, out var enLaTanda)) originales[id] = enLaTanda;
            else if (_casos.Obtener(id) is Caso fuera) originales[id] = fuera;
        }

        return originales;
    }

    /// <summary>Cuantas tarjetas hay en cada tablero, para las cifras de las pastillas.</summary>
    public IReadOnlyDictionary<FiltroDeTarjeta, int> Cuentas()
        => Enum.GetValues<FiltroDeTarjeta>().ToDictionary(f => f, f => _tarjetas.Count(t => Entra(t, f)));

    /// <summary>Cuantas tarjetas hay en ese tablero.</summary>
    public int CuantasEn(FiltroDeTarjeta filtro) => _tarjetas.Count(t => Entra(t, filtro));

    /// <summary>Las tarjetas de ese tablero, en el trozo que se pida.</summary>
    public IReadOnlyList<TarjetaDeDocumento> Ver(FiltroDeTarjeta filtro, Pagina trozo)
    {
        var suyas = _tarjetas.Where(t => Entra(t, filtro)).ToList();
        var desde = Math.Clamp(trozo.Desde, 0, suyas.Count);
        var cuantas = trozo.Tamano <= 0 ? 0 : Math.Min(trozo.Tamano, suyas.Count - desde);
        return suyas.GetRange(desde, cuantas);
    }

    /// <summary>Todas las tarjetas de ese tablero, sin trocear. Es lo que se marca con Ctrl+A.</summary>
    public IReadOnlyList<TarjetaDeDocumento> Todas(FiltroDeTarjeta filtro)
        => _tarjetas.Where(t => Entra(t, filtro)).ToList();

    /// <summary>La tarjeta de un caso, o nula si no esta cargada.</summary>
    public TarjetaDeDocumento? De(long casoId) => _tarjetas.FirstOrDefault(t => t.CasoId == casoId);

    /// <summary>El nombre en espanol de cada tablero, tal como se pinta en su pastilla.</summary>
    public static string NombreDe(FiltroDeTarjeta filtro) => filtro switch
    {
        FiltroDeTarjeta.MeFalta => DosEstados.MeFaltaEnCabecera,
        FiltroDeTarjeta.Resuelto => DosEstados.ResueltoEnCabecera,
        FiltroDeTarjeta.SinAsignar => "Sin asignar",
        FiltroDeTarjeta.FechaPasada => "Fecha pasada",
        _ => "Todo",
    };

    /// <summary>
    /// Si esa tarjeta entra en ese tablero. Uno solo, y «Todo» no filtra nada.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>«Fecha pasada» deja fuera a los archivados</b>, y con los archivados a la vista se
    /// nota. Ese tablero es el de los que hay que decidir —«esta fecha ya paso, revisar, ¿quieres
    /// archivar o completar?»—, y un archivado ya esta decidido. La decision del dueno del
    /// 2026-09-06 lo dice con todas las letras: un archivado no se retiene «en ninguna lista, en
    /// ningun contador ni en ningun aviso». Sin esta condicion, encender «Ver los archivados»
    /// sumaria a esa pastilla documentos cuya pregunta ya esta contestada.
    /// </remarks>
    private static bool Entra(TarjetaDeDocumento tarjeta, FiltroDeTarjeta filtro) => filtro switch
    {
        // ⚠️ Los dos primeros salen de la MISMA lectura y no de dos condiciones sueltas: asi no
        // pueden separarse de la palabra que la tarjeta pinta, y «Me falta» siempre trae
        // exactamente las tarjetas que dicen «me falta». Con dos condiciones escritas aparte,
        // una tarjeta archivada podia quedarse fuera de los dos tableros y desaparecer de
        // «Todo» sin que nadie lo notara.
        FiltroDeTarjeta.MeFalta => tarjeta.SeVeMeFalta,
        FiltroDeTarjeta.Resuelto => tarjeta.SeVeResuelto,
        FiltroDeTarjeta.SinAsignar => tarjeta.SinAsignar,
        FiltroDeTarjeta.FechaPasada => tarjeta.FechaYaPasada && !tarjeta.Archivado,
        _ => true,
    };

    /// <summary>Compone una tarjeta con lo ya leido; no vuelve a preguntar por documento.</summary>
    private static TarjetaDeDocumento Componer(
        Caso caso,
        IReadOnlyDictionary<long, int> personas,
        IReadOnlyDictionary<long, string> nombres,
        IReadOnlyDictionary<long, string> portadores,
        IReadOnlyDictionary<long, Caso> originales,
        IReadOnlyDictionary<string, int> comparten,
        string hoy)
    {
        var lleva = portadores.TryGetValue(caso.Id, out var quien) ? quien : null;
        return new TarjetaDeDocumento
        {
            CuantosCompartenElNumero = comparten.TryGetValue(ClaveDelNumero(caso.NumeroCaso), out var cuantos)
                ? cuantos
                : 1,
            CasoId = caso.Id,
            NumeroDeCaso = string.IsNullOrWhiteSpace(caso.NumeroCaso) ? RenglonParaAsignar.SinNumero : caso.NumeroCaso,
            TieneNumeroDeCaso = !string.IsNullOrWhiteSpace(caso.NumeroCaso),
            Archivo = TarjetaDeDocumento.NombreDelArchivo(caso.RutaPdf),
            Hoja = caso.PaginaPdf is int hoja ? $"hoja {hoja}" : string.Empty,
            Personas = personas.TryGetValue(caso.Id, out var cuantas) ? cuantas : 0,
            FechaDeViaje = string.IsNullOrWhiteSpace(caso.FechaViaje) ? RenglonParaAsignar.SinFecha : caso.FechaViaje,
            FechaDeViajeIso = caso.FechaViaje?.Trim() ?? string.Empty,
            FechaYaPasada = EsFechaPasada(caso.FechaViaje, hoy),
            Unidad = string.IsNullOrWhiteSpace(caso.UnidadNombre) ? "sin unidad" : caso.UnidadNombre,
            UnidadNumero = caso.UnidadNumero?.Trim() ?? string.Empty,
            RutaDelPdf = caso.RutaPdf ?? string.Empty,
            Estado = caso.Estado,
            Motivo = caso.Motivo,
            MotivoQueDijoElCompanero = caso.MotivoQueDijoElCompanero,
            Archivado = caso.Archivado,
            FechaDeArchivado = caso.FechaArchivado ?? string.Empty,
            EsDuplicado = caso.DuplicadoDe is not null,
            MarcaDeDuplicado = caso.DuplicadoDe is long deQuien
                ? TarjetaDeDocumento.ComponerMarcaDeDuplicado(originales.GetValueOrDefault(deQuien))
                : string.Empty,
            SinAsignar = lleva is null,
            AsignadoA = lleva ?? RenglonParaAsignar.SinAsignar,
            Firma = TarjetaDeDocumento.ComponerFirma(caso, nombres),
        };
    }

    /// <summary>Si esa fecha de viaje ya paso. Las fechas son ISO-8601, asi que se comparan como texto.</summary>
    private static bool EsFechaPasada(string? fechaViaje, string hoy)
        => !string.IsNullOrWhiteSpace(fechaViaje) && string.CompareOrdinal(fechaViaje, hoy) < 0;

    /// <summary>El nombre de cada companero por su id, activos y desactivados.</summary>
    private Dictionary<long, string> NombresDeLosCompaneros()
        => _companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue))
            .Elementos.ToDictionary(c => c.Id, c => c.Nombre);

    /// <summary>Quien lleva vivo cada caso, en UNA pasada por las asignaciones vivas.</summary>
    private Dictionary<long, string> QuienLlevaCada(IReadOnlyDictionary<long, string> nombres)
    {
        var portadores = new Dictionary<long, string>();
        var vivas = _asignaciones.Listar(FiltroDeAsignaciones.Activas, Pagina.Primera(int.MaxValue)).Elementos;
        foreach (var asignacion in vivas)
        {
            var nombre = nombres.TryGetValue(asignacion.CompaneroId, out var n) ? n : "compañero borrado";
            portadores[asignacion.CasoId] = portadores.TryGetValue(asignacion.CasoId, out var ya)
                ? $"{ya}, {nombre}"
                : nombre;
        }
        return portadores;
    }
}
