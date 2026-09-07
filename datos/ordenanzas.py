"""Como se llaman las seis casillas de ordenanzas, y como se resumen en una linea.

Las seis columnas `ord_*` de `personas` dicen a QUÉ va cada persona al templo. Los
nombres de columna sirven para el motor y no para una persona: «A qué va:
ord_sellamiento_esposos» no se puede poner en una hoja que lee un compañero ni en
un informe que leen los jefes.

Los rotulos viven aqui, en `datos/`, y no en la pantalla que los ensena, por dos
razones. La primera es que ya hacen falta en tres sitios —la pantalla de la
persona, la hoja del compañero y el informe— y una copia en cada uno acaba
diciendo tres cosas distintas de lo mismo. La segunda es la direccion de las
dependencias: `paquete/` y `reportes/` no pueden importar de `interfaz/`, que es lo
que dibuja ventanas.

⚠️ **Un `None` no es un «no».** Una casilla sin leer y una casilla leida y sin
marcar son cosas distintas (FASE 2, criterio 11b): la primera dice que nadie sabe,
la segunda que esa persona no va a eso. `resumir` solo nombra las marcadas, y dice
con palabras cuando no hay ninguna marcada.
"""

from datos.repositorio import CASILLAS_DE_ORDENANZAS


class ErrorDeOrdenanzas(RuntimeError):
    """Los rotulos y las columnas de ordenanzas dejaron de decir lo mismo."""


# En el orden literal de `DECISIONES.md`, que es el de las columnas `ord_*`.
ETIQUETAS_DE_LAS_ORDENANZAS = {
    "ord_recibir_propias": "Recibir ordenanzas propias",
    "ord_observar_sellamiento": "Observar ordenanza de sellamiento",
    "ord_traductor": "Traductor",
    "ord_investidura": "Investidura",
    "ord_sellamiento_esposos": "Sellamiento esposa a esposo",
    "ord_sellamiento_hijo_padres": "Sellamiento hijo a padres",
}

# Lo que se escribe cuando ninguna casilla de esa persona esta marcada. No se deja
# en blanco: un hueco se lee como «se me olvido rellenarlo», y esto es un dato —
# las casillas son marcas de tilde y el reconocimiento no siempre las lee.
SIN_MARCAR = "sin marcar"


def _exigir_que_los_rotulos_cubran_las_casillas():
    """Levanta si falta el rotulo de alguna casilla o sobra uno que no existe.

    Levanta al importar, como `datos/estados.py`: una casilla sin rotulo saldria
    callada de la hoja del compañero, y una persona que va a un sellamiento
    saldria como que no va a nada.
    """
    faltan = [nombre for nombre in CASILLAS_DE_ORDENANZAS if nombre not in ETIQUETAS_DE_LAS_ORDENANZAS]
    sobran = [nombre for nombre in ETIQUETAS_DE_LAS_ORDENANZAS if nombre not in CASILLAS_DE_ORDENANZAS]
    if faltan or sobran:
        raise ErrorDeOrdenanzas(
            f"Los rótulos de las ordenanzas no cuadran con las casillas: faltan "
            f"{faltan} y sobran {sobran}. Mientras no cuadren, una persona puede "
            "salir como que no va a nada cuando sí va."
        )


_exigir_que_los_rotulos_cubran_las_casillas()


def _esta_marcada(persona, nombre):
    """Si esa casilla consta marcada. Una columna que no venga cuenta como no.

    Se pregunta por `keys()` y no con un `try` porque una fila de SQLite levanta
    `IndexError` con una columna que no pidio la consulta, y eso taparia un fallo
    real —una consulta a la que se le olvido una columna— con un «no va a nada».
    """
    return nombre in persona.keys() and bool(persona[nombre])


def resumir(persona):
    """A que va esa persona, en una linea. `SIN_MARCAR` si no hay ninguna marcada.

    `persona` es cualquier cosa indexable por nombre de columna: una fila de
    SQLite o un diccionario.
    """
    marcadas = [
        ETIQUETAS_DE_LAS_ORDENANZAS[nombre]
        for nombre in CASILLAS_DE_ORDENANZAS
        if _esta_marcada(persona, nombre)
    ]
    return ", ".join(marcadas) if marcadas else SIN_MARCAR


def contar(personas):
    """Cuantas personas van a cada ordenanza. Devuelve `[(rotulo, cuantas)]`.

    Ordenado de mas a menos, y a igualdad por el orden del formulario: dos
    ordenanzas con el mismo numero de personas tienen que salir siempre en el mismo
    sitio, o dos informes del mismo periodo pareceran distintos.
    """
    cuentas = []
    for nombre in CASILLAS_DE_ORDENANZAS:
        cuantas = sum(1 for persona in personas if _esta_marcada(persona, nombre))
        if cuantas:
            cuentas.append((ETIQUETAS_DE_LAS_ORDENANZAS[nombre], cuantas))
    return sorted(cuentas, key=lambda par: -par[1])
