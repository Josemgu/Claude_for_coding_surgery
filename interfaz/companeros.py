"""La pantalla de companeros: dar de alta, corregir el nombre y desactivar.

**No hay boton de borrar, y su ausencia es la funcion principal de esta pantalla.**
`DECISIONES.md` (2026-09-02) lo decide: los companeros se desactivan, o se pierde
el historial de quien verifico que. Un boton de borrar aqui seria la unica forma
de que ese historial desapareciera, porque la capa de datos no ofrece la operacion.

Lo que si hay es **«Desactivar»**, y el texto de la pantalla dice lo que hace y lo
que NO hace: sale del desplegable, y sus verificaciones siguen ahi con su nombre.
Un boton llamado «Desactivar» que la gente lee como «borrar» produce el miedo de no
pulsarlo, y entonces la lista se llena de gente que ya no esta.

Regla permanente 4: todo lo que se ve aqui esta en espanol, errores incluidos.
"""

import tkinter as tk
from tkinter import ttk

from datos.asignaciones import casos_asignados
from datos.companeros import (
    alta_de_companero,
    desactivar_companero,
    reactivar_companero,
    renombrar_companero,
    todos_los_companeros,
)
from datos.validacion import ErrorDeValidacion
from interfaz import dialogos
from interfaz.tema import AMBAR_DE_FILA, BORDE, LETRA_PEQUENA


class PantallaDeCompaneros(ttk.Frame):
    """Quien trabaja en esto, con su estado y cuantos casos lleva."""

    _ATAJOS = ("<Control-Home>",)

    def __init__(self, padre, conexion, al_volver):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self._al_volver = al_volver
        self._seleccionado = None

        self.rowconfigure(2, weight=1)
        self.columnconfigure(0, weight=1)
        self._construir_barra()
        self._construir_alta()
        self._marco_de_la_lista = ttk.Frame(self)
        self._marco_de_la_lista.grid(row=2, column=0, sticky="nsew", pady=8)
        self.atar_atajos()
        self.refrescar()

    # ---- barra y alta ----------------------------------------------------

    def _construir_barra(self):
        barra = ttk.Frame(self)
        barra.grid(row=0, column=0, sticky="ew")
        barra.columnconfigure(0, weight=1)
        ttk.Label(barra, text="Compañeros", style="Titulo.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        ttk.Button(barra, text="Volver al inicio  Ctrl+Inicio", command=self._volver).grid(
            row=0, column=1
        )

    def _construir_alta(self):
        """La caja de alta, arriba: es lo que se viene a hacer aqui la primera vez."""
        marco = ttk.Frame(self)
        marco.grid(row=1, column=0, sticky="ew", pady=(10, 0))
        marco.columnconfigure(1, weight=1)
        ttk.Label(marco, text="Nombre del compañero:").grid(row=0, column=0, sticky="w")
        self._nombre = ttk.Entry(marco)
        self._nombre.grid(row=0, column=1, sticky="ew", padx=8)
        self._nombre.bind("<Return>", lambda evento: self._dar_de_alta())
        ttk.Button(marco, text="Agregar", command=self._dar_de_alta).grid(row=0, column=2)
        ttk.Label(
            marco,
            text=(
                "Dos compañeros pueden llamarse igual: el programa no lo impide, "
                "porque obligar a inventar un apodo sería peor."
            ),
            style="Secundario.TLabel",
        ).grid(row=1, column=0, columnspan=3, sticky="w", pady=(4, 0))

    def atar_atajos(self):
        """Ata los atajos al toplevel. La ventana la llama cada vez que la enseña."""
        self.winfo_toplevel().bind("<Control-Home>", lambda evento: self._volver())

    def soltar_atajos(self):
        """Quita los atajos de esta pantalla. Se llama al cambiar de pantalla."""
        raiz = self.winfo_toplevel()
        for atajo in self._ATAJOS:
            raiz.unbind(atajo)

    def _volver(self, evento=None):
        self._al_volver()
        return "break"

    # ---- acciones --------------------------------------------------------

    def _dar_de_alta(self):
        try:
            alta_de_companero(self.conexion, self._nombre.get())
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo agregar", str(causa), parent=self)
            return
        self._nombre.delete(0, "end")
        self.refrescar()

    def _renombrar(self, companero):
        """Corrige el nombre con una caja de texto en la misma fila."""
        ventana = tk.Toplevel(self)
        ventana.title("Corregir el nombre")
        ventana.transient(self.winfo_toplevel())
        ventana.resizable(False, False)
        ttk.Label(ventana, text="Nombre:", padding=10).grid(row=0, column=0)
        entrada = ttk.Entry(ventana, width=40)
        entrada.insert(0, companero["nombre"])
        entrada.grid(row=0, column=1, padx=(0, 10))
        entrada.focus_set()

        def guardar(evento=None):
            try:
                renombrar_companero(self.conexion, companero["id"], entrada.get())
            except ErrorDeValidacion as causa:
                dialogos.avisar_de_un_error("No se pudo guardar", str(causa), parent=ventana)
                return
            ventana.destroy()
            self.refrescar()

        ttk.Button(ventana, text="Guardar", command=guardar).grid(
            row=1, column=1, sticky="e", padx=10, pady=10
        )
        entrada.bind("<Return>", guardar)
        ventana.bind("<Escape>", lambda evento: ventana.destroy())
        ventana.grab_set()

    def _desactivar(self, companero):
        """Pregunta antes, y la pregunta dice exactamente que pasa y que NO pasa."""
        casos = casos_asignados(self.conexion, companero["id"])
        aviso = (
            f"\n\nAhora mismo lleva {len(casos)} caso(s) asignado(s). Esas "
            "asignaciones NO se retiran solas: si ya no va a trabajar en ellas, "
            "retíreselas desde la pantalla de asignación."
            if casos
            else ""
        )
        if not dialogos.preguntar_si_o_no(
            "Desactivar compañero",
            f"«{companero['nombre']}» dejará de aparecer en el desplegable de "
            "asignación.\n\nNO se borra nada: los campos que ya verificó siguen "
            "mostrando su nombre, y se le puede reactivar cuando haga falta." + aviso
            + "\n\n¿Desactivarlo?",
            parent=self,
        ):
            return
        try:
            desactivar_companero(self.conexion, companero["id"])
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo desactivar", str(causa), parent=self)
            return
        self.refrescar()

    def _reactivar(self, companero):
        try:
            reactivar_companero(self.conexion, companero["id"])
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo reactivar", str(causa), parent=self)
            return
        self.refrescar()

    # ---- repintado -------------------------------------------------------

    def refrescar(self):
        """Relee la lista entera. Los activos arriba, con su cuenta de casos."""
        for hijo in self._marco_de_la_lista.winfo_children():
            hijo.destroy()

        companeros = todos_los_companeros(self.conexion)
        if not companeros:
            ttk.Label(
                self._marco_de_la_lista,
                text=(
                    "Todavía no hay ningún compañero. Escriba un nombre arriba y "
                    "pulse «Agregar» para poder asignarle casos."
                ),
                style="Secundario.TLabel",
            ).pack(anchor="w")
            return

        activos = sum(1 for companero in companeros if companero["activo"])
        ttk.Label(
            self._marco_de_la_lista,
            text=f"{activos} activo(s) de {len(companeros)} en total",
            style="Seccion.TLabel",
        ).pack(anchor="w", pady=(0, 6))
        for companero in companeros:
            self._pintar_fila(companero)

    def _pintar_fila(self, companero):
        """Una fila con el nombre, el estado, los casos vivos y sus dos botones."""
        activo = bool(companero["activo"])
        fondo = "#FFFFFF" if activo else AMBAR_DE_FILA
        fila = tk.Frame(
            self._marco_de_la_lista, background=fondo, highlightthickness=1,
            highlightbackground=BORDE,
        )
        fila.pack(fill="x", pady=1)

        casos = len(casos_asignados(self.conexion, companero["id"]))
        texto = f"{companero['nombre']}   ·   {casos} caso(s) asignado(s)"
        if not activo:
            texto += f"   ·   DESACTIVADO el {companero['desactivado_en']}"
        tk.Label(
            fila, text=texto, background=fondo, anchor="w", padx=8, pady=6,
            font=LETRA_PEQUENA, takefocus=0,
        ).pack(side="left")

        if activo:
            ttk.Button(
                fila, text="Desactivar", command=lambda: self._desactivar(companero)
            ).pack(side="right", padx=4, pady=3)
        else:
            ttk.Button(
                fila, text="Reactivar", command=lambda: self._reactivar(companero)
            ).pack(side="right", padx=4, pady=3)
        ttk.Button(
            fila, text="Corregir nombre", command=lambda: self._renombrar(companero)
        ).pack(side="right", padx=4, pady=3)
