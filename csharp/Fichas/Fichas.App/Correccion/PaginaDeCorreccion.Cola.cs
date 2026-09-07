using Fichas.App.Completar;
using Fichas.App.Grupo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Correccion;

/// <summary>
/// El encadenado: lo que pasa en esta pantalla cuando se llega DESDE la cola de completar.
/// </summary>
/// <remarks>
/// <para>Palabras del dueno, 2026-09-06: <i>«Cuando algo no leído se lee y yo lo reviso y le
/// doy guardar, debe pasar a otro renglón de "listo para asignar" y me envía a mí a la
/// pantalla donde está todo lo que no está completo, para yo seguir trabajando»</i>. Lo que
/// faltaba no era el veredicto —<see cref="ModeloDeCorreccion.ListoParaAsignar"/> existe
/// desde el 5— sino que guardar sacara del documento y pusiera delante el siguiente.</para>
///
/// <para>⛔ <b>Aqui no se firma nada, y esa es la linea que no se cruza.</b> La regla
/// permanente 5 de <c>CLAUDE.md</c>: la firma de campos —<c>procedencia_campo.verificado</c>—
/// es de Miguel y nunca es automatica. Este archivo lee <c>ListoParaAsignar</c>, que es la
/// respuesta a «¿le falta algo?», y con ella decide a que documento ir. No escribe una sola
/// fila; lo unico que escribe en toda la cadena es <c>Guardar</c>, que ya escribia lo mismo
/// antes de que existiera la cola.</para>
///
/// <para><b>Sin cola, esta pantalla no cambia en nada.</b> Todo lo de aqui esta detras de
/// <c>_cola is not null</c>: quien entre por la pestana de Correccion a un documento suelto
/// ve exactamente lo que veia el 2026-09-05, banda incluida —que no sale—.</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    private ColaDeCompletar? _cola;
    private int _resueltosEnEstaVuelta;

    /// <summary>La cola que se esta recorriendo, o nula si se entro a un documento suelto.</summary>
    public ColaDeCompletar? ColaEnLaMano => _cola;

    /// <summary>Cuantos documentos han salido de la cola en esta vuelta.</summary>
    public int ResueltosEnEstaVuelta => _resueltosEnEstaVuelta;

    /// <summary>
    /// Entra en el modo cola: se recorre esa cola y se empieza por ese documento.
    /// </summary>
    /// <remarks>
    /// La llama <c>Completar/PaginaDeCompletar</c> justo despues de navegar aqui. La cola
    /// llega VIVA y por referencia: lo que sale de ella mientras se encadena sigue fuera al
    /// volver a la pestana, sin releer la base.
    /// </remarks>
    /// <param name="cola">La cola de trabajo.</param>
    /// <param name="casoId">El documento por el que se empieza.</param>
    public void EntrarEnLaCola(ColaDeCompletar cola, long casoId)
    {
        ArgumentNullException.ThrowIfNull(cola);

        _cola = cola;
        _resueltosEnEstaVuelta = 0;
        AbrirElCaso(casoId);
        PintarLaBandaDeLaCola();
    }

    /// <summary>
    /// Lo que hace la cola despues de guardar: sacar al resuelto y abrir el siguiente.
    /// </summary>
    /// <remarks>
    /// <para>Se llama desde <c>AlPulsarGuardar</c> y solo hace algo si hay cola. El
    /// veredicto que decide NO se calcula aqui: llega ya hecho en el resultado del guardado,
    /// que es el mismo que pinta el acuse y la cabecera. Dos formas de decidir lo mismo se
    /// separarian el dia que alguien toque una.</para>
    ///
    /// <para>⚠️ Y si el documento sigue incompleto NO pasa nada, que es lo pedido: <i>«Si no
    /// quedó completo, se queda en la cola con lo que le falta a la vista»</i>. Lo que le
    /// falta ya esta a la vista: son los campos senalados de la propia pantalla.</para>
    /// </remarks>
    /// <param name="resultado">Lo que devolvio el guardado que acaba de ocurrir.</param>
    private void SeguirLaCola(ResultadoDeGuardado resultado)
    {
        if (_cola is null || Servicios is null) return;

        var numero = _modelo?.Caso?.NumeroCaso ?? "este documento";

        if (!resultado.ListoParaAsignar)
        {
            _cola.DejarYDecirCualToca(_casoAbierto);
            Decir(resultado.SinNingunaPersonaLeida
                ? TextoDeLaCola.AlSeguirSinNingunaPersona(numero)
                : TextoDeLaCola.AlSeguirEnLaCola(numero, resultado.CamposQueLeFaltan));
            PintarLaBandaDeLaCola();
            return;
        }

        var siguiente = _cola.SacarYDecirCualToca(_casoAbierto, TodaviaLeFalta);
        _resueltosEnEstaVuelta++;
        var salida = TextoDeLaCola.AlSalirDeLaCola(numero, _cola.Quedan);

        if (siguiente is not long cual)
        {
            // Las dos frases juntas: la del documento que se acaba de resolver y la de que
            // ya no queda nada. Decir solo la segunda dejaria al ultimo documento sin su
            // veredicto, que es justo el que el dueno acaba de trabajar.
            Decir($"{salida} {TextoDeLaCola.AlVaciarseLaCola(_resueltosEnEstaVuelta)}");
            VolverALaCola();
            return;
        }

        Decir(salida);
        AbrirElCaso(cual);
        PintarLaBandaDeLaCola();
    }

    /// <summary>
    /// Si a ese documento le sigue faltando algun dato, preguntado al almacen.
    /// </summary>
    /// <remarks>
    /// Son unas pocas consultas cortas por candidato, y solo se pregunta al avanzar. Es lo que
    /// cuesta que la cola no lleve delante un documento que ya se corrigio por otra via
    /// mientras ella estaba en la mano; el motivo entero esta en <see cref="ColaDeCompletar"/>.
    /// <para>
    /// ⚠️ Desde el 2026-09-06 se lee ademas la procedencia, porque el veredicto la mira. Se
    /// pide <b>de este documento</b> y no de la base entera: leer 29 784 filas para contestar
    /// por siete campos seria pagar la pantalla de Inicio en cada paso de la cola.
    /// </para>
    /// </remarks>
    private bool TodaviaLeFalta(long casoId)
    {
        if (Servicios is null) return false;

        var caso = Servicios.Casos.Obtener(casoId);
        if (caso is null) return false;

        var personas = Servicios.Personas.DeCaso(casoId);
        var procedencias = ProcedenciasDeUnaPasada.DeUnDocumento(Servicios.Procedencia, caso, personas);
        return !LoQueLeFalta.EstaListo(caso, personas, procedencias);
    }

    /// <summary>Vuelve a la pestana de la cola, que es quien dice que ya no queda nada.</summary>
    private void VolverALaCola()
    {
        _cola = null;
        if (Frame is null || Servicios is null) return;
        Frame.Navigate(typeof(PaginaDeCompletar), Servicios, new SuppressNavigationTransitionInfo());
    }

    /// <summary>Sale de la cola sin resolver nada mas y vuelve a la pestana.</summary>
    /// <remarks>
    /// Existe porque el menu de la izquierda no puede traerle de vuelta: la entrada
    /// «Completar» sigue marcada mientras se encadena, y <c>NavigationView</c> solo avisa
    /// cuando la seleccion CAMBIA. Sin este boton, salir de la cola obligaria a pasar por
    /// otra pestana y volver.
    /// </remarks>
    private void AlPulsarVolverALaCola(object quien, RoutedEventArgs cuando) => VolverALaCola();

    /// <summary>
    /// Pinta la banda que dice que se esta dentro de la cola y por donde se va.
    /// </summary>
    /// <remarks>
    /// Sin la banda, entrar desde la cola y entrar por el desplegable se verian igual, y el
    /// dueno no tendria forma de saber por que al guardar cambia de documento. La posicion
    /// va con su denominador —«3 de 8»— por la regla §8 de <c>CLAUDE.md</c>.
    /// </remarks>
    private void PintarLaBandaDeLaCola()
    {
        if (_cola is null)
        {
            _bandaDeLaCola.Visibility = Visibility.Collapsed;
            return;
        }

        var puesto = _cola.PosicionDe(_casoAbierto);
        _bandaDeLaCola.Visibility = Visibility.Visible;
        _enLaCola.Text = puesto.Length == 0
            ? $"Cola de completar · quedan {_cola.Quedan}"
            : $"Cola de completar · {puesto}";
    }
}
