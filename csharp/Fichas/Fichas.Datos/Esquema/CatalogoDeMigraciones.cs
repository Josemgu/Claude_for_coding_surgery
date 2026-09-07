namespace Fichas.Datos.Esquema;

/// <summary>
/// Las dieciocho migraciones posteriores a la version 1, en el unico orden que cuenta.
/// </summary>
/// <remarks>
/// <para>
/// Portado de la tupla <c>MIGRACIONES</c> de <c>datos/migraciones.py</c>, medida el
/// 2026-09-04: declara [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]. La 16 y la 17
/// NO estan en el Python y no lo estaran: alli los CHECK del numero de caso y de la
/// cedula se quedan (el .exe de Python esta congelado). Son las dos unicas desviaciones
/// declaradas del catalogo.
/// </para>
/// <para>
/// ⚠️ <b>El orden de esta lista ES el orden en que se aplican</b>, y no es
/// indiferente. La 12 reconstruye <c>casos</c> entera, y la 13 y la 14 le anaden
/// columnas: al reves, la reconstruccion de la 12 tendria que conocer columnas que
/// todavia no existian, o se las llevaria por delante. La 15 reconstruye
/// <c>personas</c> y va despues de la 14 por lo mismo: una reconstruccion tiene que
/// conocer TODAS las columnas que la tabla ya tiene.
/// </para>
/// <para>
/// Quien enganche la 20: se anade aqui al final, y <see cref="AplicadorDeEsquema.VersionAlDia"/>
/// sube sola porque se deriva de esta lista. Un numero escrito a mano se queda
/// desfasado el dia que alguien anade una migracion y se olvida de subirlo, y entonces
/// la migracion nueva no se aplica NUNCA y nadie se entera.
/// </para>
/// </remarks>
public static class CatalogoDeMigraciones
{
    /// <summary>Las migraciones, de la 2 a la 19, en el orden en que se aplican.</summary>
    public static IReadOnlyList<Migracion> Todas { get; } =
    [
        new(2, MigracionesQueRehacenTablas.DescripcionDeLaVersion2,
            MigracionesQueRehacenTablas.AVersion2),

        new(3, MigracionesQueAnadenColumnas.DescripcionDeLaVersion3,
            MigracionesQueAnadenColumnas.AVersion3),

        new(4, MigracionesQueAnadenColumnas.DescripcionDeLaVersion4,
            MigracionesQueAnadenColumnas.AVersion4),

        new(5, MigracionesQueAnadenColumnas.DescripcionDeLaVersion5,
            MigracionesQueAnadenColumnas.AVersion5),

        new(6, MigracionesQueAnadenColumnas.DescripcionDeLaVersion6,
            MigracionesQueAnadenColumnas.AVersion6),

        new(7, MigracionesQueRehacenTablas.DescripcionDeLaVersion7,
            MigracionesQueRehacenTablas.AVersion7),

        new(8, MigracionesQueCreanTablas.DescripcionDeLaVersion8,
            MigracionesQueCreanTablas.AVersion8),

        new(9, MigracionesQueAnadenColumnas.DescripcionDeLaVersion9,
            MigracionesQueAnadenColumnas.AVersion9),

        new(10, MigracionesQueAnadenColumnas.DescripcionDeLaVersion10,
            MigracionesQueAnadenColumnas.AVersion10),

        new(11, MigracionesQueCreanTablas.DescripcionDeLaVersion11,
            MigracionesQueCreanTablas.AVersion11),

        // ⚠️ La 12 reconstruye `casos` entera. Todo lo que le anada columnas va
        // DESPUES, nunca antes.
        new(12, MigracionesQueRehacenTablas.DescripcionDeLaVersion12,
            MigracionesQueRehacenTablas.AVersion12),

        new(13, MigracionesQueAnadenColumnas.DescripcionDeLaVersion13,
            MigracionesQueAnadenColumnas.AVersion13),

        new(14, MigracionesQueAnadenColumnas.DescripcionDeLaVersion14,
            MigracionesQueAnadenColumnas.AVersion14),

        // ⚠️ La 15 reconstruye `personas` entera y va DESPUES de la 14.
        new(15, MigracionesQueRehacenTablas.DescripcionDeLaVersion15,
            MigracionesQueRehacenTablas.AVersion15),

        // ⚠️ La 16 reconstruye `casos` entera y va DESPUES de la 14, que es la ultima
        // que le anadio columnas. No repite a la 12: la 12 ya no se ejecuta sobre las
        // bases que el Python dejo en esa version, y son justo las del dueno.
        new(16, MigracionesQueRehacenTablas.DescripcionDeLaVersion16,
            MigracionesQueRehacenTablas.AVersion16),

        // ⚠️ La 17 reconstruye `personas` entera y va DESPUES de la 15, que es la ultima
        // que la habia reconstruido. Quita el CHECK de forma del MRN por el mismo motivo
        // que la 16 quito el del numero de caso: lo rechazado se pierde, y aqui lo que se
        // pierde es una PERSONA.
        new(17, MigracionesQueRehacenTablas.DescripcionDeLaVersion17,
            MigracionesQueRehacenTablas.AVersion17),

        // La 18 solo anade columnas, asi que puede ir despues de las reconstrucciones sin
        // ordenar nada: quien reconstruya `casos` o `companeros` mas adelante tendra que
        // conocerlas, como la 16 tuvo que conocer las de la 14.
        new(18, MigracionesQueAnadenColumnas.DescripcionDeLaVersion18,
            MigracionesQueAnadenColumnas.AVersion18),

        // La 19 solo anade columnas a `personas`, asi que va despues de la 17 —la ultima
        // que la reconstruyo— sin ordenar nada mas. Quien la reconstruya en el futuro
        // tendra que conocer las tres, como la 17 tuvo que conocer las de la 15.
        new(19, MigracionesQueAnadenColumnas.DescripcionDeLaVersion19,
            MigracionesQueAnadenColumnas.AVersion19),
    ];
}
