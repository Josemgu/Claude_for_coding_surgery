using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Importar;

/// <summary>
/// Borrar los renglones de los PDF que no se pudieron leer: copiar, preguntar con la cifra
/// delante, borrar, decirlo y anotarlo.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Este es el SEGUNDO sitio del programa que abre un cuadro, y hay que decir por
/// qué.</b> El primero es <c>Revisar/OperacionDeBorrar.cs</c>, que se declara «el único
/// sitio de toda la interfaz que abre un cuadro y detiene el trabajo». Sigue siendo el
/// único camino para borrar DOCUMENTOS; esto borra renglones de
/// <c>documentos_ilegibles</c>, que es otro puerto —<see cref="IIlegibles"/>— y aquella
/// clase está atada a <see cref="IMantenimiento"/> en su firma.
/// </para>
/// <para>
/// ⛔ <b>Lo correcto sería una sola clase para los dos</b>, y no se hizo aquí porque
/// <c>Fichas.App/Revisar/</c> está fuera del terreno de escritura de este pase: generalizar
/// <c>OperacionDeBorrar</c> es una decisión que vuelve al supervisor. Mientras tanto, lo
/// que evita que sean dos sitios donde alguien se olvide de la copia es que el ORDEN no se
/// copia de aquella clase: se hereda del puerto —la copia se hace dentro de
/// <see cref="IIlegibles.PlanearBorradoDeRenglonesSinCaso"/>, antes de preguntar— y
/// <see cref="IIlegibles.BorrarRenglonesSinCaso"/> se niega a ejecutar un plan sin copia.
/// Olvidarla aquí no es posible: no se hace aquí.
/// </para>
/// <para>
/// Borrar es la excepción declarada al requisito 9 —«avisar, nunca impedir»—
/// (DECISIONES.md 2026-09-04, punto 9), porque es lo único que no tiene vuelta atrás desde
/// el programa. Y esta clase queda vigilada por
/// <c>Fichas.Pruebas.App/Importar/PruebasSinCuadrosEnImportar.cs</c>, igual que aquella lo
/// está por la suya: una excepción que nadie vigila es una puerta abierta.
/// </para>
/// </remarks>
public sealed class OperacionDeBorrarLosPdfIlegibles
{
    private readonly IIlegibles _ilegibles;
    private readonly BuzonDeAvisos _avisos;
    private readonly Registro _registro;

    /// <summary>Ata la operación al puerto, al buzón de la franja y al cuaderno.</summary>
    /// <param name="ilegibles">El puerto que planea y borra los renglones.</param>
    /// <param name="avisos">Donde se dejan los motivos de lo que no se pudo hacer.</param>
    /// <param name="registro">El cuaderno donde queda escrito qué se borró y cuándo.</param>
    public OperacionDeBorrarLosPdfIlegibles(IIlegibles ilegibles, BuzonDeAvisos avisos, Registro registro)
    {
        ArgumentNullException.ThrowIfNull(ilegibles);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(registro);

        _ilegibles = ilegibles;
        _avisos = avisos;
        _registro = registro;
    }

    /// <summary>
    /// Prepara el borrado, lo pregunta y —solo si el dueño dice que sí— lo hace.
    /// </summary>
    /// <param name="renglonIds">Los renglones marcados en la lista.</param>
    /// <param name="raiz">La raíz visual sobre la que se levanta el cuadro.</param>
    /// <returns>La línea del acuse, o nulo si no se borró nada.</returns>
    public async Task<string?> PreguntarYBorrar(IReadOnlyCollection<long> renglonIds, XamlRoot raiz)
    {
        ArgumentNullException.ThrowIfNull(renglonIds);
        if (renglonIds.Count == 0) return null;

        // ⛔ Aquí dentro se hace la COPIA, antes de preguntar nada. Es lo que permite que la
        // pregunta diga dónde quedó.
        var plan = _ilegibles.PlanearBorradoDeRenglonesSinCaso(renglonIds);

        if (!plan.SePuedeBorrar)
        {
            _avisos.Dejar(plan.Avisos);
            return null;
        }

        // Un renglón marcado que no entró en el plan —porque ya tenía documento— se dice
        // igual, aunque el resto sí se vaya a borrar.
        _avisos.Dejar(plan.Avisos);

        if (!await LoConfirma(plan, raiz).ConfigureAwait(true))
        {
            // Dijo que no: no se toca la base. La copia se queda donde está, y se dice
            // dónde, porque un archivo que aparece sin avisar es peor que uno anunciado.
            _registro.Anotar(
                $"BORRADO DE ILEGIBLES CANCELADO POR EL DUENO  copia previa en «{plan.RutaDeLaCopia}»");
            return $"No se borró nada. La copia previa quedó en «{plan.RutaDeLaCopia}».";
        }

        var resultado = _ilegibles.BorrarRenglonesSinCaso(plan);
        _avisos.Dejar(resultado.Avisos);
        _registro.Anotar(resultado.LineaDelRegistro);
        return resultado.Linea;
    }

    /// <summary>
    /// El cuadro con la cifra delante y con la frase de que el PDF del disco se queda.
    /// </summary>
    /// <remarks>
    /// El botón por defecto es el que NO borra: si alguien pulsa Intro sin leer, no pasa
    /// nada. Y el texto se puede seleccionar para poder copiar la ruta de la copia.
    /// </remarks>
    private static async Task<bool> LoConfirma(PlanDeBorrado plan, XamlRoot raiz)
    {
        var cuerpo = new TextBlock
        {
            Text = TextosDeBorrarLosPdfIlegibles.Pregunta(plan),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var cuadro = new ContentDialog
        {
            XamlRoot = raiz,
            Title = TextosDeBorrarLosPdfIlegibles.Titulo(plan),
            Content = new ScrollViewer { Content = cuerpo, MaxHeight = 320 },
            PrimaryButtonText = TextosDeBorrarLosPdfIlegibles.TextoDelBoton(plan),
            CloseButtonText = PlanDeBorrado.TextoDelBotonQueNoBorra,
            DefaultButton = ContentDialogButton.Close,
        };

        return await cuadro.ShowAsync() == ContentDialogResult.Primary;
    }
}
