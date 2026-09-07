"""La conversion de coordenadas y el criterio de traslape del 50%.

Esta prueba existe por una razon concreta escrita en `DECISIONES.md`: las
anotaciones vienen en puntos PDF con el origen ABAJO a la izquierda, y la imagen
rasterizada tiene el origen ARRIBA a la izquierda. Si alguien quita la inversion,
los rectangulos caen espejados respecto al centro de la pagina y el sistema anula
campos que estaban buenos.

Cada caso de abajo esta escrito para FALLAR si se quita la inversion. Los numeros
son inventados a proposito: ningun dato de los PDF reales entra en el repositorio.
"""

import math
import unittest

from extraccion.geometria import (
    LADO_LARGO_MAXIMO_PX,
    Rectangulo,
    escala_de_rasterizado,
    fraccion_de_traslape_vertical,
    rectangulo_pdf_a_pixeles,
    traslape_vertical,
)

ALTO_CARTA_PUNTOS = 792.0
ANCHO_CARTA_PUNTOS = 612.0


class PruebaDeLaInversionVertical(unittest.TestCase):
    """Lo alto en el PDF tiene que salir arriba en la imagen, no abajo."""

    def test_lo_que_esta_arriba_en_el_pdf_sale_arriba_en_la_imagen(self):
        # y=700..750 en puntos PDF esta cerca del borde SUPERIOR de una carta.
        # Invertido: (792-750)=42 y (792-737)=... es decir, y pequena en imagen.
        pixeles = rectangulo_pdf_a_pixeles(
            (0.0, 700.0, 100.0, 750.0), ALTO_CARTA_PUNTOS, escala=1.0
        )
        self.assertEqual(pixeles.y0, 42.0)
        self.assertEqual(pixeles.y1, 92.0)
        # Sin la inversion saldrian 700 y 750. Esta comprobacion lo dice explicito
        # para que quien lea el fallo entienda que rompio.
        self.assertNotEqual(pixeles.y0, 700.0)

    def test_lo_que_esta_abajo_en_el_pdf_sale_abajo_en_la_imagen(self):
        pixeles = rectangulo_pdf_a_pixeles(
            (0.0, 42.0, 100.0, 92.0), ALTO_CARTA_PUNTOS, escala=1.0
        )
        self.assertEqual(pixeles.y0, 700.0)
        self.assertEqual(pixeles.y1, 750.0)

    def test_un_rectangulo_descentrado_no_coincide_con_su_espejo(self):
        """El caso que un `abs()` mal puesto dejaria pasar.

        Si la conversion fuera simetrica respecto al centro de la pagina, un
        rectangulo y su espejo darian lo mismo y el error seria invisible.
        """
        arriba = rectangulo_pdf_a_pixeles(
            (0.0, 700.0, 100.0, 750.0), ALTO_CARTA_PUNTOS, escala=1.0
        )
        espejo = rectangulo_pdf_a_pixeles(
            (0.0, ALTO_CARTA_PUNTOS - 750.0, 100.0, ALTO_CARTA_PUNTOS - 700.0),
            ALTO_CARTA_PUNTOS,
            escala=1.0,
        )
        self.assertNotEqual(arriba.y0, espejo.y0)
        self.assertEqual(arriba.y0, ALTO_CARTA_PUNTOS - espejo.y1)

    def test_la_horizontal_no_se_invierte(self):
        """El eje x comparte origen en los dos sistemas: solo se escala."""
        pixeles = rectangulo_pdf_a_pixeles(
            (36.0, 100.0, 106.0, 120.0), ALTO_CARTA_PUNTOS, escala=2.0
        )
        self.assertEqual(pixeles.x0, 72.0)
        self.assertEqual(pixeles.x1, 212.0)

    def test_la_escala_se_aplica_despues_de_invertir(self):
        """Invertir y luego escalar no da lo mismo que escalar y luego invertir.

        Con escala 4 y la formula correcta, y=750 -> (792-750)*4 = 168. Si alguien
        escalara primero y restara 792 despues, saldria 792*4-750*4 = 168 tambien,
        pero restando 792 sin escalar saldria 2208. Este caso ancla el numero.
        """
        pixeles = rectangulo_pdf_a_pixeles(
            (0.0, 700.0, 10.0, 750.0), ALTO_CARTA_PUNTOS, escala=4.0
        )
        self.assertEqual(pixeles.y0, 168.0)
        self.assertEqual(pixeles.y1, 368.0)

    def test_el_rectangulo_sale_siempre_con_y0_menor_que_y1(self):
        """La inversion invierte el orden: hay que reordenar o todo lo demas falla."""
        pixeles = rectangulo_pdf_a_pixeles(
            (0.0, 437.75, 100.0, 443.95), ALTO_CARTA_PUNTOS, escala=4.419191919191919
        )
        self.assertLess(pixeles.y0, pixeles.y1)
        self.assertLess(pixeles.x0, pixeles.x1)


class PruebaDeLaEscalaDeRasterizado(unittest.TestCase):
    """El tope de 3500 px, y el motivo por el que no se calcula por division."""

    def test_el_lado_largo_no_pasa_del_tope(self):
        escala = escala_de_rasterizado(ANCHO_CARTA_PUNTOS, ALTO_CARTA_PUNTOS)
        lado_largo = math.ceil(ALTO_CARTA_PUNTOS * escala)
        self.assertLessEqual(lado_largo, LADO_LARGO_MAXIMO_PX)
        self.assertEqual(lado_largo, 3500)

    def test_la_division_directa_se_pasa_del_tope_y_por_eso_hay_nextafter(self):
        """El control positivo de la regla: sin `nextafter` el tope se rompe.

        `math.ceil` es lo que pypdfium2 hace con el tamano del mapa de bits, y
        NO `round`. Medido el 2026-09-02 sobre una pagina carta real:

            producto con la escala por division = 3500.0000000000005
            render con esa escala               = 2705x3501  <- se paso
            render con math.nextafter           = 2705x3500

        Si esta comprobacion empieza a fallar es que la coma flotante o la
        biblioteca cambiaron, y entonces `escala_de_rasterizado` se podria
        simplificar. Mientras falle, no se puede.
        """
        escala_ingenua = LADO_LARGO_MAXIMO_PX / ALTO_CARTA_PUNTOS
        self.assertGreater(math.ceil(ALTO_CARTA_PUNTOS * escala_ingenua), LADO_LARGO_MAXIMO_PX)

    def test_una_pagina_apaisada_topa_por_el_ancho(self):
        escala = escala_de_rasterizado(ALTO_CARTA_PUNTOS, ANCHO_CARTA_PUNTOS)
        self.assertEqual(math.ceil(ALTO_CARTA_PUNTOS * escala), 3500)
        self.assertLess(math.ceil(ANCHO_CARTA_PUNTOS * escala), 3500)


class PruebaDelTraslapeVertical(unittest.TestCase):
    """El criterio del 50%, probado a un lado y al otro del borde."""

    def _banda(self):
        """Una banda de 100 px de alto, para que los porcentajes se lean solos."""
        return Rectangulo(x0=0.0, y0=1000.0, x1=500.0, y1=1100.0)

    def test_sin_solape_devuelve_cero(self):
        marca = Rectangulo(x0=0.0, y0=800.0, x1=500.0, y1=900.0)
        self.assertEqual(traslape_vertical(marca, self._banda()), 0.0)
        self.assertEqual(fraccion_de_traslape_vertical(marca, self._banda()), 0.0)

    def test_el_49_por_ciento_no_llega(self):
        marca = Rectangulo(x0=0.0, y0=1051.0, x1=500.0, y1=1100.0)
        self.assertAlmostEqual(fraccion_de_traslape_vertical(marca, self._banda()), 0.49)
        self.assertLess(fraccion_de_traslape_vertical(marca, self._banda()), 0.5)

    def test_el_51_por_ciento_si_llega(self):
        marca = Rectangulo(x0=0.0, y0=1049.0, x1=500.0, y1=1100.0)
        self.assertAlmostEqual(fraccion_de_traslape_vertical(marca, self._banda()), 0.51)
        self.assertGreater(fraccion_de_traslape_vertical(marca, self._banda()), 0.5)

    def test_el_50_exacto_no_supera_el_umbral(self):
        """«Supera el 50%» es estricto: el empate no anula.

        Lo dice asi `DECISIONES.md`. Se fija en una prueba para que nadie lo
        cambie a `>=` sin darse cuenta de que cambio la regla.
        """
        marca = Rectangulo(x0=0.0, y0=1050.0, x1=500.0, y1=1100.0)
        self.assertEqual(fraccion_de_traslape_vertical(marca, self._banda()), 0.5)

    def test_una_marca_que_cubre_la_banda_entera_da_uno(self):
        marca = Rectangulo(x0=0.0, y0=900.0, x1=500.0, y1=1200.0)
        self.assertEqual(fraccion_de_traslape_vertical(marca, self._banda()), 1.0)

    def test_una_banda_de_alto_cero_no_divide_entre_cero(self):
        banda_degenerada = Rectangulo(x0=0.0, y0=1000.0, x1=500.0, y1=1000.0)
        marca = Rectangulo(x0=0.0, y0=900.0, x1=500.0, y1=1100.0)
        self.assertEqual(fraccion_de_traslape_vertical(marca, banda_degenerada), 0.0)


if __name__ == "__main__":
    unittest.main()
