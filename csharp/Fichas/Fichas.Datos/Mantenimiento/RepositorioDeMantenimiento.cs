using System.Globalization;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Mantenimiento;

/// <summary>
/// Lo unico que borra de verdad en toda la base. Copia, cuenta, y solo entonces borra.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ningun texto del usuario se concatena jamas en una instruccion</b>
/// (ARQUITECTURA §1.7). Los ids que se van a borrar no viajan pegados a un <c>IN (...)</c>
/// ni siquiera siendo numeros: se meten uno a uno, como parametros, en la tabla temporal
/// <c>ids_a_borrar</c>, y las instrucciones la leen. Ademas de cerrar la puerta a la
/// inyeccion, esquiva el limite de 999 parametros de SQLite, que con 3 000 documentos
/// marcados no es una hipotesis.
/// </para>
/// <para>
/// ⛔ <b>El orden de las tablas no es indiferente.</b> Cada <c>REFERENCES</c> del esquema
/// va con <c>ON DELETE RESTRICT</c> —a proposito— asi que hay que soltar los hijos antes
/// que los padres. <see cref="Pasos"/> los declara UNA vez en el orden en que se dicen al
/// dueno, y se borran al reves. Que la lista sea la misma para contar y para borrar es lo
/// que garantiza que la cifra de la pregunta sea la cifra que cae.
/// </para>
/// </remarks>
public sealed class RepositorioDeMantenimiento : IMantenimiento
{
    private const string TablaTemporal = "ids_a_borrar";

    private const string ProcedenciaDeLosMarcados =
        "(tabla = 'casos' AND registro_id IN (SELECT id FROM ids_a_borrar)) " +
        "OR (tabla = 'personas' AND registro_id IN " +
        "    (SELECT id FROM personas WHERE caso_id IN (SELECT id FROM ids_a_borrar)))";

    private readonly SqliteConnection _conexion;

    /// <summary>Trabaja sobre la conexion viva del programa, con el esquema aplicado.</summary>
    public RepositorioDeMantenimiento(SqliteConnection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);
        _conexion = conexion;
    }

    /// <summary>
    /// Las tablas que caen cuando cae un documento, en el orden en que se le dicen al dueno.
    /// </summary>
    /// <remarks>
    /// Se BORRAN al reves de como estan aqui, que es hijos primero. <c>filas_descartadas</c>
    /// no cuelga de ningun caso —apunta a un companero— y por eso solo entra en «empezar de
    /// cero»: son restos del Excel de una importacion que ya no tiene documentos detras.
    /// </remarks>
    private static IReadOnlyList<PasoDelBorrado> Pasos { get; } =
    [
        new("casos", "documento", "documentos",
            "id IN (SELECT id FROM ids_a_borrar)", SoloEnTodoEnLimpio: false),
        new("personas", "persona", "personas",
            "caso_id IN (SELECT id FROM ids_a_borrar)", SoloEnTodoEnLimpio: false),
        new("asignaciones", "asignación", "asignaciones",
            "caso_id IN (SELECT id FROM ids_a_borrar)", SoloEnTodoEnLimpio: false),
        new("contactos", "contacto", "contactos",
            "caso_id IN (SELECT id FROM ids_a_borrar)", SoloEnTodoEnLimpio: false),
        new("procedencia_campo", "renglón de procedencia", "renglones de procedencia",
            ProcedenciaDeLosMarcados, SoloEnTodoEnLimpio: false),
        new("documentos_ilegibles", "renglón de ilegible", "renglones de ilegible",
            "caso_id IN (SELECT id FROM ids_a_borrar)", SoloEnTodoEnLimpio: false),
        new("filas_descartadas", "fila devuelta del Excel", "filas devueltas del Excel",
            "1 = 1", SoloEnTodoEnLimpio: true),
    ];

    /// <inheritdoc />
    public PlanDeBorrado PlanearDocumentos(IReadOnlyCollection<long> casoIds)
    {
        ArgumentNullException.ThrowIfNull(casoIds);

        var ids = casoIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new PlanDeBorrado { Alcance = AlcanceDelBorrado.Documentos, SePuedeBorrar = false };
        }

        PonerLosIdsEnLaTablaTemporal(ids);
        return PlanearConLosConteos(AlcanceDelBorrado.Documentos, ids, ContarLosPasos(todoEnLimpio: false));
    }

    /// <inheritdoc />
    public PlanDeBorrado PlanearEmpezarDeCero()
    {
        PonerLosIdsEnLaTablaTemporal([]);
        var conteos = ContarLosPasos(todoEnLimpio: true);

        if (conteos.All(c => c.Filas == 0))
        {
            return new PlanDeBorrado
            {
                Alcance = AlcanceDelBorrado.TodoEnLimpio,
                Conteos = conteos,
                SePuedeBorrar = false,
                Avisos = [Aviso.Informa("La base ya está en limpio: no hay ningún documento que borrar.")],
            };
        }

        return PlanearConLosConteos(AlcanceDelBorrado.TodoEnLimpio, [], conteos);
    }

    /// <inheritdoc />
    public PlanDeBorrado PlanearCompanero(long companeroId)
    {
        var carga = Carga(companeroId);

        if (string.IsNullOrEmpty(carga.Nombre))
        {
            return PlanDeBorrado.NoSePuede(
                AlcanceDelBorrado.Companero,
                null,
                Aviso.Problema(
                    "Ese compañero ya no está en la base.",
                    "companero_id",
                    "Puede que otra ventana lo haya quitado. Vuelve a abrir la lista del equipo."));
        }

        if (!carga.NoLlevaNada)
        {
            return PlanDeBorrado.NoSePuede(
                AlcanceDelBorrado.Companero,
                null,
                Aviso.Advierte(
                    $"{carga.Nombre} no se puede borrar: lleva {carga.Dicho}.",
                    "companero_id",
                    $"Borrarlo perdería el rastro de quién hizo qué. Lo que se hace es " +
                    $"DESACTIVARLO: deja de recibir trabajo nuevo y su nombre sigue en " +
                    $"{carga.Dicho}."));
        }

        var copia = RespaldoAntesDeBorrar.Hacer(_conexion);
        if (!copia.HayCopia)
        {
            return PlanDeBorrado.NoSePuede(AlcanceDelBorrado.Companero, null, copia.Fallo!);
        }

        return new PlanDeBorrado
        {
            Alcance = AlcanceDelBorrado.Companero,
            Ids = [companeroId],
            Nombre = carga.Nombre,
            Conteos = [new ConteoDeTabla("companeros", 1, "compañero", "compañeros")],
            RutaDeLaCopia = copia.Ruta,
            SePuedeBorrar = true,
        };
    }

    /// <inheritdoc />
    public ResultadoDeBorrado Borrar(PlanDeBorrado plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.SePuedeBorrar)
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Problema(
                    "No se borró nada: este borrado no tenía permiso.",
                    string.Empty,
                    "El plan llegó marcado como «no se puede borrar». Vuelve a intentarlo desde el botón."));
        }

        // ⛔ La regla que no se negocia: sin copia previa NO se borra. Ni con permiso.
        if (plan.RutaDeLaCopia is null)
        {
            return ResultadoDeBorrado.NoSeBorro(
                null,
                Aviso.Problema(
                    "No se borró nada: no había copia previa de la base.",
                    string.Empty,
                    "Nada se borra sin haber copiado antes. Libere espacio en el disco y vuelva a intentarlo."));
        }

        try
        {
            return plan.Alcance == AlcanceDelBorrado.Companero
                ? BorrarAlCompanero(plan)
                : BorrarLosDocumentos(plan);
        }
        catch (SqliteException fallo)
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Problema(
                    "No se pudo borrar: la base lo rechazó y se dejó todo como estaba.",
                    string.Empty,
                    $"Detalle del motor: {fallo.Message}. La copia previa está en " +
                    $"«{plan.RutaDeLaCopia}» y no se ha tocado."));
        }
    }

    /// <inheritdoc />
    public CargaDeUnCompanero Carga(long companeroId)
    {
        var nombre = NombreDelCompanero(companeroId);
        if (nombre is null) return CargaDeUnCompanero.NoEsta(companeroId);

        var detalle = new List<ConteoDeTabla>();
        foreach (var (tabla, columna) in ColumnasQueApuntanACompaneros())
        {
            var cuantas = ContarCon(
                $"SELECT COUNT(*) FROM \"{tabla}\" WHERE \"{columna}\" = $id",
                orden => orden.Parameters.AddWithValue("$id", companeroId));

            var (singular, plural) = EtiquetasDeLaCarga.Para(tabla, columna);
            detalle.Add(new ConteoDeTabla(tabla, cuantas, singular, plural));
        }

        return new CargaDeUnCompanero(companeroId, nombre, EstaActivo(companeroId), detalle);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Reactivar(long companeroId)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText =
            "UPDATE companeros SET activo = 1, desactivado_en = NULL WHERE id = $id";
        orden.Parameters.AddWithValue("$id", companeroId);

        return orden.ExecuteNonQuery() == 0
            ? ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "No se encontró a ese compañero.",
                    "companero_id",
                    $"Ninguna fila con el id {companeroId.ToString(CultureInfo.InvariantCulture)}."))
            : ResultadoDeEscritura.Bien(companeroId);
    }

    /// <summary>Borra los documentos del plan, todo o nada, y devuelve lo que de verdad cayo.</summary>
    private ResultadoDeBorrado BorrarLosDocumentos(PlanDeBorrado plan)
    {
        var todoEnLimpio = plan.Alcance == AlcanceDelBorrado.TodoEnLimpio;
        PonerLosIdsEnLaTablaTemporal(plan.Ids);

        using var trato = _conexion.BeginTransaction();
        var borradas = new List<ConteoDeTabla>();

        // Un caso puede senalar a otro con `duplicado_de`, que tambien es un REFERENCES
        // con RESTRICT. Sin soltar esa punta, borrar el original falla en el motor. El que
        // senala NO se borra: se queda sin el «repite a».
        Ejecutar(
            todoEnLimpio
                ? "UPDATE casos SET duplicado_de = NULL WHERE duplicado_de IS NOT NULL"
                : "UPDATE casos SET duplicado_de = NULL WHERE duplicado_de IN (SELECT id FROM ids_a_borrar)",
            trato);

        // Al reves de como se cuentan: los hijos antes que los padres.
        foreach (var paso in Pasos.Reverse())
        {
            if (paso.SoloEnTodoEnLimpio && !todoEnLimpio) continue;

            var cuantas = Ejecutar(
                $"DELETE FROM \"{paso.Tabla}\" WHERE {(todoEnLimpio ? "1 = 1" : paso.Donde)}", trato);
            borradas.Add(new ConteoDeTabla(paso.Tabla, cuantas, paso.Singular, paso.Plural));
        }

        trato.Commit();

        borradas.Reverse();
        return new ResultadoDeBorrado
        {
            SeBorro = true,
            Borradas = borradas,
            RutaDeLaCopia = plan.RutaDeLaCopia,
        };
    }

    /// <summary>Borra al companero, volviendo a comprobar que sigue sin llevar nada.</summary>
    private ResultadoDeBorrado BorrarAlCompanero(PlanDeBorrado plan)
    {
        var companeroId = plan.Ids[0];

        // Se vuelve a contar: entre la pregunta y el «sí» pudo entrar trabajo a su nombre.
        var carga = Carga(companeroId);
        if (!carga.NoLlevaNada)
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Advierte(
                    $"{plan.Nombre} ya no se puede borrar: lleva {carga.Dicho}.",
                    "companero_id",
                    "Algo entró a su nombre desde que se preguntó. Desactívalo en vez de borrarlo."));
        }

        using var orden = _conexion.CreateCommand();
        orden.CommandText = "DELETE FROM companeros WHERE id = $id";
        orden.Parameters.AddWithValue("$id", companeroId);
        var cuantas = orden.ExecuteNonQuery();

        return new ResultadoDeBorrado
        {
            SeBorro = cuantas > 0,
            Borradas = [new ConteoDeTabla("companeros", cuantas, "compañero", "compañeros")],
            RutaDeLaCopia = plan.RutaDeLaCopia,
        };
    }

    /// <summary>Hace la copia y arma el plan; sin copia, el plan no da permiso.</summary>
    private PlanDeBorrado PlanearConLosConteos(
        AlcanceDelBorrado alcance, IReadOnlyList<long> ids, IReadOnlyList<ConteoDeTabla> conteos)
    {
        var copia = RespaldoAntesDeBorrar.Hacer(_conexion);
        if (!copia.HayCopia) return PlanDeBorrado.NoSePuede(alcance, null, copia.Fallo!);

        return new PlanDeBorrado
        {
            Alcance = alcance,
            Ids = ids,
            Conteos = conteos,
            RutaDeLaCopia = copia.Ruta,
            SePuedeBorrar = true,
        };
    }

    /// <summary>Cuenta cuantas filas caeran, paso por paso, con el mismo filtro que borrara.</summary>
    private IReadOnlyList<ConteoDeTabla> ContarLosPasos(bool todoEnLimpio)
    {
        var conteos = new List<ConteoDeTabla>();
        foreach (var paso in Pasos)
        {
            if (paso.SoloEnTodoEnLimpio && !todoEnLimpio) continue;

            var donde = todoEnLimpio ? "1 = 1" : paso.Donde;
            conteos.Add(new ConteoDeTabla(
                paso.Tabla,
                ContarCon($"SELECT COUNT(*) FROM \"{paso.Tabla}\" WHERE {donde}", _ => { }),
                paso.Singular,
                paso.Plural));
        }

        return conteos;
    }

    /// <summary>Deja en la tabla temporal exactamente esos ids y ninguno mas.</summary>
    private void PonerLosIdsEnLaTablaTemporal(IReadOnlyList<long> ids)
    {
        Ejecutar($"CREATE TEMP TABLE IF NOT EXISTS {TablaTemporal} (id INTEGER PRIMARY KEY)", null);
        Ejecutar($"DELETE FROM {TablaTemporal}", null);
        if (ids.Count == 0) return;

        using var trato = _conexion.BeginTransaction();
        using var orden = _conexion.CreateCommand();
        orden.Transaction = trato;
        orden.CommandText = $"INSERT OR IGNORE INTO {TablaTemporal} (id) VALUES ($id)";
        var parametro = orden.CreateParameter();
        parametro.ParameterName = "$id";
        orden.Parameters.Add(parametro);

        foreach (var id in ids)
        {
            parametro.Value = id;
            orden.ExecuteNonQuery();
        }

        trato.Commit();
    }

    /// <summary>Las parejas tabla/columna que apuntan a <c>companeros</c>, segun el motor.</summary>
    private IReadOnlyList<(string Tabla, string Columna)> ColumnasQueApuntanACompaneros()
    {
        var tablas = new List<string>();
        using (var orden = _conexion.CreateCommand())
        {
            orden.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' " +
                "AND name NOT LIKE 'sqlite_%' AND name <> 'companeros' ORDER BY name";
            using var lector = orden.ExecuteReader();
            while (lector.Read()) tablas.Add(lector.GetString(0));
        }

        var parejas = new List<(string, string)>();
        foreach (var tabla in tablas)
        {
            using var orden = _conexion.CreateCommand();
            orden.CommandText = $"PRAGMA foreign_key_list(\"{tabla}\")";
            using var lector = orden.ExecuteReader();
            while (lector.Read())
            {
                if (!string.Equals(lector.GetString(2), "companeros", StringComparison.OrdinalIgnoreCase)) continue;
                parejas.Add((tabla, lector.GetString(3)));
            }
        }

        return parejas;
    }

    private string? NombreDelCompanero(long companeroId)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText = "SELECT nombre FROM companeros WHERE id = $id";
        orden.Parameters.AddWithValue("$id", companeroId);
        return orden.ExecuteScalar() as string;
    }

    private bool EstaActivo(long companeroId)
        => ContarCon(
            "SELECT COUNT(*) FROM companeros WHERE id = $id AND activo = 1",
            orden => orden.Parameters.AddWithValue("$id", companeroId)) > 0;

    private int ContarCon(string consulta, Action<SqliteCommand> ponerParametros)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText = consulta;
        ponerParametros(orden);
        return Convert.ToInt32(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private int Ejecutar(string instruccion, SqliteTransaction? trato)
    {
        using var orden = _conexion.CreateCommand();
        orden.CommandText = instruccion;
        orden.Transaction = trato;
        return orden.ExecuteNonQuery();
    }
}

/// <summary>Una tabla que cae con el documento, con su filtro y como se dice en espanol.</summary>
/// <param name="Tabla">La tabla del esquema.</param>
/// <param name="Singular">Como se dice una fila.</param>
/// <param name="Plural">Como se dicen varias.</param>
/// <param name="Donde">El filtro que selecciona lo que cae; el mismo para contar y para borrar.</param>
/// <param name="SoloEnTodoEnLimpio">Si solo entra cuando se vacia la base entera.</param>
internal sealed record PasoDelBorrado(
    string Tabla, string Singular, string Plural, string Donde, bool SoloEnTodoEnLimpio);

/// <summary>Como se dice, en espanol, lo que un companero lleva por cada columna.</summary>
/// <remarks>
/// Con respaldo por defecto a proposito: las columnas se descubren preguntandole al motor,
/// asi que manana puede aparecer una que no este en esta lista. Antes que callarsela, se
/// dice con el nombre de su tabla: una frase fea es mejor que un «no lleva nada» falso.
/// </remarks>
internal static class EtiquetasDeLaCarga
{
    private static readonly Dictionary<string, (string Singular, string Plural)> Conocidas = new(StringComparer.Ordinal)
    {
        ["asignaciones.companero_id"] = ("asignación", "asignaciones"),
        ["procedencia_campo.verificado_por"] = ("firma", "firmas"),
        ["filas_descartadas.companero_id"] = ("fila devuelta del Excel", "filas devueltas del Excel"),
        ["casos.estado_marcado_por"] = ("documento marcado por él", "documentos marcados por él"),
        ["casos.estado_del_companero_por"] = ("estado devuelto", "estados devueltos"),
        ["personas.propuesto_por"] = ("propuesta sobre una persona", "propuestas sobre personas"),
        ["contactos.contactado_por"] = ("contacto", "contactos"),
    };

    /// <summary>Las dos formas para esa columna, o una frase con el nombre de la tabla.</summary>
    internal static (string Singular, string Plural) Para(string tabla, string columna)
        => Conocidas.TryGetValue($"{tabla}.{columna}", out var etiquetas)
            ? etiquetas
            : ($"fila en «{tabla}»", $"filas en «{tabla}»");
}
