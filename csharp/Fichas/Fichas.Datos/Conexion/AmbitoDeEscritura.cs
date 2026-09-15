using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Conexion;

/// <summary>
/// Varias escrituras de varios repositorios, todo o nada: UNA transacción de SQLite abierta
/// sobre la conexión que los repositorios comparten.
/// </summary>
/// <remarks>
/// <para>Existe por R-4 del plan del 2026-09-15 (PENDIENTES.md): guardar una hoja importada
/// —su caso, sus personas, la procedencia de cada campo y su renglón de ilegibles— eran de
/// 9 a 24 confirmaciones sueltas, cada una con su escritura a disco, y si algo reventaba en
/// la tercera persona el caso y las dos primeras quedaban a medias. Con el ámbito abierto son
/// UNA confirmación, y sin <see cref="Confirmar"/> no queda nada.</para>
///
/// <para><b>Cómo se unen los repositorios: solos.</b> El plan avisaba, sin verificarlo, de que
/// una orden sin <c>Transaction</c> asignada quizá no pudiera ejecutarse con una abierta en la
/// conexión. Medido en <c>PruebaDelAmbitoDeEscritura</c> con Microsoft.Data.Sqlite 10.0.11: es
/// verdad que no puede, PERO <c>SqliteConnection.CreateCommand()</c> asigna a cada orden nueva
/// la transacción abierta en ese momento. Como los repositorios crean su orden en cada llamada,
/// se unen al ámbito sin recibir nada ni saber que esta clase existe: abrir el ámbito sobre su
/// conexión basta. (<c>SqliteConnection.Transaction</c> es <c>protected internal</c>: tampoco
/// podrían leerla para pasársela a una orden guardada, y por eso no se guarda ninguna.)</para>
///
/// <para><b>Lo que NO hace.</b> No convierte en fallo lo que hoy es aviso: una fila que el motor
/// rechaza dentro del ámbito (una cédula repetida en el mismo caso) deshace solo esa orden,
/// como siempre, y el ámbito se confirma con lo demás (requisito 9, «ninguna hoja se rechaza»).
/// Lo que deshace entero es lo que hoy dejaba la base a medias: una excepción que sale de
/// quien guarda antes de confirmar.</para>
///
/// <para>⛔ No anida: abrirlo con otra transacción en curso se rechaza, porque SQLite no anida
/// transacciones y fingirlo con puntos de guardado escondería quién es dueño de la
/// confirmación. Y el respaldo antes de borrar y de migrar no pasa por aquí: siguen con su
/// propia transacción, como estaban.</para>
/// </remarks>
public sealed class AmbitoDeEscritura : IDisposable
{
    /// <summary>La transacción abierta; nula una vez cerrada.</summary>
    private SqliteTransaction? _trato;

    /// <summary>Si ya se confirmó: el segundo <see cref="Confirmar"/> se rechaza y el cierre no deshace nada.</summary>
    private bool _confirmado;

    /// <summary>Privado: se abre con <see cref="Abrir"/>.</summary>
    /// <param name="trato">La transacción recién abierta.</param>
    private AmbitoDeEscritura(SqliteTransaction trato) => _trato = trato;

    /// <summary>Abre una transacción sobre la conexión; lo que se escriba hasta <see cref="Confirmar"/> es todo o nada.</summary>
    /// <param name="conexion">La conexión abierta que comparten los repositorios.</param>
    /// <returns>El ámbito, que hay que cerrar con <c>using</c> haya ido bien o mal.</returns>
    /// <exception cref="InvalidOperationException">Si la conexión ya tiene una transacción abierta —de otro ámbito o abierta a mano—: la lanza el propio <see cref="SqliteConnection.BeginTransaction()"/>, porque el motor no anida y esta clase no lo finge.</exception>
    public static AmbitoDeEscritura Abrir(SqliteConnection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);
        return new AmbitoDeEscritura(conexion.BeginTransaction());
    }

    /// <summary>Confirma todo lo escrito desde que se abrió: una sola escritura a disco.</summary>
    /// <exception cref="InvalidOperationException">Si ya se confirmó o ya se cerró.</exception>
    public void Confirmar()
    {
        if (_confirmado || _trato is null)
        {
            throw new InvalidOperationException("Este ámbito de escritura ya se confirmó o ya se cerró.");
        }

        _trato.Commit();
        _confirmado = true;
    }

    /// <summary>Cierra el ámbito: si no se confirmó, deshace todo lo escrito dentro.</summary>
    public void Dispose()
    {
        if (_trato is null) return;

        // Dispose de SqliteTransaction hace el rollback si no hubo Commit; no hace falta
        // llamarlo aparte, y llamarlo tras Commit lanzaría.
        _trato.Dispose();
        _trato = null;
    }
}
