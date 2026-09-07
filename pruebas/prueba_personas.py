"""Las filas de personas: cuantas salen, cuales se descartan y cuales no existen.

Los nombres y los MRN de estas pruebas son inventados. La geometria —altos de
fila de unos 50 px, filas pegadas una debajo de otra, columna de MRN a la
derecha— reproduce la que se midio en los formularios de referencia, porque es
justo lo que el codigo usa para decidir.
"""

import unittest
from collections import namedtuple

from extraccion.geometria import Rectangulo
from extraccion.personas import (
    FILAS_DEL_FORMULARIO,
    extraer_personas,
    limite_de_la_columna_de_nombres,
)

LineaFalsa = namedtuple("LineaFalsa", ("texto", "rectangulo", "confianza"))

ANCLA_NOMBRES = Rectangulo(x0=534.0, y0=776.0, x1=729.0, y1=838.0)
ANCLA_MRN = Rectangulo(x0=1337.0, y0=780.0, x1=1766.0, y1=836.0)
CIERRE = (Rectangulo(x0=156.0, y0=1381.0, x1=373.0, y1=1437.0),)


def linea(texto, y0, x0, alto=50.0, ancho=250.0, confianza=0.97):
    return LineaFalsa(texto, Rectangulo(x0, y0, x0 + ancho, y0 + alto), confianza)


def fila(nombre, mrn, y0):
    """Una fila del formulario: el nombre a la izquierda y el MRN a la derecha."""
    lineas = [linea(nombre, y0=y0, x0=544.0)]
    if mrn is not None:
        lineas.append(linea(mrn, y0=y0, x0=1428.0))
    return lineas


class PruebaDelLimiteDeColumnas(unittest.TestCase):
    def test_el_limite_cae_entre_las_dos_cabeceras(self):
        limite = limite_de_la_columna_de_nombres(ANCLA_NOMBRES, ANCLA_MRN)
        self.assertGreater(limite, ANCLA_NOMBRES.x0)
        self.assertLess(limite, ANCLA_MRN.x0)


class PruebaDeLasFilasDePersonas(unittest.TestCase):
    def _extraer(self, lineas, cierres=CIERRE):
        return extraer_personas(lineas, ANCLA_NOMBRES, ANCLA_MRN, cierres)

    def test_dos_filas_llenas_y_cuatro_vacias_dan_exactamente_dos_personas(self):
        """El criterio de aceptacion, literal: 2 personas, no 6."""
        lineas = fila("Ana Ejemplo Prueba", "111-2222-3333", 1000.0) + fila(
            "Luis Ejemplo Prueba", "111-2222-3334", 1052.0
        )
        personas, descartadas = self._extraer(lineas)
        self.assertEqual(len(personas), 2)
        self.assertEqual(descartadas, 0)
        self.assertEqual([p.nombre.valor for p in personas], ["Ana Ejemplo Prueba", "Luis Ejemplo Prueba"])
        self.assertEqual([p.mrn.valor for p in personas], ["111-2222-3333", "111-2222-3334"])
        self.assertEqual(FILAS_DEL_FORMULARIO - len(personas), 4)

    def test_una_persona_sin_mrn_legible_no_desaparece(self):
        """Se guarda con el MRN vacio y marcado, que es un problema visible.

        ⚠️ El ejemplo de esta prueba era `111-2222-333A` hasta el 2026-09-04, y
        dejo de servir ese dia: una cedula PUEDE terminar en letra, y esa ya se
        lee entera (`DECISIONES.md`, «La cedula PUEDE terminar en letra»). Lo que
        la prueba mide —que una fila ilegible no borra a la persona— no ha
        cambiado; hacia falta un texto que de verdad siga sin encajar, y la letra
        en MEDIO sigue sin encajar.
        """
        lineas = fila("Ana Ejemplo Prueba", "111-222A-3333", 1000.0)
        personas, _ = self._extraer(lineas)
        self.assertEqual(len(personas), 1)
        self.assertIsNone(personas[0].mrn.valor)
        self.assertTrue(personas[0].mrn.necesita_revision)
        # `valor_ocr` guarda la fila entera tal como la leyo la maquina, nombre
        # incluido: es lo que Miguel necesita ver para decidir que escribe.
        self.assertIn("111-222A-3333", personas[0].mrn.valor_ocr)

    def test_el_mrn_pegado_al_nombre_en_una_sola_linea_se_recupera(self):
        """Medido: el OCR junta las dos columnas cuando el escaneo va torcido."""
        lineas = [linea("Ejemplo Prueba, Ana 111-2222-3333", y0=1000.0, x0=544.0, ancho=1300.0)]
        personas, _ = self._extraer(lineas)
        self.assertEqual(len(personas), 1)
        self.assertEqual(personas[0].mrn.valor, "111-2222-3333")

    def test_una_nota_de_revision_no_es_una_persona(self):
        """«Verified for sealing» cae a la izquierda del MRN pero no es un nombre.

        Lo separa el limite de columna, que sale del punto medio entre las dos
        cabeceras y no de una coordenada fija.
        """
        lineas = fila("Ana Ejemplo Prueba", "111-2222-3333", 1000.0)
        lineas.append(linea("Verified for sealing", y0=1052.0, x0=1263.0, ancho=580.0))
        personas, _ = self._extraer(lineas)
        self.assertEqual(len(personas), 1)

    def test_el_texto_de_debajo_de_la_tabla_no_entra_como_persona(self):
        """Hay un hueco entre la ultima persona y lo que viene despues."""
        lineas = fila("Ana Ejemplo Prueba", "111-2222-3333", 1000.0)
        lineas.append(linea("Nombre del templo de ejemplo", y0=1250.0, x0=162.0))
        personas, _ = self._extraer(lineas)
        self.assertEqual(len(personas), 1)

    def test_sin_etiqueta_que_cierre_el_bloque_no_se_extrae_ninguna_persona(self):
        """El caso que producia 35 personas en una pagina que tiene una.

        Sin cierre no se sabe donde acaba la tabla. Devolver cero y marcar la
        pagina es correcto; rellenarla de personas inventadas, no.
        """
        lineas = fila("Ana Ejemplo Prueba", "111-2222-3333", 1000.0)
        lineas.append(linea("Total Costs", y0=2200.0, x0=162.0))
        personas, _ = self._extraer(lineas, cierres=(None, None))
        self.assertEqual(personas, [])

    def test_sin_las_cabeceras_no_se_extrae_ninguna_persona(self):
        lineas = fila("Ana Ejemplo Prueba", "111-2222-3333", 1000.0)
        self.assertEqual(extraer_personas(lineas, None, ANCLA_MRN, CIERRE), ([], 0))
        self.assertEqual(extraer_personas(lineas, ANCLA_NOMBRES, None, CIERRE), ([], 0))

    def test_un_formulario_sin_ninguna_fila_llena_no_produce_personas(self):
        personas, descartadas = self._extraer([])
        self.assertEqual(personas, [])
        self.assertEqual(descartadas, 0)
        self.assertEqual(FILAS_DEL_FORMULARIO - len(personas), 6)

    def test_las_filas_se_numeran_desde_uno_y_en_orden_de_arriba_abajo(self):
        lineas = fila("Beto Ejemplo", "111-2222-3335", 1052.0) + fila(
            "Ana Ejemplo", "111-2222-3333", 1000.0
        )
        personas, _ = self._extraer(lineas)
        self.assertEqual([p.fila_formulario for p in personas], [1, 2])
        self.assertEqual([p.nombre.valor for p in personas], ["Ana Ejemplo", "Beto Ejemplo"])

    def test_el_mrn_de_la_fila_de_al_lado_no_se_cuela(self):
        """Cada MRN va con SU fila: mezclarlos cambia de quien es cada MRN."""
        lineas = fila("Ana Ejemplo", "111-2222-3333", 1000.0) + fila(
            "Beto Ejemplo", "111-2222-3335", 1052.0
        )
        personas, _ = self._extraer(lineas)
        self.assertEqual(personas[0].mrn.valor, "111-2222-3333")
        self.assertEqual(personas[1].mrn.valor, "111-2222-3335")


if __name__ == "__main__":
    unittest.main()
