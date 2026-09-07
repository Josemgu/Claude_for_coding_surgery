using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Un documento COMPLETO en la base: con personas, procedencia, asignacion, contacto y
/// su renglon de ilegible.
/// </summary>
/// <remarks>
/// Existe porque la prueba que importa —«no queda nada colgando»— solo prueba algo si el
/// documento tenia de todo colgando antes. Un caso pelado se borra sin esfuerzo y la
/// prueba pasaria en verde estando mal.
/// </remarks>
internal static class SiembraParaBorrar
{
    /// <summary>Siembra un documento con una fila en cada tabla que cuelga de el.</summary>
    /// <param name="baseDePrueba">La base sobre la que se siembra.</param>
    /// <param name="numeroCaso">El numero que llevara el documento.</param>
    /// <param name="companeroId">Quien firma y a quien se asigna.</param>
    /// <param name="cuantasPersonas">Cuantas personas se le cuelgan.</param>
    /// <returns>El id del caso sembrado.</returns>
    internal static long UnDocumentoCompleto(
        BaseDePrueba baseDePrueba, string numeroCaso, long companeroId, int cuantasPersonas = 2)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var personas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var asignaciones = new RepositorioDeAsignaciones(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);
        var ilegibles = new RepositorioDeIlegibles(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            UnidadNumero = "7000011",
            FechaViaje = "2026-10-08",
            RutaPdf = $@"C:\escaneos\{numeroCaso}.pdf",
            CreadoEn = "2026-09-05 09:00:00",
        });
        Assert.IsTrue(caso.SeEscribio, $"No se pudo sembrar el caso {numeroCaso}.");

        for (var fila = 1; fila <= cuantasPersonas; fila++)
        {
            var persona = personas.Guardar(new Persona
            {
                CasoId = caso.Id,
                Nombre = $"Persona {fila} de {numeroCaso}",
                FilaFormulario = fila,
            });
            Assert.IsTrue(persona.SeEscribio, "No se pudo sembrar una persona.");

            var deLaPersona = procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = TablaDeProcedencia.Personas,
                RegistroId = persona.Id,
                Campo = "nombre",
                Origen = OrigenDeCampo.Ocr,
                Confianza = 0.91,
            });
            Assert.IsTrue(deLaPersona.SeEscribio, "No se pudo sembrar la procedencia de la persona.");

            // La firma va por `Firmar` y NO por `Anotar`: la regla permanente 5 impide que
            // guardar un dato lo deje verificado de paso. Se siembra firmada a proposito,
            // porque una firma es justo lo que hace que un companero NO se pueda borrar.
            var firma = procedencia.Firmar(
                TablaDeProcedencia.Personas, persona.Id, "nombre", companeroId, "2026-09-05 09:05:00");
            Assert.IsTrue(firma.SeEscribio, "No se pudo sembrar la firma de la persona.");
        }

        var delCaso = procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = caso.Id,
            Campo = "fecha_viaje",
            Origen = OrigenDeCampo.Anotacion,
        });
        Assert.IsTrue(delCaso.SeEscribio, "No se pudo sembrar la procedencia del caso.");

        var asignada = asignaciones.Asignar(caso.Id, companeroId, "2026-09-05 09:10:00");
        Assert.IsTrue(asignada.SeEscribio, "No se pudo sembrar la asignacion.");

        var renglon = ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = $@"C:\escaneos\{numeroCaso}.pdf",
            PaginaPdf = 2,
            Motivo = "pagina_en_blanco",
            CasoId = caso.Id,
            RegistradoEn = "2026-09-05 09:15:00",
        });
        Assert.IsTrue(renglon.SeEscribio, "No se pudo sembrar el renglon de ilegible.");

        SembrarUnContacto(baseDePrueba, caso.Id, companeroId);

        return caso.Id;
    }

    /// <summary>
    /// Un contacto, por SQL directo: no hay repositorio de contactos todavia.
    /// </summary>
    /// <remarks>
    /// Se siembra igual porque <c>contactos</c> apunta a <c>casos</c> con
    /// <c>ON DELETE RESTRICT</c>: un borrado que se olvide de esta tabla falla en el motor,
    /// y esta prueba existe para que falle AQUI y no en la maquina del dueno.
    /// </remarks>
    internal static void SembrarUnContacto(BaseDePrueba baseDePrueba, long casoId, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO contactos (caso_id, fecha, medio, con_quien, resultado, " +
            "registrado_en, contactado_por) " +
            "VALUES ($caso, $fecha, $medio, $quien, $resultado, $registrado, $por)";
        orden.Parameters.AddWithValue("$caso", casoId);
        orden.Parameters.AddWithValue("$fecha", "2026-09-05");
        orden.Parameters.AddWithValue("$medio", "telefono");
        orden.Parameters.AddWithValue("$quien", "el obispo");
        orden.Parameters.AddWithValue("$resultado", "no contesta");
        orden.Parameters.AddWithValue("$registrado", "2026-09-05 09:20:00");
        orden.Parameters.AddWithValue("$por", companeroId);
        orden.ExecuteNonQuery();
    }

    /// <summary>Deja dicho que un caso repite a otro, que es un <c>REFERENCES casos</c> mas.</summary>
    internal static void MarcarComoDuplicado(BaseDePrueba baseDePrueba, long casoId, long deCualId)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "UPDATE casos SET duplicado_de = $de WHERE id = $id";
        orden.Parameters.AddWithValue("$de", deCualId);
        orden.Parameters.AddWithValue("$id", casoId);
        orden.ExecuteNonQuery();
    }

    /// <summary>Cuantas filas de <c>procedencia_campo</c> apuntan a ese caso o a su gente.</summary>
    internal static long ProcedenciaQueCuelgaDe(BaseDePrueba baseDePrueba, long casoId)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText =
            "SELECT COUNT(*) FROM procedencia_campo " +
            "WHERE (tabla = 'casos' AND registro_id = $caso) " +
            "   OR (tabla = 'personas' AND registro_id IN " +
            "       (SELECT id FROM personas WHERE caso_id = $caso))";
        orden.Parameters.AddWithValue("$caso", casoId);
        return Convert.ToInt64(
            orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Que el motor no encuentra ni una fila huerfana en TODA la base.</summary>
    /// <remarks>
    /// <c>PRAGMA foreign_key_check</c> recorre todas las tablas y devuelve una fila por cada
    /// referencia rota. Se pregunta al motor en vez de mirar tabla por tabla a mano porque
    /// una lista escrita a mano se queda corta el dia que alguien anada una columna nueva
    /// que apunte a <c>casos</c>, y entonces la prueba diria que si estando mal.
    /// </remarks>
    internal static void NoQuedaNadaColgando(BaseDePrueba baseDePrueba)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "PRAGMA foreign_key_check";
        using var lector = orden.ExecuteReader();

        var rotas = new List<string>();
        while (lector.Read())
        {
            rotas.Add($"{lector.GetValue(0)} fila {lector.GetValue(1)} → {lector.GetValue(2)}");
        }

        Assert.IsEmpty(
            rotas, "Quedaron referencias rotas después de borrar: " + string.Join("; ", rotas));
    }
}
