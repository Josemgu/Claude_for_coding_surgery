"""Pruebas de alta, lectura y actualizacion.

El criterio 1 del pase: un caso con tres personas se da de alta y se relee
IDENTICO campo a campo.
"""

import sqlite3
import unittest

from datos.repositorio import (
    actualizar_datos_de_persona,
    actualizar_datos_del_caso,
    alta_de_caso,
    alta_de_persona,
    leer_caso_por_numero,
    leer_personas_del_caso,
)
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal, caso_de_ejemplo, personas_de_ejemplo


class PruebaDeIdaYVuelta(PruebaConBaseTemporal):

    def setUp(self):
        super().setUp()
        self.datos_del_caso = caso_de_ejemplo()
        self.resultado = alta_de_caso(self.conexion, **self.datos_del_caso)
        for persona in personas_de_ejemplo():
            alta_de_persona(self.conexion, self.resultado.id, **persona)

    def test_el_caso_vuelve_identico_campo_a_campo(self):
        leido = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertIsNotNone(leido)
        for campo, esperado in self.datos_del_caso.items():
            self.assertEqual(esperado, leido[campo], f"campo {campo}")

    def test_las_tres_personas_vuelven_identicas_campo_a_campo(self):
        leidas = leer_personas_del_caso(self.conexion, self.resultado.id)
        esperadas = personas_de_ejemplo()
        self.assertEqual(3, len(leidas))
        for esperada, leida in zip(esperadas, leidas):
            for campo, valor in esperada.items():
                self.assertEqual(valor, leida[campo], f"campo {campo}")

    def test_el_alta_del_caso_no_devolvio_avisos(self):
        self.assertEqual((), self.resultado.avisos)

    def test_las_personas_vuelven_en_el_orden_del_formulario(self):
        leidas = leer_personas_del_caso(self.conexion, self.resultado.id)
        self.assertEqual([1, 2, 3], [fila["fila_formulario"] for fila in leidas])

    def test_un_numero_de_caso_que_no_existe_devuelve_nada(self):
        self.assertIsNone(leer_caso_por_numero(self.conexion, "ZZZZ9999"))


class PruebaDeRechazosEnElAlta(PruebaConBaseTemporal):

    def test_un_mrn_de_diez_digitos_se_rechaza_y_no_deja_fila(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        with self.assertRaises(ErrorDeValidacion):
            alta_de_persona(self.conexion, resultado.id, mrn="055-1111-385", nombre="Quien sea")
        self.assertEqual(0, self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0])

    def test_un_numero_de_caso_de_tres_letras_se_rechaza_y_no_deja_fila(self):
        datos = caso_de_ejemplo()
        datos["numero_caso"] = "CAS2609"
        with self.assertRaises(ErrorDeValidacion):
            alta_de_caso(self.conexion, **datos)
        self.assertEqual(0, self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])

    def test_una_unidad_de_cinco_digitos_se_rechaza(self):
        datos = caso_de_ejemplo()
        datos["unidad_numero"] = "12345"
        with self.assertRaises(ErrorDeValidacion):
            alta_de_caso(self.conexion, **datos)

    def test_una_persona_sin_nombre_y_sin_mrn_se_rechaza(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        with self.assertRaises(ErrorDeValidacion):
            alta_de_persona(self.conexion, resultado.id)

    def test_un_numero_de_caso_repetido_YA_NO_se_rechaza(self):
        """Desde la version 12 el numero es un atributo, no la identidad del caso.

        Lo que identifica un caso es el documento del que salio. El numero son
        cuatro letras mas el ano y el mes: una unidad y un mes, no una familia.
        """
        primero = alta_de_caso(self.conexion, **caso_de_ejemplo())
        segundo = alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.assertNotEqual(primero.id, segundo.id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )

    def test_el_mismo_mrn_dos_veces_en_el_mismo_caso_se_rechaza(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        alta_de_persona(self.conexion, resultado.id, mrn="055-1111-3853", nombre="Uno")
        with self.assertRaises(sqlite3.IntegrityError):
            alta_de_persona(self.conexion, resultado.id, mrn="055-1111-3853", nombre="Otro")

    def test_dos_personas_sin_mrn_en_el_mismo_caso_si_caben(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        alta_de_persona(self.conexion, resultado.id, nombre="Uno")
        alta_de_persona(self.conexion, resultado.id, nombre="Otro")
        self.assertEqual(2, len(leer_personas_del_caso(self.conexion, resultado.id)))


class PruebaDelAvisoDelMesCruzado(PruebaConBaseTemporal):
    """Criterio 3 del pase: avisa, el motor NO rechaza."""

    def test_octubre_en_un_caso_2609_se_guarda_y_devuelve_aviso(self):
        datos = caso_de_ejemplo()
        datos["fecha_viaje"] = "2026-10-08"
        resultado = alta_de_caso(self.conexion, **datos)

        self.assertEqual(1, len(resultado.avisos))
        guardado = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertEqual("2026-10-08", guardado["fecha_viaje"])

    def test_septiembre_en_un_caso_2609_se_guarda_sin_aviso(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.assertEqual((), resultado.avisos)


class PruebaDeActualizacion(PruebaConBaseTemporal):

    def setUp(self):
        super().setUp()
        self.resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.persona_id = alta_de_persona(
            self.conexion, self.resultado.id, mrn="055-1111-3853", nombre="Nombre mal leido"
        )

    def test_actualizar_el_caso_cambia_los_tres_campos(self):
        avisos = actualizar_datos_del_caso(
            self.conexion,
            self.resultado.id,
            unidad_numero="654321",
            fecha_viaje="2026-09-20",
            estado_recomendacion="no_indicada",
        )
        leido = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertEqual("654321", leido["unidad_numero"])
        self.assertEqual("2026-09-20", leido["fecha_viaje"])
        self.assertEqual("no_indicada", leido["estado_recomendacion"])
        self.assertEqual((), avisos)

    def test_actualizar_a_un_mes_cruzado_avisa_y_guarda(self):
        avisos = actualizar_datos_del_caso(
            self.conexion, self.resultado.id, fecha_viaje="2026-10-01"
        )
        self.assertEqual(1, len(avisos))
        self.assertEqual("2026-10-01", leer_caso_por_numero(self.conexion, "CASP2609")["fecha_viaje"])

    def test_actualizar_el_caso_con_una_unidad_mala_se_rechaza_y_no_cambia_nada(self):
        with self.assertRaises(ErrorDeValidacion):
            actualizar_datos_del_caso(self.conexion, self.resultado.id, unidad_numero="12345")
        self.assertEqual("123456", leer_caso_por_numero(self.conexion, "CASP2609")["unidad_numero"])

    def test_actualizar_la_persona_cambia_nombre_y_mrn(self):
        actualizar_datos_de_persona(
            self.conexion, self.persona_id, nombre="Jose Miguel Anonimo", mrn="055-1111-3899"
        )
        leida = leer_personas_del_caso(self.conexion, self.resultado.id)[0]
        self.assertEqual("Jose Miguel Anonimo", leida["nombre"])
        self.assertEqual("055-1111-3899", leida["mrn"])

    def test_actualizar_la_persona_con_un_mrn_malo_se_rechaza_y_no_cambia_nada(self):
        with self.assertRaises(ErrorDeValidacion):
            actualizar_datos_de_persona(self.conexion, self.persona_id, mrn="05511113853")
        leida = leer_personas_del_caso(self.conexion, self.resultado.id)[0]
        self.assertEqual("055-1111-3853", leida["mrn"])


class PruebaDeVerificadoPorDefecto(PruebaConBaseTemporal):
    """Regla permanente 5: nada se marca verificado automaticamente."""

    def test_una_procedencia_recien_escrita_nace_sin_verificar(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.conexion.execute(
            "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen, confianza) "
            "VALUES (?, ?, ?, ?, ?)",
            ("casos", resultado.id, "numero_caso", "anotacion", 1.0),
        )
        fila = self.conexion.execute(
            "SELECT verificado, verificado_por, verificado_en FROM procedencia_campo"
        ).fetchone()
        self.assertEqual(0, fila["verificado"])
        self.assertIsNone(fila["verificado_por"])
        self.assertIsNone(fila["verificado_en"])

    def test_no_se_puede_marcar_verificado_sin_quien_ni_cuando(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen, verificado) "
                "VALUES (?, ?, ?, ?, ?)",
                ("casos", resultado.id, "numero_caso", "ocr", 1),
            )


if __name__ == "__main__":
    unittest.main()
