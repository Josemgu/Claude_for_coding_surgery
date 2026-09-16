using Fichas.Reportes.Reglas;

namespace Fichas.App.Asignar;

/// <summary>
/// Los casos marcados en Asignar, guardados APARTE de la lista que se ve. Sin ventana.
/// </summary>
/// <remarks>
/// <para>⛔ <b>El defecto que cierra, del dueño el 2026-09-16:</b> <i>«Si yo busco el nombre
/// de una persona, lo marco para poder asignarlo en grupo, y elimino el nombre de búsqueda
/// para buscar a otra persona, el sistema desmarca a las personas que yo ya había
/// marcado»</i>. Medido en el código ese día: las marcas vivían solo en
/// <c>ItemsView.SelectedItems</c>, y cada tecleo en el buscador ponía en la lista una
/// fuente nueva, que nace sin marcas. Buscar borraba el trabajo hecho.</para>
///
/// <para>Aquí las marcas son un conjunto de números internos de caso que no sabe nada de
/// la lista: sobreviven a buscar, a borrar la búsqueda y a repintar. La pantalla le dice
/// qué renglones acaba de pintar (<see cref="AlRepintar"/>) y recibe cuáles de ellos tiene
/// que volver a marcar; y «Asignar los marcados» usa <see cref="Ids"/> entero, estén o no
/// a la vista, y lo dice con la cifra en la línea (<see cref="Dicho"/>).</para>
///
/// <para>Ctrl+A sigue siendo de lo que se VE (<see cref="MarcarTodo"/>): marcar a ciegas
/// lo que no está en pantalla y darlo después sería asignar sin mirar. Lo que no se ve se
/// quita entero con <see cref="QuitarTodas"/>, que es un botón con su cifra al lado.</para>
/// </remarks>
public sealed class MarcasDeAsignar
{
    /// <summary>Los números internos de los casos marcados, a la vista o no.</summary>
    private readonly HashSet<long> _marcados = [];

    /// <summary>Los números internos de los renglones que la lista tiene pintados ahora mismo.</summary>
    private HashSet<long> _aLaVista = [];

    /// <summary>Los casos marcados, todos; es un conjunto, sin orden.</summary>
    public IReadOnlyCollection<long> Ids => _marcados;

    /// <summary>Cuántos hay marcados en total.</summary>
    public int Cuantas => _marcados.Count;

    /// <summary>Cuántos de los marcados están en la lista que se ve ahora.</summary>
    public int CuantasALaVista => _marcados.Count(_aLaVista.Contains);

    /// <summary>Cuántos de los marcados NO están en la lista que se ve: los que una búsqueda dejó fuera.</summary>
    public int CuantasFueraDeLaVista => Cuantas - CuantasALaVista;

    /// <summary>Marca un caso; repetirlo no lo cuenta dos veces.</summary>
    /// <param name="casoId">El número interno del caso.</param>
    public void Marcar(long casoId) => _marcados.Add(casoId);

    /// <summary>Desmarca un caso; si no estaba, no pasa nada.</summary>
    /// <param name="casoId">El número interno del caso.</param>
    public void Desmarcar(long casoId) => _marcados.Remove(casoId);

    /// <summary>Quita TODAS las marcas, también las que no están a la vista.</summary>
    public void QuitarTodas() => _marcados.Clear();

    /// <summary>
    /// La lista se acaba de pintar con estos renglones: se anota qué se ve y se devuelve
    /// cuáles de ellos hay que volver a marcar.
    /// </summary>
    /// <param name="idsPintados">Los números internos de los renglones que la lista tiene ahora, en su orden.</param>
    /// <returns>Los de <paramref name="idsPintados"/> que están marcados, en el mismo orden.</returns>
    public IReadOnlyList<long> AlRepintar(IReadOnlyList<long> idsPintados)
    {
        ArgumentNullException.ThrowIfNull(idsPintados);
        _aLaVista = idsPintados.ToHashSet();
        return idsPintados.Where(_marcados.Contains).ToList();
    }

    /// <summary>
    /// Ctrl+A: marca todo lo que se ve; si ya estaba todo lo visible marcado, lo desmarca.
    /// Lo que no está a la vista no se toca en ningún caso.
    /// </summary>
    /// <returns>Verdadero si marcó; falso si desmarcó.</returns>
    public bool MarcarODesmarcarLaVista()
    {
        var hayQueMarcar = MarcarTodo.HayQueMarcar(CuantasALaVista, _aLaVista.Count);
        if (hayQueMarcar) _marcados.UnionWith(_aLaVista);
        else _marcados.ExceptWith(_aLaVista);
        return hayQueMarcar;
    }

    /// <summary>
    /// La línea de la pantalla: cuántos hay marcados, cuántos fuera de la vista, y a quién irían.
    /// </summary>
    /// <remarks>
    /// La cifra de «fuera de la vista» solo sale cuando es mayor que cero: con todo a la
    /// vista, nombrarla sería ruido. Y cuando sale, sale ANTES del destino, porque es lo que
    /// contesta «¿por qué dice 7 si yo veo 4?».
    /// </remarks>
    /// <param name="nombreDelDestino">El compañero elegido, o nulo si no hay ninguno.</param>
    public string Dicho(string? nombreDelDestino)
    {
        if (Cuantas == 0) return "Marca los casos que quieras dar. Ctrl+A marca todos los que se ven.";

        var fuera = CuantasFueraDeLaVista;
        var cabeza = Plural.Con(Cuantas, "marcado", "marcados")
            + (fuera > 0 ? $", {fuera} fuera de la vista" : string.Empty);
        return cabeza
            + " · " + Plural.Palabra(Cuantas, "iría", "irían")
            + $" a {nombreDelDestino ?? "nadie: elige un compañero"}";
    }
}
