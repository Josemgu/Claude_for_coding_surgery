using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>Los companeros. Se desactivan, NUNCA se borran.</summary>
/// <remarks>
/// Miguel es una fila de aqui como cualquier otro. El nombre NO es unico: dos personas
/// pueden llamarse igual, y el esquema no lo impide a proposito.
/// </remarks>
public sealed class RepositorioDeCompaneros : RepositorioBase, ICompaneros
{
    /// <summary>Las siete columnas de <c>companeros</c> que se leen, en el orden exacto en que <c>Leer</c> las espera por posición.</summary>
    /// <remarks>Si se añade una columna aquí, hay que añadirla al final y darle su índice en <c>Leer</c>: la lectura es por posición, no por nombre.</remarks>
    private const string Columnas = "id, nombre, activo, desactivado_en, creado_en, rol, categoria";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    /// <param name="conexion">La conexión abierta; no puede ser nula.</param>
    public RepositorioDeCompaneros(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public PaginaDe<Companero> Listar(FiltroDeCompaneros filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var donde = ComponerElFiltro(filtro);

        return Paginar(
            $"SELECT {Columnas} FROM companeros {donde} ORDER BY nombre COLLATE NOCASE, id",
            $"SELECT COUNT(*) FROM companeros {donde}",
            orden => PonerLosParametrosDelFiltro(orden, filtro),
            Leer,
            trozo);
    }

    /// <inheritdoc />
    public IReadOnlyList<Companero> Activos()
        => ListarTodo(
            $"SELECT {Columnas} FROM companeros WHERE activo = 1 " +
            "ORDER BY nombre COLLATE NOCASE, id",
            _ => { },
            Leer);

    /// <inheritdoc />
    public Companero? Obtener(long id)
        => ObtenerUno($"SELECT {Columnas} FROM companeros WHERE id = $id", id, Leer);

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Companero companero)
    {
        ArgumentNullException.ThrowIfNull(companero);

        if (string.IsNullOrWhiteSpace(companero.Nombre))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Un companero necesita un nombre.",
                    "nombre",
                    "El nombre es lo unico que identifica a quien verifica, y una firma " +
                    "sin nombre no dice quien firmo."));
        }

        return companero.Id == 0
            ? Escribir(
                "INSERT INTO companeros (nombre, activo, desactivado_en, creado_en, rol, categoria) " +
                "VALUES ($nombre, $activo, $desactivado, $creado, $rol, $categoria); " +
                "SELECT last_insert_rowid()",
                orden => PonerLosCampos(orden, companero),
                [])
            : Escribir(
                "UPDATE companeros SET nombre = $nombre, activo = $activo, " +
                "desactivado_en = $desactivado, creado_en = $creado, rol = $rol, " +
                "categoria = $categoria WHERE id = $id",
                orden =>
                {
                    PonerLosCampos(orden, companero);
                    orden.Parameters.AddWithValue("$id", companero.Id);
                },
                [],
                companero.Id);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Desactivar(long companeroId, string desactivadoEn)
    {
        if (string.IsNullOrWhiteSpace(desactivadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Para desactivar a un companero hace falta la fecha.",
                    "desactivado_en",
                    "El esquema no admite un companero desactivado sin fecha."));
        }

        // Se desactiva, no se borra: asi se conserva quien verifico que. Un companero
        // con firmas apuntando no se podria borrar de todas formas —el RESTRICT lo
        // impide—, y ese es justo el punto.
        //
        // Y si todavia lleva casos, se DICE. Las asignaciones no se retiran solas: nada
        // se borra sin preguntar (decision del dueno). Esto lo avisaba el repositorio
        // falso y el de verdad no, medido el 2026-09-05: el doble era el que estaba bien.
        return Escribir(
            "UPDATE companeros SET activo = 0, desactivado_en = $cuando WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$cuando", desactivadoEn);
                orden.Parameters.AddWithValue("$id", companeroId);
            },
            AvisoSiTodaviaLlevaCasos(companeroId),
            companeroId);
    }

    /// <summary>El aviso de los casos que se quedan colgando, o ninguno si no hay.</summary>
    /// <param name="companeroId">El compañero que se va a desactivar.</param>
    /// <returns>Lista vacía si no lleva ninguna asignación viva; si no, un solo aviso con la cifra.</returns>
    private IReadOnlyList<Aviso> AvisoSiTodaviaLlevaCasos(long companeroId)
    {
        var vivas = ContarCon(
            "SELECT COUNT(*) FROM asignaciones WHERE companero_id = $id AND activa = 1",
            orden => orden.Parameters.AddWithValue("$id", companeroId));

        return vivas == 0
            ? []
            : [
                Aviso.Advierte(
                    $"{Obtener(companeroId)?.Nombre ?? "Este companero"} queda desactivado " +
                    $"y todavia lleva {vivas} caso(s).",
                    nameof(Companero.Activo),
                    "Las asignaciones NO se retiran solas: nada se borra sin preguntar. " +
                    "Reasignalas cuando quieras."),
            ];
    }

    /// <summary>Rellena los marcadores de un compañero para INSERT y UPDATE, que comparten los seis marcadores; el <c>$id</c> lo añade solo el UPDATE.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="companero">De dónde salen los valores; el nombre va recortado de espacios y <c>desactivado_en</c> se fuerza a NULL si está activo.</param>
    private static void PonerLosCampos(SqliteCommand orden, Companero companero)
    {
        orden.Parameters.AddWithValue("$nombre", companero.Nombre.Trim());
        orden.Parameters.AddWithValue("$activo", companero.Activo ? 1 : 0);
        orden.Parameters.AddWithValue(
            "$desactivado", companero.Activo ? DBNull.Value : ONulo(companero.DesactivadoEn));
        orden.Parameters.AddWithValue(
            "$creado",
            string.IsNullOrEmpty(companero.CreadoEn)
                ? AplicadorDeEsquema.MarcaDeTiempo()
                : companero.CreadoEn);
        orden.Parameters.AddWithValue("$rol", Companero.EscribirRol(companero.Rol));

        // La categoria va como venga: un peldano anterior al primero lo rechaza el CHECK
        // y `Escribir` lo devuelve como aviso. Corregirlo aqui en silencio taparia un
        // defecto del programa, que es de donde puede venir un valor asi.
        orden.Parameters.AddWithValue("$categoria", companero.Categoria);
    }

    /// <summary>Compone el WHERE del filtro con marcadores <c>$nombre</c>; el texto del usuario nunca entra aquí, solo en <c>PonerLosParametrosDelFiltro</c>.</summary>
    /// <param name="filtro">Lo que la pantalla pide.</param>
    /// <returns>Una cláusula <c>WHERE …</c>, o vacío si el filtro no dice nada.</returns>
    private static string ComponerElFiltro(FiltroDeCompaneros filtro)
    {
        var condiciones = new List<string>();

        if (filtro.SoloActivos)
        {
            condiciones.Add("activo = 1");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add("nombre LIKE $texto COLLATE NOCASE");
        }

        return condiciones.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condiciones);
    }

    /// <summary>Rellena los marcadores que <c>ComponerElFiltro</c> dejó, y solo esos: un parámetro sin marcador es un error del motor.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="filtro">El mismo filtro con el que se compuso el WHERE.</param>
    private static void PonerLosParametrosDelFiltro(SqliteCommand orden, FiltroDeCompaneros filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            orden.Parameters.AddWithValue("$texto", "%" + filtro.Texto.Trim() + "%");
        }
    }

    /// <summary>Convierte una fila en un compañero, columna por columna y en el orden de <c>Columnas</c>.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    private static Companero Leer(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        Nombre = lector.GetString(1),
        Activo = Booleano(lector, 2),
        DesactivadoEn = TextoONulo(lector, 3),
        CreadoEn = TextoONulo(lector, 4) ?? string.Empty,
        Rol = Companero.LeerRol(TextoONulo(lector, 5)),
        Categoria = lector.GetInt32(6),
    };
}
