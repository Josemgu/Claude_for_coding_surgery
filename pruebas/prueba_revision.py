"""Los criterios de la pantalla «Revisar», sin ventana: la migración y los estados.

Cada prueba sale de un punto del pase del 2026-09-03 y no del código que lo cumple.
Los cuatro criterios de cierre, uno a uno:

  1. «Sí, completa» deja `estado_recomendacion = 'completa'` con quién y cuándo;
     «No está completa», `no_completa`; y `casos_en_riesgo` deja fuera lo completo.
  3. La migración aplica sobre una base de la versión anterior sin perder filas.

Y lo que el dueño añadió después: cargar el Excel del compañero marca, firmado con
el nombre del compañero; lo que Miguel marca a mano manda y no borra lo del
compañero; y el volumen —3 000 documentos— con su tiempo medido.

⚠️ La migración es la **14**, no la 13 que pedía el pase: la 12 y la 13 ya estaban
escritas y registradas por otro pase de esta misma sesión. Se aplica a mano porque
todavía no está en `MIGRACIONES`.
"""

import time
import unittest
from datetime import date

from datos.archivo import archivar_caso
from datos.calendario import casos_en_riesgo
from datos.companeros import alta_de_companero
from datos.esquema import VERSION_ACTUAL
from datos.estados import COMPLETA, ESTADOS_QUE_RESUELVEN, NO_COMPLETA
from datos.marcas_de_revision import (
    ORIGEN_A_MANO,
    ErrorDeMarca,
    aplicar_las_marcas_de_la_hoja,
    marcar_a_mano,
)
from datos.migraciones_de_revision import (
    VERSION_DE_LA_REVISION,
    migrar_a_version_14,
)
from datos.pasos import NOMBRES_DE_LOS_PASOS
from datos.revision import (
    SIN_REVISAR,
    cuentas_de_los_filtros,
    documentos_para_revisar,
    filtrar,
)
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 8)
RUTA_DE_LA_HOJA = r"C:\Devueltos\hoja-de-sandy.xlsx"
OTRA_HOJA = r"C:\Devueltos\segunda-hoja.xlsx"

# Las siete columnas que la versión 14 añade, con la tabla en la que van.
def _base_parada_en_la_13():
    """Una base con las migraciones aplicadas hasta la 13 y NI UNA mas.

    ⚠️ Existe desde el 2026-09-03, cuando la 14 quedo registrada en
    `datos/migraciones.py` y `aplicar_esquema` empezo a aplicarla sola. Sin esto,
    las pruebas de la migracion partirian de una base que YA la trae, y estarian
    comprobando que estan las columnas que ellas mismas pidieron poner: verde sin
    medir nada.

    Es el mismo procedimiento que usa `pruebas/prueba_migracion_del_numero_no_unico.py`
    para pararse en la 11: aplicar las migraciones una a una y frenar.

    La conexion queda abierta y la cierra quien llama; el archivo vive en una
    carpeta temporal que el sistema se lleva.
    """
    import tempfile
    from pathlib import Path

    from datos.conexion import abrir_conexion
    from datos.esquema import (
        VERSION_INICIAL,
        crear_tablas_de_la_version_inicial,
        marca_de_tiempo,
    )
    from datos.migraciones import MIGRACIONES

    carpeta = Path(tempfile.mkdtemp(prefix="fichas_parada_13_"))
    conexion = abrir_conexion(carpeta / "fichas.db")
    crear_tablas_de_la_version_inicial(conexion)
    conexion.execute(
        "INSERT INTO version_esquema (version, aplicada_en, descripcion) VALUES (?, ?, ?)",
        (VERSION_INICIAL, marca_de_tiempo(), "Esquema inicial de la prueba."),
    )
    for version, descripcion, aplicar in MIGRACIONES:
        if version > 13:
            break
        aplicar(conexion)
        conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
            "VALUES (?, ?, ?)",
            (version, marca_de_tiempo(), descripcion),
        )
    return conexion


COLUMNAS_NUEVAS = (
    ("casos", "estado_marcado_por"),
    ("casos", "estado_marcado_en"),
    ("casos", "estado_marcado_origen"),
    ("casos", "estado_del_companero"),
    ("casos", "estado_del_companero_por"),
    ("casos", "estado_del_companero_en"),
    ("procedencia_campo", "ausente_en_el_papel"),
)


# El INSERT de una persona con sus seis pasos, escrito letra por letra. Las seis
# columnas se podrían pegar desde `NOMBRES_DE_LOS_PASOS`, y a propósito no se hace:
# la auditoría de SQL lee el árbol de sintaxis y no puede seguir una f-string hasta
# su texto. `test_las_seis_columnas_son_las_de_datos_pasos` comprueba que esta
# constante y esa tupla no se separen.
INSERCION_DE_PERSONA = (
    "INSERT INTO personas (caso_id, nombre, mrn, fila_formulario, propuesto_por, "
    "propuesto_en, paso_preparacion, paso_informacion, paso_cita_del_templo, "
    "paso_acciones_requeridas, paso_entrevistas, paso_listo_para_el_templo) "
    "VALUES (?, ?, ?, 1, ?, ?, ?, ?, ?, ?, ?, ?)"
)


def _valores(caso_id, nombre, mrn, propuesto_por, pasos):
    """Los valores de `INSERCION_DE_PERSONA`, en su orden."""
    return [
        caso_id, nombre, mrn, propuesto_por,
        "2026-09-05 09:00:00" if propuesto_por else None,
    ] + list(pasos)


def _columnas(conexion, tabla):
    """Los nombres de las columnas de una tabla, leídos del motor.

    Las dos tablas van escritas una a una y no interpoladas: `pruebas/auditoria_sql.py`
    dictamina INSEGURA toda instrucción armada con una f-string, y una prueba que
    ensucia esa auditoría es una prueba que enseña a saltársela.
    """
    if tabla == "casos":
        filas = conexion.execute("PRAGMA table_info(casos)").fetchall()
    elif tabla == "procedencia_campo":
        filas = conexion.execute("PRAGMA table_info(procedencia_campo)").fetchall()
    else:
        raise AssertionError(f"esta prueba no mira la tabla {tabla!r}")
    return {fila["name"] for fila in filas}


class PruebaConLaVersion14(PruebaConBaseTemporal):
    """Una base al día, que **ya incluye** la migración 14.

    ⚠️ **Aquí ya no se aplica a mano, y es lo que este mismo archivo predijo.** Su
    versión anterior decía: «`aplicar_esquema` llega a la 13, que es la última
    registrada **mientras otro pase no registre esta**». Ese otro pase la registró
    el 2026-09-03 —`datos/migraciones.py`, entrada `(14, ...)`—, así que
    `aplicar_esquema` la aplica sola. Aplicarla otra vez aquí reventaba con
    «duplicate column name: estado_marcado_por»: `ALTER TABLE ADD COLUMN` no es
    idempotente, y no tiene por qué serlo —`aplicar_esquema` ya lo garantiza
    comparando versiones—.
    """

    def setUp(self):
        super().setUp()
        self.miguel = alta_de_companero(self.conexion, "Miguel")
        self.sandy = alta_de_companero(self.conexion, "Sandy")

    def alta(self, numero="AAAA2609", fecha="2026-09-10", archivado=False):
        """Un documento pelado, escrito con SQL directo: aquí no se prueba el alta."""
        cursor = self.conexion.execute(
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en, archivado, "
            "fecha_archivado) VALUES (?, ?, ?, ?, ?)",
            (
                numero,
                fecha,
                "2026-09-01 10:00:00",
                1 if archivado else 0,
                "2026-09-01 10:00:00" if archivado else None,
            ),
        )
        return cursor.lastrowid

    def persona(self, caso_id, pasos=None, propuesto_por=None, nombre="Ana Pérez",
                mrn="055-1111-3853"):
        """Una persona del documento, con sus seis pasos como los devolvió la hoja.

        `pasos` es una lista de seis valores 1/0/None en el orden de `datos/pasos.py`.
        """
        pasos = pasos if pasos is not None else [None] * 6
        cursor = self.conexion.execute(INSERCION_DE_PERSONA, _valores(
            caso_id, nombre, mrn, propuesto_por, pasos))
        return cursor.lastrowid

    def leer(self, caso_id):
        return self.conexion.execute(
            "SELECT * FROM casos WHERE id = ?", (caso_id,)
        ).fetchone()


class PruebaDeLaConstanteDeInsercion(unittest.TestCase):
    """Que la constante escrita a mano no se separe de `datos/pasos.py`.

    `INSERCION_DE_PERSONA` lleva los seis pasos escritos letra por letra para que la
    auditoría de SQL pueda seguirla. El precio de escribirlos a mano es que pueden
    quedarse atrás; esto es lo que lo impide.
    """

    def test_las_seis_columnas_son_las_de_datos_pasos(self):
        for nombre in NOMBRES_DE_LOS_PASOS:
            self.assertIn(nombre, INSERCION_DE_PERSONA)
        self.assertEqual(6, len(NOMBRES_DE_LOS_PASOS))


class PruebaDeLaMigracion14(PruebaConBaseTemporal):
    """Criterio 3: aplica sobre la versión anterior sin perder filas."""

    def test_la_14_esta_registrada_y_en_su_sitio(self):
        """Lo que hace falsa la premisa del pase, medido y no contado.

        El pase pedía «la migración número 13». La 13 ya existía —`casos.duplicado_de`,
        de otro pase de esta sesión—, así que esta es la 14. Dos migraciones con el
        mismo número dejarían la segunda sin aplicarse nunca.

        ⚠️ Y desde el 2026-09-03 **está registrada** en `datos/migraciones.py`: hasta
        ese día esta prueba exigía que `VERSION_ACTUAL` fuera 13 y que la 14 fuera
        «la siguiente, todavía sin enganchar». Enganchada, `VERSION_ACTUAL` es 14, y
        eso es lo que hace que la migración llegue a la base del dueño en vez de
        quedarse escrita.

        ⚠️ **Y desde el 2026-09-04 ya no es la última**: la 15 abre `personas.mrn`
        a una letra final (`datos/migraciones_de_cedula.py`). Lo que esta prueba
        defiende no era «ser la última» —eso caduca con la siguiente migración—
        sino **estar registrada y llevar su propio número**. Eso es lo que se
        comprueba ahora, y no caduca.
        """
        self.assertEqual(14, VERSION_DE_LA_REVISION)
        self.assertLessEqual(VERSION_DE_LA_REVISION, VERSION_ACTUAL)
        from datos.migraciones import MIGRACIONES

        registradas = [version for version, _, _ in MIGRACIONES]
        self.assertIn(VERSION_DE_LA_REVISION, registradas)
        self.assertEqual(len(registradas), len(set(registradas)))

    def test_anade_las_siete_columnas(self):
        """Se construye una base parada en la 13 y se le aplica la 14 encima.

        No vale partir de `aplicar_esquema`: desde que la 14 está registrada, esa
        base ya las trae, y la prueba estaría comprobando que están las columnas que
        ella misma pidió que se pusieran. Lo que hay que medir es el paso.
        """
        conexion = _base_parada_en_la_13()
        try:
            for tabla, columna in COLUMNAS_NUEVAS:
                self.assertNotIn(columna, _columnas(conexion, tabla))
            migrar_a_version_14(conexion)
            for tabla, columna in COLUMNAS_NUEVAS:
                self.assertIn(columna, _columnas(conexion, tabla))
        finally:
            conexion.close()

    def test_no_pierde_ni_una_fila(self):
        """Las filas que ya estaban siguen ahí, con sus valores, y a NULL lo nuevo.

        Sobre una base parada en la 13, por lo mismo que la de arriba: con la 14 ya
        aplicada no habría paso que medir.
        """
        conexion = _base_parada_en_la_13()
        self.addCleanup(conexion.close)
        conexion.execute(
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en, "
            "estado_recomendacion) VALUES ('CASP2609', '2026-09-10', '2026-09-01', "
            "'incompleta')"
        )
        antes = conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]

        migrar_a_version_14(conexion)

        despues = conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]
        self.assertEqual(antes, despues)
        fila = conexion.execute("SELECT * FROM casos").fetchone()
        self.assertEqual("CASP2609", fila["numero_caso"])
        self.assertEqual("incompleta", fila["estado_recomendacion"])
        self.assertIsNone(fila["estado_marcado_por"])
        self.assertIsNone(fila["estado_del_companero"])

    def test_un_caso_ya_marcado_sin_firma_se_sigue_pudiendo_actualizar(self):
        """El `CHECK` no ata el estado con la firma, y esto lo comprueba.

        Un `CHECK` que los exigiera juntos convertiría las filas que ya tienen
        `estado_recomendacion` puesto y ninguna firma —las escribió `repositorio.py`
        antes de que estas columnas existieran— en filas que nadie podría volver a
        actualizar, desde otro módulo y sin avisar.
        """
        conexion = _base_parada_en_la_13()
        self.addCleanup(conexion.close)
        conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en, estado_recomendacion) "
            "VALUES ('CASP2609', '2026-09-01', 'incompleta')"
        )
        migrar_a_version_14(conexion)
        conexion.execute(
            "UPDATE casos SET fecha_viaje = '2026-09-20' WHERE numero_caso = 'CASP2609'"
        )
        self.assertEqual(
            "2026-09-20",
            conexion.execute("SELECT fecha_viaje FROM casos").fetchone()[0],
        )

    def test_quien_marco_no_puede_ir_sin_cuando(self):
        """El `CHECK` de la pareja vive en la tabla que deja la 14.

        Se aplica la 14 a mano sobre una base parada en la 13, y no se usa la base
        del `setUp`, porque lo que se comprueba es lo que ESA migracion deja puesto.
        """
        conexion = _base_parada_en_la_13()
        self.addCleanup(conexion.close)
        migrar_a_version_14(conexion)
        companero = alta_de_companero(conexion, "Miguel")
        conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-01')"
        )
        with self.assertRaises(Exception):
            conexion.execute(
                "UPDATE casos SET estado_marcado_por = ? WHERE numero_caso = 'CASP2609'",
                (companero,),
            )


class PruebaDeLosDosBotones(PruebaConLaVersion14):
    """Criterio 1: los dos botones escriben el estado con quién y cuándo."""

    def test_si_completa_deja_completa_con_quien_y_cuando(self):
        caso_id = self.alta()
        marcar_a_mano(self.conexion, caso_id, COMPLETA, self.miguel)

        fila = self.leer(caso_id)
        self.assertEqual(COMPLETA, fila["estado_recomendacion"])
        self.assertEqual(self.miguel, fila["estado_marcado_por"])
        self.assertIsNotNone(fila["estado_marcado_en"])
        self.assertIn("mano", fila["estado_marcado_origen"])

    def test_no_esta_completa_deja_no_completa(self):
        caso_id = self.alta()
        marcar_a_mano(self.conexion, caso_id, NO_COMPLETA, self.miguel)
        self.assertEqual(NO_COMPLETA, self.leer(caso_id)["estado_recomendacion"])

    def test_un_estado_que_no_es_de_los_dos_botones_no_se_escribe(self):
        caso_id = self.alta()
        with self.assertRaises(ErrorDeMarca):
            marcar_a_mano(self.conexion, caso_id, "resuelta", self.miguel)
        self.assertIsNone(self.leer(caso_id)["estado_recomendacion"])

    def test_no_se_firma_con_un_companero_que_no_existe(self):
        caso_id = self.alta()
        with self.assertRaises(ErrorDeMarca):
            marcar_a_mano(self.conexion, caso_id, COMPLETA, 9999)
        self.assertIsNone(self.leer(caso_id)["estado_recomendacion"])


class PruebaDelBloqueRojo(PruebaConLaVersion14):
    """Criterio 1, la mitad que importa: lo completo sale del bloque de riesgo."""

    def test_completa_es_lo_unico_que_resuelve(self):
        self.assertEqual((COMPLETA,), ESTADOS_QUE_RESUELVEN)

    def test_casos_en_riesgo_deja_fuera_lo_completo_y_deja_dentro_lo_demas(self):
        completo = self.alta("AAAA2609", "2026-09-10")
        incompleto = self.alta("BBBB2609", "2026-09-11")
        sin_revisar = self.alta("CCCC2609", "2026-09-12")
        marcar_a_mano(self.conexion, completo, COMPLETA, self.miguel)
        marcar_a_mano(self.conexion, incompleto, NO_COMPLETA, self.miguel)

        en_riesgo = {caso["id"] for caso in casos_en_riesgo(self.conexion, HOY)}
        self.assertNotIn(completo, en_riesgo)
        self.assertIn(incompleto, en_riesgo)
        self.assertIn(sin_revisar, en_riesgo)


class PruebaDeLaHojaQueVuelve(PruebaConLaVersion14):
    """Lo que el dueño decidió después: la carga del Excel marca, firmada por quien la llenó."""

    def _tres_documentos(self):
        """Uno con los seis en «Sí», uno con un «No», y uno a medias."""
        listo = self.alta("AAAA2609")
        self.persona(listo, [1, 1, 1, 1, 1, 1], propuesto_por=self.sandy)
        con_un_no = self.alta("BBBB2609")
        self.persona(con_un_no, [1, 1, 0, 1, 1, 1], propuesto_por=self.sandy)
        a_medias = self.alta("CCCC2609")
        self.persona(a_medias, [1, 1, None, 1, 1, 1], propuesto_por=self.sandy)
        return listo, con_un_no, a_medias

    def test_los_seis_en_si_quedan_completa_firmados_por_el_companero(self):
        listo, _, _ = self._tres_documentos()
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)

        fila = self.leer(listo)
        self.assertEqual(COMPLETA, fila["estado_recomendacion"])
        self.assertEqual(self.sandy, fila["estado_marcado_por"])
        self.assertEqual(RUTA_DE_LA_HOJA, fila["estado_marcado_origen"])
        self.assertEqual(COMPLETA, fila["estado_del_companero"])
        self.assertEqual(self.sandy, fila["estado_del_companero_por"])

    def test_un_solo_no_deja_el_documento_no_completo(self):
        _, con_un_no, _ = self._tres_documentos()
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)
        self.assertEqual(NO_COMPLETA, self.leer(con_un_no)["estado_recomendacion"])

    def test_lo_devuelto_a_medias_no_se_marca(self):
        """Sin contestar no es «No». Se queda sin estado, que ya cuenta como pendiente."""
        _, _, a_medias = self._tres_documentos()
        resumen = aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)

        self.assertIsNone(self.leer(a_medias)["estado_recomendacion"])
        self.assertEqual(1, len(resumen.sin_respuesta))

    def test_una_familia_con_una_persona_a_medias_no_se_da_por_completa(self):
        """Todas las personas del documento, no la primera que se mire."""
        caso_id = self.alta("DDDD2609")
        self.persona(caso_id, [1, 1, 1, 1, 1, 1], propuesto_por=self.sandy, mrn=None)
        self.persona(caso_id, [1, 1, 1, 1, 1, None], propuesto_por=self.sandy,
                     nombre="Luis Pérez", mrn="055-1111-3854")
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)
        self.assertIsNone(self.leer(caso_id)["estado_recomendacion"])

    def test_el_resumen_cuenta_los_cinco_montones(self):
        self._tres_documentos()
        sin_devolver = self.alta("EEEE2609")
        self.persona(sin_devolver)

        resumen = aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)

        self.assertEqual(1, len(resumen.completas))
        self.assertEqual(1, len(resumen.no_completas))
        self.assertEqual(1, len(resumen.sin_respuesta))
        self.assertEqual(1, resumen.sin_devolver)
        self.assertEqual(0, resumen.sin_casar)
        self.assertEqual(2, resumen.marcados)

    def test_la_segunda_carga_no_vuelve_a_marcar_lo_de_la_primera(self):
        """El fallo que `propuesto_desde` cierra, escrito antes que el arreglo.

        Sin filtrar por cuándo llegó la propuesta, cargar la segunda hoja volvía a
        marcar los documentos de la primera: les escribía la ruta del archivo
        equivocada en `estado_marcado_origen` y pisaba lo que Miguel hubiera
        corregido a mano en ellos.
        """
        de_la_primera = self.alta("AAAA2609")
        self.persona(de_la_primera, [1, 1, 1, 1, 1, 1], propuesto_por=self.sandy)
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)
        marcar_a_mano(self.conexion, de_la_primera, NO_COMPLETA, self.miguel)

        # La segunda hoja, de otro compañero y con propuestas más nuevas.
        de_la_segunda = self.alta("BBBB2609")
        self.persona(de_la_segunda, [1, 1, 1, 1, 1, 1], propuesto_por=self.sandy)
        self.conexion.execute(
            "UPDATE personas SET propuesto_en = '2026-09-06 09:00:00' WHERE caso_id = ?",
            (de_la_segunda,),
        )
        otra_ruta = OTRA_HOJA
        resumen = aplicar_las_marcas_de_la_hoja(
            self.conexion, otra_ruta, propuesto_desde="2026-09-06 00:00:00"
        )

        self.assertEqual(1, resumen.marcados)
        primero = self.leer(de_la_primera)
        # Lo de Miguel sigue en pie y la segunda hoja no le escribió su ruta encima.
        self.assertEqual(NO_COMPLETA, primero["estado_recomendacion"])
        self.assertEqual(self.miguel, primero["estado_marcado_por"])
        self.assertEqual(ORIGEN_A_MANO, primero["estado_marcado_origen"])
        # Y lo que Sandy había dicho en la primera hoja tampoco se perdió.
        self.assertEqual(COMPLETA, primero["estado_del_companero"])
        self.assertEqual(otra_ruta, self.leer(de_la_segunda)["estado_marcado_origen"])

    def test_lo_que_miguel_marca_despues_manda_y_no_borra_lo_del_companero(self):
        listo, _, _ = self._tres_documentos()
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)

        marcar_a_mano(self.conexion, listo, NO_COMPLETA, self.miguel)

        fila = self.leer(listo)
        self.assertEqual(NO_COMPLETA, fila["estado_recomendacion"])
        self.assertEqual(self.miguel, fila["estado_marcado_por"])
        # Lo que dijo Sandy sigue entero: es lo que deja ver que discreparon.
        self.assertEqual(COMPLETA, fila["estado_del_companero"])
        self.assertEqual(self.sandy, fila["estado_del_companero_por"])


class PruebaDeLaListaYSusFiltros(PruebaConLaVersion14):
    """Criterio 2 sin ventana: las tarjetas, sus filtros y su búsqueda."""

    def setUp(self):
        super().setUp()
        self.completo = self.alta("AAAA2609", "2026-09-10")
        marcar_a_mano(self.conexion, self.completo, COMPLETA, self.miguel)
        self.incompleto = self.alta("BBBB2609", "2026-09-11")
        marcar_a_mano(self.conexion, self.incompleto, NO_COMPLETA, self.miguel)
        self.nuevo = self.alta("CCCC2609", "2026-09-12")
        self.persona(self.nuevo, nombre="Ana Pérez", mrn="055-1111-3853")
        self.vencido = self.alta("DDDD2609", "2026-09-01")
        self.dice_lista = self.alta("EEEE2609", "2026-09-13")
        self.persona(self.dice_lista, [1, 1, 1, 1, 1, 1], propuesto_por=self.sandy,
                     nombre="Luis Gómez", mrn="055-1111-3855")

    def documentos(self):
        return documentos_para_revisar(self.conexion, HOY)

    def test_una_fila_por_documento_con_lo_de_su_tarjeta(self):
        documentos = self.documentos()
        self.assertEqual(5, len(documentos))
        nuevo = next(d for d in documentos if d["id"] == self.nuevo)
        self.assertEqual(SIN_REVISAR, nuevo["estado_de_la_tarjeta"])
        self.assertEqual(1, nuevo["personas"])
        self.assertTrue(nuevo["sin_asignar"])

    def test_las_cuentas_de_las_pastillas(self):
        cuentas = cuentas_de_los_filtros(self.documentos())
        self.assertEqual(5, cuentas["todo"])
        self.assertEqual(1, cuentas["completas"])
        self.assertEqual(1, cuentas["incompletas"])
        self.assertEqual(3, cuentas["sin_revisar"])
        self.assertEqual(5, cuentas["sin_asignar"])
        self.assertEqual(1, cuentas["el_companero_dice_lista"])
        self.assertEqual(1, cuentas["fecha_pasada"])

    def test_el_tablero_de_completados_es_el_filtro_completas(self):
        completados = filtrar(self.documentos(), "completas")
        self.assertEqual([self.completo], [d["id"] for d in completados])

    def test_lo_marcado_completa_sale_del_tablero_de_trabajo(self):
        """«Y pasa al tablero de completados»: deja de ser trabajo pendiente."""
        trabajo = [
            d for d in self.documentos() if d["estado_de_la_tarjeta"] != COMPLETA
        ]
        self.assertNotIn(self.completo, [d["id"] for d in trabajo])
        self.assertEqual(4, len(trabajo))

    def test_la_fecha_pasada_se_marca_sola(self):
        vencido = next(d for d in self.documentos() if d["id"] == self.vencido)
        self.assertTrue(vencido["fecha_pasada"])

    def test_el_companero_dice_lista_sale_de_la_lista_al_confirmarla(self):
        marcar_a_mano(self.conexion, self.dice_lista, COMPLETA, self.miguel)
        cuentas = cuentas_de_los_filtros(self.documentos())
        self.assertEqual(0, cuentas["el_companero_dice_lista"])

    def test_se_busca_por_nombre_por_mrn_y_por_caso(self):
        documentos = self.documentos()
        self.assertEqual([self.nuevo], [d["id"] for d in filtrar(documentos, "todo", "Ana")])
        self.assertEqual(
            [self.nuevo], [d["id"] for d in filtrar(documentos, "todo", "055-1111-3853")]
        )
        self.assertEqual(
            [self.completo], [d["id"] for d in filtrar(documentos, "todo", "aaaa2609")]
        )

    def test_la_busqueda_se_cruza_con_el_filtro(self):
        self.assertEqual([], filtrar(self.documentos(), "completas", "Ana"))

    def test_lo_archivado_sigue_saliendo_con_su_marca(self):
        archivar_caso(self.conexion, self.completo)
        archivado = next(d for d in self.documentos() if d["id"] == self.completo)
        self.assertEqual(1, archivado["archivado"])


class PruebaDeVolumen(PruebaConLaVersion14):
    """«Imagina que tenga 3000 formularios», dicho por el dueño el 2026-09-03."""

    DOCUMENTOS = 3000
    # El techo no es un número redondo elegido a ojo: es «que se pueda mirar sin
    # esperar». Cinco segundos para leer la lista entera y otros cinco para
    # aplicar la hoja son holgados frente a lo medido, y aprietan lo bastante como
    # para que una consulta por tarjeta —que sería el error natural— no pase.
    TECHO_EN_SEGUNDOS = 5.0

    def setUp(self):
        super().setUp()
        casos = [
            ("AAAA2609", "2026-09-10", "2026-09-01 10:00:00")
            for _ in range(self.DOCUMENTOS)
        ]
        self.conexion.execute("BEGIN")
        self.conexion.executemany(
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en) VALUES (?, ?, ?)",
            casos,
        )
        # Dos de cada tres vuelven listos y uno con un «No», para que los dos montones
        # que se escriben tengan volumen de verdad.
        self.conexion.executemany(
            INSERCION_DE_PERSONA,
            [
                _valores(
                    caso_id, f"Persona {caso_id}", None, self.sandy,
                    [1, 1, 1, 1, 1, 1] if caso_id % 3 else [1, 1, 0, 1, 1, 1],
                )
                for caso_id in range(1, self.DOCUMENTOS + 1)
            ],
        )
        self.conexion.execute("COMMIT")

    def test_aplicar_la_hoja_de_3000_documentos_en_un_tiempo_que_se_puede_mirar(self):
        arranque = time.perf_counter()
        resumen = aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)
        tardanza = time.perf_counter() - arranque

        self.assertEqual(self.DOCUMENTOS, resumen.marcados)
        self.assertEqual(1000, len(resumen.no_completas))
        self.assertLess(tardanza, self.TECHO_EN_SEGUNDOS, f"tardó {tardanza:.2f} s")

    def test_leer_las_3000_tarjetas_en_un_tiempo_que_se_puede_mirar(self):
        aplicar_las_marcas_de_la_hoja(self.conexion, RUTA_DE_LA_HOJA)

        arranque = time.perf_counter()
        documentos = documentos_para_revisar(self.conexion, HOY)
        cuentas = cuentas_de_los_filtros(documentos)
        tardanza = time.perf_counter() - arranque

        self.assertEqual(self.DOCUMENTOS, len(documentos))
        self.assertEqual(2000, cuentas["completas"])
        self.assertLess(tardanza, self.TECHO_EN_SEGUNDOS, f"tardó {tardanza:.2f} s")


if __name__ == "__main__":
    unittest.main()
