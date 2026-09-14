using Fichas.App.Inicio;
using Fichas.App.Vocabulario;

namespace Fichas.App.Grupo;

/// <summary>
/// De qué color va el renglón de una persona dentro de la fecha: verde si está resuelta,
/// rojo si le falta algo.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-14:</b> <i>«Quiero que cuando des clic y entres a la
/// fecha, lo que esté completo se marque en verde y lo que no en rojo, para saber cuáles
/// fueron completados y cuáles no, como se muestra en el calendario afuera»</i>.</para>
///
/// <para>⛔ <b>No es un tercer criterio.</b> Son las dos palabras del 2026-09-07 —«resuelto»
/// y «me falta», <see cref="LoQueSeLeeDeUnaPersona"/>— traducidas a los dos colores que el
/// calendario ya usa, y con la misma excepción con la que el calendario cuenta «resueltas»:
/// un archivado se lee resuelto aunque sus seis preguntas no digan que sí, porque archivar
/// es el gesto con el que el dueño cierra un documento (<i>«aunque se archive, debe quedarse
/// en el calendario marcado en verde»</i>, 2026-09-07; <c>LectorDeGrupos.ArmarLaPastilla</c>:
/// <c>confirmada || caso.Archivado</c>).</para>
///
/// <para>Devuelve <see cref="ColorDeLaPastilla"/> y no un tipo nuevo a propósito: es el MISMO
/// par de colores de fuera, y un segundo tipo con los mismos dos valores es como empiezan a
/// separarse. Una persona nunca sale en gris —el gris es de una unidad sin nadie leído, y una
/// persona es alguien—.</para>
///
/// <para>Vive aquí y no en <see cref="PinturaDelGrupo"/> por lo mismo que
/// <c>ColoresDeLaPastilla</c> vive fuera de <c>PinturaDeInicio</c>: aquella clase crea
/// pinceles, que no existen sin el tiempo de ejecución de XAML, y la DECISIÓN tiene que
/// poder medirse en una prueba sin abrir ventana (ADR-0003 §8.1).</para>
/// </remarks>
public static class ColorDelRenglon
{
    /// <summary>El color de una persona a partir de su lectura y de si su documento está archivado.</summary>
    /// <param name="lo">Lo que se lee de ella: resuelto o me falta.</param>
    /// <param name="archivado">Si el documento del que sale está archivado; entonces se lee resuelto.</param>
    public static ColorDeLaPastilla De(LoQueSeLee lo, bool archivado)
        => lo == LoQueSeLee.Resuelto || archivado ? ColorDeLaPastilla.Verde : ColorDeLaPastilla.Rojo;
}
