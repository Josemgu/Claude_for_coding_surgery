using System.Globalization;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Mantenimiento;

/// <summary>
/// Unificar un duplicado con su original: pasar lo que falta, retirar lo vivo y borrar el
/// duplicado por el camino de siempre. Todo o nada.
/// </summary>
/// <remarks>
/// <para>Lo que pasa y lo que no lo decide <see cref="LoQuePasaAlOriginal"/>, que no consulta
/// nada; aquí solo se lee, se aplica y se borra. Las dos lecturas —la de planear y la de
/// ejecutar— pasan por la misma regla para que la pregunta y lo que se escribe no se separen.</para>
///
/// <para>⚠️ <b>Ningún texto del usuario se concatena en una instrucción.</b> Los nombres de
/// columna que aparecen pegados salen de <see cref="PlanDeUnificacion.LosCincoCampos"/>, una
/// lista escrita en el código; los valores van siempre como parámetros.</para>
/// </remarks>
public sealed partial class RepositorioDeMantenimiento
{
    /// <inheritdoc />
    public PlanDeUnificacion PlanearUnificacion(long duplicadoId, string hoy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hoy);

        var casos = new RepositorioDeCasos(_conexion);
        var duplicado = casos.Obtener(duplicadoId);
        if (duplicado is null)
        {
            return PlanDeUnificacion.NoSePuede(duplicadoId, null, Aviso.Problema(
                "Ese documento ya no está en la base.", nameof(Caso.Id),
                "Puede que otra ventana lo haya borrado. Vuelve a abrir Revisar."));
        }

        if (duplicado.DuplicadoDe is not long originalId)
        {
            return PlanDeUnificacion.NoSePuede(duplicadoId, null, Aviso.Problema(
                "Ese documento no está marcado como duplicado: no hay con qué unificarlo.",
                nameof(Caso.DuplicadoDe)));
        }

        var original = casos.Obtener(originalId);
        if (original is null || original.Id == duplicado.Id)
        {
            return PlanDeUnificacion.NoSePuede(duplicadoId, null, Aviso.Advierte(
                "El documento del que este es duplicado ya no está en la base.",
                nameof(Caso.DuplicadoDe),
                "No hay con qué unificarlo. Quítale la marca de duplicado y pasa a ser un documento normal."));
        }

        var personas = new RepositorioDePersonas(_conexion);
        var reparto = LoQuePasaAlOriginal.Personas(personas.DeCaso(original.Id), personas.DeCaso(duplicado.Id));
        var campos = LoQuePasaAlOriginal.Campos(original, duplicado);

        var copia = RespaldoAntesDeBorrar.Hacer(_conexion);
        if (!copia.HayCopia) return PlanDeUnificacion.NoSePuede(duplicadoId, null, copia.Fallo!);

        return new PlanDeUnificacion
        {
            DuplicadoId = duplicado.Id,
            OriginalId = original.Id,
            DuplicadoDicho = Dicho(duplicado),
            OriginalDicho = Dicho(original),
            OriginalArchivado = original.Archivado,
            PersonasQuePasan = reparto.Pasan,
            PersonasQueYaEstaban = reparto.YaEstaban,
            CamposQuePasan = campos,
            AsignacionesVivasQueSeRetiran = ContarCon(
                "SELECT COUNT(*) FROM asignaciones WHERE caso_id = $id AND activa = 1",
                orden => orden.Parameters.AddWithValue("$id", duplicado.Id)),
            Hoy = hoy,
            RutaDeLaCopia = copia.Ruta,
            SePuedeUnificar = true,
        };
    }

    /// <inheritdoc />
    public ResultadoDeUnificacion Unificar(PlanDeUnificacion plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.SePuedeUnificar)
        {
            return ResultadoDeUnificacion.NoSeUnifico(plan.RutaDeLaCopia, Aviso.Problema(
                "No se unificó nada: este plan no tenía permiso.", string.Empty,
                "El plan llegó marcado como «no se puede unificar». Vuelve a intentarlo desde el botón."));
        }

        // ⛔ La misma regla que al borrar: sin copia previa NO se toca la base. Ni con permiso.
        if (plan.RutaDeLaCopia is null)
        {
            return ResultadoDeUnificacion.NoSeUnifico(null, Aviso.Problema(
                "No se unificó nada: no había copia previa de la base.", string.Empty,
                "Nada se borra sin haber copiado antes. Libere espacio en el disco y vuelva a intentarlo."));
        }

        var casos = new RepositorioDeCasos(_conexion);
        var duplicado = casos.Obtener(plan.DuplicadoId);
        var original = casos.Obtener(plan.OriginalId);
        if (duplicado is null || original is null || duplicado.DuplicadoDe != original.Id)
        {
            return ResultadoDeUnificacion.NoSeUnifico(plan.RutaDeLaCopia, Aviso.Problema(
                "No se unificó nada: uno de los dos documentos ya no está como estaba.", nameof(Caso.DuplicadoDe),
                "Algo cambió entre la pregunta y el «sí». Vuelve a abrir Revisar y mira los dos documentos."));
        }

        try
        {
            return UnificarLosDos(plan, original, duplicado);
        }
        catch (SqliteException fallo)
        {
            return ResultadoDeUnificacion.NoSeUnifico(plan.RutaDeLaCopia, Aviso.Problema(
                "No se pudo unificar: la base lo rechazó y se dejó todo como estaba.", string.Empty,
                $"Detalle del motor: {fallo.Message}. La copia previa está en «{plan.RutaDeLaCopia}» y no se ha tocado."));
        }
    }

    /// <inheritdoc />
    public ResultadoDeEscritura QuitarLaMarcaDeDuplicado(long casoId)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText = "UPDATE casos SET duplicado_de = NULL WHERE id = $id";
        orden.Parameters.AddWithValue("$id", casoId);

        return orden.ExecuteNonQuery() == 0
            ? ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se encontró ese documento.", nameof(Caso.Id),
                $"Ninguna fila con el id {casoId.ToString(CultureInfo.InvariantCulture)}."))
            : ResultadoDeEscritura.Bien(casoId);
    }

    /// <summary>Hace la unificación en una transacción, sobre lo que hay AHORA en la base.</summary>
    /// <param name="plan">El plan con permiso y copia.</param>
    /// <param name="original">El original recién leído.</param>
    /// <param name="duplicado">El duplicado recién leído.</param>
    /// <returns>Lo que pasó y lo que cayó.</returns>
    private ResultadoDeUnificacion UnificarLosDos(PlanDeUnificacion plan, Caso original, Caso duplicado)
    {
        var personas = new RepositorioDePersonas(_conexion);
        var reparto = LoQuePasaAlOriginal.Personas(personas.DeCaso(original.Id), personas.DeCaso(duplicado.Id));
        var campos = LoQuePasaAlOriginal.Campos(original, duplicado);

        // La tabla temporal se llena FUERA de la transacción: llenarla abre la suya.
        PonerLosIdsEnLaTablaTemporal([duplicado.Id]);

        using var trato = _conexion.BeginTransaction();

        foreach (var persona in reparto.Pasan) PasarLaPersona(trato, persona.Id, original.Id);
        foreach (var campo in campos) PasarElCampo(trato, campo, original.Id, duplicado.Id);
        var retiradas = RetirarYPasarLasAsignaciones(trato, original.Id, duplicado.Id, plan.Hoy);
        PasarLosContactos(trato, original.Id, duplicado.Id);
        ResenalarALosQueSenalabanAlDuplicado(trato, original.Id, duplicado.Id);

        var borradas = BorrarLosPasos(trato, todoEnLimpio: false);
        trato.Commit();

        return new ResultadoDeUnificacion
        {
            SeUnifico = true,
            OriginalId = original.Id,
            DuplicadoId = duplicado.Id,
            OriginalDicho = Dicho(original),
            DuplicadoDicho = Dicho(duplicado),
            PersonasQuePasaron = reparto.Pasan.Count,
            CamposQuePasaron = campos.Count,
            AsignacionesRetiradas = retiradas,
            Borradas = borradas,
            RutaDeLaCopia = plan.RutaDeLaCopia,
        };
    }

    /// <summary>Cambia una persona de caso; su procedencia y su hoja cuelgan de su id y viajan solas.</summary>
    /// <param name="trato">La transacción.</param>
    /// <param name="personaId">La persona.</param>
    /// <param name="originalId">A qué caso pasa.</param>
    private void PasarLaPersona(SqliteTransaction trato, long personaId, long originalId)
        => EjecutarCon(
            "UPDATE personas SET caso_id = $original WHERE id = $persona", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$persona", personaId);
            });

    /// <summary>
    /// Escribe en el original el valor que traía el duplicado, y pasa con él su fila de procedencia.
    /// </summary>
    /// <remarks>
    /// La fila de procedencia del original para ese campo, si existe, se retira antes: era la del
    /// vacío, y <c>UNIQUE (tabla, registro_id, campo)</c> no deja dos. La que entra es la del
    /// duplicado, que dice de dónde salió el valor de verdad (OCR, anotación o mano).
    /// </remarks>
    /// <param name="trato">La transacción.</param>
    /// <param name="campo">El campo, con su columna y su valor.</param>
    /// <param name="originalId">El original.</param>
    /// <param name="duplicadoId">El duplicado.</param>
    private void PasarElCampo(SqliteTransaction trato, CampoQuePasaAlOriginal campo, long originalId, long duplicadoId)
    {
        // El nombre de columna sale de la lista escrita en el código; se comprueba igual.
        if (!PlanDeUnificacion.LosCincoCampos.Any(c => string.Equals(c.Columna, campo.Columna, StringComparison.Ordinal)))
        {
            throw new ArgumentOutOfRangeException(nameof(campo), campo.Columna, "No es uno de los cinco campos que se unifican.");
        }

        EjecutarCon(
            $"UPDATE casos SET \"{campo.Columna}\" = $valor WHERE id = $original", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$valor", campo.Valor);
                orden.Parameters.AddWithValue("$original", originalId);
            });
        EjecutarCon(
            "DELETE FROM procedencia_campo WHERE tabla = 'casos' AND registro_id = $original AND campo = $campo", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$campo", campo.Columna);
            });
        EjecutarCon(
            "UPDATE procedencia_campo SET registro_id = $original WHERE tabla = 'casos' AND registro_id = $duplicado AND campo = $campo", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$duplicado", duplicadoId);
                orden.Parameters.AddWithValue("$campo", campo.Columna);
            });
    }

    /// <summary>
    /// Desactiva con fecha las asignaciones vivas del duplicado y pasa todas —vivas y retiradas— al original.
    /// </summary>
    /// <remarks>
    /// Se pasan y no se borran para que el informe del agente siga diciendo lo que llevó; se
    /// retiran las vivas porque el original no hereda a nadie: quién lo lleva lo decide Miguel.
    /// Con las vivas ya a 0 no choca el índice de una viva por (caso, compañero).
    /// </remarks>
    /// <param name="trato">La transacción.</param>
    /// <param name="originalId">El original.</param>
    /// <param name="duplicadoId">El duplicado.</param>
    /// <param name="hoy">La fecha de la retirada.</param>
    /// <returns>Cuántas vivas se retiraron.</returns>
    private int RetirarYPasarLasAsignaciones(SqliteTransaction trato, long originalId, long duplicadoId, string hoy)
    {
        var retiradas = EjecutarCon(
            "UPDATE asignaciones SET activa = 0, desactivada_en = $hoy WHERE caso_id = $duplicado AND activa = 1", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$hoy", hoy);
                orden.Parameters.AddWithValue("$duplicado", duplicadoId);
            });
        EjecutarCon(
            "UPDATE asignaciones SET caso_id = $original WHERE caso_id = $duplicado", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$duplicado", duplicadoId);
            });
        return retiradas;
    }

    /// <summary>Los contactos hechos por el duplicado son contactos por la misma familia: pasan al original.</summary>
    /// <param name="trato">La transacción.</param>
    /// <param name="originalId">El original.</param>
    /// <param name="duplicadoId">El duplicado.</param>
    private void PasarLosContactos(SqliteTransaction trato, long originalId, long duplicadoId)
        => EjecutarCon(
            "UPDATE contactos SET caso_id = $original WHERE caso_id = $duplicado", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$duplicado", duplicadoId);
            });

    /// <summary>Un tercero que señalaba al duplicado pasa a señalar al original, que es de quien repite de verdad.</summary>
    /// <param name="trato">La transacción.</param>
    /// <param name="originalId">El original.</param>
    /// <param name="duplicadoId">El duplicado.</param>
    private void ResenalarALosQueSenalabanAlDuplicado(SqliteTransaction trato, long originalId, long duplicadoId)
        => EjecutarCon(
            "UPDATE casos SET duplicado_de = $original WHERE duplicado_de = $duplicado", trato,
            orden =>
            {
                orden.Parameters.AddWithValue("$original", originalId);
                orden.Parameters.AddWithValue("$duplicado", duplicadoId);
            });

    /// <summary>«CASP2609_Ana_Prueba.pdf hoja 2»: el archivo sin ruta y la hoja, o «sin archivo».</summary>
    /// <param name="caso">El documento.</param>
    private static string Dicho(Caso caso)
    {
        var ruta = caso.RutaPdf;
        var archivo = string.IsNullOrWhiteSpace(ruta) ? "sin archivo" : ruta[(ruta.LastIndexOfAny(['\\', '/']) + 1)..];
        var hoja = caso.PaginaPdf is int pagina ? $" hoja {pagina.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        return archivo + hoja;
    }

    /// <summary>Ejecuta una instrucción con parámetros dentro de la transacción y devuelve cuántas filas tocó.</summary>
    /// <param name="instruccion">La instrucción; sin texto del usuario dentro.</param>
    /// <param name="trato">La transacción en curso.</param>
    /// <param name="ponerParametros">Dónde se añaden los parámetros.</param>
    private int EjecutarCon(string instruccion, SqliteTransaction trato, Action<SqliteCommand> ponerParametros)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText = instruccion;
        orden.Transaction = trato;
        ponerParametros(orden);
        return orden.ExecuteNonQuery();
    }
}
