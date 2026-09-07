"""La pantalla de inicio: lo que viaja pronto, el mes, y la cola de trabajo.

Se lee **de arriba abajo en orden de dano**. Nada de lo importante esta detras de
una pestana ni de un clic, porque —lo dice la propia FASE 5— si hay que dar un
clic para enterarse, algun dia no se da ese clic.

    menu de iconos │ titulo, fecha y la campana de avisos
                   │ UNA franja de avisos, de una linea, cerrable
                   │ los cuatro contadores
                   │ 1.º viajan pronto Y sin resolver ....... franja roja
                   │ 2.º viajan pronto y completos ......... mismo bloque
                   │ 3.º sin fecha de viaje ................ ambar
                   │ el mes (con los archivados)  │  las cuatro acciones
                   │                              │  el equipo por agente
                   │                              │  la cola de trabajo

**El bloque rojo no se dibuja vacio**: cuando no hay nada que viaje pronto se
sustituye por uno verde con la frase en espanol. Una franja roja con nada debajo
ensena a ignorar el rojo, y la unica defensa de este programa es que ese rojo
signifique siempre algo.

**«Hoy» se relee, no se congela.** Miguel deja el programa abierto dias. Sin
releer, el bloque de «proximos 7 dias» sigue contando desde el dia que se abrio, y
eso es exactamente la mentira que esta pantalla existe para no decir. Se relee con
F5, al volver de la pantalla de correccion, y con un temporizador que comprueba el
cambio de dia.

**Lo que se le anadio el 2026-09-03, y de que frase del dueno sale cada cosa.**
No se rehizo la pantalla: se le anadieron funciones, y eso lo corrigio el dueno en
mitad del pase —*«Ojo, no te envié a reconstruir todo, te envié a agregar
funciones»*—.

  · el calendario ensena los archivados marcados «archivado»
        *«Lo que archivo debe verse en el calendario, debe decir archivado.»*
  · UNA franja de avisos, de una linea, cerrable con boton y con `Esc`
        *«No pongas tanto texto» · «que se pueda cerrar el texto.»*
  · el menu de iconos y las cuatro acciones con sus atajos
        *«los botones que funcionan, el menú de iconos que funciona.»*
  · el equipo por agente, con «Generar paquete» al lado
  · `<TouchpadScroll>` ademas de `<MouseWheel>` en la cola de trabajo
        *«le doy para abajo con el trackpad de mi laptop y no baja.»*
  · un tope de filas por lista, que es lo que la puso 4,5 veces mas rapida
        *«Debe ser eficiente, se está poniendo súper lento al abrir.»*

**Y lo que ya NO hace falta advertir.** Hasta el 2026-09-03 aqui decia que
`casos_en_riesgo` devolvia lo mismo que `casos_que_viajan_pronto`, porque ningun
valor de `estado_recomendacion` significaba «lista». El dueno nombro los dos que
faltaban —«Sí, completa» / «No está completa»— y `datos/estados.py` ya trae
`ESTADOS_QUE_RESUELVEN = ("completa",)`: las dos listas se separan solas, y el
bloque de los completos deja de salir vacio.
"""

from datetime import date

import tkinter as tk
from tkinter import filedialog, ttk

from datos.avisos import motivos_de_aviso
from datos.calendario import DIAS_DE_LA_VENTANA, casos_en_riesgo, casos_que_viajan_pronto
from datos.contactos import ultimo_contacto_por_caso
from datos.equipo import contadores_del_panel, resumen_del_equipo
from datos.pendientes import casos_pendientes_de_verificar
from interfaz import dialogos
from interfaz.avisos import Aviso, FranjaDeAvisos
from interfaz.desplazamiento import atar_desplazamiento
from interfaz.documentos import CajonDeDocumentos, acciones_del_panel
from interfaz.etiquetas import marca_de_duplicado, numero_de_caso_visible
from interfaz.lista_en_lienzo import ListaEnLienzo, Renglon
from interfaz.menu_de_iconos import MenuDeIconos, secciones_de_la_ventana
from interfaz.mes import MESES, VistaDeMes
from interfaz.seguimiento import hay_que_volver_a_llamar, resumen_del_ultimo_contacto
from interfaz.tema import (
    AMBAR_DE_FILA,
    AMBAR_SIN_FECHA,
    BORDE,
    FONDO,
    LETRA_PEQUENA,
    LETRA_SECCION,
    LETRA_TITULO,
    ROJO_DE_FILA,
    ROJO_SOLIDO,
    ROJO_VENCIDO,
    TEXTO_SECUNDARIO,
    VERDE_RESUELTO,
)

# Cada cuanto se comprueba si cambio el dia. Un minuto: lo suficiente para que la
# cuenta atras nunca este mas de un minuto vieja, y lo bastante barato para que no
# se note. La comprobacion NO repinta si el dia no cambio.
MILISEGUNDOS_ENTRE_COMPROBACIONES_DE_FECHA = 60_000

# Cuantas filas se dibujan como maximo en cada lista de esta pantalla.
#
# **Esto es lo que arregla «se está poniendo súper lento al abrir toda la
# interfaz», y no es una opinion: esta medido.** Con 300 documentos inventados en
# la base, en esta maquina:
#
#     abrir el inicio ......... 7 325 ms  (mediana de 5)
#     widgets creados ......... 1 703
#       de la lista de pendientes ... 904   (300 casos × 3 widgets)
#       del bloque de arriba ........ 562   (134 filas × 4 etiquetas)
#     consultas SQL ........... 7
#
# Siete consultas no tardan siete segundos: **el coste es crear widgets de Tk**,
# uno por caso y sin tope. La base de Miguel crece sola, asi que esto empeora cada
# mes. Con el tope, lo que no cabe se cuenta —«y N más»— en vez de dibujarse: es
# lo mismo que ya hacia el calendario con «+2 casos más», y lo que el mockup v2
# pide para las tarjetas.
#
# Ninguna lista queda escondida: el numero completo sigue en la franja de arriba y
# en los contadores, y la lista entera esta en su pantalla.
FILAS_VISIBLES_POR_LISTA = 8

# Los cuatro contadores del mockup v2, con SUS rotulos y partidos en dos lineas
# como alli. Lo de las dos lineas no es estetica: medido a 1100 px de ancho, los
# cuatro rotulos en una sola linea pedian 1 092 px y, con el menu de iconos al
# lado, la fila se salia de la ventana. Partidos caben los cuatro.
ROTULOS_DE_LOS_CONTADORES = (
    ("personas_por_viajar", "Personas\npor viajar"),
    ("documentos_completos", "Con la recomendación\ncompleta"),
    ("hojas_sin_devolver", "Hojas sin\ndevolver"),
    ("personas_que_viajaron_sin_verificar", "Viajaron sin\nverificar"),
)

# Los que se pintan en negrita: el que cuenta el dano ya hecho. Los demas son
# contexto tranquilo.
RESALTADOS = ("personas_que_viajaron_sin_verificar",)

# Lo que ocupa la fila de contadores dibujada. Son dos lineas de rotulo con la
# cifra en grande al lado, que es como los parte el mockup v2.
ALTO_DE_LOS_CONTADORES = 34


def _fecha_larga(dia):
    """«lunes, 2 de septiembre de 2026», en espanol y sin depender del idioma del sistema."""
    nombres = ("lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo")
    return f"{nombres[dia.weekday()]}, {dia.day} de {MESES[dia.month - 1]} de {dia.year}"


def _cuenta_atras(caso):
    """«−1 día VENCIDO», «6 días», «hoy», «sin fecha». SIEMPRE lleva una palabra.

    Un caso sin fecha devuelve «sin fecha» y no un número: poner «hoy» —que es lo
    que sale de contar días desde una fecha que no existe— seria decirle a Miguel
    que ese caso viaja hoy, que es exactamente lo que no se sabe. Nunca se le
    inventa una fecha a un caso que no la tiene.
    """
    dias = caso.get("dias_para_el_viaje")
    if dias is None:
        return "sin fecha"
    if dias < 0:
        return f"−{abs(dias)} día{'s' if abs(dias) != 1 else ''} · VENCIDO"
    if dias == 0:
        return "hoy"
    return f"{dias} día{'s' if dias != 1 else ''}"


def _palabra_del_estado(caso):
    """La marca TEXTUAL del criterio 4 de la FASE 5. Nunca es solo un color.

    Dice **«COMPLETA»** y no «resuelta»: son las palabras del dueno —*«Sí,
    completa»* / *«No está completa»*— y `DECISIONES.md` (2026-09-03) las adopta
    en lugar del nombre `resuelta` que se habia puesto horas antes. La pantalla
    tiene que hablar como habla el que la usa.
    """
    if caso.get("ya_viajo") and caso["recomendacion_sin_resolver"]:
        return "YA VIAJÓ · SIN COMPLETAR"
    if caso["recomendacion_sin_resolver"]:
        return (caso["estado_recomendacion"] or "sin revisar").upper().replace("_", " ")
    return "COMPLETA"


# Donde empieza cada una de las cuatro columnas de una fila del bloque de arriba,
# en pixeles desde su izquierda, y cuanto mide la fila. Salen de lo que ocupaban
# las cuatro etiquetas que habia antes: 18, 12 y 24 caracteres de ancho fijo con
# `padx=8`. Aqui hay que decirlo en pixeles porque en un lienzo la geometria se
# calcula, no la reparte un `grid`.
COLUMNA_DE_LA_CUENTA = 8
COLUMNA_DEL_CASO = 130
ANCHO_DEL_ESTADO = 175
ANCHO_DE_LAS_PERSONAS = 90
ALTO_DE_LA_FILA = 26


class FilaDeCaso(tk.Frame):
    """Una fila pulsable de caso, con su cuenta atrás y su palabra de estado.

    **El texto se dibuja en un lienzo; el marco sigue siendo un control.** Las
    cuatro columnas eran cuatro `Label`, y con las ocho filas de cada nivel del
    bloque salían decenas de ventanas del escritorio de Windows —unos 5 ms cada
    una, medido por el supervisor—. Dibujadas cuestan lo que una.

    El marco se queda porque **esta es la única lista del inicio que se recorre con
    el teclado**: tiene `takefocus`, se resalta al recibir el foco, e Intro nada
    más abrir lleva al caso que más corre sin tocar el ratón. Un lienzo no se
    tabula, así que cambiar también esto quitaría una función en vez de acelerarla.
    """

    def __init__(self, padre, caso, fondo, texto, al_abrir):
        super().__init__(
            padre, background=fondo, highlightthickness=2,
            highlightbackground=fondo, highlightcolor="#1A1D21", takefocus=1,
            height=ALTO_DE_LA_FILA,
        )
        self.caso = caso
        self.grid_propagate(False)
        self.columnconfigure(0, weight=1)
        self.rowconfigure(0, weight=1)

        self.lienzo = tk.Canvas(
            self, background=fondo, highlightthickness=0, takefocus=0,
            height=ALTO_DE_LA_FILA,
        )
        self.lienzo.grid(row=0, column=0, sticky="nsew")
        self._pintar(caso, fondo, texto)

        for control in (self, self.lienzo):
            control.bind("<Button-1>", lambda evento: al_abrir(caso))
        self.bind("<Return>", lambda evento: al_abrir(caso))
        self.bind("<FocusIn>", lambda evento: self.configure(highlightbackground="#1A1D21"))
        self.bind("<FocusOut>", lambda evento: self.configure(highlightbackground=fondo))
        self.lienzo.bind("<Configure>", lambda evento: self._pintar(caso, fondo, texto))

    def _pintar(self, caso, fondo, texto):
        """Las cuatro columnas de la fila, dibujadas donde estaban las etiquetas."""
        self.lienzo.delete("all")
        ancho = max(self.lienzo.winfo_width(), 1)
        medio = ALTO_DE_LA_FILA // 2
        self.lienzo.create_text(
            COLUMNA_DE_LA_CUENTA, medio, anchor="w", text=_cuenta_atras(caso),
            fill=texto, font=LETRA_PEQUENA,
        )
        unidad = caso.get("unidad_nombre") or "unidad sin nombre"
        hueco_del_caso = max(
            ancho - COLUMNA_DEL_CASO - ANCHO_DE_LAS_PERSONAS - ANCHO_DEL_ESTADO, 40
        )
        self.lienzo.create_text(
            COLUMNA_DEL_CASO, medio, anchor="w",
            text=(
                f"{numero_de_caso_visible(caso)}   {unidad} · "
                f"{caso['unidad_numero'] or 'sin número'}"
            ),
            fill=texto, font=LETRA_SECCION, width=hueco_del_caso,
        )
        self.lienzo.create_text(
            ancho - ANCHO_DEL_ESTADO - ANCHO_DE_LAS_PERSONAS, medio, anchor="w",
            text=f"{caso['personas']} persona{'s' if caso['personas'] != 1 else ''}",
            fill=texto, font=LETRA_PEQUENA,
        )
        self.lienzo.create_text(
            ancho - ANCHO_DEL_ESTADO, medio, anchor="w", text=_palabra_del_estado(caso),
            fill=texto, font=LETRA_PEQUENA, width=ANCHO_DEL_ESTADO,
        )


class PantallaDeInicio(ttk.Frame):
    """Lo que hay que atender hoy, sin dar un solo clic para enterarse."""

    def __init__(
        self,
        padre,
        conexion,
        al_abrir_caso,
        al_importar,
        al_ver_reportes=None,
        al_ver_companeros=None,
        al_asignar=None,
        al_ver_ilegibles=None,
        al_revisar=None,
        al_bajar_excel=None,
    ):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self._al_abrir_caso = al_abrir_caso
        self._al_importar = al_importar
        # `al_ver_reportes` admite None a proposito: las pruebas de esta pantalla
        # la construyen sin ventana de reportes, y un boton que no lleva a ningun
        # sitio es mejor no dibujarlo que dibujarlo muerto. Lo mismo vale para los
        # dos de la FASE 6.
        self._al_ver_reportes = al_ver_reportes
        self._al_ver_companeros = al_ver_companeros
        self._al_asignar = al_asignar
        # Admite None por lo mismo que los tres de arriba: las pruebas construyen
        # esta pantalla sin la ventana de no legibles, y un boton que no lleva a
        # ningun sitio es peor que ninguno.
        self._al_ver_ilegibles = al_ver_ilegibles
        self._al_revisar = al_revisar
        self._al_bajar_excel = al_bajar_excel
        self.hoy = date.today()
        self._atajos_atados = []
        self._proxima_comprobacion = None
        # Los dos temporizadores de esta pantalla, para poder cancelarlos al salir:
        # la vigilancia del cambio de día y lo que `refrescar` aplaza con `after(0)`.
        self._diferido = None

        # La columna 0 es el menu de iconos y no crece; las dos de contenido si.
        self.rowconfigure(4, weight=1)
        self.columnconfigure(1, weight=1)
        self.columnconfigure(2, weight=1)
        self._construir_el_menu()
        self._construir_barra()
        self._construir_la_franja()
        self._contadores = tk.Canvas(
            self, background=FONDO, highlightthickness=0, takefocus=0,
            height=ALTO_DE_LOS_CONTADORES,
        )
        self._contadores.grid(row=2, column=1, columnspan=2, sticky="ew", pady=(8, 0))
        self._contadores.bind("<Configure>", lambda evento: self._pintar_los_contadores())
        self._bloque_de_arriba = ttk.Frame(self)
        self._bloque_de_arriba.grid(row=3, column=1, columnspan=2, sticky="ew", pady=(8, 0))
        self._mes = VistaDeMes(self, conexion, al_abrir_caso, self.hoy)
        self._mes.grid(row=4, column=1, sticky="nsew", pady=8, padx=(0, 6))
        self._construir_la_derecha()

        self.atar_atajos()
        # Cancelar también al destruirse, y no solo en `soltar_atajos()`. La
        # ventana llama a `soltar_atajos()` al cambiar de pantalla, pero una
        # pantalla se puede destruir por otros caminos —cerrar la ventana entera—,
        # y entonces Tk borra el comando del temporizador y contesta «invalid
        # command name ..._pintar_lo_diferido» al dispararse. Medido: esa línea
        # salía 11 veces en la suite; con esto y con lo mismo en corrección, 0.
        self.bind("<Destroy>", self._al_destruirse, add="+")
        self.refrescar()

    def _al_destruirse(self, evento=None):
        """Cancela los dos temporizadores si esta pantalla —no un hijo— se destruye.

        Se comprueba el widget del evento porque `<Destroy>` sube desde cada uno de
        los controles de dentro, y sin el filtro esto correría decenas de veces.
        """
        if evento is not None and evento.widget is not self:
            return
        for atributo in ("_proxima_comprobacion", "_diferido"):
            identificador = getattr(self, atributo, None)
            if identificador is None:
                continue
            try:
                self.after_cancel(identificador)
            except tk.TclError:
                # El intérprete ya se estaba cerrando: no queda cola que tocar.
                pass
            setattr(self, atributo, None)

    def _construir_el_menu(self):
        """El menu de iconos, a la izquierda y de arriba abajo.

        Vive dentro del panel y no en la ventana. En el mockup el rail recorre la
        ventana entera; ponerlo ahi obliga a tocar la cabecera de correccion,
        reportes y archivar, y dos de esas las esta escribiendo otro programador
        ahora mismo. Desde aqui se navega igual, y las demas pantallas conservan su
        boton de volver. Es la unica diferencia de estructura con el mockup, y va
        dicha en el informe del pase.
        """
        self.menu = MenuDeIconos(
            self,
            secciones_de_la_ventana(
                al_inicio=self.refrescar,
                al_revisar=self._al_revisar,
                al_equipo=self._al_ver_companeros,
                al_historial=self._al_ver_reportes,
            ),
            activa="Panel",
        )
        self.menu.grid(row=0, column=0, rowspan=4, sticky="ns", padx=(0, 10))

    def _construir_la_franja(self):
        """El marco de avisos. Su hijo es UNO, y eso es lo que lo hace una sola."""
        self._marco_de_avisos = ttk.Frame(self)
        self._marco_de_avisos.grid(row=1, column=1, columnspan=2, sticky="ew", pady=(8, 0))
        self.franja = FranjaDeAvisos(
            self._marco_de_avisos, al_cambiar=self._al_cambiar_los_avisos
        )
        self.franja.pack(fill="x")

    def _construir_la_derecha(self):
        """La columna de la derecha: el equipo arriba y los pendientes debajo."""
        derecha = ttk.Frame(self)
        derecha.grid(row=4, column=2, sticky="nsew", pady=8, padx=(6, 0))
        derecha.rowconfigure(2, weight=1)
        derecha.columnconfigure(0, weight=1)

        # **Las cuatro acciones van APILADAS en esta columna, no en la barra de
        # arriba, y el motivo esta medido.** Con los cuatro rotulos del mockup en
        # una fila horizontal, la barra pedia **1 508 px** de ancho y la ventana
        # mide 1 100: los botones se cortaban por la derecha. Apilados en la
        # columna estrecha caben enteros, que es como los dibuja el mockup —«el
        # botón más largo ("Informe para la dirección (PDF)") cabe entero»—.
        self.cajon = CajonDeDocumentos(derecha, self.acciones)
        self.cajon.grid(row=0, column=0, sticky="ew", pady=(0, 8))

        # El equipo: un rótulo fijo y una lista dibujada. Los renglones no son
        # controles; el botón de «Generar paquete» sí, y es uno solo para toda la
        # lista —lleva siempre al mismo sitio— en vez de uno por agente.
        self._marco_del_equipo = ttk.Frame(derecha)
        self._marco_del_equipo.grid(row=1, column=0, sticky="ew", pady=(0, 8))
        self._marco_del_equipo.columnconfigure(0, weight=1)
        ttk.Label(self._marco_del_equipo, text="El equipo", style="Seccion.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        self._boton_del_equipo = ttk.Button(
            self._marco_del_equipo, text="Generar paquete", command=self._ir_a_asignar
        )
        self._lista_del_equipo = ListaEnLienzo(self._marco_del_equipo, fondo=FONDO)
        self._lista_del_equipo.grid(row=1, column=0, columnspan=2, sticky="ew")

        # La cola de trabajo va dentro de un lienzo que **oye la rueda y el
        # trackpad**. Es la queja del dueno —*«le doy para abajo con el trackpad de
        # mi laptop y no baja»*— y hasta hoy esta pantalla no ataba ninguno de los
        # dos eventos: medido, `grep` de `MouseWheel` sobre `interfaz/inicio.py`
        # daba 0 lineas. Es la unica zona del inicio que puede crecer mas que la
        # ventana, asi que es donde hace falta.
        self._lienzo_de_pendientes = tk.Canvas(
            derecha, background=FONDO, highlightthickness=0, takefocus=0
        )
        self._lienzo_de_pendientes.grid(row=2, column=0, sticky="nsew")
        barra = ttk.Scrollbar(
            derecha, orient="vertical", command=self._lienzo_de_pendientes.yview
        )
        barra.grid(row=2, column=1, sticky="ns")
        self._lienzo_de_pendientes.configure(yscrollcommand=barra.set)

        # Dentro del lienzo que rueda va un marco con los dos rótulos fijos y la
        # lista dibujada. Los rótulos son controles porque son dos y no cambian de
        # número; las filas, no, porque crecen con la base y son lo que costaba.
        self._marco_de_pendientes = ttk.Frame(self._lienzo_de_pendientes)
        self._marco_de_pendientes.columnconfigure(0, weight=1)
        ttk.Label(
            self._marco_de_pendientes, text="Pendientes de verificar",
            style="Seccion.TLabel",
        ).grid(row=0, column=0, sticky="w")
        self._cuantos_pendientes = ttk.Label(
            self._marco_de_pendientes, style="Secundario.TLabel"
        )
        self._cuantos_pendientes.grid(row=1, column=0, sticky="w", pady=(0, 4))
        self._lista_de_pendientes = ListaEnLienzo(
            self._marco_de_pendientes, al_pulsar=self._al_abrir_caso, fondo=FONDO
        )
        self._lista_de_pendientes.grid(row=2, column=0, sticky="ew")
        self._faltan_en_la_cola = ttk.Label(
            self._marco_de_pendientes, style="Secundario.TLabel"
        )
        self._faltan_en_la_cola.grid(row=3, column=0, sticky="w", pady=(2, 0))
        self._ventana_de_pendientes = self._lienzo_de_pendientes.create_window(
            (0, 0), window=self._marco_de_pendientes, anchor="nw"
        )
        self._marco_de_pendientes.bind("<Configure>", self._al_crecer_la_cola)
        self._lienzo_de_pendientes.bind("<Configure>", self._al_cambiar_el_ancho)
        self.desplazador = atar_desplazamiento(self._lienzo_de_pendientes)

    def _al_crecer_la_cola(self, evento=None):
        """Reajusta lo desplazable cuando la cola crece o mengua."""
        self._lienzo_de_pendientes.configure(
            scrollregion=self._lienzo_de_pendientes.bbox("all")
        )

    def _al_cambiar_el_ancho(self, evento):
        """La cola ocupa todo el ancho: nunca aparece una barra horizontal."""
        self._lienzo_de_pendientes.itemconfigure(
            self._ventana_de_pendientes, width=evento.width
        )

    # ---- barra superior --------------------------------------------------

    def _construir_barra(self):
        """El titulo y **las cuatro acciones del mockup**, en una sola fila.

        Sustituyen a los siete botones sueltos que habia. No es un rediseno: son
        las mismas funciones detras —importar, la vuelta del Excel del compañero,
        el informe y el espejo— con los rotulos y los atajos del mockup, que es lo
        que el dueno reconoce del programa viejo.

        Los que no eran ninguna de las cuatro —«Lo que no entró…» y «Compañeros…»—
        no se pierden: el primero cuelga del aviso de hojas ilegibles y el segundo
        del menu de iconos, en «Equipo».
        """
        barra = ttk.Frame(self)
        barra.grid(row=0, column=1, columnspan=2, sticky="ew")
        barra.columnconfigure(0, weight=1)
        self._titulo = ttk.Label(barra, style="Titulo.TLabel")
        self._titulo.grid(row=0, column=0, sticky="w")

        self.acciones = acciones_del_panel(
            al_cargar=self._importar,
            al_recibir_hoja=self._recibir_la_hoja,
            al_generar_informe=self._generar_el_informe,
            al_bajar_excel=self._bajar_el_excel,
        )
        # La carpeta entera sigue teniendo su boton, y va en la barra porque no es
        # una de las cuatro: el dueno habla de 500 documentos, y elegirlos de uno en
        # uno con Ctrl es justo el trabajo que este boton quita.
        ttk.Button(
            barra, text="Carpeta…  Ctrl+Mayús+O", command=self._importar_carpeta
        ).grid(row=0, column=1, padx=3)
        self._campana = ttk.Button(
            barra, text="", command=self._reabrir_los_avisos, style="TButton"
        )

    def _recibir_la_hoja(self):
        """`Ctrl+R`: la vuelta del Excel del compañero, que vive en Asignar casos."""
        if self._al_asignar is not None:
            self._al_asignar()
        return "break"

    def _generar_el_informe(self):
        """`Ctrl+I`: el informe para la dirección, que vive en Reportes."""
        if self._al_ver_reportes is not None:
            self._al_ver_reportes()
        return "break"

    def _bajar_el_excel(self):
        """`Ctrl+E`: reescribe el `.xlsx` espejo entero desde la base."""
        if self._al_bajar_excel is not None:
            self._al_bajar_excel()
        return "break"

    # `<Control-O>` con la O mayuscula es lo que Tk entrega cuando se pulsa
    # Ctrl+Mayus+O, asi que ese atajo es el de la carpeta y `<Control-o>` el de los
    # archivos sueltos.
    #
    # ⚠️ **`Ctrl+I` cambia de significado.** Antes abria el selector de PDF; en el
    # mockup es el informe, y se sigue el mockup porque es la especificacion. Lo que
    # `Ctrl+I` hacia antes es ahora `Ctrl+O`. Es memoria muscular de Miguel: queda
    # dicho en el informe del pase para que lo confirme el dueno.
    def atar_atajos(self):
        """Ata los atajos de esta pantalla al toplevel. La ventana la llama al enseñarla.

        Es **público y repetible**: desde que las pantallas se esconden en vez de
        destruirse, esto se llama cada vez que se vuelve al inicio. Se sueltan
        primero los que hubiera para que la lista de lo atado no crezca en cada
        visita —`raiz.bind` reemplaza, pero `soltar_atajos()` recorre esa lista—.

        Y se vuelve a poner en marcha la vigilancia del cambio de día, que
        `soltar_atajos()` cancela. **Sin esto, la segunda visita al inicio dejaría
        de mirar el reloj**: un programa abierto desde el lunes seguiría pintando
        el lunes el jueves, que es la avería que esta pantalla existe para evitar.
        """
        self.soltar_atajos()
        if self._proxima_comprobacion is None:
            self._vigilar_el_cambio_de_dia()
        raiz = self.winfo_toplevel()
        atados = [
            ("<F5>", lambda evento: self.refrescar()),
            ("<Escape>", lambda evento: self.franja.cerrar()),
            ("<Control-O>", lambda evento: self._importar_carpeta()),
        ]
        for tecla, nombre in self.menu.atajos_de_la_ventana():
            atados.append((tecla, lambda evento, n=nombre: self.menu.ir_a(n)))
        for accion in self.acciones:
            tecla = f"<Control-{accion.atajo.split('+')[-1].lower()}>"
            atados.append((tecla, lambda evento, a=accion: a.comando()))
        for tecla, funcion in atados:
            raiz.bind(tecla, funcion)
            self._atajos_atados.append(tecla)

    def soltar_atajos(self):
        """Quita los atajos de esta pantalla. Se llama al cambiar de pantalla.

        Se suelta la lista de lo que se ato de verdad, y no una tupla fija escrita
        aparte: con una tupla fija, anadir un atajo y olvidarse de anotarlo ahi lo
        deja vivo apuntando a una pantalla ya destruida.
        """
        raiz = self.winfo_toplevel()
        for atajo in self._atajos_atados:
            raiz.unbind(atajo)
        self._atajos_atados = []
        if self._proxima_comprobacion is not None:
            self.after_cancel(self._proxima_comprobacion)
            self._proxima_comprobacion = None
        if self._diferido is not None:
            self.after_cancel(self._diferido)
            self._diferido = None
        self.desplazador.soltar()

    def _importar(self):
        """Elige VARIOS PDF de una vez. Antes solo dejaba elegir uno.

        `askopenfilenames` —en plural— es lo unico que cambia respecto a lo que
        habia, y es lo que el dueno pedia: hasta hoy el dialogo era
        `askopenfilename`, en singular, y no habia forma de marcar dos archivos por
        mucho que se pulsara Ctrl.

        Devuelve una tupla, y vacia cuando se cancela. Se pasa entera a quien
        importa: decidir que hacer con una lista de rutas no es cosa de esta
        pantalla.
        """
        rutas = filedialog.askopenfilenames(
            title="Elija los PDF de formularios (puede marcar varios)",
            filetypes=[("Documentos PDF", "*.pdf")],
            parent=self,
        )
        if rutas:
            self._al_importar(list(rutas))
        return "break"

    def _importar_carpeta(self):
        """Elige una carpeta entera, con sus subcarpetas.

        La carpeta se pasa tal cual, sin listar aqui lo que hay dentro: quien
        expande una carpeta a la lista de PDF es `importacion.tanda.rutas_de_pdf`,
        que es donde esta escrito que se recorre en profundidad y que no se repite
        lo que ya venia suelto.
        """
        carpeta = filedialog.askdirectory(
            title="Elija la carpeta con los PDF (se buscará también en sus subcarpetas)",
            mustexist=True,
            parent=self,
        )
        if carpeta:
            self._al_importar([carpeta])
        return "break"

    # ---- el reloj --------------------------------------------------------

    def _vigilar_el_cambio_de_dia(self):
        """Repinta si cambio el dia, y vuelve a programarse.

        Un programa abierto desde el lunes que el jueves siga pintando el lunes es
        la averia silenciosa de esta pantalla: las cuentas atras dirian tres dias
        de mas y un caso vencido seguiria apareciendo como futuro.
        """
        if date.today() != self.hoy:
            self.refrescar()
        # Se guarda el identificador para poder cancelarlo al cambiar de pantalla.
        # Sin cancelarlo, el temporizador de una pantalla ya destruida sigue
        # disparando y Tk contesta «invalid command name ..._vigilar_el_cambio_de_dia»
        # por consola. No rompia nada —el metodo ya no existe y Tk se lo traga—,
        # pero es un temporizador vivo por cada vez que se entra y se sale de un
        # caso, y en una sesion larga son muchos.
        self._proxima_comprobacion = self.after(
            MILISEGUNDOS_ENTRE_COMPROBACIONES_DE_FECHA, self._vigilar_el_cambio_de_dia
        )

    # ---- repintado -------------------------------------------------------

    def refrescar(self, evento=None):
        """Relee la base con la fecha de HOY y repinta lo urgente.

        **Lo urgente se pinta ya; el equipo y los avisos, despues de pintar.** Lo
        pidio el dueno: *«Si algo de lo nuevo cuesta, que se cargue después de
        pintar»*. El bloque rojo y el mes son la razon de ser de esta pantalla y no
        pueden esperar; la franja de avisos, los cuatro contadores y el resumen del
        equipo son contexto y llegan un instante despues, con la ventana ya
        pintada y respondiendo.

        `after(0, ...)` no es un retraso: es «en cuanto termines de pintar lo que
        tienes entre manos». Sin esto, las **nueve** consultas de esos tres
        bloques —tres de avisos, cuatro de contadores y dos del equipo— se
        sumarian al tiempo que Miguel espera mirando una ventana en blanco.
        """
        self.hoy = date.today()
        self.menu.marcar("Panel")
        self._titulo.configure(text=f"Fichas — Inicio · {_fecha_larga(self.hoy)}")
        # La cola se lee UNA vez por refresco y se reparte. Antes se leia dos: una
        # para sacar los casos sin fecha del bloque de arriba y otra para pintar la
        # cola, y es la consulta mas cara de esta pantalla —recorre `casos` y
        # cuenta `procedencia_campo` por cada uno—. Medido con
        # `set_trace_callback`: dos ejecuciones identicas por cada apertura.
        self._pendientes = casos_pendientes_de_verificar(self.conexion)
        self._pintar_bloque_de_arriba()
        self._mes.poner_hoy(self.hoy)
        self._pintar_pendientes()
        # El identificador se guarda para poder cancelarlo al salir de la pantalla,
        # igual que la vigilancia del cambio de día. Sin cancelarlo, este disparaba
        # sobre una pantalla destruida y Tk contestaba «invalid command name
        # ..._pintar_lo_diferido» por consola durante la suite.
        if self._diferido is not None:
            self.after_cancel(self._diferido)
        self._diferido = self.after(0, self._pintar_lo_diferido)
        return "break"

    def _pintar_lo_diferido(self):
        """Lo que no es urgente, ya con la ventana pintada y respondiendo."""
        # Ya disparó: no queda nada que cancelar.
        self._diferido = None
        if not self.winfo_exists():
            return
        self._pintar_los_avisos()
        self._pintar_los_contadores()
        self._pintar_el_equipo()

    # ---- la franja de avisos --------------------------------------------

    def _pintar_los_avisos(self):
        """Los motivos, ya contados y agrupados por la capa de datos."""
        self.franja.poner(
            Aviso(
                texto, cuenta,
                al_ver=self._al_ver_ilegibles if "leer" in texto else None,
            )
            for texto, cuenta in motivos_de_aviso(self.conexion)
        )

    def _al_cambiar_los_avisos(self, cuantos, esta_cerrada):
        """Dibuja la campana cuando Miguel cierra la franja. Nada se pierde."""
        if esta_cerrada and cuantos:
            self._campana.configure(text=f"🔔 {cuantos}")
            self._campana.grid(row=0, column=9, padx=3)
        else:
            self._campana.grid_remove()

    def _reabrir_los_avisos(self):
        self.franja.reabrir()

    # ---- los cuatro contadores -------------------------------------------

    def _pintar_los_contadores(self):
        """Los cuatro numeros del mockup, cada uno con su unidad escrita.

        La unidad va en el rotulo y no se da por sabida: la fila mezcla personas
        con documentos —«23 personas por viajar» junto a «3 hojas sin devolver»— y
        un numero suelto entre otros tres se lee en la unidad del vecino.

        **Dibujados y no en controles.** Eran trece widgets —cuatro cajas con dos
        etiquetas cada una— que se destruían y se rehacían enteros en cada
        refresco, para enseñar cuatro números y cuatro rótulos que nadie pulsa.
        """
        numeros = contadores_del_panel(self.conexion, self.hoy)
        self._contadores.delete("all")
        ancho = max(self._contadores.winfo_width(), 1)
        paso = ancho / len(ROTULOS_DE_LOS_CONTADORES)
        for columna, (clave, rotulo) in enumerate(ROTULOS_DE_LOS_CONTADORES):
            izquierda = columna * paso
            numero = self._contadores.create_text(
                izquierda, ALTO_DE_LOS_CONTADORES // 2, anchor="w",
                text=str(numeros[clave]),
                font=LETRA_TITULO if clave in RESALTADOS else LETRA_SECCION,
                fill="#1A1D21",
            )
            self._contadores.create_text(
                self._contadores.bbox(numero)[2] + 6, ALTO_DE_LOS_CONTADORES // 2,
                anchor="w", text=rotulo, fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
                width=max(int(paso) - 40, 40),
            )

    # ---- el equipo -------------------------------------------------------

    def _pintar_el_equipo(self):
        """Un renglon por agente: lo que lleva, y el boton de «Generar paquete».

        El paquete no se arma aqui: lleva a «Asignar casos», que es donde ya esta
        hecho y probado el recorte de los PDF y la hoja «Por verificar». Una
        segunda copia de eso seria una segunda cosa que mantener.

        **El botón es uno para toda la lista, no uno por agente.** Los de antes
        llevaban todos al mismo sitio —la pantalla de asignación, que ya pregunta a
        quién—, así que eran tantos controles como agentes para una sola acción. El
        rótulo dice «Repartir…» cuando lo que hay pendiente es sin asignar, que es
        lo que decía el botón de esa fila.
        """
        agentes = resumen_del_equipo(self.conexion)
        self._lista_del_equipo.poner(
            self._renglon_del_agente(renglon) for renglon in agentes
        )
        hay_trabajo = any(renglon["cuantos_documentos"] for renglon in agentes)
        if self._al_asignar is None or not hay_trabajo:
            self._boton_del_equipo.grid_remove()
            return
        sin_repartir = any(
            renglon["companero_id"] is None and renglon["cuantos_documentos"]
            for renglon in agentes
        )
        self._boton_del_equipo.configure(
            text="Repartir…" if sin_repartir else "Generar paquete"
        )
        self._boton_del_equipo.grid(row=0, column=1, sticky="e")

    def _renglon_del_agente(self, renglon):
        """El renglon de un agente. «—» y no «0» para lo que no lleva nadie."""
        sin_volver = (
            "—" if renglon["sin_devolver"] is None
            else f"{renglon['sin_devolver']} sin volver"
        )
        return Renglon(
            (
                f"{renglon['nombre']} · {renglon['cuantos_documentos']} doc. · "
                f"{renglon['personas']} pers. · {sin_volver}",
            ),
            fondo=FONDO,
            colores=(TEXTO_SECUNDARIO,),
            con_borde=False,
        )

    def _ir_a_asignar(self):
        """El botón del equipo. Lleva a «Asignar casos», que es donde se reparte."""
        if self._al_asignar is not None:
            self._al_asignar()

    def _pintar_bloque_de_arriba(self):
        """El bloque rojo con sus tres niveles, o el verde si no hay nada."""
        for hijo in self._bloque_de_arriba.winfo_children():
            hijo.destroy()

        proximos = casos_que_viajan_pronto(self.conexion, self.hoy)
        en_riesgo = casos_en_riesgo(self.conexion, self.hoy)
        ids_en_riesgo = {caso["id"] for caso in en_riesgo}
        resueltos = [caso for caso in proximos if caso["id"] not in ids_en_riesgo]
        sin_fecha = self._casos_sin_fecha()

        if not proximos and not sin_fecha:
            self._pintar_vacio()
            return

        self._pintar_franja(proximos, en_riesgo)
        self._pintar_nivel(
            "Viajan pronto y NO están completos — atender primero",
            en_riesgo, ROJO_DE_FILA, ROJO_VENCIDO,
        )
        self._pintar_nivel(
            "Viajan pronto y están completos", resueltos, "#FFFFFF", VERDE_RESUELTO
        )
        self._pintar_nivel(
            "Sin fecha de viaje — no se puede saber si son urgentes",
            sin_fecha, AMBAR_DE_FILA, AMBAR_SIN_FECHA,
        )

    def _casos_sin_fecha(self):
        """Los pendientes sin `fecha_viaje`, que no caben en ninguna ventana.

        Un caso sin fecha no puede salir en «próximos 7 días» —no hay con qué
        compararlo— y si no sale aquí no sale en ninguna parte de la mitad de
        arriba. Un caso invisible es exactamente el que se pierde. Nunca se le
        inventa una fecha.
        """
        casos = []
        for caso in self._pendientes:
            if caso["fecha_viaje"] is None:
                caso = dict(caso)
                # `None` y no 0: sin fecha no hay cuenta atras que dar, y un 0
                # se leeria como «viaja hoy».
                caso["dias_para_el_viaje"] = None
                caso["ya_viajo"] = False
                casos.append(caso)
        return casos

    def _pintar_franja(self, proximos, en_riesgo):
        """La franja roja con las DOS cifras: cuántos casos y cuántos sin resolver."""
        personas = sum(caso["personas"] for caso in proximos)
        personas_en_riesgo = sum(caso["personas"] for caso in en_riesgo)
        franja = tk.Frame(self._bloque_de_arriba, background=ROJO_SOLIDO)
        franja.pack(fill="x")
        tk.Label(
            franja,
            text=f"Viajan en los próximos {DIAS_DE_LA_VENTANA} días",
            background=ROJO_SOLIDO, foreground="#FFFFFF", font=LETRA_SECCION,
            anchor="w", padx=10, pady=6,
        ).pack(side="left")
        tk.Label(
            franja,
            text=(
                f"{len(proximos)} casos · {personas} personas · "
                f"{len(en_riesgo)} casos con {personas_en_riesgo} personas sin resolver"
            ),
            background=ROJO_SOLIDO, foreground="#FFFFFF", font=LETRA_PEQUENA,
            anchor="e", padx=10, pady=6,
        ).pack(side="right")

    def _pintar_nivel(self, titulo, casos, fondo, texto):
        """Un nivel del bloque de arriba, con su subtítulo. Vacío no se dibuja.

        Solo se dibujan las primeras `FILAS_VISIBLES_POR_LISTA`; el resto se
        cuenta. El motivo esta medido arriba, en la constante.
        """
        if not casos:
            return
        ttk.Label(self._bloque_de_arriba, text=titulo, style="Secundario.TLabel").pack(
            anchor="w", pady=(6, 2)
        )
        for caso in casos[:FILAS_VISIBLES_POR_LISTA]:
            FilaDeCaso(self._bloque_de_arriba, caso, fondo, texto, self._al_abrir_caso).pack(
                fill="x", pady=1
            )
        self._decir_cuantos_faltan(self._bloque_de_arriba, len(casos))

    def _decir_cuantos_faltan(self, padre, cuantos):
        """«y N más» cuando la lista no cabe entera. Nunca se callan en silencio.

        Ocultar sin decirlo esconderia justo las listas mas cargadas, que son las
        que hay que mirar. Es la misma regla que ya seguia el calendario.
        """
        de_mas = cuantos - FILAS_VISIBLES_POR_LISTA
        if de_mas <= 0:
            return
        ttk.Label(
            padre,
            text=f"y {de_mas} más — están en su pantalla",
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(2, 0))

    def _pintar_vacio(self):
        """La frase en español de la base vacía. El rojo NO se dibuja vacío."""
        marco = tk.Frame(self._bloque_de_arriba, background="#E7F5EC")
        marco.pack(fill="x")
        tk.Label(
            marco,
            text=f"No hay viajes en los próximos {DIAS_DE_LA_VENTANA} días.",
            background="#E7F5EC", foreground="#12351F", font=LETRA_SECCION,
            anchor="w", padx=10, pady=8,
        ).pack(anchor="w")
        tk.Label(
            marco,
            text="Use «Importar PDF…» para añadir formularios.",
            background="#E7F5EC", foreground="#12351F", font=LETRA_PEQUENA,
            anchor="w", padx=10,
        ).pack(anchor="w", pady=(0, 8))

    # ---- pendientes ------------------------------------------------------

    def _pintar_pendientes(self):
        """La cola de trabajo, por fecha de viaje ascendente. Sin fecha, al final.

        **Las filas se dibujan en un lienzo, no son controles.** Con ocho filas de
        tres etiquetas cada una eran veinticuatro ventanas del escritorio de
        Windows para enseñar ocho renglones, y se creaban y destruían enteras cada
        vez que se volvía de un caso. El motivo medido está en
        `interfaz/lista_en_lienzo.py`.
        """
        pendientes = self._pendientes
        self._cuantos_pendientes.configure(
            text=(
                f"{len(pendientes)} casos · el que viaja antes, primero"
                if pendientes else "Nada pendiente."
            )
        )
        # Los últimos contactos se leen UNA vez para toda la lista y no una vez por
        # fila: con cincuenta pendientes serían cincuenta consultas para pintar una
        # pantalla que se repinta cada vez que se vuelve de un caso.
        ultimos = ultimo_contacto_por_caso(self.conexion, self.hoy)
        # El tope: con 300 pendientes esto creaba 904 widgets y era la mitad de los
        # siete segundos que tardaba en abrir. Medido, no supuesto.
        self._lista_de_pendientes.poner(
            self._renglon_del_pendiente(caso, ultimos.get(caso["id"]))
            for caso in pendientes[:FILAS_VISIBLES_POR_LISTA]
        )
        self._decir_cuantos_faltan_en_la_cola(len(pendientes))

    def _renglon_del_pendiente(self, caso, ultimo_contacto=None):
        """Una fila de la cola, con cuántos campos le faltan y cuándo se contactó.

        La línea del contacto va SIEMPRE, incluso cuando no hay ninguno: «sin
        contactar» es información, y una fila que se calla cuando nadie ha llamado
        no se distingue de una que se calla porque ya se llamó.
        """
        fondo = AMBAR_DE_FILA if caso["fecha_viaje"] is None else "#FFFFFF"
        texto_de_la_fecha = caso["fecha_viaje"] or "sin fecha"
        unidad = caso.get("unidad_nombre") or "unidad sin nombre"
        if caso["campos"]:
            cuerpo = (
                f"{texto_de_la_fecha}   {numero_de_caso_visible(caso)}   {unidad}   ·   "
                f"faltan {caso['campos_pendientes']} de {caso['campos']} campos"
                + marca_de_duplicado(caso)
            )
        else:
            cuerpo = (
                f"{texto_de_la_fecha}   {numero_de_caso_visible(caso)}   {unidad}"
                "   ·   sin ninguna lectura" + marca_de_duplicado(caso)
            )
        return Renglon(
            (cuerpo, resumen_del_ultimo_contacto(ultimo_contacto)),
            clave=caso,
            fondo=fondo,
            colores=(
                "#1A1D21",
                ROJO_VENCIDO if hay_que_volver_a_llamar(ultimo_contacto) else VERDE_RESUELTO,
            ),
        )

    def _decir_cuantos_faltan_en_la_cola(self, cuantos):
        """«y N más» al pie de la cola. La misma regla que en el bloque de arriba."""
        de_mas = cuantos - FILAS_VISIBLES_POR_LISTA
        self._faltan_en_la_cola.configure(
            text=f"y {de_mas} más — están en su pantalla" if de_mas > 0 else ""
        )

    def dar_el_foco_a_lo_mas_urgente(self):
        """Pone el foco en la primera fila del bloque de arriba, si la hay.

        Lo más urgente ya tiene el foco al abrir el programa: Intro nada más abrir
        lleva al caso que más corre, sin tocar el ratón.
        """
        for hijo in self._bloque_de_arriba.winfo_children():
            if isinstance(hijo, FilaDeCaso):
                hijo.focus_set()
                return
