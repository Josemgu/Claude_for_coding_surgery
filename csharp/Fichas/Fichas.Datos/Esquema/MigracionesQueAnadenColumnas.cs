using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// Las migraciones que solo anaden columnas: la 3, 4, 5, 6, 9, 10, 13, 14, 18 y 19.
/// </summary>
/// <remarks>
/// Todas van con <c>ALTER TABLE ... ADD COLUMN</c> y sin transaccion explicita: cada
/// instruccion suelta ya es atomica para el motor, y envolverla en <c>BEGIN</c> /
/// <c>COMMIT</c> no anadiria garantia ninguna. Las que reconstruyen una tabla —2, 7,
/// 12 y 15— viven en <see cref="MigracionesQueRehacenTablas"/>, que es otro trabajo.
/// </remarks>
internal static class MigracionesQueAnadenColumnas
{
    // ==================================================================
    // VERSION 3 — los huecos que tapaban la pantalla de correccion.
    // ==================================================================

    internal const string DescripcionDeLaVersion3 =
        "Los huecos que tapaban la pantalla de correccion: 'casos' gana 'pagina_pdf' " +
        "y 'unidad_nombre', y 'procedencia_campo' gana las cuatro coordenadas de la " +
        "banda del escaneo y la marca de tachon.";

    /// <summary>
    /// Anade las siete columnas de la version 3.
    /// </summary>
    /// <remarks>
    /// La banda va en FRACCIONES de pagina y no en pixeles: un pixel depende de la
    /// escala con la que se rasterizo ese dia, y una fraccion sigue valiendo a
    /// cualquier escala. El tachon no es lo mismo que «vacio»: el papel llevaba algo
    /// escrito y alguien lo tacho.
    /// </remarks>
    internal static void AVersion3(SqliteConnection conexion)
    {
        Aplicar(conexion, 3, () =>
        {
            Ejecutar(conexion,
                "ALTER TABLE casos ADD COLUMN pagina_pdf INTEGER " +
                "CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)");
            Ejecutar(conexion, "ALTER TABLE casos ADD COLUMN unidad_nombre TEXT");

            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN banda_x0 REAL " +
                "CHECK (banda_x0 IS NULL OR (banda_x0 >= 0.0 AND banda_x0 <= 1.0))");
            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN banda_y0 REAL " +
                "CHECK (banda_y0 IS NULL OR (banda_y0 >= 0.0 AND banda_y0 <= 1.0))");
            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN banda_x1 REAL " +
                "CHECK (banda_x1 IS NULL OR (banda_x1 >= 0.0 AND banda_x1 <= 1.0))");
            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN banda_y1 REAL " +
                "CHECK (banda_y1 IS NULL OR (banda_y1 >= 0.0 AND banda_y1 <= 1.0))");
            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN anulado_por_tachon INTEGER " +
                "NOT NULL DEFAULT 0 CHECK (anulado_por_tachon IN (0, 1))");
        });
    }

    // ==================================================================
    // VERSION 4 — de que hoja salio cada persona.
    // ==================================================================

    internal const string DescripcionDeLaVersion4 =
        "'personas' gana 'pagina_pdf': cada persona sabe de que hoja del PDF salio, " +
        "y su tira del escaneo se recorta de esa hoja y no de la del caso.";

    /// <summary>
    /// Anade <c>personas.pagina_pdf</c>.
    /// </summary>
    /// <remarks>
    /// Desde que las paginas de un grupo se unen en un solo caso, un caso puede tener
    /// 12 personas en 6 hojas. Recortar las 12 tiras de la hoja del caso ensenaba la
    /// fila de OTRA persona al lado del MRN.
    /// </remarks>
    internal static void AVersion4(SqliteConnection conexion)
    {
        Aplicar(conexion, 4, () => Ejecutar(conexion,
            "ALTER TABLE personas ADD COLUMN pagina_pdf INTEGER " +
            "CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)"));
    }

    // ==================================================================
    // VERSION 5 — lo que las FASES 6 y 7 no tenian donde guardar.
    // ==================================================================

    internal const string DescripcionDeLaVersion5 =
        "Lo que las FASES 6 y 7 no tenian donde guardar: 'personas' gana las cuatro " +
        "columnas de la propuesta del companero, y 'contactos' gana quien contacto y " +
        "si respondio.";

    /// <summary>
    /// Anade las seis columnas de la version 5.
    /// </summary>
    /// <remarks>
    /// La propuesta del companero va en columnas propias y NO en
    /// <c>procedencia_campo</c>: escribir alli un <c>verificado_por</c> obliga a
    /// <c>verificado = 1</c>, y eso es marcar como verificado automaticamente, que es
    /// justo lo que la regla permanente 5 prohibe.
    /// </remarks>
    internal static void AVersion5(SqliteConnection conexion)
    {
        Aplicar(conexion, 5, () =>
        {
            Ejecutar(conexion, "ALTER TABLE personas ADD COLUMN estado_propuesto TEXT");
            Ejecutar(conexion, "ALTER TABLE personas ADD COLUMN nota_companero TEXT");
            Ejecutar(conexion,
                "ALTER TABLE personas ADD COLUMN propuesto_por INTEGER " +
                "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT");
            Ejecutar(conexion, "ALTER TABLE personas ADD COLUMN propuesto_en TEXT");

            Ejecutar(conexion,
                "ALTER TABLE contactos ADD COLUMN contactado_por INTEGER " +
                "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT");
            Ejecutar(conexion,
                "ALTER TABLE contactos ADD COLUMN respondio INTEGER " +
                "CHECK (respondio IS NULL OR respondio IN (0, 1))");
        });
    }

    // ==================================================================
    // VERSION 6 — quien no pudo viajar, y por que.
    // ==================================================================

    internal const string DescripcionDeLaVersion6 =
        "'personas' gana 'pudo_viajar' y 'motivo_no_viajo': el reporte de la FASE 8 " +
        "tiene que decir quien no pudo viajar y por que, y no habia donde guardarlo.";

    /// <summary>
    /// Anade <c>motivo_no_viajo</c> y <c>pudo_viajar</c> a <c>personas</c>.
    /// </summary>
    /// <remarks>
    /// El orden importa: <c>pudo_viajar</c> lleva un CHECK que nombra a
    /// <c>motivo_no_viajo</c>, asi que la otra columna tiene que existir ya.
    /// El reporte habla de PERSONAS y no de casos: en <c>casos</c>, una familia de
    /// cuatro donde falla uno saldria como cuatro personas con el motivo copiado.
    /// </remarks>
    internal static void AVersion6(SqliteConnection conexion)
    {
        Aplicar(conexion, 6, () =>
        {
            Ejecutar(conexion, "ALTER TABLE personas ADD COLUMN motivo_no_viajo TEXT");
            Ejecutar(conexion,
                "ALTER TABLE personas ADD COLUMN pudo_viajar INTEGER " +
                "CHECK ((pudo_viajar IS NULL OR pudo_viajar IN (0, 1)) " +
                "AND (pudo_viajar IS 0 OR motivo_no_viajo IS NULL) " +
                "AND (pudo_viajar IS NOT 0 OR motivo_no_viajo IS NOT NULL))");
        });
    }

    // ==================================================================
    // VERSION 9 — los seis pasos del sistema del lider.
    // ==================================================================

    internal const string DescripcionDeLaVersion9 =
        "'personas' gana los seis pasos del sistema del lider y si el companero " +
        "llamo al lider: un solo estado dice que no esta lista, los seis pasos dicen " +
        "en cual se quedo.";

    /// <summary>
    /// Anade los seis pasos y <c>llamo_al_lider</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Los seis <c>paso_*</c> NO son las seis <c>ord_*</c> y no se mezclan: las
    /// <c>ord_*</c> son a que va la persona al templo, leidas del papel; los
    /// <c>paso_*</c> son los pasos del sistema del lider que devuelve el companero.
    /// Y <c>llamo_al_lider</c> NO es un septimo paso.
    /// </remarks>
    internal static void AVersion9(SqliteConnection conexion)
    {
        Aplicar(conexion, 9, () =>
        {
            AnadirCasillaAPersonas(conexion, "paso_preparacion");
            AnadirCasillaAPersonas(conexion, "paso_informacion");
            AnadirCasillaAPersonas(conexion, "paso_cita_del_templo");
            AnadirCasillaAPersonas(conexion, "paso_acciones_requeridas");
            AnadirCasillaAPersonas(conexion, "paso_entrevistas");
            AnadirCasillaAPersonas(conexion, "paso_listo_para_el_templo");
            AnadirCasillaAPersonas(conexion, "llamo_al_lider");
        });
    }

    // ==================================================================
    // VERSION 10 — el nombre del templo.
    // ==================================================================

    internal const string DescripcionDeLaVersion10 =
        "'casos.templo_nombre': el templo esta impreso en el formulario y hasta ese " +
        "dia se descartaba; la cabecera del Excel del agente lo pide.";

    /// <summary>
    /// Anade <c>casos.templo_nombre</c>.
    /// </summary>
    /// <remarks>
    /// Texto libre y SIN catalogo: la lista de templos con su color es decision del
    /// dueno y no la ha tomado (P-10).
    /// </remarks>
    internal static void AVersion10(SqliteConnection conexion)
    {
        Aplicar(conexion, 10, () => Ejecutar(conexion,
            "ALTER TABLE casos ADD COLUMN templo_nombre TEXT"));
    }

    // ==================================================================
    // VERSION 13 — el duplicado entra y queda marcado.
    // ==================================================================

    internal const string DescripcionDeLaVersion13 =
        "'casos.duplicado_de': un documento que repite a otro ENTRA igual y queda " +
        "marcado con el caso del que es duplicado. Nunca se pisa el que ya estaba.";

    /// <summary>
    /// Anade <c>casos.duplicado_de</c>.
    /// </summary>
    /// <remarks>
    /// Va con <c>ALTER TABLE ADD COLUMN</c> y no reconstruyendo, porque la
    /// documentacion oficial de SQLite
    /// (<see href="https://www.sqlite.org/lang_altertable.html"/>, «ALTER TABLE ADD
    /// COLUMN», consultada 2026-09-04) lo admite para una columna con
    /// <c>REFERENCES</c> con una condicion que aqui se cumple: su valor por defecto
    /// tiene que ser NULL. Y aqui lo es, que ademas es lo correcto: un caso que no
    /// repite a ninguno no tiene nada que decir.
    /// </remarks>
    internal static void AVersion13(SqliteConnection conexion)
    {
        Aplicar(conexion, 13, () => Ejecutar(conexion,
            "ALTER TABLE casos ADD COLUMN duplicado_de INTEGER " +
            "REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT"));
    }

    // ==================================================================
    // VERSION 14 — quien marco el estado, y lo que dijo el companero.
    // ==================================================================

    internal const string DescripcionDeLaVersion14 =
        "'casos' gana quien marco el estado y cuando, y aparte lo que dijo la hoja " +
        "del companero, que no se borra cuando Miguel corrige encima; " +
        "'procedencia_campo' gana 'ausente_en_el_papel'.";

    /// <summary>
    /// Anade las siete columnas de la version 14.
    /// </summary>
    /// <remarks>
    /// Los dos grupos de tres estan duplicados A PROPOSITO: uno guarda quien puso el
    /// estado que vale AHORA, y el otro lo que dijo la hoja del companero, que no se
    /// borra cuando Miguel corrige encima. Con un solo grupo la correccion pisaria el
    /// nombre del companero y nadie sabria que discreparon.
    ///
    /// <c>ausente_en_el_papel</c> es la misma forma que <c>anulado_por_tachon</c> de
    /// la v3 y por el mismo motivo: sin ella, «no esta en el papel» se ve igual que
    /// «el OCR no supo leerlo».
    /// </remarks>
    internal static void AVersion14(SqliteConnection conexion)
    {
        Aplicar(conexion, 14, () =>
        {
            Ejecutar(conexion,
                "ALTER TABLE casos ADD COLUMN estado_marcado_por INTEGER " +
                "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT");
            Ejecutar(conexion,
                "ALTER TABLE casos ADD COLUMN estado_marcado_en TEXT CHECK ( " +
                " (estado_marcado_por IS NULL AND estado_marcado_en IS NULL) " +
                " OR (estado_marcado_por IS NOT NULL AND estado_marcado_en IS NOT NULL))");
            Ejecutar(conexion, "ALTER TABLE casos ADD COLUMN estado_marcado_origen TEXT");

            Ejecutar(conexion, "ALTER TABLE casos ADD COLUMN estado_del_companero TEXT");
            Ejecutar(conexion,
                "ALTER TABLE casos ADD COLUMN estado_del_companero_por INTEGER " +
                "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT");
            Ejecutar(conexion,
                "ALTER TABLE casos ADD COLUMN estado_del_companero_en TEXT CHECK ( " +
                " (estado_del_companero_por IS NULL AND estado_del_companero_en IS NULL) " +
                " OR (estado_del_companero_por IS NOT NULL " +
                "     AND estado_del_companero_en IS NOT NULL))");

            Ejecutar(conexion,
                "ALTER TABLE procedencia_campo ADD COLUMN ausente_en_el_papel INTEGER " +
                "NOT NULL DEFAULT 0 CHECK (ausente_en_el_papel IN (0, 1))");
        });
    }

    // ==================================================================
    // VERSION 18 — el vocabulario del trabajo nuevo: por que no esta completa,
    //              y en que peldano de la escalera esta cada companero.
    // ==================================================================

    internal const string DescripcionDeLaVersion18 =
        "'casos' gana POR QUE no esta completa, en dos columnas: la que vale ahora y la " +
        "que dijo la hoja del companero, que no se borra cuando Miguel corrige encima; " +
        "'companeros' gana su rol y su categoria, el peldano de la escalera al que sube " +
        "un caso cuando en el suyo no se consiguio hablar con el lider.";

    /// <summary>
    /// Anade las cuatro columnas de la version 18.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los dos motivos.</b> El dueno pidio tres estados para el calendario: <i>«no
    /// completado, no se pudo comunicar con el lider, o el lider no lo hizo»</i>. El
    /// primero YA existe —es <c>estado_recomendacion = 'no_completa'</c>—; los otros dos
    /// no son estados alternativos, son el MOTIVO de ese: un caso que no esta completo
    /// porque no se pudo hablar con el lider sigue estando no completo. Son dos ejes y
    /// por eso son dos columnas y no un enumerado mas grande (ADR-0005 §3).
    /// </para>
    /// <para>
    /// Y van dos motivos y no uno por el mismo motivo por el que la version 14 desdoblo
    /// el estado: <c>motivo_no_completa</c> es el que vale ahora y
    /// <c>motivo_del_companero</c> es lo que dijo su hoja, que NO se borra cuando Miguel
    /// corrige encima. De ese segundo se alimenta el reporte del gerente; con una sola
    /// columna, la primera correccion lo dejaria en blanco.
    /// </para>
    /// <para>
    /// La lista se guarda en CLAVE y no en prosa (<c>no_se_pudo_comunicar</c>, no «No se
    /// pudo comunicar con el lider»): la prosa es de la pantalla, y asi se puede cambiar
    /// la redaccion sin migrar datos.
    /// </para>
    /// <para>
    /// <b>El rol y la categoria son dos ejes, y hacen falta los dos.</b> El rol tiene
    /// lista cerrada de tres —<c>companero</c>, <c>gerente</c>, <c>administrador</c>— y
    /// existe para contestar «quien es el administrador» sin adivinarlo. La categoria es
    /// un NUMERO sin tope arriba porque los peldanos los pone el dueno y no el programa:
    /// <i>«los agentes categoria 1 no pudieron comunicarse con los lideres, debo pasarlo
    /// a los agentes de categoria 2; si los de categoria 2 no pudieron, a los de
    /// categoria 3. Asi puedes agregarle a los gerentes categoria y a los agentes
    /// categorias»</i>. Con solo el rol, un cuarto peldano necesitaria otra migracion.
    /// </para>
    /// <para>
    /// ⚠️ <b>Cuatro <c>ALTER TABLE ADD COLUMN</c> y ninguna reconstruccion</b>, asi que
    /// esta migracion puede ir despues de las que rehacen tablas sin ordenar nada.
    /// <c>https://sqlite.org/lang_altertable.html</c> (consultada el 2026-09-05) solo
    /// prohibe a una columna anadida ser PRIMARY KEY o UNIQUE, y exige valor por defecto
    /// no nulo si es NOT NULL: las cuatro lo cumplen. Los dos motivos entran como NULL,
    /// que su clausula admite; el rol entra con <c>'companero'</c> y la categoria con 1,
    /// que estan dentro de sus listas.
    /// </para>
    /// </remarks>
    internal static void AVersion18(SqliteConnection conexion)
    {
        Aplicar(conexion, 18, () =>
        {
            AnadirMotivoACasos(conexion, "motivo_no_completa");
            AnadirMotivoACasos(conexion, "motivo_del_companero");

            Ejecutar(conexion,
                "ALTER TABLE companeros ADD COLUMN rol TEXT NOT NULL DEFAULT 'companero' " +
                "CHECK (rol IN ('companero', 'gerente', 'administrador'))");

            // Sin tope arriba a proposito: el numero de peldanos lo pone el dueno. El
            // CHECK solo impide un peldano anterior al primero, que no significa nada.
            Ejecutar(conexion,
                "ALTER TABLE companeros ADD COLUMN categoria INTEGER NOT NULL DEFAULT 1 " +
                "CHECK (categoria >= 1)");
        });
    }

    // ==================================================================
    // VERSION 19 — quien contesto las seis preguntas del sistema del lider,
    //              cuando y desde donde.
    // ==================================================================

    internal const string DescripcionDeLaVersion19 =
        "'personas' gana quien contesto sus seis pasos, cuando y por que via: la misma " +
        "forma que la 14 le dio al estado de 'casos', y por el mismo motivo. El estado NO " +
        "se guarda: se deriva de los seis pasos cada vez que se pregunta.";

    /// <summary>
    /// Anade las tres columnas de la version 19.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo que se guarda es la FIRMA, y no el veredicto.</b> Si una persona esta lista
    /// para viajar sale de sus seis <c>paso_*</c> cada vez que se pregunta (ADR-0006 §2.4).
    /// Medido por el planificador el 2026-09-05 sobre 3 000 casos y 16 500 personas: guardar
    /// el estado en una tabla propia era 2,7 ms MAS lento en la consulta del mes y ocupaba
    /// un 18,7 % mas, y ademas se separaba de su evidencia en silencio —una persona con la
    /// quinta pregunta en «no» y la tabla diciendo que estaba lista—. Un veredicto guardado
    /// puede mentir; uno derivado no puede.
    /// </para>
    /// <para>
    /// <b>Por que no se reaprovechan <c>propuesto_por</c> y <c>propuesto_en</c>.</b> Aquellas
    /// son del <c>estado_propuesto</c> que escribe el Excel del companero. Estas son de las
    /// seis preguntas. Pueden venir de personas distintas el mismo dia —Sandy contesto por
    /// su hoja y Miguel entro despues en la pantalla—, y con un solo par de columnas la
    /// segunda respuesta pisaria el nombre de la primera sin dejar rastro.
    /// </para>
    /// <para>
    /// <c>pasos_origen</c> es texto libre y SIN catalogo, igual que
    /// <c>estado_marcado_origen</c> de la 14: ahi caben la ruta del Excel que trajo las
    /// respuestas, «a mano en la pantalla» y «el administrador lo hizo», y manana una cuarta
    /// via sin migrar nada.
    /// </para>
    /// <para>
    /// ⚠️ <b>El orden importa:</b> el CHECK de <c>pasos_en</c> nombra a <c>pasos_por</c>, asi
    /// que esa columna tiene que existir ya. Es el mismo cuidado que tuvo la version 6 con
    /// <c>pudo_viajar</c> y <c>motivo_no_viajo</c>.
    /// </para>
    /// <para>
    /// ⚠️ <b>Tres <c>ALTER TABLE ADD COLUMN</c> y ninguna reconstruccion</b>, asi que puede
    /// ir despues de las que rehacen tablas sin ordenar nada.
    /// <c>https://sqlite.org/lang_altertable.html</c> (consultada el 2026-09-05) solo prohibe
    /// a una columna anadida ser PRIMARY KEY o UNIQUE, exige valor por defecto no nulo si es
    /// NOT NULL, y admite <c>REFERENCES</c> siempre que ese valor por defecto sea NULL: las
    /// tres lo cumplen porque las tres entran en NULL, que es lo correcto —una persona de la
    /// que nadie ha contestado nada no tiene firma que ensenar—.
    /// </para>
    /// <para>
    /// ⛔ <b>Esto no firma ni un campo.</b> <c>procedencia_campo.verificado</c> es de Miguel,
    /// campo por campo, y nunca automatica (regla permanente 5, precisada por el dueno el
    /// 2026-09-03). Contestar las seis preguntas del sistema del lider es otra cosa y esta
    /// migracion no toca aquella tabla.
    /// </para>
    /// </remarks>
    internal static void AVersion19(SqliteConnection conexion)
    {
        Aplicar(conexion, 19, () =>
        {
            Ejecutar(conexion,
                "ALTER TABLE personas ADD COLUMN pasos_por INTEGER " +
                "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT");

            // O las dos, o ninguna: una fecha sin nombre no dice de quien fiarse, y un
            // nombre sin fecha no dice si su respuesta es anterior o posterior a la del
            // companero. Es el mismo CHECK que la 14 le puso a `estado_marcado_en`.
            Ejecutar(conexion,
                "ALTER TABLE personas ADD COLUMN pasos_en TEXT CHECK ( " +
                " (pasos_por IS NULL AND pasos_en IS NULL) " +
                " OR (pasos_por IS NOT NULL AND pasos_en IS NOT NULL))");

            Ejecutar(conexion, "ALTER TABLE personas ADD COLUMN pasos_origen TEXT");
        });
    }

    /// <summary>Una de las dos columnas de motivo, con su lista cerrada de tres claves.</summary>
    private static void AnadirMotivoACasos(SqliteConnection conexion, string columna)
    {
        // `columna` NO viene de fuera: sale de las dos llamadas literales de este mismo
        // archivo. Se compone aqui porque las dos instrucciones son identicas salvo el
        // nombre, y escribirlas a mano invita a que una se quede sin su CHECK.
        Ejecutar(conexion,
            $"ALTER TABLE casos ADD COLUMN {columna} TEXT " +
            $"CHECK ({columna} IS NULL OR {columna} IN " +
            "('no_se_pudo_comunicar', 'el_lider_no_lo_hizo', 'otra_razon'))");
    }

    /// <summary>Una casilla de tres estados: si, no, o nadie lo miro.</summary>
    private static void AnadirCasillaAPersonas(SqliteConnection conexion, string columna)
    {
        // `columna` NO viene de fuera: sale de las llamadas literales de este mismo
        // archivo. Se compone aqui porque las siete instrucciones son identicas salvo
        // el nombre, y escribirlas a mano invita a que una se quede sin su CHECK.
        Ejecutar(conexion,
            $"ALTER TABLE personas ADD COLUMN {columna} INTEGER " +
            $"CHECK ({columna} IS NULL OR {columna} IN (0, 1))");
    }

    /// <summary>Ejecuta los pasos de una migracion traduciendo el fallo del motor.</summary>
    private static void Aplicar(SqliteConnection conexion, int version, Action pasos)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        try
        {
            pasos();
        }
        catch (SqliteException causa)
        {
            throw new ErrorDeMigracion(
                $"No se pudo migrar la base a la version {version}: {causa.Message}. " +
                "La base se queda en la version anterior y no se perdio ningun dato.",
                causa);
        }
    }

    private static void Ejecutar(SqliteConnection conexion, string instruccion)
        => ReconstructorDeTablas.Ejecutar(conexion, instruccion);
}
