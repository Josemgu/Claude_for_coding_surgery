"""Generar el reporte de un periodo: se cuenta una vez y se escribe dos.

Es el unico punto por el que se pide un reporte. Que los dos formatos salgan de la
misma llamada no es comodidad: es lo que hace imposible que el `.xlsx` y el `.pdf`
del mismo periodo digan numeros distintos, que es el fallo que un reporte no se
puede permitir.

**Un formato que falla no se lleva por delante al otro.** El caso real es el
reporte anterior abierto en Excel: Windows bloquea el reemplazo del `.xlsx` y el
`.pdf` no tiene por que caer con el. Cada formato devuelve su propio aviso y el
resultado dice cual quedo escrito y cual no.
"""

from collections import namedtuple

from datos.validacion import ErrorDeValidacion
from reportes.documento import construir_documento
from reportes.excel import escribir_xlsx
from reportes.pdf import escribir_pdf
from reportes.rutas import escribir_de_un_golpe, ruta_del_reporte

ArchivoDelReporte = namedtuple("ArchivoDelReporte", ("ruta", "escrito", "avisos"))
ResultadoDelReporte = namedtuple(
    "ResultadoDelReporte", ("documento", "excel", "pdf", "avisos")
)


def _generar_un_formato(documento, periodo, extension, escribir, carpeta_de_datos):
    """Escribe el documento en un formato y dice como quedo."""
    ruta = ruta_del_reporte(periodo, extension, carpeta_de_datos)
    avisos = escribir_de_un_golpe(ruta, lambda parcial: escribir(documento, parcial))
    return ArchivoDelReporte(ruta, not avisos, avisos)


def validar_generado_en(generado_en):
    """La marca de tiempo del informe, comprobada antes de escribir nada.

    ⚠️ **Lo que arregla, medido por QA el 2026-09-03:** pasar aqui algo que no es
    texto —un `datetime`, por ejemplo, que es lo natural de escribir— no fallaba en
    esta funcion. Bajaba entera hasta el escritor de PDF y reventaba alli con un
    `TypeError` en ingles, con el rastro de un modulo que no tiene nada que ver con
    el error. Y peor: para entonces el `.xlsx` ya podia estar escrito, asi que
    quedaba media pareja de archivos.

    Se comprueba **antes** de contar nada y con mensaje en español (regla permanente
    4). Un `datetime` no se convierte por dentro a proposito: seria adivinar el
    formato en el que hay que escribirlo, y el informe pone esa cadena tal cual en
    la portada y en el pie de todas sus paginas.
    """
    if not isinstance(generado_en, str) or not generado_en.strip():
        raise ErrorDeValidacion(
            "No se puede generar el informe: la marca de tiempo «generado_en» tiene "
            f"que ser un texto con la fecha y la hora y llegó {generado_en!r} "
            f"({type(generado_en).__name__}). Se escribe tal cual en la portada y en "
            "el pie de todas las páginas, así que el formato lo elige quien llama: "
            "por ejemplo «2026-09-03 14:05:00»."
        )
    return generado_en


def generar_reporte(conexion, periodo, generado_en, carpeta_de_datos=None, avisar=print):
    """Cuenta el periodo una vez y lo escribe en `.xlsx` y en `.pdf`.

    `generado_en` entra desde fuera y no se lee del reloj aqui, por lo mismo que en
    `datos/calendario.py`: lo que mira el reloj por dentro no se puede probar. Se
    valida en la primera linea, antes de contar nada y antes de escribir ningun
    archivo: ver `validar_generado_en`.

    `avisar` recibe cada aviso ya redactado en español. Por defecto es `print`,
    igual que en `datos.arranque` y en `espejo.escritura`: si el valor por defecto
    fuera no avisar, un llamador que se olvide del resultado convertiria esto en un
    fallo callado.
    """
    generado_en = validar_generado_en(generado_en)
    documento = construir_documento(conexion, periodo, generado_en)
    excel = _generar_un_formato(
        documento, periodo, "xlsx", escribir_xlsx, carpeta_de_datos
    )
    pdf = _generar_un_formato(documento, periodo, "pdf", escribir_pdf, carpeta_de_datos)

    avisos = excel.avisos + pdf.avisos
    if avisar is not None:
        for aviso in avisos:
            avisar(aviso)
    return ResultadoDelReporte(documento, excel, pdf, avisos)
