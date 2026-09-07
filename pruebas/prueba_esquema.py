"""Pruebas del esquema: tablas exactas, version, claves foraneas e idempotencia.

Cubren los criterios 2, 4 (version_esquema), 5 (FK RESTRICT) y 8 de la FASE 1.
"""

import sqlite3
import unittest

from datos.conexion import abrir_conexion
from datos.esquema import (
    TABLAS_DEL_ESQUEMA,
    VERSION_ACTUAL,
    aplicar_esquema,
    version_de_la_base,
)
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal, caso_de_ejemplo


def _tablas_de_la_base(conexion):
    filas = conexion.execute(
        "SELECT name FROM sqlite_master WHERE type = 'table' "
        "AND name NOT LIKE 'sqlite_%' ORDER BY name"
    ).fetchall()
    return tuple(fila[0] for fila in filas)


class PruebaDeTablas(PruebaConBaseTemporal):

    def test_las_tablas_son_exactamente_las_del_esquema(self):
        self.assertEqual(sorted(TABLAS_DEL_ESQUEMA), list(_tablas_de_la_base(self.conexion)))

    def test_todos_los_nombres_de_columna_estan_en_espanol_y_en_ascii(self):
        # pragma_table_info es una funcion de tabla: admite parametro, asi que el
        # nombre de la tabla viaja como dato y no se concatena en la instruccion.
        for tabla in TABLAS_DEL_ESQUEMA:
            columnas = self.conexion.execute(
                "SELECT name FROM pragma_table_info(?)", (tabla,)
            ).fetchall()
            self.assertTrue(columnas, f"{tabla} no devolvio columnas")
            for fila in columnas:
                nombre = fila["name"]
                self.assertTrue(nombre.isascii(), f"{tabla}.{nombre} no es ASCII")
                self.assertEqual(nombre, nombre.lower())


class PruebaDeVersionDeEsquema(PruebaConBaseTemporal):

    def test_version_esquema_tiene_su_entero(self):
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertIsInstance(version_de_la_base(self.conexion), int)

    def test_la_fila_de_version_trae_fecha_y_descripcion(self):
        fila = self.conexion.execute(
            "SELECT version, aplicada_en, descripcion FROM version_esquema WHERE version = ?",
            (VERSION_ACTUAL,),
        ).fetchone()
        self.assertIsNotNone(fila)
        self.assertRegex(fila["aplicada_en"], r"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$")
        self.assertTrue(fila["descripcion"].strip())


class PruebaDeIdempotencia(PruebaConBaseTemporal):

    def test_aplicar_el_esquema_dos_veces_no_duplica_nada(self):
        alta_de_caso(self.conexion, **caso_de_ejemplo())
        casos_antes = self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
        versiones_antes = self.conexion.execute("SELECT COUNT(*) FROM version_esquema").fetchone()[0]
        tablas_antes = _tablas_de_la_base(self.conexion)

        aplicar_esquema(self.conexion)

        self.assertEqual(casos_antes, self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])
        self.assertEqual(
            versiones_antes,
            self.conexion.execute("SELECT COUNT(*) FROM version_esquema").fetchone()[0],
        )
        self.assertEqual(tablas_antes, _tablas_de_la_base(self.conexion))


class PruebaDeClavesForaneas(PruebaConBaseTemporal):

    def test_el_pragma_de_claves_foraneas_esta_encendido(self):
        self.assertEqual(1, self.conexion.execute("PRAGMA foreign_keys").fetchone()[0])

    def test_no_se_puede_borrar_un_caso_que_tiene_personas(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        alta_de_persona(self.conexion, resultado.id, nombre="Jose Miguel Anonimo")

        with self.assertRaises(sqlite3.IntegrityError) as capturado:
            self.conexion.execute("DELETE FROM casos WHERE id = ?", (resultado.id,))
        self.assertIn("FOREIGN KEY constraint failed", str(capturado.exception))

    def test_un_caso_sin_personas_si_se_puede_borrar(self):
        resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        self.conexion.execute("DELETE FROM casos WHERE id = ?", (resultado.id,))
        self.assertEqual(0, self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0])

    def test_una_persona_no_puede_apuntar_a_un_caso_inexistente(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO personas (caso_id, nombre) VALUES (?, ?)", (9999, "Fantasma")
            )


class PruebaDeModoDeDiario(PruebaConBaseTemporal):

    def test_el_modo_de_diario_es_el_de_por_defecto_y_no_wal(self):
        modo = self.conexion.execute("PRAGMA journal_mode").fetchone()[0]
        self.assertEqual("delete", modo)


class PruebaDeArranqueRepetido(unittest.TestCase):

    def test_dos_arranques_seguidos_dejan_el_mismo_conteo(self):
        import shutil
        import tempfile
        from pathlib import Path

        carpeta = Path(tempfile.mkdtemp(prefix="fichas_arranque_"))
        try:
            ruta = carpeta / "fichas.db"

            primera = abrir_conexion(ruta)
            aplicar_esquema(primera)
            alta_de_caso(primera, **caso_de_ejemplo())
            conteo_primero = primera.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
            primera.close()

            segunda = abrir_conexion(ruta)
            aplicar_esquema(segunda)
            conteo_segundo = segunda.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
            tablas = _tablas_de_la_base(segunda)
            segunda.close()

            self.assertEqual(conteo_primero, conteo_segundo)
            self.assertEqual(1, conteo_segundo)
            self.assertEqual(len(TABLAS_DEL_ESQUEMA), len(tablas))
        finally:
            shutil.rmtree(carpeta, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
