"""Las dos migraciones que hacen falta para que una importacion no pierda nada.

Van en su propio modulo y no al final de `datos/migraciones.py` por una razon de
tamano: ese archivo ya tiene 521 lineas con seis migraciones dentro, y estas dos
—una reconstruccion de tabla y una tabla nueva— le anadirian otras doscientas. Un
archivo de migraciones que no se puede leer entero de una sentada es un archivo
donde la siguiente migracion se escribe sin mirar las anteriores.

`datos/migraciones.py` sigue siendo el unico sitio donde esta la lista `MIGRACIONES`
y el unico orden que cuenta: aqui solo estan las dos funciones y sus descripciones.

---

**VERSION 7 — `casos.numero_caso` deja de ser obligatorio.**

El dano que arregla esta medido en la maquina del dueno, 2026-09-02: importo un
PDF suyo y el programa contesto «Se guardaron 0 casos de 1 página, con 0 personas
en total», y la base quedo con `casos = 0`. La pagina se habia leido; se tiro
entera porque `extraccion` no encontro el numero de caso, y sin numero
`guardar_formulario` se negaba a guardar.

Eso es lo contrario de lo que este programa existe para hacer. El trabajo de leer
la pagina ya estaba hecho: los nombres, los MRN, la fecha. Tirarlo obliga a
teclear de cero lo que la maquina habia leido bien, y —peor— deja el formulario
fuera del programa, que es justo el que hay que atender.

**Lo que NO se hace, y es la mitad de la decision: no se inventa un numero.**
Regla permanente 1. La fila entra con `numero_caso` a NULL, que significa
exactamente «todavia no se sabe», y la pantalla de correccion deja teclearlo.

**Por que esto no rompe la reconciliacion por `numero_caso` + `mrn`** (el par que
sostiene el paquete de los companeros, `paquete/reconciliacion.py`):

  - `UNIQUE` sigue puesto. Medido en esta maquina (SQLite 3.50.4): tres filas con
    `numero_caso` NULL entran las tres, y un `'CASP2609'` repetido se sigue
    rechazando con «UNIQUE constraint failed». Es lo que dice la documentacion
    oficial de SQLite sobre los indices unicos y los nulos, y aqui se comprobo.
  - `CHECK` sigue puesto, con `numero_caso IS NULL OR ...` delante. Un
    `'xx'` se sigue rechazando; medido igual.
  - `resolver_persona_por_par` ya se negaba a resolver con un numero vacio
    (`datos/propuestas.py`: «if not numero_caso or not mrn: return None`), y su
    `WHERE c.numero_caso = ?` nunca casa contra NULL. Un caso sin identificar no
    reconcilia con nada hasta que alguien le ponga el numero, que es lo correcto.

---

**VERSION 8 — `documentos_ilegibles`, el renglon que se queda.**

Lo pidio el dueno el 2026-09-02: «debe poder leer letra dentro de PDF escaneados,
y si no puede debe ponerlo en un renglón que notifique que tales documentos no son
legibles». Un cuadro de dialogo con el resumen no vale: se cierra con Aceptar y no
deja nada, y con 500 documentos no hay forma de leerlo al vuelo ni de recordarlo.

Es el mismo criterio que `interfaz/descartados.py` se aplico a si mismo y dejo
anotado como decision pendiente del dueno: «si quiere que los descartes sobrevivan
al cierre del programa, es una tabla nueva y su migracion». Aqui el dueno ya lo
dijo, y esta es la tabla.

**El motivo se guarda como codigo, no como frase.** Una frase se escribe distinta
cada vez y no se puede contar; un codigo se agrupa y contesta la pregunta que hoy
nadie puede contestar: de 500 documentos, ¿cuantos no se abrieron, cuantos no
tenian ni una letra, y cuantos tenian letra pero ningun campo? La frase en espanol
se compone al mostrarla (`datos/ilegibles.py`), que es donde tiene que estar.

**Sin `CHECK` con la lista de motivos**, por el mismo criterio con el que
`casos.estado_recomendacion` no lo lleva (`DECISIONES.md`, P-1): la lista vive en
`datos/ilegibles.py` y le pueden faltar motivos que todavia no se han visto. Un
`CHECK` con una lista incompleta rechazaria manana un motivo verdadero, y entonces
el documento ilegible se perderia por culpa de la tabla que existe para no
perderlo.
"""

import sqlite3

from datos.reconstruccion_de_tablas import ErrorDeMigracion, reconstruir_tabla

DESCRIPCION_DE_LA_VERSION_7 = (
    "'casos.numero_caso' admite NULL: una pagina cuyo numero no se pudo leer se "
    "guarda igual, pendiente de identificar, en vez de tirarse entera."
)

# La tabla se copia letra por letra de como estaba al terminar la version 6 —el
# DDL de la version 1 con el CHECK de la 2 y las dos columnas de la 3— y lo unico
# que cambia es `numero_caso`: se le quita el `NOT NULL` y su `CHECK` gana el
# `IS NULL OR` de delante. Una migracion que aprovecha para retocar otra cosa de
# paso es una migracion que nadie puede revisar.
_TABLA_NUEVA_DE_LA_VERSION_7 = """
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
"""

# Las columnas se nombran una a una en los dos lados del INSERT. Un
# `INSERT INTO ... SELECT *` copiaria por posicion y una columna anadida en medio
# dejaria los datos corridos sin que nadie se entere.
_COPIA_DE_LOS_DATOS_A_LA_VERSION_7 = """
INSERT INTO casos_version_7
    (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
     unidad_nombre)
SELECT
     id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
     unidad_nombre
FROM casos
"""


def _rehacer_la_tabla_casos(conexion):
    """Los pasos 4 a 8 del procedimiento oficial, escritos uno a uno.

    Cada instruccion en su propia llamada y no recorriendo una lista de cadenas:
    `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el arbol de
    sintaxis, y una variable de bucle no la puede seguir hasta su origen.
    """
    conexion.execute(_TABLA_NUEVA_DE_LA_VERSION_7)
    conexion.execute(_COPIA_DE_LOS_DATOS_A_LA_VERSION_7)
    conexion.execute("DROP TABLE casos")
    conexion.execute("ALTER TABLE casos_version_7 RENAME TO casos")
    conexion.execute(
        "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0"
    )


def migrar_a_version_7(conexion):
    """Reconstruye `casos` para que `numero_caso` pueda quedarse sin saber."""
    try:
        reconstruir_tabla(conexion, _rehacer_la_tabla_casos)
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 7: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


DESCRIPCION_DE_LA_VERSION_8 = (
    "'documentos_ilegibles': cada PDF o pagina que no se pudo leer deja su "
    "renglon con la ruta, la pagina y el motivo, y se puede consultar despues."
)

# `caso_id` apunta al caso que SI se llego a guardar de esa pagina, cuando lo hay.
# Es lo que convierte el renglon en algo accionable: desde la lista se sabe si hay
# a donde ir o si de esa pagina no quedo nada.
#
# `ON DELETE RESTRICT` como el resto del esquema: un caso con un renglon apuntando
# no se puede borrar sin decidir antes que pasa con el renglon.
_TABLA_DE_LA_VERSION_8 = """
CREATE TABLE IF NOT EXISTS documentos_ilegibles (
    id             INTEGER PRIMARY KEY,
    ruta_pdf       TEXT    NOT NULL,
    pagina_pdf     INTEGER CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1),
    motivo         TEXT    NOT NULL,
    detalle        TEXT,
    lineas_leidas  INTEGER CHECK (lineas_leidas IS NULL OR lineas_leidas >= 0),
    caso_id        INTEGER
        REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
    registrado_en  TEXT    NOT NULL
)
"""


def migrar_a_version_8(conexion):
    """Crea la tabla de los documentos que no se pudieron leer.

    Va sin transaccion explicita: es UNA sola instruccion, y una instruccion
    suelta ya es atomica para el motor. Envolverla en `BEGIN`/`COMMIT` no anadiria
    ninguna garantia y sugeriria que hay algo que coordinar.
    """
    try:
        conexion.execute(_TABLA_DE_LA_VERSION_8)
        conexion.execute(
            "CREATE INDEX IF NOT EXISTS idx_ilegibles_ruta "
            "ON documentos_ilegibles (ruta_pdf)"
        )
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 8: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


# ==========================================================================
# VERSION 12 — `numero_caso` deja de ser UNICO.
# ==========================================================================
#
# **Lo que lo obliga, medido por el dueno en la PC del trabajo el 2026-09-03.**
# Importo diez documentos de su carpeta real y el programa contesto «6 caso ya
# existente». Un renglon, literal: «El caso BALC2609 ya estaba en la base con 1
# persona(s)... Esta pagina traia 1 persona(s)... MRN en comun: 0». **Era otra
# familia.** El numero de caso son cuatro letras mas el ano y el mes —`BALC` +
# `2609`—, o sea que identifica UNA UNIDAD Y UN MES, no una familia: en su carpeta
# muchos documentos distintos lo comparten por construccion.
#
# El `UNIQUE` de la version 1 —y el de la 7, que lo mantuvo— convertia eso en un
# rechazo: seis de diez documentos se quedaron fuera enteros. La identidad de un
# caso pasa a ser **el documento del que salio**, y el numero pasa a ser un
# atributo mas (`DECISIONES.md`, 2026-09-03).
#
# SQLite no sabe quitar un `UNIQUE` de columna con `ALTER TABLE`: hay que
# reconstruir la tabla con el procedimiento de 12 pasos que `datos/migraciones.py`
# cita literal y que `datos/reconstruccion_de_tablas.py` aplica.
DESCRIPCION_DE_LA_VERSION_12 = (
    "'casos.numero_caso' deja de ser UNICO: el numero identifica una unidad y un "
    "mes, no una familia, y dos documentos distintos lo comparten por construccion."
)

# La tabla se copia letra por letra de como quedaba al terminar la version 11 —el
# DDL de la version 7 mas `templo_nombre`, que la version 10 anadio con un
# `ALTER TABLE`— y lo unico que cambia es que `numero_caso` pierde su `UNIQUE`. El
# `CHECK` del formato se queda igual: que el numero no sea unico no significa que
# valga cualquier cosa.
_TABLA_NUEVA_DE_LA_VERSION_12 = """
CREATE TABLE casos_version_12 (
    id                   INTEGER PRIMARY KEY,
    numero_caso          TEXT
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
    templo_nombre        TEXT,

    CHECK ((archivado = 0 AND fecha_archivado IS NULL)
           OR (archivado = 1 AND fecha_archivado IS NOT NULL))
)
"""

# Las columnas se nombran una a una en los dos lados, por lo mismo que en la
# version 7: un `INSERT INTO ... SELECT *` copiaria por posicion y una columna
# anadida en medio dejaria los datos corridos sin que nadie se entere.
_COPIA_DE_LOS_DATOS_A_LA_VERSION_12 = """
INSERT INTO casos_version_12
    (id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
     unidad_nombre, templo_nombre)
SELECT
     id, numero_caso, unidad_numero, fecha_viaje, captura_manual, archivado,
     fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, pagina_pdf,
     unidad_nombre, templo_nombre
FROM casos
"""


def _rehacer_la_tabla_casos_sin_unique(conexion):
    """Los pasos 4 a 8 del procedimiento oficial, escritos uno a uno.

    Cada instruccion en su propia llamada y no recorriendo una lista de cadenas:
    `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el arbol de
    sintaxis, y una variable de bucle no la puede seguir hasta su origen.
    """
    conexion.execute(_TABLA_NUEVA_DE_LA_VERSION_12)
    conexion.execute(_COPIA_DE_LOS_DATOS_A_LA_VERSION_12)
    conexion.execute("DROP TABLE casos")
    conexion.execute("ALTER TABLE casos_version_12 RENAME TO casos")
    conexion.execute(
        "CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0"
    )
    # Indice NO unico sobre el numero. Lo que antes daba gratis el `UNIQUE` —buscar
    # un caso por su numero sin recorrer la tabla— sigue haciendo falta: la vuelta
    # del Excel del companero y la deteccion de duplicados preguntan por ese campo
    # una vez por fila.
    conexion.execute("CREATE INDEX idx_casos_numero ON casos (numero_caso)")


def migrar_a_version_12(conexion):
    """Reconstruye `casos` para que dos documentos puedan repetir numero."""
    try:
        reconstruir_tabla(conexion, _rehacer_la_tabla_casos_sin_unique)
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 12: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa


# ==========================================================================
# VERSION 13 — `casos.duplicado_de`: el caso entra igual, y se dice de cual repite.
# ==========================================================================
#
# **Lo decidio el dueno el 2026-09-03, encima de la version 12:** «Si hay
# documentos duplicados debe decirlo y no rechazarlo». Un archivo que ya se importo
# entra como cualquier otro y queda marcado; lo que sigue prohibido es PISAR el que
# ya estaba, con sus correcciones a mano y sus firmas. El duplicado es un caso
# aparte y Miguel decide que hacer con el: **no se fusiona nada solo**.
#
# Va como columna y no como tabla aparte porque es un dato DEL caso —«este caso
# repite a aquel»—, se consulta en todas las listas que ya leen `casos`, y una
# tabla de dos columnas obligaria a un `JOIN` en cada una de ellas.
#
# `REFERENCES casos (id)` con `RESTRICT`, como el resto del esquema: un caso al que
# apunta un duplicado no se puede borrar sin decidir antes que pasa con el.
DESCRIPCION_DE_LA_VERSION_13 = (
    "'casos.duplicado_de': un documento que repite a otro ENTRA igual y queda "
    "marcado con el caso del que es duplicado. Nunca se pisa el que ya estaba."
)


def migrar_a_version_13(conexion):
    """Anade `duplicado_de` a `casos`.

    Va con `ALTER TABLE ADD COLUMN` y no reconstruyendo la tabla porque la
    documentacion oficial de SQLite
    (<https://www.sqlite.org/lang_altertable.html>, «ALTER TABLE ADD COLUMN»,
    consultada 2026-09-03) lo admite con una condicion que aqui se cumple: si se
    anade una columna con `REFERENCES` y las claves foraneas estan encendidas, su
    valor por defecto tiene que ser NULL. Esta columna no lleva `NOT NULL` y su
    defecto es NULL, que ademas es lo correcto: un caso que no repite a ninguno no
    tiene nada que decir.

    Sin transaccion explicita: es UNA sola instruccion, y una instruccion suelta ya
    es atomica para el motor.
    """
    try:
        conexion.execute(
            "ALTER TABLE casos ADD COLUMN duplicado_de INTEGER "
            "REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT"
        )
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 13: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa
