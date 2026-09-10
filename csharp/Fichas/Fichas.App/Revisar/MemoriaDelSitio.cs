using System.Globalization;

namespace Fichas.App.Revisar;

/// <summary>
/// Dónde estaba él trabajando en Revisar: qué carpeta miraba y qué ramas tenía abiertas.
/// </summary>
/// <param name="Carpeta">La clave de la carpeta a la vista, o nula si veía todos.</param>
/// <param name="Abiertas">Las claves de las ramas que estaban abiertas.</param>
public sealed record SitioDeRevisar(string? Carpeta, IReadOnlyList<string> Abiertas)
{
    /// <summary>Antes de la primera vez: no hay nada que recordar todavía.</summary>
    public static SitioDeRevisar Ninguno { get; } = new(null, []);

    /// <summary>Si no se estaba en ninguna parte, que no es lo mismo que estar en «todos».</summary>
    public bool NoRecuerdaNada => Carpeta is null && Abiertas.Count == 0;
}

/// <summary>
/// La regla de «marcar un caso sin moverse»: qué carpeta y qué ramas vuelven tras repintar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Palabras del dueño, 2026-09-07: <i>«cuando estoy trabajando en
/// Revisar y le das clic a un paquete, te envía al inicio otra vez de Revisar y te coloca
/// todos juntos. Debe permitirme marcar un caso sin moverse, para seguir trabajando con los
/// otros sin revisar»</i>.
/// </para>
/// <para>
/// <b>Qué pasaba de verdad, medido con la ventana abierta el 2026-09-09</b> sobre el paquete
/// publicado con <c>--falso 30</c>: con la carpeta «Enero 2027» elegida y abierta, el pie
/// decía «viendo 5 de 18 · «Enero 2027» · 28 en la base»; al pulsar «Resuelto» en una tarjeta
/// pasaba a «viendo 17 de 28 documentos», la rama volvía a cerrada y ninguna quedaba elegida.
/// La causa está en <c>PintarLasCarpetas</c>, que hacía <c>_carpetaALaVista = null</c> y
/// <c>_arbol.RootNodes.Clear()</c> en cada repintado. El <b>desplazamiento sí se conservaba</b>
/// —las seis primeras tarjetas seguían fuera de la vista antes y después—, así que aquí no se
/// toca.
/// </para>
/// <para>
/// ⛔ <b>El árbol SE SIGUE REHACIENDO, y eso no es negociable.</b> Las carpetas llevan en su
/// etiqueta cuántos documentos hay <i>en el tablero que se está mirando</i>, y tras marcar un
/// caso esa cifra cambia. Conservar los nodos dejaría cifras de antes junto a tarjetas de
/// ahora. Lo que se conserva no son los nodos: es <b>dónde estaba él</b>.
/// </para>
/// <para>
/// Sin ventana a propósito: lo que decide dónde se vuelve se lee desde una prueba, sin abrir
/// nada. Antes vivía en un campo privado de <c>PaginaDeRevisar</c>, donde ninguna prueba
/// llegaba, y por eso pudo perderse el sitio en cada marca sin que nada se pusiera rojo.
/// </para>
/// </remarks>
public static class MemoriaDelSitio
{
    /// <summary>Guarda dónde estaba él antes de rehacer el árbol.</summary>
    /// <param name="carpeta">La clave de la carpeta a la vista, o nula si veía todos.</param>
    /// <param name="abiertas">Las claves de las ramas abiertas ahora mismo.</param>
    public static SitioDeRevisar Recordar(string? carpeta, IEnumerable<string> abiertas)
    {
        ArgumentNullException.ThrowIfNull(abiertas);
        return new SitioDeRevisar(carpeta, [.. abiertas]);
    }

    /// <summary>
    /// A qué carpeta se vuelve tras rehacer el árbol, o nulo para ver todos.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Si la carpeta ya no está, NO se elige otra.</b> Un documento marcado puede haber
    /// sido el último de su unidad, y entonces esa carpeta desaparece del tablero. Elegir «la
    /// de al lado» le enseñaría documentos de otro grupo creyendo que son los suyos. Ver todos
    /// es lo que había antes de elegir carpeta, y es verdad.
    /// </remarks>
    /// <param name="sitio">Dónde estaba él.</param>
    /// <param name="clavesQueHay">Las claves de todas las carpetas del árbol nuevo.</param>
    public static string? CarpetaQueVuelve(SitioDeRevisar sitio, IEnumerable<string> clavesQueHay)
    {
        ArgumentNullException.ThrowIfNull(sitio);
        ArgumentNullException.ThrowIfNull(clavesQueHay);

        if (sitio.Carpeta is not string donde) return null;
        return clavesQueHay.Contains(donde, StringComparer.Ordinal) ? donde : null;
    }

    /// <summary>
    /// Si una rama del árbol nuevo tiene que nacer abierta.
    /// </summary>
    /// <remarks>
    /// Con un sitio recordado se abren las que él tenía abiertas y ninguna más — abrir además
    /// el primer mes sería devolverlo al principio, que es justo lo que se está arreglando.
    /// Sin sitio recordado se abre solo el primer mes, que es lo de siempre: con 3 000
    /// documentos, abrirlas todas construiría miles de filas de golpe.
    /// </remarks>
    /// <param name="sitio">Dónde estaba él.</param>
    /// <param name="clave">La rama que se está pintando.</param>
    /// <param name="esElPrimerMes">Si es la primera rama del árbol, la que viaja antes.</param>
    public static bool SeAbre(SitioDeRevisar sitio, string clave, bool esElPrimerMes)
    {
        ArgumentNullException.ThrowIfNull(sitio);

        return sitio.NoRecuerdaNada
            ? esElPrimerMes
            : sitio.Abiertas.Contains(clave, StringComparer.Ordinal);
    }

    /// <summary>La clave de una carpeta de mes.</summary>
    /// <remarks>
    /// ⛔ <b>La clave NO es el nombre de la carpeta</b>, y no es purismo: el mes de lo que no
    /// tiene fecha y su carpeta de fecha se llaman los DOS «Sin fecha de viaje»
    /// (<see cref="ArbolDeRevisar.SinFecha"/>). Con el nombre por clave, volver a una
    /// devolvería a la otra. Se compone del nivel y de las claves ISO, que no dependen del
    /// idioma ni de cómo se escriba la etiqueta.
    /// </remarks>
    public static string ClaveDelMes(GrupoDeMes mes)
    {
        ArgumentNullException.ThrowIfNull(mes);
        return $"mes|{mes.MesIso}";
    }

    /// <summary>La clave de una carpeta de fecha de viaje, dentro de su mes.</summary>
    public static string ClaveDeLaFecha(GrupoDeMes mes, GrupoDeFecha fecha)
    {
        ArgumentNullException.ThrowIfNull(mes);
        ArgumentNullException.ThrowIfNull(fecha);
        return $"fecha|{mes.MesIso}|{fecha.FechaIso}";
    }

    /// <summary>La clave de una carpeta de unidad, dentro de su fecha y de su mes.</summary>
    /// <remarks>
    /// Lleva el número Y el nombre porque dos unidades pueden llamarse igual y el número es lo
    /// único que las distingue, y porque una puede no traer número leído.
    /// </remarks>
    public static string ClaveDeLaUnidad(GrupoDeMes mes, GrupoDeFecha fecha, GrupoDeUnidad unidad)
    {
        ArgumentNullException.ThrowIfNull(mes);
        ArgumentNullException.ThrowIfNull(fecha);
        ArgumentNullException.ThrowIfNull(unidad);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"unidad|{mes.MesIso}|{fecha.FechaIso}|{unidad.Numero}|{unidad.Nombre}");
    }
}
