using Microsoft.UI.Text;
using Windows.UI.Text;

namespace Fichas.App.Asignar;

/// <summary>
/// Lo poco que el panel de grupos de Asignar necesita decidir para pintarse.
/// </summary>
/// <remarks>
/// <para>⛔ <b>No define ni un color.</b> Los colores del panel salen de los recursos de tema
/// de la cáscara —<c>Papel</c>, <c>Panel</c>, <c>Linea</c>, <c>Tinta</c>—, que es lo que hace
/// que esta pantalla se vea igual que las demás en los dos temas. Aquí solo vive lo que el
/// XAML no puede expresar sin un método: el grosor de la letra según el renglón sea un día de
/// viaje o una de sus unidades.</para>
///
/// <para>Va en una clase y no en <see cref="RenglonDeGrupoParaAsignar"/> a propósito: el
/// renglón se prueba sin ventana, y un <see cref="FontWeight"/> dentro lo ataría al marco
/// gráfico para no ganar nada.</para>
/// </remarks>
public static class PinturaDeLosGrupos
{
    /// <summary>
    /// El grosor de la letra de un renglón del panel: la fecha manda, la unidad cuelga.
    /// </summary>
    /// <remarks>
    /// Es la única jerarquía que tiene el panel, porque va aplanado: sin ella, «Grupo del 12
    /// de septiembre» y «325535 · Rama San Juan» se leen como dos cosas del mismo nivel, y no
    /// lo son — la segunda está DENTRO de la primera.
    /// </remarks>
    /// <param name="esLaFecha">Si el renglón es la cabecera de un día de viaje.</param>
    public static FontWeight Grosor(bool esLaFecha) => esLaFecha ? FontWeights.Bold : FontWeights.Normal;
}
