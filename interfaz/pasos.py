"""Los seis pasos que contesto el companero, vistos por Miguel. Solo de lectura.

**Que hace falta que se entienda de un vistazo, y por que.** Los seis pasos dicen
si una persona esta en condiciones de ir al templo; las seis ordenanzas de
`interfaz/casilla.py` dicen a que va. Son dos preguntas distintas sobre la misma
persona (`datos/pasos.py`), y en la pantalla van una debajo de otra. Si las dos se
pintaran igual, la que se leeria mal seria justo la que dice si esa persona puede
entrar — que es el dano que el programa entero existe para evitar. Por eso:

    ordenanza  ->  CUADRADO, y la palabra «sí» / «no» / «NO LEÍDA»
    paso       ->  CIRCULO,  y la palabra «Sí» / «No» / «sin contestar»

Formas distintas, palabras distintas, rotulo de seccion distinto. No es adorno: es
lo unico que separa las dos lecturas cuando se va con prisa.

**Los tres estados se dibujan a mano por la medicion de `interfaz/casilla.py`.**
Alli se conto que `tk.Checkbutton` en tri-estado pinta «no leida» y «marcada» con
**0 pixeles de diferencia**, contra 146 de ruido de la propia medicion. El tercer
estado de los pasos tiene el mismo problema y la misma consecuencia: «sin
contestar» confundido con «Sí» es una persona que viaja con una pregunta que nadie
miro. Un dibujo propio en un `tk.Canvas` no se lo puede cambiar una actualizacion
del sistema.

**Y no se puede tocar, a proposito.** Lo que sale aqui lo firmo un companero con
su nombre y su fecha, y el esquema guarda UNA propuesta por persona: dejar que
Miguel la editara desde aqui seria borrar el trabajo de otra persona sin decirselo
a nadie — el mismo hallazgo ALTO que cerro `datos/propuestas.py`. Que estos siete
valores se puedan corregir a mano, y quien firmaria la correccion, es una decision
del dueno y no se toma en un widget. Por eso `takefocus=0`: tampoco entra en el
recorrido del tabulador, que esta medido contra el objetivo de tres personas en
menos de un minuto.
"""

import tkinter as tk
from tkinter import ttk

from datos.pasos import COLUMNA_DE_LA_LLAMADA, PASOS, ROTULO_DE_LA_LLAMADA
from interfaz.tema import AMBAR_DE_FILA, FONDO, LETRA_NORMAL, TEXTO

LADO = 18
CONTESTADO_QUE_SI = 1
CONTESTADO_QUE_NO = 0
SIN_CONTESTAR = None

TITULO_DE_LA_SECCION = "Preparación para las ordenanzas"

# Las palabras se escriben SIEMPRE al lado del dibujo, y no solo cuando el dibujo
# no basta. Es lo mismo que hace la casilla de ordenanza y por el mismo motivo:
# una forma se puede confundir, una palabra escrita no.
PALABRA_DE_LA_RESPUESTA = {
    CONTESTADO_QUE_SI: "Sí",
    CONTESTADO_QUE_NO: "No",
}
PALABRA_SIN_CONTESTAR = "sin contestar"


class RespuestaDelPaso(ttk.Frame):
    """Un paso con su rotulo, su dibujo de tres estados y su palabra. No se edita."""

    def __init__(self, padre, rotulo, valor):
        super().__init__(padre)
        self.rotulo = rotulo
        self.valor = valor

        self.dibujo = tk.Canvas(
            self,
            width=LADO,
            height=LADO,
            highlightthickness=0,
            takefocus=0,
            background="#FFFFFF",
            borderwidth=0,
        )
        self.dibujo.grid(row=0, column=0, padx=(0, 8))
        ttk.Label(self, text=rotulo, font=LETRA_NORMAL).grid(row=0, column=1, sticky="w")
        ttk.Label(self, text=self.palabra(), font=LETRA_NORMAL).grid(
            row=0, column=2, padx=(10, 0), sticky="w"
        )
        self._pintar()

    def palabra(self):
        """La palabra que se escribe al lado del dibujo."""
        if self.valor is SIN_CONTESTAR:
            return PALABRA_SIN_CONTESTAR
        return PALABRA_DE_LA_RESPUESTA[self.valor]

    def _pintar(self):
        """Dibuja el circulo y, si hay respuesta, lo que va dentro."""
        self.dibujo.configure(
            background=AMBAR_DE_FILA if self.valor is SIN_CONTESTAR else "#FFFFFF"
        )
        self.dibujo.create_oval(2, 2, LADO - 2, LADO - 2, outline=TEXTO, width=1)
        if self.valor == CONTESTADO_QUE_SI:
            self._pintar_la_marca()
        elif self.valor == CONTESTADO_QUE_NO:
            self._pintar_el_aspa()

    def _pintar_la_marca(self):
        """La marca de «Sí». Va dentro del circulo y no toca el borde."""
        self.dibujo.create_line(5, 9, 8, 12, fill=TEXTO, width=2)
        self.dibujo.create_line(8, 12, 13, 6, fill=TEXTO, width=2)

    def _pintar_el_aspa(self):
        """El aspa de «No».

        Un aspa y no un circulo vacio: vacio es lo que se pinta cuando nadie
        contesto, y son dos cosas distintas. Un «No» es una respuesta que alguien
        dio, y se ve que alguien la dio.
        """
        self.dibujo.create_line(6, 6, LADO - 6, LADO - 6, fill=TEXTO, width=2)
        self.dibujo.create_line(LADO - 6, 6, 6, LADO - 6, fill=TEXTO, width=2)


class BloqueDeLosPasos(ttk.LabelFrame):
    """Los siete valores de la propuesta de un companero sobre una persona.

    `propuesta` es lo que devuelve `datos.propuestas.propuesta_vigente`: las siete
    respuestas, quien firmo y cuando. No se le pasa la conexion a proposito — este
    modulo dibuja y no consulta, y asi se puede probar con un diccionario.
    """

    def __init__(self, padre, propuesta):
        super().__init__(padre, text=TITULO_DE_LA_SECCION, padding=8)
        self.columnconfigure(0, weight=1)
        self.respuestas = {}

        # En una sola columna y de arriba abajo, en el orden de `datos/pasos.py`,
        # que es el orden de la pantalla del lider: el companero los copio asi y
        # Miguel los compara asi. En dos columnas habria que ir y venir para
        # cotejar el cuarto, y el cuarto es «Acciones requeridas».
        for fila, (nombre, rotulo) in enumerate(PASOS):
            self._anadir(nombre, rotulo, propuesta, fila)
        self._anadir(
            COLUMNA_DE_LA_LLAMADA, ROTULO_DE_LA_LLAMADA, propuesta, len(PASOS), pady=(8, 2)
        )

        ttk.Label(self, text=self._firma(propuesta), style="Secundario.TLabel").grid(
            row=len(PASOS) + 1, column=0, sticky="w", pady=(8, 0)
        )

    def _anadir(self, nombre, rotulo, propuesta, fila, pady=2):
        """Pinta una respuesta y la guarda por su nombre de columna."""
        respuesta = RespuestaDelPaso(self, rotulo, propuesta.get(nombre))
        respuesta.grid(row=fila, column=0, sticky="w", pady=pady)
        self.respuestas[nombre] = respuesta

    @staticmethod
    def _firma(propuesta):
        """Quien lo propuso y cuando.

        Un dato firmado sin la firma a la vista es un dato sin dueno: si dos
        companeros contestan cosas distintas sobre la misma persona, lo primero
        que Miguel necesita saber es de quien es lo que esta mirando. El nombre
        puede faltar —un companero borrado de la tabla deja la referencia sin
        fila—, y entonces se dice eso y no se deja el hueco en blanco.
        """
        quien = propuesta.get("nombre_del_companero") or "un compañero que ya no está"
        cuando = propuesta.get("propuesto_en")
        return f"Lo contestó {quien}" + (f", el {cuando}" if cuando else "")
