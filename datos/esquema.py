"""El DDL de la base y su version.

Cada tabla, columna y restriccion sale de `docs/ARQUITECTURA.md` §2, que manda
sobre cualquier plan (`CLAUDE.md` §7). Nada se anade que ese documento no pida.

Dos diferencias con lo que ese documento escribe, las dos por decision posterior
registrada en `DECISIONES.md` (2026-09-02, «Las siete preguntas del esquema»):

  - `casos` NO lleva el `CHECK` de la regla del mes cruzado (P-2). Como
    restriccion del motor haria imposible guardar un viaje reprogramado a otro
    mes, que es una cosa que pasa. Va como aviso de `datos.validacion`.
  - `estado_recomendacion` es columna guardada y NO lleva `CHECK` con una lista
    inventada (P-1). La lista de valores validos vive en `datos.estados`.
"""

from datetime import datetime

from datos.migraciones import MIGRACIONES

TABLAS_DEL_ESQUEMA = (
    "version_esquema",
    "casos",
    "personas",
    "companeros",
    "asignaciones",
    "contactos",
    "procedencia_campo",
    # `documentos_ilegibles` nacio con la version 8 y no con el DDL de la 1, pero
    # entra en esta lista igual: la lista es «las tablas que una base al dia
    # tiene», y `pruebas/prueba_esquema.py` la compara contra las que hay de
    # verdad. Dejarla fuera haria fallar esa comparacion, que es justo la que
    # avisa de una tabla que aparece sin que nadie la haya declarado.
    "documentos_ilegibles",
    # `filas_descartadas` nacio con la version 11, por el mismo motivo por el que
    # `documentos_ilegibles` nacio con la 8: lo que no entro tiene que dejar un
    # renglon que se pueda volver a mirar manana, y no un cuadro que se cierra.
    "filas_descartadas",
)

VERSION_INICIAL = 1
DESCRIPCION_DE_LA_VERSION_INICIAL = (
    "Esquema inicial: version_esquema, casos, personas, companeros, "
    "asignaciones, contactos y procedencia_campo."
)

# La version a la que llega una base despues de aplicar todo lo pendiente. Se
# deriva de `datos.migraciones` en vez de escribirse a mano: un numero copiado a
# mano se queda desfasado el dia que alguien anade una migracion y se olvida de
# subirlo, y entonces la migracion nueva no se aplica nunca y nadie se entera.
VERSION_ACTUAL = MIGRACIONES[-1][0] if MIGRACIONES else VERSION_INICIAL

# Constante literal de modulo: ni una interpolacion, ni un dato de usuario. Es
# el unico texto SQL de este proyecto que no se escribe dentro de la llamada, y
# esta asi a proposito: el DDL de una version es un artefacto congelado, y una
# migracion futura anade otra constante en vez de tocar esta.
_DDL_VERSION_1 = """
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

    -- La regla del mes cruzado NO va aqui: DECISIONES.md P-2 la deja como aviso.
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

    -- Las seis casillas, en el orden literal de DECISIONES.md. NULL = no leida,
    -- que es distinto de 0 = leida y no marcada (FASE 2 crit. 11b).
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
"""


def marca_de_tiempo():
    """Ahora, en el formato ISO-8601 extendido que usa todo el esquema."""
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def _anotar_version(conexion, version, descripcion):
    """Deja constancia de que una version quedo aplicada, y cuando."""
    conexion.execute(
        "INSERT OR IGNORE INTO version_esquema (version, aplicada_en, descripcion) "
        "VALUES (?, ?, ?)",
        (version, marca_de_tiempo(), descripcion),
    )


def crear_tablas_de_la_version_inicial(conexion):
    """Crea las tablas tal como nacian en la version 1, y nada mas.

    Es el primer paso de `aplicar_esquema` con nombre propio, y esta separado por
    dos razones. La primera: `pruebas/prueba_migraciones.py` necesita construir una
    base EXACTAMENTE como la que ya existe en la maquina del dueno para comprobar
    que la migracion la convierte; si esa prueba llamara a `aplicar_esquema` estaria
    probando el camino facil y no el que importa. La segunda: el DDL de la version 1
    se ejecuta aqui, en el mismo archivo donde esta escrito, que es lo que permite a
    `pruebas/auditoria_sql.py` seguir la constante hasta su origen y dictaminarla.

    ⚠️ Esta funcion deja la base en un esquema ANTIGUO a proposito. Quien quiera una
    base utilizable llama a `aplicar_esquema`, no a esto.
    """
    conexion.executescript(_DDL_VERSION_1)


def aplicar_esquema(conexion):
    """Crea lo que falte, aplica las migraciones pendientes y registra cada una.

    Es idempotente por las tres vias: `CREATE ... IF NOT EXISTS` para el DDL,
    `INSERT OR IGNORE` para la fila de version, y la comparacion con la version que
    la base ya tiene para no repetir una migracion. Arrancar dos veces sobre una
    base ya migrada no duplica nada ni toca los datos.

    Una base recien creada tambien pasa por las migraciones, aunque su tabla naciera
    hace un segundo. Es a proposito: un camino de codigo que solo se ejecuta sobre
    bases viejas es un camino que nadie prueba nunca hasta el dia que hace falta.
    """
    crear_tablas_de_la_version_inicial(conexion)
    _anotar_version(conexion, VERSION_INICIAL, DESCRIPCION_DE_LA_VERSION_INICIAL)

    for version, descripcion, aplicar in MIGRACIONES:
        if version > version_de_la_base(conexion):
            aplicar(conexion)
            _anotar_version(conexion, version, descripcion)
    return VERSION_ACTUAL


def version_de_la_base(conexion):
    """La version vigente de la base, o None si todavia no se aplico ninguna."""
    fila = conexion.execute("SELECT MAX(version) FROM version_esquema").fetchone()
    return fila[0] if fila is not None else None
