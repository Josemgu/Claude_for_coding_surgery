"""FASE 7: contactos con el lider. Se anulan, no se borran.

⚠️ **Los criterios de esta fase son provisionales y `PENDIENTES.md` lo dice de si
mismo.** Estas pruebas comprueban lo que el pase pidio y lo que la tabla ya
declaraba; **no** comprueban una especificacion del dueno, porque no existe. Su
criterio 4 exige una entrada en `DECISIONES.md` antes de cerrar la fase, y estas
pruebas no la sustituyen.

Lo que si vale de aqui pase lo que pase: **no hay borrado**, y el conteo de la
tabla es el mismo antes y despues de anular.
"""

import unittest
from datetime import date

from datos.companeros import alta_de_companero
from datos.contactos import (
    MEDIOS_DE_CONTACTO,
    anular_contacto,
    contactos_del_caso,
    leer_contacto,
    registrar_contacto,
    ultimo_contacto_por_caso,
)
from datos.repositorio import alta_de_caso
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 12)


class BaseConUnCaso(PruebaConBaseTemporal):
    def setUp(self):
        super().setUp()
        self.caso_id = alta_de_caso(
            self.conexion, "CASP2609", fecha_viaje="2026-09-16"
        ).id
        self.companero_id = alta_de_companero(self.conexion, "Ana Pérez")

    def _contar(self):
        return self.conexion.execute("SELECT COUNT(*) FROM contactos").fetchone()[0]


class PruebaDeRegistro(BaseConUnCaso):
    """Criterio 1: el contacto queda en la base con su fecha y su resultado."""

    def test_un_contacto_registrado_se_puede_volver_a_leer(self):
        contacto_id = registrar_contacto(
            self.conexion,
            self.caso_id,
            "2026-09-10",
            medio="whatsapp",
            con_quien="Obispo Rodríguez",
            resultado="Dice que revisa la entrevista el domingo",
            contactado_por=self.companero_id,
            respondio=1,
        )

        contacto = leer_contacto(self.conexion, contacto_id)
        self.assertEqual("2026-09-10", contacto["fecha"])
        self.assertEqual("whatsapp", contacto["medio"])
        self.assertEqual("Obispo Rodríguez", contacto["con_quien"])
        self.assertIn("domingo", contacto["resultado"])
        self.assertEqual(1, contacto["respondio"])
        self.assertEqual(0, contacto["anulado"])
        self.assertIsNotNone(contacto["registrado_en"])

    def test_varios_contactos_por_caso_con_el_mas_reciente_arriba(self):
        for fecha in ("2026-09-05", "2026-09-10", "2026-09-08"):
            registrar_contacto(self.conexion, self.caso_id, fecha, medio="llamada")

        historial = contactos_del_caso(self.conexion, self.caso_id, hoy=HOY)

        self.assertEqual(
            ["2026-09-10", "2026-09-08", "2026-09-05"],
            [contacto["fecha"] for contacto in historial],
        )

    def test_el_historial_dice_cuantos_dias_han_pasado(self):
        registrar_contacto(self.conexion, self.caso_id, "2026-09-02", medio="correo")

        historial = contactos_del_caso(self.conexion, self.caso_id, hoy=HOY)

        self.assertEqual(10, historial[0]["dias_desde"])

    def test_trae_el_nombre_de_quien_contacto_sin_una_segunda_consulta(self):
        registrar_contacto(
            self.conexion, self.caso_id, "2026-09-10", contactado_por=self.companero_id
        )
        historial = contactos_del_caso(self.conexion, self.caso_id, hoy=HOY)
        self.assertEqual("Ana Pérez", historial[0]["nombre_del_companero"])

    def test_un_caso_sin_contactos_devuelve_una_lista_vacia_no_un_error(self):
        """La frase en espanol la pone la pantalla; aqui se comprueba que hay hueco."""
        self.assertEqual([], contactos_del_caso(self.conexion, self.caso_id, hoy=HOY))

    def test_los_cinco_medios_son_los_que_pidio_el_pase(self):
        self.assertEqual(
            ("whatsapp", "llamada", "correo", "presencial", "otro"), MEDIOS_DE_CONTACTO
        )

    def test_un_medio_inventado_no_se_guarda(self):
        with self.assertRaises(ErrorDeValidacion):
            registrar_contacto(self.conexion, self.caso_id, "2026-09-10", medio="paloma")

    def test_un_contacto_sin_fecha_no_se_guarda(self):
        with self.assertRaises(ErrorDeValidacion):
            registrar_contacto(self.conexion, self.caso_id, None)

    def test_una_fecha_que_no_existe_no_se_guarda(self):
        with self.assertRaises(ErrorDeValidacion):
            registrar_contacto(self.conexion, self.caso_id, "2026-02-31")

    def test_un_contacto_sobre_un_caso_que_no_existe_no_se_guarda(self):
        with self.assertRaises(ErrorDeValidacion):
            registrar_contacto(self.conexion, 9999, "2026-09-10")

    def test_respondio_admite_los_tres_estados_y_ninguno_mas(self):
        """1 respondió, 0 no respondió, nada todavía no se sabe."""
        for valor in (0, 1, None):
            contacto_id = registrar_contacto(
                self.conexion, self.caso_id, "2026-09-10", respondio=valor
            )
            self.assertEqual(valor, leer_contacto(self.conexion, contacto_id)["respondio"])
        with self.assertRaises(ErrorDeValidacion):
            registrar_contacto(self.conexion, self.caso_id, "2026-09-10", respondio=7)

    def test_un_contacto_no_marca_nada_como_verificado(self):
        """`PENDIENTES.md`, FASE 7: un contacto no verifica nada."""
        registrar_contacto(
            self.conexion, self.caso_id, "2026-09-10", contactado_por=self.companero_id
        )
        verificados = self.conexion.execute(
            "SELECT COUNT(*) FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()[0]
        self.assertEqual(0, verificados)


class PruebaDeAnulacion(BaseConUnCaso):
    """Criterio 3: anular deja el registro visible con su motivo y su fecha."""

    def test_anular_no_borra_la_fila(self):
        contacto_id = registrar_contacto(self.conexion, self.caso_id, "2026-09-10")
        antes = self._contar()

        self.assertTrue(
            anular_contacto(self.conexion, contacto_id, "Me equivoqué de caso")
        )

        self.assertEqual(antes, self._contar())
        contacto = leer_contacto(self.conexion, contacto_id)
        self.assertEqual(1, contacto["anulado"])
        self.assertEqual("Me equivoqué de caso", contacto["motivo_anulacion"])
        self.assertIsNotNone(contacto["anulado_en"])

    def test_el_anulado_sigue_saliendo_en_el_historial(self):
        contacto_id = registrar_contacto(self.conexion, self.caso_id, "2026-09-10")
        anular_contacto(self.conexion, contacto_id, "Me equivoqué de caso")

        historial = contactos_del_caso(self.conexion, self.caso_id, hoy=HOY)

        self.assertEqual(1, len(historial))
        self.assertEqual(1, historial[0]["anulado"])
        self.assertEqual("Me equivoqué de caso", historial[0]["motivo_anulacion"])

    def test_no_se_puede_anular_sin_escribir_por_que(self):
        contacto_id = registrar_contacto(self.conexion, self.caso_id, "2026-09-10")
        with self.assertRaises(ErrorDeValidacion):
            anular_contacto(self.conexion, contacto_id, "   ")
        self.assertEqual(0, leer_contacto(self.conexion, contacto_id)["anulado"])

    def test_anular_dos_veces_no_mueve_el_motivo_de_la_primera(self):
        contacto_id = registrar_contacto(self.conexion, self.caso_id, "2026-09-10")
        anular_contacto(self.conexion, contacto_id, "El primero")

        self.assertFalse(anular_contacto(self.conexion, contacto_id, "El segundo"))

        self.assertEqual(
            "El primero", leer_contacto(self.conexion, contacto_id)["motivo_anulacion"]
        )

    def test_no_hay_ninguna_funcion_de_borrado_en_el_modulo(self):
        import datos.contactos as modulo

        sospechosas = [
            nombre
            for nombre in dir(modulo)
            if not nombre.startswith("_")
            and ("borrar" in nombre or "eliminar" in nombre or "delete" in nombre)
        ]
        self.assertEqual([], sospechosas)


class PruebaDelUltimoContacto(BaseConUnCaso):
    """Lo que la lista de pendientes necesita: hace cuantos dias y si respondio."""

    def test_un_caso_sin_contactos_no_aparece(self):
        """No aparecer es distinto de aparecer con cero días: no se ha llamado nunca."""
        self.assertEqual({}, ultimo_contacto_por_caso(self.conexion, HOY))

    def test_devuelve_el_mas_reciente_de_cada_caso(self):
        registrar_contacto(self.conexion, self.caso_id, "2026-09-02", respondio=0)
        registrar_contacto(self.conexion, self.caso_id, "2026-09-11", respondio=1)
        otro = alta_de_caso(self.conexion, "CBSP2609").id
        registrar_contacto(self.conexion, otro, "2026-09-05", respondio=0)

        ultimos = ultimo_contacto_por_caso(self.conexion, HOY)

        self.assertEqual(2, len(ultimos))
        self.assertEqual("2026-09-11", ultimos[self.caso_id]["fecha"])
        self.assertEqual(1, ultimos[self.caso_id]["dias_desde"])
        self.assertEqual(1, ultimos[self.caso_id]["respondio"])
        self.assertEqual(7, ultimos[otro]["dias_desde"])

    def test_un_contacto_anulado_no_cuenta_como_gestion_hecha(self):
        antiguo = registrar_contacto(self.conexion, self.caso_id, "2026-09-02")
        reciente = registrar_contacto(self.conexion, self.caso_id, "2026-09-11")
        anular_contacto(self.conexion, reciente, "Era de otro caso")

        ultimos = ultimo_contacto_por_caso(self.conexion, HOY)

        self.assertEqual("2026-09-02", ultimos[self.caso_id]["fecha"])
        self.assertEqual(10, ultimos[self.caso_id]["dias_desde"])
        self.assertEqual(antiguo, ultimos[self.caso_id]["id"])

    def test_si_se_anulan_todos_el_caso_vuelve_a_no_tener_contactos(self):
        contacto_id = registrar_contacto(self.conexion, self.caso_id, "2026-09-02")
        anular_contacto(self.conexion, contacto_id, "Era de otro caso")

        self.assertEqual({}, ultimo_contacto_por_caso(self.conexion, HOY))

    def test_dos_contactos_del_mismo_dia_se_desempatan_por_el_orden_de_registro(self):
        registrar_contacto(self.conexion, self.caso_id, "2026-09-10", resultado="primero")
        segundo = registrar_contacto(
            self.conexion, self.caso_id, "2026-09-10", resultado="segundo"
        )

        ultimos = ultimo_contacto_por_caso(self.conexion, HOY)

        self.assertEqual(segundo, ultimos[self.caso_id]["id"])

    def test_una_fecha_futura_no_se_corrige_a_cero(self):
        """Si alguien tecleó el mes que viene, eso hay que verlo, no taparlo."""
        registrar_contacto(self.conexion, self.caso_id, "2026-09-20")

        ultimos = ultimo_contacto_por_caso(self.conexion, HOY)

        self.assertEqual(-8, ultimos[self.caso_id]["dias_desde"])


if __name__ == "__main__":
    unittest.main()
