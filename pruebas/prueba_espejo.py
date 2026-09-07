"""Que el espejo de Excel dice lo mismo que la base, y lo dice de forma usable.

Las pruebas se derivan del criterio de aceptacion de la FASE 4 (`PENDIENTES.md`),
no del codigo que las cumple. Las cuatro que de verdad pueden fallar en produccion:

  - el MRN conserva sus ceros de delante;
  - la fecha de viaje se puede ordenar por mes de verdad;
  - el archivo bloqueado avisa y NO pierde el dato;
  - un proceso muerto a mitad no deja un `.xlsx` roto.

Ninguna prueba toca la base ni el Excel reales: todas trabajan en una carpeta
temporal que se borra al terminar.
"""

import datetime
import os
import shutil
import tempfile
import unittest
from pathlib import Path

from openpyxl import load_workbook

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.repositorio import alta_de_caso, alta_de_persona
from espejo.escritura import guardar_y_regenerar, regenerar_espejo
from espejo.hojas import HOJAS
from espejo.libro import construir_libro, convertir_a_fecha
from espejo.rutas import ruta_del_archivo_parcial, ruta_del_espejo
from pruebas.comun import caso_de_ejemplo, personas_de_ejemplo

_MRN_CON_CEROS = "055-1111-3853"
_UNIDAD_DE_SIETE_DIGITOS = "7000011"


def _sin_avisar(_):
    """Las pruebas no imprimen: cada una comprueba el aviso que le toca."""


class PruebaDelEspejo(unittest.TestCase):
    """Base con un caso, tres personas y el espejo ya generado."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_espejo_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)

        self.resultado = alta_de_caso(self.conexion, **caso_de_ejemplo())
        for persona in personas_de_ejemplo():
            alta_de_persona(self.conexion, self.resultado.id, **persona)

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _regenerar(self):
        return regenerar_espejo(self.conexion, self.carpeta, avisar=_sin_avisar)

    def _releer(self):
        self._regenerar()
        return load_workbook(ruta_del_espejo(self.carpeta))

    def _celda(self, hoja, encabezado, fila=2):
        """La celda de una columna por su nombre, no por su letra."""
        encabezados = [celda.value for celda in hoja[1]]
        return hoja.cell(row=fila, column=encabezados.index(encabezado) + 1)


class PruebaDeLasCuatroHojas(PruebaDelEspejo):

    def test_el_libro_trae_todas_las_hojas_y_ninguna_mas(self):
        """La quinta, `documentos_ilegibles`, entro con la version 8 del esquema.

        La lista se escribe entera y a mano a proposito, en vez de derivarla de
        `HOJAS`: derivada, esta prueba pasaria sola el dia que alguien anada o
        quite una hoja sin querer, que es justo el dia que tiene que fallar.
        """
        libro = self._releer()
        self.assertEqual(
            ["casos", "personas", "asignaciones", "contactos", "documentos_ilegibles"],
            libro.sheetnames,
        )

    def test_cada_hoja_lleva_sus_columnas_en_su_orden(self):
        libro = self._releer()
        for definicion in HOJAS:
            with self.subTest(hoja=definicion.nombre):
                encabezados = [celda.value for celda in libro[definicion.nombre][1]]
                self.assertEqual(
                    [columna.nombre for columna in definicion.columnas], encabezados
                )

    def test_a_ninguna_hoja_le_falta_una_columna_de_su_tabla(self):
        """La invariante que `espejo/hojas.py` escribe de si mismo, comprobada.

        Ese archivo lo repite tres veces: «un espejo al que le falta una columna de
        la tabla ya no es un espejo». Hasta hoy nadie lo comprobaba, y se cumplia
        acordandose de anadir la columna al escribir cada migracion. **Se olvido
        dos veces**: `personas.pudo_viajar` y `personas.motivo_no_viajo` entraron
        con la version 6 del esquema y no llegaron a la hoja, asi que el Excel no
        decia quien no habia podido viajar ni por que.

        Se compara contra lo que la tabla tiene de verdad en disco, y no contra una
        lista escrita a mano: una lista escrita a mano se queda desfasada por el
        mismo descuido que esta prueba caza.

        Se usa `pragma_table_info(?)` —la funcion de tabla— y no
        `PRAGMA table_info(nombre)`: la forma de pragma exigiria pegar el nombre de
        la tabla con una f-string, y `pruebas/auditoria_sql.py` dictamina INSEGURA
        cualquier instruccion armada asi, con razon: no puede seguir la variable
        hasta su valor. La funcion de tabla acepta el nombre como parametro. Medido
        en esta maquina (SQLite 3.50.4): devuelve las mismas columnas.
        """
        for definicion in HOJAS:
            with self.subTest(hoja=definicion.nombre):
                de_la_tabla = [
                    fila["name"]
                    for fila in self.conexion.execute(
                        "SELECT * FROM pragma_table_info(?)", (definicion.nombre,)
                    )
                ]
                de_la_hoja = [columna.nombre for columna in definicion.columnas]
                self.assertEqual(
                    [],
                    [nombre for nombre in de_la_tabla if nombre not in de_la_hoja],
                    f"a la hoja «{definicion.nombre}» le faltan columnas de su tabla",
                )

    def test_las_personas_de_la_base_estan_todas_en_la_hoja(self):
        libro = self._releer()
        hoja = libro["personas"]
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0],
            hoja.max_row - 1,
        )

    def test_la_hoja_de_personas_lleva_la_hoja_del_pdf_de_cada_persona(self):
        """Un espejo al que le falta una columna de la tabla ya no es un espejo.

        `personas.pagina_pdf` entro con la version 4 del esquema y tiene que
        entrar aqui a la vez: es el dato que dice de que hoja del PDF salio cada
        persona, y sin el, quien mire el Excel de un grupo de seis hojas no puede
        saber donde buscar a nadie.
        """
        alta_de_persona(
            self.conexion, self.resultado.id, nombre="DE LA HOJA 4",
            mrn="055-1111-3899", fila_formulario=8, pagina_pdf=4,
        )
        hoja = self._releer()["personas"]
        encabezados = [celda.value for celda in hoja[1]]
        self.assertIn("pagina_pdf", encabezados)
        columna = encabezados.index("pagina_pdf") + 1
        valores = [
            hoja.cell(row=fila, column=columna).value
            for fila in range(2, hoja.max_row + 1)
        ]
        self.assertIn(4, valores)

    def test_una_hoja_de_una_tabla_vacia_sale_con_su_cabecera_y_sin_filas(self):
        """`asignaciones` y `contactos` no tienen datos hasta las FASES 6 y 7."""
        libro = self._releer()
        for nombre in ("asignaciones", "contactos"):
            with self.subTest(hoja=nombre):
                self.assertEqual(1, libro[nombre].max_row)


class PruebaDeLosCerosDeDelante(PruebaDelEspejo):
    """Criterio 2: el MRN sin sus ceros deja de servir para reconciliar."""

    def test_el_mrn_conserva_los_ceros_y_va_con_formato_de_texto(self):
        libro = self._releer()
        celda = self._celda(libro["personas"], "mrn")
        self.assertEqual(_MRN_CON_CEROS, celda.value)
        self.assertEqual("@", celda.number_format)

    def test_una_unidad_que_empieza_por_cero_conserva_el_cero(self):
        self.conexion.execute(
            "UPDATE casos SET unidad_numero = '012345' WHERE id = ?", (self.resultado.id,)
        )
        celda = self._celda(self._releer()["casos"], "unidad_numero")
        self.assertEqual("012345", celda.value)
        self.assertEqual("@", celda.number_format)

    def test_una_unidad_de_siete_digitos_entra_entera(self):
        self.conexion.execute(
            "UPDATE casos SET unidad_numero = ? WHERE id = ?",
            (_UNIDAD_DE_SIETE_DIGITOS, self.resultado.id),
        )
        celda = self._celda(self._releer()["casos"], "unidad_numero")
        self.assertEqual(_UNIDAD_DE_SIETE_DIGITOS, celda.value)

    def test_el_mrn_vuelve_como_cadena_y_no_como_numero(self):
        """Control del control: si volviera como numero, el `assertEqual` de arriba
        fallaria, pero conviene decir en voz alta que el tipo importa."""
        celda = self._celda(self._releer()["personas"], "mrn")
        self.assertIsInstance(celda.value, str)


class PruebaDeLasFechas(PruebaDelEspejo):
    """Criterio 3: una fecha que es texto se ordena alfabeticamente, no por mes."""

    def test_la_fecha_de_viaje_entra_en_la_celda_como_objeto_date(self):
        """Lo que se ESCRIBE es un `date` pelado. Se mira antes de guardar el libro."""
        hoja = construir_libro(self.conexion)["casos"]
        encabezados = [celda.value for celda in hoja[1]]
        valor = hoja.cell(row=2, column=encabezados.index("fecha_viaje") + 1).value
        self.assertIs(datetime.date, type(valor))
        self.assertEqual(datetime.date(2026, 9, 8), valor)

    def test_la_fecha_de_viaje_vuelve_del_archivo_como_fecha_y_no_como_cadena(self):
        """Medido: openpyxl relee una celda de fecha como `datetime`, que ES un
        `date` (subclase). Lo que el criterio 3 prohibe —una cadena— no vuelve."""
        celda = self._celda(self._releer()["casos"], "fecha_viaje")
        self.assertNotIsInstance(celda.value, str)
        self.assertIsInstance(celda.value, datetime.date)
        self.assertEqual(datetime.date(2026, 9, 8), celda.value.date())

    def test_la_marca_de_tiempo_del_alta_vuelve_como_fecha_con_hora(self):
        celda = self._celda(self._releer()["casos"], "creado_en")
        self.assertIsInstance(celda.value, datetime.datetime)

    def test_ordenar_por_la_columna_deja_las_fechas_en_orden_cronologico(self):
        """Con meses y anios distintos, que es donde el orden alfabetico enganaria."""
        for numero, fecha in (("CASP2512", "2025-12-30"), ("CASP2701", "2027-01-05")):
            alta_de_caso(self.conexion, numero_caso=numero, fecha_viaje=fecha)

        hoja = self._releer()["casos"]
        encabezados = [celda.value for celda in hoja[1]]
        columna = encabezados.index("fecha_viaje") + 1
        valores = [
            hoja.cell(row=fila, column=columna).value for fila in range(2, hoja.max_row + 1)
        ]
        self.assertEqual(3, len(valores))
        self.assertEqual(sorted(valores), sorted(valores, key=lambda v: v.timestamp()))
        self.assertEqual(
            [datetime.date(2025, 12, 30), datetime.date(2026, 9, 8), datetime.date(2027, 1, 5)],
            sorted(valor.date() for valor in valores),
        )

    def test_una_cadena_que_no_es_fecha_se_deja_tal_cual_y_no_se_inventa(self):
        """Regla permanente 1: antes una fecha fea que una fecha inventada."""
        self.assertEqual("no consta", convertir_a_fecha("no consta"))
        self.assertIsNone(convertir_a_fecha(None))


class PruebaDeLaFormaDelLibro(PruebaDelEspejo):
    """Criterio 6: se comprueba CONTANDO, no mirando."""

    def test_cero_celdas_combinadas_en_las_cuatro_hojas(self):
        libro = self._releer()
        combinadas = sum(len(libro[hoja].merged_cells.ranges) for hoja in libro.sheetnames)
        self.assertEqual(0, combinadas)

    def test_cero_celdas_con_relleno_distinto_del_de_por_defecto(self):
        libro = self._releer()
        con_relleno = [
            f"{hoja}!{celda.coordinate}"
            for hoja in libro.sheetnames
            for fila in libro[hoja].iter_rows()
            for celda in fila
            if celda.fill is not None and celda.fill.patternType is not None
        ]
        self.assertEqual([], con_relleno)

    def test_la_fila_uno_esta_congelada_y_hay_autofiltro_en_las_cuatro_hojas(self):
        libro = self._releer()
        for nombre in libro.sheetnames:
            with self.subTest(hoja=nombre):
                self.assertEqual("A2", libro[nombre].freeze_panes)
                self.assertIsNotNone(libro[nombre].auto_filter.ref)


class PruebaDeLaEscrituraSegura(PruebaDelEspejo):
    """Criterios 4 y 5: nunca fallar callado, y nunca dejar un archivo a medias."""

    def test_el_espejo_se_escribe_en_la_misma_carpeta_que_la_base(self):
        resultado = self._regenerar()
        self.assertEqual(self.carpeta / "fichas.xlsx", resultado.ruta)
        self.assertTrue(resultado.ruta.is_file())

    def test_no_queda_ningun_archivo_parcial_cuando_todo_va_bien(self):
        self._regenerar()
        self.assertFalse(ruta_del_archivo_parcial(self.carpeta).exists())

    def test_con_el_archivo_bloqueado_avisa_y_no_levanta(self):
        self._regenerar()
        manija = open(ruta_del_espejo(self.carpeta), "rb")
        try:
            recogidos = []
            resultado = regenerar_espejo(self.conexion, self.carpeta, avisar=recogidos.append)
        finally:
            manija.close()

        self.assertFalse(resultado.escrito)
        self.assertEqual(1, len(recogidos))
        self.assertIn("fichas.xlsx", recogidos[0])
        self.assertIn("Excel", recogidos[0])
        self.assertIn("GUARDADOS EN LA BASE", recogidos[0])

    def test_con_el_archivo_bloqueado_el_dato_si_queda_en_la_base(self):
        """Criterio 4: el aviso no puede costar el guardado."""
        self._regenerar()
        manija = open(ruta_del_espejo(self.carpeta), "rb")
        try:
            resultado = guardar_y_regenerar(
                self.conexion,
                lambda: alta_de_caso(self.conexion, numero_caso="CASP2610"),
                carpeta_de_datos=self.carpeta,
                avisar=_sin_avisar,
            )
        finally:
            manija.close()

        self.assertFalse(resultado.espejo.escrito)
        guardados = self.conexion.execute(
            "SELECT COUNT(*) FROM casos WHERE numero_caso = 'CASP2610'"
        ).fetchone()[0]
        self.assertEqual(1, guardados)

    def test_con_el_archivo_bloqueado_el_xlsx_anterior_sigue_intacto(self):
        """Criterio 5: el archivo que ya estaba se sigue abriendo sin errores."""
        self._regenerar()
        antes = ruta_del_espejo(self.carpeta).read_bytes()

        manija = open(ruta_del_espejo(self.carpeta), "rb")
        try:
            regenerar_espejo(self.conexion, self.carpeta, avisar=_sin_avisar)
        finally:
            manija.close()

        self.assertEqual(antes, ruta_del_espejo(self.carpeta).read_bytes())
        self.assertEqual(len(HOJAS), len(load_workbook(ruta_del_espejo(self.carpeta)).sheetnames))

    def test_un_parcial_de_una_ejecucion_muerta_no_se_llama_como_el_definitivo(self):
        """Criterio 5, la mitad estructural: lo que queda a medias lleva otro nombre."""
        self.assertNotEqual(
            ruta_del_espejo(self.carpeta).name, ruta_del_archivo_parcial(self.carpeta).name
        )
        self.assertTrue(ruta_del_archivo_parcial(self.carpeta).name.endswith(".parcial"))

    def test_un_parcial_abandonado_lo_pisa_la_siguiente_regeneracion(self):
        parcial = ruta_del_archivo_parcial(self.carpeta)
        parcial.write_bytes(b"basura de una ejecucion muerta")
        self._regenerar()
        self.assertFalse(parcial.exists())
        self.assertTrue(load_workbook(ruta_del_espejo(self.carpeta)).sheetnames)

    def test_la_carpeta_se_crea_si_todavia_no_existe(self):
        carpeta_nueva = self.carpeta / "todavia_no_existe"
        regenerar_espejo(self.conexion, carpeta_nueva, avisar=_sin_avisar)
        self.assertTrue((carpeta_nueva / "fichas.xlsx").is_file())


class PruebaDelGuardadoQueRegenera(PruebaDelEspejo):
    """Criterio 1: no hay boton de exportar, porque no hace falta ninguno."""

    def test_guardar_deja_el_espejo_al_dia_sin_pulsar_nada(self):
        resultado = guardar_y_regenerar(
            self.conexion,
            lambda: alta_de_caso(self.conexion, numero_caso="CASP2610"),
            carpeta_de_datos=self.carpeta,
            avisar=_sin_avisar,
        )
        self.assertTrue(resultado.espejo.escrito)
        hoja = load_workbook(resultado.espejo.ruta)["casos"]
        numeros = [hoja.cell(row=fila, column=2).value for fila in range(2, hoja.max_row + 1)]
        self.assertIn("CASP2610", numeros)

    def test_la_marca_de_tiempo_del_archivo_cambia_en_cada_guardado(self):
        primera = self._regenerar().ruta
        marca_inicial = os.stat(primera).st_mtime_ns
        os.utime(primera, ns=(marca_inicial - 10**9, marca_inicial - 10**9))

        guardar_y_regenerar(
            self.conexion,
            lambda: alta_de_caso(self.conexion, numero_caso="CASP2610"),
            carpeta_de_datos=self.carpeta,
            avisar=_sin_avisar,
        )
        self.assertNotEqual(marca_inicial - 10**9, os.stat(primera).st_mtime_ns)

    def test_si_el_guardado_falla_no_se_regenera_nada(self):
        with self.assertRaises(Exception):
            guardar_y_regenerar(
                self.conexion,
                lambda: alta_de_caso(self.conexion, numero_caso="MAL"),
                carpeta_de_datos=self.carpeta,
                avisar=_sin_avisar,
            )
        self.assertFalse(ruta_del_espejo(self.carpeta).exists())


class PruebaDeLaInyeccionEnLaHoja(PruebaDelEspejo):
    """Un nombre leido por OCR no puede convertirse en una formula de Excel."""

    def test_un_nombre_que_empieza_por_igual_se_guarda_como_texto(self):
        alta_de_persona(
            self.conexion, self.resultado.id, nombre="=1+1", fila_formulario=9
        )
        libro = self._releer()
        hoja = libro["personas"]
        valores = [
            hoja.cell(row=fila, column=4).value for fila in range(2, hoja.max_row + 1)
        ]
        self.assertIn("=1+1", valores)
        for fila in range(2, hoja.max_row + 1):
            with self.subTest(fila=fila):
                self.assertNotEqual("f", hoja.cell(row=fila, column=4).data_type)


class PruebaDeLaIndependenciaDelLibro(unittest.TestCase):
    """El libro se construye sin tocar el disco: eso es lo que permite probarlo."""

    def test_construir_el_libro_no_escribe_nada(self):
        carpeta = Path(tempfile.mkdtemp(prefix="fichas_espejo_"))
        try:
            conexion = abrir_conexion(carpeta / "fichas.db")
            aplicar_esquema(conexion)
            libro = construir_libro(conexion)
            conexion.close()
            self.assertEqual(len(HOJAS), len(libro.sheetnames))
            self.assertFalse((carpeta / "fichas.xlsx").exists())
        finally:
            shutil.rmtree(carpeta, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
