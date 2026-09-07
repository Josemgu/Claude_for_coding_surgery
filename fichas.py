"""Punto de entrada del programa. Se abre con doble clic y no pide nada.

    python fichas.py                          abre la ventana
    python fichas.py --solo-mostrar-ruta      dice donde escribiria y se va
    python fichas.py --carpeta-de-datos RUTA  escribe en RUTA en vez de en Documentos

La segunda forma no dibuja nada: sirve para comprobar por el orden observable que
el programa dice la ruta ANTES de escribir en ella (`DECISIONES.md`, 2026-09-02).

⚠️ **La tercera existe para poder PROBAR el `.exe` sin tocar la base del dueño**, y
entró el 2026-09-03 con la auditoría final de QA. Sin ella, conducir el programa
empaquetado de punta a punta obligaba a escribir en `Documentos\\Fichas`, que es
donde están los datos reales; QA lo dijo así: es lo que convierte «no lo pude
probar» en «probado». Cuando se pasa, **manda sobre la carpeta que resuelve la API
de Windows** y el pie de la ventana dice cuál quedó, para que nunca se pueda estar
escribiendo en otro sitio sin verlo.
"""

import sys
import time

# ⚠️ El reloj se para AQUI, en la primera linea que corre, y no dentro de la
# ventana. El dueno dice que «el problema es cargar la interfaz», y una parte de
# esa carga son los `import` de tkinter, sqlite y lo demas: medidos desde dentro
# de la ventana, esos ya se pagaron y no saldrian en la cifra.
ARRANCADO_EN = time.perf_counter()

BANDERA_DE_LA_CARPETA = "--carpeta-de-datos"


class ErrorDeArranque(RuntimeError):
    """Los argumentos con los que se llamó al programa no se pueden usar."""


def carpeta_de_datos_pedida(argumentos):
    """La ruta que se pidió con `--carpeta-de-datos`, o None si no se pidió ninguna.

    Se acepta en las dos formas en que se escribe una opción —`--carpeta-de-datos X`
    y `--carpeta-de-datos=X`— porque las dos son normales y equivocarse de forma
    delante de un `.exe` que no imprime nada deja a quien lo intenta sin saber qué
    pasó.

    Una bandera sin ruta detrás **levanta** en vez de caerse a la carpeta de
    siempre: escribir en la base real del dueño creyendo que se escribe en una
    temporal es exactamente el accidente que esta opción existe para evitar.
    """
    for indice, argumento in enumerate(argumentos):
        if argumento.startswith(BANDERA_DE_LA_CARPETA + "="):
            ruta = argumento.split("=", 1)[1]
        elif argumento == BANDERA_DE_LA_CARPETA:
            ruta = argumentos[indice + 1] if indice + 1 < len(argumentos) else ""
        else:
            continue
        if not ruta.strip():
            raise ErrorDeArranque(
                f"«{BANDERA_DE_LA_CARPETA}» necesita una ruta detrás. Escriba por "
                f"ejemplo: Fichas.exe {BANDERA_DE_LA_CARPETA} C:\\Temp\\FichasPrueba"
            )
        from pathlib import Path

        return Path(ruta)
    return None


def main(argumentos):
    try:
        carpeta = carpeta_de_datos_pedida(argumentos)
    except ErrorDeArranque as causa:
        print(causa)
        return 2

    if "--solo-mostrar-ruta" in argumentos:
        from datos.arranque import mostrar_ruta_de_datos

        mostrar_ruta_de_datos(carpeta_de_datos=carpeta)
        return 0

    from interfaz.aplicacion import Aplicacion

    Aplicacion(carpeta_de_datos=carpeta, arrancado_en=ARRANCADO_EN).ejecutar()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
