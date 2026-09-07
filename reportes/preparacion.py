"""El número por el que existe este programa: cuántos viajaron sin estar listos.

Es la cuenta con la que abre el informe a la dirección, y viene del proyecto viejo
(`salida/informe.py`), que es el que el dueño aprobó:

    N de las M personas que ya viajaron lo hicieron SIN la preparación completa.

Una persona que viaja sin la preparación completa no hace la ordenanza: hizo
el viaje y volvió como se fue. Todo lo demás del informe está para explicar ese
número y para poder bajarlo el mes que viene.

**Aquí solo hay aritmética: ni una consulta.** Lo que llega es la lista de personas
del período que devuelve `reportes/consultas.py`, y de ella salen todas las cifras,
para que dos números del mismo documento no puedan contradecirse por haberse
calculado de dos maneras.

⚠️ **«Con la preparación completa» quiere decir los seis pasos en «Sí», y nada
más.** No se mira `estado_recomendacion`: ese campo lo escribe Miguel sobre el caso
entero y la preparación es de cada persona. Una persona sin ningún paso contestado
cuenta como **sin la preparación completa**, no como desconocida, y es el lado
seguro del error: si nadie la miró, nadie puede afirmar que viajó en regla.

⚠️ **Y por eso este módulo dejó de decir «verificada» el 2026-09-03.** La palabra
significaba dos cosas distintas en el mismo programa: en la pantalla de corrección
es la FIRMA de Miguel sobre un campo leído, y aquí eran los seis pasos que contesta
un compañero. Un informe que dice «12 verificadas» al lado de una pantalla que dice
«36 campos verificados» hace que quien lee las dos crea que habla de lo mismo. La
firma de Miguel se sigue llamando verificación —las métricas del final del informe
la nombran así, y ahí sí es eso—; la preparación de una persona se llama ahora
**«con la preparación completa»** y **«sin la preparación completa»**.

**Sin dinero, a propósito.** Citado del viejo: el presupuesto lo lleva otro
departamento, y meterlo aquí solo serviría para discutir de lo que no es. Ninguna
función de este módulo cuenta un importe, y `pruebas/` lo comprueba sobre el
documento entero.
"""

from collections import namedtuple

from datos.pasos import estado_de_los_pasos, pasos_sin_completar
from reportes.formato import SIN_AGENTE

NADIE_LA_MIRO = "nadie la miró"
NO_ESTA_COMPLETA = "no está completa"

Recuento = namedtuple(
    "Recuento",
    ("viajaron", "sin_completar", "completas", "por_viajar", "casos"),
)


def ya_viajo(persona, dia_de_hoy):
    """Si esa persona ya hizo el viaje. Sin fecha, todavía no viajó.

    El día del viaje **no** cuenta como viajado: mientras el día no termina, la
    preparación todavía se puede arreglar, y contar a esa persona entre las
    perdidas sería darla por perdida antes de tiempo.
    """
    fecha = persona.get("fecha_viaje")
    return bool(fecha) and fecha < dia_de_hoy


def tiene_la_preparacion_completa(persona):
    """Si los seis pasos de esa persona constan en «Sí». Ni uno menos.

    Se llamaba `esta_verificada` y cambió de nombre el 2026-09-03 por la misma
    razón por la que cambiaron los rótulos del informe: «verificado» significaba
    dos cosas distintas en el mismo programa —la firma de Miguel en la pantalla, y
    los seis pasos en «Sí»— y las dos se leían en la misma frase.
    """
    return estado_de_los_pasos(persona) is True


def que_paso(persona):
    """Por qué esa persona viajó sin la preparación completa, y en qué se quedó.

    Distingue las dos formas de llegar al mismo sitio, que piden dos remedios
    distintos: «no está completa» es un trabajo que alguien hizo y salió mal, y
    «nadie la miró» es un trabajo que no se hizo. Cuando se sabe en qué paso se
    quedó, se dice: es lo que hay que hablar con el líder.
    """
    if estado_de_los_pasos(persona) is False:
        trabados = pasos_sin_completar(persona)
        if trabados:
            return f"{NO_ESTA_COMPLETA}: falta {' y '.join(trabados)}"
        return NO_ESTA_COMPLETA
    return NADIE_LA_MIRO


def recontar(personas, dia_de_hoy):
    """Las cuatro cifras del encabezado, contadas una sola vez.

    Los tres primeros grupos son excluyentes y `viajaron` es la suma de los dos
    primeros: eso permite comprobar el informe sumando, que es lo que hace que un
    número mal contado se vea en vez de creerse.
    """
    viajaron = [persona for persona in personas if ya_viajo(persona, dia_de_hoy)]
    return Recuento(
        viajaron=tuple(viajaron),
        sin_completar=tuple(
            p for p in viajaron if not tiene_la_preparacion_completa(p)
        ),
        completas=tuple(p for p in viajaron if tiene_la_preparacion_completa(p)),
        por_viajar=tuple(p for p in personas if not ya_viajo(p, dia_de_hoy)),
        # ⚠️ Se cuentan CASOS y no numeros de caso. Desde la version 12 del esquema
        # dos casos pueden llevar el mismo numero, y contar numeros distintos diria
        # «1 caso en el período» donde hay dos. Se guardan los ids, que es lo que
        # de verdad se esta contando; ningun `None` se cuela en un `sorted` mezclado
        # con textos, que ademas era un `TypeError` esperando a un caso sin numero.
        casos=tuple(sorted({p["caso_id"] for p in personas})),
    )


def titular(recuento):
    """La frase con la que abre el informe. Nunca lleva porcentajes.

    Sobre ocho personas un porcentaje engaña más de lo que informa, y es la razón
    que da el proyecto viejo para no ponerlos en ningún sitio de este documento.
    """
    if not recuento.viajaron:
        return "Todavía no ha viajado nadie de los que hay cargados en el período."
    if not recuento.sin_completar:
        return (
            f"Las {len(recuento.viajaron)} personas que viajaron en este período "
            "salieron con la preparación completa. No se perdió ninguna ordenanza."
        )
    return (
        f"{len(recuento.sin_completar)} de las {len(recuento.viajaron)} personas que "
        "ya viajaron lo hicieron SIN la preparación completa. Esas ordenanzas no se "
        "hicieron: el viaje se hizo igual y la persona volvió como se fue."
    )


def _casos_de(personas):
    """Las personas agrupadas por caso, conservando el orden de lectura.

    ⚠️ **Se agrupa por `caso_id` y no por `numero_caso`, desde el 2026-09-03.**
    Mientras el numero fue `UNIQUE` las dos cosas eran la misma; desde la version 12
    del esquema no lo son, y agrupar por numero fundia en un renglon a dos familias
    distintas de la misma unidad y el mismo mes. El informe habria dicho «viajan 9»
    donde viajan 4 y 5 por separado, y quien lo lee no tiene forma de notarlo.
    """
    por_caso = {}
    for persona in personas:
        por_caso.setdefault(persona["caso_id"], []).append(persona)
    return por_caso


def resumen_por_caso(personas, dia_de_hoy, companeros_por_caso):
    """Un renglón por viaje: quién lo lleva, cuántos van y cuántos sin completar.

    ⚠️ **Quién lo tiene va en la misma fila que el viaje.** Un informe que dice que
    un caso salió con gente sin la preparación completa y no dice de quién era
    obliga a ir a buscarlo a otra parte para poder preguntar, y entonces no se
    pregunta.

    «Sin la preparación completa» solo se cuenta en los casos que ya viajaron. En
    uno que todavía no ha salido, lo que falta no es un fallo: es trabajo por hacer,
    y se dice con la palabra que dice que no aplica.

    El templo sale de la primera persona del caso porque es un dato DEL caso —lo
    trae `casos.templo_nombre` desde la versión 10 del esquema— y las personas de un
    caso lo llevan todas igual: viajan juntas al mismo sitio.
    """
    renglones = []
    for _caso_id, suyas in sorted(
        _casos_de(personas).items(),
        # Se ordena por fecha y luego por NUMERO —no por el id— porque es lo que
        # Miguel lee. El id entra solo al final para desempatar dos casos que
        # comparten numero, que es lo que ahora puede pasar: sin el, el orden entre
        # esos dos dependeria del azar del diccionario y el informe saldria
        # distinto cada vez.
        key=lambda par: (
            par[1][0].get("fecha_viaje") or "9999-99-99",
            par[1][0].get("numero_caso") or "",
            par[0],
        ),
    ):
        una = suyas[0]
        viajado = ya_viajo(una, dia_de_hoy)
        asignados = companeros_por_caso.get(una["caso_id"], ())
        renglones.append(
            {
                "numero_caso": una.get("numero_caso"),
                "fecha_viaje": una.get("fecha_viaje"),
                "templo": una.get("templo_nombre"),
                "asignado_a": ", ".join(asignados) if asignados else SIN_AGENTE,
                "viajan": len(suyas),
                "completas": sum(
                    1 for p in suyas if tiene_la_preparacion_completa(p)
                ),
                "sin_completar": (
                    sum(1 for p in suyas if not tiene_la_preparacion_completa(p))
                    if viajado
                    else None
                ),
            }
        )
    return tuple(renglones)
