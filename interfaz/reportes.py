"""La pantalla de reportes: el período, los números en pantalla, y el histórico.

**Los números se ven en la pantalla antes de generar ningún archivo.** No es un
adorno: un reporte que solo existe dentro de un `.xlsx` obliga a abrir Excel para
saber si dice algo, y lo que no se mira no se corrige. Aquí se lee el resultado y
después se decide si hace falta el archivo.

Lo que se ve en pantalla y lo que sale en los dos archivos **es el mismo
documento**, contado una sola vez por `reportes/documento.py`. No hay ningún
camino por el que la pantalla diga un número y el Excel diga otro.

**El histórico vive aquí** porque es donde tiene sentido: los casos archivados no
son trabajo pendiente —salieron de las listas a propósito— y lo que se hace con
ellos es contarlos o devolverlos, que son las dos cosas de esta pantalla.
"""

from datetime import date

import tkinter as tk
from tkinter import ttk

from datos.archivo import casos_archivados, desarchivar_caso
from espejo.escritura import guardar_y_regenerar
from interfaz import dialogos
from interfaz.tema import BORDE, LETRA_PEQUENA
from reportes.generacion import generar_reporte
from reportes.periodo import (
    ErrorDePeriodo,
    periodo,
    periodo_de_los_ultimos_dias,
    periodo_del_mes,
    texto_del_periodo,
)

_FORMATO_DE_MARCA_DE_TIEMPO = "%Y-%m-%d %H:%M:%S"


def _mes_anterior(hoy):
    """El año y el mes del mes de antes, sin restar días ni contar bisiestos."""
    return (hoy.year - 1, 12) if hoy.month == 1 else (hoy.year, hoy.month - 1)


class PantallaDeReportes(ttk.Frame):
    """Elegir un período, ver sus números, generar los dos archivos y el histórico."""

    _ATAJOS = ("<Control-Home>",)

    def __init__(self, padre, conexion, al_volver, carpeta_de_datos=None, hoy=None):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self._al_volver = al_volver
        self._carpeta_de_datos = carpeta_de_datos
        self.hoy = hoy or date.today()
        self.periodo = periodo_del_mes(self.hoy.year, self.hoy.month)

        self.rowconfigure(3, weight=1)
        self.columnconfigure(0, weight=1)
        self.columnconfigure(1, weight=1)
        self._construir_barra()
        self._construir_selector()
        self._resultado = ttk.Frame(self)
        self._resultado.grid(row=3, column=0, sticky="nsew", pady=8, padx=(0, 6))
        self._historico = ttk.Frame(self)
        self._historico.grid(row=3, column=1, sticky="nsew", pady=8, padx=(6, 0))

        self.atar_atajos()
        self.refrescar()

    def atar_atajos(self):
        """Ata los atajos al toplevel. La ventana la llama cada vez que la enseña."""
        self.winfo_toplevel().bind("<Control-Home>", lambda evento: self._volver())

    def soltar_atajos(self):
        """Quita los atajos de esta pantalla. Se llama al cambiar de pantalla."""
        raiz = self.winfo_toplevel()
        for atajo in self._ATAJOS:
            raiz.unbind(atajo)

    # ---- barra y selector de período -------------------------------------

    def _construir_barra(self):
        barra = ttk.Frame(self)
        barra.grid(row=0, column=0, columnspan=2, sticky="ew")
        barra.columnconfigure(0, weight=1)
        ttk.Label(barra, text="Fichas — Reportes", style="Titulo.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        ttk.Button(
            barra, text="Volver al inicio  Ctrl+Inicio", command=self._volver
        ).grid(row=0, column=1, padx=4)

    def _construir_selector(self):
        """Los tres períodos de siempre, y las dos fechas a mano para el resto."""
        selector = ttk.Frame(self)
        selector.grid(row=1, column=0, columnspan=2, sticky="ew", pady=(8, 0))

        atajos = (
            ("Este mes", lambda: periodo_del_mes(self.hoy.year, self.hoy.month)),
            ("Mes anterior", lambda: periodo_del_mes(*_mes_anterior(self.hoy))),
            ("Últimos 90 días", lambda: periodo_de_los_ultimos_dias(self.hoy, 90)),
        )
        for columna, (etiqueta, calcular) in enumerate(atajos):
            ttk.Button(
                selector, text=etiqueta,
                command=lambda calcular=calcular: self._poner_periodo(calcular()),
            ).grid(row=0, column=columna, padx=(0, 4))

        ttk.Label(selector, text="Desde").grid(row=0, column=3, padx=(16, 4))
        self._desde = ttk.Entry(selector, width=12)
        self._desde.grid(row=0, column=4)
        ttk.Label(selector, text="Hasta").grid(row=0, column=5, padx=(8, 4))
        self._hasta = ttk.Entry(selector, width=12)
        self._hasta.grid(row=0, column=6)
        ttk.Button(selector, text="Ver estos números", command=self._periodo_a_mano).grid(
            row=0, column=7, padx=8
        )
        ttk.Button(
            selector, text="Generar Excel y PDF", command=self.generar
        ).grid(row=0, column=8)

        self._titulo_del_periodo = ttk.Label(self, style="Seccion.TLabel")
        self._titulo_del_periodo.grid(row=2, column=0, columnspan=2, sticky="w", pady=(8, 0))

    def _poner_periodo(self, nuevo):
        """Cambia el período vigente y vuelve a contar."""
        self.periodo = nuevo
        self.refrescar()

    def _periodo_a_mano(self):
        """Toma las dos fechas escritas. Una fecha mal escrita se dice, no se adivina."""
        try:
            self._poner_periodo(periodo(self._desde.get().strip(), self._hasta.get().strip()))
        except ErrorDePeriodo as causa:
            dialogos.avisar_de_un_error("Ese período no vale", str(causa), parent=self)

    # ---- repintado -------------------------------------------------------

    def refrescar(self):
        """Vuelve a contar el período vigente y repinta los números y el histórico."""
        self._desde.delete(0, "end")
        self._desde.insert(0, self.periodo.desde)
        self._hasta.delete(0, "end")
        self._hasta.insert(0, self.periodo.hasta)
        self._titulo_del_periodo.configure(
            text=f"Período: {texto_del_periodo(self.periodo)}"
        )
        self._pintar_numeros()
        self._pintar_historico()

    def _documento_de_ahora(self):
        """El documento del período vigente, contado en este instante."""
        from reportes.documento import construir_documento

        return construir_documento(
            self.conexion, self.periodo, self._marca_de_tiempo()
        )

    def _marca_de_tiempo(self):
        """Cuándo se generó, en el mismo formato que usa todo el esquema."""
        from datetime import datetime

        return datetime.now().strftime(_FORMATO_DE_MARCA_DE_TIEMPO)

    def _pintar_numeros(self):
        """Las tres secciones del reporte, resumidas, con sus avisos delante."""
        for hijo in self._resultado.winfo_children():
            hijo.destroy()
        documento = self._documento_de_ahora()

        for aviso in documento.avisos:
            self._pintar_aviso(aviso)
        for seccion in documento.secciones:
            self._pintar_seccion(seccion)

    def _pintar_aviso(self, texto):
        """Un aviso del reporte, entero y sin recortar."""
        marco = tk.Frame(self._resultado, background="#FFF3C4")
        marco.pack(fill="x", pady=(0, 6))
        tk.Label(
            marco, text=texto, background="#FFF3C4", foreground="#3D2A00",
            font=LETRA_PEQUENA, anchor="w", justify="left", padx=10, pady=8,
            wraplength=620,
        ).pack(anchor="w", fill="x")

    def _pintar_seccion(self, seccion):
        """El título de una sección con su resumen, o sus filas si no tiene resumen.

        Las dos secciones de personas traen resumen —«N personas, de las cuales…»—
        y sus filas se leen en el Excel, que es donde caben. La sección de métricas
        no tiene resumen porque su resumen SON sus tres filas, y esas tres tienen
        que verse aquí: son el número que el pase pide que salga.
        """
        ttk.Label(self._resultado, text=seccion.titulo, style="Seccion.TLabel").pack(
            anchor="w", pady=(8, 2)
        )
        if seccion.resumen is not None:
            ttk.Label(
                self._resultado, text=seccion.resumen, style="Secundario.TLabel"
            ).pack(anchor="w")
            return
        for nombre, numero, como_se_cuenta in seccion.filas:
            self._pintar_metrica(nombre, numero, como_se_cuenta)

    def _pintar_metrica(self, nombre, numero, como_se_cuenta):
        """Una métrica con su número y, debajo, cómo se cuenta ese número.

        El «cómo se cuenta» va siempre pegado y nunca detrás de un clic: un número
        suelto sin su definición se lee como el que cada uno se imagina.
        """
        ttk.Label(
            self._resultado, text=f"{nombre}:  {numero}", font=LETRA_PEQUENA
        ).pack(anchor="w")
        ttk.Label(
            self._resultado, text=f"     {como_se_cuenta}", style="Secundario.TLabel",
            wraplength=600, justify="left",
        ).pack(anchor="w", pady=(0, 4))

    # ---- histórico -------------------------------------------------------

    def _pintar_historico(self):
        """Los casos archivados, el último primero, cada uno con su vuelta atrás."""
        for hijo in self._historico.winfo_children():
            hijo.destroy()
        archivados = casos_archivados(self.conexion)
        ttk.Label(self._historico, text="Histórico — casos archivados", style="Seccion.TLabel").pack(anchor="w")
        ttk.Label(
            self._historico,
            text=(
                f"{len(archivados)} casos archivados · siguen contando en los reportes"
                if archivados else "Todavía no hay ningún caso archivado."
            ),
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(0, 4))
        for caso in archivados:
            self._pintar_archivado(caso)

    def _pintar_archivado(self, caso):
        """Una fila del histórico con su botón de desarchivar."""
        fila = tk.Frame(self._historico, background="#FFFFFF", highlightthickness=1,
                        highlightbackground=BORDE)
        fila.pack(fill="x", pady=1)
        tk.Label(
            fila,
            text=(
                f"{caso['numero_caso']}   viajaba el {caso['fecha_viaje'] or 'sin fecha'}"
                f"   ·   {caso['personas']} personas"
                f"   ·   {caso['personas_que_no_viajaron']} no pudieron viajar"
                f"   ·   archivado el {caso['fecha_archivado']}"
            ),
            background="#FFFFFF", anchor="w", padx=8, pady=5, font=LETRA_PEQUENA,
        ).pack(side="left")
        ttk.Button(
            fila, text="Desarchivar",
            command=lambda caso=caso: self._desarchivar(caso),
        ).pack(side="right", padx=4, pady=2)

    def _desarchivar(self, caso):
        """Devuelve el caso a las listas de trabajo, con el espejo al día."""
        if not dialogos.preguntar_si_o_no(
            "Devolver el caso a las listas",
            f"El caso {caso['numero_caso']} volverá a las listas de trabajo y al "
            "bloque de viajes próximos. Lo anotado de quién viajó y quién no se "
            "conserva. ¿Seguir?",
            parent=self,
        ):
            return
        guardar_y_regenerar(
            self.conexion,
            lambda: desarchivar_caso(self.conexion, caso["id"]),
            carpeta_de_datos=self._carpeta_de_datos,
            avisar=lambda aviso: dialogos.advertir(
                "El Excel espejo no se pudo actualizar", aviso, parent=self
            ),
        )
        self.refrescar()

    # ---- generar ---------------------------------------------------------

    def generar(self):
        """Escribe el `.xlsx` y el `.pdf` del período vigente y dice dónde quedaron."""
        resultado = generar_reporte(
            self.conexion, self.periodo, self._marca_de_tiempo(),
            carpeta_de_datos=self._carpeta_de_datos,
            avisar=lambda aviso: dialogos.advertir(
                "El reporte no se pudo escribir", aviso, parent=self
            ),
        )
        escritos = [
            str(archivo.ruta)
            for archivo in (resultado.excel, resultado.pdf)
            if archivo.escrito
        ]
        if escritos:
            dialogos.informar(
                "Reporte generado",
                "Quedaron escritos:\n\n" + "\n".join(escritos),
                parent=self,
            )
        self.refrescar()
        return resultado

    def _volver(self, evento=None):
        self._al_volver()
        return "break"
