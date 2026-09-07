"""Los cambios de esquema posteriores a la version 1, en orden.

Por que existe este modulo y no se edito el DDL de la version 1: `datos/esquema.py`
lo dice de si mismo — «el DDL de una version es un artefacto congelado, y una
migracion futura anade otra constante en vez de tocar esta». Y hay un motivo
medido: en la maquina del dueno YA existe una base creada con la version 1
(`Documentos\\Fichas\\fichas.db`). El DDL se escribe con `CREATE TABLE IF NOT
EXISTS`, asi que cambiar la constante de la version 1 NO habria tocado esa base:
habria dejado el `CHECK` viejo en disco mientras el codigo afirmaba otra cosa. Eso
es exactamente la divergencia que `CLAUDE.md` §8 manda evitar.

Cambiar un `CHECK` en SQLite no se puede hacer con `ALTER TABLE`: hay que
reconstruir la tabla. La documentacion oficial
(<https://www.sqlite.org/lang_altertable.html>, seccion «Making Other Kinds Of
Table Schema Changes», consultada 2026-09-02) publica un procedimiento de 12
pasos. Los que aplican aqui, literales:

    «1. If foreign key constraints are enabled, disable them using
     PRAGMA foreign_keys=OFF.»
    «2. Start a transaction.»
    «4. Use CREATE TABLE to construct a new table "new_X" that is in the desired
     revised format of table X.»
    «5. Transfer content from X into new_X using a statement like:
     INSERT INTO new_X SELECT ... FROM X.»
    «6. Drop the old table X: DROP TABLE X.»
    «7. Change the name of new_X to X using: ALTER TABLE new_X RENAME TO X.»
    «8. Use CREATE INDEX, CREATE TRIGGER, and CREATE VIEW to reconstruct indexes,
     triggers, and views associated with table X.»
    «10. If foreign key constraints were originally enabled then run
     PRAGMA foreign_key_check to verify that the schema change did not break any
     foreign key constraints.»
    «11. Commit the transaction started in step 2.»
    «12. If foreign keys constraints were originally enabled, reenable them now.»

Los pasos 3 y 9 no aplican: `casos` no tiene disparadores ni vistas, y su unico
indice se reconstruye explicitamente en el paso 8. El paso 10 aqui no es
decorativo: `personas`, `asignaciones` y `contactos` apuntan a `casos` con
`RESTRICT`, y la migracion las deja apuntando a una tabla que se borro y se volvio
a crear. Si algo quedara huerfano, esta funcion levanta y deshace.
"""

import sqlite3

from datos.migraciones_de_importacion import (
    DESCRIPCION_DE_LA_VERSION_7,
    DESCRIPCION_DE_LA_VERSION_8,
    DESCRIPCION_DE_LA_VERSION_12,
    DESCRIPCION_DE_LA_VERSION_13,
    migrar_a_version_7,
    migrar_a_version_8,
    migrar_a_version_12,
    migrar_a_version_13,
)

from datos.migraciones_de_revision import (
    DESCRIPCION_DE_LA_VERSION_14,
    migrar_a_version_14,
)

from datos.migraciones_de_cedula import (
    DESCRIPCION_DE_LA_VERSION_15,
    migrar_a_version_15,
)

# `ErrorDeMigracion` y el procedimiento de reconstruccion nacieron en este archivo
# y se movieron a `datos/reconstruccion_de_tablas.py` cuando la version 7 —que
# vive en otro modulo— necesito el mismo procedimiento de 12 pasos. Se reexportan
# aqui para que nada de lo que ya los importaba de `datos.migraciones` cambie.
from datos.reconstruccion_de_tablas import ErrorDeMigracion, reconstruir_tabla

__all__ = ["ErrorDeMigracion", "MIGRACIONES"]


DESCRIPCION_DE_LA_VERSION_2 = (
    "unidad_numero admite 6 o 7 digitos: se reconstruye 'casos' con el CHECK "
    "corregido (DECISIONES.md 2026-09-02)."
)

# El `CHECK` de `unidad_numero` es lo unico que cambia. Todo lo demas se copia
# letra por letra de `_DDL_VERSION_1`: una migracion que aprovecha para retocar
# otra cosa de paso es una migracion que nadie puede revisar.
_TABLA_NUEVA_DE_LA_VERSION_2 = """
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
"""

# Las columnas se nombran una a una en los dos lados del INSERT. Un
# `INSERT INTO ... SELECT *` copiaria por posicion y una columna anadida en medio
# dejaria los datos corridos sin que nadie se entere.
_COPIA_DE_LOS_DATOS_A_LA_VERSION_2 = """
INSERT INTO casos_version_2
    (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion)
SELECT
     id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion
FROM casos
"""

def _rehacer_la_tabla_casos(conexion):
    """Los pasos 4 a 8 del procedimiento oficial, escritos uno a uno.

    Cada instruccion se escribe en su propia llamada y no se recorre una lista de
    cadenas: `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el
    arbol de sintaxis, y una variable de bucle no la puede seguir hasta su origen.
    Una lista blanca donde tres llamadas quedan «no resueltas» ya no es una lista
    blanca de nada.
    """
    conexion.execute(_TABLA_NUEVA_DE_LA_VERSION_2)
    conexion.execute(_COPIA_DE_LOS_DATOS_A_LA_VERSION_2)
    conexion.execute("DROP TABLE casos")
    conexion.execute("ALTER TABLE casos_version_2 RENAME TO casos")
    conexion.execute(
        "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0"
    )


def migrar_a_version_2(conexion):
    """Reconstruye `casos` para que `unidad_numero` admita 6 o 7 digitos."""
    try:
        reconstruir_tabla(conexion, _rehacer_la_tabla_casos)
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 2: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa




DESCRIPCION_DE_LA_VERSION_3 = (
    "Los huecos que tapaban la pantalla de correccion: 'casos' gana 'pagina_pdf' "
    "y 'unidad_nombre', y 'procedencia_campo' gana las cuatro coordenadas de la "
    "banda del escaneo y la marca de tachon."
)

# Por que esta migracion usa `ALTER TABLE ... ADD COLUMN` y la version 2 tuvo que
# reconstruir la tabla entera: alli cambiaba un `CHECK` que ya existia, y eso
# SQLite no lo sabe hacer. Aqui solo se ANADEN columnas nuevas, que es la
# operacion que `ALTER TABLE` si cubre.
#
# La documentacion oficial (<https://www.sqlite.org/lang_altertable.html>,
# seccion «ADD COLUMN», consultada 2026-09-02) enumera lo que la columna nueva NO
# puede llevar: PRIMARY KEY, UNIQUE, un valor por defecto no constante, y —si la
# columna es NOT NULL— un defecto NULL. Ninguna de las seis de aqui lleva nada de
# eso: las seis son opcionales y nacen a NULL en las filas que ya existen.
#
# Lo que si lleva cada una es su `CHECK`, y esto se midio en esta maquina antes
# de escribirlo (SQLite 3.50.4, el que trae este Python): `ALTER TABLE ... ADD
# COLUMN b REAL CHECK (...)` se acepta Y la restriccion se aplica de verdad a lo
# que se inserte despues. No es un adorno que el motor ignore.

# La banda se guarda en FRACCIONES de la pagina (0.0 a 1.0), no en pixeles. Es la
# diferencia entre poder volver a recortar la tira y no poder. Los pixeles que
# calcula la extraccion son pixeles DE ESA rasterizacion, a la escala que salio
# ese dia; guardados asi, el dia que el tope de 3500 px cambie —o que la pagina se
# rasterice mas pequena para caber en la pantalla— el rectangulo apuntaria a otro
# sitio del papel. Una fraccion de la pagina sigue valiendo a cualquier escala.
_COLUMNAS_DE_LA_BANDA = ("banda_x0", "banda_y0", "banda_x1", "banda_y1")


def _anadir_las_columnas_de_casos(conexion):
    """La pagina del PDF y el nombre de la unidad, que el extractor ya lee.

    `pagina_pdf` se guarda en base 1 —«pagina 2 de 6»—, que es como lo cuentan
    las personas y los visores de PDF. El `indice_de_pagina` que devuelve la
    extraccion empieza en 0; la conversion se hace en un solo sitio, al guardar,
    y no repartida por la interfaz.
    """
    conexion.execute(
        "ALTER TABLE casos ADD COLUMN pagina_pdf INTEGER "
        "CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)"
    )
    conexion.execute("ALTER TABLE casos ADD COLUMN unidad_nombre TEXT")


def _anadir_las_columnas_de_la_banda(conexion):
    """Las cuatro coordenadas del recorte, una llamada por columna.

    Cuatro llamadas escritas a mano y no un bucle sobre `_COLUMNAS_DE_LA_BANDA`:
    `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el arbol de
    sintaxis y no puede seguir una variable de bucle hasta su texto. La constante
    de arriba existe para que el resto del programa nombre las columnas en un solo
    sitio, no para armar el SQL con ella.
    """
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN banda_x0 REAL "
        "CHECK (banda_x0 IS NULL OR (banda_x0 >= 0.0 AND banda_x0 <= 1.0))"
    )
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN banda_y0 REAL "
        "CHECK (banda_y0 IS NULL OR (banda_y0 >= 0.0 AND banda_y0 <= 1.0))"
    )
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN banda_x1 REAL "
        "CHECK (banda_x1 IS NULL OR (banda_x1 >= 0.0 AND banda_x1 <= 1.0))"
    )
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN banda_y1 REAL "
        "CHECK (banda_y1 IS NULL OR (banda_y1 >= 0.0 AND banda_y1 <= 1.0))"
    )


def _anadir_la_marca_de_tachon(conexion):
    """Si el papel llevaba un tachon sobre ese campo y nadie escribio la correccion.

    El extractor lo sabe desde la FASE 2 —`CampoExtraido.anulado_por_tachon`— y no
    tenia donde guardarlo, asi que se perdia al escribir en la base. No es lo
    mismo que «vacio»: es un dato que UNA PERSONA marco como equivocado a
    proposito. Sin esta columna, la pantalla de correccion ensena el campo vacio
    igual que uno que el OCR no supo leer, y se pierde la unica pista de que el
    papel ya decia que ese dato estaba mal.
    """
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN anulado_por_tachon INTEGER "
        "NOT NULL DEFAULT 0 CHECK (anulado_por_tachon IN (0, 1))"
    )


def migrar_a_version_3(conexion):
    """Anade las siete columnas nuevas, todas o ninguna.

    Va en una transaccion aunque `ALTER TABLE ADD COLUMN` sea barato: siete
    columnas a medias dejarian el codigo leyendo una que no existe, y ese fallo
    aparece mucho despues y en otro sitio.
    """
    try:
        conexion.execute("BEGIN")
        try:
            _anadir_las_columnas_de_casos(conexion)
            _anadir_las_columnas_de_la_banda(conexion)
            _anadir_la_marca_de_tachon(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 3: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_4 = (
    "'personas' gana 'pagina_pdf': cada persona sabe de que hoja del PDF salio, "
    "y su tira del escaneo se recorta de esa hoja y no de la del caso."
)


def migrar_a_version_4(conexion):
    """La hoja del PDF de la que salio cada persona.

    Por que no basta con la que ya tiene `casos`: desde que las paginas de un
    grupo se unen en un solo caso (DECISIONES.md, 2026-09-02, opcion A), un caso
    puede tener doce personas repartidas en seis hojas, y `casos.pagina_pdf` es
    UNA sola —la de la hoja que abrio el caso—. La pantalla de correccion
    recortaba las doce tiras de esa hoja, asi que las once personas de las hojas
    2 a 6 ensenaban la tira de otra persona. Eso es peor que no ensenar tira: se
    puede dar por bueno un MRN comparandolo con una imagen que no le corresponde.

    `casos.pagina_pdf` se queda como esta y sigue significando lo mismo: la hoja
    que abrio el caso.

    Va sin transaccion explicita y las otras dos la llevan: aqui hay UNA sola
    instruccion, y una instruccion suelta ya es atomica para el motor. Envolverla
    en `BEGIN`/`COMMIT` no anadiria ninguna garantia y sugeriria que hay algo
    que coordinar.

    La columna nace a NULL en las personas que ya estaban, y es lo unico honesto:
    de una persona guardada antes de esta version nadie sabe de que hoja salio.
    Un `1` por defecto seria una hoja inventada (regla permanente 1).
    """
    try:
        conexion.execute(
            "ALTER TABLE personas ADD COLUMN pagina_pdf INTEGER "
            "CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)"
        )
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 4: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_5 = (
    "Lo que las FASES 6 y 7 no tenian donde guardar: 'personas' gana las cuatro "
    "columnas de la propuesta del companero, y 'contactos' gana quien contacto y "
    "si respondio."
)

# ⚠️ Por que la propuesta del companero NO se guarda en `procedencia_campo`.
#
# El pase de la FASE 6 pedia literalmente que lo que carga el companero entrara
# «como propuesta, con su nombre en `verificado_por`». Eso el esquema no lo
# admite, y se midio antes de escribir esta migracion (SQLite 3.50.4, esta
# maquina):
#
#     UPDATE procedencia_campo SET verificado_por = 1 WHERE id = 1
#     -> CHECK constraint failed:
#        (verificado = 0 AND verificado_por IS NULL AND verificado_en IS NULL)
#        OR (verificado = 1 AND verificado_por IS NOT NULL AND verificado_en IS NOT NULL)
#
# Ese `CHECK` es la regla permanente 5 hecha estructura (`docs/ARQUITECTURA.md`
# §2.7): no hay forma de escribir un `verificado_por` sin poner `verificado = 1`,
# y poner `verificado = 1` porque un companero mando un Excel es exactamente
# marcar algo como verificado automaticamente. Se puede cumplir la letra del pase
# o la regla permanente, no las dos.
#
# Se cumple la regla permanente. La propuesta vive en columnas propias, `verificado`
# sigue en 0, y `verificado_por` lo sigue rellenando solo el boton que Miguel pulsa.

# Las cuatro columnas de la propuesta, para que el resto del programa las nombre
# en un solo sitio. Nadie arma SQL con esta constante.
COLUMNAS_DE_LA_PROPUESTA = (
    "estado_propuesto",
    "nota_companero",
    "propuesto_por",
    "propuesto_en",
)


def _anadir_las_columnas_de_la_propuesta(conexion):
    """Lo que el companero devuelve en su Excel, pegado a la persona que lo trae.

    `estado_propuesto` va SIN `CHECK`, por el mismo motivo que
    `casos.estado_recomendacion` (DECISIONES.md, P-1): la lista de valores validos
    vive en `datos/estados.py` y todavia le faltan valores que el dueno no ha
    dicho. Un `CHECK` con una lista incompleta rechazaria manana un valor
    verdadero.

    `propuesto_por` si lleva su `REFERENCES`, y se midio que la restriccion se
    aplica de verdad sobre una columna anadida asi: insertar `propuesto_por = 999`
    sin companero 999 devuelve «FOREIGN KEY constraint failed». La documentacion
    oficial (<https://www.sqlite.org/lang_altertable.html>, seccion «ADD COLUMN»,
    consultada 2026-09-02) lo permite con una condicion que aqui se cumple: la
    columna nace con valor por defecto NULL.

    Cuatro llamadas escritas a mano y no un bucle sobre `COLUMNAS_DE_LA_PROPUESTA`:
    `pruebas/auditoria_sql.py` no puede seguir una variable de bucle hasta su
    texto, y una lista blanca con huecos deja de ser una lista blanca.
    """
    conexion.execute("ALTER TABLE personas ADD COLUMN estado_propuesto TEXT")
    conexion.execute("ALTER TABLE personas ADD COLUMN nota_companero TEXT")
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN propuesto_por INTEGER "
        "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT"
    )
    conexion.execute("ALTER TABLE personas ADD COLUMN propuesto_en TEXT")


def _anadir_las_columnas_del_contacto(conexion):
    """Quien hizo el contacto y si el lider respondio.

    `contactos` ya traia `con_quien`, que `docs/ARQUITECTURA.md` §2.6 define como
    «con quien se hablo» —el lider—. Quien llamo es otra pregunta y necesitaba otra
    columna: sin ella, un caso con seis contactos no dice quien de los companeros
    los hizo, y el historial deja de servir para repartir trabajo.

    `respondio` admite NULL a proposito, y son tres estados y no dos: 1 respondio,
    0 no respondio, NULL todavia no se sabe —un correo de esta manana—. Un 0 por
    defecto convertiria «aun no contesta» en «no contesto», que es un dato
    inventado sobre una persona (regla permanente 1).
    """
    conexion.execute(
        "ALTER TABLE contactos ADD COLUMN contactado_por INTEGER "
        "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT"
    )
    conexion.execute(
        "ALTER TABLE contactos ADD COLUMN respondio INTEGER "
        "CHECK (respondio IS NULL OR respondio IN (0, 1))"
    )


def migrar_a_version_5(conexion):
    """Anade las seis columnas nuevas, todas o ninguna.

    Va en una transaccion por el mismo motivo que la version 3: seis columnas a
    medias dejan el codigo leyendo una que no existe, y ese fallo aparece mucho
    despues y en otro sitio.
    """
    try:
        conexion.execute("BEGIN")
        try:
            _anadir_las_columnas_de_la_propuesta(conexion)
            _anadir_las_columnas_del_contacto(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 5: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_6 = (
    "'personas' gana 'pudo_viajar' y 'motivo_no_viajo': el reporte de la FASE 8 "
    "tiene que decir quien no pudo viajar y por que, y no habia donde guardarlo."
)

# ⚠️ Por que estas dos columnas van en `personas` y no en `casos`.
#
# El reporte que pide la FASE 8 habla de PERSONAS que no pudieron viajar, no de
# casos. Un caso es un formulario, y un formulario de grupo lleva hasta doce
# personas: puestas en `casos`, una familia de cuatro donde solo falla uno saldria
# en el reporte como cuatro personas que no viajaron, con el mismo motivo copiado
# cuatro veces. Eso es un dato falso sobre tres personas reales.
#
# El coste que trae, dicho: archivar sigue siendo del caso —`archivado` esta en
# `casos`— y el resultado del viaje es de cada persona. Son dos preguntas
# distintas y se responden por separado a proposito.


def _anadir_el_motivo_de_no_viajar(conexion):
    """El texto que escribe Miguel cuando alguien no llego a viajar.

    Va PRIMERO, antes que `pudo_viajar`, y el orden no es de estilo: el `CHECK` de
    `pudo_viajar` nombra esta columna, y una restriccion no puede nombrar una
    columna que todavia no existe.

    Es texto libre y no una lista cerrada de motivos. La lista no la ha dicho
    nadie, y una inventada obligaria a meter en «otros» justo el caso real que
    paso —que es el dato que este reporte lleva a los jefes—. Inventarla seria
    ademas la regla permanente 1.
    """
    conexion.execute("ALTER TABLE personas ADD COLUMN motivo_no_viajo TEXT")


def _anadir_si_pudo_viajar(conexion):
    """Si esa persona llego a viajar: 1 si, 0 no, NULL nadie lo ha dicho todavia.

    Los tres estados hacen falta y el tercero es el que importa. Un
    `NOT NULL DEFAULT 0` habria escrito «no pudo viajar» sobre todas las personas
    que ya estaban guardadas, de las que nadie ha dicho nada: un dato inventado
    (regla permanente 1), y de los que hacen dano — el reporte le diria a los jefes
    que fallaron viajes que nadie ha dicho que fallaran.

    El `CHECK` cruzado exige el motivo cuando la respuesta es «no pudo» y lo
    prohibe cuando es «si pudo» o «no se sabe»: sin el, «no pudo viajar» podria
    entrar sin explicacion, y una fila del reporte sin motivo no le sirve de nada
    a quien la lee.

    Medido en esta maquina antes de escribirlo (SQLite 3.50.4): `ALTER TABLE ...
    ADD COLUMN` acepta un `CHECK` que nombra otra columna y lo aplica de verdad —
    seis combinaciones probadas, las seis con el resultado esperado— y las filas
    que ya existian se quedan intactas.
    """
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN pudo_viajar INTEGER CHECK ("
        "  (pudo_viajar IS NULL OR pudo_viajar IN (0, 1))"
        "  AND (pudo_viajar IS 0 OR motivo_no_viajo IS NULL)"
        "  AND (pudo_viajar IS NOT 0 OR motivo_no_viajo IS NOT NULL))"
    )


def migrar_a_version_6(conexion):
    """Anade las dos columnas del resultado del viaje: las dos o ninguna.

    En transaccion por lo mismo que la version 3: a medias quedaria
    `motivo_no_viajo` sin el `CHECK` que lo defiende, y entonces se podria guardar
    un motivo escrito sobre una persona de la que nadie dijo que no viajara.
    """
    try:
        conexion.execute("BEGIN")
        try:
            _anadir_el_motivo_de_no_viajar(conexion)
            _anadir_si_pudo_viajar(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 6: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_9 = (
    "'personas' gana los seis pasos de «Preparación para las ordenanzas» y si se "
    "llamó al líder: es lo que el compañero devuelve en su hoja, y no había donde "
    "guardarlo."
)

# ⚠️ Por que hacen falta SIETE columnas y no basta con `estado_propuesto`.
#
# El dueno dijo, literal, que el Excel del proyecto viejo es el que quiere
# (`DECISIONES.md`, 2026-09-03). Ese Excel no pregunta «¿esta completa?»: pregunta
# los SEIS pasos de la pantalla del lider, uno por uno, y ademas si el companero
# llamo al lider. La diferencia no es de forma. Un solo estado dice que la persona
# no esta lista; los seis pasos dicen **en cual se quedo**, y eso es lo que hay que
# decirle al lider cuando se le llama por telefono, que es el trabajo entero.
#
# Las siete van en `personas` y no en `casos` por lo mismo que la version 6: un
# formulario de grupo lleva hasta doce personas y la preparacion es de cada una.
# Una familia de cuatro donde a uno le falta la entrevista no son cuatro personas
# con la entrevista pendiente.
#
# Las siete son parte de la PROPUESTA del companero, firmada por `propuesto_por` y
# `propuesto_en` (version 5 del esquema). No son una verificacion: `verificado` de
# `procedencia_campo` sigue en 0 y lo confirma Miguel (regla permanente 5).
#
# NULL, 0 y 1 son tres respuestas distintas y las tres hacen falta: NULL es que
# nadie miro ese paso, 0 es que se miro y no esta, 1 es que se miro y esta. Un
# `NOT NULL DEFAULT 0` escribiria «no esta preparada» sobre todas las personas que
# ya estan guardadas, de las que nadie ha dicho nada — un dato inventado sobre
# personas reales (regla permanente 1).
COLUMNAS_DE_LOS_PASOS = (
    "paso_preparacion",
    "paso_informacion",
    "paso_cita_del_templo",
    "paso_acciones_requeridas",
    "paso_entrevistas",
    "paso_listo_para_el_templo",
    "llamo_al_lider",
)


def _anadir_los_seis_pasos(conexion):
    """Los seis pasos de la pantalla del lider, uno por columna.

    Siete llamadas escritas a mano y no un bucle sobre `COLUMNAS_DE_LOS_PASOS`, por
    lo mismo que en la version 5: `pruebas/auditoria_sql.py` no puede seguir una
    variable de bucle hasta su texto, y una lista blanca con huecos deja de ser una
    lista blanca.

    Cada `CHECK` nombra solo a su propia columna, asi que ninguna depende del orden
    en que se anadan. `ALTER TABLE ... ADD COLUMN` con `CHECK` esta permitido por la
    documentacion oficial (<https://www.sqlite.org/lang_altertable.html>, seccion
    «ADD COLUMN») siempre que la columna nazca con valor por defecto NULL, que es lo
    que pasa aqui, y ya se midio en la version 6 que la restriccion se aplica de
    verdad sobre lo que se inserte despues.
    """
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_preparacion INTEGER "
        "CHECK (paso_preparacion IS NULL OR paso_preparacion IN (0, 1))"
    )
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_informacion INTEGER "
        "CHECK (paso_informacion IS NULL OR paso_informacion IN (0, 1))"
    )
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_cita_del_templo INTEGER "
        "CHECK (paso_cita_del_templo IS NULL OR paso_cita_del_templo IN (0, 1))"
    )
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_acciones_requeridas INTEGER "
        "CHECK (paso_acciones_requeridas IS NULL OR paso_acciones_requeridas IN (0, 1))"
    )
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_entrevistas INTEGER "
        "CHECK (paso_entrevistas IS NULL OR paso_entrevistas IN (0, 1))"
    )
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN paso_listo_para_el_templo INTEGER "
        "CHECK (paso_listo_para_el_templo IS NULL OR paso_listo_para_el_templo IN (0, 1))"
    )


def _anadir_si_llamo_al_lider(conexion):
    """Si el companero llego a hablar con el lider de esa unidad.

    Va aparte de los seis pasos porque no es un paso: los seis salen en la pantalla
    del lider y este dice lo que hizo el companero cuando vio que faltaba alguno.
    Mezclarlos haria que `estado_de_los_pasos` contara siete y una persona con los
    seis pasos completos que nadie llamo saldria como no lista.
    """
    conexion.execute(
        "ALTER TABLE personas ADD COLUMN llamo_al_lider INTEGER "
        "CHECK (llamo_al_lider IS NULL OR llamo_al_lider IN (0, 1))"
    )


def migrar_a_version_9(conexion):
    """Anade las siete columnas de la hoja del companero: las siete o ninguna.

    En transaccion por lo mismo que las versiones 3, 5 y 6: a medias, el lector de
    la hoja que vuelve escribiria en una columna que no existe, y ese fallo aparece
    cuando el companero ya devolvio su archivo y no esta delante para repetirlo.
    """
    try:
        conexion.execute("BEGIN")
        try:
            _anadir_los_seis_pasos(conexion)
            _anadir_si_llamo_al_lider(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 9: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_10 = (
    "'casos' gana 'templo_nombre': el templo esta impreso en el formulario y hasta "
    "hoy se descartaba, y la cabecera del Excel del agente lo pide."
)


def migrar_a_version_10(conexion):
    """El nombre del templo, leido del papel y guardado tal cual.

    **Por que texto libre y sin catalogo.** El dueno pidio el templo el 2026-09-03
    y el papel lo trae impreso —«Nombre del templo» / «Temple Name», la etiqueta ya
    esta en `extraccion/etiquetas.py`—, pero la lista de templos con sus colores es
    una decision suya que todavia no ha tomado (`DECISIONES.md`, 2026-09-03: «el
    catalogo con color espera al dueno»). Un `CHECK` con una lista inventada
    rechazaria manana un templo verdadero, que es el mismo criterio con el que
    `casos.estado_recomendacion` tampoco lo lleva (P-1).

    Va sin transaccion explicita: es UNA sola instruccion, y una instruccion suelta
    ya es atomica para el motor, igual que en la version 4.

    La columna nace a NULL en los casos que ya estaban, y es lo unico honesto: de un
    caso importado antes de esta version nadie leyo el templo. Rellenarla con
    cualquier cosa seria un dato inventado (regla permanente 1).
    """
    try:
        conexion.execute("ALTER TABLE casos ADD COLUMN templo_nombre TEXT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 10: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_11 = (
    "'filas_descartadas': lo que volvio en el Excel de un companero y NO entro en "
    "la base deja su renglon, que se queda al cerrar el programa."
)

# ⚠️ Esta tabla la anuncio `interfaz/descartados.py` de si mismo, en un aviso que
# lleva escrito desde que se escribio la pantalla: «no se guarda en la base. Se ve
# mientras la ventana esta abierta [...] si quiere que los descartes sobrevivan al
# cierre del programa, es una tabla nueva y su migracion». El dueno lo decidio tras
# la auditoria final de QA, y esta es la tabla.
#
# El dano que cierra esta medido: en la ida y vuelta de QA, la persona sin MRN
# volvio con 0 de 7 pasos. El trabajo del companero se descarta —lo cual es
# correcto: casar por nombre crea registros fantasma—, pero hasta hoy la lista de
# lo descartado se perdia al cerrar la ventana, y con ella la unica pista de que
# habia trabajo hecho que nadie recogio.
#
# **El motivo se guarda como frase y no como codigo**, al reves que en
# `documentos_ilegibles`, y la diferencia esta razonada: alli hay siete motivos
# cerrados que se pueden contar —«de 500 documentos, cuantos no se abrieron»—;
# aqui el motivo lleva dentro el numero de la fila del Excel con la que choco y la
# causa concreta («la clave "X" no tiene la forma esperada»), que es lo que hace
# falta para ir a mirar ESA fila. Un codigo perderia justo eso.
#
# `companero_id` con `REFERENCES` y `RESTRICT` como el resto del esquema: un
# companero con descartes apuntando no se puede borrar sin decidir antes que pasa
# con ellos. Y de todos modos los companeros se desactivan, no se borran.
_TABLA_DE_LA_VERSION_11 = """
CREATE TABLE IF NOT EXISTS filas_descartadas (
    id            INTEGER PRIMARY KEY,
    companero_id  INTEGER NOT NULL
        REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
    ruta_excel    TEXT,
    fila_excel    INTEGER CHECK (fila_excel IS NULL OR fila_excel >= 1),
    numero_caso   TEXT,
    mrn           TEXT,
    nombre        TEXT,
    motivo        TEXT    NOT NULL,
    registrado_en TEXT    NOT NULL
)
"""


def migrar_a_version_11(conexion):
    """Crea la tabla de las filas que volvieron y no entraron.

    Va sin transaccion explicita por lo mismo que la version 8: son dos
    instrucciones, pero la segunda es un indice sobre la tabla que acaba de nacer
    —si falla, la tabla se queda sin indice y todo lo demas sigue funcionando—.
    """
    try:
        conexion.execute(_TABLA_DE_LA_VERSION_11)
        conexion.execute(
            "CREATE INDEX IF NOT EXISTS idx_descartadas_companero "
            "ON filas_descartadas (companero_id)"
        )
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 11: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


# Cada entrada es (version, descripcion, funcion). El orden de la tupla es el
# orden en que se aplican, y `datos.esquema` solo aplica las que faltan.
MIGRACIONES = (
    (2, DESCRIPCION_DE_LA_VERSION_2, migrar_a_version_2),
    (3, DESCRIPCION_DE_LA_VERSION_3, migrar_a_version_3),
    (4, DESCRIPCION_DE_LA_VERSION_4, migrar_a_version_4),
    (5, DESCRIPCION_DE_LA_VERSION_5, migrar_a_version_5),
    (6, DESCRIPCION_DE_LA_VERSION_6, migrar_a_version_6),
    (7, DESCRIPCION_DE_LA_VERSION_7, migrar_a_version_7),
    (8, DESCRIPCION_DE_LA_VERSION_8, migrar_a_version_8),
    (9, DESCRIPCION_DE_LA_VERSION_9, migrar_a_version_9),
    (10, DESCRIPCION_DE_LA_VERSION_10, migrar_a_version_10),
    (11, DESCRIPCION_DE_LA_VERSION_11, migrar_a_version_11),
    (12, DESCRIPCION_DE_LA_VERSION_12, migrar_a_version_12),
    (13, DESCRIPCION_DE_LA_VERSION_13, migrar_a_version_13),
    # ⚠️ La 14 va DESPUES de la 12 y la 13, y ese orden importa: la 12 reconstruye
    # `casos` entera y la 14 le anade columnas. Al reves, la reconstruccion de la 12
    # tendria que conocer columnas que todavia no existian, o se las llevaria por
    # delante. El orden de esta tupla ES el orden en que se aplican.
    (14, DESCRIPCION_DE_LA_VERSION_14, migrar_a_version_14),
    # ⚠️ La 15 reconstruye `personas` entera y va DESPUES de la 14, que le anade
    # columnas a `casos` y a `procedencia_campo`. El orden importa por lo mismo que
    # entre la 12 y la 14: una reconstruccion tiene que conocer TODAS las columnas
    # que la tabla ya tiene, y las que llegaron por ALTER en las versiones 4, 5, 6
    # y 9 estan copiadas una a una en el INSERT de la 15.
    (15, DESCRIPCION_DE_LA_VERSION_15, migrar_a_version_15),
)
