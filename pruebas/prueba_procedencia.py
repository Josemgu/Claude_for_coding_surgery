"""Que la procedencia de un campo se puede guardar, releer y verificar.

Hasta ahora `procedencia_campo` tenia DDL y no tenia por donde escribirse: la
FASE 2 producia la procedencia entera en memoria y no habia donde ponerla.

Lo que estas pruebas derivan del esquema (`docs/ARQUITECTURA.md` §2.7) y de la
regla permanente 5:

  - una fila por (tabla, registro_id, campo), no dos;
  - el texto que leyo el OCR se conserva aunque el valor se corrigiera;
  - **nada queda verificado sin que conste quien y cuando.**
"""

import sqlite3
import unittest

from datos.procedencia import (
    ORIGENES_DE_CAMPO,
    TABLAS_CON_PROCEDENCIA,
    guardar_procedencia_de_campo,
    leer_procedencia_de_campo,
    leer_procedencia_del_registro,
    marcar_campo_verificado,
)
from datos.repositorio import alta_de_caso, alta_de_persona
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal, caso_de_ejemplo, personas_de_ejemplo


class PruebaConUnCasoYUnaPersona(PruebaConBaseTemporal):

    def setUp(self):
        super().setUp()
        self.caso_id = alta_de_caso(self.conexion, **caso_de_ejemplo()).id
        self.persona_id = alta_de_persona(
            self.conexion, self.caso_id, **personas_de_ejemplo()[0]
        )
        self.companero_id = self.conexion.execute(
            "INSERT INTO companeros (nombre, creado_en) VALUES ('Miguel', '2026-09-02 10:00:00')"
        ).lastrowid


class PruebaDeGuardarProcedencia(PruebaConUnCasoYUnaPersona):

    def test_se_guarda_y_se_relee_entera(self):
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.82, valor_ocr="08 Sep 2026",
        )
        leida = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual("ocr", leida["origen"])
        self.assertAlmostEqual(0.82, leida["confianza"])
        self.assertEqual("08 Sep 2026", leida["valor_ocr"])
        self.assertEqual(0, leida["verificado"])

    def test_una_anotacion_se_guarda_con_confianza_uno(self):
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "unidad_numero",
            origen="anotacion", confianza=1.0, valor_ocr="7000011",
        )
        leida = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "unidad_numero")
        self.assertEqual("anotacion", leida["origen"])
        self.assertEqual(1.0, leida["confianza"])

    def test_un_campo_vacio_se_guarda_sin_confianza(self):
        """`origen='vacio'` es un dato: dice que se miro y no habia nada."""
        guardar_procedencia_de_campo(
            self.conexion, "personas", self.persona_id, "mrn", origen="vacio"
        )
        leida = leer_procedencia_de_campo(self.conexion, "personas", self.persona_id, "mrn")
        self.assertEqual("vacio", leida["origen"])
        self.assertIsNone(leida["confianza"])

    def test_volver_a_guardar_el_mismo_campo_actualiza_y_no_duplica(self):
        guardar_procedencia_de_campo(
            self.conexion, "personas", self.persona_id, "nombre",
            origen="ocr", confianza=0.4, valor_ocr="Jose Migue1 0rtiz",
        )
        guardar_procedencia_de_campo(
            self.conexion, "personas", self.persona_id, "nombre",
            origen="manual", confianza=None, valor_ocr="Jose Migue1 0rtiz",
        )
        filas = leer_procedencia_del_registro(self.conexion, "personas", self.persona_id)
        self.assertEqual(1, len(filas))
        self.assertEqual("manual", filas[0]["origen"])
        self.assertEqual("Jose Migue1 0rtiz", filas[0]["valor_ocr"])

    def test_el_registro_devuelve_sus_campos_ordenados_por_nombre(self):
        for campo in ("nombre", "mrn"):
            guardar_procedencia_de_campo(
                self.conexion, "personas", self.persona_id, campo, origen="ocr", confianza=0.9
            )
        filas = leer_procedencia_del_registro(self.conexion, "personas", self.persona_id)
        self.assertEqual(["mrn", "nombre"], [fila["campo"] for fila in filas])

    def test_un_campo_sin_procedencia_devuelve_nada_y_no_levanta(self):
        self.assertIsNone(
            leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "ruta_pdf")
        )
        self.assertEqual([], leer_procedencia_del_registro(self.conexion, "casos", 999))

    def test_los_cuatro_origenes_del_esquema_se_admiten(self):
        """Control negativo del rechazo: si fallara alguno, el filtro estaria de mas."""
        for origen in ORIGENES_DE_CAMPO:
            with self.subTest(origen=origen):
                guardar_procedencia_de_campo(
                    self.conexion, "casos", self.caso_id, "campo_" + origen, origen=origen
                )
        self.assertEqual(
            len(ORIGENES_DE_CAMPO),
            len(leer_procedencia_del_registro(self.conexion, "casos", self.caso_id)),
        )

    def test_las_dos_tablas_del_esquema_se_admiten(self):
        for tabla, registro in (("casos", self.caso_id), ("personas", self.persona_id)):
            with self.subTest(tabla=tabla):
                guardar_procedencia_de_campo(
                    self.conexion, tabla, registro, "un_campo", origen="ocr", confianza=0.5
                )
        self.assertEqual(("casos", "personas"), TABLAS_CON_PROCEDENCIA)


class PruebaDeLoQueSeRechaza(PruebaConUnCasoYUnaPersona):
    """Lo que se rechaza, se rechaza ANTES del motor y con el campo nombrado."""

    def test_una_tabla_que_no_es_del_esquema_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            guardar_procedencia_de_campo(
                self.conexion, "companeros", 1, "nombre", origen="ocr"
            )
        self.assertIn("tabla", str(capturado.exception))

    def test_un_origen_inventado_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, "fecha_viaje", origen="adivinado"
            )
        self.assertIn("origen", str(capturado.exception))

    def test_una_confianza_fuera_de_rango_se_rechaza(self):
        for confianza in (-0.1, 1.5):
            with self.subTest(confianza=confianza):
                with self.assertRaises(ErrorDeValidacion):
                    guardar_procedencia_de_campo(
                        self.conexion, "casos", self.caso_id, "fecha_viaje",
                        origen="ocr", confianza=confianza,
                    )

    def test_una_confianza_que_no_es_numero_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, "fecha_viaje",
                origen="ocr", confianza="alta",
            )

    def test_un_nombre_de_campo_vacio_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            guardar_procedencia_de_campo(self.conexion, "casos", self.caso_id, "  ", origen="ocr")

    def test_un_registro_id_que_no_es_una_fila_se_rechaza(self):
        for registro_id in (0, -3, "1", None):
            with self.subTest(registro_id=registro_id):
                with self.assertRaises(ErrorDeValidacion):
                    guardar_procedencia_de_campo(
                        self.conexion, "casos", registro_id, "fecha_viaje", origen="ocr"
                    )

    def test_nada_de_lo_rechazado_llego_a_la_base(self):
        for intento in (
            ("companeros", 1, "nombre", "ocr"),
            ("casos", self.caso_id, "fecha_viaje", "adivinado"),
        ):
            with self.assertRaises(ErrorDeValidacion):
                guardar_procedencia_de_campo(
                    self.conexion, intento[0], intento[1], intento[2], origen=intento[3]
                )
        self.assertEqual(
            0, self.conexion.execute("SELECT COUNT(*) FROM procedencia_campo").fetchone()[0]
        )


class PruebaDeLaVerificacion(PruebaConUnCasoYUnaPersona):
    """Regla permanente 5: nada se marca como verificado automaticamente."""

    def setUp(self):
        super().setUp()
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.82, valor_ocr="08 Sep 2026",
        )

    def test_al_guardar_procedencia_el_campo_nace_sin_verificar(self):
        leida = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(0, leida["verificado"])
        self.assertIsNone(leida["verificado_por"])
        self.assertIsNone(leida["verificado_en"])

    def test_marcar_verificado_deja_quien_y_cuando(self):
        leida = marcar_campo_verificado(
            self.conexion, "casos", self.caso_id, "fecha_viaje", self.companero_id
        )
        self.assertEqual(1, leida["verificado"])
        self.assertEqual(self.companero_id, leida["verificado_por"])
        self.assertTrue(leida["verificado_en"])

    def test_no_se_puede_verificar_sin_decir_quien(self):
        with self.assertRaises(ErrorDeValidacion) as capturado:
            marcar_campo_verificado(
                self.conexion, "casos", self.caso_id, "fecha_viaje", None
            )
        self.assertIn("quién", str(capturado.exception))

    def test_no_se_puede_verificar_un_campo_que_nadie_leyo(self):
        with self.assertRaises(ErrorDeValidacion):
            marcar_campo_verificado(
                self.conexion, "casos", self.caso_id, "ruta_pdf", self.companero_id
            )

    def test_el_motor_rechaza_un_verificado_a_medias(self):
        """La ultima defensa: aunque alguien escriba SQL a mano, el CHECK no deja."""
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "UPDATE procedencia_campo SET verificado = 1 WHERE campo = 'fecha_viaje'"
            )

    def test_un_companero_que_no_existe_lo_rechaza_la_clave_foranea(self):
        with self.assertRaises(sqlite3.IntegrityError):
            marcar_campo_verificado(
                self.conexion, "casos", self.caso_id, "fecha_viaje", 9999
            )

    def test_volver_a_extraer_no_borra_la_firma_de_quien_verifico(self):
        """⚠️ Comportamiento elegido a falta de decision del dueno (P-5): la
        verificacion se conserva. Se prueba para que el dia que se decida otra cosa,
        cambiarlo rompa esta prueba y nadie lo cambie sin verlo."""
        marcar_campo_verificado(
            self.conexion, "casos", self.caso_id, "fecha_viaje", self.companero_id
        )
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.5, valor_ocr="otra lectura",
        )
        leida = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(1, leida["verificado"])
        self.assertEqual(self.companero_id, leida["verificado_por"])
        self.assertEqual("otra lectura", leida["valor_ocr"])


if __name__ == "__main__":
    unittest.main()
