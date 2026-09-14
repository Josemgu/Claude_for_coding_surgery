using System.Globalization;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Un documento con sus personas por cédula, cada fila con su procedencia y su hoja, para
/// las pruebas de unificar un duplicado con su original.
/// </summary>
/// <remarks>
/// Va aparte de <see cref="SiembraParaBorrar"/> porque aquella no deja elegir las cédulas ni
/// la hoja, y unificar decide justo por la cédula y conserva justo la hoja. Un documento
/// sembrado sin cédulas no probaría la regla de pareja.
/// </remarks>
internal static class SiembraParaUnificar
{
    /// <summary>Siembra un documento con esas personas, cada una con su fila de procedencia.</summary>
    /// <param name="baseDePrueba">La base sobre la que se siembra.</param>
    /// <param name="numeroCaso">El número del papel.</param>
    /// <param name="rutaPdf">El archivo del que salió.</param>
    /// <param name="hoja">La hoja del PDF, base 1.</param>
    /// <param name="fechaViaje">La fecha de viaje, o nula si no la trae.</param>
    /// <param name="templo">El nombre del templo, o nulo.</param>
    /// <param name="personas">Parejas (nombre, cédula); la cédula puede ser nula.</param>
    /// <returns>El id del caso sembrado.</returns>
    internal static long UnDocumento(
        BaseDePrueba baseDePrueba,
        string numeroCaso,
        string rutaPdf,
        int hoja,
        string? fechaViaje,
        string? templo,
        params (string Nombre, string? Mrn)[] personas)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var repositorioDePersonas = new RepositorioDePersonas(baseDePrueba.Conexion);
        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);

        var caso = casos.Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            UnidadNumero = "7000011",
            FechaViaje = fechaViaje,
            TemploNombre = templo,
            RutaPdf = rutaPdf,
            PaginaPdf = hoja,
            CreadoEn = "2026-09-14 09:00:00",
        });
        Assert.IsTrue(caso.SeEscribio, $"No se pudo sembrar el caso {numeroCaso}.");

        var fila = 0;
        foreach (var (nombre, mrn) in personas)
        {
            fila++;
            var persona = repositorioDePersonas.Guardar(new Persona
            {
                CasoId = caso.Id,
                Nombre = nombre,
                Mrn = mrn,
                FilaFormulario = fila,
                PaginaPdf = hoja,
            });
            Assert.IsTrue(persona.SeEscribio, $"No se pudo sembrar a {nombre}.");

            var deLaPersona = procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = TablaDeProcedencia.Personas,
                RegistroId = persona.Id,
                Campo = "nombre",
                Origen = OrigenDeCampo.Ocr,
                Confianza = 0.93,
            });
            Assert.IsTrue(deLaPersona.SeEscribio, "No se pudo sembrar la procedencia de la persona.");
        }

        AnotarProcedenciaDelCaso(baseDePrueba, caso.Id, "fecha_viaje",
            fechaViaje is null ? OrigenDeCampo.Vacio : OrigenDeCampo.Ocr);

        return caso.Id;
    }

    /// <summary>Deja dicho de dónde salió un campo del caso.</summary>
    /// <param name="baseDePrueba">La base.</param>
    /// <param name="casoId">El documento.</param>
    /// <param name="campo">La columna, tal como está en la base.</param>
    /// <param name="origen">De dónde salió.</param>
    internal static void AnotarProcedenciaDelCaso(BaseDePrueba baseDePrueba, long casoId, string campo, OrigenDeCampo origen)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        var procedencia = new RepositorioDeProcedencia(baseDePrueba.Conexion);
        var anotada = procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = campo,
            Origen = origen,
            Confianza = origen == OrigenDeCampo.Ocr ? 0.88 : null,
        });
        Assert.IsTrue(anotada.SeEscribio, $"No se pudo sembrar la procedencia de {campo}.");
    }

    /// <summary>Un valor de una columna de <c>casos</c>, leído a pelo.</summary>
    /// <param name="baseDePrueba">La base.</param>
    /// <param name="casoId">El documento.</param>
    /// <param name="columna">La columna; sale de una constante de la prueba, nunca de fuera.</param>
    internal static string? ValorDelCaso(BaseDePrueba baseDePrueba, long casoId, string columna)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = $"SELECT \"{columna}\" FROM casos WHERE id = $id";
        orden.Parameters.AddWithValue("$id", casoId);
        return orden.ExecuteScalar() as string;
    }

    /// <summary>Cuántas filas cumplen una condición escrita en la prueba, con un parámetro.</summary>
    /// <param name="baseDePrueba">La base.</param>
    /// <param name="consulta">Un <c>SELECT COUNT(*)</c> con <c>$id</c> dentro.</param>
    /// <param name="id">El valor de <c>$id</c>.</param>
    internal static long Contar(BaseDePrueba baseDePrueba, string consulta, long id)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = consulta;
        orden.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Deja a un caso señalando a un original que NO está: la situación que la marca «duplicado
    /// de un documento que ya no está en la base» contempla.
    /// </summary>
    /// <remarks>
    /// Con las claves foráneas encendidas el motor no deja escribir un id que no existe, y el
    /// borrado de siempre suelta esa punta antes de borrar. Se apaga el candado solo para esta
    /// escritura, que es la forma de fabricar en una prueba lo que solo una base vieja traería.
    /// </remarks>
    /// <param name="baseDePrueba">La base.</param>
    /// <param name="casoId">El caso que va a quedar huérfano.</param>
    /// <param name="idQueNoExiste">Un id que no está en <c>casos</c>.</param>
    internal static void SenalarAUnOriginalQueNoEsta(BaseDePrueba baseDePrueba, long casoId, long idQueNoExiste)
    {
        ArgumentNullException.ThrowIfNull(baseDePrueba);

        using var apagar = baseDePrueba.Conexion.CreateCommand();
        apagar.CommandText = "PRAGMA foreign_keys = OFF";
        apagar.ExecuteNonQuery();

        using var orden = baseDePrueba.Conexion.CreateCommand();
        orden.CommandText = "UPDATE casos SET duplicado_de = $de WHERE id = $id";
        orden.Parameters.AddWithValue("$de", idQueNoExiste);
        orden.Parameters.AddWithValue("$id", casoId);
        orden.ExecuteNonQuery();

        using var encender = baseDePrueba.Conexion.CreateCommand();
        encender.CommandText = "PRAGMA foreign_keys = ON";
        encender.ExecuteNonQuery();
    }
}
