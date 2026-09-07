"""Alta, edicion y desactivacion de companeros. Nunca borrado.

**No hay ninguna funcion de borrado en este modulo, y no la va a haber.**
`DECISIONES.md` (2026-09-02, «Companeros (FASE 6)») lo dice con su motivo: los
companeros se desactivan, o se pierde el historial de quien verifico que. Un
companero borrado se lleva por delante la firma de cada campo que dio por bueno.

El motor ya lo defiende por su lado: `procedencia_campo.verificado_por` y
`asignaciones.companero_id` apuntan aqui con `ON DELETE RESTRICT`, asi que un
`DELETE` sobre una fila con historial fallaria. Este modulo anade la otra mitad de
la defensa —**no ofrecer la operacion**—, que es la que cubre tambien al companero
que todavia no ha verificado nada y al que el motor si dejaria borrar.

Cada instruccion SQL es una cadena literal escrita dentro de la llamada, con
marcadores `?`, y los valores viajan aparte (`docs/ARQUITECTURA.md` §1.7).
"""

from datos.esquema import marca_de_tiempo
from datos.validacion import ErrorDeValidacion

# Tope de longitud del nombre. Mismo criterio que `unidad_nombre`: no es una regla
# del mundo, es lo que impide que un pegado accidental de media pagina entre en la
# base y desborde el desplegable y la celda del Excel.
LARGO_MAXIMO_DEL_NOMBRE = 120

# Las columnas de `companeros`, escritas una vez. Nadie arma SQL con esto.
COLUMNAS = ("id", "nombre", "activo", "desactivado_en", "creado_en")


def validar_nombre_de_companero(nombre):
    """El nombre tal como se teclea, sin espacios de sobra. Obligatorio.

    A diferencia de `unidad_nombre`, aqui el vacio NO se convierte en nulo: se
    rechaza. `companeros.nombre` es `NOT NULL` y un companero sin nombre no se
    puede elegir en ningun desplegable ni firmar nada.

    No se comprueba que no exista ya: `docs/ARQUITECTURA.md` §2.4 decide que
    `nombre` NO es `UNIQUE` a proposito, porque dos personas pueden llamarse igual
    y una restriccion asi obligaria a inventar un desempate.
    """
    if not isinstance(nombre, str):
        raise ErrorDeValidacion(
            f"El campo 'nombre' no vale: se recibió {nombre!r} y se esperaba texto, "
            "como 'Miguel Anonimo'."
        )
    limpio = nombre.strip()
    if not limpio:
        raise ErrorDeValidacion(
            "El campo 'nombre' no vale: llegó vacío. Un compañero sin nombre no se "
            "puede elegir en el desplegable de asignación ni puede firmar nada."
        )
    if len(limpio) > LARGO_MAXIMO_DEL_NOMBRE:
        raise ErrorDeValidacion(
            f"El campo 'nombre' no vale: se recibieron {len(limpio)} caracteres y el "
            f"máximo son {LARGO_MAXIMO_DEL_NOMBRE}."
        )
    return limpio


def alta_de_companero(conexion, nombre):
    """Da de alta un companero activo y devuelve su id."""
    cursor = conexion.execute(
        "INSERT INTO companeros (nombre, activo, creado_en) VALUES (?, 1, ?)",
        (validar_nombre_de_companero(nombre), marca_de_tiempo()),
    )
    return cursor.lastrowid


def leer_companero(conexion, companero_id):
    """Un companero por su id, o None si no esta."""
    fila = conexion.execute(
        "SELECT id, nombre, activo, desactivado_en, creado_en "
        "FROM companeros WHERE id = ?",
        (companero_id,),
    ).fetchone()
    return dict(fila) if fila is not None else None


def _exigir_companero(conexion, companero_id):
    """El companero, o un error que dice cual falta. Evita un UPDATE al vacio."""
    companero = leer_companero(conexion, companero_id)
    if companero is None:
        raise ErrorDeValidacion(
            f"No hay ningún compañero con el id {companero_id!r}."
        )
    return companero


def companeros_activos(conexion):
    """Los que salen en el desplegable de asignacion, por nombre.

    Es la consulta que sostiene el criterio 3 de la FASE 6: un companero
    desactivado deja de aparecer aqui. Lo que NO deja de aparecer es su firma en
    los campos que verifico, porque eso lo lee `leer_companero` por id y esta
    funcion no interviene.
    """
    filas = conexion.execute(
        "SELECT id, nombre, activo, desactivado_en, creado_en "
        "FROM companeros WHERE activo = 1 ORDER BY nombre, id"
    ).fetchall()
    return [dict(fila) for fila in filas]


def todos_los_companeros(conexion):
    """Todos, activos y desactivados, con los activos primero."""
    filas = conexion.execute(
        "SELECT id, nombre, activo, desactivado_en, creado_en "
        "FROM companeros ORDER BY activo DESC, nombre, id"
    ).fetchall()
    return [dict(fila) for fila in filas]


def renombrar_companero(conexion, companero_id, nombre):
    """Corrige el nombre de un companero. No toca su estado ni su historial."""
    _exigir_companero(conexion, companero_id)
    conexion.execute(
        "UPDATE companeros SET nombre = ? WHERE id = ?",
        (validar_nombre_de_companero(nombre), companero_id),
    )
    return companero_id


def desactivar_companero(conexion, companero_id):
    """Lo retira del desplegable dejando la fila donde esta. Devuelve si cambio algo.

    Desactivar dos veces no es un error ni mueve la fecha: la primera fecha de
    desactivacion es la verdadera, y sobrescribirla con la de hoy borraria cuando
    dejo de trabajar de verdad.

    Las asignaciones que tuviera vivas NO se tocan aqui. Retirar un caso es una
    operacion aparte (`datos/asignaciones.py`) y con su propia fecha: mezclarlas
    haria imposible distinguir «se le retiro el caso» de «se fue del equipo».
    """
    companero = _exigir_companero(conexion, companero_id)
    if not companero["activo"]:
        return False
    conexion.execute(
        "UPDATE companeros SET activo = 0, desactivado_en = ? WHERE id = ?",
        (marca_de_tiempo(), companero_id),
    )
    return True


def reactivar_companero(conexion, companero_id):
    """Lo devuelve al desplegable. Devuelve si cambio algo.

    Existe porque desactivar es la unica marcha atras que hay —no se borra— y una
    desactivacion por error tenia que poder deshacerse sin abrir la base a mano.
    `desactivado_en` vuelve a NULL porque el `CHECK` del esquema lo exige: activo
    con fecha de desactivacion es un estado que el motor no admite, y hace bien.
    """
    companero = _exigir_companero(conexion, companero_id)
    if companero["activo"]:
        return False
    conexion.execute(
        "UPDATE companeros SET activo = 1, desactivado_en = NULL WHERE id = ?",
        (companero_id,),
    )
    return True


# El compañero que firma lo que Miguel hace desde el programa. Nace de una
# limitación honesta: la FASE 6 traerá varios compañeros, y hasta entonces hay uno
# —el propio Miguel—. NO es una firma inventada: es la persona que está usando el
# programa, y el día que haya varios esta fila es una más y no hay nada que
# deshacer.
NOMBRE_DEL_COMPANERO_POR_DEFECTO = "Miguel"


def companero_que_firma(conexion):
    """El id del compañero que firma lo que se marca a mano. Lo da de alta la 1.ª vez.

    ⚠️ **Vive aquí y no en una pantalla desde el 2026-09-03**, y el motivo es una
    regla que este proyecto ya tenía escrita: la regla permanente 5 dice que nada se
    marca solo, y quién firma es la otra mitad de esa regla. Nació dentro de
    `interfaz/correccion.py` cuando esa era la única pantalla que firmaba; ahora la
    pantalla «Revisar» firma también, y dos definiciones de «quién es Miguel»
    acabarían dando de alta dos compañeros con el mismo nombre y repartiendo las
    firmas entre los dos sin que nadie lo notara.
    """
    fila = conexion.execute(
        "SELECT id FROM companeros WHERE nombre = ? AND activo = 1",
        (NOMBRE_DEL_COMPANERO_POR_DEFECTO,),
    ).fetchone()
    if fila is not None:
        return fila["id"]
    from datos.esquema import marca_de_tiempo

    cursor = conexion.execute(
        "INSERT INTO companeros (nombre, activo, creado_en) VALUES (?, 1, ?)",
        (NOMBRE_DEL_COMPANERO_POR_DEFECTO, marca_de_tiempo()),
    )
    return cursor.lastrowid
