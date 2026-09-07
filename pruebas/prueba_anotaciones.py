"""Clasificacion de trazos: tachon, resaltador, y lo que no se interpreta.

Los colores y grosores de referencia estan MEDIDOS sobre los cuatro documentos
reales (ver la cabecera de `extraccion/anotaciones.py`). Aqui se prueba que la
clasificacion respeta esa medicion y, sobre todo, que un trazo que no encaja
NO se interpreta: se marca desconocido y punto.
"""

import unittest

from extraccion.anotaciones import (
    CLASE_DESCONOCIDA,
    CLASE_RESALTADOR,
    CLASE_TACHON,
    COLOR_DEL_RESALTADOR,
    COLOR_DEL_TACHON,
    GROSOR_DEL_RESALTADOR,
    GROSOR_DEL_TACHON,
    TIPO_TEXTO,
    TIPO_TRAZO,
    Anotacion,
    clasificar_trazo,
    contar_por_clase,
)


class PruebaDeLaClasificacionDeTrazos(unittest.TestCase):
    def test_el_rojo_fino_medido_es_un_tachon(self):
        self.assertEqual(clasificar_trazo(COLOR_DEL_TACHON, GROSOR_DEL_TACHON), CLASE_TACHON)

    def test_el_verde_grueso_medido_es_un_resaltador(self):
        self.assertEqual(
            clasificar_trazo(COLOR_DEL_RESALTADOR, GROSOR_DEL_RESALTADOR), CLASE_RESALTADOR
        )

    def test_el_verde_que_suponia_DECISIONES_md_tambien_entra(self):
        """`DECISIONES.md` decia (0.494, 0.765, 0) y lo medido es (0.4941, 0.7686, 0).

        La diferencia esta dentro de la tolerancia. Se prueba explicito para que
        la correccion del documento no rompa nada que ya funcionaba.
        """
        self.assertEqual(clasificar_trazo((0.494, 0.765, 0.0), 16.5), CLASE_RESALTADOR)

    def test_el_rojo_que_suponia_DECISIONES_md_tambien_entra(self):
        self.assertEqual(clasificar_trazo((0.890, 0.094, 0.176), 1.65), CLASE_TACHON)

    def test_un_azul_cualquiera_no_se_interpreta(self):
        self.assertEqual(clasificar_trazo((0.0, 0.0, 1.0), 1.65), CLASE_DESCONOCIDA)

    def test_un_trazo_sin_color_no_se_interpreta(self):
        self.assertEqual(clasificar_trazo(None, 1.65), CLASE_DESCONOCIDA)

    def test_un_trazo_sin_grosor_no_se_interpreta(self):
        self.assertEqual(clasificar_trazo(COLOR_DEL_TACHON, None), CLASE_DESCONOCIDA)

    def test_el_color_solo_no_basta_hace_falta_el_grosor(self):
        """El caso que hace dano si se afloja: rojo grueso NO es un tachon.

        Si bastara el color, alguien que resaltara en rojo con un rotulador
        grueso veria como el sistema le anula un campo que estaba bien.
        """
        self.assertEqual(clasificar_trazo(COLOR_DEL_TACHON, 16.5), CLASE_DESCONOCIDA)

    def test_el_grosor_solo_no_basta_hace_falta_el_color(self):
        self.assertEqual(clasificar_trazo((0.1, 0.1, 0.1), GROSOR_DEL_TACHON), CLASE_DESCONOCIDA)

    def test_el_gris_no_se_confunde_con_ninguna_de_las_dos_familias(self):
        for grosor in (GROSOR_DEL_TACHON, GROSOR_DEL_RESALTADOR):
            with self.subTest(grosor=grosor):
                self.assertEqual(clasificar_trazo((0.5, 0.5, 0.5), grosor), CLASE_DESCONOCIDA)

    def test_un_color_de_cuatro_canales_no_se_interpreta(self):
        """CMYK en `/C` es legal en PDF y no es ninguna de las dos familias."""
        self.assertEqual(clasificar_trazo((0.0, 0.9, 0.9, 0.1), 1.65), CLASE_DESCONOCIDA)

    def test_la_tolerancia_de_grosor_no_estira_hasta_la_otra_familia(self):
        """1.65 y 16.5 se separan en un factor 10: las ventanas no se tocan."""
        self.assertEqual(clasificar_trazo(COLOR_DEL_TACHON, 2.5), CLASE_DESCONOCIDA)
        self.assertEqual(clasificar_trazo(COLOR_DEL_RESALTADOR, 10.0), CLASE_DESCONOCIDA)


class PruebaDelResumenPorClase(unittest.TestCase):
    def _trazo(self, clase):
        return Anotacion(
            tipo=TIPO_TRAZO,
            clase=clase,
            texto=None,
            rectangulo_pdf=(0.0, 0.0, 1.0, 1.0),
            color=None,
            grosor=None,
        )

    def _texto(self):
        return Anotacion(
            tipo=TIPO_TEXTO,
            clase=None,
            texto="lo que sea",
            rectangulo_pdf=(0.0, 0.0, 1.0, 1.0),
            color=None,
            grosor=None,
        )

    def test_cuenta_cada_cosa_en_su_casilla(self):
        anotaciones = [
            self._texto(),
            self._texto(),
            self._trazo(CLASE_TACHON),
            self._trazo(CLASE_RESALTADOR),
            self._trazo(CLASE_DESCONOCIDA),
            self._trazo(CLASE_TACHON),
        ]
        self.assertEqual(
            contar_por_clase(anotaciones),
            {TIPO_TEXTO: 2, CLASE_TACHON: 2, CLASE_RESALTADOR: 1, CLASE_DESCONOCIDA: 1},
        )

    def test_sin_anotaciones_devuelve_ceros_no_un_diccionario_vacio(self):
        """Quien lo lea tiene que poder decir «cero desconocidos», no «no hay dato»."""
        self.assertEqual(
            contar_por_clase([]),
            {TIPO_TEXTO: 0, CLASE_TACHON: 0, CLASE_RESALTADOR: 0, CLASE_DESCONOCIDA: 0},
        )


if __name__ == "__main__":
    unittest.main()
