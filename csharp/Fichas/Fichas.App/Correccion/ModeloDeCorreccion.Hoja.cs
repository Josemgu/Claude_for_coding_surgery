using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// La parte del modelo que sabe QUÉ HOJA tiene el dueño delante, y con ella de qué hoja sale
/// lo que teclea.
/// </summary>
/// <remarks>
/// <para><b>Existe por el defecto del 2026-09-15</b> (<see cref="LaHojaQueManda"/>): que el
/// visor se quede en la hoja elegida no basta si la base sigue diciendo que el dato salió de
/// la hoja 1. <c>casos.pagina_pdf</c> y <c>personas.pagina_pdf</c> son lo que leen Revisar y la
/// comprobación de papeles, y una persona tecleada mirando la hoja 2 con <c>pagina_pdf = 1</c>
/// señalaría la factura. Medido en la base del ensayo sobre master: la persona añadida a mano
/// heredaba la hoja del caso, 1, con el formulario en la 2.</para>
///
/// <para><b>La regla, en dos frases para el dueño:</b> un dato que el lector NO trajo sale de la
/// hoja que tenía delante cuando lo escribió; un dato que el lector SÍ trajo sigue siendo de la
/// hoja donde lo leyó, aunque lo corrija mirando otra. La segunda mitad existe por los
/// formularios de grupo de seis hojas: sin ella, arreglar una letra del nombre de alguien de la
/// hoja 4 con la 1 delante lo mandaría a la 1. La hoja se apunta <b>al teclear</b>, no al
/// guardar: volver a otra hoja antes de pulsar Guardar no cambia de dónde salió lo escrito.</para>
///
/// <para>La pantalla pone <see cref="HojaDelante"/> cada vez que el visor enseña una hoja; sin
/// pantalla —en una prueba— vale la hoja que abrió el caso, así que lo de siempre no cambia.</para>
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>La hoja que se tenía delante al teclear cada campo, por clave; se olvida al guardar el campo y al cambiar de caso.</summary>
    private readonly Dictionary<string, int> _hojaDeLoTecleado = new(StringComparer.Ordinal);

    /// <summary>La hoja que el visor enseña ahora, base 1; empieza en la que abrió el caso.</summary>
    private int _hojaDelante = 1;

    /// <summary>La hoja que el dueño tiene delante, base 1. La pone la pantalla al pintar cada hoja.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si se le da una hoja menor que 1: esa hoja no existe.</exception>
    public int HojaDelante
    {
        get => _hojaDelante;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _hojaDelante = value;
        }
    }

    /// <summary>Al abrir un caso, la hoja de delante vuelve a ser la suya y se olvida lo apuntado del anterior.</summary>
    private void VolverALaHojaDelCaso()
    {
        _hojaDelante = _caso?.PaginaPdf ?? 1;
        _hojaDeLoTecleado.Clear();
    }

    /// <summary>Apunta que ese campo se tecleó con la hoja de ahora delante.</summary>
    /// <param name="clave">La clave del campo que se acaba de teclear.</param>
    private void ApuntarLaHojaDeLoTecleado(string clave) => _hojaDeLoTecleado[clave] = _hojaDelante;

    /// <summary>Ese campo ya se guardó: la hoja con la que se tecleó deja de hacer falta.</summary>
    /// <param name="clave">La clave del campo guardado.</param>
    private void OlvidarLaHojaDeLoTecleado(string clave) => _hojaDeLoTecleado.Remove(clave);

    /// <summary>
    /// De qué hoja sale lo NUEVO que se está guardando de un registro: la que estaba delante al
    /// teclear el último campo que el lector no trajo; nulo si todo lo que se guarda corrige un
    /// dato que ya estaba.
    /// </summary>
    /// <remarks>
    /// Si dos campos nuevos del mismo registro se teclearon con hojas distintas delante —raro:
    /// el nombre mirando la 2 y la cédula mirando la 3— gana el último en el orden de lectura.
    /// Un registro tiene UNA hoja, y elegir la última es lo que menos sorprende a quien acaba de
    /// escribir.
    /// </remarks>
    /// <param name="delRegistro">Los campos de ese registro que se van a escribir.</param>
    private int? HojaDeLoNuevo(IEnumerable<CampoEnPantalla> delRegistro)
    {
        int? hoja = null;
        foreach (var campo in delRegistro)
        {
            if (ReglasDeCampo.Limpiar(campo.ValorGuardado) is not null) continue;
            if (_hojaDeLoTecleado.TryGetValue(campo.Clave, out var suya)) hoja = suya;
        }

        return hoja;
    }

    /// <summary>El caso con la hoja de lo nuevo que se le escribe, o tal cual si no se le escribe nada nuevo.</summary>
    /// <param name="caso">El caso a punto de guardarse.</param>
    /// <param name="delCaso">Los campos del caso que se van a escribir.</param>
    private Caso ConLaHojaDeLoNuevo(Caso caso, IEnumerable<CampoEnPantalla> delCaso)
        => HojaDeLoNuevo(delCaso) is int hoja && hoja != caso.PaginaPdf ? caso with { PaginaPdf = hoja } : caso;

    /// <summary>La persona con la hoja de lo nuevo que se le escribe, o tal cual si no se le escribe nada nuevo.</summary>
    /// <param name="persona">La persona a punto de guardarse.</param>
    /// <param name="suyos">Los campos de esa persona que se van a escribir.</param>
    private Persona ConLaHojaDeLoNuevo(Persona persona, IEnumerable<CampoEnPantalla> suyos)
        => HojaDeLoNuevo(suyos) is int hoja && hoja != persona.PaginaPdf ? persona with { PaginaPdf = hoja } : persona;
}
