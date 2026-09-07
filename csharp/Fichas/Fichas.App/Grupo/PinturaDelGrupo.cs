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
    public static Brush TintaDelDetalle(bool hayQueMirarlo) => PinturaDeInicio.TintaDelDetalle(hayQueMirarlo);

    /// <summary>Traduce un si/no a que se vea o no; el motivo esta en <see cref="PinturaDeInicio.SeVe"/>.</summary>
    public static Visibility SeVe(bool si) => PinturaDeInicio.SeVe(si);
}
