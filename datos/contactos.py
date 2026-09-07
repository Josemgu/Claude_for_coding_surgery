"""Los contactos con el lider de la unidad sobre un caso. Se anulan, no se borran.

⚠️ **Esta es la fase peor especificada del proyecto, y hay que leerlo antes de
tocar nada.** `PENDIENTES.md` (FASE 7) lo dice de si misma: *«`CLAUDE.md` y
`DECISIONES.md` no contienen ni una sola decision sobre esta fase: no dicen que es
un “contacto con el lider”, que campos lleva, quien es el lider respecto de un
caso»*. Y `docs/ARQUITECTURA.md` §2.6 declara la tabla **PROVISIONAL**. Su criterio
4 exige, antes de cerrar la fase, una entrada del dueno en `DECISIONES.md`.

Lo que hay aqui sale del pase de esta fase, no de una decision del dueno, y son
**tres cosas que decidi yo** y que el dueno tiene que revisar:

  1. **`medio` es una lista cerrada de cinco valores** —whatsapp, llamada, correo,
     presencial, otro—, y vive en `MEDIOS_DE_CONTACTO`, en un solo sitio, igual que
     `datos/estados.py`. No lleva `CHECK` en el motor por el mismo motivo que
     `estado_recomendacion` (`DECISIONES.md`, P-1): una lista que el dueno todavia
     puede cambiar no se congela en el DDL.
  2. **`con_quien` y `contactado_por` son dos preguntas distintas.**
     `docs/ARQUITECTURA.md` §2.6 define `con_quien` como «con quien se hablo» —el
     lider—, y el pase pedia ademas «quien contacto». Ese es `contactado_por`, un
     companero, y entro con la version 5 del esquema.
  3. **`respondio` tiene tres estados y no dos.** 1 respondio, 0 no respondio,
     NULL todavia no se sabe. Un 0 por defecto convertiria «aun no contesta» en
     «no contesto», que es inventar un dato sobre una persona (regla permanente 1).

**Un contacto no verifica nada** (`PENDIENTES.md`, FASE 7). Este modulo no escribe
ni una sola vez en `procedencia_campo`.

**No hay funcion de borrado.** Anular escribe `anulado = 1` con su motivo y su
fecha, y el `CHECK` del esquema impide anular sin escribir por que.
"""

from datetime import date, datetime

from datos.companeros import leer_companero
from datos.esquema import marca_de_tiempo
from datos.validacion import ErrorDeValidacion, validar_fecha_viaje

# Los cinco medios por los que se contacta a un lider, en el orden en que se
# ofrecen en pantalla. Como la lista de estados: vive en un solo sitio, y anadir
# uno es tocar esta tupla y nada mas.
MEDIOS_DE_CONTACTO = ("whatsapp", "llamada", "correo", "presencial", "otro")

# Tope de longitud de los tres campos de texto libre. Mismo criterio que
# `unidad_nombre`: no es una regla del mundo, es lo que impide que un pegado
# accidental entre entero en la base y desborde la celda del Excel y la fila de la
# pantalla. Un resultado de contacto es una o dos frases; 500 deja sitio de sobra.
LARGO_MAXIMO_DEL_TEXTO = 500


def validar_medio(medio):
    """Uno de los cinco medios, o nulo mientras nadie lo haya dicho."""
    if medio is None:
        return None
    if medio not in MEDIOS_DE_CONTACTO:
        validos = ", ".join(repr(valor) for valor in MEDIOS_DE_CONTACTO)
        raise ErrorDeValidacion(
            f"El campo 'medio' no vale: se recibió {medio!r} y los medios "
            f"admitidos son {validos}."
        )
    return medio


def validar_respondio(respondio):
    """1 respondió, 0 no respondió, nada todavía no se sabe.

    Los tres valores son legítimos y el tercero no es un olvido: un correo enviado
    esta mañana no ha respondido y tampoco ha dejado de responder.
    """
    if respondio is None:
        return None
    if respondio in (0, 1, True, False):
        return int(respondio)
    raise ErrorDeValidacion(
        f"El campo 'respondio' no vale: se recibió {respondio!r} y se esperaba "
        "1 (respondió), 0 (no respondió) o nada (todavía no se sabe)."
    )


def _validar_texto(nombre, valor):
    """Un campo de texto libre, sin espacios de sobra. El vacio queda en nulo.

    Vacio a nulo y no a cadena vacia por lo mismo que en `validar_unidad_nombre`:
    dos formas de decir «no hay nada» son dos formas de que una consulta se olvide
    de una.
    """
    if valor is None:
        return None
    if not isinstance(valor, str):
        raise ErrorDeValidacion(
            f"El campo '{nombre}' no vale: se recibió {valor!r} y se esperaba texto."
        )
    limpio = valor.strip()
    if not limpio:
        return None
    if len(limpio) > LARGO_MAXIMO_DEL_TEXTO:
        raise ErrorDeValidacion(
            f"El campo '{nombre}' no vale: se recibieron {len(limpio)} caracteres "
            f"y el máximo son {LARGO_MAXIMO_DEL_TEXTO}."
        )
    return limpio


def validar_fecha_de_contacto(fecha):
    """La fecha en que ocurrio el contacto, en ISO-8601. Obligatoria.

    Reutiliza el validador de `fecha_viaje` —misma regla de formato y misma
    comprobacion de que la fecha existe de verdad— y le quita el permiso de ser
    nula: `contactos.fecha` es `NOT NULL`, y un contacto sin fecha no se puede
    situar en el historial ni contar los dias que lleva.
    """
    if fecha is None:
        raise ErrorDeValidacion(
            "El campo 'fecha' no vale: llegó vacío. Un contacto sin fecha no se "
            "puede situar en el historial ni contar los días que lleva."
        )
    if isinstance(fecha, datetime):
        fecha = fecha.date()
    if isinstance(fecha, date):
        fecha = fecha.isoformat()
    return validar_fecha_viaje(fecha)


def _exigir_caso(conexion, caso_id):
    """El caso al que se le cuelga el contacto, o un error que dice cual falta."""
    fila = conexion.execute(
        "SELECT id, numero_caso FROM casos WHERE id = ?", (caso_id,)
    ).fetchone()
    if fila is None:
        raise ErrorDeValidacion(f"No hay ningún caso con el id {caso_id!r}.")
    return dict(fila)


def _validar_contactado_por(conexion, contactado_por):
    """Quien hizo el contacto. Acepta nulo; si viene, tiene que existir.

    Se comprueba aqui ademas de en el motor para poder decir cual falta: la clave
    foranea devolveria «FOREIGN KEY constraint failed», que no nombra ni la
    columna ni el id.
    """
    if contactado_por is None:
        return None
    if leer_companero(conexion, contactado_por) is None:
        raise ErrorDeValidacion(
            f"El campo 'contactado_por' no vale: no hay ningún compañero con el id "
            f"{contactado_por!r}."
        )
    return contactado_por


def registrar_contacto(
    conexion,
    caso_id,
    fecha,
    medio=None,
    con_quien=None,
    resultado=None,
    contactado_por=None,
    respondio=None,
):
    """Deja constancia de un contacto que ya ocurrio fuera. Devuelve su id.

    El programa no manda nada: `PENDIENTES.md` (FASE 7) lo prohibe y la regla
    permanente 2 no deja abrir ni un puerto. Esto REGISTRA una llamada que alguien
    hizo con su telefono.

    Varios contactos por caso: no hay ninguna restriccion que lo impida, y es lo
    que hace falta —un caso que viaja el viernes se persigue tres veces—.
    """
    _exigir_caso(conexion, caso_id)
    cursor = conexion.execute(
        "INSERT INTO contactos (caso_id, fecha, medio, con_quien, resultado, "
        "anulado, registrado_en, contactado_por, respondio) "
        "VALUES (?, ?, ?, ?, ?, 0, ?, ?, ?)",
        (
            caso_id,
            validar_fecha_de_contacto(fecha),
            validar_medio(medio),
            _validar_texto("con_quien", con_quien),
            _validar_texto("resultado", resultado),
            marca_de_tiempo(),
            _validar_contactado_por(conexion, contactado_por),
            validar_respondio(respondio),
        ),
    )
    return cursor.lastrowid


def leer_contacto(conexion, contacto_id):
    """Un contacto por su id, o None si no esta."""
    fila = conexion.execute(
        "SELECT id, caso_id, fecha, medio, con_quien, resultado, anulado, "
        "motivo_anulacion, anulado_en, registrado_en, contactado_por, respondio "
        "FROM contactos WHERE id = ?",
        (contacto_id,),
    ).fetchone()
    return dict(fila) if fila is not None else None


def _dias_desde(fecha_del_contacto, hoy):
    """Cuantos dias han pasado desde ese contacto. Negativo si la fecha es futura.

    Una fecha futura no se corrige a 0: si alguien tecleo el mes que viene, eso hay
    que verlo, no taparlo.
    """
    return (hoy - date.fromisoformat(fecha_del_contacto)).days


def contactos_del_caso(conexion, caso_id, hoy=None, incluir_anulados=True):
    """El historial de un caso, del mas reciente al mas antiguo.

    Cada fila trae `dias_desde` ya calculado —cuando se le pasa `hoy`— y el nombre
    del companero que contacto, para que la pantalla no tenga que consultar otra
    vez por fila.

    **Los anulados vienen por defecto**, y con su motivo. Esconderlos seria borrar
    con otro nombre, y el criterio 3 de la FASE 7 pide justo lo contrario: que el
    registro siga visible.

    `hoy` entra como parametro y no se lee del reloj aqui, por el mismo motivo que
    en `datos/calendario.py`: una funcion que mira el reloj por dentro no se puede
    probar por los bordes.
    """
    filas = conexion.execute(
        "SELECT ct.id, ct.caso_id, ct.fecha, ct.medio, ct.con_quien, ct.resultado, "
        "       ct.anulado, ct.motivo_anulacion, ct.anulado_en, ct.registrado_en, "
        "       ct.contactado_por, ct.respondio, co.nombre AS nombre_del_companero "
        "FROM contactos ct LEFT JOIN companeros co ON co.id = ct.contactado_por "
        "WHERE ct.caso_id = ? ORDER BY ct.fecha DESC, ct.id DESC",
        (caso_id,),
    ).fetchall()

    historial = []
    for fila in filas:
        contacto = dict(fila)
        if not incluir_anulados and contacto["anulado"]:
            continue
        contacto["dias_desde"] = (
            None if hoy is None else _dias_desde(contacto["fecha"], hoy)
        )
        historial.append(contacto)
    return historial


def ultimo_contacto_por_caso(conexion, hoy=None):
    """El ultimo contacto NO anulado de cada caso, indexado por `caso_id`.

    Es lo que la lista de pendientes necesita para poder decir «se contactó hace 10
    días y no respondió» sin una consulta por fila. Un caso que no aparece en el
    diccionario es un caso al que nadie ha llamado nunca, que es distinto de uno
    contactado ayer y **muy** distinto de uno contactado hace diez días sin
    respuesta.

    Los anulados no cuentan: un contacto anulado es uno que se registró por error,
    y dejarlo contar diría que se hizo una gestión que no se hizo.

    El ultimo se decide por `fecha` y, a igualdad de fecha, por `id`: dos contactos
    del mismo dia se ordenan por el orden en que se registraron, que es lo unico
    que los distingue.
    """
    filas = conexion.execute(
        "SELECT ct.caso_id, ct.id, ct.fecha, ct.medio, ct.respondio, "
        "       ct.con_quien, co.nombre AS nombre_del_companero "
        "FROM contactos ct LEFT JOIN companeros co ON co.id = ct.contactado_por "
        "WHERE ct.anulado = 0 "
        "  AND ct.id = (SELECT c2.id FROM contactos c2 "
        "               WHERE c2.caso_id = ct.caso_id AND c2.anulado = 0 "
        "               ORDER BY c2.fecha DESC, c2.id DESC LIMIT 1)"
    ).fetchall()

    ultimos = {}
    for fila in filas:
        contacto = dict(fila)
        contacto["dias_desde"] = (
            None if hoy is None else _dias_desde(contacto["fecha"], hoy)
        )
        ultimos[contacto["caso_id"]] = contacto
    return ultimos


def anular_contacto(conexion, contacto_id, motivo):
    """Marca un contacto como registrado por error, con su motivo y su fecha.

    La fila se queda donde esta: no hay `DELETE`. El `CHECK` del esquema hace la
    otra mitad —no se puede anular sin escribir por que—, y este modulo lo
    comprueba antes para poder decirlo en espanol.

    Anular dos veces no mueve el motivo ni la fecha del primer intento: la primera
    anulacion es la verdadera.
    """
    contacto = leer_contacto(conexion, contacto_id)
    if contacto is None:
        raise ErrorDeValidacion(f"No hay ningún contacto con el id {contacto_id!r}.")
    if contacto["anulado"]:
        return False

    motivo_limpio = _validar_texto("motivo_anulacion", motivo)
    if motivo_limpio is None:
        raise ErrorDeValidacion(
            "No se puede anular un contacto sin escribir por qué. Un registro "
            "anulado sin motivo no se distingue de uno borrado."
        )
    conexion.execute(
        "UPDATE contactos SET anulado = 1, motivo_anulacion = ?, anulado_en = ? "
        "WHERE id = ?",
        (motivo_limpio, marca_de_tiempo(), contacto_id),
    )
    return True
