"""El cajon de soltar archivos y las cuatro acciones de la derecha.

Es *«el gesto con el que empieza todo el trabajo»* (`mockups/mockup-v2-inicio.html`).

⚠️ **El cajon NO recibe archivos arrastrados, y se dice en vez de disimularlo.**
Tk no trae arrastrar-y-soltar de archivos del sistema: hace falta una extension de
Tcl (`tkdnd`), que es una dependencia nueva, y las dependencias nuevas estan fuera
del pase. El propio mockup ya lo declara: *«Tk no trae arrastrar-y-soltar de
archivos de serie; sin una extensión, el cajón funciona como botón y abre el
selector. Se dice aquí para que nadie lo dé por hecho.»*

Asi que el cajon **es un boton grande**: se pulsa y abre el selector. Y su rotulo
lo dice —«Pulse para elegir los PDF»—, porque un cajon que dice «suelta los
archivos aquí» y no los recibe al soltarlos es peor que no tener cajon: el usuario
arrastra, no pasa nada, y concluye que el programa esta roto.

**Las cuatro acciones y sus atajos salen del mockup**, no de lo que habia:

    Ctrl+O   Cargar formularios
    Ctrl+R   Recibir hoja del agente
    Ctrl+I   Informe para la dirección (PDF)
    Ctrl+E   Bajar la base en Excel

⚠️ **`Ctrl+I` cambia de significado.** Hasta hoy abria el selector de PDF
(`interfaz/inicio.py`, `_importar`); en el mockup es el informe. Se sigue el
mockup porque es la especificacion, y se deja `Ctrl+O` para lo que `Ctrl+I` hacia
antes. Queda dicho en el informe del pase: es memoria muscular de Miguel, y quien
decide si se respeta o se cambia es el dueno, no yo.
"""

import tkinter as tk

from interfaz.tema import LETRA_PEQUENA

PAPEL = "#FFFFFF"
PANEL = "#F4F5F7"
PANEL_HONDO = "#E9EBEF"
LINEA_FUERTE = "#AAB1BC"
TINTA = "#1A1D21"
TINTA_SUAVE = "#46505C"


class Accion:
    """Una de las cuatro: su rotulo, su atajo y lo que hace de verdad."""

    def __init__(self, rotulo, atajo, comando, es_principal=False):
        self.rotulo = rotulo
        self.atajo = atajo
        self.comando = comando
        self.es_principal = es_principal


def acciones_del_panel(al_cargar, al_recibir_hoja, al_generar_informe, al_bajar_excel):
    """Las cuatro del mockup, en su orden y con sus atajos en un solo sitio.

    Viven aqui —y no repartidas por la pantalla— para que el atajo y el rotulo no
    puedan separarse: un boton que dice `Ctrl+E` y responde a otra tecla es un
    fallo que solo se nota usandolo.
    """
    return [
        Accion("Cargar formularios", "Ctrl+O", al_cargar, es_principal=True),
        Accion("Recibir hoja del agente", "Ctrl+R", al_recibir_hoja),
        Accion("Informe para la dirección (PDF)", "Ctrl+I", al_generar_informe),
        Accion("Bajar la base en Excel", "Ctrl+E", al_bajar_excel),
    ]


class CajonDeDocumentos(tk.Frame):
    """El cajon punteado arriba y las cuatro acciones debajo."""

    def __init__(self, padre, acciones):
        super().__init__(padre, background=PAPEL)
        self._acciones = list(acciones)
        self._construir_el_cajon()
        self._construir_las_acciones()

    def _construir_el_cajon(self):
        """El recuadro punteado. Es un boton, y su rotulo lo dice."""
        cargar = self._acciones[0]
        self._cajon = tk.Frame(
            self, background=PANEL, highlightthickness=2,
            highlightbackground=LINEA_FUERTE, highlightcolor=TINTA, cursor="hand2",
            takefocus=1,
        )
        self._cajon.pack(fill="x", pady=(0, 8))
        tk.Label(
            self._cajon, text="↥", background=PANEL, foreground=TINTA_SUAVE,
            font=("Segoe UI", 15), takefocus=0,
        ).pack(pady=(10, 0))
        tk.Label(
            self._cajon, text="Pulse para elegir los PDF", background=PANEL,
            foreground=TINTA_SUAVE, font=LETRA_PEQUENA, takefocus=0,
        ).pack(pady=(0, 10))

        for widget in (self._cajon,) + tuple(self._cajon.winfo_children()):
            widget.bind("<Button-1>", lambda evento: cargar.comando())
        self._cajon.bind("<Return>", lambda evento: cargar.comando())
        self._cajon.bind("<space>", lambda evento: cargar.comando())

    def _construir_las_acciones(self):
        """Los cuatro botones, el primero destacado, cada uno con su atajo escrito."""
        for accion in self._acciones:
            self._boton(accion).pack(fill="x", pady=2)

    def _boton(self, accion):
        fondo = TINTA if accion.es_principal else PAPEL
        color = PAPEL if accion.es_principal else TINTA
        marco = tk.Frame(self, background=fondo, highlightthickness=1,
                         highlightbackground=LINEA_FUERTE, cursor="hand2")
        tk.Label(
            marco, text=accion.rotulo, background=fondo, foreground=color,
            font=LETRA_PEQUENA, anchor="w", padx=8, pady=5, takefocus=0,
        ).pack(side="left")
        tk.Label(
            marco, text=accion.atajo, background=fondo,
            foreground="#C3C8D0" if accion.es_principal else TINTA_SUAVE,
            font=("Segoe UI", 7), padx=8, takefocus=0,
        ).pack(side="right")
        for widget in (marco,) + tuple(marco.winfo_children()):
            widget.bind("<Button-1>", lambda evento, a=accion: a.comando())
        return marco

    def atajos_de_la_ventana(self):
        """Los `<Control-...>` ya emparejados con su accion, para que los ate la ventana.

        Igual que en el menu de iconos: quien ata teclas al toplevel es la ventana.
        Una pantalla que las ata por su cuenta las deja vivas cuando se destruye, y
        entonces `Ctrl+E` sigue escribiendo el Excel desde otra pantalla.
        """
        atados = []
        for accion in self._acciones:
            tecla = accion.atajo.split("+")[-1].lower()
            atados.append((f"<Control-{tecla}>", accion.comando))
        return atados
