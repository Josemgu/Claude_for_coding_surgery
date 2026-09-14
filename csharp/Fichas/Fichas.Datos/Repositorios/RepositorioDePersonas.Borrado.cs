using System.Globalization;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Mantenimiento;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>
/// La mitad de <see cref="RepositorioDePersonas"/> que BORRA una persona con lo que cuelga
/// de ella.
/// </summary>
/// <remarks>
/// <para>Va en su propio archivo por el mismo reparto que
/// <c>RepositorioDeIlegibles.Borrado.cs</c>: la otra mitad guarda y consulta, y esto es lo
/// único que quita filas. Que se vea de un vistazo cuál es el archivo que quita filas vale más
/// que tenerlo todo junto; y la otra mitad ya pasaba de 400 líneas.</para>
///
/// <para><b>Lo que cuelga de una persona, medido en el esquema el 2026-09-14</b>
/// (<c>grep -rn "REFERENCES" Fichas.Datos/Esquema</c>): ninguna tabla apunta a
/// <c>personas</c> con clave foránea. Lo que sí cuelga, sin clave, son los renglones de
/// <c>procedencia_campo</c> con <c>tabla = 'personas'</c> y <c>registro_id</c> igual a su id:
/// son los que dicen de dónde salió su nombre y su cédula, y sin borrarlos aquí quedarían
/// huérfanos apuntando a una fila que ya no existe. Las seis respuestas del sistema del líder y
/// su firma (<c>pasos_*</c>) son columnas de la propia fila y se van con ella.</para>
///
/// <para>⛔ <b>Ningún texto del usuario se concatena en una instrucción</b>: el id viaja como
/// parámetro. Y <b>sin copia previa no se borra</b>, la misma regla que
/// <c>RepositorioDeMantenimiento</c>: la copia la hace <see cref="RespaldoAntesDeBorrar"/> con la
/// API de respaldo en línea de SQLite, y si no se puede hacer, se devuelve el motivo y no se
/// toca nada.</para>
/// </remarks>
public sealed partial class RepositorioDePersonas
{
    /// <inheritdoc />
    public ResultadoDeBorrado Borrar(long personaId)
    {
        if (!Existe(personaId))
        {
            // Sin nada que borrar no se copia la base: una copia por una pulsación que no
            // borra nada es un archivo que aparece sin motivo.
            return ResultadoDeBorrado.NoSeBorro(
                null,
                Aviso.Problema(
                    "Esa persona ya no está en la base: no se borró nada.",
                    "persona_id",
                    $"Ninguna fila de personas con el número interno {personaId.ToString(CultureInfo.InvariantCulture)}. "
                    + "Puede que otra ventana la haya quitado. Vuelva a abrir el documento."));
        }

        var copia = RespaldoAntesDeBorrar.Hacer(Conexion);
        if (!copia.HayCopia) return ResultadoDeBorrado.NoSeBorro(null, copia.Fallo!);

        try
        {
            return BorrarConSuProcedencia(personaId, copia.Ruta!);
        }
        catch (SqliteException fallo)
        {
            return ResultadoDeBorrado.NoSeBorro(
                copia.Ruta,
                Aviso.Problema(
                    "No se pudo borrar a la persona: la base lo rechazó y se dejó todo como estaba.",
                    "persona_id",
                    $"Detalle del motor: {fallo.Message}. La copia previa está en «{copia.Ruta}» y no se ha tocado."));
        }
    }

    /// <summary>Borra los renglones de procedencia de la persona y después su fila, en una transacción.</summary>
    /// <param name="personaId">La persona, que ya se comprobó que existe.</param>
    /// <param name="rutaDeLaCopia">Dónde quedó la copia previa, para que el resultado la nombre.</param>
    /// <returns>Borrado con las cifras que devolvió el motor: primero la persona, después su procedencia.</returns>
    private ResultadoDeBorrado BorrarConSuProcedencia(long personaId, string rutaDeLaCopia)
    {
        using var trato = Conexion.BeginTransaction();

        // Los hijos antes que el padre, aunque aquí no haya clave foránea que lo exija: es el
        // mismo orden que en RepositorioDeMantenimiento, y así una futura clave no lo rompe.
        var procedencia = EjecutarConElId(
            "DELETE FROM procedencia_campo WHERE tabla = 'personas' AND registro_id = $id", personaId, trato);
        var personas = EjecutarConElId("DELETE FROM personas WHERE id = $id", personaId, trato);

        trato.Commit();

        return new ResultadoDeBorrado
        {
            SeBorro = personas > 0,
            Borradas =
            [
                new ConteoDeTabla("personas", personas, "persona", "personas"),
                new ConteoDeTabla("procedencia_campo", procedencia, "renglón de procedencia", "renglones de procedencia"),
            ],
            RutaDeLaCopia = rutaDeLaCopia,
        };
    }

    /// <summary>Si hay una fila de <c>personas</c> con ese id.</summary>
    /// <param name="personaId">El número interno.</param>
    private bool Existe(long personaId)
        => ContarCon(
            "SELECT COUNT(*) FROM personas WHERE id = $id",
            orden => orden.Parameters.AddWithValue("$id", personaId)) > 0;

    /// <summary>Ejecuta una instrucción con el id como único parámetro, dentro de la transacción.</summary>
    /// <param name="instruccion">La instrucción; sin texto del usuario dentro.</param>
    /// <param name="personaId">El id que rellena <c>$id</c>.</param>
    /// <param name="trato">La transacción en curso.</param>
    /// <returns>Cuántas filas tocó.</returns>
    private int EjecutarConElId(string instruccion, long personaId, SqliteTransaction trato)
    {
        using var orden = Conexion.CreateCommand();
        orden.CommandText = instruccion;
        orden.Transaction = trato;
        orden.Parameters.AddWithValue("$id", personaId);
        return orden.ExecuteNonQuery();
    }
}
