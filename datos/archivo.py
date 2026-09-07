"""Archivar un caso, desarchivarlo, y anotar quien llego a viajar y quien no.

Aqui no hay ninguna funcion de borrado y no la va a haber. `DECISIONES.md`
(2026-09-02, «Historico (FASE 8)») lo dice con sus palabras: un caso se archiva
(`archivado = 1` con fecha), **nunca se borra**; los archivados salen de las
listas de trabajo pero **siguen contando en los reportes**.

Las dos mitades de esa frase viven en sitios distintos y conviene saber donde:

  - «salen de las listas de trabajo» ya estaba hecho antes de esta fase.
    `datos/calendario.py` y `datos/pendientes.py` llevan `archivado = 0` escrito
    en sus cuatro consultas desde la FASE 5, cuando todavia no habia forma de
    archivar nada. Este modulo no toca ninguna de las dos.
  - «siguen contando en los reportes» es de `reportes/consultas.py`, que NO filtra
    por `archivado`. Esa ausencia es la decision, y esta escrita alli.

**Archivar se puede deshacer.** No es una concesion: archivar por error saca un
caso de todas las listas de trabajo a la vez, y sin `desarchivar_caso` el unico
camino de vuelta seria un `UPDATE` a mano sobre la base. Desarchivar no borra
nada; devuelve `archivado` a 0 y quita la fecha.

**El resultado del viaje es de cada persona, no del caso.** Un formulario de
grupo lleva hasta doce personas, y que el caso viajara no dice nada de si las doce
entraron. El motivo de por que las columnas viven en `personas` esta en
`datos/migraciones.py`, en la version 6.

Como en el resto de `datos/`, cada instruccion SQL es una cadena literal escrita
dentro de la llamada, con marcadores `?`, y los valores viajan aparte.
"""

from datos.esquema import marca_de_tiempo
from datos.validacion import ErrorDeValidacion

# Los tres valores de `personas.pudo_viajar`, con el nombre que usa el programa
# para cada uno. `None` es un valor de verdad y no un hueco: significa que nadie
# lo ha dicho, que es distinto de las otras dos respuestas.
SI_VIAJO = 1
NO_PUDO_VIAJAR = 0
NADIE_LO_HA_DICHO = None

# Tope de longitud del motivo. No es una regla de nadie: es lo que impide que un
# pegado accidental de media pagina entre entero en la base y luego desborde la
# celda del Excel y la linea del PDF del reporte. Cabe de sobra una explicacion
# escrita a mano.
LARGO_MAXIMO_DEL_MOTIVO = 300


def validar_pudo_viajar(pudo_viajar):
    """1 si viajo, 0 si no pudo, o nada mientras nadie lo haya dicho."""
    if pudo_viajar is None or pudo_viajar in (SI_VIAJO, NO_PUDO_VIAJAR):
        return pudo_viajar
    raise ErrorDeValidacion(
        f"El campo 'pudo_viajar' no vale: se recibió {pudo_viajar!r} y se esperaba "
        "1 (sí viajó), 0 (no pudo viajar) o nada (nadie lo ha dicho todavía)."
    )


def validar_motivo_no_viajo(motivo_no_viajo):
    """El motivo, sin espacios de sobra. Lo que queda vacio se trata como nulo.

    Vacio y nulo tienen que ser el mismo valor guardado: dos formas de decir «no
    hay motivo» son dos formas de que una consulta se olvide de una, y ademas el
    `CHECK` del motor mira `IS NOT NULL` y una cadena vacia lo pasaria.
    """
    if motivo_no_viajo is None:
        return None
    if not isinstance(motivo_no_viajo, str):
        raise ErrorDeValidacion(
            f"El campo 'motivo_no_viajo' no vale: se recibió {motivo_no_viajo!r} y "
            "se esperaba texto, como 'la recomendación llegó sin la firma del obispo'."
        )
    limpio = motivo_no_viajo.strip()
    if not limpio:
        return None
    if len(limpio) > LARGO_MAXIMO_DEL_MOTIVO:
        raise ErrorDeValidacion(
            f"El campo 'motivo_no_viajo' no vale: se recibieron {len(limpio)} "
            f"caracteres y el máximo son {LARGO_MAXIMO_DEL_MOTIVO}."
        )
    return limpio


def _exigir_motivo_cuando_no_viajo(pudo_viajar, motivo_no_viajo):
    """Las dos incoherencias posibles entre la respuesta y su motivo.

    El `CHECK` del motor tambien las para, y es la ultima defensa y no la primera:
    si solo estuviera alli, el fallo volveria con un mensaje en ingles que no dice
    cual de los dos campos hay que arreglar.
    """
    if pudo_viajar == NO_PUDO_VIAJAR and motivo_no_viajo is None:
        raise ErrorDeValidacion(
            "No se puede anotar que una persona no pudo viajar sin escribir por "
            "qué: una fila del reporte sin motivo no le dice nada a quien lo lee."
        )
    if pudo_viajar != NO_PUDO_VIAJAR and motivo_no_viajo is not None:
        raise ErrorDeValidacion(
            f"Se escribió el motivo {motivo_no_viajo!r} pero 'pudo_viajar' vale "
            f"{pudo_viajar!r}. El motivo solo se guarda cuando la respuesta es que "
            "la persona NO pudo viajar."
        )


def _leer_caso_para_archivar(conexion, caso_id):
    """El caso con lo justo para decidir, o el error que nombra el id que falta."""
    fila = conexion.execute(
        "SELECT id, numero_caso, archivado, fecha_archivado FROM casos WHERE id = ?",
        (caso_id,),
    ).fetchone()
    if fila is None:
        raise ErrorDeValidacion(f"No hay ningún caso con el id {caso_id!r}.")
    return fila


def archivar_caso(conexion, caso_id):
    """Marca el caso como archivado con la fecha de ahora y devuelve como quedo.

    Archivar dos veces **no reescribe la fecha**. Es a proposito: la fecha dice
    cuando se dio el caso por cerrado, y pisarla en un segundo clic borraria el
    unico dato que esta columna tiene que decir. La segunda llamada devuelve la
    fecha original y no falla — asi un doble clic no es un error que Miguel tenga
    que entender.
    """
    caso = _leer_caso_para_archivar(conexion, caso_id)
    if caso["archivado"] == 1:
        return dict(caso)

    conexion.execute(
        "UPDATE casos SET archivado = 1, fecha_archivado = ? WHERE id = ?",
        (marca_de_tiempo(), caso_id),
    )
    return dict(_leer_caso_para_archivar(conexion, caso_id))


def desarchivar_caso(conexion, caso_id):
    """Devuelve el caso a las listas de trabajo. No borra nada mas.

    Lo que NO se toca son `pudo_viajar` ni `motivo_no_viajo` de sus personas: son
    lo que paso de verdad con ese viaje, y no dejan de ser ciertos porque el caso
    vuelva a la cola. Quien se equivoco al anotarlos los corrige con
    `registrar_resultado_del_viaje`, que es donde se corrigen.
    """
    caso = _leer_caso_para_archivar(conexion, caso_id)
    if caso["archivado"] == 0:
        return dict(caso)

    conexion.execute(
        "UPDATE casos SET archivado = 0, fecha_archivado = NULL WHERE id = ?",
        (caso_id,),
    )
    return dict(_leer_caso_para_archivar(conexion, caso_id))


def esta_archivado(conexion, caso_id):
    """Dice si ese caso esta archivado. Levanta si el caso no existe."""
    return _leer_caso_para_archivar(conexion, caso_id)["archivado"] == 1


def _persona_existe(conexion, persona_id):
    """Comprueba que la persona esta antes de escribirle nada encima."""
    fila = conexion.execute(
        "SELECT id FROM personas WHERE id = ?", (persona_id,)
    ).fetchone()
    if fila is None:
        raise ErrorDeValidacion(f"No hay ninguna persona con el id {persona_id!r}.")
    return fila["id"]


def registrar_resultado_del_viaje(
    conexion, persona_id, pudo_viajar, motivo_no_viajo=None
):
    """Anota si esa persona llego a viajar y, si no, por que. Devuelve como quedo.

    Se puede volver a llamar con otra respuesta: es una correccion, no un
    duplicado. Pasar `pudo_viajar=None` devuelve la persona a «nadie lo ha dicho»
    y borra el motivo, que es la marcha atras legitima de haberlo anotado en la
    persona equivocada. Nada de esto borra a la persona ni al caso.
    """
    _persona_existe(conexion, persona_id)
    pudo_viajar = validar_pudo_viajar(pudo_viajar)
    motivo_no_viajo = validar_motivo_no_viajo(motivo_no_viajo)
    if pudo_viajar != NO_PUDO_VIAJAR:
        motivo_no_viajo = None
    _exigir_motivo_cuando_no_viajo(pudo_viajar, motivo_no_viajo)

    conexion.execute(
        "UPDATE personas SET pudo_viajar = ?, motivo_no_viajo = ? WHERE id = ?",
        (pudo_viajar, motivo_no_viajo, persona_id),
    )
    return leer_resultado_del_viaje(conexion, persona_id)


def leer_resultado_del_viaje(conexion, persona_id):
    """Lo anotado sobre el viaje de una persona, o None si esa persona no esta."""
    fila = conexion.execute(
        "SELECT id, caso_id, nombre, mrn, pudo_viajar, motivo_no_viajo "
        "FROM personas WHERE id = ?",
        (persona_id,),
    ).fetchone()
    return dict(fila) if fila is not None else None


def personas_del_caso_con_su_viaje(conexion, caso_id):
    """Las personas de un caso con lo anotado de su viaje, en el orden del papel.

    Es lo que necesita la pantalla que archiva: para preguntar quien viajo hay que
    ensenar a las personas con lo que ya estuviera anotado, no en blanco.
    """
    filas = conexion.execute(
        "SELECT id, caso_id, nombre, mrn, fila_formulario, pudo_viajar, "
        "motivo_no_viajo FROM personas WHERE caso_id = ? "
        "ORDER BY fila_formulario, id",
        (caso_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def casos_archivados(conexion):
    """El historico: los casos archivados, el ultimo archivado primero.

    Cada fila trae cuantas personas lleva y cuantas de ellas quedaron anotadas
    como que no pudieron viajar, que es lo que se lee de un vistazo en una lista
    de historico sin tener que entrar a cada caso.
    """
    filas = conexion.execute(
        "SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, c.fecha_viaje, "
        "c.estado_recomendacion, c.fecha_archivado, c.creado_en, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id AND p.pudo_viajar = 0) "
        "  AS personas_que_no_viajaron "
        "FROM casos c WHERE c.archivado = 1 "
        "ORDER BY c.fecha_archivado DESC, c.numero_caso"
    ).fetchall()
    return [dict(fila) for fila in filas]
