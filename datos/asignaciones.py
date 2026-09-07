"""Que casos lleva cada companero. Se retiran, no se borran.

`DECISIONES.md` (2026-09-02, P-4) lo decide: «las asignaciones se desactivan, no se
borran. Un historial de quien tuvo que caso vale mas que una tabla limpia». Por eso
aqui tampoco hay ninguna funcion de borrado.

**Un mismo caso no se asigna dos veces vivas al mismo companero, pero si se puede
reasignar despues de retirarlo.** Eso no lo da un `UNIQUE` normal: lo da el indice
unico PARCIAL `idx_asignacion_viva` que ya crea el esquema
(`docs/ARQUITECTURA.md` §2.5). Este modulo no lo repite en Python; lo que hace es
comprobar antes para poder decir por que, y dejar que el motor sea la ultima
palabra.

⚠️ Lo que este modulo NO decide: si un caso puede estar asignado a DOS companeros
a la vez. El indice parcial es por `(caso_id, companero_id)`, asi que el motor lo
permite, y ni `PENDIENTES.md` ni `DECISIONES.md` dicen nada. Se deja permitido —dos
personas pueden repasar el mismo caso— y se anota para el dueno.

**Lo que cambio despues de la auditoria final de QA, y lo que no.** Sigue sin
decidirse, y por eso el motor sigue permitiendolo: imponerlo con un indice unico
seria tomar por el dueno una decision que no ha tomado. Lo que se cerro es el dano
que traia, que no dependia de esa decision:

  - `datos/propuestas.py` ya **no deja que el Excel del segundo companero pise la
    propuesta del primero**. Antes lo hacia en silencio: A escribia «falta la firma
    del obispo», B escribia «todo bien», y Miguel solo veia a B.
  - `interfaz/asignacion.py` **avisa** cuando el caso que se va a asignar ya lo
    lleva vivo otra persona, y deja que Miguel siga o no. Es el mismo criterio que
    la regla del mes cruzado (`DECISIONES.md`, P-2): aviso en la pantalla, no
    restriccion del motor, mientras el dueno no diga otra cosa.
"""

from datos.companeros import leer_companero
from datos.esquema import marca_de_tiempo
from datos.validacion import ErrorDeValidacion


def _exigir_caso(conexion, caso_id):
    """El numero del caso, o un error que dice cual falta."""
    fila = conexion.execute(
        "SELECT id, numero_caso FROM casos WHERE id = ?", (caso_id,)
    ).fetchone()
    if fila is None:
        raise ErrorDeValidacion(f"No hay ningún caso con el id {caso_id!r}.")
    return dict(fila)


def _exigir_companero_activo(conexion, companero_id):
    """Solo se le asigna trabajo a quien sigue en el equipo."""
    companero = leer_companero(conexion, companero_id)
    if companero is None:
        raise ErrorDeValidacion(f"No hay ningún compañero con el id {companero_id!r}.")
    if not companero["activo"]:
        raise ErrorDeValidacion(
            f"El compañero «{companero['nombre']}» está desactivado y no se le "
            "puede asignar trabajo nuevo. Los casos que ya llevaba siguen "
            "guardados con su nombre."
        )
    return companero


def asignacion_viva(conexion, caso_id, companero_id):
    """La asignacion viva de ese caso a ese companero, o None si no la hay."""
    fila = conexion.execute(
        "SELECT id, caso_id, companero_id, asignado_en, activa, desactivada_en "
        "FROM asignaciones WHERE caso_id = ? AND companero_id = ? AND activa = 1",
        (caso_id, companero_id),
    ).fetchone()
    return dict(fila) if fila is not None else None


def asignar_caso(conexion, caso_id, companero_id):
    """Asigna un caso a un companero. Devuelve el id de la asignacion.

    Si ya estaba asignado y vivo, devuelve la que hay y NO crea otra: repetir la
    asignacion es lo que pasa cuando Miguel vuelve a seleccionar un caso que ya
    habia mandado, y no es un error que merezca detenerle el trabajo.
    """
    _exigir_caso(conexion, caso_id)
    _exigir_companero_activo(conexion, companero_id)

    ya_esta = asignacion_viva(conexion, caso_id, companero_id)
    if ya_esta is not None:
        return ya_esta["id"]

    cursor = conexion.execute(
        "INSERT INTO asignaciones (caso_id, companero_id, asignado_en, activa) "
        "VALUES (?, ?, ?, 1)",
        (caso_id, companero_id, marca_de_tiempo()),
    )
    return cursor.lastrowid


def asignar_casos(conexion, caso_ids, companero_id):
    """Asigna varios casos de una vez y devuelve la lista de ids de asignacion.

    Existe porque el paquete se genera para un companero con VARIOS casos, y
    hacerlo caso a caso desde la pantalla repartiria la validacion del companero
    por el bucle de la interfaz.
    """
    return [asignar_caso(conexion, caso_id, companero_id) for caso_id in caso_ids]


def retirar_caso(conexion, caso_id, companero_id):
    """Retira un caso a un companero dejando la fila. Devuelve si cambio algo.

    Retirar lo que no esta asignado devuelve False y no levanta: es lo que pasa al
    pulsar dos veces, y no hay nada que arreglar.
    """
    viva = asignacion_viva(conexion, caso_id, companero_id)
    if viva is None:
        return False
    conexion.execute(
        "UPDATE asignaciones SET activa = 0, desactivada_en = ? WHERE id = ?",
        (marca_de_tiempo(), viva["id"]),
    )
    return True


def casos_asignados(conexion, companero_id):
    """Los casos vivos de un companero, el que viaja antes primero.

    Trae del caso lo que hace falta para armar el paquete —el numero, la ruta del
    PDF, la fecha— sin una segunda consulta por caso. Los casos sin fecha de viaje
    van al final por el mismo motivo que en `datos/pendientes.py`: hay que
    atenderlos, pero no antes que uno que viaja el martes.
    """
    filas = conexion.execute(
        "SELECT a.id AS asignacion_id, a.asignado_en, "
        "       c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, "
        "       c.templo_nombre, "
        "       c.fecha_viaje, c.ruta_pdf, c.archivado, c.estado_recomendacion, "
        "       (SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas "
        "FROM asignaciones a JOIN casos c ON c.id = a.caso_id "
        "WHERE a.companero_id = ? AND a.activa = 1 "
        "ORDER BY c.fecha_viaje IS NULL, c.fecha_viaje, c.numero_caso",
        (companero_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def companeros_del_caso(conexion, caso_id):
    """Quien lleva vivo ese caso ahora mismo, por nombre."""
    filas = conexion.execute(
        "SELECT a.id AS asignacion_id, a.asignado_en, co.id AS companero_id, "
        "       co.nombre, co.activo "
        "FROM asignaciones a JOIN companeros co ON co.id = a.companero_id "
        "WHERE a.caso_id = ? AND a.activa = 1 ORDER BY co.nombre, co.id",
        (caso_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def historial_de_asignaciones(conexion, caso_id):
    """Todas las asignaciones de un caso, las vivas y las retiradas, en orden."""
    filas = conexion.execute(
        "SELECT a.id, a.caso_id, a.companero_id, a.asignado_en, a.activa, "
        "       a.desactivada_en, co.nombre "
        "FROM asignaciones a JOIN companeros co ON co.id = a.companero_id "
        "WHERE a.caso_id = ? ORDER BY a.asignado_en, a.id",
        (caso_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]
