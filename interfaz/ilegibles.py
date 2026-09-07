"""La lista de lo que no se pudo leer: un renglon por documento, y se queda.

Lo pidio el dueno el 2026-09-02: «debe poder leer letra dentro de PDF escaneados,
y si no puede debe ponerlo en un renglón que notifique que tales documentos no son
legibles».

**Por que es una ventana y no un aviso.** Un `messagebox` con el resumen se cierra
con Aceptar y no deja nada. Con 500 documentos es aun peor: no se puede leer al
vuelo ni recordar cual fallo. Aqui la lista sale de la tabla `documentos_ilegibles`
—donde ya estaba escrita antes de abrir esta ventana— asi que se puede consultar
manana, y la semana que viene.

Es el mismo criterio que `interfaz/descartados.py`, con la diferencia que ese
archivo dejaba anotada como pendiente del dueno: alli la lista se pierde al cerrar
porque no habia tabla; aqui la hay.

**Cada renglon dice las cuatro cosas con las que se puede hacer algo:** que
archivo, que pagina, que paso —la frase del motivo, no el codigo— y que leyo la
maquina. Lo ultimo es lo que permite distinguir «el escaneo esta en blanco» de «el
escaneo se lee pero el formulario es otro», que son dos problemas con dos arreglos
distintos.

Esta ventana **no cambia nada**: solo lee. Ni marca, ni borra, ni reintenta.
"""

import tkinter as tk
from tkinter import ttk

from datos.ilegibles import (
    MOTIVOS,
    MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO,
    contar_por_motivo,
    documentos_ilegibles,
    frase_del_motivo,
)
from interfaz.tema import (
    AMBAR_DE_FILA,
    AMBAR_SIN_FECHA,
    BORDE,
    LETRA_DATO,
    LETRA_PEQUENA,
    ROJO_DE_FILA,
    ROJO_VENCIDO,
)


def texto_de_los_ilegibles(filas):
    """La lista como texto plano, para copiar y pegar en un correo o una nota.

    Lleva la ruta entera de cada archivo y no solo su nombre: pegada fuera del
    programa, un nombre suelto no sirve para encontrar nada.
    """
    lineas = [f"Documentos y páginas que no se pudieron leer ({len(filas)}):", ""]
    for fila in filas:
        pagina = f", página {fila['pagina_pdf']}" if fila["pagina_pdf"] else ""
        lineas.append(f"{fila['ruta_pdf']}{pagina}")
        lineas.append(f"    Qué pasó: {frase_del_motivo(fila['motivo'])}")
        if fila["numero_caso"]:
            # ⚠️ Va el número **y el id**, desde el 2026-09-03. Desde la versión 12
            # del esquema dos casos pueden llevar el mismo número —es lo que ese
            # cambio existe para permitir—, así que «Caso guardado: BALC2609» ya no
            # lleva a ninguna parte: hay dos.
            lineas.append(
                f"    Caso guardado: {fila['numero_caso']} (id {fila['caso_id']})"
            )
        elif fila["caso_id"]:
            lineas.append(f"    Caso guardado sin número, id {fila['caso_id']}")
        if fila["detalle"]:
            lineas.append(f"    Lo que leyó: {fila['detalle']}")
        lineas.append(f"    Anotado el {fila['registrado_en']}")
    return "\n".join(lineas)


class VentanaDeIlegibles(tk.Toplevel):
    """Los renglones de `documentos_ilegibles`, el mas reciente arriba."""

    def __init__(self, padre, conexion):
        super().__init__(padre)
        self.conexion = conexion
        self.filas = documentos_ilegibles(conexion)
        self.title("Documentos y páginas que no entraron")
        self.transient(padre)
        self.minsize(860, 480)
        self.rowconfigure(1, weight=1)
        self.columnconfigure(0, weight=1)

        self._construir_cabecera()
        self._construir_lista()
        self._construir_pie()
        self.bind("<Escape>", lambda evento: self.destroy())
        self.grab_set()

    def _construir_cabecera(self):
        """El total y el desglose por motivo, que es lo que dice donde mirar."""
        cabecera = ttk.Frame(self, padding=12)
        cabecera.grid(row=0, column=0, sticky="ew")
        ttk.Label(
            cabecera,
            text=f"{len(self.filas)} documentos o páginas no entraron enteros",
            style="Titulo.TLabel",
        ).pack(anchor="w")
        ttk.Label(
            cabecera,
            text=self._desglose(),
            style="Secundario.TLabel",
            wraplength=800,
            justify="left",
        ).pack(anchor="w", pady=(4, 0))

    def _desglose(self):
        """«3 sin número de caso · 1 sin texto», o la frase de la lista vacia.

        El desglose por motivo va arriba del todo porque contesta la pregunta que
        se hace quien abre esta ventana —¿el problema son mis archivos, mis
        escaneos o el lector?— sin tener que leer los renglones uno a uno.
        """
        if not self.filas:
            return (
                "No hay ninguno anotado. Todo lo que se ha importado entró entero, "
                "o todavía no se ha importado nada."
            )
        cuentas = contar_por_motivo(self.conexion)
        partes = [
            f"{cuentas[motivo]} {motivo.replace('_', ' ')}"
            for motivo in MOTIVOS
            if cuentas.get(motivo)
        ]
        return " · ".join(partes)

    def _construir_lista(self):
        """Los renglones dentro de algo que rueda: pueden ser cientos."""
        marco = ttk.Frame(self, padding=(12, 0))
        marco.grid(row=1, column=0, sticky="nsew")
        marco.rowconfigure(0, weight=1)
        marco.columnconfigure(0, weight=1)

        lienzo = tk.Canvas(marco, highlightthickness=0, background="#F7F7F8")
        barra = ttk.Scrollbar(marco, orient="vertical", command=lienzo.yview)
        lienzo.configure(yscrollcommand=barra.set)
        lienzo.grid(row=0, column=0, sticky="nsew")
        barra.grid(row=0, column=1, sticky="ns")

        dentro = ttk.Frame(lienzo)
        ventana = lienzo.create_window((0, 0), window=dentro, anchor="nw")
        dentro.bind(
            "<Configure>", lambda evento: lienzo.configure(scrollregion=lienzo.bbox("all"))
        )
        lienzo.bind(
            "<Configure>", lambda evento: lienzo.itemconfigure(ventana, width=evento.width)
        )
        for fila in self.filas:
            self._pintar(dentro, fila)

    def _pintar(self, padre, registro):
        """Un renglon: dónde, qué pasó, y qué leyó la máquina.

        El ámbar y el rojo separan lo que todavía tiene arreglo de lo que no. Una
        página que entró sin número de caso está guardada y solo hay que teclear el
        número: es ámbar. De una que no se pudo abrir no quedó nada: es roja. Y la
        distinción no depende del color —la frase la dice entera—, el color solo
        acelera a quien lo ve.
        """
        se_guardo_algo = registro["motivo"] in MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO
        fondo = AMBAR_DE_FILA if se_guardo_algo else ROJO_DE_FILA
        letra = AMBAR_SIN_FECHA if se_guardo_algo else ROJO_VENCIDO

        fila = tk.Frame(padre, background=fondo, highlightthickness=1, highlightbackground=BORDE)
        fila.pack(fill="x", pady=1)

        def renglon(texto, fuente, arriba=0, abajo=0):
            tk.Label(
                fila, text=texto, background=fondo, foreground=letra, font=fuente,
                anchor="w", padx=8, pady=0, wraplength=800, justify="left",
            ).pack(fill="x", pady=(arriba, abajo))

        pagina = f"   ·   página {registro['pagina_pdf']}" if registro["pagina_pdf"] else ""
        renglon(f"{registro['ruta_pdf']}{pagina}", LETRA_DATO, arriba=6)
        renglon(frase_del_motivo(registro["motivo"]), LETRA_PEQUENA)
        if registro["detalle"]:
            renglon(f"Lo que leyó: {registro['detalle']}", LETRA_PEQUENA)
        renglon(f"Anotado el {registro['registrado_en']}", LETRA_PEQUENA, abajo=6)

    def _construir_pie(self):
        pie = ttk.Frame(self, padding=12)
        pie.grid(row=2, column=0, sticky="ew")
        ttk.Button(pie, text="Copiar la lista", command=self._copiar).pack(side="left")
        ttk.Button(pie, text="Cerrar  Esc", command=self.destroy).pack(side="right")

    def _copiar(self):
        """Deja la lista en el portapapeles.

        `update()` despues de escribir no es adorno: sin el, en Windows el
        contenido se pierde si el programa se cierra antes de que otro lo pida.
        """
        self.clipboard_clear()
        self.clipboard_append(texto_de_los_ilegibles(self.filas))
        self.update()
