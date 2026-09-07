"""Asignar casos a un companero, generar su paquete y cargar el que devuelve.

Las tres cosas viven en la MISMA pantalla y en ese orden de arriba abajo, porque
son un solo trabajo repetido cada semana: se elige a quien, se le marcan los casos,
se le genera la carpeta, y cuando contesta se carga su Excel. Repartirlas en tres
sitios obligaria a acordarse de los tres.

**El orden de los casos es el del dano**: el que viaja antes, arriba. Es el mismo
criterio de la pantalla de inicio y de `datos/pendientes.py`, y por el mismo motivo
—si hay que ordenar a mano para saber que corre, algun dia no se ordena—.

**Lo que vuelve entra como propuesta.** El resumen que se ensena al terminar lo
dice con esas palabras: nada queda verificado, y el caso lo sigue confirmando
Miguel. Regla permanente 5.

**Las cuatro acciones que escriben pasan por `espejo.escritura.guardar_y_regenerar`.**
Asignar, retirar y las dos formas de cargar el Excel que vuelve. No es una
comodidad: `asignaciones` y `personas` son hojas del espejo, y la auditoria final
de QA midio que estos caminos dejaban el `.xlsx` atrasado —«asignaciones base=1
espejo=0»— contra lo que `espejo/escritura.py` promete de si mismo. Quien anada
aqui una accion que escriba en la base tiene que pasar por ahi tambien.
"""

import os
import tkinter as tk
from tkinter import filedialog, ttk

from datos.asignaciones import (
    asignar_caso,
    casos_asignados,
    companeros_del_caso,
    retirar_caso,
)
from datos.calendario import casos_que_viajan_pronto
from datos.companeros import companeros_activos
from datos.pendientes import casos_pendientes_de_verificar
from datos.repositorio import leer_caso_por_id
from datos.validacion import ErrorDeValidacion
from espejo.escritura import guardar_y_regenerar
from interfaz import dialogos
from interfaz.descartados import VentanaDeDescartados
from interfaz.tema import AMBAR_DE_FILA, BORDE, LETRA_PEQUENA
from paquete.exportacion import ErrorDeExportacion, exportar_paquete
from paquete.lectura import ErrorDeLectura
from paquete.reconciliacion import reconciliar_excel, resumen_de_la_vuelta


class PantallaDeAsignacion(ttk.Frame):
    """Elegir compañero, marcarle casos, darle el paquete y recoger su Excel."""

    _ATAJOS = ("<Control-Home>",)

    def __init__(self, padre, conexion, al_volver, hoy, carpeta_de_datos=None):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self._al_volver = al_volver
        self.hoy = hoy
        self._carpeta_de_datos = carpeta_de_datos
        self._companeros = []
        self._marcados = {}

        self.rowconfigure(2, weight=1)
        self.columnconfigure(0, weight=1)
        self.columnconfigure(1, weight=1)
        self._construir_barra()
        self._construir_seleccion_de_companero()
        self._marco_de_disponibles = ttk.Frame(self)
        self._marco_de_disponibles.grid(row=2, column=0, sticky="nsew", padx=(0, 6), pady=8)
        self._marco_de_asignados = ttk.Frame(self)
        self._marco_de_asignados.grid(row=2, column=1, sticky="nsew", padx=(6, 0), pady=8)
        self._construir_pie()
        self.atar_atajos()
        self.refrescar()

    # ---- barra -----------------------------------------------------------

    def _construir_barra(self):
        barra = ttk.Frame(self)
        barra.grid(row=0, column=0, columnspan=2, sticky="ew")
        barra.columnconfigure(0, weight=1)
        ttk.Label(barra, text="Asignar casos", style="Titulo.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        ttk.Button(barra, text="Volver al inicio  Ctrl+Inicio", command=self._volver).grid(
            row=0, column=1
        )

    def _construir_seleccion_de_companero(self):
        marco = ttk.Frame(self)
        marco.grid(row=1, column=0, columnspan=2, sticky="ew", pady=(10, 0))
        marco.columnconfigure(1, weight=1)
        ttk.Label(marco, text="Compañero:").grid(row=0, column=0, sticky="w")
        # `state="readonly"`: se elige de la lista y no se teclea. Un nombre
        # tecleado no seria un companero de la base, y no habria a quien asignarle.
        self._desplegable = ttk.Combobox(marco, state="readonly", width=40)
        self._desplegable.grid(row=0, column=1, sticky="w", padx=8)
        self._desplegable.bind("<<ComboboxSelected>>", lambda evento: self.refrescar_listas())
        self._aviso_del_desplegable = ttk.Label(marco, style="Secundario.TLabel")
        self._aviso_del_desplegable.grid(row=1, column=0, columnspan=2, sticky="w", pady=(4, 0))

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

    # ---- pie -------------------------------------------------------------

    def _construir_pie(self):
        pie = ttk.Frame(self)
        pie.grid(row=3, column=0, columnspan=2, sticky="ew", pady=(8, 0))
        pie.columnconfigure(0, weight=1)
        self._resumen = ttk.Label(pie, style="Secundario.TLabel", wraplength=760)
        self._resumen.grid(row=0, column=0, sticky="w")

        botones = ttk.Frame(pie)
        botones.grid(row=0, column=1, sticky="e")
        ttk.Button(
            botones, text="Generar paquete…", command=self._generar_paquete
        ).grid(row=0, column=0, padx=4)
        ttk.Button(
            botones, text="Cargar Excel devuelto…", command=self._cargar_devuelto
        ).grid(row=0, column=1, padx=4)
        ttk.Button(
            botones, text="Importar Excel libre…", command=self._importar_libre
        ).grid(row=0, column=2, padx=4)
        # La puerta a lo que quedó guardado de cargas anteriores. Sin ella la tabla
        # `filas_descartadas` existiría y nadie la vería nunca, que es un escalón
        # por debajo del defecto que cierra: antes se perdía, ahora se guardaría
        # sin que hubiera forma de mirarla.
        ttk.Button(
            botones, text="Descartes guardados…", command=self._ver_descartes_guardados
        ).grid(row=0, column=3, padx=4)

    # ---- datos -----------------------------------------------------------

    def _companero_elegido(self):
        """El companero seleccionado, o None si no hay ninguno.

        Se busca por POSICION en la lista y no por el texto del desplegable: dos
        companeros pueden llamarse igual (`docs/ARQUITECTURA.md` §2.4), y buscando
        por nombre el trabajo del segundo iria a parar al primero.
        """
        indice = self._desplegable.current()
        if indice < 0 or indice >= len(self._companeros):
            return None
        return self._companeros[indice]

    def refrescar(self):
        """Relee los companeros activos y repinta las dos listas."""
        elegido = self._companero_elegido()
        self._companeros = companeros_activos(self.conexion)
        self._desplegable["values"] = [
            # El id va en el texto porque dos pueden llamarse igual y, sin algo que
            # los distinga, el desplegable enseña dos entradas idénticas.
            f"{companero['nombre']}  (#{companero['id']})"
            for companero in self._companeros
        ]
        if self._companeros:
            posicion = 0
            if elegido is not None:
                for indice, companero in enumerate(self._companeros):
                    if companero["id"] == elegido["id"]:
                        posicion = indice
                        break
            self._desplegable.current(posicion)
            self._aviso_del_desplegable.configure(text="")
        else:
            self._desplegable.set("")
            self._aviso_del_desplegable.configure(
                text=(
                    "No hay ningún compañero activo. Dé de alta uno en la pantalla "
                    "de compañeros antes de asignar casos."
                )
            )
        self.refrescar_listas()

    def refrescar_listas(self):
        """Repinta la lista de disponibles y la de asignados al elegido."""
        companero = self._companero_elegido()
        self._pintar_disponibles(companero)
        self._pintar_asignados(companero)
        self._pintar_resumen(companero)

    def _casos_disponibles(self, companero):
        """Los casos que se le pueden dar: los pendientes, sin los que ya lleva.

        Se parte de los pendientes de verificar y no de todos los casos: dar a
        revisar uno que ya esta verificado entero es trabajo que no hace falta.
        Se anaden los que viajan pronto aunque estuvieran verificados, porque esos
        se persiguen igual.
        """
        if companero is None:
            return []
        ya_lleva = {caso["id"] for caso in casos_asignados(self.conexion, companero["id"])}
        candidatos = {}
        for caso in casos_pendientes_de_verificar(self.conexion):
            candidatos[caso["id"]] = caso
        for caso in casos_que_viajan_pronto(self.conexion, self.hoy):
            candidatos.setdefault(caso["id"], caso)
        return [caso for id_caso, caso in candidatos.items() if id_caso not in ya_lleva]

    # ---- pintado ---------------------------------------------------------

    def _pintar_disponibles(self, companero):
        for hijo in self._marco_de_disponibles.winfo_children():
            hijo.destroy()
        self._marcados = {}

        ttk.Label(
            self._marco_de_disponibles, text="Casos que se le pueden asignar",
            style="Seccion.TLabel",
        ).pack(anchor="w")

        casos = self._casos_disponibles(companero)
        if not casos:
            ttk.Label(
                self._marco_de_disponibles,
                text=(
                    "No hay casos que asignar a este compañero."
                    if companero is not None
                    else "Elija un compañero arriba."
                ),
                style="Secundario.TLabel",
            ).pack(anchor="w", pady=(4, 0))
            return

        ttk.Label(
            self._marco_de_disponibles,
            text=f"{len(casos)} casos · marque los que le toquen y pulse «Asignar»",
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(0, 4))

        lista = self._lista_que_rueda(self._marco_de_disponibles)
        for caso in sorted(
            casos, key=lambda c: (c["fecha_viaje"] is None, c["fecha_viaje"] or "")
        ):
            marcado = tk.BooleanVar(value=False)
            self._marcados[caso["id"]] = marcado
            fondo = AMBAR_DE_FILA if caso["fecha_viaje"] is None else "#FFFFFF"
            fila = tk.Frame(lista, background=fondo, highlightthickness=1, highlightbackground=BORDE)
            fila.pack(fill="x", pady=1)
            tk.Checkbutton(
                fila,
                text=self._texto_del_caso(caso),
                variable=marcado,
                background=fondo, activebackground=fondo, anchor="w",
                font=LETRA_PEQUENA, padx=6, pady=4,
            ).pack(fill="x")

        ttk.Button(
            self._marco_de_disponibles, text="Asignar los marcados",
            command=self._asignar_los_marcados,
        ).pack(anchor="w", pady=6)

    def _pintar_asignados(self, companero):
        for hijo in self._marco_de_asignados.winfo_children():
            hijo.destroy()
        ttk.Label(
            self._marco_de_asignados, text="Casos que ya lleva", style="Seccion.TLabel"
        ).pack(anchor="w")

        if companero is None:
            return
        casos = casos_asignados(self.conexion, companero["id"])
        if not casos:
            ttk.Label(
                self._marco_de_asignados,
                text="Todavía no lleva ninguno.",
                style="Secundario.TLabel",
            ).pack(anchor="w", pady=(4, 0))
            return

        ttk.Label(
            self._marco_de_asignados,
            text=f"{len(casos)} casos · el que viaja antes, primero",
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(0, 4))
        lista = self._lista_que_rueda(self._marco_de_asignados)
        for caso in casos:
            fondo = AMBAR_DE_FILA if caso["fecha_viaje"] is None else "#FFFFFF"
            fila = tk.Frame(lista, background=fondo, highlightthickness=1, highlightbackground=BORDE)
            fila.pack(fill="x", pady=1)
            tk.Label(
                fila, text=self._texto_del_caso(caso), background=fondo, anchor="w",
                padx=6, pady=5, font=LETRA_PEQUENA, takefocus=0,
            ).pack(side="left")
            ttk.Button(
                fila, text="Retirar",
                command=lambda c=caso: self._retirar(companero, c),
            ).pack(side="right", padx=4, pady=2)

    def _lista_que_rueda(self, padre):
        """Un contenedor con barra vertical. Las listas pueden ser largas."""
        marco = ttk.Frame(padre)
        marco.pack(fill="both", expand=True)
        lienzo = tk.Canvas(marco, highlightthickness=0, background="#F7F7F8", takefocus=0)
        barra = ttk.Scrollbar(marco, orient="vertical", command=lienzo.yview)
        lienzo.configure(yscrollcommand=barra.set)
        lienzo.pack(side="left", fill="both", expand=True)
        barra.pack(side="right", fill="y")
        dentro = ttk.Frame(lienzo)
        ventana = lienzo.create_window((0, 0), window=dentro, anchor="nw")
        dentro.bind(
            "<Configure>", lambda evento: lienzo.configure(scrollregion=lienzo.bbox("all"))
        )
        lienzo.bind(
            "<Configure>", lambda evento: lienzo.itemconfigure(ventana, width=evento.width)
        )
        return dentro

    def _texto_del_caso(self, caso):
        """Una linea de caso: cuando viaja, que numero es y de que unidad."""
        fecha = caso.get("fecha_viaje") or "sin fecha"
        unidad = caso.get("unidad_nombre") or "unidad sin nombre"
        personas = caso.get("personas")
        cola = f" · {personas} persona(s)" if personas is not None else ""
        return f"{fecha}   {caso['numero_caso']}   {unidad}{cola}"

    def _pintar_resumen(self, companero):
        if companero is None:
            self._resumen.configure(text="")
            return
        casos = casos_asignados(self.conexion, companero["id"])
        self._resumen.configure(
            text=(
                f"«{companero['nombre']}» lleva {len(casos)} caso(s). El paquete "
                "sale con el PDF recortado de cada uno —solo sus hojas— y la hoja "
                "«Por verificar»: los seis pasos de «Preparación para las "
                "ordenanzas» con menú de Sí/No, y el número de caso y el MRN "
                "bloqueados."
            )
        )

    # ---- acciones --------------------------------------------------------

    def _avisar_del_espejo(self, aviso):
        """Lo que se hace cuando el `.xlsx` no se pudo reescribir: decirlo."""
        dialogos.advertir(
            "El Excel espejo no se pudo actualizar", aviso, parent=self
        )

    def _guardar(self, operacion):
        """Ejecuta una escritura y reescribe el espejo detras, siempre.

        Existe para que las cuatro acciones de esta pantalla no repitan cuatro
        veces la misma llamada con la misma carpeta y el mismo aviso: repetida
        cuatro veces, la quinta se olvida, y esa quinta es la que deja el Excel
        atrasado sin que nadie lo note.
        """
        return guardar_y_regenerar(
            self.conexion,
            operacion,
            carpeta_de_datos=self._carpeta_de_datos,
            avisar=self._avisar_del_espejo,
        ).guardado

    def _otros_que_ya_lo_llevan(self, ids_de_caso, companero):
        """Los casos de la lista que YA lleva vivos otra persona, por su numero.

        ⚠️ Esto **avisa**, no impide. Si un caso puede llevarlo mas de una persona
        a la vez es una decision del dueno que no esta tomada: `DECISIONES.md` no
        la trata y `datos/asignaciones.py` lo dice de si mismo. Mientras no se
        tome, se sigue el mismo criterio que la regla del mes cruzado
        (`DECISIONES.md`, P-2): **aviso en la pantalla, no restriccion del motor**.

        Avisar no es decorativo aqui: desde la auditoria final de QA, el Excel del
        segundo companero YA NO pisa la propuesta del primero —se rechaza fila a
        fila y va a la lista de descartados (`datos/propuestas.py`)—, asi que
        asignar dos veces el mismo caso sin saberlo lleva a una ronda que vuelve
        entera descartada.
        """
        repetidos = []
        for id_caso in ids_de_caso:
            otros = [
                quien["nombre"]
                for quien in companeros_del_caso(self.conexion, id_caso)
                if quien["companero_id"] != companero["id"]
            ]
            if otros:
                repetidos.append((self._numero_del_caso(id_caso), otros))
        return repetidos

    def _numero_del_caso(self, id_caso):
        """El numero que Miguel reconoce, para poder nombrarlo en un aviso.

        Pasa por `datos.repositorio` y no escribe SQL aqui: la pantalla no habla
        con el motor. Es lo que permite ademas que `pruebas/auditoria_sql.py`
        siga dictaminando todas las llamadas leyendo solo la capa de datos.
        """
        caso = leer_caso_por_id(self.conexion, id_caso)
        return caso["numero_caso"] if caso is not None else str(id_caso)

    def _confirmar_los_repetidos(self, repetidos):
        """Ensena quien lleva ya cada caso y deja que Miguel decida. Devuelve si sigue."""
        detalle = "\n".join(
            f"  · {numero}: lo lleva {', '.join(nombres)}" for numero, nombres in repetidos
        )
        return dialogos.preguntar_si_o_no(
            "Ese caso ya lo lleva otra persona",
            f"{len(repetidos)} de los casos marcados ya están asignados y vivos "
            f"con otro compañero:\n\n{detalle}\n\n"
            "Si los dos devuelven su Excel, el segundo NO pisará lo que escribió "
            "el primero: sus filas se descartarán y usted tendrá que decidir cuál "
            "vale.\n\n¿Asignarlos igualmente?",
            parent=self,
        )

    def _asignar_los_marcados(self):
        companero = self._companero_elegido()
        if companero is None:
            return
        marcados = [id_caso for id_caso, marca in self._marcados.items() if marca.get()]
        if not marcados:
            dialogos.informar(
                "No hay nada marcado",
                "Marque al menos un caso de la lista de la izquierda.",
                parent=self,
            )
            return
        repetidos = self._otros_que_ya_lo_llevan(marcados, companero)
        if repetidos and not self._confirmar_los_repetidos(repetidos):
            return
        try:
            self._guardar(
                lambda: [
                    asignar_caso(self.conexion, id_caso, companero["id"])
                    for id_caso in marcados
                ]
            )
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo asignar", str(causa), parent=self)
        self.refrescar_listas()

    def _retirar(self, companero, caso):
        if not dialogos.preguntar_si_o_no(
            "Retirar el caso",
            f"El caso {caso['numero_caso']} deja de estar asignado a "
            f"«{companero['nombre']}».\n\nLa asignación NO se borra: queda "
            "registrada como retirada, con su fecha.\n\n¿Retirarlo?",
            parent=self,
        ):
            return
        self._guardar(lambda: retirar_caso(self.conexion, caso["id"], companero["id"]))
        self.refrescar_listas()

    def _generar_paquete(self):
        companero = self._companero_elegido()
        if companero is None:
            dialogos.informar(
                "Elija un compañero", "Primero elija a quién va el paquete.", parent=self
            )
            return
        carpeta = filedialog.askdirectory(
            title="¿Dónde se guarda la carpeta del paquete?", parent=self
        )
        if not carpeta:
            return
        try:
            resultado = exportar_paquete(self.conexion, companero["id"], carpeta)
        except (ErrorDeValidacion, ErrorDeExportacion) as causa:
            dialogos.avisar_de_un_error("No se pudo generar el paquete", str(causa), parent=self)
            return

        mensaje = (
            f"Paquete de «{companero['nombre']}» listo en:\n{resultado.carpeta}\n\n"
            f"  · {len(resultado.casos)} caso(s) en la hoja «Por verificar».\n"
            f"  · {len(resultado.pdf_escritos)} PDF recortado(s), solo con las hojas "
            "de cada caso."
        )
        if resultado.avisos:
            mensaje += "\n\n" + "\n".join(resultado.avisos)
        dialogos.informar("Paquete generado", mensaje, parent=self)
        # Abrir la carpeta es lo siguiente que se hace SIEMPRE —hay que mandarla—,
        # y un fallo al abrirla no puede tapar que el paquete sí se escribió.
        try:
            os.startfile(str(resultado.carpeta))
        except OSError:
            pass

    def _cargar_devuelto(self):
        """Carga la hoja «Por verificar» tal como el programa la genero."""
        companero = self._companero_elegido()
        if companero is None:
            dialogos.informar(
                "Elija un compañero",
                "Primero elija de quién viene el Excel: su nombre queda pegado a "
                "cada propuesta que traiga.",
                parent=self,
            )
            return
        ruta = filedialog.askopenfilename(
            title="Elija el Excel que devolvió el compañero",
            filetypes=[("Libros de Excel", "*.xlsx")],
            parent=self,
        )
        if not ruta:
            return
        try:
            resultado = self._guardar(
                lambda: reconciliar_excel(self.conexion, ruta, companero["id"])
            )
        except (ErrorDeLectura, ErrorDeValidacion) as causa:
            dialogos.avisar_de_un_error("No se pudo cargar el Excel", str(causa), parent=self)
            return
        self._ensenar_la_vuelta(resultado)

    def _importar_libre(self):
        """Abre un Excel cualquiera y pide el mapeo de columnas antes de nada."""
        companero = self._companero_elegido()
        if companero is None:
            dialogos.informar(
                "Elija un compañero",
                "Primero elija de quién viene el Excel.",
                parent=self,
            )
            return
        ruta = filedialog.askopenfilename(
            title="Elija el Excel que quiere importar",
            filetypes=[("Libros de Excel", "*.xlsx")],
            parent=self,
        )
        if not ruta:
            return
        from interfaz.mapeo import VentanaDeMapeo

        ventana = VentanaDeMapeo(
            self.winfo_toplevel(),
            self.conexion,
            ruta,
            companero,
            carpeta_de_datos=self._carpeta_de_datos,
        )
        self.wait_window(ventana)
        if ventana.resultado is not None:
            self._ensenar_la_vuelta(ventana.resultado)

    def _ver_descartes_guardados(self):
        """Abre la lista de todo lo que volvió y no entró, de todas las cargas."""
        from datos.descartadas import contar_filas_descartadas

        if not contar_filas_descartadas(self.conexion):
            dialogos.informar(
                "No hay ningún descarte guardado",
                "Todavía no se ha descartado ninguna fila de ningún Excel devuelto. "
                "Cuando pase, cada una queda aquí con su número de fila y su motivo, "
                "y se queda aunque se cierre el programa.",
                parent=self,
            )
            return
        VentanaDeDescartados.de_lo_guardado(self.winfo_toplevel(), self.conexion)

    def _ensenar_la_vuelta(self, resultado):
        """El resumen con las tres cifras y, si hay descartes, su lista entera."""
        mensaje = resumen_de_la_vuelta(resultado)
        if resultado.avisos:
            mensaje += "\n\n" + "\n".join(resultado.avisos)
        dialogos.informar("Excel cargado", mensaje, parent=self)
        if resultado.descartadas:
            VentanaDeDescartados(self.winfo_toplevel(), resultado)
        self.refrescar_listas()
