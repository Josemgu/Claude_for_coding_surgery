using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Esquema;

/// <summary>
/// Las cinco migraciones que reconstruyen una tabla entera: la 2, la 7, la 12, la 15 y la 16.
/// </summary>
/// <remarks>
/// <para>
/// Cambiar un <c>CHECK</c> o quitar un <c>NOT NULL</c> o un <c>UNIQUE</c> de columna
/// no se puede hacer con <c>ALTER TABLE</c> en SQLite: hay que reconstruir. El
/// procedimiento de 12 pasos vive una sola vez, en
/// <see cref="ReconstructorDeTablas"/>; aqui van los pasos 4 a 8 de cada una.
/// </para>
/// <para>
/// En las cuatro, las columnas se nombran UNA A UNA en los dos lados del INSERT. Un
/// <c>INSERT INTO ... SELECT *</c> copiaria por posicion, y una columna anadida en
/// medio dejaria los datos corridos sin que nadie se entere. Y la columna <c>id</c> va
/// nombrada la primera: la procedencia de cada campo apunta a ella por un par
/// (tabla, registro_id) que el motor NO puede defender con una clave foranea, asi que
/// si el id cambiara, la procedencia quedaria apuntando a otra fila en silencio.
/// </para>
/// </remarks>
internal static class MigracionesQueRehacenTablas
{
    // ==================================================================
    // VERSION 2 — `unidad_numero` admite 6 O 7 digitos.
    // ==================================================================

    internal const string DescripcionDeLaVersion2 =
        "unidad_numero admite 6 o 7 digitos: se reconstruye 'casos' con el CHECK " +
        "corregido (DECISIONES.md 2026-09-02).";

    /// <summary>
    /// La tabla de la version 2: solo cambia el CHECK de <c>unidad_numero</c>.
    /// </summary>
    /// <remarks>
    /// El CHECK de seis rechazaba un dato VERDADERO: 4 de las 9 paginas reales traen
    /// <c>7000011</c>, de siete digitos. Todo lo demas se copia letra por letra de la
    /// version 1: una migracion que aprovecha para retocar otra cosa de paso es una
    /// migracion que nadie puede revisar.
    /// </remarks>
    private const string TablaNuevaDeLaVersion2 = """
        CREATE TABLE casos_version_2 (
            id                   INTEGER PRIMARY KEY,
            numero_caso          TEXT    NOT NULL UNIQUE
                CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]'),
            unidad_numero        TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje          TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual       INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado            INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado      TEXT,
            ruta_pdf             TEXT,
            creado_en            TEXT    NOT NULL,
            estado_recomendacion TEXT,

            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL))
        )
        """;

    private const string CopiaDeLosDatosALaVersion2 = """
        INSERT INTO casos_version_2
            (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion)
        SELECT
             id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion
        FROM casos
        """;

    /// <summary>Reconstruye <c>casos</c> para que <c>unidad_numero</c> admita 6 o 7 digitos.</summary>
    internal static void AVersion2(SqliteConnection conexion)
    {
        Aplicar(conexion, 2, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion2);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion2);
            Ejecutar(conexion, "DROP TABLE casos");
            Ejecutar(conexion, "ALTER TABLE casos_version_2 RENAME TO casos");
            Ejecutar(conexion,
                "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0");
        }));
    }

    // ==================================================================
    // VERSION 7 — `numero_caso` admite NULL.
    // ==================================================================

    internal const string DescripcionDeLaVersion7 =
        "'casos.numero_caso' admite NULL: una pagina cuyo numero no se pudo leer se " +
        "guarda igual, pendiente de identificar, en vez de tirarse entera.";

    /// <summary>
    /// La tabla de la version 7: <c>numero_caso</c> pierde el <c>NOT NULL</c>.
    /// </summary>
    /// <remarks>
    /// Medido en la maquina del dueno el 2026-09-02: un PDF suyo dio «0 casos de 1
    /// pagina» porque no se leyo el numero, y se tiro la pagina entera con los
    /// nombres, los MRN y la fecha YA leidos. No se inventa un numero (regla
    /// permanente 1): entra a NULL, que significa «todavia no se sabe».
    /// </remarks>
    private const string TablaNuevaDeLaVersion7 = """
        CREATE TABLE casos_version_7 (
            id                   INTEGER PRIMARY KEY,
            numero_caso          TEXT    UNIQUE
                CHECK (numero_caso IS NULL
                       OR numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]'),
            unidad_numero        TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje          TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual       INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado            INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado      TEXT,
            ruta_pdf             TEXT,
            creado_en            TEXT    NOT NULL,
            estado_recomendacion TEXT,
            pagina_pdf           INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            unidad_nombre        TEXT,

            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL))
        )
        """;

    private const string CopiaDeLosDatosALaVersion7 = """
        INSERT INTO casos_version_7
            (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre)
        SELECT
             id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre
        FROM casos
        """;

    /// <summary>Reconstruye <c>casos</c> para que <c>numero_caso</c> pueda quedarse sin saber.</summary>
    internal static void AVersion7(SqliteConnection conexion)
    {
        Aplicar(conexion, 7, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion7);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion7);
            Ejecutar(conexion, "DROP TABLE casos");
            Ejecutar(conexion, "ALTER TABLE casos_version_7 RENAME TO casos");
            Ejecutar(conexion,
                "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0");
        }));
    }

    // ==================================================================
    // VERSION 12 — `numero_caso` deja de ser UNICO, y aqui va LA desviacion.
    // ==================================================================

    internal const string DescripcionDeLaVersion12 =
        "'casos.numero_caso' deja de ser UNICO y pierde su CHECK de forma: el numero " +
        "es una unidad y un mes, no una familia, y lo que venga se guarda y se avisa " +
        "(DECISIONES.md 2026-09-04, requisito 9 del dueno).";

    /// <summary>
    /// La tabla de la version 12, con EL UNICO cambio respecto del Python.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo que quita el UNIQUE, y por que.</b> Medido por el dueno en la PC del
    /// trabajo el 2026-09-03: importo diez documentos y SEIS se rechazaron con «caso
    /// ya existente». Era otra familia. El numero es <c>BALC</c> + <c>2609</c> = una
    /// unidad y un mes, no una familia, y en su carpeta muchos documentos lo comparten
    /// por construccion. El <c>UNIQUE</c> convertia eso en tirar el documento entero.
    /// </para>
    /// <para>
    /// ⚠️ <b>Y aqui esta la UNICA diferencia deliberada con el Python.</b> El Python
    /// conserva en esta reconstruccion el <c>CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')</c>.
    /// Aqui NO se pone, por decision del dueno (DECISIONES.md 2026-09-04, «El CHECK
    /// del numero de caso» y requisito 9): se guarda lo que venga y quien avise es la
    /// pantalla. Un CHECK del motor no avisa: rechaza, y lo que rechaza se pierde.
    /// Es el mismo error que ya costo 2 de 7 cedulas y 4 de 9 unidades, y esta vez se
    /// corta de raiz en vez de aflojar el patron una vez mas.
    /// </para>
    /// <para>
    /// Los otros CHECK de coherencia se quedan todos: lo que se retira es UNO.
    /// </para>
    /// </remarks>
    private const string TablaNuevaDeLaVersion12 = """
        CREATE TABLE casos_version_12 (
            id                   INTEGER PRIMARY KEY,
            numero_caso          TEXT,
            unidad_numero        TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje          TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual       INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado            INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado      TEXT,
            ruta_pdf             TEXT,
            creado_en            TEXT    NOT NULL,
            estado_recomendacion TEXT,
            pagina_pdf           INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            unidad_nombre        TEXT,
            templo_nombre        TEXT,

            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL))
        )
        """;

    private const string CopiaDeLosDatosALaVersion12 = """
        INSERT INTO casos_version_12
            (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre, templo_nombre)
        SELECT
             id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre, templo_nombre
        FROM casos
        """;

    /// <summary>Reconstruye <c>casos</c> sin el UNIQUE y sin el CHECK de forma del numero.</summary>
    internal static void AVersion12(SqliteConnection conexion)
    {
        Aplicar(conexion, 12, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion12);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion12);
            Ejecutar(conexion, "DROP TABLE casos");
            Ejecutar(conexion, "ALTER TABLE casos_version_12 RENAME TO casos");
            Ejecutar(conexion,
                "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0");

            // Indice NO unico sobre el numero. Lo que antes daba gratis el UNIQUE
            // —encontrar un caso por su numero sin recorrer la tabla— sigue haciendo
            // falta en cada fila que vuelve del Excel del companero y en cada
            // comprobacion de duplicado, y al quitar el UNIQUE hay que reponerlo.
            Ejecutar(conexion, "CREATE INDEX idx_casos_numero ON casos (numero_caso)");
        }));
    }

    // ==================================================================
    // VERSION 15 — la cedula de miembro puede terminar en LETRA.
    // ==================================================================

    internal const string DescripcionDeLaVersion15 =
        "la cedula de miembro puede terminar en letra: se reconstruye 'personas' con " +
        "el CHECK de mrn corregido (DECISIONES.md 2026-09-04).";

    /// <summary>
    /// La tabla de la version 15: el ultimo caracter del MRN admite letra.
    /// </summary>
    /// <remarks>
    /// El 2026-09-04 el supervisor recorto del PDF la banda de la cedula en dos
    /// escaneos reales del dueno y miro la imagen: el papel dice <c>...-...-###A</c>.
    /// El OCR las leia bien; las tiraba nuestra regla de «11 digitos en patron 3-4-4».
    /// Sobre los siete PDF reales eso costaba 2 de 7 cedulas: el campo quedaba vacio y
    /// lo leido solo vivia en <c>procedencia_campo.valor_ocr</c>, donde el dueno no lo
    /// ve.
    ///
    /// Cambia UNA sola cosa: el ultimo de los cuatro caracteres del tercer grupo pasa
    /// a admitir tambien una letra. Todo lo demas se copia del DDL que la version 14
    /// dejaba, incluidas las columnas que llegaron por ALTER en las versiones 4, 5, 6
    /// y 9 —que en la base viven pegadas al final de la lista—.
    /// </remarks>
    private const string TablaNuevaDeLaVersion15 = """
        CREATE TABLE personas_version_15 (
            id                          INTEGER PRIMARY KEY,
            caso_id                     INTEGER NOT NULL
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            mrn                         TEXT
                CHECK (mrn IS NULL
                       OR mrn GLOB '[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9A-Za-z]'),
            nombre                      TEXT,
            fila_formulario             INTEGER
                CHECK (fila_formulario IS NULL OR fila_formulario >= 1),
            ord_recibir_propias         INTEGER
                CHECK (ord_recibir_propias IS NULL OR ord_recibir_propias IN (0, 1)),
            ord_observar_sellamiento    INTEGER
                CHECK (ord_observar_sellamiento IS NULL OR ord_observar_sellamiento IN (0, 1)),
            ord_traductor               INTEGER
                CHECK (ord_traductor IS NULL OR ord_traductor IN (0, 1)),
            ord_investidura             INTEGER
                CHECK (ord_investidura IS NULL OR ord_investidura IN (0, 1)),
            ord_sellamiento_esposos     INTEGER
                CHECK (ord_sellamiento_esposos IS NULL OR ord_sellamiento_esposos IN (0, 1)),
            ord_sellamiento_hijo_padres INTEGER
                CHECK (ord_sellamiento_hijo_padres IS NULL OR ord_sellamiento_hijo_padres IN (0, 1)),
            pagina_pdf                  INTEGER
                CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            estado_propuesto            TEXT,
            nota_companero              TEXT,
            propuesto_por               INTEGER
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            propuesto_en                TEXT,
            motivo_no_viajo             TEXT,
            pudo_viajar                 INTEGER
                CHECK ((pudo_viajar IS NULL OR pudo_viajar IN (0, 1))
                       AND (pudo_viajar IS 0 OR motivo_no_viajo IS NULL)
                       AND (pudo_viajar IS NOT 0 OR motivo_no_viajo IS NOT NULL)),
            paso_preparacion            INTEGER
                CHECK (paso_preparacion IS NULL OR paso_preparacion IN (0, 1)),
            paso_informacion            INTEGER
                CHECK (paso_informacion IS NULL OR paso_informacion IN (0, 1)),
            paso_cita_del_templo        INTEGER
                CHECK (paso_cita_del_templo IS NULL OR paso_cita_del_templo IN (0, 1)),
            paso_acciones_requeridas    INTEGER
                CHECK (paso_acciones_requeridas IS NULL OR paso_acciones_requeridas IN (0, 1)),
            paso_entrevistas            INTEGER
                CHECK (paso_entrevistas IS NULL OR paso_entrevistas IN (0, 1)),
            paso_listo_para_el_templo   INTEGER
                CHECK (paso_listo_para_el_templo IS NULL OR paso_listo_para_el_templo IN (0, 1)),
            llamo_al_lider              INTEGER
                CHECK (llamo_al_lider IS NULL OR llamo_al_lider IN (0, 1)),

            -- No se guarda una persona en blanco.
            CHECK (nombre IS NOT NULL OR mrn IS NOT NULL),

            -- La mitad de la clave de reconciliacion del Excel que vuelve (FASE 6).
            UNIQUE (caso_id, mrn)
        )
        """;

    private const string CopiaDeLosDatosALaVersion15 = """
        INSERT INTO personas_version_15
            (id, caso_id, mrn, nombre, fila_formulario,
             ord_recibir_propias, ord_observar_sellamiento, ord_traductor,
             ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres,
             pagina_pdf, estado_propuesto, nota_companero, propuesto_por, propuesto_en,
             motivo_no_viajo, pudo_viajar,
             paso_preparacion, paso_informacion, paso_cita_del_templo,
             paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo,
             llamo_al_lider)
        SELECT
             id, caso_id, mrn, nombre, fila_formulario,
             ord_recibir_propias, ord_observar_sellamiento, ord_traductor,
             ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres,
             pagina_pdf, estado_propuesto, nota_companero, propuesto_por, propuesto_en,
             motivo_no_viajo, pudo_viajar,
             paso_preparacion, paso_informacion, paso_cita_del_templo,
             paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo,
             llamo_al_lider
        FROM personas
        """;

    /// <summary>Reconstruye <c>personas</c> para que <c>mrn</c> admita una letra al final.</summary>
    internal static void AVersion15(SqliteConnection conexion)
    {
        Aplicar(conexion, 15, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion15);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion15);
            Ejecutar(conexion, "DROP TABLE personas");
            Ejecutar(conexion, "ALTER TABLE personas_version_15 RENAME TO personas");
            Ejecutar(conexion, "CREATE INDEX idx_personas_caso ON personas (caso_id)");
        }));
    }

    // ==================================================================
    // VERSION 16 — quita el CHECK del numero de caso venga la base de donde venga.
    // ==================================================================

    internal const string DescripcionDeLaVersion16 =
        "'casos.numero_caso' pierde su CHECK de forma tambien en las bases que ya " +
        "paso el Python: la 12 del Python lo conservaba y la del C# no, asi que dos " +
        "bases al dia aceptaban cosas distintas (DECISIONES.md 2026-09-04).";

    /// <summary>
    /// La tabla <c>casos</c> al dia, sin el <c>CHECK</c> de forma del numero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que hace falta una migracion NUEVA y no arreglar la 12.</b> El aplicador
    /// solo ejecuta una migracion cuyo numero sea MAYOR que el que la base ya tiene
    /// (<see cref="AplicadorDeEsquema.AplicarHasta"/>). La base del dueno registro la 12
    /// el 2026-09-03 con el Python, asi que la 12 del C# no volvera a ejecutarse sobre
    /// ella NUNCA, se reescriba como se reescriba. Hacerla «idempotente» exigiria
    /// reaplicar migraciones ya registradas, y eso reconstruiria tablas en cada arranque.
    /// La 12 se queda como esta —una migracion aplicada no se reescribe— y la correccion
    /// entra por la primera version libre.
    /// </para>
    /// <para>
    /// <b>Medido el 2026-09-04 sobre una copia de <c>C:\Users\josem\Documents\Fichas\fichas.db</c></b>
    /// (nunca sobre la original): la base venia en la version 13 y su <c>casos</c> traia
    /// <c>CHECK (numero_caso IS NULL OR numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')</c>.
    /// Al llevarla a la 15 el CHECK seguia dentro, e insertar <c>CASP26O9</c> —una letra O
    /// donde va un cero, el error tipico del OCR— devolvia «SQLite Error 19: CHECK
    /// constraint failed». En una base nueva del C# esa misma fila entra. El dueno decidio
    /// que se guarde y se senale en la pantalla, nunca que se rechace.
    /// </para>
    /// <para>
    /// Se nombran las 20 columnas que <c>casos</c> tiene en la version 15: las 13 de la
    /// reconstruccion de la 12, <c>duplicado_de</c> de la 13 y las seis del estado de la
    /// 14. Se retira UN CHECK y nada mas; los demas se copian letra por letra. Los dos del
    /// estado —que la 14 escribio pegados a su columna porque un <c>ALTER TABLE ADD
    /// COLUMN</c> no sabe escribir otra cosa— quedan aqui como CHECK de tabla: para el
    /// motor es lo mismo, y ademas cada uno nombra dos columnas, que es donde vive la
    /// regla de verdad.
    /// </para>
    /// </remarks>
    private const string TablaNuevaDeLaVersion16 = """
        CREATE TABLE casos_version_16 (
            id                       INTEGER PRIMARY KEY,
            numero_caso              TEXT,
            unidad_numero            TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje              TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual           INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado                INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado          TEXT,
            ruta_pdf                 TEXT,
            creado_en                TEXT    NOT NULL,
            estado_recomendacion     TEXT,
            pagina_pdf               INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            unidad_nombre            TEXT,
            templo_nombre            TEXT,
            duplicado_de             INTEGER
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            estado_marcado_por       INTEGER
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            estado_marcado_en        TEXT,
            estado_marcado_origen    TEXT,
            estado_del_companero     TEXT,
            estado_del_companero_por INTEGER
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            estado_del_companero_en  TEXT,

            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL)),

            CHECK ((estado_marcado_por IS NULL AND estado_marcado_en IS NULL)
                   OR (estado_marcado_por IS NOT NULL AND estado_marcado_en IS NOT NULL)),

            CHECK ((estado_del_companero_por IS NULL AND estado_del_companero_en IS NULL)
                   OR (estado_del_companero_por IS NOT NULL
                       AND estado_del_companero_en IS NOT NULL))
        )
        """;

    private const string CopiaDeLosDatosALaVersion16 = """
        INSERT INTO casos_version_16
            (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre, templo_nombre, duplicado_de,
             estado_marcado_por, estado_marcado_en, estado_marcado_origen,
             estado_del_companero, estado_del_companero_por, estado_del_companero_en)
        SELECT
             id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
             fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
             unidad_nombre, templo_nombre, duplicado_de,
             estado_marcado_por, estado_marcado_en, estado_marcado_origen,
             estado_del_companero, estado_del_companero_por, estado_del_companero_en
        FROM casos
        """;

    /// <summary>Reconstruye <c>casos</c> sin el CHECK del numero, venga de donde venga la base.</summary>
    internal static void AVersion16(SqliteConnection conexion)
    {
        Aplicar(conexion, 16, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion16);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion16);
            Ejecutar(conexion, "DROP TABLE casos");
            Ejecutar(conexion, "ALTER TABLE casos_version_16 RENAME TO casos");
            Ejecutar(conexion,
                "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0");
            Ejecutar(conexion, "CREATE INDEX idx_casos_numero ON casos (numero_caso)");
        }));
    }

    // ==================================================================
    // VERSION 17 — la cedula de miembro se guarda como venga y se senala.
    // ==================================================================

    internal const string DescripcionDeLaVersion17 =
        "'personas.mrn' pierde su CHECK de forma: una cedula mal leida se guarda y se " +
        "senala en la pantalla, no se rechaza. Lo que un CHECK tira aqui no es un dato, " +
        "es la fila de una persona (DECISIONES.md 2026-09-04, requisito 9).";

    /// <summary>
    /// La tabla <c>personas</c> al dia, sin el <c>CHECK</c> de forma del MRN.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Medido por QA el 2026-09-04</b> insertando en una copia de la base: con el CHECK
    /// de la version 15 puesto, <c>066-2222-133A</c> entra, y <c>123</c> y <c>''</c>
    /// devuelven «CHECK constraint failed». Es el mismo caso por el que el dueno mando
    /// quitar el CHECK del numero de caso (migracion 16), y aqui pesa mas: lo que se
    /// pierde no es un numero, es una PERSONA que iba a viajar.
    /// </para>
    /// <para>
    /// <b>Por que una migracion nueva y no arreglar la 15.</b> Lo mismo que dijo la 16:
    /// el aplicador solo ejecuta migraciones con numero MAYOR que el de la base, asi que
    /// la 15 no se volvera a ejecutar sobre ninguna base que ya la registro —y la del
    /// dueno la registro el 2026-09-04 18:05—. Una migracion aplicada no se reescribe.
    /// </para>
    /// <para>
    /// <b>Se retira UN CHECK y ni uno mas.</b> Los otros dieciseis se copian letra por
    /// letra, incluido <c>CHECK (nombre IS NOT NULL OR mrn IS NOT NULL)</c>: el requisito
    /// 9 habla de un valor RARO, que se guarda y se senala; una fila sin nombre y sin
    /// cedula no tiene nada que senalar en pantalla. El <c>UNIQUE (caso_id, mrn)</c>
    /// tambien se queda: es la mitad de la clave con la que vuelve el Excel del companero.
    /// </para>
    /// </remarks>
    private const string TablaNuevaDeLaVersion17 = """
        CREATE TABLE personas_version_17 (
            id                          INTEGER PRIMARY KEY,
            caso_id                     INTEGER NOT NULL
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            mrn                         TEXT,
            nombre                      TEXT,
            fila_formulario             INTEGER
                CHECK (fila_formulario IS NULL OR fila_formulario >= 1),
            ord_recibir_propias         INTEGER
                CHECK (ord_recibir_propias IS NULL OR ord_recibir_propias IN (0, 1)),
            ord_observar_sellamiento    INTEGER
                CHECK (ord_observar_sellamiento IS NULL OR ord_observar_sellamiento IN (0, 1)),
            ord_traductor               INTEGER
                CHECK (ord_traductor IS NULL OR ord_traductor IN (0, 1)),
            ord_investidura             INTEGER
                CHECK (ord_investidura IS NULL OR ord_investidura IN (0, 1)),
            ord_sellamiento_esposos     INTEGER
                CHECK (ord_sellamiento_esposos IS NULL OR ord_sellamiento_esposos IN (0, 1)),
            ord_sellamiento_hijo_padres INTEGER
                CHECK (ord_sellamiento_hijo_padres IS NULL OR ord_sellamiento_hijo_padres IN (0, 1)),
            pagina_pdf                  INTEGER
                CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
            estado_propuesto            TEXT,
            nota_companero              TEXT,
            propuesto_por               INTEGER
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            propuesto_en                TEXT,
            motivo_no_viajo             TEXT,
            pudo_viajar                 INTEGER
                CHECK ((pudo_viajar IS NULL OR pudo_viajar IN (0, 1))
                       AND (pudo_viajar IS 0 OR motivo_no_viajo IS NULL)
                       AND (pudo_viajar IS NOT 0 OR motivo_no_viajo IS NOT NULL)),
            paso_preparacion            INTEGER
                CHECK (paso_preparacion IS NULL OR paso_preparacion IN (0, 1)),
            paso_informacion            INTEGER
                CHECK (paso_informacion IS NULL OR paso_informacion IN (0, 1)),
            paso_cita_del_templo        INTEGER
                CHECK (paso_cita_del_templo IS NULL OR paso_cita_del_templo IN (0, 1)),
            paso_acciones_requeridas    INTEGER
                CHECK (paso_acciones_requeridas IS NULL OR paso_acciones_requeridas IN (0, 1)),
            paso_entrevistas            INTEGER
                CHECK (paso_entrevistas IS NULL OR paso_entrevistas IN (0, 1)),
            paso_listo_para_el_templo   INTEGER
                CHECK (paso_listo_para_el_templo IS NULL OR paso_listo_para_el_templo IN (0, 1)),
            llamo_al_lider              INTEGER
                CHECK (llamo_al_lider IS NULL OR llamo_al_lider IN (0, 1)),

            -- No se guarda una persona en blanco.
            CHECK (nombre IS NOT NULL OR mrn IS NOT NULL),

            -- La mitad de la clave de reconciliacion del Excel que vuelve (FASE 6).
            UNIQUE (caso_id, mrn)
        )
        """;

    private const string CopiaDeLosDatosALaVersion17 = """
        INSERT INTO personas_version_17
            (id, caso_id, mrn, nombre, fila_formulario,
             ord_recibir_propias, ord_observar_sellamiento, ord_traductor,
             ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres,
             pagina_pdf, estado_propuesto, nota_companero, propuesto_por, propuesto_en,
             motivo_no_viajo, pudo_viajar,
             paso_preparacion, paso_informacion, paso_cita_del_templo,
             paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo,
             llamo_al_lider)
        SELECT
             id, caso_id, mrn, nombre, fila_formulario,
             ord_recibir_propias, ord_observar_sellamiento, ord_traductor,
             ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres,
             pagina_pdf, estado_propuesto, nota_companero, propuesto_por, propuesto_en,
             motivo_no_viajo, pudo_viajar,
             paso_preparacion, paso_informacion, paso_cita_del_templo,
             paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo,
             llamo_al_lider
        FROM personas
        """;

    /// <summary>Reconstruye <c>personas</c> sin el CHECK de forma del MRN.</summary>
    internal static void AVersion17(SqliteConnection conexion)
    {
        Aplicar(conexion, 17, () => ReconstructorDeTablas.Reconstruir(conexion, _ =>
        {
            Ejecutar(conexion, TablaNuevaDeLaVersion17);
            Ejecutar(conexion, CopiaDeLosDatosALaVersion17);
            Ejecutar(conexion, "DROP TABLE personas");
            Ejecutar(conexion, "ALTER TABLE personas_version_17 RENAME TO personas");
            Ejecutar(conexion, "CREATE INDEX idx_personas_caso ON personas (caso_id)");
        }));
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
