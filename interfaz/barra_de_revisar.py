"""La barra de arriba de «Revisar»: los tableros, las pastillas de filtro y los avisos.

Va aparte de `interfaz/revisar.py` por el limite de 300 lineas por archivo y porque
de verdad son dos cosas: aqui se dice **que se ve**, alli se pinta la rejilla con lo
que salga de aqui. Ninguna de estas clases lee la base ni escribe nada; reciben las
cuentas ya hechas y devuelven clics.

**La franja de avisos es UNA linea, agrupada y cerrable.** Las tres palabras del
dueno el 2026-09-03: «no pongas tanto texto», «que se pueda cerrar el texto». Lo que
se agrupa son las clases de aviso, no los documentos: «sin numero de caso x 3» en
vez de tres renglones.
"""

import tkinter as tk
from tkinter import ttk

from interfaz.desplazamiento import atar_desplazamiento
from interfaz.tema import BORDE, LETRA_PEQUENA

AMBAR_FONDO = "#FFF3C4"
AMBAR_TINTA = "#3D2A00"
TINTA = "#1A1D21"
PAPEL = "#FFFFFF"

# Los filtros del tablero de trabajo, con el rotulo que se lee en su pastilla. En
# «Completados» no hay mas filtro que el propio tablero: son los completos y ya.
FILTROS_DEL_TRABAJO = (
    ("todo", "Todo"),
    ("sin_revisar", "Sin revisar"),
    ("incompletas", "Incompletas"),
    ("sin_asignar", "Sin asignar"),
    ("el_companero_dice_lista", "El compañero dice: lista"),
    ("fecha_pasada", "Fecha pasada"),
)


class BarraDeFiltros(ttk.Frame):
    """Las pastillas con su cuenta dentro. La puesta va en tinta sobre blanco.

    La cuenta va DENTRO de la pastilla y no suelta al lado, que es lo que hacia el
    programa viejo: dentro se lee sin buscarla.
    """

    def __init__(self, padre, al_elegir):
        super().__init__(padre)
        self.al_elegir = al_elegir

    def pintar(self, cuentas, filtro_puesto):
        """Vuelve a dibujar las seis pastillas con las cuentas de ahora."""
        for hijo in self.winfo_children():
            hijo.destroy()
        for filtro, rotulo in FILTROS_DEL_TRABAJO:
            puesto = filtro == filtro_puesto
            tk.Button(
                self,
                text=f"{rotulo}  {cuentas[filtro]}",
                font=LETRA_PEQUENA, relief="solid", borderwidth=1,
                background=TINTA if puesto else PAPEL,
                foreground=PAPEL if puesto else TINTA,
                command=lambda f=filtro: self.al_elegir(f),
            ).pack(side="left", padx=(0, 4))


class FranjaDeAvisos(ttk.Frame):
    """Una linea con lo que hay que mirar, agrupado por clase, y una «×» que la cierra.

    Cerrada se queda cerrada mientras dure la pantalla. No vuelve sola al refrescar:
    un aviso que reaparece despues de cerrarlo deja de ser un aviso y pasa a ser un
    estorbo, y entonces se aprende a no leerlo.
    """

    def __init__(self, padre):
        super().__init__(padre)
        self.cerrada = False

    def pintar(self, avisos):
        """`avisos` es una lista de `(cuantos, frase)`. Los ceros no salen."""
        for hijo in self.winfo_children():
            hijo.destroy()
        if self.cerrada:
            return
        trozos = [f"{frase} × {cuantos}" for cuantos, frase in avisos if cuantos]
        if not trozos:
            return
        franja = tk.Frame(
            self, background=AMBAR_FONDO, highlightthickness=1, highlightbackground=BORDE
        )
        franja.pack(fill="x", pady=(6, 0))
        tk.Label(
            franja, text="  ·  ".join(trozos), background=AMBAR_FONDO,
            foreground=AMBAR_TINTA, font=LETRA_PEQUENA, anchor="w", padx=8, pady=2,
        ).pack(side="left")
        tk.Button(
            franja, text="×", font=LETRA_PEQUENA, relief="flat",
            background=AMBAR_FONDO, command=self.cerrar,
        ).pack(side="right", padx=4)

    def cerrar(self):
        self.cerrada = True
        self.pintar([])


class RejillaQueRueda(ttk.Frame):
    """El lienzo con barra vertical donde se pintan las tarjetas.

    **La rueda del raton y el trackpad son dos eventos distintos en Tk 9**, y los
    dos los ata **un solo ayudante para toda la aplicacion**,
    `interfaz/desplazamiento.py`. Aqui habia una copia propia, y tenia dos fallos
    que la copia es justo lo que permite:

      1. **El desempaquetado estaba al reves.** `tk::PreciseScrollDeltas` deja x en
         los 16 bits altos e y en los bajos; esta clase leia los ALTOS y los movia
         como si fueran el vertical. Medido con un evento sintetico de x=0, y=-40
         —un deslizamiento vertical puro—: la rejilla se movia **0 px**. O sea que
         la queja del dueno seguia viva justo en la pantalla escrita para
         resolverla.
      2. **La atadura era local al lienzo**, y encima del lienzo estan las
         tarjetas: la rueda le llega al control que hay bajo el puntero, asi que
         solo desplazaba sobre los huecos entre tarjetas.

    ⚠️ `<TouchpadScroll>` **sigue sin estar comprobado con un trackpad de verdad**:
    esta maquina no tiene. Lo que se mide es el evento sintetico.
    """

    def __init__(self, padre):
        super().__init__(padre)
        self._congelada = False
        self.rowconfigure(0, weight=1)
        self.columnconfigure(0, weight=1)

        self.lienzo = tk.Canvas(self, highlightthickness=0, background="#F7F7F8")
        barra = ttk.Scrollbar(self, orient="vertical", command=self.lienzo.yview)
        self.lienzo.configure(yscrollcommand=barra.set)
        self.lienzo.grid(row=0, column=0, sticky="nsew")
        barra.grid(row=0, column=1, sticky="ns")

        self.dentro = ttk.Frame(self.lienzo)
        ventana = self.lienzo.create_window((0, 0), window=self.dentro, anchor="nw")
        self.dentro.bind("<Configure>", self._al_cambiar_de_tamano)
        self.lienzo.bind(
            "<Configure>",
            lambda evento: self.lienzo.itemconfigure(ventana, width=evento.width),
        )
        self.desplazador = atar_desplazamiento(self.lienzo)

    # ---- llenar la rejilla sin pagar n² --------------------------------------
    #
    # ⚠️ Medido en esta maquina antes de escribir esto (Tk 9.0.4, Python 3.14.7):
    # con el `<Configure>` atado mientras se llena, pintar la rejilla costaba
    #
    #      50 tarjetas ->  7,97 s     200 -> 37,97 s     500 -> 65,75 s
    #
    # y no es el coste de crear los controles —un `tk.Label` cuesta 0,44 ms y un
    # `ttk.Frame` 1,75 ms, o sea unos 90 ms para 50 tarjetas—. Es que **cada control
    # que entra dispara `<Configure>`, y el manejador llama a `bbox("all")`, que
    # recorre todo lo que ya hay**: n tarjetas cuestan n² recorridos. Con los 3000
    # documentos que el dueno nombro, eso no es lento, es inservible.
    #
    # Se congela mientras se llena y se calcula UNA vez al terminar.

    def congelar(self):
        """Deja de recalcular la region de desplazamiento mientras se llena."""
        self._congelada = True

    def descongelar(self):
        """Vuelve a calcularla, una sola vez, con todo ya dentro."""
        self._congelada = False
        self.lienzo.configure(scrollregion=self.lienzo.bbox("all"))

    def _al_cambiar_de_tamano(self, evento):
        if not self._congelada:
            self.lienzo.configure(scrollregion=self.lienzo.bbox("all"))

    def soltar(self):
        """Suelta la rueda y el trackpad. La pantalla lo llama al salir."""
        self.desplazador.soltar()
