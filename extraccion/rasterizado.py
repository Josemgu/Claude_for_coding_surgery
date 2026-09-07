"""Convertir una pagina de PDF en la imagen que el OCR va a leer.

Dos reglas de no regresion viven aqui, y las dos son de rendimiento medido:

  - `cv2.medianBlur(gris, 3)` y SOLO eso. `fastNlMeansDenoising` se comia 56 de
    los 65 segundos de una pagina con la misma precision.
  - Tope de 3500 px en el lado largo. Sin tope, un escaneo a 300 DPI tardaba
    entre 5 y 10 minutos por pagina.

De OpenCV este proyecto usa unicamente `cvtColor` y `medianBlur`.
"""

from collections import namedtuple

PaginaRasterizada = namedtuple(
    "PaginaRasterizada", ("imagen", "escala", "ancho_puntos", "alto_puntos")
)


def rasterizar_pagina(ruta_pdf, indice_de_pagina):
    """Devuelve la pagina en escala de grises, filtrada, y la escala que se uso.

    La escala hace falta despues para cruzar las anotaciones —que estan en puntos
    PDF— con lo que el OCR encuentre en pixeles. Se devuelve junto a la imagen
    para que nadie tenga que recalcularla y equivocarse.
    """
    import cv2
    import numpy
    import pypdfium2

    from extraccion.geometria import escala_de_rasterizado

    documento = pypdfium2.PdfDocument(str(ruta_pdf))
    try:
        pagina = documento[indice_de_pagina]
        ancho_puntos, alto_puntos = pagina.get_size()
        escala = escala_de_rasterizado(ancho_puntos, alto_puntos)
        imagen_en_color = pagina.render(scale=escala).to_numpy()
    finally:
        documento.close()

    gris = cv2.cvtColor(imagen_en_color, cv2.COLOR_RGB2GRAY)
    # `ascontiguousarray` no es un adorno: el recorte que devuelve OpenCV puede
    # no serlo, y onnxruntime falla con matrices no contiguas.
    filtrada = numpy.ascontiguousarray(cv2.medianBlur(gris, 3))
    return PaginaRasterizada(
        imagen=filtrada,
        escala=escala,
        ancho_puntos=float(ancho_puntos),
        alto_puntos=float(alto_puntos),
    )


def contar_paginas(ruta_pdf):
    """Cuantas paginas trae el PDF.

    Un PDF puede traer VARIOS formularios, uno por pagina: medido sobre un
    documento real de seis. El nombre del archivo no identifica un caso.
    """
    import pypdfium2

    documento = pypdfium2.PdfDocument(str(ruta_pdf))
    try:
        return len(documento)
    finally:
        documento.close()
