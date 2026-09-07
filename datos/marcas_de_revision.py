"""Quien escribe el estado de un documento: la hoja del companero, o Miguel encima.

Va aparte de `datos/revision.py` porque no hace lo mismo: alli se lee, aqui se
escribe, y son las dos unicas escrituras de esta pantalla.

⚠️ **Como queda la regla permanente 5 aqui, y por decision del dueno.** CLAUDE.md
dice «nada se marca como verificado automaticamente: el sistema propone, Miguel
confirma». El dueno la partio en dos el 2026-09-03, literal:

    «La firma de campos ("Todo correcto", verificado_por) sigue siendo de Miguel;
    el ESTADO de la recomendacion lo pone el Excel del companero con su nombre.
    No mezcles las dos cosas.»

Y el motivo, tambien suyo: «me enviaron 300, no me puedo poner a ver uno a uno a ver
cual se completo y cual no».

Asi que aqui hay DOS cosas que no se mezclan:

  - `procedencia_campo.verificado` / `verificado_por` — la firma por campo. **No se
    toca en este modulo.** Sigue siendo de Miguel y sigue exigiendo su clic.
  - `casos.estado_recomendacion` — el estado del documento. Lo escribe la carga de la
    hoja del companero, **firmado con el nombre del companero**, no con el de Miguel.

⚠️ Esto contradice la lectura literal de la regla permanente 5 de `CLAUDE.md`, que es
un documento que este pase tiene prohibido tocar. **Queda dicho aqui para que el
supervisor lo lleve a `CLAUDE.md` y a `DECISIONES.md`**: mientras esos dos documentos
no lo recojan, el codigo y las reglas del proyecto no dicen lo mismo.

Lo que Miguel marca a mano despues manda, y **no borra lo que dijo el companero**:
esa marca vive en sus tres columnas propias (`estado_del_companero*`) y la de Miguel
en las suyas (`estado_marcado_*`).
"""

from datos.esquema import marca_de_tiempo
from datos.estados import COMPLETA, NO_COMPLETA
from datos.revision import estado_propuesto_del_documento
from datos.validacion import validar_estado_recomendacion

# De donde salio una marca puesta con los dos botones de la tarjeta. Es texto libre
# en la base —no hay catalogo de origenes y no se inventa uno—, pero la frase se
# escribe en un solo sitio para que dos pantallas no la escriban distinta.
ORIGEN_A_MANO = "a mano, en la pantalla «Revisar»"

# El valor por defecto de `propuesto_desde`: una fecha anterior a cualquier marca de
# tiempo del esquema, para que «desde siempre» sea un caso mas de la misma consulta y
# no un `WHERE` distinto. Se compara como TEXTO, igual que el resto de fechas del
# programa (docs/ARQUITECTURA.md §1.2).
INICIO_DE_LOS_TIEMPOS = "0000-01-01 00:00:00"


class ErrorDeMarca(ValueError):
    """A una marca de estado se le paso algo que no se puede guardar."""


class ResumenDeLaCarga:
    """Cuantos documentos quedaron de cada forma tras aplicar una hoja devuelta.

    Los cinco numeros son los que el dueno pidio ver al terminar la carga, y suman
    algo que se puede comprobar: `completas + no_completas + sin_respuesta` es lo que
    volvio en la hoja, y `sin_devolver` y `sin_casar` son lo que no volvio y lo que
    volvio y no encajo con ningun documento.
    """

    def __init__(self, completas, no_completas, sin_respuesta, sin_devolver, sin_casar):
        self.completas = completas
        self.no_completas = no_completas
        self.sin_respuesta = sin_respuesta
        self.sin_devolver = sin_devolver
        self.sin_casar = sin_casar

    @property
    def marcados(self):
        """Cuantos documentos cambiaron de estado con esta carga."""
        return len(self.completas) + len(self.no_completas)

    def texto(self):
        """El resumen de una linea por cifra que se ensena con «Entendido».

        Sin parrafos: son cinco lineas de «rotulo: numero», que es lo que el dueno
        pidio para no mirar 300 documentos uno a uno.
        """
        return "\n".join(
            (
                f"Completadas: {len(self.completas)}",
                f"No completas: {len(self.no_completas)}",
                f"Devueltas a medias, sin marcar: {len(self.sin_respuesta)}",
                f"Sin devolver: {self.sin_devolver}",
                f"Filas que no casaron con ningún documento: {self.sin_casar}",
            )
        )


def _exigir_companero(conexion, companero_id):
    """Que quien firma exista. Sin esto la marca quedaria firmada por un hueco."""
    fila = conexion.execute(
        "SELECT id, nombre FROM companeros WHERE id = ?", (companero_id,)
    ).fetchone()
    if fila is None:
        raise ErrorDeMarca(
            f"No hay ningún compañero con el id {companero_id!r}, y una marca de "
            "estado sin quien la puso no dice nada. Hay que darlo de alta antes."
        )
    return fila


def marcar_a_mano(conexion, caso_id, estado, companero_id):
    """El clic de Miguel en «Sí, completa» o «No está completa». Manda sobre la hoja.

    Escribe el estado y las tres columnas de la marca —quien, cuando, de donde—, y
    **no toca las tres del companero**: por eso la tarjeta puede decir «Corregida por
    Miguel» encima de «Sandy dijo: completa» sin haber perdido lo segundo.

    `estado` se valida contra `datos/estados.py`, que es el unico sitio donde vive la
    lista. Un estado inventado aqui apagaria el bloque rojo de viajes proximos sin
    que nadie lo viera.
    """
    if estado not in (COMPLETA, NO_COMPLETA):
        raise ErrorDeMarca(
            f"Los dos botones de la tarjeta solo escriben {COMPLETA!r} o "
            f"{NO_COMPLETA!r}, y se recibió {estado!r}."
        )
    validar_estado_recomendacion(estado)
    _exigir_companero(conexion, companero_id)

    cambiadas = conexion.execute(
        "UPDATE casos SET estado_recomendacion = ?, estado_marcado_por = ?, "
        "estado_marcado_en = ?, estado_marcado_origen = ? WHERE id = ?",
        (estado, companero_id, marca_de_tiempo(), ORIGEN_A_MANO, caso_id),
    ).rowcount
    if not cambiadas:
        raise ErrorDeMarca(
            f"No hay ningún documento con el id {caso_id!r}: no se marcó nada."
        )
    return estado


# El companero que firma la marca de un documento es el de la PRIMERA persona del
# formulario que trajo propuesta. En la practica una hoja devuelta es de un solo
# companero, asi que el «primero» y el «unico» son el mismo; cuando no lo sean, la
# marca se firma con uno y **la propuesta de cada persona sigue guardada con la
# suya**, que es donde se puede mirar quien dijo que.
_CONSULTA_DE_LO_QUE_VOLVIO = """
SELECT c.id,
       c.numero_caso,
       pe.personas,
       pe.con_propuesta,
       pe.con_algun_no,
       pe.con_los_seis_si,
       (SELECT p2.propuesto_por FROM personas p2
         WHERE p2.caso_id = c.id AND p2.propuesto_por IS NOT NULL
         ORDER BY p2.fila_formulario, p2.id LIMIT 1) AS companero_que_propuso
FROM casos c
JOIN (
    SELECT p.caso_id,
           COUNT(*) AS personas,
           SUM(CASE WHEN p.propuesto_por IS NOT NULL THEN 1 ELSE 0 END) AS con_propuesta,
           SUM(CASE WHEN p.paso_preparacion = 0 OR p.paso_informacion = 0
                      OR p.paso_cita_del_templo = 0 OR p.paso_acciones_requeridas = 0
                      OR p.paso_entrevistas = 0 OR p.paso_listo_para_el_templo = 0
                    THEN 1 ELSE 0 END) AS con_algun_no,
           SUM(CASE WHEN p.paso_preparacion = 1 AND p.paso_informacion = 1
                     AND p.paso_cita_del_templo = 1 AND p.paso_acciones_requeridas = 1
                     AND p.paso_entrevistas = 1 AND p.paso_listo_para_el_templo = 1
                    THEN 1 ELSE 0 END) AS con_los_seis_si
    FROM personas p GROUP BY p.caso_id
) pe ON pe.caso_id = c.id
WHERE c.archivado = 0 AND pe.con_propuesta > 0
  AND EXISTS (SELECT 1 FROM personas p3
               WHERE p3.caso_id = c.id AND p3.propuesto_por IS NOT NULL
                 AND p3.propuesto_en >= ?)
"""


def _repartir_lo_que_volvio(conexion, propuesto_desde):
    """Los documentos con propuesta, repartidos en los tres montones que se escriben.

    Devuelve `(completas, no_completas, sin_respuesta)`, cada uno una lista de
    `(caso_id, numero_caso, companero_que_propuso)`. `sin_respuesta` son los que
    volvieron a medias —alguna casilla en blanco— y **no se marcan**: dar por
    incompleto lo que nadie miro es tan inventado como darlo por completo, y de todas
    formas un `estado_recomendacion` vacio ya cuenta como sin resolver en el bloque
    rojo, asi que no marcarlos no esconde nada.

    ⚠️ `propuesto_desde` no es un adorno y sin el esto tiene un fallo serio: sin
    filtrar por cuando llego la propuesta, cargar la SEGUNDA hoja volveria a marcar
    todos los documentos de la primera —con la ruta del archivo equivocada en
    `estado_marcado_origen`, y pisando lo que Miguel hubiera corregido a mano en
    ellos—. Solo entra lo que trajo ESTA carga.
    """
    completas, no_completas, sin_respuesta = [], [], []
    filas = conexion.execute(
        _CONSULTA_DE_LO_QUE_VOLVIO, (propuesto_desde,)
    ).fetchall()
    for fila in filas:
        propuesta = estado_propuesto_del_documento(fila)
        destino = {
            COMPLETA: completas,
            NO_COMPLETA: no_completas,
            None: sin_respuesta,
        }[propuesta]
        destino.append((fila["id"], fila["numero_caso"], fila["companero_que_propuso"]))
    return completas, no_completas, sin_respuesta


def _contar_lo_que_no_volvio(conexion, ruta_excel):
    """Cuantos documentos sin archivar no traen ninguna propuesta, y cuantas filas
    del Excel no casaron con ninguno.

    Lo segundo sale de `filas_descartadas`, que es donde la reconciliacion deja lo que
    volvio y no entro. Se cuenta por la ruta del archivo: dos cargas distintas no se
    suman en la misma cifra.
    """
    sin_devolver = conexion.execute(
        "SELECT COUNT(*) FROM casos c WHERE c.archivado = 0 AND NOT EXISTS ("
        "  SELECT 1 FROM personas p WHERE p.caso_id = c.id "
        "  AND p.propuesto_por IS NOT NULL)"
    ).fetchone()[0]
    sin_casar = conexion.execute(
        "SELECT COUNT(*) FROM filas_descartadas WHERE ruta_excel = ?", (ruta_excel,)
    ).fetchone()[0]
    return sin_devolver, sin_casar


def aplicar_las_marcas_de_la_hoja(conexion, ruta_excel, propuesto_desde=INICIO_DE_LOS_TIEMPOS):
    """Escribe el estado de cada documento que volvio, firmado por su companero.

    Se llama DESPUES de que la carga del Excel haya guardado las propuestas: esto no
    lee el archivo, lee lo que la carga dejo en la base. Ese es el limite a proposito
    —la lectura del Excel es de `paquete/`— y hace que esta funcion se pueda probar
    sin ningun `.xlsx`.

    Todo en UNA transaccion y con `executemany`: son hasta 3000 documentos, y 3000
    `UPDATE` sueltos en autoconfirmacion son 3000 escrituras a disco. Si algo falla,
    no queda media carga aplicada.

    No pregunta nada: el dueno lo decidio asi el 2026-09-03 —«el documento que ellos
    llenan de Excel es el que marca»—. El resumen se ensena DESPUES, y lo que Miguel
    no comparta lo corrige con los dos botones de la tarjeta.

    **`propuesto_desde` lo pone quien carga**, con la marca de tiempo de justo antes
    de empezar a leer el Excel. Por defecto entra todo, que es lo correcto la primera
    vez y lo peligroso a partir de la segunda: ver `_repartir_lo_que_volvio`.
    """
    completas, no_completas, sin_respuesta = _repartir_lo_que_volvio(
        conexion, propuesto_desde
    )
    cuando = marca_de_tiempo()
    escrituras = [
        (estado, companero, cuando, ruta_excel, estado, companero, cuando, caso_id)
        for estado, monton in ((COMPLETA, completas), (NO_COMPLETA, no_completas))
        for caso_id, _, companero in monton
        if companero is not None
    ]

    conexion.execute("BEGIN")
    try:
        conexion.executemany(
            "UPDATE casos SET estado_recomendacion = ?, estado_marcado_por = ?, "
            "estado_marcado_en = ?, estado_marcado_origen = ?, "
            "estado_del_companero = ?, estado_del_companero_por = ?, "
            "estado_del_companero_en = ? WHERE id = ?",
            escrituras,
        )
    except Exception:
        conexion.execute("ROLLBACK")
        raise
    conexion.execute("COMMIT")

    sin_devolver, sin_casar = _contar_lo_que_no_volvio(conexion, ruta_excel)
    return ResumenDeLaCarga(
        completas, no_completas, sin_respuesta, sin_devolver, sin_casar
    )
