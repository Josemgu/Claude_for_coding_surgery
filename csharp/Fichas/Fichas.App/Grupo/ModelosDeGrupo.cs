using Fichas.App.Inicio;
using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Grupo;

/// <summary>
/// Como se dicen en la pantalla el estado y el motivo, en un solo sitio.
/// </summary>
/// <remarks>
/// El vocabulario —<see cref="MotivoDeNoCompletar"/>— vive en <c>Fichas.Contratos</c> desde
/// la migracion 18, junto a las dos columnas que lo guardan
/// (<c>casos.motivo_no_completa</c> y <c>casos.motivo_del_companero</c>, ADR-0005 §3.3).
/// Aqui solo estan las PALABRAS que se leen en pantalla, que son cosa de la pantalla: la
/// base guarda claves como <c>no_se_pudo_comunicar</c> justamente para que la redaccion se
/// pueda cambiar sin migrar datos.
/// </remarks>
public static class PalabrasDelEstado
{
    /// <summary>Lo que se lee bajo un documento: su estado y, si lo hay, su motivo.</summary>
    /// <remarks>
    /// Las tres frases son las del dueno, literales: «no completado», «no se pudo
    /// comunicar con el líder», «el líder no lo hizo» (criterio C13-4).
    /// </remarks>
    public static string Decir(EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo)
    {
        if (estado == EstadoDeRecomendacion.Completa) return "completa";
        if (estado == EstadoDeRecomendacion.SinMarcar) return "sin marcar";

        return motivo switch
        {
            MotivoDeNoCompletar.NoSePudoComunicar => "no se pudo comunicar con el líder",
            MotivoDeNoCompletar.ElLiderNoLoHizo => "el líder no lo hizo",
            MotivoDeNoCompletar.OtraRazon => "otra razón",
            _ => "no completado",
        };
    }

    /// <summary>El motivo suelto, para el resumen de un grupo entero.</summary>
    public static string DecirElMotivo(MotivoDeNoCompletar motivo) => motivo switch
    {
        MotivoDeNoCompletar.NoSePudoComunicar => "no se pudo comunicar con el líder",
        MotivoDeNoCompletar.ElLiderNoLoHizo => "el líder no lo hizo",
        MotivoDeNoCompletar.OtraRazon => "otra razón",
        _ => "sin motivo dicho",
    };
}

/// <summary>
/// Una persona del grupo, con el documento del que sale.
/// </summary>
/// <remarks>
/// El renglon del grupo es la PERSONA y no el documento, porque eso fue lo que pidio el
/// dueno: <i>«ver solamente al grupo de personas que viajara en esa fecha, con su PDF»</i>.
/// Un documento con cinco personas pinta cinco renglones que comparten caso, estado y PDF.
/// </remarks>
/// <param name="PersonaId">Id de la persona.</param>
/// <param name="CasoId">Id del documento del que sale; es lo que se asigna y lo que se verifica.</param>
/// <param name="Nombre">Su nombre, o lo que se sepa decir de ella.</param>
/// <param name="Cedula">Su cedula de miembro, o vacio si no se leyo.</param>
/// <param name="NumeroCaso">Las cuatro letras y cuatro digitos del documento.</param>
/// <param name="RutaPdf">Donde esta el PDF; nula si el documento no la trae.</param>
/// <param name="HayPdf">Si ese archivo existe ahora mismo en el disco.</param>
/// <param name="EstadoDelDocumento">Lo que dice la recomendacion DEL DOCUMENTO; no es el estado de ella.</param>
/// <param name="Motivo">Por que no esta completa, cuando se sepa.</param>
/// <param name="CuantoLeFalta">Cuantos datos le faltan al documento para poder asignarse.</param>
/// <param name="Dueno">Quien lleva el documento, o vacio si no lo lleva nadie.</param>
/// <param name="Recomendacion">
/// Si SU recomendacion esta confirmada en el sistema del obispo: si, no, o nada si nadie la
/// miro. Sale de sus seis preguntas y de nada mas.
/// </param>
/// <param name="SeQuedoEn">Los pasos que estan marcados que NO; vacio si no hay ninguno.</param>
public sealed record PersonaDelGrupo(
    long PersonaId,
    long CasoId,
    string Nombre,
    string Cedula,
    string NumeroCaso,
    string? RutaPdf,
    bool HayPdf,
    EstadoDeRecomendacion EstadoDelDocumento,
    MotivoDeNoCompletar Motivo,
    int CuantoLeFalta,
    string Dueno,
    bool? Recomendacion = null,
    IReadOnlyList<string>? SeQuedoEn = null)
{
    /// <summary>Los pasos donde se quedo, nunca nulo.</summary>
    public IReadOnlyList<string> PasosSinCompletar => SeQuedoEn ?? [];

    /// <summary>
    /// El estado DEL DOCUMENTO y su motivo, con las palabras del dueno (C13-4).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>No es el estado de esta persona</b>, y hasta el 2026-09-05 lo parecia: cinco
    /// personas del mismo documento salian con el mismo estado aunque tres estuvieran
    /// resueltas y dos no. El de la persona es <see cref="RecomendacionTexto"/>.
    /// <para>
    /// Se conserva porque es una respuesta de verdad y de otro sitio: lo escribe el Excel
    /// que devuelve el companero, con su nombre (regla permanente 5, precisada por el dueno
    /// el 2026-09-03). Taparlo con el de la persona seria perderlo.
    /// </para>
    /// </remarks>
    public string EstadoDelDocumentoTexto => PalabrasDelEstado.Decir(EstadoDelDocumento, Motivo);

    /// <summary>
    /// Como esta ESTA persona: «lista para viajar», «no lista para viajar» o «sin mirar», con
    /// su recomendacion y con el paso donde se quedo.
    /// </summary>
    /// <remarks>
    /// Es la pregunta que el dueno contesta de verdad: <i>«verificar que los hermanos que
    /// están en el PDF tengan la recomendación hecha en el sistema»</i>. Criterios C17-3,
    /// C18-2 y C18-3.
    /// </remarks>
    public string RecomendacionTexto => LasDosPreguntas.FraseDeUnaPersona(Recomendacion, PasosSinCompletar);

    /// <summary>«055-1111-3853» o «sin cédula leída»; nunca un hueco mudo.</summary>
    public string CedulaTexto => string.IsNullOrWhiteSpace(Cedula) ? "sin cédula leída" : Cedula;

    /// <summary>Lo que se dice del PDF: si no esta, se dice y el renglon sigue ahi (C13-6).</summary>
    public string PdfTexto => RutaPdf is null
        ? "sin PDF anotado"
        : HayPdf ? "con su PDF" : "el PDF no está en su ruta";

    /// <summary>Si hay que llamar la atencion sobre el PDF, que es cuando no se puede abrir.</summary>
    public bool ElPdfNoSePuedeAbrir => !HayPdf;

    /// <summary>Si de este documento no se leyo a nadie; el renglon lo dice con su frase.</summary>
    /// <remarks>
    /// Se deduce de <see cref="PersonaId"/> en cero, que es como <c>LectorDeGrupos</c> arma el
    /// renglon de un documento sin personas. No es una marca aparte: son lo mismo, y con dos
    /// podrian contradecirse.
    /// </remarks>
    public bool SinNingunaPersonaLeida => PersonaId == 0;

    /// <summary>«le faltan 3 datos», «le falta 1 dato» o «listo para asignar · …».</summary>
    /// <remarks>
    /// ⛔ Es la OTRA pregunta —la que contesta el programa mirando nuestros campos— y no se
    /// mezcla con <see cref="RecomendacionTexto"/>. El motivo entero esta en
    /// <see cref="LasDosPreguntas"/>, que es ademas donde se COMPONE la frase desde el
    /// 2026-09-07: aqui se componia aparte, y una frase compuesta en dos sitios acaba
    /// diciendo dos cosas del mismo documento.
    /// </remarks>
    public string LoQueFaltaTexto
        => LasDosPreguntas.LoQueLeFaltaAlDocumento(CuantoLeFalta, SinNingunaPersonaLeida);

    /// <summary>Quien lo lleva, o que no lo lleva nadie; nunca se deja en blanco.</summary>
    public string DuenoTexto => string.IsNullOrWhiteSpace(Dueno) ? "sin asignar" : Dueno;

    /// <summary>
    /// La linea de debajo del nombre: caso, como esta ELLA, quien lo lleva y su PDF.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Hasta el 2026-09-05 aqui iba el estado del DOCUMENTO</b>, y esa es la frase que
    /// el dueno leia como si fuera de la persona. Ahora va el de la persona; el del documento
    /// sigue diciendose, con su nombre, en la cabecera de la unidad.
    /// </remarks>
    public string Detalle => $"{NumeroCaso} · {RecomendacionTexto} · {DuenoTexto} · {PdfTexto}";
}

/// <summary>
/// Las personas de una unidad que viajan el mismo dia.
/// </summary>
/// <remarks>
/// ⛔ <b>Que la unidad parta el dia es una decision del dueno todavia abierta</b>
/// (ADR-0005 §2.5). Se construye partida —una cabecera por unidad dentro del dia— porque
/// es lo que recomendo el planificador: el habla con un lider por unidad, no con «el lider
/// del martes». Si prefiere un solo grupo por dia, es juntar estas cabeceras y nada mas:
/// el dia entero ya viene en <see cref="GrupoDelDia"/>.
/// </remarks>
/// <param name="UnidadNumero">Su numero, o vacio si el papel no lo traia.</param>
/// <param name="UnidadNombre">Su nombre tal como se leyo.</param>
/// <param name="CasoIds">Los documentos de esta unidad ese dia; un archivado no llega hasta aqui.</param>
/// <param name="Personas">Las personas, en el orden en que se ensenan.</param>
/// <param name="CuantasCompletas">Cuantos de esos documentos estan completos.</param>
/// <param name="Motivo">El motivo que mas se repite entre los que no estan completos.</param>
public sealed record UnidadDelGrupo(
    string UnidadNumero,
    string UnidadNombre,
    IReadOnlyList<long> CasoIds,
    IReadOnlyList<PersonaDelGrupo> Personas,
    int CuantasCompletas,
    MotivoDeNoCompletar Motivo)
{
    /// <summary>Cuantos documentos trae esta unidad ese dia.</summary>
    public int CuantosDocumentos => CasoIds.Count;

    /// <summary>
    /// Los documentos de esta unidad que se pueden asignar en lote.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Un documento archivado NO se asigna: esta cerrado.</b> Es la regla del dueno del
    /// 2026-09-05 —«cuando yo archive, debe salir del sistema visible»—, y desde el
    /// 2026-09-06 se cumple mas arriba: <see cref="LectorDeGrupos"/> ya no trae ni un
    /// archivado, asi que aqui son todos.
    /// <para>Medido el 2026-09-05 sobre el paquete publicado, antes de arreglarlo: «Asignar
    /// el grupo entero» sobre el 8 de septiembre dejo <b>8 filas</b> en <c>asignaciones</c>,
    /// y una era la del documento archivado. Esta propiedad se queda —en vez de que la
    /// pantalla use <see cref="CasoIds"/> a pelo— para que ese defecto tenga un solo sitio
    /// donde volver a mirarse.</para>
    /// </remarks>
    public IReadOnlyList<long> CasosQueSePuedenAsignar => CasoIds;

    /// <summary>Si queda algo que asignar en esta unidad.</summary>
    public bool SePuedeAsignar => CasosQueSePuedenAsignar.Count > 0;

    /// <summary>«700001 · Castries Branch», o solo lo que se sepa.</summary>
    public string Titulo
    {
        get
        {
            var numero = string.IsNullOrWhiteSpace(UnidadNumero) ? "sin número de unidad" : UnidadNumero;
            var nombre = string.IsNullOrWhiteSpace(UnidadNombre) ? "sin unidad leída" : UnidadNombre;
            return $"{numero} · {nombre}";
        }
    }

    /// <summary>De sus personas, cuantas tienen la recomendacion confirmada en el sistema del obispo.</summary>
    /// <remarks>
    /// Se cuenta sobre las PERSONAS y no sobre los documentos: es la unidad de trabajo que
    /// el dueno declaro el 2026-09-05 —<i>«Es por persona que se revisa la información»</i>—.
    /// </remarks>
    public int CuantasPersonasConfirmadas => Personas.Count(p => p.Recomendacion == true);

    /// <summary>Las que todavia no: las que estan en «no» y las que nadie miro.</summary>
    public int CuantasPersonasSinConfirmar => Personas.Count - CuantasPersonasConfirmadas;

    /// <summary>«3 documentos · 7 personas · 2 de 7 personas confirmadas · 1 de 3 completas».</summary>
    /// <remarks>
    /// Las dos cuentas van juntas y con su palabra delante para que no se confundan: las
    /// PERSONAS confirmadas son la recomendacion en el sistema del obispo, y los DOCUMENTOS
    /// completos son lo que dijo el Excel del companero. Son dos respuestas de dos sitios.
    /// </remarks>
    public string Detalle
        => Plural.Con(CuantosDocumentos, "documento", "documentos")
           + " · " + Plural.Con(Personas.Count, "persona", "personas")
           + $" · {CuantasPersonasConfirmadas} de {Personas.Count} "
           + Plural.Palabra(Personas.Count, "persona confirmada", "personas confirmadas")
           + $" · {CuantasCompletas} de {CuantosDocumentos} "
           + Plural.Palabra(CuantosDocumentos, "completa", "completas");
}

/// <summary>
/// El grupo que viaja un dia: sus unidades, sus documentos y sus personas.
/// </summary>
/// <param name="Fecha">El dia del viaje.</param>
/// <param name="Unidades">Las unidades que viajan ese dia, de mas documentos a menos.</param>
/// <param name="CuantosDocumentos">Cuantos documentos trae el dia entero.</param>
/// <param name="CuantasPersonas">Cuantas personas trae el dia entero.</param>
/// <param name="CuantasCompletas">Cuantos de esos documentos estan completos.</param>
/// <param name="Motivo">El motivo que mas se repite entre los que no estan completos.</param>
public sealed record GrupoDelDia(
    DateOnly Fecha,
    IReadOnlyList<UnidadDelGrupo> Unidades,
    int CuantosDocumentos,
    int CuantasPersonas,
    int CuantasCompletas,
    MotivoDeNoCompletar Motivo)
{
    /// <summary>Un dia sin ningun documento; se ensena vacio y no revienta.</summary>
    public static GrupoDelDia Vacio(DateOnly fecha)
        => new(fecha, [], 0, 0, 0, MotivoDeNoCompletar.SinMotivo);

    /// <summary>«martes 8 de septiembre de 2026», la cabecera de la pantalla.</summary>
    public string Titulo => FechasEnEspanol.DecirElDiaCompleto(Fecha);

    /// <summary>La fecha en ISO-8601, que es como la guarda la base.</summary>
    public string FechaIso => FechasEnEspanol.Escribir(Fecha);

    /// <summary>Si el dia no trae ni un documento.</summary>
    public bool EstaVacio => CuantosDocumentos == 0;

    /// <summary>Todos los documentos del dia; es el denominador.</summary>
    public IReadOnlyList<long> TodosLosCasos => [.. Unidades.SelectMany(u => u.CasoIds)];

    /// <summary>Todas las personas del dia, en el orden en que se ensenan.</summary>
    public IReadOnlyList<PersonaDelGrupo> TodasLasPersonas => [.. Unidades.SelectMany(u => u.Personas)];

    /// <summary>Cuantas tienen la recomendacion confirmada en el sistema del obispo.</summary>
    /// <remarks>
    /// ⛔ <b>El grupo NO tiene estado propio</b> (ADR-0006 §3.1): dice cuantas personas de
    /// cuantas estan confirmadas. Un grupo no se «completa»: se vacia de personas sin
    /// confirmar.
    /// </remarks>
    public int CuantasPersonasConfirmadas => TodasLasPersonas.Count(p => p.Recomendacion == true);

    /// <summary>Cuantas tienen alguna de las seis preguntas marcada que NO.</summary>
    public int CuantasPersonasNoListas => TodasLasPersonas.Count(p => p.Recomendacion == false);

    /// <summary>
    /// Cuantas tienen alguna pregunta en blanco y ninguna en no: nadie las miro.
    /// </summary>
    /// <remarks>Va aparte de las anteriores porque «sin mirar» no es «no» (C18-2).</remarks>
    public int CuantasPersonasSinMirar => TodasLasPersonas.Count(p => p.Recomendacion is null);

    /// <summary>Las que le quedan por verificar: las que estan en «no» y las que nadie miro.</summary>
    public int CuantasPersonasSinConfirmar => CuantasPersonasNoListas + CuantasPersonasSinMirar;

    /// <summary>
    /// «4 de 10 personas confirmadas · 3 sin mirar», la linea del dia en PERSONAS.
    /// </summary>
    /// <remarks>
    /// Va aparte de <see cref="ComoVa"/>, que sigue contando DOCUMENTOS completos y conserva
    /// sus palabras exactas: son dos respuestas distintas y ponerlas en la misma frase es lo
    /// que hacia que el dueno leyera una creyendo la otra.
    /// </remarks>
    public string ComoVanLasPersonas
    {
        get
        {
            if (EstaVacio) return "no viaja nadie este día";

            var todas = TodasLasPersonas.Count;
            var cuenta = $"{CuantasPersonasConfirmadas} de {todas} "
                         + Plural.Palabra(todas, "persona confirmada", "personas confirmadas");

            return CuantasPersonasSinMirar == 0
                ? cuenta
                : $"{cuenta} · {CuantasPersonasSinMirar} sin mirar";
        }
    }

    /// <summary>
    /// Los que se pueden asignar del dia entero.
    /// </summary>
    /// <remarks>El motivo entero esta en <see cref="UnidadDelGrupo.CasosQueSePuedenAsignar"/>.</remarks>
    public IReadOnlyList<long> LosQueSePuedenAsignar => [.. Unidades.SelectMany(u => u.CasosQueSePuedenAsignar)];

    /// <summary>El denominador que el criterio exige decir: N documentos, N personas, N unidades.</summary>
    /// <remarks>
    /// ⛔ Ya no lleva «N ARCHIVADO» detras: desde el 2026-09-06 un archivado no entra en este
    /// grupo, asi que decir cuantos hay aqui seria hablar de algo que no esta en la lista.
    /// Donde se cuentan es en Reportes.
    /// </remarks>
    public string LineaDelDenominador
        => Plural.Con(CuantosDocumentos, "documento", "documentos")
           + " · " + Plural.Con(CuantasPersonas, "persona", "personas")
           + " · " + Plural.Con(Unidades.Count, "unidad", "unidades");

    /// <summary>«2 de 7 completas», y si falta alguno, por que (regla C11-4).</summary>
    public string ComoVa
    {
        get
        {
            if (EstaVacio) return "no viaja nadie este día";

            var cuenta = $"{CuantasCompletas} de {CuantosDocumentos} "
                         + Plural.Palabra(CuantosDocumentos, "completa", "completas");

            return CuantasCompletas == CuantosDocumentos
                ? cuenta
                : $"{cuenta} · {PalabrasDelEstado.DecirElMotivo(Motivo)}";
        }
    }

    /// <summary>
    /// El grupo puesto en UNA sola lista: una cabecera por unidad y detras sus personas.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es plana a proposito y no es un capricho de estilo.</b> La forma natural
    /// —un repetidor de unidades con otro repetidor de personas dentro— construye TODAS
    /// las personas de golpe: el repetidor de dentro no virtualiza porque su alto no esta
    /// acotado. Con el dia mas cargado de una base de 3 000 —98 documentos y 517
    /// personas— eso son 517 renglones vivos, y el criterio C13-5 pide justo lo contrario:
    /// que el numero de elementos visuales tenga tope. Aplanada, un solo repetidor
    /// virtualiza la lista entera y solo construye los renglones que se ven.
    /// </remarks>
    public IReadOnlyList<RenglonDelGrupo> EnUnaSolaLista()
    {
        var renglones = new List<RenglonDelGrupo>();

        foreach (var unidad in Unidades)
        {
            renglones.Add(RenglonDelGrupo.Cabecera(unidad));
            renglones.AddRange(unidad.Personas.Select(RenglonDelGrupo.DeUnaPersona));
        }

        return renglones;
    }
}

/// <summary>
/// Un renglon de la pantalla del grupo: o la cabecera de una unidad, o una persona.
/// </summary>
/// <param name="EsCabecera">Si este renglon es la cabecera de una unidad.</param>
/// <param name="Titulo">La unidad, o el nombre de la persona.</param>
/// <param name="Detalle">Lo que se lee debajo del titulo.</param>
/// <param name="CasoId">El documento que se abre al pulsar; 0 en una cabecera.</param>
/// <param name="CasosDeLaUnidad">Los documentos que asigna el boton de la cabecera; vacio en una persona.</param>
/// <param name="Cedula">La cedula de la persona, o vacio en una cabecera.</param>
/// <param name="EstaCompleta">Si la recomendacion DEL DOCUMENTO esta completa.</param>
/// <param name="ElPdfNoSePuedeAbrir">Si el PDF no esta donde el documento dice.</param>
/// <param name="Recomendacion">
/// Si la recomendacion de ESTA PERSONA esta confirmada: si, no, o nada si nadie la miro.
/// En una cabecera de unidad va nula: una unidad no tiene seis preguntas.
/// </param>
/// <param name="LoQueLeFaltaAlDocumento">
/// Si al DOCUMENTO de esta persona le falta algun dato, ya en palabras. Vacio en una cabecera
/// de unidad: una unidad no es un documento y no tiene campos que llenar.
/// </param>
public sealed record RenglonDelGrupo(
    bool EsCabecera,
    string Titulo,
    string Detalle,
    long CasoId,
    IReadOnlyList<long> CasosDeLaUnidad,
    string Cedula,
    bool EstaCompleta,
    bool ElPdfNoSePuedeAbrir,
    bool? Recomendacion = null,
    string LoQueLeFaltaAlDocumento = "")
{
    /// <summary>La cabecera de una unidad dentro del dia.</summary>
    public static RenglonDelGrupo Cabecera(UnidadDelGrupo unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        return new RenglonDelGrupo(
            true, unidad.Titulo, unidad.Detalle, 0, unidad.CasosQueSePuedenAsignar, string.Empty,
            unidad.CuantasCompletas == unidad.CuantosDocumentos && unidad.CuantosDocumentos > 0,
            false);
    }

    /// <summary>El renglon de una persona del grupo.</summary>
    /// <remarks>
    /// ⚠️ <b>Desde el 2026-09-07 se lleva ademas lo que le falta al documento.</b> Se
    /// calculaba desde el 2026-09-06 —<c>PersonaDelGrupo.CuantoLeFalta</c>— y hasta hoy se
    /// quedaba aqui: la pantalla no lo recibia, asi que un documento entero y uno al que le
    /// faltan tres campos se leian exactamente igual. Es la queja del dueno «cuando guardo
    /// información ya corregida no cambia de estado, sigue igual».
    /// </remarks>
    public static RenglonDelGrupo DeUnaPersona(PersonaDelGrupo persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return new RenglonDelGrupo(
            false, persona.Nombre, persona.Detalle, persona.CasoId, [], persona.CedulaTexto,
            persona.EstadoDelDocumento == EstadoDeRecomendacion.Completa,
            persona.ElPdfNoSePuedeAbrir,
            persona.Recomendacion,
            persona.LoQueFaltaTexto);
    }

    /// <summary>Si este renglon es una persona; lo lee la plantilla para ensenar su mitad.</summary>
    public bool EsUnaPersona => !EsCabecera;

    /// <summary>Si el boton de la cabecera tiene algo que asignar.</summary>
    public bool SePuedeAsignarLaUnidad => EsCabecera && CasosDeLaUnidad.Count > 0;

    /// <summary>Lo que lee en voz alta un lector de pantalla.</summary>
    /// <remarks>
    /// Lo que le falta al documento va tambien aqui: si solo estuviera pintado, quien no ve la
    /// pantalla no se enteraria de lo unico que cambia al corregir un documento, y la mitad
    /// del arreglo del 2026-09-07 no existiria para el.
    /// </remarks>
    public string ParaElLector => EsCabecera
        ? $"Unidad {Titulo}. {Detalle}"
        : $"{Titulo}. {Cedula}. {Detalle}. {LoQueLeFaltaAlDocumento}. Pulse para verificar este documento.";
}
