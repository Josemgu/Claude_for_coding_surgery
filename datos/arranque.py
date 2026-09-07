"""El arranque de la capa de datos: primero se dice donde, despues se escribe.

`DECISIONES.md` (2026-09-02) obliga a que el programa MUESTRE la ruta que resolvio
antes de escribir nada, para que se vea si cayo donde debia. El orden de este
modulo es esa obligacion: `mostrar_ruta_de_datos` no crea nada, y
`preparar_base_de_datos` la llama antes de tocar el disco.
"""

import sqlite3

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema, version_de_la_base
from datos.rutas import esta_bajo_onedrive, resolver_carpeta_de_datos, ruta_de_la_base


def mostrar_ruta_de_datos(carpeta_de_datos=None, escribir=print):
    """Escribe la ruta resuelta y la devuelve. No crea ni carpeta ni archivo.

    Si la ruta cae bajo OneDrive lo dice, pero no falla: puede ser la ruta
    legitima si el usuario tiene activada la copia de seguridad de carpetas
    conocidas. Lo que estaria mal es no decirlo.
    """
    carpeta = carpeta_de_datos if carpeta_de_datos is not None else resolver_carpeta_de_datos()
    ruta = ruta_de_la_base(carpeta)

    escribir(f"Carpeta de datos: {carpeta}")
    escribir(f"Base de datos:    {ruta}")
    escribir(f"Motor SQLite:     {sqlite3.sqlite_version}")
    if esta_bajo_onedrive(carpeta):
        escribir(
            f"AVISO: la carpeta de datos está dentro de OneDrive ({carpeta}). "
            "La base lleva nombres y MRN de personas reales y se va a sincronizar "
            "con la nube. No se detiene el programa: hay que decidirlo a mano."
        )
    return ruta


def preparar_base_de_datos(carpeta_de_datos=None, escribir=print):
    """Muestra la ruta, crea la carpeta si falta y aplica el esquema.

    Idempotente: sobre una base ya creada no duplica tablas ni filas y no toca los
    datos que ya estan.
    """
    ruta = mostrar_ruta_de_datos(carpeta_de_datos=carpeta_de_datos, escribir=escribir)

    existia = ruta.is_file()
    ruta.parent.mkdir(parents=True, exist_ok=True)

    conexion = abrir_conexion(ruta)
    try:
        aplicar_esquema(conexion)
        version = version_de_la_base(conexion)
        casos = conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
    finally:
        conexion.close()

    escribir(f"Base {'abierta' if existia else 'creada'} con esquema versión {version}.")
    escribir(f"Casos guardados: {casos}")
    return ruta
