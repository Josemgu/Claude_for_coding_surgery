using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Esquema;
using Fichas.Datos.Validacion;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>Las personas de los formularios: 28 columnas por fila.</summary>
/// <remarks>
/// <para>
/// ⚠️ Las seis <c>ord_*</c> son a que va la persona al templo, leidas del papel. Los
/// seis <c>paso_*</c> son los pasos del sistema del lider, que devuelve el companero.
/// NO son lo mismo y no se mezclan; <c>llamo_al_lider</c> no es un septimo paso.
/// </para>
/// <para>
/// ⚠️ <b>Las tres <c>pasos_*</c> de la migracion 19 no se leen en <see cref="Leer"/>.</b>
/// Son la firma de las seis preguntas y salen por <see cref="FirmasDeLosPasosDelCaso"/>,
/// no dentro de <c>Persona</c>: ese modelo vive en <c>Fichas.Contratos/Modelos</c>, que
/// esta congelado. Es una desviacion declarada, no un olvido.
/// </para>
/// </remarks>
public sealed class RepositorioDePersonas : RepositorioBase, IPersonas
{
    /// <summary>Las 25 columnas de <c>personas</c> que se leen, en el orden exacto en que <c>Leer</c> las espera por posición.</summary>
    /// <remarks>Si se añade una columna aquí, hay que añadirla al final y darle su índice en <c>Leer</c>: la lectura es por posición, no por nombre.</remarks>
    /// <remarks>Faltan a propósito <c>pasos_por</c>, <c>pasos_en</c> y <c>pasos_origen</c>: ver la nota de la clase.</remarks>
    private const string Columnas =
        "id, caso_id, mrn, nombre, fila_formulario, ord_recibir_propias, " +
        "ord_observar_sellamiento, ord_traductor, ord_investidura, " +
        "ord_sellamiento_esposos, ord_sellamiento_hijo_padres, pagina_pdf, " +
        "estado_propuesto, nota_companero, propuesto_por, propuesto_en, " +
        "motivo_no_viajo, pudo_viajar, paso_preparacion, paso_informacion, " +
        "paso_cita_del_templo, paso_acciones_requeridas, paso_entrevistas, " +
        "paso_listo_para_el_templo, llamo_al_lider";

    /// <summary>
    /// Lo que se escribe en <c>pasos_origen</c> cuando las seis llegan por el Excel que
    /// devuelve el companero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Es el mismo texto que usa la pantalla de Correccion</b>
    /// (<c>LoQueContestoElCompanero.ElExcelDeVuelta</c>), y esta escrito dos veces porque
    /// <c>Fichas.Datos</c> no puede depender de <c>Fichas.App</c>. Que no se separen no se
    /// deja a la buena fe: lo comprueba
    /// <c>PruebasDeQuienContestoDesdeRevisar.LaVentanaYCorreccionNombranLaViaDelExcelIgual</c>
    /// comparando el texto que VUELVE de la base con el de alli. Si se separan, la misma
    /// respuesta se leeria de dos formas segun la pantalla desde la que se mire.
    /// </para>
    /// <para>
    /// Es una constante y no la ruta del Excel porque <see cref="IPersonas.AnotarPropuesta"/>
    /// no recibe el origen y <c>Fichas.Contratos</c> esta congelado. Se dice la via, que es
    /// lo que separa lo que llego del companero de lo que se contesto a mano; la ruta exacta
    /// del archivo se sabra el dia que se descongele y se le pueda pasar.
    /// </para>
    /// </remarks>
    public const string OrigenDelExcelDeVuelta = "su Excel de vuelta";

    /// <summary>Trabaja sobre una conexion ya abierta con el esquema aplicado.</summary>
    /// <param name="conexion">La conexión abierta; no puede ser nula.</param>
    public RepositorioDePersonas(SqliteConnection conexion) : base(conexion)
    {
    }

    /// <inheritdoc />
    public PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var donde = ComponerElFiltro(filtro);

        return Paginar(
            $"SELECT {Columnas} FROM personas {donde} ORDER BY caso_id, fila_formulario, id",
            $"SELECT COUNT(*) FROM personas {donde}",
            orden => PonerLosParametrosDelFiltro(orden, filtro),
            Leer,
            trozo);
    }

    /// <inheritdoc />
    public int Contar(FiltroDePersonas filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return ContarCon(
            $"SELECT COUNT(*) FROM personas {ComponerElFiltro(filtro)}",
            orden => PonerLosParametrosDelFiltro(orden, filtro));
    }

    /// <inheritdoc />
    public Persona? Obtener(long id)
        => ObtenerUno($"SELECT {Columnas} FROM personas WHERE id = $id", id, Leer);

    /// <inheritdoc />
    public IReadOnlyList<Persona> DeCaso(long casoId)
        => ListarTodo(
            $"SELECT {Columnas} FROM personas WHERE caso_id = $caso " +
            "ORDER BY fila_formulario, id",
            orden => orden.Parameters.AddWithValue("$caso", casoId),
            Leer);

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        var avisos = ReglasDeFormato.Juntar(
            ReglasDeFormato.RevisarMrn(persona.Mrn),
            ReglasDeFormato.RevisarPaginaPdf(persona.PaginaPdf));

        return persona.Id == 0 ? Insertar(persona, avisos) : Cambiar(persona, avisos);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(propuesta);

        // ⚠️ Esto ANOTA lo que el companero propone; NO verifica nada. La propuesta va
        // en columnas propias y no en `procedencia_campo` a proposito: escribir alli un
        // `verificado_por` obliga a `verificado = 1`, y eso seria marcar como verificado
        // automaticamente, que es justo lo que la regla permanente 5 prohibe.
        var contestaAlgunaDeLasSeis = ContestaAlgunaDeLasSeis(propuesta);

        return Escribir(
            "UPDATE personas SET estado_propuesto = $estado, nota_companero = $nota, " +
            "propuesto_por = $por, propuesto_en = $cuando, " +
            "paso_preparacion = $p1, paso_informacion = $p2, paso_cita_del_templo = $p3, " +
            "paso_acciones_requeridas = $p4, paso_entrevistas = $p5, " +
            "paso_listo_para_el_templo = $p6, llamo_al_lider = $lider" +
            (contestaAlgunaDeLasSeis ? LaFirmaDeLasSeis : string.Empty) +
            " WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$estado", ONulo(propuesta.EstadoPropuesto));
                orden.Parameters.AddWithValue("$nota", ONulo(propuesta.NotaCompanero));
                orden.Parameters.AddWithValue("$por", companeroId);
                orden.Parameters.AddWithValue("$cuando", AplicadorDeEsquema.MarcaDeTiempo());
                orden.Parameters.AddWithValue("$p1", DeCasilla(propuesta.PasoPreparacion));
                orden.Parameters.AddWithValue("$p2", DeCasilla(propuesta.PasoInformacion));
                orden.Parameters.AddWithValue("$p3", DeCasilla(propuesta.PasoCitaDelTemplo));
                orden.Parameters.AddWithValue("$p4", DeCasilla(propuesta.PasoAccionesRequeridas));
                orden.Parameters.AddWithValue("$p5", DeCasilla(propuesta.PasoEntrevistas));
                orden.Parameters.AddWithValue("$p6", DeCasilla(propuesta.PasoListoParaElTemplo));
                orden.Parameters.AddWithValue("$lider", DeCasilla(propuesta.LlamoAlLider));
                orden.Parameters.AddWithValue("$id", personaId);

                if (contestaAlgunaDeLasSeis)
                {
                    orden.Parameters.AddWithValue("$origen", OrigenDelExcelDeVuelta);
                }
            },
            [],
            personaId);
    }

    /// <summary>
    /// Las tres columnas de la migracion 19, con los mismos <c>$por</c> y <c>$cuando</c> que
    /// firman el estado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Quien escribe los seis <c>paso_*</c> es quien queda como que los contesto.</b>
    /// Hasta el 2026-09-06 este UPDATE pisaba los seis y dejaba <c>pasos_por</c> apuntando a
    /// quien hubiera contestado ANTES: la base quedaba diciendo que contesto alguien que no
    /// fue, y la ventana de las seis de Revisar —que lee estas tres columnas— decia «nadie
    /// ha contestado» de una persona cuyas seis si habia contestado el Excel del companero.
    /// </para>
    /// <para>
    /// Se reusan <c>$por</c> y <c>$cuando</c> a proposito: la firma del estado y la de las
    /// seis salen de la MISMA escritura, asi que llevan el mismo nombre y la misma marca de
    /// tiempo, y nadie tiene que comparar fechas en ninguna pantalla para adivinar cual de
    /// las dos vias escribio lo que se esta viendo. Siguen siendo columnas distintas porque
    /// otro dia pueden venir de personas distintas: <c>propuesto_por</c> habla del ESTADO y
    /// <c>pasos_por</c> de las seis preguntas.
    /// </para>
    /// <para>
    /// ⚠️ El CHECK de la migracion 19 exige <c>pasos_por</c> y <c>pasos_en</c> las dos o
    /// ninguna; aqui van siempre juntas.
    /// </para>
    /// </remarks>
    private const string LaFirmaDeLasSeis =
        ", pasos_por = $por, pasos_en = $cuando, pasos_origen = $origen";

    /// <summary>
    /// Si el Excel del companero contesto alguna de las seis preguntas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Las seis EN BLANCO no cuentan como que ese companero las contesto.</b> Un
    /// companero puede devolver su Excel diciendo el estado del caso —«no completa»— sin
    /// haber mirado ninguna de las seis: firmarlas ahi le atribuye un trabajo que no hizo, y
    /// es el mismo dano por el otro lado. En ese caso no se escriben las tres columnas y no
    /// se toca lo que hubiera: si Miguel las habia contestado, su firma sigue siendo suya.
    /// </para>
    /// <para>
    /// ⚠️ <c>llamo_al_lider</c> NO cuenta: no es un septimo paso.
    /// </para>
    /// </remarks>
    /// <param name="propuesta">Lo que trajo el Excel del compañero.</param>
    /// <returns>Verdadero si alguno de los seis <c>Paso*</c> no es nulo.</returns>
    private static bool ContestaAlgunaDeLasSeis(Persona propuesta)
        => propuesta.PasoPreparacion is not null
        || propuesta.PasoInformacion is not null
        || propuesta.PasoCitaDelTemplo is not null
        || propuesta.PasoAccionesRequeridas is not null
        || propuesta.PasoEntrevistas is not null
        || propuesta.PasoListoParaElTemplo is not null;

    /// <inheritdoc />
    public ResultadoDeEscritura ResponderLosPasos(
        long personaId, RespuestaALosPasos respuesta, long companeroId, string origen)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        // ⚠️ Se nombran las NUEVE columnas que cambian y ninguna mas. `estado_propuesto`,
        // `nota_companero`, `propuesto_por`, `propuesto_en` y `llamo_al_lider` NO estan en
        // este UPDATE a proposito: son del Excel del companero, llevan su nombre y no se
        // pisan. Y `procedencia_campo` no se toca por ningun camino: firmar un campo es
        // otra cosa (regla permanente 5).
        return Escribir(
            "UPDATE personas SET paso_preparacion = $p1, paso_informacion = $p2, " +
            "paso_cita_del_templo = $p3, paso_acciones_requeridas = $p4, " +
            "paso_entrevistas = $p5, paso_listo_para_el_templo = $p6, " +
            "pasos_por = $por, pasos_en = $cuando, pasos_origen = $origen WHERE id = $id",
            orden =>
            {
                orden.Parameters.AddWithValue("$p1", DeCasilla(respuesta.Preparacion));
                orden.Parameters.AddWithValue("$p2", DeCasilla(respuesta.Informacion));
                orden.Parameters.AddWithValue("$p3", DeCasilla(respuesta.CitaDelTemplo));
                orden.Parameters.AddWithValue("$p4", DeCasilla(respuesta.AccionesRequeridas));
                orden.Parameters.AddWithValue("$p5", DeCasilla(respuesta.Entrevistas));
                orden.Parameters.AddWithValue("$p6", DeCasilla(respuesta.ListoParaElTemplo));
                orden.Parameters.AddWithValue("$por", companeroId);
                orden.Parameters.AddWithValue("$cuando", AplicadorDeEsquema.MarcaDeTiempo());
                orden.Parameters.AddWithValue("$origen", ONulo(origen));
                orden.Parameters.AddWithValue("$id", personaId);
            },
            [],
            personaId);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId)
    {
        var firmas = new Dictionary<long, FirmaDeLosPasos>();

        using var orden = Conexion.CreateCommand();
        orden.CommandText =
            "SELECT id, pasos_por, pasos_en, pasos_origen FROM personas WHERE caso_id = $caso";
        orden.Parameters.AddWithValue("$caso", casoId);

        using var lector = orden.ExecuteReader();
        while (lector.Read())
        {
            firmas[lector.GetInt64(0)] = new FirmaDeLosPasos(
                LargoONulo(lector, 1), TextoONulo(lector, 2), TextoONulo(lector, 3));
        }

        return firmas;
    }

    /// <summary>Crea la fila y devuelve su id nuevo con <c>last_insert_rowid()</c>. Las tres <c>pasos_*</c> nacen en NULL: nadie ha contestado.</summary>
    /// <param name="persona">La persona con <c>Id</c> 0.</param>
    /// <param name="avisos">Lo que las reglas de formato ya dijeron; se devuelven junto con el resultado.</param>
    private ResultadoDeEscritura Insertar(Persona persona, IReadOnlyList<Aviso> avisos)
        => Escribir(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario, " +
            "ord_recibir_propias, ord_observar_sellamiento, ord_traductor, " +
            "ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres, " +
            "pagina_pdf, estado_propuesto, nota_companero, propuesto_por, propuesto_en, " +
            "motivo_no_viajo, pudo_viajar, paso_preparacion, paso_informacion, " +
            "paso_cita_del_templo, paso_acciones_requeridas, paso_entrevistas, " +
            "paso_listo_para_el_templo, llamo_al_lider) " +
            "VALUES ($caso, $mrn, $nombre, $fila, $o1, $o2, $o3, $o4, $o5, $o6, " +
            "$pagina, $estado, $nota, $propuestoPor, $propuestoEn, $motivo, $pudo, " +
            "$p1, $p2, $p3, $p4, $p5, $p6, $lider); SELECT last_insert_rowid()",
            orden => PonerLosCamposDeLaPersona(orden, persona),
            avisos);

    /// <summary>Reescribe las 24 columnas de la fila con ese id, sin tocar las tres <c>pasos_*</c>; si no existe, devuelve un problema en vez de crearla.</summary>
    /// <param name="persona">La persona con su <c>Id</c>.</param>
    /// <param name="avisos">Lo que las reglas de formato ya dijeron; se devuelven junto con el resultado.</param>
    private ResultadoDeEscritura Cambiar(Persona persona, IReadOnlyList<Aviso> avisos)
        => Escribir(
            "UPDATE personas SET caso_id = $caso, mrn = $mrn, nombre = $nombre, " +
            "fila_formulario = $fila, ord_recibir_propias = $o1, " +
            "ord_observar_sellamiento = $o2, ord_traductor = $o3, ord_investidura = $o4, " +
            "ord_sellamiento_esposos = $o5, ord_sellamiento_hijo_padres = $o6, " +
            "pagina_pdf = $pagina, estado_propuesto = $estado, nota_companero = $nota, " +
            "propuesto_por = $propuestoPor, propuesto_en = $propuestoEn, " +
            "motivo_no_viajo = $motivo, pudo_viajar = $pudo, paso_preparacion = $p1, " +
            "paso_informacion = $p2, paso_cita_del_templo = $p3, " +
            "paso_acciones_requeridas = $p4, paso_entrevistas = $p5, " +
            "paso_listo_para_el_templo = $p6, llamo_al_lider = $lider WHERE id = $id",
            orden =>
            {
                PonerLosCamposDeLaPersona(orden, persona);
                orden.Parameters.AddWithValue("$id", persona.Id);
            },
            avisos,
            persona.Id);

    /// <summary>Rellena los marcadores de una persona para INSERT y UPDATE, que comparten los 24 marcadores; el <c>$id</c> lo añade solo el UPDATE.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="persona">De dónde salen los valores.</param>
    private static void PonerLosCamposDeLaPersona(SqliteCommand orden, Persona persona)
    {
        orden.Parameters.AddWithValue("$caso", persona.CasoId);
        orden.Parameters.AddWithValue("$mrn", ONulo(persona.Mrn));
        orden.Parameters.AddWithValue("$nombre", ONulo(persona.Nombre));
        orden.Parameters.AddWithValue("$fila", ONulo(persona.FilaFormulario));

        orden.Parameters.AddWithValue("$o1", DeCasilla(persona.OrdRecibirPropias));
        orden.Parameters.AddWithValue("$o2", DeCasilla(persona.OrdObservarSellamiento));
        orden.Parameters.AddWithValue("$o3", DeCasilla(persona.OrdTraductor));
        orden.Parameters.AddWithValue("$o4", DeCasilla(persona.OrdInvestidura));
        orden.Parameters.AddWithValue("$o5", DeCasilla(persona.OrdSellamientoEsposos));
        orden.Parameters.AddWithValue("$o6", DeCasilla(persona.OrdSellamientoHijoPadres));

        orden.Parameters.AddWithValue("$pagina", ONulo(persona.PaginaPdf));
        orden.Parameters.AddWithValue("$estado", ONulo(persona.EstadoPropuesto));
        orden.Parameters.AddWithValue("$nota", ONulo(persona.NotaCompanero));
        orden.Parameters.AddWithValue("$propuestoPor", ONulo(persona.PropuestoPor));
        orden.Parameters.AddWithValue("$propuestoEn", ONulo(persona.PropuestoEn));
        orden.Parameters.AddWithValue("$motivo", ONulo(persona.MotivoNoViajo));
        orden.Parameters.AddWithValue("$pudo", DeCasilla(persona.PudoViajar));

        orden.Parameters.AddWithValue("$p1", DeCasilla(persona.PasoPreparacion));
        orden.Parameters.AddWithValue("$p2", DeCasilla(persona.PasoInformacion));
        orden.Parameters.AddWithValue("$p3", DeCasilla(persona.PasoCitaDelTemplo));
        orden.Parameters.AddWithValue("$p4", DeCasilla(persona.PasoAccionesRequeridas));
        orden.Parameters.AddWithValue("$p5", DeCasilla(persona.PasoEntrevistas));
        orden.Parameters.AddWithValue("$p6", DeCasilla(persona.PasoListoParaElTemplo));
        orden.Parameters.AddWithValue("$lider", DeCasilla(persona.LlamoAlLider));
    }

    /// <summary>Compone el WHERE del filtro con marcadores <c>$nombre</c>; el texto del usuario nunca entra aquí, solo en <c>PonerLosParametrosDelFiltro</c>.</summary>
    /// <param name="filtro">Lo que la pantalla pide.</param>
    /// <returns>Una cláusula <c>WHERE …</c>, o vacío si el filtro no dice nada.</returns>
    private static string ComponerElFiltro(FiltroDePersonas filtro)
    {
        var condiciones = new List<string>();

        if (filtro.CasoId is not null)
        {
            condiciones.Add("caso_id = $caso");
        }

        if (filtro.SinMrn)
        {
            // Las que rompen la reconciliacion del Excel que vuelve: sin MRN no hay
            // con que casar la fila, y el trabajo del companero se descarta.
            condiciones.Add("mrn IS NULL");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(
                "(nombre LIKE $texto COLLATE NOCASE OR mrn LIKE $texto COLLATE NOCASE)");
        }

        return condiciones.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condiciones);
    }

    /// <summary>Rellena los marcadores que <c>ComponerElFiltro</c> dejó, y solo esos: un parámetro sin marcador es un error del motor.</summary>
    /// <param name="orden">La orden en la que se añaden los parámetros.</param>
    /// <param name="filtro">El mismo filtro con el que se compuso el WHERE.</param>
    private static void PonerLosParametrosDelFiltro(SqliteCommand orden, FiltroDePersonas filtro)
    {
        if (filtro.CasoId is not null)
        {
            orden.Parameters.AddWithValue("$caso", filtro.CasoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            orden.Parameters.AddWithValue("$texto", "%" + filtro.Texto.Trim() + "%");
        }
    }

    /// <summary>Convierte una fila en una persona, columna por columna y en su orden.</summary>
    private static Persona Leer(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt64(0),
        CasoId = lector.GetInt64(1),
        Mrn = TextoONulo(lector, 2),
        Nombre = TextoONulo(lector, 3),
        FilaFormulario = EnteroONulo(lector, 4),
        OrdRecibirPropias = CasillaDeTresEstados(lector, 5),
        OrdObservarSellamiento = CasillaDeTresEstados(lector, 6),
        OrdTraductor = CasillaDeTresEstados(lector, 7),
        OrdInvestidura = CasillaDeTresEstados(lector, 8),
        OrdSellamientoEsposos = CasillaDeTresEstados(lector, 9),
        OrdSellamientoHijoPadres = CasillaDeTresEstados(lector, 10),
        PaginaPdf = EnteroONulo(lector, 11),
        EstadoPropuesto = TextoONulo(lector, 12),
        NotaCompanero = TextoONulo(lector, 13),
        PropuestoPor = LargoONulo(lector, 14),
        PropuestoEn = TextoONulo(lector, 15),
        MotivoNoViajo = TextoONulo(lector, 16),
        PudoViajar = CasillaDeTresEstados(lector, 17),
        PasoPreparacion = CasillaDeTresEstados(lector, 18),
        PasoInformacion = CasillaDeTresEstados(lector, 19),
        PasoCitaDelTemplo = CasillaDeTresEstados(lector, 20),
        PasoAccionesRequeridas = CasillaDeTresEstados(lector, 21),
        PasoEntrevistas = CasillaDeTresEstados(lector, 22),
        PasoListoParaElTemplo = CasillaDeTresEstados(lector, 23),
        LlamoAlLider = CasillaDeTresEstados(lector, 24),
    };
}
