"""Pruebas de las cuatro reglas de formato y del aviso del mes cruzado.

Cubren los criterios 4, 5 y 6 de la FASE 1. El criterio 6 se prueba tal como lo
decidio DECISIONES.md (P-2, 2026-09-02): la regla del mes cruzado es un AVISO,
no una pared del motor.
"""

import unittest

from datos.estados import ESTADOS_RECOMENDACION
from datos.validacion import (
    ErrorDeValidacion,
    avisos_de_caso,
    validar_estado_recomendacion,
    validar_fecha_viaje,
    validar_mrn,
    validar_numero_caso,
    validar_unidad_numero,
)


class PruebaDeNumeroDeCaso(unittest.TestCase):

    def test_cuatro_letras_y_cuatro_digitos_se_acepta(self):
        self.assertEqual("CASP2609", validar_numero_caso("CASP2609"))

    def test_tres_letras_y_cuatro_digitos_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_numero_caso("CAS2609")
        mensaje = str(capturado.exception)
        self.assertIn("numero_caso", mensaje)
        self.assertIn("4 letras mayúsculas", mensaje)

    def test_minusculas_se_rechazan(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_numero_caso("casp2609")

    def test_nueve_caracteres_se_rechazan(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_numero_caso("CASP26099")

    def test_vacio_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_numero_caso(None)


class PruebaDeMrn(unittest.TestCase):

    def test_once_digitos_en_patron_3_4_4_se_acepta(self):
        self.assertEqual("055-1111-3853", validar_mrn("055-1111-3853"))

    def test_una_cedula_terminada_en_letra_se_acepta(self):
        """El papel las trae, y son muchas (`DECISIONES.md`, 2026-09-04).

        Las formas y los casos de borde de este formato viven enteros en
        `pruebas/prueba_cedula_con_letra.py`. Aqui va solo la forma, para que quien
        lea este archivo vea las DOS que el validador admite y no solo una.
        """
        self.assertEqual("066-2222-133A", validar_mrn("066-2222-133A"))

    def test_diez_caracteres_se_rechazan_con_mensaje_que_nombra_el_campo(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_mrn("055-1111-385")
        mensaje = str(capturado.exception)
        self.assertIn("mrn", mensaje)
        self.assertIn("055-1111-3853", mensaje)
        self.assertIn("066-2222-133A", mensaje)

    def test_once_digitos_sin_guiones_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_mrn("05511113853")

    def test_nulo_se_acepta_porque_el_ocr_puede_no_haberlo_leido(self):
        self.assertIsNone(validar_mrn(None))


class PruebaDeUnidad(unittest.TestCase):

    def test_seis_digitos_se_acepta(self):
        self.assertEqual("123456", validar_unidad_numero("123456"))

    def test_siete_digitos_se_acepta_porque_el_papel_los_trae(self):
        """`7000011` sale de 4 de las 9 paginas reales (DECISIONES.md 2026-09-02).

        La regla vieja de seis digitos rechazaba este dato verdadero y dejaba el
        campo vacio en esas cuatro paginas.
        """
        self.assertEqual("7000011", validar_unidad_numero("7000011"))

    def test_cinco_digitos_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_unidad_numero("12345")
        self.assertIn("unidad_numero", str(capturado.exception))
        self.assertIn("6 o 7 dígitos", str(capturado.exception))

    def test_ocho_digitos_se_rechaza(self):
        """El borde de arriba tambien se prueba: 6 o 7 no significa «los que sean»."""
        with self.assertRaises(ErrorDeValidacion):
            validar_unidad_numero("70000117")

    def test_nulo_se_acepta(self):
        self.assertIsNone(validar_unidad_numero(None))


class PruebaDeFechaDeViaje(unittest.TestCase):

    def test_fecha_real_se_acepta(self):
        self.assertEqual("2026-09-08", validar_fecha_viaje("2026-09-08"))

    def test_fecha_con_forma_correcta_pero_inexistente_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_fecha_viaje("2026-02-31")
        self.assertIn("fecha_viaje", str(capturado.exception))

    def test_otro_formato_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_fecha_viaje("08/09/2026")

    def test_nulo_se_acepta(self):
        self.assertIsNone(validar_fecha_viaje(None))


class PruebaDelMesCruzado(unittest.TestCase):
    """DECISIONES.md P-2: avisa, no rechaza."""

    def test_septiembre_en_un_caso_2609_no_produce_aviso(self):
        self.assertEqual((), avisos_de_caso("CASP2609", "2026-09-08"))

    def test_octubre_en_un_caso_2609_produce_aviso_y_no_excepcion(self):
        avisos = avisos_de_caso("CASP2609", "2026-10-08")
        self.assertEqual(1, len(avisos))
        self.assertIn("2610", avisos[0])
        self.assertIn("2609", avisos[0])
        self.assertIn("fecha_viaje", avisos[0])

    def test_sin_fecha_no_hay_aviso(self):
        self.assertEqual((), avisos_de_caso("CASP2609", None))


class PruebaDeEstadoDeRecomendacion(unittest.TestCase):

    def test_la_lista_lleva_los_cuatro_valores_que_constan(self):
        """~~Los dos que constaban~~ — el dueno anadio los suyos el 2026-09-03.

        `completa` y `no_completa` son las palabras del dueno para el DOCUMENTO
        (DECISIONES.md, 2026-09-03); los dos primeros siguen siendo los de la
        propuesta del companero por PERSONA y no se han ido a ninguna parte.
        """
        self.assertEqual(
            ("no_indicada", "incompleta", "completa", "no_completa"),
            ESTADOS_RECOMENDACION,
        )

    def test_un_valor_de_la_lista_se_acepta(self):
        self.assertEqual("incompleta", validar_estado_recomendacion("incompleta"))

    def test_un_valor_fuera_de_la_lista_se_rechaza_nombrando_los_validos(self):
        """El valor de ejemplo cambia: «completa» ya es valido desde el 2026-09-03.

        Se usa `resuelta`, que es el nombre que el supervisor propuso y que el dueno
        sustituyo por sus propias palabras. Que siga rechazandose es lo que impide
        que ese nombre vuelva por la puerta de atras.
        """
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_estado_recomendacion("resuelta")
        mensaje = str(capturado.exception)
        self.assertIn("estado_recomendacion", mensaje)
        self.assertIn("no_indicada", mensaje)
        self.assertIn("incompleta", mensaje)
        self.assertIn("completa", mensaje)

    def test_nulo_se_acepta_porque_el_dueno_no_ha_dicho_los_otros_valores(self):
        self.assertIsNone(validar_estado_recomendacion(None))


if __name__ == "__main__":
    unittest.main()
