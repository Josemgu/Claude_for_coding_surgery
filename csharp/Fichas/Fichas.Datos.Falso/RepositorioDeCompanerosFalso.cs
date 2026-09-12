using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Los companeros inventados. Cumple <see cref="ICompaneros"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeCompanerosFalso : ICompaneros
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public RepositorioDeCompanerosFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de companeros que cumplen el filtro, con el total detras.</summary>
    /// <param name="filtro">Qué compañeros entran; ver <see cref="Filtrar"/>.</param>
    /// <param name="trozo">Qué página se pide.</param>
    public PaginaDe<Companero> Listar(FiltroDeCompaneros filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Devuelve todos los activos, ordenados por nombre.</summary>
    public IReadOnlyList<Companero> Activos() => Filtrar(FiltroDeCompaneros.Activos);

    /// <summary>Devuelve un companero por su id, o nulo si no esta.</summary>
    /// <param name="id">El número interno.</param>
    public Companero? Obtener(long id) => _almacen.Companeros.TryGetValue(id, out var companero) ? companero : null;

    /// <summary>Da de alta un companero o cambia su nombre.</summary>
    /// <remarks>
    /// ⚠️ Un companero sin nombre NO entra, igual que en <c>RepositorioDeCompaneros</c>.
    /// Hasta el 2026-09-05 aqui entraba con una simple advertencia mientras el de verdad
    /// lo rechazaba: una pantalla probada contra este doble creeria que el alta salio
    /// bien y en la maquina del dueno no habria entrado nadie.
    /// </remarks>
    /// <param name="companero">El compañero; con <c>Id</c> 0 se le da uno nuevo y, si viene sin <c>CreadoEn</c>, el instante del reloj.</param>
    public ResultadoDeEscritura Guardar(Companero companero)
    {
        if (string.IsNullOrWhiteSpace(companero.Nombre))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Un companero necesita un nombre.",
                nameof(Companero.Nombre),
                "El nombre es lo unico que identifica a quien verifica, y una firma sin " +
                "nombre no dice quien firmo."));
        }

        var id = companero.Id == 0 ? _almacen.SiguienteId() : companero.Id;
        var creadoEn = string.IsNullOrWhiteSpace(companero.CreadoEn) ? _almacen.Reloj.Ahora() : companero.CreadoEn;
        _almacen.Companeros[id] = companero with { Id = id, CreadoEn = creadoEn };
        return ResultadoDeEscritura.Bien(id);
    }

    /// <summary>Desactiva un companero dejando su fecha; nunca lo borra.</summary>
    /// <remarks>
    /// ⚠️ Sin fecha NO se desactiva, igual que en <c>RepositorioDeCompaneros</c>. Hasta el
    /// 2026-09-05 aqui se ponia <c>Reloj.Ahora()</c> cuando venia vacia: inventar el dato
    /// que falta tapa que quien llama se olvido de pasarlo, y en el de verdad el esquema
    /// lo rechaza.
    /// </remarks>
    /// <param name="companeroId">A quién; si no existe, no se escribe y se dice.</param>
    /// <param name="desactivadoEn">Cuándo, en ISO; en blanco no se escribe.</param>
    /// <returns>Bien; con advertencia si todavía lleva casos, que no se retiran solos.</returns>
    public ResultadoDeEscritura Desactivar(long companeroId, string desactivadoEn)
    {
        if (string.IsNullOrWhiteSpace(desactivadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Para desactivar a un companero hace falta la fecha.",
                nameof(Companero.DesactivadoEn),
                "El esquema no admite un companero desactivado sin fecha."));
        }

        if (!_almacen.Companeros.TryGetValue(companeroId, out var companero))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun companero con el numero interno {companeroId}."));

        _almacen.Companeros[companeroId] = companero with { Activo = false, DesactivadoEn = desactivadoEn };

        var vivas = _almacen.Asignaciones.Values.Count(a => a.Activa && a.CompaneroId == companeroId);
        return vivas == 0
            ? ResultadoDeEscritura.Bien(companeroId)
            : ResultadoDeEscritura.BienCon(companeroId, Aviso.Advierte(
                $"{companero.Nombre} queda desactivado y todavia lleva {vivas} caso(s).",
                nameof(Companero.Activo),
                "Las asignaciones NO se retiran solas: nada se borra sin preguntar. Reasignalas cuando quieras."));
    }

    /// <summary>Aplica los filtros simples y devuelve la lista ordenada por nombre.</summary>
    /// <remarks>
    /// ⚠️ Ordena con la cultura de la máquina (<c>CurrentCulture</c>), que es lo único de este
    /// proyecto que depende de ella; el de verdad ordena como ordene SQLite. Con nombres solo
    /// ASCII dan lo mismo.
    /// </remarks>
    /// <param name="filtro">Solo activos, y un texto que se busca en el nombre.</param>
    private List<Companero> Filtrar(FiltroDeCompaneros filtro)
    {
        IEnumerable<Companero> companeros = _almacen.Companeros.Values;
        if (filtro.SoloActivos) companeros = companeros.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) companeros = companeros.Where(c => Trozos.Contiene(c.Nombre, filtro.Texto));
        return companeros.OrderBy(c => c.Nombre, StringComparer.CurrentCulture).ThenBy(c => c.Id).ToList();
    }
}
