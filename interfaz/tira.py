"""La tira del escaneo que va al lado de cada campo.

Es la mitad de la pantalla de correccion: sin ella hay que buscar el dato en la
pagina completa, y corregir un formulario de tres personas en menos de un minuto
deja de ser posible.

**No usa Pillow.** Medido en esta maquina (Tk 9.0.4): `tkinter.PhotoImage(data=...)`
acepta bytes CRUDOS en formato PGM y construye la imagen; los mismos bytes en
base64 fallan con «data stream does not have a PNG signature». Asi que la tira
viaja como PGM binario desde `extraccion/recorte.py` y no hace falta anadir
ninguna biblioteca de imagenes al ejecutable.

**La referencia a la imagen se guarda en el objeto.** Es la trampa clasica de Tk:
`PhotoImage` no queda referenciada por el widget que la dibuja, asi que si la
variable local muere el recolector se la lleva y la tira aparece en blanco. Por
eso `self._imagen` existe, y no es una variable de mas.

**Cuando NO hay banda se dibuja un rectangulo rayado con su motivo escrito**, no
una tira en blanco. Una tira en blanco parece un escaneo vacio —«el papel no
tenia nada ahi»— y no lo es: significa que el OCR no encontro la etiqueta y no se
sabe donde mirar. Son dos cosas distintas y en pantalla se ven distintas.
"""

import tkinter as tk

from extraccion.recorte import ALTO_DE_LA_TIRA_PX, ANCHO_DE_LA_TIRA_PX
from interfaz.tema import BORDE, LETRA_PEQUENA, TEXTO_SECUNDARIO

# El ancho del hueco sale del MISMO sitio que el tope con el que se escala la
# imagen (`extraccion/recorte.py`). Tenerlo escrito aqui aparte fue el fallo de la
# FASE 9: el hueco media 380 px, la imagen salia de 610, y el ultimo digito de un
# numero de unidad no habia forma de comprobarlo.
ANCHO_VISIBLE_POR_DEFECTO = ANCHO_DE_LA_TIRA_PX
SIN_BANDA = "el lector no encontró esta fila en el escaneo"


class TiraDelEscaneo(tk.Canvas):
    """El trozo del escaneo de un campo, o el hueco rayado si no lo hay.

    Nunca toma el foco (`takefocus=0`): Tab tiene que ir de campo editable a campo
    editable, y una tira es para mirar, no para teclear. Con seis personas, que Tab
    pasara por cada tira serian catorce pulsaciones de mas por formulario.
    """

    def __init__(self, padre, ancho=ANCHO_VISIBLE_POR_DEFECTO, alto=ALTO_DE_LA_TIRA_PX):
        super().__init__(
            padre,
            width=ancho,
            height=alto,
            highlightthickness=1,
            highlightbackground=BORDE,
            background="#FFFFFF",
            takefocus=0,
            borderwidth=0,
        )
        self._imagen = None
        self._ancho_visible = ancho
        self._alto = alto
        # Arrastrar con el raton mueve la tira cuando es mas ancha que su hueco.
        # No es la via principal —esta pantalla se recorre con el teclado— pero
        # una tira recortada sin forma de ver el resto seria esconder datos.
        self.bind("<Button-1>", self._empezar_a_arrastrar)
        self.bind("<B1-Motion>", self._arrastrar)
        self._origen_del_arrastre = 0

    def _empezar_a_arrastrar(self, evento):
        self.scan_mark(evento.x, evento.y)
        self._origen_del_arrastre = evento.x

    def _arrastrar(self, evento):
        self.scan_dragto(evento.x, self._origen_del_arrastre, gain=1)

    def mostrar(self, bytes_pgm, motivo_si_no_hay=SIN_BANDA):
        """Pinta la tira, o el hueco rayado con su motivo si no hay imagen."""
        self.delete("all")
        self._imagen = None
        if bytes_pgm is None:
            self._dibujar_hueco(motivo_si_no_hay)
            return False
        try:
            self._imagen = tk.PhotoImage(data=bytes_pgm, master=self)
        except tk.TclError:
            # Un PGM que Tk no sabe leer no puede tumbar la pantalla entera: el
            # resto del caso sigue siendo corregible sin esta tira.
            self._dibujar_hueco("la imagen de esta fila no se pudo dibujar")
            return False
        # Centrada en vertical: una tira que cabe entera es mas baja que el hueco
        # —el escalado le baja el alto para no cortarle el ancho—, y pegada arriba
        # quedaria flotando con un hueco blanco debajo que parece un defecto.
        self.create_image(0, self._alto / 2, anchor="w", image=self._imagen)
        self.configure(scrollregion=(0, 0, self._imagen.width(), self._alto))
        return True

    def _dibujar_hueco(self, motivo):
        """El rectangulo rayado que dice por que no hay tira, con su texto."""
        for desplazamiento in range(-self._alto, self._ancho_visible, 10):
            self.create_line(
                desplazamiento,
                self._alto,
                desplazamiento + self._alto,
                0,
                fill=BORDE,
                width=1,
            )
        self.create_rectangle(
            6, self._alto / 2 - 9, self._ancho_visible - 6, self._alto / 2 + 9,
            fill="#FFFFFF", outline="",
        )
        self.create_text(
            self._ancho_visible / 2,
            self._alto / 2,
            text=motivo,
            font=LETRA_PEQUENA,
            fill=TEXTO_SECUNDARIO,
            width=self._ancho_visible - 16,
        )
