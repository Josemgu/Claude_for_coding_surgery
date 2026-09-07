using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// Un cambio de esquema: su numero, que cambio y como se aplica.
/// </summary>
/// <remarks>
/// El orden en que estas entradas aparecen en <see cref="CatalogoDeMigraciones.Todas"/>
/// ES el orden en que se aplican, y no es indiferente: la 12 reconstruye <c>casos</c>
/// entera y la 13 y la 14 le anaden columnas; la 15 reconstruye <c>personas</c> y va
/// despues de la 14. Al reves, una reconstruccion tendria que conocer columnas que
/// todavia no existian, o se las llevaria por delante.
/// </remarks>
/// <param name="Version">El entero que la identifica.</param>
/// <param name="Descripcion">Que cambio, en espanol; se guarda en <c>version_esquema</c>.</param>
/// <param name="Aplicar">Lo que hay que hacerle a la base.</param>
public sealed record Migracion(
    int Version,
    string Descripcion,
    Action<SqliteConnection> Aplicar);
