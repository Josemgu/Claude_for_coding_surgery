"""El reporte escrito como `.xlsx` con openpyxl. Aqui no se cuenta nada.

Lo que llega es un `Documento` ya armado por `reportes/documento.py`; este modulo
solo lo dibuja. `PENDIENTES.md` (FASE 8, criterio 6) fija el formato: «El reporte
es un `.xlsx` escrito con `openpyxl`», y el motivo esta escrito alli — no hay otra
biblioteca de escritura en `CLAUDE.md` §3, la fase prohibe engordar el ejecutable,
y Miguel ya trabaja con el Excel espejo de la FASE 4.

**El MRN y el numero de caso llevan formato de texto (`@`).** Sin el, Excel lee
'055-1111-3853' como algo que puede normalizar y el cero de delante
desaparecen. Es la misma defensa que la FASE 4 se molesta en poner en el espejo, y
un reporte que se la salta entrega a los jefes un MRN que no identifica a nadie.

**Las fechas entran como objeto `date`, no como cadena**, por lo mismo que en el
espejo: una cadena se ordena alfabeticamente, y eso funciona por casualidad
mientras todas las filas traigan el mismo formato.

Una hoja por seccion. Sin celdas combinadas y sin colores: un color no es un dato,
y este archivo lo va a leer alguien que quiza lo imprima en blanco y negro.
"""

from datetime import datetime

from openpyxl import Workbook
from openpyxl.styles.numbers import FORMAT_TEXT
from openpyxl.utils import get_column_letter

from reportes.documento import TEMPORAL, TEXTO

_FORMATO_DE_FECHA = "%Y-%m-%d"

# Cuanto se ensancha una columna respecto a los caracteres que declara. El ancho
# de openpyxl se mide en caracteres de la fuente por defecto y una columna clavada
# al ancho de su titulo corta el contenido en cuanto una fila es mas larga.
_HOLGURA_DE_LA_COLUMNA = 1.2
_ANCHO_MAXIMO_DE_COLUMNA = 60


def _convertir_a_fecha(valor):
    """Una cadena 'AAAA-MM-DD' como `date`; cualquier otra cosa, intacta.

    Lo que no se puede leer como fecha se deja tal cual: no se adivina, no se
    rellena y no se descarta. Una fecha inventada en un reporte a los jefes es
    peor que una fecha fea (regla permanente 1).
    """
    if not isinstance(valor, str):
        return valor
    try:
        return datetime.strptime(valor, _FORMATO_DE_FECHA).date()
    except ValueError:
        return valor


def _valor_de_la_celda(valor, clase):
    """El valor que entra en la celda, segun la clase de su columna."""
    if valor is None:
        return None
    if clase == TEXTO:
        return str(valor)
    if clase == TEMPORAL:
        return _convertir_a_fecha(valor)
    return valor


def _escribir_celda(hoja, fila, columna, valor, clase):
    """Escribe una celda con su valor y su formato."""
    celda = hoja.cell(row=fila, column=columna, value=_valor_de_la_celda(valor, clase))
    if clase == TEXTO:
        celda.number_format = FORMAT_TEXT
    if celda.data_type == "f":
        # Un texto que empieza por `=` lo escribiria openpyxl como FORMULA. Un
        # motivo escrito a mano no es una formula, y una celda que se calcula sola
        # deja de decir lo que alguien escribio. Se fuerza a cadena, igual que en
        # `espejo/libro.py`.
        celda.data_type = "s"
    return celda


def _escribir_cabecera(hoja, documento, seccion):
    """Las lineas de arriba: titulo, periodo, cuando se genero y los avisos.

    Los avisos van en la cabecera de TODAS las hojas y no solo en la primera. Una
    hoja de Excel se imprime suelta y se manda suelta por correo, y un aviso que
    dice «esta columna no se puede calcular» tiene que viajar pegado a la columna
    que no se puede calcular.
    """
    lineas = [documento.titulo, documento.subtitulo,
              f"Generado el {documento.generado_en}"]
    # La portada va en la cabecera de TODAS las hojas, por lo mismo que los avisos:
    # una hoja de Excel se manda suelta por correo, y el numero por el que existe
    # este programa no puede quedarse solo en la primera.
    lineas.append(documento.portada.titular)
    lineas.append(documento.portada.frase)
    lineas.extend(
        f"{cifra.numero} {cifra.rotulo}" for cifra in documento.portada.cifras
    )
    lineas.append(seccion.titulo)
    lineas.extend(seccion.notas)
    lineas.extend(documento.avisos)
    for numero, texto in enumerate(lineas, start=1):
        hoja.cell(row=numero, column=1, value=texto)
    return len(lineas) + 2


def _ajustar_anchos(hoja, columnas):
    """Deja cada columna con el ancho que declara su definicion."""
    for numero, columna in enumerate(columnas, start=1):
        ancho = min(columna.ancho * _HOLGURA_DE_LA_COLUMNA, _ANCHO_MAXIMO_DE_COLUMNA)
        hoja.column_dimensions[get_column_letter(numero)].width = ancho


def _escribir_seccion(libro, documento, seccion):
    """Una hoja con la cabecera, los titulos de columna y las filas."""
    hoja = libro.create_sheet(title=_nombre_de_hoja(seccion.titulo))
    fila_de_titulos = _escribir_cabecera(hoja, documento, seccion)

    for numero, columna in enumerate(seccion.columnas, start=1):
        hoja.cell(row=fila_de_titulos, column=numero, value=columna.nombre)

    for desplazamiento, valores in enumerate(seccion.filas, start=1):
        for numero, (columna, valor) in enumerate(zip(seccion.columnas, valores), start=1):
            _escribir_celda(
                hoja, fila_de_titulos + desplazamiento, numero, valor, columna.clase
            )

    if seccion.resumen is not None:
        hoja.cell(
            row=fila_de_titulos + len(seccion.filas) + 2, column=1, value=seccion.resumen
        )
    hoja.freeze_panes = hoja.cell(row=fila_de_titulos + 1, column=1).coordinate
    _ajustar_anchos(hoja, seccion.columnas)
    return hoja


# Los cinco caracteres que Excel no admite en el nombre de una pestaña, y el tope
# de 31 caracteres. No es una regla nuestra: es del formato, y openpyxl levanta si
# se pasa. El titulo de una seccion es una frase larga, asi que se recorta.
_CARACTERES_PROHIBIDOS_EN_UNA_HOJA = ("[", "]", ":", "*", "?", "/", "\\")
_LARGO_MAXIMO_DEL_NOMBRE_DE_HOJA = 31


def _nombre_de_hoja(titulo):
    """El titulo de la seccion recortado a lo que admite una pestaña de Excel."""
    limpio = titulo
    for caracter in _CARACTERES_PROHIBIDOS_EN_UNA_HOJA:
        limpio = limpio.replace(caracter, " ")
    return limpio[:_LARGO_MAXIMO_DEL_NOMBRE_DE_HOJA].strip()


def construir_libro(documento):
    """El libro entero, una hoja por seccion, sin tocar el disco.

    Se quita la hoja que `Workbook()` crea sola: si se dejara, el reporte abriria
    por una pestaña vacía llamada «Sheet», en inglés y sin datos.
    """
    libro = Workbook()
    libro.remove(libro.active)
    for seccion in documento.secciones:
        _escribir_seccion(libro, documento, seccion)
    return libro


def escribir_xlsx(documento, ruta_parcial):
    """Escribe el libro en esa ruta. Quien la asciende a definitiva es `rutas`."""
    construir_libro(documento).save(str(ruta_parcial))
