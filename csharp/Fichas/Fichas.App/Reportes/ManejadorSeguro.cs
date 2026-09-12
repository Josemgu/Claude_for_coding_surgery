using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Reportes;

/// <summary>
/// Lo que envuelve a TODO manejador de boton de estas dos pantallas para que ninguno
/// pueda quedarse mudo.
/// </summary>
/// <remarks>
/// <para>⚠️ Existe por un fallo medido, no por precaucion: QA encontro el 2026-09-04 que los
/// dos botones de la pantalla de Importar estaban MUERTOS —al pulsar «Elegir archivos…» no
/// pasaba nada: sin selector, sin error, sin mensaje y sin una linea en <c>fichas.log</c>—.
/// La causa es la forma <c>async void</c>: la excepcion del selector no la recoge nadie y el
/// boton parece que no hace nada. Estas dos pantallas tambien eligen archivo y carpeta, asi
/// que les habria pasado igual.</para>
///
/// <para><b>Se captura <see cref="Exception"/> a secas, y esto NO es silenciar.</b> Lo
/// prohibido del proyecto es capturar para callar; aqui se captura para HABLAR: la linea va
/// a la franja de avisos, el detalle con la excepcion entera va detras de «ver», y ademas
/// queda escrita en <c>fichas.log</c>. Lo unico que no hace es tumbar el programa. En el
/// borde de un manejador de interfaz la alternativa a este catch no es un error limpio: es
/// el boton que no responde y no deja rastro, que es exactamente el fallo que se esta
/// arreglando.</para>
/// </remarks>
public static class ManejadorSeguro
{
    /// <summary>
    /// Corre el trabajo de un boton y, pase lo que pase, deja dicho que paso.
    /// </summary>
    /// <param name="queSePulso">Como se llama el boton en la pantalla; sale en el aviso.</param>
    /// <param name="servicios">De donde salen el buzon de avisos y el cuaderno.</param>
    /// <param name="trabajo">Lo que hace el boton.</param>
    public static void Correr(string queSePulso, Servicios? servicios, Func<Task> trabajo)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // El descarte es a proposito y es seguro: CorrerYContar no deja escapar ninguna
        // excepcion, asi que la tarea nunca queda con un fallo sin observar.
        _ = CorrerYContar(queSePulso, servicios, trabajo);
    }

    /// <summary>El aviso de una linea que deja un manejador que se rompio.</summary>
    /// <remarks>
    /// Es una funcion aparte para poder probarla sin abrir ventana: lo que importa de este
    /// archivo es QUE se dice cuando algo falla, y eso se puede comprobar por los bordes.
    /// </remarks>
    /// <param name="queSePulso">Cómo se llama el botón en la pantalla.</param>
    /// <param name="fallo">Lo que se escapó del trabajo; va entero en el detalle.</param>
    public static Aviso TextoDelFallo(string queSePulso, Exception fallo)
    {
        ArgumentNullException.ThrowIfNull(fallo);

        return Aviso.Problema(
            $"«{queSePulso}» no pudo terminar: {fallo.GetType().Name}.",
            string.Empty,
            $"El programa no se cerró y no se ha perdido nada de lo que ya estaba guardado. "
            + $"Lo que dijo el sistema: {fallo.Message}"
            + Environment.NewLine + Environment.NewLine
            + "Esto mismo quedó escrito en fichas.log, en la carpeta de datos:"
            + Environment.NewLine + fallo);
    }

    /// <summary>Espera al trabajo y, si se rompe, lo deja dicho en los tres sitios.</summary>
    /// <param name="queSePulso">Cómo se llama el botón en la pantalla.</param>
    /// <param name="servicios">De donde salen el buzón y el cuaderno; nulo si la pantalla aún no los tiene, y entonces solo se dice en el pie.</param>
    /// <param name="trabajo">Lo que hace el botón.</param>
    private static async Task CorrerYContar(string queSePulso, Servicios? servicios, Func<Task> trabajo)
    {
        try
        {
            await trabajo().ConfigureAwait(true);
        }
        catch (Exception fallo)
        {
            var aviso = TextoDelFallo(queSePulso, fallo);
            servicios?.Avisos.Dejar(aviso);
            servicios?.Registro.Anotar($"BOTON ROTO  {queSePulso}  {fallo}");
            (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(aviso.Linea);
        }
    }
}
