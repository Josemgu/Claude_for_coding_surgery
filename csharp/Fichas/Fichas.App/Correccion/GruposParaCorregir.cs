using Fichas.App.Revisar;

namespace Fichas.App.Correccion;

/// <summary>
/// Los grupos de trabajo de la pantalla de Correccion: una fecha de viaje y una unidad, con
/// TODOS sus documentos dentro.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Palabras del dueno el 2026-09-07, probando el programa:
/// <i>«En corrección, permite trabajar por grupo, no todos juntos. Eso es súper incómodo. El
/// día que tenga 800 solicitudes tendré problemas»</i>, y después: <i>«Esto no me funciona
/// para nada, no le veo función. Prefiero trabajar y corregir por grupo que todos
/// juntos»</i>.</para>
///
/// <para><b>Lo que habia, medido.</b> <c>PaginaDeCorreccion</c> ofrecia un desplegable plano
/// con los primeros <b>50</b> casos de la base y nada mas: ni agrupados, ni filtrados, y sin
/// ninguna forma de llegar al 51. Con 800 solicitudes, 750 quedaban fuera de la pantalla.</para>
///
/// <para>⛔ <b>Aqui NO se inventa ningun agrupado.</b> Se llama a
/// <see cref="ArbolDeRevisar.Agrupar"/>, que es el mismo que pinta el arbol de Revisar y el
/// que vuelca las carpetas al disco, y se APLANA a un solo nivel de trabajo. Esa es la mitad
/// que importa: con dos reglas de agrupado, el «grupo del 17 de septiembre» de una pantalla
/// podria no ser el de la otra, y el dueno trabajaria dos listas creyendo que son una. Del
/// arbol viene ademas, gratis, el orden que el pidio —lo que viaja antes arriba, lo que no
/// tiene fecha al final con su llamada a mirarlo— y no hay que volver a decidirlo.</para>
///
/// <para>⚠️ <b>Un grupo es (fecha, unidad) y no (fecha).</b> Es como ya estan las carpetas de
/// Revisar y como el trabaja: habla con UN lider por unidad, no con «el lider del martes»
/// (ADR-0005 §2.5). El mes no hace falta como escalon: aqui no se navega un arbol, se elige
/// un grupo de una lista.</para>
///
/// <para>⛔ De aqui no sale ni una escritura: es un reparto de lo ya leido. La regla
/// permanente 5 sigue entera.</para>
/// </remarks>
public static class GruposParaCorregir
{
    /// <summary>
    /// Reparte las tarjetas ya leidas en grupos de (fecha de viaje, unidad), en su orden.
    /// </summary>
    /// <remarks>
    /// Recibe las tarjetas y no los puertos a proposito: quien llama ya las tiene leidas de
    /// UNA pasada —<see cref="TableroDeRevisar.Cargar"/>—, y asi esto se prueba sin base.
    /// </remarks>
    /// <param name="tarjetas">Los documentos, tal como los arma Revisar.</param>
    public static IReadOnlyList<GrupoParaCorregir> Armar(IEnumerable<TarjetaDeDocumento> tarjetas)
    {
        ArgumentNullException.ThrowIfNull(tarjetas);

        return
        [
            .. from mes in ArbolDeRevisar.Agrupar(tarjetas)
               from fecha in mes.Fechas
               from unidad in fecha.Unidades
               select new GrupoParaCorregir(fecha.FechaIso, fecha.Carpeta, unidad.Carpeta, unidad.Documentos),
        ];
    }

    /// <summary>
    /// En que puesto de la lista esta el grupo de ese documento, o −1 si no esta en ninguno.
    /// </summary>
    /// <remarks>
    /// El −1 no es un descuido: es lo que pasa cuando se llega a un documento desde otra
    /// pantalla y ese documento esta archivado, asi que no entra en ningun grupo. Quien
    /// pregunta tiene que poder distinguirlo de «esta en el primero», y por eso no se
    /// devuelve 0.
    /// </remarks>
    /// <param name="grupos">Los grupos ya armados.</param>
    /// <param name="casoId">El documento que se busca.</param>
    public static int DondeEsta(IReadOnlyList<GrupoParaCorregir> grupos, long casoId)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        for (var donde = 0; donde < grupos.Count; donde++)
        {
            if (grupos[donde].Documentos.Any(documento => documento.CasoId == casoId)) return donde;
        }

        return -1;
    }
}

/// <summary>
/// Un grupo de trabajo: quienes viajan un dia por una unidad, con sus documentos.
/// </summary>
/// <param name="FechaIso">«2026-09-17», o vacio si de esos documentos no se pudo leer la fecha.</param>
/// <param name="CarpetaDeLaFecha">«Grupo del 17 de septiembre», como se llama en Revisar.</param>
/// <param name="CarpetaDeLaUnidad">«700001 · Castries Branch», como se llama en Revisar.</param>
/// <param name="Documentos">Sus documentos, TODOS, en el orden en que los ordena el arbol.</param>
public sealed record GrupoParaCorregir(
    string FechaIso,
    string CarpetaDeLaFecha,
    string CarpetaDeLaUnidad,
    IReadOnlyList<TarjetaDeDocumento> Documentos)
{
    /// <summary>Cuantos documentos trae este grupo. Sin tope: son todos los suyos.</summary>
    public int CuantosDocumentos => Documentos.Count;

    /// <summary>«Grupo del 12 de septiembre · 325535 · Rama San Juan», sin la cuenta detras.</summary>
    /// <remarks>
    /// Va aparte de <see cref="Etiqueta"/> porque se usa para otra cosa: la etiqueta es lo que
    /// se elige en un desplegable —y ahi la cifra manda—, y esto es como se NOMBRA el grupo al
    /// decir a donde pasa un documento que sale de Correccion. Ahi la cuenta sobra y encima
    /// mentiria: el documento que acaba de salir todavia no esta contado en ella.
    /// </remarks>
    public string Nombre => $"{CarpetaDeLaFecha} · {CarpetaDeLaUnidad}";

    /// <summary>Si es el grupo de los documentos cuya fecha de viaje no se pudo leer.</summary>
    /// <remarks>
    /// Se deduce de <see cref="FechaIso"/> vacio y no de una marca aparte, por lo mismo que
    /// en <see cref="GrupoDeFecha.HayQueRevisarlo"/>: no hay fecha que poner cuando no se
    /// pudo leer, y esos son justo los que el dueno pidio que le pidieran mirarlos.
    /// </remarks>
    public bool HayQueRevisarlo => FechaIso.Length == 0;

    /// <summary>
    /// Lo que se lee en el desplegable: la fecha, la unidad y cuantos documentos trae.
    /// </summary>
    /// <remarks>
    /// La cifra va SIEMPRE, que es la regla §8 de <c>CLAUDE.md</c>: sin ella, elegir un grupo
    /// es elegir a ciegas cuanto trabajo se acaba de abrir. Y la llamada a mirarlo va delante
    /// en el grupo sin fecha, con las palabras del dueno, por lo mismo que en el arbol de
    /// Revisar: sin ella esa seccion se lee igual que las demas y no pide nada.
    /// </remarks>
    public string Etiqueta
        => ArbolDeRevisar.EtiquetaDe($"{CarpetaDeLaFecha} · {CarpetaDeLaUnidad}", CuantosDocumentos, HayQueRevisarlo);
}
