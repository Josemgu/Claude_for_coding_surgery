"""La casilla de tres estados, dibujada a mano porque las de Tk no sirven.

**Por que no se usa `tk.Checkbutton` con `tristatevalue`.** El disenador tomo
capturas reales en esta maquina (Tk 9.0.4, Windows 11) y conto los pixeles que
cambian entre un estado y otro, con un control que compara un estado consigo
mismo para saber cuanto ruido tiene la medicion:

    control (el mismo estado dos veces) ....... 146 px de ruido
    marcada vs no marcada ..................... 185 px
    no leida vs no marcada .................... 185 px
    no leida vs marcada .......................   0 px   <-- INSERVIBLE

Cero pixeles de diferencia entre «no leida» y «marcada», contra 146 de ruido. Y
en la direccion peligrosa: un dato que el sistema NUNCA leyo aparece en pantalla
como una ordenanza confirmada. Es exactamente la confusion que el tercer estado
existe para impedir.

`ttk.Checkbutton` con el estado `alternate` si distingue, pero por poco margen y
dependiendo del motor de temas de Windows. Aqui no se depende de eso: las tres
formas se pintan en un `tk.Canvas` de 18x18 propio, y al lado va la palabra
escrita. Un dibujo propio no se lo puede cambiar una actualizacion del sistema.

**La barra espaciadora solo alterna entre marcada y no marcada.** No vuelve a «no
leida»: ese estado se puede abandonar pero no se alcanza con un resbalon del
pulgar, porque volver a el borraria sin aviso algo que una persona ya miro. Para
devolverlo hay `Ctrl+Espacio`, y pregunta antes.
"""

import tkinter as tk
from tkinter import ttk

from interfaz import dialogos
from interfaz.tema import AMBAR_DE_FILA, ANILLO_DE_FOCO, FONDO, LETRA_NORMAL, TEXTO

LADO = 18
MARCADA = 1
NO_MARCADA = 0
NO_LEIDA = None

PALABRA_DE_LA_CASILLA = {
    MARCADA: "sí",
    NO_MARCADA: "no",
}
PALABRA_NO_LEIDA = "NO LEÍDA"


class CasillaDeOrdenanza(ttk.Frame):
    """Una casilla de ordenanza con sus tres estados y su palabra al lado.

    `al_cambiar` se llama con el valor nuevo cada vez que cambia, para que el pie
    de la pantalla pueda recontar cuantas quedan sin resolver sin preguntarle a
    cada casilla.
    """

    def __init__(self, padre, etiqueta, valor=NO_LEIDA, al_cambiar=None):
        super().__init__(padre)
        self.etiqueta = etiqueta
        self.valor = valor
        # Lo que traia la base al dibujarla. Es la mitad de `cambio()`, y `cambio()`
        # es lo unico que distingue una casilla que una persona resolvio de una que
        # nadie toco: las dos valen 0 en la columna. La diferencia se guarda como
        # `origen='manual'` en `procedencia_campo`, y sin esta memoria no habria de
        # donde sacarla.
        self.valor_inicial = valor
        self._al_cambiar = al_cambiar

        self._dibujo = tk.Canvas(
            self,
            width=LADO,
            height=LADO,
            highlightthickness=2,
            highlightbackground=FONDO,
            highlightcolor=ANILLO_DE_FOCO,
            takefocus=1,
            background="#FFFFFF",
            borderwidth=0,
        )
        self._dibujo.grid(row=0, column=0, padx=(0, 8))
        self._texto = ttk.Label(self, text=etiqueta, font=LETRA_NORMAL)
        self._texto.grid(row=0, column=1, sticky="w")
        self._estado = ttk.Label(self, font=LETRA_NORMAL)
        self._estado.grid(row=0, column=2, padx=(10, 0), sticky="w")

        self._dibujo.bind("<space>", self._alternar)
        self._dibujo.bind("<Control-space>", self._devolver_a_no_leida)
        self._dibujo.bind("<Button-1>", self._alternar)
        self._texto.bind("<Button-1>", self._alternar)
        self._dibujo.bind("<FocusIn>", lambda evento: self._repintar())
        self._dibujo.bind("<FocusOut>", lambda evento: self._repintar())
        self._repintar()

    def _alternar(self, evento=None):
        """Marcada <-> no marcada. Desde «no leida» entra por «marcada»."""
        self._dibujo.focus_set()
        self.fijar(NO_MARCADA if self.valor == MARCADA else MARCADA)
        return "break"

    def _devolver_a_no_leida(self, evento=None):
        """Vuelve a «no leida», preguntando antes. Es una marcha atras rara."""
        self._dibujo.focus_set()
        if self.valor is NO_LEIDA:
            return "break"
        if dialogos.preguntar_si_o_no(
            "Devolver a «no leída»",
            f"¿Devolver «{self.etiqueta}» al estado «no leída»?\n\n"
            "Eso borra lo que ya se había mirado en esta casilla y vuelve a "
            "dejarla como si nadie la hubiera visto. El caso no se podrá "
            "verificar mientras siga así.",
            parent=self,
        ):
            self.fijar(NO_LEIDA)
        return "break"

    def fijar(self, valor):
        """Pone la casilla en un valor concreto y avisa a quien la escucha."""
        self.valor = valor
        self._repintar()
        if self._al_cambiar is not None:
            self._al_cambiar(valor)

    def esta_sin_leer(self):
        """Si sigue en «no leida». Se pregunta asi y no comparando con `None` para
        que quien resuelve un formulario entero no tenga que saber que «no leida»
        se representa con `None`."""
        return self.valor is NO_LEIDA

    def cambio(self):
        """Si el valor de ahora no es el que se cargo de la base.

        `None` —«no leida»— se resuelve aparte de 0 y 1 y no se mete en el `int()`:
        es un estado, no un numero, e `int(None)` levanta. La comparacion de
        estados va con `is`, la de valores con `int()`.

        ⚠️ Y de lo que sale de aqui depende algo mas grande de lo que parece: «no
        leida» resuelta como «no marcada» y «leida sin marca» acaban las dos como
        un `0` en la misma columna. Lo unico que las separa despues es que este
        metodo haya visto el cambio, porque es lo que hace que se escriba la fila
        de procedencia con `origen='manual'`.
        """
        if self.valor is NO_LEIDA or self.valor_inicial is NO_LEIDA:
            return (self.valor is NO_LEIDA) != (self.valor_inicial is NO_LEIDA)
        return int(self.valor) != int(self.valor_inicial)

    def _repintar(self):
        """Vuelve a dibujar la caja y a escribir la palabra que le toca."""
        self._dibujo.delete("all")
        fondo = AMBAR_DE_FILA if self.valor is NO_LEIDA else "#FFFFFF"
        self._dibujo.configure(background=fondo)
        self._dibujo.create_rectangle(2, 2, LADO - 2, LADO - 2, outline=TEXTO, width=1)

        if self.valor is NO_LEIDA:
            self._dibujar_rayado()
            self._estado.configure(text=PALABRA_NO_LEIDA)
        else:
            if self.valor == MARCADA:
                self._dibujar_marca()
            self._estado.configure(text=PALABRA_DE_LA_CASILLA[self.valor])

    def _dibujar_rayado(self):
        """Las diagonales que dicen «aquí no se leyó nada».

        Un rayado y no un guion ni un cuadrado relleno: relleno se confunde con
        marcada, que es el error concreto que se midio en la casilla nativa.
        """
        for desplazamiento in range(-LADO, LADO, 4):
            self._dibujo.create_line(
                max(2, desplazamiento),
                2,
                min(LADO - 2, desplazamiento + LADO - 4),
                LADO - 2,
                fill=TEXTO,
                width=1,
            )

    def _dibujar_marca(self):
        """La marca de verificacion de una casilla marcada."""
        self._dibujo.create_line(4, 9, 8, 13, fill=TEXTO, width=2)
        self._dibujo.create_line(8, 13, 14, 5, fill=TEXTO, width=2)

    def widget_que_toma_el_foco(self):
        """El control por el que Tab entra en esta casilla."""
        return self._dibujo
