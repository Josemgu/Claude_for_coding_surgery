"""Donde se escribe el Excel espejo, y donde se escribe mientras se escribe.

La carpeta NO se compone aqui: se pide a `datos.rutas.resolver_carpeta_de_datos`,
que la resuelve por la API de carpetas conocidas de Windows. Pegar la cadena
«Documentos» a `USERPROFILE` es exactamente lo que `DECISIONES.md` (2026-09-02, «La
carpeta de datos se resuelve por API, nunca por nombre») prohibe, y por un motivo
medido: en la maquina del dueno hay tres carpetas candidatas y dos sincronizan con
OneDrive.
"""

from pathlib import Path

from datos.rutas import resolver_carpeta_de_datos

NOMBRE_DEL_ESPEJO = "fichas.xlsx"

# El archivo a medio escribir lleva SIEMPRE este nombre, y nunca el definitivo.
# Dos consecuencias buscadas:
#   - Si el programa muere a mitad de la escritura, lo que queda a medias se llama
#     `fichas.xlsx.parcial`. El `fichas.xlsx` anterior sigue entero y sigue
#     abriendose en Excel sin mensaje de archivo danado.
#   - El nombre es fijo, no lleva numero ni marca de tiempo: un resto de una
#     ejecucion muerta lo pisa la siguiente. No se acumulan restos en la carpeta.
NOMBRE_DEL_ARCHIVO_PARCIAL = NOMBRE_DEL_ESPEJO + ".parcial"


def _carpeta(carpeta_de_datos=None):
    """La carpeta donde viven la base y el espejo. No la crea."""
    if carpeta_de_datos is not None:
        return Path(carpeta_de_datos)
    return resolver_carpeta_de_datos()


def ruta_del_espejo(carpeta_de_datos=None):
    """La ruta del `.xlsx` definitivo, en la misma carpeta que la base."""
    return _carpeta(carpeta_de_datos) / NOMBRE_DEL_ESPEJO


def ruta_del_archivo_parcial(carpeta_de_datos=None):
    """La ruta del archivo temporal, en la MISMA carpeta que el definitivo.

    Tiene que ser la misma carpeta, y no la de temporales del sistema: el
    reemplazo final solo es atomico dentro del mismo volumen. Si el temporal
    viviera en otro disco, el ultimo paso seria una copia, y una copia
    interrumpida deja el archivo definitivo a medias.
    """
    return _carpeta(carpeta_de_datos) / NOMBRE_DEL_ARCHIVO_PARCIAL
