using Fichas.Contratos.Modelos;
using Fichas.App.Cascara;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Correccion;

/// <summary>
/// Eliminar a una persona del documento abierto: preguntar con el nombre delante, borrar,
/// decirlo y anotarlo.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Este es el TERCER sitio del programa que abre un cuadro, y hay que decir por qué.</b>
/// El primero es <c>Revisar/OperacionDeBorrar.cs</c>, el único camino para borrar DOCUMENTOS
/// —y por él va también «Eliminar el documento entero» desde esta pantalla—; el segundo es
/// <c>Importar/OperacionDeBorrarLosPdfIlegibles.cs</c>. Este borra UNA persona, que es otro
/// puerto —<see cref="Fichas.Contratos.Puertos.IPersonas.Borrar"/>— y aquella clase está atada
/// a <c>IMantenimiento</c> en su firma; generalizarla es una decisión que vuelve al supervisor,
/// como ya se dijo en Importar.
/// </para>
/// <para>
/// Borrar es la excepción declarada al requisito 9 —«avisar, nunca impedir»— (DECISIONES.md
/// 2026-09-04, punto 9), porque es lo único que no tiene vuelta atrás desde el programa. Y esta
/// clase queda vigilada por <c>Fichas.Pruebas.App/Correccion/PruebasSinCuadrosEnCorreccion.cs</c>:
/// una excepción que nadie vigila es una puerta abierta.
/// </para>
/// <para>
/// ⛔ <b>Aquí no se decide nada de lo que se borra.</b> A quién, con qué palabras y qué queda lo
/// decide <see cref="ModeloDeCorreccion"/>, que se prueba sin ventana; la copia previa la hace el
/// puerto. Esto pregunta, llama y anota.
/// </para>
/// </remarks>
public sealed class OperacionDeEliminarUnaPersona
{
    /// <summary>El modelo de la pantalla, que planea y elimina.</summary>
    private readonly ModeloDeCorreccion _modelo;
    /// <summary>La franja de avisos de la ventana, donde se dejan los motivos de lo que no se pudo.</summary>
    private readonly BuzonDeAvisos _avisos;
    /// <summary>El cuaderno <c>fichas.log</c>: toda eliminación y toda eliminación cancelada quedan anotadas.</summary>
    private readonly Registro _registro;

    /// <summary>Ata la operación al modelo, al buzón de la franja y al cuaderno.</summary>
    /// <param name="modelo">El modelo de la pantalla, con el documento abierto.</param>
    /// <param name="avisos">Donde se dejan los motivos de lo que no se pudo hacer.</param>
    /// <param name="registro">El cuaderno donde queda escrito qué se eliminó y cuándo.</param>
    public OperacionDeEliminarUnaPersona(ModeloDeCorreccion modelo, BuzonDeAvisos avisos, Registro registro)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(registro);

        _modelo = modelo;
        _avisos = avisos;
        _registro = registro;
    }

    /// <summary>
    /// Arma la pregunta, la hace y —solo si él dice que sí— elimina a la persona.
    /// </summary>
    /// <param name="personaId">A quién.</param>
    /// <param name="raiz">La raíz visual sobre la que se levanta el cuadro.</param>
    /// <returns>Lo que devolvió el modelo al eliminar; o nulo si no había nada que preguntar o él dijo que no, y entonces la franja o el pie ya lo dicen.</returns>
    public async Task<ResultadoDeEliminarPersona?> PreguntarYEliminar(long personaId, XamlRoot raiz)
    {
        if (_modelo.PlanearEliminarPersona(personaId) is not PreguntaDeEliminarPersona pregunta)
        {
            _avisos.Dejar(Aviso.Advierte(TextoDeEliminar.NoEsDeEsteDocumento));
            return null;
        }

        if (!await LoConfirma(pregunta, raiz).ConfigureAwait(true))
        {
            // Dijo que no: no se toca la base y se anota, sin el nombre —el cuaderno de tiempos
            // no es sitio para datos de personas—.
            _registro.Anotar($"ELIMINAR PERSONA CANCELADO POR EL DUENO  persona {personaId}");
            return null;
        }

        var resultado = _modelo.EliminarPersona(personaId);
        _avisos.Dejar(resultado.Avisos);
        _registro.Anotar(resultado.SeBorro
            ? $"ELIMINADA PERSONA {personaId}  quedan {resultado.QuedanPersonas}  "
              + (resultado.RutaDeLaCopia is null ? "sin copia" : $"copia previa en «{resultado.RutaDeLaCopia}»")
            : $"ELIMINAR PERSONA NO EJECUTADO  persona {personaId}");
        return resultado;
    }

    /// <summary>
    /// El cuadro con el nombre delante. Lo único de esta pantalla que detiene el trabajo.
    /// </summary>
    /// <remarks>
    /// El botón por defecto es el que NO elimina: si alguien pulsa Intro sin leer, no pasa
    /// nada. Y el texto se puede seleccionar.
    /// </remarks>
    /// <param name="pregunta">La pregunta ya armada por el modelo.</param>
    /// <param name="raiz">La raíz visual sobre la que se levanta el cuadro.</param>
    /// <returns>Verdadero solo si pulsó el botón que elimina.</returns>
    private static async Task<bool> LoConfirma(PreguntaDeEliminarPersona pregunta, XamlRoot raiz)
    {
        var cuerpo = new TextBlock
        {
            Text = pregunta.Pregunta,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var cuadro = new ContentDialog
        {
            XamlRoot = raiz,
            Title = pregunta.Titulo,
            Content = new ScrollViewer { Content = cuerpo, MaxHeight = 320 },
            PrimaryButtonText = pregunta.TextoDelBoton,
            CloseButtonText = TextoDeEliminar.BotonQueNoElimina,
            DefaultButton = ContentDialogButton.Close,
        };

        return await cuadro.ShowAsync() == ContentDialogResult.Primary;
    }
}
