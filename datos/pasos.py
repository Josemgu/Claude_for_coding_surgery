"""Los seis pasos de «Preparación para las ordenanzas», y la llamada al líder.

**Qué son, y qué NO son.** Son los seis pasos que salen en la pantalla de esa
persona dentro del sistema del líder, y el compañero los copia uno por uno tal
como los ve. **No son las seis ordenanzas** del formulario (`ord_recibir_propias`
y las otras cinco de `personas`): aquellas dicen a QUÉ va la persona al templo, y
estas dicen si está en condiciones de ir. Son dos preguntas distintas sobre la
misma persona y se guardan por separado a propósito.

Vienen del proyecto viejo (`salida/asignacion.py`, `PASOS`), que es el que el
dueño llama perfecto (`DECISIONES.md`, 2026-09-03). Aquí viven en un solo sitio
—los rótulos del Excel, el orden, las columnas de la base y cómo se lee una
respuesta— porque son lo mismo visto desde cuatro lados: si el rótulo del Excel y
el nombre de la columna pudieran separarse, la hoja que vuelve dejaría de
encontrar su columna y el trabajo del compañero se iría entero a descartados.

⚠️ **Sin contestar NO es «No».** Las tres respuestas son sí, no y nada, y la
tercera es la que importa: una casilla en blanco es una pregunta que nadie miró, y
esa persona queda a medias, no reprobada. Por eso las columnas admiten NULL y por
eso `estado_de_los_pasos` devuelve tres valores y no dos.
"""

import re
import unicodedata

# Cada paso: el nombre de su columna en `personas` y el rótulo con el que sale
# impreso en la hoja del compañero. El orden es el de la pantalla del líder, y no
# se altera: el compañero los copia de arriba abajo sin ir y venir.
PASOS = (
    ("paso_preparacion", "1. Preparación"),
    ("paso_informacion", "2. Información"),
    ("paso_cita_del_templo", "3. Cita del templo"),
    ("paso_acciones_requeridas", "4. Acciones requeridas"),
    ("paso_entrevistas", "5. Entrevistas"),
    ("paso_listo_para_el_templo", "6. Listo para el templo"),
)

NOMBRES_DE_LOS_PASOS = tuple(nombre for nombre, _ in PASOS)

# La séptima pregunta de la hoja. No es un paso —no sale en la pantalla del
# líder— sino lo que hizo el compañero cuando vio que faltaba algo.
COLUMNA_DE_LA_LLAMADA = "llamo_al_lider"
ROTULO_DE_LA_LLAMADA = "¿Llamó al líder?"

# Todas las columnas que el compañero rellena, en el orden en que salen impresas.
COLUMNAS_QUE_RELLENA_EL_COMPANERO = NOMBRES_DE_LOS_PASOS + (COLUMNA_DE_LA_LLAMADA,)

# Lo que ofrece el menú desplegable. Dos opciones y ninguna más: la tercera
# respuesta —no contestar— se da dejando la celda en blanco, y un valor de menú
# que dijera «sin mirar» invitaría a rellenarlo por rellenar.
RESPUESTAS = ("Sí", "No")


class RespuestaIlegible(ValueError):
    """El compañero escribió algo que no es ni sí ni no ni un blanco."""


# Cómo llega escrito «sí» y cómo llega escrito «no» cuando alguien no usa el menú.
# La lista sale del proyecto viejo (`salida/asignacion.py`, `SI` y `NO`), donde se
# fue llenando con lo que los agentes escribían de verdad.
_FORMAS_DEL_SI = frozenset(
    {"si", "si completa", "completa", "s", "x", "true", "1", "listo", "hecho"}
)
_FORMAS_DEL_NO = frozenset(
    {"no", "no esta completa", "incompleta", "n", "false", "0", "falta", "pendiente"}
)


def normalizar(texto):
    """Un texto sin tildes, sin signos y en minúsculas, para poder compararlo.

    Se van los acentos **y** los signos de puntuación. Quien contesta a mano
    escribe «Sí,» o «Si.» según le salga, y las dos quieren decir lo mismo;
    comparar con la coma puesta convertía una respuesta buena en un reparo. Es el
    mismo criterio del proyecto viejo, y allí nació de respuestas reales.
    """
    sin_tildes = unicodedata.normalize("NFKD", str(texto if texto is not None else ""))
    sin_tildes = "".join(letra for letra in sin_tildes if not unicodedata.combining(letra))
    sin_signos = re.sub(r"[^\w\s]", " ", sin_tildes, flags=re.UNICODE)
    return re.sub(r"\s+", " ", sin_signos).strip().lower()


def leer_respuesta(texto):
    """Sí → 1, no → 0, en blanco → None. Cualquier otra cosa levanta.

    Levanta en vez de devolver None porque las dos cosas no significan lo mismo y
    quien llama tiene que poder distinguirlas: un blanco es una pregunta sin
    contestar y se guarda como tal, y un «más o menos» es una respuesta que
    alguien escribió y que **nadie puede interpretar sin inventar**. Esa fila se
    devuelve con su reparo para que la mire una persona (regla permanente 1).
    """
    if texto is None:
        return None
    if isinstance(texto, bool):
        return 1 if texto else 0
    limpio = normalizar(texto)
    if not limpio:
        return None
    if limpio in _FORMAS_DEL_SI:
        return 1
    if limpio in _FORMAS_DEL_NO:
        return 0
    raise RespuestaIlegible(
        f"no se entiende «{texto}»: se esperaba «Sí» o «No», o la celda en blanco "
        "si todavía no se miró"
    )


def estado_de_los_pasos(respuestas):
    """Si esa persona está lista: True, False, o None si todavía no se sabe.

    Lista es tener los seis. Si alguno está marcado que no, no lo está. Y si
    alguno se quedó en blanco **no se sabe**, que es distinto de las otras dos:
    dar por lista a una persona de la que faltan preguntas por mirar es
    exactamente lo que manda a alguien al templo con la recomendación mal.

    `respuestas` es un diccionario de nombre de columna a 1/0/None; una columna
    que no venga cuenta como sin contestar.
    """
    valores = [respuestas.get(nombre) for nombre in NOMBRES_DE_LOS_PASOS]
    if any(valor == 0 for valor in valores):
        return False
    if all(valor == 1 for valor in valores):
        return True
    return None


def pasos_sin_completar(respuestas):
    """Los rótulos de los pasos marcados que NO, sin el número de delante.

    Es lo que hay que decirle al líder cuando se le llama: el nombre del paso
    donde se quedó vale más que cualquier nota escrita a mano.
    """
    return tuple(
        rotulo.split(". ", 1)[-1]
        for nombre, rotulo in PASOS
        if respuestas.get(nombre) == 0
    )
