"""Pruebas de la resolucion de la carpeta de datos (FASE 1, criterio 1).

La ruta se obtiene con la API de carpetas conocidas de Windows. Estas pruebas
comparan lo que devuelve la API con lo que devuelve PowerShell por un segundo
camino, y comprueban que se muestra ANTES de escribir nada.
"""

import subprocess
import sys
import unittest
from pathlib import Path

from datos.rutas import (
    NOMBRE_DE_LA_BASE,
    NOMBRE_DE_LA_CARPETA_DE_DATOS,
    esta_bajo_onedrive,
    resolver_carpeta_de_datos,
    resolver_carpeta_de_documentos,
    ruta_de_la_base,
)


def _documentos_segun_powershell():
    salida = subprocess.run(
        ["powershell", "-NoProfile", "-Command", "[Environment]::GetFolderPath('MyDocuments')"],
        capture_output=True,
        text=True,
        check=True,
    )
    return salida.stdout.strip()


@unittest.skipUnless(sys.platform == "win32", "la API de carpetas conocidas es de Windows")
class PruebaDeResolucionPorApi(unittest.TestCase):

    def test_la_api_devuelve_una_carpeta_que_existe(self):
        documentos = resolver_carpeta_de_documentos()
        self.assertIsInstance(documentos, Path)
        self.assertTrue(documentos.is_dir(), f"{documentos} no existe")

    def test_coincide_caracter_a_caracter_con_el_segundo_camino(self):
        self.assertEqual(_documentos_segun_powershell(), str(resolver_carpeta_de_documentos()))

    def test_la_carpeta_de_datos_cuelga_de_la_que_devolvio_la_api(self):
        documentos = resolver_carpeta_de_documentos()
        self.assertEqual(documentos / NOMBRE_DE_LA_CARPETA_DE_DATOS, resolver_carpeta_de_datos())

    def test_la_base_cuelga_de_la_carpeta_de_datos(self):
        self.assertEqual(resolver_carpeta_de_datos() / NOMBRE_DE_LA_BASE, ruta_de_la_base())

    def test_resolver_no_crea_ninguna_carpeta(self):
        existia = resolver_carpeta_de_datos().exists()
        resolver_carpeta_de_datos()
        resolver_carpeta_de_datos()
        self.assertEqual(existia, resolver_carpeta_de_datos().exists())


class PruebaDelAvisoDeOneDrive(unittest.TestCase):

    def test_una_ruta_bajo_onedrive_se_detecta(self):
        self.assertTrue(esta_bajo_onedrive(Path(r"C:\Users\quien\OneDrive\Documentos\Fichas")))

    def test_una_ruta_fuera_de_onedrive_no_se_detecta(self):
        self.assertFalse(esta_bajo_onedrive(Path(r"C:\Users\quien\Documents\Fichas")))

    def test_la_deteccion_no_se_deja_enganar_por_un_nombre_parecido(self):
        self.assertFalse(esta_bajo_onedrive(Path(r"C:\Users\quien\OneDriveViejo\Fichas")))


if __name__ == "__main__":
    unittest.main()
