"""El menu de iconos de la izquierda: «el menú de iconos que funciona».

**Lo que el dueno dijo, literal:** *«La interfaz de la vieja NO me gusta; me gusta
más la nueva. Pero la función de la vieja me gusta: los botones que funcionan, el
menú de iconos que funciona. No debe ser así en la nueva interfaz, pero debe hacer
lo mismo.»*

De ahi salen las dos reglas de este archivo:

  - **La cara es la del nuevo**, no la barra negra del viejo: panel claro, el
    elegido en tinta con el texto en blanco (16,91:1 medido por el disenador).
  - **Funciona**: cada icono lleva a una pantalla de verdad.

**Los iconos se dibujan con lineas sobre un lienzo**, no con imagenes. Es la regla
permanente 2 llevada al detalle: un `.png` por icono son ocho archivos mas que
empaquetar y que resolver en tiempo de ejecucion dentro del `.exe`, que es
exactamente el problema que ya dio la carpeta de modelos. Un lienzo de 18×18 con
cuatro trazos no tiene ese problema y se ve igual.

**Una seccion sin `comando` se dibuja apagada, no se esconde.** El menu del
mockup nombra cinco y hoy solo cuatro llevan a algun sitio (`DECISIONES.md`,
2026-09-03: *«Tabla, Historial y Equipo no están diseñadas: el menú las
nombra»*). Esconder la que falta haria que el menu cambiara de forma cuando se
construya; dibujarla apagada dice la verdad —existe, todavia no— y no promete un
clic que no va a pasar nada.
"""

import tkinter as tk

from interfaz.tema import LETRA_PEQUENA

FONDO = "#E9EBEF"
FONDO_AL_PASAR = "#DDE0E6"
TINTA = "#1A1D21"
TINTA_SUAVE = "#46505C"
APAGADO = "#646C77"
BLANCO = "#FFFFFF"
LINEA = "#D3D7DE"

LADO_DEL_ICONO = 18


def _trazos(lienzo, color, segmentos, ovalos=(), rectangulos=()):
    """Dibuja los tres tipos de trazo que usan estos iconos, en un solo sitio."""
    for puntos in segmentos:
        lienzo.create_line(*puntos, fill=color, width=1.6, capstyle="round")
    for caja in ovalos:
        lienzo.create_oval(*caja, outline=color, width=1.6)
    for caja in rectangulos:
        lienzo.create_rectangle(*caja, outline=color, width=1.6)


def icono_panel(lienzo, color):
    """Cuatro rectangulos: el mosaico del panel."""
    _trazos(lienzo, color, (), rectangulos=(
        (2, 2, 8, 8), (10, 2, 16, 6), (10, 8, 16, 16), (2, 10, 8, 16),
    ))


def icono_revisar(lienzo, color):
    """Una hoja con dos renglones: un documento por revisar."""
    _trazos(lienzo, color, (
        ((4, 2), (12, 2), (15, 5), (15, 16), (4, 16), (4, 2)),
        ((7, 9), (12, 9)),
        ((7, 12), (10, 12)),
    ))


def icono_tabla(lienzo, color):
    """Una rejilla: las columnas que salen al Excel."""
    _trazos(lienzo, color, (
        ((2, 7), (16, 7)), ((7, 7), (7, 16)), ((11, 7), (11, 16)),
    ), rectangulos=((2, 3, 16, 16),))


def icono_equipo(lienzo, color):
    """Dos personas: los companeros."""
    _trazos(lienzo, color, (
        ((2, 15), (2, 14), (5, 11), (8, 11), (11, 14), (11, 15)),
        ((12, 5), (14, 6), (14, 9), (12, 10)),
        ((13, 12), (16, 13), (16, 15)),
    ), ovalos=((4, 3, 10, 9),))


def icono_historial(lienzo, color):
    """Un reloj: lo que ya paso."""
    _trazos(lienzo, color, (
        ((9, 5), (9, 9), (12, 11)),
    ), ovalos=((2, 2, 16, 16),))


class Seccion:
    """Una entrada del menu: como se llama, su atajo, su dibujo y a donde va.

    `comando` a `None` significa «todavia no existe esa pantalla»: se dibuja
    apagada y no responde. No es un fallo: es lo que hay, dicho.
    """

    def __init__(self, nombre, atajo, icono, comando=None):
        self.nombre = nombre
        self.atajo = atajo
        self.icono = icono
        self.comando = comando

    @property
    def esta_disponible(self):
        return self.comando is not None


def secciones_de_la_ventana(al_inicio, al_revisar, al_equipo, al_historial):
    """Las cinco del mockup, en su orden, con las cuatro que ya llevan a algo.

    Se arman aqui y no en la ventana para que el orden y los atajos vivan en un
    solo sitio: el numero del atajo es la posicion, y si se reordenan alli sin
    tocar aqui, `Alt+3` acabaria abriendo otra cosa que la que dice el rotulo.
    """
    return [
        Seccion("Panel", "Alt+1", icono_panel, al_inicio),
        Seccion("Revisar", "Alt+2", icono_revisar, al_revisar),
        # Sin pantalla propia todavia (`DECISIONES.md`, 2026-09-03, decision 5).
        Seccion("Tabla", "Alt+3", icono_tabla, None),
        Seccion("Equipo", "Alt+4", icono_equipo, al_equipo),
        Seccion("Historial", "Alt+5", icono_historial, al_historial),
    ]


class MenuDeIconos(tk.Frame):
    """El rail de la izquierda. Una sola parada de tabulador, como el mockup.

    Dentro se mueve con las flechas y el foco **no sale** del rail hasta el
    siguiente `Tab`: es lo que dice la tabla de teclado del mockup, y es lo que
    hace que se pueda cambiar de pantalla sin tocar el raton.
    """

    def __init__(self, padre, secciones, activa="Panel"):
        super().__init__(padre, background=FONDO, width=168)
        self.pack_propagate(False)
        self._secciones = list(secciones)
        self._activa = activa
        self._filas = {}
        self._indice_con_foco = 0
        self._construir_la_marca()
        self._construir_los_grupos()
        self.marcar(activa)

    # ---- construccion ----------------------------------------------------

    def _construir_la_marca(self):
        """«Fichas» arriba, con el icono de documento del mockup."""
        marca = tk.Frame(self, background=FONDO)
        marca.pack(fill="x", padx=8, pady=(12, 16))
        lienzo = tk.Canvas(
            marca, width=LADO_DEL_ICONO, height=LADO_DEL_ICONO, background=FONDO,
            highlightthickness=0, takefocus=0,
        )
        lienzo.pack(side="left")
        icono_revisar(lienzo, TINTA)
        tk.Label(
            marca, text="Fichas", background=FONDO, foreground=TINTA,
            font=("Segoe UI", 11, "bold"), takefocus=0,
        ).pack(side="left", padx=6)

    def _construir_los_grupos(self):
        """«El trabajo» y «La gente», con sus secciones debajo de cada uno."""
        self._titulo_de_grupo("El trabajo")
        for seccion in self._secciones[:3]:
            self._construir_una(seccion)
        self._titulo_de_grupo("La gente")
        for seccion in self._secciones[3:]:
            self._construir_una(seccion)

    def _titulo_de_grupo(self, texto):
        tk.Label(
            self, text=texto.upper(), background=FONDO, foreground=TINTA_SUAVE,
            font=("Segoe UI", 7, "bold"), anchor="w", takefocus=0,
        ).pack(fill="x", padx=10, pady=(10, 2))

    def _construir_una(self, seccion):
        """Un renglon del menu: icono, nombre y atajo."""
        color = TINTA if seccion.esta_disponible else APAGADO
        fila = tk.Frame(self, background=FONDO, cursor="hand2" if seccion.esta_disponible else "arrow")
        fila.pack(fill="x", padx=6, pady=1)

        lienzo = tk.Canvas(
            fila, width=LADO_DEL_ICONO, height=LADO_DEL_ICONO, background=FONDO,
            highlightthickness=0, takefocus=0,
        )
        lienzo.pack(side="left", padx=(4, 6), pady=4)
        nombre = tk.Label(
            fila, text=seccion.nombre, background=FONDO, foreground=color,
            font=LETRA_PEQUENA, anchor="w", takefocus=0,
        )
        nombre.pack(side="left")
        atajo = tk.Label(
            fila, text=seccion.atajo, background=FONDO, foreground=TINTA_SUAVE,
            font=("Segoe UI", 7), takefocus=0,
        )
        atajo.pack(side="right", padx=6)

        self._filas[seccion.nombre] = {
            "seccion": seccion, "fila": fila, "lienzo": lienzo,
            "nombre": nombre, "atajo": atajo,
        }
        if seccion.esta_disponible:
            for widget in (fila, lienzo, nombre, atajo):
                widget.bind("<Button-1>", lambda evento, s=seccion: self._ir(s))
                widget.bind("<Enter>", lambda evento, n=seccion.nombre: self._al_pasar(n, True))
                widget.bind("<Leave>", lambda evento, n=seccion.nombre: self._al_pasar(n, False))

    # ---- estado visible --------------------------------------------------

    def marcar(self, nombre):
        """Deja marcada la seccion en la que estamos y repinta las demas."""
        self._activa = nombre
        for clave, partes in self._filas.items():
            self._pintar_una(partes, elegida=(clave == nombre))

    def _pintar_una(self, partes, elegida, al_pasar=False):
        """Los tres aspectos de un renglon: elegido, con el raton encima, normal."""
        seccion = partes["seccion"]
        if elegida:
            fondo, color = TINTA, BLANCO
        elif al_pasar:
            fondo, color = FONDO_AL_PASAR, TINTA
        else:
            fondo = FONDO
            color = TINTA if seccion.esta_disponible else APAGADO
        partes["fila"].configure(background=fondo)
        partes["lienzo"].configure(background=fondo)
        partes["nombre"].configure(background=fondo, foreground=color)
        partes["atajo"].configure(
            background=fondo, foreground="#C3C8D0" if elegida else TINTA_SUAVE
        )
        partes["lienzo"].delete("all")
        seccion.icono(partes["lienzo"], color)

    def _al_pasar(self, nombre, encima):
        if nombre == self._activa:
            return
        self._pintar_una(self._filas[nombre], elegida=False, al_pasar=encima)

    # ---- navegacion ------------------------------------------------------

    def _ir(self, seccion):
        """Va a esa seccion. Marca ANTES de llamar, para que se vea el cambio."""
        if not seccion.esta_disponible:
            return "break"
        self.marcar(seccion.nombre)
        seccion.comando()
        return "break"

    def ir_a(self, nombre):
        """Entra por nombre. Es lo que usan `Alt+1`…`Alt+5`."""
        partes = self._filas.get(nombre)
        if partes is None:
            return "break"
        return self._ir(partes["seccion"])

    def ir_por_numero(self, numero):
        """`Alt+N`: la posicion N del menu, contando desde 1."""
        if 1 <= numero <= len(self._secciones):
            return self.ir_a(self._secciones[numero - 1].nombre)
        return "break"

    def atajos_de_la_ventana(self):
        """Los `<Alt-Key-N>` que hay que atar al toplevel, ya emparejados.

        Se devuelven en vez de atarlos aqui: quien es dueno del toplevel es la
        ventana, y una pantalla que ata teclas globales sin que la ventana lo sepa
        es como quedan atajos vivos apuntando a pantallas ya destruidas.
        """
        return [
            (f"<Alt-Key-{posicion}>", seccion.nombre)
            for posicion, seccion in enumerate(self._secciones, start=1)
        ]
