using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Los casos inventados. Cumple <see cref="ICasos"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeCasosFalso : ICasos
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public RepositorioDeCasosFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de casos que cumplen el filtro, con el total detras.</summary>
    /// <remarks>
    /// Igual que el de verdad en qué filtra y en qué orden devuelve; se diferencia en que
    /// filtra en memoria sobre el diccionario y ordena las fechas como texto ISO.
    /// </remarks>
    /// <param name="filtro">Qué casos entran; ver <see cref="Filtrar"/> para cada campo.</param>
    /// <param name="trozo">Qué página se pide.</param>
    public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Cuenta cuantos casos cumplen el filtro, sin traerlos.</summary>
    /// <param name="filtro">Qué casos entran.</param>
    public int Contar(FiltroDeCasos filtro) => Filtrar(filtro).Count;

    /// <summary>Devuelve un caso por su id, o nulo si no esta.</summary>
    /// <param name="id">El número interno.</param>
    public Caso? Obtener(long id) => _almacen.Casos.TryGetValue(id, out var caso) ? caso : null;

    /// <summary>Cuenta las personas de cada caso de la lista que se le pase, en una sola pasada.</summary>
    /// <param name="casoIds">Los casos; cada uno sale en el resultado aunque tenga cero personas.</param>
    public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds)
    {
        var pedidos = casoIds.ToHashSet();
        var cuenta = pedidos.ToDictionary(id => id, _ => 0);
        foreach (var persona in _almacen.Personas.Values)
        {
            if (pedidos.Contains(persona.CasoId)) cuenta[persona.CasoId]++;
        }
        return cuenta;
    }

    /// <summary>Guarda un caso nuevo o cambia uno existente; un numero raro entra y sale avisado.</summary>
    /// <param name="caso">El caso; con <c>Id</c> 0 se le da uno nuevo, si no se sustituye el que tenga.</param>
    public ResultadoDeEscritura Guardar(Caso caso)
    {
        var avisos = RevisarSinImpedir(caso);
        var id = caso.Id == 0 ? _almacen.SiguienteId() : caso.Id;
        _almacen.Casos[id] = caso with { Id = id };
        return new ResultadoDeEscritura(true, id, avisos);
    }

    /// <summary>Escribe el estado de la recomendacion con quien lo marca y de donde vino la marca.</summary>
    /// <remarks>
    /// ⚠️ Escribe SOLO el estado vigente, igual que <c>RepositorioDeCasos</c>. Hasta el
    /// 2026-09-05 escribia tambien <c>EstadoDelCompanero</c>, <c>_por</c> y <c>_en</c>, y
    /// el de verdad NO las escribia por ningun camino: las pruebas que corren sobre este
    /// doble pasaban en verde sobre un hueco real, y en la base del dueno esas tres
    /// columnas llevaban vacias desde la migracion 14.
    ///
    /// Escribir las dos mitades aqui era ademas lo INCORRECTO: la migracion 14 las
    /// desdoblo para que la correccion de Miguel no pisara el nombre del companero. Lo
    /// que dijo una hoja se escribe con <see cref="MarcarEstadoDelCompanero"/>.
    /// </remarks>
    /// <param name="casoId">El caso; si no existe, no se escribe y se dice.</param>
    /// <param name="estado">El estado vigente que se escribe.</param>
    /// <param name="companeroId">Quién lo marca.</param>
    /// <param name="origen">De dónde vino la marca, tal como se guarda en <c>estado_marcado_origen</c>.</param>
    public ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
    {
        if (!_almacen.Casos.TryGetValue(casoId, out var caso))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun caso con el numero interno {casoId}."));

        _almacen.Casos[casoId] = caso with
        {
            EstadoRecomendacion = Caso.EscribirEstado(estado),
            EstadoMarcadoPor = companeroId,
            EstadoMarcadoEn = _almacen.Reloj.Ahora(),
            EstadoMarcadoOrigen = origen,
        };
        return ResultadoDeEscritura.Bien(casoId);
    }

    /// <summary>Escribe lo que dijo la hoja de un companero: el estado vigente Y su registro.</summary>
    /// <param name="casoId">El caso; si no existe, no se escribe y se dice.</param>
    /// <param name="estado">Lo que dijo la hoja.</param>
    /// <param name="motivo">Por qué no está completa, si no lo está; va al motivo del compañero y nunca al vigente.</param>
    /// <param name="companeroId">De quién era la hoja.</param>
    /// <param name="origen">De dónde vino la marca.</param>
    public ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId,
        EstadoDeRecomendacion estado,
        MotivoDeNoCompletar motivo,
        long companeroId,
        string origen)
    {
        if (!_almacen.Casos.TryGetValue(casoId, out var caso))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun caso con el numero interno {casoId}."));

        var texto = Caso.EscribirEstado(estado);
        var ahora = _almacen.Reloj.Ahora();

        // Sin tocar `MotivoNoCompleta`: criterio C14-4, lo que escribe la hoja va al
        // motivo del companero y NUNCA al vigente.
        _almacen.Casos[casoId] = caso with
        {
            EstadoRecomendacion = texto,
            EstadoMarcadoPor = companeroId,
            EstadoMarcadoEn = ahora,
            EstadoMarcadoOrigen = origen,
            EstadoDelCompanero = texto,
            EstadoDelCompaneroPor = companeroId,
            EstadoDelCompaneroEn = ahora,
            MotivoDelCompanero = Caso.EscribirMotivo(motivo),
        };
        return ResultadoDeEscritura.Bien(casoId);
    }

    /// <summary>Archiva o desarchiva un caso; archivar exige fecha y desarchivar la quita.</summary>
    /// <param name="casoId">El caso; si no existe, no se escribe y se dice.</param>
    /// <param name="archivado">Verdadero para archivar, falso para devolverlo al trabajo.</param>
    /// <param name="fechaDeArchivado">La fecha ISO; si va en blanco al archivar, se pone la de hoy del reloj.</param>
    public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
    {
        if (!_almacen.Casos.TryGetValue(casoId, out var caso))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun caso con el numero interno {casoId}."));

        // El esquema ata archivado con su fecha: o las dos o ninguna (ARQUITECTURA §2.2).
        var fecha = archivado
            ? (string.IsNullOrWhiteSpace(fechaDeArchivado) ? _almacen.Reloj.Hoy() : fechaDeArchivado)
            : null;
        _almacen.Casos[casoId] = caso with { Archivado = archivado, FechaArchivado = fecha };
        return ResultadoDeEscritura.Bien(casoId);
    }

    /// <summary>Mira el caso y devuelve lo que hay que senalar; NUNCA impide guardar (requisito 9).</summary>
    /// <param name="caso">El caso que se va a guardar.</param>
    private static List<Aviso> RevisarSinImpedir(Caso caso)
    {
        var avisos = new List<Aviso>();

        if (caso.NumeroCaso is { Length: > 0 } numero && !TieneFormaDeNumeroDeCaso(numero))
        {
            avisos.Add(Aviso.Advierte(
                "El numero de caso no tiene la forma de cuatro letras y cuatro digitos.",
                nameof(Caso.NumeroCaso),
                $"Se guardo «{numero}» tal como se leyo. El programa no lo cambia ni lo rechaza; " +
                "queda senalado para que lo mires."));
        }

        if (caso.FechaViaje is { Length: > 0 } fecha && !TieneFormaDeFecha(fecha))
        {
            avisos.Add(Aviso.Advierte(
                "La fecha de viaje no tiene la forma AAAA-MM-DD.",
                nameof(Caso.FechaViaje),
                $"Se guardo «{fecha}» tal como venia. No aparecera en el calendario hasta que se corrija."));
        }

        return avisos;
    }

    /// <summary>Cuatro letras mayusculas y cuatro digitos, que es lo que el papel trae.</summary>
    /// <param name="numero">El número tal como se leyó.</param>
    private static bool TieneFormaDeNumeroDeCaso(string numero)
        => numero.Length == 8
           && numero.Take(4).All(c => c is >= 'A' and <= 'Z')
           && numero.Skip(4).All(char.IsAsciiDigit);

    /// <summary>Forma ISO-8601 de solo fecha, comprobada por posiciones y no por libreria.</summary>
    /// <param name="fecha">El texto tal como se leyó.</param>
    private static bool TieneFormaDeFecha(string fecha)
        => fecha.Length == 10 && fecha[4] == '-' && fecha[7] == '-'
           && fecha.Where((_, i) => i is not 4 and not 7).All(char.IsAsciiDigit);

    /// <summary>Aplica los filtros simples y devuelve la lista ordenada por fecha de viaje.</summary>
    /// <param name="filtro">Los campos del filtro; el texto busca en número, unidad y en el nombre o MRN de sus personas.</param>
    private List<Caso> Filtrar(FiltroDeCasos filtro)
    {
        var hoy = _almacen.Reloj.Hoy();
        var tope = filtro.VentanaDeDias is int dias ? _almacen.Reloj.HoyMasDias(dias) : null;

        IEnumerable<Caso> casos = _almacen.Casos.Values;

        if (!filtro.IncluirArchivados) casos = casos.Where(c => !c.Archivado);
        if (filtro.SoloDeHoy) casos = casos.Where(c => c.FechaViaje == hoy);
        if (tope is not null)
        {
            casos = casos.Where(c => c.FechaViaje is not null
                                     && string.CompareOrdinal(c.FechaViaje, hoy) >= 0
                                     && string.CompareOrdinal(c.FechaViaje, tope) <= 0);
        }
        if (filtro.SoloVencidos)
        {
            casos = casos.Where(c => c.FechaViaje is not null
                                     && string.CompareOrdinal(c.FechaViaje, hoy) < 0
                                     && c.Estado != EstadoDeRecomendacion.Completa);
        }
        if (filtro.Estado is EstadoDeRecomendacion estado) casos = casos.Where(c => c.Estado == estado);
        if (filtro.CompaneroId is long companero)
        {
            var suyos = _almacen.Asignaciones.Values
                .Where(a => a.Activa && a.CompaneroId == companero)
                .Select(a => a.CasoId)
                .ToHashSet();
            casos = casos.Where(c => suyos.Contains(c.Id));
        }
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto;
            var porPersona = _almacen.Personas.Values
                .Where(p => Trozos.Contiene(p.Nombre, texto) || Trozos.Contiene(p.Mrn, texto))
                .Select(p => p.CasoId)
                .ToHashSet();
            casos = casos.Where(c => Trozos.Contiene(c.NumeroCaso, texto)
                                     || Trozos.Contiene(c.UnidadNombre, texto)
                                     || porPersona.Contains(c.Id));
        }

        // Sin fecha de viaje al final: van en su propia lista, como pidio el dueno.
        return casos
            .OrderBy(c => c.FechaViaje is null)
            .ThenBy(c => c.FechaViaje, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .ToList();
    }
}
