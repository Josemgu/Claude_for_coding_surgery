using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Paquetes;

/// <summary>
/// El Excel que va al companero y el que vuelve firmado por el, escrito de verdad en disco.
/// </summary>
/// <remarks>
/// La regla del dueno (CLAUDE.md, regla permanente 5 precisada el 2026-09-03): el Excel que
/// devuelve el companero es el que escribe <c>completa</c> / <c>no_completa</c>, con SU
/// nombre. Eso NO es firmar campos: la firma por campo sigue siendo de Miguel y este
/// servicio no la toca nunca.
/// <para>
/// <b>Como se identifica una fila que vuelve, y en que orden.</b> La clave del Excel es
/// <c>CASO:MRN:ID</c>, y el id del caso es la tercera parte justamente porque desde la version
/// 12 del esquema dos casos pueden llevar el MISMO numero: son cuatro letras mas el ano y el
/// mes, o sea una unidad y un mes, no una familia. <see cref="LeerExcelDevuelto"/> lee la clave
/// entera y deja el id en <see cref="MarcaDelCompanero.CasoId"/>; <see cref="AplicarMarcas"/>
/// lo usa como PRIMER criterio. Si el id viene, manda el id.
/// </para>
/// <para>
/// Con el id nulo —una hoja generada antes del 2026-09-03— se resuelve por el camino viejo,
/// <c>numero_caso</c> + <c>mrn</c>, y ese par puede quedar ambiguo. Entonces la fila se
/// DESCARTA con su motivo escrito: escribir el trabajo del companero sobre la familia
/// equivocada es peor que no escribirlo, y un descarte al menos deja su renglon.
/// </para>
/// </remarks>
public sealed class Paquetes : IPaquetes
{
    /// <summary>Cuántos casos se piden por página al buscar por número; <see cref="CasosConEseNumero"/> recorre las que hagan falta.</summary>
    private const int TamanoDelTrozo = 500;

    /// <summary>Para leer casos por id, buscarlos por número y estampar el estado del compañero.</summary>
    private readonly ICasos _casos;

    /// <summary>Para las personas de cada caso y para anotar en ellas la propuesta del compañero.</summary>
    private readonly IPersonas _personas;

    /// <summary>Para comprobar que el compañero existe y poner su nombre en la hoja.</summary>
    private readonly ICompaneros _companeros;

    /// <summary>Donde se anotan las filas del Excel devuelto que no casaron con nadie.</summary>
    private readonly IIlegibles _ilegibles;

    /// <summary>La hora que se estampa en los descartes y en el estado; inyectada para poder probarla.</summary>
    private readonly IReloj _reloj;

    /// <summary>Se ata a los puertos que necesita; ninguno se inventa dentro.</summary>
    /// <param name="casos">El puerto de casos.</param>
    /// <param name="personas">El puerto de personas.</param>
    /// <param name="companeros">El puerto de compañeros.</param>
    /// <param name="ilegibles">El puerto donde se registran las filas descartadas.</param>
    /// <param name="reloj">El reloj del programa.</param>
    public Paquetes(ICasos casos, IPersonas personas, ICompaneros companeros, IIlegibles ilegibles, IReloj reloj)
    {
        _casos = casos;
        _personas = personas;
        _companeros = companeros;
        _ilegibles = ilegibles;
        _reloj = reloj;
    }

    // ─────────────────────────────── la ida ───────────────────────────────

    /// <summary>Genera el Excel de ida de un companero con los casos que lleva.</summary>
    /// <remarks>
    /// No se escribe nada si el compañero no existe o si ningún caso tiene personas; con el
    /// archivo de destino abierto en Excel se devuelve el problema en español y el archivo
    /// anterior queda intacto. No toca la base.
    /// </remarks>
    /// <param name="companeroId">El número interno del compañero al que va el paquete.</param>
    /// <param name="casoIds">Los casos que lleva, en el orden en que saldrán sus renglones.</param>
    /// <param name="rutaDestino">Ruta del <c>.xlsx</c> a escribir; se sobrescribe si ya existe.</param>
    /// <returns>Si se escribió, el id del compañero y los avisos, empezando por el que resume el paquete.</returns>
    public ResultadoDeEscritura GenerarExcelDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
    {
        var companero = _companeros.Obtener(companeroId);
        if (companero is null)
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"No hay ningún compañero con el número interno {companeroId}.",
                string.Empty,
                "Un paquete sin compañero no se puede entregar ni se puede reconciliar al volver."));

        var avisos = new List<Aviso>();
        var filas = ArmarLasFilas(casoIds, avisos);
        if (filas.Count == 0)
        {
            avisos.Add(Aviso.Problema(
                $"El compañero «{companero.Nombre}» no tiene ninguna persona en los casos pedidos.",
                string.Empty,
                "No hay paquete que generar. Asígnele casos con personas antes."));
            return new ResultadoDeEscritura(false, 0, avisos);
        }

        avisos.AddRange(AvisosDeQuienNoTieneMrn(filas));

        try
        {
            using var libro = LibroDeTrabajo.Construir(filas, companero.Nombre);
            libro.SaveAs(rutaDestino);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
        {
            // C6-7: Excel abierto y bloqueado. Se avisa en espanol y no se cae; el .xlsx que ya
            // estuviera ahi se queda intacto porque no se llego a escribir nada.
            avisos.Add(Aviso.Problema(
                $"No se pudo escribir «{Path.GetFileName(rutaDestino)}» porque otro programa lo tiene abierto.",
                string.Empty,
                $"Casi siempre es el propio Excel. Ciérrelo y vuelva a generar el paquete. "
                + $"El archivo anterior NO se ha tocado. El sistema dijo: {causa.Message}"));
            return new ResultadoDeEscritura(false, 0, avisos);
        }

        avisos.Insert(0, Aviso.Informa(
            $"Paquete de «{companero.Nombre}»: {filas.Count} persona(s) en {casoIds.Count} caso(s).",
            string.Empty,
            $"Escrito en «{rutaDestino}»."));
        return new ResultadoDeEscritura(true, companeroId, avisos);
    }

    /// <summary>
    /// Junta en UN solo PDF las hojas de los documentos del paquete, en el orden del Excel.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno: <i>«el Excel normal y tambien un PDF de todos, asignado, en un
    /// solo PDF»</i>. Antes de esto el companero recibia la hoja de calculo y tenia que ir
    /// abriendo los escaneos uno a uno.</para>
    ///
    /// <para><b>El orden es el del Excel y esa es la mitad del trabajo.</b> Se recorren los
    /// mismos <paramref name="casoIds"/> en el mismo orden que <see cref="ArmarLasFilas"/>, asi
    /// que la primera hoja del PDF es la del primer renglon de la hoja de calculo. Un PDF con
    /// las hojas correctas en otro orden no sirve: el companero no sabria a que fila mirar.</para>
    ///
    /// <para><b>Una hoja por CASO, no por persona.</b> El documento es uno aunque viajen cuatro
    /// hermanos: el Excel lleva cuatro renglones y el PDF una sola hoja. Y un caso sin ninguna
    /// persona no lleva hoja, porque tampoco tiene renglon — meterla descolocaria la
    /// correspondencia de todo lo que va debajo.</para>
    ///
    /// <para>⚠️ Este metodo NO escribe nada en la base: solo lee <c>ruta_pdf</c> y
    /// <c>pagina_pdf</c>. La regla permanente 5 sigue intacta.</para>
    ///
    /// <para><b>Un documento que no se pueda leer NO deja un hueco.</b> En su sitio va una hoja
    /// que dice cual falta, quien viaja en el y por que no esta (<see cref="HojaDeAviso"/>), asi
    /// que salen tantas hojas como renglones tiene el Excel y la correspondencia no se mueve.
    /// Decidido por el dueno el 2026-09-05: <i>«Sí, mete la hoja de aviso en el hueco»</i>.
    /// Antes de eso se quedaba fuera, y estaba medido lo que costaba: con los siete escaneos
    /// del dueno y dos documentos rotos a proposito, de 7 casos salian 5 hojas y solo las 2
    /// anteriores al primer hueco seguian cuadrando.</para>
    /// </remarks>
    /// <param name="companeroId">El número interno del compañero al que va el paquete.</param>
    /// <param name="casoIds">Los mismos casos y en el mismo orden que se pasaron al Excel.</param>
    /// <param name="rutaDestino">Ruta del <c>.pdf</c> a escribir.</param>
    /// <returns>Si se escribió, el id del compañero y los avisos: uno por hoja de aviso, y el resumen delante.</returns>
    public ResultadoDeEscritura GenerarPdfDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
    {
        ArgumentNullException.ThrowIfNull(casoIds);

        var companero = _companeros.Obtener(companeroId);
        if (companero is null)
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"No hay ningún compañero con el número interno {companeroId}.",
                string.Empty,
                "Un PDF de documentos sin saber de quién es el paquete no se puede entregar."));

        var hojas = HojasDeLosCasos(casoIds);
        if (hojas.Count == 0)
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"El paquete de «{companero.Nombre}» no lleva ningún documento que juntar en un PDF.",
                string.Empty,
                "Ninguno de los casos pedidos tiene personas dentro, así que tampoco tiene renglón "
                + "en el Excel. El Excel sale igual."));

        var union = PdfDelPaquete.Unir(hojas, rutaDestino);
        var avisos = new List<Aviso>();

        foreach (var falta in union.Faltan)
            avisos.Add(Aviso.Advierte(falta.Motivo, string.Empty, falta.Detalle));

        if (!union.SeEscribio)
        {
            avisos.Insert(0, Aviso.Problema(
                $"No se pudo hacer el PDF de los documentos de «{companero.Nombre}».",
                string.Empty,
                $"{union.Fallo} El Excel del paquete NO depende de esto y sale igual."));
            return new ResultadoDeEscritura(false, 0, avisos);
        }

        avisos.Insert(0, Aviso.Informa(
            $"PDF de los documentos de «{companero.Nombre}»: {union.Hojas} hoja(s)"
            + (union.Faltan.Count == 0
                ? ", ninguna se quedó fuera."
                : $", {union.Faltan.Count} con hoja de aviso en vez del documento."),
            string.Empty,
            $"Escrito en «{rutaDestino}». Las hojas van en el mismo orden que los renglones del Excel."));
        return new ResultadoDeEscritura(true, companeroId, avisos);
    }

    /// <summary>
    /// La hoja que aporta cada caso al PDF, en el orden en que salen sus renglones en el Excel.
    /// </summary>
    /// <remarks>
    /// Un caso entra si y solo si tiene al menos una persona, que es exactamente la condicion
    /// con la que <see cref="ArmarLasFilas"/> le da renglon. Las dos listas se recorren igual y
    /// eso es lo que mantiene la correspondencia; si alguna vez dejaran de hacerlo, el PDF
    /// seguiria teniendo las hojas correctas en el orden equivocado, que es el fallo mas dificil
    /// de ver de los dos.
    /// </remarks>
    /// <param name="casoIds">Los casos del paquete, en el orden del Excel; los que ya no existen se saltan.</param>
    private List<HojaDelPaquete> HojasDeLosCasos(IReadOnlyList<long> casoIds)
    {
        var hojas = new List<HojaDelPaquete>();
        foreach (var casoId in casoIds)
        {
            var caso = _casos.Obtener(casoId);
            if (caso is null) continue;

            var gente = _personas.DeCaso(casoId);
            if (gente.Count == 0) continue;

            var comoSeLlama = string.IsNullOrWhiteSpace(caso.NumeroCaso)
                ? $"sin número (interno {caso.Id})"
                : caso.NumeroCaso;

            // Sin archivo o sin hoja NO se salta en silencio: se mete con una ruta vacia para
            // que la union le ponga en su sitio una hoja de aviso con el nombre del caso. Un
            // caso que desaparece sin decir nada es el que nadie echa de menos.
            //
            // Los nombres van SIEMPRE aunque casi nunca se usen: solo se escriben cuando la
            // hoja falla, y entonces son lo que le dice al companero a que renglon del Excel
            // esta mirando. Leerlos aqui no cuesta una consulta mas —la lista ya esta pedida
            // para saber si el caso lleva gente— y pedirlos despues obligaria a que el que
            // junta los PDF supiera lo que es una persona.
            hojas.Add(new HojaDelPaquete(
                comoSeLlama,
                caso.RutaPdf ?? string.Empty,
                caso.PaginaPdf ?? 0,
                [.. gente.Select(persona => persona.Nombre ?? string.Empty)]));
        }
        return hojas;
    }

    /// <summary>Una fila por persona de esos casos, con lo que la hoja «Por verificar» pide.</summary>
    /// <remarks>
    /// Los seis pasos y la llamada al lider salen VACIOS aunque la persona ya traiga una
    /// propuesta de una ronda anterior. Es a proposito: lo que se le manda a un companero es
    /// lo que tiene que mirar, no lo que otro contesto. Rellenarlo de antemano invita a
    /// confirmarlo sin comprobarlo, que es la averia contra la que existe la regla permanente 5.
    /// </remarks>
    /// <param name="casoIds">Los casos del paquete, en el orden en que saldrán.</param>
    /// <param name="avisos">La lista a la que se añade un aviso por cada caso que ya no está en la base.</param>
    private List<FilaDeTrabajo> ArmarLasFilas(IReadOnlyList<long> casoIds, List<Aviso> avisos)
    {
        var filas = new List<FilaDeTrabajo>();
        foreach (var casoId in casoIds)
        {
            var caso = _casos.Obtener(casoId);
            if (caso is null)
            {
                avisos.Add(Aviso.Advierte(
                    $"El caso con el número interno {casoId} ya no está en la base y no va en el paquete.",
                    string.Empty,
                    "Puede que se borrara entre la asignación y la generación. El resto del paquete sale igual."));
                continue;
            }
            foreach (var persona in _personas.DeCaso(casoId))
            {
                filas.Add(new FilaDeTrabajo
                {
                    NumeroCaso = caso.NumeroCaso,
                    // Aqui se ponian a nulo `fecha_solicitud` y `estaca`, que la base no guarda.
                    // El dueno las quito de la hoja el 2026-09-06: ya no hay nada que poner.
                    FechaViaje = caso.FechaViaje,
                    Templo = caso.TemploNombre,
                    // ⚠️ 2026-09-07: los dos datos de la unidad van SEPARADOS, cada uno en su
                    // columna, porque el dueno lo pidio asi: «el numero de unidad en un lado y
                    // al otro el nombre de la unidad». Antes se pegaban en una sola celda.
                    // Cada uno sale como lo guarda la base y ninguno se retoca.
                    UnidadNumero = caso.UnidadNumero,
                    UnidadNombre = caso.UnidadNombre,
                    Nombre = persona.Nombre,
                    Mrn = persona.Mrn,
                    AQueVa = ResumirOrdenanzas(persona),
                    // El id del caso va DENTRO de la clave: es lo unico que no se repite.
                    Clave = Columnas.ArmarLaClave(caso.NumeroCaso, persona.Mrn, caso.Id),
                });
            }
        }
        return filas;
    }

    // ⛔ Aqui vivia `UnidadConSuNumero`, que pegaba el numero detras del nombre —«Cuatricentenaria
    // (7000014)»— cuidando de no repetirlo cuando el escaneo ya lo traia dentro. El dueno pidio
    // el 2026-09-07 los dos datos en dos columnas, asi que ya no hay nada que pegar y se quedo
    // sin ningun sitio desde donde llamarla. Lo que hacia queda escrito aqui por si algun dia se
    // quiere volver a juntar los dos en una sola celda.

    /// <summary>A que va la persona al templo, leido de las seis casillas del formulario.</summary>
    /// <remarks>Una casilla sin marcar no dice «no»: dice que nadie la leyo, y por eso no aparece.</remarks>
    /// <param name="persona">La persona con sus seis casillas <c>Ord*</c>.</param>
    /// <returns>Las marcadas, en el orden del formulario y separadas por coma; nulo si no hay ninguna.</returns>
    private static string? ResumirOrdenanzas(Persona persona)
    {
        var partes = new List<string>();
        if (persona.OrdRecibirPropias == true) partes.Add("Ordenanzas propias");
        if (persona.OrdObservarSellamiento == true) partes.Add("Observar sellamiento");
        if (persona.OrdTraductor == true) partes.Add("Traductor");
        if (persona.OrdInvestidura == true) partes.Add("Investidura");
        if (persona.OrdSellamientoEsposos == true) partes.Add("Sellamiento esposa a esposo");
        if (persona.OrdSellamientoHijoPadres == true) partes.Add("Sellamiento hijo a padres");
        return partes.Count > 0 ? string.Join(", ", partes) : null;
    }

    /// <summary>
    /// Nombra a quien va en el paquete sin MRN, o nada si no hay nadie (criterio C6-5).
    /// </summary>
    /// <remarks>
    /// La frase nombra a cada uno, y eso es la mitad de lo que sirve: «hay 1 persona sin MRN»
    /// obliga a abrir el Excel a buscarla, y entonces no se busca. El compañero hace el
    /// trabajo, lo devuelve, y al reconciliar se descarta — generar es el único momento en que
    /// todavía se puede arreglar sin gastar el trabajo de nadie.
    /// </remarks>
    /// <param name="filas">Las filas ya armadas del paquete.</param>
    /// <returns>Ningún aviso, o uno solo que nombra a todos los que van sin MRN.</returns>
    private static IEnumerable<Aviso> AvisosDeQuienNoTieneMrn(IReadOnlyList<FilaDeTrabajo> filas)
    {
        var sinMrn = filas
            .Where(fila => string.IsNullOrWhiteSpace(fila.Mrn))
            .Select(fila => fila.Nombre ?? "— sin nombre leído —")
            .ToList();
        if (sinMrn.Count == 0)
            yield break;

        var varias = sinMrn.Count != 1;
        yield return Aviso.Advierte(
            $"{sinMrn.Count} persona{(varias ? "s" : "")} de este paquete {(varias ? "van" : "va")} SIN cédula de miembro: {string.Join(", ", sinMrn)}.",
            string.Empty,
            "El compañero puede contestar por " + (varias ? "ellas" : "ella") + ", pero al volver el Excel esas "
            + "filas NO se pueden emparejar —la vuelta casa por «número de caso + MRN + id» y NUNCA por "
            + "nombre— y se descartan enteras: se pierden los siete pasos que el compañero haya contestado. "
            + "Tecléele la cédula en la pantalla de corrección y vuelva a generar el paquete.");
    }

    // ────────────────────────────── la vuelta ──────────────────────────────

    /// <summary>Lee el Excel devuelto y devuelve lo que caso, lo que no y lo que hay que decir.</summary>
    /// <remarks>
    /// ⚠️ Las filas descartadas se ANOTAN aqui, y es el unico sitio donde pueden anotarse:
    /// <see cref="AplicarMarcas"/> no las recibe. Que la fila no entre esta bien decidido
    /// —casar por nombre crea registros fantasma— pero si la lista se pierde al cerrar la
    /// ventana se pierde con ella la unica pista de que un companero hizo un trabajo que nadie
    /// recogio (criterio C6-4).
    /// <para>
    /// Consecuencia dicha, la misma que en el programa en Python: leer dos veces el mismo
    /// archivo deja los renglones de descarte otra vez, con su fecha y su hora. Es lo correcto:
    /// son dos cargas distintas del mismo archivo y las dos pasaron.
    /// </para>
    /// </remarks>
    /// <param name="rutaExcel">Ruta del <c>.xlsx</c> que devolvió el compañero.</param>
    /// <param name="companeroId">El número interno del compañero que lo devuelve; se guarda con cada marca y cada descarte.</param>
    /// <returns>Las marcas listas para <see cref="AplicarMarcas"/>, las filas descartadas ya anotadas, y los avisos; con un error de lectura, todo vacío y un solo problema.</returns>
    public ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId)
    {
        if (_companeros.Obtener(companeroId) is null)
            return new ResultadoDelExcelDevuelto([], [], [Aviso.Problema(
                $"No hay ningún compañero con el número interno {companeroId}.",
                string.Empty,
                "No se puede cargar un Excel sin decir de qué compañero viene: la propuesta se guarda con su nombre.")]);

        LibroLeido libro;
        try
        {
            libro = LectorDeExcel.Leer(rutaExcel);
        }
        catch (ErrorDeLectura causa)
        {
            // No se silencia: se convierte en el aviso que se pinta en la franja (requisito 9).
            return new ResultadoDelExcelDevuelto([], [], [Aviso.Problema("No se pudo leer el Excel devuelto.", string.Empty, causa.Message)]);
        }

        var resultado = Reconciliacion.Reconciliar(
            libro, PersonasQueCasan, companeroId, rutaExcel, _reloj.Ahora());

        foreach (var descartada in resultado.Descartadas)
            _ilegibles.RegistrarDescartada(descartada);

        var avisos = new List<Aviso>(libro.Avisos);
        avisos.AddRange(resultado.Avisos);
        if (resultado.SinNadaQueProponer.Count > 0)
        {
            avisos.Add(Aviso.Informa(
                $"{resultado.SinNadaQueProponer.Count} fila(s) volvieron con las siete casillas en blanco.",
                string.Empty,
                "Nadie las miró, que no es lo mismo que un «No». Filas: " + string.Join(", ", resultado.SinNadaQueProponer) + "."));
        }

        var marcas = ArmarLasMarcas(resultado.Renglones, avisos);
        return new ResultadoDelExcelDevuelto(marcas, resultado.Descartadas, avisos);
    }

    /// <summary>
    /// Convierte los renglones que casaron en marcas, con el estado del documento ya decidido.
    /// </summary>
    /// <remarks>
    /// El estado es del DOCUMENTO y no de la persona, asi que se decide por caso y se estampa
    /// en todas sus filas. La regla es la de los pasos subida de la persona al documento, con
    /// los mismos tres valores: si alguna persona trae algun paso en «No», el documento no
    /// esta completo; si TODAS traen los seis en «Sí», esta completo; en cualquier otro caso
    /// NO SE SABE — y no se sabe no es «no».
    /// <para>
    /// «Todas» son todas las personas del caso EN LA BASE, no solo las que volvieron en la
    /// hoja: una persona del caso que no volvio deja el documento sin marcar, que es lo
    /// correcto. Dar por completo un documento del que falta gente es exactamente lo que manda
    /// a alguien al templo con la recomendacion mal.
    /// </para>
    /// </remarks>
    /// <param name="renglones">Los renglones que casaron con una persona, tal como los dio <see cref="Reconciliacion"/>.</param>
    /// <param name="avisos">La lista a la que se añaden los avisos de motivos repetidos y de contradicciones.</param>
    /// <returns>Una marca por renglón, con el estado del documento ya calculado y repetido en cada fila del caso.</returns>
    private List<MarcaDelCompanero> ArmarLasMarcas(
        IReadOnlyList<RenglonDeLaVuelta> renglones, List<Aviso> avisos)
    {
        var estadoPorCaso = new Dictionary<long, EstadoDeRecomendacion>();
        var motivoPorCaso = MotivoPorCaso(renglones, avisos);
        foreach (var casoId in renglones.Select(r => r.CasoId).Distinct())
        {
            var estado = EstadoDelDocumento(casoId, renglones);
            var motivo = motivoPorCaso.GetValueOrDefault(casoId, MotivoDeNoCompletar.SinMotivo);
            AvisarDeLaContradiccion(casoId, estado, motivo, renglones, avisos);
            estadoPorCaso[casoId] = EstadoConElMotivo(estado, motivo);
        }

        return [.. renglones.Select(renglon => new MarcaDelCompanero(
            renglon.NumeroCaso,
            renglon.Mrn,
            renglon.Nombre,
            estadoPorCaso[renglon.CasoId],
            EstadoPropuestoDe(renglon.Respuestas),
            renglon.Comentario,
            renglon.Respuestas["paso_preparacion"],
            renglon.Respuestas["paso_informacion"],
            renglon.Respuestas["paso_cita_del_templo"],
            renglon.Respuestas["paso_acciones_requeridas"],
            renglon.Respuestas["paso_entrevistas"],
            renglon.Respuestas["paso_listo_para_el_templo"],
            renglon.Respuestas[Pasos.ColumnaDeLaLlamada],
            renglon.FilaExcel,
            // El id del caso viaja hasta la marca desde el 2026-09-04. Aqui SIEMPRE se sabe
            // —la clave del Excel es CASO:MRN:ID y la fila ya resolvio a una persona—, y sin
            // el, aplicar tenia que adivinarlo otra vez por el par numero_caso + mrn.
            renglon.CasoId,
            renglon.Motivo))];
    }

    /// <summary>
    /// Un motivo dicho es una respuesta: quien dice POR QUE no se completo esta diciendo
    /// que no esta completa.
    /// </summary>
    /// <remarks>
    /// Solo sube de «sin marcar» a «no completa», nunca al reves. Y hace falta: con el
    /// estado en «sin marcar» el calendario lee «sin marcar» y NO ensena ningun motivo
    /// (<c>PalabrasDelEstado.Decir</c>), asi que el caso entero del dueno —«no se pudo
    /// comunicar con el lider», con las siete casillas en blanco porque no hubo nada que
    /// mirar— quedaria invisible justo en la pantalla donde lo pidio.
    /// <para>
    /// ⚠️ Es lo unico de este pase que el programa DEDUCE en vez de copiar. Se sostiene en
    /// el rotulo de la columna, que pregunta «¿por que no se completo?»: elegir una opcion
    /// ahi es afirmar que no lo esta. Si el dueno lo quiere de otra manera, se cambia esta
    /// funcion y nada mas.
    /// </para>
    /// </remarks>
    /// <param name="estado">El estado que dicen los pasos.</param>
    /// <param name="motivo">El motivo que eligió el compañero, o sin motivo.</param>
    /// <returns>«No completa» si estaba sin marcar y hay motivo; en cualquier otro caso, el mismo estado.</returns>
    private static EstadoDeRecomendacion EstadoConElMotivo(
        EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo)
        => estado == EstadoDeRecomendacion.SinMarcar && motivo != MotivoDeNoCompletar.SinMotivo
            ? EstadoDeRecomendacion.NoCompleta
            : estado;

    /// <summary>
    /// El motivo de cada caso: el de su PRIMERA fila que lo diga, con aviso si otra dice otro.
    /// </summary>
    /// <remarks>
    /// La columna del caso admite un solo motivo y la hoja tiene una fila por persona, asi
    /// que dos personas del mismo documento pueden traer motivos distintos. Se escribe el
    /// de la primera fila —es lo unico que no depende de en que orden se recorra— y el otro
    /// NO se pierde en silencio: se dice con su fila. El texto entero de cada uno sigue
    /// entero en el comentario de SU persona.
    /// </remarks>
    /// <param name="renglones">Los renglones que casaron; solo cuentan los que traen motivo.</param>
    /// <param name="avisos">La lista a la que se añade un aviso por cada fila cuyo motivo difiere del ya guardado para su caso.</param>
    /// <returns>El motivo por id de caso; los casos sin ningún motivo no aparecen.</returns>
    private static Dictionary<long, MotivoDeNoCompletar> MotivoPorCaso(
        IReadOnlyList<RenglonDeLaVuelta> renglones, List<Aviso> avisos)
    {
        var motivos = new Dictionary<long, MotivoDeNoCompletar>();
        var deQueFila = new Dictionary<long, int>();
        foreach (var renglon in renglones.Where(r => r.Motivo != MotivoDeNoCompletar.SinMotivo)
                                         .OrderBy(r => r.FilaExcel))
        {
            if (motivos.TryAdd(renglon.CasoId, renglon.Motivo))
            {
                deQueFila[renglon.CasoId] = renglon.FilaExcel;
                continue;
            }
            if (motivos[renglon.CasoId] == renglon.Motivo)
                continue;

            avisos.Add(Aviso.Advierte(
                "Un mismo documento volvió con dos motivos distintos.",
                MotivosDeLaHoja.ColumnaDelMotivo,
                $"Se guardó «{MotivosDeLaHoja.Decir(motivos[renglon.CasoId])}», que es el de la "
                + $"fila {deQueFila[renglon.CasoId]}. La fila {renglon.FilaExcel} decía "
                + $"«{MotivosDeLaHoja.Decir(renglon.Motivo)}» y el documento solo guarda uno; "
                + "el comentario de cada persona sí quedó entero."));
        }
        return motivos;
    }

    /// <summary>Dice en voz alta que el documento salio completo Y con un motivo escrito.</summary>
    /// <remarks>
    /// Mandan los pasos: seis «Sí» son una afirmacion de que se miro cada uno, y el motivo
    /// se guarda igual para que quien lo lea decida. Callar la contradiccion seria elegir
    /// por Miguel.
    /// </remarks>
    /// <param name="casoId">El caso que se está decidiendo.</param>
    /// <param name="estado">El estado que dicen los pasos.</param>
    /// <param name="motivo">El motivo elegido para ese caso.</param>
    /// <param name="renglones">Todos los renglones que casaron, para nombrar las filas con motivo de este caso.</param>
    /// <param name="avisos">La lista a la que se añade el aviso, solo si hay contradicción.</param>
    private static void AvisarDeLaContradiccion(
        long casoId,
        EstadoDeRecomendacion estado,
        MotivoDeNoCompletar motivo,
        IReadOnlyList<RenglonDeLaVuelta> renglones,
        List<Aviso> avisos)
    {
        if (estado != EstadoDeRecomendacion.Completa || motivo == MotivoDeNoCompletar.SinMotivo)
            return;

        var filas = renglones
            .Where(r => r.CasoId == casoId && r.Motivo != MotivoDeNoCompletar.SinMotivo)
            .Select(r => r.FilaExcel);
        avisos.Add(Aviso.Advierte(
            "Un documento volvió completo y con un motivo escrito al mismo tiempo.",
            MotivosDeLaHoja.ColumnaDelMotivo,
            $"Manda lo que dicen los pasos —completa—, y el motivo «{MotivosDeLaHoja.Decir(motivo)}» "
            + $"se guarda igual. Está en la(s) fila(s) {string.Join(", ", filas)}: mírelo antes de "
            + "dar la ronda por cerrada."));
    }

    /// <summary>
    /// El estado del documento según los pasos de TODAS sus personas en la base: las que
    /// volvieron en la hoja con lo que trajeron, las que no con lo que ya tenían guardado.
    /// </summary>
    /// <param name="casoId">El caso que se está decidiendo.</param>
    /// <param name="renglones">Todos los renglones que casaron; se filtran a los de este caso.</param>
    /// <returns>No completa si alguien tiene un «No»; completa si todas tienen los seis «Sí»; si no, sin marcar. Un caso sin personas queda sin marcar.</returns>
    private EstadoDeRecomendacion EstadoDelDocumento(long casoId, IReadOnlyList<RenglonDeLaVuelta> renglones)
    {
        var deLaHoja = renglones.Where(r => r.CasoId == casoId).ToDictionary(r => r.PersonaId, r => r.Respuestas);
        var personas = _personas.DeCaso(casoId);
        if (personas.Count == 0)
            return EstadoDeRecomendacion.SinMarcar;

        var estados = personas.Select(persona => deLaHoja.TryGetValue(persona.Id, out var respuestas)
            ? Pasos.EstadoDeLosPasos(respuestas)
            : Pasos.EstadoDeLosPasos(RespuestasGuardadas(persona))).ToList();

        if (estados.Any(estado => estado == false))
            return EstadoDeRecomendacion.NoCompleta;
        return estados.All(estado => estado == true) ? EstadoDeRecomendacion.Completa : EstadoDeRecomendacion.SinMarcar;
    }

    /// <summary>
    /// Las siete respuestas que la persona ya tiene en la base, en la misma forma en que
    /// vienen las de la hoja, para que <see cref="Pasos.EstadoDeLosPasos"/> las lea igual.
    /// </summary>
    /// <param name="persona">Una persona del caso que no volvió en la hoja.</param>
    private static Dictionary<string, bool?> RespuestasGuardadas(Persona persona) => new()
    {
        ["paso_preparacion"] = persona.PasoPreparacion,
        ["paso_informacion"] = persona.PasoInformacion,
        ["paso_cita_del_templo"] = persona.PasoCitaDelTemplo,
        ["paso_acciones_requeridas"] = persona.PasoAccionesRequeridas,
        ["paso_entrevistas"] = persona.PasoEntrevistas,
        ["paso_listo_para_el_templo"] = persona.PasoListoParaElTemplo,
        [Pasos.ColumnaDeLaLlamada] = persona.LlamoAlLider,
    };

    /// <summary>
    /// El unico estado que se puede deducir de los seis pasos de UNA persona.
    /// </summary>
    /// <remarks>
    /// ⚠️ De «los seis dicen que sí» NO se deduce ningún valor, y no es un olvido: el texto que
    /// significaría «resuelta» a nivel de persona es uno de los que el dueño todavía no ha
    /// dicho. Una persona con los seis pasos en «Sí» se guarda con estado propuesto nulo y con
    /// sus seis columnas puestas, que es donde consta que alguien la miró y que salió bien.
    /// </remarks>
    /// <param name="respuestas">Las siete respuestas de la fila, por nombre de columna.</param>
    /// <returns>«incompleta» si algún paso dice que no; nulo en cualquier otro caso.</returns>
    private static string? EstadoPropuestoDe(IReadOnlyDictionary<string, bool?> respuestas)
        => Pasos.EstadoDeLosPasos(respuestas) == false ? "incompleta" : null;

    // ────────────────────────────── aplicar ──────────────────────────────

    /// <summary>Aplica a la base las marcas leidas; escribe estado, nunca firma campos.</summary>
    /// <remarks>
    /// <b>Si el id del caso viene, MANDA el id.</b> Es lo unico que no se repite, y con el la
    /// resolucion es como mucho una persona. Cuando viene nulo —una hoja generada antes del
    /// 2026-09-03, sin el id en la clave— se sigue el camino viejo por <c>numero_caso</c> +
    /// <c>mrn</c>, y ese camino no cambia: si el par apunta a dos familias, la fila se DESCARTA
    /// con su motivo. No se escoge una al azar.
    /// </remarks>
    /// <param name="marcas">Las marcas que dio <see cref="LeerExcelDevuelto"/>, una por fila que casó.</param>
    /// <param name="companeroId">Quién las trae; va en cada propuesta, en cada descarte y en el estado del documento.</param>
    /// <param name="rutaExcel">De qué archivo salieron; se guarda como origen del estado y en los descartes.</param>
    /// <returns>Escrito si se aplicó alguna fila o se marcó algún documento; los avisos empiezan por el recuento.</returns>
    public ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel)
    {
        if (_companeros.Obtener(companeroId) is null)
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"No hay ningún compañero con el número interno {companeroId}.",
                string.Empty,
                "Una propuesta sin quien la hace no dice nada, y una marca de estado sin quien la puso tampoco."));

        var avisos = new List<Aviso>();
        var aplicadas = 0;
        var descartadas = 0;
        var estadoPorCaso = new Dictionary<long, EstadoDeRecomendacion>();
        var motivoPorCaso = new Dictionary<long, MotivoDeNoCompletar>();

        foreach (var marca in marcas)
        {
            var casan = PersonasQueCasan(marca.NumeroCaso, marca.Mrn, marca.CasoId);
            if (casan.Count != 1)
            {
                descartadas++;
                _ilegibles.RegistrarDescartada(new FilaDescartada
                {
                    CompaneroId = companeroId,
                    RutaExcel = rutaExcel,
                    FilaExcel = marca.FilaExcel,
                    NumeroCaso = marca.NumeroCaso,
                    Mrn = marca.Mrn,
                    Nombre = marca.Nombre,
                    Motivo = $"Fila {marca.FilaExcel}: "
                             + (casan.Count == 0 ? Motivos.SinPar : Motivos.ClaveAmbigua(casan.Count)) + ".",
                    RegistradoEn = _reloj.Ahora(),
                });
                continue;
            }

            var persona = casan[0];
            var escritura = _personas.AnotarPropuesta(persona.Id, persona with
            {
                EstadoPropuesto = marca.EstadoPropuesto,
                NotaCompanero = marca.NotaCompanero,
                PasoPreparacion = marca.PasoPreparacion,
                PasoInformacion = marca.PasoInformacion,
                PasoCitaDelTemplo = marca.PasoCitaDelTemplo,
                PasoAccionesRequeridas = marca.PasoAccionesRequeridas,
                PasoEntrevistas = marca.PasoEntrevistas,
                PasoListoParaElTemplo = marca.PasoListoParaElTemplo,
                LlamoAlLider = marca.LlamoAlLider,
            }, companeroId);
            avisos.AddRange(escritura.Avisos);
            if (!escritura.SeEscribio)
            {
                descartadas++;
                continue;
            }

            aplicadas++;
            if (marca.Motivo != MotivoDeNoCompletar.SinMotivo)
                motivoPorCaso.TryAdd(persona.CasoId, marca.Motivo);
            var estado = EstadoConElMotivo(marca.EstadoDeLaRecomendacion, marca.Motivo);
            if (estado != EstadoDeRecomendacion.SinMarcar)
                estadoPorCaso[persona.CasoId] = estado;
        }

        // El estado del documento se escribe UNA vez por caso, con el nombre del companero y
        // con la ruta del Excel como origen: «el documento que ellos llenan es el que marca».
        // Esto NO es firmar campos: la firma por campo sigue exigiendo el clic de Miguel.
        //
        // Va por `MarcarEstadoDelCompanero` y no por `MarcarEstado`, y esa es la diferencia
        // que faltaba: ademas del estado vigente escribe `estado_del_companero`, `_por` y
        // `_en`, que la migracion 14 creo en su dia y que nadie escribia por ningun camino.
        // Sin ellas, cuando Miguel corrige encima se pierde lo que dijo el agente, que es
        // justo el dato del que se alimenta el reporte del gerente.
        //
        // ⚠️ El motivo es el que ELIGIO el companero en su hoja (columna
        // «¿Por qué no se completó?», anadida el 2026-09-05). Va a `motivo_del_companero` y
        // NUNCA a `motivo_no_completa`, que es el de Miguel: criterio C14-4 de PENDIENTES.md.
        // Antes de esa fecha aqui iba SinMotivo fijo, y esa columna llevaba desde la
        // migracion 18 nula en toda la base.
        var marcados = estadoPorCaso.Keys.Union(motivoPorCaso.Keys).ToList();
        foreach (var casoId in marcados)
        {
            avisos.AddRange(
                _casos.MarcarEstadoDelCompanero(
                    casoId,
                    estadoPorCaso.GetValueOrDefault(casoId, EstadoDeRecomendacion.NoCompleta),
                    motivoPorCaso.GetValueOrDefault(casoId, MotivoDeNoCompletar.SinMotivo),
                    companeroId,
                    rutaExcel).Avisos);
        }

        avisos.Insert(0, Aviso.Informa(
            $"Aplicadas {aplicadas} fila(s); descartadas {descartadas}; documentos marcados {marcados.Count}.",
            string.Empty,
            "Nada de esto queda verificado: lo que trae el compañero es una propuesta, y los campos "
            + "los sigue confirmando usted uno a uno."));
        return new ResultadoDeEscritura(aplicadas > 0 || marcados.Count > 0, companeroId, avisos);
    }

    // ─────────────────────────── resolver la clave ───────────────────────────

    /// <summary>
    /// TODAS las personas a las que puede referirse esa clave. Cero, una, o varias.
    /// </summary>
    /// <remarks>
    /// ⚠️ Devuelve una lista y no una persona a proposito. Mientras el numero de caso fue
    /// unico, el par «caso + MRN» encadenaba a una fila o a ninguna, nunca a dos. Desde la
    /// version 12 del esquema no lo es —el numero son cuatro letras mas el ano y el mes:
    /// identifica una unidad y un mes, no una familia— y ademas un documento duplicado entra
    /// en vez de rechazarse. Escribir la propuesta del companero sobre «la primera que salga»
    /// seria escribirla sobre la familia equivocada sin que nadie se entere.
    /// <para>
    /// Con el id del caso la respuesta vuelve a ser como mucho una, y entonces el numero de
    /// caso YA NO SE MIRA: si Miguel corrigio el numero despues de generar el paquete, el
    /// Excel del companero sigue diciendo el viejo, y exigir que coincidiera tiraria trabajo
    /// bueno.
    /// </para>
    /// <para>
    /// Un MRN vacio devuelve la lista vacia a proposito: un caso puede tener varias personas
    /// sin MRN y no hay forma de saber a cual se referia el companero.
    /// </para>
    /// </remarks>
    /// <param name="numeroCaso">La primera parte de la clave; solo se mira cuando no hay id.</param>
    /// <param name="mrn">La segunda parte; vacía devuelve la lista vacía sin buscar.</param>
    /// <param name="casoId">La tercera parte; con ella se busca solo dentro de ese caso.</param>
    private IReadOnlyList<Persona> PersonasQueCasan(string? numeroCaso, string? mrn, long? casoId)
    {
        if (string.IsNullOrWhiteSpace(mrn))
            return [];

        if (casoId is long id)
            return [.. _personas.DeCaso(id).Where(persona => persona.Mrn == mrn)];

        if (string.IsNullOrWhiteSpace(numeroCaso))
            return [];

        var encontradas = new List<Persona>();
        foreach (var caso in CasosConEseNumero(numeroCaso))
            encontradas.AddRange(_personas.DeCaso(caso.Id).Where(persona => persona.Mrn == mrn));
        return encontradas;
    }

    /// <summary>
    /// Los casos que llevan ese numero, archivados incluidos.
    /// </summary>
    /// <remarks>
    /// Los archivados entran a proposito: un caso archivado sigue teniendo personas y el
    /// companero pudo haberlo recibido antes de que se archivara. Dejarlo fuera convertiria su
    /// trabajo en un descarte «sin par» que nadie sabria explicar.
    /// <para>
    /// El filtro por texto del puerto busca tambien por nombre y por MRN, asi que despues se
    /// compara el numero LETRA POR LETRA: un filtro que sobra se recorta aqui, uno que falta no
    /// se puede recuperar.
    /// </para>
    /// </remarks>
    /// <param name="numeroCaso">El número tal como vino en la clave; se compara sin distinguir mayúsculas.</param>
    private List<Caso> CasosConEseNumero(string numeroCaso)
    {
        var encontrados = new List<Caso>();
        var filtro = new FiltroDeCasos(Texto: numeroCaso, IncluirArchivados: true);
        var trozo = Pagina.Primera(TamanoDelTrozo);
        while (true)
        {
            var pagina = _casos.Listar(filtro, trozo);
            encontrados.AddRange(pagina.Elementos.Where(caso =>
                string.Equals(caso.NumeroCaso, numeroCaso, StringComparison.OrdinalIgnoreCase)));
            if (!pagina.HayMas)
                return encontrados;
            trozo = trozo.Siguiente();
        }
    }
}
