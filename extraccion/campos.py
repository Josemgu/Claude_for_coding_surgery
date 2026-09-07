"""La precedencia de valores, que es lo que esta fase existe para construir.

El problema real, visto en un formulario de referencia: en la fila «Date
traveling to the temple» el papel dice «September 7, 2026», encima hay un tachon
rojo que lo cruza, y al lado hay una correccion escrita que dice «8 Sept 2026».
El OCR lee las dos fechas y no tiene forma de saber cual vale. La capa de
anotaciones si lo sabe.

El orden, tal como lo fija `DECISIONES.md`:

  1. Un tachon que solapa la banda ANULA lo que el OCR leyo ahi.
  2. Una correccion escrita en la misma banda gana, con `origen='anotacion'` y
     confianza 1.0 — no es una lectura, es un dato exacto del PDF.
  3. Si no hay ninguna de las dos, vale el OCR con la confianza que dio.
  4. Si hubo tachon y NO hay correccion, el campo queda VACIO y marcado para
     revision. Nunca se recupera el valor tachado: alguien lo tacho por algo.

El punto 4 es el que hay que defender cuando alguien proponga «aprovechar» el
texto de debajo del tachon. Si se aprovecha, el sistema guarda justo el dato que
una persona marco como equivocado.
"""

from collections import namedtuple

ORIGEN_ANOTACION = "anotacion"
ORIGEN_OCR = "ocr"
ORIGEN_VACIO = "vacio"

# Los cuatro valores de `origen` que admite la columna `procedencia_campo.origen`
# son 'anotacion', 'ocr', 'vacio' y 'manual'. Aqui no se produce 'manual' nunca:
# eso lo escribe la pantalla de correccion, que es la FASE 3.
CONFIANZA_DE_UNA_ANOTACION = 1.0

CampoExtraido = namedtuple(
    "CampoExtraido",
    ("valor", "origen", "confianza", "valor_ocr", "anulado_por_tachon", "necesita_revision"),
)


def campo_vacio(valor_ocr=None, anulado_por_tachon=False):
    """Un campo que no se pudo leer. Se marca para revision, no se rellena."""
    return CampoExtraido(
        valor=None,
        origen=ORIGEN_VACIO,
        confianza=None,
        valor_ocr=valor_ocr,
        anulado_por_tachon=anulado_por_tachon,
        necesita_revision=True,
    )


def _texto_de_las_lineas(lineas):
    """Junta el texto crudo del OCR de una banda, en orden de izquierda a derecha.

    Se guarda entero aunque luego no se use: `procedencia_campo.valor_ocr` existe
    para que Miguel vea QUE leyo la maquina cuando corrija, y un texto recortado
    no le sirve para decidir.
    """
    partes = [linea.texto.strip() for linea in lineas if linea.texto and linea.texto.strip()]
    return " ".join(partes) or None


def _mejor_correccion(correcciones, dar_forma):
    """La correccion que de verdad corrige ESTE campo, o None.

    Una anotacion solo cuenta como correccion del campo si su texto produce un
    valor con la forma que el campo pide. Una que no la produce esta corrigiendo
    otra cosa de la misma fila, no este campo.

    El caso medido que lo obliga: en la fila «Ward/Branch Name and Unit Number»
    de un formulario real hay una anotacion con el NOMBRE de la unidad y nada
    mas. Tomandola como correccion del NUMERO de unidad, el numero —que el OCR
    habia leido perfecto— se perdia y el campo salia vacio.

    Con varias validas gana la de mas a la izquierda: es una regla fija, para no
    dejarlo al azar del orden en que el PDF las guarde.
    """
    validas = [
        correccion
        for correccion in correcciones
        if correccion.texto and correccion.texto.strip() and dar_forma(correccion.texto) is not None
    ]
    if not validas:
        return None
    return min(validas, key=lambda correccion: correccion.rectangulo_pdf[0])


def resolver_campo(lineas_ocr, correcciones, hay_tachon, normalizar=None):
    """Aplica la precedencia y devuelve el campo con su procedencia.

    `normalizar` es la funcion que convierte el texto en el valor con formato
    (una fecha ISO, un MRN, un numero de unidad). Si devuelve None, el campo
    queda vacio y marcado: se leyo algo pero no tenia la forma esperada, y eso
    tambien es trabajo para la pantalla de correccion.

    Pasa de 30 lineas y NO se parte, a proposito: es el arbol de precedencia
    entero, y su valor esta en que las cuatro ramas —correccion, tachon sin
    correccion, OCR, y nada— se leen seguidas y en orden. Repartidas en cuatro
    funciones, comprobar que el orden es el correcto obliga a saltar entre ellas,
    que es justo donde se cuelan los errores de precedencia. Lo que si esta
    fuera, porque son decisiones separables, es que texto cuenta como correccion
    (`_mejor_correccion`) y como se junta el texto del OCR (`_texto_de_las_lineas`).
    """
    dar_forma = normalizar if normalizar is not None else (lambda texto: texto or None)
    valor_ocr = _texto_de_las_lineas(lineas_ocr)

    correccion = _mejor_correccion(correcciones, dar_forma)
    if correccion is not None:
        return CampoExtraido(
            valor=dar_forma(correccion.texto),
            origen=ORIGEN_ANOTACION,
            confianza=CONFIANZA_DE_UNA_ANOTACION,
            valor_ocr=valor_ocr,
            anulado_por_tachon=hay_tachon,
            necesita_revision=False,
        )

    if hay_tachon:
        # Hubo tachon y nadie escribio el valor bueno. El dato de debajo esta
        # marcado como equivocado y NO se usa.
        return campo_vacio(valor_ocr=valor_ocr, anulado_por_tachon=True)

    if not lineas_ocr:
        return campo_vacio()

    valor = dar_forma(valor_ocr)
    if valor is None:
        return campo_vacio(valor_ocr=valor_ocr)

    confianza = min(linea.confianza for linea in lineas_ocr)
    return CampoExtraido(
        valor=valor,
        origen=ORIGEN_OCR,
        confianza=confianza,
        valor_ocr=valor_ocr,
        anulado_por_tachon=False,
        necesita_revision=False,
    )
