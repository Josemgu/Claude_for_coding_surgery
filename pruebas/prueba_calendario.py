"""Las tres consultas de fecha de la FASE 5, probadas por sus bordes.

Cada prueba sale de un criterio de aceptacion, no de leer el codigo que acaba de
escribirse. Los criterios estan en `PENDIENTES.md`, FASE 5, y se citan uno a uno.

«Hoy» es siempre una fecha fija inyectada. Ninguna prueba de este archivo depende
de que dia se corra, y eso se comprueba ademas de forma directa mas abajo.
"""

import ast
import unittest
from datetime import date, datetime
from pathlib import Path
from unittest import mock

from datos import estados
from datos.calendario import (
    DIAS_DE_LA_VENTANA,
    ErrorDeConsulta,
    casos_en_riesgo,
    casos_que_viajan_pronto,
    vista_de_mes,
)
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 1)


class PruebaDelCalendario(PruebaConBaseTemporal):
    """Base comun: un ayudante para dar de alta casos con la fecha que haga falta."""

    def alta(self, numero_caso, fecha_viaje, estado=None, unidad="123456", personas=0):
        caso_id = alta_de_caso(
            self.conexion,
            numero_caso,
            unidad_numero=unidad,
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

    def archivar(self, caso_id):
        """Lo que hara la FASE 8. Aqui se hace a mano para poder probar el filtro."""
        self.conexion.execute(
            "UPDATE casos SET archivado = 1, fecha_archivado = ? WHERE id = ?",
            ("2026-09-01 10:00:00", caso_id),
        )

    def numeros(self, casos):
        return [caso["numero_caso"] for caso in casos]


class PruebaDeLaVentanaDeSieteDias(PruebaDelCalendario):
    """Criterio 3: el dia 7 entra, el dia 8 no, y lo ya pasado se distingue."""

    def test_el_dia_siete_entra_y_el_ocho_no(self):
        self.alta("AAAA2609", "2026-09-08")  # hoy + 7
        self.alta("BBBB2609", "2026-09-09")  # hoy + 8

        proximos = casos_que_viajan_pronto(self.conexion, HOY)

        self.assertEqual(["AAAA2609"], self.numeros(proximos))
        self.assertEqual(7, proximos[0]["dias_para_el_viaje"])

    def test_el_dia_de_hoy_entra(self):
        self.alta("AAAA2609", "2026-09-01")

        proximos = casos_que_viajan_pronto(self.conexion, HOY)

        self.assertEqual(["AAAA2609"], self.numeros(proximos))
        self.assertEqual(0, proximos[0]["dias_para_el_viaje"])
        self.assertFalse(proximos[0]["ya_viajo"])

    def test_un_caso_a_treinta_dias_no_aparece(self):
        """Criterio 2, literal."""
        self.alta("AAAA2610", "2026-10-01")

        self.assertEqual([], casos_que_viajan_pronto(self.conexion, HOY))

    def test_lo_que_ya_viajo_aparece_y_queda_distinguido(self):
        self.alta("AAAA2608", "2026-08-25")
        self.alta("BBBB2609", "2026-09-03")

        proximos = casos_que_viajan_pronto(self.conexion, HOY)

        self.assertEqual(["AAAA2608", "BBBB2609"], self.numeros(proximos))
        self.assertTrue(proximos[0]["ya_viajo"])
        self.assertEqual(-7, proximos[0]["dias_para_el_viaje"])
        self.assertFalse(proximos[1]["ya_viajo"])

    def test_la_ventana_se_puede_estrechar_y_ensanchar(self):
        self.alta("AAAA2609", "2026-09-09")

        self.assertEqual([], casos_que_viajan_pronto(self.conexion, HOY))
        self.assertEqual(
            ["AAAA2609"],
            self.numeros(casos_que_viajan_pronto(self.conexion, HOY, dias_de_la_ventana=8)),
        )

    def test_la_ventana_por_defecto_mide_siete_dias(self):
        self.assertEqual(7, DIAS_DE_LA_VENTANA)

    def test_un_caso_sin_fecha_de_viaje_no_se_puede_situar(self):
        self.alta("AAAA2609", None)

        self.assertEqual([], casos_que_viajan_pronto(self.conexion, HOY))

    def test_con_la_base_vacia_devuelve_una_lista_vacia_sin_error(self):
        """Criterio 5: la pantalla de inicio tiene que poder abrir sin datos."""
        self.assertEqual([], casos_que_viajan_pronto(self.conexion, HOY))
        self.assertEqual([], casos_en_riesgo(self.conexion, HOY))
        self.assertEqual({}, vista_de_mes(self.conexion, 2026, 9))

    def test_salen_ordenados_por_fecha_ascendente(self):
        self.alta("CCCC2609", "2026-09-06")
        self.alta("AAAA2609", "2026-09-02")
        self.alta("BBBB2609", "2026-09-04")

        self.assertEqual(
            ["AAAA2609", "BBBB2609", "CCCC2609"],
            self.numeros(casos_que_viajan_pronto(self.conexion, HOY)),
        )

    def test_cada_caso_trae_su_unidad_y_cuantas_personas_lleva(self):
        self.alta("AAAA2609", "2026-09-03", unidad="7000011", personas=3)

        caso = casos_que_viajan_pronto(self.conexion, HOY)[0]

        self.assertEqual("7000011", caso["unidad_numero"])
        self.assertEqual(3, caso["personas"])


class PruebaDelFiltroDeArchivados(PruebaDelCalendario):
    """Criterio 6, mitad de diseno: el filtro esta desde el primer dia.

    ⚠️ **Este grupo cambio el 2026-09-03 porque cambio la DECISION, no porque el
    codigo estorbara.** Hasta ese dia un caso archivado no salia en ninguna de las
    tres consultas. El dueno lo revirtio para el calendario, literal: *«Lo que
    archivo debe verse en el calendario, debe decir archivado.»*
    (`DECISIONES.md`, 2026-09-03). Las otras dos consultas siguen igual, y eso se
    sigue comprobando aqui: si alguien colara un archivado en la franja roja o en
    la lista de pendientes, estas pruebas lo cazan.
    """

    def test_un_caso_archivado_sigue_fuera_de_las_dos_consultas_de_alarma(self):
        caso_id = self.alta("AAAA2609", "2026-09-03", estado="incompleta")
        self.archivar(caso_id)

        self.assertEqual([], casos_que_viajan_pronto(self.conexion, HOY))
        self.assertEqual([], casos_en_riesgo(self.conexion, HOY))

    def test_un_caso_archivado_SI_sale_en_el_mes_y_marcado(self):
        """La reversion del dueno, en su borde: sale, y se puede decir que lo esta."""
        caso_id = self.alta("AAAA2609", "2026-09-03", estado="incompleta")
        self.archivar(caso_id)

        mes = vista_de_mes(self.conexion, 2026, 9)

        self.assertEqual(["2026-09-03"], list(mes))
        caso = mes["2026-09-03"][0]
        self.assertEqual(caso_id, caso["id"])
        self.assertTrue(caso["esta_archivado"])
        # Y no se pinta de rojo: ya se cerro, no pide nada.
        self.assertFalse(caso["recomendacion_sin_resolver"])

    def test_el_que_sigue_vivo_se_distingue_del_archivado_en_el_mismo_mes(self):
        """Los dos salen, y `esta_archivado` es lo unico que los separa."""
        archivado = self.alta("AAAA2609", "2026-09-03", estado="incompleta")
        self.alta("BBBB2609", "2026-09-04", estado="incompleta")
        self.archivar(archivado)

        mes = vista_de_mes(self.conexion, 2026, 9)

        self.assertTrue(mes["2026-09-03"][0]["esta_archivado"])
        self.assertFalse(mes["2026-09-04"][0]["esta_archivado"])
        self.assertTrue(mes["2026-09-04"][0]["recomendacion_sin_resolver"])

    def test_archivar_uno_no_se_lleva_por_delante_al_otro(self):
        archivado = self.alta("AAAA2609", "2026-09-03")
        self.alta("BBBB2609", "2026-09-04")
        self.archivar(archivado)

        self.assertEqual(
            ["BBBB2609"], self.numeros(casos_que_viajan_pronto(self.conexion, HOY))
        )


class PruebaDeLosCasosEnRiesgo(PruebaDelCalendario):
    """Criterio 1 y 4: la consulta que separa lo que viaja pronto sin resolver."""

    def test_va_separada_y_coincide_con_la_marca_elemento_a_elemento(self):
        """Criterio 4: contado, `k de N`, con casos de los dos tipos a la vez."""
        self.alta("AAAA2609", "2026-09-03", estado="incompleta")
        self.alta("BBBB2609", "2026-09-04", estado="no_indicada")
        self.alta("CCCC2609", "2026-09-05", estado=None)

        proximos = casos_que_viajan_pronto(self.conexion, HOY)
        en_riesgo = casos_en_riesgo(self.conexion, HOY)
        marcados = [c for c in proximos if c["recomendacion_sin_resolver"]]

        self.assertEqual(3, len(proximos), "N")
        self.assertEqual(self.numeros(marcados), self.numeros(en_riesgo), "k de N")

    def test_completa_resuelve_y_por_eso_las_dos_listas_ya_no_coinciden(self):
        """El disparador de la prueba anterior salto, y esto es lo que dejo.

        ⚠️ **Hasta el 2026-09-03 esta prueba decia lo contrario** y afirmaba
        `ESTADOS_QUE_RESUELVEN == ()`, con la nota «el dia que exista un valor que
        resuelva, esta prueba falla y hay que venir a mirarla». Ese dia llego: el
        dueno nombro los dos estados que faltaban —*«Sí, completa»* / *«No está
        completa»*— y `datos/estados.py` ya trae `ESTADOS_QUE_RESUELVEN =
        ("completa",)` (`DECISIONES.md`, 2026-09-03). Se vino a mirar, y lo que
        antes era «las dos listas coinciden» ahora es su contrario, que es la
        deuda que la FASE 5 llevaba abierta desde el principio.
        """
        self.assertIn("completa", estados.ESTADOS_QUE_RESUELVEN)

        self.alta("AAAA2609", "2026-09-03", estado="completa")
        self.alta("BBBB2609", "2026-09-04", estado="incompleta")
        self.alta("CCCC2609", "2026-09-05", estado=None)

        proximos = casos_que_viajan_pronto(self.conexion, HOY)
        en_riesgo = casos_en_riesgo(self.conexion, HOY)

        # Los tres viajan pronto; el que esta completo ya no esta en riesgo.
        self.assertEqual(["AAAA2609", "BBBB2609", "CCCC2609"], self.numeros(proximos))
        self.assertEqual(["BBBB2609", "CCCC2609"], self.numeros(en_riesgo))
        self.assertNotEqual(self.numeros(proximos), self.numeros(en_riesgo))

    def test_anadir_un_valor_a_la_lista_es_lo_unico_que_hay_que_tocar(self):
        """La prueba de que la consulta no lleva ningun estado escrito dentro.

        ⚠️ Que `incompleta` resuelva es MENTIRA, y aqui no se afirma: es una sonda.
        Se cambia la lista de `datos.estados` y nada mas, y la consulta tiene que
        cambiar de respuesta sola. Si no cambiara, es que el valor estaria escrito
        en el SQL y anadir los que faltan obligaria a tocar consultas.
        """
        self.alta("AAAA2609", "2026-09-03", estado="incompleta")
        self.alta("BBBB2609", "2026-09-04", estado="no_indicada")

        with mock.patch.object(estados, "ESTADOS_QUE_RESUELVEN", ("incompleta",)):
            en_riesgo = casos_en_riesgo(self.conexion, HOY)
            proximos = casos_que_viajan_pronto(self.conexion, HOY)

        self.assertEqual(["BBBB2609"], self.numeros(en_riesgo))
        self.assertEqual(
            ["BBBB2609"],
            self.numeros([c for c in proximos if c["recomendacion_sin_resolver"]]),
            "la marca de la fila y el filtro de la consulta tienen que ir juntos",
        )

    def test_el_borde_de_siete_dias_tambien_manda_en_la_consulta_de_riesgo(self):
        self.alta("AAAA2609", "2026-09-08", estado="incompleta")
        self.alta("BBBB2609", "2026-09-09", estado="incompleta")

        self.assertEqual(["AAAA2609"], self.numeros(casos_en_riesgo(self.conexion, HOY)))


class PruebaDeLaVistaDeMes(PruebaDelCalendario):
    """La vista de mes: por dia, que casos hay, de que unidad y cuantas personas."""

    def test_agrupa_por_dia_con_unidad_y_conteo_de_personas(self):
        self.alta("AAAA2609", "2026-09-08", unidad="123456", personas=3)
        self.alta("BBBB2609", "2026-09-08", unidad="7000011", personas=1)
        self.alta("CCCC2609", "2026-09-20", unidad="654321", personas=2)

        mes = vista_de_mes(self.conexion, 2026, 9)

        self.assertEqual(["2026-09-08", "2026-09-20"], list(mes))
        self.assertEqual(2, len(mes["2026-09-08"]))
        self.assertEqual(
            [("AAAA2609", "123456", 3), ("BBBB2609", "7000011", 1)],
            [(c["numero_caso"], c["unidad_numero"], c["personas"]) for c in mes["2026-09-08"]],
        )
        self.assertEqual(2, mes["2026-09-20"][0]["personas"])

    def test_solo_aparecen_los_dias_que_tienen_algo(self):
        self.alta("AAAA2609", "2026-09-15")

        self.assertEqual(["2026-09-15"], list(vista_de_mes(self.conexion, 2026, 9)))

    def test_los_bordes_del_mes(self):
        self.alta("AAAA2608", "2026-08-31")
        self.alta("BBBB2609", "2026-09-01")
        self.alta("CCCC2609", "2026-09-30")
        self.alta("DDDD2610", "2026-10-01")

        mes = vista_de_mes(self.conexion, 2026, 9)

        self.assertEqual(["2026-09-01", "2026-09-30"], list(mes))

    def test_diciembre_no_se_desborda_al_ano_siguiente(self):
        self.alta("AAAA2612", "2026-12-31")
        self.alta("BBBB2701", "2027-01-01")

        mes = vista_de_mes(self.conexion, 2026, 12)

        self.assertEqual(["2026-12-31"], list(mes))

    def test_febrero_de_ano_bisiesto_entra_entero(self):
        self.alta("AAAA2802", "2028-02-29")

        self.assertEqual(["2028-02-29"], list(vista_de_mes(self.conexion, 2028, 2)))

    def test_cada_caso_del_mes_dice_si_su_recomendacion_sigue_sin_resolver(self):
        self.alta("AAAA2609", "2026-09-10", estado="incompleta")

        caso = vista_de_mes(self.conexion, 2026, 9)["2026-09-10"][0]

        self.assertTrue(caso["recomendacion_sin_resolver"])


class PruebaDeLosParametrosQueSeRechazan(PruebaDelCalendario):
    """Lo que no vale se rechaza con un mensaje en espanol que nombra el campo."""

    def test_una_fecha_de_hoy_que_no_existe(self):
        with self.assertRaises(ErrorDeConsulta) as fallo:
            casos_que_viajan_pronto(self.conexion, "2026-02-31")
        self.assertIn("hoy", str(fallo.exception))

    def test_un_hoy_que_no_es_una_fecha(self):
        with self.assertRaises(ErrorDeConsulta):
            casos_que_viajan_pronto(self.conexion, 20260901)

    def test_una_ventana_negativa(self):
        with self.assertRaises(ErrorDeConsulta) as fallo:
            casos_que_viajan_pronto(self.conexion, HOY, dias_de_la_ventana=-1)
        self.assertIn("dias_de_la_ventana", str(fallo.exception))

    def test_una_ventana_que_no_es_un_entero(self):
        with self.assertRaises(ErrorDeConsulta):
            casos_que_viajan_pronto(self.conexion, HOY, dias_de_la_ventana=True)

    def test_un_mes_que_no_existe(self):
        with self.assertRaises(ErrorDeConsulta) as fallo:
            vista_de_mes(self.conexion, 2026, 13)
        self.assertIn("13", str(fallo.exception))

    def test_una_fecha_de_viaje_imposible_guardada_a_la_fuerza(self):
        """El `GLOB` del esquema valida la forma, no que la fecha exista."""
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en) VALUES (?, ?, ?)",
            ("ZZZZ2602", "2026-02-31", "2026-09-01 10:00:00"),
        )

        with self.assertRaises(ErrorDeConsulta) as fallo:
            casos_que_viajan_pronto(self.conexion, date(2026, 2, 28))
        self.assertIn("ZZZZ2602", str(fallo.exception))


class PruebaDeQueHoySiempreEsUnParametro(PruebaDelCalendario):
    """«Hoy» se inyecta: el modulo no puede mirar el reloj por su cuenta."""

    def test_el_modulo_no_llama_a_ninguna_funcion_de_reloj(self):
        """Se lee el arbol de sintaxis, no el texto: un `grep` cazaria el docstring.

        Se revisan los tres modulos de la fase a la vez. Un reloj escondido en
        cualquiera de ellos vuelve a hacer imposible probar los bordes.
        """
        raiz = Path(__file__).resolve().parent.parent
        relojes_de_python = ("today", "now", "utcnow", "fromtimestamp")
        relojes_de_sqlite = ("CURRENT_DATE", "CURRENT_TIMESTAMP", "'now'")

        for nombre in ("calendario.py", "pendientes.py"):
            codigo = (raiz / "datos" / nombre).read_text(encoding="utf-8")
            arbol = ast.parse(codigo, filename=nombre)
            for nodo in ast.walk(arbol):
                if isinstance(nodo, ast.Call):
                    llamada = getattr(nodo.func, "attr", getattr(nodo.func, "id", ""))
                    self.assertNotIn(llamada, relojes_de_python, f"{nombre}:{nodo.lineno}")
                if isinstance(nodo, ast.Constant) and isinstance(nodo.value, str):
                    for reloj in relojes_de_sqlite:
                        self.assertNotIn(reloj, nodo.value, f"{nombre}:{nodo.lineno}")

    def test_el_mismo_caso_entra_o_no_segun_el_hoy_que_se_le_pase(self):
        self.alta("AAAA2609", "2026-09-08")

        self.assertEqual(
            ["AAAA2609"], self.numeros(casos_que_viajan_pronto(self.conexion, date(2026, 9, 1)))
        )
        self.assertEqual([], casos_que_viajan_pronto(self.conexion, date(2026, 8, 31)))

    def test_hoy_admite_texto_y_datetime_ademas_de_date(self):
        self.alta("AAAA2609", "2026-09-08")

        por_texto = casos_que_viajan_pronto(self.conexion, "2026-09-01")
        por_datetime = casos_que_viajan_pronto(self.conexion, datetime(2026, 9, 1, 23, 59))

        self.assertEqual(["AAAA2609"], self.numeros(por_texto))
        self.assertEqual(["AAAA2609"], self.numeros(por_datetime))


if __name__ == "__main__":
    unittest.main()
