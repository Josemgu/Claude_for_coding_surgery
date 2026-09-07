"""Las tres metricas de trabajo del equipo. Solo aritmetica: aqui no se consulta.

Las tres parten de la MISMA lectura —`consultas.casos_con_su_verificacion`— y por
eso no pueden discrepar entre ellas. Cada una recorta el trozo que le toca, y las
tres dicen a las claras que columna de fecha usan, porque **cada una usa una
distinta** y confundirlas es leer el reporte al reves:

    1. Casos verificados ............ por `verificado_en` (cuando se termino)
    2. Demora de importar a verificar por `verificado_en`, midiendo hasta `creado_en`
    3. Deteccion antes del viaje .... por `fecha_viaje` (cuando viajaba la gente)

**Un caso esta verificado cuando tiene campos y TODOS estan verificados.** Cero
campos no es «todo verificado»: es que nadie leyo nada. Contarlo al reves inflaria
la metrica del equipo con casos que nadie ha tocado.

⚠️ **La metrica 3 y el hueco que hoy tiene.** «Detectado con problema» significa
que el caso lleva un `estado_recomendacion` escrito y ese valor **no** es de los
que resuelven. Un `estado_recomendacion` vacio NO cuenta como problema: cuenta
como que nadie ha dicho nada, y sale en su propio numero. Esa distincion es lo
contrario de lo que hace `datos/calendario.py` —alli un estado vacio pinta en rojo,
que es el lado seguro para una alarma— y es a proposito: una alarma de mas se
descarta mirandola, pero un numero de mas en un reporte a los jefes es una cifra
falsa. Aqui no se cuenta como problema lo que nadie ha dicho que lo sea.
"""

from collections import namedtuple
from datetime import datetime

from datos.estados import recomendacion_sin_resolver

_FORMATO_DE_MARCA_DE_TIEMPO = "%Y-%m-%d %H:%M:%S"
_LARGO_DE_LA_FECHA = 10

Demora = namedtuple(
    "Demora",
    ("casos_medidos", "promedio_en_horas", "minimo_en_horas", "maximo_en_horas",
     "descartados_por_fechas_incoherentes"),
)

Deteccion = namedtuple(
    "Deteccion",
    ("casos_del_periodo", "con_problema_registrado", "detectados_a_tiempo",
     "detectados_despues_del_viaje", "con_problema_sin_fecha_de_deteccion",
     "sin_estado_registrado", "con_recomendacion_resuelta"),
)


def caso_verificado(caso):
    """Dice si a ese caso no le queda ni un campo por verificar."""
    return caso["campos"] > 0 and caso["campos_verificados"] == caso["campos"]


def _dentro_del_periodo(marca_de_tiempo, periodo):
    """Si una marca de tiempo con hora cae dentro del periodo, extremos incluidos.

    Se compara como texto contra `siguiente_a_hasta` y no contra `hasta`: '2026-
    09-30 14:12:03' es mayor que '2026-09-30', y un `<=` se dejaria fuera todo lo
    que paso ese dia despues de medianoche.
    """
    if marca_de_tiempo is None:
        return False
    return periodo.desde <= marca_de_tiempo < periodo.siguiente_a_hasta


def casos_verificados_en_el_periodo(casos, periodo):
    """Metrica 1: los casos que quedaron verificados dentro del periodo.

    Se ordenan por el instante en que se terminaron, que es como se lee una lista
    de trabajo hecho.
    """
    verificados = [
        caso
        for caso in casos
        if caso_verificado(caso) and _dentro_del_periodo(caso["verificado_en"], periodo)
    ]
    return sorted(verificados, key=lambda caso: caso["verificado_en"])


def _horas_entre(creado_en, verificado_en):
    """Cuantas horas pasaron entre las dos marcas, o None si alguna no se puede leer.

    Devuelve None y no cero cuando una fecha esta mal escrita: un cero se sumaria
    al promedio como si el caso se hubiera verificado al instante, que es una
    cifra inventada. Sin fecha legible el caso se descarta y se cuenta aparte.
    """
    try:
        entrada = datetime.strptime(creado_en, _FORMATO_DE_MARCA_DE_TIEMPO)
        salida = datetime.strptime(verificado_en, _FORMATO_DE_MARCA_DE_TIEMPO)
    except (TypeError, ValueError):
        return None
    return (salida - entrada).total_seconds() / 3600.0


def demora_de_importar_a_verificar(casos, periodo):
    """Metrica 2: cuanto se tarda desde que un caso entra hasta que queda verificado.

    Mide los mismos casos que la metrica 1 —los verificados dentro del periodo—,
    asi que el denominador de las dos es el mismo numero y se pueden leer juntas.

    Una demora negativa se descarta y se cuenta aparte en vez de promediarse. Una
    fecha de verificacion anterior a la de importacion no es un trabajo hecho en
    tiempo negativo: es un reloj que se movio, y meterla en la media bajaria el
    promedio de todos los demas sin que nadie sepa por que.
    """
    horas = []
    descartados = 0
    for caso in casos_verificados_en_el_periodo(casos, periodo):
        medida = _horas_entre(caso["creado_en"], caso["verificado_en"])
        if medida is None or medida < 0:
            descartados += 1
            continue
        horas.append(medida)

    if not horas:
        return Demora(0, None, None, None, descartados)
    return Demora(len(horas), sum(horas) / len(horas), min(horas), max(horas), descartados)


def _dia_de(marca_de_tiempo):
    """El dia de una marca de tiempo, para compararlo con una `fecha_viaje`."""
    return None if marca_de_tiempo is None else marca_de_tiempo[:_LARGO_DE_LA_FECHA]


def _tiene_problema_registrado(caso):
    """Si alguien escribio un estado y ese estado no es de los que resuelven."""
    estado = caso["estado_recomendacion"]
    return estado is not None and recomendacion_sin_resolver(estado)


def _clasificar_la_deteccion(caso):
    """En cual de los cinco cubos cae un caso del periodo. Uno y solo uno.

    Los cinco son excluyentes y suman el total, que es lo que permite comprobar el
    reporte sumando: si los cinco no suman el universo, algo se conto dos veces.
    """
    if caso["estado_recomendacion"] is None:
        return "sin_estado_registrado"
    if not _tiene_problema_registrado(caso):
        return "con_recomendacion_resuelta"
    dia_de_la_deteccion = _dia_de(caso["verificado_en"])
    if dia_de_la_deteccion is None:
        return "con_problema_sin_fecha_de_deteccion"
    if dia_de_la_deteccion < caso["fecha_viaje"]:
        return "detectados_a_tiempo"
    return "detectados_despues_del_viaje"


def deteccion_antes_del_viaje(casos, periodo):
    """Metrica 3: de los que viajaban en el periodo, en cuantos se vio el problema a tiempo.

    Es la metrica por la que existe el programa: un problema visto el dia antes se
    puede arreglar, y visto el dia despues ya mando a alguien al templo para nada.

    «A tiempo» es **el dia anterior o antes**, no el mismo dia. Detectar el problema
    la manana del viaje no deja margen para arreglar una recomendacion, y contarlo
    como exito seria contar como salvado a alguien que no se salvo.

    La fecha de deteccion es el instante en que se termino de verificar el caso.
    Es lo mas cercano a «cuando alguien miro esto» que la base guarda hoy; un caso
    sin ningun campo verificado no tiene fecha de deteccion y sale en su propio
    numero, no repartido entre los otros dos.
    """
    del_periodo = [
        caso
        for caso in casos
        if caso["fecha_viaje"] is not None
        and periodo.desde <= caso["fecha_viaje"] <= periodo.hasta
    ]
    cubos = {
        "sin_estado_registrado": 0,
        "con_recomendacion_resuelta": 0,
        "con_problema_sin_fecha_de_deteccion": 0,
        "detectados_a_tiempo": 0,
        "detectados_despues_del_viaje": 0,
    }
    for caso in del_periodo:
        cubos[_clasificar_la_deteccion(caso)] += 1

    con_problema = (
        cubos["detectados_a_tiempo"]
        + cubos["detectados_despues_del_viaje"]
        + cubos["con_problema_sin_fecha_de_deteccion"]
    )
    return Deteccion(
        casos_del_periodo=len(del_periodo),
        con_problema_registrado=con_problema,
        detectados_a_tiempo=cubos["detectados_a_tiempo"],
        detectados_despues_del_viaje=cubos["detectados_despues_del_viaje"],
        con_problema_sin_fecha_de_deteccion=cubos["con_problema_sin_fecha_de_deteccion"],
        sin_estado_registrado=cubos["sin_estado_registrado"],
        con_recomendacion_resuelta=cubos["con_recomendacion_resuelta"],
    )
