"""Pruebas del arranque (FASE 1, criterios 1a, 1d y 1e).

Lo primero que aparece es la ruta de la carpeta de datos; solo despues existe el
archivo. Se comprueba por el orden observable, no por la intencion del codigo.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.arranque import mostrar_ruta_de_datos, preparar_base_de_datos
from datos.esquema import TABLAS_DEL_ESQUEMA


class PruebaDelOrdenObservable(unittest.TestCase):

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_arranque_"))
        self.carpeta_de_datos = self.carpeta_temporal / "Fichas"
        self.lineas = []

    def tearDown(self):
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    def test_mostrar_la_ruta_no_crea_ni_carpeta_ni_archivo(self):
        ruta = mostrar_ruta_de_datos(
            carpeta_de_datos=self.carpeta_de_datos, escribir=self.lineas.append
        )
        self.assertEqual(self.carpeta_de_datos / "fichas.db", ruta)
        self.assertFalse(self.carpeta_de_datos.exists())
        self.assertTrue(any(str(self.carpeta_de_datos) in linea for linea in self.lineas))

    def test_la_ruta_se_imprime_antes_de_que_exista_el_archivo(self):
        momentos = []

        def anotar(linea):
            self.lineas.append(linea)
            momentos.append((linea, (self.carpeta_de_datos / "fichas.db").exists()))

        preparar_base_de_datos(carpeta_de_datos=self.carpeta_de_datos, escribir=anotar)

        primera_linea_con_la_ruta = next(
            (existia for linea, existia in momentos if str(self.carpeta_de_datos) in linea), None
        )
        self.assertIs(False, primera_linea_con_la_ruta)

    def test_despues_de_preparar_la_base_el_archivo_existe_y_tiene_las_tablas(self):
        import sqlite3

        ruta = preparar_base_de_datos(
            carpeta_de_datos=self.carpeta_de_datos, escribir=self.lineas.append
        )
        self.assertTrue(ruta.is_file())

        conexion = sqlite3.connect(ruta)
        try:
            tablas = [
                fila[0]
                for fila in conexion.execute(
                    "SELECT name FROM sqlite_master WHERE type = 'table' "
                    "AND name NOT LIKE 'sqlite_%' ORDER BY name"
                )
            ]
        finally:
            conexion.close()
        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), tablas)

    def test_el_arranque_informa_de_la_version_de_sqlite(self):
        preparar_base_de_datos(carpeta_de_datos=self.carpeta_de_datos, escribir=self.lineas.append)
        self.assertTrue(any("SQLite" in linea for linea in self.lineas))

    def test_una_ruta_bajo_onedrive_produce_aviso_pero_no_falla(self):
        bajo_onedrive = self.carpeta_temporal / "OneDrive" / "Documentos" / "Fichas"
        ruta = preparar_base_de_datos(
            carpeta_de_datos=bajo_onedrive, escribir=self.lineas.append
        )
        self.assertTrue(ruta.is_file())
        self.assertTrue(any("OneDrive" in linea and "AVISO" in linea for linea in self.lineas))

    def test_preparar_dos_veces_no_pierde_los_datos(self):
        from datos.conexion import abrir_conexion
        from datos.repositorio import alta_de_caso
        from pruebas.comun import caso_de_ejemplo

        ruta = preparar_base_de_datos(
            carpeta_de_datos=self.carpeta_de_datos, escribir=self.lineas.append
        )
        conexion = abrir_conexion(ruta)
        alta_de_caso(conexion, **caso_de_ejemplo())
        conexion.close()

        preparar_base_de_datos(carpeta_de_datos=self.carpeta_de_datos, escribir=self.lineas.append)
        conexion = abrir_conexion(ruta)
        try:
            self.assertEqual(1, conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])
        finally:
            conexion.close()


if __name__ == "__main__":
    unittest.main()
