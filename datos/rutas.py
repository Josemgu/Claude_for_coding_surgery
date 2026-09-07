"""Donde viven la base y el Excel espejo, resuelto por la API de Windows.

DECISIONES.md (2026-09-02, «La carpeta de datos se resuelve por API, nunca por
nombre») es vinculante aqui: la carpeta de documentos se pide a
`SHGetKnownFolderPath` con el identificador de carpeta conocida de Documentos, y
NUNCA se compone pegando el nombre de la carpeta a `USERPROFILE`.

El motivo esta medido: en la maquina del dueno existen tres carpetas candidatas
y dos de ellas sincronizan con OneDrive. Componer la ruta por nombre puede
aterrizar en la de OneDrive y subir a la nube una base con MRN de personas
reales.
"""

import ctypes
import sys
from ctypes import wintypes
from pathlib import Path

# Identificador de la carpeta conocida de Documentos, tal como lo publica la API
# de carpetas conocidas de Windows. Es un GUID, no un nombre de carpeta: por eso
# no depende del idioma del sistema ni de que existan carpetas homonimas.
_GUID_CARPETA_CONOCIDA_DE_DOCUMENTOS = "FDD39AD0-238F-46AF-ADB4-6C85480369C7"

NOMBRE_DE_LA_CARPETA_DE_DATOS = "Fichas"
NOMBRE_DE_LA_BASE = "fichas.db"


class ErrorDeRuta(RuntimeError):
    """No se pudo resolver la carpeta de datos."""


class _Guid(ctypes.Structure):
    """Estructura GUID de Windows, en el orden de bytes que espera la API."""

    _fields_ = [
        ("datos1", ctypes.c_ulong),
        ("datos2", ctypes.c_ushort),
        ("datos3", ctypes.c_ushort),
        ("datos4", ctypes.c_ubyte * 8),
    ]


def _guid_desde_texto(texto):
    """Convierte el GUID escrito en texto a la estructura que pide la API."""
    import uuid

    estructura = _Guid()
    ctypes.memmove(ctypes.byref(estructura), uuid.UUID(texto).bytes_le, 16)
    return estructura


def resolver_carpeta_de_documentos():
    """Devuelve la carpeta de Documentos que el sistema declara vigente.

    No crea nada. Si Windows devuelve un error, se propaga con su codigo: es
    preferible no arrancar a escribir la base en un sitio equivocado.
    """
    if sys.platform != "win32":
        raise ErrorDeRuta(
            "La carpeta de datos solo se puede resolver en Windows: "
            f"este sistema se identifica como '{sys.platform}'."
        )

    shell32 = ctypes.windll.shell32
    shell32.SHGetKnownFolderPath.argtypes = [
        ctypes.POINTER(_Guid),
        wintypes.DWORD,
        wintypes.HANDLE,
        ctypes.POINTER(ctypes.c_wchar_p),
    ]
    shell32.SHGetKnownFolderPath.restype = ctypes.HRESULT

    identificador = _guid_desde_texto(_GUID_CARPETA_CONOCIDA_DE_DOCUMENTOS)
    apuntador = ctypes.c_wchar_p()
    codigo = shell32.SHGetKnownFolderPath(
        ctypes.byref(identificador), 0, None, ctypes.byref(apuntador)
    )
    if codigo != 0 or not apuntador.value:
        raise ErrorDeRuta(
            "Windows no devolvió la carpeta de Documentos: "
            f"SHGetKnownFolderPath respondió con el código {codigo}."
        )
    try:
        return Path(apuntador.value)
    finally:
        ctypes.windll.ole32.CoTaskMemFree(apuntador)


def resolver_carpeta_de_datos():
    """La carpeta donde viven la base y el Excel espejo. No la crea."""
    return resolver_carpeta_de_documentos() / NOMBRE_DE_LA_CARPETA_DE_DATOS


def ruta_de_la_base(carpeta_de_datos=None):
    """La ruta del archivo de base de datos. No lo crea."""
    carpeta = carpeta_de_datos if carpeta_de_datos is not None else resolver_carpeta_de_datos()
    return Path(carpeta) / NOMBRE_DE_LA_BASE


def esta_bajo_onedrive(ruta):
    """Dice si la ruta cuelga de una carpeta de OneDrive.

    Compara nombre a nombre, no por subcadena: 'OneDriveViejo' no es OneDrive.
    """
    return any(parte.lower() == "onedrive" for parte in Path(ruta).parts)
