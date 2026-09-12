using Fichas.App.Grupo;
using Fichas.App.Vocabulario;
using Fichas.Reportes.Reglas;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Inicio;

/// <summary>
/// Las DOS cifras de Inicio, y no hay una tercera.
/// </summary>
/// <remarks>
/// <para>Palabras del dueno el 2026-09-05: <i>«Haz una mejor gestion de las notificaciones.
/// Lo unico que quiero ver en Home es lo que esta listo para asignar y lo que esta asignado
/// a los agentes. Luego crea una ventana de revisar lo que no esta completo, y ponlo por
/// grupo de fechas»</i>.</para>
///
/// <para><b>Lo que habia antes no se borro: se movio.</b> «Personas por viajar», «Con la
/// recomendacion completa», «Viajaron sin verificar», «Por verificar antes de que salgan» y
/// «Sin fecha de viaje» son todas preguntas sobre lo que NO esta resuelto, y viven ahora en
/// la ventana de incompletos (<see cref="LectorDeIncompletos"/>), agrupadas por fecha de
/// viaje, que es donde el pidio verlas. «Hojas sin devolver» y «El equipo» son preguntas
/// sobre lo asignado, y se quedan aqui dentro de la segunda cifra.</para>
/// </remarks>
/// <param name="ListoParaAsignar">Documentos sin archivar, sin nadie que los lleve y sin ningun dato pendiente.</param>
/// <param name="AsignadoALosAgentes">Documentos sin archivar que ahora mismo lleva un companero.</param>
/// <param name="PersonasListas">Cuantas personas suman los documentos listos.</param>
/// <param name="SinDevolver">De los asignados, cuantos no han vuelto todavia con su hoja.</param>
public sealed record ContadoresDeInicio(
    int ListoParaAsignar,
    int AsignadoALosAgentes,
    int PersonasListas,
    int SinDevolver);

/// <summary>
/// El cuadro de Inicio: las DOS cifras que el dueno dejo en esa pantalla, con sus palabras.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Peticion del dueno del 2026-09-07, repetida el 2026-09-09
/// porque todavia no estaba hecha: <i>«Lo unico que quiero [en Inicio] es el calendario y un
/// cuadro informando cuales hacen falta por completar, y cuantos casos tienen los
/// agentes»</i>.</para>
///
/// <para>⛔ <b>Es un cuadro de cifras, no una lista para trabajar.</b> Las tres listas que
/// Inicio tenia —lo listo para asignar, lo asignado a los agentes y lo listo para viajar— se
/// fueron enteras a la pestana del flujo, con su entrada propia en el menu. No se borro nada:
/// cada una tiene su puerta nueva antes de que se cerrara la vieja.</para>
///
/// <para>⚠️ <b>Las dos cifras se calculan en la MISMA pasada que arma Inicio</b>, no montando
/// un segundo lector. La ventana de incompletos vuelve a calcular la suya por su cuenta, asi
/// que <c>PruebasDelCuadroDeInicio</c> compara las dos: si las reglas se separan, el dueno
/// leeria un numero en Inicio y contaria otro al abrir la ventana.</para>
/// </remarks>
/// <param name="DocumentosPorCompletar">Cuantos documentos vivos tienen algo que falta.</param>
/// <param name="PersonasPorCompletar">Cuantas personas van en esos documentos.</param>
/// <param name="CasosDeLosAgentes">Cuantos documentos llevan ahora mismo los companeros.</param>
/// <param name="CompanerosConCasos">Cuantos companeros llevan al menos uno.</param>
/// <param name="SinDevolver">De los de los agentes, cuantos no han vuelto todavia.</param>
public sealed record CuadroDeInicio(
    int DocumentosPorCompletar,
    int PersonasPorCompletar,
    int CasosDeLosAgentes,
    int CompanerosConCasos,
    int SinDevolver)
{
    /// <summary>La cifra grande de lo que falta por completar.</summary>
    public string CifraDeLoQueFalta => EnCifras(DocumentosPorCompletar);

    /// <summary>La cifra grande de lo que llevan los agentes.</summary>
    public string CifraDeLosAgentes => EnCifras(CasosDeLosAgentes);

    /// <summary>«12 documentos · 41 personas», el denominador de la primera cifra.</summary>
    /// <remarks>
    /// Nunca deja un cero mudo (C20-6): un cero que no dice de que es se lee como «no hay
    /// datos», y este cuadro es lo unico que queda en Inicio junto al calendario.
    /// </remarks>
    public string LineaDeLoQueFalta
        => Plural.Con(DocumentosPorCompletar, "documento", "documentos")
           + " · " + Plural.Con(PersonasPorCompletar, "persona", "personas");

    /// <summary>«3 compañeros · 5 sin devolver», el denominador de la segunda.</summary>
    /// <remarks>
    /// Con cero companeros llevando algo no se escribe «0 compañeros», que suena a que no hay
    /// equipo: se dice que no lo lleva nadie, que es lo que pasa de verdad.
    /// </remarks>
    public string LineaDeLosAgentes
        => (CompanerosConCasos == 0
                ? "no lo lleva nadie"
                : Plural.Con(CompanerosConCasos, "compañero", "compañeros"))
           + $" · {SinDevolver} sin devolver";

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre la primera cifra.</summary>
    public string LoQueFaltaParaElLector
        => $"Me falta por completar: {LineaDeLoQueFalta}. Pulse para ver cuáles.";

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre la segunda.</summary>
    public string LosAgentesParaElLector
        => $"Lo que tienen los agentes: {Plural.Con(CasosDeLosAgentes, "caso", "casos")}, "
           + $"{LineaDeLosAgentes}. Pulse para abrir el flujo de trabajo.";

    /// <summary>Escribe un numero sin que el idioma de la maquina le cambie el separador.</summary>
    /// <param name="numero">La cifra que va grande en el cuadro.</param>
    private static string EnCifras(int numero)
        => numero.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Los denominadores. Existen porque el criterio C1-1 lo exige: «N casos en la base,
/// N en pantalla». Una cifra sin su denominador no se puede comprobar.
/// </summary>
/// <param name="CasosEnLaBase">Cuantos casos hay, archivados incluidos.</param>
/// <param name="CasosNoArchivados">Cuantos quedan tras quitar los archivados.</param>
/// <param name="PersonasEnLaBase">Cuantas personas hay en total.</param>
/// <param name="CompanerosActivos">Cuantos companeros pueden recibir casos hoy.</param>
public sealed record DenominadoresDeInicio(
    int CasosEnLaBase,
    int CasosNoArchivados,
    int PersonasEnLaBase,
    int CompanerosActivos);

/// <summary>
/// Un caso tal como se ensena en una lista de Inicio o de la ventana de incompletos.
/// </summary>
/// <param name="CasoId">Numero interno, para poder abrirlo al pulsar.</param>
/// <param name="NumeroCaso">Las cuatro letras y cuatro digitos, o «sin numero» si no se leyo.</param>
/// <param name="Unidad">La unidad tal como se leyo, o «sin unidad leida».</param>
/// <param name="FechaViaje">La fecha en ISO-8601, o nula si no hay o no se entiende.</param>
/// <param name="DiasHastaElViaje">Cuantos dias faltan; negativo si ya paso; 0 si es hoy.</param>
/// <param name="EsVencido">Ya viajo y sigue sin «completa».</param>
/// <param name="SinFecha">No tiene fecha de viaje utilizable.</param>
/// <param name="CuantasPersonas">Cuantas personas van en ese documento.</param>
/// <param name="Estado">Lo que dijo el Excel del companero; sin marcar es lo normal.</param>
/// <param name="CuantoLeFalta">Cuantos datos le faltan al documento para poder asignarse.</param>
/// <param name="Dueno">Quien lo lleva ahora mismo, o vacio si no lo lleva nadie.</param>
public sealed record RenglonDeCaso(
    long CasoId,
    string NumeroCaso,
    string Unidad,
    string? FechaViaje,
    int DiasHastaElViaje,
    bool EsVencido,
    bool SinFecha,
    int CuantasPersonas,
    EstadoDeRecomendacion Estado,
    int CuantoLeFalta = 0,
    string Dueno = "")
{
    /// <summary>«hoy», «en 3 días», «hace 2 días» o «sin fecha»; lo que se pinta en la lista.</summary>
    public string CuandoViaja => SinFecha ? "sin fecha" : FechasEnEspanol.DecirLosDias(DiasHastaElViaje);

    /// <summary>«4 personas» o «1 persona», ya en singular o plural.</summary>
    public string CuantasPersonasTexto => Plural.Con(CuantasPersonas, "persona", "personas");

    /// <summary>
    /// Lo que se lee de este documento: una de las dos palabras, y detras su detalle.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Aqui habia DOS frases</b> y el dueno leia una creyendo la otra: <c>EstadoTexto</c>
    /// —«completa», «no completada» o «sin marcar», que es lo que dijo el Excel del companero—
    /// y <c>LoQueFaltaTexto</c> —«listo para asignar» o «le faltan 3 datos», que es lo que dice
    /// el programa mirando nuestros campos—. Por eso un renglon podia decir «listo para asignar
    /// · no completada» a la vez sin estar roto. Desde el 2026-09-07 son UNA lectura con dos
    /// palabras posibles, y las dos respuestas siguen dentro, en el detalle.
    /// </remarks>
    public LoQueSeLeeDeUnDocumento Lectura => LoQueSeLeeDeUnDocumento.De(
        Estado,
        archivado: false,
        CuantoLeFalta,
        sinNingunaPersonaLeida: CuantasPersonas == 0,
        quienLoLleva: Dueno,
        firma: string.Empty);

    /// <summary>«resuelto» o «me falta»; la unica palabra de estado de esta pantalla.</summary>
    public string PalabraDelEstado => Lectura.Palabra;

    /// <summary>Que falta y a quien le toca; se lee cuando el dueno lo pide.</summary>
    public string DetalleDelEstado => Lectura.Detalle;

    /// <summary>Quien lo lleva, o que no lo lleva nadie; nunca se deja en blanco.</summary>
    public string DuenoTexto => string.IsNullOrWhiteSpace(Dueno) ? "sin asignar" : Dueno;

    /// <summary>La unidad y el estado en un renglon, que es lo que cabe bajo el numero de caso.</summary>
    public string Detalle => $"{Unidad} · {PalabraDelEstado}";

    /// <summary>Lo que se lee bajo un documento listo: su unidad y cuantas personas van.</summary>
    /// <remarks>
    /// No lleva palabra de estado a proposito: esta lista ES la de lo que no le falta nada, y
    /// repetir «resuelto» en cada renglon de una lista que solo trae resueltos es ruido.
    /// </remarks>
    public string DetalleDeLoListo => $"{Unidad} · {CuantasPersonasTexto}";

    /// <summary>Lo que se lee bajo un documento asignado: quien lo lleva y como va.</summary>
    public string DetalleDeLoAsignado => $"{Unidad} · {DuenoTexto} · {PalabraDelEstado}";

    /// <summary>Lo que se lee bajo un documento incompleto: la palabra y quien lo lleva.</summary>
    public string DetalleDeLoIncompleto => $"{Unidad} · {PalabraDelEstado} · {DuenoTexto}";

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre el renglon entero.</summary>
    /// <remarks>
    /// Un renglon son cuatro trozos de texto sueltos; sin esto, el lector los lee uno a uno
    /// y quien no ve la pantalla no sabe que forman una sola cosa que ademas se puede pulsar.
    /// <para>Lleva el DETALLE entero aunque en pantalla este a un clic: quien no ve la pantalla
    /// no puede pulsar para enterarse de que le falta.</para>
    /// </remarks>
    public string ParaElLector
        => $"{NumeroCaso}, {Unidad}, {CuandoViaja}, {CuantasPersonasTexto}, {Lectura.ParaElLector}, "
           + $"{DuenoTexto}. Pulse para abrir este documento.";
}

/// <summary>
/// De qué color va la pastilla de un grupo en el calendario. Son TRES y no dos.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>El tercero nace el 2026-09-07 de un defecto que el dueño vio:</b> «0 de 0
/// confirmadas» pintado en ROJO. <c>PinturaDeInicio</c> tenía dos respuestas y decidía con una
/// condición que exige más de cero personas; con cero, el falso caía directo en el rojo.</para>
///
/// <para><b>Vive aquí y no en <c>PinturaDeInicio</c> a propósito.</b> Aquella clase crea
/// <c>SolidColorBrush</c>, que no se puede construir sin el tiempo de ejecución de XAML: tocar
/// el tipo desde una prueba lanza un <c>COMException</c> antes de llegar a la regla. Separando
/// la DECISIÓN del pincel, la decisión se mide en una prueba y el pincel se queda en ser un
/// color. Es la misma disciplina del ADR-0003 §8.1 que ya sostiene el resto de esta pantalla.</para>
/// </remarks>
public enum ColorDeLaPastilla
{
    /// <summary>No le queda ninguna persona por resolver: verde.</summary>
    Verde = 0,

    /// <summary>Le queda alguna, y esa es la que no podría entrar al templo: rojo.</summary>
    Rojo = 1,

    /// <summary>
    /// No se leyó a nadie de ese grupo: gris, ni la alarma ni el visto bueno.
    /// </summary>
    /// <remarks>
    /// El rojo de esta pantalla significa «alguien va a viajar sin la recomendación
    /// confirmada». Con cero personas leídas no va a viajar nadie, así que el rojo afirmaría un
    /// riesgo que no existe, y un rojo que salta cuando no pasa nada es un rojo que se deja de
    /// mirar. Verde tampoco: nadie ha dicho que esté resuelto, solo que no hay a quién
    /// preguntar. El gris es el único que dice «esto todavía no contesta esa pregunta».
    /// </remarks>
    Gris = 2,
}

/// <summary>De qué color va cada pastilla; la regla, sin un solo pincel dentro.</summary>
public static class ColoresDeLaPastilla
{
    /// <summary>El color de un grupo, a partir de sus dos cifras.</summary>
    /// <param name="cuantasResueltas">Cuantas de sus personas ya no le dejan nada que hacer.</param>
    /// <param name="cuantasPersonas">Cuantas personas trae el grupo.</param>
    public static ColorDeLaPastilla De(int cuantasResueltas, int cuantasPersonas)
    {
        if (cuantasPersonas <= 0) return ColorDeLaPastilla.Gris;
        return cuantasResueltas >= cuantasPersonas ? ColorDeLaPastilla.Verde : ColorDeLaPastilla.Rojo;
    }
}

/// <summary>
/// Una unidad que viaja un dia, dentro de una celda del calendario.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ha cambiado de significado y conviene saberlo:</b> hasta el 2026-09-05 una
/// pastilla era UN documento. Ahora es un GRUPO —una unidad de un dia— porque el dueno
/// dijo cual es su unidad de trabajo: <i>«asi puedo ver los grupos por fechas y saber con
/// que grupo trabajar»</i>. El tope de <see cref="CalendarioDelMes.PastillasPorDia"/> se
/// queda igual, y ahora acota aun mas: un dia con 98 documentos de dos unidades pinta dos
/// pastillas donde antes pintaba tres y un «+95 mas».
/// </remarks>
/// <param name="UnidadNumero">El numero de la unidad, o vacio si el papel no lo traia.</param>
/// <param name="UnidadNombre">El nombre de la unidad tal como se leyo.</param>
/// <param name="CuantosDocumentos">Cuantos documentos trae esa unidad ese dia.</param>
/// <param name="CuantasPersonas">Cuantas personas suman.</param>
/// <param name="CuantasCompletas">Cuantos de esos documentos estan completos.</param>
/// <param name="Motivo">El motivo que mas se repite entre los que no estan completos.</param>
/// <param name="CuantasPersonasConfirmadas">
/// Cuantas de esas personas tienen la recomendacion confirmada en el sistema del obispo.
/// </param>
/// <param name="CuantasPersonasResueltas">
/// Cuantas de esas personas ya no le dejan nada que hacer: las confirmadas MAS las que van en
/// un documento que el archivo.
/// </param>
public sealed record PastillaDeDia(
    string UnidadNumero,
    string UnidadNombre,
    int CuantosDocumentos,
    int CuantasPersonas,
    int CuantasCompletas,
    MotivoDeNoCompletar Motivo,
    int CuantasPersonasConfirmadas = 0,
    int CuantasPersonasResueltas = 0)
{
    /// <summary>El renglon de arriba de la pastilla: el numero de la unidad, o su nombre.</summary>
    public string Titulo
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(UnidadNumero)) return UnidadNumero;
            return string.IsNullOrWhiteSpace(UnidadNombre) ? "sin unidad" : UnidadNombre;
        }
    }

    /// <summary>
    /// El renglon de abajo: cuantas PERSONAS de cuantas tienen la recomendacion confirmada.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Cambio de unidad el 2026-09-05, y es el criterio C20-3.</b> Antes decia
    /// «8 pers. · 0/4», donde el 0 y el 4 eran DOCUMENTOS completos. Con dos documentos de
    /// cinco y una persona, un «1/2» no dice cuanta gente queda por verificar, que es lo
    /// unico que el dueno va a hacer con esa cifra: <i>«faltan 3, 4 o 5 personas que la
    /// recomendación no está confirmada»</i>.</para>
    /// <para>⛔ <b>La palabra ARCHIVADO no aparece aqui</b> (2026-09-06). El dueno la pidio el
    /// 2026-09-03 y la quito con su motivo: <i>«si se queda en el tablero y dice archivado, lo
    /// que hace es que me confunda»</i>. Desde el 2026-09-07 un archivado SI llega a esta
    /// pastilla —lo pidio con estas palabras: <i>«aunque se archive, debe quedarse en el
    /// calendario marcado en verde»</i>— y sigue sin llevar la etiqueta. Son dos cosas: verlo
    /// resuelto no le molesta; la etiqueta en medio del trabajo, si.</para>
    ///
    /// <para>⛔ <b>Y desde el 2026-09-07 dice una de DOS palabras y no «N de N confirmadas».</b>
    /// «Confirmadas» era una de las cuatro que el retiro. La cifra se queda —una cifra a la
    /// vista— pero detras de la palabra: «resuelto», o «me falta 6 de 10».</para>
    /// </remarks>
    public string Etiqueta => DosEstados.Cuenta(CuantasPersonasResueltas, CuantasPersonas);

    /// <summary>
    /// Si de esta unidad no se leyo ni una persona; entonces no hay a quien confirmar.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es el defecto que el dueno vio:</b> «0 de 0 confirmadas» pintado en ROJO. Cero de
    /// cero no puede ser rojo —el rojo dice «alguien va a viajar sin confirmar» y aqui no va
    /// nadie— y tampoco verde, porque nadie ha dicho que este resuelto. Su color lo decide
    /// <c>PinturaDeInicio.FondoDeLaPastilla</c>, que ahora tiene tres respuestas y no dos.
    /// </remarks>
    public bool SinNadieALaVista => CuantasPersonas == 0;

    /// <summary>De qué color va esta pastilla; la plantilla lo pinta, la regla vive aquí.</summary>
    /// <remarks>
    /// ⚠️ Mira <see cref="CuantasPersonasResueltas"/> y no
    /// <see cref="CuantasPersonasConfirmadas"/>: un documento archivado va en verde aunque sus
    /// seis preguntas no digan que si, porque lo cerro el. Es la peticion del 2026-09-07.
    /// </remarks>
    public ColorDeLaPastilla Color => ColoresDeLaPastilla.De(CuantasPersonasResueltas, CuantasPersonas);

    /// <summary>Lo que lee en voz alta un lector de pantalla; la pastilla es diminuta.</summary>
    /// <remarks>
    /// Aqui SI se dice «confirmada», y no es una contradiccion: el lector de pantalla lee la
    /// explicacion entera, que es el equivalente del detalle que en pantalla esta a un clic.
    /// Lo que el dueno retiro son las palabras de ESTADO que compiten en la pantalla, no la
    /// explicacion de que se esta contando.
    /// </remarks>
    public string ParaElLector
        => $"{Titulo} {UnidadNombre}, "
           + Plural.Con(CuantosDocumentos, "documento", "documentos") + ", "
           + Plural.Con(CuantasPersonas, "persona", "personas")
           + (SinNadieALaVista
               ? ", sin ninguna persona leída: no hay a quién confirmar"
               : $", {CuantasPersonasConfirmadas} con la recomendación "
                 + Plural.Palabra(CuantasPersonasConfirmadas, "confirmada", "confirmadas"))
           + $", {CuantasCompletas} " + Plural.Palabra(CuantasCompletas, "documento completo", "documentos completos");
}

/// <summary>Una celda del calendario del mes.</summary>
/// <param name="Fecha">El dia entero, que es lo que hace falta para poder abrir su grupo.</param>
/// <param name="Numero">El dia del mes, 1 a 31.</param>
/// <param name="EsDelMes">Si pertenece al mes que se ensena o es relleno de los bordes.</param>
/// <param name="EsHoy">Si es el dia de hoy segun el reloj del programa.</param>
/// <param name="EnLaVentana">Si cae dentro de los proximos 7 dias contando hoy.</param>
/// <param name="Pastillas">Los grupos que se ensenan en la celda, ya recortados.</param>
/// <param name="CuantasMas">Cuantos grupos de ese dia no cupieron.</param>
public sealed record DiaDelCalendario(
    DateOnly Fecha,
    int Numero,
    bool EsDelMes,
    bool EsHoy,
    bool EnLaVentana,
    IReadOnlyList<PastillaDeDia> Pastillas,
    int CuantasMas)
{
    /// <summary>«+7 más», o vacío si cupieron todos.</summary>
    public string TextoDeLasQueFaltan => CuantasMas == 0 ? string.Empty : $"+{CuantasMas} más";

    /// <summary>Si hay que ensenar el renglon de «+N más»; lo lee la plantilla.</summary>
    public bool HayMas => CuantasMas > 0;

    /// <summary>
    /// Si pulsar este dia lleva a alguna parte. Un dia sin grupos NO es pulsable y no da
    /// error: no hace nada y se ve que no hace nada (criterio C12-6).
    /// </summary>
    public bool SePuedePulsar => Pastillas.Count > 0;

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre la celda entera.</summary>
    public string ParaElLector
        => SePuedePulsar
            ? $"{FechasEnEspanol.DecirElDiaCompleto(Fecha)}, "
              + Plural.Con(Pastillas.Count + CuantasMas, "grupo", "grupos") + ". Pulse para abrirlo."
            : $"{FechasEnEspanol.DecirElDiaCompleto(Fecha)}, sin grupos.";
}

/// <summary>El mes entero del calendario, siempre de seis semanas para que no salte de alto.</summary>
/// <param name="Titulo">«septiembre de 2026», en espanol y en minusculas.</param>
/// <param name="Dias">Las 42 celdas, de lunes a domingo.</param>
public sealed record MesDelCalendario(string Titulo, IReadOnlyList<DiaDelCalendario> Dias);

/// <summary>Un renglon del cuadro «Asignado a los agentes».</summary>
/// <param name="Id">Id del companero; 0 es la fila de los casos que no lleva nadie.</param>
/// <param name="Nombre">Su nombre, o «Sin asignar».</param>
/// <param name="Casos">Cuantos casos lleva vivos.</param>
/// <param name="SinDevolver">De esos, cuantos no ha devuelto todavia.</param>
public sealed record RenglonDeCompanero(long Id, string Nombre, int Casos, int SinDevolver)
{
    /// <summary>«8 doc.», que es lo que cabe en la columna del mockup.</summary>
    public string CasosTexto => $"{Casos} doc.";

    /// <summary>«3 sin volver», o una raya si no debe ninguna.</summary>
    public string SinDevolverTexto => SinDevolver == 0 ? "—" : $"{SinDevolver} sin volver";
}

/// <summary>
/// Todo lo que la pantalla de Inicio necesita para pintarse, leido de una vez.
/// </summary>
/// <remarks>
/// Vive aqui, y no dentro de la pagina, porque asi se puede probar SIN ABRIR VENTANA
/// (ADR-0003 §8.1). Si para comprobar «cuantos documentos estan listos para asignar»
/// hubiera que montar una ventana, la regla estaria en el sitio equivocado.
/// </remarks>
/// <param name="Contadores">Las dos cifras que el dueno pidio ver, y ninguna mas.</param>
/// <param name="Denominadores">Contra que se comparan esas cifras.</param>
/// <param name="Listos">Lo que esta listo para asignar, lo que viaja antes arriba.</param>
/// <param name="Asignados">Lo que ahora mismo llevan los companeros.</param>
/// <param name="Equipo">Cuanto lleva cada companero, con «Sin asignar» al final.</param>
/// <param name="Mes">El calendario del mes que se ensena; es por donde se entra al trabajo.</param>
/// <param name="Avisos">Lo que hay que decir en una linea; nunca detiene el pintado.</param>
/// <param name="ElSistemaDelObispo">
/// Lo que hay que verificar en el sistema de la Iglesia, contado en PERSONAS. Es la tercera
/// cosa de Inicio y no toca las dos anteriores (C20-2).
/// </param>
/// <param name="Cuadro">
/// Las dos cifras que quedan en la pantalla de Inicio desde el 2026-09-07. Lo demas de este
/// resumen lo pinta la pestana del flujo, que lee por este mismo camino para que las cifras
/// del cuadro y las listas no puedan decir cosas distintas.
/// </param>
public sealed record ResumenDeInicio(
    ContadoresDeInicio Contadores,
    DenominadoresDeInicio Denominadores,
    IReadOnlyList<RenglonDeCaso> Listos,
    IReadOnlyList<RenglonDeCaso> Asignados,
    IReadOnlyList<RenglonDeCompanero> Equipo,
    MesDelCalendario Mes,
    IReadOnlyList<Aviso> Avisos,
    LoDelSistemaDelObispo ElSistemaDelObispo,
    CuadroDeInicio Cuadro)
{
    /// <summary>La linea del denominador que exige el C1-1, ya escrita para la cabecera.</summary>
    /// <remarks>
    /// ⚠️ 2026-09-06. Decia «12 casos en la base · 9 sin archivar». Las dos cifras juntas
    /// obligan al dueno a restar para saber cuantos archivo, y eso es exactamente lo que el
    /// pidio quitar: «si se queda en el tablero y dice archivado, lo que hace es que me
    /// confunda; debe pasar a archivado y no aparecer mas en ningun lado». El denominador se
    /// queda —sin el, la cifra de arriba no se puede comprobar— pero cuenta lo que se trabaja
    /// y no nombra el archivo. Donde se comprueba cuantos hay archivados es en Revisar, con
    /// «Ver los archivados», que es el unico sitio donde el pidio verlos a proposito.
    /// </remarks>
    public string LineaDelDenominador =>
        Plural.Con(Denominadores.CasosNoArchivados, "caso", "casos") + " · "
        + Plural.Con(Denominadores.PersonasEnLaBase, "persona", "personas") + " · "
        + Plural.Con(Denominadores.CompanerosActivos, "compañero activo", "compañeros activos");

    /// <summary>«12 documentos · 41 personas», la linea bajo la cifra de lo listo.</summary>
    public string DeQueVaLoListo
        => Plural.Con(Listos.Count, "documento", "documentos")
           + " · " + Plural.Con(Contadores.PersonasListas, "persona", "personas");

    /// <summary>«30 documentos · 12 sin devolver», la linea bajo la cifra de lo asignado.</summary>
    public string DeQueVaLoAsignado
        => Plural.Con(Asignados.Count, "documento", "documentos")
           + $" · {Contadores.SinDevolver} sin devolver";
}
