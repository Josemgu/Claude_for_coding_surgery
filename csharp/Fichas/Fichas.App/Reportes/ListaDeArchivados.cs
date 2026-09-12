using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Reportes;

/// <summary>Un caso archivado tal como se pinta en el historico, en un renglon.</summary>
/// <param name="CasoId">El numero interno del caso, por si hay que ir a buscarlo.</param>
/// <param name="Texto">El renglon entero, ya escrito en espanol.</param>
public sealed record RenglonArchivado(long CasoId, string Texto);

/// <summary>Lo que el historico tiene que pintar: cuantos hay, cuales se ensenan y si sobran.</summary>
/// <param name="CuantosHayArchivados">Cuantos casos archivados hay en la base, no cuantos se ensenan.</param>
/// <param name="Renglones">Los que caben en el tope, del mas nuevo al mas viejo.</param>
/// <param name="SeQuedaronFuera">Si el tope dejo alguno fuera; la pantalla lo tiene que decir.</param>
public sealed record HistoricoEnPantalla(
    int CuantosHayArchivados,
    IReadOnlyList<RenglonArchivado> Renglones,
    bool SeQuedaronFuera);

/// <summary>
/// El historico de la pantalla de Reportes: lo archivado, con la palabra y con su fecha.
/// </summary>
/// <remarks>
/// <para>Vive aqui por lo mismo que en el programa viejo (<c>interfaz/reportes.py</c>): un
/// caso archivado no es trabajo pendiente —salio de las listas a proposito— y lo unico que
/// se hace con el es mirarlo.</para>
///
/// <para>⚠️ <b>Se recorre la lista entera para quedarse con los archivados</b>, y no es un
/// descuido: <c>FiltroDeCasos</c> esta CONGELADO y no tiene «solo archivados». El coste
/// medido sobre 3 000 casos en memoria esta en <c>PruebasDelHistoricoEnPantalla</c>; sobre
/// SQLite NO se ha medido.</para>
/// </remarks>
public static class ListaDeArchivados
{
    /// <summary>Cuantos renglones se pintan como mucho.</summary>
    /// <remarks>
    /// Hay tope porque la pantalla los pinta TODOS seguidos, sin lista con su propia barra:
    /// una lista anidada se traga la rueda del raton y entonces la pagina deja de bajar, que
    /// es el requisito 1 del dueno. Lo que el tope deja fuera se dice en la pantalla.
    /// </remarks>
    public const int TopeDeRenglones = 200;

    /// <summary>Cuantos casos se piden a la vez al recorrer la base.</summary>
    private const int TamanoDelTrozo = 500;

    /// <summary>Lee los archivados de la base y los deja escritos en renglones.</summary>
    /// <param name="casos">Por donde se cuentan y se listan.</param>
    /// <param name="tope">Cuántos renglones como mucho; las pruebas lo bajan para medir el corte.</param>
    /// <returns>La cuenta total, los renglones que caben y si el tope dejó alguno fuera.</returns>
    public static HistoricoEnPantalla Leer(ICasos casos, int tope = TopeDeRenglones)
    {
        ArgumentNullException.ThrowIfNull(casos);

        var cuantos = casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true })
                    - casos.Contar(FiltroDeCasos.Todo);

        var archivados = ReunirLosArchivados(casos, tope);
        var personasPorCaso = casos.ContarPersonasDe([.. archivados.Select(caso => caso.Id)]);

        return new HistoricoEnPantalla(
            cuantos,
            [.. archivados.Select(caso => new RenglonArchivado(caso.Id, Renglon(caso, personasPorCaso)))],
            cuantos > archivados.Count);
    }

    /// <summary>Recorre la base por trozos y se queda con los archivados hasta llenar el tope.</summary>
    /// <param name="casos">Por donde se listan.</param>
    /// <param name="tope">Cuántos como mucho; al llenarlo se deja de recorrer.</param>
    private static List<Caso> ReunirLosArchivados(ICasos casos, int tope)
    {
        var encontrados = new List<Caso>();
        var trozo = Pagina.Primera(TamanoDelTrozo);
        while (encontrados.Count < tope)
        {
            var pagina = casos.Listar(FiltroDeCasos.Todo with { IncluirArchivados = true }, trozo);
            encontrados.AddRange(pagina.Elementos.Where(caso => caso.Archivado).Take(tope - encontrados.Count));
            if (!pagina.HayMas) break;
            trozo = trozo.Siguiente();
        }
        return encontrados;
    }

    /// <summary>El renglon de un caso archivado, con la palabra «archivado» y su fecha.</summary>
    /// <remarks>
    /// La fecha que falta se dice, no se calla: un archivado sin fecha en la base es un dato
    /// incompleto que alguien tiene que poder ver, y sustituirlo por el dia de hoy seria
    /// inventarlo.
    /// </remarks>
    /// <param name="caso">El caso archivado.</param>
    /// <param name="personasPorCaso">Cuántas personas tiene cada caso; el que no esté cuenta cero.</param>
    private static string Renglon(Caso caso, IReadOnlyDictionary<long, int> personasPorCaso)
    {
        var personas = personasPorCaso.TryGetValue(caso.Id, out var cuantas) ? cuantas : 0;
        var cuando = string.IsNullOrWhiteSpace(caso.FechaArchivado)
            ? "archivado, sin fecha anotada"
            : $"archivado el {caso.FechaArchivado}";

        return string.Join("   ·   ", new[]
        {
            caso.NumeroCaso is { Length: > 0 } numero ? numero : "sin número de caso",
            string.IsNullOrWhiteSpace(caso.FechaViaje) ? "sin fecha de viaje" : $"viajaba el {caso.FechaViaje}",
            personas == 1 ? "1 persona" : $"{personas} personas",
            cuando,
        });
    }
}
