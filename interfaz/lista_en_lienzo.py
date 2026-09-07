"""Una lista de renglones dibujada en un solo lienzo, en vez de un control por fila.

**Por que existe.** Lo midio el supervisor en esta maquina, y gobierna el pase del
dibujado:

    186 widgets ttk creados y pintados ..... 0,87–1,40 s   destruirlos 0,32–0,68 s
    un solo Canvas con 186 textos y rects ... 0,52 s        destruirlo  0,01 s
    2000 llamadas Tk triviales ............. 0,006 s

Ni las consultas ni las llamadas a Tk cuestan nada: **lo que cuesta es mapear y
destruir cada widget**, porque en Windows cada widget de Tk es una ventana del
sistema. Una lista de ocho filas con tres etiquetas cada una son veinticuatro
ventanas del escritorio para enseñar ocho renglones de texto.

**Que hace y que no.** Dibuja renglones de una o dos lineas, con su fondo y su
color, y avisa cuando se pulsa uno. **No** desplaza —eso es de
`interfaz/desplazamiento.py`, que es el unico ayudante de rueda del programa— y
**no** da foco de teclado por fila: donde hace falta recorrer con el tabulador
sigue habiendo controles de verdad, porque un lienzo no se tabula.
"""

import tkinter as tk

from interfaz.tema import BORDE, LETRA_PEQUENA

# Lo que ocupa cada cosa, en pixeles. Sale de lo que median las filas de la
# pantalla de inicio: una etiqueta con `pady=3` y letra pequena da unos 18 px.
ALTO_DE_UNA_LINEA = 17
MARGEN_DE_LA_FILA = 3
SANGRIA = 8


class Renglon:
    """Lo que se dibuja en una fila: sus lineas, sus colores y a que caso lleva.

    `clave` es lo que se devuelve al pulsar. Va suelto y no como el objeto entero
    para que esta clase no tenga que saber nada de casos ni de compañeros: dibuja
    texto y avisa de un clic.
    """

    def __init__(self, lineas, clave=None, fondo="#FFFFFF", colores=None, con_borde=True):
        self.lineas = tuple(lineas)
        self.clave = clave
        self.fondo = fondo
        # Un color por linea; si falta, se hereda el ultimo que haya.
        self.colores = tuple(colores or ())
        self.con_borde = con_borde

    def color_de(self, indice):
        if not self.colores:
            return "#1A1D21"
        return self.colores[min(indice, len(self.colores) - 1)]

    @property
    def alto(self):
        return len(self.lineas) * ALTO_DE_UNA_LINEA + 2 * MARGEN_DE_LA_FILA


class ListaEnLienzo(tk.Canvas):
    """Los renglones que se le den, dibujados de una vez y pulsables por etiqueta.

    Se usa como cualquier control: se coloca con `grid` o `pack` y se le llama a
    `poner(...)` cada vez que cambian los datos.
    """

    def __init__(self, padre, al_pulsar=None, fondo="#F7F7F8", **opciones):
        super().__init__(
            padre, background=fondo, highlightthickness=0, takefocus=0, **opciones
        )
        self._al_pulsar = al_pulsar
        self._claves = {}
        self._renglones = ()
        self.bind("<Configure>", self._al_cambiar_de_ancho)

    def poner(self, renglones):
        """Sustituye lo dibujado por estos renglones y ajusta el alto que pide."""
        self._renglones = tuple(renglones)
        self._dibujar()

    @property
    def alto_que_pide(self):
        """Lo que ocuparia dibujado entero. Quien lo coloca decide si se lo da."""
        return sum(renglon.alto + 1 for renglon in self._renglones)

    def _al_cambiar_de_ancho(self, evento=None):
        self._dibujar()

    def _dibujar(self):
        """Borra y vuelve a pintar. Borrar un lienzo cuesta 0,01 s; 186 `destroy`, 0,5."""
        self.delete("all")
        self._claves = {}
        ancho = self.winfo_width()
        arriba = 0
        for numero, renglon in enumerate(self._renglones):
            self._dibujar_renglon(numero, renglon, arriba, ancho)
            arriba += renglon.alto + 1
        self.configure(height=max(arriba, 1))

    def _dibujar_renglon(self, numero, renglon, arriba, ancho):
        """Un renglon: su fondo, su borde y sus lineas de texto."""
        etiqueta = f"renglon:{numero}"
        if renglon.clave is not None:
            self._claves[etiqueta] = renglon.clave
        self.create_rectangle(
            0, arriba, max(ancho - 1, 1), arriba + renglon.alto,
            fill=renglon.fondo,
            outline=BORDE if renglon.con_borde else renglon.fondo,
            tags=(etiqueta,),
        )
        for indice, linea in enumerate(renglon.lineas):
            self.create_text(
                SANGRIA,
                arriba + MARGEN_DE_LA_FILA + indice * ALTO_DE_UNA_LINEA
                + ALTO_DE_UNA_LINEA // 2,
                anchor="w", text=linea, fill=renglon.color_de(indice),
                font=LETRA_PEQUENA, tags=(etiqueta,),
            )
        if renglon.clave is not None and self._al_pulsar is not None:
            self.tag_bind(
                etiqueta, "<Button-1>",
                lambda evento, e=etiqueta: self._pulsar(e),
            )

    def _pulsar(self, etiqueta):
        clave = self._claves.get(etiqueta)
        if clave is not None:
            self._al_pulsar(clave)
