"""La prueba que no depende de ningun comando (FASE 1, criterio 3d).

Se da de alta un dato con `'); DROP TABLE casos; --` dentro y se comprueba que
las tablas siguen enteras y que el valor vuelve LITERAL, con sus comillas y su
punto y coma. El motor recibio la logica y los datos por separado.
"""

import unittest

from datos.esquema import TABLAS_DEL_ESQUEMA
from datos.repositorio import (
    actualizar_datos_de_persona,
    alta_de_caso,
    alta_de_persona,
    leer_caso_por_numero,
    leer_personas_del_caso,
)
from pruebas.comun import PruebaConBaseTemporal, caso_de_ejemplo

INTENTO_DE_INYECCION = "'); DROP TABLE casos; --"
INTENTO_EN_CADENA = "Robert'); DROP TABLE personas; SELECT * FROM companeros WHERE ''='"


def _tablas(conexion):
    filas = conexion.execute(
        "SELECT name FROM sqlite_master WHERE type = 'table' "
        "AND name NOT LIKE 'sqlite_%' ORDER BY name"
    ).fetchall()
    return tuple(fila[0] for fila in filas)


class PruebaDeInyeccionInerte(PruebaConBaseTemporal):

    def test_el_intento_en_el_nombre_de_una_persona_queda_inerte(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        alta_de_persona(self.conexion, resultado.id, nombre=INTENTO_DE_INYECCION)

        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), list(_tablas(self.conexion)))
        leida = leer_personas_del_caso(self.conexion, resultado.id)[0]
        self.assertEqual(INTENTO_DE_INYECCION, leida["nombre"])

    def test_el_intento_en_la_ruta_del_pdf_queda_inerte(self):
        datos = caso_de_ejemplo()
        datos["ruta_pdf"] = INTENTO_DE_INYECCION
        alta_de_caso(self.conexion, **datos)

        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), list(_tablas(self.conexion)))
        self.assertEqual(
            INTENTO_DE_INYECCION, leer_caso_por_numero(self.conexion, "CASP2609")["ruta_pdf"]
        )

    def test_el_intento_en_una_actualizacion_queda_inerte(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        persona_id = alta_de_persona(self.conexion, resultado.id, nombre="Nombre normal")
        actualizar_datos_de_persona(self.conexion, persona_id, nombre=INTENTO_EN_CADENA)

        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), list(_tablas(self.conexion)))
        leida = leer_personas_del_caso(self.conexion, resultado.id)[0]
        self.assertEqual(INTENTO_EN_CADENA, leida["nombre"])

    def test_el_intento_en_una_lectura_no_devuelve_nada_y_no_rompe_nada(self):
        alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.assertIsNone(leer_caso_por_numero(self.conexion, INTENTO_DE_INYECCION))
        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), list(_tablas(self.conexion)))
        self.assertEqual(1, self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])

    def test_el_valor_vuelve_con_sus_comillas_y_su_punto_y_coma(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        alta_de_persona(self.conexion, resultado.id, nombre=INTENTO_DE_INYECCION)
        leido = leer_personas_del_caso(self.conexion, resultado.id)[0]["nombre"]
        self.assertIn("'", leido)
        self.assertIn(";", leido)
        self.assertIn("--", leido)
        self.assertEqual(len(INTENTO_DE_INYECCION), len(leido))


if __name__ == "__main__":
    unittest.main()
