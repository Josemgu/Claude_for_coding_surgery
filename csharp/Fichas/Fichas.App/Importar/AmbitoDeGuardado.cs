namespace Fichas.App.Importar;

/// <summary>
/// Lo que el guardado de una hoja necesita para que sus filas entren todas o ninguna: abrir,
/// confirmar, y deshacer si se cierra sin confirmar.
/// </summary>
/// <remarks>
/// <para>Es la cara que <see cref="GuardadoDeHojas"/> ve de la transacción (R-4 del plan del
/// 2026-09-15). No nombra SQLite: quien la implementa de verdad es
/// <c>Cascara.AmbitoDeGuardadoSobreSqlite</c>, sobre <c>Fichas.Datos.Conexion.AmbitoDeEscritura</c>;
/// con la base inventada y en las pruebas que no abren SQLite va <see cref="SinAmbitoDeGuardado"/>.</para>
///
/// <para>⚠️ Iría en <c>Fichas.Contratos</c>, junto a los puertos, pero ese proyecto está
/// congelado; queda aquí, en el único sitio que la usa, y se dice en la entrega.</para>
/// </remarks>
public interface IAmbitoDeGuardado : IDisposable
{
    /// <summary>Confirma todo lo escrito desde que se abrió. Sin esta llamada, cerrar deshace.</summary>
    void Confirmar();
}

/// <summary>
/// El ámbito que no abre nada: cada escritura queda por su cuenta, como antes de R-4.
/// </summary>
/// <remarks>
/// Es lo que recibe el guardado cuando detrás no hay SQLite: los repositorios inventados de
/// <c>Fichas.Datos.Falso</c> no tienen transacciones que abrir, y una prueba sobre ellos mide
/// lo que se guarda, no cuántas confirmaciones cuesta. Con la base de verdad NO se usa: ahí
/// va el ámbito real, y si alguien montara este, cada hoja volvería a costar de 9 a 24
/// confirmaciones sin que ninguna prueba lo viera.
/// </remarks>
public sealed class SinAmbitoDeGuardado : IAmbitoDeGuardado
{
    /// <summary>No hay nada que confirmar: nada se abrió.</summary>
    public void Confirmar()
    {
    }

    /// <summary>No hay nada que cerrar ni que deshacer.</summary>
    public void Dispose()
    {
    }
}
