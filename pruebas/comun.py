"""Utilidades compartidas por las pruebas de la capa de datos.

Ninguna prueba toca la base real de Miguel: todas trabajan sobre una carpeta
temporal que se borra al terminar.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema


class PruebaConBaseTemporal(unittest.TestCase):
    """Crea una base desde cero en una carpeta temporal y la cierra al final."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_prueba_"))
        self.ruta_de_la_base = self.carpeta_temporal / "fichas.db"
        self.conexion = abrir_conexion(self.ruta_de_la_base)
        aplicar_esquema(self.conexion)

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)


def caso_de_ejemplo():
    """Los valores del caso que usan las pruebas de ida y vuelta."""
    return {
        "numero_caso": "CASP2609",
        "unidad_numero": "123456",
        "fecha_viaje": "2026-09-08",
        "captura_manual": 0,
        "ruta_pdf": r"C:\Escaneos\CASP2609_Zutano_Family.pdf",
        "estado_recomendacion": "incompleta",
    }


def personas_de_ejemplo():
    """Tres personas del mismo caso, cada una con sus seis casillas."""
    return [
        {
            "mrn": "055-1111-3853",
            "nombre": "Jose Miguel Anonimo",
            "fila_formulario": 1,
            "ord_recibir_propias": 1,
            "ord_observar_sellamiento": 0,
            "ord_traductor": None,
            "ord_investidura": 1,
            "ord_sellamiento_esposos": 1,
            "ord_sellamiento_hijo_padres": 0,
        },
        {
            "mrn": "055-1111-3854",
            "nombre": "Maria de los Angeles Perez",
            "fila_formulario": 2,
            "ord_recibir_propias": 0,
            "ord_observar_sellamiento": 1,
            "ord_traductor": 1,
            "ord_investidura": None,
            "ord_sellamiento_esposos": 1,
            "ord_sellamiento_hijo_padres": None,
        },
        {
            "mrn": None,
            "nombre": "Pedro Antonio Anonimo Perez",
            "fila_formulario": 3,
            "ord_recibir_propias": None,
            "ord_observar_sellamiento": None,
            "ord_traductor": None,
            "ord_investidura": None,
            "ord_sellamiento_esposos": None,
            "ord_sellamiento_hijo_padres": 1,
        },
    ]
