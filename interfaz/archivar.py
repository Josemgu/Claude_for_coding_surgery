"""La pantalla de archivar un caso: quien viajó, quien no, y por qué.

Va como pantalla y no como una ventana encima porque `interfaz/aplicacion.py` lo
decide asi para todo el programa: «una sola ventana... las pantallas se sustituyen
dentro; no se abren ventanas encima», y el motivo es que tres ventanas apiladas se
pierden detras de las demas del escritorio.

**Archivar no obliga a responder.** Quien no sepa si alguien viajo deja «no
consta», que es un valor de verdad y no un hueco. Obligar a contestar es lo que
hace que alguien marque cualquier cosa por salir del paso, y entonces el reporte
que va a los jefes lleva datos inventados sobre personas reales (regla permanente 1).

**Lo unico que si es obligatorio es el motivo cuando alguien NO pudo viajar.** Una
fila del reporte que dice «no pudo viajar» sin decir por que no le sirve de nada a
quien la lee, y ademas el `CHECK` del motor la rechazaria.

**Archivar se puede deshacer**, y la pantalla lo dice antes de archivar. Se
deshace desde la pantalla de reportes, en la lista del historico.
"""

import tkinter as tk
from tkinter import ttk

from datos.archivo import (
    NADIE_LO_HA_DICHO,
    NO_PUDO_VIAJAR,
    SI_VIAJO,
    archivar_caso,
    personas_del_caso_con_su_viaje,
    registrar_resultado_del_viaje,
)
from datos.repositorio import leer_caso_por_id
from datos.validacion import ErrorDeValidacion
from espejo.escritura import guardar_y_regenerar
from interfaz import dialogos
from interfaz.tema import AMBAR_DE_FILA, AMBAR_SIN_FECHA, LETRA_PEQUENA, LETRA_SECCION

# Las tres respuestas, con el texto que se lee en pantalla y el valor que se
# guarda. En este orden a proposito: «no consta» va PRIMERO y es el que viene
# marcado, porque es lo unico cierto mientras nadie diga otra cosa.
RESPUESTAS = (
    ("no_consta", "No consta", NADIE_LO_HA_DICHO),
    ("si", "Sí viajó", SI_VIAJO),
    ("no", "NO pudo viajar", NO_PUDO_VIAJAR),
)

_VALOR_POR_RESPUESTA = {clave: valor for clave, _, valor in RESPUESTAS}
_RESPUESTA_POR_VALOR = {valor: clave for clave, _, valor in RESPUESTAS}


class BloqueDeViajeDePersona(ttk.Frame):
    """Una persona con las tres respuestas y el hueco del motivo."""

    def __init__(self, padre, persona):
        super().__init__(padre, padding=(0, 6))
        self.persona = persona
        self.columnconfigure(3, weight=1)

        ttk.Label(
            self, text=persona["nombre"] or "nombre sin leer", style="Seccion.TLabel"
        ).grid(row=0, column=0, sticky="w")
        ttk.Label(
            self, text=persona["mrn"] or "sin MRN", style="Secundario.TLabel"
        ).grid(row=0, column=1, sticky="w", padx=(12, 12))

        self.respuesta = tk.StringVar(
            value=_RESPUESTA_POR_VALOR.get(persona["pudo_viajar"], "no_consta")
        )
        opciones = ttk.Frame(self)
        opciones.grid(row=0, column=2, sticky="w")
        for columna, (clave, etiqueta, _) in enumerate(RESPUESTAS):
            ttk.Radiobutton(
                opciones, text=etiqueta, value=clave, variable=self.respuesta,
                command=self._ajustar_el_motivo,
            ).grid(row=0, column=columna, padx=4)

        self.motivo = ttk.Entry(self)
        self.motivo.grid(row=0, column=3, sticky="ew", padx=(12, 0))
        if persona["motivo_no_viajo"]:
            self.motivo.insert(0, persona["motivo_no_viajo"])
        self._ajustar_el_motivo()

    def _ajustar_el_motivo(self):
        """El motivo solo se puede escribir cuando la respuesta es «no pudo viajar».

        Apagado y no escondido: un hueco que aparece y desaparece mueve toda la
        fila de sitio, y un campo que se mueve mientras se rellena se rellena mal.
        """
        toca = self.respuesta.get() == "no"
        self.motivo.configure(state="normal" if toca else "disabled")

    def valor_y_motivo(self):
        """Lo que hay que guardar de esta persona: `(pudo_viajar, motivo)`."""
        clave = self.respuesta.get()
        valor = _VALOR_POR_RESPUESTA[clave]
        motivo = self.motivo.get().strip() if clave == "no" else None
        return valor, (motivo or None)

    def guardar(self, conexion):
        """Escribe lo respondido de esta persona. Levanta si falta el motivo."""
        valor, motivo = self.valor_y_motivo()
        return registrar_resultado_del_viaje(conexion, self.persona["id"], valor, motivo)


class PantallaDeArchivo(ttk.Frame):
    """Preguntar quién viajó y archivar el caso, en una sola pantalla."""

    _ATAJOS = ("<Control-Home>",)

    def __init__(self, padre, conexion, caso_id, al_volver, carpeta_de_datos=None):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self.caso_id = caso_id
        self._al_volver = al_volver
        self._carpeta_de_datos = carpeta_de_datos
        self.caso = leer_caso_por_id(conexion, caso_id)
        if self.caso is None:
            raise ErrorDeValidacion(f"No hay ningún caso con el id {caso_id!r}.")

        self.rowconfigure(2, weight=1)
        self.columnconfigure(0, weight=1)
        self._construir_cabecera()
        self._construir_personas()
        self._construir_pie()
        self.atar_atajos()

    def atar_atajos(self):
        """Ata los atajos al toplevel. La ventana la llama cada vez que la enseña."""
        self.winfo_toplevel().bind("<Control-Home>", lambda evento: self._volver())

    def soltar_atajos(self):
        """Quita los atajos de esta pantalla. Se llama al cambiar de pantalla."""
        raiz = self.winfo_toplevel()
        for atajo in self._ATAJOS:
            raiz.unbind(atajo)

    def _construir_cabecera(self):
        """El caso, su fecha de viaje, y qué significa archivar."""
        ttk.Label(
            self, text=f"Archivar el caso {self.caso['numero_caso']}", style="Titulo.TLabel"
        ).grid(row=0, column=0, sticky="w")

        aviso = tk.Frame(self, background=AMBAR_DE_FILA)
        aviso.grid(row=1, column=0, sticky="ew", pady=(8, 8))
        tk.Label(
            aviso,
            text=(
                f"Fecha de viaje: {self.caso['fecha_viaje'] or 'sin fecha'}.   "
                "Archivar saca este caso de las listas de trabajo y del bloque de "
                "viajes próximos. NO se borra nada: sigue contando en los reportes, "
                "y se puede desarchivar desde la pantalla de reportes."
            ),
            background=AMBAR_DE_FILA, foreground=AMBAR_SIN_FECHA, font=LETRA_PEQUENA,
            anchor="w", justify="left", padx=10, pady=8, wraplength=1000,
        ).pack(anchor="w", fill="x")

    def _construir_personas(self):
        """Un bloque por persona, con el denominador escrito en el título."""
        personas = personas_del_caso_con_su_viaje(self.conexion, self.caso_id)
        marco = ttk.Frame(self)
        marco.grid(row=2, column=0, sticky="nsew")
        marco.columnconfigure(0, weight=1)

        ttk.Label(
            marco,
            text=f"¿Quién llegó a viajar?  ({len(personas)} personas en este caso)",
            style="Seccion.TLabel",
        ).grid(row=0, column=0, sticky="w", pady=(0, 4))
        ttk.Label(
            marco,
            text=(
                "Lo que no se sepa se deja en «No consta». Es preferible un hueco "
                "honesto a una respuesta puesta por salir del paso."
            ),
            style="Secundario.TLabel",
        ).grid(row=1, column=0, sticky="w", pady=(0, 8))

        self._bloques = []
        for numero, persona in enumerate(personas, start=2):
            bloque = BloqueDeViajeDePersona(marco, persona)
            bloque.grid(row=numero, column=0, sticky="ew")
            self._bloques.append(bloque)

    def _construir_pie(self):
        """Los dos botones: archivar, o volver sin tocar nada."""
        pie = ttk.Frame(self)
        pie.grid(row=3, column=0, sticky="ew", pady=(12, 0))
        pie.columnconfigure(0, weight=1)
        ttk.Label(
            pie,
            text="Nada se guarda hasta que pulse «Archivar el caso».",
            style="Secundario.TLabel",
        ).grid(row=0, column=0, sticky="w")
        ttk.Button(pie, text="Archivar el caso", command=self.archivar).grid(
            row=0, column=1, padx=4
        )
        ttk.Button(pie, text="Volver sin archivar  Ctrl+Inicio", command=self._volver).grid(
            row=0, column=2, padx=4
        )

    def _guardar_todo(self):
        """Anota el viaje de cada persona y archiva el caso. Todo o nada.

        El orden importa: primero las personas y despues el caso. Si el motivo de
        alguien falta, esto levanta antes de haber archivado nada, y el caso se
        queda donde estaba en vez de desaparecer de las listas con la mitad de las
        respuestas escritas.
        """
        for bloque in self._bloques:
            bloque.guardar(self.conexion)
        return archivar_caso(self.conexion, self.caso_id)

    def archivar(self):
        """Guarda, archiva, regenera el Excel espejo y vuelve al inicio.

        Pasa por `guardar_y_regenerar` y no por `archivar_caso` a secas: el espejo
        tiene una columna `archivado` y otra `fecha_archivado`, y sin regenerarlo
        el Excel seguiria diciendo que el caso está vivo.
        """
        try:
            guardar_y_regenerar(
                self.conexion,
                self._guardar_todo,
                carpeta_de_datos=self._carpeta_de_datos,
                avisar=lambda aviso: dialogos.advertir(
                    "El Excel espejo no se pudo actualizar", aviso, parent=self
                ),
            )
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo archivar", str(causa), parent=self)
            return False

        dialogos.informar(
            "Caso archivado",
            f"El caso {self.caso['numero_caso']} queda archivado. Sale de las "
            "listas de trabajo y sigue contando en los reportes.",
            parent=self,
        )
        self._al_volver()
        return True

    def _volver(self, evento=None):
        """Vuelve al inicio sin escribir nada."""
        self._al_volver()
        return "break"
