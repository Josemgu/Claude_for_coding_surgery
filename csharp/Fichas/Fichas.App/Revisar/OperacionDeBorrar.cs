using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Revisar;

/// <summary>
/// El unico camino del programa para borrar: copiar, preguntar con el numero delante,
/// borrar, decirlo y anotarlo.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Es el unico sitio de toda la interfaz que abre un cuadro y detiene el trabajo.</b>
/// El requisito 9 dice «avisar, nunca impedir» y las 84 validaciones del programa viejo se
/// convirtieron en lineas de franja justo para no hacer esto. Borrar es la excepcion
/// declarada (DECISIONES.md 2026-09-04, punto 9) porque es lo unico que no tiene vuelta
/// atras desde el programa.
/// </para>
/// <para>
/// Vive en <c>Revisar</c> y la usa tambien <c>Asignar</c> para quitar a un companero, por
/// el mismo criterio con el que <c>OperacionDeAsignar</c> vive en <c>Asignar</c> y la usa
/// <c>Revisar</c>: una sola puerta para una operacion, aunque la empujen dos pantallas.
/// Dos copias de esto serian dos sitios donde alguien puede olvidarse de la copia previa.
/// </para>
/// </remarks>
public sealed class OperacionDeBorrar
{
    private readonly IMantenimiento? _mantenimiento;
    private readonly BuzonDeAvisos _avisos;
    private readonly Registro _registro;

    /// <summary>Ata la operacion al puerto, al buzon de la franja y al cuaderno.</summary>
    /// <param name="mantenimiento">
    /// El puerto que borra, o NULO si el programa abrio con datos inventados. Nulo no es
    /// un descuido: con <c>--falso</c> no hay base que copiar y no se puede borrar nada.
    /// </param>
    /// <param name="avisos">Donde se dejan los motivos de lo que no se pudo hacer.</param>
    /// <param name="registro">El cuaderno donde queda escrito que se borro y cuando.</param>
    public OperacionDeBorrar(IMantenimiento? mantenimiento, BuzonDeAvisos avisos, Registro registro)
    {
        _mantenimiento = mantenimiento;
        _avisos = avisos;
        _registro = registro;
    }

    /// <summary>Si hay con que borrar; falso con datos inventados o sin base.</summary>
    public bool SePuedeBorrarAqui => _mantenimiento is not null;

    /// <summary>
    /// Prepara el borrado, lo pregunta y —solo si el dueno dice que si— lo hace.
    /// </summary>
    /// <param name="planear">Que plan se pide al puerto: unos documentos, todo, o un companero.</param>
    /// <param name="raiz">La raiz visual sobre la que se levanta el cuadro.</param>
    /// <returns>La linea del acuse, o nulo si no se borro nada.</returns>
    public async Task<string?> PreguntarYBorrar(
        Func<IMantenimiento, PlanDeBorrado> planear, XamlRoot raiz)
    {
        ArgumentNullException.ThrowIfNull(planear);

        if (_mantenimiento is null)
        {
            _avisos.Dejar(Aviso.Problema(
                "Aquí no se puede borrar: el programa abrió con datos inventados.",
                string.Empty,
                "Borrar exige copiar antes la base de verdad, y con «--falso» no hay ninguna. "
                + "Cierra el programa y ábrelo normal."));
            return null;
        }

        // ⛔ Aqui dentro se hace la COPIA, antes de preguntar nada. Es lo que permite que
        // la pregunta diga donde quedo.
        var plan = planear(_mantenimiento);

        if (plan.NoHayNadaQueBorrar && !plan.SePuedeBorrar && plan.Avisos.Count == 0)
        {
            return null;
        }

        if (!plan.SePuedeBorrar)
        {
            _avisos.Dejar(plan.Avisos);
            return null;
        }

        if (!await LoConfirma(plan, raiz).ConfigureAwait(true))
        {
            // Dijo que no: no se toca la base. La copia se queda donde esta, y se dice
            // donde, porque un archivo que aparece sin avisar es peor que uno anunciado.
            _registro.Anotar($"BORRADO CANCELADO POR EL DUENO  copia previa en «{plan.RutaDeLaCopia}»");
            return $"No se borró nada. La copia previa quedó en «{plan.RutaDeLaCopia}».";
        }

        var resultado = _mantenimiento.Borrar(plan);
        _avisos.Dejar(resultado.Avisos);
        _registro.Anotar(resultado.LineaDelRegistro);
        return resultado.Linea;
    }

    /// <summary>
    /// El cuadro con el numero delante. Lo unico que detiene el trabajo en este programa.
    /// </summary>
    /// <remarks>
    /// El boton por defecto es el que NO borra: si alguien pulsa Intro sin leer, no pasa
    /// nada. Y el texto se puede seleccionar para poder copiar la ruta de la copia.
    /// </remarks>
    private static async Task<bool> LoConfirma(PlanDeBorrado plan, XamlRoot raiz)
    {
        var cuerpo = new TextBlock
        {
            Text = plan.Pregunta,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var cuadro = new ContentDialog
        {
            XamlRoot = raiz,
            Title = plan.Titulo,
            Content = new ScrollViewer { Content = cuerpo, MaxHeight = 320 },
            PrimaryButtonText = plan.TextoDelBoton,
            CloseButtonText = PlanDeBorrado.TextoDelBotonQueNoBorra,
            DefaultButton = ContentDialogButton.Close,
        };

        return await cuadro.ShowAsync() == ContentDialogResult.Primary;
    }
}
