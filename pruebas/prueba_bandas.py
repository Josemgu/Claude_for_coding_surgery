"""Localizar el ancla y su banda, con el caso de confusion que se midio de verdad.

El caso que da sentido a todo este modulo: en una pagina real el OCR leyo la
etiqueta «Date traveling home from the temple» como «Date traveling home fror the
temple». Esa lectura se parece un 0.77 a «Date traveling to the temple», que es
OTRA fila del formulario. Sin la regla de la etiqueta rival, el sistema habria
guardado la fecha de VUELTA como fecha de IDA — y la fecha de ida es la que
decide si un caso sale avisado en el calendario.
"""

import unittest
from collections import namedtuple

from extraccion.bandas import (
    FACTOR_DE_ALTO_DE_LA_BANDA,
    PARECIDO_MINIMO_DEL_ANCLA,
    banda_de_valor,
    esta_en_la_banda,
    lineas_en_la_banda,
    localizar_ancla,
    parecido,
)
from extraccion.geometria import Rectangulo

LineaFalsa = namedtuple("LineaFalsa", ("texto", "rectangulo", "confianza"))

ETIQUETA_DE_IDA = "Date traveling to the temple"
ETIQUETA_DE_VUELTA = "Date traveling home from the temple"


def linea(texto, y0=1000.0, y1=1050.0, x0=160.0, x1=570.0, confianza=0.99):
    return LineaFalsa(texto, Rectangulo(x0, y0, x1, y1), confianza)


class PruebaDelParecido(unittest.TestCase):
    def test_iguales_dan_uno(self):
        self.assertEqual(parecido("Full Name(s)", "Full Name(s)"), 1.0)

    def test_no_distingue_mayusculas(self):
        self.assertEqual(parecido("FULL NAME(S)", "full name(s)"), 1.0)

    def test_una_letra_cambiada_baja_poco(self):
        """«Upjt» por «Unit»: dos letras sobre 32. Es OCR real, no un supuesto."""
        medido = parecido("Ward/Branch Name and Upjt Number", "Ward/Branch Name and Unit Number")
        self.assertGreater(medido, PARECIDO_MINIMO_DEL_ANCLA)

    def test_cadenas_vacias(self):
        self.assertEqual(parecido("", ""), 1.0)
        self.assertEqual(parecido("", "algo"), 0.0)


class PruebaDeLaLocalizacionDelAncla(unittest.TestCase):
    def test_encuentra_el_ancla_exacta(self):
        lineas = [linea("Temple Name"), linea(ETIQUETA_DE_IDA, y0=2000.0, y1=2050.0)]
        encontrada = localizar_ancla(lineas, ETIQUETA_DE_IDA, (ETIQUETA_DE_VUELTA,))
        self.assertIsNotNone(encontrada)
        self.assertEqual(encontrada.texto, ETIQUETA_DE_IDA)

    def test_encuentra_el_ancla_con_una_errata_del_ocr(self):
        lineas = [linea("Ward/Branch Name and Upjt Number")]
        encontrada = localizar_ancla(lineas, "Ward/Branch Name and Unit Number")
        self.assertIsNotNone(encontrada)

    def test_no_confunde_la_fecha_de_vuelta_con_la_de_ida(self):
        """El caso medido. Sin la etiqueta rival, esta prueba falla.

        «Date traveling home fror the temple» se parece 0.77 a la etiqueta de ida
        —por encima de nada— pero se parece MUCHO mas a la de vuelta. Gana la de
        vuelta, asi que como ancla de ida no vale.
        """
        lineas = [linea("Date traveling home fror the temple")]
        self.assertIsNone(localizar_ancla(lineas, ETIQUETA_DE_IDA, (ETIQUETA_DE_VUELTA,)))

    def test_las_dos_defensas_contra_la_confusion_son_distintas(self):
        """Hay DOS protecciones y esta prueba separa lo que hace cada una.

        Medido: «Date traveling home fror the temple» se parece 0.77 a la
        etiqueta de ida. Con el umbral en 0.85 ya no pasa, asi que el umbral solo
        basta para ESTE caso. Pero el umbral es un numero que alguien puede
        aflojar; la regla de la etiqueta rival no depende de el, y es la que
        sigue protegiendo si se afloja. Aqui se comprueba justo eso.
        """
        lineas = [linea("Date traveling home fror the temple")]
        # Con un umbral permisivo —0.75 era la primera eleccion natural— el
        # parecido basta y la etiqueta equivocada entra.
        self.assertIsNotNone(localizar_ancla(lineas, ETIQUETA_DE_IDA, (), parecido_minimo=0.75))
        # Con el mismo umbral permisivo, la etiqueta rival la sigue rechazando.
        self.assertIsNone(
            localizar_ancla(lineas, ETIQUETA_DE_IDA, (ETIQUETA_DE_VUELTA,), parecido_minimo=0.75)
        )

    def test_texto_que_no_se_parece_a_nada_no_devuelve_ancla(self):
        lineas = [linea("Round trip travel from airport to temple 80.00")]
        self.assertIsNone(localizar_ancla(lineas, ETIQUETA_DE_IDA, (ETIQUETA_DE_VUELTA,)))

    def test_sin_lineas_no_hay_ancla(self):
        self.assertIsNone(localizar_ancla([], ETIQUETA_DE_IDA))

    def test_de_dos_parecidas_gana_la_mas_parecida(self):
        lineas = [linea("Date traveling to the templ", y0=1000.0), linea(ETIQUETA_DE_IDA, y0=2000.0)]
        encontrada = localizar_ancla(lineas, ETIQUETA_DE_IDA)
        self.assertEqual(encontrada.rectangulo.y0, 2000.0)


class PruebaDeLaBandaDeValor(unittest.TestCase):
    def test_la_banda_cuelga_por_debajo_del_ancla_nunca_por_encima(self):
        ancla = Rectangulo(x0=160.0, y0=1000.0, x1=570.0, y1=1050.0)
        banda = banda_de_valor(ancla)
        self.assertEqual(banda.y0, 1050.0)
        self.assertGreater(banda.y1, banda.y0)
        self.assertAlmostEqual(banda.y1, 1050.0 + 50.0 * FACTOR_DE_ALTO_DE_LA_BANDA)

    def test_la_banda_es_mas_ancha_que_el_ancla_por_los_dos_lados(self):
        """El valor suele desbordar su etiqueta, y a veces empieza a su izquierda."""
        ancla = Rectangulo(x0=160.0, y0=1000.0, x1=570.0, y1=1050.0)
        banda = banda_de_valor(ancla)
        self.assertLess(banda.x0, ancla.x0)
        self.assertGreater(banda.x1, ancla.x1)

    def test_un_tachon_de_la_otra_columna_no_entra_en_la_banda(self):
        """El formulario tiene dos columnas: la altura sola no basta.

        Sin la comprobacion horizontal, un tachon sobre la fecha de VUELTA
        —misma altura, columna derecha— anularia la fecha de IDA.
        """
        ancla = Rectangulo(x0=160.0, y0=1000.0, x1=570.0, y1=1050.0)
        banda = banda_de_valor(ancla)
        tachon_de_la_otra_columna = Rectangulo(x0=2000.0, y0=1055.0, x1=2400.0, y1=1080.0)
        self.assertFalse(esta_en_la_banda(tachon_de_la_otra_columna, banda))

    def test_lo_que_cae_dentro_sale_ordenado_de_izquierda_a_derecha(self):
        ancla = Rectangulo(x0=160.0, y0=1000.0, x1=570.0, y1=1050.0)
        banda = banda_de_valor(ancla)
        derecha = linea("2027", y0=1055.0, y1=1085.0, x0=400.0, x1=500.0)
        izquierda = linea("March 14,", y0=1055.0, y1=1085.0, x0=170.0, x1=390.0)
        dentro = lineas_en_la_banda([derecha, izquierda], banda)
        self.assertEqual([l.texto for l in dentro], ["March 14,", "2027"])

    def test_una_linea_muy_por_debajo_no_pertenece_a_la_banda(self):
        ancla = Rectangulo(x0=160.0, y0=1000.0, x1=570.0, y1=1050.0)
        banda = banda_de_valor(ancla)
        lejana = linea("Associated Costs", y0=1400.0, y1=1450.0)
        self.assertEqual(lineas_en_la_banda([lejana], banda), [])


if __name__ == "__main__":
    unittest.main()
