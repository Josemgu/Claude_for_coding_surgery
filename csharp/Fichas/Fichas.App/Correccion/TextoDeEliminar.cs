using System.Globalization;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Las palabras de «Eliminar» en Corrección —el botón, la pregunta y el acuse—, en un solo
/// sitio y fuera de todo XAML, para que se puedan leer en una prueba.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe el botón.</b> Palabras del dueño el 2026-09-14: <i>«Agrega un
/// botón en la Corrección de eliminar información también. No lo tengo y es importante
/// tenerlo»</i>. Y antes: <i>«Si quiero eliminar un nombre puedo hacerlo»</i> (2026-09-07). Medido
/// antes de tocar nada: en toda la carpeta de Corrección no había ni un botón de borrar, y
/// borrar una persona no existía en ningún puerto.</para>
///
/// <para>⛔ <b>La pregunta lleva el nombre delante, nunca un «¿seguro?».</b> Es la misma regla
/// que <c>PlanDeBorrado.Titulo</c>: un «¿seguro?» se contesta que sí sin leerlo; «Eliminar a
/// Ana» no. Y dice que no hay vuelta atrás, porque desde el programa no la hay.</para>
/// </remarks>
public static class TextoDeEliminar
{
    /// <summary>Lo que dice el botón del pie que abre las dos opciones.</summary>
    public const string Boton = "Eliminar";

    /// <summary>Cómo se llama ese botón para quien no ve la pantalla.</summary>
    public const string BotonParaElLector =
        "Eliminar una persona de este documento o el documento entero; pregunta antes";

    /// <summary>La opción del menú que borra el documento entero.</summary>
    public const string OpcionDelDocumentoEntero = "Eliminar el documento entero";

    /// <summary>La frase que cierra toda pregunta de eliminar.</summary>
    public const string NoSePuedeDeshacer = "Desde el programa esto no se puede deshacer.";

    /// <summary>Lo que se avisa cuando la persona que se va a eliminar es la última del documento.</summary>
    public const string SeQuedaraSinNadie =
        "Es la última persona de este documento: al eliminarla, el documento se quedará sin nadie "
        + "y se le preguntará si quiere eliminar el documento entero.";

    /// <summary>Lo que dice el acuse cuando el documento se quedó sin ninguna persona.</summary>
    public const string SinNingunaPersonaDentro = "el documento se quedó sin ninguna persona";

    /// <summary>Lo que va en el botón del cuadro que NO elimina.</summary>
    public const string BotonQueNoElimina = "No eliminar nada";

    /// <summary>Lo que se dice cuando no hay ningún documento abierto.</summary>
    public const string SinDocumentoAbierto = "no hay ningún documento abierto: no hay nada que eliminar";

    /// <summary>Lo que dicen el pie y el visor cuando se borró el último documento y no queda otro que abrir.</summary>
    public const string NoQuedaNingunoAbierto = "no queda ningún documento abierto";

    /// <summary>Lo que se dice cuando la persona pedida no es de este documento.</summary>
    public const string NoEsDeEsteDocumento =
        "esa persona no está en este documento: no se eliminó nada";

    /// <summary>La opción del menú para una persona: «Eliminar a Ana».</summary>
    /// <param name="persona">La persona, de la que se toma cómo se llama.</param>
    public static string OpcionDeLaPersona(Persona persona) => "Eliminar a " + ComoSeLlama(persona);

    /// <summary>
    /// Cómo se nombra a una persona en la pregunta: por su nombre; sin nombre, por su cédula;
    /// sin ninguna de las dos, por su fila del papel.
    /// </summary>
    /// <remarks>
    /// Una fila sin nombre y sin cédula no entra en la base (el <c>CHECK</c> de la versión 1),
    /// así que la tercera forma es para una persona que perdió las dos a mano; se dice igual, por
    /// su fila, antes que dejar una opción del menú en blanco.
    /// </remarks>
    /// <param name="persona">La persona.</param>
    public static string ComoSeLlama(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        if (ReglasDeCampo.Limpiar(persona.Nombre) is string nombre) return nombre;
        if (ReglasDeCampo.Limpiar(persona.Mrn) is string cedula) return "la persona con cédula " + cedula;
        return "la persona de la fila " + (persona.FilaFormulario ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>La línea del pie al eliminar una persona: a quién y cuántas quedan.</summary>
    /// <param name="comoSeLlama">Cómo se llama la eliminada, tal como la dio <see cref="ComoSeLlama"/>.</param>
    /// <param name="quedan">Cuántas personas quedan en el documento.</param>
    public static string AlEliminarUnaPersona(string comoSeLlama, int quedan)
        => quedan switch
        {
            0 => $"Eliminada {comoSeLlama}: {SinNingunaPersonaDentro}.",
            1 => $"Eliminada {comoSeLlama} (1 persona queda en el documento).",
            _ => $"Eliminada {comoSeLlama} ({quedan.ToString(CultureInfo.InvariantCulture)} personas quedan en el documento).",
        };

    /// <summary>La línea del pie al eliminar el documento entero: cuál y dónde quedó la copia.</summary>
    /// <param name="numeroCaso">El número del documento; en blanco se dice «el documento».</param>
    /// <param name="rutaDeLaCopia">Dónde quedó la copia previa, o nulo si no la hubo.</param>
    public static string AlEliminarElDocumento(string? numeroCaso, string? rutaDeLaCopia)
    {
        var cual = ReglasDeCampo.Limpiar(numeroCaso) is string numero ? $"Documento {numero}" : "El documento";
        return rutaDeLaCopia is null
            ? $"{cual} eliminado; sin copia previa."
            : $"{cual} eliminado; copia en «{rutaDeLaCopia}».";
    }

    /// <summary>La banda que se deja antes de preguntar por el documento, cuando se eliminó a la última persona.</summary>
    /// <param name="numeroCaso">El número del documento; en blanco se dice «este documento».</param>
    public static string EraLaUltimaDe(string? numeroCaso)
        => ReglasDeCampo.Limpiar(numeroCaso) is string numero
            ? $"Era la última persona de {numero}: el documento se quedó sin nadie."
            : "Era la última persona: el documento se quedó sin nadie.";
}

/// <summary>
/// La pregunta de eliminar una persona, ya armada: todavía no se borró nada.
/// </summary>
/// <remarks>
/// Lo que hay aquí es lo que hace falta para PREGUNTAR bien: el nombre delante, cuántas quedan
/// y si es la última. No hay ningún método que borre: borrar es
/// <see cref="ModeloDeCorreccion.EliminarPersona"/>, y así una pregunta no puede ejecutarse sola.
/// </remarks>
/// <param name="PersonaId">A quién.</param>
/// <param name="ComoSeLlama">Cómo se la nombra, tal como la dio <see cref="TextoDeEliminar.ComoSeLlama"/>.</param>
/// <param name="QuedarianPersonas">Cuántas quedarían en el documento después.</param>
public sealed record PreguntaDeEliminarPersona(long PersonaId, string ComoSeLlama, int QuedarianPersonas)
{
    /// <summary>Si es la última del documento.</summary>
    public bool EsLaUltima => QuedarianPersonas == 0;

    /// <summary>El título del cuadro: la acción CON el nombre delante.</summary>
    public string Titulo => "Eliminar a " + ComoSeLlama + " de este documento";

    /// <summary>Lo que va en el botón que elimina; también con el nombre delante.</summary>
    public string TextoDelBoton => "Eliminar a " + ComoSeLlama;

    /// <summary>El cuerpo de la pregunta: qué cae, qué queda, y que no hay vuelta atrás.</summary>
    public string Pregunta
    {
        get
        {
            var lineas = new List<string>
            {
                $"Se va a borrar de la base a {ComoSeLlama}, con su nombre, su cédula, lo que tuviera "
                + "contestado de las seis preguntas y de dónde salió cada dato.",
                string.Empty,
            };

            lineas.Add(EsLaUltima
                ? TextoDeEliminar.SeQuedaraSinNadie
                : QuedarianPersonas == 1
                    ? "El documento se queda con 1 persona."
                    : $"El documento se queda con {QuedarianPersonas.ToString(CultureInfo.InvariantCulture)} personas.");

            lineas.Add(string.Empty);
            lineas.Add("Antes de borrar se hará una copia de la base al lado de ella; si no se puede, no se borra nada.");
            lineas.Add(string.Empty);
            lineas.Add(TextoDeEliminar.NoSePuedeDeshacer);

            return string.Join(Environment.NewLine, lineas);
        }
    }
}

/// <summary>Lo que devuelve eliminar una persona del documento abierto.</summary>
/// <param name="SeBorro">Si la persona salió de la base.</param>
/// <param name="EraLaUltima">Si el documento se quedó sin ninguna persona.</param>
/// <param name="QuedanPersonas">Cuántas quedan en el documento; 0 si era la última.</param>
/// <param name="RutaDeLaCopia">Dónde quedó la copia previa de la base, o nulo si no la hubo (con datos inventados no la hay).</param>
/// <param name="Avisos">Lo que hay que enseñar en la franja; puede estar vacía.</param>
/// <param name="LineaDelAcuse">La línea del pie, ya compuesta.</param>
public sealed record ResultadoDeEliminarPersona(
    bool SeBorro,
    bool EraLaUltima,
    int QuedanPersonas,
    string? RutaDeLaCopia,
    IReadOnlyList<Aviso> Avisos,
    string LineaDelAcuse);
