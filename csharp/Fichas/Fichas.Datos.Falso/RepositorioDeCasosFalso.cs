using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Los casos inventados. Cumple <see cref="ICasos"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeCasosFalso : ICasos
{
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    public RepositorioDeCasosFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de casos que cumplen el filtro, con el total detras.</summary>
    public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Cuenta cuantos casos cumplen el filtro, sin traerlos.</summary>
    public int Contar(FiltroDeCasos filtro) => Filtrar(filtro).Count;

    /// <summary>Devuelve un caso por su id, o nulo si no esta.</summary>
    public Caso? Obtener(long id) => _almacen.Casos.TryGetValue(id, out var caso) ? caso : null;

    /// <summary>Cuenta las personas de cada caso de la lista que se le pase, en una sola pasada.</summary>
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
    private static bool TieneFormaDeNumeroDeCaso(string numero)
        => numero.Length == 8
           && numero.Take(4).All(c => c is >= 'A' and <= 'Z')
           && numero.Skip(4).All(char.IsAsciiDigit);

    /// <summary>Forma ISO-8601 de solo fecha, comprobada por posiciones y no por libreria.</summary>
    private static bool TieneFormaDeFecha(string fecha)
        => fecha.Length == 10 && fecha[4] == '-' && fecha[7] == '-'
           && fecha.Where((_, i) => i is not 4 and not 7).All(char.IsAsciiDigit);

    /// <summary>Aplica los filtros simples y devuelve la lista ordenada por fecha de viaje.</summary>
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
