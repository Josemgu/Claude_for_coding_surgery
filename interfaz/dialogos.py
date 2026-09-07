"""Los cuadros de diálogo del programa, con los botones rotulados en español.

**Por qué existe este archivo, y por qué no bastaba con configurar Tk.** Medido en
la máquina del dueño el 2026-09-03 (Windows 11, Tk 9.0.4):

    (Get-UICulture)                                   en-US
    locale c      -> ['Yes', 'No', 'OK', 'Cancel']
    locale en     -> ['Yes', 'No', 'OK', 'Cancel']
    locale es     -> ['Yes', 'No', 'OK', 'Cancel']
    locale es_es  -> ['Yes', 'No', 'OK', 'Cancel']

Tk 9.0.4 **no trae catálogo de mensajes en español**: pedirle el locale español
devuelve las mismas cuatro palabras inglesas. Y en Windows hay una segunda capa que
lo hace irreversible: `tkinter.messagebox` no dibuja el cuadro, llama al diálogo
**nativo** del sistema, que se rotula con el idioma de la interfaz de Windows y no
con nada que el programa pueda cambiar.

Eran 51 puntos de llamada con el mensaje escrito en español y los botones en
inglés. Que un «¿Sigue?» delante de una firma irreversible se remate con **Yes/No**
no es un detalle de traducción: es justo el diálogo que se despacha sin leer,
porque el ojo reconoce la forma del botón y no el texto. Y choca con la regla
permanente 4, que nombra los textos de interfaz por su nombre.

La única salida es dibujar el cuadro aquí. Lo que se gana además de los rótulos:

  - **El botón que sigue adelante nunca es el que tiene el foco.** En un cuadro
    nativo el foco entra en el botón por defecto, y con Intro pulsado por inercia
    se confirma lo que no se leyó. Aquí entra en el de cancelar.
  - **Esc y la cruz de la ventana valen «no».** Nunca «sí». Cerrar un cuadro sin
    contestar no puede significar seguir adelante.
  - El mensaje se puede seleccionar y copiar, que en un error hace falta.

**Lo que este módulo NO hace, dicho:** no sustituye a `filedialog` —el cuadro de
elegir archivo sigue siendo el nativo de Windows, y ese sí sale en el idioma del
sistema—. Cambiarlo pedía escribir un explorador de archivos entero, que es mucho
más código del que arregla, y sus botones no confirman nada irreversible.
"""

import tkinter as tk
from tkinter import ttk

from interfaz.tema import (
    FONDO,
    LETRA_NORMAL,
    LETRA_SECCION,
    ROJO_SOLIDO,
    TEXTO,
)

# Los rótulos, en un solo sitio. `pruebas/prueba_dialogos_en_espanol.py` los lee de
# aquí y comprueba que ninguno es una de las cuatro palabras que pone Tk.
#
# «No, cancelar» y no «No» a secas: la palabra «No» sola es idéntica en los dos
# idiomas, así que un botón rotulado «No» no demuestra nada —ni a quien lo mira ni
# a la prueba— sobre si el cuadro es el nuestro o el de Windows.
ROTULO_DEL_SI = "Sí"
ROTULO_DEL_NO = "No, cancelar"
ROTULO_DE_ENTERADO = "Entendido"

ROTULOS_DE_LOS_BOTONES = (ROTULO_DEL_SI, ROTULO_DEL_NO, ROTULO_DE_ENTERADO)

# Cuánto texto cabe en una línea antes de partir, en píxeles. Los mensajes de este
# programa son párrafos —el de «Todo correcto» pasa de 300 caracteres— y sin esto
# el cuadro se estira hasta salirse de la pantalla.
ANCHO_DEL_MENSAJE = 520

# Los tres tonos del rótulo de arriba. Solo cambia el color del título: la forma
# del cuadro es la misma siempre, para que no haya que aprenderse tres cuadros.
TONO_NORMAL = "normal"
TONO_AVISO = "aviso"
TONO_ERROR = "error"

COLOR_DEL_TITULO = {
    TONO_NORMAL: TEXTO,
    TONO_AVISO: "#8A5B00",
    TONO_ERROR: ROJO_SOLIDO,
}


class CuadroDeDialogo(tk.Toplevel):
    """Una ventana modal con un título, un mensaje y uno o dos botones.

    Con `con_pregunta=True` trae «Sí» y «No, cancelar» y `respuesta` acaba en un
    booleano. Sin ella trae un solo «Entendido» y `respuesta` se queda en None.

    Se construye y se muestra en el mismo acto: quien la abre llama a
    `esperar_respuesta()`, que no vuelve hasta que se cierra.
    """

    def __init__(self, padre, titulo, mensaje, con_pregunta=False, tono=TONO_NORMAL):
        super().__init__(padre)
        self.respuesta = None
        self._con_pregunta = con_pregunta
        self.title(titulo)
        self.configure(background=FONDO)
        self.resizable(False, False)
        # `transient` mantiene el cuadro por encima de su ventana y lo minimiza con
        # ella. Sin esto un cuadro modal puede quedar detrás de la ventana que lo
        # abrió, y entonces el programa parece colgado: no responde y no se ve por
        # qué.
        self.transient(padre)

        cuerpo = ttk.Frame(self, padding=18)
        cuerpo.grid(sticky="nsew")
        ttk.Label(
            cuerpo, text=titulo, font=LETRA_SECCION, foreground=COLOR_DEL_TITULO[tono]
        ).grid(row=0, column=0, sticky="w", pady=(0, 8))
        ttk.Label(
            cuerpo,
            text=mensaje,
            font=LETRA_NORMAL,
            wraplength=ANCHO_DEL_MENSAJE,
            justify="left",
        ).grid(row=1, column=0, sticky="w")

        self._botones = self._construir_botones(cuerpo)
        self._atar_teclas()
        self._colocar_sobre_el_padre(padre)

    # ---- construccion ----------------------------------------------------

    def _construir_botones(self, cuerpo):
        """Los uno o dos botones del pie, y a cuál va el foco.

        ⚠️ **El foco entra en el botón que NO sigue adelante**, y es lo contrario de
        lo que hace el cuadro nativo. El motivo es el mismo por el que el diálogo de
        «Todo correcto» lleva el número delante: es una firma que no se puede
        deshacer campo por campo, y un Intro pulsado por inercia no puede firmarla.
        Quien quiere seguir tiene que llegar al botón, con Tab o con el ratón.
        """
        fila = ttk.Frame(cuerpo)
        fila.grid(row=2, column=0, sticky="e", pady=(16, 0))
        botones = []
        if self._con_pregunta:
            botones.append(
                ttk.Button(fila, text=ROTULO_DEL_SI, command=lambda: self.cerrar(True))
            )
            botones.append(
                ttk.Button(fila, text=ROTULO_DEL_NO, command=lambda: self.cerrar(False))
            )
        else:
            botones.append(
                ttk.Button(
                    fila, text=ROTULO_DE_ENTERADO, command=lambda: self.cerrar(None)
                )
            )
        for columna, boton in enumerate(botones):
            boton.grid(row=0, column=columna, padx=(8, 0))
        botones[-1].focus_set()
        return botones

    def _atar_teclas(self):
        """Esc y la cruz de la ventana cierran, y las dos cuentan como «no».

        `WM_DELETE_WINDOW` se ata a lo mismo que Esc: si no se ata, la cruz de la
        ventana destruye el cuadro sin soltar el `grab` y el programa se queda sin
        poder recibir un clic en ninguna parte.
        """
        self.bind("<Escape>", lambda evento: self.al_escapar())
        self.protocol("WM_DELETE_WINDOW", self.al_escapar)

    def _colocar_sobre_el_padre(self, padre):
        """Centra el cuadro sobre la ventana que lo abrió, no sobre la pantalla.

        Con dos monitores, centrar en «la pantalla» lo manda al monitor primario
        mientras el programa está en el otro, y el cuadro modal aparece donde nadie
        está mirando.
        """
        self.update_idletasks()
        try:
            ventana = padre.winfo_toplevel()
            x = ventana.winfo_rootx() + (ventana.winfo_width() - self.winfo_width()) // 2
            y = ventana.winfo_rooty() + (ventana.winfo_height() - self.winfo_height()) // 3
        except tk.TclError:  # pragma: no cover - solo si el padre ya no existe
            return
        self.geometry(f"+{max(x, 0)}+{max(y, 0)}")

    # ---- respuesta -------------------------------------------------------

    def textos_de_los_botones(self):
        """Lo que de verdad está escrito en los botones dibujados.

        Existe para que la prueba mida el rótulo pintado y no la constante: una
        constante en español y un botón que se rotula solo seguirían siendo el
        defecto que este módulo cierra.
        """
        return [str(boton.cget("text")) for boton in self._botones]

    def al_escapar(self):
        """Esc y la cruz. En una pregunta valen «no»; en un aviso, cerrar."""
        self.cerrar(False if self._con_pregunta else None)

    def cerrar(self, respuesta):
        """Guarda la respuesta, suelta el bloqueo y destruye el cuadro."""
        self.respuesta = respuesta
        try:
            self.grab_release()
        except tk.TclError:  # pragma: no cover - si nunca llego a agarrarlo
            pass
        self.destroy()

    def esperar_respuesta(self):
        """Bloquea hasta que se cierre, y devuelve lo que se contestó.

        `grab_set` es lo que lo hace modal de verdad: sin él se puede seguir
        pulsando en la ventana de detrás con el cuadro abierto, y se llega a tener
        dos cuadros del mismo botón encima.
        """
        try:
            self.grab_set()
        except tk.TclError:  # pragma: no cover - depende del gestor de ventanas
            pass
        self.wait_window(self)
        return self.respuesta


def _mostrar(padre, titulo, mensaje, con_pregunta, tono):
    """El camino único por el que sale cualquier cuadro de este programa."""
    cuadro = CuadroDeDialogo(padre, titulo, mensaje, con_pregunta=con_pregunta, tono=tono)
    return cuadro.esperar_respuesta()


def preguntar_si_o_no(titulo, mensaje, parent=None):
    """Una pregunta de sí o no. Devuelve cierto solo si se pulsó «Sí».

    `parent` conserva el nombre inglés a propósito y es el único de este módulo:
    es el que ya traían los 51 puntos de llamada de `messagebox`, y cambiarlo
    obligaba a tocar los 51 sin que ninguna palabra la lea nadie en pantalla. La
    regla permanente 4 habla de lo que se ve y de lo que se lee en el código; este
    nombre es el precio de que la sustitución sea de una línea por archivo.
    """
    return bool(_mostrar(parent, titulo, mensaje, True, TONO_NORMAL))


def informar(titulo, mensaje, parent=None):
    """Un aviso que solo hay que leer. Un botón: «Entendido»."""
    _mostrar(parent, titulo, mensaje, False, TONO_NORMAL)


def advertir(titulo, mensaje, parent=None):
    """Lo mismo, con el título en ámbar: algo salió a medias y hay que mirarlo."""
    _mostrar(parent, titulo, mensaje, False, TONO_AVISO)


def avisar_de_un_error(titulo, mensaje, parent=None):
    """Lo mismo, con el título en rojo: algo no se pudo hacer."""
    _mostrar(parent, titulo, mensaje, False, TONO_ERROR)
