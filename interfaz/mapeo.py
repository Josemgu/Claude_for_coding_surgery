"""La pantalla de mapeo: Miguel dice qué columna del Excel es cada cosa.

Existe porque **nadie nombra las columnas igual dos veces**. Sin ella, cualquier
hoja que no sea exactamente la que genero el programa se descarta entera y quien lo
mira concluye que el programa esta roto.

**El programa propone y Miguel confirma** (regla permanente 5, otra vez). La
correspondencia que sale del automatismo llega ya elegida en los desplegables, y
**no se aplica hasta que se pulsa el boton**. Un campo que el automatismo no
reconocio se queda en «(sin asignar)» y se ve: adivinarle un parecido remoto seria
emparejar por aproximacion, que es de lo que este proyecto huye.

Y se ensenan **las tres primeras filas del archivo debajo de cada columna**. Es lo
que convierte la pantalla en comprobable: un desplegable que dice «MRN → N. de
Registro» se puede confirmar de verdad viendo que debajo pone `055-1111-3853`, y no
se puede confirmar de ninguna otra forma.
"""

import tkinter as tk
from tkinter import ttk

from datos.validacion import ErrorDeValidacion
from espejo.escritura import guardar_y_regenerar
from interfaz import dialogos
from interfaz.tema import BORDE, LETRA_DATO
from paquete.columnas import COLUMNAS_POR_NOMBRE
from paquete.lectura import ErrorDeLectura, fila_por_titulo
from paquete.mapeo import (
    CAMPOS_OBLIGATORIOS,
    CAMPOS_QUE_SE_PUEDEN_MAPEAR,
    abrir_para_mapear,
    campos_sin_asignar,
    traducir,
)
from paquete.reconciliacion import reconciliar_filas

SIN_ASIGNAR = "(sin asignar)"

# Cuantas filas del archivo se ensenan como muestra debajo de cada columna. Tres:
# suficiente para reconocer un MRN de un numero de caso, y poco para que quepa.
FILAS_DE_MUESTRA = 3


class VentanaDeMapeo(tk.Toplevel):
    """Un desplegable por campo, con una muestra de lo que trae esa columna.

    Al cerrarse deja en `self.resultado` el resultado de la reconciliacion, o
    `None` si se cancelo. Quien la abre hace `wait_window` y lo lee.
    """

    def __init__(self, padre, conexion, ruta, companero, carpeta_de_datos=None):
        super().__init__(padre)
        self.conexion = conexion
        self.companero = companero
        self._carpeta_de_datos = carpeta_de_datos
        # Se guarda porque el renglon de cada descarte la lleva dentro: «fila 14»
        # no lleva a ningun sitio cuando hay tres Excel en la misma carpeta.
        self.ruta = ruta
        self.resultado = None
        self._elegidos = {}

        self.title("¿Qué columna es cada cosa?")
        self.transient(padre)
        self.minsize(820, 520)
        self.rowconfigure(1, weight=1)
        self.columnconfigure(0, weight=1)

        try:
            self.libro, propuesta = abrir_para_mapear(ruta)
        except ErrorDeLectura as causa:
            dialogos.avisar_de_un_error("No se pudo abrir el archivo", str(causa), parent=padre)
            self.destroy()
            return

        self._construir_cabecera(ruta)
        self._construir_filas(propuesta)
        self._construir_pie()
        self.bind("<Escape>", lambda evento: self._cancelar())
        self.protocol("WM_DELETE_WINDOW", self._cancelar)
        self.grab_set()

    # ---- construccion ----------------------------------------------------

    def _construir_cabecera(self, ruta):
        cabecera = ttk.Frame(self, padding=12)
        cabecera.grid(row=0, column=0, sticky="ew")
        ttk.Label(cabecera, text="¿Qué columna es cada cosa?", style="Titulo.TLabel").pack(
            anchor="w"
        )
        ttk.Label(
            cabecera,
            text=(
                f"Archivo: {ruta}\nHoja leída: «{self.libro.hoja}» · "
                f"{len(self.libro.filas)} filas con datos.\n\n"
                "El número de caso y el MRN son obligatorios: son los dos que "
                "identifican a cada persona. NUNCA se empareja por nombre. Las "
                "filas cuyo par no exista en la base no se insertarán."
            ),
            style="Secundario.TLabel",
            justify="left",
        ).pack(anchor="w", pady=(4, 0))

    def _muestra_de(self, titulo):
        """Las tres primeras celdas de esa columna, para poder comprobar el mapeo."""
        if not titulo:
            return ""
        valores = []
        for fila in self.libro.filas[:FILAS_DE_MUESTRA]:
            valor = fila_por_titulo(self.libro.titulos, fila).get(titulo)
            valores.append(valor if valor is not None else "—")
        return "   ".join(str(valor) for valor in valores)

    def _construir_filas(self, propuesta):
        """Una fila por campo: nombre, desplegable de columnas, y la muestra."""
        marco = ttk.Frame(self, padding=(12, 0))
        marco.grid(row=1, column=0, sticky="nsew")
        marco.columnconfigure(2, weight=1)

        opciones = [SIN_ASIGNAR] + [
            titulo for titulo in self.libro.titulos if titulo
        ]
        self._muestras = {}
        for numero, campo in enumerate(CAMPOS_QUE_SE_PUEDEN_MAPEAR):
            obligatorio = campo in CAMPOS_OBLIGATORIOS
            etiqueta = COLUMNAS_POR_NOMBRE[campo].titulo + (" *" if obligatorio else "")
            ttk.Label(marco, text=etiqueta).grid(
                row=numero, column=0, sticky="w", pady=3
            )
            desplegable = ttk.Combobox(marco, state="readonly", values=opciones, width=28)
            desplegable.set(propuesta.get(campo, SIN_ASIGNAR))
            desplegable.grid(row=numero, column=1, sticky="w", padx=8)
            desplegable.bind(
                "<<ComboboxSelected>>", lambda evento, c=campo: self._actualizar_muestra(c)
            )
            self._elegidos[campo] = desplegable

            muestra = tk.Label(
                marco, text=self._muestra_de(propuesta.get(campo)), anchor="w",
                font=LETRA_DATO, background="#FFFFFF", padx=6, pady=3,
                highlightthickness=1, highlightbackground=BORDE,
            )
            muestra.grid(row=numero, column=2, sticky="ew", pady=3)
            self._muestras[campo] = muestra

        ttk.Label(
            marco,
            text="* obligatorio. Los campos sin asignar simplemente no se importan.",
            style="Secundario.TLabel",
        ).grid(row=len(CAMPOS_QUE_SE_PUEDEN_MAPEAR), column=0, columnspan=3, sticky="w",
               pady=(8, 0))

    def _actualizar_muestra(self, campo):
        titulo = self._elegidos[campo].get()
        self._muestras[campo].configure(
            text="" if titulo == SIN_ASIGNAR else self._muestra_de(titulo)
        )

    def _construir_pie(self):
        pie = ttk.Frame(self, padding=12)
        pie.grid(row=2, column=0, sticky="ew")
        ttk.Label(
            pie,
            text=(
                f"Las propuestas se guardarán a nombre de «{self.companero['nombre']}». "
                "Nada quedará verificado."
            ),
            style="Secundario.TLabel",
        ).pack(side="left")
        ttk.Button(pie, text="Cancelar  Esc", command=self._cancelar).pack(side="right")
        ttk.Button(pie, text="Importar", command=self._importar).pack(side="right", padx=6)

    # ---- acciones --------------------------------------------------------

    def _correspondencia(self):
        """Lo que Miguel dejo elegido, sin los campos sin asignar.

        Una misma columna elegida para dos campos se rechaza en `_importar`: dos
        campos leyendo la misma columna es siempre un error de quien lo eligio, y
        dejarlo pasar produciria un MRN copiado en el numero de caso.
        """
        return {
            campo: desplegable.get()
            for campo, desplegable in self._elegidos.items()
            if desplegable.get() and desplegable.get() != SIN_ASIGNAR
        }

    def _importar(self):
        correspondencia = self._correspondencia()

        faltan = campos_sin_asignar(correspondencia)
        if faltan:
            nombres = ", ".join(COLUMNAS_POR_NOMBRE[campo].titulo for campo in faltan)
            dialogos.avisar_de_un_error(
                "Faltan columnas obligatorias",
                f"Sin {nombres} no se puede saber a qué persona se refiere cada "
                "fila, y NUNCA se empareja por nombre. Asígnelas antes de importar.",
                parent=self,
            )
            return

        usadas = list(correspondencia.values())
        repetidas = {titulo for titulo in usadas if usadas.count(titulo) > 1}
        if repetidas:
            dialogos.avisar_de_un_error(
                "Una columna asignada dos veces",
                f"La(s) columna(s) {sorted(repetidas)} están asignadas a más de un "
                "campo. Cada columna del archivo puede ser una sola cosa.",
                parent=self,
            )
            return

        titulos, filas = traducir(self.libro, correspondencia)
        try:
            # Pasa por `guardar_y_regenerar` y no por `reconciliar_filas` a secas:
            # la reconciliacion escribe las cuatro columnas de la propuesta sobre
            # `personas`, y `personas` es una hoja del espejo. Sin esto, el Excel
            # espejo se queda sin lo que trajo el companero.
            self.resultado = guardar_y_regenerar(
                self.conexion,
                lambda: reconciliar_filas(
                    self.conexion, titulos, filas, self.companero["id"],
                    # La ruta viaja para que el renglon de cada descarte diga de
                    # que archivo salio esa fila. Sin ella, «fila 14» no lleva a
                    # ningun sitio cuando hay tres Excel en la carpeta.
                    ruta_excel=self.ruta,
                ),
                carpeta_de_datos=self._carpeta_de_datos,
                avisar=lambda aviso: dialogos.advertir(
                    "El Excel espejo no se pudo actualizar", aviso, parent=self
                ),
            ).guardado
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo importar", str(causa), parent=self)
            return
        self.destroy()

    def _cancelar(self):
        self.resultado = None
        self.destroy()
