"""Los dos sistemas de coordenadas, y el criterio de traslape del 50%.

Un PDF mide en puntos con el origen ABAJO a la izquierda. Una imagen rasterizada
mide en pixeles con el origen ARRIBA a la izquierda. Todo el trabajo de este
paquete consiste en cruzar anotaciones del PDF con texto leido de la imagen, asi
que las dos cosas tienen que hablar el mismo idioma antes de compararse.

Aqui se convierte a pixeles de imagen, y se convierte una sola vez.
"""

import math
from collections import namedtuple

# Rectangulo en pixeles de imagen: origen arriba a la izquierda, `y0` es el borde
# de arriba y `y1` el de abajo. Siempre `y0 <= y1` y `x0 <= x1`.
Rectangulo = namedtuple("Rectangulo", ("x0", "y0", "x1", "y1"))

# Regla de no regresion (`DECISIONES.md`): sin tope, un escaneo grande a 300 DPI
# tardaba entre 5 y 10 minutos por pagina.
LADO_LARGO_MAXIMO_PX = 3500


def escala_de_rasterizado(ancho_puntos, alto_puntos, lado_largo_maximo_px=LADO_LARGO_MAXIMO_PX):
    """La escala que deja el lado largo justo EN el tope, nunca uno por encima.

    El `math.nextafter` no es un adorno y no se puede simplificar a una division
    (`DECISIONES.md`, FASE 0): `3500/792` vale 3500.0000000000005 en coma
    flotante, pypdfium2 redondea el tamano del mapa de bits, y la pagina sale a
    3501 px. Bajar la escala al float inmediatamente anterior la deja en 3500.
    `pruebas/prueba_geometria.py` fija los dos numeros.
    """
    lado_largo_puntos = max(ancho_puntos, alto_puntos)
    return math.nextafter(lado_largo_maximo_px / lado_largo_puntos, 0.0)


def rectangulo_pdf_a_pixeles(rectangulo_pdf, alto_pagina_puntos, escala):
    """Pasa un `/Rect` de anotacion a pixeles de la imagen rasterizada.

    `rectangulo_pdf` llega como `(x1, y1, x2, y2)` en puntos, con `y` creciendo
    hacia ARRIBA. La `y` se invierte restandola del alto de la pagina; la `x`
    solo se escala, porque comparte origen en los dos sistemas.

    Omitir la inversion deja los rectangulos espejados respecto al centro de la
    pagina y el sistema anula campos que estaban buenos. Por eso la conversion
    esta aislada en esta funcion y tiene prueba propia.
    """
    x_izquierda_pdf, y_abajo_pdf, x_derecha_pdf, y_arriba_pdf = (
        float(valor) for valor in rectangulo_pdf
    )
    return Rectangulo(
        x0=min(x_izquierda_pdf, x_derecha_pdf) * escala,
        y0=(alto_pagina_puntos - max(y_abajo_pdf, y_arriba_pdf)) * escala,
        x1=max(x_izquierda_pdf, x_derecha_pdf) * escala,
        y1=(alto_pagina_puntos - min(y_abajo_pdf, y_arriba_pdf)) * escala,
    )


def rectangulo_desde_puntos(puntos):
    """La caja que envuelve una lista de puntos `(x, y)` en pixeles de imagen.

    Es lo que devuelve el OCR para cada linea: cuatro esquinas, no un rectangulo.
    """
    equis = [float(punto[0]) for punto in puntos]
    yes = [float(punto[1]) for punto in puntos]
    return Rectangulo(x0=min(equis), y0=min(yes), x1=max(equis), y1=max(yes))


def traslape_vertical(rectangulo, banda):
    """Los pixeles de alto que los dos rectangulos comparten. Nunca negativo."""
    return max(0.0, min(rectangulo.y1, banda.y1) - max(rectangulo.y0, banda.y0))


def fraccion_de_traslape_vertical(rectangulo, banda):
    """Que parte del ALTO DE LA BANDA cubre el rectangulo, entre 0.0 y 1.0.

    El denominador es la altura de la banda y no la del rectangulo: asi lo fija
    `DECISIONES.md`. Una banda de alto cero devuelve 0.0 en vez de reventar.
    """
    alto_de_la_banda = banda.y1 - banda.y0
    if alto_de_la_banda <= 0.0:
        return 0.0
    return traslape_vertical(rectangulo, banda) / alto_de_la_banda


def pertenece_a_la_banda(rectangulo, banda, fraccion_minima=0.5):
    """Cierto cuando el traslape SUPERA la fraccion pedida.

    La comparacion es estricta (`>`), no `>=`: `DECISIONES.md` dice «supera el
    50%», y con el empate exacto la banda no se anula. `prueba_geometria.py`
    prueba los dos lados del borde y el empate.
    """
    return fraccion_de_traslape_vertical(rectangulo, banda) > fraccion_minima


def se_solapan_en_horizontal(rectangulo, banda):
    """Cierto cuando los dos comparten al menos un pixel de ancho.

    Sirve para no dejar que un tachon de la columna derecha anule un campo de la
    columna izquierda: el formulario tiene dos columnas y una banda no ocupa la
    pagina entera.
    """
    return min(rectangulo.x1, banda.x1) > max(rectangulo.x0, banda.x0)
