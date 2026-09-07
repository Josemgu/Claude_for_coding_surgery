using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>De donde salio cada valor y quien lo dio por bueno.</summary>
/// <remarks>
/// ⚠️ <b>Regla permanente 5.</b> <see cref="Firmar"/> es el UNICO camino a
/// <c>verificado = 1</c>, exige quien y cuando, y no lo llama ninguna importacion ni
/// ningun Excel: solo el boton que pulsa Miguel. <see cref="Anotar"/> escribe la
/// procedencia y NUNCA pone verificado, aunque el objeto que le pasen lo traiga puesto.
/// El esquema lo respalda con un CHECK, pero aqui se corta antes de llegar al motor
/// para que el aviso diga POR QUE, en vez de devolver un fallo de restriccion.
/// </remarks>
public sealed class RepositorioDeProcedencia : RepositorioBase, IProcedencia
{
    private const string Columnas =
        "id, tabla, registro_id, campo, origen, confianza, valor_ocr, verificado, " +
        "verificado_por, verificado_en, banda_x0, banda_y0, banda_x1, banda_y1, " +
        "anulado_por_tachon, ausente_en_el_papel";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    public RepositorioDeProcedencia(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId)
        => ListarTodo(
            $"SELECT {Columnas} FROM procedencia_campo " +
            "WHERE tabla = $tabla AND registro_id = $registro ORDER BY campo",
            orden =>
            {
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));
                orden.Parameters.AddWithValue("$registro", registroId);
            },
            Leer);

    /// <inheritdoc />
    public PaginaDe<ProcedenciaDeCampo> PorDebajoDeConfianza(double umbral, Pagina trozo)
        => Paginar(
            $"SELECT {Columnas} FROM procedencia_campo " +
            "WHERE confianza IS NOT NULL AND confianza < $umbral " +
            "ORDER BY confianza, tabla, registro_id, campo",
            "SELECT COUNT(*) FROM procedencia_campo " +
            "WHERE confianza IS NOT NULL AND confianza < $umbral",
            orden => orden.Parameters.AddWithValue("$umbral", umbral),
            Leer,
            trozo);

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <b>Las cinco condiciones van con OR y ninguna sobra.</b> Quitar una deja fuera un
    /// caso entero de los seis que se midieron el 2026-09-06, y la pantalla que lea esto dara
    /// por bueno un campo que nadie miro. El orden de las cinco es el mismo que el de
    /// <c>EstadosDeCampo.EsDudoso</c>, para poder leerlas en paralelo.
    /// <para>
    /// No se pagina: es una lectura de pantalla entera. Sobre la base inventada de 3 000
    /// documentos —29 784 filas de procedencia— el filtro deja unas 3 500, que es lo que
    /// hace que quepa en el presupuesto de Inicio.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProcedenciaDeCampo> LasQuePesanEnElVeredicto(double umbral)
        => ListarTodo(
            $"SELECT {Columnas} FROM procedencia_campo " +
            "WHERE verificado = 1 OR ausente_en_el_papel = 1 OR anulado_por_tachon = 1 " +
            "   OR confianza IS NULL OR confianza < $umbral " +
            "ORDER BY tabla, registro_id, campo",
            orden => orden.Parameters.AddWithValue("$umbral", umbral),
            Leer);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla)
    {
        var porRegistro = new Dictionary<long, IReadOnlyList<string>>();

        using var orden = Conexion.CreateCommand();
        orden.CommandText =
            "SELECT registro_id, campo FROM procedencia_campo WHERE tabla = $tabla " +
            "ORDER BY registro_id, campo";
        orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));

        using var lector = orden.ExecuteReader();
        while (lector.Read())
        {
            var registroId = lector.GetInt64(0);
            var campo = TextoONulo(lector, 1) ?? string.Empty;
            if (!porRegistro.TryGetValue(registroId, out var suyos))
            {
                suyos = new List<string>();
                porRegistro[registroId] = suyos;
            }

            ((List<string>)suyos).Add(campo);
        }

        return porRegistro;
    }

    /// <inheritdoc />
    public int ContarVerificados(TablaDeProcedencia tabla, long registroId)
        => ContarCon(
            "SELECT COUNT(*) FROM procedencia_campo " +
            "WHERE tabla = $tabla AND registro_id = $registro AND verificado = 1",
            orden =>
            {
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));
                orden.Parameters.AddWithValue("$registro", registroId);
            });

    /// <inheritdoc />
    public ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia)
    {
        ArgumentNullException.ThrowIfNull(procedencia);

        var avisos = new List<Aviso>();
        if (procedencia.Verificado)
        {
            // No se rechaza la anotacion entera: se anota SIN la firma y se dice. Lo
            // contrario perderia la procedencia por culpa de un campo que ni siquiera
            // hacia falta.
            avisos.Add(Aviso.Advierte(
                "Se anoto la procedencia, pero sin darla por buena.",
                "verificado",
                "Nada se marca como verificado automaticamente (regla permanente 5). " +
                "Firmar un campo es de Miguel, y va por el boton de firmar."));
        }

        // `INSERT OR REPLACE` sobre la clave (tabla, registro_id, campo): volver a
        // extraer el mismo campo pisa lo que habia, que es lo correcto —la extraccion
        // vieja ya no vale—. La firma NO viaja aqui: si el campo estaba firmado, esta
        // instruccion lo devuelve a sin firmar, y eso es lo que tiene que pasar cuando
        // el valor de debajo cambia.
        return Escribir(
            "INSERT OR REPLACE INTO procedencia_campo " +
            "(tabla, registro_id, campo, origen, confianza, valor_ocr, verificado, " +
            "verificado_por, verificado_en, banda_x0, banda_y0, banda_x1, banda_y1, " +
            "anulado_por_tachon, ausente_en_el_papel) " +
            "VALUES ($tabla, $registro, $campo, $origen, $confianza, $valorOcr, 0, " +
            "NULL, NULL, $x0, $y0, $x1, $y1, $tachon, $ausente); SELECT last_insert_rowid()",
            orden =>
            {
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(procedencia.Tabla));
                orden.Parameters.AddWithValue("$registro", procedencia.RegistroId);
                orden.Parameters.AddWithValue("$campo", procedencia.Campo);
                orden.Parameters.AddWithValue("$origen", EscribirOrigen(procedencia.Origen));
                orden.Parameters.AddWithValue("$confianza", ONulo(procedencia.Confianza));
                orden.Parameters.AddWithValue("$valorOcr", ONulo(procedencia.ValorOcr));
                orden.Parameters.AddWithValue("$x0", ONulo(procedencia.BandaX0));
                orden.Parameters.AddWithValue("$y0", ONulo(procedencia.BandaY0));
                orden.Parameters.AddWithValue("$x1", ONulo(procedencia.BandaX1));
                orden.Parameters.AddWithValue("$y1", ONulo(procedencia.BandaY1));
                orden.Parameters.AddWithValue("$tachon", procedencia.AnuladoPorTachon ? 1 : 0);
                orden.Parameters.AddWithValue("$ausente", procedencia.AusenteEnElPapel ? 1 : 0);
            },
            avisos);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Firmar(
        TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn)
    {
        // El UNICO camino a verificado = 1, y exige las dos cosas: quien y cuando.
        if (string.IsNullOrWhiteSpace(verificadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "Para firmar un campo hace falta la fecha.",
                    "verificado_en",
                    "La regla permanente 5 es estructura en el esquema: no se marca " +
                    "verificado sin QUIEN y CUANDO."));
        }

        if (!ExisteElCompanero(companeroId))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema(
                    "No se puede firmar a nombre de alguien que no esta en la base.",
                    "verificado_por",
                    $"No hay ningun companero con el id {companeroId}. Una firma sin " +
                    "nombre no dice quien firmo."));
        }

        return Escribir(
            "UPDATE procedencia_campo SET verificado = 1, verificado_por = $por, " +
            "verificado_en = $cuando " +
            "WHERE tabla = $tabla AND registro_id = $registro AND campo = $campo",
            orden =>
            {
                orden.Parameters.AddWithValue("$por", companeroId);
                orden.Parameters.AddWithValue("$cuando", verificadoEn);
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));
                orden.Parameters.AddWithValue("$registro", registroId);
                orden.Parameters.AddWithValue("$campo", campo);
            },
            [],
            registroId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo)
    {
        // El contrato lo dice con todas las letras: «retirar una firma que no existe no es
        // un error, es que no habia nada que retirar». Pasandolo por el camino normal, una
        // fila que no esta devolvia NoSeEscribio con un PROBLEMA, y esto se llama cada vez
        // que cambia el valor de un campo: la pantalla de Correccion pintaba la franja
        // roja al editar cualquier campo que nadie habia firmado. Medido el 2026-09-05
        // contra el repositorio falso, que ya lo hacia bien.
        if (!EstaFirmado(tabla, registroId, campo))
        {
            return ResultadoDeEscritura.BienCon(
                registroId,
                Aviso.Informa(
                    "No habia ninguna firma que retirar en ese campo.",
                    campo,
                    "Se llama al cambiar un valor, y un campo sin firmar es lo normal."));
        }

        return Escribir(
            "UPDATE procedencia_campo SET verificado = 0, verificado_por = NULL, " +
            "verificado_en = NULL " +
            "WHERE tabla = $tabla AND registro_id = $registro AND campo = $campo",
            orden =>
            {
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));
                orden.Parameters.AddWithValue("$registro", registroId);
                orden.Parameters.AddWithValue("$campo", campo);
            },
            [],
            registroId);
    }

    /// <summary>Si ese campo tiene hoy una firma puesta.</summary>
    private bool EstaFirmado(TablaDeProcedencia tabla, long registroId, string campo)
        => ContarCon(
            "SELECT COUNT(*) FROM procedencia_campo WHERE tabla = $tabla " +
            "AND registro_id = $registro AND campo = $campo AND verificado = 1",
            orden =>
            {
                orden.Parameters.AddWithValue("$tabla", EscribirTabla(tabla));
                orden.Parameters.AddWithValue("$registro", registroId);
                orden.Parameters.AddWithValue("$campo", campo);
            }) > 0;

    private bool ExisteElCompanero(long companeroId)
    {
        using var orden = Conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM companeros WHERE id = $id";
        orden.Parameters.AddWithValue("$id", companeroId);
        return Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>El texto que la columna <c>tabla</c> guarda para cada valor.</summary>
    internal static string EscribirTabla(TablaDeProcedencia tabla) => tabla switch
    {
        TablaDeProcedencia.Personas => "personas",
        _ => "casos",
    };

    /// <summary>Lee el texto de la columna <c>tabla</c> sin lanzar nunca.</summary>
    internal static TablaDeProcedencia LeerTabla(string? texto)
        => string.Equals(texto, "personas", StringComparison.Ordinal)
            ? TablaDeProcedencia.Personas
            : TablaDeProcedencia.Casos;

    /// <summary>El texto que la columna <c>origen</c> guarda para cada valor.</summary>
    internal static string EscribirOrigen(OrigenDeCampo origen) => origen switch
    {
        OrigenDeCampo.Anotacion => "anotacion",
        OrigenDeCampo.Ocr => "ocr",
        OrigenDeCampo.Vacio => "vacio",
        OrigenDeCampo.Manual => "manual",
        _ => "ocr",
    };

    /// <summary>
    /// Lee el texto de la columna <c>origen</c> sin lanzar nunca.
    /// </summary>
    /// <remarks>
    /// Lo que no se reconoce cae en <see cref="OrigenDeCampo.Ocr"/>, que es el valor
    /// mas conservador: dice «lo leyo una maquina», que es lo que obliga a mirarlo.
    /// Caer en <c>Manual</c> afirmaria que alguien lo tecleo, y eso seria inventar.
    /// </remarks>
    internal static OrigenDeCampo LeerOrigen(string? texto) => texto switch
    {
        "anotacion" => OrigenDeCampo.Anotacion,
        "vacio" => OrigenDeCampo.Vacio,
        "manual" => OrigenDeCampo.Manual,
        _ => OrigenDeCampo.Ocr,
    };

    private static ProcedenciaDeCampo Leer(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        Tabla = LeerTabla(TextoONulo(lector, 1)),
        RegistroId = lector.GetInt64(2),
        Campo = TextoONulo(lector, 3) ?? string.Empty,
        Origen = LeerOrigen(TextoONulo(lector, 4)),
        Confianza = DecimalONulo(lector, 5),
        ValorOcr = TextoONulo(lector, 6),
        Verificado = Booleano(lector, 7),
        VerificadoPor = LargoONulo(lector, 8),
        VerificadoEn = TextoONulo(lector, 9),
        BandaX0 = DecimalONulo(lector, 10),
        BandaY0 = DecimalONulo(lector, 11),
        BandaX1 = DecimalONulo(lector, 12),
        BandaY1 = DecimalONulo(lector, 13),
        AnuladoPorTachon = Booleano(lector, 14),
        AusenteEnElPapel = Booleano(lector, 15),
    };
}
