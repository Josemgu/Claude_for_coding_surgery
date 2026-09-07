"""El vocabulario con el que se describe un reporte: columnas, secciones y cifras.

Aquí no se cuenta nada y no se consulta nada. Son las formas que usan los dos
módulos que arman secciones —`documento.py` y `secciones_de_direccion.py`— y los
dos que las dibujan —`excel.py` y `pdf.py`—. Vive aparte para que los dos que arman
puedan compartirlas sin importarse entre ellos, que sería un círculo.

**Las tres clases de columna.** `TEXTO` no es cosmética: es la única defensa contra
que Excel se coma el cero de delante de un MRN como '055-1111-3853', y un MRN
sin sus ceros deja de identificar a nadie. Misma razón que en `espejo/hojas.py`,
escrita aparte porque son dos documentos distintos con dos propósitos distintos.

⚠️ **Una cifra lleva un TONO, no un color.** `TONO_MALO` no es «rojo»: es «esto
salió mal», y cada formato decide cómo se ve. El PDF lo pinta de rojo; el `.xlsx`
sale **sin colores a propósito**, porque alguien lo va a imprimir en blanco y negro
y un color no es un dato. Si aquí pusiera «#D0342C», el Excel tendría que saber qué
hacer con un código hexadecimal que no puede usar.
"""

from collections import namedtuple

TEXTO = "texto"
TEMPORAL = "temporal"
CRUDO = "crudo"

TONO_MALO = "malo"
TONO_BUENO = "bueno"
TONO_NEUTRO = "neutro"

Columna = namedtuple("Columna", ("nombre", "clase", "ancho"))
Seccion = namedtuple("Seccion", ("titulo", "notas", "columnas", "filas", "resumen"))
Cifra = namedtuple("Cifra", ("numero", "rotulo", "tono"))
Portada = namedtuple("Portada", ("titular", "frase", "cifras"))
Documento = namedtuple(
    "Documento", ("titulo", "subtitulo", "generado_en", "portada", "avisos", "secciones")
)

TITULO = "Fichas — Reporte de recomendaciones al templo"

# Con lo que abre el informe. Es la pregunta que se le hace al programa, escrita
# como pregunta y no como categoría: «Resumen» no dice nada, y esto sí.
TITULAR_DE_LA_PORTADA = "Cuántas personas viajaron sin estar listas"

# Lo que se escribe donde la base no tiene el dato. La misma palabra que usa
# `paquete/columnas.py`: dos maneras de decir «no lo sé» en el mismo programa se
# leen como dos cosas distintas.
SIN_DATO = "no consta"
NO_SE_PUEDE_SABER = "no se puede saber"

# Como se llama un caso que no lleva nadie. Vive aqui —y no en cada seccion— porque
# lo escriben DOS modulos distintos: `preparacion.py` en la columna «Asignado a» de
# «Los viajes», y `secciones_de_direccion.py` en la fila de «El equipo». Si cada uno
# escribiera la suya, un informe podria decir «sin asignar» en una tabla y «sin
# agente» en la otra, y quien lo lee no sabria si son dos cosas.
SIN_AGENTE = "sin asignar"

# Y como se llama una unidad de la que no consta el nombre, por lo mismo: la
# escriben la tabla de unidades y la de quienes viajaron sin la preparacion.
SIN_UNIDAD = "sin unidad"


def unidad_con_su_numero(fila):
    """El nombre de la unidad con su número, o lo que haya de los dos."""
    nombre = fila.get("unidad_nombre") or "unidad sin nombre"
    return f"{nombre} · {fila.get('unidad_numero') or 'sin número'}"


def persona_o_sin_nombre(fila):
    """El nombre de la persona, o la palabra que dice que no se leyó.

    No se deja en blanco: un hueco en un informe a la dirección se lee como un dato
    que se perdió por el camino, y lo que pasa es que el reconocimiento no leyó ese
    nombre y nadie lo ha corregido todavía.
    """
    return fila.get("nombre") or "nombre sin leer"
