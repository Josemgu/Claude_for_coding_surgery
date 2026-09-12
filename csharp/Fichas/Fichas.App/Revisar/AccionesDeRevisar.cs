using System.Globalization;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// Lo que la pantalla de Revisar ESCRIBE: archivar en lote, marcar a mano y desarchivar.
/// </summary>
/// <remarks>
/// Va aparte del tablero por la misma razon por la que en el programa viejo
/// <c>datos/marcas_de_revision.py</c> va aparte de <c>datos/revision.py</c>: lo que lee y
/// lo que escribe no se mezclan.
///
/// **Asignar NO esta aqui**: esa operacion es <see cref="Asignar.OperacionDeAsignar"/>, una
/// sola para todo el programa (criterio C5-1).
///
/// **La firma de campos de Miguel tampoco esta aqui.** Lo que se escribe es
/// <c>estado_recomendacion</c>, que es lo que el Excel del companero marca; «Todo correcto»
/// sobre los campos es otra cosa y vive en la pantalla de correccion (CLAUDE.md §1.5).
/// </remarks>
public sealed class AccionesDeRevisar
{
    /// <summary>El puerto de documentos: aquí se archiva, se marca y se pone la fecha.</summary>
    private readonly ICasos _casos;
    /// <summary>De dónde sale la fecha de archivado; nunca de <c>DateTime.Now</c>, para poder probarla.</summary>
    private readonly IReloj _reloj;
    /// <summary>La franja de la cáscara: aquí se dejan los motivos de lo que no entró.</summary>
    private readonly BuzonDeAvisos _avisos;
    /// <summary>Lo que se le quita a un documento al archivarlo: su asignación (dueño, 2026-09-11).</summary>
    private readonly RetiradaAlArchivar _retirada;

    /// <summary>Ata las acciones al repositorio de casos, al reloj, al buzon de la franja y a la retirada.</summary>
    /// <remarks>
    /// La retirada entra por aqui y no se monta dentro por lo mismo que la limpieza de la
    /// vuelta en <c>OperacionDeLaVuelta</c>: montarla sin ella dejaria las pruebas midiendo
    /// una tuberia mas corta que la del programa.
    /// </remarks>
    /// <param name="casos">El puerto de documentos.</param>
    /// <param name="reloj">De dónde sale «hoy».</param>
    /// <param name="avisos">La franja donde se dejan los motivos.</param>
    /// <param name="retirada">La que quita la asignación a lo que queda archivado.</param>
    public AccionesDeRevisar(ICasos casos, IReloj reloj, BuzonDeAvisos avisos, RetiradaAlArchivar retirada)
    {
        _casos = casos;
        _reloj = reloj;
        _avisos = avisos;
        _retirada = retirada;
    }

    /// <summary>
    /// Archiva de golpe todos los casos marcados, y a cada uno que queda archivado le quita
    /// la asignacion a quien lo llevara. Archivar NO es borrar: pone <c>archivado</c> con su
    /// fecha y el caso sigue contando en los reportes y en el calendario (criterio C8-3), asi
    /// que no hace falta preguntar nada.
    /// </summary>
    /// <remarks>
    /// <para>La asignacion se quita <b>despues</b> de que el archivado haya entrado, y solo
    /// entonces: si archivar no se pudo escribir, el documento sigue siendo trabajo de quien lo
    /// lleva. Lo pidio el dueno el 2026-09-11 —<i>«cuando los documentos se archiven, ya no
    /// aparezcan asignados al agente»</i>— y el como es el de <see cref="RetiradaAlArchivar"/>:
    /// se desactiva con su fecha, nunca se borra.</para>
    ///
    /// <para>⚠️ Desarchivar NO la devuelve. Un documento que vuelve del archivo queda sin
    /// asignar y el dueno decide a quien va; devolverselo solo al de antes seria decidir por el.</para>
    ///
    /// <para>⚠️ <b>Dónde se ve después un archivado no lo decide esta función</b>, y la frase
    /// del resumen sobre el calendario viene de antes de que el dueño lo cambiara: el 2026-09-06
    /// pidió que desapareciera «de todas partes» y el 2026-09-07 (§5 de esa entrada) que en el
    /// calendario se quedara «marcado en verde». Aquí solo se escribe <c>archivado</c> con su
    /// fecha y se quita la asignación; cada pantalla lee esa columna con su propia regla.</para>
    /// </remarks>
    /// <param name="casoIds">Los números internos de los documentos marcados.</param>
    /// <returns>Cuántos entraron, cuántos no y cuántos dejaron de estar asignados; los motivos ya quedaron en la franja.</returns>
    public ResumenDeLote ArchivarEnLote(IReadOnlyCollection<long> casoIds)
    {
        var retirados = 0;
        var resumen = EnLote(
            casoIds,
            id =>
            {
                var archivado = _casos.Archivar(id, true, _reloj.Hoy());
                if (archivado.SeEscribio && _retirada.QuitarLasDe(id) > 0) retirados++;
                return archivado;
            },
            "archivado",
            "archivados");

        return resumen with { DejanDeEstarAsignados = retirados };
    }

    /// <summary>Desarchiva de golpe: la vuelta atras de lo anterior, y por el mismo camino.</summary>
    /// <remarks>
    /// Por el mismo camino en lo que toca al caso; la asignacion que se quito al archivar
    /// <b>no</b> se devuelve, y eso es a proposito (ver <see cref="ArchivarEnLote"/>).
    /// </remarks>
    /// <param name="casoIds">Los números internos de los documentos marcados.</param>
    /// <returns>Cuántos entraron y cuántos no; <c>DejanDeEstarAsignados</c> va en cero porque desarchivar no asigna a nadie.</returns>
    public ResumenDeLote DesarchivarEnLote(IReadOnlyCollection<long> casoIds)
        => EnLote(casoIds, id => _casos.Archivar(id, false, string.Empty), "desarchivado", "desarchivados");

    /// <summary>
    /// Marca a mano el estado de un documento, con el nombre de quien lo marca y SIN motivo.
    /// </summary>
    /// <remarks>
    /// Del dueno, 2026-09-03: «lo que Miguel marque a mano despues manda». Escribe el mismo
    /// campo que escribe el Excel del companero, y por eso deja dicho de donde vino la
    /// marca: en la tarjeta se distingue «Completada por Sandy» de lo que puso Miguel.
    /// </remarks>
    /// <param name="casoId">El documento que se marca.</param>
    /// <param name="estado">Lo que se dice de la recomendación.</param>
    /// <param name="quienMarca">Quién lo está marcando; su nombre queda en la firma del estado.</param>
    public ResultadoDeEscritura MarcarAMano(long casoId, EstadoDeRecomendacion estado, long quienMarca)
        => MarcarAMano(casoId, estado, MotivoDeNoCompletar.SinMotivo, quienMarca);

    /// <summary>
    /// Marca a mano el estado y su motivo. Los dos van juntos, siempre, en el mismo gesto.
    /// </summary>
    /// <remarks>
    /// <para>El dueno pidio tres estados el 2026-09-05: «no completado, no se pudo comunicar
    /// con el lider, o el lider no lo hizo». La migracion 18 dejo el vocabulario y las
    /// columnas, y la pantalla del grupo ya sabia leerlos; medido con grep, <b>nadie los
    /// escribia</b>, asi que todo salia como «no completado». Esta es la mano de Miguel.</para>
    ///
    /// <para><b>El motivo se escribe SIEMPRE</b>, aunque sea para vaciarlo. Es lo que hace que
    /// «quitar el motivo» y «dar por completo un caso que lo tenia» no dejen atras un motivo
    /// huerfano diciendo por que no se completo algo que si esta completo.</para>
    ///
    /// <para>⚠️ Va a <c>casos.motivo_no_completa</c> y <b>nunca</b> a
    /// <c>casos.motivo_del_companero</c>, que es lo que dijo la hoja y no se toca desde aqui
    /// (ADR-0005 §3.3). Y esto <b>no firma nada</b>: la firma de campos de Miguel es
    /// <c>IProcedencia.Firmar</c> y son dos cosas distintas (CLAUDE.md §1.5).</para>
    /// </remarks>
    /// <param name="casoId">El documento que se marca.</param>
    /// <param name="estado">Lo que se dice de la recomendacion.</param>
    /// <param name="motivo">Por que no esta completa; <c>SinMotivo</c> lo deja o lo quita.</param>
    /// <param name="quienMarca">Quien lo esta marcando; su nombre queda en la firma del estado.</param>
    public ResultadoDeEscritura MarcarAMano(
        long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo, long quienMarca)
    {
        if (estado != EstadoDeRecomendacion.NoCompleta && motivo != MotivoDeNoCompletar.SinMotivo)
        {
            // No es un dato mal leido que haya que conservar: es una contradiccion del
            // programa. Escribirla dejaria en la base «completa porque el lider no lo hizo».
            var contradiccion = ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se marcó: solo un documento «no está completa» puede llevar un motivo.",
                nameof(Caso.MotivoNoCompleta),
                $"Se pidió marcar «{RenglonParaAsignar.PalabraDe(estado)}» con el motivo "
                + $"«{PalabrasDelEstado.DecirElMotivo(motivo)}», y esas dos cosas se contradicen."));
            _avisos.Dejar(contradiccion.Avisos);
            return contradiccion;
        }

        var marca = _casos.MarcarEstado(casoId, estado, quienMarca, OrigenAMano);
        if (!marca.SeEscribio)
        {
            _avisos.Dejar(marca.Avisos);
            return marca;
        }

        var conMotivo = EscribirElMotivo(casoId, motivo);
        _avisos.Dejar([.. marca.Avisos, .. conMotivo.Avisos]);
        return conMotivo;
    }

    /// <summary>
    /// Le da a un documento su fecha de viaje, desde la misma pantalla de Revisar.
    /// </summary>
    /// <remarks>
    /// <para>Del dueño, 2026-09-05, petición 6: «lo que no se reconozca la fecha, ahí mismo
    /// en revisión, en una sección que diga "revisar este documento" <b>para que se pueda ir
    /// a una fecha</b>». Escrita la fecha, el documento sale de esa sección y entra en la
    /// carpeta de su día: es el mismo <see cref="ArbolDeRevisar.Agrupar"/> de siempre, que
    /// ahora tiene un dato que antes no tenía.</para>
    ///
    /// <para>⛔ <b>La fecha no se adivina</b> (regla permanente 1). Solo se acepta
    /// AAAA-MM-DD y un día que exista de verdad; «17 de septiembre», «9/17/26» o
    /// «2026-02-31» no se escriben y se dice por qué. Una fecha de viaje inventada es
    /// exactamente el daño que este programa evita.</para>
    ///
    /// <para>⚠️ <b>El mismo hueco declarado que <see cref="EscribirElMotivo"/>:</b>
    /// <see cref="ICasos"/> no tiene por dónde escribir SOLO la fecha, y
    /// <c>Fichas.Contratos</c> está congelado; se pasa por <c>Guardar</c>, que reescribe la
    /// fila entera con lo que se acaba de leer. Lo que haría falta ahí es un
    /// <c>PonerFechaDeViaje(casoId, fecha)</c>.</para>
    ///
    /// <para>De los avisos que devuelve <c>Guardar</c> se dejan en la franja <b>solo los de
    /// la fecha</b> —el del mes cruzado, que dice que la fecha cae en otro periodo que el
    /// número de caso, y es justo lo que hay que ver al escribirla—. Los de los otros
    /// campos hablan de cosas que este gesto no toca y ya se dijeron cuando el documento
    /// entró.</para>
    /// </remarks>
    /// <param name="casoId">El documento al que se le pone la fecha.</param>
    /// <param name="fecha">La fecha en AAAA-MM-DD; cualquier otra cosa no se escribe.</param>
    public ResultadoDeEscritura PonerLaFechaDeViaje(long casoId, string? fecha)
    {
        if (LeerLaFecha(fecha) is not string fechaIso)
        {
            return NoSePuso(Aviso.Problema(
                "No se puso la fecha: hay que escribirla como AAAA-MM-DD, por ejemplo 2026-09-17.",
                CampoDeLaFecha,
                $"Se escribió «{fecha}», y de ahí no sale una fecha sin adivinar cuál es. "
                + "Un día que no existe, como 2026-02-31, tampoco se guarda."));
        }

        if (_casos.Obtener(casoId) is not Caso caso)
        {
            return NoSePuso(Aviso.Problema(
                "No se puso la fecha: ese documento ya no está en la base.",
                CampoDeLaFecha,
                $"Ningún documento con el número interno {casoId}."));
        }

        if (string.Equals(caso.FechaViaje?.Trim(), fechaIso, StringComparison.Ordinal))
        {
            return ResultadoDeEscritura.Bien(casoId);
        }

        var guardado = _casos.Guardar(caso with { FechaViaje = fechaIso });
        var deLaFecha = guardado.Avisos.Where(a => a.Campo == CampoDeLaFecha).ToArray();
        _avisos.Dejar(guardado.SeEscribio ? deLaFecha : guardado.Avisos);
        return guardado.SeEscribio
            ? new ResultadoDeEscritura(true, casoId, deLaFecha)
            : guardado;
    }

    /// <summary>Deja el aviso en la franja y devuelve que no se escribió nada.</summary>
    /// <remarks>
    /// El aviso se deja AQUÍ y no en la pantalla para que haya un solo sitio que lo haga: si
    /// lo dejara quien llama, un camino que se olvidara de hacerlo dejaría al dueño con una
    /// fecha que no entró y sin nada en pantalla que se lo dijera.
    /// </remarks>
    /// <param name="porque">El aviso que explica por qué no entró la fecha.</param>
    private ResultadoDeEscritura NoSePuso(Aviso porque)
    {
        _avisos.Dejar([porque]);
        return ResultadoDeEscritura.NoSeEscribio(porque);
    }

    /// <summary>La fecha tal como se guarda, o nulo si lo escrito no es una fecha.</summary>
    /// <remarks>
    /// Estricta a propósito: <c>TryParseExact</c> con un solo formato y cultura invariante.
    /// Con <c>TryParse</c> a secas, «9/17/26» se interpretaría según el idioma de la máquina
    /// y en media Europa saldría otro día del que se escribió.
    /// </remarks>
    /// <param name="fecha">Lo que se escribió en el cuadro; se le quitan los espacios de los lados.</param>
    /// <returns>La fecha normalizada como AAAA-MM-DD, o nulo si no es un día que exista escrito así.</returns>
    private static string? LeerLaFecha(string? fecha)
        => DateOnly.TryParseExact(
            fecha?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    /// <summary>El campo al que apuntan los avisos de esta operación, como en la base.</summary>
    private const string CampoDeLaFecha = "fecha_viaje";

    /// <summary>
    /// Deja el motivo vigente del caso en el que se pide, y no escribe si ya era ese.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Hueco declarado.</b> <see cref="ICasos"/> no tiene por donde escribir SOLO el
    /// motivo de Miguel: <c>MarcarEstado</c> no lo lleva y <c>MarcarEstadoDelCompanero</c>
    /// escribe la otra columna. Lo que hace falta ahi es un <c>MarcarMotivo(casoId, motivo)</c>,
    /// y <c>Fichas.Contratos</c> esta congelado, asi que se pasa por <c>Guardar</c>, que
    /// reescribe la fila entera con lo que se acaba de leer.
    ///
    /// Consecuencias, dichas y no escondidas: son DOS escrituras por gesto en vez de una, y
    /// entre la lectura y la escritura cabe otra ventana. En un programa de un solo usuario
    /// con la base en su disco eso no se ha podido provocar, pero no esta cerrado por
    /// construccion, que es lo que si daria un metodo en el puerto.
    ///
    /// Los avisos de formato que devuelve <c>Guardar</c> —el numero de caso, la fecha— NO se
    /// dejan en la franja: hablan de campos que este gesto no toca y ya se dijeron cuando el
    /// documento entro. Si la escritura falla, el problema si sale entero.
    /// </remarks>
    /// <param name="casoId">El documento cuyo motivo se deja vigente.</param>
    /// <param name="motivo">El motivo de Miguel; <c>SinMotivo</c> lo vacía.</param>
    /// <returns>«Bien» si ya era ese o si se escribió; si no, lo que devolvió <c>Guardar</c>.</returns>
    private ResultadoDeEscritura EscribirElMotivo(long casoId, MotivoDeNoCompletar motivo)
    {
        if (_casos.Obtener(casoId) is not Caso caso)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "Se marcó el estado, pero no se pudo releer el documento para escribir el motivo.",
                nameof(Caso.MotivoNoCompleta),
                $"Ningún documento con el número interno {casoId}."));
        }

        if (caso.Motivo == motivo) return ResultadoDeEscritura.Bien(casoId);

        var guardado = _casos.Guardar(caso with { MotivoNoCompleta = Caso.EscribirMotivo(motivo) });
        return guardado.SeEscribio ? ResultadoDeEscritura.Bien(casoId) : guardado;
    }

    /// <summary>
    /// Los tres que el dueno nombro el 2026-09-05, en su orden y sin uno mas.
    /// </summary>
    /// <remarks>
    /// <c>OtraRazon</c> esta en el vocabulario porque una hoja del companero puede traerlo, y
    /// la pantalla del grupo lo sabe leer. En el menu de Miguel NO entra: un cajon de sastre
    /// ofrecido de primeras es lo que hace que todo acabe dentro de el y nadie sepa por que.
    /// </remarks>
    public static IReadOnlyList<MotivoDeNoCompletar> LosTresQuePidioElDueno { get; } =
    [
        MotivoDeNoCompletar.SinMotivo,
        MotivoDeNoCompletar.NoSePudoComunicar,
        MotivoDeNoCompletar.ElLiderNoLoHizo,
    ];

    /// <summary>Como se lee cada opcion del menu de la tarjeta.</summary>
    /// <remarks>
    /// La primera no dice «no completado» a secas como en <see cref="PalabrasDelEstado"/>: ahi
    /// se esta LEYENDO un estado y aqui se esta ELIGIENDO, y quien elige tiene que saber que
    /// esa opcion ademas quita el motivo que hubiera puesto antes.
    /// </remarks>
    /// <param name="motivo">La opción del menú que se está pintando.</param>
    public static string DecirLaOpcion(MotivoDeNoCompletar motivo) => motivo switch
    {
        MotivoDeNoCompletar.SinMotivo => "No está completa, sin decir por qué",
        MotivoDeNoCompletar.NoSePudoComunicar => "No se pudo comunicar con el líder",
        MotivoDeNoCompletar.ElLiderNoLoHizo => "El líder no lo hizo",
        _ => PalabrasDelEstado.DecirElMotivo(motivo),
    };

    /// <summary>Lo que se guarda en <c>estado_marcado_origen</c> cuando la marca la pone una persona aqui.</summary>
    public const string OrigenAMano = "a mano en la pantalla Revisar";

    /// <summary>
    /// Quien firma las marcas hechas a mano en esta pantalla.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Hueco declarado, no resuelto.** No hay en <c>Fichas.Contratos</c> ninguna nocion
    /// de «quien esta usando el programa»: <see cref="ICompaneros"/> no tiene un «yo». Hasta
    /// que el dueno o el planificador digan como se identifica, se toma el companero activo
    /// que se llame Miguel, y si no lo hay, el primer activo. Si no hay ninguno activo se
    /// devuelve nulo y quien llame deja un aviso: no se inventa una firma.
    /// </remarks>
    /// <param name="activos">Los compañeros activos ahora mismo.</param>
    /// <returns>El compañero que firma, o nulo si no hay ninguno activo.</returns>
    public static Companero? QuienFirmaAMano(IReadOnlyList<Companero> activos)
        => activos.FirstOrDefault(c => string.Equals(c.Nombre, "Miguel", StringComparison.OrdinalIgnoreCase))
           ?? activos.FirstOrDefault();

    /// <summary>Aplica la misma escritura a cada caso marcado y cuenta lo que entro y lo que no.</summary>
    /// <param name="casoIds">Los documentos sobre los que se escribe.</param>
    /// <param name="escribir">La escritura que se aplica a cada uno.</param>
    /// <param name="participioSingular">Cómo se dice de uno: «archivado».</param>
    /// <param name="participioPlural">Cómo se dice de varios: «archivados».</param>
    /// <returns>Cuántos entraron y cuántos no; los avisos de todos quedan juntos en la franja.</returns>
    private ResumenDeLote EnLote(
        IReadOnlyCollection<long> casoIds,
        Func<long, ResultadoDeEscritura> escribir,
        string participioSingular,
        string participioPlural)
    {
        var entraron = 0;
        var noEntraron = 0;
        var avisos = new List<Aviso>();
        foreach (var casoId in casoIds)
        {
            var resultado = escribir(casoId);
            if (resultado.SeEscribio) entraron++;
            else noEntraron++;
            avisos.AddRange(resultado.Avisos);
        }

        // Los avisos del lote se dejan juntos: 300 documentos no son 300 franjas.
        _avisos.Dejar(avisos);
        return new ResumenDeLote(entraron, noEntraron, participioSingular, participioPlural);
    }
}

/// <summary>Lo que dejo una escritura en lote, para decirlo en una linea en el acuse.</summary>
/// <param name="Hechos">Cuantos se escribieron.</param>
/// <param name="NoSePudieron">Cuantos no; su motivo ya esta en la franja.</param>
/// <param name="ParticipioSingular">Como se dice de uno: «archivado», «desarchivado».</param>
/// <param name="ParticipioPlural">Como se dice de varios: «archivados», «desarchivados».</param>
/// <param name="DejanDeEstarAsignados">De los hechos, cuantos llevaban a alguien y ya no; solo al archivar.</param>
public sealed record ResumenDeLote(
    int Hechos, int NoSePudieron, string ParticipioSingular, string ParticipioPlural, int DejanDeEstarAsignados = 0)
{
    /// <summary>
    /// La linea de una sola frase que se ensena en el acuse del pie.
    /// </summary>
    /// <remarks>
    /// <para>Las dos formas se escriben a mano y no las adivina un pluralizador, que es la regla de
    /// <see cref="Plural"/>: acertaria con «documento» y fallaria con cualquier palabra que no
    /// haga el plural en «-s», y un programa que inventa una palabra en espanol delante de
    /// quien lo usa es peor que uno repetitivo.</para>
    ///
    /// <para>Cuando al archivar se le quito la asignacion a alguno, se dice cuantos: un numero
    /// que baja en el cuadro de los agentes sin explicacion se lee como que algo se perdio.</para>
    /// </remarks>
    public string Linea
    {
        get
        {
            var hechos = Plural.Con(Hechos, "documento", "documentos")
                + " " + Plural.Palabra(Hechos, ParticipioSingular, ParticipioPlural);
            var colas = new List<string>();
            if (DejanDeEstarAsignados > 0)
                colas.Add($"{DejanDeEstarAsignados} " + Plural.Palabra(DejanDeEstarAsignados, "deja", "dejan") + " de estar "
                    + Plural.Palabra(DejanDeEstarAsignados, "asignado", "asignados"));
            if (NoSePudieron > 0)
                colas.Add($"{NoSePudieron} no " + Plural.Palabra(NoSePudieron, "se pudo", "se pudieron") + " (mira la franja)");

            return colas.Count == 0 ? $"{hechos}." : $"{hechos}; {string.Join("; ", colas)}.";
        }
    }
}
