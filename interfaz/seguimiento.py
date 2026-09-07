"""El panel de seguimiento de un caso: la recomendacion y los contactos.

Vive dentro de la pantalla de correccion, debajo de las personas, y junta las dos
cosas que se hacen sobre un caso DESPUES de leerlo:

  - **La recomendacion.** Lo que los companeros propusieron (FASE 6) y el valor que
    Miguel decide guardar. La propuesta se ensena; **no se copia sola**. Hay un
    boton «Usar esta» por propuesta, y hay que pulsarlo: eso es la regla permanente
    5 —el sistema propone, Miguel confirma— en el sitio donde mas se nota.
  - **Los contactos con el lider** (FASE 7), con su historial y los dias que han
    pasado. El programa no manda nada: registra lo que ocurrio por fuera.

⚠️ **La FASE 7 no tiene ni una decision del dueno.** `PENDIENTES.md` lo dice de si
misma y su criterio 4 exige una entrada en `DECISIONES.md` antes de cerrar. Lo que
se ve aqui sale del pase, no de una especificacion. Los campos concretos y por que
son esos estan en la cabecera de `datos/contactos.py`.
"""

from datetime import date

import tkinter as tk
from tkinter import ttk

from datos.companeros import companeros_activos
from datos.contactos import (
    MEDIOS_DE_CONTACTO,
    anular_contacto,
    contactos_del_caso,
    registrar_contacto,
)
from datos.estados import ESTADOS_RECOMENDACION
from datos.propuestas import propuestas_del_caso
from datos.validacion import ErrorDeValidacion
from espejo.escritura import guardar_y_regenerar
from interfaz import dialogos
from interfaz.tema import (
    AMBAR_DE_FILA,
    BORDE,
    LETRA_PEQUENA,
    ROJO_DE_FILA,
    ROJO_VENCIDO,
    VERDE_RESUELTO,
)

# Lo que se elige cuando nadie ha dicho nada todavia. No es un estado: es la
# ausencia de estado, y se escribe como NULL en la base. Tiene texto propio porque
# una casilla vacia en un desplegable se lee como «se me olvido rellenarlo».
SIN_DECIR = "(todavía nadie lo ha dicho)"

# A partir de cuantos dias un contacto sin respuesta deja de ser «ya se llamó» y
# pasa a ser «hay que volver a llamar». Siete: es la misma ventana que usa la
# pantalla de inicio para los viajes (`DIAS_DE_LA_VENTANA`), y usar dos numeros
# distintos para «pronto» en la misma aplicacion confunde sin ganar nada.
#
# ⚠️ Es un numero que elegí yo. No sale de ninguna decision del dueño.
DIAS_PARA_VOLVER_A_LLAMAR = 7


def guardar_en_la_base_y_en_el_espejo(conexion, operacion, carpeta_de_datos, ventana):
    """Escribe y reescribe el `.xlsx` detras. Devuelve lo que devolviera la escritura.

    Las dos escrituras de este archivo —registrar un contacto y anularlo— la usan
    en vez de llamar a `datos.contactos` a secas. `contactos` es una hoja del
    espejo, y la auditoria final de QA midio que estos dos caminos lo dejaban
    atrasado: «contactos base=1 espejo=0», contra lo que `espejo/escritura.py`
    promete de si mismo.

    Va como funcion de modulo y no como metodo porque las dos escrituras viven en
    clases distintas —el panel y la ventana del formulario— y un metodo obligaria
    a repetirlo en las dos o a heredar por una linea.
    """
    return guardar_y_regenerar(
        conexion,
        operacion,
        carpeta_de_datos=carpeta_de_datos,
        avisar=lambda aviso: dialogos.advertir(
            "El Excel espejo no se pudo actualizar", aviso, parent=ventana
        ),
    ).guardado


def texto_de_los_dias(dias):
    """«hoy», «ayer», «hace 10 días», «dentro de 3 días». Siempre con palabras."""
    if dias is None:
        return "sin fecha"
    if dias == 0:
        return "hoy"
    if dias == 1:
        return "ayer"
    if dias < 0:
        return f"dentro de {abs(dias)} día{'s' if abs(dias) != 1 else ''}"
    return f"hace {dias} días"


def palabra_de_la_respuesta(respondio):
    """Los tres estados de la respuesta, en palabras y nunca solo en color."""
    if respondio == 1:
        return "RESPONDIÓ"
    if respondio == 0:
        return "NO RESPONDIÓ"
    return "sin respuesta todavía"


def resumen_del_ultimo_contacto(contacto):
    """La linea que la pantalla de inicio pone en un caso ya contactado.

    Un caso que viaja en 4 dias y se contacto hace 10 sin respuesta necesita otra
    cosa que uno contactado ayer, y esta linea es la que lo dice: lleva SIEMPRE los
    dias y SIEMPRE la palabra de la respuesta, no una sola de las dos.
    """
    if contacto is None:
        return "sin contactar"
    partes = [f"contactado {texto_de_los_dias(contacto['dias_desde'])}"]
    if contacto.get("medio"):
        partes.append(contacto["medio"])
    partes.append(palabra_de_la_respuesta(contacto.get("respondio")))
    return " · ".join(partes)


def hay_que_volver_a_llamar(contacto):
    """Dice si ese ultimo contacto ya no sirve como gestion hecha.

    Es cierto cuando no hubo respuesta —o todavia no se sabe— y han pasado mas dias
    de la cuenta. Un contacto que SI respondio no caduca por tiempo: la gestion se
    hizo y alguien contesto.
    """
    if contacto is None:
        return True
    if contacto.get("respondio") == 1:
        return False
    dias = contacto.get("dias_desde")
    return dias is not None and dias >= DIAS_PARA_VOLVER_A_LLAMAR


class PanelDeSeguimiento(ttk.Frame):
    """La recomendacion y los contactos de un caso, debajo de sus personas."""

    def __init__(self, padre, conexion, caso, al_cambiar=None, carpeta_de_datos=None):
        super().__init__(padre)
        self.conexion = conexion
        self.caso = caso
        self._al_cambiar = al_cambiar
        self._carpeta_de_datos = carpeta_de_datos
        self.columnconfigure(0, weight=1)

        self._construir_recomendacion()
        self._marco_de_propuestas = ttk.Frame(self)
        self._marco_de_propuestas.grid(row=2, column=0, sticky="ew")
        self._construir_contactos()
        self.refrescar()

    # ---- la recomendacion ------------------------------------------------

    def _construir_recomendacion(self):
        ttk.Label(self, text="Recomendación", style="Seccion.TLabel").grid(
            row=0, column=0, sticky="w", pady=(14, 4)
        )
        marco = ttk.Frame(self)
        marco.grid(row=1, column=0, sticky="ew")
        ttk.Label(marco, text="Estado:").grid(row=0, column=0, sticky="w")
        # Las opciones salen de `datos/estados.py`, en un solo sitio: el dia que el
        # dueno diga los valores que faltan (`DECISIONES.md`, P-1) es tocar esa
        # tupla y ninguna linea de aqui.
        #
        # ⚠️ **Desde el 2026-09-03 esa tupla incluye `completa` y `no_completa`**, y
        # elegir uno de los dos aqui YA NO deja el documento marcado sin firma:
        # quien guarda —`interfaz/correccion.py`,
        # `_firmar_el_estado_si_es_de_los_dos_que_marcan`— lo escribe con
        # `marcar_a_mano`, que deja quien, cuando y de donde. Se decidio firmarlos en
        # vez de quitarlos del desplegable: este desplegable es la mano de Miguel, y
        # quitarselos le obligaria a salir a otra pantalla para decir lo que ya sabe.
        # La firma NO se hace aqui porque este panel no escribe en la base: devuelve
        # lo elegido y quien guarda guarda.
        self._estado = ttk.Combobox(
            marco, state="readonly", width=30,
            values=[SIN_DECIR] + list(ESTADOS_RECOMENDACION),
        )
        self._estado.set(self.caso.get("estado_recomendacion") or SIN_DECIR)
        self._estado.grid(row=0, column=1, sticky="w", padx=8)
        self._estado.bind("<<ComboboxSelected>>", lambda evento: self._avisar())
        ttk.Label(
            marco,
            text="Se guarda con el resto del caso, al pulsar «Guardar».",
            style="Secundario.TLabel",
        ).grid(row=0, column=2, sticky="w", padx=8)

    def valor_del_estado(self):
        """El estado elegido, o None si nadie lo ha dicho. Lo lee quien guarda."""
        elegido = self._estado.get()
        return None if elegido in ("", SIN_DECIR) else elegido

    def _avisar(self):
        """Le dice a la pantalla que hay un cambio sin guardar."""
        if self._al_cambiar is not None:
            self._al_cambiar()

    def _usar_la_propuesta(self, estado):
        """Copia una propuesta al desplegable. NO la guarda: eso lo hace Miguel."""
        self._estado.set(estado)
        self._avisar()

    def _pintar_propuestas(self):
        for hijo in self._marco_de_propuestas.winfo_children():
            hijo.destroy()

        propuestas = propuestas_del_caso(self.conexion, self.caso["id"])
        if not propuestas:
            return
        ttk.Label(
            self._marco_de_propuestas,
            text="Lo que propusieron los compañeros — nada de esto está confirmado",
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(6, 2))

        for propuesta in propuestas:
            fila = tk.Frame(
                self._marco_de_propuestas, background=AMBAR_DE_FILA,
                highlightthickness=1, highlightbackground=BORDE,
            )
            fila.pack(fill="x", pady=1)
            quien = propuesta["nombre_del_companero"] or "un compañero borrado"
            estado = propuesta["estado_propuesto"] or "sin estado"
            texto = (
                f"{propuesta['nombre'] or 'sin nombre'}  ·  {propuesta['mrn'] or 'sin MRN'}"
                f"   →   {estado}"
            )
            if propuesta["nota_companero"]:
                texto += f"\n«{propuesta['nota_companero']}»"
            texto += f"\n{quien}, el {propuesta['propuesto_en']}"
            tk.Label(
                fila, text=texto, background=AMBAR_DE_FILA, anchor="w", justify="left",
                padx=8, pady=5, font=LETRA_PEQUENA, takefocus=0,
            ).pack(side="left")
            if propuesta["estado_propuesto"]:
                ttk.Button(
                    fila, text="Usar esta",
                    command=lambda e=propuesta["estado_propuesto"]: self._usar_la_propuesta(e),
                ).pack(side="right", padx=4, pady=3)

    # ---- los contactos ---------------------------------------------------

    def _construir_contactos(self):
        cabecera = ttk.Frame(self)
        cabecera.grid(row=3, column=0, sticky="ew", pady=(14, 4))
        cabecera.columnconfigure(0, weight=1)
        ttk.Label(cabecera, text="Contactos con el líder", style="Seccion.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        ttk.Button(
            cabecera, text="Registrar contacto…", command=self._registrar
        ).grid(row=0, column=1, sticky="e")
        self._marco_de_contactos = ttk.Frame(self)
        self._marco_de_contactos.grid(row=4, column=0, sticky="ew")

    def _pintar_contactos(self):
        for hijo in self._marco_de_contactos.winfo_children():
            hijo.destroy()

        hoy = date.today()
        historial = contactos_del_caso(self.conexion, self.caso["id"], hoy=hoy)
        if not historial:
            # Criterio 2 de la FASE 7: una frase en espanol, no una lista vacia muda.
            ttk.Label(
                self._marco_de_contactos,
                text=(
                    "Todavía no se ha registrado ningún contacto con el líder sobre "
                    "este caso. El programa no manda mensajes: aquí se anota una "
                    "llamada, un WhatsApp o una visita que ya ocurrió."
                ),
                style="Secundario.TLabel",
                wraplength=700,
                justify="left",
            ).pack(anchor="w")
            return

        vigentes = [contacto for contacto in historial if not contacto["anulado"]]
        ttk.Label(
            self._marco_de_contactos,
            text=(
                f"{len(vigentes)} contacto(s) vigente(s) de {len(historial)} "
                "registrado(s). El más reciente, arriba."
            ),
            style="Secundario.TLabel",
        ).pack(anchor="w", pady=(0, 4))
        for contacto in historial:
            self._pintar_contacto(contacto)

    def _pintar_contacto(self, contacto):
        """Una fila del historial, con sus dias, su medio y su resultado."""
        anulado = bool(contacto["anulado"])
        caduco = not anulado and hay_que_volver_a_llamar(contacto)
        fondo = ROJO_DE_FILA if caduco else ("#EFEFF1" if anulado else "#FFFFFF")
        color = ROJO_VENCIDO if caduco else ("#5A5F66" if anulado else VERDE_RESUELTO)

        fila = tk.Frame(
            self._marco_de_contactos, background=fondo, highlightthickness=1,
            highlightbackground=BORDE,
        )
        fila.pack(fill="x", pady=1)

        encabezado = (
            f"{contacto['fecha']}  ·  {texto_de_los_dias(contacto['dias_desde'])}"
            f"  ·  {contacto['medio'] or 'sin medio'}"
            f"  ·  {palabra_de_la_respuesta(contacto['respondio'])}"
        )
        if anulado:
            encabezado = "ANULADO — " + encabezado
        tk.Label(
            fila, text=encabezado, background=fondo, foreground=color, anchor="w",
            # `pady` de un `tk.Label` es UN número, no una pareja: con `(5, 0)` Tk
            # contesta «expected screen distance» y la ficha entera no se dibuja.
            # El margen asimétrico se pide en el `pack`, que sí admite la pareja.
            padx=8, pady=0, font=LETRA_PEQUENA, takefocus=0,
        ).pack(fill="x", pady=(5, 0))

        detalle = []
        if contacto["con_quien"]:
            detalle.append(f"con {contacto['con_quien']}")
        if contacto["nombre_del_companero"]:
            detalle.append(f"lo hizo {contacto['nombre_del_companero']}")
        if contacto["resultado"]:
            detalle.append(f"«{contacto['resultado']}»")
        if anulado:
            detalle.append(
                f"anulado el {contacto['anulado_en']}: {contacto['motivo_anulacion']}"
            )
        if detalle:
            tk.Label(
                fila, text="   ·   ".join(detalle), background=fondo, anchor="w",
                padx=8, pady=0, font=LETRA_PEQUENA, wraplength=680,
                justify="left", takefocus=0,
            ).pack(fill="x", pady=(0, 5))

        if not anulado:
            ttk.Button(
                fila, text="Anular", command=lambda c=contacto: self._anular(c)
            ).pack(side="right", padx=4, pady=3)

    def _registrar(self):
        ventana = VentanaDeContacto(
            self.winfo_toplevel(),
            self.conexion,
            self.caso,
            carpeta_de_datos=self._carpeta_de_datos,
        )
        self.winfo_toplevel().wait_window(ventana)
        if ventana.guardado:
            self.refrescar()

    def _anular(self, contacto):
        """Pide el motivo. Sin motivo no se anula: lo exige el `CHECK` del esquema."""
        motivo = _pedir_texto(
            self.winfo_toplevel(),
            "Anular el contacto",
            "El contacto NO se borra: queda visible marcado como anulado, con este "
            "motivo y la fecha de hoy.\n\n¿Por qué se anula?",
        )
        if not motivo:
            return
        try:
            guardar_en_la_base_y_en_el_espejo(
                self.conexion,
                lambda: anular_contacto(self.conexion, contacto["id"], motivo),
                self._carpeta_de_datos,
                self,
            )
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo anular", str(causa), parent=self)
            return
        self.refrescar()

    # ---- repintado -------------------------------------------------------

    def refrescar(self):
        """Relee las propuestas y el historial de contactos."""
        self._pintar_propuestas()
        self._pintar_contactos()


def _pedir_texto(padre, titulo, pregunta):
    """Una caja de texto de una linea, en espanol. Devuelve el texto o None.

    No se usa `tkinter.simpledialog` porque sus botones salen en el idioma de Tk
    —«OK» y «Cancel»—, y la regla permanente 4 dice que todo lo que se ve esta en
    espanol, sin excepcion para los botones de una biblioteca.
    """
    ventana = tk.Toplevel(padre)
    ventana.title(titulo)
    ventana.transient(padre)
    ventana.resizable(False, False)
    respuesta = {"texto": None}

    ttk.Label(ventana, text=pregunta, wraplength=420, justify="left", padding=12).pack(
        anchor="w"
    )
    entrada = ttk.Entry(ventana, width=60)
    entrada.pack(fill="x", padx=12)
    entrada.focus_set()

    def aceptar(evento=None):
        respuesta["texto"] = entrada.get().strip() or None
        ventana.destroy()

    botones = ttk.Frame(ventana, padding=12)
    botones.pack(fill="x")
    ttk.Button(botones, text="Cancelar", command=ventana.destroy).pack(side="right")
    ttk.Button(botones, text="Aceptar", command=aceptar).pack(side="right", padx=6)
    entrada.bind("<Return>", aceptar)
    ventana.bind("<Escape>", lambda evento: ventana.destroy())
    ventana.grab_set()
    padre.wait_window(ventana)
    return respuesta["texto"]


class VentanaDeContacto(tk.Toplevel):
    """El formulario de un contacto: cuándo, por qué medio, con quién y qué resultó."""

    def __init__(self, padre, conexion, caso, carpeta_de_datos=None):
        super().__init__(padre)
        self.conexion = conexion
        self.caso = caso
        self._carpeta_de_datos = carpeta_de_datos
        self.guardado = False

        self.title(f"Registrar contacto — {caso['numero_caso']}")
        self.transient(padre)
        self.resizable(False, False)
        self._companeros = companeros_activos(conexion)
        self._construir()
        self.bind("<Escape>", lambda evento: self.destroy())
        self.grab_set()

    def _construir(self):
        marco = ttk.Frame(self, padding=12)
        marco.pack(fill="both", expand=True)
        marco.columnconfigure(1, weight=1)

        ttk.Label(
            marco,
            text=(
                "El programa no manda nada: aquí se anota un contacto que ya "
                "ocurrió por fuera."
            ),
            style="Secundario.TLabel",
            wraplength=460,
            justify="left",
        ).grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, 8))

        ttk.Label(marco, text="Fecha (AAAA-MM-DD):").grid(row=1, column=0, sticky="w", pady=3)
        self._fecha = ttk.Entry(marco, width=18)
        # Se propone HOY porque es la respuesta correcta casi siempre, y se deja
        # editable porque «llamé el viernes y lo apunto el lunes» pasa. No se
        # bloquea: eso obligaría a mentir con la fecha.
        self._fecha.insert(0, date.today().isoformat())
        self._fecha.grid(row=1, column=1, sticky="w", padx=8)

        ttk.Label(marco, text="Medio:").grid(row=2, column=0, sticky="w", pady=3)
        self._medio = ttk.Combobox(
            marco, state="readonly", width=18, values=list(MEDIOS_DE_CONTACTO)
        )
        self._medio.current(0)
        self._medio.grid(row=2, column=1, sticky="w", padx=8)

        ttk.Label(marco, text="Con quién se habló:").grid(row=3, column=0, sticky="w", pady=3)
        self._con_quien = ttk.Entry(marco, width=44)
        self._con_quien.grid(row=3, column=1, sticky="ew", padx=8)

        ttk.Label(marco, text="Quién contactó:").grid(row=4, column=0, sticky="w", pady=3)
        self._quien = ttk.Combobox(
            marco, state="readonly", width=30,
            values=["(no se dice)"] + [c["nombre"] for c in self._companeros],
        )
        self._quien.current(0)
        self._quien.grid(row=4, column=1, sticky="w", padx=8)

        ttk.Label(marco, text="¿Respondió?").grid(row=5, column=0, sticky="w", pady=3)
        self._respondio = ttk.Combobox(
            marco, state="readonly", width=30,
            values=["todavía no se sabe", "sí, respondió", "no respondió"],
        )
        self._respondio.current(0)
        self._respondio.grid(row=5, column=1, sticky="w", padx=8)

        ttk.Label(marco, text="Resultado:").grid(row=6, column=0, sticky="nw", pady=3)
        self._resultado = tk.Text(marco, width=44, height=4, wrap="word")
        self._resultado.grid(row=6, column=1, sticky="ew", padx=8)

        botones = ttk.Frame(marco)
        botones.grid(row=7, column=0, columnspan=2, sticky="e", pady=(10, 0))
        ttk.Button(botones, text="Cancelar  Esc", command=self.destroy).pack(side="right")
        ttk.Button(botones, text="Guardar", command=self._guardar).pack(side="right", padx=6)
        self._fecha.focus_set()

    def _companero_elegido(self):
        """El id del companero que contacto, o None. Se busca por posicion."""
        indice = self._quien.current()
        if indice <= 0:
            return None
        return self._companeros[indice - 1]["id"]

    def _valor_de_respondio(self):
        """La respuesta como 1, 0 o None, en el orden del desplegable."""
        return (None, 1, 0)[self._respondio.current()]

    def _guardar(self):
        try:
            guardar_en_la_base_y_en_el_espejo(
                self.conexion,
                lambda: registrar_contacto(
                    self.conexion,
                    self.caso["id"],
                    self._fecha.get().strip(),
                    medio=self._medio.get(),
                    con_quien=self._con_quien.get(),
                    resultado=self._resultado.get("1.0", "end"),
                    contactado_por=self._companero_elegido(),
                    respondio=self._valor_de_respondio(),
                ),
                self._carpeta_de_datos,
                self,
            )
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo registrar", str(causa), parent=self)
            return
        self.guardado = True
        self.destroy()
