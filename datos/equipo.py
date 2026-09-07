"""Lo que el panel enseña del equipo y los cuatro contadores de arriba.

Es la quinta consulta de la pantalla de inicio, y va aparte de
`datos/calendario.py` y de `datos/pendientes.py` porque no pregunta lo mismo: el
calendario pregunta *cuándo* viaja un caso, pendientes pregunta *si alguien lo
miró*, y esto pregunta **quién lo lleva y cómo va el conjunto**.

**Por qué existe este módulo y no se metió en `datos/companeros.py`.** Lo que
había era `casos_asignados(conexion, companero_id)`: una consulta **por
compañero**. Pintar la tarjeta del equipo con eso son N+1 consultas para dibujar
tres renglones, y el panel se repinta cada vez que se vuelve de un caso. Aquí la
misma pregunta se contesta de una vez para todo el equipo.

**«Hojas sin devolver» significa una sola cosa**, y está decidida:
*«documentos asignados en los que **ninguna** persona trae propuesta. Un solo
número, el simple»* (`DECISIONES.md`, 2026-09-03). No es «le falta alguna»: son
dos números distintos y el dueño eligió éste. Una persona «trae propuesta» cuando
tiene `propuesto_en` puesto, que es lo que escribe la vuelta del Excel del
compañero.

**Las unidades de cada contador van dichas**, porque el panel mezcla documentos y
personas y un número sin unidad se lee mal:

    personas_por_viajar .......... PERSONAS
    documentos_completos ......... DOCUMENTOS
    hojas_sin_devolver ........... DOCUMENTOS
    personas_que_viajaron_sin_verificar ... PERSONAS

Como en el resto de `datos/`, cada instrucción SQL es una cadena literal escrita
dentro de la llamada, con marcadores `?`, y todos los valores viajan aparte.

⚠️ **Por qué la clave del renglón se llama `cuantos_documentos` y no `documentos`.**
No es gusto: `pruebas/auditoria_rutas.py` prohíbe que exista en el código un
literal de cadena que sea **exactamente** «documentos», porque así es como se
compondría a mano la ruta de la carpeta de datos, y en esta máquina hay tres
carpetas con ese nombre —dos de ellas sincronizando a la nube con MRN de personas
reales— (`DECISIONES.md`, 2026-09-02). Un `renglon["documentos"]` era ese literal.
La clave más larga dice lo mismo, sigue en español, y deja la auditoría en 0.
"""

from datos.calendario import a_fecha, estados_que_resuelven_para_el_motor

# El nombre del renglón que agrupa lo que no lleva nadie. No es un compañero: es
# la ausencia de uno, y por eso va con `companero_id` a `None` y no con un id
# inventado que alguien pudiera confundir con una persona real.
SIN_ASIGNAR = "Sin asignar"


def _documentos_por_companero(conexion):
    """Cuántos documentos vivos lleva cada compañero, y cuántos sin devolver.

    Una sola consulta para todo el equipo. `sin_devolver` cuenta el documento en
    el que NINGUNA persona trae propuesta: el `NOT EXISTS` es literalmente esa
    frase, y por eso se escribe así y no contando propuestas y comparando.

    Un documento archivado no cuenta: ya se cerró, y seguir pidiéndoselo al
    compañero sería trabajo que no existe.
    """
    filas = conexion.execute(
        "SELECT co.id AS companero_id, co.nombre, "
        "       COUNT(c.id) AS cuantos_documentos, "
        "       SUM(CASE WHEN NOT EXISTS ("
        "             SELECT 1 FROM personas p "
        "             WHERE p.caso_id = c.id AND p.propuesto_en IS NOT NULL"
        "           ) THEN 1 ELSE 0 END) AS sin_devolver, "
        "       SUM((SELECT COUNT(*) FROM personas p2 WHERE p2.caso_id = c.id)) AS personas "
        "FROM companeros co "
        "JOIN asignaciones a ON a.companero_id = co.id AND a.activa = 1 "
        "JOIN casos c ON c.id = a.caso_id AND c.archivado = 0 "
        "WHERE co.activo = 1 "
        "GROUP BY co.id, co.nombre "
        "ORDER BY co.nombre, co.id"
    ).fetchall()
    return [dict(fila) for fila in filas]


def _companeros_sin_ningun_documento(conexion):
    """Los compañeros activos a los que no les queda nada vivo.

    Salen con 0, y no se omiten. Un compañero que desaparece de la tarjeta cuando
    termina su trabajo se lee como «ya no está», que es otra cosa: el reparto se
    hace mirando quién tiene hueco, y para eso hay que ver a los que están a cero.
    """
    filas = conexion.execute(
        "SELECT co.id AS companero_id, co.nombre "
        "FROM companeros co "
        "WHERE co.activo = 1 "
        "  AND NOT EXISTS ("
        "        SELECT 1 FROM asignaciones a JOIN casos c ON c.id = a.caso_id "
        "        WHERE a.companero_id = co.id AND a.activa = 1 AND c.archivado = 0"
        "      ) "
        "ORDER BY co.nombre, co.id"
    ).fetchall()
    return [
        {
            "companero_id": fila["companero_id"],
            "nombre": fila["nombre"],
            "cuantos_documentos": 0,
            "sin_devolver": 0,
            "personas": 0,
        }
        for fila in filas
    ]


def _documentos_sin_asignar(conexion):
    """El renglón «Sin asignar»: lo vivo que no lleva ningún compañero.

    Es el que dispara el botón «Repartir». Va con `companero_id = None` a
    propósito: quien lo pinte no puede confundirlo con una persona ni mandarle un
    paquete.
    """
    fila = conexion.execute(
        "SELECT COUNT(*) AS cuantos_documentos, "
        "       COALESCE(SUM("
        "         (SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id)"
        "       ), 0) AS personas "
        "FROM casos c "
        "WHERE c.archivado = 0 "
        "  AND NOT EXISTS ("
        "        SELECT 1 FROM asignaciones a "
        "        WHERE a.caso_id = c.id AND a.activa = 1"
        "      )"
    ).fetchone()
    return {
        "companero_id": None,
        "nombre": SIN_ASIGNAR,
        "cuantos_documentos": fila["cuantos_documentos"],
        # «Sin devolver» no significa nada para lo que no lleva nadie: no se le ha
        # pedido a ninguna persona. `None` y no 0, para que la pantalla pueda
        # escribir «—» en vez de un cero que se leería como «ya volvieron».
        "sin_devolver": None,
        "personas": fila["personas"],
    }


def resumen_del_equipo(conexion):
    """Un renglón por compañero activo, más el de «Sin asignar» al final.

    Cada renglón trae `nombre`, `documentos`, `sin_devolver` y `personas`. El de
    «Sin asignar» va siempre, incluso con 0 documentos: es el hueco por el que se
    escapa un caso que nadie mira, y un renglón que desaparece cuando vale 0 no se
    puede distinguir de uno que nunca se dibujó.
    """
    equipo = _documentos_por_companero(conexion)
    equipo.extend(_companeros_sin_ningun_documento(conexion))
    equipo.sort(key=lambda renglon: (renglon["nombre"].lower(), renglon["companero_id"]))
    equipo.append(_documentos_sin_asignar(conexion))
    return equipo


def _personas_por_viajar(conexion, hoy):
    """PERSONAS de documentos vivos cuyo viaje aún no ha pasado."""
    return conexion.execute(
        "SELECT COUNT(*) FROM personas p "
        "JOIN casos c ON c.id = p.caso_id "
        "WHERE c.archivado = 0 "
        "  AND c.fecha_viaje IS NOT NULL "
        "  AND c.fecha_viaje >= ?",
        (hoy.isoformat(),),
    ).fetchone()[0]


def _documentos_completos(conexion):
    """DOCUMENTOS vivos cuya recomendación ya está resuelta.

    Qué resuelve lo dice `datos/estados.py` y nada más, y llega por parámetro con
    el mismo `instr` que usa la franja roja. Ni un estado escrito dentro del SQL:
    el día que el dueño añada un cuarto valor, este contador lo cuenta solo.
    """
    return conexion.execute(
        "SELECT COUNT(*) FROM casos c "
        "WHERE c.archivado = 0 "
        "  AND c.estado_recomendacion IS NOT NULL "
        "  AND instr(?, ',' || c.estado_recomendacion || ',') > 0",
        (estados_que_resuelven_para_el_motor(),),
    ).fetchone()[0]


def _hojas_sin_devolver(conexion):
    """DOCUMENTOS asignados en los que ninguna persona trae propuesta."""
    return conexion.execute(
        "SELECT COUNT(*) FROM casos c "
        "WHERE c.archivado = 0 "
        "  AND EXISTS ("
        "        SELECT 1 FROM asignaciones a "
        "        WHERE a.caso_id = c.id AND a.activa = 1"
        "      ) "
        "  AND NOT EXISTS ("
        "        SELECT 1 FROM personas p "
        "        WHERE p.caso_id = c.id AND p.propuesto_en IS NOT NULL"
        "      )"
    ).fetchone()[0]


def _personas_que_viajaron_sin_verificar(conexion, hoy):
    """PERSONAS cuyo viaje ya pasó y su documento sigue sin resolver.

    Es el daño consumado, y por eso se cuenta en personas y no en documentos: lo
    que duele es cuántas personas llegaron al templo con la recomendación mal, no
    cuántos papeles hubo. Un documento archivado no cuenta: se cerró a sabiendas.
    """
    return conexion.execute(
        "SELECT COUNT(*) FROM personas p "
        "JOIN casos c ON c.id = p.caso_id "
        "WHERE c.archivado = 0 "
        "  AND c.fecha_viaje IS NOT NULL "
        "  AND c.fecha_viaje < ? "
        "  AND (c.estado_recomendacion IS NULL "
        "       OR instr(?, ',' || c.estado_recomendacion || ',') = 0)",
        (hoy.isoformat(), estados_que_resuelven_para_el_motor()),
    ).fetchone()[0]


def contadores_del_panel(conexion, hoy):
    """Los cuatro números de la fila de contadores, con su unidad en el nombre.

    «Hoy» entra como parámetro por lo mismo que en `datos/calendario.py`: una
    función que mira el reloj por dentro no se puede probar por sus bordes, y el
    borde —el documento que viajó ayer— es justo lo que hay que probar.
    """
    hoy = a_fecha(hoy, "hoy")
    return {
        "personas_por_viajar": _personas_por_viajar(conexion, hoy),
        "documentos_completos": _documentos_completos(conexion),
        "hojas_sin_devolver": _hojas_sin_devolver(conexion),
        "personas_que_viajaron_sin_verificar": _personas_que_viajaron_sin_verificar(
            conexion, hoy
        ),
    }
