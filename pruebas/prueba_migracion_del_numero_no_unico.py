"""Que las versiones 12 y 13 convierten una base CON DATOS sin perder una fila.

El criterio que hay que defender no es «el codigo nuevo admite un numero repetido»
—eso lo prueban `prueba_dos_familias_un_numero.py` y `prueba_repositorio.py`—, sino
el que de verdad se puede romper: **la base que el dueno tiene en
`Documentos\\Fichas`, con casos, personas, companeros, asignaciones, contactos,
procedencia y renglones dentro, queda migrada y no se pierde NI UNA fila.**

La version 12 reconstruye `casos` entera con el procedimiento de 12 pasos de
SQLite, y una reconstruccion es exactamente donde se pierden columnas y donde se
rompen las claves foraneas de las cuatro tablas que apuntan a `casos`.

Por eso esta prueba construye la base vieja aplicando las migraciones 2 a 11 una a
una y parandose ahi, en vez de llamar a `aplicar_esquema`: llamar a
`aplicar_esquema` ya migraria hasta la ultima, y entonces se estaria probando el
camino facil y no el que importa.
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

# La ultima version que existia antes de este cambio. Una base del dueno
# actualizada a la entrega anterior esta exactamente aqui.
VERSION_ANTERIOR_AL_CAMBIO = 11

# Las tablas cuyas filas se cuentan antes y despues. `casos` va la primera porque
# es la que se reconstruye; las otras porque cuatro de ellas apuntan a `casos` con
# `RESTRICT` y una reconstruccion mal hecha las deja huerfanas.
TABLAS_QUE_NO_PUEDEN_PERDER_FILAS = (
    "casos",
    "personas",
    "companeros",
    "asignaciones",
    "contactos",
    "procedencia_campo",
    "documentos_ilegibles",
    "filas_descartadas",
)


def _contar(conexion, tabla):
    """Cuenta las filas de una tabla nombrada, con cada consulta escrita entera.

    ⚠️ **Ocho `if` y no un diccionario de consultas**, aunque el diccionario se
    lea mejor. `pruebas/auditoria_sql.py` enumera las llamadas al motor leyendo el
    arbol de sintaxis y solo da por conforme la instruccion que puede seguir hasta
    una cadena literal; con `conexion.execute(consultas[tabla])` dictamina «NO
    RESUELTA: el argumento es un Subscript que no se puede seguir». Medido: con el
    diccionario, la auditoria pasaba de 384 llamadas conformes a 383 y el veredicto
    era NO PASA. Una lista blanca que admite excepciones deja de ser una lista
    blanca, y aqui el que cede es este archivo.
    """
    if tabla == "casos":
        return conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
    if tabla == "personas":
        return conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0]
    if tabla == "companeros":
        return conexion.execute("SELECT COUNT(*) FROM companeros").fetchone()[0]
    if tabla == "asignaciones":
        return conexion.execute("SELECT COUNT(*) FROM asignaciones").fetchone()[0]
    if tabla == "contactos":
        return conexion.execute("SELECT COUNT(*) FROM contactos").fetchone()[0]
    if tabla == "procedencia_campo":
        return conexion.execute("SELECT COUNT(*) FROM procedencia_campo").fetchone()[0]
    if tabla == "documentos_ilegibles":
        return conexion.execute("SELECT COUNT(*) FROM documentos_ilegibles").fetchone()[0]
    if tabla == "filas_descartadas":
        return conexion.execute("SELECT COUNT(*) FROM filas_descartadas").fetchone()[0]
    raise AssertionError(f"La prueba no sabe contar la tabla {tabla!r}.")


class PruebaDeLaBaseEnLaVersionOnce(unittest.TestCase):
    """Una base como la que hay hoy en la maquina del dueno, con datos dentro."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_migracion_12_"))
        self.conexion = abrir_conexion(self.carpeta_temporal / "fichas.db")
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) VALUES (?, ?, ?)",
            (VERSION_INICIAL, marca_de_tiempo(), "Esquema inicial de la prueba."),
        )
        for version, descripcion, aplicar in MIGRACIONES:
            if version > VERSION_ANTERIOR_AL_CAMBIO:
                break
            aplicar(self.conexion)
            self.conexion.execute(
                "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
                "VALUES (?, ?, ?)",
                (version, marca_de_tiempo(), descripcion),
            )
        self._meter_datos()
        self.antes = {
            tabla: _contar(self.conexion, tabla)
            for tabla in TABLAS_QUE_NO_PUEDEN_PERDER_FILAS
        }

    def _meter_datos(self):
        """Una fila en cada tabla que apunta a `casos`, y dos casos, y un archivado.

        El caso archivado esta a proposito: lleva el `CHECK` de pareja
        —archivado con fecha, o ninguno de los dos— que la reconstruccion podria
        perder, y un `CHECK` perdido no se nota hasta el dia que hace falta.
        """
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, creado_en, "
            "pagina_pdf, unidad_nombre, templo_nombre) VALUES "
            "('CASP2609', '7000011', '2026-09-08', '2026-09-02 10:00:00', 3, "
            "'Paramaribo Branch', 'Templo de Santo Domingo')"
        )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en, archivado, fecha_archivado) "
            "VALUES ('CASD2609', '2026-09-02 10:00:00', 1, '2026-09-03 09:00:00')"
        )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES (NULL, '2026-09-02 10:00:00')"
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, '055-1111-3853', 'ANONIMO, M', 1)"
        )
        self.conexion.execute(
            "INSERT INTO companeros (nombre, creado_en) VALUES ('Ana Pérez', '2026-09-02 10:00:00')"
        )
        self.conexion.execute(
            "INSERT INTO asignaciones (caso_id, companero_id, asignado_en, activa) "
            "VALUES (1, 1, '2026-09-02 10:00:00', 1)"
        )
        self.conexion.execute(
            "INSERT INTO contactos (caso_id, fecha, medio, resultado, registrado_en, "
            "contactado_por) VALUES (1, '2026-09-02', 'llamada', 'contactado', "
            "'2026-09-02 10:00:00', 1)"
        )
        self.conexion.execute(
            "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen, "
            "confianza) VALUES ('casos', 1, 'numero_caso', 'ocr', 0.9)"
        )
        self.conexion.execute(
            "INSERT INTO documentos_ilegibles (ruta_pdf, pagina_pdf, motivo, "
            "registrado_en, caso_id) VALUES ('C:\\Escaneos\\uno.pdf', 1, 'sin_texto', "
            "'2026-09-02 10:00:00', 1)"
        )
        self.conexion.execute(
            "INSERT INTO filas_descartadas (companero_id, motivo, registrado_en) "
            "VALUES (1, 'la clave no tiene la forma esperada', '2026-09-02 10:00:00')"
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    # ---- control positivo -------------------------------------------------

    def test_la_base_vieja_de_verdad_exigia_que_el_numero_fuera_unico(self):
        """Sin esto, todo lo de abajo podria estar probando nada."""
        self.assertEqual(VERSION_ANTERIOR_AL_CAMBIO, version_de_la_base(self.conexion))
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-02 11:00:00')"
            )

    def test_la_base_vieja_no_tenia_la_columna_del_duplicado(self):
        with self.assertRaises(sqlite3.OperationalError):
            self.conexion.execute("SELECT duplicado_de FROM casos")

    # ---- lo que la migracion tiene que dejar ------------------------------

    def test_la_base_sube_hasta_la_ultima_version(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertGreaterEqual(VERSION_ACTUAL, 13)

    def test_no_se_pierde_ni_una_fila_de_ninguna_tabla(self):
        """Es el criterio entero: una reconstruccion es donde se pierden filas."""
        aplicar_esquema(self.conexion)
        despues = {
            tabla: _contar(self.conexion, tabla)
            for tabla in TABLAS_QUE_NO_PUEDEN_PERDER_FILAS
        }
        self.assertEqual(self.antes, despues)

    def test_el_caso_que_ya_estaba_conserva_todas_sus_columnas(self):
        """Una migracion que reconstruye la tabla es donde se pierden columnas."""
        aplicar_esquema(self.conexion)
        caso = self.conexion.execute(
            "SELECT numero_caso, unidad_numero, fecha_viaje, creado_en, pagina_pdf, "
            "unidad_nombre, templo_nombre, archivado, duplicado_de FROM casos WHERE id = 1"
        ).fetchone()
        self.assertEqual(
            (
                caso["numero_caso"],
                caso["unidad_numero"],
                caso["fecha_viaje"],
                caso["pagina_pdf"],
                caso["unidad_nombre"],
                caso["templo_nombre"],
                caso["archivado"],
            ),
            ("CASP2609", "7000011", "2026-09-08", 3, "Paramaribo Branch",
             "Templo de Santo Domingo", 0),
        )
        self.assertIsNone(caso["duplicado_de"])

    def test_el_caso_archivado_sigue_archivado_con_su_fecha(self):
        aplicar_esquema(self.conexion)
        fila = self.conexion.execute(
            "SELECT archivado, fecha_archivado FROM casos WHERE numero_caso = 'CASD2609'"
        ).fetchone()
        self.assertEqual((fila["archivado"], fila["fecha_archivado"]),
                         (1, "2026-09-03 09:00:00"))

    def test_el_caso_sin_numero_sigue_sin_numero_y_no_se_le_invento_uno(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso IS NULL"
            ).fetchone()[0],
            1,
        )

    def test_ninguna_fila_queda_huerfana(self):
        """Cuatro tablas apuntan a `casos` con `RESTRICT`, y la tabla se rehizo."""
        aplicar_esquema(self.conexion)
        self.assertEqual(self.conexion.execute("PRAGMA foreign_key_check").fetchall(), [])

    def test_las_claves_foraneas_quedan_encendidas(self):
        """Una base migrada con las claves apagadas es peor que no haber migrado."""
        aplicar_esquema(self.conexion)
        self.assertEqual(self.conexion.execute("PRAGMA foreign_keys").fetchone()[0], 1)

    # ---- lo que la migracion tiene que cambiar, y lo que NO ---------------

    def test_despues_de_migrar_el_numero_repetido_entra(self):
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-02 11:00:00')"
        )
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso = 'CASP2609'"
            ).fetchone()[0],
            2,
        )

    def test_el_CHECK_de_la_forma_del_numero_NO_se_aflojo(self):
        """Que el numero no sea unico no significa que valga cualquier cosa."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES ('casp2609', '2026-09-02 11:00:00')"
            )

    def test_el_CHECK_del_archivado_sigue_en_la_tabla(self):
        """Archivado sin fecha no existe, y la reconstruccion podria perderlo."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute("UPDATE casos SET archivado = 1 WHERE id = 1")

    def test_la_unidad_de_siete_digitos_sigue_entrando(self):
        """El `CHECK` de la version 2 tiene que sobrevivir a la reconstruccion."""
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
            "VALUES ('CASE2609', '7000011', '2026-09-02 11:00:00')"
        )

    def test_el_indice_de_los_viajes_activos_se_vuelve_a_crear(self):
        """Paso 8 del procedimiento oficial: los indices se rehacen a mano."""
        aplicar_esquema(self.conexion)
        indices = {
            fila["name"]
            for fila in self.conexion.execute(
                "SELECT name FROM sqlite_schema WHERE type = 'index' AND tbl_name = 'casos'"
            )
        }
        self.assertIn("idx_casos_viaje_activos", indices)
        self.assertIn("idx_casos_numero", indices)

    def test_el_ddl_guardado_ya_no_dice_UNIQUE_en_el_numero(self):
        """Contra la divergencia que `CLAUDE.md` §8 manda evitar."""
        aplicar_esquema(self.conexion)
        sql = self.conexion.execute(
            "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = 'casos'"
        ).fetchone()[0]
        self.assertNotIn("numero_caso          TEXT    UNIQUE", sql)
        self.assertIn("duplicado_de", sql)

    def test_un_duplicado_que_apunta_a_un_caso_que_no_existe_se_rechaza(self):
        """El `REFERENCES` de la version 13 tiene que estar vivo, no decorativo."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en, duplicado_de) "
                "VALUES ('CASF2609', '2026-09-02 11:00:00', 9999)"
            )

    def test_migrar_dos_veces_no_cambia_nada(self):
        """Arrancar el programa dos veces sobre la misma base no puede tocar datos."""
        aplicar_esquema(self.conexion)
        primera = {
            tabla: _contar(self.conexion, tabla)
            for tabla in TABLAS_QUE_NO_PUEDEN_PERDER_FILAS
        }
        aplicar_esquema(self.conexion)
        segunda = {
            tabla: _contar(self.conexion, tabla)
            for tabla in TABLAS_QUE_NO_PUEDEN_PERDER_FILAS
        }
        self.assertEqual(primera, segunda)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))


if __name__ == "__main__":
    unittest.main()
