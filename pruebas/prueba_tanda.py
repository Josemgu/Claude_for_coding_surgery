"""Cientos de PDF de una vez: que se elijan, que ninguno tumbe al resto, y el resumen.

Los criterios salen del pase del 2026-09-02 y se escribieron antes que el codigo:

    2. Se seleccionan muchos PDF a la vez, y tambien una carpeta entera.
    3. Con una tanda grande lo procesado queda guardado aunque se cancele.
    4. Un PDF roto en mitad de la tanda no tumba el resto.
    5. El resumen final dice cuantos entraron, cuantos a medias y cuales fallaron.

**Lo que estas pruebas NO comprueban, y hay que decirlo:** que la ventana responda.
Eso depende del bucle de eventos de Tk y de un hilo, y comprobarlo de verdad exige
mover la ventana con el raton mientras corre una tanda larga. Aqui se comprueba lo
que si se puede comprobar sin ventana —que leer y guardar estan separados, que un
fallo se convierte en dato y que las cifras salen—, y lo de la ventana queda para
QA con su nombre puesto.

Ningun PDF real se toca: los archivos de estas pruebas son ficheros temporales con
texto dentro, que es exactamente lo que hace falta para probar el camino del fallo.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.ilegibles import NO_SE_PUDO_ABRIR, documentos_ilegibles
from importacion.tanda import (
    LecturaDeUnPdf,
    ResumenDeLaTanda,
    guardar_un_pdf,
    leer_un_pdf,
    rutas_de_pdf,
)
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _formulario


class CarpetaDePrueba(unittest.TestCase):
    """Arma un arbol de archivos temporal y lo borra al terminar."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_tanda_"))

    def tearDown(self):
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _crear(self, nombre_relativo, contenido="no soy un PDF"):
        ruta = self.carpeta / nombre_relativo
        ruta.parent.mkdir(parents=True, exist_ok=True)
        ruta.write_text(contenido, encoding="utf-8")
        return ruta


class ElegirLosPdf(CarpetaDePrueba):
    """Criterio 2: varios archivos, o una carpeta entera."""

    def test_varios_archivos_sueltos_se_devuelven_los_dos(self):
        primero = self._crear("uno.pdf")
        segundo = self._crear("dos.pdf")
        self.assertEqual(rutas_de_pdf([primero, segundo]), sorted([primero, segundo]))

    def test_una_carpeta_devuelve_los_pdf_que_tiene_dentro(self):
        self._crear("uno.pdf")
        self._crear("dos.pdf")
        self.assertEqual(len(rutas_de_pdf([self.carpeta])), 2)

    def test_una_carpeta_se_recorre_tambien_por_dentro(self):
        """Los escaneos llegan repartidos por meses, en subcarpetas."""
        self._crear("septiembre/uno.pdf")
        self._crear("octubre/dos.pdf")
        self.assertEqual(len(rutas_de_pdf([self.carpeta])), 2)

    def test_lo_que_no_es_pdf_se_queda_fuera(self):
        self._crear("uno.pdf")
        self._crear("notas.txt")
        self.assertEqual(len(rutas_de_pdf([self.carpeta])), 1)

    def test_la_extension_en_mayusculas_tambien_cuenta(self):
        """Un escaner que escribe «.PDF» no puede dejar el archivo fuera."""
        self._crear("uno.PDF")
        self.assertEqual(len(rutas_de_pdf([self.carpeta])), 1)

    def test_elegir_un_archivo_y_ademas_su_carpeta_no_lo_procesa_dos_veces(self):
        """Es un descuido normal, y duplicarlo pareceria un fallo del programa."""
        uno = self._crear("uno.pdf")
        self.assertEqual(rutas_de_pdf([uno, self.carpeta]), [uno])

    def test_una_carpeta_sin_pdf_devuelve_la_lista_vacia(self):
        self._crear("notas.txt")
        self.assertEqual(rutas_de_pdf([self.carpeta]), [])


class UnPdfRotoNoTumbaLaTanda(CarpetaDePrueba):
    """Criterio 4, medido sobre el camino real: un archivo que no es un PDF."""

    def test_leer_un_archivo_que_no_es_pdf_devuelve_el_error_y_no_levanta(self):
        roto = self._crear("roto.pdf", "esto no es un PDF ni de lejos")
        lectura = leer_un_pdf(roto, motor_ocr=None)
        self.assertEqual(lectura.formularios, [])
        self.assertIsNotNone(lectura.error)

    def test_el_error_dice_de_que_tipo_fue(self):
        """«No se pudo» no sirve: el tipo de la excepcion es la pista."""
        roto = self._crear("roto.pdf", "esto no es un PDF")
        self.assertIn(":", leer_un_pdf(roto, motor_ocr=None).error)

    def test_un_archivo_que_ni_existe_tampoco_levanta(self):
        lectura = leer_un_pdf(self.carpeta / "no_existe.pdf", motor_ocr=None)
        self.assertIsNotNone(lectura.error)


class ElFalloSeGuardaComoRenglon(PruebaConBaseTemporal):
    """Criterio 4, segunda mitad: el que falla deja rastro."""

    def test_un_pdf_que_no_se_pudo_abrir_deja_su_renglon(self):
        lectura = LecturaDeUnPdf(Path(r"C:\Escaneos\roto.pdf"), [], "PdfReadError: fin inesperado")
        guardar_un_pdf(self.conexion, lectura)
        filas = documentos_ilegibles(self.conexion)
        self.assertEqual(len(filas), 1)
        self.assertEqual(filas[0]["motivo"], NO_SE_PUDO_ABRIR)
        self.assertIn("PdfReadError", filas[0]["detalle"])

    def test_ese_renglon_no_lleva_pagina_porque_no_hubo_ninguna(self):
        """Inventar «página 1» de un archivo que no se abrio seria un dato falso."""
        guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: x")
        )
        self.assertIsNone(documentos_ilegibles(self.conexion)[0]["pagina_pdf"])

    def test_el_resultado_del_documento_roto_trae_todo_a_cero(self):
        resultado = guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: x")
        )
        self.assertEqual((resultado.paginas, resultado.casos, resultado.personas), (0, 0, 0))

    def test_despues_de_uno_roto_el_siguiente_entra_igual(self):
        """Es el criterio entero: 488 documentos buenos no se pierden por uno malo."""
        guardar_un_pdf(self.conexion, LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: x"))
        bueno = guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("bueno.pdf"), [_formulario()], None)
        )
        self.assertEqual(bueno.casos, 1)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )


class LasCifrasDeUnDocumento(PruebaConBaseTemporal):
    """Que cuenta cada documento, que es lo que suma el resumen."""

    def test_un_documento_bueno_cuenta_su_caso_y_sus_personas(self):
        resultado = guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("uno.pdf"), [_formulario()], None)
        )
        self.assertEqual((resultado.paginas, resultado.casos, resultado.personas), (1, 1, 1))
        self.assertEqual(resultado.pendientes, 0)

    def test_una_pagina_sin_numero_cuenta_como_pendiente(self):
        from extraccion.campos import campo_vacio

        sin_numero = _formulario()._replace(numero_caso=campo_vacio())
        resultado = guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("uno.pdf"), [sin_numero], None)
        )
        self.assertEqual(resultado.pendientes, 1)
        self.assertEqual(resultado.casos, 1)

    def test_una_pagina_repetida_entra_y_cuenta_como_duplicado(self):
        """Dado un caso ya importado, cuando vuelve a llegar, entonces entra —cuenta
        como caso y como duplicado— y NO cuenta como rechazado: ya no se rechaza."""
        guardar_un_pdf(self.conexion, LecturaDeUnPdf(Path("uno.pdf"), [_formulario()], None))
        segundo = guardar_un_pdf(
            self.conexion, LecturaDeUnPdf(Path("dos.pdf"), [_formulario()], None)
        )
        self.assertEqual(segundo.rechazadas, 0)
        self.assertEqual(segundo.casos, 1)
        self.assertEqual(segundo.duplicados, 1)


class ElResumenDeLaTanda(PruebaConBaseTemporal):
    """Criterio 5: cuantos entraron, cuantos a medias, y cuales fallaron."""

    def _tanda(self, *lecturas):
        resumen = ResumenDeLaTanda(len(lecturas))
        for lectura in lecturas:
            resumen.anotar(guardar_un_pdf(self.conexion, lectura))
        return resumen

    def test_suma_los_documentos_y_sus_casos(self):
        resumen = self._tanda(
            LecturaDeUnPdf(Path("uno.pdf"), [_formulario(numero="CASP2609")], None),
            LecturaDeUnPdf(Path("dos.pdf"), [_formulario(numero="CASD2609")], None),
        )
        self.assertEqual(resumen.documentos, 2)
        self.assertEqual(resumen.casos, 2)
        self.assertEqual(resumen.personas, 2)

    def test_los_fallidos_se_nombran_uno_a_uno_con_su_motivo(self):
        resumen = self._tanda(
            LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: fin inesperado"),
        )
        self.assertEqual(len(resumen.fallidos), 1)
        texto = resumen.texto()
        self.assertIn("roto.pdf", texto)
        self.assertIn("PdfReadError", texto)

    def test_el_texto_lleva_siempre_las_cinco_cifras_aunque_sean_cero(self):
        """Un resumen que se calla los ceros hace dudar de los que si escribe."""
        texto = ResumenDeLaTanda(0).texto()
        for cifra in (
            "Documentos procesados", "Páginas leídas", "Casos guardados",
            "Pendientes de identificar", "Documentos que fallaron",
        ):
            self.assertIn(cifra, texto)

    def test_una_tanda_cancelada_lo_dice_y_dice_que_lo_hecho_quedo(self):
        resumen = ResumenDeLaTanda(10)
        resumen.cancelada = True
        texto = resumen.texto()
        self.assertIn("CANCELADA", texto)
        self.assertIn("quedó guardado", texto)

    def test_lo_procesado_antes_de_cancelar_sigue_en_la_base(self):
        """Criterio 3: cancelar no deshace nada de lo que ya entro."""
        self._tanda(LecturaDeUnPdf(Path("uno.pdf"), [_formulario()], None))
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )


if __name__ == "__main__":
    unittest.main()
