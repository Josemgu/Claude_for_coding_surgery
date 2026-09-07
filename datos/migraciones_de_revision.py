"""La migracion que la pantalla «Revisar» necesita: quien marco el estado y cuando.

⚠️ **ES LA VERSION 14, NO LA 13.** El pase pedia «tu migracion numero 13». Medido
antes de escribir nada, el 2026-09-03:

    $ python -c "from datos.migraciones import MIGRACIONES; print([v for v,_,_ in MIGRACIONES])"
    [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]

La 12 y la 13 ya estan escritas en `datos/migraciones_de_importacion.py` —son de
otro pase que corre en paralelo: `numero_caso` deja de ser unico, y
`casos.duplicado_de`— y ya estan registradas en la lista. Escribir otra 13 habria
dado dos migraciones con el mismo numero: la segunda no se aplicaria nunca, porque
`datos/esquema.py` solo aplica las que superan la version que la base ya tiene, y
nadie se enteraria hasta que una consulta preguntara por una columna que no existe.

**Esta funcion NO esta registrada en `MIGRACIONES` a proposito.** `datos/migraciones.py`
esta en manos de otro programador en esta misma sesion; el supervisor la registra al
cerrar los tres pases. Hasta entonces, quien la quiera aplicar la llama a mano —es lo
que hacen sus pruebas—.

**Que anade y por que.**

`casos` gana SEIS columnas, en dos grupos de tres, y esa duplicacion es a proposito:

  - `estado_marcado_por`, `estado_marcado_en`, `estado_marcado_origen`: quien puso el
    estado que vale AHORA, cuando, y de donde salio. Es lo que se lee en la tarjeta.
  - `estado_del_companero`, `estado_del_companero_por`, `estado_del_companero_en`: lo
    que dijo la hoja del companero, que **no se borra cuando Miguel corrige encima**.
    Palabras del dueno el 2026-09-03: «lo que Miguel marque a mano despues manda, y
    queda "Corregida por Miguel" encima de lo del companero, sin borrar lo que dijo el
    companero». Con un solo grupo de columnas eso no se puede cumplir: la correccion
    de Miguel pisaria el nombre de Sandy y nadie sabria que discreparon.

Van como columnas planas y no como una tabla de historial con un `JOIN`, y el motivo
esta medido en el propio pase: «imagina que tenga 3000 formularios». La lista de
«Revisar» lee una fila por documento y no puede pagar un `JOIN` correlacionado por
tarjeta. Lo que se pierde a cambio, dicho: si la misma hoja vuelve dos veces, la
segunda pisa a la primera. Guardar TODAS las marcas de la historia es una tabla nueva
y una decision de arquitectura — **no la tomo yo**, va al planificador.

`procedencia_campo` gana `ausente_en_el_papel`: un campo que el papel no trae no es un
fallo de lectura ni un borrado (DECISIONES.md, 2026-09-03, decision 2). Es la misma
forma que `anulado_por_tachon` de la version 3, y por el mismo motivo: sin ella, «no
esta en el papel» se ve exactamente igual que «el OCR no supo leerlo».

Todo con `ALTER TABLE ... ADD COLUMN`, sin reconstruir ninguna tabla. La documentacion
oficial (<https://www.sqlite.org/lang_altertable.html>, seccion «ADD COLUMN»,
consultada 2026-09-03) lo admite con las condiciones que aqui se cumplen: ninguna
columna es PRIMARY KEY ni UNIQUE, las que llevan `REFERENCES` nacen con defecto NULL,
y la unica `NOT NULL` nace con un defecto constante.
"""

import sqlite3

from datos.reconstruccion_de_tablas import ErrorDeMigracion

VERSION_DE_LA_REVISION = 14

DESCRIPCION_DE_LA_VERSION_14 = (
    "'casos' gana quien marco el estado de la recomendacion, cuando y de donde "
    "salio, mas lo que dijo la hoja del companero aparte para que la correccion de "
    "Miguel no lo borre; 'procedencia_campo' gana 'ausente_en_el_papel'."
)

# Los nombres en un solo sitio, para que el resto del programa no los escriba a mano
# repartidos. Nadie arma SQL con estas constantes: las instrucciones de abajo van
# escritas letra por letra dentro de su llamada, que es lo que `pruebas/auditoria_sql.py`
# puede seguir hasta su origen.
COLUMNAS_DE_LA_MARCA = (
    "estado_marcado_por",
    "estado_marcado_en",
    "estado_marcado_origen",
)
COLUMNAS_DE_LA_MARCA_DEL_COMPANERO = (
    "estado_del_companero",
    "estado_del_companero_por",
    "estado_del_companero_en",
)
COLUMNA_DE_LO_AUSENTE = "ausente_en_el_papel"


def _anadir_quien_marco_el_estado(conexion):
    """Quien puso el estado que vale ahora, cuando, y de donde salio.

    `estado_marcado_por` apunta a `companeros` y no a un texto libre con el nombre,
    igual que `procedencia_campo.verificado_por`: un nombre escrito a mano se
    escribe de dos formas el mismo dia y entonces no se puede agrupar por quien.
    Miguel es una fila de `companeros` como cualquier otro — ya tenia que serlo para
    poder firmar campos.

    `estado_marcado_en` va PRIMERO que su `CHECK`... no: va primero a secas, y el
    `CHECK` que ata los dos se pone en `estado_marcado_en`, que es la segunda. Una
    restriccion no puede nombrar una columna que todavia no existe, y es el mismo
    orden que la version 6 uso para `motivo_no_viajo` y `pudo_viajar`.

    El `CHECK` ata quien con cuando y nada mas. **No ata el estado con la firma**, y
    eso es deliberado: hay casos guardados hoy con `estado_recomendacion` puesto y sin
    firma —los escribio `datos/repositorio.py` antes de que estas columnas existieran—
    y un `CHECK` que los exigiera juntos convertiria esas filas en filas que ya no se
    pueden actualizar nunca mas, sin avisar y desde otro modulo.

    `estado_marcado_origen` es texto libre: la ruta del Excel que trajo la marca, o la
    frase que dice que se puso a mano. No es un catalogo cerrado y no se inventa uno.
    """
    conexion.execute(
        "ALTER TABLE casos ADD COLUMN estado_marcado_por INTEGER "
        "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT"
    )
    conexion.execute(
        "ALTER TABLE casos ADD COLUMN estado_marcado_en TEXT CHECK ("
        "  (estado_marcado_por IS NULL AND estado_marcado_en IS NULL)"
        "  OR (estado_marcado_por IS NOT NULL AND estado_marcado_en IS NOT NULL))"
    )
    conexion.execute("ALTER TABLE casos ADD COLUMN estado_marcado_origen TEXT")


def _anadir_lo_que_dijo_el_companero(conexion):
    """Lo que trajo la hoja del companero, guardado aparte para que no se pierda.

    Se escribe a la vez que la marca cuando la marca viene de una hoja, y **no se
    toca** cuando Miguel corrige encima. Asi la tarjeta puede decir las dos cosas:
    «Corregida por Miguel» arriba y «Sandy dijo: completa» debajo.

    `estado_del_companero` va sin `CHECK` de lista, por el mismo motivo que
    `casos.estado_recomendacion` (DECISIONES.md, P-1): la lista de valores validos
    vive en `datos/estados.py` y ahi es donde se anade uno nuevo, no aqui.
    """
    conexion.execute("ALTER TABLE casos ADD COLUMN estado_del_companero TEXT")
    conexion.execute(
        "ALTER TABLE casos ADD COLUMN estado_del_companero_por INTEGER "
        "REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT"
    )
    conexion.execute(
        "ALTER TABLE casos ADD COLUMN estado_del_companero_en TEXT CHECK ("
        "  (estado_del_companero_por IS NULL AND estado_del_companero_en IS NULL)"
        "  OR (estado_del_companero_por IS NOT NULL"
        "      AND estado_del_companero_en IS NOT NULL))"
    )


def _anadir_lo_que_no_esta_en_el_papel(conexion):
    """El campo que el formulario no trae, distinto de vacio y de no leido.

    `NOT NULL DEFAULT 0` con `CHECK` de 0/1, la misma forma que `anulado_por_tachon`
    de la version 3, que ya se midio que el motor aplica de verdad sobre una columna
    anadida asi. El 0 por defecto no inventa nada sobre las filas que ya estaban: dice
    «nadie ha marcado que este campo falte del papel», que es exactamente cierto.
    """
    conexion.execute(
        "ALTER TABLE procedencia_campo ADD COLUMN ausente_en_el_papel INTEGER "
        "NOT NULL DEFAULT 0 CHECK (ausente_en_el_papel IN (0, 1))"
    )


def migrar_a_version_14(conexion):
    """Anade las siete columnas nuevas: las siete o ninguna.

    En transaccion por lo mismo que las versiones 3, 5, 6 y 9: siete columnas a
    medias dejan a la pantalla leyendo una que no existe, y ese fallo aparece mucho
    despues y en otro sitio.
    """
    try:
        conexion.execute("BEGIN")
        try:
            _anadir_quien_marco_el_estado(conexion)
            _anadir_lo_que_dijo_el_companero(conexion)
            _anadir_lo_que_no_esta_en_el_papel(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    except sqlite3.Error as causa:
        raise ErrorDeMigracion(
            f"No se pudo migrar la base a la versión 14: {causa}. La base se queda "
            "en la versión anterior y no se perdió ningún dato."
        ) from causa
