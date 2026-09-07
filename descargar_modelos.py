# MATERIAL DE PRUEBA - FASE 0. Se descarta al cerrar la fase; no es codigo del programa Fichas.
"""Descarga, una sola vez y en tiempo de construccion, los tres modelos .onnx
que usa el esqueleto de prueba. El programa empaquetado NO descarga nada: recibe
las rutas ya resueltas dentro de su propia carpeta.
"""

import hashlib
import sys
import urllib.request
from pathlib import Path

CARPETA_MODELOS = Path(__file__).resolve().parent / "modelos"

# URL y SHA256 copiados literalmente de
# .venv/Lib/site-packages/rapidocr/default_models.yaml (rapidocr 3.9.2).
MODELOS = {
    "ch_PP-OCRv5_det_mobile.onnx": (
        "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv5/det/ch_PP-OCRv5_det_mobile.onnx",
        "4d97c44a20d30a81aad087d6a396b08f786c4635742afc391f6621f5c6ae78ae",
    ),
    "latin_PP-OCRv5_rec_mobile.onnx": (
        "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv5/rec/latin_PP-OCRv5_rec_mobile.onnx",
        "b20bd37c168a570f583afbc8cd7925603890efbcdc000a59e22c269d160b5f5a",
    ),
    "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx": (
        "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv5/cls/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
        "54379ae5174d026780215fc748a7f31910dee36818e63d49e17dc598ecc82df7",
    ),
}


def calcular_sha256(ruta: Path) -> str:
    """Devuelve el SHA256 en hexadecimal del archivo indicado."""
    resumen = hashlib.sha256()
    with ruta.open("rb") as archivo:
        for bloque in iter(lambda: archivo.read(1024 * 1024), b""):
            resumen.update(bloque)
    return resumen.hexdigest()


def descargar_modelo(nombre: str, url: str, sha256_esperado: str) -> bool:
    """Descarga un modelo si falta y verifica su huella. Devuelve si quedo valido."""
    destino = CARPETA_MODELOS / nombre
    if not destino.exists():
        print(f"Descargando {nombre} ...")
        with urllib.request.urlopen(url) as respuesta, destino.open("wb") as salida:
            salida.write(respuesta.read())

    huella = calcular_sha256(destino)
    tamano_mb = destino.stat().st_size / (1024 * 1024)
    coincide = huella == sha256_esperado
    print(f"{nombre}: {tamano_mb:.2f} MB  SHA256={huella}  esperado={'SI' if coincide else 'NO'}")
    return coincide


def main() -> int:
    CARPETA_MODELOS.mkdir(exist_ok=True)
    resultados = [
        descargar_modelo(nombre, url, sha) for nombre, (url, sha) in MODELOS.items()
    ]
    if all(resultados):
        print("Los tres modelos estan en", CARPETA_MODELOS)
        return 0
    print("ERROR: alguna huella no coincide.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
