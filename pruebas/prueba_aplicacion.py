"""La ventana dice donde escribe, y lo dice DONDE SE VE.

Criterio 6a de la FASE 9: «el programa muestra la ruta de datos que resolvio
antes de escribir nada». Hasta la FASE 9 eso se cumplia imprimiendo por `stdout`,
y bastaba porque el programa se arrancaba desde una consola.

**Empaquetado con `console=False` no basta**, y ese es el motivo de este archivo.
PyInstaller deja `sys.stdout` en `None` en un paquete sin consola, y `print` sobre
`None` no levanta nada: se traga la linea en silencio. La ruta se seguiria
«mostrando» y nadie la veria nunca. Por eso la prueba mira la VENTANA, no la
salida estandar: es lo unico que Miguel puede leer.
"""

import shutil
import sys
import tempfile
import unittest
from pathlib import Path


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class PruebaDeLaVentana(unittest.TestCase):
    """Se abre la aplicacion de verdad sobre una carpeta temporal."""

    def setUp(self):
        from interfaz.aplicacion import Aplicacion

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_ventana_"))
        self.aplicacion = Aplicacion(carpeta_de_datos=self.carpeta, escribir=lambda _: None)
        self.aplicacion.raiz.withdraw()

    def tearDown(self):
        self.aplicacion.conexion.close()
        self.aplicacion.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _textos_de_la_ventana(self, widget=None):
        """Todo el texto dibujado en la ventana, mirando widget por widget."""
        widget = widget if widget is not None else self.aplicacion.raiz
        textos = []
        try:
            textos.append(str(widget.cget("text")))
        except Exception:
            pass
        for hijo in widget.winfo_children():
            textos.extend(self._textos_de_la_ventana(hijo))
        return textos

    def test_la_ruta_de_datos_se_ve_en_la_ventana(self):
        """La carpeta resuelta aparece dibujada, no solo impresa por stdout."""
        dibujado = " | ".join(self._textos_de_la_ventana())
        self.assertIn(str(self.carpeta), dibujado)

    def test_la_ventana_no_depende_de_que_haya_consola(self):
        """Con `sys.stdout` a None —el paquete sin consola— la ruta sigue viendose.

        Es la condicion exacta de un `.exe` construido con `console=False`. Si el
        unico camino fuera `print`, aqui no se veria nada.
        """
        from interfaz.aplicacion import Aplicacion

        carpeta = Path(tempfile.mkdtemp(prefix="fichas_sin_consola_"))
        salida = sys.stdout
        sys.stdout = None
        try:
            aplicacion = Aplicacion(carpeta_de_datos=carpeta)
            aplicacion.raiz.withdraw()
        finally:
            sys.stdout = salida
        try:
            dibujado = " | ".join(self._textos_de_la_ventana(aplicacion.raiz))
            self.assertIn(str(carpeta), dibujado)
        finally:
            aplicacion.conexion.close()
            aplicacion.raiz.destroy()
            shutil.rmtree(carpeta, ignore_errors=True)

    def test_las_lineas_del_arranque_quedan_guardadas(self):
        """Las mismas lineas que se imprimian siguen disponibles para mirarlas."""
        juntas = "\n".join(self.aplicacion.lineas_de_arranque)
        self.assertIn("Carpeta de datos:", juntas)
        self.assertIn("Motor SQLite:", juntas)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LasPantallasNoSeReconstruyenAlVolver(unittest.TestCase):
    """Cambiar de pantalla las esconde y las vuelve a enseñar; no las rehace.

    **Es el punto 4 del pase, y sale de una medición del supervisor**: destruir la
    pantalla de inicio son 186 `destroy` y 1,35 s en su perfil, y volver a crearla
    cuesta otro tanto. En Windows cada widget de Tk es una ventana del sistema, así
    que crearla y destruirla pasa por el escritorio; esconder y enseñar el mismo
    marco, no.

    Lo que se comprueba es la **identidad del objeto**: que al volver al inicio sea
    exactamente el mismo `PantallaDeInicio` de antes. Un objeto nuevo con el mismo
    aspecto es justo lo que este cambio existe para evitar, y desde fuera se ven
    igual.
    """

    def setUp(self):
        from interfaz.aplicacion import Aplicacion

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_pantallas_"))
        self.aplicacion = Aplicacion(carpeta_de_datos=self.carpeta, escribir=lambda _: None)
        self.aplicacion.raiz.withdraw()

    def tearDown(self):
        self.aplicacion.conexion.close()
        self.aplicacion.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def test_volver_al_inicio_devuelve_la_misma_pantalla(self):
        primera = self.aplicacion._pantalla
        self.aplicacion.mostrar_reportes()
        self.aplicacion.mostrar_inicio()
        self.assertIs(primera, self.aplicacion._pantalla)

    def test_la_pantalla_escondida_sigue_viva(self):
        """Escondida con `grid_remove`, no destruida: por eso se puede volver."""
        inicio = self.aplicacion._pantalla
        self.aplicacion.mostrar_reportes()
        self.assertTrue(
            inicio.winfo_exists(), "el inicio se destruyó al cambiar de pantalla"
        )
        self.assertFalse(
            inicio.winfo_ismapped(), "el inicio siguió dibujado debajo de reportes"
        )

    def test_revisar_y_reportes_tampoco_se_rehacen(self):
        for mostrar in (self.aplicacion.mostrar_revisar, self.aplicacion.mostrar_reportes):
            mostrar()
            primera = self.aplicacion._pantalla
            self.aplicacion.mostrar_inicio()
            mostrar()
            self.assertIs(primera, self.aplicacion._pantalla)

    def test_la_pantalla_que_no_se_guarda_acaba_destruida(self):
        """Esconder la que se va no puede convertirse en no destruirla nunca.

        Las que no se guardan —corrección, archivar, compañeros, asignación— se
        destruyen **después** de enseñar la nueva, para que sus `destroy` no estén
        entre el clic y la pantalla que se espera: medido con `cProfile`, destruir
        la pantalla de corrección eran 77 ms de los 283 que costaba volver al
        inicio. Pero se destruyen: dejarlas vivas escondidas sería una pantalla más
        en memoria por cada caso que se abre en la sesión.
        """
        companeros = None
        self.aplicacion.mostrar_companeros()
        companeros = self.aplicacion._pantalla
        self.aplicacion.mostrar_inicio()
        self.aplicacion.raiz.update()
        self.assertFalse(
            companeros.winfo_exists(),
            "la pantalla de compañeros se quedó viva después de salir de ella",
        )

    def test_al_volver_al_inicio_los_atajos_vuelven_a_responder(self):
        """Soltar los atajos al esconder no puede dejarlos sueltos para siempre.

        `F5` es el que relee la pantalla con la fecha de hoy. Si al volver del
        historial no responde, la pantalla se queda pintando el día de ayer, que es
        exactamente la avería silenciosa que el inicio existe para no tener.
        """
        raiz = self.aplicacion.raiz
        self.aplicacion.mostrar_reportes()
        self.aplicacion.mostrar_inicio()
        self.assertNotEqual("", raiz.bind("<F5>"))


if __name__ == "__main__":
    unittest.main()
