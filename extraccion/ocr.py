"""El motor de OCR, con los modelos del grupo latino cargados por ruta explicita.

Los modelos por defecto de RapidOCR apuntan a chino e ingles. Con los PP-OCRv5
del grupo latino el espanol sube a casi 100%: es una regla de no regresion del
proyecto, y por eso los tres modelos se pasan por ruta y no se dejan al azar de
la configuracion del paquete.

Pasar `model_path` tiene un segundo efecto que aqui importa mas que el primero:
RapidOCR NO consulta ninguna URL para descargarlos. Sin eso, el programa buscaria
modelos en internet la primera vez que arranque en la maquina de Miguel.
"""

from collections import namedtuple
from pathlib import Path

from extraccion.geometria import rectangulo_desde_puntos

NOMBRE_DEL_MODELO_DE_DETECCION = "ch_PP-OCRv5_det_mobile.onnx"
NOMBRE_DEL_MODELO_DE_RECONOCIMIENTO = "latin_PP-OCRv5_rec_mobile.onnx"
NOMBRE_DEL_MODELO_DE_ORIENTACION = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"

LineaLeida = namedtuple("LineaLeida", ("texto", "rectangulo", "confianza"))


class ErrorDeModelos(RuntimeError):
    """Falta alguno de los tres modelos .onnx donde deberia estar."""


def carpeta_de_los_modelos(carpeta_base=None):
    """Donde viven los `.onnx`, resuelto tambien dentro del ejecutable.

    Empaquetado con `--onedir`, PyInstaller apunta `sys._MEIPASS` a `_internal` y
    la carpeta no esta donde el codigo la espera. Se resuelve aqui una vez.
    """
    import sys

    if carpeta_base is not None:
        return Path(carpeta_base)
    carpeta_empaquetada = getattr(sys, "_MEIPASS", None)
    if carpeta_empaquetada is not None:
        return Path(carpeta_empaquetada) / "modelos"
    return Path(__file__).resolve().parent.parent / "modelos"


def rutas_de_los_modelos(carpeta_base=None):
    """Las tres rutas absolutas, comprobando que los archivos existen.

    Se comprueba aqui y no al primer uso: un fallo al arrancar se entiende; el
    mismo fallo a mitad de procesar veinte formularios, no.
    """
    carpeta = carpeta_de_los_modelos(carpeta_base)
    rutas = {
        "deteccion": carpeta / NOMBRE_DEL_MODELO_DE_DETECCION,
        "reconocimiento": carpeta / NOMBRE_DEL_MODELO_DE_RECONOCIMIENTO,
        "orientacion": carpeta / NOMBRE_DEL_MODELO_DE_ORIENTACION,
    }
    faltan = [str(ruta) for ruta in rutas.values() if not ruta.is_file()]
    if faltan:
        raise ErrorDeModelos(
            "No están los modelos de OCR y sin ellos no se puede leer nada. "
            f"Faltan: {faltan}. Se bajan con `descargar_modelos.py`."
        )
    return rutas


def crear_motor_ocr(carpeta_base=None):
    """Crea el motor con los tres modelos dados por ruta.

    `ocr_version` se declara por etapa y exige el Enum, no la cadena. No basta
    con el modelo: `ch_ppocr_cls` elige la forma de la imagen segun la version
    configurada, y con el valor por defecto (PP-OCRv4 -> [3, 48, 192]) sobre un
    modelo PP-OCRv5 (que espera [3, 80, 160]) onnxruntime aborta.
    """
    from rapidocr import RapidOCR
    from rapidocr.utils.typings import OCRVersion

    rutas = rutas_de_los_modelos(carpeta_base)
    return RapidOCR(
        params={
            "Global.log_level": "warning",
            "Det.model_path": str(rutas["deteccion"]),
            "Det.ocr_version": OCRVersion.PPOCRV5,
            "Cls.model_path": str(rutas["orientacion"]),
            "Cls.ocr_version": OCRVersion.PPOCRV5,
            "Rec.model_path": str(rutas["reconocimiento"]),
            "Rec.ocr_version": OCRVersion.PPOCRV5,
        }
    )


def leer_lineas(motor, imagen):
    """Las lineas de texto con su caja en pixeles y su confianza, de arriba abajo.

    Una pagina sin texto legible devuelve la lista vacia, no una excepcion: un
    escaneo en blanco es un caso normal, no un fallo del programa.
    """
    resultado = motor(imagen)
    if resultado is None or resultado.txts is None or resultado.boxes is None:
        return []
    lineas = [
        LineaLeida(
            texto=texto,
            rectangulo=rectangulo_desde_puntos(caja),
            confianza=float(puntuacion),
        )
        for caja, texto, puntuacion in zip(resultado.boxes, resultado.txts, resultado.scores)
    ]
    return sorted(lineas, key=lambda linea: (linea.rectangulo.y0, linea.rectangulo.x0))
