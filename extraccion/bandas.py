"""Localizar una banda del formulario por su ANCLA de texto, nunca por coordenada.

Un escaneo nunca cae dos veces en el mismo sitio: los margenes del escaner mueven
todo unos milimetros, y una coordenada fija acaba capturando el encabezado en vez
del dato. Asi que cada campo se encuentra por la etiqueta impresa que lo nombra
—«Date traveling to the temple»— y el valor se busca en la fila de debajo.

El OCR se equivoca al leer esas etiquetas, y esta medido: sobre los cuatro
documentos de referencia salieron «Ward/Branch Name and Upjt Number» y «Date
traveling home fror the temple». Por eso el ancla se busca por PARECIDO y no por
igualdad. Y por eso hay una segunda regla, que es la que de verdad protege:

    un ancla solo vale si se parece a SU etiqueta mas que a cualquier otra
    etiqueta conocida del formulario.

Sin esa regla, «Date traveling home fror the temple» se parecia un 0.77 a «Date
traveling to the temple» y el sistema habria leido la fecha de vuelta como fecha
de ida. Es el error que manda a alguien al templo el dia equivocado.
"""

from extraccion.etiquetas import normalizar_para_comparar
from extraccion.geometria import (
    Rectangulo,
    fraccion_de_traslape_vertical,
    se_solapan_en_horizontal,
)

# Parecido minimo para aceptar un ancla. Medido sobre las 9 paginas reales: las
# anclas correctas dieron entre 0.94 y 1.00, y la unica confusion peligrosa dio
# 0.77. El corte en 0.85 las separa con holgura por los dos lados.
PARECIDO_MINIMO_DEL_ANCLA = 0.85

# Alto de la fila de valor, en multiplos del alto del ancla. Medido: con 0.8 los
# cinco tachones rojos que caen sobre una banda de interes la anulan con
# fracciones de 0.52 a 0.72, y todos los valores del OCR siguen dentro con 0.82 a
# 1.00. Con 1.2 el tachon de la fecha baja a 0.43 y DEJA DE ANULAR: el sistema se
# quedaria con la fecha tachada. El numero no se eligio a ojo.
FACTOR_DE_ALTO_DE_LA_BANDA = 0.8

# Cuanto se ensancha la banda a la derecha del ancla, en multiplos de su alto. El
# valor suele ser mas ancho que su etiqueta y a veces empieza a su izquierda.
FACTOR_DE_ANCHO_A_LA_DERECHA = 6.0
FACTOR_DE_ANCHO_A_LA_IZQUIERDA = 0.5


def distancia_de_edicion(cadena_a, cadena_b):
    """Cuantas ediciones de un caracter separan dos cadenas."""
    fila_previa = list(range(len(cadena_b) + 1))
    for indice_a, caracter_a in enumerate(cadena_a, start=1):
        fila = [indice_a]
        for indice_b, caracter_b in enumerate(cadena_b, start=1):
            coste = 0 if caracter_a == caracter_b else 1
            fila.append(
                min(fila_previa[indice_b] + 1, fila[indice_b - 1] + 1, fila_previa[indice_b - 1] + coste)
            )
        fila_previa = fila
    return fila_previa[-1]


def parecido(cadena_a, cadena_b):
    """Entre 0.0 y 1.0. No distingue mayusculas ni tildes: el OCR las confunde.

    Las tildes se ignoran SOLO aqui, para comparar. Lo que se guarda conserva las
    suyas: `normalizar_para_comparar` lleva escrito el motivo y los numeros.
    """
    izquierda = normalizar_para_comparar(cadena_a)
    derecha = normalizar_para_comparar(cadena_b)
    if not izquierda and not derecha:
        return 1.0
    if not izquierda or not derecha:
        return 0.0
    return 1.0 - distancia_de_edicion(izquierda, derecha) / max(len(izquierda), len(derecha))


def _formas_de_una_etiqueta(etiquetas):
    """Las formas aceptadas de UNA etiqueta, venga una sola o una familia.

    Una cadena suelta es una familia de una. Existe porque el mismo rotulo llega
    en dos idiomas y las dos formas valen igual, pero hay codigo y pruebas que
    siguen pasando una sola cadena y tienen que seguir funcionando.
    """
    return (etiquetas,) if isinstance(etiquetas, str) else tuple(etiquetas)


def localizar_ancla(lineas, etiquetas, etiquetas_rivales=(), parecido_minimo=PARECIDO_MINIMO_DEL_ANCLA):
    """La linea del OCR que es la etiqueta buscada, o None si no hay ninguna clara.

    `etiquetas` son las formas que valen para ESTE campo —la inglesa y la
    espanola del mismo rotulo—, y gana la que mas se parezca. `etiquetas_rivales`
    son las de los demas campos, cada una tambien con sus dos formas. Una linea
    que se parezca mas a una rival que a la pedida se descarta aunque supere el
    minimo: es la otra etiqueta, mal leida.

    Mezclar los dos idiomas en la misma familia es seguro y esta medido: el cruce
    maximo entre una etiqueta inglesa y una espanola es 0.357 (ver
    `etiquetas.py`), asi que la forma que gana siempre es la del idioma en que
    esta impresa la pagina.
    """
    propias = _formas_de_una_etiqueta(etiquetas)
    rivales = tuple(
        forma for rival in etiquetas_rivales for forma in _formas_de_una_etiqueta(rival)
    )

    mejor_linea = None
    mejor_parecido = 0.0
    for linea in lineas:
        parecido_actual = max(parecido(linea.texto, forma) for forma in propias)
        if parecido_actual > mejor_parecido:
            mejor_linea, mejor_parecido = linea, parecido_actual

    if mejor_linea is None or mejor_parecido < parecido_minimo:
        return None
    if any(parecido(mejor_linea.texto, rival) > mejor_parecido for rival in rivales):
        return None
    return mejor_linea


def banda_de_valor(ancla_rectangulo):
    """La fila donde vive el valor: justo debajo del ancla, y de su mismo alto."""
    alto_del_ancla = ancla_rectangulo.y1 - ancla_rectangulo.y0
    return Rectangulo(
        x0=ancla_rectangulo.x0 - alto_del_ancla * FACTOR_DE_ANCHO_A_LA_IZQUIERDA,
        y0=ancla_rectangulo.y1,
        x1=ancla_rectangulo.x1 + alto_del_ancla * FACTOR_DE_ANCHO_A_LA_DERECHA,
        y1=ancla_rectangulo.y1 + alto_del_ancla * FACTOR_DE_ALTO_DE_LA_BANDA,
    )


def esta_en_la_banda(rectangulo, banda, fraccion_minima=0.5):
    """Cierto cuando el rectangulo cae en la banda por vertical y por horizontal.

    La comprobacion horizontal no es un adorno: el formulario tiene dos columnas,
    y sin ella un tachon de la columna derecha anularia el campo de la izquierda
    solo por estar a su misma altura.
    """
    return fraccion_de_traslape_vertical(rectangulo, banda) > fraccion_minima and (
        se_solapan_en_horizontal(rectangulo, banda)
    )


def lineas_en_la_banda(lineas, banda):
    """Las lineas del OCR que pertenecen a la banda, de izquierda a derecha."""
    dentro = [linea for linea in lineas if esta_en_la_banda(linea.rectangulo, banda)]
    return sorted(dentro, key=lambda linea: linea.rectangulo.x0)
