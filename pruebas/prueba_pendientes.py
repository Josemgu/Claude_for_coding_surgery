"""La lista de pendientes de la FASE 5: lo que entro y nadie ha dado por bueno.

Las pruebas salen de lo que la lista promete —casos sin verificar, ordenados por
fecha de viaje ascendente, sin los archivados— y no de leer la consulta.
"""

import unittest

from datos.pendientes import casos_pendientes_de_verificar
from datos.procedencia import guardar_procedencia_de_campo, marcar_campo_verificado
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal


class PruebaDeLosPendientes(PruebaConBaseTemporal):
    def setUp(self):
        super().setUp()
        self.companero_id = self.conexion.execute(
            "INSERT INTO companeros (nombre, creado_en) VALUES (?, ?)",
            ("Miguel", "2026-09-01 10:00:00"),
        ).lastrowid

    def alta(self, numero_caso, fecha_viaje):
        return alta_de_caso(
            self.conexion, numero_caso, unidad_numero="123456", fecha_viaje=fecha_viaje
        ).id

    def con_procedencia(self, tabla, registro_id, campo, verificado=False):
        guardar_procedencia_de_campo(self.conexion, tabla, registro_id, campo, "ocr", 0.9)
        if verificado:
            marcar_campo_verificado(
                self.conexion, tabla, registro_id, campo, self.companero_id
            )

    def numeros(self, casos):
        return [caso["numero_caso"] for caso in casos]

    def test_con_la_base_vacia_no_hay_nada_pendiente(self):
        self.assertEqual([], casos_pendientes_de_verificar(self.conexion))

    def test_un_caso_sin_ninguna_procedencia_esta_pendiente(self):
        """Cero campos sin verificar NO es lo mismo que todo verificado."""
        self.alta("AAAA2609", "2026-09-08")

        pendientes = casos_pendientes_de_verificar(self.conexion)

        self.assertEqual(["AAAA2609"], self.numeros(pendientes))
        self.assertTrue(pendientes[0]["sin_ninguna_lectura"])
        self.assertEqual(0, pendientes[0]["campos"])
        self.assertEqual(0, pendientes[0]["campos_pendientes"])

    def test_un_caso_a_medio_verificar_sigue_pendiente_y_dice_cuanto_falta(self):
        caso_id = self.alta("AAAA2609", "2026-09-08")
        self.con_procedencia("casos", caso_id, "unidad_numero", verificado=True)
        self.con_procedencia("casos", caso_id, "fecha_viaje")

        pendientes = casos_pendientes_de_verificar(self.conexion)

        self.assertEqual(["AAAA2609"], self.numeros(pendientes))
        self.assertEqual(2, pendientes[0]["campos"])
        self.assertEqual(1, pendientes[0]["campos_verificados"])
        self.assertEqual(1, pendientes[0]["campos_pendientes"])
        self.assertFalse(pendientes[0]["sin_ninguna_lectura"])

    def test_un_caso_con_todo_verificado_sale_de_la_lista(self):
        caso_id = self.alta("AAAA2609", "2026-09-08")
        persona_id = alta_de_persona(self.conexion, caso_id, nombre="Jose Miguel Anonimo")
        self.con_procedencia("casos", caso_id, "fecha_viaje", verificado=True)
        self.con_procedencia("personas", persona_id, "nombre", verificado=True)

        self.assertEqual([], casos_pendientes_de_verificar(self.conexion))

    def test_un_campo_de_una_persona_sin_verificar_deja_el_caso_pendiente(self):
        """Lo del caso puede estar entero y aun asi faltar lo de la gente."""
        caso_id = self.alta("AAAA2609", "2026-09-08")
        persona_id = alta_de_persona(self.conexion, caso_id, nombre="Jose Miguel Anonimo")
        self.con_procedencia("casos", caso_id, "fecha_viaje", verificado=True)
        self.con_procedencia("personas", persona_id, "mrn")

        pendientes = casos_pendientes_de_verificar(self.conexion)

        self.assertEqual(["AAAA2609"], self.numeros(pendientes))
        self.assertEqual(1, pendientes[0]["campos_pendientes"])
        self.assertEqual(1, pendientes[0]["personas"])

    def test_la_procedencia_de_otro_caso_no_se_cuela_en_este(self):
        """`procedencia_campo` es polimorfica y el motor no la puede defender."""
        primero = self.alta("AAAA2609", "2026-09-08")
        segundo = self.alta("BBBB2609", "2026-09-09")
        self.con_procedencia("casos", primero, "fecha_viaje", verificado=True)
        self.con_procedencia("casos", segundo, "fecha_viaje")

        pendientes = casos_pendientes_de_verificar(self.conexion)

        self.assertEqual(["BBBB2609"], self.numeros(pendientes))
        self.assertEqual(1, pendientes[0]["campos"])

    def test_una_persona_con_el_mismo_id_que_un_caso_no_se_mezcla(self):
        """El par (tabla, registro_id) tiene que mirarse entero, no solo el id."""
        caso_id = self.alta("AAAA2609", "2026-09-08")
        persona_id = alta_de_persona(self.conexion, caso_id, nombre="Jose Miguel Anonimo")
        self.assertEqual(caso_id, persona_id, "los dos primeros ids valen 1")

        self.con_procedencia("casos", caso_id, "fecha_viaje", verificado=True)
        self.con_procedencia("personas", persona_id, "nombre")

        pendientes = casos_pendientes_de_verificar(self.conexion)

        self.assertEqual(2, pendientes[0]["campos"])
        self.assertEqual(1, pendientes[0]["campos_verificados"])

    def test_un_caso_archivado_no_aparece_aunque_le_falte_todo(self):
        caso_id = self.alta("AAAA2609", "2026-09-08")
        self.conexion.execute(
            "UPDATE casos SET archivado = 1, fecha_archivado = ? WHERE id = ?",
            ("2026-09-01 10:00:00", caso_id),
        )

        self.assertEqual([], casos_pendientes_de_verificar(self.conexion))

    def test_se_ordenan_por_fecha_de_viaje_ascendente(self):
        self.alta("CCCC2609", "2026-09-20")
        self.alta("AAAA2609", "2026-09-02")
        self.alta("BBBB2609", "2026-09-08")

        self.assertEqual(
            ["AAAA2609", "BBBB2609", "CCCC2609"],
            self.numeros(casos_pendientes_de_verificar(self.conexion)),
        )

    def test_los_casos_sin_fecha_de_viaje_van_al_final_y_no_al_principio(self):
        """Sin este orden explicito, SQLite pondria los `NULL` los primeros."""
        self.alta("ZZZZ2609", None)
        self.alta("AAAA2609", "2026-09-02")

        self.assertEqual(
            ["AAAA2609", "ZZZZ2609"],
            self.numeros(casos_pendientes_de_verificar(self.conexion)),
        )

    def test_dos_casos_del_mismo_dia_salen_en_un_orden_estable(self):
        self.alta("BBBB2609", "2026-09-08")
        self.alta("AAAA2609", "2026-09-08")

        self.assertEqual(
            ["AAAA2609", "BBBB2609"],
            self.numeros(casos_pendientes_de_verificar(self.conexion)),
        )


if __name__ == "__main__":
    unittest.main()
