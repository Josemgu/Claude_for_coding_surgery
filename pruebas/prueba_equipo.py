"""El resumen por agente y los cuatro contadores del panel, por sus bordes.

Cada prueba sale de una decision escrita, no de leer el codigo. Las dos que
gobiernan este archivo:

  - *«"Hojas sin devolver" = documentos asignados en los que **ninguna** persona
    trae propuesta. Un solo numero, el simple.»* (`DECISIONES.md`, 2026-09-03).
    El borde que la separa de la otra lectura posible —«le falta ALGUNA»— se
    prueba abajo con un documento de dos personas y solo una devuelta.
  - *«Lo que archivo debe verse en el calendario»*, y en ningun otro sitio: un
    documento archivado no es trabajo de nadie, asi que no cuenta ni para el
    equipo ni para los contadores.

«Hoy» es una fecha fija inyectada. Ninguna prueba depende de que dia se corra.
"""

import unittest
from datetime import date

from datos.asignaciones import asignar_caso, retirar_caso
from datos.companeros import alta_de_companero, desactivar_companero
from datos.equipo import SIN_ASIGNAR, contadores_del_panel, resumen_del_equipo
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 10)


class PruebaDelEquipo(PruebaConBaseTemporal):
    """Base comun: dar de alta documentos, personas, companeros y devoluciones."""

    def alta(self, numero_caso, fecha_viaje=None, estado=None, personas=1):
        caso_id = alta_de_caso(
            self.conexion,
            numero_caso,
            unidad_numero="123456",
            fecha_viaje=fecha_viaje,
            estado_recomendacion=estado,
        ).id
        for fila in range(1, personas + 1):
            alta_de_persona(
                self.conexion,
                caso_id,
                nombre=f"Persona {fila} del {numero_caso}",
                fila_formulario=fila,
            )
        return caso_id

    def companero(self, nombre):
        # `alta_de_companero` devuelve el id pelado, no un objeto con `.id` como
        # `alta_de_caso`. Medido, no supuesto.
        return alta_de_companero(self.conexion, nombre)

    def devolver_una_persona(self, caso_id, companero_id, fila_formulario=1):
        """Lo que hace la vuelta del Excel: deja `propuesto_en` en esa persona.

        `propuesto_por` es el ID del companero y lleva `REFERENCES companeros(id)`
        —medido: con un nombre dentro, SQLite levanta FOREIGN KEY constraint
        failed—. Se firma con quien la trajo, no con un texto suelto.
        """
        self.conexion.execute(
            "UPDATE personas SET propuesto_en = ?, propuesto_por = ? "
            "WHERE caso_id = ? AND fila_formulario = ?",
            ("2026-09-09 10:00:00", companero_id, caso_id, fila_formulario),
        )

    def archivar(self, caso_id):
        self.conexion.execute(
            "UPDATE casos SET archivado = 1, fecha_archivado = ? WHERE id = ?",
            ("2026-09-09 10:00:00", caso_id),
        )

    def renglon(self, equipo, nombre):
        for fila in equipo:
            if fila["nombre"] == nombre:
                return fila
        raise AssertionError(f"«{nombre}» no salio en el resumen: {equipo}")


class PruebaDelResumenPorAgente(PruebaDelEquipo):
    """Lo que pinta la tarjeta «El equipo»: documentos y cuantos sin volver."""

    def test_cada_agente_trae_sus_documentos_y_lo_que_le_falta_por_devolver(self):
        agente = self.companero("Agente A")
        con_vuelta = self.alta("AAAA2609", "2026-09-20", personas=2)
        sin_vuelta = self.alta("BBBB2609", "2026-09-21", personas=1)
        asignar_caso(self.conexion, con_vuelta, agente)
        asignar_caso(self.conexion, sin_vuelta, agente)
        self.devolver_una_persona(con_vuelta, agente)

        fila = self.renglon(resumen_del_equipo(self.conexion), "Agente A")

        self.assertEqual(2, fila["cuantos_documentos"])
        self.assertEqual(1, fila["sin_devolver"])
        self.assertEqual(3, fila["personas"])

    def test_un_documento_con_UNA_persona_devuelta_ya_NO_cuenta_sin_devolver(self):
        """El borde que separa las dos lecturas posibles de «sin devolver».

        El documento lleva dos personas y solo vuelve una. Con la lectura que el
        dueno eligio —«ninguna trae propuesta»— este documento **no** cuenta. Con
        la otra —«le falta alguna»— contaria. Si esta prueba se pone en rojo, es
        que alguien cambio la definicion sin decirlo.
        """
        agente = self.companero("Agente A")
        caso = self.alta("AAAA2609", "2026-09-20", personas=2)
        asignar_caso(self.conexion, caso, agente)
        self.devolver_una_persona(caso, agente, fila_formulario=1)

        fila = self.renglon(resumen_del_equipo(self.conexion), "Agente A")

        self.assertEqual(1, fila["cuantos_documentos"])
        self.assertEqual(0, fila["sin_devolver"])

    def test_el_agente_sin_nada_sale_con_cero_y_no_desaparece(self):
        """Un agente que no aparece se lee como «ya no esta», que es otra cosa."""
        self.companero("Agente B")

        fila = self.renglon(resumen_del_equipo(self.conexion), "Agente B")

        self.assertEqual(0, fila["cuantos_documentos"])
        self.assertEqual(0, fila["sin_devolver"])

    def test_lo_que_no_lleva_nadie_sale_en_su_propio_renglon(self):
        self.alta("AAAA2609", "2026-09-20", personas=3)

        fila = self.renglon(resumen_del_equipo(self.conexion), SIN_ASIGNAR)

        self.assertEqual(1, fila["cuantos_documentos"])
        self.assertEqual(3, fila["personas"])
        self.assertIsNone(fila["companero_id"])
        # «Sin devolver» no significa nada aqui: no se le pidio a nadie.
        self.assertIsNone(fila["sin_devolver"])

    def test_el_renglon_sin_asignar_va_siempre_aunque_valga_cero(self):
        agente = self.companero("Agente A")
        caso = self.alta("AAAA2609", "2026-09-20")
        asignar_caso(self.conexion, caso, agente)

        self.assertEqual(0, self.renglon(resumen_del_equipo(self.conexion), SIN_ASIGNAR)["cuantos_documentos"])

    def test_un_documento_archivado_deja_de_contarle_al_agente(self):
        agente = self.companero("Agente A")
        caso = self.alta("AAAA2609", "2026-09-20")
        asignar_caso(self.conexion, caso, agente)
        self.archivar(caso)

        fila = self.renglon(resumen_del_equipo(self.conexion), "Agente A")

        self.assertEqual(0, fila["cuantos_documentos"])

    def test_retirar_la_asignacion_lo_devuelve_a_sin_asignar(self):
        agente = self.companero("Agente A")
        caso = self.alta("AAAA2609", "2026-09-20")
        asignar_caso(self.conexion, caso, agente)
        retirar_caso(self.conexion, caso, agente)

        equipo = resumen_del_equipo(self.conexion)

        self.assertEqual(0, self.renglon(equipo, "Agente A")["cuantos_documentos"])
        self.assertEqual(1, self.renglon(equipo, SIN_ASIGNAR)["cuantos_documentos"])

    def test_un_companero_dado_de_baja_no_sale_en_la_tarjeta(self):
        agente = self.companero("Agente B")
        desactivar_companero(self.conexion, agente)

        nombres = [fila["nombre"] for fila in resumen_del_equipo(self.conexion)]

        self.assertNotIn("Agente B", nombres)


class PruebaDeLosCuatroContadores(PruebaDelEquipo):
    """Los cuatro numeros de arriba, cada uno en su unidad."""

    def test_personas_por_viajar_cuenta_personas_y_no_documentos(self):
        self.alta("AAAA2609", "2026-09-20", personas=4)
        self.alta("BBBB2609", "2026-09-25", personas=2)

        self.assertEqual(
            6, contadores_del_panel(self.conexion, HOY)["personas_por_viajar"]
        )

    def test_el_que_viaja_HOY_todavia_cuenta_como_por_viajar(self):
        """El borde del dia: hoy no ha pasado todavia."""
        self.alta("AAAA2609", HOY.isoformat(), personas=2)

        self.assertEqual(
            2, contadores_del_panel(self.conexion, HOY)["personas_por_viajar"]
        )

    def test_el_que_ya_viajo_sin_resolver_cuenta_en_el_contador_rojo(self):
        self.alta("AAAA2609", "2026-09-01", estado="incompleta", personas=3)

        contadores = contadores_del_panel(self.conexion, HOY)

        self.assertEqual(0, contadores["personas_por_viajar"])
        self.assertEqual(3, contadores["personas_que_viajaron_sin_verificar"])

    def test_el_que_ya_viajo_COMPLETO_no_cuenta_en_el_contador_rojo(self):
        self.alta("AAAA2609", "2026-09-01", estado="completa", personas=3)

        self.assertEqual(
            0,
            contadores_del_panel(self.conexion, HOY)[
                "personas_que_viajaron_sin_verificar"
            ],
        )

    def test_un_documento_sin_estado_cuenta_como_sin_verificar(self):
        """Regla permanente 5: de lo que nadie dijo nada, no se supone nada bueno."""
        self.alta("AAAA2609", "2026-09-01", estado=None, personas=1)

        self.assertEqual(
            1,
            contadores_del_panel(self.conexion, HOY)[
                "personas_que_viajaron_sin_verificar"
            ],
        )

    def test_documentos_completos_cuenta_documentos_con_el_estado_del_dueno(self):
        self.alta("AAAA2609", "2026-09-20", estado="completa", personas=5)
        self.alta("BBBB2609", "2026-09-21", estado="incompleta", personas=1)
        self.alta("CCCC2609", "2026-09-22", estado=None, personas=1)

        self.assertEqual(
            1, contadores_del_panel(self.conexion, HOY)["documentos_completos"]
        )

    def test_hojas_sin_devolver_solo_cuenta_lo_asignado(self):
        """Un documento que no lleva nadie no esta «sin devolver»: no se pidio."""
        agente = self.companero("Agente A")
        asignado = self.alta("AAAA2609", "2026-09-20")
        self.alta("BBBB2609", "2026-09-21")
        asignar_caso(self.conexion, asignado, agente)

        self.assertEqual(
            1, contadores_del_panel(self.conexion, HOY)["hojas_sin_devolver"]
        )

    def test_lo_archivado_no_cuenta_en_ninguno_de_los_cuatro(self):
        agente = self.companero("Agente A")
        futuro = self.alta("AAAA2609", "2026-09-20", estado="completa", personas=2)
        pasado = self.alta("BBBB2609", "2026-09-01", estado="incompleta", personas=2)
        asignar_caso(self.conexion, futuro, agente)
        self.archivar(futuro)
        self.archivar(pasado)

        self.assertEqual(
            {
                "personas_por_viajar": 0,
                "documentos_completos": 0,
                "hojas_sin_devolver": 0,
                "personas_que_viajaron_sin_verificar": 0,
            },
            contadores_del_panel(self.conexion, HOY),
        )

    def test_un_documento_sin_fecha_no_cuenta_en_ninguno_de_los_dos_de_fecha(self):
        """Sin fecha no se puede decir si ya viajo ni si viajara. No se inventa."""
        self.alta("AAAA2609", None, estado="incompleta", personas=4)

        contadores = contadores_del_panel(self.conexion, HOY)

        self.assertEqual(0, contadores["personas_por_viajar"])
        self.assertEqual(0, contadores["personas_que_viajaron_sin_verificar"])

    def test_hoy_puede_llegar_como_texto(self):
        self.alta("AAAA2609", "2026-09-20", personas=2)

        self.assertEqual(
            contadores_del_panel(self.conexion, HOY),
            contadores_del_panel(self.conexion, "2026-09-10"),
        )


if __name__ == "__main__":
    unittest.main()
