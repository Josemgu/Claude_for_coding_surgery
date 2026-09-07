"""La version 15: la cedula de miembro puede terminar en LETRA.

**El hecho, y como se midio.** El 2026-09-04 el supervisor recorto del PDF la banda
de la cedula en dos escaneos reales del dueno y miro la imagen: el papel dice
`...-...-###A`. El OCR las leia bien; las tiraba nuestra regla de «11 digitos en
patron 3-4-4». Sobre los siete PDF reales eso costaba **2 de 7 cedulas**: el campo
quedaba vacio y lo leido solo vivia en `procedencia_campo.valor_ocr`, donde el
dueno no lo ve. Palabras suyas: «Muchas cedulas de miembro tienen una A u otra
letra al final».

Es el mismo error que la version 2 arreglo para `unidad_numero` —una regla escrita
mirando cuatro formularios de referencia que no son los del dueno— y tiene el mismo
sintoma: un dato verdadero rechazado y un campo vacio que nadie sabe que falta.

**Que cambia exactamente.** Una sola cosa: el `CHECK` de `personas.mrn`. El ultimo
de los cuatro caracteres del tercer grupo pasa a admitir tambien una letra
(`[0-9A-Za-z]`). Todo lo demas de la tabla se copia letra por letra del DDL que la
version 14 dejaba: una migracion que aprovecha para retocar otra cosa de paso es
una migracion que nadie puede revisar.

**Por que hay que reconstruir la tabla entera.** SQLite no puede cambiar un `CHECK`
con `ALTER TABLE`; la documentacion oficial
(<https://www.sqlite.org/lang_altertable.html>, seccion «Making Other Kinds Of
Table Schema Changes», consultada 2026-09-04) publica el procedimiento de 12 pasos,
que este proyecto tiene escrito una sola vez en `datos/reconstruccion_de_tablas.py`
desde la version 7.

Los pasos 3 y 9 no aplican: `personas` no tiene disparadores ni vistas, y su unico
indice —`idx_personas_caso`— se reconstruye explicitamente en el paso 8. El paso 10
si importa: `personas` apunta a `casos` con `RESTRICT` y `procedencia_campo` apunta
a `personas` por un par (tabla, registro_id) que el motor NO puede defender con una
clave foranea (`docs/ARQUITECTURA.md` §2.7). Los ids se copian tal cual —la columna
`id` va nombrada en los dos lados del INSERT— para que esa procedencia siga
apuntando a la misma persona.

**Lo que esta migracion NO hace, y es a proposito.** No vuelve a leer ningun PDF.
Las cedulas que se perdieron en importaciones anteriores siguen perdidas en la base
del dueno: lo que se leyo esta en `procedencia_campo.valor_ocr` y se recupera
volviendo a importar el documento, o tecleandolo. Rellenarlas desde aqui seria
escribir en `personas.mrn` un texto que nadie confirmo.
"""

import sqlite3

from datos.reconstruccion_de_tablas import ErrorDeMigracion, reconstruir_tabla

VERSION_DE_LA_CEDULA_CON_LETRA = 15

DESCRIPCION_DE_LA_VERSION_15 = (
    "la cedula de miembro puede terminar en letra: se reconstruye 'personas' con "
    "el CHECK de mrn corregido (DECISIONES.md 2026-09-04)."
)

# El DDL de `personas` tal como lo dejaba la version 14, con UNA diferencia: el
# ultimo caracter de `mrn` admite letra. Las columnas van en el mismo orden en que
# estaban, incluidas las que llegaron por `ALTER TABLE ... ADD COLUMN` en las
# versiones 4, 5, 6 y 9 —que en la base viven pegadas al final de la lista—.
_TABLA_NUEVA_DE_LA_VERSION_15 = """
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
"""

# Las 26 columnas se nombran una a una en los dos lados del INSERT. Un
# `INSERT INTO ... SELECT *` copiaria por posicion y una columna anadida en medio
# dejaria los datos corridos sin que nadie se entere. `id` va nombrada la primera
# porque la procedencia de cada campo apunta a ella y NO puede cambiar.
_COPIA_DE_LOS_DATOS_A_LA_VERSION_15 = """
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
"""


def _rehacer_la_tabla_personas(conexion):
    """Los pasos 4 a 8 del procedimiento oficial, escritos uno a uno.

    Cada instruccion se escribe en su propia llamada y no se recorre una lista de
    cadenas: `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el
    arbol de sintaxis, y una variable de bucle no la puede seguir hasta su origen.
    Una lista blanca donde tres llamadas quedan «no resueltas» ya no es una lista
    blanca de nada.
    """
    conexion.execute(_TABLA_NUEVA_DE_LA_VERSION_15)
    conexion.execute(_COPIA_DE_LOS_DATOS_A_LA_VERSION_15)
    conexion.execute("DROP TABLE personas")
    conexion.execute("ALTER TABLE personas_version_15 RENAME TO personas")
    conexion.execute("CREATE INDEX idx_personas_caso ON personas (caso_id)")


def migrar_a_version_15(conexion):
    """Reconstruye `personas` para que `mrn` admita una letra al final."""
    try:
        reconstruir_tabla(conexion, _rehacer_la_tabla_personas)
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 15: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa
