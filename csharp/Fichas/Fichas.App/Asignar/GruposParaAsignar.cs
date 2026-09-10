using Fichas.App.Revisar;

namespace Fichas.App.Asignar;

/// <summary>
/// Los grupos de reparto de la pantalla de Asignar: una fecha de viaje y, dentro, sus
/// unidades, con los documentos de cada una.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe.</b> Palabras del dueño el 2026-09-09: <i>«En asignar debe poder
/// asignarlo por grupo. Cargar todos los documentos en un solo lugar no me conviene para
/// nada»</i>. Y el ejemplo con el que lo explicó, que es como trabaja: <i>«Rama San Juan No
/// 325535, 10 personas viajarán el 12 de septiembre. Barrio Marito 656351, 5 personas
/// viajarán el 12 de septiembre»</i> — un día, varias unidades, un líder por unidad.</para>
///
/// <para><b>Lo que había, medido.</b> <c>ListaParaAsignar.Ofrecer</c> servía una lista PLANA
/// de todos los casos sin archivar. Repartir el grupo del 12 de septiembre era buscarlos uno
/// a uno entre los demás y marcarlos a mano.</para>
///
/// <para>⛔ <b>Aquí NO se inventa ningún agrupado.</b> Se llama a
/// <see cref="ArbolDeRevisar.Agrupar"/>, que es el mismo que pinta el árbol de Revisar, el
/// que vuelca las carpetas al disco y el que usa <c>GruposParaCorregir</c>. Con dos reglas
/// de agrupado, el «grupo del 12 de septiembre» de esta pantalla podría no ser el de
/// Corrección, y el dueño repartiría una lista creyendo que es la que va a corregir. Del
/// árbol vienen además, gratis, el orden que él pidió —lo que viaja antes arriba, lo que no
/// tiene fecha al final con su llamada a mirarlo— y los nombres de las carpetas.</para>
///
/// <para>⚠️ <b>Agrupar reparte; no descarta.</b> De aquí sale exactamente lo que entró: la
/// suma de los grupos es el total ofrecido. Esta clase no lee de la base, no filtra y no
/// escribe. La queja del programa viejo era «no me deja asignar» y un agrupado que además
/// escondiera documentos la resucitaría con otra cara.</para>
/// </remarks>
public static class GruposParaAsignar
{
    /// <summary>
    /// Reparte los renglones ya leídos en grupos de fecha de viaje, con sus unidades dentro.
    /// </summary>
    /// <remarks>
    /// Recibe los renglones y no los puertos a propósito: quien llama ya los tiene leídos de
    /// UNA pasada —<see cref="ListaParaAsignar.Ofrecer"/>—, así que agrupar no cuesta ni una
    /// consulta más y esto se prueba sin base.
    /// </remarks>
    /// <param name="renglones">Los documentos que la pantalla ofrece, tal como los sirve la lista.</param>
    public static IReadOnlyList<GrupoDeUnaFecha> Armar(IEnumerable<RenglonParaAsignar> renglones)
    {
        ArgumentNullException.ThrowIfNull(renglones);

        var porCaso = new Dictionary<long, RenglonParaAsignar>();
        foreach (var renglon in renglones) porCaso[renglon.CasoId] = renglon;
        if (porCaso.Count == 0) return [];

        return
        [
            .. from mes in ArbolDeRevisar.Agrupar(porCaso.Values.Select(ComoTarjeta))
               from fecha in mes.Fechas
               select new GrupoDeUnaFecha(fecha.FechaIso, fecha.Carpeta, UnidadesDe(fecha, porCaso)),
        ];
    }

    /// <summary>
    /// El panel de grupos en UNA sola lista: cada fecha y, debajo, sus unidades.
    /// </summary>
    /// <remarks>
    /// Aplanado y no un repetidor dentro de otro, por lo mismo que
    /// <c>GrupoDelDia.EnUnaSolaLista</c>: anidar repetidores construye todos los renglones de
    /// golpe, y el número de elementos visuales tiene que tener tope.
    /// </remarks>
    /// <param name="grupos">Los grupos ya armados.</param>
    public static IReadOnlyList<RenglonDeGrupoParaAsignar> EnUnaSolaLista(IReadOnlyList<GrupoDeUnaFecha> grupos)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        var renglones = new List<RenglonDeGrupoParaAsignar>();
        foreach (var grupo in grupos)
        {
            renglones.Add(RenglonDeGrupoParaAsignar.DeLaFecha(grupo));
            renglones.AddRange(grupo.Unidades.Select(RenglonDeGrupoParaAsignar.DeLaUnidad));
        }

        return renglones;
    }

    /// <summary>Las unidades de una fecha, con los renglones de esta pantalla dentro.</summary>
    /// <remarks>
    /// El árbol devuelve tarjetas —que es lo que sabe agrupar—, y aquí se cambian por los
    /// renglones de verdad buscándolos por su número de caso. Así el orden lo pone el árbol y
    /// lo que se pinta sigue siendo lo que leyó esta pantalla.
    /// </remarks>
    private static IReadOnlyList<UnidadDeUnaFecha> UnidadesDe(
        GrupoDeFecha fecha, IReadOnlyDictionary<long, RenglonParaAsignar> porCaso)
        => [.. fecha.Unidades.Select(unidad => new UnidadDeUnaFecha(
            unidad.Carpeta,
            [.. unidad.Documentos.Select(documento => porCaso[documento.CasoId])]))];

    /// <summary>
    /// Un renglón de esta pantalla visto como la tarjeta que <see cref="ArbolDeRevisar"/> sabe agrupar.
    /// </summary>
    /// <remarks>
    /// <para>Se llenan los CUATRO campos de los que depende el agrupado y ninguno más: el
    /// número interno, la fecha ISO, el número de unidad y su nombre. Lo demás de una tarjeta
    /// —el PDF, las procedencias, la firma— no entra en la decisión de en qué carpeta cae un
    /// documento, y traerlo aquí costaría leer la base otra vez para no cambiar nada.</para>
    ///
    /// <para>⚠️ Esta tarjeta NO se pinta en ningún sitio: vive lo que dura el agrupado y se
    /// cambia otra vez por su renglón antes de salir. Lo que la ventana enseña es siempre el
    /// <see cref="RenglonParaAsignar"/> que leyó la pantalla.</para>
    /// </remarks>
    private static TarjetaDeDocumento ComoTarjeta(RenglonParaAsignar renglon)
        => new()
        {
            CasoId = renglon.CasoId,
            NumeroDeCaso = renglon.NumeroDeCaso,
            FechaDeViajeIso = renglon.FechaDeViajeIso,
            UnidadNumero = renglon.UnidadNumero,
            Unidad = renglon.Unidad,
        };
}

/// <summary>Un grupo de reparto: los que viajan un día, repartidos por unidad.</summary>
/// <param name="FechaIso">«2026-09-12», o vacío si de esos documentos no se pudo leer la fecha.</param>
/// <param name="Carpeta">«Grupo del 12 de septiembre», como se llama en Revisar y en Corrección.</param>
/// <param name="Unidades">Sus unidades, en el orden en que las ordena el árbol.</param>
public sealed record GrupoDeUnaFecha(
    string FechaIso,
    string Carpeta,
    IReadOnlyList<UnidadDeUnaFecha> Unidades)
{
    /// <summary>Cuántos documentos viajan ese día, sumando todas sus unidades.</summary>
    public int CuantosDocumentos => Unidades.Sum(unidad => unidad.Documentos.Count);

    /// <summary>Los documentos del día entero; es lo que asigna el botón de la fecha.</summary>
    public IReadOnlyList<long> CasosDeLaFecha
        => [.. Unidades.SelectMany(unidad => unidad.CasosDeLaUnidad)];

    /// <summary>Si es el grupo de los documentos cuya fecha de viaje no se pudo leer.</summary>
    /// <remarks>
    /// Se deduce de <see cref="FechaIso"/> vacío y no de una marca aparte, por lo mismo que en
    /// <see cref="GrupoDeFecha.HayQueRevisarlo"/>: no hay fecha que poner cuando no se pudo
    /// leer, y esos son justo los que el dueño pidió que le pidieran mirarlos.
    /// </remarks>
    public bool HayQueRevisarlo => FechaIso.Length == 0;

    /// <summary>Lo que se lee en el panel: la fecha y cuántos documentos trae.</summary>
    public string Etiqueta => ArbolDeRevisar.EtiquetaDe(Carpeta, CuantosDocumentos, HayQueRevisarlo);
}

/// <summary>Una unidad dentro de un día: «325535 · Rama San Juan», con sus documentos.</summary>
/// <param name="Carpeta">El número Y el nombre, como en el árbol: dos unidades pueden llamarse igual.</param>
/// <param name="Documentos">Sus documentos, tal como los leyó esta pantalla.</param>
public sealed record UnidadDeUnaFecha(string Carpeta, IReadOnlyList<RenglonParaAsignar> Documentos)
{
    /// <summary>Los documentos de la unidad; es lo que asigna el botón de la unidad.</summary>
    public IReadOnlyList<long> CasosDeLaUnidad => [.. Documentos.Select(documento => documento.CasoId)];

    /// <summary>Lo que se lee en el panel: la unidad y cuántos documentos trae.</summary>
    public string Etiqueta => ArbolDeRevisar.ConSuCifra(Carpeta, Documentos.Count);
}

/// <summary>
/// Un renglón del panel de grupos: o una fecha, o una de sus unidades.
/// </summary>
/// <remarks>
/// UN solo renglón para las dos cosas, igual que <c>RenglonDelGrupo</c> en la pantalla del
/// día: la lista va aplanada y la plantilla enseña una mitad o la otra según
/// <see cref="EsLaFecha"/>.
/// </remarks>
/// <param name="EsLaFecha">Si es la cabecera de un día; si no, es una unidad de ese día.</param>
/// <param name="Etiqueta">Lo que se lee en el panel, con su cifra dentro.</param>
/// <param name="Nombre">Cómo se llama el grupo SIN la cifra: «Grupo del 12 de septiembre».</param>
/// <param name="Casos">Los documentos que asigna el botón de este renglón.</param>
/// <param name="FechaIso">La fecha del día al que pertenece; vacía en el grupo sin fecha.</param>
public sealed record RenglonDeGrupoParaAsignar(
    bool EsLaFecha,
    string Etiqueta,
    string Nombre,
    IReadOnlyList<long> Casos,
    string FechaIso)
{
    /// <summary>La cabecera de un día, con los documentos del día entero.</summary>
    public static RenglonDeGrupoParaAsignar DeLaFecha(GrupoDeUnaFecha grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        return new(true, grupo.Etiqueta, grupo.Carpeta, grupo.CasosDeLaFecha, grupo.FechaIso);
    }

    /// <summary>Una unidad de un día, con los documentos de esa unidad.</summary>
    public static RenglonDeGrupoParaAsignar DeLaUnidad(UnidadDeUnaFecha unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        return new(false, unidad.Etiqueta, unidad.Carpeta, unidad.CasosDeLaUnidad, string.Empty);
    }

    /// <summary>Si es una unidad dentro de un día; la otra mitad de la plantilla.</summary>
    public bool EsUnaUnidad => !EsLaFecha;

    /// <summary>Si su botón tiene algo que asignar; con cero no se enciende.</summary>
    public bool SePuedeAsignar => Casos.Count > 0;

    /// <summary>Lo que dice su botón: el gesto del día y el de la unidad no son el mismo.</summary>
    public string PalabraDelBoton => EsLaFecha ? "Asignar el grupo entero" : "Asignar esta unidad";

    /// <summary>
    /// Cómo se llama ese botón para quien no ve la pantalla; nombra SU grupo.
    /// </summary>
    /// <remarks>
    /// Nombra el grupo porque en el panel hay un botón por renglón: un lector de pantalla que
    /// dijera «asignar esta unidad» siete veces seguidas no diría de cuál.
    /// </remarks>
    public string NombreDelBoton => $"{PalabraDelBoton}: {Etiqueta}";
}
