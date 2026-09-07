using Fichas.App.Grupo;
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

    /// <summary>Lo que dice el estado en la pantalla, en espanol y sin abreviar.</summary>
    public string EstadoTexto => Estado switch
    {
        EstadoDeRecomendacion.Completa => "completa",
        EstadoDeRecomendacion.NoCompleta => "no completada",
        _ => "sin marcar",
    };

    /// <summary>«listo para asignar» o «le faltan 3 datos»; es la lectura, nunca una firma.</summary>
    /// <remarks>
    /// La frase sale de <see cref="Fichas.App.Grupo.LasDosPreguntas"/> y no de un literal
    /// aqui: escrita en dos sitios, el dia que se cambie una quedan dos redacciones para la
    /// misma cosa, que es como empieza la confusion que esta fase viene a quitar.
    /// </remarks>
    public string LoQueFaltaTexto => CuantoLeFalta == 0
        ? Fichas.App.Grupo.LasDosPreguntas.ListoParaAsignar
        : "le " + Plural.Palabra(CuantoLeFalta, "falta", "faltan") + " " + Plural.Con(CuantoLeFalta, "dato", "datos");

    /// <summary>Quien lo lleva, o que no lo lleva nadie; nunca se deja en blanco.</summary>
    public string DuenoTexto => string.IsNullOrWhiteSpace(Dueno) ? "sin asignar" : Dueno;

    /// <summary>La unidad y el estado en un renglon, que es lo que cabe bajo el numero de caso.</summary>
    public string Detalle => $"{Unidad} · {EstadoTexto}";

    /// <summary>Lo que se lee bajo un documento listo: su unidad y cuantas personas van.</summary>
    public string DetalleDeLoListo => $"{Unidad} · {CuantasPersonasTexto}";

    /// <summary>Lo que se lee bajo un documento asignado: quien lo lleva y como va.</summary>
    public string DetalleDeLoAsignado => $"{Unidad} · {DuenoTexto} · {EstadoTexto}";

    /// <summary>Lo que se lee bajo un documento incompleto: por que lo esta.</summary>
    public string DetalleDeLoIncompleto => $"{Unidad} · {LoQueFaltaTexto} · {EstadoTexto}";

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre el renglon entero.</summary>
    /// <remarks>
    /// Un renglon son cuatro trozos de texto sueltos; sin esto, el lector los lee uno a uno
    /// y quien no ve la pantalla no sabe que forman una sola cosa que ademas se puede pulsar.
    /// </remarks>
    public string ParaElLector
        => $"{NumeroCaso}, {Unidad}, {CuandoViaja}, {CuantasPersonasTexto}, {EstadoTexto}, "
           + $"{DuenoTexto}. Pulse para abrir este documento.";
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
public sealed record PastillaDeDia(
    string UnidadNumero,
    string UnidadNombre,
    int CuantosDocumentos,
    int CuantasPersonas,
    int CuantasCompletas,
    MotivoDeNoCompletar Motivo,
    int CuantasPersonasConfirmadas = 0)
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
    /// <para>⛔ <b>La palabra ARCHIVADO ya no aparece aqui</b> (2026-09-06). El dueno la
    /// pidio el 2026-09-03 y la quito con su motivo: <i>«si se queda en el tablero y dice
    /// archivado, lo que hace es que me confunda»</i>. Un archivado no llega a esta pastilla,
    /// asi que la cuenta habla solo de lo que sigue abierto.</para>
    /// </remarks>
    public string Etiqueta => $"{CuantasPersonasConfirmadas} de {CuantasPersonas} confirmadas";

    /// <summary>Lo que lee en voz alta un lector de pantalla; la pastilla es diminuta.</summary>
    public string ParaElLector
        => $"{Titulo} {UnidadNombre}, "
           + Plural.Con(CuantosDocumentos, "documento", "documentos") + ", "
           + Plural.Con(CuantasPersonas, "persona", "personas")
           + $", {CuantasPersonasConfirmadas} con la recomendación "
           + Plural.Palabra(CuantasPersonasConfirmadas, "confirmada", "confirmadas")
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
public sealed record ResumenDeInicio(
    ContadoresDeInicio Contadores,
    DenominadoresDeInicio Denominadores,
    IReadOnlyList<RenglonDeCaso> Listos,
    IReadOnlyList<RenglonDeCaso> Asignados,
    IReadOnlyList<RenglonDeCompanero> Equipo,
    MesDelCalendario Mes,
    IReadOnlyList<Aviso> Avisos,
    LoDelSistemaDelObispo ElSistemaDelObispo)
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
