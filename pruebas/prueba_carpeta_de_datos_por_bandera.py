"""`--carpeta-de-datos` manda sobre la carpeta que resuelve Windows, y se ve.

**Por qué existe esta opción, dicho por QA en la auditoría final (2026-09-03):** el
`.exe` no admitía otra carpeta de datos, así que conducirlo de punta a punta
obligaba a escribir en `Documentos\\Fichas`, que es donde está la base real del
dueño con nombres y MRN de personas reales. QA no pudo probar el ejecutable
completo y lo dijo así: es lo que convierte «no lo pude probar» en «probado».

La otra mitad, y la que la hace segura: **el pie de la ventana lo dice**. Un
programa que escribe en otro sitio sin decirlo es como se pierde una tarde
buscando los datos en la carpeta de siempre. El estado peligroso no es escribir en
otra carpeta: es no saber en cuál se está escribiendo.

**Lo que estas pruebas NO cubren:** que el `.exe` empaquetado la respete. Eso no se
puede comprobar sin construir el paquete, y va medido a mano en el informe del
pase, no aquí.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from fichas import BANDERA_DE_LA_CARPETA, ErrorDeArranque, carpeta_de_datos_pedida, main


class LaBanderaSeLeeEnSusDosFormas(unittest.TestCase):
    """Las dos formas en que se escribe una opción son las dos normales."""

    def test_separada_por_un_espacio(self):
        pedida = carpeta_de_datos_pedida([BANDERA_DE_LA_CARPETA, r"C:\Temp\Prueba"])
        self.assertEqual(pedida, Path(r"C:\Temp\Prueba"))

    def test_pegada_con_un_igual(self):
        pedida = carpeta_de_datos_pedida([f"{BANDERA_DE_LA_CARPETA}=C:\\Temp\\Prueba"])
        self.assertEqual(pedida, Path(r"C:\Temp\Prueba"))

    def test_sin_la_bandera_no_se_pide_ninguna_carpeta(self):
        """Y entonces manda la que resuelve la API de Windows, como siempre."""
        self.assertIsNone(carpeta_de_datos_pedida([]))
        self.assertIsNone(carpeta_de_datos_pedida(["--solo-mostrar-ruta"]))

    def test_la_bandera_sin_ruta_detras_levanta_y_no_se_cae_a_la_de_siempre(self):
        """Escribir en la base real creyendo que se escribe en una temporal es el
        accidente que esta opción existe para evitar."""
        with self.assertRaises(ErrorDeArranque):
            carpeta_de_datos_pedida([BANDERA_DE_LA_CARPETA])
        with self.assertRaises(ErrorDeArranque):
            carpeta_de_datos_pedida([f"{BANDERA_DE_LA_CARPETA}="])

    def test_el_mensaje_de_ese_error_esta_en_espanol_y_dice_como_se_escribe(self):
        try:
            carpeta_de_datos_pedida([BANDERA_DE_LA_CARPETA])
        except ErrorDeArranque as causa:
            self.assertIn("necesita una ruta detrás", str(causa))
            self.assertIn(BANDERA_DE_LA_CARPETA, str(causa))
        else:  # pragma: no cover - la de arriba ya lo comprueba
            self.fail("no levantó")


class LaBanderaMandaSobreLaCarpetaDeLaApi(unittest.TestCase):
    """Dada la bandera, cuando el programa dice dónde escribiría, entonces dice la
    carpeta que se le pidió y no la de Documentos."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_bandera_"))
        self.addCleanup(shutil.rmtree, self.carpeta, ignore_errors=True)

    def test_mostrar_la_ruta_dice_la_carpeta_pedida(self):
        from datos.arranque import mostrar_ruta_de_datos

        lineas = []
        ruta = mostrar_ruta_de_datos(
            carpeta_de_datos=self.carpeta, escribir=lineas.append
        )
        self.assertEqual(ruta.parent, self.carpeta)
        self.assertTrue(any(str(self.carpeta) in linea for linea in lineas))

    def test_la_base_se_crea_dentro_de_la_carpeta_pedida_y_en_ningun_otro_sitio(self):
        from datos.arranque import preparar_base_de_datos

        ruta = preparar_base_de_datos(
            carpeta_de_datos=self.carpeta, escribir=lambda linea: None
        )
        self.assertEqual(ruta.parent, self.carpeta)
        self.assertTrue(ruta.is_file())

    def test_el_punto_de_entrada_pasa_la_carpeta_al_mostrar_la_ruta(self):
        """Se comprueba por el camino real —`main`— y no llamando por debajo."""
        import io
        from contextlib import redirect_stdout

        salida = io.StringIO()
        with redirect_stdout(salida):
            codigo = main(
                ["--solo-mostrar-ruta", BANDERA_DE_LA_CARPETA, str(self.carpeta)]
            )
        self.assertEqual(codigo, 0)
        self.assertIn(str(self.carpeta), salida.getvalue())

    def test_una_bandera_sin_ruta_no_arranca_y_lo_dice(self):
        import io
        from contextlib import redirect_stdout

        salida = io.StringIO()
        with redirect_stdout(salida):
            codigo = main(["--solo-mostrar-ruta", BANDERA_DE_LA_CARPETA])
        self.assertEqual(codigo, 2)
        self.assertIn("necesita una ruta detrás", salida.getvalue())


def _hay_ventanas():
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElPieDeLaVentanaLoDice(unittest.TestCase):
    """Dada la ventana abierta con una carpeta forzada, cuando se lee su pie,
    entonces dice la carpeta Y dice que no es la de Documentos."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_pie_"))
        self.addCleanup(shutil.rmtree, self.carpeta, ignore_errors=True)

    def _pie(self, carpeta_de_datos):
        from interfaz.aplicacion import Aplicacion

        aplicacion = Aplicacion(
            carpeta_de_datos=carpeta_de_datos, escribir=lambda linea: None
        )
        try:
            aplicacion.raiz.attributes("-alpha", 0.0)
            return aplicacion._texto_del_pie()
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()

    def test_el_pie_dice_la_carpeta_que_se_le_pidio(self):
        self.assertIn(str(self.carpeta), self._pie(self.carpeta))

    def test_el_pie_avisa_de_que_no_es_la_carpeta_de_siempre(self):
        """Sin este aviso, la opción sería un modo silencioso, que es lo peligroso."""
        from interfaz.aplicacion import Aplicacion

        self.assertIn(Aplicacion.AVISO_DE_CARPETA_FORZADA, self._pie(self.carpeta))


class ElPieDiceCuantoTardoEnAbrir(unittest.TestCase):
    """El único número que puede venir del portátil del dueño.

        DADO el programa arrancado con su reloj de partida,
        CUANDO la ventana ya está pintada y responde,
        ENTONCES el pie dice «Abrió en N,N s».

    Existe porque de su máquina no hay ni una medición: él dice «lento» y «se está
    poniendo súper lento al abrir toda la interfaz», y eso no se puede comparar con
    nada. Con la cifra en el pie, manda un número.
    """

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_apertura_"))
        self.addCleanup(shutil.rmtree, self.carpeta, ignore_errors=True)

    def _aplicacion(self, arrancado_en):
        from interfaz.aplicacion import Aplicacion

        aplicacion = Aplicacion(
            carpeta_de_datos=self.carpeta,
            escribir=lambda linea: None,
            arrancado_en=arrancado_en,
        )
        aplicacion.raiz.attributes("-alpha", 0.0)
        return aplicacion

    def test_el_pie_dice_la_cifra_de_apertura_en_segundos(self):
        import time

        aplicacion = self._aplicacion(time.perf_counter())
        try:
            aplicacion.anotar_cuanto_tardo_en_abrir()
            self.assertRegex(aplicacion._texto_del_pie(), r"Abrió en \d+,\d s")
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()

    def test_la_cifra_se_escribe_con_coma_y_no_con_punto(self):
        """En español el decimal es una coma. Un «1.4 s» se lee como otra cosa.

        El reloj de partida se pone DESPUÉS de construir la ventana, no antes:
        construirla ya tarda medio segundo y ese medio segundo se sumaría al 1,42
        que esta prueba fija. Lo que se mide aquí es cómo se escribe la cifra, no
        cuánto tarda la ventana.
        """
        import time

        aplicacion = self._aplicacion(None)
        try:
            aplicacion._arrancado_en = time.perf_counter() - 1.42
            aplicacion.anotar_cuanto_tardo_en_abrir()
            self.assertIn("Abrió en 1,4 s", aplicacion._texto_del_pie())
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()

    def test_sin_reloj_de_partida_no_se_inventa_ninguna_cifra(self):
        """Regla permanente 1 aplicada a un número: si no se sabe, no se dice."""
        aplicacion = self._aplicacion(None)
        try:
            aplicacion.anotar_cuanto_tardo_en_abrir()
            self.assertIsNone(aplicacion.segundos_hasta_verse)
            self.assertNotIn("Abrió en", aplicacion._texto_del_pie())
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()

    def test_la_ruta_de_datos_sigue_estando_delante_de_la_cifra(self):
        """Lo que no puede perderse es dónde escribe el programa."""
        import time

        aplicacion = self._aplicacion(time.perf_counter())
        try:
            aplicacion.anotar_cuanto_tardo_en_abrir()
            texto = aplicacion._texto_del_pie()
            self.assertLess(texto.index("Datos en:"), texto.index("Abrió en"))
            self.assertIn(str(self.carpeta), texto)
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()


if __name__ == "__main__":  # pragma: no cover
    unittest.main()
