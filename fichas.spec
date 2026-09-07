# -*- mode: python ; coding: utf-8 -*-
"""El paquete que Miguel abre con doble clic. FASE 9.

**`--onedir`, no `--onefile`** (`DECISIONES.md`). Con `--onefile` los tres `.onnx`
se descomprimen a una carpeta temporal en CADA arranque. `exclude_binaries=True`
en `EXE` mas `COLLECT` es la forma de una sola carpeta: el criterio 1 de la fase
se comprueba viendo que existe `dist\\Fichas\\_internal`.

**`console=False`**: no sale una consola negra detras de la ventana. Tiene una
consecuencia que hubo que arreglar en `interfaz/aplicacion.py`: PyInstaller deja
`sys.stdout` en `None` y `print` se traga las lineas del arranque sin avisar, asi
que la ruta de datos se DIBUJA en el pie de la ventana.

**Los `.onnx` viajan en `datas`** y `extraccion/ocr.py` los resuelve en tiempo de
ejecucion por `sys._MEIPASS`, que en un paquete de una carpeta apunta a
`_internal`. Sin eso el programa los buscaria en internet la primera vez que
arranque en la maquina de Miguel, y la regla permanente 2 lo prohibe.

**Los dos `.yaml` de rapidocr se anaden a mano** porque no son `.py` y el
analizador no los ve. Sin `default_models.yaml` rapidocr no arranca.
"""

from PyInstaller.utils.hooks import collect_data_files

# El `.dll` de ffmpeg que NO se lleva, medido: 29.45 MiB de 244. Este proyecto usa
# de OpenCV `medianBlur` y `cvtColor` (`CLAUDE.md` §3) y rapidocr usa `imgproc`,
# `core` e `imgcodecs`. Ninguna de las dos abre un video.
#
# ⚠️ Medido el 2026-09-02, y contradice lo que el plan suponia: cambiar a
# `opencv-python-headless` **NO quita este archivo** —viene en el RECORD del
# propio paquete headless— y ahorra solo 0.42 MiB en `cv2.pyd`. Quitarlo hay que
# pedirlo aqui, a mano, o no se va.
BINARIOS_QUE_NO_VIAJAN = ("opencv_videoio_ffmpeg",)

# Se excluye `pandas` por la regla permanente 3, y se excluye AQUI aunque ningun
# `import` lo pida: es lo que hace comprobable el criterio 8 de la fase sobre el
# producto y no sobre el codigo.
PAQUETES_EXCLUIDOS = ["pandas", "matplotlib", "scipy", "pytest", "IPython"]


def sin_los_binarios_pesados(lista):
    """Quita de una lista de PyInstaller los archivos que no se llevan.

    Cada entrada es `(destino, origen, tipo)`. Se compara contra el destino, que
    es el nombre con el que el archivo acabaria dentro de `_internal`.
    """
    return [
        entrada
        for entrada in lista
        if not any(marca in entrada[0].lower() for marca in BINARIOS_QUE_NO_VIAJAN)
    ]


a = Analysis(
    ["fichas.py"],
    pathex=[],
    binaries=[],
    datas=[("modelos", "modelos")] + collect_data_files("rapidocr", includes=["*.yaml"]),
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=PAQUETES_EXCLUIDOS,
    noarchive=False,
    optimize=0,
)

a.binaries = sin_los_binarios_pesados(a.binaries)
a.datas = sin_los_binarios_pesados(a.datas)

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="Fichas",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    # `upx=False` a proposito: UPX comprime pero se sabe que corrompe algunas DLL
    # nativas, y aqui viajan `onnxruntime` y `cv2`. Un paquete unos MB mas grande
    # que arranca vale mas que uno mas pequeno que a veces no.
    upx=False,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name="Fichas",
)
