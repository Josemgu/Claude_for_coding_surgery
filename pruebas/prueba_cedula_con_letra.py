"""La cedula de miembro puede terminar en letra, y termina en letra a menudo.

**El hecho, medido antes de escribir una linea de esto.** Se importaron los siete
PDF reales del dueno sobre una base temporal: 5 de 7 cedulas se guardaban y 2
quedaban vacias. Las dos perdidas tienen la forma `DDD-DDDD-DDDL` en el papel —el
OCR las leyo bien— y las tiraba la regla vieja de «11 digitos en patron 3-4-4».

**Lo que dijo el dueno el 2026-09-04, literal:** «Muchas cedulas de miembro tienen
una A u otra letra al final». Las dos mitades de esa frase mandan aqui:

  - «u otra letra»: no hay lista blanca de letras. Cualquiera vale.
  - «muchas»: NO es un caso raro. Una cedula terminada en letra se guarda como
    cualquier otra, sin aviso y sin marca de duda. Lo de senalar el campo queda
    solo para lo que de verdad no encaja con el formato.

El criterio que estas pruebas derivan, y de donde sale cada uno:

  1. El formato aceptado es 3 digitos, guion, 4 digitos, guion, 3 digitos y un
     ultimo caracter que puede ser digito **o letra**.
  2. La letra no se normaliza ni se corrige: se guarda tal como venia.
  3. Las TRES definiciones de la regla —el validador, el normalizador y el `CHECK`
     del motor— dicen lo mismo. Que se puedan separar ya paso una vez en este
     proyecto con `unidad_numero`, y el sintoma fue un dato verdadero tirado.
  4. Una base que ya existia, con datos dentro, migra sin perder nada.
  5. La clave del Excel de los companeros —`CASO:MRN:ID`— sigue entera con la
     letra dentro.
  6. Lo que NO encaje con el formato deja de perderse en silencio: la pantalla de
     correccion ensena lo que el lector leyo, junto al campo.
"""

import shutil
import sqlite3
import string
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
from datos.validacion import ErrorDeValidacion, validar_mrn
from extraccion.normalizacion import normalizar_fecha, normalizar_mrn
from interfaz.campo import linea_de_lo_que_se_leyo
from paquete.columnas import COLUMNAS, TEXTO, armar_la_clave, partir_la_clave

# Las dos formas que el papel trae, sacadas de los escaneos reales. La segunda es
# la que se perdia: tres digitos y una letra en el ultimo grupo.
CEDULA_TODA_DIGITOS = "055-1111-3853"
CEDULA_CON_LETRA = "066-2222-133A"

# Version del esquema desde la que se migra en estas pruebas: la ultima que
# todavia rechazaba la letra.
VERSION_QUE_RECHAZABA_LA_LETRA = 14


class PruebaDelFormatoQueValida(unittest.TestCase):
    """Criterio 1 y 2, en `datos/validacion.py`."""

    def test_una_cedula_toda_de_digitos_se_sigue_aceptando(self):
        self.assertEqual(CEDULA_TODA_DIGITOS, validar_mrn(CEDULA_TODA_DIGITOS))

    def test_una_cedula_terminada_en_letra_se_acepta(self):
        self.assertEqual(CEDULA_CON_LETRA, validar_mrn(CEDULA_CON_LETRA))

    def test_vale_cualquier_letra_y_no_solo_la_a(self):
        """«una A u otra letra al final» (dueno, 2026-09-04). Sin lista blanca."""
        for letra in string.ascii_uppercase:
            with self.subTest(letra=letra):
                cedula = f"066-2222-133{letra}"
                self.assertEqual(cedula, validar_mrn(cedula))

    def test_una_letra_minuscula_se_guarda_tal_cual_y_no_se_sube_a_mayuscula(self):
        """No se normaliza. Si el papel trae minuscula, se guarda minuscula."""
        self.assertEqual("066-2222-133a", validar_mrn("066-2222-133a"))

    def test_una_letra_en_otra_posicion_se_sigue_rechazando(self):
        for cedula in ("066-222A-1330", "06A-2222-1330", "066-2222-1A30"):
            with self.subTest(cedula=cedula):
                with self.assertRaises(ErrorDeValidacion):
                    validar_mrn(cedula)

    def test_dos_letras_al_final_se_rechazan(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_mrn("066-2222-13AB")

    def test_diez_caracteres_se_siguen_rechazando(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_mrn("055-1111-385")

    def test_sin_guiones_se_sigue_rechazando(self):
        with self.assertRaises(ErrorDeValidacion):
            validar_mrn("055111138 53")

    def test_el_mensaje_deja_de_decir_once_digitos_y_trae_las_dos_formas(self):
        """Criterio 3 del pase: el texto de error dice el formato real."""
        with self.assertRaises(ErrorDeValidacion) as capturado:
            validar_mrn("055-1111-385")
        mensaje = str(capturado.exception)
        self.assertIn("mrn", mensaje)
        self.assertNotIn("11 dígitos", mensaje)
        self.assertIn(CEDULA_TODA_DIGITOS, mensaje)
        self.assertIn(CEDULA_CON_LETRA, mensaje)
        self.assertIn("letra", mensaje)


class PruebaDelFormatoQueSeLee(unittest.TestCase):
    """Criterio 1 y 2, en `extraccion/normalizacion.py`."""

    def test_una_cedula_terminada_en_letra_se_lee_entera(self):
        self.assertEqual(CEDULA_CON_LETRA, normalizar_mrn(CEDULA_CON_LETRA))

    def test_la_letra_no_se_pasa_a_mayuscula(self):
        self.assertEqual("066-2222-133a", normalizar_mrn("066-2222-133a"))

    def test_se_lee_aunque_lleve_texto_pegado_como_en_el_papel(self):
        self.assertEqual(
            CEDULA_CON_LETRA,
            normalizar_mrn(f"Elder Ejemplo Apellido {CEDULA_CON_LETRA} Complete"),
        )

    def test_no_se_recorta_la_letra_para_hacerla_encajar(self):
        """Recortar `066-2222-1330A` a `066-2222-1330` seria inventar un dato."""
        self.assertIsNone(normalizar_mrn("066-2222-1330A"))

    def test_una_cedula_con_letra_sigue_sin_leerse_como_fecha(self):
        """La columna de cedulas esta llena de cadenas con dos guiones."""
        self.assertIsNone(normalizar_fecha(CEDULA_CON_LETRA))


class PruebaDeQueLasTresDefinicionesNoSePuedenSeparar(unittest.TestCase):
    """Criterio 3: el validador, el normalizador y el `CHECK` dicen lo mismo.

    Es la prueba que faltaba cuando `unidad_numero` se rompio: el validador
    aceptaba siete digitos y el normalizador los tiraba, y nadie lo vio hasta
    medirlo sobre papeles reales.
    """

    # Cada caso es (texto, si_deberia_aceptarse). No se generan: se escriben, para
    # que el que lea la prueba vea que forma se esta afirmando.
    CASOS = (
        (CEDULA_TODA_DIGITOS, True),
        (CEDULA_CON_LETRA, True),
        ("066-2222-133a", True),
        ("999-9999-9999", True),
        ("066-222A-1330", False),
        ("066-2222-13AB", False),
        ("055-1111-385", False),
        ("0055-1111-3853", False),
    )

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_cedula_"))
        self.conexion = abrir_conexion(self.carpeta_temporal / "fichas.db")
        aplicar_esquema(self.conexion)
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', ?)",
            (marca_de_tiempo(),),
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    def _el_motor_lo_acepta(self, mrn, fila):
        try:
            self.conexion.execute(
                "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
                "VALUES (1, ?, 'Ejemplo', ?)",
                (mrn, fila),
            )
        except sqlite3.IntegrityError:
            return False
        return True

    def _el_validador_lo_acepta(self, mrn):
        try:
            validar_mrn(mrn)
        except ErrorDeValidacion:
            return False
        return True

    def test_las_tres_dan_la_misma_respuesta_en_cada_forma(self):
        for fila, (texto, se_acepta) in enumerate(self.CASOS, start=1):
            with self.subTest(texto=texto):
                self.assertEqual(se_acepta, self._el_validador_lo_acepta(texto),
                                 "el validador no dice lo que el caso afirma")
                self.assertEqual(se_acepta, normalizar_mrn(texto) == texto,
                                 "el normalizador no dice lo que el caso afirma")
                self.assertEqual(se_acepta, self._el_motor_lo_acepta(texto, fila),
                                 "el CHECK del motor no dice lo que el caso afirma")


class PruebaDeUnaBaseQueYaExistia(unittest.TestCase):
    """Criterio 4: una base con datos dentro sube de version sin perder nada."""

    def setUp(self):
        self.carpeta_temporal = Path(tempfile.mkdtemp(prefix="fichas_migra_letra_"))
        self.ruta_de_la_base = self.carpeta_temporal / "fichas.db"
        self.conexion = abrir_conexion(self.ruta_de_la_base)

        # La base vieja se construye a mano, parandola en la version 14: el DDL de
        # la version 1 y las migraciones de la 2 a la 14, ni una mas. Llamar a
        # `aplicar_esquema` aqui ya migraria, y entonces la prueba comprobaria el
        # camino facil y no el que importa. Es el mismo procedimiento de
        # `pruebas/prueba_migraciones.py`.
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
            "VALUES (?, ?, ?)",
            (VERSION_INICIAL, marca_de_tiempo(), "Esquema inicial de la prueba."),
        )
        for version, descripcion, migrar in MIGRACIONES:
            if version > VERSION_QUE_RECHAZABA_LA_LETRA:
                break
            migrar(self.conexion)
            self.conexion.execute(
                "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
                "VALUES (?, ?, ?)",
                (version, marca_de_tiempo(), descripcion),
            )
        self.conexion.commit()

        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', ?)",
            (marca_de_tiempo(),),
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, ?, 'Persona Que Ya Estaba', 1)",
            (CEDULA_TODA_DIGITOS,),
        )
        self.conexion.commit()

        # La base vieja RECHAZABA la letra. Sin comprobarlo, la prueba de que
        # ahora entra no dice nada: podria estar entrando en una base que ya la
        # admitia.
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
                "VALUES (1, ?, 'La Que No Entraba', 99)",
                (CEDULA_CON_LETRA,),
            )
        self.conexion.rollback()

        aplicar_esquema(self.conexion)

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta_temporal, ignore_errors=True)

    def _check_de_personas(self):
        return self.conexion.execute(
            "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = 'personas'"
        ).fetchone()[0]

    def test_la_base_sube_hasta_la_version_actual(self):
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertGreater(VERSION_ACTUAL, VERSION_QUE_RECHAZABA_LA_LETRA)

    def test_despues_de_migrar_entra_una_cedula_con_letra(self):
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, ?, 'Persona Nueva', 2)",
            (CEDULA_CON_LETRA,),
        )
        guardada = self.conexion.execute(
            "SELECT mrn FROM personas WHERE fila_formulario = 2"
        ).fetchone()[0]
        self.assertEqual(CEDULA_CON_LETRA, guardada)

    def test_una_cedula_mal_formada_la_sigue_parando_el_motor(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
                "VALUES (1, '066-222A-1330', 'Persona Nueva', 3)"
            )

    def test_lo_que_ya_estaba_guardado_sobrevive_entero(self):
        fila = self.conexion.execute(
            "SELECT mrn, nombre, fila_formulario, caso_id FROM personas"
        ).fetchall()
        self.assertEqual(1, len(fila))
        self.assertEqual(CEDULA_TODA_DIGITOS, fila[0]["mrn"])
        self.assertEqual("Persona Que Ya Estaba", fila[0]["nombre"])
        self.assertEqual(1, fila[0]["fila_formulario"])

    def test_las_personas_siguen_colgando_de_su_caso(self):
        self.assertEqual(
            1,
            self.conexion.execute(
                "SELECT COUNT(*) FROM personas p JOIN casos c ON c.id = p.caso_id"
            ).fetchone()[0],
        )

    def test_el_restrict_sigue_impidiendo_borrar_un_caso_con_personas(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute("DELETE FROM casos WHERE id = 1")

    def test_no_se_pueden_repetir_dos_cedulas_iguales_en_el_mismo_caso(self):
        """El `UNIQUE (caso_id, mrn)` es la mitad de la clave del Excel."""
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
                "VALUES (1, ?, 'Otra Persona', 9)",
                (CEDULA_TODA_DIGITOS,),
            )

    def test_las_claves_foraneas_quedan_encendidas_al_terminar(self):
        self.assertEqual(1, self.conexion.execute("PRAGMA foreign_keys").fetchone()[0])

    def test_el_check_guardado_en_la_base_admite_el_ultimo_caracter_con_letra(self):
        self.assertIn("A-Za-z", self._check_de_personas())

    def test_aplicar_el_esquema_dos_veces_no_rompe_nada(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(VERSION_ACTUAL, version_de_la_base(self.conexion))
        self.assertEqual(
            1, self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0]
        )


class PruebaDeLaClaveDelExcel(unittest.TestCase):
    """Criterio 5: `CASO:MRN:ID` sigue entera con la letra dentro."""

    def test_la_clave_se_arma_y_se_parte_con_la_letra_dentro(self):
        clave = armar_la_clave("CASP2609", CEDULA_CON_LETRA, 12)
        self.assertEqual(f"CASP2609:{CEDULA_CON_LETRA}:12", clave)
        self.assertEqual(("CASP2609", CEDULA_CON_LETRA, 12), partir_la_clave(clave))

    def test_la_letra_no_se_pierde_al_partir_la_clave(self):
        _, mrn, _ = partir_la_clave(f"CASP2609:{CEDULA_CON_LETRA}:12")
        self.assertTrue(mrn.endswith("A"))

    def test_la_columna_de_la_cedula_sigue_yendo_en_formato_de_texto(self):
        """Sin `number_format='@'`, Excel se come los ceros de delante."""
        columna = next(columna for columna in COLUMNAS if columna.nombre == "mrn")
        self.assertEqual(TEXTO, columna.clase)


class PruebaDeQueNadaSePierdeEnSilencio(unittest.TestCase):
    """Criterio 6: la pantalla de correccion ensena lo que el lector leyo."""

    def test_un_campo_vacio_con_lectura_ensena_lo_que_se_leyo(self):
        linea = linea_de_lo_que_se_leyo(
            None, {"origen": "vacio", "valor_ocr": "066-2222-9XYZ", "anulado_por_tachon": 0}
        )
        self.assertIn("066-2222-9XYZ", linea)

    def test_una_cedula_con_letra_guardada_no_se_senala_de_ninguna_forma(self):
        """«Muchas cedulas tienen una A u otra letra»: es formato normal."""
        linea = linea_de_lo_que_se_leyo(
            CEDULA_CON_LETRA,
            {"origen": "ocr", "valor_ocr": CEDULA_CON_LETRA, "anulado_por_tachon": 0},
        )
        self.assertEqual("", linea)

    def test_un_campo_tachado_no_resucita_lo_que_alguien_tacho(self):
        """`extraccion/campos.py` punto 4: el valor tachado NO se recupera nunca."""
        linea = linea_de_lo_que_se_leyo(
            None,
            {"origen": "vacio", "valor_ocr": "066-2222-1330", "anulado_por_tachon": 1},
        )
        self.assertEqual("", linea)

    def test_un_campo_que_vacio_una_mano_no_dice_que_se_perdio_algo(self):
        """`origen='manual'`: alguien decidio que ahi no va nada. No es una perdida."""
        linea = linea_de_lo_que_se_leyo(
            None,
            {"origen": "manual", "valor_ocr": "066-2222-9XYZ", "anulado_por_tachon": 0},
        )
        self.assertEqual("", linea)

    def test_un_campo_vacio_sin_lectura_no_inventa_una_linea(self):
        for procedencia in (
            None,
            {"origen": "vacio", "valor_ocr": None, "anulado_por_tachon": 0},
            {"origen": "vacio", "valor_ocr": "   ", "anulado_por_tachon": 0},
        ):
            with self.subTest(procedencia=procedencia):
                self.assertEqual("", linea_de_lo_que_se_leyo(None, procedencia))


if __name__ == "__main__":
    unittest.main()
