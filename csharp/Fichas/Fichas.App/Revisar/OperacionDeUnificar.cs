using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Fichas.App.Revisar;

/// <summary>
/// Unificar un duplicado con su original desde la tarjeta: copiar, preguntar con lo que va a
/// pasar delante, unificar, decirlo y anotarlo. Y, si el original ya no está, quitar la marca.
/// </summary>
/// <remarks>
/// <para>Unificar <b>borra</b> el duplicado, y por eso pregunta con un cuadro: es la excepción
/// declarada a «avisar, nunca impedir» (DECISIONES.md 2026-09-04, punto 9). El cuadro NO está
/// aquí: sale por <see cref="OperacionDeBorrar.PreguntarAntesDeBorrar"/>, la única puerta de
/// Revisar que puede preguntar, y <c>PruebasSinCuadros</c> lo vigila. Quitar la marca no borra
/// nada, así que pregunta con un menú ligero que se cierra pulsando fuera y no detiene nada.</para>
///
/// <para>Lo que se decide —qué pasa y qué no— NO está aquí: lo decide la base en
/// <see cref="IMantenimiento.PlanearUnificacion"/>, y los textos salen del
/// <see cref="PlanDeUnificacion"/>, que se prueba sin ventana. Aquí solo se pasan mensajes.</para>
/// </remarks>
public sealed class OperacionDeUnificar
{
    /// <summary>El puerto que copia, unifica y quita la marca; nulo con datos inventados.</summary>
    private readonly IMantenimiento? _mantenimiento;
    /// <summary>De dónde sale «hoy», la fecha con la que se retiran las asignaciones vivas.</summary>
    private readonly IReloj _reloj;
    /// <summary>La franja de la cáscara, donde se dice por qué no se pudo.</summary>
    private readonly BuzonDeAvisos _avisos;
    /// <summary>El cuaderno <c>fichas.log</c>: toda unificación y toda cancelada quedan anotadas.</summary>
    private readonly Registro _registro;

    /// <summary>Ata la operación al puerto, al reloj, al buzón de la franja y al cuaderno.</summary>
    /// <param name="mantenimiento">El puerto, o NULO si el programa abrió con datos inventados: entonces no se unifica nada.</param>
    /// <param name="reloj">De dónde sale «hoy».</param>
    /// <param name="avisos">Dónde se dejan los motivos de lo que no se pudo hacer.</param>
    /// <param name="registro">El cuaderno donde queda escrito qué se unificó y cuándo.</param>
    public OperacionDeUnificar(IMantenimiento? mantenimiento, IReloj reloj, BuzonDeAvisos avisos, Registro registro)
    {
        _mantenimiento = mantenimiento;
        _reloj = reloj;
        _avisos = avisos;
        _registro = registro;
    }

    /// <summary>Lo que dice el botón del menú ligero que quita la marca.</summary>
    public const string TextoDelBotonDeQuitarLaMarca = "Quitar la marca";

    /// <summary>Si hay con qué unificar; falso con datos inventados o sin base.</summary>
    public bool SePuedeUnificarAqui => _mantenimiento is not null;

    /// <summary>
    /// Prepara la unificación, la pregunta y —solo si el dueño dice que sí— la hace.
    /// </summary>
    /// <param name="duplicadoId">El documento marcado como duplicado.</param>
    /// <param name="raiz">La raíz visual sobre la que se levanta el cuadro.</param>
    /// <returns>La línea del acuse, o nula si no hubo nada que acusar.</returns>
    public async Task<string?> PreguntarYUnificar(long duplicadoId, XamlRoot raiz)
    {
        if (_mantenimiento is null)
        {
            _avisos.Dejar(SinBase("unificar"));
            return null;
        }

        // ⛔ Aquí dentro se hace la COPIA, antes de preguntar nada.
        var plan = _mantenimiento.PlanearUnificacion(duplicadoId, _reloj.Hoy());
        if (!plan.SePuedeUnificar)
        {
            _avisos.Dejar(plan.Avisos);
            return null;
        }

        var confirmado = await OperacionDeBorrar.PreguntarAntesDeBorrar(
            plan.Titulo, plan.Pregunta, plan.TextoDelBoton, PlanDeUnificacion.TextoDelBotonQueNoUnifica, raiz)
            .ConfigureAwait(true);
        if (!confirmado)
        {
            _registro.Anotar($"UNIFICACION CANCELADA POR EL DUENO  copia previa en «{plan.RutaDeLaCopia}»");
            return $"No se unificó nada. La copia previa quedó en «{plan.RutaDeLaCopia}».";
        }

        var resultado = _mantenimiento.Unificar(plan);
        _avisos.Dejar(resultado.Avisos);
        _registro.Anotar(resultado.LineaDelRegistro);
        return resultado.Linea;
    }

    /// <summary>
    /// Abre sobre el botón un menú ligero que pregunta, y con su botón le quita la marca.
    /// </summary>
    /// <remarks>
    /// Un <see cref="Flyout"/> y no un cuadro: se cierra pulsando fuera y no detiene la pantalla
    /// (requisito 9). Lo que hace cuando se pulsa se le pasa en <paramref name="alQuitarla"/>,
    /// para que la pantalla acuse y repinte como con todo lo demás.
    /// </remarks>
    /// <param name="tarjeta">La tarjeta del documento, para nombrarlo en la pregunta.</param>
    /// <param name="boton">El botón de la tarjeta sobre el que se abre el menú.</param>
    /// <param name="alQuitarla">Qué hacer con la línea del acuse cuando se quitó, o con nula si no se pudo.</param>
    public void PreguntarYQuitarLaMarca(TarjetaDeDocumento tarjeta, FrameworkElement boton, Action<string?> alQuitarla)
    {
        ArgumentNullException.ThrowIfNull(tarjeta);
        ArgumentNullException.ThrowIfNull(boton);
        ArgumentNullException.ThrowIfNull(alQuitarla);

        if (_mantenimiento is null)
        {
            _avisos.Dejar(SinBase("quitar la marca"));
            return;
        }

        var pregunta = new TextBlock
        {
            Text = $"{tarjeta.Archivo} está marcado como {tarjeta.MarcaDeDuplicado}. "
                   + "No hay con qué unificarlo. Sin la marca pasa a ser un documento normal: "
                   + "no se borra nada y no se mueve nada.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        };
        var quitar = new Button { Content = TextoDelBotonDeQuitarLaMarca, HorizontalAlignment = HorizontalAlignment.Right };
        var menu = new Flyout
        {
            Content = new StackPanel { Spacing = 10, Children = { pregunta, quitar } },
            Placement = FlyoutPlacementMode.Bottom,
        };
        quitar.Click += (_, _) =>
        {
            menu.Hide();
            alQuitarla(QuitarLaMarca(tarjeta));
        };

        menu.ShowAt(boton);
    }

    /// <summary>Le quita la marca al documento y compone la línea del acuse.</summary>
    /// <param name="tarjeta">El documento.</param>
    /// <returns>La línea del acuse, o nula si no se escribió; el motivo queda en la franja.</returns>
    private string? QuitarLaMarca(TarjetaDeDocumento tarjeta)
    {
        if (_mantenimiento is null) return null;

        var resultado = _mantenimiento.QuitarLaMarcaDeDuplicado(tarjeta.CasoId);
        _avisos.Dejar(resultado.Avisos);
        if (!resultado.SeEscribio) return null;

        _registro.Anotar($"MARCA DE DUPLICADO QUITADA  caso {tarjeta.CasoId}");
        return $"{tarjeta.Archivo} ya no está marcado como duplicado.";
    }

    /// <summary>El aviso de que con datos inventados no hay base con la que hacer esto.</summary>
    /// <param name="que">Qué se intentaba: «unificar» o «quitar la marca».</param>
    private static Aviso SinBase(string que)
        => Aviso.Problema(
            $"Aquí no se puede {que}: el programa abrió con datos inventados.",
            string.Empty,
            "Exige copiar antes la base de verdad, y con «--falso» no hay ninguna. Cierra el programa y ábrelo normal.");
}
