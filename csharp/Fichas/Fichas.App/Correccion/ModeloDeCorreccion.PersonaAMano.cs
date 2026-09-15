using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.App.Correccion;

/// <summary>
/// Lo que devuelve anadir a mano una persona a un documento.
/// </summary>
/// <param name="SeEscribio">Si la persona llego a entrar en la base.</param>
/// <param name="PersonaId">El numero interno de la persona nueva; 0 si no entro.</param>
/// <param name="Avisos">Lo que hay que ensenar en la franja; puede estar vacia.</param>
/// <param name="LineaDelAcuse">La linea del pie, ya compuesta.</param>
public sealed record ResultadoDeAnadirPersona(
    bool SeEscribio,
    long PersonaId,
    IReadOnlyList<Aviso> Avisos,
    string LineaDelAcuse);

/// <summary>
/// Escribir a mano quien va en un documento del que el lector no saco a nadie.
/// </summary>
/// <remarks>
/// <para><b>La queja del dueno, 2026-09-07:</b> <i>«¿Cómo voy a confirmar la recomendación si
/// no me da la opción?»</i>, sobre un renglon que dice «sin ninguna persona leída · sin cédula
/// leída». Un documento asi no puede avanzar nunca: sin persona no hay seis preguntas del
/// sistema del obispo que contestar, y el veredicto «listo para asignar» exige que haya
/// alguien a quien recomendar desde el 2026-09-06.</para>
///
/// <para>⛔ <b>Esto ESCRIBE lo que una mano tecleo y no adivina nada</b>, que es la regla
/// permanente 1 de <c>CLAUDE.md</c>. El nombre del archivo se ENSENA como referencia y no se
/// copia a ninguna casilla; el motivo entero, con los dos escaneos reales que lo delatan,
/// esta en <see cref="TextoDeLaPersonaAMano"/>.</para>
///
/// <para>⛔ <b>Y NO firma nada</b> (regla permanente 5): la procedencia se escribe por
/// <see cref="Fichas.Contratos.Puertos.IProcedencia.Anotar"/>, que jamas pone
/// <c>verificado</c>. Lo vigila <c>PruebasDeLaPersonaAMano</c> contando filas del almacen y
/// no mirando la pantalla.</para>
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>Lo que el programa propone para el nombre: NADA, y por eso es constante.</summary>
    /// <remarks>
    /// Existe como propiedad y no como un vacio implicito para que una prueba pueda fijarlo:
    /// el dia que alguien rellene esta casilla desde el nombre del archivo, esa prueba se
    /// pone roja el mismo dia.
    /// </remarks>
    public string NombrePropuestoParaLaPersonaNueva => string.Empty;

    /// <summary>Lo que el programa propone para la cedula: NADA, por lo mismo y con mas motivo.</summary>
    public string CedulaPropuestaParaLaPersonaNueva => string.Empty;

    /// <summary>Como se llama el archivo de este documento, para que el lo lea y decida.</summary>
    public string DeDondeSacarElNombre => TextoDeLaPersonaAMano.DeDondeSacarElNombre(_caso?.RutaPdf);

    /// <summary>
    /// El titulo del cuadro de anadir: el del atasco si no hay nadie dentro, y el de «una mas» si
    /// ya hay gente. Lo decide el modelo para que la pantalla no tenga que decidirlo.
    /// </summary>
    /// <remarks>
    /// Desde el 2026-09-14 el cuadro se ofrece SIEMPRE, y no solo cuando no hay ninguna persona:
    /// es lo que pidio el dueno el 2026-09-10 —<i>«agregar personas que quizas el escaner no
    /// contemplo»</i>—, que es un documento con gente al que le falta una.
    /// </remarks>
    public string TituloDelCuadroDeLaPersonaAMano => SinNingunaPersonaLeida
        ? TextoDeLaPersonaAMano.Titulo
        : TextoDeLaPersonaAMano.TituloConGenteDentro;

    /// <summary>
    /// Anade a este documento una persona con el nombre y la cedula que se tecleen.
    /// </summary>
    /// <remarks>
    /// <para>El nombre es lo unico imprescindible: es lo que identifica a la persona cuya
    /// recomendacion hay que confirmar. La <b>cedula puede quedarse en blanco</b>, y entonces
    /// el documento sigue diciendo que le falta, que es la verdad: la persona ya existe —se le
    /// pueden contestar las seis preguntas— pero al papel le sigue faltando un dato.</para>
    ///
    /// <para>Una cedula con la forma equivocada <b>entra igual</b> y queda senalada, que es el
    /// requisito 9 del dueno —«avisar, nunca impedir»— y lo mismo que dice la migracion 17 al
    /// quitarle el <c>CHECK</c> a <c>personas.mrn</c>: lo que un <c>CHECK</c> tira ahi no es un
    /// dato, es la fila de una persona.</para>
    ///
    /// <para>Por que anadir UNA persona basta para desatascar el documento: la unidad de trabajo
    /// es la persona, no el documento —dueno, 2026-09-05, <c>DECISIONES.md</c> «El ticket es por
    /// persona»—. Cada persona es un ticket con sus seis preguntas; sin ninguna, no hay ticket
    /// que abrir.</para>
    /// </remarks>
    /// <param name="nombre">El nombre que se tecleo; sin el no se escribe nada.</param>
    /// <param name="cedula">La cedula que se tecleo, o nada.</param>
    /// <returns>Si entro, con su numero interno; si no, el motivo en una linea. Nunca lanza.</returns>
    public ResultadoDeAnadirPersona AnadirUnaPersonaAMano(string? nombre, string? cedula)
    {
        if (_caso is null) return NoSePudo(TextoDeLaPersonaAMano.SinDocumentoAbierto);

        if (ReglasDeCampo.Limpiar(nombre) is not string comoSeLlama)
        {
            return NoSePudo(
                TextoDeLaPersonaAMano.SinNombre,
                TextoDeLaPersonaAMano.PorQueHaceFaltaElNombre,
                Extraccion.CampoNombreDePersona);
        }

        var suCedula = ReglasDeCampo.Limpiar(cedula);
        var escritura = _personas.Guardar(new Persona
        {
            CasoId = _caso.Id,
            Nombre = comoSeLlama,
            Mrn = suCedula,
            FilaFormulario = LaFilaQueSigue(),
            // La hoja que el dueno tiene delante, no la que abrio el caso: hasta el 2026-09-15
            // heredaba la del caso, y con la factura en la 1 y el formulario en la 2, la persona
            // tecleada mirando la 2 nacia en la 1 y enfocar su nombre devolvia el visor a la
            // factura (ModeloDeCorreccion.Hoja.cs). Sin pantalla las dos son la misma.
            PaginaPdf = HojaDelante,
        });

        if (!escritura.SeEscribio)
        {
            return new ResultadoDeAnadirPersona(
                false, 0, escritura.Avisos, "No se pudo añadir la persona; mire el aviso de al lado.");
        }

        AnotarQueLoEscribioUnaMano(escritura.Id, Extraccion.CampoNombreDePersona, comoSeLlama);
        AnotarQueLoEscribioUnaMano(escritura.Id, Extraccion.CampoCedula, suCedula);
        VolverALeerLasPersonas();

        var avisos = new List<Aviso>(escritura.Avisos);
        if (suCedula is not null && ReglasDeCampo.MotivoDelMrn(suCedula) is string motivo)
        {
            avisos.Add(Aviso.Advierte(
                $"la cédula de «{comoSeLlama}» se guardó, y hay que mirarla",
                Extraccion.CampoCedula,
                motivo + " El dato entra igual y queda señalado: avisar, nunca impedir."));
        }

        return new ResultadoDeAnadirPersona(
            true, escritura.Id, avisos, TextoDeLaPersonaAMano.AlAnadirla(comoSeLlama, suCedula is not null));
    }

    /// <summary>En que fila del formulario va la persona nueva: la siguiente de las que hay.</summary>
    /// <remarks>
    /// Se toma la mayor y se suma una, en vez de contar cuantas hay: si una fila se borrara,
    /// contar daria un numero ya usado y <c>UNIQUE (caso_id, mrn)</c> no lo impediria —la
    /// fila no es la cedula—, asi que quedarian dos personas en la misma fila del papel.
    /// </remarks>
    private int LaFilaQueSigue()
    {
        var mayor = 0;
        foreach (var persona in _personasDelCaso) mayor = Math.Max(mayor, persona.FilaFormulario ?? 0);
        return mayor + 1;
    }

    /// <summary>
    /// Deja escrito que ese campo lo tecleo una mano. NUNCA pone verificado.
    /// </summary>
    /// <remarks>
    /// El origen es lo que separa este dato de lo que salio del papel, y por eso se escribe
    /// aunque el campo quede vacio: un campo sin fila de procedencia no se distingue de uno
    /// que se leyo bien, y ademas no se puede firmar despues —<c>Firmar</c> es un
    /// <c>UPDATE</c> y cambiaria cero filas—.
    /// <para>
    /// Una casilla vacia se anota con origen «vacio» y no «manual»: nadie tecleo nada ahi, y
    /// decir que si seria inventarse un origen.
    /// </para>
    /// </remarks>
    /// <param name="personaId">La persona recien escrita.</param>
    /// <param name="campo">La columna: el nombre o la cedula.</param>
    /// <param name="valor">Lo que se tecleo, ya limpio; nulo anota origen «vacio».</param>
    private void AnotarQueLoEscribioUnaMano(long personaId, string campo, string? valor)
        => _procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Personas,
            RegistroId = personaId,
            Campo = campo,
            Origen = valor is null ? OrigenDeCampo.Vacio : OrigenDeCampo.Manual,
        });

    /// <summary>
    /// Vuelve a leer las personas y rehace los campos, SIN perder lo que haya tecleado encima.
    /// </summary>
    /// <remarks>
    /// No se llama a <see cref="Cargar"/> a proposito: aquello vacia <c>_tecleado</c>, y
    /// anadir a una persona no puede tirar lo que se estaba escribiendo en el campo de al
    /// lado. Las claves de los campos que ya existian no cambian —son (tabla, registro,
    /// columna)—, asi que lo tecleado sigue encontrando su campo.
    /// </remarks>
    private void VolverALeerLasPersonas()
    {
        if (_caso is null) return;

        _personasDelCaso = _personas.DeCaso(_caso.Id);
        _campos.Clear();
        ArmarLosCampos(_caso, _personasDelCaso);
        ArmarLasRespuestas(_caso.Id, _personasDelCaso);
    }

    /// <summary>No se escribio nada, y se dice por que en una linea.</summary>
    /// <param name="linea">El motivo, que va a la vez en el aviso y en el pie.</param>
    /// <param name="detalle">Lo que hay que hacer, para el aviso; nulo si la linea basta.</param>
    /// <param name="campo">La columna a la que apunta el aviso; vacio si no es de ningun campo.</param>
    private static ResultadoDeAnadirPersona NoSePudo(string linea, string? detalle = null, string campo = "")
        => new(false, 0, [Aviso.Advierte(linea, campo, detalle)], linea);
}
