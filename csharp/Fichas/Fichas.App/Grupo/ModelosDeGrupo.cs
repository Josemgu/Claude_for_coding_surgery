using Fichas.App.Inicio;
using Fichas.App.Vocabulario;
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
    /// <param name="estado">Lo que dice <c>casos.estado_recomendacion</c>.</param>
    /// <param name="motivo">Por qué no está completa; solo cuenta con «no completa».</param>
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
    /// <param name="motivo">El motivo que se dice.</param>
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
/// <param name="Archivado">
/// Si el documento del que sale esta archivado. Entro el 2026-09-14 para que el color del
/// renglon siga la MISMA regla con la que el calendario cuenta «resueltas» —seis en si, o
/// archivado—, y desde ese mismo dia <see cref="LectorDeGrupos.DelDia"/> los trae: el renglon
/// sale verde, se lee «resuelto» con la nota «archivado» al lado, y ni se asigna ni se
/// verifica desde aqui.
/// </param>
/// <param name="LasQueNoDicenSi">
/// Sus pasos que NO dicen que si —en no o en blanco—, sin el numero de delante; nulo si quien
/// la arma no los tiene. Entro el 2026-09-16 para saber si esta A MEDIAS: con alguna en si y no
/// las seis, el renglon va en naranja y dice cuales le faltan. Sin esto se lee como hasta ese
/// dia: rojo.
/// </param>
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
    IReadOnlyList<string>? SeQuedoEn = null,
    bool Archivado = false,
    IReadOnlyList<string>? LasQueNoDicenSi = null)
{
    /// <summary>Los pasos donde se quedo, nunca nulo.</summary>
    public IReadOnlyList<string> PasosSinCompletar => SeQuedoEn ?? [];

    /// <summary>
    /// Verde si esta resuelta, naranja si esta a medias, rojo si le falta todo; la regla esta
    /// en <see cref="ColorDelRenglon"/>.
    /// </summary>
    /// <remarks>
    /// Sale de la MISMA lectura que <see cref="PalabraDelEstado"/>: el color no puede decir
    /// una cosa y la palabra otra, que es justo lo que el dueno leia el 2026-09-05.
    /// </remarks>
    public ColorDeLaPastilla Color => ColorDelRenglon.De(Lectura, Archivado);

    /// <summary>Si esta a medias: alguna de las seis en si y no las seis, y no archivada.</summary>
    /// <remarks>
    /// Un archivado a medias NO esta a medias: esta resuelto, porque lo cerro el dueno. Es lo
    /// que dice <see cref="Lectura"/>, que para un archivado no mira las seis.
    /// </remarks>
    public bool AMedias => Lectura.AMedias;

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

    /// <summary>La nota que lleva al lado de su palabra el renglon de un archivado.</summary>
    /// <remarks>
    /// ⛔ <b>No es una tercera palabra de estado.</b> Las dos del 2026-09-07 siguen siendo las
    /// unicas —<see cref="DosEstados.LasDos"/>—; esto es una nota al lado de «resuelto», y va
    /// en minuscula y sin destacar porque lo que al dueno le estorbaba el 2026-09-06 era la
    /// etiqueta «ARCHIVADO» en medio del trabajo, no saber que lo archivo.
    /// </remarks>
    public const string NotaDeArchivado = "archivado";

    /// <summary>Donde se ve y se desarchiva lo archivado; es el motivo de cada boton apagado.</summary>
    public const string DondeSeDesarchiva = "se ve y se desarchiva en Revisar";

    /// <summary>Lo que se lee de ESTA persona: una de las dos palabras, y su detalle.</summary>
    /// <remarks>
    /// <para>⛔ <b>Es la persona y no el documento.</b> Cinco personas del mismo papel pueden
    /// leerse distinto, y esa separacion es del 2026-09-05 y no se toca. Lo del documento sigue
    /// en <see cref="EstadoDelDocumentoTexto"/>, que ya no se pinta en el renglon.</para>
    ///
    /// <para>⚠️ <b>Una persona de un documento archivado se lee «resuelto» aunque sus seis
    /// preguntas no digan que si</b>, con la misma excepcion con la que el calendario cuenta
    /// «resueltas» (<c>LectorDeGrupos.ArmarLaPastilla</c>: <c>confirmada || caso.Archivado</c>):
    /// archivar es el gesto con el que el dueno cierra un documento. Su detalle dice que fue el
    /// quien lo cerro y donde se deshace, y NO dice que sus seis esten confirmadas: eso seria
    /// inventarlo, y <see cref="Recomendacion"/> sigue diciendo lo que dicen.</para>
    /// </remarks>
    public LoQueSeLeeDeUnaPersona Lectura => Archivado
        ? new LoQueSeLeeDeUnaPersona(LoQueSeLee.Resuelto, "lo archivaste tú · no queda nada que hacer con ella")
        : LoQueSeLeeDeUnaPersona.De(Recomendacion, PasosSinCompletar, LasQueNoDicenSi);

    /// <summary>Si no le queda nada que hacer con ella: las seis en si, o su documento archivado.</summary>
    public bool EstaResuelta => Lectura.EsResuelto;

    /// <summary>«resuelto» o «me falta»; la unica palabra de estado del renglon.</summary>
    public string PalabraDelEstado => Lectura.Palabra;

    /// <summary>Que le falta a esta persona y a quien le toca; se lee cuando el lo pide.</summary>
    public string DetalleDelEstado => Lectura.Detalle;

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

    /// <summary>
    /// Que le falta al DOCUMENTO y a quien le toca, ya en palabras.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>2026-09-07:</b> decia «le faltan 3 datos» o «listo para asignar · el sistema
    /// llenó todos los campos». Lo segundo era una de las cuatro palabras retiradas.</para>
    ///
    /// <para>⚠️ <b>Aqui va el DETALLE y no la palabra</b>, y a proposito: la palabra de este
    /// renglon es la de la PERSONA —<see cref="PalabraDelEstado"/>, que ya va en
    /// <see cref="Detalle"/>—, y poner otra al lado devolveria las dos frases que el dueno leia
    /// una creyendo la otra. Esta columna contesta «¿por que estoy mirando este renglon?», que
    /// es a quien hay que llamar.</para>
    ///
    /// <para>Y aqui el detalle SI va a la vista, no a un clic: esta pantalla es a la que el
    /// entra cuando ya ha preguntado —eligio un dia y un grupo—, y es donde marca el telefono.
    /// El clic que ahorra el detalle es el de Revisar, donde hay 3 000 tarjetas compitiendo.</para>
    ///
    /// <para>⚠️ Sale de <see cref="LasDosPreguntas.LoQueLeFaltaAlDocumento"/> y NO se compone
    /// aqui con <c>LoQueSeLeeDeUnDocumento.De</c> aparte: por ese metodo
    /// pasan tambien <c>LoQueLeFaltaACadaDocumento</c> y el pie de Correccion, y componerlo
    /// aqui aparte es exactamente lo que hacia que el mismo documento se leyera distinto en dos
    /// pantallas. Lo vigila
    /// <c>PruebasDeLoQueLeFaltaACadaDocumento.ContestaLoMismoQueLaPantallaDelGrupoEnTodaLaBase</c>.</para>
    ///
    /// <para>⚠️ <b>Un archivado no contesta esta pregunta: dice que esta archivado y donde se
    /// deshace.</b> Correccion no lo recibe (2026-09-06), asi que decirle «te toca a ti, en
    /// Correccion» lo mandaria a una pantalla donde no esta. Esta es la unica columna del
    /// renglon que cambia de frase por estar archivado, y es donde va el motivo de que su
    /// boton este apagado.</para>
    /// </remarks>
    public string LoQueFaltaTexto => Archivado
        ? $"{NotaDeArchivado} · {DondeSeDesarchiva}"
        : LasDosPreguntas.LoQueLeFaltaAlDocumento(CuantoLeFalta, SinNingunaPersonaLeida);

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
    /// <remarks>
    /// ⛔ <b>2026-09-07:</b> aqui iba <see cref="RecomendacionTexto"/> —«lista para viajar · 
    /// recomendacion confirmada», tres de las palabras retiradas—. Lo que decia no se pierde:
    /// va en <see cref="DetalleDelEstado"/>, que se lee al pulsar la palabra.
    /// </remarks>
    /// <remarks>
    /// ⚠️ <b>2026-09-14:</b> un archivado lleva la nota «archivado» pegada a su palabra
    /// —«resuelto · archivado»—, que es donde el dueno pidio ver el estado. Sigue sin haber
    /// una tercera palabra: <see cref="PalabraDelEstado"/> es una de las dos.
    /// <para>⚠️ <b>2026-09-16:</b> y una persona a medias lleva la nota «a medias» de la misma
    /// forma —«me falta · a medias»— (<see cref="DosEstados.NotaDeAMedias"/>): el color nunca va
    /// solo, y asi el naranja tiene su palabra al lado. Que le falta va en su propia linea del
    /// renglon, <see cref="DetalleDelEstado"/>, que para ella si se ensena.</para>
    /// </remarks>
    public string Detalle
        => $"{NumeroCaso} · {PalabraDelEstado}{Nota}"
           + $" · {DuenoTexto} · {PdfTexto}";

    /// <summary>« · archivado», « · a medias», o nada.</summary>
    private string Nota => Archivado
        ? " · " + NotaDeArchivado
        : AMedias ? " · " + DosEstados.NotaDeAMedias : string.Empty;
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
/// <param name="Fecha">
/// El dia en que viaja esta unidad. Entro el 2026-09-09: el dueno lee la unidad ENTERA en un
/// renglon —<i>«Rama San Juan No 325535, 10 personas viajarán el 12 de septiembre»</i>— y con
/// la fecha solo en el titulo de la pantalla, ese renglon copiado o leido en voz alta no dice
/// de que dia habla.
/// </param>
/// <param name="CasoIds">Los documentos de esta unidad ese dia, archivados incluidos desde el 2026-09-14.</param>
/// <param name="Personas">Las personas, en el orden en que se ensenan.</param>
/// <param name="CuantasCompletas">Cuantos de esos documentos estan completos.</param>
/// <param name="Motivo">El motivo que mas se repite entre los que no estan completos.</param>
public sealed record UnidadDelGrupo(
    string UnidadNumero,
    string UnidadNombre,
    DateOnly Fecha,
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
    /// 2026-09-05 —«cuando yo archive, debe salir del sistema visible»—. Del 2026-09-06 al
    /// 2026-09-14 se cumplia mas arriba, porque <see cref="LectorDeGrupos"/> no traia ni un
    /// archivado; desde el 14 los trae —para que dentro de la fecha cuadre con el calendario— y
    /// vuelve a cumplirse AQUI: se dejan fuera los que <see cref="Personas"/> dicen archivados.
    /// Se mira en las personas y no en una lista aparte porque cada documento tiene al menos un
    /// renglon —el de «sin ninguna persona leida» si no trajo a nadie—, y una segunda lista
    /// seria una segunda fuente que algun dia diria otra cosa.
    /// <para>Medido el 2026-09-05 sobre el paquete publicado, antes de arreglarlo: «Asignar
    /// el grupo entero» sobre el 8 de septiembre dejo <b>8 filas</b> en <c>asignaciones</c>,
    /// y una era la del documento archivado. Esta propiedad se queda —en vez de que la
    /// pantalla use <see cref="CasoIds"/> a pelo— para que ese defecto tenga un solo sitio
    /// donde volver a mirarse.</para>
    /// </remarks>
    public IReadOnlyList<long> CasosQueSePuedenAsignar
    {
        get
        {
            var archivados = Personas.Where(p => p.Archivado).Select(p => p.CasoId).ToHashSet();
            return [.. CasoIds.Where(id => !archivados.Contains(id))];
        }
    }

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

    /// <summary>
    /// De sus personas, cuantas ya no le dejan nada que hacer: las confirmadas MAS las que van
    /// en un documento archivado.
    /// </summary>
    /// <remarks>
    /// <para>Es la MISMA cuenta que <see cref="PastillaDeDia.CuantasPersonasResueltas"/> hace
    /// para el calendario de fuera (<c>confirmada || caso.Archivado</c>), y por eso cuadran.
    /// Las dos cifras van aparte y no se pisan: «confirmada» es una afirmacion sobre las seis
    /// preguntas y no se toca; «resuelta» dice si al dueno le queda algo que hacer, y ahi
    /// archivar cuenta (2026-09-07).</para>
    ///
    /// <para>⚠️ La fila «sin ninguna persona leida» de un archivado NO se cuenta, igual que fuera:
    /// la pastilla solo cuenta personas que existen, y esa fila es la ausencia de una.</para>
    /// </remarks>
    public int CuantasPersonasResueltas => Personas.Count(p => !p.SinNingunaPersonaLeida && p.EstaResuelta);

    /// <summary>Las que todavia no: las que estan en «no» y las que nadie miro, sin las de un archivado.</summary>
    /// <remarks>
    /// Se cuenta lo que NO esta resuelto y no «las que no estan confirmadas»: desde el
    /// 2026-09-14 una persona de un archivado no esta confirmada y aun asi no queda por
    /// verificar, porque ese documento lo cerro el dueno. Con la cuenta vieja la cabecera diria
    /// «resuelto» y debajo «falta verificar 2 recomendaciones» del mismo grupo.
    /// </remarks>
    public int CuantasPersonasSinConfirmar => Personas.Count(p => !p.EstaResuelta);

    /// <summary>
    /// De que color va la cabecera de esta unidad dentro de la fecha: el mismo que su pastilla
    /// en el calendario de fuera.
    /// </summary>
    /// <remarks>
    /// <para>Es <c>ColoresDeLaPastilla.De</c> y no una regla nueva: verde con todas resueltas,
    /// rojo con alguna sin resolver, gris sin nadie leido. Se cuenta sobre
    /// <see cref="CuantasPersonasResueltas"/> —y no sobre las confirmadas— desde el 2026-09-14,
    /// que es cuando los archivados entraron a esta lista: hasta entonces una unidad con uno
    /// podia ir verde fuera y rojo dentro, y eso se midio y se dijo.</para>
    ///
    /// <para>⚠️ Se cuenta sobre <see cref="CuantasPersonasLeidas"/> y no sobre
    /// <c>Personas.Count</c>: la fila «sin ninguna persona leida» va en esa lista para que el
    /// documento no desaparezca, y contarla como persona pintaria en rojo una unidad de la que
    /// no se leyo a nadie, que es el «0 de 0 en rojo» que el dueno ya vio el 2026-09-07.</para>
    /// </remarks>
    public ColorDeLaPastilla Color => ColoresDeLaPastilla.De(CuantasPersonasResueltas, CuantasPersonasLeidas);

    /// <summary>Cuantas personas de verdad se leyeron: sin la fila que dice que no se leyo a nadie.</summary>
    public int CuantasPersonasLeidas => Personas.Count(p => !p.SinNingunaPersonaLeida);

    /// <summary>«10 personas viajan el 12 de septiembre · 2 documentos · me falta 5 de 10».</summary>
    /// <remarks>
    /// <para>⛔ <b>Decia «2 de 7 personas confirmadas · 1 de 3 completas»</b> hasta el
    /// 2026-09-07: DOS cuentas de dos sitios distintos —las PERSONAS confirmadas en el sistema
    /// del lider y los DOCUMENTOS que dio por completos el Excel del companero—, con dos de las
    /// cuatro palabras que el dueno retiro. Las dos siguen calculandose y siguen aparte
    /// (<see cref="CuantasPersonasConfirmadas"/> y <c>CuantasCompletas</c>); lo que se pinta es
    /// una, en PERSONAS, que es la unidad de trabajo que el declaro el 2026-09-05.</para>
    ///
    /// <para>⚠️ <b>2026-09-09: las personas van delante y con su fecha.</b> El dueno dicto el
    /// renglon con sus palabras —<i>«Rama San Juan No 325535, 10 personas viajarán el 12 de
    /// septiembre»</i>—, y lo primero que dice de una unidad es cuanta gente viaja y cuando. Los
    /// documentos siguen ahi: son lo que se asigna, y sin esa cifra el boton «Asignar esta
    /// unidad» diria cuanto trabajo mueve solo despues de pulsarlo.</para>
    ///
    /// <para>⚠️ <b>2026-09-14: la cuenta es de RESUELTAS y no de confirmadas</b>, que es la de
    /// la pastilla de fuera. Con los archivados dentro, contar confirmadas diria «me falta 5 de
    /// 5» de una unidad cuya pastilla dice «me falta 3 de 5» o «resuelto».</para>
    /// </remarks>
    public string Detalle
        => Plural.Con(Personas.Count, "persona", "personas")
           + $" {(Personas.Count == 1 ? "viaja" : "viajan")} el {FechasEnEspanol.DecirElDiaYElMes(Fecha)}"
           + " · " + Plural.Con(CuantosDocumentos, "documento", "documentos")
           + " · " + DosEstados.Cuenta(CuantasPersonasResueltas, Personas.Count);

    /// <summary>Cuantos de sus documentos tienen todavia algun hueco del papel.</summary>
    /// <remarks>
    /// ⛔ La cuenta la trae ya hecha <see cref="PersonaDelGrupo.CuantoLeFalta"/>, que sale de
    /// <see cref="LoQueLeFalta"/> —el veredicto unico—. Aqui solo se cuentan documentos
    /// DISTINTOS: un documento de cinco personas pinta cinco renglones con la misma cifra
    /// dentro, y sumarlos diria cinco documentos donde hay uno.
    /// </remarks>
    public int CuantosDocumentosConHuecos
        => Personas.Where(p => p.CuantoLeFalta > 0).Select(p => p.CasoId).Distinct().Count();

    /// <summary>
    /// Que le falta a esta unidad, dicho de forma que se sepa QUE hacer: «resuelto», o las dos
    /// faltas que existen, cada una con su sitio.
    /// </summary>
    /// <remarks>
    /// <para><b>De donde sale.</b> El dueno dicto la cabecera de la unidad pequena asi:
    /// <i>«Barrio Marito 656351, 5 personas viajarán el 12 de septiembre, falta verificar
    /// recomendaciones»</i>. La cifra sola —«me falta 5 de 10»— no dice de que, y las dos
    /// faltas que puede tener una unidad <b>se arreglan por caminos distintos</b>: los huecos
    /// del papel se llenan escribiendo en Correccion, y las recomendaciones se resuelven
    /// llamando al lider (<i>«yo debo llamar al obispo»</i>, 2026-09-05). Meterlas en una sola
    /// frase obligaria a abrir la unidad para saber cual de los dos trabajos toca.</para>
    ///
    /// <para>⛔ <b>Es un DETALLE y no una palabra de estado.</b> La palabra sigue siendo una de
    /// las dos y va en <see cref="Detalle"/>; esto es la otra mitad de la decision del
    /// 2026-09-07: «me falta» siempre puede decir que falta y a quien le toca.</para>
    /// </remarks>
    public string LoQueLeFaltaALaUnidad
    {
        get
        {
            var trozos = new List<string>();

            if (CuantosDocumentosConHuecos > 0)
            {
                trozos.Add(Plural.Con(CuantosDocumentosConHuecos, "documento", "documentos")
                           + " con datos del papel por completar · te toca a ti, en Corrección");
            }

            if (CuantasPersonasSinConfirmar > 0)
            {
                trozos.Add("falta verificar "
                           + Plural.Con(CuantasPersonasSinConfirmar, "recomendación", "recomendaciones")
                           + " en el sistema del líder");
            }

            return trozos.Count == 0 ? DosEstados.Resuelto : string.Join(" · ", trozos);
        }
    }
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
    /// <param name="fecha">El día que no trae nada.</param>
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

    /// <summary>
    /// Cuantas ya no le dejan nada que hacer: las confirmadas MAS las de un documento archivado.
    /// </summary>
    /// <remarks>
    /// Es la suma de <see cref="UnidadDelGrupo.CuantasPersonasResueltas"/> de cada unidad, que
    /// es la cuenta de la pastilla de fuera; asi la cabecera del dia dice lo mismo que el
    /// calendario (2026-09-14). La fila «sin ninguna persona leida» no cuenta, como fuera.
    /// </remarks>
    public int CuantasPersonasResueltas => Unidades.Sum(u => u.CuantasPersonasResueltas);

    /// <summary>Cuantas tienen alguna de las seis preguntas marcada que NO, sin las de un archivado.</summary>
    /// <remarks>
    /// Desde el 2026-09-14 las de un archivado no entran aqui ni en
    /// <see cref="CuantasPersonasSinMirar"/>: ese documento lo cerro el dueno y no queda por
    /// verificar, aunque sus seis preguntas sigan diciendo lo que dicen.
    /// </remarks>
    public int CuantasPersonasNoListas => TodasLasPersonas.Count(p => !p.Archivado && p.Recomendacion == false);

    /// <summary>
    /// Cuantas tienen alguna pregunta en blanco y ninguna en no: nadie las miro. Sin las de un
    /// archivado.
    /// </summary>
    /// <remarks>Va aparte de las anteriores porque «sin mirar» no es «no» (C18-2).</remarks>
    public int CuantasPersonasSinMirar => TodasLasPersonas.Count(p => !p.Archivado && p.Recomendacion is null);

    /// <summary>Las que le quedan por verificar: las que estan en «no» y las que nadie miro.</summary>
    public int CuantasPersonasSinConfirmar => CuantasPersonasNoListas + CuantasPersonasSinMirar;

    /// <summary>
    /// «resuelto» o «me falta 6 de 10», la linea del dia contada en PERSONAS.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Decia «4 de 10 personas confirmadas · 3 sin mirar»</b> hasta el 2026-09-07,
    /// con dos de las palabras retiradas. Lo que separaba «sin mirar» de «no lista» sigue vivo
    /// —<see cref="CuantasPersonasSinMirar"/> y <see cref="CuantasPersonasNoListas"/> se
    /// calculan igual, y <c>LoQueSeLeeDeUnaPersona</c> las dice distinto en el detalle de cada
    /// renglon—; lo que se retira es esa distincion de la CABECERA, donde competia con la
    /// cuenta de documentos y hacia que el dueno leyera una creyendo la otra.</para>
    ///
    /// <para>⚠️ <b>2026-09-14: se cuenta en RESUELTAS</b>, que es la cuenta del calendario de
    /// fuera; con los archivados dentro, contar confirmadas diria otra cifra que la pastilla.</para>
    /// </remarks>
    public string ComoVanLasPersonas
        => EstaVacio ? "no viaja nadie este día" : DosEstados.Cuenta(CuantasPersonasResueltas, TodasLasPersonas.Count);

    /// <summary>
    /// Los que se pueden asignar del dia entero.
    /// </summary>
    /// <remarks>El motivo entero esta en <see cref="UnidadDelGrupo.CasosQueSePuedenAsignar"/>.</remarks>
    public IReadOnlyList<long> LosQueSePuedenAsignar => [.. Unidades.SelectMany(u => u.CasosQueSePuedenAsignar)];

    /// <summary>El denominador que el criterio exige decir: N documentos, N personas, N unidades.</summary>
    /// <remarks>
    /// ⛔ No lleva «N ARCHIVADO» detras. Se quito el 2026-09-06 porque el archivado no entraba
    /// en este grupo, y NO vuelve el 2026-09-14 aunque ahora entre: la etiqueta en la cabecera
    /// es justo lo que al dueno le confundia; la nota va en cada renglon, al lado de su palabra.
    /// Donde se cuentan es en Reportes.
    /// </remarks>
    public string LineaDelDenominador
        => Plural.Con(CuantosDocumentos, "documento", "documentos")
           + " · " + Plural.Con(CuantasPersonas, "persona", "personas")
           + " · " + Plural.Con(Unidades.Count, "unidad", "unidades");

    /// <summary>«resuelto» o «me falta 5 de 7», contado en DOCUMENTOS, con su motivo.</summary>
    /// <remarks>
    /// ⛔ Decia «2 de 7 completas» hasta el 2026-09-07. El motivo se conserva —es lo que el
    /// dueno pidio el 2026-09-05 al nombrar sus tres motivos— porque un motivo NO es una
    /// palabra de estado: dice por que falta, no si falta.
    /// </remarks>
    public string ComoVa
    {
        get
        {
            if (EstaVacio) return "no viaja nadie este día";

            var cuenta = DosEstados.Cuenta(CuantasCompletas, CuantosDocumentos);
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
/// <param name="PalabraDelEstado">
/// «resuelto» o «me falta», dicho de ESTA PERSONA. Vacio en una cabecera: la cabecera lleva su
/// cuenta en <see cref="Detalle"/>.
/// </param>
/// <param name="DetalleDelEstado">
/// Que le falta a esta persona y a quien le toca. Se lee cuando el dueno lo pide, no de entrada.
/// </param>
/// <param name="LoQueLeFaltaALaUnidad">
/// Que le falta a la UNIDAD de esta cabecera y donde se arregla. Vacio en el renglon de una
/// persona: una persona no es una unidad, y lo suyo va en <paramref name="DetalleDelEstado"/>.
/// </param>
/// <param name="Color">
/// De que color va el renglon: verde si esta resuelto, rojo si le falta algo, y gris solo en
/// la cabecera de una unidad sin nadie leido. Es lo que el dueno pidio el 2026-09-14 —<i>«lo
/// que este completo se marque en verde y lo que no en rojo, como se muestra en el calendario
/// afuera»</i>— y la plantilla lo pinta con el mismo par de colores del calendario.
/// </param>
/// <param name="Archivado">
/// Si el documento de esta persona esta archivado. Entro el 2026-09-14, cuando los archivados
/// empezaron a entrar al grupo de su fecha: el renglon va verde, y su boton de verificar se
/// apaga (<see cref="SePuedeVerificar"/>). Falso en una cabecera: lo suyo va en
/// <paramref name="CasosDeLaUnidad"/>, que ya viene sin archivados.
/// </param>
/// <param name="AMedias">
/// Si esta persona esta a medias: alguna de las seis en si y no las seis (2026-09-16). El
/// renglon va en naranja y ensena <paramref name="DetalleDelEstado"/> a la vista, que es donde
/// dice cuales le faltan: el dueno pidio «indicar que le falta». Falso en una cabecera.
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
    string LoQueLeFaltaAlDocumento = "",
    string PalabraDelEstado = "",
    string DetalleDelEstado = "",
    string LoQueLeFaltaALaUnidad = "",
    ColorDeLaPastilla Color = ColorDeLaPastilla.Rojo,
    bool Archivado = false,
    bool AMedias = false)
{
    /// <summary>La cabecera de una unidad dentro del dia.</summary>
    /// <remarks>
    /// ⚠️ <b>Desde el 2026-09-09 se lleva ademas lo que le falta a la unidad.</b> Hasta hoy la
    /// cabecera decia cuanto faltaba —«me falta 5 de 10»— y no de que, asi que habia que abrir
    /// la unidad para saber si el trabajo era escribir en Correccion o llamar al lider. Es lo
    /// que el dueno dicto el 2026-09-07: <i>«5 personas viajarán el 12 de septiembre, falta
    /// verificar recomendaciones»</i>.
    /// </remarks>
    /// <param name="unidad">La unidad de la que es cabecera; no puede ser nulo.</param>
    public static RenglonDelGrupo Cabecera(UnidadDelGrupo unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        return new RenglonDelGrupo(
            true, unidad.Titulo, unidad.Detalle, 0, unidad.CasosQueSePuedenAsignar, string.Empty,
            unidad.CuantasCompletas == unidad.CuantosDocumentos && unidad.CuantosDocumentos > 0,
            false,
            LoQueLeFaltaALaUnidad: unidad.LoQueLeFaltaALaUnidad,
            Color: unidad.Color);
    }

    /// <summary>El renglon de una persona del grupo.</summary>
    /// <remarks>
    /// ⚠️ <b>Desde el 2026-09-07 se lleva ademas lo que le falta al documento.</b> Se
    /// calculaba desde el 2026-09-06 —<c>PersonaDelGrupo.CuantoLeFalta</c>— y hasta hoy se
    /// quedaba aqui: la pantalla no lo recibia, asi que un documento entero y uno al que le
    /// faltan tres campos se leian exactamente igual. Es la queja del dueno «cuando guardo
    /// información ya corregida no cambia de estado, sigue igual».
    /// </remarks>
    /// <param name="persona">La persona del renglón; no puede ser nulo.</param>
    public static RenglonDelGrupo DeUnaPersona(PersonaDelGrupo persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return new RenglonDelGrupo(
            false, persona.Nombre, persona.Detalle, persona.CasoId, [], persona.CedulaTexto,
            persona.EstadoDelDocumento == EstadoDeRecomendacion.Completa,
            persona.ElPdfNoSePuedeAbrir,
            persona.Recomendacion,
            persona.LoQueFaltaTexto,
            persona.PalabraDelEstado,
            persona.DetalleDelEstado,
            Color: persona.Color,
            Archivado: persona.Archivado,
            AMedias: persona.AMedias);
    }

    /// <summary>Si este renglon es una persona; lo lee la plantilla para ensenar su mitad.</summary>
    public bool EsUnaPersona => !EsCabecera;

    /// <summary>Si el boton de la cabecera tiene algo que asignar.</summary>
    public bool SePuedeAsignarLaUnidad => EsCabecera && CasosDeLaUnidad.Count > 0;

    /// <summary>
    /// Si pulsar este renglon abre su documento en Correccion: una persona de un documento que
    /// no esta archivado.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Un archivado no se verifica desde aqui.</b> Correccion no lo recibe
    /// (2026-09-06, «sale de todos lados»), asi que abrirlo desde este renglon lo mandaria a una
    /// pantalla que no lo tiene. Lo que se puede hacer con el —verlo y desarchivarlo— esta en
    /// Revisar, y el renglon lo dice en su columna de lo que le falta y en voz alta.</para>
    ///
    /// <para>Medido el 2026-09-14 antes de tocar nada: el renglon de una persona ofrece UN solo
    /// boton, el renglon entero (<c>AlPulsarUnaPersona</c>), y la cabecera de su unidad otro,
    /// «Asignar esta unidad», que ya viene sin archivados por <see cref="CasosDeLaUnidad"/>.
    /// Los dos se apagan para el archivado; no hay un tercero.</para>
    /// </remarks>
    public bool SePuedeVerificar => EsUnaPersona && !Archivado;

    /// <summary>Lo que lee en voz alta un lector de pantalla.</summary>
    /// <remarks>
    /// <para>Lo que le falta al documento va tambien aqui: si solo estuviera pintado, quien no
    /// ve la pantalla no se enteraria de lo unico que cambia al corregir un documento, y la
    /// mitad del arreglo del 2026-09-07 no existiria para el.</para>
    ///
    /// <para>Y un archivado no dice «pulse para verificar»: su boton esta apagado, y decirlo
    /// seria prometer algo que no pasa. Dice en su lugar donde se ve y se desarchiva.</para>
    /// </remarks>
    public string ParaElLector => EsCabecera
        ? $"Unidad {Titulo}. {Detalle}. {LoQueLeFaltaALaUnidad}"
        : $"{Titulo}. {Cedula}. {Detalle}. {DetalleDelEstado}. {LoQueLeFaltaAlDocumento}. "
          + (SePuedeVerificar
              ? "Pulse para verificar este documento."
              : "Documento archivado: no se verifica desde aquí.");
}
