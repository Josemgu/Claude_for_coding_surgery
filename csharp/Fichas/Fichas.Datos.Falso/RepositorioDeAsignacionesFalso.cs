using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Las asignaciones inventadas. Cumple <see cref="IAsignaciones"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeAsignacionesFalso : IAsignaciones
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public RepositorioDeAsignacionesFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de asignaciones que cumplen el filtro, con el total detras.</summary>
    /// <param name="filtro">Qué asignaciones entran; ver <see cref="Filtrar"/>.</param>
    /// <param name="trozo">Qué página se pide.</param>
    public PaginaDe<Asignacion> Listar(FiltroDeAsignaciones filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Cuenta cuantas asignaciones cumplen el filtro, sin traerlas.</summary>
    /// <param name="filtro">Qué asignaciones entran.</param>
    public int Contar(FiltroDeAsignaciones filtro) => Filtrar(filtro).Count;

    /// <summary>Devuelve las asignaciones vivas de un caso; hoy pueden ser mas de una (P-11 abierta).</summary>
    /// <param name="casoId">El caso.</param>
    public IReadOnlyList<Asignacion> VivasDeCaso(long casoId)
        => Filtrar(new FiltroDeAsignaciones(CasoId: casoId, SoloActivas: true));

    /// <summary>
    /// Asigna un caso a un companero activo. La unica puerta de asignar que existe:
    /// la tarjeta, la correccion y la lista llaman aqui y a ningun otro sitio (requisito 2).
    /// </summary>
    /// <remarks>
    /// Igual que el de verdad en las cuatro salidas: sin fecha no se escribe, un caso o un
    /// compañero que no existe no se escribe, un desactivado es PROBLEMA y no advertencia, y
    /// la misma pareja viva no se duplica. Un segundo compañero sobre el mismo caso entra y avisa.
    /// </remarks>
    /// <param name="casoId">El caso; tiene que existir.</param>
    /// <param name="companeroId">A quién; tiene que existir y estar activo.</param>
    /// <param name="asignadoEn">Cuándo, en ISO; en blanco no se escribe.</param>
    public ResultadoDeEscritura Asignar(long casoId, long companeroId, string asignadoEn)
    {
        // Sin fecha no se asigna, igual que en `RepositorioDeAsignaciones`: sin ella no se
        // puede saber despues cuando se repartio el trabajo.
        if (string.IsNullOrWhiteSpace(asignadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Para asignar hace falta la fecha.",
                nameof(Asignacion.AsignadoEn),
                "Sin fecha no se puede saber despues cuando se repartio el trabajo."));
        }

        if (!_almacen.Casos.ContainsKey(casoId))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun caso con el numero interno {casoId}."));

        if (!_almacen.Companeros.TryGetValue(companeroId, out var companero))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun companero con el numero interno {companeroId}."));

        // La UNICA condicion que queda del programa viejo (requisito 8): no se asigna a un
        // companero desactivado. NO hay filtro por estado del caso: cualquier caso se asigna.
        if (!companero.Activo)
        {
            // Va como PROBLEMA y no como advertencia: la accion NO se hizo, y una
            // advertencia significa «se guardo igual, senalado». El de verdad ya lo
            // devolvia asi; hasta el 2026-09-05 este doble lo bajaba a advertencia y la
            // franja de la pantalla salia del color que no era.
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"{companero.Nombre} esta desactivado y no puede recibir casos.",
                nameof(Asignacion.CompaneroId),
                "Es la unica condicion que queda para asignar. Reactivalo o elige a otro."));
        }

        var yaLaTiene = _almacen.Asignaciones.Values
            .FirstOrDefault(a => a.Activa && a.CasoId == casoId && a.CompaneroId == companeroId);
        if (yaLaTiene is not null)
        {
            return ResultadoDeEscritura.BienCon(yaLaTiene.Id, Aviso.Informa(
                $"{companero.Nombre} ya llevaba este caso; no se duplico.",
                nameof(Asignacion.CasoId)));
        }

        var id = _almacen.SiguienteId();
        _almacen.Asignaciones[id] = new Asignacion
        {
            Id = id,
            CasoId = casoId,
            CompaneroId = companeroId,
            AsignadoEn = string.IsNullOrWhiteSpace(asignadoEn) ? _almacen.Reloj.Ahora() : asignadoEn,
            Activa = true,
        };

        // ⚠️ El motor permite hoy dos companeros vivos sobre el mismo caso (P-11, abierta).
        // No se inventa la regla que falta: se avisa y se guarda.
        var otras = _almacen.Asignaciones.Values.Count(a => a.Activa && a.CasoId == casoId);
        return otras <= 1
            ? ResultadoDeEscritura.Bien(id)
            : ResultadoDeEscritura.BienCon(id, Aviso.Advierte(
                $"Este caso lo llevan ahora {otras} companeros a la vez.",
                nameof(Asignacion.CasoId),
                "Nadie ha escrito todavia si un caso puede llevarlo mas de uno. Se guarda y se avisa."));
    }

    /// <summary>Retira una asignacion desactivandola con su fecha; nunca la borra.</summary>
    /// <param name="asignacionId">La asignación; si no existe, no se escribe y se dice.</param>
    /// <param name="desactivadaEn">Cuándo, en ISO; en blanco no se escribe.</param>
    public ResultadoDeEscritura Retirar(long asignacionId, string desactivadaEn)
    {
        // Sin fecha no se retira, igual que en `RepositorioDeAsignaciones`: el esquema ata
        // `activa` con `desactivada_en`. Hasta el 2026-09-05 aqui se ponia
        // `Reloj.Ahora()`, e inventar el dato que falta tapa que quien llama se olvido de
        // pasarlo.
        if (string.IsNullOrWhiteSpace(desactivadaEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Para retirar una asignacion hace falta la fecha.",
                nameof(Asignacion.DesactivadaEn),
                "El esquema no admite una asignacion retirada sin fecha."));
        }

        if (!_almacen.Asignaciones.TryGetValue(asignacionId, out var asignacion))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ninguna asignacion con el numero interno {asignacionId}."));

        _almacen.Asignaciones[asignacionId] = asignacion with { Activa = false, DesactivadaEn = desactivadaEn };
        return ResultadoDeEscritura.Bien(asignacionId);
    }

    /// <summary>Aplica los filtros simples y devuelve la lista ordenada por fecha de asignacion.</summary>
    /// <param name="filtro">Los campos del filtro: solo activas, un caso, un compañero, o sin devolver.</param>
    /// <returns>De la más reciente a la más vieja y luego por id.</returns>
    private List<Asignacion> Filtrar(FiltroDeAsignaciones filtro)
    {
        IEnumerable<Asignacion> asignaciones = _almacen.Asignaciones.Values;

        if (filtro.SoloActivas) asignaciones = asignaciones.Where(a => a.Activa);
        if (filtro.CasoId is long caso) asignaciones = asignaciones.Where(a => a.CasoId == caso);
        if (filtro.CompaneroId is long companero) asignaciones = asignaciones.Where(a => a.CompaneroId == companero);
        if (filtro.SinDevolver)
        {
            // «Sin devolver» es una asignacion viva cuyo caso todavia no tiene estado marcado.
            asignaciones = asignaciones.Where(a =>
                _almacen.Casos.TryGetValue(a.CasoId, out var caso)
                && caso.Estado == EstadoDeRecomendacion.SinMarcar);
        }

        return asignaciones
            .OrderByDescending(a => a.AsignadoEn, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToList();
    }
}
