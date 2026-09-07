"""Los cuadros de diálogo salen en español, botones incluidos.

**El defecto, medido por QA en la máquina del dueño el 2026-09-03:**

    (Get-UICulture) = en-US
    msgcat::mc "Yes" -> Yes    (con locale c, en, es y es_es)

Comprobado otra vez aquí antes de tocar nada, Tk 9.0.4:

    locale c      -> ['Yes', 'No', 'OK', 'Cancel']
    locale en     -> ['Yes', 'No', 'OK', 'Cancel']
    locale es     -> ['Yes', 'No', 'OK', 'Cancel']
    locale es_es  -> ['Yes', 'No', 'OK', 'Cancel']

Tk 9.0.4 **no trae catálogo español**, y en Windows `tkinter.messagebox` ni
siquiera dibuja el diálogo: llama al nativo del sistema, que se rotula en el idioma
de la interfaz de Windows. Eran 51 puntos de llamada con el mensaje en español y
los botones en inglés. Un «¿Sigue?» rematado con **Yes/No** es justo el que se
pulsa sin leer, y choca con la regla permanente 4.

El programa no controla ese texto, así que la única salida es un diálogo propio:
`interfaz/dialogos.py`, dibujado con `ttk` sobre un `Toplevel`, donde el rótulo de
cada botón lo escribe este proyecto.

**Lo que estas pruebas fijan** sale del criterio 4 del pase —«los diálogos salen en
español, botones incluidos»— y no del código:

  1. ningún botón de ningún diálogo lleva una de las cuatro palabras inglesas;
  2. «Sí» devuelve cierto, «No» devuelve falso, y Esc devuelve falso;
  3. ningún módulo de `interfaz/` vuelve a importar `tkinter.messagebox`. Esta
     tercera es la que impide que el defecto vuelva por un punto de llamada nuevo,
     que es como volvería.
"""

import ast
import unittest
from pathlib import Path

CARPETA_DE_LA_INTERFAZ = Path(__file__).resolve().parent.parent / "interfaz"

# Las cuatro palabras que Tk pone en los botones y que el programa no controla.
PALABRAS_QUE_NO_PUEDEN_SALIR = ("Yes", "No", "OK", "Cancel")

# El módulo que no se puede volver a importar en la interfaz, y el único sitio
# donde sí se puede: el diálogo propio no lo usa, pero si algún día hiciera falta
# un cuadro nativo para algo, saldría de ahí y de ningún otro archivo.
MODULO_PROHIBIDO = "messagebox"


def _hay_ventanas():
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


class NingunModuloDeLaInterfazUsaElCuadroNativo(unittest.TestCase):
    """La puerta que impide que el defecto vuelva por un punto de llamada nuevo.

    Se lee el árbol de sintaxis y no el texto: un `grep` de «messagebox» también
    casaría con la palabra dentro de un comentario o de una cadena, y entonces la
    prueba se pondría roja por un texto que explica el defecto —como los de este
    mismo archivo— en vez de por una llamada de verdad.
    """

    def _importaciones_de(self, ruta):
        arbol = ast.parse(ruta.read_text(encoding="utf-8"))
        nombres = []
        for nodo in ast.walk(arbol):
            if isinstance(nodo, ast.ImportFrom):
                nombres.extend(alias.name for alias in nodo.names)
                if nodo.module:
                    nombres.append(nodo.module)
            elif isinstance(nodo, ast.Import):
                nombres.extend(alias.name for alias in nodo.names)
        return nombres

    def test_ningun_archivo_de_interfaz_importa_messagebox(self):
        culpables = [
            ruta.name
            for ruta in sorted(CARPETA_DE_LA_INTERFAZ.glob("*.py"))
            if any(
                MODULO_PROHIBIDO in nombre.split(".")
                for nombre in self._importaciones_de(ruta)
            )
        ]
        self.assertEqual(
            culpables,
            [],
            "Estos módulos vuelven a usar el cuadro nativo de Windows, que se "
            "rotula en inglés: " + ", ".join(culpables),
        )

    def test_el_modulo_de_dialogos_existe_y_ofrece_las_cuatro(self):
        """Las cuatro que sustituyen a las cuatro de `messagebox`."""
        from interfaz import dialogos

        for nombre in ("preguntar_si_o_no", "informar", "advertir", "avisar_de_un_error"):
            self.assertTrue(
                callable(getattr(dialogos, nombre, None)),
                f"Falta «{nombre}» en interfaz/dialogos.py",
            )


class LasPalabrasDeLosBotonesEstanEnEspanol(unittest.TestCase):
    """Los rótulos son constantes del proyecto, así que se leen sin abrir ventana."""

    def test_ninguna_de_las_cuatro_palabras_inglesas_sale_en_un_boton(self):
        from interfaz.dialogos import ROTULOS_DE_LOS_BOTONES

        for rotulo in ROTULOS_DE_LOS_BOTONES:
            self.assertNotIn(rotulo, PALABRAS_QUE_NO_PUEDEN_SALIR)

    def test_la_pregunta_ofrece_si_y_no_escritos_en_espanol(self):
        from interfaz.dialogos import ROTULO_DEL_NO, ROTULO_DEL_SI

        self.assertEqual(ROTULO_DEL_SI, "Sí")
        self.assertEqual(ROTULO_DEL_NO, "No, cancelar")


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElDialogoDibujadoResponde(unittest.TestCase):
    """Que lo dibujado son los rótulos del proyecto, y que contestan lo que dicen.

    No se pulsa con el ratón: se llama al mismo método que el botón tiene atado.
    Automatizar un clic sobre coordenadas mediría dónde está el botón, que no es lo
    que este criterio pregunta.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)

    def tearDown(self):
        self.raiz.destroy()

    def _abrir(self, **argumentos):
        from interfaz.dialogos import CuadroDeDialogo

        cuadro = CuadroDeDialogo(self.raiz, **argumentos)
        self.raiz.update()
        return cuadro

    def test_los_botones_dibujados_llevan_texto_espanol(self):
        cuadro = self._abrir(
            titulo="Todo correcto", mensaje="¿Sigue?", con_pregunta=True
        )
        try:
            textos = cuadro.textos_de_los_botones()
            self.assertEqual(textos, ["Sí", "No, cancelar"])
        finally:
            cuadro.cerrar(False)

    def test_el_cuadro_de_aviso_lleva_un_solo_boton_y_en_espanol(self):
        cuadro = self._abrir(titulo="Caso verificado", mensaje="Listo.")
        try:
            self.assertEqual(cuadro.textos_de_los_botones(), ["Entendido"])
        finally:
            cuadro.cerrar(None)

    def test_si_devuelve_cierto_y_no_devuelve_falso(self):
        cuadro = self._abrir(titulo="t", mensaje="m", con_pregunta=True)
        cuadro.cerrar(True)
        self.assertIs(cuadro.respuesta, True)

        otro = self._abrir(titulo="t", mensaje="m", con_pregunta=True)
        otro.cerrar(False)
        self.assertIs(otro.respuesta, False)

    def test_escape_cuenta_como_no_y_nunca_como_si(self):
        """Cerrar sin contestar no puede valer por «sí»: es la tecla del reflejo."""
        cuadro = self._abrir(titulo="t", mensaje="m", con_pregunta=True)
        cuadro.al_escapar()
        self.assertIs(cuadro.respuesta, False)

    def test_el_titulo_del_cuadro_es_el_que_se_pidio(self):
        cuadro = self._abrir(titulo="Hay cambios sin guardar", mensaje="m")
        try:
            self.assertEqual(cuadro.title(), "Hay cambios sin guardar")
        finally:
            cuadro.cerrar(None)
