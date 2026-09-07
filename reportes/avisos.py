"""Los avisos del reporte: lo que hoy no se puede saber, y por qué no se puede.

Van aparte de `reportes/documento.py` porque son otra responsabilidad, y de las
que importan: el documento **cuenta**, y esto **dice de qué no se fía el número que
acaba de contar**. Es la mitad del reporte que evita que un cero se lea como
«ningún problema» cuando significa «nadie lo anotó».

**Ninguno está escrito a mano.** Cada aviso nace de una comprobación —una tupla
vacía, un conteo mayor que cero— y desaparece solo cuando la causa desaparece. Un
aviso fijo que dice «esto no se puede calcular» seguiría ahí el día que sí se
pueda, y entonces el reporte estaría mintiendo al revés.
"""

from datos.estados import ESTADOS_QUE_RESUELVEN

# Como se nombra aqui un caso al que no se le pudo leer el numero. Va escrito en
# este modulo y no importado de `interfaz/`: ningun modulo de `reportes`, `datos`
# o `espejo` importa de `interfaz` —medido: `grep -rn "from interfaz" reportes/
# espejo/ datos/` no devuelve ninguna linea—, y abrir esa dependencia por una
# cadena de texto pondria la capa de pantallas debajo de la de documentos.
SIN_NUMERO_DE_CASO = "(sin número de caso)"


def aviso_de_la_recomendacion_completa():
    """Existe mientras nadie haya dicho qué valor significa «resuelta».

    Se apaga solo: el día que `ESTADOS_QUE_RESUELVEN` deje de estar vacía, esta
    función devuelve `None` y el aviso desaparece del reporte sin tocar nada.
    """
    if ESTADOS_QUE_RESUELVEN:
        return None
    return (
        "«¿Recomendación completa?» sale «no se puede saber» en todas las filas, y "
        "no es un fallo del reporte. De los valores de estado de recomendación que "
        "constan en el material del proyecto, NINGUNO significa que la "
        "recomendación esté resuelta; el valor que lo significaría todavía no lo ha "
        "dicho el dueño. Hasta que lo diga, este reporte prefiere decir que no lo "
        "sabe antes que afirmar que alguien viajó con todo en regla."
    )


def aviso_de_los_casos_sin_estado(deteccion):
    """Existe cuando hay casos del período de los que nadie ha dicho nada.

    Es el aviso que impide leer la métrica 3 al revés. Un cero en «problemas
    detectados» puede ser un mes sin problemas o un mes sin anotar, y sin este
    aviso las dos cosas se ven exactamente igual.
    """
    if deteccion.sin_estado_registrado == 0:
        return None
    return (
        f"{deteccion.sin_estado_registrado} de los {deteccion.casos_del_periodo} "
        "casos que viajaban en el período no tienen escrito ningún estado de "
        "recomendación. Esos casos NO cuentan como problema en la métrica 3: "
        "cuentan como que nadie ha dicho nada. Si la métrica 3 sale en cero, "
        "puede ser porque no hubo problemas o porque nadie los anotó, y con estos "
        "datos no se puede distinguir una cosa de la otra."
    )


def aviso_de_los_que_no_caben_en_el_periodo(personas_sin_fecha):
    """Existe cuando alguien no pudo viajar y su caso no tiene fecha de viaje.

    Sin fecha no cabe en ningún período, así que no sale en ninguna de las dos
    partes. Una persona que no pudo viajar y no aparece en ningún reporte es
    exactamente la que se pierde, y por eso el aviso da los números de caso: con
    ellos se arregla en un minuto poniéndoles la fecha.

    ⚠️ **Un caso puede no tener número, y eso rompía el reporte entero.** Desde la
    versión 7 del esquema `casos.numero_caso` admite NULL, y desde la identidad por
    documento esos casos llegan hasta aquí. `sorted` sobre un conjunto donde cae un
    `None` junto a texto levanta `TypeError: '<' not supported between instances of
    'NoneType' and 'str'`, así que el informe completo se caía por culpa del caso
    que más falta hace mirar. Se le pone la palabra antes de ordenar, y así el
    conjunto es de texto y el orden vuelve a estar definido.
    """
    if not personas_sin_fecha:
        return None
    casos = ", ".join(
        sorted({fila["numero_caso"] or SIN_NUMERO_DE_CASO for fila in personas_sin_fecha})
    )
    return (
        f"{len(personas_sin_fecha)} personas anotadas como que no pudieron viajar "
        "NO salen en la parte 2 porque su caso no tiene fecha de viaje, y sin fecha "
        f"no caben en ningún período. Están en los casos: {casos}. Ponerles la "
        "fecha de viaje las hace aparecer."
    )


def avisos_del_reporte(deteccion, personas_sin_fecha):
    """Los avisos que hoy tocan, cada uno derivado de una medición."""
    posibles = (
        aviso_de_la_recomendacion_completa(),
        aviso_de_los_casos_sin_estado(deteccion),
        aviso_de_los_que_no_caben_en_el_periodo(personas_sin_fecha),
    )
    return tuple(aviso for aviso in posibles if aviso is not None)
