using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>Que caso lleva que companero.</summary>
/// <remarks>
/// <para>
/// UNA sola operacion de asignar —<see cref="Asignar"/>—, que es lo que hace posible
/// el requisito 2 del dueno: asignar desde cualquier lugar. La tarjeta de Revisar, la
/// correccion y la lista de Asignar llaman a la misma y no a tres cosas parecidas.
/// </para>
/// <para>
/// ⚠️ El motor permite hoy DOS asignaciones vivas sobre el mismo caso con companeros
/// distintos: el indice unico <c>idx_asignacion_viva</c> es sobre el PAR
/// (caso, companero), no sobre el caso solo. Es la P-11, abierta y devuelta al dueno;
/// este repositorio NO inventa la regla que falta.
/// </para>
/// </remarks>
public sealed class RepositorioDeAsignaciones : RepositorioBase, IAsignaciones
{
    private const string Columnas =
        "id, caso_id, companero_id, asignado_en, activa, desactivada_en";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    public RepositorioDeAsignaciones(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public PaginaDe<Asignacion> Listar(FiltroDeAsignaciones filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var donde = ComponerElFiltro(filtro);

        return Paginar(
            $"SELECT {Columnas} FROM asignaciones {donde} ORDER BY asignado_en DESC, id DESC",
            $"SELECT COUNT(*) FROM asignaciones {donde}",
            orden => PonerLosParametrosDelFiltro(orden, filtro),
            Leer,
            trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDeAsignaciones filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return ContarCon(
            $"SELECT COUNT(*) FROM asignaciones {ComponerElFiltro(filtro)}",
            orden => PonerLosParametrosDelFiltro(orden, filtro));
    }

    /// <inheritdoc />
    public IReadOnlyList<Asignacion> VivasDeCaso(long casoId)
        => ListarTodo(
            $"SELECT {Columnas} FROM asignaciones WHERE caso_id = $caso AND activa = 1 " +
            "ORDER BY asignado_en, id",
            orden => orden.Parameters.AddWithValue("$caso", casoId),
            Leer);

    /// <inheritdoc />
    public ResultadoDeEscritura Asignar(long casoId, long companeroId, string asignadoEn)
    {
        if (string.IsNullOrWhiteSpace(asignadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Para asignar hace falta la fecha.",
                    "asignado_en",
                    "Sin fecha no se puede saber despues cuando se repartio el trabajo."));
        }

        // El companero tiene que estar ACTIVO: uno desactivado no recibe casos nuevos.
        // Se comprueba aqui y no con un CHECK del motor porque un CHECK no puede mirar
        // otra tabla, y un disparador seria logica escondida donde nadie la busca.
        //
        // Y NO hay filtro de estado del caso: cualquier caso se asigna a cualquier
        // companero activo, que es lo que dice el contrato.
        if (!EstaActivo(companeroId))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Ese companero ya no esta activo.",
                    "companero_id",
                    $"El companero {companeroId} esta desactivado o no existe, y un " +
                    "companero desactivado no recibe casos nuevos."));
        }

        // Pedir lo que ya esta hecho no es un fallo. `idx_asignacion_viva` impide la
        // segunda fila, pero dejar que salte devolvia «La base rechazo el dato» como
        // PROBLEMA: la pantalla pintaba la franja roja del motor por una peticion que ya
        // estaba satisfecha. Se mira antes y se dice lo que pasa. Medido el 2026-09-05
        // contra el repositorio falso, que ya lo hacia bien.
        var yaLaTiene = VivasDeCaso(casoId).FirstOrDefault(a => a.CompaneroId == companeroId);
        if (yaLaTiene is not null)
        {
            return ResultadoDeEscritura.BienCon(
                yaLaTiene.Id,
                Aviso.Informa(
                    "Ese companero ya llevaba este caso.",
                    "companero_id",
                    $"La asignacion viva es del {yaLaTiene.AsignadoEn}. No se creo otra."));
        }

        return Escribir(
            "INSERT INTO asignaciones (caso_id, companero_id, asignado_en, activa) " +
            "VALUES ($caso, $companero, $cuando, 1); SELECT last_insert_rowid()",
            orden =>
            {
                orden.Parameters.AddWithValue("$caso", casoId);
                orden.Parameters.AddWithValue("$companero", companeroId);
                orden.Parameters.AddWithValue("$cuando", asignadoEn);
            },
            []);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Retirar(long asignacionId, string desactivadaEn)
    {
        if (string.IsNullOrWhiteSpace(desactivadaEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Para retirar una asignacion hace falta la fecha.",
                    "desactivada_en",
                    "El esquema no admite una asignacion retirada sin fecha."));
        }

        // Se desactiva, NUNCA se borra: es lo que conserva quien llevo que caso, que es
        // la evidencia de trabajo individual que este programa existe para no perder.
        return Escribir(
            "UPDATE asignaciones SET activa = 0, desactivada_en = $cuando " +
            "WHERE id = $id AND activa = 1",
            orden =>
            {
                orden.Parameters.AddWithValue("$cuando", desactivadaEn);
                orden.Parameters.AddWithValue("$id", asignacionId);
            },
            [],
            asignacionId);
    }

    /// <summary>Si ese companero existe y sigue activo.</summary>
    private bool EstaActivo(long companeroId)
    {
        using var orden = Conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM companeros WHERE id = $id AND activo = 1";
        orden.Parameters.AddWithValue("$id", companeroId);
        return Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static string ComponerElFiltro(FiltroDeAsignaciones filtro)
    {
        var condiciones = new List<string>();

        if (filtro.SoloActivas)
        {
            condiciones.Add("activa = 1");
        }

        if (filtro.CasoId is not null)
        {
            condiciones.Add("caso_id = $caso");
        }

        if (filtro.CompaneroId is not null)
        {
            condiciones.Add("companero_id = $companero");
        }

        if (filtro.SinDevolver)
        {
            // «Sin devolver» es: el caso al que apunta esta asignacion no tiene todavia
            // lo que dijo la hoja del companero. Se mira `estado_del_companero`, que es
            // la columna que la version 14 anadio justo para esto, y NO
            // `estado_recomendacion`: esa la puede haber escrito Miguel a mano, y
            // entonces el caso saldria como devuelto sin que el companero devolviera
            // nada.
            condiciones.Add(
                "EXISTS (SELECT 1 FROM casos c WHERE c.id = asignaciones.caso_id " +
                "AND c.estado_del_companero IS NULL)");
        }

        return condiciones.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condiciones);
    }

    private static void PonerLosParametrosDelFiltro(SqliteCommand orden, FiltroDeAsignaciones filtro)
    {
        if (filtro.CasoId is not null)
        {
            orden.Parameters.AddWithValue("$caso", filtro.CasoId.Value);
        }

        if (filtro.CompaneroId is not null)
        {
            orden.Parameters.AddWithValue("$companero", filtro.CompaneroId.Value);
        }
    }

    private static Asignacion Leer(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        CasoId = lector.GetInt64(1),
        CompaneroId = lector.GetInt64(2),
        AsignadoEn = TextoONulo(lector, 3) ?? string.Empty,
        Activa = Booleano(lector, 4),
        DesactivadaEn = TextoONulo(lector, 5),
    };
}
