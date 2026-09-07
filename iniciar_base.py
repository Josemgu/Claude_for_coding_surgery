"""Punto de entrada de la capa de datos, sin interfaz.

    python iniciar_base.py                     crea o abre la base
    python iniciar_base.py --solo-mostrar-ruta muestra la ruta y se va sin escribir

La segunda forma existe para poder comprobar el criterio 1a de la FASE 1 por el
orden observable: si el programa se cierra en cuanto muestra la ruta, la carpeta
sigue sin contener el archivo de base de datos.
"""

import sys

from datos.arranque import mostrar_ruta_de_datos, preparar_base_de_datos


def main(argumentos):
    if "--solo-mostrar-ruta" in argumentos:
        mostrar_ruta_de_datos()
        return 0
    preparar_base_de_datos()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
