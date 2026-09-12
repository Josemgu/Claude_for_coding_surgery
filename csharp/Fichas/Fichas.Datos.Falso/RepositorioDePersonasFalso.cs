using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Las personas inventadas. Cumple <see cref="IPersonas"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDePersonasFalso : IPersonas
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public RepositorioDePersonasFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de personas que cumplen el filtro, con el total detras.</summary>
    /// <param name="filtro">Qué personas entran; ver <see cref="Filtrar"/>.</param>
    /// <param name="trozo">Qué página se pide.</param>
    public PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Cuenta cuantas personas cumplen el filtro, sin traerlas.</summary>
    /// <param name="filtro">Qué personas entran.</param>
    public int Contar(FiltroDePersonas filtro) => Filtrar(filtro).Count;

    /// <summary>Devuelve una persona por su id, o nulo si no esta.</summary>
    /// <param name="id">El número interno.</param>
    public Persona? Obtener(long id) => _almacen.Personas.TryGetValue(id, out var persona) ? persona : null;

    /// <summary>Devuelve todas las personas de un caso, en el orden del formulario.</summary>
    /// <param name="casoId">El caso; uno que no existe da la lista vacía.</param>
    public IReadOnlyList<Persona> DeCaso(long casoId) => _almacen.PersonasDe(casoId);

    /// <summary>Guarda una persona nueva o cambia una existente; un MRN corto entra y sale avisado.</summary>
    /// <param name="persona">La persona; sin nombre ni MRN no entra, como en la base de verdad.</param>
    public ResultadoDeEscritura Guardar(Persona persona)
    {
        // Una fila sin nombre Y sin MRN no es una persona: el formulario trae seis
        // renglones y casi nunca vienen todos llenos. El esquema lo ata con
        // `CHECK (nombre IS NOT NULL OR mrn IS NOT NULL)` desde la version 1, asi que el
        // de verdad la rechaza; hasta el 2026-09-05 aqui entraba, y una pantalla probada
        // contra este doble creeria que guardo un renglon en blanco que en la maquina
        // del dueno no habria entrado.
        if (string.IsNullOrWhiteSpace(persona.Nombre) && string.IsNullOrWhiteSpace(persona.Mrn))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Una persona necesita al menos nombre o cedula.",
                nameof(Persona.Nombre),
                "El formulario trae seis renglones y casi nunca vienen todos llenos; " +
                "un renglon sin nada no se guarda."));
        }

        var avisos = RevisarSinImpedir(persona);
        var id = persona.Id == 0 ? _almacen.SiguienteId() : persona.Id;
        _almacen.Personas[id] = persona with { Id = id };
        return new ResultadoDeEscritura(true, id, avisos);
    }

    /// <summary>
    /// Lo que se escribe en <c>pasos_origen</c> cuando las seis llegan por el Excel del
    /// companero.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es el mismo texto que <c>RepositorioDePersonas.OrigenDelExcelDeVuelta</c>, y esta
    /// escrito dos veces porque este proyecto no depende de <c>Fichas.Datos</c>. Que los dos
    /// no se separen lo comprueba
    /// <c>PruebaDeQuienEscribioLasSeis.ElFalsoDejaLaMismaFirmaQueElDeVerdadAlAplicarUnExcel</c>,
    /// comparando los dos valores observados y no una constante consigo misma.
    /// </remarks>
    public const string OrigenDelExcelDeVuelta = "su Excel de vuelta";

    /// <summary>Anota lo que el companero propuso sobre una persona, con su firma; no verifica nada.</summary>
    /// <remarks>
    /// ⛔ <b>Quien escribe los seis <c>Paso*</c> queda como que los contesto</b>, igual que en
    /// el de verdad: dejar la firma apuntando a quien contesto antes hace que la base diga que
    /// contesto alguien que no fue. Y las seis EN BLANCO no cuentan: un Excel que solo dice el
    /// estado no es trabajo suyo sobre las preguntas, y ahi no se toca la firma que hubiera.
    /// </remarks>
    /// <param name="personaId">La persona; si no existe, no se escribe y se dice.</param>
    /// <param name="propuesta">De dónde se copian el estado propuesto, la nota, los seis pasos y «llamó al líder».</param>
    /// <param name="companeroId">Quién lo propuso; queda como firma de los pasos si contestó alguno.</param>
    public ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(propuesta);

        if (!_almacen.Personas.TryGetValue(personaId, out var persona))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ninguna persona con el numero interno {personaId}."));

        var cuando = _almacen.Reloj.Ahora();

        if (ContestaAlgunaDeLasSeis(propuesta))
        {
            _almacen.FirmasDeLosPasos[personaId] =
                new FirmaDeLosPasos(companeroId, cuando, OrigenDelExcelDeVuelta);
        }

        // Escribe la propuesta y su firma. NO toca procedencia_campo.verificado: eso solo lo
        // pone Miguel con el boton (regla permanente 5, ARQUITECTURA §2.3).
        _almacen.Personas[personaId] = persona with
        {
            EstadoPropuesto = propuesta.EstadoPropuesto,
            NotaCompanero = propuesta.NotaCompanero,
            PasoPreparacion = propuesta.PasoPreparacion,
            PasoInformacion = propuesta.PasoInformacion,
            PasoCitaDelTemplo = propuesta.PasoCitaDelTemplo,
            PasoAccionesRequeridas = propuesta.PasoAccionesRequeridas,
            PasoEntrevistas = propuesta.PasoEntrevistas,
            PasoListoParaElTemplo = propuesta.PasoListoParaElTemplo,
            LlamoAlLider = propuesta.LlamoAlLider,
            PropuestoPor = companeroId,
            PropuestoEn = cuando,
        };
        return ResultadoDeEscritura.Bien(personaId);
    }

    /// <summary>Si el Excel del companero contesto alguna de las seis; <c>LlamoAlLider</c> no cuenta.</summary>
    /// <param name="propuesta">Lo que trajo el Excel.</param>
    private static bool ContestaAlgunaDeLasSeis(Persona propuesta)
        => propuesta.PasoPreparacion is not null
        || propuesta.PasoInformacion is not null
        || propuesta.PasoCitaDelTemplo is not null
        || propuesta.PasoAccionesRequeridas is not null
        || propuesta.PasoEntrevistas is not null
        || propuesta.PasoListoParaElTemplo is not null;

    /// <summary>
    /// Contesta las seis preguntas del sistema del lider de UNA persona y firma quien,
    /// cuando y desde donde. No firma ningun campo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Escribe las seis y la firma, y ni una cosa mas. No toca <c>EstadoPropuesto</c>,
    /// <c>NotaCompanero</c>, <c>PropuestoPor</c>, <c>PropuestoEn</c> ni <c>LlamoAlLider</c>:
    /// eso es del Excel del companero, lleva su nombre y no se pisa. La paridad con el de
    /// verdad la comprueba <c>PruebaDeContestarLosPasos</c> corriendo la misma operacion
    /// contra los dos.
    /// </remarks>
    /// <param name="personaId">La persona; si no existe, no se escribe y se dice.</param>
    /// <param name="respuesta">Las seis respuestas.</param>
    /// <param name="companeroId">Quién contestó.</param>
    /// <param name="origen">Desde dónde; en blanco se guarda como nulo.</param>
    public ResultadoDeEscritura ResponderLosPasos(
        long personaId, RespuestaALosPasos respuesta, long companeroId, string origen)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        if (!_almacen.Personas.TryGetValue(personaId, out var persona))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ninguna persona con el numero interno {personaId}."));

        _almacen.Personas[personaId] = persona with
        {
            PasoPreparacion = respuesta.Preparacion,
            PasoInformacion = respuesta.Informacion,
            PasoCitaDelTemplo = respuesta.CitaDelTemplo,
            PasoAccionesRequeridas = respuesta.AccionesRequeridas,
            PasoEntrevistas = respuesta.Entrevistas,
            PasoListoParaElTemplo = respuesta.ListoParaElTemplo,
        };

        _almacen.FirmasDeLosPasos[personaId] = new FirmaDeLosPasos(
            companeroId, _almacen.Reloj.Ahora(), string.IsNullOrWhiteSpace(origen) ? null : origen);

        return ResultadoDeEscritura.Bien(personaId);
    }

    /// <summary>La firma de las seis preguntas de cada persona de un caso, por su id.</summary>
    /// <remarks>
    /// Toda persona del caso sale, tambien la que nadie contesto: sale
    /// <see cref="FirmaDeLosPasos.SinFirmar"/> en vez de faltar, igual que en el de verdad.
    /// </remarks>
    /// <param name="casoId">El caso cuyas personas se miran.</param>
    public IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId)
        => _almacen.PersonasDe(casoId).ToDictionary(
            persona => persona.Id,
            persona => _almacen.FirmasDeLosPasos.TryGetValue(persona.Id, out var firma)
                ? firma
                : FirmaDeLosPasos.SinFirmar);

    /// <summary>Mira la persona y devuelve lo que hay que senalar; NUNCA impide guardar (requisito 9).</summary>
    /// <param name="persona">La persona que se va a guardar.</param>
    private static List<Aviso> RevisarSinImpedir(Persona persona)
    {
        var avisos = new List<Aviso>();

        if (persona.Nombre is null && persona.Mrn is null)
        {
            avisos.Add(Aviso.Advierte(
                "Esta persona no tiene ni nombre ni MRN.",
                nameof(Persona.Nombre),
                "El esquema de la base no admite una fila sin las dos cosas: al pasar a la base real " +
                "esta fila se quedara fuera. Escribe al menos una de las dos."));
        }
        else if (persona.Mrn is { Length: > 0 } mrn && !TieneFormaDeMrn(mrn))
        {
            avisos.Add(Aviso.Advierte(
                "El MRN no tiene la forma 000-0000-0000.",
                nameof(Persona.Mrn),
                $"Se guardo «{mrn}» tal como se leyo. El MRN es la mitad de la clave con la que se " +
                "casan las hojas que devuelven los companeros: si esta mal, esa fila volvera descartada."));
        }

        return avisos;
    }

    /// <summary>
    /// Patron 3-4-4 con guiones, y el ULTIMO caracter puede ser una letra.
    ///
    /// El dueno lo dijo con estas palabras el 2026-09-04: «muchas cedulas de miembro
    /// tienen una A u otra letra al final». Se comprobo mirando el papel: se recorto
    /// del PDF la banda de la cedula en dos escaneos reales suyos y las dos terminan
    /// en letra, impresa y nitida. Exigir once digitos costaba 2 de cada 7 cedulas de
    /// sus documentos: el OCR las leia bien y esta regla las tiraba.
    /// </summary>
    /// <param name="mrn">El MRN tal como se leyó.</param>
    private static bool TieneFormaDeMrn(string mrn)
        => mrn.Length == 13 && mrn[3] == '-' && mrn[8] == '-'
           && mrn.Where((_, i) => i is not 3 and not 8 and not 12).All(char.IsAsciiDigit)
           && (char.IsAsciiDigit(mrn[12]) || char.IsAsciiLetter(mrn[12]));

    /// <summary>Aplica los filtros simples y devuelve la lista ordenada por caso y fila.</summary>
    /// <param name="filtro">Los campos del filtro: un solo caso, sin MRN, o un texto en nombre o MRN.</param>
    private List<Persona> Filtrar(FiltroDePersonas filtro)
    {
        IEnumerable<Persona> personas = _almacen.Personas.Values;

        if (filtro.CasoId is long caso) personas = personas.Where(p => p.CasoId == caso);
        if (filtro.SinMrn) personas = personas.Where(p => p.Mrn is null);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            personas = personas.Where(p => Trozos.Contiene(p.Nombre, filtro.Texto)
                                           || Trozos.Contiene(p.Mrn, filtro.Texto));
        }

        return personas
            .OrderBy(p => p.CasoId)
            .ThenBy(p => p.FilaFormulario ?? int.MaxValue)
            .ThenBy(p => p.Id)
            .ToList();
    }
}
