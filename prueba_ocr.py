# MATERIAL DE PRUEBA - FASE 0. Se descarta al cerrar la fase; no es la base del programa Fichas.
"""Esqueleto desechable de la FASE 0.

Existe para responder una sola pregunta: si RapidOCR con los modelos PP-OCRv5
del grupo latino sobrevive a PyInstaller --onedir y arranca sin Python en la
maquina. No mide precision, no toca base de datos, no escribe Excel.

Uso:
    prueba_ocr.exe [ruta_al_pdf] [--cerrar-en SEGUNDOS]
"""

import math
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

INSTANTE_DE_ARRANQUE = time.perf_counter()

LADO_LARGO_MAXIMO_PX = 3500  # Regla de no regresion: tope al rasterizar.

NOMBRE_MODELO_DETECCION = "ch_PP-OCRv5_det_mobile.onnx"
NOMBRE_MODELO_RECONOCIMIENTO = "latin_PP-OCRv5_rec_mobile.onnx"
NOMBRE_MODELO_ORIENTACION = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"


def carpeta_base() -> Path:
    """Devuelve la carpeta donde viven los datos empaquetados.

    Empaquetado con --onedir, PyInstaller apunta sys._MEIPASS a _internal.
    Sin empaquetar, es la carpeta del propio archivo.
    """
    carpeta_empaquetada = getattr(sys, "_MEIPASS", None)
    if carpeta_empaquetada is not None:
        return Path(carpeta_empaquetada)
    return Path(__file__).resolve().parent


def rutas_de_los_modelos() -> dict:
    """Devuelve la ruta absoluta de cada modelo .onnx, resuelta desde la base."""
    carpeta_modelos = carpeta_base() / "modelos"
    return {
        "deteccion": carpeta_modelos / NOMBRE_MODELO_DETECCION,
        "reconocimiento": carpeta_modelos / NOMBRE_MODELO_RECONOCIMIENTO,
        "orientacion": carpeta_modelos / NOMBRE_MODELO_ORIENTACION,
    }


def imprimir_rutas_de_carga(rutas: dict) -> None:
    """Imprime de donde sale cada modelo y cada biblioteca nativa, con su estado."""
    print("=== RUTAS DE CARGA ===")
    print(f"empaquetado: {'si' if getattr(sys, 'frozen', False) else 'no'}")
    print(f"carpeta_base: {carpeta_base()}")
    print(f"ejecutable: {Path(sys.executable).resolve()}")
    for papel, ruta in rutas.items():
        existe = ruta.is_file()
        tamano = ruta.stat().st_size if existe else 0
        print(f"modelo[{papel}]: {ruta} | existe={'si' if existe else 'NO'} | bytes={tamano}")
    imprimir_rutas_de_los_modulos()
    imprimir_rutas_internas_de_rapidocr()
    print("=== FIN RUTAS DE CARGA ===")


def imprimir_rutas_de_los_modulos() -> None:
    """Imprime desde que archivo se cargo cada biblioteca de terceros.

    Se importan por su nombre real, no con __import__("pypdf"). Una importacion
    por cadena es invisible al analisis estatico de PyInstaller: en la primera
    construccion de esta fase pypdf no entro en el paquete por eso mismo, y el
    .exe murio con ModuleNotFoundError donde el .py funcionaba.
    """
    import cv2
    import onnxruntime
    import pypdf
    import pypdfium2
    import rapidocr

    for modulo in (rapidocr, onnxruntime, cv2, pypdfium2, pypdf):
        ruta = getattr(modulo, "__file__", "sin __file__")
        print(f"modulo[{modulo.__name__}]: {ruta}")


def imprimir_rutas_internas_de_rapidocr() -> None:
    """Imprime los archivos que RapidOCR resuelve por su cuenta, no por parametro.

    Son los que delatarian una dependencia de la maquina de desarrollo: su
    config.yaml y la carpeta donde descarga modelos si nadie le da model_path.
    """
    import rapidocr

    carpeta_paquete = Path(rapidocr.__file__).resolve().parent
    for papel, ruta in (
        ("config.yaml", carpeta_paquete / "config.yaml"),
        ("default_models.yaml", carpeta_paquete / "default_models.yaml"),
        ("cache_de_modelos", carpeta_paquete / "models"),
    ):
        print(f"rapidocr[{papel}]: {ruta} | existe={'si' if ruta.exists() else 'NO'}")


def calcular_escala_de_rasterizado(ancho_puntos: float, alto_puntos: float) -> float:
    """Devuelve la escala que deja el lado largo justo en el tope de pixeles.

    El nextafter no es un adorno: pypdfium2 redondea hacia arriba el tamano del
    mapa de bits. Con 792 puntos y 3500/792, el producto sale 3500.0000000000005
    en coma flotante y la pagina se rasteriza a 3501 px, un pixel por encima del
    tope. Bajar la escala al float inmediatamente anterior deja el lado en 3500.
    """
    lado_largo_puntos = max(ancho_puntos, alto_puntos)
    return math.nextafter(LADO_LARGO_MAXIMO_PX / lado_largo_puntos, 0.0)


def rasterizar_primera_pagina(ruta_pdf: Path):
    """Rasteriza la primera pagina del PDF a una matriz en escala de grises."""
    import cv2
    import numpy
    import pypdfium2

    documento = pypdfium2.PdfDocument(str(ruta_pdf))
    pagina = documento[0]
    ancho_puntos, alto_puntos = pagina.get_size()
    escala = calcular_escala_de_rasterizado(ancho_puntos, alto_puntos)
    imagen = pagina.render(scale=escala).to_numpy()
    gris = cv2.cvtColor(imagen, cv2.COLOR_RGB2GRAY)
    print(f"pagina rasterizada: {gris.shape[1]}x{gris.shape[0]} px (escala {escala:.3f})")
    return numpy.ascontiguousarray(cv2.medianBlur(gris, 3))


def crear_motor_ocr(rutas: dict):
    """Crea el motor de RapidOCR con los tres modelos dados por ruta explicita.

    Al pasar model_path para deteccion, orientacion y reconocimiento, RapidOCR no
    consulta ninguna URL: no hay red en tiempo de ejecucion.
    """
    from rapidocr import RapidOCR
    from rapidocr.utils.typings import OCRVersion

    return RapidOCR(
        params={
            "Global.log_level": "warning",
            # ocr_version exige el Enum, no la cadena: parse_parameters.py:60 lanza
            # "The value of Det.ocr_version must be Enum Type." con un str.
            "Det.model_path": str(rutas["deteccion"]),
            "Det.ocr_version": OCRVersion.PPOCRV5,
            "Cls.model_path": str(rutas["orientacion"]),
            # La generacion se declara por etapa. NO sirve tocar "Cls.cls_image_shape":
            # esa clave existe en config.yaml y nadie la lee. ch_ppocr_cls/main.py:35
            # hace CLS_SHAPE_BY_OCR_VERSION[cfg["ocr_version"]], y el valor por
            # defecto de Cls es PP-OCRv4 -> [3, 48, 192]. Con el modelo PP-OCRv5,
            # que espera [3, 80, 160], onnxruntime aborta con INVALID_ARGUMENT.
            "Cls.ocr_version": OCRVersion.PPOCRV5,
            "Rec.model_path": str(rutas["reconocimiento"]),
            "Rec.ocr_version": OCRVersion.PPOCRV5,
        }
    )


def extraer_lineas(motor, imagen) -> list:
    """Devuelve las lineas de texto que el OCR encontro, en orden de lectura."""
    resultado = motor(imagen)
    if resultado is None or resultado.txts is None:
        return []
    return list(resultado.txts)


def distancia_de_edicion(cadena_a: str, cadena_b: str) -> int:
    """Cuenta cuantas ediciones de un caracter separan dos cadenas."""
    fila_previa = list(range(len(cadena_b) + 1))
    for indice_a, caracter_a in enumerate(cadena_a, start=1):
        fila = [indice_a]
        for indice_b, caracter_b in enumerate(cadena_b, start=1):
            coste = 0 if caracter_a == caracter_b else 1
            fila.append(
                min(
                    fila_previa[indice_b] + 1,
                    fila[indice_b - 1] + 1,
                    fila_previa[indice_b - 1] + coste,
                )
            )
        fila_previa = fila
    return fila_previa[-1]


def elegir_linea_mas_parecida(lineas: list, referencia: str) -> str:
    """Devuelve la linea del OCR mas cercana a la referencia transcrita."""
    if not lineas:
        return ""
    return min(lineas, key=lambda linea: distancia_de_edicion(linea, referencia))


def comparar_con_la_referencia(lineas: list) -> tuple:
    """Devuelve (linea elegida, caracteres que difieren, largo de la referencia)."""
    from referencia_prueba import LINEA_REFERENCIA

    elegida = elegir_linea_mas_parecida(lineas, LINEA_REFERENCIA)
    return elegida, distancia_de_edicion(elegida, LINEA_REFERENCIA), len(LINEA_REFERENCIA)


def leer_argumentos(argumentos: list) -> tuple:
    """Separa la ruta del PDF y los segundos de cierre automatico."""
    ruta_pdf = None
    segundos_hasta_cerrar = 0
    resto = list(argumentos)
    while resto:
        actual = resto.pop(0)
        if actual == "--cerrar-en" and resto:
            segundos_hasta_cerrar = int(resto.pop(0))
        elif ruta_pdf is None:
            ruta_pdf = Path(actual)
    if ruta_pdf is None:
        ruta_pdf = carpeta_base() / "prueba_latina.pdf"
    return ruta_pdf, segundos_hasta_cerrar


def construir_ventana(ruta_pdf: Path):
    """Abre la ventana y devuelve (ventana, area de texto) ya visible en pantalla."""
    import tkinter

    ventana = tkinter.Tk()
    ventana.title("Fichas - prueba de empaquetado (FASE 0)")
    ventana.geometry("900x520")
    area = tkinter.Text(ventana, wrap="word", font=("Consolas", 11))
    area.pack(fill="both", expand=True, padx=8, pady=8)
    area.insert("end", f"PDF de entrada: {ruta_pdf}\n\nCargando los modelos...\n")
    ventana.update()
    return ventana, area


def segundos_desde_que_windows_creo_el_proceso() -> float:
    """Segundos transcurridos desde que Windows creo este proceso.

    Un cronometro interno de Python no sirve para el criterio 2: empaquetado, el
    cargador de PyInstaller corre ANTES de la primera linea de Python, y ese
    tiempo es justo el que el criterio quiere medir. GetProcessTimes lo ve.
    """
    import ctypes
    from ctypes import wintypes

    UNIDADES_DE_100NS_POR_SEGUNDO = 10_000_000
    # FILETIME cuenta desde 1601-01-01; este es el desplazamiento hasta 1970-01-01.
    DESPLAZAMIENTO_HASTA_LA_EPOCA_UNIX = 116_444_736_000_000_000

    creacion = wintypes.FILETIME()
    descartados = (wintypes.FILETIME * 3)()
    # El pseudo-handle del proceso actual es -1. Se pasa como c_void_p a
    # proposito: sin tipo, ctypes lo manda como int de 32 bits, la llamada falla
    # en silencio y la estructura vuelve en ceros (da 425 anos de "arranque").
    ctypes.windll.kernel32.GetProcessTimes(
        ctypes.c_void_p(-1),
        ctypes.byref(creacion),
        ctypes.byref(descartados[0]),
        ctypes.byref(descartados[1]),
        ctypes.byref(descartados[2]),
    )
    marca = (creacion.dwHighDateTime << 32) | creacion.dwLowDateTime
    creado_en_epoca_unix = (
        marca - DESPLAZAMIENTO_HASTA_LA_EPOCA_UNIX
    ) / UNIDADES_DE_100NS_POR_SEGUNDO
    return time.time() - creado_en_epoca_unix


def anunciar_ventana_lista() -> None:
    """Imprime el instante exacto en que la ventana ya responde en pantalla."""
    ahora = datetime.now(timezone.utc).astimezone().isoformat()
    interno = time.perf_counter() - INSTANTE_DE_ARRANQUE
    desde_el_proceso = segundos_desde_que_windows_creo_el_proceso()
    print(
        f"VENTANA_LISTA {ahora} interno={interno:.3f}s "
        f"desde_creacion_del_proceso={desde_el_proceso:.3f}s",
        flush=True,
    )


def imprimir_resultado(lineas: list, elegida: str, difieren: int, largo: int, duracion: float) -> None:
    """Vuelca a la consola el texto extraido y los numeros del criterio 4."""
    print("=== TEXTO EXTRAIDO ===")
    for linea in lineas:
        print(linea)
    print("=== FIN TEXTO EXTRAIDO ===")
    print(f"LINEA_OCR_ELEGIDA {elegida}")
    print(f"CARACTERES_QUE_DIFIEREN {difieren} de {largo}")
    print(f"OCR_SEGUNDOS {duracion:.2f}", flush=True)


def ejecutar_ocr_y_mostrar(area, ruta_pdf: Path, rutas: dict) -> None:
    """Corre el OCR sobre el PDF y vuelca el resultado en la ventana y la consola."""
    inicio = time.perf_counter()
    imagen = rasterizar_primera_pagina(ruta_pdf)
    motor = crear_motor_ocr(rutas)
    lineas = extraer_lineas(motor, imagen)
    elegida, difieren, largo = comparar_con_la_referencia(lineas)
    duracion = time.perf_counter() - inicio
    imprimir_resultado(lineas, elegida, difieren, largo, duracion)

    area.delete("1.0", "end")
    area.insert("end", f"PDF de entrada: {ruta_pdf}\n\n")
    area.insert("end", "Texto extraido:\n" + "\n".join(lineas) + "\n\n")
    area.insert("end", f"Caracteres que difieren de la referencia: {difieren} de {largo}\n")
    area.insert("end", f"OCR en {duracion:.2f} s\n")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ruta_pdf, segundos_hasta_cerrar = leer_argumentos(sys.argv[1:])
    rutas = rutas_de_los_modelos()
    imprimir_rutas_de_carga(rutas)

    ventana, area = construir_ventana(ruta_pdf)
    anunciar_ventana_lista()

    if segundos_hasta_cerrar > 0:
        ventana.after(segundos_hasta_cerrar * 1000, ventana.destroy)
    ventana.after(50, lambda: ejecutar_ocr_y_mostrar(area, ruta_pdf, rutas))
    ventana.mainloop()
    return 0


if __name__ == "__main__":
    sys.exit(main())
