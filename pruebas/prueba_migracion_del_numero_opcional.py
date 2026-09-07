"""Que la version 7 convierte una base que YA existia sin perder nada.

El criterio que hay que defender no es «el codigo nuevo acepta un numero vacio»
—eso lo prueba `prueba_paginas_sin_numero.py`—, sino el que de verdad se puede
romper: **la base que el dueno tiene en `Documentos\\Fichas`, en la version 6 y con
datos dentro, queda migrada, con sus datos intactos, con sus claves foraneas vivas
y con el `UNIQUE` y el `CHECK` de `numero_caso` todavia haciendo su trabajo.**

Por eso esta prueba construye la base vieja aplicando las migraciones 2 a 6 una a
una y parandose ahi, en vez de llamar a `aplicar_esquema`: llamar a
`aplicar_esquema` ya migraria hasta la 8, y entonces se estaria probando el camino
facil y no el que importa.

Se midio antes de escribir la migracion, en esta maquina (SQLite 3.50.4), que un
`UNIQUE` de SQLite admite varios nulos y sigue rechazando los repetidos que no lo
son. Estas pruebas lo vuelven a comprobar sobre la tabla de verdad, que es donde
cuenta.
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

VERSION_DE_LA_BASE_DEL_DUENO = 6


def _sql_de_la_tabla(conexion, nombre):
    """El DDL literal de esa tabla tal como esta guardado en la base."""
    return conexion.execute(
        "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = ?", (nombre,)
    ).fetchone()[0]


class PruebaDeLaBaseEnLaVersionSeis(unittest.TestCase):
    """Una base como la que hay hoy en la maquina del dueno, con datos dentro."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_migracion_7_"))
        self.conexion = abrir_conexion(self.carpeta_temporal / "fichas.db")
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) VALUES (?, ?, ?)",
            (VERSION_INICIAL, marca_de_tiempo(), "Esquema inicial de la prueba."),
        )
        for version, descripcion, aplicar in MIGRACIONES:
            if version > VERSION_DE_LA_BASE_DEL_DUENO:
                break
            aplicar(self.conexion)
            self.conexion.execute(
                "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
                "VALUES (?, ?, ?)",
                (version, marca_de_tiempo(), descripcion),
            )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, creado_en, "
            "pagina_pdf, unidad_nombre) VALUES ('CASP2609', '7000011', '2026-09-08', "
            "'2026-09-02 10:00:00', 3, 'Paramaribo Branch')"
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, '055-1111-3853', 'ANONIMO, M', 1)"
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    # ---- control positivo -------------------------------------------------

    def test_la_base_vieja_de_verdad_exigia_el_numero_de_caso(self):
        """Sin esto, todo lo de abajo podria estar probando nada."""
        self.assertEqual(VERSION_DE_LA_BASE_DEL_DUENO, version_de_la_base(self.conexion))
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES (NULL, '2026-09-02 10:00:00')"
            )

    # ---- lo que la migracion tiene que dejar ------------------------------

    def test_al_aplicar_el_esquema_la_base_sube_hasta_la_ultima_version(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertGreaterEqual(VERSION_ACTUAL, 8)

    def test_el_caso_que_ya_estaba_sigue_entero_con_sus_doce_columnas(self):
        """Una migracion que reconstruye la tabla es donde se pierden columnas."""
        aplicar_esquema(self.conexion)
        caso = self.conexion.execute("SELECT * FROM casos WHERE id = 1").fetchone()
        self.assertEqual(caso["numero_caso"], "CASP2609")
        self.assertEqual(caso["unidad_numero"], "7000011")
        self.assertEqual(caso["fecha_viaje"], "2026-09-08")
        self.assertEqual(caso["pagina_pdf"], 3)
        self.assertEqual(caso["unidad_nombre"], "Paramaribo Branch")
        self.assertEqual(caso["creado_en"], "2026-09-02 10:00:00")

    def test_la_persona_sigue_colgando_de_su_caso(self):
        """`casos` se borra y se vuelve a crear: la clave foranea puede quedar rota."""
        aplicar_esquema(self.conexion)
        self.assertEqual(
            self.conexion.execute("SELECT caso_id FROM personas WHERE id = 1").fetchone()[0], 1
        )
        self.assertEqual(self.conexion.execute("PRAGMA foreign_key_check").fetchall(), [])

    def test_las_claves_foraneas_quedan_encendidas_despues_de_migrar(self):
        """El procedimiento oficial las apaga; dejarlas apagadas seria peor."""
        aplicar_esquema(self.conexion)
        self.assertEqual(self.conexion.execute("PRAGMA foreign_keys").fetchone()[0], 1)

    def test_el_indice_de_los_viajes_activos_se_vuelve_a_crear(self):
        """`DROP TABLE` se lleva los indices por delante."""
        aplicar_esquema(self.conexion)
        indices = [
            fila[0]
            for fila in self.conexion.execute(
                "SELECT name FROM sqlite_schema WHERE type = 'index' AND tbl_name = 'casos'"
            ).fetchall()
        ]
        self.assertIn("idx_casos_viaje_activos", indices)

    # ---- lo que la migracion NO puede aflojar -----------------------------

    def test_despues_de_migrar_el_numero_de_caso_puede_faltar(self):
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES (NULL, '2026-09-02 11:00:00')"
        )
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso IS NULL"
            ).fetchone()[0],
            1,
        )

    def test_varios_casos_pueden_estar_sin_numero_a_la_vez(self):
        """Un `UNIQUE` que contara los nulos como iguales dejaria solo uno.

        Es lo que se midio antes de escribir la migracion, comprobado aqui sobre la
        tabla de verdad: en una tanda de 500 documentos puede haber decenas sin
        numero, y si solo entrara el primero el arreglo no serviria de nada.
        """
        aplicar_esquema(self.conexion)
        for _ in range(3):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES (NULL, '2026-09-02 11:00:00')"
            )
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso IS NULL"
            ).fetchone()[0],
            3,
        )

    def test_un_numero_repetido_ya_NO_se_rechaza(self):
        """La version 12 quito el `UNIQUE`, y esta prueba dio la vuelta con el.

        Hasta el 2026-09-03 exigia lo contrario. El criterio nuevo lo puso el dueno
        con una medicion en la PC del trabajo: con `UNIQUE`, seis de diez documentos
        de su carpeta real se rechazaban enteros, porque el numero de caso son
        cuatro letras mas el ano y el mes —identifica una unidad y un mes, no una
        familia— y muchos documentos distintos lo comparten.
        """
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-02 11:00:00')"
        )
        self.assertGreaterEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso = 'CASP2609'"
            ).fetchone()[0],
            2,
        )

    def test_un_numero_con_mala_forma_se_sigue_rechazando(self):
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES ('casp2609', '2026-09-02 11:00:00')"
            )

    def test_la_unidad_de_siete_digitos_de_la_version_2_sigue_entrando(self):
        """La reconstruccion copia el `CHECK` de la version 2; si se cayera, aqui.""" ""
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, creado_en) "
            "VALUES ('CASD2609', '7000011', '2026-09-02 11:00:00')"
        )

    def test_el_check_del_archivado_sigue_en_la_tabla(self):
        """Archivado sin fecha no existe, y la reconstruccion podria perderlo."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute("UPDATE casos SET archivado = 1 WHERE id = 1")

    def test_el_ddl_guardado_dice_lo_mismo_que_el_codigo(self):
        """Contra la divergencia que `CLAUDE.md` §8 manda evitar."""
        aplicar_esquema(self.conexion)
        sql = _sql_de_la_tabla(self.conexion, "casos")
        self.assertIn("numero_caso IS NULL", sql)
        self.assertNotIn("numero_caso          TEXT    NOT NULL", sql)


class PruebaDeLaTablaDeLoQueNoEntro(PruebaDeLaBaseEnLaVersionSeis):
    """La version 8, sobre la misma base vieja."""

    def test_la_tabla_se_crea_al_migrar(self):
        aplicar_esquema(self.conexion)
        self.assertIsNotNone(_sql_de_la_tabla(self.conexion, "documentos_ilegibles"))

    def test_migrar_dos_veces_no_duplica_ni_rompe_nada(self):
        """Arrancar el programa dos veces sobre la misma base es lo normal."""
        aplicar_esquema(self.conexion)
        aplicar_esquema(self.conexion)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )

    def test_el_renglon_puede_no_llevar_pagina_ni_caso(self):
        """Un PDF que no se pudo abrir no tiene ni pagina ni caso que apuntar."""
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO documentos_ilegibles (ruta_pdf, motivo, registrado_en) "
            "VALUES ('C:\\Escaneos\\roto.pdf', 'no_se_pudo_abrir', '2026-09-02 11:00:00')"
        )
        fila = self.conexion.execute("SELECT * FROM documentos_ilegibles").fetchone()
        self.assertIsNone(fila["pagina_pdf"])
        self.assertIsNone(fila["caso_id"])

    def test_un_renglon_no_puede_apuntar_a_un_caso_que_no_existe(self):
        """`REFERENCES` decorativo seria peor que ninguno."""
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO documentos_ilegibles (ruta_pdf, motivo, caso_id, registrado_en) "
                "VALUES ('x.pdf', 'sin_texto', 999, '2026-09-02 11:00:00')"
            )


if __name__ == "__main__":
    unittest.main()
