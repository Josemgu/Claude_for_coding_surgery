"""Como se construye el libro en memoria: valores, formatos y nada mas.

Este modulo no toca el disco. Construye el `Workbook` y lo devuelve; escribirlo sin
romper el que ya estaba es cosa de `espejo.escritura`.

Tres cosas que hace, y las tres estan medidas en `pruebas/prueba_espejo.py`:

  1. **`mrn` y `unidad_numero` con formato de texto.** `openpyxl.styles.numbers`
     define `FORMAT_TEXT = '@'` —comprobado en openpyxl 3.1.5, que es la version
     de `requirements.txt`—. Sin ese formato Excel lee `055-1111-3853` como algo
     que puede normalizar y el cero de delante desaparece. Y el MRN sin sus
     ceros deja de servir para reconciliar (FASE 6), que es todo su trabajo.
  2. **Las fechas entran como `date` o `datetime`, no como cadena.** Una cadena se
     ordena alfabeticamente; con `AAAA-MM-DD` eso parece funcionar, y deja de
     funcionar en cuanto una fila trae otro formato. Lo que hace falta —filtrar y
     ordenar por mes— necesita que Excel sepa que aquello es una fecha.
  3. **Lo que no se puede leer como fecha se deja tal cual.** No se adivina, no se
     rellena y no se descarta: se muestra la cadena que hay en la base. La regla
     permanente 1 prohibe inventar un dato, y una fecha inventada en un espejo es
     peor que una fecha fea.

Sin celdas combinadas, sin relleno de color y sin negrita: no se escribe ni una
sola instruccion de estilo aparte del formato de numero. Es una base de datos, no
un reporte, y un color no es un dato. La fila 1 se distingue porque esta congelada
y porque lleva el autofiltro, que son dos marcas estructurales y no decorativas.
"""

from datetime import datetime

from openpyxl import Workbook
from openpyxl.styles.numbers import FORMAT_TEXT

from espejo.hojas import HOJAS, TEMPORAL, TEXTO

_FORMATO_DE_FECHA = "%Y-%m-%d"
_FORMATO_DE_MARCA_DE_TIEMPO = "%Y-%m-%d %H:%M:%S"

# La fila 1 lleva los nombres de las columnas; los datos empiezan en la 2.
_PRIMERA_FILA_DE_DATOS = 2
_CELDA_QUE_CONGELA_LA_CABECERA = "A2"


def convertir_a_fecha(valor):
    """Una cadena ISO-8601 como `date` o `datetime`; cualquier otra cosa, intacta.

    El orden importa: se prueba primero la fecha sola. Si se probara antes la marca
    de tiempo, `'2026-09-08'` fallaria y caeria al camino de «no se pudo», cuando
    si se puede.
    """
    if not isinstance(valor, str):
        return valor
    try:
        return datetime.strptime(valor, _FORMATO_DE_FECHA).date()
    except ValueError:
        pass
    try:
        return datetime.strptime(valor, _FORMATO_DE_MARCA_DE_TIEMPO)
    except ValueError:
        return valor


def convertir_a_texto(valor):
    """Lo que va en una columna de texto, como cadena. El nulo sigue siendo nulo."""
    return valor if valor is None else str(valor)


def _valor_de_la_celda(valor, clase):
    """El valor que entra en la celda, segun la clase de su columna."""
    if valor is None:
        return None
    if clase == TEXTO:
        return convertir_a_texto(valor)
    if clase == TEMPORAL:
        return convertir_a_fecha(valor)
    return valor


def _escribir_celda(hoja, fila, columna, valor, clase):
    """Escribe una celda con su valor y su formato, y la devuelve."""
    celda = hoja.cell(row=fila, column=columna, value=_valor_de_la_celda(valor, clase))
    if clase == TEXTO:
        celda.number_format = FORMAT_TEXT
    if celda.data_type == "f":
        # Un texto que empieza por `=` lo escribiria openpyxl como FORMULA —medido:
        # `data_type` vale 'f'—. Un nombre leido por OCR no es una formula, y una
        # celda que se calcula sola deja de ser un espejo de la base. Se fuerza a
        # cadena. Los otros arranques peligrosos de una hoja de calculo (`+`, `-`,
        # `@`) ya salen como cadena por si solos, tambien medido.
        celda.data_type = "s"
    return celda


def _escribir_hoja(libro, conexion, definicion):
    """Vuelca una tabla entera en su hoja, con cabecera, congelado y autofiltro."""
    hoja = libro.create_sheet(title=definicion.nombre)
    hoja.append([columna.nombre for columna in definicion.columnas])

    for numero_de_fila, fila in enumerate(
        definicion.leer_filas(conexion), start=_PRIMERA_FILA_DE_DATOS
    ):
        for numero_de_columna, columna in enumerate(definicion.columnas, start=1):
            _escribir_celda(
                hoja, numero_de_fila, numero_de_columna, fila[columna.nombre], columna.clase
            )

    hoja.freeze_panes = _CELDA_QUE_CONGELA_LA_CABECERA
    hoja.auto_filter.ref = hoja.dimensions
    return hoja


def construir_libro(conexion):
    """El libro entero, con las cuatro hojas en el orden de `espejo.hojas.HOJAS`.

    Se quita la hoja que `Workbook()` crea sola: si se dejara, el libro abriria por
    una pestana vacia llamada «Sheet», en ingles y sin datos.
    """
    libro = Workbook()
    libro.remove(libro.active)
    for definicion in HOJAS:
        _escribir_hoja(libro, conexion, definicion)
    return libro
