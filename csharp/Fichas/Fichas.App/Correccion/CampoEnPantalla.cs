using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Un campo tal como aparece en la pantalla de correccion: que es, que dice y de donde salio.
/// </summary>
/// <remarks>
/// Es el equivalente sin ventana de <c>interfaz/campo.py</c>: lleva la banda —donde estaba
/// este campo en la hoja— para que el visor pueda iluminarla al recibir el foco, y la
/// procedencia entera para poder decir con palabras de donde vino el valor.
/// <para>
/// ⚠️ <b>Tres propiedades tienen <c>set</c> y es a proposito.</b> Al guardar, lo que se
/// escribio deja de ser «un cambio sin guardar» y su procedencia pasa a manual; y la banda
/// puede llegar despues de pintar la pantalla, cuando se lee el documento. Rehacer la lista
/// entera en esos tres momentos dejaria a la pantalla con fichas viejas en la mano.
/// </para>
/// <para>
/// ⚠️ <b>Y las cuatro primeras NO llevan <c>required</c> aunque lo pidan.</b> Medido en este
/// pase: con <c>required</c>, el generador de tipos de XAML escribe un
/// <c>new CampoEnPantalla()</c> para la plantilla del repetidor y la compilacion se cae con
/// cuatro CS9035. El unico sitio que construye estas fichas es
/// <see cref="ModeloDeCorreccion"/>, y las pone todas.
/// </para>
/// </remarks>
public sealed class CampoEnPantalla
{
    /// <summary>A que tabla pertenece: el caso o una persona.</summary>
    public TablaDeProcedencia Tabla { get; init; }

    /// <summary>Id de la fila; el del caso o el de la persona.</summary>
    public long RegistroId { get; init; }

    /// <summary>Nombre de la columna, tal como se llama en el modelo de contratos.</summary>
    public string Campo { get; init; } = string.Empty;

    /// <summary>Como se llama en pantalla, en espanol y corto.</summary>
    public string Etiqueta { get; init; } = string.Empty;

    /// <summary>Como se llama en el papel, para poder buscarlo con la vista en el escaneo.</summary>
    public string EtiquetaDelPapel { get; init; } = string.Empty;

    /// <summary>Lo que hay guardado ahora en el almacen; cambia al guardar.</summary>
    public string? ValorGuardado { get; set; }

    /// <summary>De donde salio el valor; cambia a manual cuando Miguel lo teclea y se guarda.</summary>
    public ProcedenciaDeCampo? Procedencia { get; set; }

    /// <summary>De que hoja del PDF salio, base 1; es la hoja que ensena el visor.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>Donde estaba en la hoja, en fracciones; nula mientras no se sepa.</summary>
    public BandaDeLaPagina? Banda { get; set; }

    /// <summary>Si no se puede editar. Solo el numero de caso, y por un motivo concreto.</summary>
    public bool SoloLectura { get; init; }

    /// <summary>En que fila del formulario venia la persona; nulo si el campo es del caso.</summary>
    public int? FilaFormulario { get; init; }

    /// <summary>De quien es este campo, para poder agrupar; vacio si es del caso.</summary>
    public string NombreDeLaPersona { get; init; } = string.Empty;

    /// <summary>La clave con la que la pantalla dice «este campo».</summary>
    public string Clave => ClaveDe(Tabla, RegistroId, Campo);

    /// <summary>
    /// Como se llama este campo para quien NO ve la pantalla.
    /// </summary>
    /// <remarks>
    /// Sale de un defecto que QA midio sobre el paquete publicado: los doce cuadros de texto
    /// llegaban con el nombre vacio, asi que un lector de pantalla decia «cuadro de edicion»
    /// doce veces. Con cuatro personas en un formulario, cuatro cuadros llamados «Cedula»
    /// tampoco distinguen: por eso el nombre de la persona va dentro, y cuando el escaneo no
    /// dejo leer el nombre —que es justo cuando hay que teclearlo— va la fila del formulario.
    /// </remarks>
    public string NombreParaElLector
    {
        get
        {
            if (Tabla != TablaDeProcedencia.Personas) return Etiqueta;

            // La fila va SIEMPRE que se sepa, incluso con el nombre delante: dos personas
            // del mismo formulario pueden llamarse igual, y dos controles que se llaman
            // igual son el defecto que se esta arreglando, no una version mas corta de el.
            var deQuien = string.IsNullOrWhiteSpace(NombreDeLaPersona)
                ? "una persona sin nombre leído"
                : NombreDeLaPersona;
            return FilaFormulario is int fila
                ? $"{Etiqueta} de {deQuien}, fila {fila}"
                : $"{Etiqueta} de {deQuien}";
        }
    }

    /// <summary>Como se llama el boton de firma de este campo, para quien no ve la pantalla.</summary>
    /// <remarks>
    /// Los once botones se llamaban todos «Esta bien». Firmar es un acto de Miguel (regla
    /// permanente 5); un boton que no dice sobre QUE firma convierte ese acto en una loteria.
    /// </remarks>
    public string NombreDelBotonDeFirma => $"Dar por bueno: {NombreParaElLector}";

    /// <summary>Como se llama la casilla de «no está en el papel», para quien no ve la pantalla.</summary>
    /// <remarks>
    /// Por lo mismo que el boton de firma: en el repetidor las doce casillas comparten
    /// plantilla, asi que sin esto un lector de pantalla diria doce veces «No está en el
    /// papel» sin decir de que campo.
    /// </remarks>
    public string NombreDeLaCasillaDeAusente => $"No está en el papel: {NombreParaElLector}";

    /// <summary>La etiqueta con la persona delante, para nombrarlo en el pie sin ambiguedad.</summary>
    /// <remarks>
    /// Con cuatro personas, «Cedula» a secas no dice cual se quedo sin guardar, que es
    /// justo lo que el acuse existe para decir.
    /// </remarks>
    public string EtiquetaCompleta => FilaFormulario is int fila
        ? $"{Etiqueta} ({fila})"
        : Etiqueta;

    /// <summary>Compone la clave de un campo; una sola forma para que nadie invente otra.</summary>
    /// <remarks>
    /// Es la misma terna que la clave de <c>procedencia_campo</c> —(tabla, registro_id, campo)—,
    /// asi que un campo tiene la misma clave en la pantalla y en la base.
    /// </remarks>
    /// <param name="tabla">Si es del caso o de una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campo">El nombre de la columna.</param>
    public static string ClaveDe(TablaDeProcedencia tabla, long registroId, string campo)
        => $"{tabla}:{registroId}:{campo}";

    /// <summary>La banda que trae una procedencia, o nula si no tiene las cuatro esquinas.</summary>
    /// <remarks>
    /// Las cuatro van juntas o ninguna: media banda no se puede iluminar, y una banda de
    /// area cero se trata como si no la hubiera (<c>interfaz/encuadre.py</c>).
    /// </remarks>
    /// <param name="procedencia">La fila de procedencia del campo; nula devuelve nulo sin lanzar.</param>
    public static BandaDeLaPagina? BandaDe(ProcedenciaDeCampo? procedencia)
    {
        if (procedencia is null) return null;
        if (procedencia.BandaX0 is not double x0 || procedencia.BandaY0 is not double y0) return null;
        if (procedencia.BandaX1 is not double x1 || procedencia.BandaY1 is not double y1) return null;
        return x1 > x0 && y1 > y0 ? new BandaDeLaPagina(x0, y0, x1, y1) : null;
    }
}
