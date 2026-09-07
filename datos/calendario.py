"""Las tres consultas de fecha que alimentan la pantalla de inicio.

Esto es la FASE 5 sin interfaz: funciones que devuelven datos. Ni una ventana, ni
un color, ni una frase de pantalla. Quien pinte esto decide como se ve; aqui se
decide QUE se ve, que es lo que no puede salir mal.

Las tres consultas comparten dos cosas que no son casualidad:

  - **`archivado = 0` va dentro del `WHERE` de las DOS consultas de alarma.**
    Lo pide `PENDIENTES.md`, FASE 5, criterio 6, con su motivo: anadirlo despues
    obliga a repasar todas las consultas de la fase, y basta que se escape una
    para que un caso archivado reaparezca en el bloque rojo.

    ⚠️ **`vista_de_mes` es la excepcion, y es una reversion pedida por el dueno**
    (`DECISIONES.md`, 2026-09-03, «Lo archivado se sigue viendo en el calendario,
    marcado archivado»), literal: *«Lo que archivo debe verse en el calendario,
    debe decir archivado.»* El calendario es contexto —contesta «que paso este
    mes»— y un mes al que le faltan los casos cerrados no cuenta lo que paso.
    Las otras dos consultas NO cambian: la franja roja y la lista de pendientes
    son trabajo por hacer, y un caso archivado ya no lo es. Meterlo ahi taparia
    los que si lo son.
  - **«Hoy» entra como parametro.** Ninguna funcion de este modulo llama a
    `date.today()`. Una funcion que mira el reloj por dentro no se puede probar
    por los bordes, y el borde es justo lo que hay que probar aqui.

Sobre las fechas: se comparan como TEXTO ISO-8601, sin funciones de fecha del
motor. `docs/ARQUITECTURA.md` §1.2 lo decide asi porque ese formato ordena
cronologicamente como cadena, y por eso `idx_casos_viaje_activos` —que es un
indice sobre `fecha_viaje` filtrado por `archivado = 0`— sirve para estas tres.

Como en el resto de `datos/`, cada instruccion SQL es una cadena literal escrita
dentro de la llamada, con marcadores `?`, y todos los valores viajan aparte.
"""

from datetime import date, datetime, timedelta

from datos.estados import (
    SEPARADOR_DE_ESTADOS,
    recomendacion_sin_resolver,
    texto_de_estados_que_resuelven,
)
from datos.validacion import ErrorDeValidacion, validar_fecha_viaje

# Los dias que abarca el bloque de arriba de la pantalla de inicio. El dia 7 entra
# y el dia 8 no (`PENDIENTES.md`, FASE 5, criterio 3).
DIAS_DE_LA_VENTANA = 7


class ErrorDeConsulta(ValueError):
    """A una consulta del calendario se le paso algo que no puede usar."""


def a_fecha(valor, nombre_del_parametro):
    """Convierte a `date` lo que llegue: un `date`, un `datetime` o 'AAAA-MM-DD'.

    Un `datetime` se recorta a su dia a proposito. Si se dejara pasar entero, su
    `isoformat()` traeria la hora pegada y toda comparacion contra `fecha_viaje`
    —que es 'AAAA-MM-DD' pelado— saldria corrida sin que nadie lo note.
    """
    if isinstance(valor, datetime):
        return valor.date()
    if isinstance(valor, date):
        return valor
    if isinstance(valor, str):
        try:
            return date.fromisoformat(validar_fecha_viaje(valor))
        except ErrorDeValidacion as causa:
            raise ErrorDeConsulta(
                f"El parámetro {nombre_del_parametro!r} no vale: {causa}"
            ) from causa
    raise ErrorDeConsulta(
        f"El parámetro {nombre_del_parametro!r} no vale: se recibió {valor!r} y se "
        "esperaba una fecha, como date(2026, 9, 8) o '2026-09-08'."
    )


def _limite_de_la_ventana(hoy, dias_de_la_ventana):
    """El ultimo dia que entra en la ventana, ya en texto para comparar."""
    if isinstance(dias_de_la_ventana, bool) or not isinstance(dias_de_la_ventana, int):
        raise ErrorDeConsulta(
            f"El parámetro 'dias_de_la_ventana' no vale: se recibió "
            f"{dias_de_la_ventana!r} y se esperaba un número entero de días."
        )
    if dias_de_la_ventana < 0:
        raise ErrorDeConsulta(
            f"El parámetro 'dias_de_la_ventana' no vale: se recibió "
            f"{dias_de_la_ventana!r} y una ventana no puede medir menos de 0 días."
        )
    return (hoy + timedelta(days=dias_de_la_ventana)).isoformat()


def estados_que_resuelven_para_el_motor():
    """El texto que la consulta compara, con el separador comprobado antes de irse.

    Es publica desde el 2026-09-03 porque `datos/equipo.py` cuenta con el mismo
    criterio: los contadores del panel y la franja roja tienen que estar de acuerdo
    sobre que significa «resuelta». Dos definiciones de eso en dos modulos es como
    el panel acabaria diciendo 9 completas mientras la franja roja cuenta 10.

    La consulta lleva la coma escrita dentro, en `',' || estado || ','`. Si alguien
    cambiara `SEPARADOR_DE_ESTADOS` y no la consulta, el filtro no casaria con
    ningun estado y el bloque de riesgo saldria vacio: exactamente el fallo que no
    se puede permitir que ocurra callado.
    """
    if SEPARADOR_DE_ESTADOS != ",":
        raise ErrorDeConsulta(
            f"SEPARADOR_DE_ESTADOS vale {SEPARADOR_DE_ESTADOS!r} y las consultas de "
            "este modulo llevan ',' escrita dentro. Mientras no coincidan, el filtro "
            "de recomendaciones sin resolver no casaria con nada."
        )
    return texto_de_estados_que_resuelven()


def _dias_hasta(fecha_viaje, hoy, numero_caso):
    """Cuantos dias faltan para ese viaje. Negativo si ya paso."""
    try:
        return (date.fromisoformat(fecha_viaje) - hoy).days
    except ValueError as causa:
        raise ErrorDeConsulta(
            f"El caso {numero_caso!r} tiene guardada la fecha de viaje "
            f"{fecha_viaje!r}, que no es una fecha que exista ({causa}). Hay que "
            "corregirla a mano antes de que el calendario la pueda situar."
        ) from causa


def _caso_con_la_cuenta_de_dias(fila, hoy):
    """Una fila de caso, con lo que la pantalla necesita saber ya calculado."""
    caso = dict(fila)
    caso["dias_para_el_viaje"] = _dias_hasta(
        caso["fecha_viaje"], hoy, caso["numero_caso"]
    )
    caso["ya_viajo"] = caso["dias_para_el_viaje"] < 0
    caso["recomendacion_sin_resolver"] = recomendacion_sin_resolver(
        caso["estado_recomendacion"]
    )
    return caso


def casos_que_viajan_pronto(conexion, hoy, dias_de_la_ventana=DIAS_DE_LA_VENTANA):
    """Los casos sin archivar que viajan de aqui a `dias_de_la_ventana` dias.

    Entran tambien los que **ya viajaron** y siguen sin archivar, marcados con
    `ya_viajo` (`PENDIENTES.md`, FASE 5, criterio 3: «un caso cuya fecha ya paso
    aparece tambien, y distinguido de los futuros»). No es un descuido de la
    ventana: un viaje que ya ocurrio con la recomendacion mal es el dano
    consumado, y esconderlo seria lo peor que podria hacer esta consulta. Lo que
    los saca de la lista es archivarlos, que es la FASE 8.

    Un caso sin `fecha_viaje` no sale aqui: no se puede situar en el calendario.
    Sale en `datos.pendientes`, que es donde se atiende lo que le falta.

    Devuelve una lista de diccionarios ordenada por fecha de viaje ascendente.
    """
    hoy = a_fecha(hoy, "hoy")
    limite = _limite_de_la_ventana(hoy, dias_de_la_ventana)

    filas = conexion.execute(
        "SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, c.fecha_viaje, "
        "c.estado_recomendacion, c.captura_manual, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas "
        "FROM casos c "
        "WHERE c.archivado = 0 "
        "AND c.fecha_viaje IS NOT NULL "
        "AND c.fecha_viaje <= ? "
        "ORDER BY c.fecha_viaje, c.numero_caso",
        (limite,),
    ).fetchall()
    return [_caso_con_la_cuenta_de_dias(fila, hoy) for fila in filas]


def casos_en_riesgo(conexion, hoy, dias_de_la_ventana=DIAS_DE_LA_VENTANA):
    """Los de arriba que ademas llevan la recomendacion sin resolver.

    Esta es la consulta por la que existe el programa: un caso que viaja pronto
    con la recomendacion sin resolver es alguien que llega al templo y no entra.
    Va aparte de `casos_que_viajan_pronto` a proposito, y no como un filtro que la
    pantalla aplique despues: lo que decide quien esta en riesgo se decide aqui,
    en una sola consulta, y no repartido por la interfaz.

    Que cuenta como «sin resolver» lo dice `datos.estados` y nada mas. ⚠️ Mientras
    `ESTADOS_QUE_RESUELVEN` siga vacia —hoy lo esta, porque el dueno no ha dicho el
    valor que significa «resuelta»— esta consulta devuelve **lo mismo** que
    `casos_que_viajan_pronto`. Las dos listas se separan solas el dia que ese valor
    exista; no hay nada mas que tocar aqui.
    """
    hoy = a_fecha(hoy, "hoy")
    limite = _limite_de_la_ventana(hoy, dias_de_la_ventana)
    estados_que_resuelven = estados_que_resuelven_para_el_motor()

    filas = conexion.execute(
        "SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, c.fecha_viaje, "
        "c.estado_recomendacion, c.captura_manual, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas "
        "FROM casos c "
        "WHERE c.archivado = 0 "
        "AND c.fecha_viaje IS NOT NULL "
        "AND c.fecha_viaje <= ? "
        "AND (c.estado_recomendacion IS NULL "
        "     OR instr(?, ',' || c.estado_recomendacion || ',') = 0) "
        "ORDER BY c.fecha_viaje, c.numero_caso",
        (limite, estados_que_resuelven),
    ).fetchall()
    return [_caso_con_la_cuenta_de_dias(fila, hoy) for fila in filas]


def _primer_dia_del_mes_siguiente(anio, mes):
    """El limite abierto por arriba del mes, sin sumar dias ni contar bisiestos."""
    return date(anio + 1, 1, 1) if mes == 12 else date(anio, mes + 1, 1)


def vista_de_mes(conexion, anio, mes):
    """Los casos de un mes, **archivados incluidos**, agrupados por dia de viaje.

    Devuelve un diccionario `{'AAAA-MM-DD': [caso, ...]}` con los dias en orden
    ascendente. **Solo aparecen los dias que tienen algun caso**: rellenar el mes
    entero de dias vacios es dibujar una cuadricula, y eso es de quien pinte.

    Cada caso trae su unidad y cuantas personas lleva, que es lo que se lee de un
    vistazo en una casilla de calendario, y ademas **`archivado`** —0 o 1— para que
    quien pinte pueda escribir la palabra al lado. Se devuelve el dato y no una
    frase hecha: esta capa decide QUE se ve, no como se dice.

    Un caso archivado sale ordenado igual que los demas, por fecha y numero. No va
    al final: el dia que viajo es el dia que viajo, y sacarlo de su sitio moveria
    de casilla lo que el dueno quiere ver en su casilla.
    """
    try:
        primero = date(anio, mes, 1)
    except (TypeError, ValueError) as causa:
        raise ErrorDeConsulta(
            f"No hay ningún mes {mes!r} del año {anio!r} ({causa}). Se esperaban dos "
            "enteros, como vista_de_mes(conexion, 2026, 9)."
        ) from causa
    siguiente = _primer_dia_del_mes_siguiente(anio, mes)

    # Aqui NO va `c.archivado = 0`. Es lo unico que separa esta consulta de las
    # dos de arriba, y esta dicho en la cabecera del modulo con la frase del dueno
    # que lo pidio. `fecha_archivado` viaja para poder decir cuando se cerro sin
    # una segunda consulta por caso.
    filas = conexion.execute(
        "SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, c.fecha_viaje, "
        "c.estado_recomendacion, c.captura_manual, c.archivado, c.fecha_archivado, "
        "(SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas "
        "FROM casos c "
        "WHERE c.fecha_viaje >= ? "
        "AND c.fecha_viaje < ? "
        "ORDER BY c.fecha_viaje, c.numero_caso",
        (primero.isoformat(), siguiente.isoformat()),
    ).fetchall()

    dias = {}
    for fila in filas:
        caso = dict(fila)
        caso["esta_archivado"] = bool(caso["archivado"])
        # Un caso archivado NUNCA se pinta de rojo, se llame como se llame su
        # `estado_recomendacion`. Ya se cerro: la alarma dejo de tener sentido, y
        # un rojo que no pide nada es el que ensena a ignorar los rojos.
        caso["recomendacion_sin_resolver"] = (
            False
            if caso["esta_archivado"]
            else recomendacion_sin_resolver(caso["estado_recomendacion"])
        )
        dias.setdefault(caso["fecha_viaje"], []).append(caso)
    return dias
