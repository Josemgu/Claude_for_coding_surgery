"""La capa de anotaciones del PDF: texto que se lee sin OCR, y trazos a mano.

Los formularios llegan escaneados —la hoja es una imagen sin texto— pero encima
llevan anotaciones que SI son objetos reales del PDF. Ahi esta lo que el revisor
corrigio, y se lee exacto, sin pasar por el OCR y sin margen de error.

Dos subtipos importan:

  - `/FreeText`: lleva su texto en `/Contents`. Es una correccion escrita.
  - `/Ink`: un trazo a mano, sin texto, pero con color y grosor. El color y el
    grosor son lo unico que distingue un tachon de un resaltador.

Un `/Ink` que no encaje en ninguna de las dos familias conocidas se registra como
DESCONOCIDO y no se interpreta. No se adivina que quiso decir una marca: si el
sistema tratara un trazo raro como tachon, anularia un campo bueno; si lo tratara
como resaltador, dejaria pasar un dato tachado.
"""

from collections import namedtuple

# ---------------------------------------------------------------------------
# Las dos familias de trazo, MEDIDAS sobre los cuatro documentos de referencia
# el 2026-09-02 con `pypdf`, no citadas de ningun plan.
#
#   familia            /C                            /BS /W   cuantos
#   ---------------    ---------------------------   ------   -------
#   tachon rojo        (0.8902, 0.0941, 0.1765)      1.65     45
#   resaltador verde   (0.4941, 0.7686, 0.0)         16.5      1
#
#   Total /Ink en el corpus: 46. Familias distintas: 2. Sin clasificar: 0.
#
# `DECISIONES.md` suponia (0.890, 0.094, 0.176)/1.65 y (0.494, 0.765, 0)/16.5.
# El rojo coincide con el redondeo del documento. El verde NO: el documento dice
# 0.765 en el canal verde y lo medido es 0.7686. La tolerancia de abajo cubre esa
# diferencia de sobra; el numero que manda es el medido.
# ---------------------------------------------------------------------------
COLOR_DEL_TACHON = (0.8902, 0.0941, 0.1765)
GROSOR_DEL_TACHON = 1.65

COLOR_DEL_RESALTADOR = (0.4941, 0.7686, 0.0)
GROSOR_DEL_RESALTADOR = 16.5

# Cuanto puede alejarse un canal de color del valor medido y seguir siendo la
# misma familia. 0.02 sobre 1.0 son unos 5 valores de 255: mas que suficiente
# para el redondeo de un visor, y muy lejos de confundir el rojo con el verde,
# que se separan en 0.40 en el canal rojo y 0.67 en el verde.
TOLERANCIA_DE_COLOR = 0.02

# El grosor se compara en relativo porque los dos valores conocidos se separan en
# un factor de 10 (1.65 frente a 16.5). Un 20% sobre 1.65 es 0.33, y sobre 16.5
# es 3.3: ninguna de las dos ventanas alcanza a la otra.
TOLERANCIA_RELATIVA_DE_GROSOR = 0.20

CLASE_TACHON = "tachon"
CLASE_RESALTADOR = "resaltador"
CLASE_DESCONOCIDA = "desconocido"

TIPO_TEXTO = "texto"
TIPO_TRAZO = "trazo"

Anotacion = namedtuple(
    "Anotacion",
    ("tipo", "clase", "texto", "rectangulo_pdf", "color", "grosor"),
)


def _color_se_parece(color, color_de_referencia):
    """Cierto cuando los tres canales caen dentro de la tolerancia."""
    if color is None or len(color) != len(color_de_referencia):
        return False
    return all(
        abs(canal - referencia) <= TOLERANCIA_DE_COLOR
        for canal, referencia in zip(color, color_de_referencia)
    )


def _grosor_se_parece(grosor, grosor_de_referencia):
    """Cierto cuando el grosor cae dentro del porcentaje admitido."""
    if grosor is None:
        return False
    return abs(grosor - grosor_de_referencia) <= grosor_de_referencia * TOLERANCIA_RELATIVA_DE_GROSOR


def clasificar_trazo(color, grosor):
    """Devuelve `tachon`, `resaltador` o `desconocido`. Nunca adivina.

    Exige que coincidan las DOS cosas, color y grosor. Con solo el color, un
    trazo rojo grueso hecho para resaltar se leeria como tachon y anularia un
    campo que estaba bien.
    """
    if _color_se_parece(color, COLOR_DEL_TACHON) and _grosor_se_parece(grosor, GROSOR_DEL_TACHON):
        return CLASE_TACHON
    if _color_se_parece(color, COLOR_DEL_RESALTADOR) and _grosor_se_parece(
        grosor, GROSOR_DEL_RESALTADOR
    ):
        return CLASE_RESALTADOR
    return CLASE_DESCONOCIDA


def _numeros(valor):
    """Convierte una lista de objetos PDF a tupla de float, o None si no hay."""
    if not valor:
        return None
    return tuple(float(elemento) for elemento in valor)


def _grosor_del_borde(anotacion_pdf):
    """El `/BS /W` de la anotacion, o None si no declara estilo de borde."""
    estilo = anotacion_pdf.get("/BS")
    if estilo is None:
        return None
    ancho = estilo.get_object().get("/W")
    return None if ancho is None else float(ancho)


def _anotacion_de_texto(anotacion_pdf, rectangulo_pdf):
    """Una correccion escrita. El texto va tal cual: no se limpia ni se corrige."""
    contenido = anotacion_pdf.get("/Contents")
    return Anotacion(
        tipo=TIPO_TEXTO,
        clase=None,
        texto=None if contenido is None else str(contenido),
        rectangulo_pdf=rectangulo_pdf,
        color=None,
        grosor=None,
    )


def _anotacion_de_trazo(anotacion_pdf, rectangulo_pdf):
    """Un trazo a mano, clasificado por su color y su grosor."""
    color = _numeros(anotacion_pdf.get("/C"))
    grosor = _grosor_del_borde(anotacion_pdf)
    return Anotacion(
        tipo=TIPO_TRAZO,
        clase=clasificar_trazo(color, grosor),
        texto=None,
        rectangulo_pdf=rectangulo_pdf,
        color=color,
        grosor=grosor,
    )


def leer_anotaciones(pagina_pypdf):
    """Todas las `/FreeText` y `/Ink` de una pagina, en el orden del PDF.

    Los demas subtipos se ignoran en silencio a proposito: un `/Link` o un
    `/Popup` no dicen nada del formulario. Lo que NO se ignora es un `/Ink` que
    no se sepa clasificar; ese vuelve con clase `desconocido`.
    """
    referencias = pagina_pypdf.get("/Annots") or []
    anotaciones = []
    for referencia in referencias:
        anotacion_pdf = referencia.get_object()
        subtipo = str(anotacion_pdf.get("/Subtype"))
        rectangulo_pdf = _numeros(anotacion_pdf.get("/Rect"))
        if rectangulo_pdf is None:
            continue
        if subtipo == "/FreeText":
            anotaciones.append(_anotacion_de_texto(anotacion_pdf, rectangulo_pdf))
        elif subtipo == "/Ink":
            anotaciones.append(_anotacion_de_trazo(anotacion_pdf, rectangulo_pdf))
    return anotaciones


def contar_por_clase(anotaciones):
    """Cuantas hay de cada cosa. Es el resumen que se anota al procesar un PDF."""
    resumen = {
        TIPO_TEXTO: 0,
        CLASE_TACHON: 0,
        CLASE_RESALTADOR: 0,
        CLASE_DESCONOCIDA: 0,
    }
    for anotacion in anotaciones:
        clave = TIPO_TEXTO if anotacion.tipo == TIPO_TEXTO else anotacion.clase
        resumen[clave] += 1
    return resumen
