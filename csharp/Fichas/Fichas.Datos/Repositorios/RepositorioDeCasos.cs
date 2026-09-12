using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Esquema;
using Fichas.Datos.Validacion;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>Los casos: un formulario de recomendacion al templo por fila.</summary>
/// <remarks>
/// Cumple <see cref="ICasos"/> tal cual. Las 22 columnas van nombradas una a una en
/// cada consulta y nunca por <c>SELECT *</c>: una columna anadida en medio dejaria las
/// lecturas corridas sin que nadie se entere.
/// </remarks>
public sealed class RepositorioDeCasos : RepositorioBase, ICasos
{
    /// <summary>Las 22 columnas de <c>casos</c> que se leen, en el orden exacto en que <c>Leer</c> las espera por posición.</summary>
    /// <remarks>Si se añade una columna aquí, hay que añadirla al final y darle su índice en <c>Leer</c>: la lectura es por posición, no por nombre.</remarks>
    private const string Columnas =
        "id, numero_caso, duplicado_de, estado_marcado_por, estado_marcado_en, " +
        "estado_marcado_origen, estado_del_companero, estado_del_companero_por, " +
        "estado_del_companero_en, unidad_numero, fecha_viaje, captura_manual, " +
        "archivado, fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, " +
        "pagina_pdf, unidad_nombre, templo_nombre, motivo_no_completa, " +
        "motivo_del_companero";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    /// <param name="conexion">La conexión abierta; no puede ser nula.</param>
    public RepositorioDeCasos(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var donde = ComponerElFiltro(filtro);

        return Paginar(
            $"SELECT {Columnas} FROM casos {donde} ORDER BY fecha_viaje IS NULL, fecha_viaje, id",
            $"SELECT COUNT(*) FROM casos {donde}",
            orden => PonerLosParametrosDelFiltro(orden, filtro),
            Leer,
            trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDeCasos filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return ContarCon(
            $"SELECT COUNT(*) FROM casos {ComponerElFiltro(filtro)}",
            orden => PonerLosParametrosDelFiltro(orden, filtro));
    }

    /// <inheritdoc />
    public Caso? Obtener(long id)
        => ObtenerUno($"SELECT {Columnas} FROM casos WHERE id = $id", id, Leer);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds)
    {
        ArgumentNullException.ThrowIfNull(casoIds);

        var cuenta = new Dictionary<long, int>();
        if (casoIds.Count == 0)
        {
            return cuenta;
        }

        // Una sola pasada, como pide el contrato: la lista de Revisar pinta 50 tarjetas
        // y preguntar una vez por cada una son 50 viajes al motor.
        //
        // Los marcadores se generan por POSICION —$c0, $c1…— y los ids se pasan como
        // parametros. No se concatena ni un solo valor: los ids son numeros que vienen
        // de la pantalla, y la costumbre de ARQUITECTURA §1.7 no se afloja porque el
        // dato parezca inofensivo.
        var marcadores = string.Join(", ", casoIds.Select((_, i) => "$c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        using var orden = Conexion.CreateCommand();
        orden.CommandText =
            $"SELECT caso_id, COUNT(*) FROM personas WHERE caso_id IN ({marcadores}) " +
            "GROUP BY caso_id";
        for (var i = 0; i < casoIds.Count; i++)
        {
            orden.Parameters.AddWithValue("$c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), casoIds[i]);
        }

        using var lector = orden.ExecuteReader();
        while (lector.Read())
        {
            cuenta[lector.GetInt64(0)] = lector.GetInt32(1);
        }

        // Un caso sin personas no sale del GROUP BY, y la pantalla necesita su cero:
        // sin esto tendria que distinguir «no vino» de «no tiene», que es la clase de
        // hueco por el que una tarjeta se queda en blanco.
        foreach (var id in casoIds)
        {
            _ = cuenta.TryAdd(id, 0);
        }

        return cuenta;
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Caso caso)
    {
        ArgumentNullException.ThrowIfNull(caso);

        var avisos = ReglasDeFormato.Juntar(
            ReglasDeFormato.RevisarNumeroCaso(caso.NumeroCaso),
            ReglasDeFormato.RevisarUnidadNumero(caso.UnidadNumero),
            ReglasDeFormato.RevisarFechaViaje(caso.FechaViaje),
            ReglasDeFormato.RevisarUnidadNombre(caso.UnidadNombre),
            ReglasDeFormato.RevisarPaginaPdf(caso.PaginaPdf),
            ReglasDeFormato.RevisarMesCruzado(caso.NumeroCaso, caso.FechaViaje));

        return caso.Id == 0 ? Insertar(caso, avisos) : Cambiar(caso, avisos);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura MarcarEstado(
        long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
    {
        // ⚠️ Esto NO firma nada. La regla permanente 5, precisada por el dueno el
        // 2026-09-03, separa dos cosas que no se mezclan: la FIRMA de campos es de
        // Miguel y nunca automatica (vive en IProcedencia.Firmar), y el ESTADO de la
        // recomendacion lo escribe el Excel que devuelve el companero, con su nombre.
        // Esto es lo segundo.
        return Escribir(
            "UPDATE casos SET estado_recomendacion = $estado, estado_marcado_por = $por, " +
            "estado_marcado_en = $cuando, estado_marcado_origen = $origen WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$estado", ONulo(Caso.EscribirEstado(estado)));
                orden.Parameters.AddWithValue("$por", companeroId);
                orden.Parameters.AddWithValue("$cuando", AplicadorDeEsquema.MarcaDeTiempo());
                orden.Parameters.AddWithValue("$origen", ONulo(origen));
                orden.Parameters.AddWithValue("$id", casoId);
            },
            [],
            casoId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura MarcarEstadoDelCompanero(
        long casoId,
        EstadoDeRecomendacion estado,
        MotivoDeNoCompletar motivo,
        long companeroId,
        string origen)
    {
        // Las dos mitades en una sola instruccion: el estado vigente, que es el que se ve,
        // y el registro de lo que dijo el companero, que sobrevive a que Miguel corrija
        // encima. Hasta ahora `estado_del_companero` quedaba NULL siempre porque nadie lo
        // escribia por ningun camino, y con el vacio el reporte del gerente sale en blanco.
        //
        // ⚠️ NO se toca `motivo_no_completa`: criterio C14-4 de PENDIENTES.md, «lo que
        // escribe la hoja va a motivo_del_companero y NUNCA a motivo_no_completa».
        return Escribir(
            "UPDATE casos SET estado_recomendacion = $estado, estado_marcado_por = $por, " +
            "estado_marcado_en = $cuando, estado_marcado_origen = $origen, " +
            "estado_del_companero = $estado, estado_del_companero_por = $por, " +
            "estado_del_companero_en = $cuando, motivo_del_companero = $motivo " +
            "WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$estado", ONulo(Caso.EscribirEstado(estado)));
                orden.Parameters.AddWithValue("$por", companeroId);
                orden.Parameters.AddWithValue("$cuando", AplicadorDeEsquema.MarcaDeTiempo());
                orden.Parameters.AddWithValue("$origen", ONulo(origen));
                orden.Parameters.AddWithValue("$motivo", ONulo(Caso.EscribirMotivo(motivo)));
                orden.Parameters.AddWithValue("$id", casoId);
            },
            [],
            casoId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
    {
        // El esquema exige que las dos vayan juntas o ninguna: un caso archivado tiene
        // fecha, uno no archivado no la tiene, y no hay medio archivado. Desarchivar la
        // quita, que es lo que dice el contrato.
        if (archivado && string.IsNullOrWhiteSpace(fechaDeArchivado))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Para archivar un caso hace falta la fecha.",
                    "fecha_archivado",
                    "El esquema no admite un caso archivado sin fecha de archivado."));
        }

        return Escribir(
            "UPDATE casos SET archivado = $archivado, fecha_archivado = $fecha WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$archivado", archivado ? 1 : 0);
                orden.Parameters.AddWithValue("$fecha", archivado ? fechaDeArchivado : DBNull.Value);
                orden.Parameters.AddWithValue("$id", casoId);
            },
            [],
            casoId);
    }

    /// <summary>Crea la fila y devuelve su id nuevo con <c>last_insert_rowid()</c>; <c>creado_en</c> se pone a ahora si viene vacío.</summary>
    /// <param name="caso">El caso con <c>Id</c> 0.</param>
    /// <param name="avisos">Lo que las reglas de formato ya dijeron; se devuelven junto con el resultado.</param>
    private ResultadoDeEscritura Insertar(Caso caso, IReadOnlyList<Aviso> avisos)
        => Escribir(
            "INSERT INTO casos (numero_caso, duplicado_de, estado_marcado_por, " +
            "estado_marcado_en, estado_marcado_origen, estado_del_companero, " +
            "estado_del_companero_por, estado_del_companero_en, unidad_numero, " +
            "fecha_viaje, captura_manual, archivado, fecha_archivado, ruta_pdf, " +
            "creado_en, estado_recomendacion, pagina_pdf, unidad_nombre, templo_nombre, " +
            "motivo_no_completa, motivo_del_companero) " +
            "VALUES ($numero, $duplicado, $marcadoPor, $marcadoEn, $marcadoOrigen, " +
            "$delCompanero, $delCompaneroPor, $delCompaneroEn, $unidad, $viaje, " +
            "$manual, $archivado, $fechaArchivado, $ruta, $creado, $estado, $pagina, " +
            "$unidadNombre, $templo, $motivo, $motivoDelCompanero); " +
            "SELECT last_insert_rowid()",
            orden => PonerLosCamposDelCaso(orden, caso),
            avisos);

    /// <summary>Reescribe las 21 columnas de la fila con ese id; si no existe, devuelve un problema en vez de crearla.</summary>
    /// <param name="caso">El caso con su <c>Id</c>.</param>
    /// <param name="avisos">Lo que las reglas de formato ya dijeron; se devuelven junto con el resultado.</param>
    private ResultadoDeEscritura Cambiar(Caso caso, IReadOnlyList<Aviso> avisos)
        => Escribir(
            "UPDATE casos SET numero_caso = $numero, duplicado_de = $duplicado, " +
            "estado_marcado_por = $marcadoPor, estado_marcado_en = $marcadoEn, " +
            "estado_marcado_origen = $marcadoOrigen, estado_del_companero = $delCompanero, " +
            "estado_del_companero_por = $delCompaneroPor, " +
            "estado_del_companero_en = $delCompaneroEn, unidad_numero = $unidad, " +
            "fecha_viaje = $viaje, captura_manual = $manual, archivado = $archivado, " +
            "fecha_archivado = $fechaArchivado, ruta_pdf = $ruta, creado_en = $creado, " +
            "estado_recomendacion = $estado, pagina_pdf = $pagina, " +
            "unidad_nombre = $unidadNombre, templo_nombre = $templo, " +
            "motivo_no_completa = $motivo, motivo_del_companero = $motivoDelCompanero " +
            "WHERE id = $id",
            orden =>
            {
                PonerLosCamposDelCaso(orden, caso);
                orden.Parameters.AddWithValue("$id", caso.Id);
            },
            avisos,
            caso.Id);

    /// <summary>Rellena los marcadores de un caso para INSERT y UPDATE, que comparten los 21 marcadores; el <c>$id</c> lo añade solo el UPDATE.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="caso">De dónde salen los valores.</param>
    private static void PonerLosCamposDelCaso(SqliteCommand orden, Caso caso)
    {
        orden.Parameters.AddWithValue("$numero", ONulo(caso.NumeroCaso));
        orden.Parameters.AddWithValue("$duplicado", ONulo(caso.DuplicadoDe));
        orden.Parameters.AddWithValue("$marcadoPor", ONulo(caso.EstadoMarcadoPor));
        orden.Parameters.AddWithValue("$marcadoEn", ONulo(caso.EstadoMarcadoEn));
        orden.Parameters.AddWithValue("$marcadoOrigen", ONulo(caso.EstadoMarcadoOrigen));
        orden.Parameters.AddWithValue("$delCompanero", ONulo(caso.EstadoDelCompanero));
        orden.Parameters.AddWithValue("$delCompaneroPor", ONulo(caso.EstadoDelCompaneroPor));
        orden.Parameters.AddWithValue("$delCompaneroEn", ONulo(caso.EstadoDelCompaneroEn));
        orden.Parameters.AddWithValue("$unidad", ONulo(caso.UnidadNumero));
        orden.Parameters.AddWithValue("$viaje", ONulo(caso.FechaViaje));
        orden.Parameters.AddWithValue("$manual", caso.CapturaManual ? 1 : 0);
        orden.Parameters.AddWithValue("$archivado", caso.Archivado ? 1 : 0);
        orden.Parameters.AddWithValue("$fechaArchivado", ONulo(caso.FechaArchivado));
        orden.Parameters.AddWithValue("$ruta", ONulo(caso.RutaPdf));
        orden.Parameters.AddWithValue(
            "$creado",
            string.IsNullOrEmpty(caso.CreadoEn) ? AplicadorDeEsquema.MarcaDeTiempo() : caso.CreadoEn);
        orden.Parameters.AddWithValue("$estado", ONulo(caso.EstadoRecomendacion));
        orden.Parameters.AddWithValue("$pagina", ONulo(caso.PaginaPdf));
        orden.Parameters.AddWithValue("$unidadNombre", ONulo(caso.UnidadNombre));
        orden.Parameters.AddWithValue("$templo", ONulo(caso.TemploNombre));
        orden.Parameters.AddWithValue("$motivo", ONulo(caso.MotivoNoCompleta));
        orden.Parameters.AddWithValue("$motivoDelCompanero", ONulo(caso.MotivoDelCompanero));
    }

    /// <summary>Compone el WHERE del filtro, con marcadores para todo valor.</summary>
    private static string ComponerElFiltro(FiltroDeCasos filtro)
    {
        var condiciones = new List<string>();

        if (!filtro.IncluirArchivados)
        {
            condiciones.Add("archivado = 0");
        }

        if (filtro.SoloDeHoy)
        {
            condiciones.Add("fecha_viaje = $hoy");
        }
        else if (filtro.VentanaDeDias is not null)
        {
            // Comparacion de CADENAS, sin funciones de fecha: ARQUITECTURA §1.2 elige
            // TEXT ISO-8601 justo porque ordena cronologicamente como texto.
            condiciones.Add("fecha_viaje >= $hoy AND fecha_viaje <= $hasta");
        }

        if (filtro.SoloVencidos)
        {
            condiciones.Add("fecha_viaje IS NOT NULL AND fecha_viaje < $hoy");
        }

        if (filtro.CompaneroId is not null)
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM asignaciones a WHERE a.caso_id = casos.id " +
                "AND a.companero_id = $companero AND a.activa = 1)");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Busca por numero de caso, y por nombre o MRN de cualquiera de sus
            // personas. Sin distinguir mayusculas, que es lo que espera quien teclea.
            condiciones.Add(
                "(numero_caso LIKE $texto COLLATE NOCASE " +
                "OR EXISTS (SELECT 1 FROM personas p WHERE p.caso_id = casos.id " +
                "AND (p.nombre LIKE $texto COLLATE NOCASE OR p.mrn LIKE $texto COLLATE NOCASE)))");
        }

        if (filtro.Estado is not null)
        {
            condiciones.Add(filtro.Estado == EstadoDeRecomendacion.SinMarcar
                ? "(estado_recomendacion IS NULL OR estado_recomendacion NOT IN ('completa', 'no_completa'))"
                : "estado_recomendacion = $estadoFiltro");
        }

        return condiciones.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condiciones);
    }

    /// <summary>Rellena los marcadores que <c>ComponerElFiltro</c> dejó, y solo esos: un parámetro sin marcador es un error del motor.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="filtro">El mismo filtro con el que se compuso el WHERE.</param>
    /// <remarks><c>$hoy</c> se añade siempre, aunque el WHERE no lo use: un parámetro de más no molesta al motor y así el filtro de hoy, la ventana y los vencidos comparten la misma fecha.</remarks>
    private static void PonerLosParametrosDelFiltro(SqliteCommand orden, FiltroDeCasos filtro)
    {
        var hoy = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        orden.Parameters.AddWithValue("$hoy", hoy);

        if (filtro.VentanaDeDias is not null)
        {
            orden.Parameters.AddWithValue(
                "$hasta",
                DateTime.Now.AddDays(filtro.VentanaDeDias.Value)
                    .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (filtro.CompaneroId is not null)
        {
            orden.Parameters.AddWithValue("$companero", filtro.CompaneroId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Los comodines van en el VALOR, no en la instruccion: asi el texto del
            // usuario sigue siendo un dato y nunca parte de la consulta.
            orden.Parameters.AddWithValue("$texto", "%" + filtro.Texto.Trim() + "%");
        }

        if (filtro.Estado is not null && filtro.Estado != EstadoDeRecomendacion.SinMarcar)
        {
            orden.Parameters.AddWithValue(
                "$estadoFiltro", ONulo(Caso.EscribirEstado(filtro.Estado.Value)));
        }
    }

    /// <summary>Convierte una fila en un caso, columna por columna y en su orden.</summary>
    private static Caso Leer(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        NumeroCaso = TextoONulo(lector, 1),
        DuplicadoDe = LargoONulo(lector, 2),
        EstadoMarcadoPor = LargoONulo(lector, 3),
        EstadoMarcadoEn = TextoONulo(lector, 4),
        EstadoMarcadoOrigen = TextoONulo(lector, 5),
        EstadoDelCompanero = TextoONulo(lector, 6),
        EstadoDelCompaneroPor = LargoONulo(lector, 7),
        EstadoDelCompaneroEn = TextoONulo(lector, 8),
        UnidadNumero = TextoONulo(lector, 9),
        FechaViaje = TextoONulo(lector, 10),
        CapturaManual = Booleano(lector, 11),
        Archivado = Booleano(lector, 12),
        FechaArchivado = TextoONulo(lector, 13),
        RutaPdf = TextoONulo(lector, 14),
        CreadoEn = TextoONulo(lector, 15) ?? string.Empty,
        EstadoRecomendacion = TextoONulo(lector, 16),
        PaginaPdf = EnteroONulo(lector, 17),
        UnidadNombre = TextoONulo(lector, 18),
        TemploNombre = TextoONulo(lector, 19),
        MotivoNoCompleta = TextoONulo(lector, 20),
        MotivoDelCompanero = TextoONulo(lector, 21),
    };
}
