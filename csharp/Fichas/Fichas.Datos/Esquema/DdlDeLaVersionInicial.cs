namespace Fichas.Datos.Esquema;

/// <summary>
/// El DDL con el que nacio la base: la version 1, congelada.
/// </summary>
/// <remarks>
/// <para>
/// Portado letra por letra de <c>datos/esquema.py</c> (<c>_DDL_VERSION_1</c>). Es un
/// artefacto congelado: una migracion futura anade otra constante en vez de tocar
/// esta. Si se retoca, una base vieja deja de poder reconstruirse igual que se
/// construyo, y las migraciones dejan de significar lo que dicen.
/// </para>
/// <para>
/// ⚠️ <c>numero_caso</c> nace aqui con <c>NOT NULL UNIQUE</c> y con el CHECK de cuatro
/// letras y cuatro digitos. Las tres cosas se van: el NOT NULL en la version 7, el
/// UNIQUE en la 12 y el CHECK en la reconstruccion de la 12 (decision del dueno del
/// 2026-09-04). Nacen aqui porque asi nacio la base del dueno y las migraciones
/// tienen que partir de lo que hay, no de lo que quisieramos que hubiera.
/// </para>
/// <para>
/// Dos diferencias con ARQUITECTURA §2, las dos por decision registrada en
/// DECISIONES.md (2026-09-02, «Las siete preguntas del esquema»): <c>casos</c> no
/// lleva el CHECK de la regla del mes cruzado (P-2, va como aviso), y
/// <c>estado_recomendacion</c> no lleva CHECK con una lista inventada (P-1).
/// </para>
/// </remarks>
internal static class DdlDeLaVersionInicial
{
    /// <summary>El guion completo de la version 1, tal como estaba escrito en Python.</summary>
    internal const string Guion = """
        CREATE TABLE IF NOT EXISTS version_esquema (
            version     INTEGER NOT NULL PRIMARY KEY,
            aplicada_en TEXT    NOT NULL,
            descripcion TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS casos (
            id                   INTEGER PRIMARY KEY,
            numero_caso          TEXT    NOT NULL UNIQUE
                CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]'),
            unidad_numero        TEXT
                CHECK (unidad_numero IS NULL
                       OR unidad_numero GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]'),
            fecha_viaje          TEXT
                CHECK (fecha_viaje IS NULL
                       OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            captura_manual       INTEGER NOT NULL DEFAULT 0 CHECK (captura_manual IN (0, 1)),
            archivado            INTEGER NOT NULL DEFAULT 0 CHECK (archivado IN (0, 1)),
            fecha_archivado      TEXT,
            ruta_pdf             TEXT,
            creado_en            TEXT    NOT NULL,
            estado_recomendacion TEXT,

            -- Un caso archivado tiene fecha; uno no archivado, no. No hay medio archivado.
            CHECK ((archivado = 0 AND fecha_archivado IS NULL)
                   OR (archivado = 1 AND fecha_archivado IS NOT NULL))
        );

        CREATE TABLE IF NOT EXISTS personas (
            id                          INTEGER PRIMARY KEY,
            caso_id                     INTEGER NOT NULL
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            mrn                         TEXT
                CHECK (mrn IS NULL
                       OR mrn GLOB '[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            nombre                      TEXT,
            fila_formulario             INTEGER
                CHECK (fila_formulario IS NULL OR fila_formulario >= 1),

            -- Las seis casillas. NULL = no leida, que es distinto de 0 = leida y no marcada.
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

            -- No se guarda una persona en blanco.
            CHECK (nombre IS NOT NULL OR mrn IS NOT NULL),

            -- La mitad de la clave de reconciliacion del Excel que vuelve (FASE 6).
            UNIQUE (caso_id, mrn)
        );

        CREATE TABLE IF NOT EXISTS companeros (
            id             INTEGER PRIMARY KEY,
            nombre         TEXT    NOT NULL,
            activo         INTEGER NOT NULL DEFAULT 1 CHECK (activo IN (0, 1)),
            desactivado_en TEXT,
            creado_en      TEXT    NOT NULL,

            CHECK ((activo = 1 AND desactivado_en IS NULL)
                   OR (activo = 0 AND desactivado_en IS NOT NULL))
        );

        CREATE TABLE IF NOT EXISTS asignaciones (
            id             INTEGER PRIMARY KEY,
            caso_id        INTEGER NOT NULL
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            companero_id   INTEGER NOT NULL
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            asignado_en    TEXT    NOT NULL,
            activa         INTEGER NOT NULL DEFAULT 1 CHECK (activa IN (0, 1)),
            desactivada_en TEXT,

            CHECK ((activa = 1 AND desactivada_en IS NULL)
                   OR (activa = 0 AND desactivada_en IS NOT NULL))
        );

        CREATE TABLE IF NOT EXISTS contactos (
            id               INTEGER PRIMARY KEY,
            caso_id          INTEGER NOT NULL
                REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            fecha            TEXT    NOT NULL,
            medio            TEXT,
            con_quien        TEXT,
            resultado        TEXT,
            anulado          INTEGER NOT NULL DEFAULT 0 CHECK (anulado IN (0, 1)),
            motivo_anulacion TEXT,
            anulado_en       TEXT,
            registrado_en    TEXT    NOT NULL,

            -- No se puede anular sin escribir por que.
            CHECK ((anulado = 0 AND motivo_anulacion IS NULL AND anulado_en IS NULL)
                   OR (anulado = 1 AND motivo_anulacion IS NOT NULL AND anulado_en IS NOT NULL))
        );

        CREATE TABLE IF NOT EXISTS procedencia_campo (
            id             INTEGER PRIMARY KEY,
            tabla          TEXT    NOT NULL CHECK (tabla IN ('casos', 'personas')),
            registro_id    INTEGER NOT NULL,
            campo          TEXT    NOT NULL,
            origen         TEXT    NOT NULL
                CHECK (origen IN ('anotacion', 'ocr', 'vacio', 'manual')),
            confianza      REAL
                CHECK (confianza IS NULL OR (confianza >= 0.0 AND confianza <= 1.0)),
            valor_ocr      TEXT,
            verificado     INTEGER NOT NULL DEFAULT 0 CHECK (verificado IN (0, 1)),
            verificado_por INTEGER
                REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
            verificado_en  TEXT,

            UNIQUE (tabla, registro_id, campo),

            -- Regla permanente 5 hecha estructura: no se marca verificado sin QUIEN y CUANDO.
            CHECK ((verificado = 0 AND verificado_por IS NULL AND verificado_en IS NULL)
                   OR (verificado = 1 AND verificado_por IS NOT NULL AND verificado_en IS NOT NULL))
        );

        CREATE INDEX IF NOT EXISTS idx_casos_viaje_activos
            ON casos (fecha_viaje) WHERE archivado = 0;
        CREATE INDEX IF NOT EXISTS idx_personas_caso
            ON personas (caso_id);
        CREATE INDEX IF NOT EXISTS idx_procedencia_registro
            ON procedencia_campo (tabla, registro_id);
        CREATE INDEX IF NOT EXISTS idx_asignaciones_companero
            ON asignaciones (companero_id) WHERE activa = 1;
        CREATE UNIQUE INDEX IF NOT EXISTS idx_asignacion_viva
            ON asignaciones (caso_id, companero_id) WHERE activa = 1;
        """;

    /// <summary>Lo que la version 1 dice de si misma en <c>version_esquema</c>.</summary>
    internal const string Descripcion =
        "Esquema inicial: version_esquema, casos, personas, companeros, " +
        "asignaciones, contactos y procedencia_campo.";
}
