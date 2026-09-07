"""La pantalla «Revisar»: una tarjeta por documento, con sus filtros y su buscador.

Dos tableros en la misma pantalla, que es lo que el dueno pidio el 2026-09-03 —«y
pasa al tablero de completados»—:

  - **Trabajo**: todo lo que no esta completo. Es donde se trabaja.
  - **Completados**: lo marcado `completa`, con quien lo completo y cuando, con
    casillas y «Archivar seleccionados».

Un documento marcado completa sale del primero y aparece en el segundo. No son dos
consultas ni dos pantallas: son el mismo `datos/revision.py` con otro filtro.

**Aqui no se decide nada de datos.** Que documento entra en que filtro, que cuenta
como completo y quien firma una marca lo deciden `datos/revision.py` y
`datos/marcas_de_revision.py`. Esta pantalla pinta y llama.

**Cambiar de filtro no reconstruye las tarjetas**: las oculta y las ensena. Con la
carpeta entera del dueno —3000 documentos— reconstruir la rejilla en cada clic es
lo que hace que una pantalla de Tk se sienta tosca.
"""

import tkinter as tk
from tkinter import ttk

from datos.archivo import archivar_caso
from datos.estados import COMPLETA
from datos.marcas_de_revision import marcar_a_mano
from datos.revision import (
    cuentas_de_los_filtros,
    documentos_para_revisar,
    filtrar,
)
from interfaz import dialogos
from interfaz.barra_de_revisar import BarraDeFiltros, FranjaDeAvisos, RejillaQueRueda
from interfaz.etiquetas import numero_de_caso_visible
from interfaz.tarjeta_de_documento import Tarjeta

TABLERO_DE_TRABAJO = "trabajo"
TABLERO_DE_COMPLETADOS = "completados"

TARJETAS_POR_FILA = 3

# Cuantas tarjetas se dibujan de una vez. No es un numero de estilo, es lo unico que
# hoy mantiene la pantalla abrible con los 3000 documentos que el dueno nombro:
# `interfaz/barra_de_revisar.py` trae la medicion de lo que cuesta cada tarjeta
# dentro del lienzo. 24 son ocho filas de tres —dos pantallas— y el resto entra con
# «Ver mas».
#
# ⚠️ NO es el dibujado por ventana visible que pide el mockup («con 100 tarjetas, la
# rejilla dibuja solo lo visible»). Eso sigue sin hacerse y esta en el informe.
TARJETAS_POR_PAGINA = 24


class PantallaDeRevisar(ttk.Frame):
    """Los documentos en tarjetas, en dos tableros, con lo que hace falta para marcar."""

    _ATAJOS = ("<Control-a>", "<Control-A>", "<Escape>", "<F5>")

    def __init__(self, padre, conexion, hoy, al_abrir, companero_id, al_volver=None):
        super().__init__(padre, padding=(12, 10))
        self.conexion = conexion
        self.hoy = hoy
        self.al_abrir = al_abrir
        self.al_volver = al_volver
        # Quien firma lo que se marque desde aqui. Es Miguel: los dos botones de la
        # tarjeta son suyos (`datos/marcas_de_revision.py` lo explica entero).
        self.companero_id = companero_id

        self.tablero = TABLERO_DE_TRABAJO
        self.filtro = "todo"
        self.busqueda = tk.StringVar()
        self.busqueda.trace_add("write", lambda *_: self._repintar())
        self._tarjetas = []
        self._ver_mas = None
        self._pagina = 1

        self.columnconfigure(0, weight=1)
        self.rowconfigure(4, weight=1)
        self._construir_barra()
        self._construir_tableros()
        self._filtros = BarraDeFiltros(self, self._poner_filtro)
        self._filtros.grid(row=2, column=0, sticky="ew", pady=(6, 0))
        self._avisos = FranjaDeAvisos(self)
        self._avisos.grid(row=3, column=0, sticky="ew")
        self._rueda = RejillaQueRueda(self)
        self._rueda.grid(row=4, column=0, sticky="nsew", pady=(8, 0))
        self._rejilla = self._rueda.dentro
        self.atar_atajos()
        self.refrescar()

    # ---- armazon ---------------------------------------------------------

    def _construir_barra(self):
        barra = ttk.Frame(self)
        barra.grid(row=0, column=0, sticky="ew")
        barra.columnconfigure(1, weight=1)
        ttk.Label(barra, text="Revisar", style="Titulo.TLabel").grid(row=0, column=0)
        self._cuenta = ttk.Label(barra, text="", style="Secundario.TLabel")
        self._cuenta.grid(row=0, column=1, sticky="w", padx=10)
        ttk.Entry(barra, textvariable=self.busqueda, width=34).grid(row=0, column=2)
        ttk.Label(
            barra, text="caso, unidad, nombre o MRN", style="Secundario.TLabel"
        ).grid(row=0, column=3, padx=(6, 0))
        if self.al_volver is not None:
            ttk.Button(barra, text="Volver", command=self.al_volver).grid(
                row=0, column=4, padx=(10, 0)
            )

    def _construir_tableros(self):
        fila = ttk.Frame(self)
        fila.grid(row=1, column=0, sticky="ew", pady=(8, 0))
        self._botones_de_tablero = {}
        for tablero, rotulo in (
            (TABLERO_DE_TRABAJO, "Trabajo"),
            (TABLERO_DE_COMPLETADOS, "Completados"),
        ):
            boton = ttk.Button(
                fila, text=rotulo, command=lambda t=tablero: self._cambiar_de_tablero(t)
            )
            boton.pack(side="left", padx=(0, 6))
            self._botones_de_tablero[tablero] = boton
        self._archivar = ttk.Button(
            fila, text="Archivar seleccionados", command=self._archivar_seleccionados
        )

    def atar_atajos(self):
        """Ata los atajos al toplevel. La ventana la llama cada vez que la enseña."""
        raiz = self.winfo_toplevel()
        raiz.bind("<Control-a>", lambda evento: self._seleccionar_todo())
        raiz.bind("<Control-A>", lambda evento: self._seleccionar_todo())
        raiz.bind("<Escape>", lambda evento: self._poner_filtro("todo"))
        raiz.bind("<F5>", lambda evento: self.refrescar())

    def soltar_atajos(self):
        """Quita los atajos y la rueda de esta pantalla. Se llama al cambiar.

        La rueda va aquí por lo mismo que los atajos: se ata a toda la ventana
        —`bind_all`, porque encima del lienzo están las tarjetas y se comerían el
        evento— y una atadura de toda la ventana que nadie suelta sobrevive a la
        pantalla que la puso.
        """
        raiz = self.winfo_toplevel()
        for atajo in self._ATAJOS:
            raiz.unbind(atajo)
        self._rueda.soltar()

    # ---- lo que se ve ----------------------------------------------------

    def refrescar(self):
        """Relee los documentos de la base y vuelve a pintar todo."""
        self.documentos = documentos_para_revisar(self.conexion, self.hoy)
        self.cuentas = cuentas_de_los_filtros(self.documentos)
        self._pintar_filtros()
        self._pintar_avisos()
        self._pintar_rejilla()

    def _del_tablero(self):
        """Los documentos del tablero puesto, antes de aplicarles filtro y busqueda.

        **Lo archivado no sale en ninguno de los dos**, y lo decidio el dueno el
        2026-09-03: «lo archivado sale del tablero de completados y queda en el
        historico y en el calendario marcado archivado». En el de trabajo tampoco,
        porque archivar es justo decir que ya no hay trabajo ahi.

        ⚠️ Esto choca con `mockups/mockup-v2-revisar.html`, que dibuja una tarjeta
        archivada con sus dos botones inactivos. La palabra del dueno es posterior al
        mockup y manda, pero la cara archivada de `interfaz/tarjeta_de_documento.py` **se conserva**
        —no se borra lo que parece muerto durante el desarrollo— por si el dueno
        quiere de vuelta el filtro que las ensena. Va al informe.
        """
        vivos = [
            documento for documento in self.documentos if not documento["archivado"]
        ]
        if self.tablero == TABLERO_DE_COMPLETADOS:
            return [
                documento
                for documento in vivos
                if documento["estado_de_la_tarjeta"] == COMPLETA
            ]
        return [
            documento
            for documento in vivos
            if documento["estado_de_la_tarjeta"] != COMPLETA
        ]

    def _visibles(self):
        """Lo que de verdad se pinta: tablero, mas filtro, mas lo buscado."""
        del_tablero = {documento["id"] for documento in self._del_tablero()}
        filtro = "todo" if self.tablero == TABLERO_DE_COMPLETADOS else self.filtro
        return [
            documento
            for documento in filtrar(self.documentos, filtro, self.busqueda.get())
            if documento["id"] in del_tablero
        ]

    def _pintar_filtros(self):
        """En «Completados» no hay pastillas: hay casillas y un botón de archivar."""
        if self.tablero == TABLERO_DE_COMPLETADOS:
            for hijo in self._filtros.winfo_children():
                hijo.destroy()
            self._archivar.pack(side="left", padx=(12, 0))
            return
        self._archivar.pack_forget()
        self._filtros.pintar(self.cuentas, self.filtro)

    def _pintar_avisos(self):
        """Los avisos del tablero, agrupados por clase en una sola línea."""
        self._avisos.pintar(
            (
                (sum(1 for d in self.documentos if not d["numero_caso"]),
                 "sin número de caso"),
                (sum(1 for d in self.documentos if d["es_duplicado"]), "duplicado"),
                (self.cuentas["fecha_pasada"], "con la fecha ya pasada"),
            )
        )

    def _pintar_rejilla(self):
        """Dibuja una pagina de tarjetas, congelando la rejilla mientras se llena.

        **Solo se dibuja una pagina**, y no las 3000 que puede haber. El mockup lo
        pide con esas palabras —«con 100 tarjetas, la rejilla dibuja solo lo
        visible»— y esta medido por que: sin el tope, y aun con la rejilla congelada,
        cada tarjeta cuesta controles de Tk que se pagan aunque nadie las mire.
        Esto es un tope por pagina, **no** el dibujado por ventana visible que pide
        el mockup: ese sigue sin hacerse y va al informe.
        """
        for tarjeta in self._tarjetas:
            tarjeta.destroy()
        self._tarjetas = []
        visibles = self._visibles()
        pagina = visibles[: self._pagina * TARJETAS_POR_PAGINA]
        self._cuenta.configure(
            text=f"viendo {len(pagina)} de {len(visibles)} · "
            f"{len(self.documentos)} documentos en total"
        )
        self._rueda.congelar()
        for columna in range(TARJETAS_POR_FILA):
            self._rejilla.columnconfigure(columna, weight=1, uniform="tarjetas")
        for indice, documento in enumerate(pagina):
            tarjeta = Tarjeta(
                self._rejilla, documento, self,
                con_casilla=self.tablero == TABLERO_DE_COMPLETADOS,
            )
            tarjeta.grid(
                row=indice // TARJETAS_POR_FILA, column=indice % TARJETAS_POR_FILA,
                sticky="nsew", padx=4, pady=4,
            )
            self._tarjetas.append(tarjeta)
        self._pintar_el_boton_de_ver_mas(len(pagina), len(visibles))
        self._rueda.descongelar()

    def _pintar_el_boton_de_ver_mas(self, pintadas, todas):
        """«Ver más» al pie de la rejilla, con cuántas quedan. Nada si no queda ninguna."""
        if self._ver_mas is not None:
            self._ver_mas.destroy()
            self._ver_mas = None
        if pintadas >= todas:
            return
        self._ver_mas = ttk.Button(
            self._rejilla,
            text=f"Ver más  ({todas - pintadas} sin dibujar)",
            command=self._pagina_siguiente,
        )
        self._ver_mas.grid(
            row=pintadas // TARJETAS_POR_FILA + 1, column=0,
            columnspan=TARJETAS_POR_FILA, pady=8,
        )

    def _pagina_siguiente(self):
        self._pagina += 1
        self._pintar_rejilla()

    def _repintar(self):
        """Vuelve a repartir las tarjetas sin volver a leer la base."""
        self._pagina = 1
        self._pintar_filtros()
        self._pintar_rejilla()

    # ---- lo que hacen los controles --------------------------------------

    def _cambiar_de_tablero(self, tablero):
        self.tablero = tablero
        self.filtro = "todo"
        self._repintar()

    def _poner_filtro(self, filtro):
        self.filtro = filtro
        self._repintar()

    def _seleccionar_todo(self):
        """Ctrl+A: marca todas las casillas del tablero de completados."""
        for tarjeta in self._tarjetas:
            if tarjeta._con_casilla:
                tarjeta.seleccionada.set(True)

    # ---- lo que la tarjeta llama -----------------------------------------

    def abrir(self, documento):
        """Abre el documento en la pantalla de corrección que ya existe."""
        self.al_abrir(documento)

    def marcar(self, documento, estado):
        """Los dos botones de la tarjeta. Manda sobre lo que dijo el compañero."""
        try:
            marcar_a_mano(self.conexion, documento["id"], estado, self.companero_id)
        except Exception as causa:
            dialogos.avisar_de_un_error(
                "No se pudo marcar el documento",
                f"El documento {numero_de_caso_visible(documento)} no se marcó: {causa}",
                parent=self,
            )
            return
        self.refrescar()

    def asignar(self, documento):
        """El desplegable de asignar a un compañero.

        ⚠️ Todavía no asigna: la pantalla de asignación existe
        (`interfaz/asignacion.py`) y es de otro pase. Esto lo dice en vez de no
        hacer nada, que es lo que haría creer que se asignó.
        """
        dialogos.informar(
            "Asignar",
            "Asignar desde la tarjeta todavía no está conectado. Se asigna en la "
            "pantalla «Asignar casos».",
            parent=self,
        )

    def avisar_de_lo_archivado(self):
        """Por qué los dos botones de una tarjeta archivada no hacen nada."""
        dialogos.advertir(
            "Documento archivado",
            "Este documento está archivado. Para cambiar su estado hay que "
            "desarchivarlo primero.",
            parent=self,
        )

    def archivar(self, documentos):
        """Archiva los documentos que se le pasen, diciendo antes cuántos son."""
        if not documentos:
            dialogos.advertir(
                "No hay nada seleccionado",
                "Hay que marcar la casilla de al menos un documento.",
                parent=self,
            )
            return
        if not dialogos.preguntar_si_o_no(
            "Archivar",
            f"Se van a archivar {len(documentos)} documento(s). "
            "Salen de los tableros y siguen contando en los reportes. ¿Archivar?",
            parent=self,
        ):
            return
        for documento in documentos:
            archivar_caso(self.conexion, documento["id"])
        self.refrescar()

    def _archivar_seleccionados(self):
        self.archivar(
            [
                tarjeta.documento
                for tarjeta in self._tarjetas
                if tarjeta.seleccionada.get()
            ]
        )
