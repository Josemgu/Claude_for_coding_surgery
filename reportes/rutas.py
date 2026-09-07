"""Donde se guardan los reportes, y como llegan al disco sin quedarse a medias.

Cuelgan de una carpeta propia dentro de la carpeta de datos —`Reportes`— y no al
lado de `fichas.db` y `fichas.xlsx`. El motivo es de uso: los reportes se
acumulan, uno por periodo y por formato, y mezclados con la base y el espejo
convierten la carpeta que Miguel abre a diario en una lista donde hay que buscar.

La carpeta NO se compone pegando nombres: se pide a
`datos.rutas.resolver_carpeta_de_datos`, que la resuelve por la API de carpetas
conocidas de Windows (`DECISIONES.md`, 2026-09-02).

**El nombre del archivo lleva el periodo dentro.** Volver a generar el mismo
periodo pisa el archivo anterior, que es lo que se quiere: dos reportes del mismo
mes con numeros distintos son dos verdades, y la buena es siempre la ultima.
"""

import os
from pathlib import Path

from datos.rutas import resolver_carpeta_de_datos
from reportes.periodo import nombre_corto_del_periodo

NOMBRE_DE_LA_CARPETA_DE_REPORTES = "Reportes"

# Igual que en `espejo/rutas.py`: lo que esta a medias NUNCA se llama como el
# definitivo. Si el programa muere escribiendo, lo que queda a medias lleva esta
# terminacion y el reporte anterior sigue abriendose sin mensaje de archivo dañado.
TERMINACION_DEL_ARCHIVO_PARCIAL = ".parcial"


def carpeta_de_reportes(carpeta_de_datos=None):
    """La carpeta donde viven los reportes. No la crea."""
    carpeta = (
        Path(carpeta_de_datos)
        if carpeta_de_datos is not None
        else resolver_carpeta_de_datos()
    )
    return carpeta / NOMBRE_DE_LA_CARPETA_DE_REPORTES


def ruta_del_reporte(periodo, extension, carpeta_de_datos=None):
    """La ruta del reporte de ese periodo en ese formato. No lo crea.

    `extension` va sin punto: 'xlsx' o 'pdf'.
    """
    nombre = f"reporte_{nombre_corto_del_periodo(periodo)}.{extension}"
    return carpeta_de_reportes(carpeta_de_datos) / nombre


def ruta_del_archivo_parcial(ruta_definitiva):
    """El archivo a medio escribir, en la MISMA carpeta que el definitivo.

    Tiene que ser la misma carpeta: el reemplazo final solo es atomico dentro del
    mismo volumen, y desde otro disco el ultimo paso seria una copia — y una copia
    interrumpida deja el archivo definitivo a medias.
    """
    return Path(str(ruta_definitiva) + TERMINACION_DEL_ARCHIVO_PARCIAL)


def escribir_de_un_golpe(ruta_definitiva, escribir_en):
    """Escribe con `escribir_en(ruta_parcial)` y asciende el parcial a definitivo.

    `PermissionError` es la unica excepcion que se atrapa, y no se atrapa para
    silenciarla: se convierte en un aviso en español que nombra el archivo y dice
    que hacer. Es el mismo caso que el Excel espejo — el reporte anterior abierto
    en Excel bloquea el reemplazo— y la respuesta tiene que ser la misma.

    Devuelve una tupla de avisos: vacia cuando el archivo quedo escrito.
    """
    ruta_definitiva = Path(ruta_definitiva)
    ruta_definitiva.parent.mkdir(parents=True, exist_ok=True)
    ruta_parcial = ruta_del_archivo_parcial(ruta_definitiva)
    try:
        escribir_en(ruta_parcial)
        os.replace(ruta_parcial, ruta_definitiva)
    except PermissionError as causa:
        return (aviso_de_archivo_bloqueado(ruta_definitiva, causa),)
    return ()


def aviso_de_archivo_bloqueado(ruta, causa):
    """Lo que ve Miguel cuando el reporte no se pudo escribir por estar abierto."""
    return (
        f"AVISO: no se pudo escribir el reporte '{ruta}' porque otro programa lo "
        "tiene abierto (casi siempre es el propio Excel o el visor de PDF). "
        "NO SE HA PERDIDO NINGÚN DATO: el reporte se saca de la base y se puede "
        "volver a generar cuantas veces haga falta. Cierre el archivo y vuelva a "
        f"pulsar «Generar». El sistema dijo: {causa}"
    )
