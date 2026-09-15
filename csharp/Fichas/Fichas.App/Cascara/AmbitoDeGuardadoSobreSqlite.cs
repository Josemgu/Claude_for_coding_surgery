using Fichas.App.Importar;
using Fichas.Datos.Conexion;
using Microsoft.Data.Sqlite;

namespace Fichas.App.Cascara;

/// <summary>
/// El ámbito de guardado de verdad: una transacción de SQLite sobre la conexión única del
/// programa, la misma que comparten los seis repositorios.
/// </summary>
/// <remarks>
/// Es un pasamanos de <see cref="AmbitoDeEscritura"/> a <see cref="IAmbitoDeGuardado"/>, y vive
/// en la cáscara porque es el único sitio de la App que conoce la conexión (<see cref="Servicios"/>).
/// <c>Importar</c> no sabe que detrás hay SQLite.
/// </remarks>
public sealed class AmbitoDeGuardadoSobreSqlite : IAmbitoDeGuardado
{
    /// <summary>La transacción abierta al construirse.</summary>
    private readonly AmbitoDeEscritura _ambito;

    /// <summary>Abre la transacción sobre la conexión en el momento de construirse.</summary>
    /// <param name="conexion">La conexión abierta y migrada que comparten los repositorios.</param>
    public AmbitoDeGuardadoSobreSqlite(SqliteConnection conexion) => _ambito = AmbitoDeEscritura.Abrir(conexion);

    /// <inheritdoc />
    public void Confirmar() => _ambito.Confirmar();

    /// <inheritdoc />
    public void Dispose() => _ambito.Dispose();
}
