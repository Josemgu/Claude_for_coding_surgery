"""Las hojas del espejo: que columnas lleva cada una y de que clase es cada una.

Las columnas salen una a una de `docs/ARQUITECTURA.md` §2, que manda sobre
cualquier plan (`CLAUDE.md` §7). No se anade ninguna que ese documento no declare,
ni se calcula ninguna: un espejo que calcula deja de ser un espejo y pasa a ser un
reporte, y entonces hay dos verdades.

Las consultas viven aqui, junto a la definicion de la hoja que alimentan, y no en
`datos/repositorio.py`. La razon: `repositorio.py` contesta preguntas del dominio
—«el caso numero X», «las personas de este caso»—, y estas cuatro no son preguntas,
son el volcado entero de una tabla en el orden en que el espejo la muestra. Son
cadenas literales sin parametros y sin dato de usuario, como exige
`docs/ARQUITECTURA.md` §1.7.

Y cada una se ejecuta en su propia funcion, con su SQL escrito dentro de la
llamada, en vez de guardarse como texto en la definicion de la hoja y ejecutarse
desde un sitio comun. No es preferencia de estilo: `pruebas/auditoria_sql.py` lee
el arbol de sintaxis para enumerar TODAS las llamadas al motor y dictaminar cada
una, y un `conexion.execute(definicion.consulta)` le sale «no resuelta» —no puede
seguir un atributo hasta la cadena—. Una lista blanca con huecos deja de servir.

Las tres clases de columna, y por que hay tres y no una:

  - `TEXTO`   — la celda se marca con formato de texto (`@`). Es la unica defensa
                contra que Excel se coma los ceros de delante de `055-1111-3853`.
  - `TEMPORAL`— el valor sale de la base como cadena ISO-8601 y entra en la celda
                como objeto `date` o `datetime` de Python. Sin eso, ordenar la
                columna en Excel ordena alfabeticamente, que casualmente funciona
                dentro de un mismo formato y deja de funcionar en cuanto no lo es.
  - `CRUDO`   — enteros y texto libre, tal como vienen.
"""

from collections import namedtuple

TEXTO = "texto"
TEMPORAL = "temporal"
CRUDO = "crudo"

Columna = namedtuple("Columna", ("nombre", "clase"))
Hoja = namedtuple("Hoja", ("nombre", "leer_filas", "columnas"))


def _crudas(*nombres):
    """Atajo para las columnas que entran en la celda tal como salen de la base."""
    return tuple(Columna(nombre, CRUDO) for nombre in nombres)


def leer_filas_de_casos(conexion):
    """Todos los casos, en el orden en que se dieron de alta."""
    return conexion.execute(
        "SELECT id, numero_caso, unidad_numero, unidad_nombre, templo_nombre, "
        "fecha_viaje, pagina_pdf, captura_manual, archivado, fecha_archivado, "
        "ruta_pdf, creado_en, estado_recomendacion, duplicado_de, "
        "estado_marcado_por, estado_marcado_en, estado_marcado_origen, "
        "estado_del_companero, estado_del_companero_por, estado_del_companero_en "
        "FROM casos ORDER BY id"
    ).fetchall()


def leer_filas_de_personas(conexion):
    """Todas las personas, agrupadas por su caso y en el orden del formulario."""
    return conexion.execute(
        "SELECT id, caso_id, mrn, nombre, fila_formulario, pagina_pdf, "
        "ord_recibir_propias, ord_observar_sellamiento, ord_traductor, "
        "ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres, "
        "estado_propuesto, nota_companero, propuesto_por, propuesto_en, "
        "pudo_viajar, motivo_no_viajo, "
        "paso_preparacion, paso_informacion, paso_cita_del_templo, "
        "paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo, "
        "llamo_al_lider "
        "FROM personas ORDER BY caso_id, fila_formulario, id"
    ).fetchall()


def leer_filas_de_asignaciones(conexion):
    """Todas las asignaciones, las vivas y las desactivadas."""
    return conexion.execute(
        "SELECT id, caso_id, companero_id, asignado_en, activa, desactivada_en "
        "FROM asignaciones ORDER BY id"
    ).fetchall()


def leer_filas_de_contactos(conexion):
    """Todos los contactos, los vigentes y los anulados con su motivo."""
    return conexion.execute(
        "SELECT id, caso_id, fecha, medio, con_quien, resultado, anulado, "
        "motivo_anulacion, anulado_en, registrado_en, contactado_por, respondio "
        "FROM contactos ORDER BY id"
    ).fetchall()


def leer_filas_de_documentos_ilegibles(conexion):
    """Todo lo que no entro entero, en el orden en que se fue anotando."""
    return conexion.execute(
        "SELECT id, ruta_pdf, pagina_pdf, motivo, detalle, lineas_leidas, caso_id, "
        "registrado_en FROM documentos_ilegibles ORDER BY id"
    ).fetchall()


# `casos` (docs/ARQUITECTURA.md §2.2). `unidad_numero` va en TEXTO por §1.4:
# es un numero que puede empezar por cero y un `INTEGER` se lo comeria.
#
# `unidad_nombre` y `pagina_pdf` entraron con la version 3 del esquema, y entran
# aqui a la vez: un espejo al que le falta una columna de la tabla ya no es un
# espejo. `unidad_nombre` va en CRUDO —es texto libre— y `pagina_pdf` tambien: es
# un entero de verdad, no un numero con ceros delante, y en Excel se ordena mejor
# como numero.
#
# `templo_nombre` entro con la version 10 y llego aqui por la misma puerta, pero no
# por haberlo recordado: `pruebas/prueba_espejo.py` compara las columnas de cada
# hoja contra las de su tabla y se puso roja sola. Va en CRUDO, que es texto libre:
# no hay catalogo de templos y no se valida contra ninguna lista (DECISIONES.md,
# 2026-09-03).
HOJA_DE_CASOS = Hoja(
    nombre="casos",
    leer_filas=leer_filas_de_casos,
    columnas=(
        Columna("id", CRUDO),
        Columna("numero_caso", CRUDO),
        Columna("unidad_numero", TEXTO),
        Columna("unidad_nombre", CRUDO),
        Columna("templo_nombre", CRUDO),
        Columna("fecha_viaje", TEMPORAL),
        Columna("pagina_pdf", CRUDO),
        Columna("captura_manual", CRUDO),
        Columna("archivado", CRUDO),
        Columna("fecha_archivado", TEMPORAL),
        Columna("ruta_pdf", CRUDO),
        Columna("creado_en", TEMPORAL),
        Columna("estado_recomendacion", CRUDO),
        # `duplicado_de` entro con la version 13 del esquema y entra aqui a la vez,
        # por lo mismo que `pagina_pdf` en su dia: un espejo al que le falta una
        # columna de la tabla ya no es un espejo. Es el `id` del caso que este
        # repite, o vacio.
        Columna("duplicado_de", CRUDO),
        # Las seis de la version 14: quien marco el estado del documento, cuando y
        # desde donde, y lo que dijo el companero antes de que Miguel lo corrigiera.
        # Entran aqui por lo mismo que las anteriores, y ademas porque son
        # exactamente lo que el dueno querra mirar en el Excel el dia que pregunte
        # «¿quien dio esto por completo?».
        Columna("estado_marcado_por", CRUDO),
        Columna("estado_marcado_en", TEMPORAL),
        Columna("estado_marcado_origen", CRUDO),
        Columna("estado_del_companero", CRUDO),
        Columna("estado_del_companero_por", CRUDO),
        Columna("estado_del_companero_en", TEMPORAL),
    ),
)

# `personas` (§2.3). `pagina_pdf` entro con la version 4 del esquema y entra aqui
# a la vez, por el mismo motivo que la de `casos`: un espejo al que le falta una
# columna de la tabla ya no es un espejo. Va en CRUDO —es un entero de verdad, no
# un numero con ceros delante— y dice de que hoja del PDF salio esa persona, que
# en un grupo de seis hojas no es la misma que la del caso.
#
# Las seis casillas van en el orden literal de `DECISIONES.md`,
# el mismo de `datos.repositorio.CASILLAS_DE_ORDENANZAS`. Se dejan como 0/1 y no se
# traducen a «si»/«no»: una casilla vacia significa «no leida», que NO es lo mismo
# que «leida y sin marcar», y esa diferencia se pierde en cuanto se traduce.
HOJA_DE_PERSONAS = Hoja(
    nombre="personas",
    leer_filas=leer_filas_de_personas,
    columnas=(
        Columna("id", CRUDO),
        Columna("caso_id", CRUDO),
        Columna("mrn", TEXTO),
        Columna("nombre", CRUDO),
        Columna("fila_formulario", CRUDO),
        Columna("pagina_pdf", CRUDO),
        *_crudas(
            "ord_recibir_propias",
            "ord_observar_sellamiento",
            "ord_traductor",
            "ord_investidura",
            "ord_sellamiento_esposos",
            "ord_sellamiento_hijo_padres",
        ),
        # Las cuatro de la propuesta del companero, que entraron con la version 5
        # del esquema. Entran aqui por el mismo motivo que las anteriores: un
        # espejo al que le falta una columna de la tabla ya no es un espejo.
        #
        # `estado_propuesto` va en CRUDO y NO se traduce a nada: es una PROPUESTA,
        # y `casos.estado_recomendacion` —lo que Miguel confirmo— es otra columna
        # y esta en su hoja. Juntarlas en el espejo haria imposible distinguir lo
        # propuesto de lo confirmado, que es la distincion entera de la FASE 6.
        Columna("estado_propuesto", CRUDO),
        Columna("nota_companero", CRUDO),
        Columna("propuesto_por", CRUDO),
        Columna("propuesto_en", TEMPORAL),
        # Las dos del resultado del viaje, que entraron con la version 6 del
        # esquema y NO llegaron a llegar aqui hasta que una prueba las echo de
        # menos (`pruebas/prueba_espejo.py`, «a ninguna hoja le falta una columna
        # de su tabla»). Sin ellas el Excel no decia quien no habia podido viajar
        # ni por que, que es justo lo que el reporte de la FASE 8 lleva a los jefes.
        #
        # `pudo_viajar` va en CRUDO y se deja como 0/1/vacio, sin traducir a
        # «sí»/«no»: tiene TRES estados —vacio significa «nadie lo ha dicho
        # todavia»— y esa diferencia se pierde en cuanto se traduce. Es el mismo
        # motivo por el que `respondio` se deja igual en la hoja de contactos.
        Columna("pudo_viajar", CRUDO),
        Columna("motivo_no_viajo", CRUDO),
        # Los seis pasos de «Preparacion para las ordenanzas» y la llamada al
        # lider, que entraron con la version 9 del esquema. Son lo que el companero
        # devuelve en su hoja y son parte de la misma propuesta que las cuatro de
        # arriba: las firma el mismo `propuesto_por`.
        #
        # Van en CRUDO como 0/1/vacio y NO se traducen a «Sí»/«No», por el mismo
        # motivo que las seis casillas y que `pudo_viajar`: son TRES estados, y el
        # vacio —«nadie miro ese paso»— es justo el que se pierde al traducir. Una
        # persona sin mirar y una con el paso reprobado no se pueden ver igual en un
        # espejo que existe para poder comprobar la base sin abrirla.
        *_crudas(
            "paso_preparacion",
            "paso_informacion",
            "paso_cita_del_templo",
            "paso_acciones_requeridas",
            "paso_entrevistas",
            "paso_listo_para_el_templo",
            "llamo_al_lider",
        ),
    ),
)

# `asignaciones` (§2.5).
HOJA_DE_ASIGNACIONES = Hoja(
    nombre="asignaciones",
    leer_filas=leer_filas_de_asignaciones,
    columnas=(
        Columna("id", CRUDO),
        Columna("caso_id", CRUDO),
        Columna("companero_id", CRUDO),
        Columna("asignado_en", TEMPORAL),
        Columna("activa", CRUDO),
        Columna("desactivada_en", TEMPORAL),
    ),
)

# `contactos` (§2.6). ⚠️ Esa seccion se declara PROVISIONAL: sus columnas salen de
# una sola frase y su fase (la 7) exige una entrada del dueno en `DECISIONES.md`
# antes de cerrar. Si esas columnas cambian, esta hoja cambia con ellas.
HOJA_DE_CONTACTOS = Hoja(
    nombre="contactos",
    leer_filas=leer_filas_de_contactos,
    columnas=(
        Columna("id", CRUDO),
        Columna("caso_id", CRUDO),
        Columna("fecha", TEMPORAL),
        Columna("medio", CRUDO),
        Columna("con_quien", CRUDO),
        Columna("resultado", CRUDO),
        Columna("anulado", CRUDO),
        Columna("motivo_anulacion", CRUDO),
        Columna("anulado_en", TEMPORAL),
        Columna("registrado_en", TEMPORAL),
        # Las dos de la version 5 del esquema: quien hizo el contacto y si el
        # lider respondio. `respondio` se deja en 0/1 y no se traduce a «si»/«no»
        # porque tiene TRES estados —vacio significa «todavia no se sabe»—, y esa
        # diferencia se pierde en cuanto se traduce.
        Columna("contactado_por", CRUDO),
        Columna("respondio", CRUDO),
    ),
)

# `documentos_ilegibles` (version 8 del esquema). Entra en el espejo por lo mismo
# que entraron `unidad_nombre` y las dos `pagina_pdf`: **un espejo al que le falta
# una tabla de la base ya no es un espejo**, y aqui ademas es lo que el dueno pidio
# — un renglon por documento que no entro, que se pueda consultar despues. En el
# Excel se puede ademas filtrar por motivo y ordenar por fecha, que es justo lo que
# hace falta con cientos de documentos.
#
# `detalle` va en CRUDO y no en TEXTO: es texto libre largo, no un numero con ceros
# delante que Excel pueda estropear.
HOJA_DE_DOCUMENTOS_ILEGIBLES = Hoja(
    nombre="documentos_ilegibles",
    leer_filas=leer_filas_de_documentos_ilegibles,
    columnas=(
        Columna("id", CRUDO),
        Columna("ruta_pdf", CRUDO),
        Columna("pagina_pdf", CRUDO),
        Columna("motivo", CRUDO),
        Columna("detalle", CRUDO),
        Columna("lineas_leidas", CRUDO),
        Columna("caso_id", CRUDO),
        Columna("registrado_en", TEMPORAL),
    ),
)

# El orden de esta tupla es el orden de las pestanas en el libro.
HOJAS = (
    HOJA_DE_CASOS,
    HOJA_DE_PERSONAS,
    HOJA_DE_ASIGNACIONES,
    HOJA_DE_CONTACTOS,
    HOJA_DE_DOCUMENTOS_ILEGIBLES,
)
