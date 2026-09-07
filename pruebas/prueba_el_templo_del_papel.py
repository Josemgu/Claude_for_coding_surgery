"""El templo se lee del formulario, se guarda, y sale en la cabecera del Excel.

**El hallazgo MEDIO de la auditoría final de QA (2026-09-03).** La cabecera A2 del
Excel del agente del proyecto viejo dice «Templo: … · Sale el …», y aquí salía sin
la primera mitad. No porque el dato no estuviera: **está impreso en el papel** —la
etiqueta «Nombre del templo» / «Temple Name» ya vivía en `extraccion/etiquetas.py`
y ya se localizaba, porque hace de cierre del bloque de personas— y lo que hubiera
debajo se tiraba.

**La decisión, literal:** se lee a `casos.templo_nombre`, **texto y sin catálogo**,
con su migración (versión 10 del esquema). El catálogo de templos con sus colores
es del dueño y todavía no existe; validar contra una lista inventada rechazaría
mañana un templo verdadero, que es el mismo criterio con el que
`casos.estado_recomendacion` tampoco lleva `CHECK`.

**Lo que este circuito NO cubre, y va dicho aquí para que no se descubra después:**
el templo no se dibuja en la pantalla de corrección, así que **un templo mal leído
no se puede corregir a mano**. Añadir un quinto campo a esa pantalla no lo pidió el
pase y esa pantalla acaba de pasar auditoría. Queda como lo que le falta a esta
entrega.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.asignaciones import asignar_caso
from datos.companeros import alta_de_companero
from datos.repositorio import alta_de_caso, alta_de_persona, leer_caso_por_numero
from extraccion.campos import campo_vacio
from extraccion.normalizacion import normalizar_nombre_del_templo
from importacion.guardado import guardar_las_paginas_del_documento
from paquete.exportacion import exportar_paquete
from paquete.trabajo import cabecera_de
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _campo, _formulario

TEMPLO = "Santo Domingo República Dominicana"


class ElNormalizadorDelTemploNoCorrigeNada(unittest.TestCase):
    """Regla permanente 1: no hay catálogo y no se elige «el más parecido»."""

    def test_devuelve_el_texto_tal_como_esta(self):
        self.assertEqual(normalizar_nombre_del_templo(TEMPLO), TEMPLO)

    def test_junta_los_espacios_de_sobra_del_recorte(self):
        """No es corregir el dato: es la basura que deja el recorte del OCR."""
        self.assertEqual(
            normalizar_nombre_del_templo("  Santo   Domingo  "), "Santo Domingo"
        )

    def test_un_templo_mal_leido_vuelve_mal_leido_y_no_convertido_en_otro(self):
        """Elegir «el más parecido» de una lista inventada sería inventar un dato."""
        self.assertEqual(
            normalizar_nombre_del_templo("Sant0 D0mingo"), "Sant0 D0mingo"
        )

    def test_sin_texto_vuelve_nulo_y_no_una_cadena_vacia(self):
        """None significa «no se leyó»; una cadena vacía se guardaría como dato."""
        self.assertIsNone(normalizar_nombre_del_templo("   "))
        self.assertIsNone(normalizar_nombre_del_templo(None))


class ElTemploLeidoLlegaALaBase(PruebaConBaseTemporal):
    """Dada una página cuyo formulario trae el templo, cuando se importa, entonces
    queda guardado en `casos.templo_nombre`."""

    def test_el_templo_de_la_pagina_se_guarda_en_el_caso(self):
        guardar_las_paginas_del_documento(
            self.conexion,
            [_formulario()._replace(templo_nombre=_campo(TEMPLO, confianza=0.93))],
        )
        caso = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertEqual(caso["templo_nombre"], TEMPLO)

    def test_una_pagina_sin_templo_lo_deja_a_nulo_y_no_lo_inventa(self):
        guardar_las_paginas_del_documento(
            self.conexion, [_formulario()._replace(templo_nombre=campo_vacio())]
        )
        caso = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertIsNone(caso["templo_nombre"])

    def test_el_templo_no_se_usa_para_decidir_si_dos_hojas_se_unen(self):
        """La decisión es «misma fecha y misma unidad», y el templo no está.

        Va comprobado y no supuesto: si entrara en la comparación, un templo mal
        leído en una hoja partiría un grupo real en dos casos.
        """
        resultados = guardar_las_paginas_del_documento(
            self.conexion,
            [
                _formulario(pagina=1)._replace(
                    templo_nombre=_campo(TEMPLO, confianza=0.93)
                ),
                _formulario(pagina=2)._replace(
                    templo_nombre=_campo("Otro Templo", confianza=0.93)
                ),
            ],
        )
        self.assertTrue(resultados[1].importada)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )


class LaCabeceraDelExcelDelAgenteLoPinta(PruebaConBaseTemporal):
    """Dado un caso con templo guardado, cuando se genera el paquete del compañero,
    entonces la cabecera A2 dice «Templo: … · Sale el …», como la del viejo."""

    def setUp(self):
        super().setUp()
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_templo_"))
        self.addCleanup(shutil.rmtree, self.carpeta, ignore_errors=True)
        self.companero_id = alta_de_companero(self.conexion, "Ana")
        self.caso_id = alta_de_caso(
            self.conexion, "CASP2609", unidad_numero="123456",
            unidad_nombre="Cuatricentenaria", fecha_viaje="2026-09-08",
            pagina_pdf=1, templo_nombre=TEMPLO,
        ).id
        asignar_caso(self.conexion, self.caso_id, self.companero_id)
        alta_de_persona(
            self.conexion, self.caso_id, mrn="055-1111-3853", nombre="ANONIMO, J",
            fila_formulario=1, pagina_pdf=1,
        )

    def _celda(self, ruta, referencia):
        from openpyxl import load_workbook

        libro = load_workbook(str(ruta))
        try:
            return libro.active[referencia].value
        finally:
            libro.close()

    def test_la_segunda_linea_lleva_el_templo_y_la_salida(self):
        paquete = exportar_paquete(self.conexion, self.companero_id, self.carpeta)
        self.assertEqual(
            self._celda(paquete.ruta_del_excel, "A2"),
            f"Templo: {TEMPLO} · Sale el 08-09-2026",
        )

    def test_la_primera_linea_sigue_llevando_el_numero_de_caso(self):
        """Lo que ya estaba no se toca: A1 es «Preparación… · CASP2609»."""
        paquete = exportar_paquete(self.conexion, self.companero_id, self.carpeta)
        self.assertIn("CASP2609", self._celda(paquete.ruta_del_excel, "A1"))

    def test_sin_templo_guardado_la_linea_sale_solo_con_la_salida(self):
        """Sin el dato NO se escribe «Templo: » a secas: parecería un dato perdido."""
        self.conexion.execute(
            "UPDATE casos SET templo_nombre = NULL WHERE id = ?", (self.caso_id,)
        )
        paquete = exportar_paquete(self.conexion, self.companero_id, self.carpeta)
        self.assertEqual(
            self._celda(paquete.ruta_del_excel, "A2"), "Sale el 08-09-2026"
        )

    def test_con_dos_templos_distintos_en_el_paquete_no_se_elige_ninguno(self):
        """Un título que afirma un templo sobre una hoja que lleva dos es peor."""
        cabecera = cabecera_de(
            [
                {"numero_caso": "CASP2609", "fecha_viaje": "2026-09-08", "templo": TEMPLO},
                {"numero_caso": "CBSP2609", "fecha_viaje": "2026-09-09", "templo": "Otro"},
            ],
            agente="Ana",
        )
        self.assertEqual(cabecera.templo, "")


if __name__ == "__main__":  # pragma: no cover
    unittest.main()
