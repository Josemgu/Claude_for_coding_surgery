"""Las tres tarjetas de arriba y la fila de cuatro contadores.

Son **la funcion del viejo con la cara del nuevo**, que es la decision del dueno
del 2026-09-03: *«los botones que funcionan, el menú de iconos que funciona. No
debe ser así en la nueva interfaz, pero debe hacer lo mismo.»*

**Ni un parrafo.** El mockup lo cuenta medido: `querySelectorAll('p').length` = 0
dentro de la ventana. Aqui eso se traduce en una regla simple: cada renglon es una
cifra y hasta cuatro palabras. Lo que haya que explicar no va en la pantalla.

**Cada numero lleva su unidad al lado**, y no es adorno: la fila mezcla personas
con documentos —«23 personas por viajar» y «3 hojas sin devolver»— y un numero
suelto entre otros tres se lee en la unidad del vecino.

**El color nunca va solo.** Es la regla que ya gobierna `interfaz/tema.py`: cada
estado lleva ademas su palabra. Un rojo sin palabra no se lee si no se distinguen
los colores.
"""

import tkinter as tk

from interfaz.tema import LETRA_PEQUENA, LETRA_SECCION

PAPEL = "#FFFFFF"
PANEL = "#F4F5F7"
PANEL_HONDO = "#E9EBEF"
LINEA = "#D3D7DE"
LINEA_SUAVE = "#EEF0F3"
TINTA = "#1A1D21"
TINTA_SUAVE = "#46505C"

ROJO_SOLIDO = "#B3261E"
ROJO_BORDE = "#8E1B15"
VERDE_FONDO = "#E7F5EC"
VERDE_MARCA = "#1B6E3C"
AMBAR_FONDO = "#FFF3C4"
AMBAR_MARCA = "#8A5B00"
GRIS_MARCA = "#5B6470"

LETRA_CIFRA = ("Segoe UI", 20, "bold")
LETRA_CONTADOR = ("Segoe UI", 16, "bold")
LETRA_CASO = ("Consolas", 9, "bold")

# Cuantos renglones caben en una tarjeta sin que la fila de arriba crezca. Tres es
# lo que dibuja el mockup, y el resto se alcanza por el enlace de la cabecera.
RENGLONES_POR_TARJETA = 3


class Tarjeta(tk.Frame):
    """El marco blanco con su cabecera y su cuerpo. Las tres se construyen sobre esto."""

    def __init__(self, padre, titulo, texto_del_enlace=None, al_pulsar_el_enlace=None,
                 en_alarma=False):
        super().__init__(padre, background=PAPEL, highlightthickness=1,
                         highlightbackground=LINEA)
        fondo_de_la_cabecera = ROJO_SOLIDO if en_alarma else PANEL
        color_del_titulo = PAPEL if en_alarma else TINTA

        cabecera = tk.Frame(self, background=fondo_de_la_cabecera)
        cabecera.pack(fill="x")
        tk.Label(
            cabecera, text=titulo, background=fondo_de_la_cabecera,
            foreground=color_del_titulo, font=LETRA_SECCION, anchor="w",
            padx=10, pady=5, takefocus=0,
        ).pack(side="left")
        if texto_del_enlace and al_pulsar_el_enlace is not None:
            tk.Button(
                cabecera, text=texto_del_enlace, command=al_pulsar_el_enlace,
                font=LETRA_PEQUENA, background=fondo_de_la_cabecera,
                foreground=color_del_titulo, relief="flat", borderwidth=0,
                activebackground=ROJO_BORDE if en_alarma else PANEL_HONDO,
                padx=6, cursor="hand2",
            ).pack(side="right", padx=6)

        self.cuerpo = tk.Frame(self, background=PAPEL)
        self.cuerpo.pack(fill="both", expand=True, padx=10, pady=(6, 8))

    def cifra(self, numero, unidad, en_alarma=False):
        """La cifra grande con su unidad al lado. Una por tarjeta."""
        fila = tk.Frame(self.cuerpo, background=PAPEL)
        fila.pack(fill="x", pady=(0, 4))
        tk.Label(
            fila, text=str(numero), background=PAPEL,
            foreground=ROJO_SOLIDO if en_alarma else TINTA,
            font=LETRA_CIFRA, takefocus=0,
        ).pack(side="left")
        tk.Label(
            fila, text=unidad, background=PAPEL, foreground=TINTA_SUAVE,
            font=LETRA_PEQUENA, anchor="w", justify="left", takefocus=0,
        ).pack(side="left", padx=6)

    def vacia(self, frase):
        """Lo que se dibuja cuando no hay nada. Nunca se deja el hueco en blanco."""
        tk.Label(
            self.cuerpo, text=frase, background=PAPEL, foreground=VERDE_MARCA,
            font=LETRA_PEQUENA, anchor="w", takefocus=0,
        ).pack(fill="x", pady=2)


class RenglonDeCaso(tk.Frame):
    """Un renglon pulsable: cuanto falta, que documento, y cuanta gente lleva."""

    def __init__(self, padre, izquierda, centro, debajo, derecha, al_abrir,
                 color_de_la_izquierda=TINTA_SUAVE):
        super().__init__(padre, background=PAPEL, highlightthickness=1,
                         highlightbackground=PAPEL, highlightcolor=TINTA, takefocus=1)
        self.columnconfigure(1, weight=1)

        tk.Label(
            self, text=izquierda, background=PAPEL, foreground=color_de_la_izquierda,
            font=LETRA_PEQUENA, width=11, anchor="w", takefocus=0,
        ).grid(row=0, column=0, rowspan=2, sticky="w", padx=(2, 4))
        tk.Label(
            self, text=centro, background=PAPEL, foreground=TINTA,
            font=LETRA_CASO, anchor="w", takefocus=0,
        ).grid(row=0, column=1, sticky="w")
        tk.Label(
            self, text=debajo, background=PAPEL, foreground=TINTA_SUAVE,
            font=LETRA_PEQUENA, anchor="w", takefocus=0,
        ).grid(row=1, column=1, sticky="w")
        tk.Label(
            self, text=derecha, background=PAPEL, foreground=TINTA_SUAVE,
            font=LETRA_PEQUENA, anchor="e", takefocus=0,
        ).grid(row=0, column=2, rowspan=2, sticky="e", padx=2)

        for widget in (self,) + tuple(self.winfo_children()):
            widget.bind("<Button-1>", lambda evento: al_abrir())
        self.bind("<Return>", lambda evento: al_abrir())
        self.bind("<FocusIn>", lambda evento: self.configure(highlightbackground=TINTA))
        self.bind("<FocusOut>", lambda evento: self.configure(highlightbackground=PAPEL))


class RenglonDeAgente(tk.Frame):
    """Un agente: cuantos documentos lleva, cuanta gente, y su boton de paquete.

    El boton va **al lado del agente** y no en un menu aparte porque es lo que el
    pase pide y porque es donde se decide: se mira lo que lleva y se le manda.
    """

    def __init__(self, padre, renglon, al_generar_paquete=None, al_repartir=None):
        super().__init__(padre, background=PAPEL)
        self.columnconfigure(0, weight=1)

        tk.Label(
            self, text=renglon["nombre"], background=PAPEL, foreground=TINTA,
            font=LETRA_PEQUENA, anchor="w", takefocus=0,
        ).grid(row=0, column=0, sticky="w", padx=2)
        tk.Label(
            self, text=f"{renglon['cuantos_documentos']} doc.", background=PAPEL,
            foreground=TINTA_SUAVE, font=LETRA_PEQUENA, anchor="e", width=7,
            takefocus=0,
        ).grid(row=0, column=1, sticky="e")
        tk.Label(
            self, text=f"{renglon['personas']} pers.", background=PAPEL,
            foreground=TINTA_SUAVE, font=LETRA_PEQUENA, anchor="e", width=8,
            takefocus=0,
        ).grid(row=0, column=2, sticky="e")
        tk.Label(
            self, text=self._texto_de_lo_que_falta(renglon), background=PAPEL,
            foreground=AMBAR_MARCA if renglon["sin_devolver"] else TINTA_SUAVE,
            font=LETRA_PEQUENA, anchor="e", width=12, takefocus=0,
        ).grid(row=0, column=3, sticky="e")

        self._boton_del_agente(renglon, al_generar_paquete, al_repartir)

    def _texto_de_lo_que_falta(self, renglon):
        """«3 sin volver», «0 sin volver», o «—» para lo que no lleva nadie.

        El guion y no un cero: a lo que no lleva nadie no se le ha pedido nada, y
        un «0 sin volver» ahi se leeria como «ya devolvieron todo».
        """
        if renglon["sin_devolver"] is None:
            return "—"
        return f"{renglon['sin_devolver']} sin volver"

    def _boton_del_agente(self, renglon, al_generar_paquete, al_repartir):
        """«Generar paquete» al lado del agente; «Repartir» en el de sin asignar."""
        es_sin_asignar = renglon["companero_id"] is None
        if es_sin_asignar:
            if al_repartir is None or not renglon["cuantos_documentos"]:
                return
            texto, comando = "Repartir…", al_repartir
        else:
            if al_generar_paquete is None or not renglon["cuantos_documentos"]:
                return
            texto = "Generar paquete"
            comando = lambda: al_generar_paquete(renglon)
        tk.Button(
            self, text=texto, command=comando, font=LETRA_PEQUENA,
            background=PANEL, foreground=TINTA, activebackground=PANEL_HONDO,
            relief="solid", borderwidth=1, padx=6, pady=0, cursor="hand2",
        ).grid(row=0, column=4, sticky="e", padx=(6, 2))


class Contador(tk.Frame):
    """Un contador: su glifo de color, su rotulo en dos lineas y su numero."""

    def __init__(self, padre, rotulo, numero, fondo_del_glifo, color_del_glifo,
                 signo, en_alarma=False):
        super().__init__(padre, background=PAPEL, highlightthickness=1,
                         highlightbackground=LINEA)
        glifo = tk.Label(
            self, text=signo, background=fondo_del_glifo, foreground=color_del_glifo,
            font=LETRA_SECCION, width=3, takefocus=0,
        )
        glifo.pack(side="left", padx=6, pady=6)
        tk.Label(
            self, text=rotulo, background=PAPEL, foreground=TINTA_SUAVE,
            font=LETRA_PEQUENA, anchor="w", justify="left", takefocus=0,
        ).pack(side="left")
        tk.Label(
            self, text=str(numero), background=PAPEL,
            foreground=ROJO_SOLIDO if en_alarma else TINTA,
            font=LETRA_CONTADOR, takefocus=0,
        ).pack(side="right", padx=8)


# Los cuatro contadores del mockup, en su orden, con su glifo y su unidad. El
# rotulo dice la unidad porque la fila mezcla personas y documentos.
DEFINICION_DE_LOS_CONTADORES = (
    ("personas_por_viajar", "Personas\npor viajar", PANEL_HONDO, GRIS_MARCA, "◉", False),
    ("documentos_completos", "Con la recomendación\ncompleta", VERDE_FONDO, VERDE_MARCA, "✓", False),
    ("hojas_sin_devolver", "Hojas sin\ndevolver", AMBAR_FONDO, AMBAR_MARCA, "≡", False),
    ("personas_que_viajaron_sin_verificar", "Viajaron sin\nverificar", "#FDE7E7", ROJO_SOLIDO, "⚠", True),
)


class FilaDeContadores(tk.Frame):
    """Los cuatro numeros de arriba, en una sola fila y siempre los cuatro.

    Un contador que desaparece cuando vale 0 obliga a recordar cuantos habia. Los
    cuatro van siempre, con su cero si toca.
    """

    def __init__(self, padre, contadores):
        super().__init__(padre, background=PANEL)
        for columna, definicion in enumerate(DEFINICION_DE_LOS_CONTADORES):
            clave, rotulo, fondo, color, signo, en_alarma = definicion
            self.columnconfigure(columna, weight=1, uniform="contadores")
            numero = contadores.get(clave, 0)
            Contador(
                self, rotulo, numero, fondo, color, signo,
                en_alarma=en_alarma and bool(numero),
            ).grid(row=0, column=columna, sticky="ew", padx=(0 if columna == 0 else 4, 0))
