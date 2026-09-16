using Fichas.App.Inicio;
using Fichas.App.Vocabulario;

namespace Fichas.App.Grupo;

/// <summary>
/// De qué color va el renglón de una persona dentro de la fecha —o la tarjeta de un documento
/// en Revisar—: verde si está resuelto, naranja si está a medias, rojo si le falta todo.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-14:</b> <i>«Quiero que cuando des clic y entres a la
/// fecha, lo que esté completo se marque en verde y lo que no en rojo, para saber cuáles
/// fueron completados y cuáles no, como se muestra en el calendario afuera»</i>. <b>Y el
/// 2026-09-16:</b> <i>«Las personas que se han completado, por ejemplo 4 preguntas de las 6,
/// deben pasar a color naranja e indicar que le falta»</i>.</para>
///
/// <para>⛔ <b>No es un tercer criterio ni una tercera palabra.</b> Son las dos palabras del
/// 2026-09-07 —«resuelto» y «me falta», <see cref="LoQueSeLeeDeUnaPersona"/>— traducidas a
/// color, con el naranja como MATIZ de «me falta»: alguna de las seis en sí y no las seis
/// (<see cref="LoQueSeLeeDeUnaPersona.AMedias"/>). La regla de qué es «a medias» vive en el
/// vocabulario, y aquí solo se elige el color; con dos sitios decidiendo, un día dirían cosas
/// distintas.</para>
///
/// <para>Y con la misma excepción con la que el calendario cuenta «resueltas»: un archivado se
/// lee resuelto aunque sus seis preguntas no digan que sí —y aunque esté a medias—, porque
/// archivar es el gesto con el que el dueño cierra un documento (<i>«aunque se archive, debe
/// quedarse en el calendario marcado en verde»</i>, 2026-09-07; <c>LectorDeGrupos.ArmarLaPastilla</c>:
/// <c>confirmada || caso.Archivado</c>).</para>
///
/// <para>Devuelve <see cref="ColorDeLaPastilla"/> y no un tipo nuevo a propósito: es el MISMO
/// par de colores de fuera más el naranja, y un segundo tipo con los mismos valores es como
/// empiezan a separarse. Una persona nunca sale en gris —el gris es de una unidad sin nadie
/// leído, y una persona es alguien—. ⚠️ La pastilla del calendario y la cabecera de la unidad
/// NO devuelven naranja: siguen en <c>ColoresDeLaPastilla.De</c>, y el motivo está en
/// <see cref="ColorDeLaPastilla.Naranja"/>.</para>
///
/// <para>Vive aquí y no en <see cref="PinturaDelGrupo"/> por lo mismo que
/// <c>ColoresDeLaPastilla</c> vive fuera de <c>PinturaDeInicio</c>: aquella clase crea
/// pinceles, que no existen sin el tiempo de ejecución de XAML, y la DECISIÓN tiene que
/// poder medirse en una prueba sin abrir ventana (ADR-0003 §8.1).</para>
/// </remarks>
public static class ColorDelRenglon
{
    /// <summary>El color a partir de la palabra, del matiz y de si el documento está archivado.</summary>
    /// <remarks>Es la ÚNICA regla; las otras dos formas de llamar solo desempaquetan una lectura.</remarks>
    /// <param name="lo">Lo que se lee: resuelto o me falta.</param>
    /// <param name="aMedias">Si es «me falta» a medias: alguna de las seis en sí y no las seis.</param>
    /// <param name="archivado">Si el documento del que sale está archivado; entonces se lee resuelto.</param>
    public static ColorDeLaPastilla De(LoQueSeLee lo, bool aMedias, bool archivado)
    {
        if (lo == LoQueSeLee.Resuelto || archivado) return ColorDeLaPastilla.Verde;
        return aMedias ? ColorDeLaPastilla.Naranja : ColorDeLaPastilla.Rojo;
    }

    /// <summary>El color de una persona a partir de su lectura y de si su documento está archivado.</summary>
    /// <param name="lectura">Lo que se lee de ella, con su matiz.</param>
    /// <param name="archivado">Si el documento del que sale está archivado; entonces se lee resuelto.</param>
    public static ColorDeLaPastilla De(LoQueSeLeeDeUnaPersona lectura, bool archivado)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        return De(lectura.Lo, lectura.AMedias, archivado);
    }

    /// <summary>El color de un documento a partir de su lectura, que ya sabe si está archivado.</summary>
    /// <param name="lectura">Lo que se lee del documento, con su matiz.</param>
    public static ColorDeLaPastilla De(LoQueSeLeeDeUnDocumento lectura)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        return De(lectura.Lo, lectura.AMedias, archivado: false);
    }

    /// <summary>La forma de antes del 2026-09-16, sin matiz: verde o rojo.</summary>
    /// <remarks>Se queda para las pruebas que fijaron la regla del 14; no hay ninguna pantalla que la use.</remarks>
    /// <param name="lo">Lo que se lee: resuelto o me falta.</param>
    /// <param name="archivado">Si el documento del que sale está archivado; entonces se lee resuelto.</param>
    public static ColorDeLaPastilla De(LoQueSeLee lo, bool archivado) => De(lo, aMedias: false, archivado);
}
