"""Lo que la base dice para el reporte. Solo consultas: aqui no se cuenta nada.

**Ninguna consulta de este modulo filtra por `archivado`**, y esa ausencia es la
decision de `DECISIONES.md` (2026-09-02, «Historico»): «los archivados salen de
las listas de trabajo pero **siguen contando en los reportes**». Es justo al reves
que las cuatro consultas de `datos/calendario.py` y `datos/pendientes.py`, que
llevan `archivado = 0` escrito dentro. Las dos mitades de la misma frase, cada una
en su sitio.

La consecuencia comprobable, que es el criterio 3 de la FASE 8: archivar un caso
del periodo **no cambia ningun total de este modulo**.

Como en `datos/`, cada instruccion es una cadena literal escrita dentro de la
llamada, con marcadores `?`, y los valores viajan aparte. La lista de columnas se
repite entera en cada consulta en vez de guardarse en una constante: una lista
blanca que admite excepciones deja de ser una lista blanca
(`pruebas/auditoria_sql.py`).
"""


def personas_del_periodo(conexion, periodo):
    """Las personas cuyo caso viaja dentro del periodo, con lo anotado del viaje.

    Sale UNA sola consulta y de ella salen las dos mitades del reporte —quien
    viajo y quien no pudo—, separadas despues en Python por `pudo_viajar`. Es a
    proposito: con dos consultas, un filtro escrito distinto en cada una podria
    dejar a alguien fuera de las dos listas a la vez, y esa persona no aparecería
    en ninguna parte del reporte sin que nadie lo notara.

    El orden es el de lectura: por fecha de viaje, luego por caso, luego por la
    fila del formulario.
    """
    filas = conexion.execute(
        "SELECT p.id AS persona_id, p.nombre, p.mrn, p.fila_formulario, "
        "p.pudo_viajar, p.motivo_no_viajo, "
        "p.ord_recibir_propias, p.ord_observar_sellamiento, p.ord_traductor, "
        "p.ord_investidura, p.ord_sellamiento_esposos, p.ord_sellamiento_hijo_padres, "
        "p.paso_preparacion, p.paso_informacion, p.paso_cita_del_templo, "
        "p.paso_acciones_requeridas, p.paso_entrevistas, p.paso_listo_para_el_templo, "
        "p.llamo_al_lider, p.propuesto_por, p.propuesto_en, "
        "c.id AS caso_id, c.numero_caso, c.unidad_numero, c.unidad_nombre, "
        "c.templo_nombre, "
        "c.fecha_viaje, c.estado_recomendacion, c.archivado, c.fecha_archivado "
        "FROM personas p JOIN casos c ON c.id = p.caso_id "
        "WHERE c.fecha_viaje IS NOT NULL "
        "AND c.fecha_viaje >= ? AND c.fecha_viaje <= ? "
        "ORDER BY c.fecha_viaje, c.numero_caso, p.fila_formulario, p.id",
        (periodo.desde, periodo.hasta),
    ).fetchall()
    return [dict(fila) for fila in filas]


def companeros_por_caso(conexion):
    """Quien lleva cada caso hoy. Devuelve `caso_id -> (nombres ordenados)`.

    Solo las asignaciones vivas: quien llevo un caso y ya no lo lleva no es a quien
    hay que preguntarle por el. El historial completo lo guarda
    `datos/asignaciones.py` y no es lo que un informe a la direccion necesita.

    Se juntan en Python y no con un `GROUP_CONCAT` a proposito: el orden de un
    `GROUP_CONCAT` no lo garantiza SQLite, y dos informes del mismo periodo que
    listan los mismos dos nombres en distinto orden se leen como si hubieran
    cambiado.
    """
    filas = conexion.execute(
        "SELECT a.caso_id, co.nombre "
        "FROM asignaciones a JOIN companeros co ON co.id = a.companero_id "
        "WHERE a.activa = 1 "
        "ORDER BY a.caso_id, co.nombre"
    ).fetchall()
    por_caso = {}
    for fila in filas:
        por_caso.setdefault(fila["caso_id"], []).append(fila["nombre"])
    return {caso_id: tuple(nombres) for caso_id, nombres in por_caso.items()}


def personas_que_no_viajaron_sin_fecha(conexion):
    """Las anotadas como que no pudieron viajar cuyo caso NO tiene fecha de viaje.

    No caben en ningun periodo —no hay con que compararlas— y por eso salen
    aparte: una persona que no pudo viajar y no aparece en ningun reporte es
    exactamente la que se pierde. El reporte las cuenta en su cabecera aunque no
    sean del periodo, para que el numero de arriba no mienta por omision.
    """
    filas = conexion.execute(
        "SELECT p.id AS persona_id, p.nombre, p.mrn, p.motivo_no_viajo, "
        "c.numero_caso, c.unidad_nombre "
        "FROM personas p JOIN casos c ON c.id = p.caso_id "
        "WHERE p.pudo_viajar = 0 AND c.fecha_viaje IS NULL "
        "ORDER BY c.numero_caso, p.fila_formulario, p.id"
    ).fetchall()
    return [dict(fila) for fila in filas]


def casos_con_su_verificacion(conexion):
    """Todos los casos con cuantos campos tienen, cuantos verificados, y cuando.

    `verificado_en` es la marca de tiempo del **ultimo** campo verificado del caso
    —los suyos y los de sus personas—: el instante en que alguien termino de
    mirarlo. Es `NULL` mientras no haya ni un campo verificado.

    Un caso con `campos = 0` no esta verificado, y no es lo mismo que uno con
    todos sus campos verificados. Es la misma distincion que hace
    `datos/pendientes.py`: cero campos sin verificar no significa todo verificado
    —significa que nadie leyo nada—, y contarlo como verificado inflaria la
    metrica de trabajo del equipo con casos que nadie ha tocado.

    Devuelve TODOS los casos, sin filtrar por periodo ni por archivado. Quien
    necesite un trozo lo recorta despues: asi las tres metricas parten de la misma
    lectura y no pueden discrepar entre ellas.
    """
    filas = conexion.execute(
        "SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, "
        "c.fecha_viaje, c.estado_recomendacion, c.creado_en, c.archivado, "
        "c.fecha_archivado, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas, "
        "(SELECT COUNT(*) FROM procedencia_campo pc "
        "   WHERE (pc.tabla = 'casos' AND pc.registro_id = c.id) "
        "      OR (pc.tabla = 'personas' AND pc.registro_id IN "
        "          (SELECT p2.id FROM personas p2 WHERE p2.caso_id = c.id))"
        ") AS campos, "
        "(SELECT COUNT(*) FROM procedencia_campo pc "
        "   WHERE pc.verificado = 1 "
        "     AND ((pc.tabla = 'casos' AND pc.registro_id = c.id) "
        "       OR (pc.tabla = 'personas' AND pc.registro_id IN "
        "           (SELECT p2.id FROM personas p2 WHERE p2.caso_id = c.id)))"
        ") AS campos_verificados, "
        "(SELECT MAX(pc.verificado_en) FROM procedencia_campo pc "
        "   WHERE pc.verificado = 1 "
        "     AND ((pc.tabla = 'casos' AND pc.registro_id = c.id) "
        "       OR (pc.tabla = 'personas' AND pc.registro_id IN "
        "           (SELECT p2.id FROM personas p2 WHERE p2.caso_id = c.id)))"
        ") AS verificado_en "
        "FROM casos c ORDER BY c.numero_caso, c.id"
    ).fetchall()
    return [dict(fila) for fila in filas]


def total_de_casos(conexion):
    """Cuantos casos hay en la base, archivados incluidos.

    Es el denominador del criterio 2 de la FASE 8: este numero tiene que ser el
    mismo antes y despues de archivar, porque archivar no borra nada.
    """
    return conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]


def total_de_personas(conexion):
    """Cuantas personas hay en la base. Nunca baja: aqui no se borra a nadie."""
    return conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0]
