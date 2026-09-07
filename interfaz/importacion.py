"""La ventana que ensena como va la tanda, y el hilo que la mueve sin congelarla.

El dueno lo dijo asi: «el programa se para». No se paraba: estaba leyendo. Toda la
importacion corria dentro del `command` de un boton, o sea dentro del bucle de
eventos de Tk, y mientras una funcion no devuelve, Tk no repinta ni atiende un
clic. Con un PDF de una hoja son 8 segundos de ventana muerta; con 500 documentos
son horas de una ventana que Windows marca «no responde».

**Como se arregla, y por que asi.** La lectura —rasterizar y pasar el OCR, que es
lo lento— se va a un hilo aparte. El guardado en SQLite se queda en el hilo de la
ventana, sin excepcion: `sqlite3` prohibe usar una conexion desde un hilo distinto
del que la creo, y desactivar esa comprobacion cambiaria un cuelgue que se ve por
una corrupcion que no se ve. El hilo lector deja lo que lee en una cola; el hilo de
la ventana la vacia con `after`, guarda, y repinta.

La cola tiene tope a proposito. Sin tope, el lector correria por delante y las
paginas leidas de 500 documentos se irian acumulando en memoria; con tope, cuando
la cola esta llena el lector se espera. En esta tanda el lector nunca va a ir por
delante —leer tarda segundos y guardar milisegundos—, y justo por eso el tope no
cuesta nada y cubre el dia que deje de ser verdad.

**Cancelar para de verdad y no deshace nada.** La bandera se mira entre documento y
documento, no a mitad de uno: parar a mitad de un PDF dejaria unas paginas suyas
dentro y otras fuera, sin forma de saber cuales. Lo ya guardado se queda guardado,
que es lo que el dueno pidio, y el resumen dice por donde se quedo.
"""

import queue
import threading
import tkinter as tk
from tkinter import ttk

from importacion.tanda import ResumenDeLaTanda, cerrar_la_tanda, guardar_un_pdf, leer_un_pdf
from interfaz.tema import LETRA_PEQUENA, LETRA_SECCION

# Cada cuanto vacia la cola el hilo de la ventana. 120 ms es mas que suficiente
# para que las cifras parezcan continuas —un documento tarda segundos— y lo
# bastante espaciado para no gastar el bucle de eventos en vueltas vacias.
MILISEGUNDOS_ENTRE_VUELTAS = 120

# Cuantas lecturas caben en la cola antes de que el lector tenga que esperar.
TOPE_DE_LA_COLA = 2


class VentanaDeProgreso(tk.Toplevel):
    """Cual va, cuantos quedan, que ha entrado, y un boton para parar.

    Ensena las cifras que cambian una decision y no una barra a secas: con 500
    documentos, «va por el 240» no dice si hay que quedarse mirando o irse a
    comer, y «240 · 0 casos» si.
    """

    def __init__(self, padre, total_de_documentos, al_cancelar):
        super().__init__(padre)
        self.title("Importando…")
        self.transient(padre)
        self.resizable(False, False)
        self.minsize(560, 0)
        self._al_cancelar = al_cancelar

        # El peso en la fila y la columna del `Toplevel` es lo que hace que el
        # `sticky="nsew"` del marco signifique algo. Sin el, `minsize(560, 0)`
        # ensancha la VENTANA pero no el marco: el contenido se queda en su ancho
        # pedido y el resto es una franja gris que no pinta nadie. Se veia peor al
        # principio —el marco vacio mide 135 px de 560— y seguia viendose despues,
        # a 394 de 560. Y los segundos del principio son justo los que cargan el
        # motor de OCR, o sea los que el dueno describio como «se para al leer un
        # pdf»: una ventana a medio pintar en ese momento se lee como un cuelgue.
        self.rowconfigure(0, weight=1)
        self.columnconfigure(0, weight=1)

        marco = ttk.Frame(self, padding=16)
        marco.grid(sticky="nsew")
        marco.columnconfigure(0, weight=1)

        self._cuenta = ttk.Label(marco, font=LETRA_SECCION)
        self._cuenta.grid(row=0, column=0, sticky="w")
        self._documento = ttk.Label(
            marco, style="Secundario.TLabel", wraplength=520, justify="left"
        )
        self._documento.grid(row=1, column=0, sticky="w", pady=(2, 8))

        self._barra = ttk.Progressbar(
            marco, orient="horizontal", mode="determinate", maximum=max(total_de_documentos, 1)
        )
        self._barra.grid(row=2, column=0, sticky="ew")

        self._cifras = ttk.Label(marco, font=LETRA_PEQUENA)
        self._cifras.grid(row=3, column=0, sticky="w", pady=(8, 8))

        self._boton = ttk.Button(marco, text="Cancelar", command=self._cancelar)
        self._boton.grid(row=4, column=0, sticky="e")

        # Cerrar con la X hace lo mismo que Cancelar y NO cierra la ventana. Cerrarla
        # dejaria el hilo leyendo detras, sin nada que ensene como va ni forma de
        # pararlo: una tanda invisible es peor que una tanda que tarda.
        self.protocol("WM_DELETE_WINDOW", self._cancelar)
        # `grab_set` impide tocar la ventana de detras mientras se importa. No es
        # para molestar: la pantalla de inicio lee la misma base que la tanda esta
        # escribiendo, y abrir un caso a mitad ensenaria cifras a medio hacer.
        self.grab_set()

    def _cancelar(self):
        self._boton.configure(state="disabled", text="Cancelando…")
        self._al_cancelar()

    def poner(self, resumen, nombre_del_documento):
        """Repinta las tres lineas y la barra con lo que hay ahora mismo."""
        self._cuenta.configure(
            text=f"Documento {resumen.documentos} de {resumen.total_de_documentos}"
        )
        self._documento.configure(text=nombre_del_documento)
        self._barra.configure(value=resumen.documentos)
        self._cifras.configure(
            text=(
                f"{resumen.casos} casos · {resumen.personas} personas · "
                f"{resumen.pendientes} sin número de caso · "
                f"{resumen.duplicados} duplicados · "
                f"{len(resumen.fallidos)} documentos con fallo"
            )
        )


class TandaEnMarcha:
    """Coordina el hilo lector, el guardado y la ventana de progreso.

    No hereda de nada de Tk a proposito: la ventana es una cosa que esta clase
    usa, no lo que esta clase es. Asi el dia que haga falta correr una tanda sin
    ventana —desde una prueba, o desde una tarea programada— lo unico que sobra es
    la ventana.
    """

    def __init__(
        self, raiz, conexion, rutas, dar_motor, carpeta_de_datos=None, al_terminar=None,
        avisar=None,
    ):
        self.raiz = raiz
        self.conexion = conexion
        self.rutas = list(rutas)
        self._dar_motor = dar_motor
        self._carpeta_de_datos = carpeta_de_datos
        self._al_terminar = al_terminar
        self._avisar = avisar
        self.resumen = ResumenDeLaTanda(len(self.rutas))
        self._cola = queue.Queue(maxsize=TOPE_DE_LA_COLA)
        self._cancelada = threading.Event()
        self._ventana = VentanaDeProgreso(raiz, len(self.rutas), self.cancelar)
        self._hilo = threading.Thread(target=self._leer_todo, daemon=True)

    def arrancar(self):
        """Lanza el hilo lector y programa la primera vuelta de la ventana."""
        self._hilo.start()
        self.raiz.after(MILISEGUNDOS_ENTRE_VUELTAS, self._vaciar_la_cola)

    def cancelar(self):
        """Pide parar. Lo ya guardado se queda; lo que se estaba leyendo se tira."""
        self._cancelada.set()

    # ---- el hilo que lee -------------------------------------------------

    def _leer_todo(self):
        """Lee documento a documento y va dejando cada lectura en la cola.

        Corre en el hilo aparte. Lo unico que hace con la base es NADA: ni la
        abre, ni la lee, ni la escribe. Todo lo que produce sale por la cola.

        El motor de OCR se pide aqui dentro y no antes de arrancar el hilo:
        cargarlo tarda segundos, y hacerlo en el hilo de la ventana dejaria la
        ventana de progreso congelada justo al aparecer, que es cuando mas parece
        que el programa se colgo.
        """
        try:
            motor = self._dar_motor()
        except Exception as causa:
            self._cola.put(("fallo del motor", f"{type(causa).__name__}: {causa}"))
            self._cola.put(("fin", None))
            return

        for ruta in self.rutas:
            if self._cancelada.is_set():
                break
            self._cola.put(("lectura", leer_un_pdf(ruta, motor)))
        self._cola.put(("fin", None))

    # ---- el hilo de la ventana -------------------------------------------

    def _vaciar_la_cola(self):
        """Guarda lo que haya llegado, repinta, y se vuelve a programar.

        Se saca con `get_nowait` en bucle y no una por vuelta: si por lo que sea
        llegaran dos lecturas entre dos vueltas, dejar una para la siguiente
        atrasaria la barra respecto a lo que ya esta guardado en la base.
        """
        while True:
            try:
                clase, contenido = self._cola.get_nowait()
            except queue.Empty:
                break
            if clase == "fin":
                self._terminar()
                return
            if clase == "fallo del motor":
                self._terminar(fallo=contenido)
                return
            resultado = guardar_un_pdf(self.conexion, contenido)
            self.resumen.anotar(resultado)
            self._ventana.poner(self.resumen, resultado.ruta.name)
        self.raiz.after(MILISEGUNDOS_ENTRE_VUELTAS, self._vaciar_la_cola)

    def _terminar(self, fallo=None):
        """Cierra la tanda: regenera el espejo, cierra la ventana y avisa.

        El espejo se regenera SIEMPRE, tambien cuando se cancelo y tambien cuando
        el motor no arranco. Es lo unico que impide que una tanda a medias deje la
        base al dia y el Excel atrasado.
        """
        self.resumen.cancelada = self._cancelada.is_set()
        if fallo is not None:
            self.resumen.fallidos.append(("el lector de OCR no arrancó", fallo))
        cerrar_la_tanda(
            self.conexion,
            carpeta_de_datos=self._carpeta_de_datos,
            avisar=self._avisar if self._avisar is not None else (lambda aviso: None),
        )
        self._ventana.grab_release()
        self._ventana.destroy()
        if self._al_terminar is not None:
            self._al_terminar(self.resumen)
