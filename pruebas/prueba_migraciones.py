"""Que la migracion a la version 2 hace lo que dice sobre una base que YA existia.

El criterio que estas pruebas derivan no es «el codigo nuevo acepta 7 digitos»
—eso lo prueba `prueba_validacion.py`—, sino el que de verdad se puede romper:
**una base creada con la version 1, con datos dentro, queda migrada, con sus datos
intactos y con sus claves foraneas siguiendo vivas.**

Por eso estas pruebas construyen la base vieja a mano, con el DDL literal de la
version 1, en vez de llamar a `aplicar_esquema`: llamar a `aplicar_esquema` ya
migraria, y entonces la prueba comprobaria el camino facil y no el que importa.
"""

import shutil
import sqlite3
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import (
    VERSION_ACTUAL,
    VERSION_INICIAL,
    aplicar_esquema,
    crear_tablas_de_la_version_inicial,
    marca_de_tiempo,
    version_de_la_base,
)
from datos.migraciones import MIGRACIONES

_UNIDAD_DE_SIETE_DIGITOS = "7000011"


def _check_de_la_tabla_casos(conexion):
    """El DDL literal de `casos` tal como esta guardado en la base."""
    return conexion.execute(
        "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = 'casos'"
    ).fetchone()[0]


class PruebaDeUnaBaseVieja(unittest.TestCase):
    """Una base en version 1, con datos, que se abre con el codigo de hoy."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_migracion_"))
        self.ruta_de_la_base = self.carpeta_temporal / "fichas.db"
        self.conexion = abrir_conexion(self.ruta_de_la_base)

        # La base vieja, tal cual: solo el DDL de la version 1 y su fila de version.
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
            "VALUES (?, ?, ?)",
            (VERSION_INICIAL, marca_de_tiempo(), "Esquema inicial de la prueba."),
        )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, creado_en) "
            "VALUES ('CASP2609', '123456', '2026-09-08', '2026-09-02 10:00:00')"
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, '055-1111-3853', 'Jose Miguel Anonimo', 1)"
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    def test_la_base_vieja_de_verdad_rechazaba_los_siete_digitos(self):
        """Control positivo: sin esto, todo lo de abajo podria estar probando nada."""
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
                "VALUES ('CASP2610', ?, '2026-09-02 10:00:00')",
                (_UNIDAD_DE_SIETE_DIGITOS,),
            )

    def test_al_aplicar_el_esquema_la_base_sube_de_version(self):
        self.assertEqual(VERSION_INICIAL, version_de_la_base(self.conexion))
        aplicar_esquema(self.conexion)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))

    def test_despues_de_migrar_entran_los_siete_digitos(self):
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
            "VALUES ('CASP2610', ?, '2026-09-02 10:00:00')",
            (_UNIDAD_DE_SIETE_DIGITOS,),
        )
        guardado = self.conexion.execute(
            "SELECT unidad_numero FROM casos WHERE numero_caso = 'CASP2610'"
        ).fetchone()[0]
        self.assertEqual(_UNIDAD_DE_SIETE_DIGITOS, guardado)

    def test_los_ocho_digitos_siguen_rechazandose_despues_de_migrar(self):
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
                "VALUES ('CASP2611', '70000117', '2026-09-02 10:00:00')"
            )

    def test_los_datos_que_ya_estaban_sobreviven_enteros(self):
        aplicar_esquema(self.conexion)
        caso = self.conexion.execute(
            "SELECT id, numero_caso, unidad_numero, fecha_viaje, creado_en FROM casos"
        ).fetchall()
        self.assertEqual(1, len(caso))
        self.assertEqual(1, caso[0]["id"])
        self.assertEqual("CASP2609", caso[0]["numero_caso"])
        self.assertEqual("123456", caso[0]["unidad_numero"])
        self.assertEqual("2026-09-08", caso[0]["fecha_viaje"])
        self.assertEqual("2026-09-02 10:00:00", caso[0]["creado_en"])

    def test_las_personas_siguen_colgando_de_su_caso(self):
        aplicar_esquema(self.conexion)
        personas = self.conexion.execute(
            "SELECT caso_id, mrn FROM personas"
        ).fetchall()
        self.assertEqual(1, len(personas))
        self.assertEqual(1, personas[0]["caso_id"])
        self.assertEqual("055-1111-3853", personas[0]["mrn"])
        self.assertEqual([], self.conexion.execute("PRAGMA foreign_key_check").fetchall())

    def test_el_restrict_sigue_impidiendo_borrar_un_caso_con_personas(self):
        """La reconstruccion borra y recrea `casos`. Si el RESTRICT no sobreviviera,
        un `DELETE` dejaria personas huerfanas sin que el motor dijera nada."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute("DELETE FROM casos WHERE id = 1")

    def test_las_claves_foraneas_quedan_encendidas_al_terminar(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(1, self.conexion.execute("PRAGMA foreign_keys").fetchone()[0])

    def test_el_indice_de_viajes_activos_se_reconstruye(self):
        aplicar_esquema(self.conexion)
        indices = self.conexion.execute(
            "SELECT name FROM sqlite_schema WHERE type = 'index' AND tbl_name = 'casos'"
        ).fetchall()
        self.assertIn("idx_casos_viaje_activos", [fila["name"] for fila in indices])

    def test_aplicar_el_esquema_dos_veces_no_repite_la_migracion(self):
        aplicar_esquema(self.conexion)
        aplicar_esquema(self.conexion)
        filas = self.conexion.execute(
            "SELECT COUNT(*) FROM version_esquema WHERE version = ?", (VERSION_ACTUAL,)
        ).fetchone()[0]
        self.assertEqual(1, filas)
        self.assertEqual(1, self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])


class PruebaDeUnaBaseNueva(unittest.TestCase):
    """Una base creada hoy tiene que quedar igual que una vieja ya migrada."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_migracion_"))
        self.conexion = abrir_conexion(self.carpeta_temporal / "fichas.db")
        aplicar_esquema(self.conexion)

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    def test_una_base_nueva_nace_en_la_version_actual(self):
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))

    def test_una_base_nueva_admite_los_siete_digitos_sin_pasos_extra(self):
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
            "VALUES ('CASP2609', ?, '2026-09-02 10:00:00')",
            (_UNIDAD_DE_SIETE_DIGITOS,),
        )
        self.assertEqual(
            _UNIDAD_DE_SIETE_DIGITOS,
            self.conexion.execute("SELECT unidad_numero FROM casos").fetchone()[0],
        )

    def test_el_historial_de_versiones_registra_cada_una_con_su_fecha(self):
        """`version_esquema` guarda historial, no un solo entero (ARQUITECTURA §2.1)."""
        filas = self.conexion.execute(
            "SELECT version, aplicada_en, descripcion FROM version_esquema ORDER BY version"
        ).fetchall()
        self.assertEqual(1 + len(MIGRACIONES), len(filas))
        for fila in filas:
            self.assertTrue(fila["aplicada_en"])
            self.assertTrue(fila["descripcion"])

    def test_el_check_guardado_en_la_base_nombra_las_dos_longitudes(self):
        ddl = _check_de_la_tabla_casos(self.conexion)
        self.assertIn("[0-9][0-9][0-9][0-9][0-9][0-9][0-9]", ddl)
        self.assertIn("[0-9][0-9][0-9][0-9][0-9][0-9]", ddl)


if __name__ == "__main__":
    unittest.main()
