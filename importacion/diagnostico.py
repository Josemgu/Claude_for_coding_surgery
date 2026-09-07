"""Que le pasa a una pagina que no salio bien, dicho con lo que de verdad se sabe.

Este modulo contesta una sola pregunta —«¿esta pagina hay que anotarla como no
legible, y por que?»— y no toca ni la base ni la pantalla. Esta aparte de
`importacion/guardado.py` porque son dos decisiones distintas: una es que se
guarda, y otra es que se le cuenta a Miguel de lo que no se pudo leer.

**Por que el motivo se decide con lo que la pagina PRODUJO y no con una excepcion.**
La lectura de una pagina casi nunca falla con un error: rasteriza, pasa el OCR y
devuelve un formulario con los campos vacios. Desde fuera, «el PDF venia en
blanco», «el escaneo esta del reves» y «el formulario es de otro modelo» son
exactamente el mismo resultado, y hasta hoy los tres producian el mismo mensaje —
«no se pudo leer el número de caso»— que no dice cual de los tres es. Aqui se
separan con los dos datos que la extraccion ya trae y que hasta ahora se tiraban:
**cuantas lineas leyo el OCR** y **que leyo**.

El orden de las preguntas no es indiferente y va de fuera hacia dentro:

  1. ¿Salio ALGUN dato de la pagina? Si no salio ninguno, se mira cuantas lineas
     leyo el OCR para separar las dos causas: cero lineas es el ESCANEO, muchas
     lineas es el papel o las reglas de lectura.
  2. ¿Salio el numero de caso? Si no, la pagina se guarda igual y solo falta eso.

Preguntar al reves —empezar por el numero de caso— es lo que hacia el programa
antes, y por eso una pagina en blanco y una pagina llena de datos sin numero
salian con la misma frase.
"""

from collections import namedtuple

from datos.ilegibles import (
    ANCLAS_PERDIDAS,
    SIN_CAMPOS,
    SIN_NUMERO_DE_CASO,
    SIN_TEXTO,
)

AnotacionDeIlegible = namedtuple(
    "AnotacionDeIlegible", ("motivo", "detalle", "lineas_leidas")
)

# Los campos del caso que cuentan como «de esta pagina salio algo». `unidad_nombre`
# entra igual que los otros tres: es un dato del papel que Miguel no tendra que
# teclear, y una pagina de la que solo se saco el nombre de la unidad no es una
# pagina de la que no se saco nada.
CAMPOS_QUE_CUENTAN = ("numero_caso", "fecha_viaje", "unidad_numero", "unidad_nombre")


def hay_algun_dato(formulario):
    """Cierto si de esta pagina salio aunque sea un campo o una persona."""
    if formulario.personas:
        return True
    return any(getattr(formulario, campo).valor is not None for campo in CAMPOS_QUE_CUENTAN)


def _detalle_de_lo_leido(formulario):
    """Lo que el lector leyo, y por que no se uso, en una frase para el renglon.

    Lleva SIEMPRE el texto crudo cuando lo hay. Es la unica pista que queda de por
    que fallo una pagina que ya no esta delante, y sin ella el renglon de la lista
    dice «no se pudo» y no se puede hacer nada con eso.
    """
    partes = [f"El lector leyó {formulario.lineas_leidas} línea(s) en esta página."]
    if formulario.captura_manual:
        partes.append(
            "La página se marcó ILEGIBLE por la regla de confianza baja, así que "
            "sus campos se vaciaron a propósito y no se dio por bueno nada de lo "
            "leído."
        )
    if formulario.anclas_no_encontradas:
        partes.append(
            "No se encontraron en el papel estas etiquetas del formulario: "
            + ", ".join(formulario.anclas_no_encontradas)
            + "."
        )
    if formulario.texto_leido:
        partes.append(f"Esto es lo que leyó, tal cual: «{formulario.texto_leido}»")
    return " ".join(partes)


def diagnosticar_la_pagina(formulario):
    """La anotacion de ilegible que le toca a esta pagina, o None si no le toca.

    Devolver None es un resultado normal y el mas frecuente: una pagina de la que
    salio el numero de caso no tiene nada que anotar.
    """
    if not hay_algun_dato(formulario):
        # `sin_texto` se decide con las lineas del OCR **y** con que no haya salido
        # ningun dato, no solo con las lineas. Un PDF que trae los datos en su capa
        # de anotaciones puede dar cero lineas de OCR y aun asi entregar el
        # formulario entero: llamarlo «no tiene ni una letra» seria falso.
        motivo = SIN_TEXTO if not formulario.lineas_leidas else SIN_CAMPOS
        return AnotacionDeIlegible(
            motivo, _detalle_de_lo_leido(formulario), formulario.lineas_leidas
        )
    if formulario.numero_caso.valor is None:
        return AnotacionDeIlegible(
            SIN_NUMERO_DE_CASO, _detalle_de_lo_leido(formulario), formulario.lineas_leidas
        )
    return None


# Lo que se le dice a Miguel de una pagina que perdio etiquetas, en el aviso del
# resumen. Va aparte de la frase larga de `datos/ilegibles.py` porque son dos
# sitios distintos: aqui cabe una linea, alli un parrafo.
AVISO_DE_ANCLAS_PERDIDAS = (
    "El lector no encontró {cuantas} etiqueta{plural} impresa{plural} en esta "
    "página ({cuales}). La página entró y sus datos se guardaron, pero sin esas "
    "etiquetas no sabe dónde mirar: compruébela contra el PDF antes de darla por "
    "buena. Queda anotada en la lista de lo que no salió bien."
)


def anotaciones_de_lo_dudoso(formulario):
    """Lo que hay que dejar dicho de una pagina que SI entro. Devuelve una tupla.

    Es la mitad que `diagnosticar_la_pagina` no cubre y no puede cubrir: aquella
    contesta «¿esta pagina es legible?» y solo tiene una respuesta, mientras que
    esta contesta «¿de esta pagina que entro hay algo que mirar?», que admite
    varias a la vez y ninguna.

    **El unico caso de hoy: se perdieron anclas.** Un ancla es la etiqueta impresa
    que localiza un campo en la hoja; sin ella el extractor no sabe donde recortar,
    y lo que devuelva para ese campo puede venir de otra parte del papel.

    ⚠️ **Por que esto y no bajar el umbral de confianza.** QA midio la pagina 6 de
    un grupo real: 2 anclas perdidas, unidad ilegible y un nombre que no parece un
    nombre con confianza 0.796, y ni `captura_manual` ni `documentos_ilegibles` la
    marcaron, porque 0.796 pasa el umbral de 0.6. Mover ese umbral cambiaria el
    comportamiento de TODAS las paginas para arreglar dos, y ademas sale de
    `DECISIONES.md`, no de aqui.

    La puerta que si se puede cerrar esta **medida** sobre las 14 paginas reales de
    `pdfs_referencia/` (2026-09-03): 10 pierden CERO anclas —y de las 10 salieron
    numero de caso, fecha y unidad—, y las 4 que pierden alguna (2 o 4) son
    exactamente las 4 de las que no salio ni fecha ni unidad. Ni un falso positivo
    ni uno negativo sobre el material que hay. Con eso, «se perdio alguna etiqueta»
    es una senal util y no hace falta ningun numero nuevo que nadie haya calibrado.
    """
    if not formulario.anclas_no_encontradas:
        return ()
    return (
        AnotacionDeIlegible(
            ANCLAS_PERDIDAS,
            _detalle_de_lo_leido(formulario),
            formulario.lineas_leidas,
        ),
    )


def aviso_de_anclas_perdidas(formulario):
    """La linea del resumen, o ninguna si esta pagina no perdio ninguna etiqueta."""
    perdidas = formulario.anclas_no_encontradas
    if not perdidas:
        return ()
    plural = "s" if len(perdidas) != 1 else ""
    return (
        AVISO_DE_ANCLAS_PERDIDAS.format(
            cuantas=len(perdidas), plural=plural, cuales=", ".join(perdidas)
        ),
    )
