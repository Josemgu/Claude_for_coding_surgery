using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>Lo que no se pudo leer, y lo que volvio del Excel y no entro.</summary>
/// <remarks>
/// <para>
/// Las dos listas viven juntas por el mismo motivo: son trabajo que se perdio y que
/// alguien tiene que poder mirar despues. Un cuadro que se cierra con Aceptar no vale.
/// </para>
/// <para>
/// ⚠️ Ni <c>numero_caso</c> ni <c>mrn</c> se revisan en
/// <see cref="RegistrarDescartada"/>: lo que venia escrito puede ser justo lo que
/// estaba mal, y una tabla que existe para guardar lo que no entro no puede rechazar
/// lo que no entro.
/// </para>
/// <para>
/// Lo que BORRA renglones no esta aqui: vive en <c>RepositorioDeIlegibles.Borrado.cs</c>,
/// para que se vea de un vistazo cual es el archivo que quita filas.
/// </para>
/// </remarks>
public sealed partial class RepositorioDeIlegibles : RepositorioBase, IIlegibles
{
    /// <summary>Las ocho columnas de <c>documentos_ilegibles</c>, en el orden exacto en que <see cref="LeerRenglon"/> las espera por posición.</summary>
    private const string ColumnasDeIlegibles =
        "id, ruta_pdf, pagina_pdf, motivo, detalle, lineas_leidas, caso_id, registrado_en";

    /// <summary>Las nueve columnas de <c>filas_descartadas</c>, en el orden exacto en que <see cref="LeerDescartada"/> las espera por posición.</summary>
    private const string ColumnasDeDescartadas =
        "id, companero_id, ruta_excel, fila_excel, numero_caso, mrn, nombre, motivo, " +
        "registrado_en";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    /// <param name="conexion">La conexión abierta; no puede ser nula.</param>
    public RepositorioDeIlegibles(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public PaginaDe<RenglonIlegible> Listar(FiltroDeIlegibles filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var donde = ComponerElFiltro(filtro);

        return Paginar(
            $"SELECT {ColumnasDeIlegibles} FROM documentos_ilegibles {donde} " +
            "ORDER BY registrado_en DESC, id DESC",
            $"SELECT COUNT(*) FROM documentos_ilegibles {donde}",
            orden => PonerLosParametrosDelFiltro(orden, filtro),
            LeerRenglon,
            trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDeIlegibles filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return ContarCon(
            $"SELECT COUNT(*) FROM documentos_ilegibles {ComponerElFiltro(filtro)}",
            orden => PonerLosParametrosDelFiltro(orden, filtro));
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Registrar(RenglonIlegible renglon)
    {
        ArgumentNullException.ThrowIfNull(renglon);

        // Sin la ruta el renglon no sirve para ir a mirar nada, que es lo unico para lo
        // que existe.
        if (string.IsNullOrWhiteSpace(renglon.RutaPdf))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Un renglon de ilegible necesita la ruta del archivo.",
                    "ruta_pdf",
                    "Sin ella no hay a donde ir a mirar, y el renglon no sirve de nada."));
        }

        return Escribir(
            "INSERT INTO documentos_ilegibles " +
            "(ruta_pdf, pagina_pdf, motivo, detalle, lineas_leidas, caso_id, registrado_en) " +
            "VALUES ($ruta, $pagina, $motivo, $detalle, $lineas, $caso, $registrado); " +
            "SELECT last_insert_rowid()",
            orden =>
            {
                orden.Parameters.AddWithValue("$ruta", renglon.RutaPdf);
                orden.Parameters.AddWithValue("$pagina", ONulo(renglon.PaginaPdf));
                orden.Parameters.AddWithValue(
                    "$motivo",
                    string.IsNullOrWhiteSpace(renglon.Motivo) ? "sin_motivo" : renglon.Motivo);
                orden.Parameters.AddWithValue("$detalle", ONulo(renglon.Detalle));
                orden.Parameters.AddWithValue("$lineas", ONulo(renglon.LineasLeidas));
                orden.Parameters.AddWithValue("$caso", ONulo(renglon.CasoId));
                orden.Parameters.AddWithValue(
                    "$registrado",
                    string.IsNullOrEmpty(renglon.RegistradoEn)
                        ? AplicadorDeEsquema.MarcaDeTiempo()
                        : renglon.RegistradoEn);
            },
            []);
    }

    /// <inheritdoc />
    public PaginaDe<FilaDescartada> ListarDescartadas(long? companeroId, Pagina trozo)
    {
        var donde = companeroId is null ? string.Empty : "WHERE companero_id = $companero";

        return Paginar(
            $"SELECT {ColumnasDeDescartadas} FROM filas_descartadas {donde} " +
            "ORDER BY registrado_en DESC, id DESC",
            $"SELECT COUNT(*) FROM filas_descartadas {donde}",
            orden =>
            {
                if (companeroId is not null)
                {
                    orden.Parameters.AddWithValue("$companero", companeroId.Value);
                }
            },
            LeerDescartada,
            trozo);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura RegistrarDescartada(FilaDescartada fila)
    {
        ArgumentNullException.ThrowIfNull(fila);

        return Escribir(
            "INSERT INTO filas_descartadas " +
            "(companero_id, ruta_excel, fila_excel, numero_caso, mrn, nombre, motivo, " +
            "registrado_en) " +
            "VALUES ($companero, $ruta, $fila, $numero, $mrn, $nombre, $motivo, " +
            "$registrado); SELECT last_insert_rowid()",
            orden =>
            {
                orden.Parameters.AddWithValue("$companero", fila.CompaneroId);
                orden.Parameters.AddWithValue("$ruta", ONulo(fila.RutaExcel));
                orden.Parameters.AddWithValue("$fila", ONulo(fila.FilaExcel));

                // Tal como venia escrito, SIN validar. Ver el comentario de la clase.
                orden.Parameters.AddWithValue("$numero", ONulo(fila.NumeroCaso));
                orden.Parameters.AddWithValue("$mrn", ONulo(fila.Mrn));
                orden.Parameters.AddWithValue("$nombre", ONulo(fila.Nombre));

                orden.Parameters.AddWithValue(
                    "$motivo",
                    string.IsNullOrWhiteSpace(fila.Motivo)
                        ? "No se dijo por que no entro."
                        : fila.Motivo);
                orden.Parameters.AddWithValue(
                    "$registrado",
                    string.IsNullOrEmpty(fila.RegistradoEn)
                        ? AplicadorDeEsquema.MarcaDeTiempo()
                        : fila.RegistradoEn);
            },
            []);
    }

    /// <summary>Compone el WHERE del filtro con marcadores <c>$nombre</c>; el texto del usuario nunca entra aquí, solo en <c>PonerLosParametrosDelFiltro</c>.</summary>
    /// <param name="filtro">Lo que la pantalla pide.</param>
    /// <returns>Una cláusula <c>WHERE …</c>, o vacío si el filtro no dice nada.</returns>
    /// <remarks>Ruta y motivo se comparan con <c>=</c> y no con LIKE: son valores exactos que la pantalla ya conoce, no texto tecleado.</remarks>
    private static string ComponerElFiltro(FiltroDeIlegibles filtro)
    {
        var condiciones = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtro.RutaPdf))
        {
            condiciones.Add("ruta_pdf = $ruta");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Motivo))
        {
            condiciones.Add("motivo = $motivo");
        }

        return condiciones.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condiciones);
    }

    /// <summary>Rellena los marcadores que <c>ComponerElFiltro</c> dejó, y solo esos: un parámetro sin marcador es un error del motor.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="filtro">El mismo filtro con el que se compuso el WHERE.</param>
    private static void PonerLosParametrosDelFiltro(SqliteCommand orden, FiltroDeIlegibles filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.RutaPdf))
        {
            orden.Parameters.AddWithValue("$ruta", filtro.RutaPdf);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Motivo))
        {
            orden.Parameters.AddWithValue("$motivo", filtro.Motivo);
        }
    }

    /// <summary>Convierte una fila en un renglón de ilegible, columna por columna y en el orden de <c>Columnas</c>.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    private static RenglonIlegible LeerRenglon(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        RutaPdf = TextoONulo(lector, 1) ?? string.Empty,
        PaginaPdf = EnteroONulo(lector, 2),
        Motivo = TextoONulo(lector, 3) ?? string.Empty,
        Detalle = TextoONulo(lector, 4),
        LineasLeidas = EnteroONulo(lector, 5),
        CasoId = LargoONulo(lector, 6),
        RegistradoEn = TextoONulo(lector, 7) ?? string.Empty,
    };

    /// <summary>Convierte una fila en una fila descartada del Excel, columna por columna y en el orden de <c>Columnas</c>.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    private static FilaDescartada LeerDescartada(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        CompaneroId = lector.GetInt64(1),
        RutaExcel = TextoONulo(lector, 2),
        FilaExcel = EnteroONulo(lector, 3),
        NumeroCaso = TextoONulo(lector, 4),
        Mrn = TextoONulo(lector, 5),
        Nombre = TextoONulo(lector, 6),
        Motivo = TextoONulo(lector, 7) ?? string.Empty,
        RegistradoEn = TextoONulo(lector, 8) ?? string.Empty,
    };
}
