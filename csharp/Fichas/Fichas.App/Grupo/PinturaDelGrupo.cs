using Fichas.App.Inicio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Fichas.App.Grupo;

/// <summary>
/// El color de las pantallas del grupo y de lo incompleto.
/// </summary>
/// <remarks>
/// ⛔ <b>No define ni un color propio: los pide todos a <see cref="PinturaDeInicio"/>.</b> Las
/// tres pantallas son la misma cosa vista por tres sitios, y dos paletas parecidas se separan
/// el dia que alguien cambie una. Esta clase existe solo para que el XAML de esta carpeta
/// pueda nombrar lo que necesita con su propio prefijo.
/// </remarks>
public static class PinturaDelGrupo
{
    /// <summary>El fondo de una tarjeta.</summary>
    public static Brush Papel => PinturaDeInicio.Papel;

    /// <summary>El fondo de la pantalla.</summary>
    public static Brush Panel => PinturaDeInicio.Panel;

    /// <summary>La cabecera de una unidad o de una fecha dentro de una lista.</summary>
    public static Brush PanelHondo => PinturaDeInicio.PanelHondo;

    /// <summary>El borde de una tarjeta.</summary>
    public static Brush Linea => PinturaDeInicio.Linea;

    /// <summary>El texto normal.</summary>
    public static Brush Tinta => PinturaDeInicio.Tinta;

    /// <summary>El texto secundario.</summary>
    public static Brush TintaSuave => PinturaDeInicio.TintaSuave;

    /// <summary>Las notas al pie.</summary>
    public static Brush Apagado => PinturaDeInicio.Apagado;

    /// <summary>La palabra ARCHIVADO: se ve, pero no llama.</summary>
    public static Brush GrisMarca => PinturaDeInicio.GrisMarca;

    /// <summary>El acuse de lo que acaba de pasar al asignar.</summary>
    public static Brush AzulMarca => PinturaDeInicio.AzulMarca;

    /// <summary>El rojo de la cabecera «Sin completar»; el mismo en los dos temas.</summary>
    public static Brush CabeceraRoja => PinturaDeInicio.CabeceraRoja;

    /// <summary>El borde inferior de esa cabecera.</summary>
    public static Brush CabeceraRojaOscura => PinturaDeInicio.CabeceraRojaOscura;

    /// <summary>El texto de una cabecera de color: blanco en los dos temas.</summary>
    public static Brush TintaDeCabecera => PinturaDeInicio.TintaDeCabecera;

    /// <summary>
    /// El detalle de un renglon; en rojo cuando hay que mirarlo —el PDF que no esta en su
    /// ruta (C13-6), o una fecha que ya paso—.
    /// </summary>
    /// <param name="hayQueMirarlo">Si el detalle avisa de algo: entonces va en rojo.</param>
    public static Brush TintaDelDetalle(bool hayQueMirarlo) => PinturaDeInicio.TintaDelDetalle(hayQueMirarlo);

    // ---- el verde, el naranja y el rojo de cada renglon, los del calendario (2026-09-14/16) ----

    /// <summary>
    /// El fondo del renglon de una persona: el de la pastilla verde si esta resuelta, el
    /// naranja si esta a medias, el de la roja si le falta todo.
    /// </summary>
    /// <remarks>
    /// <para>Palabras del dueno, 2026-09-14: <i>«lo que este completo se marque en verde y lo que
    /// no en rojo, como se muestra en el calendario afuera»</i>; y 2026-09-16: <i>«las personas
    /// que se han completado, por ejemplo 4 preguntas de las 6, deben pasar a color naranja»</i>.
    /// Son <c>VerdeFondo</c>, <c>NaranjaFondo</c> y <c>RojoFondo</c> de
    /// <see cref="PinturaDeInicio"/>, los mismos pinceles con los que se pinta la pastilla del
    /// dia y la capa de la tarjeta de Revisar, en los dos temas; aqui no nace ningun color.</para>
    /// <para>La DECISION del color no esta aqui: viene ya tomada en
    /// <see cref="RenglonDelGrupo.Color"/>, que se puede probar sin ventana. Esta funcion solo
    /// traduce un valor a un pincel.</para>
    /// </remarks>
    /// <param name="color">El color que el modelo ya decidio para el renglon.</param>
    public static Brush FondoDelRenglon(ColorDeLaPastilla color) => color switch
    {
        ColorDeLaPastilla.Verde => PinturaDeInicio.VerdeFondo,
        ColorDeLaPastilla.Naranja => PinturaDeInicio.NaranjaFondo,
        ColorDeLaPastilla.Rojo => PinturaDeInicio.RojoFondo,
        _ => Papel,
    };

    /// <summary>
    /// La franja de la izquierda del renglon o de la cabecera, que es lo que se ve de lejos:
    /// verde, naranja, rojo, o gris en la cabecera de una unidad sin nadie leido.
    /// </summary>
    /// <remarks>
    /// Es la misma raya que lleva la pastilla en el calendario (<c>BordeDeLaPastilla</c>), con
    /// los mismos pinceles y en el mismo sitio, para que dentro de la fecha se lea igual que
    /// fuera; el naranja solo lo trae una persona, nunca la cabecera.
    /// </remarks>
    /// <param name="color">El color que el modelo ya decidio.</param>
    public static Brush FranjaDelRenglon(ColorDeLaPastilla color) => color switch
    {
        ColorDeLaPastilla.Verde => PinturaDeInicio.VerdeMarca,
        ColorDeLaPastilla.Naranja => PinturaDeInicio.NaranjaMarca,
        ColorDeLaPastilla.Rojo => PinturaDeInicio.RojoMarca,
        _ => GrisMarca,
    };

    /// <summary>
    /// El detalle del renglon de una persona, ya con su color: rojo si hay que mirarlo —el PDF
    /// que no esta—, y si no, la tinta del estado del renglon.
    /// </summary>
    /// <remarks>
    /// El aviso del PDF (C13-6) manda sobre el color del estado: un renglon resuelto cuyo PDF no
    /// esta en su ruta sigue llevando su fondo verde, y la linea que lo dice va en rojo para que
    /// no pase inadvertida. El color del estado va ademas en la franja y en el fondo, asi que
    /// no se pierde.
    /// </remarks>
    /// <param name="hayQueMirarlo">Si el detalle avisa de algo: entonces va en rojo, sea cual sea el estado.</param>
    /// <param name="color">El color que el modelo ya decidio para el renglon.</param>
    public static Brush TintaDelDetalle(bool hayQueMirarlo, ColorDeLaPastilla color)
        => hayQueMirarlo ? PinturaDeInicio.RojoMarca : FranjaDelRenglon(color);

    /// <summary>Traduce un si/no a que se vea o no; el motivo esta en <see cref="PinturaDeInicio.SeVe"/>.</summary>
    /// <param name="si">Si el elemento tiene que verse.</param>
    public static Visibility SeVe(bool si) => PinturaDeInicio.SeVe(si);
}
