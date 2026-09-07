"""El unico sitio donde vive la lista de valores de `estado_recomendacion`.

DECISIONES.md (2026-09-02, P-1) lo decide asi: la columna se guarda, NO lleva un
`CHECK` con una lista inventada, y la lista de valores validos vive en un solo
sitio del codigo.

La lista arranca con los dos unicos valores que constan en el material del
proyecto. ⚠️ Faltan otros que el dueno todavia no ha dicho: anadirlos es tocar
esta tupla y nada mas. Ni el esquema ni ninguna otra funcion los enumera.

Desde la FASE 5 este modulo responde ademas a una segunda pregunta, que es la que
decide si un caso sale pintado en rojo: **cual de esos valores significa que la
recomendacion ya esta resuelta**. Va aqui, junto a la lista, y no dentro de la
consulta, por el mismo motivo por el que la lista vive en un solo sitio.
"""


class ErrorDeEstados(RuntimeError):
    """La lista de estados quedo en un estado que no se puede usar."""


# Los dos primeros son los que constaban en el material del proyecto y los usa la
# propuesta del companero por PERSONA (`datos/propuestas.py`,
# `paquete/reconciliacion.py`). Los dos ultimos los dijo el dueno el 2026-09-03,
# con sus palabras, y son los del DOCUMENTO: «Sí, completa» / «No está completa»
# (DECISIONES.md, 2026-09-03, decision 1). Se ANADEN al final y no sustituyen a los
# dos primeros: `datos/propuestas.py` ordena por el indice de esta tupla, y meter
# valores en medio le cambiaria el orden a un codigo que no es de esta pantalla.
COMPLETA = "completa"
NO_COMPLETA = "no_completa"
ESTADOS_RECOMENDACION = ("no_indicada", "incompleta", COMPLETA, NO_COMPLETA)

# Cuales de los valores de arriba significan «con esta recomendacion ya no hay
# nada que hacer».
#
# ~~Hoy esta VACIA~~ **Desde el 2026-09-03 lleva `completa`**, que es la palabra que
# el dueno dijo y que sustituye al nombre `resuelta` que se habia propuesto antes
# (DECISIONES.md, 2026-09-03). Hasta ese dia estuvo vacia a proposito, porque
# ninguno de los dos valores que habia significaba «resuelta» y aqui no se inventa.
#
# `no_completa` NO entra, y no es un descuido: un documento que Miguel —o la hoja
# del companero— dio por incompleto es justo el que tiene que seguir saliendo en el
# bloque rojo de viajes proximos.
#
# ⚠️ Que esto cambia, dicho para que nadie se sorprenda: hasta hoy TODO caso sin
# archivar contaba como «sin resolver» y `casos_en_riesgo` devolvia lo mismo que
# `casos_que_viajan_pronto`. Desde ahora las dos listas se separan, y lo que las
# separa es esta linea.
ESTADOS_QUE_RESUELVEN = (COMPLETA,)

# Con que se separan los estados cuando viajan al motor como un solo parametro.
# `datos/calendario.py` lo comprueba antes de usarlo: la consulta lleva este mismo
# caracter escrito dentro, y si los dos dejaran de coincidir el filtro no casaria
# con nada y el bloque rojo saldria vacio sin avisar.
SEPARADOR_DE_ESTADOS = ","


def _comprobar_la_clasificacion():
    """Un valor mal escrito en `ESTADOS_QUE_RESUELVEN` apaga la alarma en silencio.

    Por eso esto levanta al importar el modulo, y no devuelve un aviso: un
    programa que no arranca se arregla en un minuto; un bloque de viajes proximos
    que sale vacio porque alguien escribio `completa ` con un espacio detras no se
    nota hasta que alguien no entra al templo.
    """
    for estado in ESTADOS_QUE_RESUELVEN:
        if estado not in ESTADOS_RECOMENDACION:
            raise ErrorDeEstados(
                f"El estado {estado!r} figura en ESTADOS_QUE_RESUELVEN pero no en "
                f"ESTADOS_RECOMENDACION. Un valor que no se puede guardar tampoco "
                "puede resolver nada: revisa si es una errata."
            )
    for estado in ESTADOS_RECOMENDACION:
        if not estado:
            raise ErrorDeEstados(
                "ESTADOS_RECOMENDACION lleva un valor vacío. Un estado sin nombre no "
                "se puede distinguir de «no hay estado», que ya significa otra cosa."
            )
        if SEPARADOR_DE_ESTADOS in estado:
            raise ErrorDeEstados(
                f"El estado {estado!r} lleva un {SEPARADOR_DE_ESTADOS!r} dentro, "
                "que es justo el carácter con el que se separan los estados cuando "
                "viajan al motor. Renómbralo o cambia SEPARADOR_DE_ESTADOS."
            )


_comprobar_la_clasificacion()


def texto_de_estados_que_resuelven():
    """Los estados que resuelven, en una sola cadena rodeada de separadores.

    Sale `,` con la tupla vacia, `,completa,` con un valor y `,completa,otra,` con
    dos. Los separadores de los extremos no son adorno: son lo que hace que
    buscar `,completa,` dentro no pueda casar a medias con `,incompleta,`.

    Existe para que la consulta pueda filtrar por una lista de largo variable sin
    armar el SQL con una f-string. El texto viaja como PARAMETRO, no como parte de
    la instruccion: el motor lo recibe como dato inerte, y `pruebas/auditoria_sql.py`
    lo sigue dictaminando CONFORME.
    """
    _comprobar_la_clasificacion()
    if not ESTADOS_QUE_RESUELVEN:
        # Un solo separador, no dos. `,,` contendria el hueco `,` + `` + `,`, y un
        # `estado_recomendacion` vacio colado por SQL crudo casaria dentro: se
        # daria por resuelta una recomendacion de la que nadie ha dicho nada.
        return SEPARADOR_DE_ESTADOS
    return (
        SEPARADOR_DE_ESTADOS
        + SEPARADOR_DE_ESTADOS.join(ESTADOS_QUE_RESUELVEN)
        + SEPARADOR_DE_ESTADOS
    )


def recomendacion_sin_resolver(estado_recomendacion):
    """Dice si esa recomendacion sigue pendiente. `None` cuenta como pendiente.

    Un caso cuyo `estado_recomendacion` esta vacio no es un caso tranquilo: es un
    caso del que nadie ha dicho nada todavia (regla permanente 5).
    """
    return estado_recomendacion not in ESTADOS_QUE_RESUELVEN
