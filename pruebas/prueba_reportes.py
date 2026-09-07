"""El reporte: las dos partes, las tres metricas, y los dos archivos.

Las pruebas salen del criterio de aceptacion de la FASE 8, no de leer el codigo.
Los tres criterios que gobiernan este archivo, literales:

  3. «El total del reporte del periodo es el mismo numero antes y despues de
     archivar un caso de ese periodo. Los archivados siguen contando.»
  4. «El total que muestra el reporte coincide con el que devuelve la consulta SQL
     equivalente hecha a mano: el mismo numero por dos caminos. Si no coinciden,
     la fase no pasa.»
  6. «El archivo abre en Excel sin mensaje de archivo dañado, y releido con
     `openpyxl` el total que muestra coincide con el del criterio 4.»

El criterio 4 es el que decide la forma de estas pruebas: el numero del reporte se
compara contra un `SELECT COUNT(*)` escrito **aparte**, en la prueba, no contra la
misma consulta que produce el reporte. Comparar una consulta consigo misma no
prueba nada.
"""

import ast
import shutil
import tempfile
import unittest
from pathlib import Path

from openpyxl import load_workbook
from pypdf import PdfReader

from datos.archivo import NO_PUDO_VIAJAR, SI_VIAJO, archivar_caso, registrar_resultado_del_viaje
from datos.estados import ESTADOS_QUE_RESUELVEN
from datos.procedencia import guardar_procedencia_de_campo, marcar_campo_verificado
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal
from reportes import metricas
from reportes.avisos import SIN_NUMERO_DE_CASO
from reportes.consultas import casos_con_su_verificacion
from reportes.documento import NO_SE_PUEDE_SABER, construir_documento
from reportes.generacion import generar_reporte
from reportes.periodo import ErrorDePeriodo, periodo, periodo_del_mes

GENERADO_EN = "2026-10-01 09:00:00"
SEPTIEMBRE = periodo_del_mes(2026, 9)

# ⚠️ Las secciones se buscan **por su titulo** y no por su posicion, y no es por
# gusto: el informe abrio por las tres tablas del proyecto viejo el 2026-09-03 y
# todos los indices se corrieron de golpe. Una prueba anclada a `secciones[0]` no
# midio nada nuevo ese dia: fallo por haberse movido la lista, que no es lo que
# estaba comprobando.
PARTE_DE_QUIEN_VIAJO = "Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación"
PARTE_DE_QUIEN_NO_VIAJO = "Parte 2 — Personas que NO pudieron viajar"
PARTE_DE_LAS_METRICAS = "Métricas del trabajo del equipo"


def seccion_de(documento, titulo):
    """La seccion de ese titulo. Levanta si no esta, que es lo que hay que saber."""
    for seccion in documento.secciones:
        if seccion.titulo == titulo:
            return seccion
    raise AssertionError(
        f"El reporte no trae ninguna sección titulada «{titulo}». Las que trae son "
        f"{[seccion.titulo for seccion in documento.secciones]}."
    )


class BaseConCasosDeSeptiembre(PruebaConBaseTemporal):
    """Tres casos: dos viajan en septiembre y uno en octubre."""

    def setUp(self):
        super().setUp()
        self.carpeta_de_reportes = Path(tempfile.mkdtemp(prefix="fichas_reportes_"))
        self.companero = self.conexion.execute(
            "INSERT INTO companeros (nombre, activo, creado_en) VALUES (?, 1, ?)",
            ("Miguel", "2026-09-01 08:00:00"),
        ).lastrowid

        self.de_septiembre = self._caso("CASP2609", "2026-09-08", "incompleta")
        self.tambien_de_septiembre = self._caso("CBSP2609", "2026-09-30", None)
        self.de_octubre = self._caso("CCSP2610", "2026-10-04", None)

    def tearDown(self):
        shutil.rmtree(self.carpeta_de_reportes, ignore_errors=True)
        super().tearDown()

    def _caso(self, numero_caso, fecha_viaje, estado):
        """Un caso con dos personas y la procedencia de un campo de cada una."""
        caso = alta_de_caso(
            self.conexion, numero_caso, unidad_numero="123456", fecha_viaje=fecha_viaje,
            unidad_nombre="Paramaribo Branch", estado_recomendacion=estado,
        ).id
        self.personas_de = getattr(self, "personas_de", {})
        self.personas_de[numero_caso] = []
        cuantos_casos_ya_hay = len(self.personas_de)
        for fila in (1, 2):
            persona = alta_de_persona(
                self.conexion, caso, mrn=f"055-1111-38{cuantos_casos_ya_hay}{fila}",
                nombre=f"Persona {fila} de {numero_caso}", fila_formulario=fila,
            )
            guardar_procedencia_de_campo(
                self.conexion, "personas", persona, "nombre", "ocr", 0.9
            )
            self.personas_de[numero_caso].append(persona)
        return caso

    def _documento(self, el_periodo=SEPTIEMBRE):
        return construir_documento(self.conexion, el_periodo, GENERADO_EN)

    def _personas_del_periodo_por_sql(self, el_periodo):
        """El mismo total por el otro camino: SQL escrito a mano en la prueba."""
        return self.conexion.execute(
            "SELECT COUNT(*) FROM personas p JOIN casos c ON c.id = p.caso_id "
            "WHERE c.fecha_viaje >= ? AND c.fecha_viaje <= ?",
            (el_periodo.desde, el_periodo.hasta),
        ).fetchone()[0]


class PruebaDeLosTotalesDelReporte(BaseConCasosDeSeptiembre):
    """Criterios 3 y 4: el total no cambia al archivar, y cuadra por dos caminos."""

    def test_el_total_coincide_con_el_sql_hecho_a_mano(self):
        """Criterio 4: el mismo numero por dos caminos."""
        documento = self._documento()
        del_reporte = sum(
            len(seccion_de(documento, parte).filas)
            for parte in (PARTE_DE_QUIEN_VIAJO, PARTE_DE_QUIEN_NO_VIAJO)
        )
        self.assertEqual(del_reporte, self._personas_del_periodo_por_sql(SEPTIEMBRE))
        self.assertEqual(del_reporte, 4)

    def test_el_total_no_cambia_al_archivar_un_caso_del_periodo(self):
        """Criterio 3: los archivados siguen contando."""
        antes = self._personas_del_periodo_por_sql(SEPTIEMBRE)
        filas_antes = len(seccion_de(self._documento(), PARTE_DE_QUIEN_VIAJO).filas)

        archivar_caso(self.conexion, self.de_septiembre)

        self.assertEqual(self._personas_del_periodo_por_sql(SEPTIEMBRE), antes)
        self.assertEqual(
            len(seccion_de(self._documento(), PARTE_DE_QUIEN_VIAJO).filas), filas_antes
        )

    def test_el_caso_de_octubre_no_entra_en_el_reporte_de_septiembre(self):
        """Si entrara, el periodo no estaria filtrando nada."""
        filas = seccion_de(self._documento(), PARTE_DE_QUIEN_VIAJO).filas
        self.assertNotIn("CCSP2610", [fila[0] for fila in filas])

    def test_los_dos_bordes_del_periodo_entran(self):
        """El dia 1 y el dia 30 son del mes. Es el fallo clasico de un `<`."""
        del_ultimo_dia = [
            fila
            for fila in seccion_de(self._documento(), PARTE_DE_QUIEN_VIAJO).filas
            if fila[0] == "CBSP2609"
        ]
        self.assertEqual(len(del_ultimo_dia), 2)
        self.assertEqual(
            len(seccion_de(self._documento(periodo("2026-09-30", "2026-09-30")),
                           PARTE_DE_QUIEN_VIAJO).filas), 2
        )

    def test_un_periodo_del_reves_se_rechaza_con_su_motivo(self):
        """«Del 30 al 1» daria todo a cero, que se lee igual que un mes sin trabajo."""
        with self.assertRaises(ErrorDePeriodo):
            periodo("2026-09-30", "2026-09-01")


class PruebaDeLasDosPartesDelReporte(BaseConCasosDeSeptiembre):
    """Quien viajo y quien no, con el motivo y el numero de caso."""

    def test_quien_no_pudo_viajar_sale_con_su_motivo_y_su_caso(self):
        """Es la parte 2 entera: caso, persona y por que no viajo."""
        persona = self.personas_de["CASP2609"][1]
        registrar_resultado_del_viaje(
            self.conexion, persona, NO_PUDO_VIAJAR, "la recomendación no llevaba firma"
        )
        seccion = seccion_de(self._documento(), PARTE_DE_QUIEN_NO_VIAJO)
        self.assertEqual(len(seccion.filas), 1)
        fila = seccion.filas[0]
        self.assertEqual(fila[0], "CASP2609")
        self.assertIn("no llevaba firma", fila[5])

    def test_quien_no_pudo_viajar_sale_de_la_parte_1(self):
        """Nadie puede estar en las dos listas: el total dejaria de cuadrar."""
        persona = self.personas_de["CASP2609"][1]
        registrar_resultado_del_viaje(
            self.conexion, persona, NO_PUDO_VIAJAR, "sin firma"
        )
        documento = self._documento()
        self.assertEqual(len(seccion_de(documento, PARTE_DE_QUIEN_VIAJO).filas), 3)
        self.assertEqual(len(seccion_de(documento, PARTE_DE_QUIEN_NO_VIAJO).filas), 1)
        self.assertEqual(self._personas_del_periodo_por_sql(SEPTIEMBRE), 4)

    def test_la_recomendacion_completa_dice_que_no_se_puede_saber(self):
        """Mientras nadie diga que valor significa «resuelta», no se rellena.

        Si algun dia `ESTADOS_QUE_RESUELVEN` deja de estar vacia, esta prueba
        cambia de rama sola y sigue midiendo lo que toca.
        """
        columna = seccion_de(self._documento(), PARTE_DE_QUIEN_VIAJO).filas[0][7]
        if ESTADOS_QUE_RESUELVEN:
            self.assertIn(columna, ("sí", "no", "no consta"))
        else:
            self.assertEqual(columna, NO_SE_PUEDE_SABER)

    def test_el_reporte_avisa_de_por_que_no_se_puede_saber(self):
        """El pase lo pide con esas palabras: decirlo en vez de rellenarlo."""
        if ESTADOS_QUE_RESUELVEN:
            self.skipTest("ya hay un valor que significa «resuelta»")
        avisos = " ".join(self._documento().avisos)
        self.assertIn("no se puede saber", avisos)
        self.assertIn("NINGUNO significa", avisos)

    def test_quien_no_viajo_sin_fecha_de_viaje_no_se_pierde(self):
        """Sin fecha no cabe en ningun periodo, y aun asi tiene que verse."""
        caso = alta_de_caso(self.conexion, "CDSP2609").id
        persona = alta_de_persona(self.conexion, caso, nombre="Sin fecha", fila_formulario=1)
        registrar_resultado_del_viaje(
            self.conexion, persona, NO_PUDO_VIAJAR, "se le perdió el pasaporte"
        )
        avisos = " ".join(self._documento().avisos)
        self.assertIn("CDSP2609", avisos)

    def test_quien_no_viajo_en_un_caso_sin_numero_tampoco_se_pierde(self):
        """Un caso SIN numero entra en ese aviso y lo escribe sin romper el reporte.

        Desde la version 7 del esquema `casos.numero_caso` puede ser NULL, y desde
        la identidad por documento esos casos entran de verdad en las listas. El
        aviso ordenaba los numeros en un conjunto donde ahora cae `None`, y en
        Python ordenar `None` junto a texto levanta `TypeError`: el reporte entero
        se caia por un caso al que no se le pudo leer el numero, que es justo el
        que hay que ver.

        Se comprueba tambien que NO dice «None»: la palabra que va ahi es la que
        `interfaz/etiquetas.py` tiene escrita para este hueco.
        """
        caso = alta_de_caso(self.conexion, None).id
        persona = alta_de_persona(
            self.conexion, caso, nombre="Sin número", fila_formulario=1
        )
        registrar_resultado_del_viaje(
            self.conexion, persona, NO_PUDO_VIAJAR, "no se pudo leer el número"
        )
        avisos = " ".join(self._documento().avisos)
        self.assertIn(SIN_NUMERO_DE_CASO, avisos)
        self.assertNotIn("None", avisos)


class PruebaDeLasTresMetricas(BaseConCasosDeSeptiembre):
    """Las tres salen con su numero, y los denominadores cuadran."""

    def _verificar_entero(self, numero_caso, caso_id):
        """Deja el caso con todos sus campos verificados, como hace el boton."""
        for persona in self.personas_de[numero_caso]:
            marcar_campo_verificado(
                self.conexion, "personas", persona, "nombre", self.companero
            )

    def test_los_cinco_cubos_de_la_deteccion_suman_el_universo(self):
        """Si no suman, algo se conto dos veces o se perdio por el camino."""
        deteccion = metricas.deteccion_antes_del_viaje(
            casos_con_su_verificacion(self.conexion), SEPTIEMBRE
        )
        suma = (
            deteccion.detectados_a_tiempo
            + deteccion.detectados_despues_del_viaje
            + deteccion.con_problema_sin_fecha_de_deteccion
            + deteccion.sin_estado_registrado
            + deteccion.con_recomendacion_resuelta
        )
        self.assertEqual(suma, deteccion.casos_del_periodo)
        self.assertEqual(deteccion.casos_del_periodo, 2)

    def test_un_caso_sin_estado_no_cuenta_como_problema(self):
        """Nadie ha dicho nada NO es lo mismo que hay un problema."""
        deteccion = metricas.deteccion_antes_del_viaje(
            casos_con_su_verificacion(self.conexion), SEPTIEMBRE
        )
        self.assertEqual(deteccion.sin_estado_registrado, 1)
        self.assertEqual(deteccion.con_problema_registrado, 1)

    def test_un_caso_sin_verificar_no_cuenta_como_verificado(self):
        """Cero campos verificados no es «todo verificado»."""
        verificados = metricas.casos_verificados_en_el_periodo(
            casos_con_su_verificacion(self.conexion), SEPTIEMBRE
        )
        self.assertEqual(verificados, [])

    def test_un_caso_verificado_cuenta_en_su_periodo(self):
        """La metrica 1 sube cuando alguien termina de verificar un caso."""
        self._verificar_entero("CASP2609", self.de_septiembre)
        hoy = periodo_del_mes(2026, 9)
        casos = casos_con_su_verificacion(self.conexion)
        el_caso = next(c for c in casos if c["numero_caso"] == "CASP2609")
        del_dia = periodo(el_caso["verificado_en"][:10], el_caso["verificado_en"][:10])
        self.assertEqual(len(metricas.casos_verificados_en_el_periodo(casos, del_dia)), 1)
        self.assertEqual(metricas.demora_de_importar_a_verificar(casos, del_dia).casos_medidos, 1)
        self.assertIsNotNone(hoy)

    def test_la_demora_no_se_puede_calcular_sin_casos_verificados(self):
        """Se dice que no se puede, no se inventa un cero."""
        demora = metricas.demora_de_importar_a_verificar(
            casos_con_su_verificacion(self.conexion), SEPTIEMBRE
        )
        self.assertEqual(demora.casos_medidos, 0)
        self.assertIsNone(demora.promedio_en_horas)

    def test_una_demora_negativa_se_descarta_y_se_cuenta(self):
        """Un reloj movido no baja el promedio de todos los demas en silencio."""
        caso = {
            "campos": 1, "campos_verificados": 1,
            "creado_en": "2026-09-10 12:00:00", "verificado_en": "2026-09-09 12:00:00",
            "fecha_viaje": "2026-09-20", "estado_recomendacion": None,
        }
        demora = metricas.demora_de_importar_a_verificar([caso], SEPTIEMBRE)
        self.assertEqual(demora.casos_medidos, 0)
        self.assertEqual(demora.descartados_por_fechas_incoherentes, 1)

    def test_las_tres_metricas_salen_con_su_numero(self):
        """Criterio de cierre: las tres, cada una con su número o su razón."""
        filas = seccion_de(self._documento(), PARTE_DE_LAS_METRICAS).filas
        self.assertEqual(len(filas), 3)
        for nombre, numero, como_se_cuenta in filas:
            self.assertTrue(nombre and como_se_cuenta)
            self.assertIsNotNone(numero)


class LaMarcaDeTiempoSeComprueba(BaseConCasosDeSeptiembre):
    """Dado un `generado_en` que no es texto, cuando se pide el informe, entonces
    falla en español y ANTES de escribir ningún archivo.

    ⚠️ **El defecto, medido por QA el 2026-09-03:** pasar un `datetime` —que es lo
    natural de escribir— no fallaba en `generar_reporte`. Bajaba entero hasta el
    escritor de PDF y reventaba allí con un `TypeError` en inglés, con el rastro de
    un módulo que no tiene nada que ver con el error. Y para entonces el `.xlsx` ya
    podía estar escrito, así que quedaba media pareja de archivos en la carpeta.

    El `datetime` **no se convierte por dentro** a propósito: sería adivinar el
    formato, y esa cadena se escribe tal cual en la portada y en el pie de todas las
    páginas.
    """

    def _generar(self, generado_en):
        return generar_reporte(
            self.conexion, SEPTIEMBRE, generado_en,
            carpeta_de_datos=self.carpeta_de_reportes, avisar=None,
        )

    def test_un_datetime_no_revienta_dentro_del_escritor_de_pdf(self):
        from datetime import datetime

        from datos.validacion import ErrorDeValidacion

        with self.assertRaises(ErrorDeValidacion):
            self._generar(datetime(2026, 10, 1, 9, 0, 0))

    def test_el_mensaje_esta_en_espanol_y_dice_que_se_esperaba(self):
        from datos.validacion import ErrorDeValidacion

        with self.assertRaises(ErrorDeValidacion) as fallo:
            self._generar(20261001)
        dicho = str(fallo.exception)
        self.assertIn("generado_en", dicho)
        self.assertIn("texto", dicho)
        self.assertIn("2026-09-03 14:05:00", dicho)

    def test_una_cadena_vacia_tampoco_pasa(self):
        """Un informe con el pie en blanco no dice cuándo se generó."""
        from datos.validacion import ErrorDeValidacion

        for valor in ("", "   ", None):
            with self.subTest(valor=valor), self.assertRaises(ErrorDeValidacion):
                self._generar(valor)

    def test_no_queda_ningun_archivo_a_medias(self):
        """Falla antes de contar nada, así que la carpeta se queda como estaba."""
        from datos.validacion import ErrorDeValidacion

        antes = sorted(p.name for p in self.carpeta_de_reportes.glob("*"))
        with self.assertRaises(ErrorDeValidacion):
            self._generar(None)
        self.assertEqual(
            antes, sorted(p.name for p in self.carpeta_de_reportes.glob("*"))
        )

    def test_una_marca_de_tiempo_normal_sigue_pasando(self):
        """La otra mitad: la validación no puede cerrarle la puerta al camino bueno."""
        resultado = self._generar(GENERADO_EN)
        self.assertTrue(resultado.excel.escrito)
        self.assertTrue(resultado.pdf.escrito)


class PruebaDeLosArchivosDelReporte(BaseConCasosDeSeptiembre):
    """Criterio 6: los dos archivos se escriben y se vuelven a leer."""

    def _generar(self):
        return generar_reporte(
            self.conexion, SEPTIEMBRE, GENERADO_EN,
            carpeta_de_datos=self.carpeta_de_reportes, avisar=None,
        )

    def test_los_dos_archivos_quedan_escritos(self):
        resultado = self._generar()
        self.assertTrue(resultado.excel.escrito)
        self.assertTrue(resultado.pdf.escrito)
        self.assertTrue(resultado.excel.ruta.exists())
        self.assertTrue(resultado.pdf.ruta.exists())

    def _hoja_de(self, libro, titulo):
        """La pestana de esa seccion. Excel recorta el nombre a 31 caracteres.

        Se busca por prefijo y no por posicion, por lo mismo que `seccion_de`: el
        dia que el reporte gane una seccion, una prueba anclada a la pestana numero
        cero deja de medir lo que decia medir.
        """
        for nombre in libro.sheetnames:
            if titulo.startswith(nombre.strip()):
                return libro[nombre]
        raise AssertionError(
            f"El libro no trae ninguna pestaña de la sección «{titulo}». Las que "
            f"trae son {libro.sheetnames}."
        )

    def test_el_xlsx_se_relee_y_el_total_coincide(self):
        """Criterio 6: releido con openpyxl, el total es el del criterio 4."""
        resultado = self._generar()
        libro = load_workbook(resultado.excel.ruta)
        self.assertEqual(len(libro.sheetnames), len(self._documento().secciones))
        hoja = self._hoja_de(libro, PARTE_DE_QUIEN_VIAJO)
        numeros_de_caso = [
            celda.value
            for (celda,) in hoja.iter_rows(min_col=1, max_col=1)
            if celda.value in ("CASP2609", "CBSP2609")
        ]
        self.assertEqual(len(numeros_de_caso), self._personas_del_periodo_por_sql(SEPTIEMBRE))

    def test_el_mrn_conserva_los_ceros_de_delante(self):
        """Sin formato de texto, Excel se come el 0 y el MRN deja de identificar."""
        resultado = self._generar()
        hoja = self._hoja_de(load_workbook(resultado.excel.ruta), PARTE_DE_QUIEN_VIAJO)
        celdas = [
            celda
            for fila in hoja.iter_rows()
            for celda in fila
            if isinstance(celda.value, str) and celda.value.startswith("055-")
        ]
        self.assertTrue(celdas)
        for celda in celdas:
            self.assertEqual(celda.number_format, "@")

    def test_el_pdf_se_abre_y_trae_lo_que_tiene_que_traer(self):
        """Se relee con `pypdf`, que ya es dependencia del proyecto."""
        persona = self.personas_de["CASP2609"][1]
        registrar_resultado_del_viaje(
            self.conexion, persona, NO_PUDO_VIAJAR, "sin la firma del obispo"
        )
        resultado = self._generar()
        lector = PdfReader(str(resultado.pdf.ruta))
        self.assertGreaterEqual(len(lector.pages), 1)
        texto = "\n".join(pagina.extract_text() for pagina in lector.pages)
        self.assertIn("CASP2609", texto)
        self.assertIn("firma del obispo", texto)

    def test_el_pdf_conserva_las_tildes_y_las_enes(self):
        """WinAnsi cubre el alfabeto latino: un nombre con eñe no se altera."""
        caso = alta_de_caso(self.conexion, "CESP2609", fecha_viaje="2026-09-15").id
        alta_de_persona(self.conexion, caso, nombre="José Peña Muñoz", fila_formulario=1)
        resultado = self._generar()
        texto = "\n".join(p.extract_text() for p in PdfReader(str(resultado.pdf.ruta)).pages)
        self.assertIn("Peña", texto)

    def test_volver_a_generar_el_mismo_periodo_pisa_el_archivo(self):
        """Dos reportes del mismo mes con numeros distintos son dos verdades."""
        primero = self._generar()
        registrar_resultado_del_viaje(
            self.conexion, self.personas_de["CASP2609"][0], SI_VIAJO
        )
        segundo = self._generar()
        self.assertEqual(primero.excel.ruta, segundo.excel.ruta)
        self.assertEqual(len(list(segundo.excel.ruta.parent.glob("*.xlsx"))), 1)

    def test_no_queda_ningun_archivo_parcial(self):
        """Lo que esta a medias se llama `.parcial`, y al terminar no queda ninguno."""
        resultado = self._generar()
        self.assertEqual(list(resultado.excel.ruta.parent.glob("*.parcial")), [])


class PruebaDeQueElPdfNoTraeDependencias(unittest.TestCase):
    """El PDF se escribe a mano: `requirements.txt` no cambia por esta fase."""

    # Lo unico que los dos modulos del PDF pueden importar: la biblioteca estandar
    # de Python y el propio proyecto. Cualquier otro nombre aqui es una dependencia
    # nueva en `requirements.txt` y unos megas mas en el ejecutable.
    PERMITIDOS = {"collections", "reportes"}

    def _modulos_importados(self, ruta):
        """Los modulos que importa un archivo, leidos de su arbol de sintaxis."""
        arbol = ast.parse(Path(ruta).read_text(encoding="utf-8"))
        modulos = set()
        for nodo in ast.walk(arbol):
            if isinstance(nodo, ast.Import):
                modulos.update(alias.name.split(".")[0] for alias in nodo.names)
            elif isinstance(nodo, ast.ImportFrom) and nodo.module:
                modulos.add(nodo.module.split(".")[0])
        return modulos

    def test_los_modulos_del_pdf_no_importan_nada_de_fuera(self):
        """Leido del arbol de sintaxis, no de la memoria de quien lo escribio.

        Si algun dia alguien mete `reportlab` aqui «solo para una tabla», esta
        prueba lo dice antes de que el ejecutable engorde otros 20 MB.
        """
        for ruta in ("reportes/pdf.py", "reportes/formato_pdf.py"):
            with self.subTest(ruta=ruta):
                self.assertLessEqual(self._modulos_importados(ruta), self.PERMITIDOS)


if __name__ == "__main__":
    unittest.main()
