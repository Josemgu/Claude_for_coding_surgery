using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// La parte del modelo que lleva el atajo del administrador: dar el documento por completo
/// sin firmar campo por campo, y decir quien puso la marca vigente.
/// </summary>
/// <remarks>
/// Aplica la decision del dueno del 2026-09-07 (<c>DECISIONES.md</c>, «Su palabra gana a la
/// del programa»): <i>«Si yo marco algo completo, debe cambiar a completo, no importa si el
/// sistema diga que está mal»</i>. Por eso ninguna condicion sobre lo que le falte al
/// documento esconde el boton ni frena la escritura; lo unico que lo frena es no saber a
/// nombre de quien firmar.
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>
    /// Si se puede ofrecer el atajo: hay un caso abierto y UN solo administrador activo.
    /// </summary>
    /// <remarks>
    /// ADR-0005 §6.3: «si no hay exactamente uno, el boton no esta y se dice por que». Un
    /// boton que aparece y luego contesta que no se puede es peor que uno que no aparece:
    /// obliga a descubrir por ensayo lo que ya se sabia antes de pintarlo.
    /// </remarks>
    public bool HayAtajoDeAdministrador => _caso is not null && ElAdministrador.De(_companeros.Activos()) is not null;

    /// <summary>
    /// Por que no hay atajo, en una linea; vacia cuando si lo hay.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Es la otra mitad de esconder el boton, y sin ella esconderlo es peor que
    /// nada.</b> ADR-0005 §6.3 pide las dos cosas —«el boton no esta <i>y se dice por
    /// que</i>»—, y medido con la ventana abierta el 2026-09-05 solo estaba la primera: sobre
    /// una base sin administrador el sitio del boton quedaba en blanco, sin una palabra, y no
    /// habia forma de saber que faltaba un alta. Un hueco mudo se lee como que la funcion no
    /// existe.
    /// <para>
    /// Con un caso abierto y un administrador dado de alta devuelve vacio, que es lo que
    /// esconde el renglon: el sitio o lleva el boton o lleva el motivo, nunca los dos ni
    /// ninguno.
    /// </para>
    /// </remarks>
    public string PorQueNoHayAtajo
    {
        get
        {
            if (_caso is null) return string.Empty;
            var activos = _companeros.Activos();
            return ElAdministrador.De(activos) is null ? ElAdministrador.PorQueNoSePuede(activos).Linea : string.Empty;
        }
    }

    /// <summary>
    /// Como quedo marcado el documento abierto, en una frase; vacia si nadie lo marco.
    /// </summary>
    /// <remarks>
    /// Sale de la base y no de lo que acaba de pasar en pantalla: es la unica forma de que
    /// lo que se lee y lo que hay guardado no puedan decir cosas distintas.
    /// </remarks>
    public string ComoQuedoLaMarca
        => _caso is null ? string.Empty : TextoDeLaMarcaDelEstado.Componer(_caso, QuienPusoLaMarca(_caso));

    /// <summary>
    /// Da el documento por completo SIN firmar ni un campo. Lo pulsa el administrador.
    /// </summary>
    /// <remarks>
    /// Palabras del dueno: <i>«Yo puedo completarlos tambien, los paquetes, desde el sistema
    /// sin pasar la verificacion, y cuando pase eso debe decir "el administrador lo hizo"»</i>.
    /// <para>
    /// ⛔ <b>Esto NO rompe la regla permanente 5, y no es un automatismo.</b> La regla dice
    /// dos cosas desde que el dueno la preciso el 2026-09-03: que la FIRMA de campos es suya
    /// y nunca automatica, y que el ESTADO de la recomendacion es otra cosa. Esto escribe lo
    /// segundo y no toca lo primero: ni una fila de <c>procedencia_campo</c> pasa a
    /// <c>verificado = 1</c> por este camino, y el unico que lo hace sigue siendo
    /// <see cref="Firmar"/>, que lo pulsa el campo a campo. Que no firma nada va MEDIDO en
    /// <c>PruebasDelAtajoDelAdministrador</c>, no prometido aqui.
    /// </para>
    /// <para>
    /// ⛔ Y no es automatico porque no ocurre solo: es un boton que el pulsa sobre el
    /// documento que tiene delante.
    /// </para>
    /// <para>
    /// ⚠️ <b>Si no hay exactamente un administrador activo, no se escribe NADA</b> y se
    /// devuelve el porque en una linea. Es el defecto que ADR-0005 §6.3 midio sobre la base
    /// viva del dueno, que tiene una sola fila de companero y se llama «Sandy»: la regla
    /// vieja habria dejado la marca firmada por ella. Nunca se firma a nombre de quien no fue.
    /// </para>
    /// <para>
    /// ⛔ <b>No mira lo que le falte al documento</b>, y es por decision del dueno del
    /// 2026-09-07: <i>«Si yo marco algo completo, debe cambiar a completo, no importa si el
    /// sistema diga que está mal»</i>. Un documento con campos vacios se marca igual; lo que
    /// falta sigue dicho en los campos, no como pared.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Si se escribio, con un aviso mas que deja dicho que no se firmo ningun campo; si no,
    /// el motivo en una linea, sin haber tocado la base.
    /// </returns>
    public ResultadoDeEscritura DarPorCompletoComoAdministrador()
    {
        if (_caso is null)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "no hay ningún caso abierto: no hay nada que dar por completo"));
        }

        var activos = _companeros.Activos();
        if (ElAdministrador.De(activos) is not Companero quien)
            return ResultadoDeEscritura.NoSeEscribio(ElAdministrador.PorQueNoSePuede(activos));

        var resultado = _casos.MarcarEstado(
            _caso.Id, EstadoDeRecomendacion.Completa, quien.Id, ElAdministrador.Origen);

        // Se relee de la base pase lo que pase: la frase del pie tiene que decir lo que hay
        // guardado, no lo que se acaba de intentar. Una pantalla que se adelanta a la base es
        // la mentira que mas cuesta descubrir.
        _caso = _casos.Obtener(_caso.Id) ?? _caso;
        if (!resultado.SeEscribio) return resultado;

        return new ResultadoDeEscritura(true, resultado.Id, [.. resultado.Avisos, AvisoDeQueNoSeFirmoNada(quien)]);
    }

    /// <summary>Deja dicho, en el mismo acto, que esto no dio por bueno ni un campo.</summary>
    /// <remarks>
    /// Va como aviso y no callado a proposito: el dueno acaba de saltarse la verificacion, y
    /// que el documento pase a «completa» sin que nadie diga que los campos siguen sin
    /// comprobar se lee como si se hubieran comprobado.
    /// </remarks>
    /// <param name="quien">El administrador que pulso el atajo; su nombre va en la linea.</param>
    private static Aviso AvisoDeQueNoSeFirmoNada(Companero quien) => Aviso.Informa(
        $"documento dado por completo por {quien.Nombre}: {ElAdministrador.Origen}",
        string.Empty,
        "Queda escrito en el documento que la marca la puso el administrador y que fue SIN verificar "
        + "campo por campo. Los campos siguen tal como estaban: esto no ha dado ninguno por bueno.");

    /// <summary>El nombre de quien puso la marca vigente, o nulo si no se puede resolver.</summary>
    /// <remarks>
    /// Nulo y no un nombre de relleno: un hueco se dice callando el nombre, nunca poniendo
    /// el del primero de la lista.
    /// </remarks>
    /// <param name="caso">El caso tal como esta en la base, con <c>estado_marcado_por</c>.</param>
    private string? QuienPusoLaMarca(Caso caso)
        => caso.EstadoMarcadoPor is long id ? _companeros.Obtener(id)?.Nombre : null;
}
