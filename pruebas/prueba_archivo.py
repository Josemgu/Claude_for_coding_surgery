"""Archivar un caso: sale de las listas de trabajo y no se borra nada.

Cada prueba de aqui sale de un punto del criterio de aceptacion de la FASE 8, no
de leer el codigo que acaba de escribirse. Los dos primeros criterios, literales:

  1. «Archivar un caso: `SELECT archivado, fecha_archivado FROM ...` devuelve `1`
     y una fecha. El caso desaparece de las listas de trabajo y del bloque de
     viajes proximos.»
  2. «`SELECT COUNT(*)` sobre la tabla de casos devuelve el mismo numero antes y
     despues de archivar.»
"""

import unittest

from datos.archivo import (
    NADIE_LO_HA_DICHO,
    NO_PUDO_VIAJAR,
    SI_VIAJO,
    archivar_caso,
    casos_archivados,
    desarchivar_caso,
    esta_archivado,
    leer_resultado_del_viaje,
    personas_del_caso_con_su_viaje,
    registrar_resultado_del_viaje,
)
from datos.calendario import casos_que_viajan_pronto, vista_de_mes
from datos.pendientes import casos_pendientes_de_verificar
from datos.repositorio import alta_de_caso, alta_de_persona
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal

HOY = "2026-09-05"


class PruebaDeArchivar(PruebaConBaseTemporal):
    """Un caso archivado desaparece de las listas y sigue en la base."""

    def setUp(self):
        super().setUp()
        self.caso = alta_de_caso(
            self.conexion, "CASP2609", unidad_numero="123456", fecha_viaje="2026-09-08"
        ).id
        self.persona = alta_de_persona(
            self.conexion, self.caso, mrn="055-1111-3853", nombre="José Peña",
            fila_formulario=1,
        )
        self.otro = alta_de_caso(
            self.conexion, "CBSP2609", unidad_numero="123456", fecha_viaje="2026-09-09"
        ).id
        alta_de_persona(self.conexion, self.otro, nombre="María Anonimo", fila_formulario=1)

    def _cuantos_casos(self):
        return self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]

    def _cuantas_personas(self):
        return self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0]

    def test_archivar_deja_la_marca_y_la_fecha(self):
        """Criterio 1: la columna vale 1 y la fecha no esta vacia."""
        archivar_caso(self.conexion, self.caso)
        fila = self.conexion.execute(
            "SELECT archivado, fecha_archivado FROM casos WHERE id = ?", (self.caso,)
        ).fetchone()
        self.assertEqual(fila["archivado"], 1)
        self.assertIsNotNone(fila["fecha_archivado"])

    def test_el_caso_archivado_desaparece_de_las_dos_listas_de_trabajo(self):
        """Criterio 1: fuera del bloque de viajes proximos y de pendientes.

        ⚠️ El mes ya NO entra en esta prueba, y no porque estorbara: el dueno
        revirtio esa parte el 2026-09-03 —*«Lo que archivo debe verse en el
        calendario, debe decir archivado»*—. Lo comprueba la prueba de abajo.
        """
        archivar_caso(self.conexion, self.caso)
        proximos = {c["id"] for c in casos_que_viajan_pronto(self.conexion, HOY)}
        pendientes = {c["id"] for c in casos_pendientes_de_verificar(self.conexion)}
        self.assertNotIn(self.caso, proximos)
        self.assertNotIn(self.caso, pendientes)

    def test_el_caso_archivado_sigue_en_el_mes_y_marcado(self):
        """La otra mitad de la reversion: se archiva de verdad y aun asi se ve."""
        archivar_caso(self.conexion, self.caso)
        del_mes = {
            caso["id"]: caso
            for casos in vista_de_mes(self.conexion, 2026, 9).values()
            for caso in casos
        }
        self.assertIn(self.caso, del_mes)
        self.assertTrue(del_mes[self.caso]["esta_archivado"])

    def test_archivar_uno_no_se_lleva_por_delante_al_otro(self):
        """El caso que sigue vivo sigue en las listas. Si no, el filtro esta mal."""
        archivar_caso(self.conexion, self.caso)
        proximos = {c["id"] for c in casos_que_viajan_pronto(self.conexion, HOY)}
        self.assertIn(self.otro, proximos)

    def test_el_conteo_de_casos_y_personas_no_cambia(self):
        """Criterio 2: el mismo numero antes y despues. Aqui no se borra nada."""
        casos_antes, personas_antes = self._cuantos_casos(), self._cuantas_personas()
        archivar_caso(self.conexion, self.caso)
        self.assertEqual(self._cuantos_casos(), casos_antes)
        self.assertEqual(self._cuantas_personas(), personas_antes)

    def test_archivar_dos_veces_no_reescribe_la_fecha(self):
        """La fecha dice cuando se cerro el caso; un segundo clic no la pisa."""
        primera = archivar_caso(self.conexion, self.caso)["fecha_archivado"]
        segunda = archivar_caso(self.conexion, self.caso)["fecha_archivado"]
        self.assertEqual(primera, segunda)

    def test_desarchivar_lo_devuelve_a_las_listas(self):
        """Archivar por error tiene vuelta atras sin tocar la base a mano."""
        archivar_caso(self.conexion, self.caso)
        desarchivar_caso(self.conexion, self.caso)
        self.assertFalse(esta_archivado(self.conexion, self.caso))
        proximos = {c["id"] for c in casos_que_viajan_pronto(self.conexion, HOY)}
        self.assertIn(self.caso, proximos)

    def test_desarchivar_conserva_lo_anotado_del_viaje(self):
        """Lo que paso con el viaje no deja de ser cierto porque el caso vuelva."""
        registrar_resultado_del_viaje(
            self.conexion, self.persona, NO_PUDO_VIAJAR, "sin firma del obispo"
        )
        archivar_caso(self.conexion, self.caso)
        desarchivar_caso(self.conexion, self.caso)
        resultado = leer_resultado_del_viaje(self.conexion, self.persona)
        self.assertEqual(resultado["pudo_viajar"], NO_PUDO_VIAJAR)
        self.assertEqual(resultado["motivo_no_viajo"], "sin firma del obispo")

    def test_un_caso_que_no_existe_se_dice_por_su_nombre(self):
        """El error nombra el id que no esta, en español."""
        with self.assertRaises(ErrorDeValidacion) as fallo:
            archivar_caso(self.conexion, 9999)
        self.assertIn("9999", str(fallo.exception))

    def test_el_historico_cuenta_las_personas_que_no_viajaron(self):
        """La lista del historico dice de un vistazo cuantas fallaron."""
        registrar_resultado_del_viaje(
            self.conexion, self.persona, NO_PUDO_VIAJAR, "sin firma"
        )
        archivar_caso(self.conexion, self.caso)
        historico = casos_archivados(self.conexion)
        self.assertEqual(len(historico), 1)
        self.assertEqual(historico[0]["numero_caso"], "CASP2609")
        self.assertEqual(historico[0]["personas_que_no_viajaron"], 1)


class PruebaDelResultadoDelViaje(PruebaConBaseTemporal):
    """Quien viajo, quien no, y por que. Nada se rellena solo."""

    def setUp(self):
        super().setUp()
        caso = alta_de_caso(self.conexion, "CASP2609", fecha_viaje="2026-09-08").id
        self.persona = alta_de_persona(
            self.conexion, caso, mrn="055-1111-3853", nombre="José Peña",
            fila_formulario=1,
        )
        self.caso = caso

    def test_una_persona_nace_sin_que_nadie_haya_dicho_nada(self):
        """El valor de partida es «no consta», no «si viajo» ni «no pudo»."""
        resultado = leer_resultado_del_viaje(self.conexion, self.persona)
        self.assertIs(resultado["pudo_viajar"], NADIE_LO_HA_DICHO)
        self.assertIsNone(resultado["motivo_no_viajo"])

    def test_no_se_puede_decir_que_no_viajo_sin_escribir_por_que(self):
        """Una fila del reporte sin motivo no le sirve de nada a quien la lee."""
        with self.assertRaises(ErrorDeValidacion):
            registrar_resultado_del_viaje(self.conexion, self.persona, NO_PUDO_VIAJAR)

    def test_un_motivo_en_blanco_cuenta_como_no_haberlo_escrito(self):
        """Espacios sueltos no son un motivo: se rechaza igual que el vacio."""
        with self.assertRaises(ErrorDeValidacion):
            registrar_resultado_del_viaje(
                self.conexion, self.persona, NO_PUDO_VIAJAR, "    "
            )

    def test_decir_que_si_viajo_no_admite_motivo(self):
        """El motivo es de quien NO viajo. Pegado a un «sí» seria una contradiccion."""
        registrar_resultado_del_viaje(self.conexion, self.persona, SI_VIAJO, "lo que sea")
        self.assertIsNone(
            leer_resultado_del_viaje(self.conexion, self.persona)["motivo_no_viajo"]
        )

    def test_se_puede_corregir_lo_anotado(self):
        """Anotarlo en la persona equivocada tiene marcha atras."""
        registrar_resultado_del_viaje(
            self.conexion, self.persona, NO_PUDO_VIAJAR, "sin firma"
        )
        registrar_resultado_del_viaje(self.conexion, self.persona, NADIE_LO_HA_DICHO)
        resultado = leer_resultado_del_viaje(self.conexion, self.persona)
        self.assertIsNone(resultado["pudo_viajar"])
        self.assertIsNone(resultado["motivo_no_viajo"])

    def test_un_valor_que_no_es_ninguno_de_los_tres_se_rechaza(self):
        """2 no es una respuesta. El error dice cuales son las tres."""
        with self.assertRaises(ErrorDeValidacion) as fallo:
            registrar_resultado_del_viaje(self.conexion, self.persona, 2)
        self.assertIn("pudo_viajar", str(fallo.exception))

    def test_el_motor_tambien_lo_defiende_por_su_cuenta(self):
        """Saltandose la validacion, el `CHECK` del esquema para lo mismo.

        Es la ultima defensa y tiene que estar: si el dia de manana otra pantalla
        escribe la columna sin pasar por `datos/archivo.py`, esto sigue en pie.
        """
        import sqlite3

        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "UPDATE personas SET pudo_viajar = 0, motivo_no_viajo = NULL WHERE id = ?",
                (self.persona,),
            )

    def test_las_personas_del_caso_salen_con_lo_ya_anotado(self):
        """La pantalla que archiva las ensena con su respuesta, no en blanco."""
        registrar_resultado_del_viaje(self.conexion, self.persona, SI_VIAJO)
        personas = personas_del_caso_con_su_viaje(self.conexion, self.caso)
        self.assertEqual(len(personas), 1)
        self.assertEqual(personas[0]["pudo_viajar"], SI_VIAJO)


if __name__ == "__main__":
    unittest.main()
