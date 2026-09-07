"""La línea del pie que dice que se guardó, y qué se quedó sin guardar.

**Lo que dijo el dueño el 2026-09-04, literal:** «El botón Guardar no funciona
cuando se revisa y se coloca la información en un lateral faltante: no hace nada».

Medido sobre una importación real antes de escribir esto: con un MRN bueno el
guardado **sí ocurría** —la fila de la base cambiaba— y en la pantalla no había
una sola palabra que lo dijera. Un botón que hace su trabajo en silencio es, para
quien lo mira, un botón roto; y lo siguiente que hace quien lo mira es pulsarlo
otra vez, o dar por perdido lo tecleado.

Tres decisiones que gobiernan este archivo:

  - **La línea existe desde que se construye la pantalla, vacía.** No se crea al
    guardar y no se destruye al borrarse: así el pie mide siempre lo mismo y el
    botón **no cambia de sitio** al pulsarlo. Un botón que se mueve bajo el dedo
    se falla al repetir.
  - **Se borra sola a los pocos segundos.** «Guardado a las 09:41» diez minutos
    después, con tres campos tecleados encima sin guardar, es una mentira que
    además se cree.
  - **El color cambia; la letra y el alto, no.** Lo que avisa de que algo se quedó
    fuera es el tono, no un tamaño distinto, porque un texto más alto movería el
    botón —que es lo que este archivo existe para impedir—.
"""

import tkinter as tk
from datetime import datetime
from tkinter import ttk

from interfaz.tema import COLORES, NO_VALIDO, TEXTO_SECUNDARIO

# Cuánto se ve el acuse antes de borrarse solo. Ocho segundos son de sobra para
# leer siete palabras y poco para que la frase siga en pantalla cuando ya no es
# verdad. No se mide: es un juicio, y va escrito para que se pueda discutir.
SEGUNDOS_QUE_SE_VE_EL_ACUSE = 8

# Dónde parte la línea. Es el mismo corte que el motivo del bloqueo, que vive
# justo encima: así el pie no se puede ensanchar por esta línea y empujar los
# botones hacia la derecha.
ANCHO_DEL_ACUSE = 600


def hora_de_ahora():
    """La hora del reloj de quien mira la pantalla, en «HH:MM».

    Hora local y no UTC a propósito: el acuse existe para que Miguel compare con
    el reloj de su barra de tareas.
    """
    return datetime.now().strftime("%H:%M")


def texto_del_acuse(hora, campos_guardados, campos_sin_guardar):
    """Lo que dice el pie después de guardar. Sin ventana, para poder leerlo.

    `campos_sin_guardar` son las etiquetas —«MRN», «N.º de unidad»— de los campos
    que no valían y por eso no se escribieron. Cuando hay alguno, el acuse **los
    nombra y calla la hora**: lo que hay que mirar entonces no es cuándo se guardó
    sino qué falta, y dos datos en la misma línea hacen que no se lea ninguno.
    """
    if campos_sin_guardar:
        cuantos = len(campos_sin_guardar)
        plural = "s" if cuantos != 1 else ""
        return (
            f"Guardado · {cuantos} campo{plural} sin guardar: "
            + ", ".join(campos_sin_guardar)
        )
    if not campos_guardados:
        return f"Guardado a las {hora} · sin cambios"
    plural = "s" if campos_guardados != 1 else ""
    return f"Guardado a las {hora} · {campos_guardados} campo{plural}"


class AcuseDeGuardado(ttk.Label):
    """La línea del pie. Se construye vacía y ocupa su sitio desde el principio."""

    def __init__(self, padre, segundos=SEGUNDOS_QUE_SE_VE_EL_ACUSE):
        super().__init__(
            padre, text="", style="Secundario.TLabel", wraplength=ANCHO_DEL_ACUSE
        )
        self._segundos = segundos
        self._borrado = None
        # Cancelar al destruirse, o Tk contesta «invalid command name» al dispararse
        # el temporizador sobre una pantalla que ya no existe. Ya pasó una vez en
        # este proyecto y salía por consola durante la suite.
        self.bind("<Destroy>", self._al_destruirse, add="+")

    def anunciar(self, campos_guardados, campos_sin_guardar=()):
        """Dice que se guardó, y programa su propio borrado."""
        campos_sin_guardar = tuple(campos_sin_guardar)
        self.configure(
            text=texto_del_acuse(hora_de_ahora(), campos_guardados, campos_sin_guardar),
            foreground=COLORES[NO_VALIDO][1] if campos_sin_guardar else TEXTO_SECUNDARIO,
        )
        self._programar_el_borrado()

    def texto(self):
        """Lo que dice ahora mismo. Para poder comprobarlo sin leer el widget."""
        return self.cget("text")

    def limpiar(self, evento=None):
        """Deja la línea en blanco sin mover nada de sitio."""
        self._cancelar_el_borrado()
        self.configure(text="")

    def _programar_el_borrado(self):
        self._cancelar_el_borrado()
        self._borrado = self.after(self._segundos * 1000, self.limpiar)

    def _cancelar_el_borrado(self):
        if self._borrado is None:
            return
        try:
            self.after_cancel(self._borrado)
        except tk.TclError:
            # El intérprete ya se estaba cerrando: no queda cola que tocar. Es el
            # mismo caso —y el mismo motivo— que `PantallaDeCorreccion._al_destruirse`.
            pass
        self._borrado = None

    def _al_destruirse(self, evento=None):
        if evento is not None and evento.widget is not self:
            return
        self._cancelar_el_borrado()
