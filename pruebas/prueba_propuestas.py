"""Dos companeros sobre el mismo caso: lo que dijo el primero no desaparece.

El escenario que se prueba aqui es LITERALMENTE el que midio QA en la auditoria
final, con los mismos nombres y las mismas frases, para que se pueda comparar la
salida de antes con la de ahora sin traducir nada:

    A dice: «falta la firma del obispo»   -> incompleta
    B dice: «todo bien»                   -> no_indicada

Lo que se medio antes del arreglo (esta maquina, 2026-09-02):

    tras A: [('incompleta', 'A dice: falta la firma del obispo', 'Companero A')]
    tras B: [('no_indicada', 'B dice: todo bien',                'Companero B')]
    QUEDA RASTRO DE LO QUE DIJO A EN ALGUN SITIO DE LA BASE: 0 filas

Miguel solo veia a B, y lo de A no estaba en ninguna parte. Es la persona que
llega al templo y no entra.

⚠️ **Estas pruebas NO deciden si un caso puede llevarlo mas de una persona.** Eso
no lo ha dicho el dueno —`DECISIONES.md` no lo trata y `datos/asignaciones.py` lo
dice de si mismo— y no lo decide el programador. Lo que se prueba aqui es lo otro:
pase lo que pase con esa regla, **un dato escrito por una persona real no
desaparece sin que nadie lo sepa**.
"""

from datos.asignaciones import asignar_caso, companeros_del_caso
from datos.companeros import alta_de_companero, desactivar_companero
from datos.propuestas import (
    guardar_propuesta,
    propuesta_vigente,
)
from datos.repositorio import alta_de_caso, alta_de_persona
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal

NOTA_DE_A = "A dice: falta la firma del obispo"
NOTA_DE_B = "B dice: todo bien"


class PruebaDeDosCompanerosSobreElMismoCaso(PruebaConBaseTemporal):
    """El escenario de la auditoria final de QA, paso por paso."""

    def setUp(self):
        super().setUp()
        self.caso_id, _ = alta_de_caso(
            self.conexion, numero_caso="CASP2609", fecha_viaje="2026-09-08"
        )
        self.persona_id = alta_de_persona(
            self.conexion,
            self.caso_id,
            mrn="055-1111-3853",
            nombre="Persona Una",
            fila_formulario=1,
        )
        self.a = alta_de_companero(self.conexion, "Companero A")
        self.b = alta_de_companero(self.conexion, "Companero B")

    def _lo_guardado(self):
        """Las tres columnas que QA leyo: estado, nota y de quien es."""
        fila = self.conexion.execute(
            "SELECT p.estado_propuesto, p.nota_companero, co.nombre "
            "FROM personas p LEFT JOIN companeros co ON co.id = p.propuesto_por "
            "WHERE p.id = ?",
            (self.persona_id,),
        ).fetchone()
        return tuple(fila)

    def _rastro_de_a(self):
        """Las filas de la base donde todavia se lee lo que escribio A."""
        return self.conexion.execute(
            "SELECT COUNT(*) FROM personas WHERE nota_companero = ?", (NOTA_DE_A,)
        ).fetchone()[0]

    def test_el_motor_sigue_admitiendo_dos_asignaciones_vivas_sobre_un_caso(self):
        """La premisa de QA, tal cual, para que el dia que cambie se vea aqui.

        Esta prueba NO afirma que este bien: afirma cual es el estado de hoy. Si
        el dueno decide que un caso lo lleva una sola persona, esta prueba falla y
        obliga a mirar este archivo, que es exactamente lo que tiene que pasar.
        """
        asignar_caso(self.conexion, self.caso_id, self.a)
        asignar_caso(self.conexion, self.caso_id, self.b)

        self.assertEqual(2, len(companeros_del_caso(self.conexion, self.caso_id)))

    def test_lo_que_dijo_el_primero_no_lo_pisa_el_segundo(self):
        """El escenario entero: A escribe, B llega detras, y A sigue ahi."""
        guardar_propuesta(self.conexion, self.persona_id, "incompleta", NOTA_DE_A, self.a)
        self.assertEqual(("incompleta", NOTA_DE_A, "Companero A"), self._lo_guardado())

        with self.assertRaises(ErrorDeValidacion):
            guardar_propuesta(self.conexion, self.persona_id, "no_indicada", NOTA_DE_B, self.b)

        self.assertEqual(("incompleta", NOTA_DE_A, "Companero A"), self._lo_guardado())
        self.assertEqual(1, self._rastro_de_a())

    def test_el_aviso_dice_quien_lo_escribio_y_que_dijo(self):
        """Un rechazo que no dice que hay debajo obliga a ir a buscarlo a mano."""
        guardar_propuesta(self.conexion, self.persona_id, "incompleta", NOTA_DE_A, self.a)

        with self.assertRaises(ErrorDeValidacion) as recogido:
            guardar_propuesta(self.conexion, self.persona_id, "no_indicada", NOTA_DE_B, self.b)

        aviso = str(recogido.exception)
        self.assertIn("Companero A", aviso)
        self.assertIn(NOTA_DE_A, aviso)
        self.assertIn("incompleta", aviso)

    def test_el_mismo_companero_si_escribe_encima_de_lo_suyo(self):
        """Cargar dos veces el Excel de A es lo normal y no puede fallar.

        Es lo que `paquete/reconciliacion.py` promete de si mismo: «reconciliar dos
        veces el mismo archivo no duplica nada». Corregir la nota y volver a
        mandarla tampoco puede quedar bloqueado por el arreglo de arriba.
        """
        guardar_propuesta(self.conexion, self.persona_id, "incompleta", NOTA_DE_A, self.a)
        guardar_propuesta(self.conexion, self.persona_id, "no_indicada", "A se corrige", self.a)

        self.assertEqual(("no_indicada", "A se corrige", "Companero A"), self._lo_guardado())

    def test_sobre_una_persona_sin_propuesta_escribe_cualquiera(self):
        """Lo normal: nadie ha dicho nada todavia y llega el primer Excel."""
        guardar_propuesta(self.conexion, self.persona_id, "no_indicada", NOTA_DE_B, self.b)

        self.assertEqual(("no_indicada", NOTA_DE_B, "Companero B"), self._lo_guardado())

    def test_una_propuesta_de_un_companero_desactivado_tambien_se_respeta(self):
        """Que A ya no este en el equipo no borra lo que escribio cuando estaba."""
        guardar_propuesta(self.conexion, self.persona_id, "incompleta", NOTA_DE_A, self.a)
        desactivar_companero(self.conexion, self.a)

        with self.assertRaises(ErrorDeValidacion):
            guardar_propuesta(self.conexion, self.persona_id, "no_indicada", NOTA_DE_B, self.b)

        self.assertEqual(1, self._rastro_de_a())


class PruebaDeLaPropuestaVigente(PruebaConBaseTemporal):
    """`propuesta_vigente` es lo que la pantalla lee para poder avisar."""

    def setUp(self):
        super().setUp()
        self.caso_id, _ = alta_de_caso(self.conexion, numero_caso="CASP2609")
        self.persona_id = alta_de_persona(
            self.conexion, self.caso_id, mrn="055-1111-3853", nombre="Persona Una"
        )
        self.a = alta_de_companero(self.conexion, "Companero A")

    def test_sin_propuesta_devuelve_none(self):
        self.assertIsNone(propuesta_vigente(self.conexion, self.persona_id))

    def test_con_propuesta_devuelve_quien_y_que(self):
        guardar_propuesta(self.conexion, self.persona_id, "incompleta", NOTA_DE_A, self.a)

        vigente = propuesta_vigente(self.conexion, self.persona_id)

        self.assertEqual(self.a, vigente["propuesto_por"])
        self.assertEqual("Companero A", vigente["nombre_del_companero"])
        self.assertEqual(NOTA_DE_A, vigente["nota_companero"])

    def test_una_persona_que_no_existe_devuelve_none(self):
        self.assertIsNone(propuesta_vigente(self.conexion, 9999))
