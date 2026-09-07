"""Los cuatro bloqueadores que tapaban la pantalla de correccion, uno por uno.

Cada prueba de aqui existe porque su bloqueador se podia volver a cerrar sin que
nadie se enterara. La migracion a la version 3 es la mas fragil de todas: se
aplica una sola vez sobre una base que ya existe en disco, y si algun dia no se
aplica, el programa afirma tener columnas que la base no tiene y falla mucho
despues, al abrir un caso.

⚠️ Estas pruebas las escribe el programador para no entregar algo roto. **La
verificacion de la FASE 3 y de la FASE 5 es de QA**, y no esta hecha aqui.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import (
    VERSION_ACTUAL,
    aplicar_esquema,
    crear_tablas_de_la_version_inicial,
    marca_de_tiempo,
    version_de_la_base,
)
from datos.procedencia import (
    banda_de_la_procedencia,
    guardar_procedencia_de_campo,
    leer_procedencia_de_campo,
    procedencia_por_campo,
)
from datos.repositorio import (
    CASILLAS_DE_ORDENANZAS,
    actualizar_datos_de_persona,
    actualizar_datos_del_caso,
    alta_de_caso,
    alta_de_persona,
    leer_caso_por_id,
    leer_caso_por_numero,
    leer_personas_del_caso,
)
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal


def _caso_de_ejemplo(**cambios):
    datos = {
        "numero_caso": "CASP2609",
        "unidad_numero": "7000011",
        "fecha_viaje": "2026-09-08",
        "pagina_pdf": 2,
        "unidad_nombre": "Paramaribo Branch",
    }
    datos.update(cambios)
    return datos


class LaMigracionALaVersion3(unittest.TestCase):
    """Una base de la version 1 con datos dentro llega entera a la version 3."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_bloq_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
            "VALUES (1, ?, 'inicial')",
            (marca_de_tiempo(),),
        )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, creado_en) "
            "VALUES ('VIEJ2609', '700001', '2026-09-08', ?)",
            (marca_de_tiempo(),),
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre) "
            "VALUES (1, '055-1111-3853', 'FILA VIEJA')"
        )
        self.conexion.execute(
            "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen) "
            "VALUES ('casos', 1, 'fecha_viaje', 'ocr')"
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    # Las dos consultas se escriben enteras y no se arma una con el nombre de la
    # tabla dentro de una f-string. `pruebas/auditoria_sql.py` dictamina INSEGURA
    # cualquier instruccion armada asi, sin excepcion por «pero el nombre lo pongo
    # yo». Una lista blanca que admite excepciones deja de ser una lista blanca —
    # y menos aun se le puede pedir a las pruebas que la incumplan.

    def _columnas_de_casos(self):
        return [fila[1] for fila in self.conexion.execute("PRAGMA table_info(casos)")]

    def _columnas_de_procedencia(self):
        return [
            fila[1] for fila in self.conexion.execute("PRAGMA table_info(procedencia_campo)")
        ]

    def test_una_base_de_la_version_1_llega_a_la_version_actual(self):
        self.assertEqual(version_de_la_base(self.conexion), 1)
        aplicar_esquema(self.conexion)
        self.assertEqual(version_de_la_base(self.conexion), VERSION_ACTUAL)
        self.assertGreaterEqual(VERSION_ACTUAL, 3)

    def test_casos_gana_la_pagina_y_el_nombre_de_la_unidad(self):
        aplicar_esquema(self.conexion)
        columnas = self._columnas_de_casos()
        self.assertIn("pagina_pdf", columnas)
        self.assertIn("unidad_nombre", columnas)

    def test_procedencia_gana_la_banda_y_la_marca_de_tachon(self):
        aplicar_esquema(self.conexion)
        columnas = self._columnas_de_procedencia()
        for nombre in ("banda_x0", "banda_y0", "banda_x1", "banda_y1", "anulado_por_tachon"):
            self.assertIn(nombre, columnas)

    def test_los_datos_que_ya_estaban_siguen_ahi(self):
        aplicar_esquema(self.conexion)
        fila = self.conexion.execute(
            "SELECT numero_caso, unidad_numero, fecha_viaje FROM casos"
        ).fetchone()
        self.assertEqual(fila["numero_caso"], "VIEJ2609")
        self.assertEqual(fila["unidad_numero"], "700001")
        self.assertEqual(fila["fecha_viaje"], "2026-09-08")
        self.assertEqual(
            self.conexion.execute("SELECT nombre FROM personas").fetchone()["nombre"],
            "FILA VIEJA",
        )

    def test_las_columnas_nuevas_nacen_vacias_y_no_inventadas(self):
        """Una fila que ya existia no gana valores: gana huecos.

        Es lo unico honesto: de un caso que entro antes de la version 3 nadie sabe
        de que pagina salio. Un `1` por defecto seria una pagina inventada, y el
        boton de abrir el PDF llevaria al formulario equivocado.
        """
        aplicar_esquema(self.conexion)
        fila = self.conexion.execute(
            "SELECT pagina_pdf, unidad_nombre FROM casos"
        ).fetchone()
        self.assertIsNone(fila["pagina_pdf"])
        self.assertIsNone(fila["unidad_nombre"])

    def test_aplicarla_dos_veces_no_rompe_nada(self):
        aplicar_esquema(self.conexion)
        aplicar_esquema(self.conexion)
        aplicar_esquema(self.conexion)
        self.assertEqual(version_de_la_base(self.conexion), VERSION_ACTUAL)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )

    def test_el_motor_rechaza_una_pagina_menor_que_uno(self):
        aplicar_esquema(self.conexion)
        with self.assertRaises(Exception):
            self.conexion.execute("UPDATE casos SET pagina_pdf = 0")

    def test_el_motor_rechaza_una_banda_fuera_de_la_pagina(self):
        aplicar_esquema(self.conexion)
        with self.assertRaises(Exception):
            self.conexion.execute("UPDATE procedencia_campo SET banda_x0 = 1.5")


class LaPaginaYElNombreDeLaUnidad(PruebaConBaseTemporal):
    """Bloqueadores 2 y 3: los dos datos que el extractor lee y se perdian."""

    def test_se_guardan_y_se_vuelven_a_leer(self):
        alta_de_caso(self.conexion, **_caso_de_ejemplo())
        caso = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertEqual(caso["pagina_pdf"], 2)
        self.assertEqual(caso["unidad_nombre"], "Paramaribo Branch")

    def test_leer_por_id_devuelve_lo_mismo_que_leer_por_numero(self):
        alta = alta_de_caso(self.conexion, **_caso_de_ejemplo())
        self.assertEqual(
            leer_caso_por_id(self.conexion, alta.id),
            leer_caso_por_numero(self.conexion, "CASP2609"),
        )

    def test_el_nombre_de_la_unidad_se_corrige_a_mano(self):
        alta = alta_de_caso(self.conexion, **_caso_de_ejemplo())
        actualizar_datos_del_caso(
            self.conexion, alta.id, unidad_nombre="Castries Branch"
        )
        self.assertEqual(
            leer_caso_por_id(self.conexion, alta.id)["unidad_nombre"], "Castries Branch"
        )

    def test_una_pagina_cero_se_rechaza_antes_de_llegar_al_motor(self):
        with self.assertRaises(ErrorDeValidacion) as recogido:
            alta_de_caso(self.conexion, **_caso_de_ejemplo(pagina_pdf=0))
        self.assertIn("pagina_pdf", str(recogido.exception))

    def test_un_nombre_de_unidad_desbocado_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            alta_de_caso(self.conexion, **_caso_de_ejemplo(unidad_nombre="x" * 500))

    def test_un_nombre_solo_de_espacios_se_guarda_como_nulo(self):
        """Vacio y nulo no pueden ser dos cosas distintas en la misma columna."""
        alta = alta_de_caso(self.conexion, **_caso_de_ejemplo(unidad_nombre="   "))
        self.assertIsNone(leer_caso_por_id(self.conexion, alta.id)["unidad_nombre"])


class LaBandaDelEscaneo(PruebaConBaseTemporal):
    """Bloqueador 1: el recorte que se calculaba y no llegaba a ninguna parte."""

    def setUp(self):
        super().setUp()
        self.caso_id = alta_de_caso(self.conexion, **_caso_de_ejemplo()).id

    def test_se_guarda_y_se_vuelve_a_leer_entera(self):
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.9, valor_ocr="Sept 8 2026",
            banda=(0.1, 0.2, 0.9, 0.24),
        )
        fila = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(banda_de_la_procedencia(fila), (0.1, 0.2, 0.9, 0.24))

    def test_sin_banda_no_hay_banda_y_no_hay_ceros(self):
        """Un campo sin ancla devuelve None, no un rectangulo de area cero.

        Un `(0, 0, 0, 0)` se recortaria como una tira de un pixel y pareceria un
        escaneo en blanco. `None` es lo que hace que se dibuje el rectangulo
        rayado que dice que ahi no se encontro la fila.
        """
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "numero_caso", origen="anotacion"
        )
        fila = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "numero_caso")
        self.assertIsNone(banda_de_la_procedencia(fila))

    def test_una_banda_del_reves_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, "fecha_viaje",
                origen="ocr", banda=(0.9, 0.2, 0.1, 0.24),
            )

    def test_una_banda_fuera_de_la_pagina_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, "fecha_viaje",
                origen="ocr", banda=(0.1, 0.2, 1.4, 0.24),
            )

    def test_corregir_a_mano_NO_borra_la_banda(self):
        """La tira tiene que seguir viendose despues de teclear encima.

        Es contra la tira contra lo que se comprueba lo tecleado. Si el guardado
        manual la borrara, el segundo repaso del mismo campo se haria a ciegas.
        """
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.5, valor_ocr="Sept 7", banda=(0.1, 0.2, 0.9, 0.24),
        )
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="manual", confianza=None, valor_ocr="Sept 7",
        )
        fila = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(fila["origen"], "manual")
        self.assertEqual(banda_de_la_procedencia(fila), (0.1, 0.2, 0.9, 0.24))

    def test_corregir_a_mano_NO_borra_lo_que_el_lector_habia_leido(self):
        """`valor_ocr` es la unica prueba de que la maquina leyo otra cosa."""
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="ocr", confianza=0.5, valor_ocr="September 7, 2026",
        )
        anterior = procedencia_por_campo(self.conexion, "casos", self.caso_id)["fecha_viaje"]
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="manual", valor_ocr=anterior["valor_ocr"],
        )
        fila = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(fila["valor_ocr"], "September 7, 2026")

    def test_la_marca_de_tachon_se_guarda(self):
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje",
            origen="vacio", anulado_por_tachon=1,
        )
        fila = leer_procedencia_de_campo(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(fila["anulado_por_tachon"], 1)


class LasCasillasSeResuelvenAMano(PruebaConBaseTemporal):
    """Bloqueador 4: hasta ahora no habia forma de resolver una «no leida»."""

    def setUp(self):
        super().setUp()
        caso_id = alta_de_caso(self.conexion, **_caso_de_ejemplo()).id
        self.caso_id = caso_id
        self.persona_id = alta_de_persona(
            self.conexion, caso_id, mrn="055-1111-3853", nombre="ANONIMO, M", fila_formulario=1
        )

    def _persona(self):
        return leer_personas_del_caso(self.conexion, self.caso_id)[0]

    def test_todas_nacen_sin_leer(self):
        persona = self._persona()
        for nombre in CASILLAS_DE_ORDENANZAS:
            self.assertIsNone(persona[nombre])

    def test_una_casilla_se_marca_y_se_queda_marcada(self):
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=1)
        self.assertEqual(self._persona()["ord_traductor"], 1)

    def test_las_seis_se_resuelven_de_una_vez(self):
        actualizar_datos_de_persona(
            self.conexion, self.persona_id, **{n: 0 for n in CASILLAS_DE_ORDENANZAS}
        )
        persona = self._persona()
        for nombre in CASILLAS_DE_ORDENANZAS:
            self.assertEqual(persona[nombre], 0)

    def test_la_casilla_que_no_se_nombra_no_se_toca(self):
        """Lo que no se pasa se conserva. Es lo que permite guardar de una en una."""
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=1)
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_investidura=0)
        persona = self._persona()
        self.assertEqual(persona["ord_traductor"], 1)
        self.assertEqual(persona["ord_investidura"], 0)
        self.assertIsNone(persona["ord_recibir_propias"])

    def test_una_casilla_puede_volver_a_no_leida_a_proposito(self):
        """`None` NO significa «sin cambio»: significa «devuelvela a no leida».

        Son dos cosas distintas y la diferencia importa: si `None` fuera «sin
        cambio», no habria forma de deshacer una casilla marcada por error.
        """
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=1)
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=None)
        self.assertIsNone(self._persona()["ord_traductor"])

    def test_una_casilla_que_no_existe_se_rechaza_nombrandola(self):
        with self.assertRaises(ErrorDeValidacion) as recogido:
            actualizar_datos_de_persona(self.conexion, self.persona_id, ord_inventada=1)
        self.assertIn("ord_inventada", str(recogido.exception))

    def test_un_valor_que_no_es_ni_si_ni_no_se_rechaza(self):
        with self.assertRaises(ErrorDeValidacion):
            actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=7)

    def test_marcar_casillas_no_pierde_el_nombre_ni_el_mrn(self):
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=1)
        persona = self._persona()
        self.assertEqual(persona["nombre"], "ANONIMO, M")
        self.assertEqual(persona["mrn"], "055-1111-3853")


if __name__ == "__main__":
    unittest.main()
