"""La tira del escaneo: normalizar, recortar, escalar y convertir a PGM.

Sin OCR y sin PDF: se recorta sobre una imagen construida a mano, con una franja
de un valor conocido en un sitio conocido. Asi la prueba mide el recorte y no la
calidad de un escaneo.
"""

import unittest

import numpy

from extraccion.geometria import Rectangulo
from extraccion.recorte import (
    ALTO_DE_LA_TIRA_PX,
    ANCHO_DE_LA_TIRA_PX,
    a_pgm,
    banda_en_fracciones,
    escalar_para_caber,
    escalar_por_factor,
    recortar,
    region_de_la_pagina,
    tira_de_la_banda,
    vista_de_la_region,
)


def _imagen_de_prueba(ancho=1000, alto=1400, arriba=400, abajo=440):
    """Gris claro con una franja oscura entre `arriba` y `abajo`."""
    imagen = numpy.full((alto, ancho), 240, dtype=numpy.uint8)
    imagen[arriba:abajo, 100:900] = 30
    return imagen


class NormalizarLaBanda(unittest.TestCase):
    def test_los_pixeles_se_vuelven_fracciones_de_la_pagina(self):
        banda = banda_en_fracciones(Rectangulo(100, 400, 900, 440), 1000, 1400)
        self.assertAlmostEqual(banda[0], 0.1)
        self.assertAlmostEqual(banda[2], 0.9)
        self.assertAlmostEqual(banda[1], 400 / 1400)
        self.assertAlmostEqual(banda[3], 440 / 1400)

    def test_sin_rectangulo_no_hay_banda(self):
        self.assertIsNone(banda_en_fracciones(None, 1000, 1400))

    def test_una_banda_que_se_sale_se_recorta_al_borde(self):
        """Guardarla fuera de rango la rechazaria el CHECK del motor.

        La banda nace ensanchada seis veces el alto del ancla hacia la derecha, asi
        que en un campo pegado al margen se sale de la pagina de verdad. Recortarla
        al borde ensena un poco menos de papel; dejarla salir tumba el guardado.
        """
        banda = banda_en_fracciones(Rectangulo(-50, 400, 1400, 440), 1000, 1400)
        self.assertEqual(banda[0], 0.0)
        self.assertEqual(banda[2], 1.0)

    def test_un_rectangulo_del_reves_sale_ordenado(self):
        banda = banda_en_fracciones(Rectangulo(900, 440, 100, 400), 1000, 1400)
        self.assertLessEqual(banda[0], banda[2])
        self.assertLessEqual(banda[1], banda[3])

    def test_una_imagen_sin_tamano_no_revienta(self):
        self.assertIsNone(banda_en_fracciones(Rectangulo(0, 0, 1, 1), 0, 0))


class RecortarLaBanda(unittest.TestCase):
    def setUp(self):
        self.imagen = _imagen_de_prueba()
        self.banda = banda_en_fracciones(Rectangulo(100, 400, 900, 440), 1000, 1400)

    def test_el_recorte_cae_sobre_la_franja_oscura(self):
        """Lo que sale tiene que ser el dato, no el papel de al lado.

        Es la comprobacion que de verdad importa: una banda mal convertida da un
        recorte perfectamente valido de un trozo equivocado de la pagina, y en
        pantalla se veria como una tira en blanco sin que nada avise.
        """
        recorte = recortar(self.imagen, self.banda)
        self.assertIsNotNone(recorte)
        self.assertLess(recorte.mean(), 200, "el recorte no toco la franja oscura")

    def test_lleva_un_margen_arriba_y_abajo(self):
        """Un poco de papel alrededor es lo que deja ver si la tira esta bien situada."""
        recorte = recortar(self.imagen, self.banda)
        self.assertGreater(recorte.shape[0], 40)

    def test_sin_banda_no_hay_recorte(self):
        self.assertIsNone(recortar(self.imagen, None))

    def test_sin_imagen_no_hay_recorte(self):
        self.assertIsNone(recortar(None, self.banda))

    def test_una_banda_de_area_cero_no_da_un_recorte_vacio(self):
        self.assertIsNone(recortar(self.imagen, (0.5, 0.5, 0.5, 0.5)))


class EscalarYConvertir(unittest.TestCase):
    def test_la_tira_sale_con_el_alto_pedido(self):
        recorte = _imagen_de_prueba()[400:460, 100:900]
        escalada = escalar_para_caber(recorte, ALTO_DE_LA_TIRA_PX)
        self.assertEqual(escalada.shape[0], ALTO_DE_LA_TIRA_PX)

    def test_el_ancho_guarda_la_proporcion(self):
        recorte = numpy.zeros((60, 600), dtype=numpy.uint8)
        escalada = escalar_para_caber(recorte, 30)
        self.assertEqual(escalada.shape, (30, 300))

    def test_escalar_nada_devuelve_nada(self):
        self.assertIsNone(escalar_para_caber(None))
        self.assertIsNone(escalar_para_caber(numpy.zeros((0, 0), dtype=numpy.uint8)))

    def test_el_pgm_lleva_su_cabecera_y_todos_los_pixeles(self):
        """El formato es el que Tk sabe leer sin ninguna biblioteca de imagenes.

        Medido en esta maquina (Tk 9.0.4): `PhotoImage(data=...)` acepta estos
        bytes crudos. Con los mismos bytes en base64 falla, porque Tk 9 trata el
        texto base64 como PNG. Por eso son bytes y no texto.
        """
        bytes_pgm = a_pgm(numpy.zeros((4, 5), dtype=numpy.uint8))
        self.assertTrue(bytes_pgm.startswith(b"P5\n5 4\n255\n"))
        self.assertEqual(len(bytes_pgm), len(b"P5\n5 4\n255\n") + 20)

    def test_el_camino_entero_en_una_llamada(self):
        imagen = _imagen_de_prueba()
        banda = banda_en_fracciones(Rectangulo(100, 400, 900, 440), 1000, 1400)
        bytes_pgm = tira_de_la_banda(imagen, banda)
        self.assertIsNotNone(bytes_pgm)
        self.assertTrue(bytes_pgm.startswith(b"P5\n"))

    def test_una_banda_ancha_cabe_entera_en_el_hueco(self):
        """El fallo de la FASE 9: la imagen salia de 610 px y el hueco medía 380.

        Se veian los cinco primeros digitos de un numero de seis, y el ultimo no
        habia forma de comprobarlo — que es justo para lo que la tira existe.
        """
        recorte = numpy.zeros((63, 835), dtype=numpy.uint8)
        escalada = escalar_para_caber(recorte, ALTO_DE_LA_TIRA_PX, ANCHO_DE_LA_TIRA_PX)
        self.assertLessEqual(escalada.shape[1], ANCHO_DE_LA_TIRA_PX)
        self.assertLessEqual(escalada.shape[0], ALTO_DE_LA_TIRA_PX)

    def test_al_caber_de_ancho_no_se_pierde_ni_una_columna_del_papel(self):
        """Se ve el papel entero: la ultima columna del origen llega al destino."""
        recorte = numpy.full((63, 835), 240, dtype=numpy.uint8)
        recorte[:, -8:] = 0
        escalada = escalar_para_caber(recorte, ALTO_DE_LA_TIRA_PX, ANCHO_DE_LA_TIRA_PX)
        self.assertEqual(int(escalada[:, -1].max()), 0)

    def test_una_banda_estrecha_se_queda_con_el_alto_de_siempre(self):
        """El tope solo actua cuando estorba: lo que ya cabia no se encoge."""
        recorte = numpy.zeros((60, 200), dtype=numpy.uint8)
        escalada = escalar_para_caber(recorte, ALTO_DE_LA_TIRA_PX, ANCHO_DE_LA_TIRA_PX)
        self.assertEqual(escalada.shape[0], ALTO_DE_LA_TIRA_PX)

    def test_la_proporcion_se_conserva_al_caber_de_ancho(self):
        """Encoger solo el ancho deformaria las letras y las haria ilegibles.

        La holgura es la del redondeo a pixeles enteros: 63 x 0.455 sale 28.67 y
        se guarda como 29. Aplastar solo el ancho daria 380/46 = 8.3, que esta
        lejisimos de esta holgura y por eso la prueba lo cazaria.
        """
        recorte = numpy.zeros((63, 835), dtype=numpy.uint8)
        escalada = escalar_para_caber(recorte, ALTO_DE_LA_TIRA_PX, ANCHO_DE_LA_TIRA_PX)
        self.assertAlmostEqual(
            escalada.shape[1] / escalada.shape[0], 835 / 63, delta=0.5
        )

    def test_el_camino_entero_trae_el_tope_puesto_sin_que_nadie_lo_pase(self):
        """Olvidar el tope era el fallo; por eso viene por defecto."""
        imagen = numpy.full((1400, 2705), 240, dtype=numpy.uint8)
        banda = banda_en_fracciones(Rectangulo(200, 600, 1035, 642), 2705, 1400)
        bytes_pgm = tira_de_la_banda(imagen, banda)
        ancho = int(bytes_pgm.split(b"\n")[1].split(b" ")[0])
        self.assertLessEqual(ancho, ANCHO_DE_LA_TIRA_PX)

    def test_sin_banda_el_camino_entero_devuelve_nada(self):
        """Nada, para que quien dibuja ponga el rectangulo rayado con su motivo.

        Una tira en blanco pareceria un escaneo vacio —«el papel no tenia nada
        ahi»— y no lo es: significa que no se sabe donde mirar.
        """
        self.assertIsNone(tira_de_la_banda(_imagen_de_prueba(), None))


class LaVistaDelVisorDelDocumento(unittest.TestCase):
    """Recortar una REGION de la hoja y escalarla por un factor pedido.

    Es lo que el visor de la pantalla partida necesita y la tira no daba: la tira
    escala a un alto fijo de 46 px, y el visor escala a la escala del zoom del
    momento. El criterio, en Given/When/Then:

      DADO el visor con un zoom puesto,
      CUANDO hay que repintar,
      ENTONCES solo se reescala LA REGION VISIBLE y no la hoja entera —una hoja al
      tope de 3500 px son unos 9,5 MB de PGM y `PhotoImage` guarda otra copia—,
      Y lo que sale son los mismos pixeles del papel, sin filtrar nada.
    """

    def test_la_region_pedida_es_la_que_sale(self):
        imagen = _imagen_de_prueba()
        recorte = region_de_la_pagina(imagen, (100, 400, 300, 440))
        self.assertEqual(recorte.shape, (40, 200))

    def test_al_doble_de_escala_salen_el_doble_de_pixeles(self):
        recorte = region_de_la_pagina(_imagen_de_prueba(), (100, 400, 300, 440))
        escalada = escalar_por_factor(recorte, 2.0)
        self.assertEqual(escalada.shape, (80, 400))

    def test_a_la_mitad_de_escala_salen_la_mitad(self):
        recorte = region_de_la_pagina(_imagen_de_prueba(), (100, 400, 300, 440))
        self.assertEqual(escalar_por_factor(recorte, 0.5).shape, (20, 100))

    def test_una_region_que_se_sale_de_la_hoja_se_recorta_al_borde(self):
        """El encuadre ya acota, pero una region mal pasada no puede reventar."""
        recorte = region_de_la_pagina(_imagen_de_prueba(), (-50, -50, 5000, 5000))
        self.assertEqual(recorte.shape, (1400, 1000))

    def test_una_region_de_area_cero_no_devuelve_nada(self):
        self.assertIsNone(region_de_la_pagina(_imagen_de_prueba(), (100, 400, 100, 400)))

    def test_sin_imagen_no_hay_region(self):
        self.assertIsNone(region_de_la_pagina(None, (0, 0, 10, 10)))

    def test_el_camino_entero_devuelve_pgm_con_el_tamano_escalado(self):
        bytes_pgm = vista_de_la_region(_imagen_de_prueba(), (100, 400, 300, 440), 2.0)
        cabecera = bytes_pgm.split(b"\n")[1].split(b" ")
        self.assertEqual((int(cabecera[0]), int(cabecera[1])), (400, 80))

    def test_el_camino_entero_sin_imagen_devuelve_nada(self):
        """Es lo que hay cuando el PDF se movio: el visor dibuja su hueco rayado."""
        self.assertIsNone(vista_de_la_region(None, (0, 0, 10, 10), 1.0))

    def test_no_se_filtra_ni_se_aclara_nada(self):
        """Regla permanente 1: lo que se ve es lo que hay en el papel.

        La franja oscura vale 30 y el fondo 240. Despues de escalar tienen que
        seguir valiendo 30 y 240 y ningun valor intermedio inventado por un
        promediado, porque un promediado sobre un escaneo es un filtro.
        """
        imagen = _imagen_de_prueba()
        escalada = escalar_por_factor(region_de_la_pagina(imagen, (0, 0, 1000, 1400)), 0.37)
        self.assertEqual(sorted(set(escalada.flatten().tolist())), [30, 240])

    def test_un_factor_que_deja_la_region_en_nada_devuelve_al_menos_un_pixel(self):
        """Un alto de 0 px no lo acepta `PhotoImage`; un pixel si."""
        recorte = region_de_la_pagina(_imagen_de_prueba(), (100, 400, 300, 440))
        escalada = escalar_por_factor(recorte, 0.001)
        self.assertGreaterEqual(escalada.shape[0], 1)
        self.assertGreaterEqual(escalada.shape[1], 1)

    def test_un_factor_de_cero_o_negativo_no_devuelve_nada(self):
        recorte = region_de_la_pagina(_imagen_de_prueba(), (100, 400, 300, 440))
        self.assertIsNone(escalar_por_factor(recorte, 0))
        self.assertIsNone(escalar_por_factor(recorte, -1))


if __name__ == "__main__":
    unittest.main()
