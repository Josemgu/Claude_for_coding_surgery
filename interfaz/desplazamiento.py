"""Que la pantalla baje con la rueda **y** con el trackpad.

**La queja que cierra este modulo, literal del dueno:** *«le doy para abajo con
el trackpad de mi laptop y no baja. Eso es super incomodo.»*

**La causa, medida y no supuesta.** El proyecto corre sobre **Tk 9.0.4** (medido:
`tkinter.TkVersion` -> 9.0). Tk 9 separo el gesto de dos dedos del trackpad en un
evento propio, `<TouchpadScroll>`, distinto de `<MouseWheel>`. Medido en este
repositorio antes de escribir una linea:

    grep -rn "TouchpadScroll" --include=*.py .   ->  0 lineas

El programa solo ataba `<MouseWheel>`, asi que **el gesto del trackpad no tenia a
quien llegar**. No es que desplazara poco: es que no desplazaba nada.

**Lo que Tk hace por su cuenta, leido de la propia biblioteca y no de la
memoria** (`bind Text <TouchpadScroll>` consultado desde Python):

    lassign [tk::PreciseScrollDeltas %D] deltaX deltaY
    %W yview scroll [tk::ScaleNum [expr {-$deltaY}]] pixels

De ahi salen los tres hechos que gobiernan este archivo:

  1. **`%D` no es un numero de muescas**: empaqueta dos deltas, x en los 16 bits
     altos e y en los bajos. Se desempaqueta con `tk::PreciseScrollDeltas`, y aqui
     se llama a **esa misma funcion de Tcl** en vez de reimplementar el corrimiento
     de bits: si Tk cambia el empaquetado, esto cambia con el.
  2. El signo va **al reves**: `-deltaY`.
  3. **`Canvas` no trae ninguna de las dos ataduras de fabrica.** Medido:
     `bind Canvas <MouseWheel>` y `bind Canvas <TouchpadScroll>` devuelven los dos
     la cadena vacia. `Text`, `Listbox` y `Treeview` si las traen; el lienzo sobre
     el que se monta esta pantalla, no. Por eso hay que atarlas a mano.

**Y por que se desplaza en pixeles y no en «unidades».** Lo decide
`DECISIONES.md` (2026-09-03): *«el desplazamiento acumula el delta en pixeles, no
redondea a muescas de 120»*, porque un gesto pequeno tiene que mover algo. Medido
sobre un `Canvas` de esta maquina:

    yview_scroll(30, "pixels") -> TclError: bad argument "pixels": must be pages or units

O sea que el truco del `Text` **no sirve en un `Canvas`**. Lo que si sirve, y es
lo que se usa, es `yscrollincrement=1`: con eso una «unidad» del lienzo mide
exactamente un pixel. Medido despues de ponerlo: `yview_scroll(37, "units")`
mueve **37 px**.

**El resto se acumula y no se tira.** Un delta que traduce a 0,4 px desplazaria
cero y el gesto se perderia, que es la queja original vestida de otra forma. Se
guarda la fraccion y se suma a la siguiente, asi que muchos gestos pequenos
acaban moviendo.

⚠️ **Lo que NO esta verificado.** Aqui no hay trackpad: `<TouchpadScroll>` se
prueba con un evento sintetico (`event_generate`), que comprueba que la atadura
existe, que desempaqueta bien `%D` y que mueve el lienzo — **pero no que el
gesto real del portatil del dueno emita el evento con esa magnitud**. Eso se
comprueba en su maquina. `PIXELES_POR_UNIDAD_DE_TRACKPAD` esta puesto a 1 y es la
perilla que hay que tocar si allí se queda corto o se pasa.
"""

import tkinter as tk

# Cuanto baja la pantalla por cada muesca de una rueda de raton. En Windows una
# muesca son 120 unidades de `delta`, asi que esto es «pixeles por muesca» y la
# division de abajo lo convierte.
PIXELES_POR_MUESCA_DE_RUEDA = 50
UNIDADES_DE_UNA_MUESCA = 120

# Cuanto baja por cada unidad que manda el trackpad. Tk ya entrega estos deltas en
# una escala de pixeles, asi que el factor natural es 1. Se deja con nombre porque
# es lo unico que habria que ajustar si en el portatil del dueno el gesto se queda
# corto o se pasa de largo.
PIXELES_POR_UNIDAD_DE_TRACKPAD = 1


class Desplazador:
    """Ata la rueda y el trackpad a un lienzo, y sabe soltarlas.

    Las ataduras se ponen y se quitan al entrar y salir el raton del lienzo, y no
    de una vez para toda la ventana. `bind_all` sin soltar es como la pantalla de
    inicio acabaria desplazando mientras el raton esta sobre otra pantalla: es el
    mismo fallo que ya obligo a escribir `soltar_atajos()` en `interfaz/inicio.py`.
    """

    def __init__(self, lienzo):
        self._lienzo = lienzo
        # Con esto una «unidad» del lienzo mide un pixel. Es lo que permite
        # desplazar en pixeles pese a que `yview_scroll` no acepte "pixels".
        self._lienzo.configure(yscrollincrement=1)
        self._resto = 0.0
        self._atado = False
        lienzo.bind("<Enter>", self._al_entrar, add="+")
        lienzo.bind("<Leave>", self._al_salir, add="+")
        lienzo.bind("<Destroy>", self._al_salir, add="+")

    # ---- traduccion de cada evento a pixeles ----------------------------

    def _pixeles_de_la_rueda(self, evento):
        """Pixeles que pide una rueda de raton. `delta` positivo es hacia arriba."""
        return -evento.delta * PIXELES_POR_MUESCA_DE_RUEDA / UNIDADES_DE_UNA_MUESCA

    def _pixeles_del_trackpad(self, evento):
        """Pixeles que pide un gesto de dos dedos, desempaquetando `%D` con Tk.

        Se llama a `tk::PreciseScrollDeltas`, que es la funcion que usa la propia
        biblioteca de Tk para esto. Reimplementar aqui el corrimiento de bits
        seria copiar un detalle interno que Tk puede cambiar.
        """
        delta_x, delta_y = self._lienzo.tk.call("tk::PreciseScrollDeltas", evento.delta)
        return -int(delta_y) * PIXELES_POR_UNIDAD_DE_TRACKPAD

    # ---- el movimiento ---------------------------------------------------

    def _mover(self, pixeles):
        """Desplaza esos pixeles, guardando la fraccion que no llega a uno."""
        total = self._resto + pixeles
        enteros = int(total)
        self._resto = total - enteros
        if enteros:
            self._lienzo.yview_scroll(enteros, "units")

    def por_la_rueda(self, evento):
        self._mover(self._pixeles_de_la_rueda(evento))
        return "break"

    def por_el_trackpad(self, evento):
        self._mover(self._pixeles_del_trackpad(evento))
        return "break"

    # ---- atar y soltar ---------------------------------------------------

    def _al_entrar(self, evento=None):
        """Ata los dos eventos en toda la ventana mientras el raton este encima.

        Va con `bind_all` a proposito: la rueda le llega al widget que esta bajo el
        puntero, y encima del lienzo hay decenas de etiquetas y marcos que se la
        comerian. Atarlo solo al lienzo haria que la pantalla bajara unicamente
        sobre los huecos vacios, que es peor que no bajar porque parece averiado.
        """
        if self._atado:
            return
        self._lienzo.bind_all("<MouseWheel>", self.por_la_rueda)
        self._lienzo.bind_all("<TouchpadScroll>", self.por_el_trackpad)
        self._atado = True

    def _al_salir(self, evento=None):
        """Suelta las dos. Sin esto la pantalla seguiria desplazandose desde otra."""
        if not self._atado:
            return
        try:
            self._lienzo.unbind_all("<MouseWheel>")
            self._lienzo.unbind_all("<TouchpadScroll>")
        except tk.TclError:
            # La ventana ya se estaba cerrando. No hay nada que soltar y no es un
            # error que Miguel deba ver.
            pass
        self._atado = False

    soltar = _al_salir


class MarcoQueSeDesplaza(tk.Frame):
    """Un marco con barra y con las dos ataduras puestas. Se le mete todo dentro.

    Quien lo use trabaja contra `.interior`, que es un marco normal: nada de lo
    que se meta ahi tiene que saber que esta dentro de un lienzo.
    """

    def __init__(self, padre, fondo="#F4F5F7", **opciones):
        super().__init__(padre, **opciones)
        self.rowconfigure(0, weight=1)
        self.columnconfigure(0, weight=1)

        self._lienzo = tk.Canvas(
            self, background=fondo, highlightthickness=0, takefocus=0
        )
        self._lienzo.grid(row=0, column=0, sticky="nsew")
        self._barra = tk.Scrollbar(
            self, orient="vertical", command=self._lienzo.yview
        )
        self._barra.grid(row=0, column=1, sticky="ns")
        self._lienzo.configure(yscrollcommand=self._barra.set)

        self.interior = tk.Frame(self._lienzo, background=fondo)
        self._ventana = self._lienzo.create_window(
            (0, 0), window=self.interior, anchor="nw"
        )
        self.interior.bind("<Configure>", self._al_cambiar_el_contenido)
        self._lienzo.bind("<Configure>", self._al_cambiar_el_lienzo)

        self.desplazador = Desplazador(self._lienzo)

    def _al_cambiar_el_contenido(self, evento=None):
        """Reajusta la region desplazable cuando el contenido crece o mengua."""
        self._lienzo.configure(scrollregion=self._lienzo.bbox("all"))

    def _al_cambiar_el_lienzo(self, evento):
        """El contenido ocupa todo el ancho del lienzo, sin barra horizontal."""
        self._lienzo.itemconfigure(self._ventana, width=evento.width)

    def soltar(self):
        self.desplazador.soltar()


def atar_desplazamiento(lienzo):
    """Deja un lienzo ya existente desplazable con rueda y trackpad."""
    return Desplazador(lienzo)
